using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeoAutoEngine.Application;
using GeoAutoEngine.Domain;
using GeoAutoEngine.Domain.AnnotationLayout;

namespace GeoAutoEngine.Cad2026;

internal sealed record ResolvedTextPlacement(
    TextPlanItem Item,
    Point3d Position,
    double Rotation,
    string Layer,
    bool UsedFallback,
    string? SourceLayer,
    double? Distance,
    string CandidateId,
    LayoutStatus LayoutStatus,
    LayoutDecisionKind DecisionKind);

internal sealed record ResolvedBlockPlacement(
    BlockPlanItem Item,
    Point3d Position,
    double Rotation,
    bool UsedFallback,
    string? SourceLayer,
    double? Distance);

internal sealed class AutoCadNetworkGeometryResolver : IDisposable
{
    private const double PrimaryRadius = 0.10;
    private const double SecondaryRadius = 0.50;
    private const double MaximumRadius = 0.70;
    private const double ExtendedOwnNetworkRadius = 5.00;
    private const double AmbiguityTolerance = 0.10;
    private const double TangentTolerance = 1.0e-12;
    private const double TangentSampleDistance = 0.05;
    private const double GridSize = ExtendedOwnNetworkRadius;
    private const double LongitudinalStep = 0.50;
    private const double MaximumLongitudinalOffset = 2.00;
    private const double PerpendicularStep = 0.10;
    private const double MaximumAdditionalPerpendicularOffset = 0.30;
    private const double TextCollisionMarginFactor = 0.30;
    private const double CurveCollisionMarginFactor = 0.20;
    private const double PlannedManholeProtectionRadius = 1.50;
    private const int MaximumIndexedCellsPerCurve = 10_000;
    private const double RealityAuditRadius = 5.00;
    private const double CurveSnapshotStep = 0.25;
    private const int MaximumCurveSnapshotSegments = 2_048;
    private static readonly double[] ProgressiveRadii = [PrimaryRadius, SecondaryRadius, MaximumRadius];
    private static readonly HashSet<string> SupportedTypes =
    [
        "AcDbLine",
        "AcDbPolyline",
        "AcDb2dPolyline",
        "AcDb3dPolyline",
        "AcDbArc",
        "AcDbSpline",
    ];

    private readonly List<CurveEntry> _entries = [];
    private readonly Dictionary<(long X, long Y), List<CurveEntry>> _grid = [];
    private readonly List<CurveEntry> _globalEntries = [];
    private readonly List<Box2> _existingManholeObstacles = [];
    private readonly List<LayoutObstacle> _capturedObstacles = [];
    private readonly Dictionary<string, ExistingTextSnapshot> _existingGeneratedTexts = new(StringComparer.Ordinal);
    private readonly List<LayoutRealityEntity> _realityEntities = [];
    private readonly string? _realityAuditPath = Environment.GetEnvironmentVariable(
        "GEOAUTOENGINE_ANNOTATION_REALITY_AUDIT_PATH");

    internal AnnotationLayoutStatistics? LastLayoutStatistics { get; private set; }

    internal AnnotationRealityAudit? LastRealityAudit { get; private set; }

    internal AutoCadNetworkGeometryResolver(BlockTableRecord currentSpace, Transaction transaction)
    {
        ScanBlock(currentSpace, transaction, Matrix3d.Identity, new HashSet<ObjectId>(), "MODEL", 0);
    }

    internal IReadOnlyList<ResolvedTextPlacement> ResolveTexts(
        IEnumerable<TextPlanItem> items,
        ICollection<Diagnostic> diagnostics)
    {
        var snapshot = items.ToArray();
        try
        {
            return ResolveTextsV2(snapshot, diagnostics);
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            diagnostics.Add(new Diagnostic(
                "ANNOTATION_LAYOUT_LEGACY_FALLBACK",
                $"O snapshot do layout V2 falhou ({exception.Message}); aplicada a contingência geométrica anterior.",
                DiagnosticSeverity.Warning,
                DiagnosticScope.Batch));
            return ResolveTextsLegacy(snapshot, diagnostics);
        }
    }

    internal void AddPlannedObstacles(
        IEnumerable<BlockPlanItem> blocks,
        IEnumerable<IPolygonStructurePlanItem> structures)
    {
        foreach (var block in blocks)
        {
            var radius = string.Equals(block.Family, "PV", StringComparison.Ordinal)
                ? PlannedManholeProtectionRadius
                : Math.Max(0.50, Math.Max(block.Scale.X, block.Scale.Y) * 0.75);
            var bounds = new Box2(
                block.Position.X - radius,
                block.Position.Y - radius,
                block.Position.X + radius,
                block.Position.Y + radius);
            _existingManholeObstacles.Add(bounds);
            _capturedObstacles.Add(ToLayoutObstacle(
                $"planned-block:{block.Context.StableKey}",
                LayoutObstacleKind.Block,
                bounds,
                LayoutObstacleMeasurementSource.PlannedConservativeBounds));
        }

        foreach (var structure in structures)
        {
            var bounds = new Box2(
                structure.Vertices.Min(point => point.X),
                structure.Vertices.Min(point => point.Y),
                structure.Vertices.Max(point => point.X),
                structure.Vertices.Max(point => point.Y));
            _capturedObstacles.Add(ToLayoutObstacle(
                $"planned-structure:{structure.Context.StableKey}",
                LayoutObstacleKind.Structure,
                bounds,
                LayoutObstacleMeasurementSource.PlannedConservativeBounds));
        }
    }

    internal void AddPlannedBlockObstacles(IEnumerable<BlockPlanItem> blocks) =>
        AddPlannedObstacles(blocks, []);

    private IReadOnlyList<ResolvedTextPlacement> ResolveTextsV2(
        IReadOnlyList<TextPlanItem> items,
        ICollection<Diagnostic> diagnostics)
    {
        var fixedPlacements = items.Where(item => item.RotationMode == PlanRotationMode.Fixed)
            .Select(Fixed)
            .ToArray();
        var managed = items.Where(item => item.RotationMode == PlanRotationMode.FollowNetwork).ToArray();
        if (managed.Length == 0)
        {
            LastLayoutStatistics = new AnnotationLayoutStatistics(0, 0, 0, 0, 0);
            return fixedPlacements;
        }

        var managedKeys = new HashSet<string>(managed.Select(item => item.Context.StableKey), StringComparer.Ordinal);
        var obstacles = _capturedObstacles
            .Where(obstacle => obstacle.OwnerItemId is null || !managedKeys.Contains(obstacle.OwnerItemId))
            .Concat(items.Where(item => item.RotationMode == PlanRotationMode.Fixed).Select(FixedTextObstacle))
            .ToArray();
        var scene = new LayoutScene(SnapshotNetworks(), obstacles);
        var itemById = managed.ToDictionary(item => item.Context.StableKey, StringComparer.Ordinal);
        var legacyDiagnostics = new List<Diagnostic>();
        var legacyByGroup = ResolveTextsLegacy(managed, legacyDiagnostics)
            .GroupBy(placement => GroupKey(placement.Item), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.Item.Kind).ToArray(), StringComparer.Ordinal);
        var requests = managed
            .GroupBy(GroupKey, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => CreateLayoutRequest(
                group.Key,
                group.OrderBy(item => item.Kind).ToArray(),
                legacyByGroup[group.Key]))
            .ToArray();
        var result = new AnnotationLayoutEngine().Layout(requests, scene);
        LastLayoutStatistics = result.Statistics;
        if (!string.IsNullOrWhiteSpace(_realityAuditPath))
        {
            LastRealityAudit = BuildRealityAudit(requests, result);
            TryWriteRealityAudit(LastRealityAudit, diagnostics);
        }

        var resolved = new List<ResolvedTextPlacement>(managed.Length + fixedPlacements.Length);
        resolved.AddRange(fixedPlacements);
        foreach (var decision in result.Decisions)
        {
            var destinationLayer = decision.AssociatedNetworkId is null
                ? null
                : NetworkCadCatalog.All.FirstOrDefault(definition =>
                    string.Equals(definition.NetworkId, decision.AssociatedNetworkId, StringComparison.Ordinal))?.TextLayer;
            foreach (var placement in decision.Placements)
            {
                var item = itemById[placement.ItemId];
                resolved.Add(new ResolvedTextPlacement(
                    item,
                    new Point3d(placement.Position.X, placement.Position.Y, item.Anchor.Z),
                    placement.RotationRadians,
                    destinationLayer ?? item.Context.Layer,
                    decision.Status == LayoutStatus.RequiresReview,
                    decision.AssociatedNetworkLayer,
                    null,
                    decision.CandidateId,
                    decision.Status,
                    decision.DecisionKind));
            }

            if (decision.Status == LayoutStatus.RequiresReview)
            {
                var groupItems = decision.Placements.Select(placement => itemById[placement.ItemId]).ToArray();
                var reasons = decision.Score.Reasons.Count == 0
                    ? "sem associação ou candidato aceitável"
                    : string.Join(", ", decision.Score.Reasons.Take(3));
                diagnostics.Add(new Diagnostic(
                    "ANNOTATION_LAYOUT_UNRESOLVED",
                    $"Anotação {decision.AnnotationId} requer revisão; melhor candidato " +
                    $"{decision.CandidateId}, score {decision.Score.Total:F2}: {reasons}.",
                    DiagnosticSeverity.Warning,
                    DiagnosticScope.Record,
                    origins: groupItems.SelectMany(item => item.Context.Origins).Distinct()));
            }
        }

        return items.Select(item => resolved.Single(placement => placement.Item.Context.Id == item.Context.Id)).ToArray();
    }

    private AnnotationLayoutRequest CreateLayoutRequest(
        string annotationId,
        IReadOnlyList<TextPlanItem> items,
        IReadOnlyList<ResolvedTextPlacement> legacyPlacements)
    {
        var depth = items.FirstOrDefault(item => item.Kind == TextPlanKind.Depth)
            ?? throw new InvalidOperationException($"Annotation group {annotationId} has no depth text.");
        var diameter = items.FirstOrDefault(item => item.Kind == TextPlanKind.Diameter);
        var definition = NetworkCadCatalog.All.FirstOrDefault(candidate =>
                depth.ReferenceCurveLayers.Any(layer => candidate.CurveLayers.Any(curveLayer => LayerMatches(layer, curveLayer))))
            ?? NetworkCadCatalog.Resolve("Não identificada");
        var depthElement = Element(depth, AnnotationTextRole.Depth);
        var diameterElement = diameter is null ? null : Element(diameter, AnnotationTextRole.Diameter);
        var previous = PreviousLayout(items);
        var legacy = LegacyLayout(depth, legacyPlacements);
        return new AnnotationLayoutRequest(
            annotationId,
            new LayoutPoint2(depth.Anchor.X, depth.Anchor.Y),
            definition.NetworkId,
            depth.ReferenceCurveLayers,
            depthElement,
            diameterElement,
            previous,
            legacy);
    }

    private static LegacyAnnotationLayout LegacyLayout(
        TextPlanItem depth,
        IReadOnlyList<ResolvedTextPlacement> placements)
    {
        var metrics = new ApproximateTextMetricsProvider();
        var corePlacements = placements.Select(placement =>
        {
            var role = placement.Item.Kind == TextPlanKind.Depth
                ? AnnotationTextRole.Depth
                : AnnotationTextRole.Diameter;
            var element = Element(placement.Item, role);
            var position = new LayoutPoint2(placement.Position.X, placement.Position.Y);
            var footprint = TextFootprint.Create(element, position, placement.Rotation, metrics.Measure(element));
            return new LayoutTextPlacement(
                placement.Item.Context.StableKey,
                role,
                position,
                placement.Rotation,
                footprint);
        }).ToArray();
        var rotation = placements[0].Rotation;
        return new LegacyAnnotationLayout(
            "v1-real",
            new LayoutPoint2(depth.Anchor.X, depth.Anchor.Y),
            new LayoutVector2(Math.Cos(rotation), Math.Sin(rotation)),
            corePlacements);
    }

    private PreviousAnnotationLayout? PreviousLayout(IReadOnlyList<TextPlanItem> items)
    {
        if (items.Any(item => !_existingGeneratedTexts.ContainsKey(item.Context.StableKey)))
        {
            return null;
        }

        var placements = items.Select(item =>
        {
            var previous = _existingGeneratedTexts[item.Context.StableKey];
            var role = item.Kind == TextPlanKind.Depth ? AnnotationTextRole.Depth : AnnotationTextRole.Diameter;
            var footprint = new TextFootprint(item.Context.StableKey, previous.Footprint, approximate: true);
            return new LayoutTextPlacement(
                item.Context.StableKey,
                role,
                previous.Position,
                previous.Rotation,
                footprint);
        }).ToArray();
        return new PreviousAnnotationLayout("existing", placements);
    }

    private static AnnotationTextElement Element(TextPlanItem item, AnnotationTextRole role) =>
        new(
            item.Context.StableKey,
            role,
            item.Content,
            item.Height,
            item.PerpendicularOffset);

    private static LayoutObstacle FixedTextObstacle(TextPlanItem item)
    {
        var placement = Fixed(item);
        var width = Math.Max(item.Height * 0.60, item.Content.Length * item.Height * 0.60);
        var tangent = new LayoutVector2(Math.Cos(placement.Rotation), Math.Sin(placement.Rotation));
        var normal = new LayoutVector2(-tangent.Y, tangent.X);
        var insertion = new LayoutPoint2(placement.Position.X, placement.Position.Y);
        var center = insertion + (tangent * (width / 2)) + (normal * (item.Height / 2));
        return new LayoutObstacle(
            $"planned-fixed-text:{item.Context.StableKey}",
            LayoutObstacleKind.PlannedText,
            new OrientedRect(center, width / 2, item.Height / 2, placement.Rotation),
            item.Context.StableKey,
            LayoutObstacleMeasurementSource.PlannedApproximateMetrics);
    }

    private IReadOnlyList<LayoutNetworkPath> SnapshotNetworks()
    {
        var result = new List<LayoutNetworkPath>(_entries.Count);
        foreach (var entry in _entries.OrderBy(item => item.Path, StringComparer.Ordinal))
        {
            var vertices = SnapshotCurve(entry.ProjectedCurve);
            if (vertices.Count < 2)
            {
                continue;
            }

            result.Add(new LayoutNetworkPath(
                entry.Path,
                entry.Network.NetworkId,
                entry.Layer,
                vertices));
        }

        return result;
    }

    private static IReadOnlyList<LayoutPoint2> SnapshotCurve(Curve curve)
    {
        try
        {
            var startDistance = curve.GetDistanceAtParameter(curve.StartParam);
            var endDistance = curve.GetDistanceAtParameter(curve.EndParam);
            var length = Math.Abs(endDistance - startDistance);
            var segmentCount = Math.Max(1, Math.Min(
                MaximumCurveSnapshotSegments,
                checked((int)Math.Ceiling(length / CurveSnapshotStep))));
            var vertices = new List<LayoutPoint2>(segmentCount + 1);
            for (var index = 0; index <= segmentCount; index++)
            {
                var distance = startDistance + ((endDistance - startDistance) * index / segmentCount);
                var point = curve.GetPointAtDist(distance);
                vertices.Add(new LayoutPoint2(point.X, point.Y));
            }

            return vertices;
        }
        catch (Exception exception) when (exception is Autodesk.AutoCAD.Runtime.Exception or
            ArgumentException or InvalidOperationException or OverflowException)
        {
            try
            {
                return
                [
                    new LayoutPoint2(curve.StartPoint.X, curve.StartPoint.Y),
                    new LayoutPoint2(curve.EndPoint.X, curve.EndPoint.Y),
                ];
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return [];
            }
        }
    }

    private IReadOnlyList<ResolvedTextPlacement> ResolveTextsLegacy(
        IReadOnlyList<TextPlanItem> items,
        ICollection<Diagnostic> diagnostics)
    {
        var result = new List<ResolvedTextPlacement>();
        foreach (var item in items)
        {
            if (item.RotationMode == PlanRotationMode.Fixed)
            {
                result.Add(Fixed(item));
                continue;
            }

            var association = Resolve(item.Anchor, item.ReferenceCurveLayers, allowCrossNetwork: true, inferUnknownNetwork: true);
            if (association is null)
            {
                diagnostics.Add(new Diagnostic(
                    "CAD_NETWORK_GEOMETRY_FALLBACK",
                    $"Sem associação geométrica segura para {item.Content}; aplicada contingência horizontal.",
                    DiagnosticSeverity.Warning,
                    DiagnosticScope.Record,
                    origins: item.Context.Origins));
                result.Add(Fallback(item));
                continue;
            }

            var basePoint = association.ClosestPoint;
            var position = new Point3d(
                basePoint.X + (association.NormalX * item.PerpendicularOffset),
                basePoint.Y + (association.NormalY * item.PerpendicularOffset),
                item.Anchor.Z);
            result.Add(new ResolvedTextPlacement(
                item,
                position,
                association.ReadableRotation,
                association.DestinationTextLayer ?? item.Context.Layer,
                false,
                association.Entry.Layer,
                association.Distance,
                "legacy",
                LayoutStatus.Resolved,
                LayoutDecisionKind.KeepLegacy));
        }

        return ApplyTextPositioning(result);
    }

    internal IReadOnlyList<ResolvedBlockPlacement> ResolveBlocks(
        IEnumerable<BlockPlanItem> items,
        ICollection<Diagnostic> diagnostics)
    {
        var result = new List<ResolvedBlockPlacement>();
        foreach (var item in items)
        {
            if (item.RotationMode == PlanRotationMode.Fixed)
            {
                result.Add(new ResolvedBlockPlacement(
                    item,
                    Point(item.Position),
                    item.FixedRotationRadians,
                    false,
                    null,
                    null));
                continue;
            }

            var association = Resolve(item.Position, item.ReferenceCurveLayers, allowCrossNetwork: false, inferUnknownNetwork: false);
            if (association is null)
            {
                diagnostics.Add(new Diagnostic(
                    "CAD_BLOCK_GEOMETRY_FALLBACK",
                    $"Sem rede elegível para o bloco {item.BlockName}; mantida a rotação fixa configurada.",
                    DiagnosticSeverity.Warning,
                    DiagnosticScope.Record,
                    origins: item.Context.Origins));
                result.Add(new ResolvedBlockPlacement(
                    item,
                    Point(item.Position),
                    item.FixedRotationRadians,
                    true,
                    null,
                    null));
                continue;
            }

            result.Add(new ResolvedBlockPlacement(
                item,
                Point(item.Position),
                association.ReadableRotation,
                false,
                association.Entry.Layer,
                association.Distance));
        }

        return result;
    }

    public void Dispose()
    {
        foreach (var entry in _entries)
        {
            entry.ProjectedCurve.Dispose();
        }
    }

    private Association? Resolve(
        PlanPoint3 source,
        IReadOnlyList<string> allowedLayers,
        bool allowCrossNetwork,
        bool inferUnknownNetwork)
    {
        var candidates = Query(source.X, source.Y)
            .Select(entry => Evaluate(entry, source.X, source.Y, allowedLayers))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var unknown = inferUnknownNetwork && allowedLayers.Any(layer =>
            LayerMatches(layer, NetworkCadCatalog.Resolve("Não identificada").TextLayer));
        if (unknown)
        {
            var inferred = candidates
                .Where(candidate => candidate.Network is not null && candidate.Network.NetworkId != "nao_identificada" && candidate.Distance <= PrimaryRadius)
                .OrderBy(CandidateSortKey)
                .ToArray();
            if (inferred.Length > 0)
            {
                var firstNetwork = inferred[0].Network!;
                var ambiguous = inferred.Skip(1).Any(candidate =>
                    candidate.Network!.NetworkId != firstNetwork.NetworkId &&
                    Math.Abs(candidate.Distance - inferred[0].Distance) <= AmbiguityTolerance);
                if (ambiguous)
                {
                    return null;
                }

                return ToAssociation(inferred[0], PrimaryRadius, firstNetwork.TextLayer);
            }
        }

        var own = SelectProgressive(candidates.Where(candidate => candidate.NetworkMatch));
        if (own is not null)
        {
            return own;
        }

        var extended = candidates
            .Where(candidate => candidate.NetworkMatch && candidate.Distance <= ExtendedOwnNetworkRadius)
            .OrderBy(CandidateSortKey)
            .FirstOrDefault();
        if (extended is not null)
        {
            return ToAssociation(extended, ExtendedOwnNetworkRadius, null);
        }

        return allowCrossNetwork
            ? SelectProgressive(candidates.Where(candidate => !candidate.NetworkMatch))
            : null;
    }

    private static Association? SelectProgressive(IEnumerable<Candidate> source)
    {
        var candidates = source.ToArray();
        foreach (var radius in ProgressiveRadii)
        {
            var selected = candidates
                .Where(candidate => candidate.Distance <= radius)
                .OrderBy(CandidateSortKey)
                .FirstOrDefault();
            if (selected is not null)
            {
                return ToAssociation(selected, radius, null);
            }
        }

        return null;
    }

    private static Candidate? Evaluate(CurveEntry entry, double x, double y, IReadOnlyList<string> allowedLayers)
    {
        try
        {
            var query = new Point3d(x, y, 0);
            var closest = entry.ProjectedCurve.GetClosestPointTo(query, extend: false);
            var dx = closest.X - x;
            var dy = closest.Y - y;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (!NumberCompatibility.IsFinite(distance) || distance > ExtendedOwnNetworkRadius)
            {
                return null;
            }

            var tangent = Tangent(entry.ProjectedCurve, closest);
            if (tangent is null)
            {
                return null;
            }

            var exact = allowedLayers.Any(layer => ExactLayer(entry.Layer, layer));
            var networkMatch = exact || allowedLayers.Any(layer => LayerMatches(entry.Layer, layer));
            return new Candidate(entry, closest, distance, tangent.Value.X, tangent.Value.Y, networkMatch, exact, entry.Network);
        }
        catch (Exception exception) when (exception is Autodesk.AutoCAD.Runtime.Exception or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static (double X, double Y)? Tangent(Curve curve, Point3d closest)
    {
        try
        {
            var parameter = curve.GetParameterAtPoint(closest);
            var derivative = curve.GetFirstDerivative(parameter);
            var normalized = Normalize(derivative.X, derivative.Y);
            if (normalized is not null)
            {
                return normalized;
            }

            var distance = curve.GetDistanceAtParameter(parameter);
            var endDistance = curve.GetDistanceAtParameter(curve.EndParam);
            var before = distance > TangentTolerance
                ? curve.GetPointAtDist(Math.Max(0, distance - TangentSampleDistance))
                : (Point3d?)null;
            var after = distance < endDistance - TangentTolerance
                ? curve.GetPointAtDist(Math.Min(endDistance, distance + TangentSampleDistance))
                : (Point3d?)null;
            if (before.HasValue && after.HasValue)
            {
                return Normalize(after.Value.X - before.Value.X, after.Value.Y - before.Value.Y);
            }

            return before.HasValue
                ? Normalize(closest.X - before.Value.X, closest.Y - before.Value.Y)
                : after.HasValue
                    ? Normalize(after.Value.X - closest.X, after.Value.Y - closest.Y)
                    : null;
        }
        catch (Exception exception) when (exception is Autodesk.AutoCAD.Runtime.Exception or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static (double X, double Y)? Normalize(double x, double y)
    {
        var length = Math.Sqrt((x * x) + (y * y));
        return NumberCompatibility.IsFinite(length) && length > TangentTolerance ? (x / length, y / length) : null;
    }

    private static Association ToAssociation(Candidate candidate, double radius, string? destinationTextLayer)
    {
        var geometric = Math.Atan2(candidate.TangentY, candidate.TangentX);
        var readable = Math.Atan2(Math.Sin(geometric), Math.Cos(geometric));
        if (readable > Math.PI / 2 || readable <= -Math.PI / 2)
        {
            readable += readable > 0 ? -Math.PI : Math.PI;
        }

        readable = Math.Atan2(Math.Sin(readable), Math.Cos(readable));
        return new Association(
            candidate.Entry,
            candidate.ClosestPoint,
            candidate.Distance,
            readable,
            -Math.Sin(readable),
            Math.Cos(readable),
            radius,
            destinationTextLayer);
    }

    private IEnumerable<CurveEntry> Query(double x, double y)
    {
        var seen = new HashSet<int>();
        foreach (var entry in _globalEntries)
        {
            if (seen.Add(entry.Index))
            {
                yield return entry;
            }
        }

        var minX = Cell(x - ExtendedOwnNetworkRadius);
        var maxX = Cell(x + ExtendedOwnNetworkRadius);
        var minY = Cell(y - ExtendedOwnNetworkRadius);
        var maxY = Cell(y + ExtendedOwnNetworkRadius);
        for (var cellX = minX; cellX <= maxX; cellX++)
        {
            for (var cellY = minY; cellY <= maxY; cellY++)
            {
                if (!_grid.TryGetValue((cellX, cellY), out var entries))
                {
                    continue;
                }

                foreach (var entry in entries)
                {
                    if (seen.Add(entry.Index))
                    {
                        yield return entry;
                    }
                }
            }
        }
    }

    private void ScanBlock(
        BlockTableRecord block,
        Transaction transaction,
        Matrix3d transform,
        HashSet<ObjectId> ancestry,
        string path,
        int depth)
    {
        if (depth > 8 || !ancestry.Add(block.ObjectId))
        {
            return;
        }

        foreach (ObjectId id in block)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, openErased: false) is not Entity entity)
            {
                continue;
            }

            if (depth == 0 && entity is DBText text)
            {
                var captured = TryCaptureTextObstacle(text);
                RecordReality(
                    text,
                    path,
                    captured,
                    captured ? LayoutObstacleKind.ExistingText : null,
                    captured ? "DBTextGeometricExtents" : "MissingReliableGeometricExtents");
                continue;
            }

            if (depth == 0 && entity is MText mtext)
            {
                var captured = TryCaptureVisualTextObstacle(
                    mtext,
                    $"existing-mtext:{mtext.Handle}",
                    LayoutObstacleMeasurementSource.MTextGeometricExtents);
                RecordReality(
                    mtext,
                    path,
                    captured,
                    captured ? LayoutObstacleKind.ExistingText : null,
                    captured ? "MTextGeometricExtents" : "MissingReliableGeometricExtents");
                continue;
            }

            if (depth == 0 && entity is AttributeReference attribute)
            {
                var captured = TryCaptureVisualTextObstacle(
                    attribute,
                    $"existing-attribute:{attribute.Handle}",
                    LayoutObstacleMeasurementSource.CadGeometricExtents);
                RecordReality(
                    attribute,
                    path,
                    captured,
                    captured ? LayoutObstacleKind.ExistingText : null,
                    captured ? "AttributeReferenceGeometricExtents" : "MissingReliableGeometricExtents");
                continue;
            }

            if (depth == 0 && entity is Polyline closedPolyline && IsClosed(closedPolyline) &&
                IsStructureLayer(closedPolyline.Layer))
            {
                var captured = TryCaptureEntityObstacle(
                    closedPolyline,
                    LayoutObstacleKind.Structure,
                    $"structure:{closedPolyline.Handle}",
                    LayoutObstacleMeasurementSource.CadGeometricExtents);
                RecordReality(
                    closedPolyline,
                    path,
                    captured,
                    captured ? LayoutObstacleKind.Structure : null,
                    captured ? "RecognizedStructureLayer" : "MissingReliableGeometricExtents");
                continue;
            }

            if (entity is Curve curve && SupportedTypes.Contains(curve.GetRXClass().Name) && !IsClosed(curve))
            {
                var included = AddCurve(curve, transform, $"{path}/{entity.Handle}");
                if (depth == 0)
                {
                    RecordReality(
                        curve,
                        path,
                        included,
                        null,
                        included ? "NetworkGeometry" : "NonNetworkCurve");
                }

                continue;
            }

            if (entity is BlockReference reference && !reference.BlockTableRecord.IsNull)
            {
                var nested = (BlockTableRecord)transaction.GetObject(reference.BlockTableRecord, OpenMode.ForRead);
                if (depth == 0)
                {
                    var isExternalReference = nested.IsFromExternalReference || nested.IsFromOverlayReference;
                    var isImportant = IsImportantObstacleBlock(nested.Name);
                    var isAnnotated = !isExternalReference && HasVisualAnnotation(reference, nested, transaction);
                    var classification = LayoutRealityPolicy.Classify(
                        reference.GetRXClass().Name,
                        isExternalReference,
                        isAnnotated,
                        isImportant);
                    var captured = false;
                    var reason = classification.Reason;
                    if (classification.IsObstacle)
                    {
                        if (isImportant)
                        {
                            TryAddManholeObstacle(reference);
                        }

                        captured = TryCaptureEntityObstacle(
                            reference,
                            LayoutObstacleKind.Block,
                            $"block:{reference.Handle}",
                            isAnnotated
                                ? LayoutObstacleMeasurementSource.AnnotatedBlockGeometricExtents
                                : LayoutObstacleMeasurementSource.CadGeometricExtents);
                        reason = captured ? classification.Reason : "MissingReliableGeometricExtents";
                    }

                    RecordReality(
                        reference,
                        path,
                        captured,
                        captured ? LayoutObstacleKind.Block : null,
                        reason);
                    RecordBlockAttributes(reference, transaction, path, captured);
                }

                var nestedTransform = reference.BlockTransform.PreMultiplyBy(transform);
                ScanBlock(nested, transaction, nestedTransform, ancestry, $"{path}/{entity.Handle}", depth + 1);
                continue;
            }

            if (depth == 0)
            {
                var civilKind = CivilVisualObstacleKind(entity);
                if (civilKind is not null)
                {
                    var captured = TryCaptureEntityObstacle(
                        entity,
                        civilKind.Value,
                        $"civil:{entity.Handle}",
                        LayoutObstacleMeasurementSource.CivilEntityGeometricExtents);
                    RecordReality(
                        entity,
                        path,
                        captured,
                        captured ? civilKind : null,
                        captured ? "RecognizedCivilVisualEntity" : "MissingReliableGeometricExtents");
                }
                else
                {
                    RecordReality(entity, path, false, null, IgnoredReason(entity));
                }
            }
        }

        ancestry.Remove(block.ObjectId);
    }

    private bool AddCurve(Curve source, Matrix3d transform, string path)
    {
        Curve? transformed = null;
        Curve? projected = null;
        try
        {
            var layer = source.Layer;
            var network = FindNetwork(layer);
            if (network is null)
            {
                return false;
            }

            transformed = (Curve)source.Clone();
            transformed.TransformBy(transform);
            projected = transformed.GetProjectedCurve(new Plane(Point3d.Origin, Vector3d.ZAxis), Vector3d.ZAxis);
            if (projected is null || IsClosed(projected))
            {
                projected?.Dispose();
                return false;
            }

            var extents = projected.GeometricExtents;
            var entry = new CurveEntry(
                _entries.Count,
                projected,
                layer,
                source.GetRXClass().Name,
                path,
                network,
                extents.MinPoint.X,
                extents.MinPoint.Y,
                extents.MaxPoint.X,
                extents.MaxPoint.Y);
            projected = null;
            _entries.Add(entry);
            Index(entry);
            return true;
        }
        catch (Exception exception) when (exception is Autodesk.AutoCAD.Runtime.Exception or ArgumentException or InvalidOperationException)
        {
            projected?.Dispose();
            return false;
        }
        finally
        {
            transformed?.Dispose();
        }
    }

    private void Index(CurveEntry entry)
    {
        var minX = Cell(entry.MinX);
        var maxX = Cell(entry.MaxX);
        var minY = Cell(entry.MinY);
        var maxY = Cell(entry.MaxY);
        var cells = ((maxX - minX) + 1L) * ((maxY - minY) + 1L);
        if (cells > MaximumIndexedCellsPerCurve)
        {
            _globalEntries.Add(entry);
            return;
        }

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                if (!_grid.TryGetValue((x, y), out var bucket))
                {
                    bucket = [];
                    _grid.Add((x, y), bucket);
                }

                bucket.Add(entry);
            }
        }
    }

    private IReadOnlyList<ResolvedTextPlacement> ApplyTextPositioning(IReadOnlyList<ResolvedTextPlacement> source)
    {
        var output = source.ToDictionary(item => item.Item.Context.Id);
        var occupied = new List<Box2>(_existingManholeObstacles);
        var groups = source
            .Where(item => item.Item.RotationMode == PlanRotationMode.FollowNetwork)
            .GroupBy(item => GroupKey(item.Item))
            .OrderBy(group => group.Min(item => item.Item.Context.Id.Value))
            .ToArray();

        foreach (var group in groups)
        {
            var original = group.OrderBy(item => item.Item.Kind).ToArray();
            var perpendicularCandidates = Values(PerpendicularStep, MaximumAdditionalPerpendicularOffset, signed: false);
            var perpendicular = ChooseBest(
                perpendicularCandidates.Select(value => Shift(original, 0, value)).ToArray(),
                [],
                checkOccupied: false);
            var longitudinalCandidates = Values(LongitudinalStep, MaximumLongitudinalOffset, signed: true);
            var final = ChooseBest(
                longitudinalCandidates.Select(value => Shift(perpendicular.Items, value, 0)).ToArray(),
                occupied,
                checkOccupied: true).Items;

            foreach (var item in final)
            {
                output[item.Item.Context.Id] = item;
                occupied.Add(TextBox(item).Expand(item.Item.Height * TextCollisionMarginFactor));
            }
        }

        return source.Select(item => output[item.Item.Context.Id]).ToArray();
    }

    private CandidatePlacement ChooseBest(
        IReadOnlyList<ResolvedTextPlacement[]> candidates,
        IReadOnlyList<Box2> occupied,
        bool checkOccupied)
    {
        CandidatePlacement? best = null;
        foreach (var items in candidates)
        {
            var curveCollisions = items.Count(item => IntersectsNetworkCurve(
                TextBox(item).Expand(item.Item.Height * CurveCollisionMarginFactor)));
            var textCollisions = checkOccupied
                ? items.Select(item => TextBox(item).Expand(item.Item.Height * TextCollisionMarginFactor))
                    .Count(box => occupied.Any(box.Overlaps))
                : 0;
            var candidate = new CandidatePlacement(items, curveCollisions + textCollisions);
            if (candidate.CollisionCount == 0)
            {
                return candidate;
            }

            if (best is null || candidate.CollisionCount < best.CollisionCount)
            {
                best = candidate;
            }
        }

        return best ?? new CandidatePlacement(candidates[0], 0);
    }

    private bool IntersectsNetworkCurve(Box2 box)
    {
        var center = new Point3d((box.MinX + box.MaxX) / 2, (box.MinY + box.MaxY) / 2, 0);
        foreach (var entry in Query(center.X, center.Y))
        {
            if (!box.Overlaps(new Box2(entry.MinX, entry.MinY, entry.MaxX, entry.MaxY)))
            {
                continue;
            }

            try
            {
                var closest = entry.ProjectedCurve.GetClosestPointTo(center, extend: false);
                if (box.Contains(closest.X, closest.Y) ||
                    box.Contains(entry.ProjectedCurve.StartPoint.X, entry.ProjectedCurve.StartPoint.Y) ||
                    box.Contains(entry.ProjectedCurve.EndPoint.X, entry.ProjectedCurve.EndPoint.Y))
                {
                    return true;
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // Collision is fail-open, matching the frozen legacy policy.
            }
        }

        return false;
    }

    private static ResolvedTextPlacement[] Shift(
        IReadOnlyList<ResolvedTextPlacement> source,
        double longitudinal,
        double additionalPerpendicular)
    {
        return source.Select(item =>
        {
            var tangentX = Math.Cos(item.Rotation);
            var tangentY = Math.Sin(item.Rotation);
            var normalX = -tangentY;
            var normalY = tangentX;
            var side = item.Item.PerpendicularOffset < 0 ? -1 : 1;
            return item with
            {
                Position = new Point3d(
                    item.Position.X + (tangentX * longitudinal) + (normalX * additionalPerpendicular * side),
                    item.Position.Y + (tangentY * longitudinal) + (normalY * additionalPerpendicular * side),
                    item.Position.Z),
            };
        }).ToArray();
    }

    private static Box2 TextBox(ResolvedTextPlacement item)
    {
        var width = Math.Max(item.Item.Height * 0.60, item.Item.Content.Length * item.Item.Height * 0.60);
        var tangentX = Math.Cos(item.Rotation);
        var tangentY = Math.Sin(item.Rotation);
        var normalX = -tangentY;
        var normalY = tangentX;
        var points = new[]
        {
            (item.Position.X, item.Position.Y),
            (item.Position.X + (tangentX * width), item.Position.Y + (tangentY * width)),
            (item.Position.X + (normalX * item.Item.Height), item.Position.Y + (normalY * item.Item.Height)),
            (item.Position.X + (tangentX * width) + (normalX * item.Item.Height),
                item.Position.Y + (tangentY * width) + (normalY * item.Item.Height)),
        };
        return new Box2(
            points.Min(point => point.Item1),
            points.Min(point => point.Item2),
            points.Max(point => point.Item1),
            points.Max(point => point.Item2));
    }

    private static double[] Values(double step, double maximum, bool signed)
    {
        var values = new List<double> { 0 };
        for (var value = step; value <= maximum + 1.0e-9; value += step)
        {
            values.Add(value);
            if (signed)
            {
                values.Add(-value);
            }
        }

        return values.ToArray();
    }

    private static string GroupKey(TextPlanItem item)
    {
        var origin = item.Context.Origins[0];
        return $"{origin.SourceFileId}:{origin.SourceRecordIndex}";
    }

    private void TryAddManholeObstacle(BlockReference reference)
    {
        try
        {
            var extents = reference.GeometricExtents;
            _existingManholeObstacles.Add(new Box2(
                extents.MinPoint.X,
                extents.MinPoint.Y,
                extents.MaxPoint.X,
                extents.MaxPoint.Y).Expand(TextCollisionMarginFactor));
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            _existingManholeObstacles.Add(new Box2(
                reference.Position.X - PlannedManholeProtectionRadius,
                reference.Position.Y - PlannedManholeProtectionRadius,
                reference.Position.X + PlannedManholeProtectionRadius,
                reference.Position.Y + PlannedManholeProtectionRadius));
        }
    }

    private bool TryCaptureTextObstacle(DBText text)
    {
        try
        {
            var bounds = Box(text.GeometricExtents);
            using var data = text.GetXDataForApplication("GEOAUTOENGINE");
            var stableKey = AutoCadDrawingPlanApplier.XDataValue(data, "REGISTRO");
            var ownerItemId = string.IsNullOrWhiteSpace(stableKey) ? null : stableKey;
            var footprint = ToOrientedRect(bounds);
            _capturedObstacles.Add(new LayoutObstacle(
                $"existing-text:{text.Handle}",
                LayoutObstacleKind.ExistingText,
                footprint,
                ownerItemId,
                LayoutObstacleMeasurementSource.ExistingTextGeometricExtents));
            if (ownerItemId is not null)
            {
                var managedFootprint = ManagedTextFootprint(text);
                _existingGeneratedTexts[ownerItemId] = new ExistingTextSnapshot(
                    new LayoutPoint2(text.Position.X, text.Position.Y),
                    LayoutAngles.NormalizeReadable(text.Rotation),
                    managedFootprint);
            }

            return true;
        }
        catch (Exception exception) when (exception is Autodesk.AutoCAD.Runtime.Exception or
            ArgumentException or InvalidOperationException)
        {
            // An entity without reliable extents is omitted from the lightweight obstacle snapshot.
            return false;
        }
    }

    private static OrientedRect ManagedTextFootprint(DBText text)
    {
        var height = Math.Max(LayoutGeometryTolerance.Distance, text.Height);
        var widthFactor = NumberCompatibility.IsFinite(text.WidthFactor) && text.WidthFactor > 0
            ? text.WidthFactor
            : 1;
        var width = Math.Max(height * 0.60, text.TextString.Length * height * 0.60 * widthFactor);
        var rotation = LayoutAngles.NormalizeReadable(text.Rotation);
        var tangent = new LayoutVector2(Math.Cos(rotation), Math.Sin(rotation));
        var normal = new LayoutVector2(-tangent.Y, tangent.X);
        var insertion = new LayoutPoint2(text.Position.X, text.Position.Y);
        var center = insertion + (tangent * (width / 2)) + (normal * (height / 2));
        return new OrientedRect(center, width / 2, height / 2, rotation);
    }

    private bool TryCaptureVisualTextObstacle(
        Entity entity,
        string id,
        LayoutObstacleMeasurementSource measurementSource) =>
        TryCaptureEntityObstacle(entity, LayoutObstacleKind.ExistingText, id, measurementSource);

    private bool TryCaptureEntityObstacle(
        Entity entity,
        LayoutObstacleKind kind,
        string id,
        LayoutObstacleMeasurementSource measurementSource)
    {
        try
        {
            _capturedObstacles.Add(ToLayoutObstacle(
                id,
                kind,
                Box(entity.GeometricExtents),
                measurementSource));
            return true;
        }
        catch (Exception exception) when (exception is Autodesk.AutoCAD.Runtime.Exception or
            ArgumentException or InvalidOperationException)
        {
            // Planned entities still provide conservative fallback bounds elsewhere.
            return false;
        }
    }

    private static bool HasVisualAnnotation(
        BlockReference reference,
        BlockTableRecord definition,
        Transaction transaction)
    {
        foreach (ObjectId id in reference.AttributeCollection)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, openErased: false) is AttributeReference attribute &&
                !attribute.Invisible &&
                !string.IsNullOrWhiteSpace(attribute.TextString))
            {
                return true;
            }
        }

        foreach (ObjectId id in definition)
        {
            var entity = transaction.GetObject(id, OpenMode.ForRead, openErased: false);
            if (entity is DBText or MText || entity is AttributeDefinition definitionAttribute && !definitionAttribute.Invisible)
            {
                return true;
            }
        }

        return false;
    }

    private void RecordBlockAttributes(
        BlockReference reference,
        Transaction transaction,
        string path,
        bool coveredByParent)
    {
        if (string.IsNullOrWhiteSpace(_realityAuditPath))
        {
            return;
        }

        foreach (ObjectId id in reference.AttributeCollection)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, openErased: false) is AttributeReference attribute)
            {
                if (attribute.Invisible || string.IsNullOrWhiteSpace(attribute.TextString))
                {
                    continue;
                }

                RecordReality(
                    attribute,
                    $"{path}/{reference.Handle}",
                    coveredByParent,
                    null,
                    coveredByParent ? "CoveredByParentAnnotatedBlock" : "ParentBlockWasNotCaptured");
            }
        }
    }

    private static LayoutObstacleKind? CivilVisualObstacleKind(Entity entity)
        => LayoutRealityPolicy.Classify(RealityType(entity)).ObstacleKind;

    private static string IgnoredReason(Entity entity)
    {
        return LayoutRealityPolicy.Classify(RealityType(entity)).Reason;
    }

    private void RecordReality(
        Entity entity,
        string path,
        bool included,
        LayoutObstacleKind? obstacleKind,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(_realityAuditPath))
        {
            return;
        }

        _realityEntities.Add(new LayoutRealityEntity(
            RealityType(entity),
            entity.Layer,
            entity.Handle.ToString(),
            path,
            TryRealityBounds(entity),
            included,
            obstacleKind,
            reason));
    }

    private static string RealityType(Entity entity)
    {
        try
        {
            return entity is ProxyEntity proxy && !string.IsNullOrWhiteSpace(proxy.OriginalClassName)
                ? $"AcDbProxyEntity({proxy.OriginalClassName})"
                : entity.GetRXClass().Name;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            return entity.GetType().Name;
        }
    }

    private static LayoutRealityBounds? TryRealityBounds(Entity entity)
    {
        try
        {
            var bounds = Box(entity.GeometricExtents);
            return new LayoutRealityBounds(bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY);
        }
        catch (Exception exception) when (exception is Autodesk.AutoCAD.Runtime.Exception or
            ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private AnnotationRealityAudit BuildRealityAudit(
        IReadOnlyList<AnnotationLayoutRequest> requests,
        AnnotationLayoutBatchResult result)
    {
        var decisionById = result.Decisions.ToDictionary(item => item.AnnotationId, StringComparer.Ordinal);
        var annotations = requests.Select(request =>
        {
            var nearby = _realityEntities
                .Where(entity => entity.Bounds?.DistanceTo(request.Anchor) <= RealityAuditRadius)
                .OrderBy(entity => entity.Bounds?.DistanceTo(request.Anchor) ?? double.PositiveInfinity)
                .ThenBy(entity => entity.Handle, StringComparer.Ordinal)
                .ToArray();
            var decision = decisionById[request.AnnotationId];
            return new AnnotationRealityItem(
                request.AnnotationId,
                request.AnnotationId,
                request.Anchor,
                nearby,
                nearby.Where(entity => entity.IncludedInLayoutScene && entity.ObstacleKind is not null).ToArray(),
                nearby.Where(entity => !entity.IncludedInLayoutScene).ToArray(),
                !decision.LegacyValidation.IsValid,
                decision.LegacyValidation.Details,
                decision.DecisionKind,
                decision.MovementDistance);
        }).ToArray();
        return new AnnotationRealityAudit(
            DateTimeOffset.UtcNow,
            _realityEntities.Count,
            _realityEntities.Count(entity => entity.IncludedInLayoutScene && entity.ObstacleKind is not null),
            _realityEntities.Count(entity => !entity.IncludedInLayoutScene),
            result.Statistics,
            annotations);
    }

    private void TryWriteRealityAudit(AnnotationRealityAudit audit, ICollection<Diagnostic> diagnostics)
    {
        try
        {
            var path = Path.GetFullPath(_realityAuditPath!);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            options.Converters.Add(new JsonStringEnumConverter());
            File.WriteAllText(path, JsonSerializer.Serialize(audit, options), Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException)
        {
            diagnostics.Add(new Diagnostic(
                "ANNOTATION_REALITY_AUDIT_WRITE_FAILED",
                $"NÃ£o foi possÃ­vel gravar a auditoria de layout: {exception.Message}",
                DiagnosticSeverity.Warning,
                DiagnosticScope.Batch));
        }
    }

    private static LayoutObstacle ToLayoutObstacle(
        string id,
        LayoutObstacleKind kind,
        Box2 bounds,
        LayoutObstacleMeasurementSource measurementSource) =>
        new(id, kind, ToOrientedRect(bounds), measurementSource: measurementSource);

    private static OrientedRect ToOrientedRect(Box2 bounds)
    {
        var width = Math.Max(LayoutGeometryTolerance.Distance, bounds.MaxX - bounds.MinX);
        var height = Math.Max(LayoutGeometryTolerance.Distance, bounds.MaxY - bounds.MinY);
        return new OrientedRect(
            new LayoutPoint2((bounds.MinX + bounds.MaxX) / 2, (bounds.MinY + bounds.MaxY) / 2),
            width / 2,
            height / 2,
            0);
    }

    private static Box2 Box(Extents3d extents) =>
        new(extents.MinPoint.X, extents.MinPoint.Y, extents.MaxPoint.X, extents.MaxPoint.Y);

    private static bool IsManholeBlock(string name) =>
        name.StartsWith("pv", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("poço", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("poco", StringComparison.OrdinalIgnoreCase);

    private static bool IsImportantObstacleBlock(string name) =>
        IsManholeBlock(name) ||
        name.StartsWith("poste", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("hidrante", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("rg_", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("grelha", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("boca", StringComparison.OrdinalIgnoreCase);

    private static bool IsStructureLayer(string layer) =>
        layer.Contains("GRELHA", StringComparison.OrdinalIgnoreCase) ||
        layer.Contains("BOCA DE LOBO", StringComparison.OrdinalIgnoreCase);

    private static bool IsClosed(Curve curve)
    {
        try
        {
            return curve.Closed;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            return true;
        }
    }

    private static NetworkCadDefinition? FindNetwork(string layer) =>
        NetworkCadCatalog.All.FirstOrDefault(definition =>
            definition.CurveLayers.Any(candidate => LayerMatches(layer, candidate)));

    private static bool ExactLayer(string actual, string expected) =>
        string.Equals(NormalizeLayer(actual), NormalizeLayer(expected), StringComparison.Ordinal);

    private static bool LayerMatches(string actual, string expected)
    {
        var normalizedActual = NormalizeLayer(actual);
        var normalizedExpected = NormalizeLayer(expected);
        if (string.Equals(normalizedActual, normalizedExpected, StringComparison.Ordinal))
        {
            return true;
        }

        var separator = normalizedActual.LastIndexOf('|');
        return separator >= 0 && string.Equals(normalizedActual.Substring(separator + 1), normalizedExpected, StringComparison.Ordinal);
    }

    private static string NormalizeLayer(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static (double Distance, int ExactRank, int TypeRank, string Path) CandidateSortKey(Candidate candidate) =>
        (candidate.Distance, candidate.ExactLayerMatch ? 0 : 1, TypePriority(candidate.Entry.Type), candidate.Entry.Path);

    private static int TypePriority(string type) => type switch
    {
        "AcDbLine" => 0,
        "AcDbPolyline" => 1,
        "AcDb2dPolyline" => 2,
        "AcDb3dPolyline" => 3,
        "AcDbArc" => 4,
        "AcDbSpline" => 5,
        _ => 99,
    };

    private static long Cell(double coordinate) => checked((long)Math.Floor(coordinate / GridSize));

    private static ResolvedTextPlacement Fixed(TextPlanItem item)
    {
        var sine = Math.Sin(item.FixedRotationRadians);
        var cosine = Math.Cos(item.FixedRotationRadians);
        return new ResolvedTextPlacement(
            item,
            new Point3d(
                item.Anchor.X - (sine * item.PerpendicularOffset),
                item.Anchor.Y + (cosine * item.PerpendicularOffset),
                item.Anchor.Z),
            item.FixedRotationRadians,
            item.Context.Layer,
            false,
            null,
            null,
            "fixed",
            LayoutStatus.Resolved,
            LayoutDecisionKind.KeepLegacy);
    }

    private static ResolvedTextPlacement Fallback(TextPlanItem item) =>
        new(
            item,
            new Point3d(item.Anchor.X, item.Anchor.Y + item.PerpendicularOffset, item.Anchor.Z),
            0,
            item.Context.Layer,
            true,
            null,
            null,
            "legacy-fallback",
            LayoutStatus.RequiresReview,
            LayoutDecisionKind.RequiresReview);

    private static Point3d Point(PlanPoint3 point) => new(point.X, point.Y, point.Z);

    private sealed record CurveEntry(
        int Index,
        Curve ProjectedCurve,
        string Layer,
        string Type,
        string Path,
        NetworkCadDefinition Network,
        double MinX,
        double MinY,
        double MaxX,
        double MaxY);

    private sealed record Candidate(
        CurveEntry Entry,
        Point3d ClosestPoint,
        double Distance,
        double TangentX,
        double TangentY,
        bool NetworkMatch,
        bool ExactLayerMatch,
        NetworkCadDefinition? Network);

    private sealed record Association(
        CurveEntry Entry,
        Point3d ClosestPoint,
        double Distance,
        double ReadableRotation,
        double NormalX,
        double NormalY,
        double SearchRadius,
        string? DestinationTextLayer);

    private sealed record CandidatePlacement(ResolvedTextPlacement[] Items, int CollisionCount);

    private sealed record ExistingTextSnapshot(
        LayoutPoint2 Position,
        double Rotation,
        OrientedRect Footprint);

    private readonly record struct Box2(double MinX, double MinY, double MaxX, double MaxY)
    {
        internal Box2 Expand(double amount) => new(MinX - amount, MinY - amount, MaxX + amount, MaxY + amount);

        internal bool Contains(double x, double y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;

        internal bool Overlaps(Box2 other) =>
            MinX <= other.MaxX && MaxX >= other.MinX && MinY <= other.MaxY && MaxY >= other.MinY;
    }
}
