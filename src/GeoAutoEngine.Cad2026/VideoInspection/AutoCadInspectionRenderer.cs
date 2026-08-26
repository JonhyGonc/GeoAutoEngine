using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Cad2026.VideoInspection;

public sealed record AutoCadInspectionCommittedEntity(
    Guid OccurrenceId,
    string EntityType,
    string ObjectId,
    string Handle,
    bool ObjectIdIsNull,
    bool ObjectIdIsValid,
    bool Created);

public sealed record AutoCadInspectionCommitResult(
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    bool TransactionCommitted,
    string CurrentSpaceName,
    bool CurrentSpaceIsModelSpace,
    string TargetSpaceName,
    IReadOnlyList<AutoCadInspectionCommittedEntity> Entities);

public sealed class AutoCadInspectionRenderer
{
    public const string RegisteredApplicationName = "GEOAUTOENGINE_VI";
    public const string EventLayerName = "GF-Chamado-Estaqueamento";
    public const string EventTextStyleName = "GEO_ARIAL";
    private const short EventLayerColorIndex = 2;
    private const short ByLayerColorIndex = 256;
    private const string MarkerComponent = "MARKER";
    private const string IntervalEndMarkerComponent = "INTERVAL_END_MARKER";
    private const string IntervalConnectorComponent = "INTERVAL_CONNECTOR";
    private const string LeaderComponent = "LEADER";
    private const string TextComponent = "TEXT";
    private const string SectionNameComponent = "SECTION_NAME";
    private const string SectionDiameterComponent = "SECTION_DIAMETER";
    private const double TextHeight = InspectionCalloutLayoutEngine.TextHeight;
    private const double SectionIdentificationOffset = 0.45;
    private const double GeometryTolerance = 1e-9;

    public AutoCadInspectionCommitResult Commit(
        Document document,
        VideoInspectionSession session,
        InspectionSectionSession section,
        IReadOnlyList<PositionedInspectionEvent> positionedEvents)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(positionedEvents);
        var association = section.Association
            ?? throw new InvalidOperationException("O trecho ainda não possui geometria associada.");
        using var documentLock = document.LockDocument();
        var created = 0;
        var updated = 0;
        var skipped = positionedEvents.Count(positionedEvent =>
            !positionedEvent.CanCommit ||
            positionedEvent.LocationMode == InspectionLocationMode.NotApplicable);
        var pendingEntities = new List<(Guid OccurrenceId, ObjectId ObjectId, bool Created)>();
        string currentSpaceName;
        string targetSpaceName;
        bool currentSpaceIsModelSpace;

        using (var transaction = document.Database.TransactionManager.StartTransaction())
        {
            EnsureRegisteredApplication(document.Database, transaction);
            var eventLayerId = EnsureLayer(
                document.Database,
                transaction,
                EventLayerName,
                EventLayerColorIndex);
            var eventTextStyleId = EnsureArialTextStyle(document.Database, transaction);
            var blockTable = (BlockTable)transaction.GetObject(
                document.Database.BlockTableId,
                OpenMode.ForRead);
            var modelSpaceId = blockTable[BlockTableRecord.ModelSpace];
            var modelSpace = (BlockTableRecord)transaction.GetObject(modelSpaceId, OpenMode.ForWrite);
            var currentSpace = (BlockTableRecord)transaction.GetObject(
                document.Database.CurrentSpaceId,
                OpenMode.ForRead);
            currentSpaceName = currentSpace.Name;
            targetSpaceName = modelSpace.Name;
            currentSpaceIsModelSpace = document.Database.CurrentSpaceId == modelSpaceId;
            if (!AutoCadInspectionAssociationStore.TryResolvePolyline(
                    document.Database,
                    transaction,
                    association.GeometryHandle,
                    OpenMode.ForRead,
                    out var polyline))
            {
                throw new InvalidOperationException("A geometria associada não existe mais.");
            }

            var filmingStart = CreateFilmingStart(polyline!, association.Direction);
            var calls = InspectionAnnotationPlanner.Create(positionedEvents, filmingStart);
            var existing = ReadExisting(modelSpace, transaction);
            var obstacles = ReadLayoutObstacles(
                modelSpace,
                polyline!.ObjectId,
                session.ReportId,
                section.SectionId,
                transaction);
            var placements = InspectionCalloutLayoutEngine.Arrange(calls, obstacles);
            var desiredKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var placement in placements)
            {
                var call = placement.Call;
                var stableKey = CallStableKey(session.ReportId, section.SectionId, call);
                desiredKeys.Add(stableKey);
                var anchor = polyline!.GetPointAtDist(call.Anchor.CadDistance);
                var leaderStartPoint = RelativePoint(
                    anchor,
                    call.Anchor.Position,
                    placement.LeaderStartPoint);
                var leaderEndPoint = RelativePoint(
                    anchor,
                    call.Anchor.Position,
                    placement.LeaderEndPoint);
                var textPoint = RelativePoint(
                    anchor,
                    call.Anchor.Position,
                    placement.TextPoint);
                var existed = existing.TryGetValue(stableKey, out var managedEntities);
                var retainedIds = new HashSet<ObjectId>();

                var marker = ExistingEntity<Circle>(
                    managedEntities,
                    MarkerComponent,
                    transaction);
                var markerCreated = marker is null;
                if (marker is null)
                {
                    marker = new Circle();
                    marker.SetDatabaseDefaults(document.Database);
                    modelSpace.AppendEntity(marker);
                    transaction.AddNewlyCreatedDBObject(marker, true);
                }

                ApplyMarker(marker, anchor, eventLayerId);
                SetMetadata(marker, session, section, call, stableKey, MarkerComponent);
                retainedIds.Add(marker.ObjectId);
                pendingEntities.Add((call.OccurrenceId, marker.ObjectId, markerCreated));

                if (call.IntervalEnd is { } intervalEnd)
                {
                    var intervalEndAnchor = polyline.GetPointAtDist(intervalEnd.CadDistance);
                    var intervalEndMarker = ExistingEntity<Circle>(
                        managedEntities,
                        IntervalEndMarkerComponent,
                        transaction);
                    var intervalEndMarkerCreated = intervalEndMarker is null;
                    if (intervalEndMarker is null)
                    {
                        intervalEndMarker = new Circle();
                        intervalEndMarker.SetDatabaseDefaults(document.Database);
                        modelSpace.AppendEntity(intervalEndMarker);
                        transaction.AddNewlyCreatedDBObject(intervalEndMarker, true);
                    }

                    ApplyMarker(intervalEndMarker, intervalEndAnchor, eventLayerId);
                    SetMetadata(
                        intervalEndMarker,
                        session,
                        section,
                        call,
                        stableKey,
                        IntervalEndMarkerComponent,
                        intervalEnd);
                    retainedIds.Add(intervalEndMarker.ObjectId);
                    pendingEntities.Add((
                        call.OccurrenceId,
                        intervalEndMarker.ObjectId,
                        intervalEndMarkerCreated));

                    var intervalConnector = ExistingEntity<Polyline>(
                        managedEntities,
                        IntervalConnectorComponent,
                        transaction);
                    var intervalConnectorCreated = intervalConnector is null;
                    if (intervalConnector is null)
                    {
                        intervalConnector = new Polyline();
                        intervalConnector.SetDatabaseDefaults(document.Database);
                        modelSpace.AppendEntity(intervalConnector);
                        transaction.AddNewlyCreatedDBObject(intervalConnector, true);
                    }

                    ApplyCalloutPolyline(
                        intervalConnector,
                        [anchor, leaderStartPoint, intervalEndAnchor],
                        eventLayerId);
                    SetMetadata(
                        intervalConnector,
                        session,
                        section,
                        call,
                        stableKey,
                        IntervalConnectorComponent);
                    retainedIds.Add(intervalConnector.ObjectId);
                    pendingEntities.Add((
                        call.OccurrenceId,
                        intervalConnector.ObjectId,
                        intervalConnectorCreated));
                }

                var leader = ExistingEntity<Polyline>(
                    managedEntities,
                    LeaderComponent,
                    transaction);
                var leaderCreated = leader is null;
                if (leader is null)
                {
                    leader = new Polyline();
                    leader.SetDatabaseDefaults(document.Database);
                    modelSpace.AppendEntity(leader);
                    transaction.AddNewlyCreatedDBObject(leader, true);
                }

                ApplyCalloutPolyline(
                    leader,
                    [leaderStartPoint, leaderEndPoint],
                    eventLayerId);
                SetMetadata(leader, session, section, call, stableKey, LeaderComponent);
                retainedIds.Add(leader.ObjectId);
                pendingEntities.Add((call.OccurrenceId, leader.ObjectId, leaderCreated));

                var text = ExistingEntity<MText>(
                    managedEntities,
                    TextComponent,
                    transaction);
                var textCreated = text is null;
                if (text is null)
                {
                    text = new MText();
                    text.SetDatabaseDefaults(document.Database);
                    modelSpace.AppendEntity(text);
                    transaction.AddNewlyCreatedDBObject(text, true);
                }

                ApplyText(
                    text,
                    textPoint,
                    call,
                    placement,
                    eventLayerId,
                    eventTextStyleId);
                SetMetadata(text, session, section, call, stableKey, TextComponent);
                retainedIds.Add(text.ObjectId);
                pendingEntities.Add((call.OccurrenceId, text.ObjectId, textCreated));

                EraseExcept(managedEntities, retainedIds, transaction);
                if (call.IsSyntheticStart)
                {
                    continue;
                }

                if (existed)
                {
                    updated++;
                }
                else
                {
                    created++;
                }
            }

            UpsertSectionIdentification(
                document.Database,
                modelSpace,
                polyline!,
                session,
                section,
                existing,
                desiredKeys,
                eventLayerId,
                eventTextStyleId,
                transaction);

            EraseStaleManagedEntities(
                existing,
                desiredKeys,
                session.ReportId,
                section.SectionId,
                transaction);
            transaction.Commit();
        }

        var committedEntities = VerifyCommittedEntities(document, pendingEntities);
        document.Editor.Regen();
        document.Editor.UpdateScreen();
        return new AutoCadInspectionCommitResult(
            created,
            updated,
            skipped,
            true,
            currentSpaceName,
            currentSpaceIsModelSpace,
            targetSpaceName,
            committedEntities);
    }

    private static IReadOnlyList<AutoCadInspectionCommittedEntity> VerifyCommittedEntities(
        Document document,
        IReadOnlyList<(Guid OccurrenceId, ObjectId ObjectId, bool Created)> pendingEntities)
    {
        var committedEntities = new List<AutoCadInspectionCommittedEntity>(pendingEntities.Count);
        using var verification = document.Database.TransactionManager.StartOpenCloseTransaction();
        foreach (var pending in pendingEntities)
        {
            var objectIdIsNull = pending.ObjectId.IsNull;
            var objectIdIsValid = !objectIdIsNull &&
                pending.ObjectId.IsValid &&
                !pending.ObjectId.IsErased;
            if (!objectIdIsValid ||
                verification.GetObject(
                    pending.ObjectId,
                    OpenMode.ForRead,
                    openErased: false) is not Entity entity)
            {
                throw new InvalidOperationException(
                    $"A entidade permanente da ocorrência {pending.OccurrenceId:D} " +
                    "não permaneceu válida após o Commit.");
            }

            committedEntities.Add(new AutoCadInspectionCommittedEntity(
                pending.OccurrenceId,
                entity.GetRXClass().Name,
                pending.ObjectId.ToString(),
                entity.Handle.ToString(),
                objectIdIsNull,
                objectIdIsValid,
                pending.Created));
        }

        verification.Commit();
        return Array.AsReadOnly(committedEntities.ToArray());
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
        var linetypes = (LinetypeTable)transaction.GetObject(
            database.LinetypeTableId,
            OpenMode.ForRead);
        if (linetypes.Has("Continuous"))
        {
            record.LinetypeObjectId = linetypes["Continuous"];
        }

        return record.ObjectId;
    }

    private static ObjectId EnsureArialTextStyle(
        Database database,
        Transaction transaction)
    {
        var table = (TextStyleTable)transaction.GetObject(
            database.TextStyleTableId,
            OpenMode.ForRead);
        if (table.Has(EventTextStyleName))
        {
            return table[EventTextStyleName];
        }

        foreach (ObjectId objectId in table)
        {
            var existing = (TextStyleTableRecord)transaction.GetObject(
                objectId,
                OpenMode.ForRead);
            if (string.Equals(
                    Path.GetFileName(existing.FileName),
                    "arial.ttf",
                    StringComparison.OrdinalIgnoreCase))
            {
                return existing.ObjectId;
            }
        }

        table.UpgradeOpen();
        var record = new TextStyleTableRecord
        {
            Name = EventTextStyleName,
            FileName = "arial.ttf",
            TextSize = 0,
        };
        table.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
        return record.ObjectId;
    }

    private static void EnsureRegisteredApplication(Database database, Transaction transaction)
    {
        var table = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
        if (table.Has(RegisteredApplicationName))
        {
            return;
        }

        table.UpgradeOpen();
        var record = new RegAppTableRecord { Name = RegisteredApplicationName };
        table.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ObjectId>> ReadExisting(
        BlockTableRecord modelSpace,
        Transaction transaction)
    {
        var result = new Dictionary<string, List<ObjectId>>(StringComparer.Ordinal);
        foreach (ObjectId objectId in modelSpace)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead, openErased: false) is not Entity entity)
            {
                continue;
            }

            using var data = entity.GetXDataForApplication(RegisteredApplicationName);
            var key = ReadXData(data, "STABLE_KEY");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!result.TryGetValue(key!, out var objectIds))
            {
                objectIds = [];
                result.Add(key!, objectIds);
            }

            objectIds.Add(objectId);
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<ObjectId>)Array.AsReadOnly(pair.Value.ToArray()),
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<InspectionLayoutObstacle> ReadLayoutObstacles(
        BlockTableRecord modelSpace,
        ObjectId associatedGeometryId,
        Guid reportId,
        Guid sectionId,
        Transaction transaction)
    {
        var obstacles = new List<InspectionLayoutObstacle>();
        foreach (ObjectId objectId in modelSpace)
        {
            if (objectId == associatedGeometryId ||
                transaction.GetObject(objectId, OpenMode.ForRead, openErased: false) is not Entity entity)
            {
                continue;
            }

            using var data = entity.GetXDataForApplication(RegisteredApplicationName);
            var belongsToCurrentSection =
                string.Equals(
                    ReadXData(data, "REPORT_ID"),
                    reportId.ToString("D"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    ReadXData(data, "SECTION_ID"),
                    sectionId.ToString("D"),
                    StringComparison.OrdinalIgnoreCase);
            if (belongsToCurrentSection)
            {
                continue;
            }

            try
            {
                var extents = entity.GeometricExtents;
                if (IsFinite(extents.MinPoint.X) &&
                    IsFinite(extents.MinPoint.Y) &&
                    IsFinite(extents.MaxPoint.X) &&
                    IsFinite(extents.MaxPoint.Y))
                {
                    obstacles.Add(new InspectionLayoutObstacle(
                        extents.MinPoint.X,
                        extents.MinPoint.Y,
                        extents.MaxPoint.X,
                        extents.MaxPoint.Y));
                }
            }
            catch (System.Exception exception) when (
                exception is not StackOverflowException and not OutOfMemoryException)
            {
                // Algumas entidades do AutoCAD não expõem extents geométricos.
                // Elas permanecem intactas e só não participam do layout.
                continue;
            }
        }

        return Array.AsReadOnly(obstacles.ToArray());
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private static T? ExistingEntity<T>(
        IReadOnlyList<ObjectId>? objectIds,
        string component,
        Transaction transaction)
        where T : Entity
    {
        if (objectIds is null)
        {
            return null;
        }

        foreach (var objectId in objectIds)
        {
            if (transaction.GetObject(objectId, OpenMode.ForWrite, openErased: false) is not T entity)
            {
                continue;
            }

            using var data = entity.GetXDataForApplication(RegisteredApplicationName);
            if (string.Equals(
                    ReadXData(data, "COMPONENT"),
                    component,
                    StringComparison.Ordinal))
            {
                return entity;
            }
        }

        return null;
    }

    private static void UpsertSectionIdentification(
        Database database,
        BlockTableRecord modelSpace,
        Polyline polyline,
        VideoInspectionSession session,
        InspectionSectionSession section,
        IReadOnlyDictionary<string, IReadOnlyList<ObjectId>> existing,
        ISet<string> desiredKeys,
        ObjectId layerId,
        ObjectId textStyleId,
        Transaction transaction)
    {
        var identification = InspectionAnnotationPlanner.CreateSectionIdentification(section);
        var centerDistance = polyline.Length / 2.0;
        var center = polyline.GetPointAtDist(centerDistance);
        var tangent = polyline.GetFirstDerivative(center);
        var planarLength = Math.Sqrt(tangent.X * tangent.X + tangent.Y * tangent.Y);
        var textRotation = InspectionCalloutLayoutEngine.ReadableReferenceRotation(
            new LinearVector(tangent.X, tangent.Y, tangent.Z));
        var normal = planarLength <= GeometryTolerance
            ? Vector3d.YAxis
            : new Vector3d(-tangent.Y / planarLength, tangent.X / planarLength, 0);
        if (normal.Y < -GeometryTolerance ||
            Math.Abs(normal.Y) <= GeometryTolerance && normal.X < 0)
        {
            normal = -normal;
        }

        UpsertSectionText(
            database,
            modelSpace,
            session,
            section,
            existing,
            desiredKeys,
            SectionNameComponent,
            identification.SectionLine,
            center + normal * SectionIdentificationOffset,
            textRotation,
            layerId,
            textStyleId,
            transaction);
        UpsertSectionText(
            database,
            modelSpace,
            session,
            section,
            existing,
            desiredKeys,
            SectionDiameterComponent,
            identification.DiameterLine,
            center - normal * SectionIdentificationOffset,
            textRotation,
            layerId,
            textStyleId,
            transaction);
    }

    private static void UpsertSectionText(
        Database database,
        BlockTableRecord modelSpace,
        VideoInspectionSession session,
        InspectionSectionSession section,
        IReadOnlyDictionary<string, IReadOnlyList<ObjectId>> existing,
        ISet<string> desiredKeys,
        string component,
        string contents,
        Point3d location,
        double rotation,
        ObjectId layerId,
        ObjectId textStyleId,
        Transaction transaction)
    {
        var stableKey = SectionStableKey(session.ReportId, section.SectionId, component);
        desiredKeys.Add(stableKey);
        existing.TryGetValue(stableKey, out var managedEntities);
        var text = ExistingEntity<MText>(managedEntities, component, transaction);
        if (text is null)
        {
            text = new MText();
            text.SetDatabaseDefaults(database);
            modelSpace.AppendEntity(text);
            transaction.AddNewlyCreatedDBObject(text, true);
        }

        ApplySectionText(text, location, contents, rotation, layerId, textStyleId);
        SetSectionMetadata(text, session, section, stableKey, component);
        EraseExcept(managedEntities, new HashSet<ObjectId> { text.ObjectId }, transaction);
    }

    private static string SectionStableKey(Guid reportId, Guid sectionId, string component) =>
        $"VI:{reportId:D}:{sectionId:D}:SECTION:{component}";

    private static string CallStableKey(
        Guid reportId,
        Guid sectionId,
        InspectionAnnotationCall call) => call.IsSyntheticStart
            ? $"VI:{reportId:D}:{sectionId:D}:FILMING_START"
            : VideoInspectionStableKey.Create(
                reportId,
                sectionId,
                call.OccurrenceId,
                call.Anchor.LocationIndex,
                call.Anchor.Role);

    private static PositionedInspectionLocation CreateFilmingStart(
        Polyline polyline,
        InspectionDirection direction)
    {
        var cadDistance = direction == InspectionDirection.Forward ? 0.0 : polyline.Length;
        var anchor = polyline.GetPointAtDist(cadDistance);
        var tangent = Vector3d.XAxis;
        if (polyline.Length > GeometryTolerance)
        {
            var tangentDistance = direction == InspectionDirection.Forward
                ? Math.Min(polyline.Length, 0.000001)
                : Math.Max(0.0, polyline.Length - 0.000001);
            var tangentPoint = polyline.GetPointAtDist(tangentDistance);
            var derivative = polyline.GetFirstDerivative(tangentPoint);
            if (derivative.Length > GeometryTolerance)
            {
                tangent = derivative;
            }
        }

        return new PositionedInspectionLocation(
            0,
            PositionedLocationRole.Start,
            0,
            cadDistance,
            new LinearPoint(anchor.X, anchor.Y, anchor.Z),
            new LinearVector(tangent.X, tangent.Y, tangent.Z));
    }

    private static void ApplyMarker(
        Circle marker,
        Point3d anchor,
        ObjectId layerId)
    {
        marker.LayerId = layerId;
        marker.ColorIndex = ByLayerColorIndex;
        marker.Center = anchor;
        marker.Normal = Vector3d.ZAxis;
        marker.Radius = InspectionCalloutLayoutEngine.MarkerRadius;
    }

    private static void ApplyCalloutPolyline(
        Polyline callout,
        IReadOnlyList<Point3d> vertices,
        ObjectId layerId)
    {
        if (vertices.Count < 2)
        {
            throw new ArgumentException(
                "A polyline do chamado deve possuir ao menos dois vértices.",
                nameof(vertices));
        }

        callout.LayerId = layerId;
        callout.ColorIndex = ByLayerColorIndex;
        callout.Closed = false;
        callout.Normal = Vector3d.ZAxis;
        callout.Elevation = vertices[0].Z;
        while (callout.NumberOfVertices > 0)
        {
            callout.RemoveVertexAt(callout.NumberOfVertices - 1);
        }

        for (var index = 0; index < vertices.Count; index++)
        {
            var vertex = vertices[index];
            callout.AddVertexAt(index, new Point2d(vertex.X, vertex.Y), 0, 0, 0);
        }
    }

    private static void ApplyText(
        MText text,
        Point3d textPoint,
        InspectionAnnotationCall call,
        InspectionCalloutPlacement placement,
        ObjectId layerId,
        ObjectId textStyleId)
    {
        text.LayerId = layerId;
        text.ColorIndex = ByLayerColorIndex;
        text.Contents = MTextContents(call);
        text.Location = textPoint;
        text.TextHeight = TextHeight;
        text.TextStyleId = textStyleId;
        text.Rotation = placement.TextRotationRadians;
        text.Attachment = placement.AttachAtRightEdge
            ? AttachmentPoint.MiddleRight
            : AttachmentPoint.MiddleLeft;
    }

    private static void ApplySectionText(
        MText text,
        Point3d location,
        string contents,
        double rotation,
        ObjectId layerId,
        ObjectId textStyleId)
    {
        text.LayerId = layerId;
        text.ColorIndex = ByLayerColorIndex;
        text.Contents = EscapeMText(contents);
        text.Location = location;
        text.TextHeight = TextHeight;
        text.TextStyleId = textStyleId;
        text.Rotation = rotation;
        text.Attachment = AttachmentPoint.MiddleCenter;
    }

    private static Point3d RelativePoint(
        Point3d exactAnchor,
        LinearPoint plannedAnchor,
        LinearPoint plannedPoint) => exactAnchor + new Vector3d(
            plannedPoint.X - plannedAnchor.X,
            plannedPoint.Y - plannedAnchor.Y,
            plannedPoint.Z - plannedAnchor.Z);

    private static string MTextContents(InspectionAnnotationCall call) =>
        string.Join(
            "\\P",
            InspectionCalloutLayoutEngine.FormatTextLines(call).Select(EscapeMText));

    private static string EscapeMText(string value) => value
        .Replace("\\", "\\\\")
        .Replace("{", "\\{")
        .Replace("}", "\\}");

    private static void SetMetadata(
        Entity entity,
        VideoInspectionSession session,
        InspectionSectionSession section,
        InspectionAnnotationCall call,
        string stableKey,
        string component,
        PositionedInspectionLocation? metadataLocation = null)
    {
        var location = metadataLocation ?? call.Anchor;
        using var data = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegisteredApplicationName),
            Value("SCHEMA_VERSION", InspectionExchangeSchema.CurrentVersion.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
            Value("REPORT_ID", session.ReportId.ToString("D")),
            Value("SECTION_ID", section.SectionId.ToString("D")),
            Value("OCCURRENCE_ID", call.OccurrenceId.ToString("D")),
            Value("LOCATION_INDEX", location.LocationIndex.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
            Value("LOCATION_MODE", call.LocationMode.ToString()),
            Value("ROLE", location.Role.ToString().ToUpperInvariant()),
            Value("INSPECTION_DISTANCE", location.InspectionDistance.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture)),
            Value("CAD_DISTANCE", location.CadDistance.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture)),
            Value("COMPONENT", component),
            Value("STABLE_KEY", stableKey));
        entity.XData = data;
    }

    private static void SetSectionMetadata(
        Entity entity,
        VideoInspectionSession session,
        InspectionSectionSession section,
        string stableKey,
        string component)
    {
        using var data = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegisteredApplicationName),
            Value("SCHEMA_VERSION", InspectionExchangeSchema.CurrentVersion.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
            Value("REPORT_ID", session.ReportId.ToString("D")),
            Value("SECTION_ID", section.SectionId.ToString("D")),
            Value("COMPONENT", component),
            Value("STABLE_KEY", stableKey));
        entity.XData = data;
    }

    private static void EraseExcept(
        IReadOnlyList<ObjectId>? objectIds,
        ISet<ObjectId> retainedIds,
        Transaction transaction)
    {
        if (objectIds is null)
        {
            return;
        }

        foreach (var objectId in objectIds.Where(objectId => !retainedIds.Contains(objectId)))
        {
            Erase(objectId, transaction);
        }
    }

    private static void EraseStaleManagedEntities(
        IReadOnlyDictionary<string, IReadOnlyList<ObjectId>> existing,
        ISet<string> desiredKeys,
        Guid reportId,
        Guid sectionId,
        Transaction transaction)
    {
        foreach (var pair in existing.Where(pair => !desiredKeys.Contains(pair.Key)))
        {
            foreach (var objectId in pair.Value)
            {
                if (transaction.GetObject(objectId, OpenMode.ForRead, openErased: false) is not Entity entity)
                {
                    continue;
                }

                using var data = entity.GetXDataForApplication(RegisteredApplicationName);
                if (string.Equals(
                        ReadXData(data, "REPORT_ID"),
                        reportId.ToString("D"),
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        ReadXData(data, "SECTION_ID"),
                        sectionId.ToString("D"),
                        StringComparison.OrdinalIgnoreCase))
                {
                    entity.UpgradeOpen();
                    entity.Erase();
                }
            }
        }
    }

    private static void Erase(ObjectId objectId, Transaction transaction)
    {
        if (transaction.GetObject(objectId, OpenMode.ForWrite, openErased: false) is Entity entity)
        {
            entity.Erase();
        }
    }

    private static TypedValue Value(string key, string value) =>
        new((int)DxfCode.ExtendedDataAsciiString, $"{key}={value}");

    private static string? ReadXData(ResultBuffer? data, string key)
    {
        if (data is null)
        {
            return null;
        }

        var prefix = key + "=";
        return data.AsArray()
            .Where(value => value.TypeCode == (int)DxfCode.ExtendedDataAsciiString)
            .Select(value => Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture))
            .FirstOrDefault(value => value?.StartsWith(prefix, StringComparison.Ordinal) == true)
            ?.Substring(prefix.Length);
    }
}
