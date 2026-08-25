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
    public const string EventLayerName = "GF-Rede Pluvial";
    public const string EventTextStyleName = "GEO_ARIAL";
    private const short MarkerColorIndex = 1;
    private const short LeaderColorIndex = 2;
    private const short TextColorIndex = 3;
    private const string MarkerComponent = "MARKER";
    private const string LeaderComponent = "LEADER";
    private const string TextComponent = "TEXT";
    private const double TextHeight = InspectionCalloutLayoutEngine.TextHeight;

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
        var calls = InspectionAnnotationPlanner.Create(positionedEvents);
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
                TextColorIndex);
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
                var stableKey = VideoInspectionStableKey.Create(
                    session.ReportId,
                    section.SectionId,
                    call.OccurrenceId,
                    call.Anchor.LocationIndex,
                    call.Anchor.Role);
                desiredKeys.Add(stableKey);
                var anchor = polyline!.GetPointAtDist(call.Anchor.CadDistance);
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

                var leader = ExistingEntity<Line>(
                    managedEntities,
                    LeaderComponent,
                    transaction);
                var leaderCreated = leader is null;
                if (leader is null)
                {
                    leader = new Line();
                    leader.SetDatabaseDefaults(document.Database);
                    modelSpace.AppendEntity(leader);
                    transaction.AddNewlyCreatedDBObject(leader, true);
                }

                ApplyLeader(leader, anchor, leaderEndPoint, eventLayerId);
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
                if (existed)
                {
                    updated++;
                }
                else
                {
                    created++;
                }
            }

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
        if (table.Has(name))
        {
            return table[name];
        }

        table.UpgradeOpen();
        var record = new LayerTableRecord
        {
            Name = name,
            Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex),
        };
        table.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
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

    private static void ApplyMarker(
        Circle marker,
        Point3d anchor,
        ObjectId layerId)
    {
        marker.LayerId = layerId;
        marker.ColorIndex = MarkerColorIndex;
        marker.Center = anchor;
        marker.Normal = Vector3d.ZAxis;
        marker.Radius = InspectionCalloutLayoutEngine.MarkerRadius;
    }

    private static void ApplyLeader(
        Line leader,
        Point3d anchor,
        Point3d leaderEndPoint,
        ObjectId layerId)
    {
        leader.LayerId = layerId;
        leader.ColorIndex = LeaderColorIndex;
        leader.StartPoint = anchor;
        leader.EndPoint = leaderEndPoint;
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
        text.ColorIndex = TextColorIndex;
        text.Contents = MTextContents(call);
        text.Location = textPoint;
        text.TextHeight = TextHeight;
        text.TextStyleId = textStyleId;
        text.Rotation = placement.TextRotationRadians;
        text.Attachment = placement.AttachAtRightEdge
            ? AttachmentPoint.MiddleRight
            : AttachmentPoint.MiddleLeft;
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
        string component)
    {
        using var data = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegisteredApplicationName),
            Value("SCHEMA_VERSION", InspectionExchangeSchema.CurrentVersion.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
            Value("REPORT_ID", session.ReportId.ToString("D")),
            Value("SECTION_ID", section.SectionId.ToString("D")),
            Value("OCCURRENCE_ID", call.OccurrenceId.ToString("D")),
            Value("LOCATION_INDEX", call.Anchor.LocationIndex.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
            Value("LOCATION_MODE", call.LocationMode.ToString()),
            Value("ROLE", call.Anchor.Role.ToString().ToUpperInvariant()),
            Value("INSPECTION_DISTANCE", call.Anchor.InspectionDistance.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture)),
            Value("CAD_DISTANCE", call.Anchor.CadDistance.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture)),
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
