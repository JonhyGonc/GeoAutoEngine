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
    private const short EventColorIndex = 3;
    private const double TextHeight = InspectionCalloutLayoutEngine.TextHeight;
    private const double DoglegLength = 0.75;

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
        var placements = InspectionCalloutLayoutEngine.Arrange(calls);
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
                EventColorIndex);
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
                var textPoint = TextPoint(anchor, call.Anchor.Position, placement.TextPoint);
                var existed = existing.TryGetValue(stableKey, out var managedEntities);
                var mleader = ExistingMLeader(managedEntities, transaction);
                var duplicates = managedEntities;
                if (mleader is null)
                {
                    Erase(managedEntities, transaction);
                    duplicates = null;
                    mleader = new MLeader();
                    mleader.SetDatabaseDefaults(document.Database);
                    modelSpace.AppendEntity(mleader);
                    transaction.AddNewlyCreatedDBObject(mleader, true);
                }

                ApplyMLeader(
                    mleader,
                    anchor,
                    textPoint,
                    call,
                    placement,
                    eventLayerId,
                    eventTextStyleId);
                SetMetadata(mleader, session, section, call, stableKey);
                EraseDuplicates(duplicates, mleader.ObjectId, transaction);
                pendingEntities.Add((call.OccurrenceId, mleader.ObjectId, !existed));
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

    private static MLeader? ExistingMLeader(
        IReadOnlyList<ObjectId>? objectIds,
        Transaction transaction)
    {
        if (objectIds is null)
        {
            return null;
        }

        foreach (var objectId in objectIds)
        {
            if (transaction.GetObject(objectId, OpenMode.ForWrite, openErased: false) is MLeader mleader)
            {
                return mleader;
            }
        }

        return null;
    }

    private static void ApplyMLeader(
        MLeader mleader,
        Point3d anchor,
        Point3d textPoint,
        InspectionAnnotationCall call,
        InspectionCalloutPlacement placement,
        ObjectId layerId,
        ObjectId textStyleId)
    {
        foreach (var oldLeaderIndex in mleader.GetLeaderIndexes().Cast<int>().ToArray())
        {
            mleader.RemoveLeader(oldLeaderIndex);
        }

        mleader.LayerId = layerId;
        mleader.ColorIndex = EventColorIndex;
        mleader.ContentType = ContentType.MTextContent;
        mleader.EnableLanding = true;
        mleader.EnableDogleg = true;
        mleader.DoglegLength = DoglegLength;
        mleader.LandingGap = 0.10;
        mleader.TextHeight = TextHeight;
        mleader.TextStyleId = textStyleId;
        var leaderIndex = mleader.AddLeader();
        var lineIndex = mleader.AddLeaderLine(leaderIndex);
        mleader.AddFirstVertex(lineIndex, anchor);
        mleader.AddLastVertex(lineIndex, textPoint);
        using var text = new MText
        {
            Contents = MTextContents(call),
            Location = textPoint,
            TextHeight = TextHeight,
            TextStyleId = textStyleId,
            ColorIndex = EventColorIndex,
            Rotation = placement.TextRotationRadians,
            Attachment = placement.AttachAtRightEdge
                ? AttachmentPoint.MiddleRight
                : AttachmentPoint.MiddleLeft,
        };
        mleader.MText = text;
    }

    private static Point3d TextPoint(
        Point3d exactAnchor,
        LinearPoint plannedAnchor,
        LinearPoint plannedTextPoint) => exactAnchor + new Vector3d(
            plannedTextPoint.X - plannedAnchor.X,
            plannedTextPoint.Y - plannedAnchor.Y,
            plannedTextPoint.Z - plannedAnchor.Z);

    private static string MTextContents(InspectionAnnotationCall call)
    {
        var lines = new List<string>
        {
            call.LocationLine,
            call.Cause,
        };
        if (!string.IsNullOrWhiteSpace(call.PositionLine))
        {
            lines.Add(call.PositionLine!);
        }

        return string.Join("\\P", lines.Select(EscapeMText));
    }

    private static string EscapeMText(string value) => value
        .Replace("\\", "\\\\")
        .Replace("{", "\\{")
        .Replace("}", "\\}");

    private static void SetMetadata(
        Entity entity,
        VideoInspectionSession session,
        InspectionSectionSession section,
        InspectionAnnotationCall call,
        string stableKey)
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
            Value("STABLE_KEY", stableKey));
        entity.XData = data;
    }

    private static void EraseDuplicates(
        IReadOnlyList<ObjectId>? objectIds,
        ObjectId retainedId,
        Transaction transaction)
    {
        if (objectIds is null)
        {
            return;
        }

        foreach (var objectId in objectIds.Where(objectId => objectId != retainedId))
        {
            Erase(objectId, transaction);
        }
    }

    private static void Erase(
        IReadOnlyList<ObjectId>? objectIds,
        Transaction transaction)
    {
        if (objectIds is null)
        {
            return;
        }

        foreach (var objectId in objectIds)
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
                if (string.Equals(ReadXData(data, "REPORT_ID"), reportId.ToString("D"), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ReadXData(data, "SECTION_ID"), sectionId.ToString("D"), StringComparison.OrdinalIgnoreCase))
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
