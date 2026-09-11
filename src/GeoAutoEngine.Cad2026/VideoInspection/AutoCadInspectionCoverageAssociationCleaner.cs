using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace GeoAutoEngine.Cad2026.VideoInspection;

public sealed class AutoCadInspectionCoverageAssociationCleaner
{
    private const string DictionaryName = "GEOAUTOENGINE_VIDEO_INSPECTION_COVERAGE";

    public void Clear(Document document, Guid reportId, Guid sectionId)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var namedObjects = (DBDictionary)transaction.GetObject(
            document.Database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(DictionaryName))
        {
            transaction.Commit();
            return;
        }

        var dictionary = (DBDictionary)transaction.GetObject(
            namedObjects.GetAt(DictionaryName),
            OpenMode.ForWrite);
        var key = EntryKey(reportId, sectionId);
        if (!dictionary.Contains(key))
        {
            transaction.Commit();
            return;
        }

        var recordId = dictionary.GetAt(key);
        var record = (Xrecord)transaction.GetObject(recordId, OpenMode.ForWrite);
        var values = ReadValues(record.Data);
        EraseOverlay(document.Database, transaction, Value(values, "INSPECTED_OVERLAY_HANDLE"));
        EraseOverlay(document.Database, transaction, Value(values, "NOT_INSPECTED_OVERLAY_HANDLE"));

        // O segmento criado pelo BREAK não é apagado nem unido aqui. Remover a
        // associação nunca deve alterar novamente a geometria física da rede.
        dictionary.Remove(key);
        record.Erase();
        transaction.Commit();
        document.Editor.Regen();
        document.Editor.UpdateScreen();
    }

    private static void EraseOverlay(
        Database database,
        Transaction transaction,
        string? handleValue)
    {
        if (string.IsNullOrWhiteSpace(handleValue))
        {
            return;
        }

        try
        {
            var handle = new Handle(Convert.ToInt64(handleValue, 16));
            var objectId = database.GetObjectId(false, handle, 0);
            if (objectId.IsNull || !objectId.IsValid || objectId.IsErased)
            {
                return;
            }

            if (transaction.GetObject(objectId, OpenMode.ForWrite, openErased: false) is Entity entity)
            {
                entity.Erase();
            }
        }
        catch (System.Exception exception) when (
            exception is Autodesk.AutoCAD.Runtime.Exception or FormatException or OverflowException)
        {
            // A limpeza da associação não deve falhar porque uma linha visual já
            // foi removida manualmente do desenho.
        }
    }

    private static IReadOnlyDictionary<string, string> ReadValues(ResultBuffer? data)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (data is null)
        {
            return result;
        }

        foreach (var value in data.AsArray())
        {
            var text = Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture);
            var separator = text?.IndexOf('=') ?? -1;
            if (separator > 0)
            {
                result[text!.Substring(0, separator)] = text.Substring(separator + 1);
            }
        }

        return result;
    }

    private static string? Value(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static string EntryKey(Guid reportId, Guid sectionId) =>
        $"R_{reportId:N}_S_{sectionId:N}";
}
