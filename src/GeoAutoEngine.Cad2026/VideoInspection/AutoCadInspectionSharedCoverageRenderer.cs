using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Cad2026.VideoInspection;

internal sealed class AutoCadInspectionSharedCoverageRenderer
{
    private const string LegacyDictionaryName = "GEOAUTOENGINE_VIDEO_INSPECTION_COVERAGE";
    private const short InspectedLayerColorIndex = 3;
    private const short NotInspectedLayerColorIndex = 1;
    private const short ByLayerColorIndex = 256;
    private const double GeometryTolerance = 1e-6;
    private const string PhysicalSegmentComponent = "COVERAGE_PHYSICAL_SEGMENT";
    private const string InspectedComponent = "COVERAGE_INSPECTED_SHARED";
    private const string NotInspectedComponent = "COVERAGE_NOT_INSPECTED_SHARED";

    internal AutoCadInspectionCoverageResult Apply(
        Document document,
        VideoInspectionSession session,
        InspectionSectionSession currentSection,
        IReadOnlyList<PositionedInspectionEvent> currentPositionedEvents,
        AutoCadInspectionCoverageOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(currentSection);
        ArgumentNullException.ThrowIfNull(currentPositionedEvents);
        ArgumentNullException.ThrowIfNull(options);
        var association = currentSection.Association
            ?? throw new InvalidOperationException("O trecho ainda não possui geometria associada.");

        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        EnsureRegisteredApplication(document.Database, transaction);
        var rootHandle = AutoCadInspectionSharedCoverageStore.NormalizeRootHandle(
            document.Database,
            transaction,
            session.ReportId,
            association.GeometryHandle);
        using var fullGeometry = AutoCadInspectionSharedCoverageStore.CreateLogicalGeometry(
            document.Database,
            transaction,
            session.ReportId,
            rootHandle);
        var legacyStates = LoadLegacyStates(
            document.Database,
            transaction,
            session.ReportId,
            rootHandle);
        var state = AutoCadInspectionSharedCoverageStore.Load(
            document.Database,
            transaction,
            session.ReportId,
            rootHandle);
        var physicalHandles = state is { PhysicalSegmentHandles.Count: > 0 }
            ? state.PhysicalSegmentHandles.ToList()
            : ResolveInitialPhysicalHandles(
                document.Database,
                transaction,
                rootHandle,
                legacyStates);
        var generatedIds = new HashSet<Guid>(state?.GeneratedSectionIds ?? Array.Empty<Guid>());
        foreach (var legacy in legacyStates)
        {
            generatedIds.Add(legacy.SectionId);
        }
        generatedIds.Add(currentSection.SectionId);

        var generatedSections = session.Sections
            .Where(section => generatedIds.Contains(section.SectionId) && section.Association is not null)
            .Where(section => string.Equals(
                AutoCadInspectionSharedCoverageStore.NormalizeRootHandle(
                    document.Database,
                    transaction,
                    session.ReportId,
                    section.Association!.GeometryHandle),
                rootHandle,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        generatedIds = generatedSections.Select(section => section.SectionId).ToHashSet();
        var contributions = generatedSections
            .Select(section =>
            {
                var positioned = section.SectionId == currentSection.SectionId
                    ? currentPositionedEvents
                    : section.PositionedEvents;
                var individual = InspectionCoveragePlanner.Create(
                    section.InvestigatedMeters,
                    fullGeometry.Length,
                    section.Association!.Direction,
                    positioned);
                return new InspectionCoverageContribution(
                    section.SectionId,
                    section.Association.Direction,
                    individual.InspectedLength);
            })
            .ToArray();
        var plan = InspectionSharedCoveragePlanner.Create(fullGeometry.Length, contributions);

        var blockTable = (BlockTable)transaction.GetObject(
            document.Database.BlockTableId,
            OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(
            blockTable[BlockTableRecord.ModelSpace],
            OpenMode.ForWrite);

        if (options.ApplyBreak && plan.BreakCadDistances.Count > 0)
        {
            physicalHandles = ApplyBreaks(
                document.Database,
                modelSpace,
                transaction,
                fullGeometry,
                rootHandle,
                physicalHandles,
                plan.BreakCadDistances,
                session.ReportId);
        }

        var inspectedOverlayHandles = state?.InspectedOverlayHandles.ToList() ?? [];
        var notInspectedOverlayHandles = state?.NotInspectedOverlayHandles.ToList() ?? [];
        foreach (var legacy in legacyStates)
        {
            AddIfPresent(inspectedOverlayHandles, legacy.InspectedOverlayHandle);
            AddIfPresent(notInspectedOverlayHandles, legacy.NotInspectedOverlayHandle);
        }

        if (options.AddCoverageLines)
        {
            var inspectedLayer = EnsureLayer(
                document.Database,
                transaction,
                AutoCadInspectionCoverageRenderer.InspectedLayerName,
                InspectedLayerColorIndex);
            var notInspectedLayer = EnsureLayer(
                document.Database,
                transaction,
                AutoCadInspectionCoverageRenderer.NotInspectedLayerName,
                NotInspectedLayerColorIndex);
            inspectedOverlayHandles = UpsertIntervals(
                document.Database,
                modelSpace,
                transaction,
                fullGeometry,
                plan.InspectedIntervals,
                inspectedOverlayHandles,
                inspectedLayer,
                session.ReportId,
                rootHandle,
                InspectedComponent);
            notInspectedOverlayHandles = UpsertIntervals(
                document.Database,
                modelSpace,
                transaction,
                fullGeometry,
                plan.RemainingIntervals,
                notInspectedOverlayHandles,
                notInspectedLayer,
                session.ReportId,
                rootHandle,
                NotInspectedComponent);
        }
        else
        {
            EraseHandles(document.Database, transaction, inspectedOverlayHandles);
            EraseHandles(document.Database, transaction, notInspectedOverlayHandles);
            inspectedOverlayHandles.Clear();
            notInspectedOverlayHandles.Clear();
        }

        RemoveLegacyStates(document.Database, transaction, legacyStates);
        var savedState = new AutoCadInspectionSharedCoverageState(
            session.ReportId,
            rootHandle,
            fullGeometry.Length,
            generatedIds.OrderBy(id => id).ToArray(),
            physicalHandles.ToArray(),
            inspectedOverlayHandles.ToArray(),
            notInspectedOverlayHandles.ToArray());
        AutoCadInspectionSharedCoverageStore.Save(document.Database, transaction, savedState);
        transaction.Commit();
        document.Editor.Regen();
        document.Editor.UpdateScreen();
        return new AutoCadInspectionCoverageResult(
            plan.InspectedLength,
            plan.RemainingLength,
            options.ApplyBreak && plan.BreakCadDistances.Count > 0,
            AutoCadInspectionCoverageRenderer.InspectedLayerName,
            AutoCadInspectionCoverageRenderer.NotInspectedLayerName);
    }

    internal void RemoveContribution(
        Document document,
        VideoInspectionSession session,
        InspectionSectionSession section,
        bool keepCoverageLines)
    {
        var association = section.Association;
        if (association is null)
        {
            return;
        }

        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        EnsureRegisteredApplication(document.Database, transaction);
        var rootHandle = AutoCadInspectionSharedCoverageStore.NormalizeRootHandle(
            document.Database,
            transaction,
            session.ReportId,
            association.GeometryHandle);
        var state = AutoCadInspectionSharedCoverageStore.Load(
            document.Database,
            transaction,
            session.ReportId,
            rootHandle);
        if (state is null)
        {
            transaction.Commit();
            return;
        }

        using var fullGeometry = AutoCadInspectionSharedCoverageStore.CreateLogicalGeometry(
            document.Database,
            transaction,
            session.ReportId,
            rootHandle);
        var generatedIds = state.GeneratedSectionIds
            .Where(id => id != section.SectionId)
            .ToHashSet();
        var remainingSections = session.Sections
            .Where(item => generatedIds.Contains(item.SectionId) && item.Association is not null)
            .Where(item => string.Equals(
                AutoCadInspectionSharedCoverageStore.NormalizeRootHandle(
                    document.Database,
                    transaction,
                    session.ReportId,
                    item.Association!.GeometryHandle),
                rootHandle,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var contributions = remainingSections.Select(item =>
        {
            var individual = InspectionCoveragePlanner.Create(
                item.InvestigatedMeters,
                fullGeometry.Length,
                item.Association!.Direction,
                item.PositionedEvents);
            return new InspectionCoverageContribution(
                item.SectionId,
                item.Association.Direction,
                individual.InspectedLength);
        }).ToArray();
        var plan = InspectionSharedCoveragePlanner.Create(fullGeometry.Length, contributions);
        var inspectedHandles = state.InspectedOverlayHandles.ToList();
        var notInspectedHandles = state.NotInspectedOverlayHandles.ToList();
        if (keepCoverageLines && remainingSections.Length > 0)
        {
            var blockTable = (BlockTable)transaction.GetObject(
                document.Database.BlockTableId,
                OpenMode.ForRead);
            var modelSpace = (BlockTableRecord)transaction.GetObject(
                blockTable[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);
            var inspectedLayer = EnsureLayer(
                document.Database,
                transaction,
                AutoCadInspectionCoverageRenderer.InspectedLayerName,
                InspectedLayerColorIndex);
            var notInspectedLayer = EnsureLayer(
                document.Database,
                transaction,
                AutoCadInspectionCoverageRenderer.NotInspectedLayerName,
                NotInspectedLayerColorIndex);
            inspectedHandles = UpsertIntervals(
                document.Database,
                modelSpace,
                transaction,
                fullGeometry,
                plan.InspectedIntervals,
                inspectedHandles,
                inspectedLayer,
                session.ReportId,
                rootHandle,
                InspectedComponent);
            notInspectedHandles = UpsertIntervals(
                document.Database,
                modelSpace,
                transaction,
                fullGeometry,
                plan.RemainingIntervals,
                notInspectedHandles,
                notInspectedLayer,
                session.ReportId,
                rootHandle,
                NotInspectedComponent);
        }
        else
        {
            EraseHandles(document.Database, transaction, inspectedHandles);
            EraseHandles(document.Database, transaction, notInspectedHandles);
            inspectedHandles.Clear();
            notInspectedHandles.Clear();
        }

        AutoCadInspectionSharedCoverageStore.Save(
            document.Database,
            transaction,
            state with
            {
                GeneratedSectionIds = remainingSections.Select(item => item.SectionId).OrderBy(id => id).ToArray(),
                InspectedOverlayHandles = inspectedHandles.ToArray(),
                NotInspectedOverlayHandles = notInspectedHandles.ToArray(),
            });
        transaction.Commit();
        document.Editor.Regen();
        document.Editor.UpdateScreen();
    }

    private static List<string> ApplyBreaks(
        Database database,
        BlockTableRecord modelSpace,
        Transaction transaction,
        Polyline fullGeometry,
        string rootHandle,
        IReadOnlyList<string> currentPhysicalHandles,
        IReadOnlyList<double> breakDistances,
        Guid reportId)
    {
        using var pieces = SplitPieces.Create(fullGeometry, breakDistances);
        var root = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
            database,
            transaction,
            rootHandle,
            OpenMode.ForWrite)
            ?? throw new InvalidOperationException("A Polyline original associada à rede não existe mais.");
        using var template = (Polyline)root.Clone();
        var reusable = currentPhysicalHandles
            .Where(handle => !string.Equals(handle, rootHandle, StringComparison.OrdinalIgnoreCase))
            .Select(handle => AutoCadInspectionSharedCoverageStore.ResolvePolyline(
                database,
                transaction,
                handle,
                OpenMode.ForWrite))
            .Where(polyline => polyline is not null)
            .Cast<Polyline>()
            .ToList();
        CopyGeometry(pieces.Items[0], root);
        var result = new List<string> { rootHandle };
        for (var index = 1; index < pieces.Items.Count; index++)
        {
            Polyline target;
            if (index - 1 < reusable.Count)
            {
                target = reusable[index - 1];
            }
            else
            {
                target = (Polyline)template.Clone();
                modelSpace.AppendEntity(target);
                transaction.AddNewlyCreatedDBObject(target, true);
            }

            CopyGeometry(pieces.Items[index], target);
            SetManagedMetadata(target, reportId, rootHandle, PhysicalSegmentComponent);
            result.Add(target.Handle.ToString());
        }

        for (var index = pieces.Items.Count - 1; index < reusable.Count; index++)
        {
            if (!reusable[index].IsErased)
            {
                reusable[index].Erase();
            }
        }

        return result;
    }

    private static List<string> UpsertIntervals(
        Database database,
        BlockTableRecord modelSpace,
        Transaction transaction,
        Polyline fullGeometry,
        IReadOnlyList<InspectionCoverageInterval> intervals,
        IReadOnlyList<string> existingHandles,
        ObjectId layerId,
        Guid reportId,
        string rootHandle,
        string component)
    {
        var existing = existingHandles
            .Select(handle => AutoCadInspectionSharedCoverageStore.ResolvePolyline(
                database,
                transaction,
                handle,
                OpenMode.ForWrite))
            .Where(polyline => polyline is not null)
            .Cast<Polyline>()
            .ToList();
        var result = new List<string>();
        for (var index = 0; index < intervals.Count; index++)
        {
            using var intervalGeometry = ExtractInterval(fullGeometry, intervals[index]);
            Polyline overlay;
            if (index < existing.Count)
            {
                overlay = existing[index];
            }
            else
            {
                overlay = new Polyline();
                overlay.SetDatabaseDefaults(database);
                modelSpace.AppendEntity(overlay);
                transaction.AddNewlyCreatedDBObject(overlay, true);
            }

            CopyGeometry(intervalGeometry, overlay);
            overlay.LayerId = layerId;
            overlay.ColorIndex = ByLayerColorIndex;
            SetManagedMetadata(overlay, reportId, rootHandle, component);
            result.Add(overlay.Handle.ToString());
        }

        for (var index = intervals.Count; index < existing.Count; index++)
        {
            if (!existing[index].IsErased)
            {
                existing[index].Erase();
            }
        }

        return result;
    }

    private static Polyline ExtractInterval(
        Polyline source,
        InspectionCoverageInterval interval)
    {
        if (interval.StartCadDistance <= GeometryTolerance &&
            interval.EndCadDistance >= source.Length - GeometryTolerance)
        {
            return (Polyline)source.Clone();
        }

        var splitDistances = new List<double>();
        if (interval.StartCadDistance > GeometryTolerance)
        {
            splitDistances.Add(interval.StartCadDistance);
        }
        if (interval.EndCadDistance < source.Length - GeometryTolerance)
        {
            splitDistances.Add(interval.EndCadDistance);
        }

        using var pieces = SplitPieces.Create(source, splitDistances);
        var startPoint = source.GetPointAtDist(interval.StartCadDistance);
        var endPoint = source.GetPointAtDist(interval.EndCadDistance);
        var match = pieces.Items.FirstOrDefault(piece =>
            piece.StartPoint.DistanceTo(startPoint) <= GeometryTolerance &&
            piece.EndPoint.DistanceTo(endPoint) <= GeometryTolerance);
        if (match is null)
        {
            throw new InvalidOperationException("Não foi possível extrair o intervalo visual da cobertura.");
        }

        return (Polyline)match.Clone();
    }

    private static List<string> ResolveInitialPhysicalHandles(
        Database database,
        Transaction transaction,
        string rootHandle,
        IReadOnlyList<LegacyCoverageState> legacyStates)
    {
        var root = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
            database,
            transaction,
            rootHandle,
            OpenMode.ForRead);
        var legacy = legacyStates.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.RemainderHandle));
        if (root is null || legacy?.RemainderHandle is null)
        {
            return [rootHandle];
        }

        var remainder = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
            database,
            transaction,
            legacy.RemainderHandle,
            OpenMode.ForRead);
        if (remainder is null)
        {
            return [rootHandle];
        }

        if (root.EndPoint.DistanceTo(remainder.StartPoint) <= GeometryTolerance)
        {
            return [rootHandle, legacy.RemainderHandle];
        }
        if (remainder.EndPoint.DistanceTo(root.StartPoint) <= GeometryTolerance)
        {
            return [legacy.RemainderHandle, rootHandle];
        }

        return [rootHandle];
    }

    private static IReadOnlyList<LegacyCoverageState> LoadLegacyStates(
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
            return Array.Empty<LegacyCoverageState>();
        }

        var dictionary = (DBDictionary)transaction.GetObject(
            namedObjects.GetAt(LegacyDictionaryName),
            OpenMode.ForRead);
        var result = new List<LegacyCoverageState>();
        foreach (DBDictionaryEntry entry in dictionary)
        {
            var record = (Xrecord)transaction.GetObject(entry.Value, OpenMode.ForRead);
            var values = ReadValues(record.Data);
            if (!values.TryGetValue("REPORT_ID", out var reportValue) ||
                !Guid.TryParse(reportValue, out var parsedReport) || parsedReport != reportId ||
                !values.TryGetValue("SECTION_ID", out var sectionValue) ||
                !Guid.TryParse(sectionValue, out var sectionId) ||
                !string.Equals(Value(values, "ORIGINAL_HANDLE"), rootHandle, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new LegacyCoverageState(
                entry.Key,
                sectionId,
                Value(values, "REMAINDER_HANDLE"),
                Value(values, "INSPECTED_OVERLAY_HANDLE"),
                Value(values, "NOT_INSPECTED_OVERLAY_HANDLE")));
        }

        return result;
    }

    private static void RemoveLegacyStates(
        Database database,
        Transaction transaction,
        IReadOnlyList<LegacyCoverageState> legacyStates)
    {
        if (legacyStates.Count == 0)
        {
            return;
        }

        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(LegacyDictionaryName))
        {
            return;
        }

        var dictionary = (DBDictionary)transaction.GetObject(
            namedObjects.GetAt(LegacyDictionaryName),
            OpenMode.ForWrite);
        foreach (var legacy in legacyStates)
        {
            if (!dictionary.Contains(legacy.Key))
            {
                continue;
            }

            var record = (Xrecord)transaction.GetObject(dictionary.GetAt(legacy.Key), OpenMode.ForWrite);
            dictionary.Remove(legacy.Key);
            record.Erase();
        }
    }

    private static void AddIfPresent(ICollection<string> handles, string? handle)
    {
        if (!string.IsNullOrWhiteSpace(handle) &&
            !handles.Contains(handle, StringComparer.OrdinalIgnoreCase))
        {
            handles.Add(handle);
        }
    }

    private static void EraseHandles(
        Database database,
        Transaction transaction,
        IEnumerable<string> handles)
    {
        foreach (var handle in handles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var entity = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
                database,
                transaction,
                handle,
                OpenMode.ForWrite);
            if (entity is not null && !entity.IsErased)
            {
                entity.Erase();
            }
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

    private static ObjectId EnsureLayer(
        Database database,
        Transaction transaction,
        string name,
        short colorIndex)
    {
        var table = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        LayerTableRecord record;
        if (table.Has(name))
        {
            record = (LayerTableRecord)transaction.GetObject(table[name], OpenMode.ForWrite);
        }
        else
        {
            table.UpgradeOpen();
            record = new LayerTableRecord { Name = name };
            table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        record.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
        var linetypes = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
        if (linetypes.Has("Continuous"))
        {
            record.LinetypeObjectId = linetypes["Continuous"];
        }

        return record.ObjectId;
    }

    private static void EnsureRegisteredApplication(Database database, Transaction transaction)
    {
        var table = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
        if (table.Has(AutoCadInspectionRenderer.RegisteredApplicationName))
        {
            return;
        }

        table.UpgradeOpen();
        var record = new RegAppTableRecord { Name = AutoCadInspectionRenderer.RegisteredApplicationName };
        table.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
    }

    private static void SetManagedMetadata(
        Entity entity,
        Guid reportId,
        string rootHandle,
        string component)
    {
        using var data = new ResultBuffer(
            new TypedValue(
                (int)DxfCode.ExtendedDataRegAppName,
                AutoCadInspectionRenderer.RegisteredApplicationName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"REPORT_ID={reportId:D}"),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"ROOT_HANDLE={rootHandle}"),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"COMPONENT={component}"));
        entity.XData = data;
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

    private sealed record LegacyCoverageState(
        string Key,
        Guid SectionId,
        string? RemainderHandle,
        string? InspectedOverlayHandle,
        string? NotInspectedOverlayHandle);

    private sealed class SplitPieces : IDisposable
    {
        private SplitPieces(IReadOnlyList<Polyline> items) => Items = items;

        internal IReadOnlyList<Polyline> Items { get; }

        internal static SplitPieces Create(Polyline source, IReadOnlyList<double> distances)
        {
            var valid = distances
                .Where(value => value > GeometryTolerance && value < source.Length - GeometryTolerance)
                .OrderBy(value => value)
                .Aggregate(new List<double>(), (values, value) =>
                {
                    if (values.Count == 0 || Math.Abs(values[^1] - value) > GeometryTolerance)
                    {
                        values.Add(value);
                    }
                    return values;
                });
            if (valid.Count == 0)
            {
                return new SplitPieces([(Polyline)source.Clone()]);
            }

            var points = new Point3dCollection();
            foreach (var distance in valid)
            {
                points.Add(source.GetPointAtDist(distance));
            }

            var objects = source.GetSplitCurves(points);
            var pieces = objects.Cast<DBObject>().OfType<Polyline>().ToArray();
            foreach (var item in objects.Cast<DBObject>().Where(item => item is not Polyline))
            {
                item.Dispose();
            }

            if (pieces.Length != valid.Count + 1)
            {
                foreach (var piece in pieces)
                {
                    piece.Dispose();
                }
                throw new InvalidOperationException(
                    $"O BREAK retornou {pieces.Length} segmento(s); eram esperados {valid.Count + 1}.");
            }

            var ordered = pieces
                .OrderBy(piece => SourceDistance(source, piece.StartPoint))
                .ToArray();
            return new SplitPieces(Array.AsReadOnly(ordered));
        }

        private static double SourceDistance(Polyline source, Point3d point)
        {
            try
            {
                return source.GetDistAtPoint(point);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return source.GetDistAtPoint(source.GetClosestPointTo(point, false));
            }
        }

        public void Dispose()
        {
            foreach (var item in Items)
            {
                item.Dispose();
            }
        }
    }
}
