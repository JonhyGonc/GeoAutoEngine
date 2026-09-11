using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace GeoAutoEngine.Cad2026.VideoInspection;

internal sealed record AutoCadInspectionSharedCoverageState(
    Guid ReportId,
    string RootHandle,
    double OriginalLength,
    IReadOnlyList<Guid> GeneratedSectionIds,
    IReadOnlyList<string> PhysicalSegmentHandles,
    IReadOnlyList<string> InspectedOverlayHandles,
    IReadOnlyList<string> NotInspectedOverlayHandles);

internal static class AutoCadInspectionSharedCoverageStore
{
    internal const string DictionaryName = "GEOAUTOENGINE_VIDEO_INSPECTION_SHARED_COVERAGE";
    private const string LegacyDictionaryName = "GEOAUTOENGINE_VIDEO_INSPECTION_COVERAGE";
    private const double GeometryTolerance = 1e-6;

    internal static AutoCadInspectionSharedCoverageState? Load(
        Database database,
        Transaction transaction,
        Guid reportId,
        string rootHandle)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(DictionaryName))
        {
            return null;
        }

        var dictionary = (DBDictionary)transaction.GetObject(
            namedObjects.GetAt(DictionaryName),
            OpenMode.ForRead);
        var key = EntryKey(reportId, rootHandle);
        if (!dictionary.Contains(key))
        {
            return null;
        }

        var record = (Xrecord)transaction.GetObject(dictionary.GetAt(key), OpenMode.ForRead);
        return Parse(record.Data);
    }

    internal static void Save(
        Database database,
        Transaction transaction,
        AutoCadInspectionSharedCoverageState state)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        DBDictionary dictionary;
        if (namedObjects.Contains(DictionaryName))
        {
            dictionary = (DBDictionary)transaction.GetObject(
                namedObjects.GetAt(DictionaryName),
                OpenMode.ForWrite);
        }
        else
        {
            namedObjects.UpgradeOpen();
            dictionary = new DBDictionary();
            namedObjects.SetAt(DictionaryName, dictionary);
            transaction.AddNewlyCreatedDBObject(dictionary, true);
        }

        var key = EntryKey(state.ReportId, state.RootHandle);
        Xrecord record;
        if (dictionary.Contains(key))
        {
            record = (Xrecord)transaction.GetObject(dictionary.GetAt(key), OpenMode.ForWrite);
        }
        else
        {
            record = new Xrecord();
            dictionary.SetAt(key, record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        record.Data = new ResultBuffer(
            Text("STORAGE_VERSION=1"),
            Text($"REPORT_ID={state.ReportId:D}"),
            Text($"ROOT_HANDLE={state.RootHandle}"),
            Text(FormattableString.Invariant($"ORIGINAL_LENGTH={state.OriginalLength:R}")),
            Text($"GENERATED_SECTION_IDS={JoinGuids(state.GeneratedSectionIds)}"),
            Text($"PHYSICAL_SEGMENT_HANDLES={JoinStrings(state.PhysicalSegmentHandles)}"),
            Text($"INSPECTED_OVERLAY_HANDLES={JoinStrings(state.InspectedOverlayHandles)}"),
            Text($"NOT_INSPECTED_OVERLAY_HANDLES={JoinStrings(state.NotInspectedOverlayHandles)}"));
    }

    internal static string NormalizeRootHandle(
        Database database,
        Transaction transaction,
        Guid reportId,
        string selectedHandle)
    {
        // Dê prioridade ao estado legado porque linhas de cobertura criadas pelas versões
        // anteriores podem estar exatamente sobre o segmento físico do BREAK. Se o usuário
        // selecionar a linha verde/vermelha, ela deve voltar à Polyline raiz, nunca virar
        // uma nova rede independente.
        var legacyRoot = FindLegacyRootHandleContaining(
            database,
            transaction,
            reportId,
            selectedHandle);
        if (!string.IsNullOrWhiteSpace(legacyRoot))
        {
            return legacyRoot;
        }

        var state = FindStateContainingHandle(database, transaction, reportId, selectedHandle);
        if (state is not null)
        {
            return state.RootHandle;
        }

        return selectedHandle;
    }

    internal static Polyline CreateLogicalGeometry(
        Database database,
        Transaction transaction,
        Guid reportId,
        string rootHandle)
    {
        var normalizedRoot = NormalizeRootHandle(database, transaction, reportId, rootHandle);
        var state = Load(database, transaction, reportId, normalizedRoot);
        if (state is null)
        {
            var legacy = TryCreateLegacyLogicalGeometry(
                database,
                transaction,
                reportId,
                normalizedRoot);
            if (legacy is not null)
            {
                return legacy;
            }
        }

        var handles = state is not null && state.PhysicalSegmentHandles.Count > 0
            ? state.PhysicalSegmentHandles
            : Array.AsReadOnly(new[] { normalizedRoot });
        Polyline? merged = null;
        foreach (var handle in handles)
        {
            var segment = ResolvePolyline(database, transaction, handle, OpenMode.ForRead)
                ?? throw new InvalidOperationException(
                    "Um segmento da rede compartilhada não existe mais. Reassocie a geometria do trecho.");
            if (merged is null)
            {
                merged = (Polyline)segment.Clone();
                continue;
            }

            AppendGeometry(merged, segment);
        }

        return merged
            ?? throw new InvalidOperationException("A geometria compartilhada não pôde ser reconstruída.");
    }

    internal static AutoCadInspectionSharedCoverageState? FindStateContainingHandle(
        Database database,
        Transaction transaction,
        Guid reportId,
        string handle)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(DictionaryName))
        {
            return null;
        }

        var dictionary = (DBDictionary)transaction.GetObject(
            namedObjects.GetAt(DictionaryName),
            OpenMode.ForRead);
        foreach (DBDictionaryEntry entry in dictionary)
        {
            var record = (Xrecord)transaction.GetObject(entry.Value, OpenMode.ForRead);
            var state = Parse(record.Data);
            if (state is null || state.ReportId != reportId)
            {
                continue;
            }

            if (string.Equals(state.RootHandle, handle, StringComparison.OrdinalIgnoreCase) ||
                state.PhysicalSegmentHandles.Any(item =>
                    string.Equals(item, handle, StringComparison.OrdinalIgnoreCase)) ||
                state.InspectedOverlayHandles.Any(item =>
                    string.Equals(item, handle, StringComparison.OrdinalIgnoreCase)) ||
                state.NotInspectedOverlayHandles.Any(item =>
                    string.Equals(item, handle, StringComparison.OrdinalIgnoreCase)))
            {
                return state;
            }
        }

        return null;
    }

    internal static Polyline? ResolvePolyline(
        Database database,
        Transaction transaction,
        string? handleValue,
        OpenMode openMode)
    {
        if (string.IsNullOrWhiteSpace(handleValue))
        {
            return null;
        }

        try
        {
            var handle = new Handle(Convert.ToInt64(handleValue, 16));
            var objectId = database.GetObjectId(false, handle, 0);
            if (objectId.IsNull || !objectId.IsValid || objectId.IsErased)
            {
                return null;
            }

            return transaction.GetObject(objectId, openMode, openErased: false) as Polyline;
        }
        catch (System.Exception exception) when (
            exception is Autodesk.AutoCAD.Runtime.Exception or FormatException or OverflowException)
        {
            return null;
        }
    }

    private static string? FindLegacyRootHandleContaining(
        Database database,
        Transaction transaction,
        Guid reportId,
        string handle)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(LegacyDictionaryName))
        {
            return null;
        }

        var dictionary = (DBDictionary)transaction.GetObject(
            namedObjects.GetAt(LegacyDictionaryName),
            OpenMode.ForRead);
        foreach (DBDictionaryEntry entry in dictionary)
        {
            var record = (Xrecord)transaction.GetObject(entry.Value, OpenMode.ForRead);
            var values = ReadValues(record.Data);
            if (!SameReport(values, reportId))
            {
                continue;
            }

            var original = Value(values, "ORIGINAL_HANDLE");
            var remainder = Value(values, "REMAINDER_HANDLE");
            var inspectedOverlay = Value(values, "INSPECTED_OVERLAY_HANDLE");
            var notInspectedOverlay = Value(values, "NOT_INSPECTED_OVERLAY_HANDLE");
            if (!string.IsNullOrWhiteSpace(original) &&
                (string.Equals(original, handle, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(remainder, handle, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(inspectedOverlay, handle, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(notInspectedOverlay, handle, StringComparison.OrdinalIgnoreCase)))
            {
                return original;
            }
        }

        return null;
    }

    private static Polyline? TryCreateLegacyLogicalGeometry(
        Database database,
        Transaction transaction,
        Guid reportId,
        string rootHandle)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(LegacyDictionaryName))
        {
            return null;
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
                    Value(values, "ORIGINAL_HANDLE"),
                    rootHandle,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var root = ResolvePolyline(database, transaction, rootHandle, OpenMode.ForRead);
            var remainder = ResolvePolyline(
                database,
                transaction,
                Value(values, "REMAINDER_HANDLE"),
                OpenMode.ForRead);
            if (root is null || remainder is null)
            {
                continue;
            }

            if (root.EndPoint.DistanceTo(remainder.StartPoint) <= GeometryTolerance)
            {
                var merged = (Polyline)root.Clone();
                AppendGeometry(merged, remainder);
                return merged;
            }

            if (remainder.EndPoint.DistanceTo(root.StartPoint) <= GeometryTolerance)
            {
                var merged = (Polyline)remainder.Clone();
                AppendGeometry(merged, root);
                return merged;
            }
        }

        return null;
    }

    private static bool SameReport(IReadOnlyDictionary<string, string> values, Guid reportId) =>
        values.TryGetValue("REPORT_ID", out var reportValue) &&
        Guid.TryParse(reportValue, out var parsed) &&
        parsed == reportId;

    private static void AppendGeometry(Polyline target, Polyline next)
    {
        if (target.EndPoint.DistanceTo(next.StartPoint) > GeometryTolerance)
        {
            throw new InvalidOperationException(
                "Os segmentos da rede compartilhada não são mais contíguos. Reassocie a geometria.");
        }

        if (next.NumberOfVertices == 0)
        {
            return;
        }

        var junction = target.NumberOfVertices - 1;
        target.SetBulgeAt(junction, next.GetBulgeAt(0));
        target.SetStartWidthAt(junction, next.GetStartWidthAt(0));
        target.SetEndWidthAt(junction, next.GetEndWidthAt(0));
        for (var index = 1; index < next.NumberOfVertices; index++)
        {
            target.AddVertexAt(
                target.NumberOfVertices,
                next.GetPoint2dAt(index),
                next.GetBulgeAt(index),
                next.GetStartWidthAt(index),
                next.GetEndWidthAt(index));
        }

        target.Closed = false;
    }

    private static AutoCadInspectionSharedCoverageState? Parse(ResultBuffer? data)
    {
        var values = ReadValues(data);
        if (!values.TryGetValue("REPORT_ID", out var reportValue) ||
            !Guid.TryParse(reportValue, out var reportId) ||
            !values.TryGetValue("ROOT_HANDLE", out var rootHandle) ||
            string.IsNullOrWhiteSpace(rootHandle))
        {
            return null;
        }

        var originalLength = values.TryGetValue("ORIGINAL_LENGTH", out var lengthValue) &&
            double.TryParse(
                lengthValue,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedLength)
            ? parsedLength
            : 0.0;
        return new AutoCadInspectionSharedCoverageState(
            reportId,
            rootHandle,
            originalLength,
            ParseGuids(Value(values, "GENERATED_SECTION_IDS")),
            ParseStrings(Value(values, "PHYSICAL_SEGMENT_HANDLES")),
            ParseStrings(Value(values, "INSPECTED_OVERLAY_HANDLES")),
            ParseStrings(Value(values, "NOT_INSPECTED_OVERLAY_HANDLES")));
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

    private static IReadOnlyList<string> ParseStrings(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return Array.AsReadOnly(value
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToArray());
    }

    private static IReadOnlyList<Guid> ParseGuids(string? value) =>
        ParseStrings(value)
            .Select(item => Guid.TryParse(item, out var parsed) ? parsed : Guid.Empty)
            .Where(item => item != Guid.Empty)
            .ToArray();

    private static string JoinStrings(IEnumerable<string> values) =>
        string.Join(";", values.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static string JoinGuids(IEnumerable<Guid> values) =>
        string.Join(";", values.Where(value => value != Guid.Empty).Select(value => value.ToString("D")));

    private static string EntryKey(Guid reportId, string rootHandle) =>
        $"R_{reportId:N}_H_{rootHandle.Trim().ToUpperInvariant()}";

    private static TypedValue Text(string value) => new((int)DxfCode.Text, value);
}
