namespace GeoAutoEngine.Domain.AnnotationLayout;

public enum AnnotationTextRole
{
    Depth,
    Diameter,
}

public enum LayoutObstacleKind
{
    ExistingText,
    PlannedText,
    Block,
    Structure,
    Other,
}

public enum LayoutStatus
{
    Resolved,
    RequiresReview,
}

public enum LayoutDecisionKind
{
    KeepExisting,
    KeepLegacy,
    Corrected,
    RequiresReview,
}

public enum AnnotationLayoutStrategy
{
    HybridSimple,
    HybridV2 = HybridSimple,
    PhaseAFreeScore,
    LegacyOnly,
}

[Flags]
public enum LegacyIssue
{
    None = 0,
    TextCollision = 1 << 0,
    BlockCollision = 1 << 1,
    StructureCollision = 1 << 2,
    OtherHardCollision = 1 << 3,
    InvalidRotation = 1 << 4,
    InsufficientClearance = 1 << 5,
    DepthDiameterCollision = 1 << 6,
    ExcessiveDistance = 1 << 7,
    InvalidGeometry = 1 << 8,
    OrderInvariantViolation = 1 << 9,
    RotationInvariantViolation = 1 << 10,
    SideInvariantViolation = 1 << 11,
    GroupIntegrityViolation = 1 << 12,
}

public enum CorrectionQuality
{
    Excellent,
    Good,
    Acceptable,
    Review,
}

[Flags]
public enum AnnotationGroupInvariantViolation
{
    None = 0,
    Order = 1 << 0,
    Rotation = 1 << 1,
    Side = 1 << 2,
    GroupIntegrity = 1 << 3,
}

public enum LayoutObstacleMeasurementSource
{
    Unknown,
    ExistingTextGeometricExtents,
    MTextGeometricExtents,
    AnnotatedBlockGeometricExtents,
    CivilEntityGeometricExtents,
    CadGeometricExtents,
    PlannedApproximateMetrics,
    PlannedConservativeBounds,
}

public enum LayoutMode
{
    Standard,
    Callout,
}

public sealed record AnnotationTextElement
{
    public AnnotationTextElement(
        string itemId,
        AnnotationTextRole role,
        string content,
        double height,
        double perpendicularOffset,
        double widthFactor = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (!NumberCompatibility.IsFinite(height) || height <= 0 ||
            !NumberCompatibility.IsFinite(perpendicularOffset) ||
            !NumberCompatibility.IsFinite(widthFactor) || widthFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        ItemId = itemId;
        Role = role;
        Content = content;
        Height = height;
        PerpendicularOffset = perpendicularOffset;
        WidthFactor = widthFactor;
    }

    public string ItemId { get; }

    public AnnotationTextRole Role { get; }

    public string Content { get; }

    public double Height { get; }

    public double PerpendicularOffset { get; }

    public double WidthFactor { get; }
}

public sealed record TextMetrics
{
    public TextMetrics(double width, double height, double baseline, bool approximate)
    {
        if (!NumberCompatibility.IsFinite(width) || !NumberCompatibility.IsFinite(height) ||
            !NumberCompatibility.IsFinite(baseline) || width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        Width = width;
        Height = height;
        Baseline = baseline;
        Approximate = approximate;
    }

    public double Width { get; }

    public double Height { get; }

    public double Baseline { get; }

    public bool Approximate { get; }
}

public interface ITextMetricsProvider
{
    TextMetrics Measure(AnnotationTextElement text);
}

public sealed class ApproximateTextMetricsProvider : ITextMetricsProvider
{
    public ApproximateTextMetricsProvider(double averageGlyphWidthFactor = 0.60)
    {
        if (!NumberCompatibility.IsFinite(averageGlyphWidthFactor) || averageGlyphWidthFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(averageGlyphWidthFactor));
        }

        AverageGlyphWidthFactor = averageGlyphWidthFactor;
    }

    public double AverageGlyphWidthFactor { get; }

    public TextMetrics Measure(AnnotationTextElement text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var glyphCount = Math.Max(1, text.Content.Length);
        var width = Math.Max(
            text.Height * AverageGlyphWidthFactor,
            glyphCount * text.Height * AverageGlyphWidthFactor * text.WidthFactor);
        return new TextMetrics(width, text.Height, 0, approximate: true);
    }
}

public sealed record TextFootprint
{
    public TextFootprint(string itemId, OrientedRect rectangle, bool approximate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        ItemId = itemId;
        Rectangle = rectangle;
        Approximate = approximate;
    }

    public string ItemId { get; }

    public OrientedRect Rectangle { get; }

    public bool Approximate { get; }

    public static TextFootprint Create(
        AnnotationTextElement text,
        LayoutPoint2 insertionPoint,
        double rotationRadians,
        TextMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(metrics);
        var tangent = new LayoutVector2(Math.Cos(rotationRadians), Math.Sin(rotationRadians));
        var normal = new LayoutVector2(-Math.Sin(rotationRadians), Math.Cos(rotationRadians));
        var center = insertionPoint + (tangent * (metrics.Width / 2)) +
            (normal * ((metrics.Height / 2) - metrics.Baseline));
        return new TextFootprint(
            text.ItemId,
            new OrientedRect(center, metrics.Width / 2, metrics.Height / 2, rotationRadians),
            metrics.Approximate);
    }
}

public sealed record LayoutObstacle
{
    public LayoutObstacle(
        string id,
        LayoutObstacleKind kind,
        OrientedRect footprint,
        string? ownerItemId = null,
        LayoutObstacleMeasurementSource measurementSource = LayoutObstacleMeasurementSource.Unknown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        Kind = kind;
        Footprint = footprint;
        OwnerItemId = string.IsNullOrWhiteSpace(ownerItemId) ? null : ownerItemId;
        MeasurementSource = measurementSource;
    }

    public string Id { get; }

    public LayoutObstacleKind Kind { get; }

    public OrientedRect Footprint { get; }

    public string? OwnerItemId { get; }

    public LayoutObstacleMeasurementSource MeasurementSource { get; }
}

public sealed record LayoutNetworkPath
{
    public LayoutNetworkPath(
        string id,
        string semanticId,
        string layer,
        IEnumerable<LayoutPoint2> vertices)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticId);
        ArgumentException.ThrowIfNullOrWhiteSpace(layer);
        var snapshot = CollectionSnapshot.List(vertices, nameof(vertices));
        if (snapshot.Count < 2 || snapshot.Zip(snapshot.Skip(1), (left, right) => (right - left).Length)
            .All(length => length <= LayoutGeometryTolerance.Distance))
        {
            throw new ArgumentException("A network path requires at least one non-degenerate segment.", nameof(vertices));
        }

        Id = id;
        SemanticId = semanticId;
        Layer = layer;
        Vertices = snapshot;
    }

    public string Id { get; }

    public string SemanticId { get; }

    public string Layer { get; }

    public IReadOnlyList<LayoutPoint2> Vertices { get; }
}

public sealed record LayoutTextPlacement
{
    public LayoutTextPlacement(
        string itemId,
        AnnotationTextRole role,
        LayoutPoint2 position,
        double rotationRadians,
        TextFootprint footprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        ArgumentNullException.ThrowIfNull(footprint);
        if (!NumberCompatibility.IsFinite(rotationRadians) || footprint.ItemId != itemId)
        {
            throw new ArgumentOutOfRangeException(nameof(rotationRadians));
        }

        ItemId = itemId;
        Role = role;
        Position = position;
        RotationRadians = LayoutAngles.NormalizeReadable(rotationRadians);
        Footprint = footprint;
    }

    public string ItemId { get; }

    public AnnotationTextRole Role { get; }

    public LayoutPoint2 Position { get; }

    public double RotationRadians { get; }

    public TextFootprint Footprint { get; }
}

public sealed record PreviousAnnotationLayout
{
    public PreviousAnnotationLayout(string candidateId, IEnumerable<LayoutTextPlacement> placements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        var snapshot = CollectionSnapshot.List(placements, nameof(placements));
        if (snapshot.Count == 0)
        {
            throw new ArgumentException("A previous layout must contain at least one text placement.", nameof(placements));
        }

        CandidateId = candidateId;
        Placements = snapshot;
    }

    public string CandidateId { get; }

    public IReadOnlyList<LayoutTextPlacement> Placements { get; }
}

public sealed record LegacyAnnotationLayout
{
    public LegacyAnnotationLayout(
        string candidateId,
        LayoutPoint2 anchor,
        LayoutVector2 tangent,
        IEnumerable<LayoutTextPlacement> placements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        var snapshot = CollectionSnapshot.List(placements, nameof(placements));
        if (snapshot.Count == 0)
        {
            throw new ArgumentException("A legacy layout must contain at least one text placement.", nameof(placements));
        }

        CandidateId = candidateId;
        Anchor = anchor;
        Tangent = tangent.Normalized();
        Normal = new LayoutVector2(-Tangent.Y, Tangent.X);
        Placements = snapshot;
    }

    public string CandidateId { get; }

    public LayoutPoint2 Anchor { get; }

    public LayoutVector2 Tangent { get; }

    public LayoutVector2 Normal { get; }

    public IReadOnlyList<LayoutTextPlacement> Placements { get; }
}

public sealed record AnnotationLayoutRequest
{
    public AnnotationLayoutRequest(
        string annotationId,
        LayoutPoint2 anchor,
        string networkSemanticId,
        IEnumerable<string> allowedNetworkLayers,
        AnnotationTextElement depth,
        AnnotationTextElement? diameter = null,
        PreviousAnnotationLayout? previousLayout = null,
        LegacyAnnotationLayout? legacyLayout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(annotationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(networkSemanticId);
        ArgumentNullException.ThrowIfNull(depth);
        if (depth.Role != AnnotationTextRole.Depth || diameter?.Role == AnnotationTextRole.Depth)
        {
            throw new ArgumentException("Depth and diameter roles must be explicit.");
        }

        var layers = CollectionSnapshot.List(allowedNetworkLayers, nameof(allowedNetworkLayers));
        if (layers.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Allowed network layers cannot be empty.", nameof(allowedNetworkLayers));
        }

        var itemIds = new[] { depth.ItemId, diameter?.ItemId }.Where(value => value is not null).ToArray();
        if (itemIds.Distinct(StringComparer.Ordinal).Count() != itemIds.Length)
        {
            throw new ArgumentException("Annotation text identifiers must be unique.");
        }

        AnnotationId = annotationId;
        Anchor = anchor;
        NetworkSemanticId = networkSemanticId;
        AllowedNetworkLayers = layers;
        Depth = depth;
        Diameter = diameter;
        PreviousLayout = previousLayout;
        LegacyLayout = legacyLayout;
    }

    public string AnnotationId { get; }

    public LayoutPoint2 Anchor { get; }

    public string NetworkSemanticId { get; }

    public IReadOnlyList<string> AllowedNetworkLayers { get; }

    public AnnotationTextElement Depth { get; }

    public AnnotationTextElement? Diameter { get; }

    public PreviousAnnotationLayout? PreviousLayout { get; }

    public LegacyAnnotationLayout? LegacyLayout { get; }

    public IReadOnlyList<AnnotationTextElement> Texts => Diameter is null ? [Depth] : [Depth, Diameter];
}

public sealed record LayoutScene
{
    public LayoutScene(IEnumerable<LayoutNetworkPath> networks, IEnumerable<LayoutObstacle> obstacles)
    {
        Networks = CollectionSnapshot.List(networks, nameof(networks));
        Obstacles = CollectionSnapshot.List(obstacles, nameof(obstacles));
    }

    public IReadOnlyList<LayoutNetworkPath> Networks { get; }

    public IReadOnlyList<LayoutObstacle> Obstacles { get; }
}

public sealed record AnnotationLayoutOptions
{
    public AnnotationLayoutOptions(
        double clearance = 0.15,
        double associationDistance = 5,
        double unknownNetworkInferenceDistance = 0.10,
        double maximumDisplacement = 2.5,
        double spatialCellSize = 2,
        double distanceWeight = 12,
        double preferredPositionReward = 30,
        double stabilityReward = 80,
        double oppositeSidePenalty = 5,
        double associatedNetworkCrossingPenalty = 12,
        double otherNetworkCrossingPenalty = 45,
        double otherObstaclePenalty = 80,
        double clearanceRewardWeight = 4,
        double minimumAcceptableScore = -100,
        int maximumIndexedCellsPerItem = 10_000,
        double preferredAdjustmentFactor = 0.50,
        double maximumStandardAdjustmentFactor = 1.5,
        double existingMovementBudgetMultiplier = 2.0,
        double approximateContactToleranceFactor = 0.05,
        double visualContactToleranceFactor = 0.02,
        AnnotationLayoutStrategy strategy = AnnotationLayoutStrategy.HybridSimple)
    {
        var positive = new[] { clearance, associationDistance, maximumDisplacement, spatialCellSize };
        if (positive.Any(value => !NumberCompatibility.IsFinite(value) || value <= 0) ||
            !NumberCompatibility.IsFinite(unknownNetworkInferenceDistance) || unknownNetworkInferenceDistance <= 0 ||
            !NumberCompatibility.IsFinite(distanceWeight) || distanceWeight < 0 ||
            !NumberCompatibility.IsFinite(preferredPositionReward) || preferredPositionReward < 0 ||
            !NumberCompatibility.IsFinite(stabilityReward) || stabilityReward < 0 ||
            !NumberCompatibility.IsFinite(oppositeSidePenalty) || oppositeSidePenalty < 0 ||
            !NumberCompatibility.IsFinite(associatedNetworkCrossingPenalty) || associatedNetworkCrossingPenalty < 0 ||
            !NumberCompatibility.IsFinite(otherNetworkCrossingPenalty) || otherNetworkCrossingPenalty < 0 ||
            !NumberCompatibility.IsFinite(otherObstaclePenalty) || otherObstaclePenalty < 0 ||
            !NumberCompatibility.IsFinite(clearanceRewardWeight) || clearanceRewardWeight < 0 ||
            !NumberCompatibility.IsFinite(minimumAcceptableScore) || maximumIndexedCellsPerItem < 1 ||
            !NumberCompatibility.IsFinite(preferredAdjustmentFactor) || preferredAdjustmentFactor <= 0 ||
            !NumberCompatibility.IsFinite(maximumStandardAdjustmentFactor) || maximumStandardAdjustmentFactor <= 0 ||
            maximumStandardAdjustmentFactor < preferredAdjustmentFactor ||
            !NumberCompatibility.IsFinite(existingMovementBudgetMultiplier) || existingMovementBudgetMultiplier < 1 ||
            !NumberCompatibility.IsFinite(approximateContactToleranceFactor) || approximateContactToleranceFactor < 0 ||
            !NumberCompatibility.IsFinite(visualContactToleranceFactor) || visualContactToleranceFactor < 0 ||
            !Enum.IsDefined(typeof(AnnotationLayoutStrategy), strategy))
        {
            throw new ArgumentOutOfRangeException(nameof(clearance));
        }

        Clearance = clearance;
        AssociationDistance = associationDistance;
        UnknownNetworkInferenceDistance = unknownNetworkInferenceDistance;
        MaximumDisplacement = maximumDisplacement;
        SpatialCellSize = spatialCellSize;
        DistanceWeight = distanceWeight;
        PreferredPositionReward = preferredPositionReward;
        StabilityReward = stabilityReward;
        OppositeSidePenalty = oppositeSidePenalty;
        AssociatedNetworkCrossingPenalty = associatedNetworkCrossingPenalty;
        OtherNetworkCrossingPenalty = otherNetworkCrossingPenalty;
        OtherObstaclePenalty = otherObstaclePenalty;
        ClearanceRewardWeight = clearanceRewardWeight;
        MinimumAcceptableScore = minimumAcceptableScore;
        MaximumIndexedCellsPerItem = maximumIndexedCellsPerItem;
        PreferredAdjustmentFactor = preferredAdjustmentFactor;
        MaximumStandardAdjustmentFactor = maximumStandardAdjustmentFactor;
        ExistingMovementBudgetMultiplier = existingMovementBudgetMultiplier;
        ApproximateContactToleranceFactor = approximateContactToleranceFactor;
        VisualContactToleranceFactor = visualContactToleranceFactor;
        Strategy = strategy;
    }

    public double Clearance { get; }

    public double AssociationDistance { get; }

    public double UnknownNetworkInferenceDistance { get; }

    public double MaximumDisplacement { get; }

    public double SpatialCellSize { get; }

    public double DistanceWeight { get; }

    public double PreferredPositionReward { get; }

    public double StabilityReward { get; }

    public double OppositeSidePenalty { get; }

    public double AssociatedNetworkCrossingPenalty { get; }

    public double OtherNetworkCrossingPenalty { get; }

    public double OtherObstaclePenalty { get; }

    public double ClearanceRewardWeight { get; }

    public double MinimumAcceptableScore { get; }

    public int MaximumIndexedCellsPerItem { get; }

    public double PreferredAdjustmentFactor { get; }

    public double MaximumStandardAdjustmentFactor { get; }

    public double ExistingMovementBudgetMultiplier { get; }

    public double ApproximateContactToleranceFactor { get; }

    public double VisualContactToleranceFactor { get; }

    public AnnotationLayoutStrategy Strategy { get; }
}

public sealed record MovementBudget
{
    public MovementBudget(double preferredAdjustment, double maximumStandardAdjustment, double maximumExistingDistance)
    {
        if (!NumberCompatibility.IsFinite(preferredAdjustment) || preferredAdjustment <= 0 ||
            !NumberCompatibility.IsFinite(maximumStandardAdjustment) || maximumStandardAdjustment < preferredAdjustment ||
            !NumberCompatibility.IsFinite(maximumExistingDistance) || maximumExistingDistance < maximumStandardAdjustment)
        {
            throw new ArgumentOutOfRangeException(nameof(preferredAdjustment));
        }

        PreferredAdjustment = preferredAdjustment;
        MaximumStandardAdjustment = maximumStandardAdjustment;
        MaximumExistingDistance = maximumExistingDistance;
    }

    public double PreferredAdjustment { get; }

    public double MaximumStandardAdjustment { get; }

    public double MaximumExistingDistance { get; }
}

public sealed record LegacyValidationResult
{
    public LegacyValidationResult(
        LegacyIssue issues,
        double minimumClearance,
        IEnumerable<string> details)
    {
        if (!NumberCompatibility.IsFinite(minimumClearance) && !double.IsPositiveInfinity(minimumClearance))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumClearance));
        }

        Issues = issues;
        MinimumClearance = minimumClearance;
        Details = CollectionSnapshot.List(details, nameof(details));
    }

    public LegacyIssue Issues { get; }

    public double MinimumClearance { get; }

    public IReadOnlyList<string> Details { get; }

    public bool IsValid => Issues == LegacyIssue.None;
}

public sealed record AnnotationGroupInvariantResult
{
    public AnnotationGroupInvariantResult(
        AnnotationGroupInvariantViolation violations,
        IEnumerable<string> details)
    {
        Violations = violations;
        Details = CollectionSnapshot.List(details, nameof(details));
    }

    public AnnotationGroupInvariantViolation Violations { get; }

    public IReadOnlyList<string> Details { get; }

    public bool IsValid => Violations == AnnotationGroupInvariantViolation.None;
}

public sealed record LayoutScore
{
    public LayoutScore(
        double distance,
        double preferredPosition,
        double stability,
        double sidePreference,
        double networkCrossing,
        double clearance,
        double obstaclePenalty,
        int hardConflictCount,
        int networkCrossingCount,
        IEnumerable<string> reasons)
    {
        Distance = distance;
        PreferredPosition = preferredPosition;
        Stability = stability;
        SidePreference = sidePreference;
        NetworkCrossing = networkCrossing;
        Clearance = clearance;
        ObstaclePenalty = obstaclePenalty;
        HardConflictCount = hardConflictCount;
        NetworkCrossingCount = networkCrossingCount;
        Reasons = CollectionSnapshot.List(reasons, nameof(reasons));
        Total = distance + preferredPosition + stability + sidePreference + networkCrossing + clearance +
            obstaclePenalty - (hardConflictCount * 1_000_000);
    }

    public double Distance { get; }

    public double PreferredPosition { get; }

    public double Stability { get; }

    public double SidePreference { get; }

    public double NetworkCrossing { get; }

    public double Clearance { get; }

    public double ObstaclePenalty { get; }

    public int HardConflictCount { get; }

    public int NetworkCrossingCount { get; }

    public IReadOnlyList<string> Reasons { get; }

    public double Total { get; }

    public bool IsValid => HardConflictCount == 0;
}

public sealed record LayoutDecision
{
    public LayoutDecision(
        string annotationId,
        LayoutStatus status,
        LayoutMode mode,
        string candidateId,
        IEnumerable<LayoutTextPlacement> placements,
        LayoutScore score,
        string? associatedNetworkId,
        string? associatedNetworkLayer,
        int evaluatedCandidateCount,
        IEnumerable<string> diagnostics,
        LayoutDecisionKind decisionKind = LayoutDecisionKind.Corrected,
        LegacyValidationResult? legacyValidation = null,
        double movementDistance = 0,
        bool sideChanged = false,
        CorrectionQuality correctionQuality = CorrectionQuality.Good,
        string? reasonForMove = null,
        IEnumerable<LayoutTextPlacement>? legacyPlacements = null,
        AnnotationGroupInvariantResult? groupInvariants = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(annotationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        ArgumentNullException.ThrowIfNull(score);
        var placementSnapshot = CollectionSnapshot.List(placements, nameof(placements));
        if (placementSnapshot.Count == 0)
        {
            throw new ArgumentException("A layout decision requires at least one text placement.", nameof(placements));
        }

        AnnotationId = annotationId;
        Status = status;
        Mode = mode;
        CandidateId = candidateId;
        Placements = placementSnapshot;
        Score = score;
        AssociatedNetworkId = associatedNetworkId;
        AssociatedNetworkLayer = associatedNetworkLayer;
        EvaluatedCandidateCount = evaluatedCandidateCount;
        Diagnostics = CollectionSnapshot.List(diagnostics, nameof(diagnostics));
        DecisionKind = decisionKind;
        LegacyValidation = legacyValidation ?? new LegacyValidationResult(LegacyIssue.None, double.PositiveInfinity, []);
        if (!NumberCompatibility.IsFinite(movementDistance) || movementDistance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(movementDistance));
        }

        MovementDistance = movementDistance;
        SideChanged = sideChanged;
        CorrectionQuality = correctionQuality;
        ReasonForMove = reasonForMove;
        LegacyPlacements = legacyPlacements is null
            ? Array.Empty<LayoutTextPlacement>()
            : CollectionSnapshot.List(legacyPlacements, nameof(legacyPlacements));
        GroupInvariants = groupInvariants ?? new AnnotationGroupInvariantResult(
            AnnotationGroupInvariantViolation.None,
            []);
    }

    public string AnnotationId { get; }

    public LayoutStatus Status { get; }

    public LayoutMode Mode { get; }

    public string CandidateId { get; }

    public IReadOnlyList<LayoutTextPlacement> Placements { get; }

    public LayoutScore Score { get; }

    public string? AssociatedNetworkId { get; }

    public string? AssociatedNetworkLayer { get; }

    public int EvaluatedCandidateCount { get; }

    public IReadOnlyList<string> Diagnostics { get; }

    public LayoutDecisionKind DecisionKind { get; }

    public LegacyValidationResult LegacyValidation { get; }

    public double MovementDistance { get; }

    public bool SideChanged { get; }

    public CorrectionQuality CorrectionQuality { get; }

    public string? ReasonForMove { get; }

    public IReadOnlyList<LayoutTextPlacement> LegacyPlacements { get; }

    public AnnotationGroupInvariantResult GroupInvariants { get; }
}

public sealed record AnnotationLayoutStatistics(
    int AnnotationCount,
    int CandidateCount,
    long SpatialQueryCount,
    int UnresolvedCount,
    long ElapsedMilliseconds,
    int LegacyAcceptedCount = 0,
    int LegacyRejectedCount = 0,
    int KeptExistingCount = 0,
    int KeptLegacyCount = 0,
    int CorrectedCount = 0,
    int UnnecessaryMoveCount = 0,
    double AverageCorrectionDistance = 0,
    double MaximumCorrectionDistance = 0,
    int SideChangeCount = 0,
    int CorrectionsUpTo025Count = 0,
    int Corrections025To050Count = 0,
    int Corrections050To075Count = 0,
    int CorrectionsOver075Count = 0,
    int OrderInvariantViolationCount = 0,
    int RotationInvariantViolationCount = 0,
    int SideInvariantViolationCount = 0,
    int GroupIntegrityViolationCount = 0)
{
    public int OrderInvariantViolations => OrderInvariantViolationCount;

    public int RotationInvariantViolations => RotationInvariantViolationCount;

    public int SideInvariantViolations => SideInvariantViolationCount;

    public int GroupIntegrityViolations => GroupIntegrityViolationCount;

    public int UnnecessaryMoves => UnnecessaryMoveCount;
}

public sealed record AnnotationLayoutBatchResult(
    IReadOnlyList<LayoutDecision> Decisions,
    AnnotationLayoutStatistics Statistics);
