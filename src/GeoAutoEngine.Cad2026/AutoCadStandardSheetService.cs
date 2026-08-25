using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace GeoAutoEngine.Cad2026;

public sealed record AutoCadStandardSheetOptions
{
    public AutoCadStandardSheetOptions(string templatePath, int scaleDenominator, bool enabled = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(scaleDenominator, 1);
        TemplatePath = Path.GetFullPath(templatePath);
        ScaleDenominator = scaleDenominator;
        Enabled = enabled;
    }

    public string TemplatePath { get; }

    public int ScaleDenominator { get; }

    public bool Enabled { get; }
}

public sealed record AutoCadStandardSheetResult(bool Created, bool ScaleUpdated, string? LayoutName)
{
    internal bool Changed => Created || ScaleUpdated;
}

internal sealed record AutoCadStandardSheetInspection(
    IReadOnlyList<string> PaperLayouts,
    string? MarkedLayoutName,
    IReadOnlyList<string> ScaleTexts);

public static class AutoCadStandardSheetService
{
    private static readonly Regex ScalePattern = new(
        @"\bH\s*=\s*1\s*:\s*\d+\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private const string MarkerKey = "GEOAUTOENGINE_STANDARD_SHEET";

    public static AutoCadStandardSheetResult Ensure(
        Document document,
        AutoCadStandardSheetOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            return new AutoCadStandardSheetResult(false, false, null);
        }

        if (!File.Exists(options.TemplatePath))
        {
            throw new FileNotFoundException("O template oficial da folha padrão não foi encontrado.", options.TemplatePath);
        }

        using var sourceDatabase = new Database(false, true);
        sourceDatabase.ReadDwgFile(
            options.TemplatePath,
            FileOpenMode.OpenForReadAndAllShare,
            allowCPConversion: true,
            password: string.Empty);
        string sourceLayoutName;
        using (var sourceTransaction = sourceDatabase.TransactionManager.StartTransaction())
        {
            sourceLayoutName = SelectSourceLayout(sourceDatabase, sourceTransaction).LayoutName;
            sourceTransaction.Abort();
        }

        using (var transaction = document.Database.TransactionManager.StartTransaction())
        {
            var layouts = (DBDictionary)transaction.GetObject(
                document.Database.LayoutDictionaryId,
                OpenMode.ForRead);
            var markedLayoutName = ReadMarkedLayoutName(document.Database, transaction);
            if (!string.IsNullOrWhiteSpace(markedLayoutName) && layouts.Contains(markedLayoutName))
            {
                var layout = (Layout)transaction.GetObject(layouts.GetAt(markedLayoutName), OpenMode.ForRead);
                var updated = UpdateScale(layout.BlockTableRecordId, transaction, options.ScaleDenominator);
                if (!updated)
                {
                    throw new InvalidOperationException(
                        $"O campo de escala H=1:... não foi encontrado no Layout '{markedLayoutName}'.");
                }

                transaction.Commit();
                document.Editor.Regen();
                return new AutoCadStandardSheetResult(false, true, markedLayoutName);
            }

            if (layouts.Contains(sourceLayoutName))
            {
                var existing = (Layout)transaction.GetObject(layouts.GetAt(sourceLayoutName), OpenMode.ForRead);
                if (!UpdateScale(existing.BlockTableRecordId, transaction, options.ScaleDenominator))
                {
                    throw new InvalidOperationException(
                        $"Já existe um Layout chamado '{sourceLayoutName}', mas ele não é a folha padrão esperada.");
                }

                WriteMarker(document.Database, transaction, sourceLayoutName);
                transaction.Commit();
                document.Editor.Regen();
                return new AutoCadStandardSheetResult(false, true, sourceLayoutName);
            }

            transaction.Abort();
        }

        var currentLayout = LayoutManager.Current.CurrentLayout;
        document.Editor.Command(
            "_.-LAYOUT",
            "_Template",
            options.TemplatePath,
            sourceLayoutName);
        using var importedTransaction = document.Database.TransactionManager.StartTransaction();
        var importedLayouts = (DBDictionary)importedTransaction.GetObject(
            document.Database.LayoutDictionaryId,
            OpenMode.ForRead);
        if (!importedLayouts.Contains(sourceLayoutName))
        {
            throw new InvalidOperationException(
                $"O AutoCAD não importou o Layout '{sourceLayoutName}' do template oficial.");
        }

        var importedLayout = (Layout)importedTransaction.GetObject(
            importedLayouts.GetAt(sourceLayoutName),
            OpenMode.ForRead);
        if (!UpdateScale(importedLayout.BlockTableRecordId, importedTransaction, options.ScaleDenominator))
        {
            throw new InvalidOperationException(
                $"O campo de escala H=1:... não foi encontrado no Layout importado '{sourceLayoutName}'.");
        }

        WriteMarker(document.Database, importedTransaction, sourceLayoutName);
        importedTransaction.Commit();
        if (!string.Equals(LayoutManager.Current.CurrentLayout, currentLayout, StringComparison.OrdinalIgnoreCase))
        {
            LayoutManager.Current.CurrentLayout = currentLayout;
        }
        document.Editor.Regen();
        return new AutoCadStandardSheetResult(true, true, sourceLayoutName);
    }

    internal static AutoCadStandardSheetInspection Inspect(Database database, Transaction transaction)
    {
        var dictionary = (DBDictionary)transaction.GetObject(database.LayoutDictionaryId, OpenMode.ForRead);
        var names = dictionary.Cast<System.Collections.DictionaryEntry>()
            .Select(entry => (Layout)transaction.GetObject(GetDictionaryObjectId(entry), OpenMode.ForRead))
            .Where(layout => !layout.ModelType)
            .OrderBy(layout => layout.TabOrder)
            .Select(layout => layout.LayoutName)
            .ToArray();
        var marked = ReadMarkedLayoutName(database, transaction);
        var scales = new List<string>();
        if (!string.IsNullOrWhiteSpace(marked) && dictionary.Contains(marked))
        {
            var layout = (Layout)transaction.GetObject(dictionary.GetAt(marked), OpenMode.ForRead);
            VisitTextObjects(layout.BlockTableRecordId, transaction, update: null, matchedTexts: scales);
        }

        return new AutoCadStandardSheetInspection(names, marked, scales.AsReadOnly());
    }

    private static Layout SelectSourceLayout(Database database, Transaction transaction)
    {
        var layouts = (DBDictionary)transaction.GetObject(database.LayoutDictionaryId, OpenMode.ForRead);
        var candidates = layouts.Cast<System.Collections.DictionaryEntry>()
            .Select(entry => (Layout)transaction.GetObject(GetDictionaryObjectId(entry), OpenMode.ForRead))
            .Where(layout => !layout.ModelType)
            .Select(layout => new
            {
                Layout = layout,
                IsPlan = layout.LayoutName.Contains("PLANTA", StringComparison.OrdinalIgnoreCase),
            })
            .OrderByDescending(item => item.IsPlan)
            .ThenBy(item => item.Layout.TabOrder)
            .ToArray();
        var selected = candidates.FirstOrDefault()?.Layout;
        return selected ?? throw new InvalidOperationException(
            "O template oficial não possui Layout de Paper Space.");
    }

    private static ObjectId GetDictionaryObjectId(System.Collections.DictionaryEntry entry) =>
        entry.Value is ObjectId objectId
            ? objectId
            : throw new InvalidOperationException("Entrada inválida no dicionário de Layouts do AutoCAD.");

    private static bool UpdateScale(
        ObjectId blockTableRecordId,
        Transaction transaction,
        int denominator) =>
        VisitTextObjects(blockTableRecordId, transaction, text =>
        {
            var replacement = $"H=1:{denominator}";
            return ScalePattern.Replace(text, replacement);
        }) > 0;

    private static int VisitTextObjects(
        ObjectId blockTableRecordId,
        Transaction transaction,
        Func<string, string>? update,
        HashSet<ObjectId>? visitedDefinitions = null,
        ICollection<string>? matchedTexts = null)
    {
        visitedDefinitions ??= [];
        if (!visitedDefinitions.Add(blockTableRecordId))
        {
            return 0;
        }

        var matches = 0;
        var record = (BlockTableRecord)transaction.GetObject(blockTableRecordId, OpenMode.ForRead);
        foreach (ObjectId objectId in record)
        {
            var item = transaction.GetObject(objectId, OpenMode.ForRead, openErased: false);
            matches += MatchAndUpdate(item, update, matchedTexts);
            if (item is not BlockReference block)
            {
                continue;
            }

            foreach (ObjectId attributeId in block.AttributeCollection)
            {
                matches += MatchAndUpdate(
                    transaction.GetObject(attributeId, OpenMode.ForRead, openErased: false),
                    update,
                    matchedTexts);
            }

            matches += VisitTextObjects(
                block.BlockTableRecord,
                transaction,
                update,
                visitedDefinitions,
                matchedTexts);
        }

        return matches;
    }

    private static int MatchAndUpdate(
        DBObject item,
        Func<string, string>? update,
        ICollection<string>? matchedTexts)
    {
        var current = item switch
        {
            AttributeReference attribute => attribute.TextString,
            AttributeDefinition attribute => attribute.TextString,
            DBText text => text.TextString,
            MText text => text.Contents,
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(current) || !ScalePattern.IsMatch(current))
        {
            return 0;
        }

        matchedTexts?.Add(ScalePattern.Match(current).Value);

        if (update is null)
        {
            return 1;
        }

        var updated = update(current!);
        item.UpgradeOpen();
        switch (item)
        {
            case AttributeReference attribute:
                attribute.TextString = updated;
                break;
            case AttributeDefinition attribute:
                attribute.TextString = updated;
                break;
            case DBText text:
                text.TextString = updated;
                break;
            case MText text:
                text.Contents = updated;
                break;
        }

        return 1;
    }

    private static string? ReadMarkedLayoutName(Database database, Transaction transaction)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (!namedObjects.Contains(MarkerKey))
        {
            return null;
        }

        var record = (Xrecord)transaction.GetObject(namedObjects.GetAt(MarkerKey), OpenMode.ForRead);
        using var data = record.Data;
        return data?.AsArray()
            .Where(value => value.TypeCode == (int)DxfCode.Text)
            .Select(value => Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture))
            .FirstOrDefault();
    }

    private static void WriteMarker(Database database, Transaction transaction, string layoutName)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForWrite);
        using var data = new ResultBuffer(new TypedValue((int)DxfCode.Text, layoutName));
        if (namedObjects.Contains(MarkerKey))
        {
            var existing = (Xrecord)transaction.GetObject(namedObjects.GetAt(MarkerKey), OpenMode.ForWrite);
            existing.Data = data;
            return;
        }

        using var marker = new Xrecord { Data = data };
        namedObjects.SetAt(MarkerKey, marker);
        transaction.AddNewlyCreatedDBObject(marker, add: true);
    }

}
