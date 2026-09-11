using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace GeoAutoEngine.Cad2026.VideoInspection;

/// <summary>
/// Prepara uma nova geração de cobertura sem alterar antecipadamente a rede.
///
/// A geração anterior apagava a linha interrompida antes de a nova transação de
/// cobertura ter sucesso. Se a cobertura seguinte falhasse por causa de um estado
/// antigo de associação, o desenho ficava sem a linha anterior e sem a nova
/// cobertura. Agora este passo só descarta estado VISUAL obsoleto quando o próprio
/// trecho foi reassociado para outro Handle. BREAKs e segmentos físicos nunca são
/// apagados aqui.
/// </summary>
public sealed class AutoCadInspectionInterruptedCoverageHandoff
{
    private const string AssociationDictionaryName = "GEOAUTOENGINE_VIDEO_INSPECTION_ASSOCIATIONS";
    private const string CoverageDictionaryName = "GEOAUTOENGINE_VIDEO_INSPECTION_COVERAGE";

    public void Prepare(Document document, Guid reportId, string geometryHandle)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(geometryHandle))
        {
            return;
        }

        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();

        var sectionIds = FindSectionsUsingGeometry(
            document.Database,
            transaction,
            reportId,
            geometryHandle);
        if (sectionIds.Count == 0)
        {
            transaction.Commit();
            return;
        }

        var changed = false;
        foreach (var sectionId in sectionIds)
        {
            changed |= ClearStaleCoverageState(
                document.Database,
                transaction,
                reportId,
                sectionId,
                geometryHandle);
        }

        transaction.Commit();
        if (changed)
        {
            document.Editor.Regen();
            document.Editor.UpdateScreen();
        }
    }

    private static IReadOnlyList<Guid> FindSectionsUsingGeometry(
        Database database,
        Transaction transaction,
        Guid reportId,
        string geometryHandle)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(AssociationDictionaryName))
        {
            return Array.Empty<Guid>();
        }

        var dictionary = (DBDictionary)transaction.GetObject(
            namedObjects.GetAt(AssociationDictionaryName),
            OpenMode.ForRead);
        var result = new List<Guid>();
        foreach (DBDictionaryEntry entry in dictionary)
        {
            var record = (Xrecord)transaction.GetObject(entry.Value, OpenMode.ForRead);
            var values = ReadValues(record.Data);
            if (!values.TryGetValue("REPORT_ID", out var reportValue) ||
                !Guid.TryParse(reportValue, out var storedReportId) ||
                storedReportId != reportId ||
                !values.TryGetValue("SECTION_ID", out var sectionValue) ||
                !Guid.TryParse(sectionValue, out var sectionId) ||
                !string.Equals(
                    Value(values, "GEOMETRY_HANDLE"),
                    geometryHandle,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(sectionId);
        }

        return result;
    }

    private static bool ClearStaleCoverageState(
        Database database,
        Transaction transaction,
        Guid reportId,
        Guid sectionId,
        string currentGeometryHandle)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(CoverageDictionaryName))
        {
            return false;
        }

        var dictionary = (DBDictionary)transaction.GetObject(
            namedObjects.GetAt(CoverageDictionaryName),
            OpenMode.ForWrite);
        var key = EntryKey(reportId, sectionId);
        if (!dictionary.Contains(key))
        {
            return false;
        }

        var recordId = dictionary.GetAt(key);
        var record = (Xrecord)transaction.GetObject(recordId, OpenMode.ForWrite);
        var values = ReadValues(record.Data);
        var storedOriginalHandle = Value(values, "ORIGINAL_HANDLE");

        // Se o trecho continua associado à mesma geometria, preserve o estado. Ele é
        // necessário para regenerar a cobertura já quebrada de forma idempotente.
        if (string.Equals(
                storedOriginalHandle,
                currentGeometryHandle,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Reassociação: apague somente as duas linhas VISUAIS da cobertura antiga.
        // O REMAINDER_HANDLE é um segmento físico criado pelo BREAK e deve continuar
        // existindo no desenho.
        EraseOverlay(database, transaction, Value(values, "INSPECTED_OVERLAY_HANDLE"));
        EraseOverlay(database, transaction, Value(values, "NOT_INSPECTED_OVERLAY_HANDLE"));

        dictionary.Remove(key);
        record.Erase();
        return true;
    }

    private static void EraseOverlay(
        Database database,
        Transaction transaction,
        string? handle)
    {
        var overlay = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
            database,
            transaction,
            handle,
            OpenMode.ForWrite);
        if (overlay is not null && !overlay.IsErased)
        {
            overlay.Erase();
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
            var text = Convert.ToString(
                value.Value,
                System.Globalization.CultureInfo.InvariantCulture);
            var separator = text?.IndexOf('=') ?? -1;
            if (separator > 0)
            {
                result[text!.Substring(0, separator)] = text.Substring(separator + 1);
            }
        }

        return result;
    }

    private static string? Value(
        IReadOnlyDictionary<string, string> values,
        string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static string EntryKey(Guid reportId, Guid sectionId) =>
        $"R_{reportId:N}_S_{sectionId:N}";
}
