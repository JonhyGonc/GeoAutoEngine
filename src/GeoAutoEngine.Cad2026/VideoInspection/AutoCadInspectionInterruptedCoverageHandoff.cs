using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace GeoAutoEngine.Cad2026.VideoInspection;

public sealed class AutoCadInspectionInterruptedCoverageHandoff
{
    private const string LegacyDictionaryName = "GEOAUTOENGINE_VIDEO_INSPECTION_COVERAGE";
    private const double GeometryTolerance = 1e-6;

    public void Prepare(Document document, Guid reportId, string geometryHandle)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(geometryHandle))
        {
            return;
        }

        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var selected = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
            document.Database,
            transaction,
            geometryHandle,
            OpenMode.ForRead);
        if (selected is null)
        {
            transaction.Commit();
            return;
        }

        ClearSharedInterruptedOverlay(document.Database, transaction, reportId, selected);
        ClearLegacyInterruptedOverlay(document.Database, transaction, reportId, geometryHandle);
        transaction.Commit();
        document.Editor.Regen();
        document.Editor.UpdateScreen();
    }

    private static void ClearSharedInterruptedOverlay(
        Database database,
        Transaction transaction,
        Guid reportId,
        Polyline selected)
    {
        var state = AutoCadInspectionSharedCoverageStore.FindStateContainingHandle(
            database,
            transaction,
            reportId,
            selected.Handle.ToString());
        if (state is null)
        {
            return;
        }

        foreach (var handle in state.NotInspectedOverlayHandles)
        {
            var overlay = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
                database,
                transaction,
                handle,
                OpenMode.ForWrite);
            if (overlay is not null && !overlay.IsErased && Coincident(selected, overlay))
            {
                overlay.Erase();
            }
        }
    }

    private static void ClearLegacyInterruptedOverlay(
        Database database,
        Transaction transaction,
        Guid reportId,
        string selectedHandle)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(LegacyDictionaryName))
        {
            return;
        }

        var dictionary = (DBDictionary)transaction.GetObject(
            namedObjects.GetAt(LegacyDictionaryName),
            OpenMode.ForRead);
        foreach (DBDictionaryEntry entry in dictionary)
        {
            var record = (Xrecord)transaction.GetObject(entry.Value, OpenMode.ForRead);
            var values = ReadValues(record.Data);
            if (!SameReport(values, reportId) ||
                !string.Equals(
                    Value(values, "REMAINDER_HANDLE"),
                    selectedHandle,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var overlay = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
                database,
                transaction,
                Value(values, "NOT_INSPECTED_OVERLAY_HANDLE"),
                OpenMode.ForWrite);
            if (overlay is not null && !overlay.IsErased)
            {
                overlay.Erase();
            }
        }
    }

    private static bool Coincident(Polyline first, Polyline second)
    {
        if (Math.Abs(first.Length - second.Length) > GeometryTolerance)
        {
            return false;
        }

        var sameDirection =
            first.StartPoint.DistanceTo(second.StartPoint) <= GeometryTolerance &&
            first.EndPoint.DistanceTo(second.EndPoint) <= GeometryTolerance;
        var oppositeDirection =
            first.StartPoint.DistanceTo(second.EndPoint) <= GeometryTolerance &&
            first.EndPoint.DistanceTo(second.StartPoint) <= GeometryTolerance;
        return sameDirection || oppositeDirection;
    }

    private static bool SameReport(IReadOnlyDictionary<string, string> values, Guid reportId) =>
        values.TryGetValue("REPORT_ID", out var reportValue) &&
        Guid.TryParse(reportValue, out var parsed) &&
        parsed == reportId;

    private static string? Value(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

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
}
