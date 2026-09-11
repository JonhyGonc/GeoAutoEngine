using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace GeoAutoEngine.Cad2026.VideoInspection;

public sealed class AutoCadInspectionRenderGeometryBridge
{
    private const string LegacyDictionaryName = "GEOAUTOENGINE_VIDEO_INSPECTION_COVERAGE";
    private const double GeometryTolerance = 1e-6;

    public IDisposable Prepare(Document document, Guid reportId, string geometryHandle)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(geometryHandle);
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var rootHandle = AutoCadInspectionSharedCoverageStore.NormalizeRootHandle(
            document.Database,
            transaction,
            reportId,
            geometryHandle);
        var root = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
            document.Database,
            transaction,
            rootHandle,
            OpenMode.ForWrite)
            ?? throw new InvalidOperationException("A Polyline original associada à rede não existe mais.");
        using var logicalGeometry = AutoCadInspectionSharedCoverageStore.CreateLogicalGeometry(
            document.Database,
            transaction,
            reportId,
            rootHandle);
        var managedHandles = ManagedHandles(
            document.Database,
            transaction,
            reportId,
            rootHandle);
        if (managedHandles.Count == 0 &&
            Math.Abs(root.Length - logicalGeometry.Length) <= GeometryTolerance)
        {
            transaction.Commit();
            return NoopDisposable.Instance;
        }

        var originalRootGeometry = (Polyline)root.Clone();
        CopyGeometry(logicalGeometry, root);
        var erasedIds = new List<ObjectId>();
        foreach (var handle in managedHandles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(handle, rootHandle, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entity = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
                document.Database,
                transaction,
                handle,
                OpenMode.ForWrite);
            if (entity is null || entity.IsErased)
            {
                continue;
            }

            erasedIds.Add(entity.ObjectId);
            entity.Erase();
        }

        transaction.Commit();
        document.Editor.Regen();
        document.Editor.UpdateScreen();
        return new RestoreScope(document, rootHandle, originalRootGeometry, erasedIds);
    }

    private static IReadOnlyList<string> ManagedHandles(
        Database database,
        Transaction transaction,
        Guid reportId,
        string rootHandle)
    {
        var handles = new List<string>();
        var state = AutoCadInspectionSharedCoverageStore.Load(database, transaction, reportId, rootHandle);
        if (state is not null)
        {
            handles.AddRange(state.PhysicalSegmentHandles);
            handles.AddRange(state.InspectedOverlayHandles);
            handles.AddRange(state.NotInspectedOverlayHandles);
        }

        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(LegacyDictionaryName))
        {
            return handles;
        }

        var dictionary = (DBDictionary)transaction.GetObject(
            namedObjects.GetAt(LegacyDictionaryName),
            OpenMode.ForRead);
        foreach (DBDictionaryEntry entry in dictionary)
        {
            var record = (Xrecord)transaction.GetObject(entry.Value, OpenMode.ForRead);
            var values = ReadValues(record.Data);
            if (!values.TryGetValue("REPORT_ID", out var reportValue) ||
                !Guid.TryParse(reportValue, out var parsedReport) || parsedReport != reportId ||
                !string.Equals(
                    Value(values, "ORIGINAL_HANDLE"),
                    rootHandle,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Add(handles, Value(values, "REMAINDER_HANDLE"));
            Add(handles, Value(values, "INSPECTED_OVERLAY_HANDLE"));
            Add(handles, Value(values, "NOT_INSPECTED_OVERLAY_HANDLE"));
        }

        return handles;
    }

    private static void Add(ICollection<string> handles, string? handle)
    {
        if (!string.IsNullOrWhiteSpace(handle))
        {
            handles.Add(handle);
        }
    }

    private static void CopyGeometry(Polyline source, Polyline target)
    {
        while (target.NumberOfVertices > 0)
        {
            target.RemoveVertexAt(target.NumberOfVertices - 1);
        }

        target.Normal = source.Normal;
        target.Elevation = source.Elevation;
        target.Thickness = source.Thickness;
        for (var index = 0; index < source.NumberOfVertices; index++)
        {
            target.AddVertexAt(
                target.NumberOfVertices,
                source.GetPoint2dAt(index),
                source.GetBulgeAt(index),
                source.GetStartWidthAt(index),
                source.GetEndWidthAt(index));
        }

        target.Closed = source.Closed;
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

    private sealed class RestoreScope : IDisposable
    {
        private readonly Document _document;
        private readonly string _rootHandle;
        private readonly Polyline _originalRootGeometry;
        private readonly IReadOnlyList<ObjectId> _erasedIds;
        private bool _disposed;

        internal RestoreScope(
            Document document,
            string rootHandle,
            Polyline originalRootGeometry,
            IReadOnlyList<ObjectId> erasedIds)
        {
            _document = document;
            _rootHandle = rootHandle;
            _originalRootGeometry = originalRootGeometry;
            _erasedIds = erasedIds;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            try
            {
                using var documentLock = _document.LockDocument();
                using var transaction = _document.Database.TransactionManager.StartTransaction();
                var root = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
                    _document.Database,
                    transaction,
                    _rootHandle,
                    OpenMode.ForWrite);
                if (root is not null)
                {
                    CopyGeometry(_originalRootGeometry, root);
                }

                foreach (var objectId in _erasedIds)
                {
                    if (objectId.IsNull || !objectId.IsValid)
                    {
                        continue;
                    }

                    if (transaction.GetObject(
                            objectId,
                            OpenMode.ForWrite,
                            openErased: true) is Entity entity && entity.IsErased)
                    {
                        entity.Erase(false);
                    }
                }

                transaction.Commit();
                _document.Editor.Regen();
                _document.Editor.UpdateScreen();
            }
            finally
            {
                _originalRootGeometry.Dispose();
            }
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        internal static NoopDisposable Instance { get; } = new();
        public void Dispose()
        {
        }
    }
}
