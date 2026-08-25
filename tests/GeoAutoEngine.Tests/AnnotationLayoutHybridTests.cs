using GeoAutoEngine.Domain.AnnotationLayout;

namespace GeoAutoEngine.Tests;

public sealed class AnnotationLayoutHybridTests
{
    [Fact]
    [Trait("Category", "FAST")]
    [Trait("GoldenScene", "LAYOUT_A1_001_LEGACY_CLEAR")]
    public void OneHundredConflictFreeAnnotations_KeepExactLegacyAndEvaluateNoAlternatives()
    {
        var requests = Enumerable.Range(0, 100)
            .Select(index => Request($"A{index}", index * 4, legacyTangentialShift: 0.35))
            .ToArray();

        var result = Layout(requests, Scene(Network(-10, 410)));

        Assert.Equal(100, result.Statistics.LegacyAcceptedCount);
        Assert.Equal(100, result.Statistics.KeptLegacyCount);
        Assert.Equal(0, result.Statistics.CorrectedCount);
        Assert.Equal(0, result.Statistics.UnnecessaryMoveCount);
        Assert.Equal(100, result.Statistics.CandidateCount);
        Assert.All(result.Decisions, decision =>
        {
            Assert.Equal(LayoutDecisionKind.KeepLegacy, decision.DecisionKind);
            Assert.Equal(decision.LegacyPlacements.Select(item => item.Position), decision.Placements.Select(item => item.Position));
        });
    }

    [Theory]
    [Trait("Category", "FAST")]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(89.9)]
    [InlineData(45)]
    public void ValidLegacyAtEveryLinearOrientation_IsKeptExactly(double degrees)
    {
        var radians = degrees * Math.PI / 180;
        var tangent = new LayoutVector2(Math.Cos(radians), Math.Sin(radians));
        var request = Request("orientation", 0, tangent: tangent, legacyTangentialShift: 0.37);
        var network = new LayoutNetworkPath(
            "network", "water", "WATER",
            [new LayoutPoint2(-tangent.X * 20, -tangent.Y * 20), new LayoutPoint2(tangent.X * 20, tangent.Y * 20)]);

        var decision = Assert.Single(Layout([request], Scene(network)).Decisions);

        Assert.Equal(LayoutDecisionKind.KeepLegacy, decision.DecisionKind);
        Assert.Equal(decision.LegacyPlacements.Select(item => item.Position), decision.Placements.Select(item => item.Position));
        Assert.Equal(1, decision.EvaluatedCandidateCount);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ValidLegacyOnCurve_IsKeptExactly()
    {
        var tangent = new LayoutVector2(1, 1).Normalized();
        var request = Request("curve", 0, tangent: tangent, legacyTangentialShift: 0.31);
        var curve = new LayoutNetworkPath(
            "curve", "water", "WATER",
            [new LayoutPoint2(-10, -4), new LayoutPoint2(-2, -1), new LayoutPoint2(0, 0), new LayoutPoint2(7, 7)]);

        var decision = Assert.Single(Layout([request], Scene(curve)).Decisions);

        Assert.Equal(LayoutDecisionKind.KeepLegacy, decision.DecisionKind);
        Assert.Equal(decision.LegacyPlacements.Select(item => item.Position), decision.Placements.Select(item => item.Position));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void LegacyAtFrozenV1MaximumShift_IsStillAnAcceptableBaseline()
    {
        var request = Request("legacy-limit", 0, legacyTangentialShift: 2.0);

        var decision = Assert.Single(Layout([request], Scene(Network(-10, 10))).Decisions);

        Assert.Equal(LayoutDecisionKind.KeepLegacy, decision.DecisionKind);
        Assert.True(decision.LegacyValidation.IsValid);
        Assert.Equal(decision.LegacyPlacements.Select(item => item.Position), decision.Placements.Select(item => item.Position));
    }

    [Fact]
    [Trait("Category", "FAST")]
    [Trait("GoldenScene", "LAYOUT_A1_003_LEGACY_BLOCK_COLLISION")]
    [Trait("GoldenScene", "LAYOUT_A1_004_MINIMAL_CORRECTION")]
    public void LegacyBlockCollision_UsesNearestValidTierCandidate()
    {
        var request = Request("blocked", 0, includeDiameter: false);
        var obstacle = Obstacle(
            "block", LayoutObstacleKind.Block, 0.45, 0.05, 0.65, 0.20,
            LayoutObstacleMeasurementSource.CadGeometricExtents);

        var result = Layout([request], Scene(Network(-10, 10), obstacle));
        var decision = Assert.Single(result.Decisions);

        Assert.Equal(LayoutDecisionKind.Corrected, decision.DecisionKind);
        Assert.True(decision.LegacyValidation.Issues.HasFlag(LegacyIssue.BlockCollision));
        Assert.Equal(0.25, decision.MovementDistance, 10);
        Assert.Contains("source=CadGeometricExtents", decision.LegacyValidation.Details.Single());
        Assert.Equal(1, result.Statistics.CorrectionsUpTo025Count);
        Assert.Equal(0, result.Statistics.Corrections025To050Count);
        Assert.Equal(0, result.Statistics.Corrections050To075Count);
        Assert.Equal(0, result.Statistics.CorrectionsOver075Count);
    }

    [Fact]
    [Trait("Category", "FAST")]
    [Trait("GoldenScene", "LAYOUT_A1_002_LEGACY_TEXT_COLLISION")]
    public void LegacyTextCollision_UsesSmallestLocalCorrection()
    {
        var request = Request("text", 0, includeDiameter: false);
        var obstacle = Obstacle(
            "existing-text", LayoutObstacleKind.ExistingText, 0.45, 0.05, 0.65, 0.20,
            LayoutObstacleMeasurementSource.ExistingTextGeometricExtents);

        var decision = Assert.Single(Layout([request], Scene(Network(-10, 10), obstacle)).Decisions);

        Assert.Equal(LayoutDecisionKind.Corrected, decision.DecisionKind);
        Assert.True(decision.LegacyValidation.Issues.HasFlag(LegacyIssue.TextCollision));
        Assert.Equal(0.25, decision.MovementDistance, 10);
        Assert.Contains("ExistingTextGeometricExtents", decision.ReasonForMove, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "FAST")]
    [Trait("GoldenScene", "LAYOUT_LEGACY_RIGID_GROUP_025")]
    public void CollisionCorrection025_PreservesTheCompleteLegacyGroup()
    {
        var request = Request("rigid", 0);
        var obstacle = Obstacle(
            "depth-block", LayoutObstacleKind.Block, 0.45, 0.21, 0.65, 0.30,
            LayoutObstacleMeasurementSource.CadGeometricExtents);

        var result = Layout([request], Scene(Network(-10, 10), obstacle));
        var decision = Assert.Single(result.Decisions);
        var legacyById = decision.LegacyPlacements.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        var translations = decision.Placements.Select(item =>
        {
            var baseline = legacyById[item.ItemId];
            return item.Position - baseline.Position;
        }).ToArray();
        var depth = decision.Placements.Single(item => item.Role == AnnotationTextRole.Depth);
        var diameter = decision.Placements.Single(item => item.Role == AnnotationTextRole.Diameter);
        var legacyDepth = legacyById[depth.ItemId];
        var legacyDiameter = legacyById[diameter.ItemId];

        Assert.Equal(LayoutDecisionKind.Corrected, decision.DecisionKind);
        Assert.Equal(0.25, decision.MovementDistance, 10);
        Assert.All(translations, translation =>
        {
            Assert.Equal(translations[0].X, translation.X, 12);
            Assert.Equal(translations[0].Y, translation.Y, 12);
        });
        Assert.Equal(legacyDepth.Position - legacyDiameter.Position, depth.Position - diameter.Position);
        Assert.Equal(legacyDepth.RotationRadians, depth.RotationRadians, 12);
        Assert.Equal(legacyDiameter.RotationRadians, diameter.RotationRadians, 12);
        Assert.True(depth.Position.Y > diameter.Position.Y);
        Assert.False(decision.SideChanged);
        Assert.True(decision.GroupInvariants.IsValid);
        AssertInvariantMetricsAreZero(result.Statistics);
    }

    [Fact]
    [Trait("Category", "FAST")]
    [Trait("GoldenScene", "LAYOUT_A1_005_MOVEMENT_BUDGET")]
    [Trait("GoldenScene", "LAYOUT_A1_010_REQUIRES_REVIEW")]
    public void NoCandidateInsideMovementBudget_ReturnsRequiresReview()
    {
        var request = Request("blocked", 0);
        var obstacle = Obstacle("dense", LayoutObstacleKind.Structure, -4, -4, 4, 4);

        var decision = Assert.Single(Layout([request], Scene(Network(-10, 10), obstacle)).Decisions);

        Assert.Equal(LayoutDecisionKind.RequiresReview, decision.DecisionKind);
        Assert.Equal(LayoutStatus.RequiresReview, decision.Status);
        Assert.True(decision.MovementDistance <= 2.0 + LayoutGeometryTolerance.Distance);
    }

    [Fact]
    [Trait("Category", "FAST")]
    [Trait("GoldenScene", "LAYOUT_A1_009_OPPOSITE_SIDE_FORBIDDEN")]
    public void OppositeSide_IsNeverUsedWhenSameSideTiersFail()
    {
        var request = Request("opposite", 0, includeDiameter: false);
        var obstacle = Obstacle("upper-block", LayoutObstacleKind.Block, -3, 0.15, 3, 3);

        var decision = Assert.Single(Layout([request], Scene(Network(-10, 10), obstacle)).Decisions);

        Assert.Equal(LayoutDecisionKind.RequiresReview, decision.DecisionKind);
        Assert.False(decision.SideChanged);
        Assert.Equal("legacy:v1-real", decision.CandidateId);
        Assert.Equal(0, decision.MovementDistance);
    }

    [Fact]
    [Trait("Category", "FAST")]
    [Trait("GoldenScene", "LAYOUT_A1_006_KEEP_EXISTING")]
    public void ValidExistingManagedPosition_HasPriorityOverLegacy()
    {
        var request = Request("existing", 0, existingTangentialShift: 0.70);

        var decision = Assert.Single(Layout([request], Scene(Network(-10, 10))).Decisions);

        Assert.Equal(LayoutDecisionKind.KeepExisting, decision.DecisionKind);
        Assert.StartsWith("existing:", decision.CandidateId, StringComparison.Ordinal);
        Assert.Equal(0.70, decision.MovementDistance, 10);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ValidExistingPosition_IsPreservedEvenWhenFarFromCurrentLegacy()
    {
        var request = Request("existing-far", 0, existingTangentialShift: 5.0);

        var decision = Assert.Single(Layout([request], Scene(Network(-10, 20))).Decisions);

        Assert.Equal(LayoutDecisionKind.KeepExisting, decision.DecisionKind);
        Assert.Equal(5.0, decision.MovementDistance, 10);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ExistingPositionThatBreaksTheGroup_IsRejectedInFavorOfLegacy()
    {
        var baseline = Request("broken-existing", 0);
        var previousPlacements = baseline.LegacyLayout!.Placements.Select((placement, index) =>
        {
            var translation = new LayoutVector2(index == 0 ? 0.20 : 0.40, 0);
            var rectangle = placement.Footprint.Rectangle;
            return new LayoutTextPlacement(
                placement.ItemId,
                placement.Role,
                placement.Position + translation,
                placement.RotationRadians,
                new TextFootprint(
                    placement.ItemId,
                    new OrientedRect(
                        rectangle.Center + translation,
                        rectangle.HalfWidth,
                        rectangle.HalfHeight,
                        rectangle.RotationRadians),
                    placement.Footprint.Approximate));
        }).ToArray();
        var request = new AnnotationLayoutRequest(
            baseline.AnnotationId,
            baseline.Anchor,
            baseline.NetworkSemanticId,
            baseline.AllowedNetworkLayers,
            baseline.Depth,
            baseline.Diameter,
            new PreviousAnnotationLayout("broken", previousPlacements),
            baseline.LegacyLayout);

        var result = Layout([request], Scene(Network(-10, 10)));
        var decision = Assert.Single(result.Decisions);

        Assert.Equal(LayoutDecisionKind.KeepLegacy, decision.DecisionKind);
        Assert.Equal(decision.LegacyPlacements.Select(item => item.Position), decision.Placements.Select(item => item.Position));
        AssertInvariantMetricsAreZero(result.Statistics);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ClearanceOnly_DoesNotInvalidateOrMoveLegacy()
    {
        var request = Request("clearance", 0, includeDiameter: false);
        var obstacle = Obstacle("near-text", LayoutObstacleKind.ExistingText, 0.0, 0.61, 1.8, 0.70);

        var decision = Assert.Single(Layout([request], Scene(Network(-10, 10), obstacle)).Decisions);

        Assert.Equal(LayoutDecisionKind.KeepLegacy, decision.DecisionKind);
        Assert.True(decision.LegacyValidation.IsValid);
        Assert.Contains(decision.Score.Reasons, reason => reason.StartsWith("SOFT:InsufficientClearance", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void PrintLikeClutter_IsKnownToSceneAndNeverProducesBroadCorrection()
    {
        var request = Request("print-clutter", 0);
        var obstacles = new[]
        {
            Obstacle("survey-point-label", LayoutObstacleKind.Block, -0.2, -0.9, 2.1, 0.9,
                LayoutObstacleMeasurementSource.AnnotatedBlockGeometricExtents),
            Obstacle("large-mtext", LayoutObstacleKind.ExistingText, -1.2, 0.7, 2.8, 2.2,
                LayoutObstacleMeasurementSource.MTextGeometricExtents),
            Obstacle("civil-label", LayoutObstacleKind.ExistingText, -1.0, -2.0, 2.5, -0.7,
                LayoutObstacleMeasurementSource.CivilEntityGeometricExtents),
        };

        var decision = Assert.Single(Layout([request], Scene(Network(-10, 10), obstacles)).Decisions);

        Assert.Equal(LayoutDecisionKind.RequiresReview, decision.DecisionKind);
        Assert.True(decision.LegacyValidation.Issues.HasFlag(LegacyIssue.BlockCollision));
        Assert.Contains(decision.LegacyValidation.Details, detail => detail.Contains("AnnotatedBlockGeometricExtents", StringComparison.Ordinal));
        Assert.True(decision.MovementDistance <= 0.75 + LayoutGeometryTolerance.Distance);
    }

    [Fact]
    [Trait("Category", "FAST")]
    [Trait("GoldenScene", "LAYOUT_A1_007_NO_DOMINO")]
    public void OnlyConflictingMiddleAnnotationMoves_NoDominoEffect()
    {
        var requests = Enumerable.Range(0, 5).Select(index => Request($"A{index}", index * 4)).ToArray();
        var obstacle = Obstacle("middle", LayoutObstacleKind.Block, 8.45, 0.05, 8.65, 0.20);

        var result = Layout(requests, Scene(Network(-10, 30), obstacle));

        Assert.Equal(1, result.Statistics.CorrectedCount);
        Assert.Equal(4, result.Statistics.KeptLegacyCount);
        Assert.Equal(LayoutDecisionKind.Corrected, result.Decisions.Single(item => item.AnnotationId == "A2").DecisionKind);
        Assert.All(result.Decisions.Where(item => item.AnnotationId != "A2"),
            decision => Assert.Equal(LayoutDecisionKind.KeepLegacy, decision.DecisionKind));
        Assert.Equal(0, result.Statistics.UnnecessaryMoveCount);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void HybridIsDeterministicAndUnaffectedByDistantObstacle()
    {
        var requests = new[] { Request("A", 0), Request("B", 4) };
        var first = Layout(requests, Scene(Network(-10, 20)));
        var second = Layout(requests, Scene(
            Network(-10, 20),
            Obstacle("far", LayoutObstacleKind.Block, 1000, 1000, 1010, 1010)));

        Assert.Equal(Semantic(first), Semantic(second));
        Assert.Equal(0, first.Statistics.UnnecessaryMoveCount);
        Assert.Equal(0, second.Statistics.UnnecessaryMoveCount);
    }

    [Fact]
    [Trait("Category", "FAST")]
    [Trait("GoldenScene", "LAYOUT_A1_008_NO_UNNECESSARY_MOVE")]
    public void PhaseAComparisonFlagsMoveThatHybridPrevents()
    {
        var request = Request("crossing", 0, legacyTangentialShift: 0.35);
        var associated = Network(-10, 10);
        var other = new LayoutNetworkPath(
            "other", "sewer", "SEWER", [new LayoutPoint2(0.5, -10), new LayoutPoint2(0.5, 10)]);
        var scene = new LayoutScene([associated, other], []);
        var phaseA = new AnnotationLayoutEngine(new AnnotationLayoutOptions(strategy: AnnotationLayoutStrategy.PhaseAFreeScore))
            .Layout([request], scene);
        var hybrid = Layout([request], scene);

        Assert.Equal(1, phaseA.Statistics.UnnecessaryMoveCount);
        Assert.Equal(0, hybrid.Statistics.UnnecessaryMoveCount);
        Assert.Equal(LayoutDecisionKind.KeepLegacy, Assert.Single(hybrid.Decisions).DecisionKind);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void SecondRun_KeepsFirstRunCorrectionWithoutAnyNewMove()
    {
        var request = Request("idempotent", 0, includeDiameter: false);
        var obstacle = Obstacle("block", LayoutObstacleKind.Block, 0.45, 0.05, 0.65, 0.20);
        var scene = Scene(Network(-10, 10), obstacle);
        var first = Assert.Single(Layout([request], scene).Decisions);
        var secondRequest = new AnnotationLayoutRequest(
            request.AnnotationId,
            request.Anchor,
            request.NetworkSemanticId,
            request.AllowedNetworkLayers,
            request.Depth,
            request.Diameter,
            new PreviousAnnotationLayout("managed", first.Placements),
            request.LegacyLayout);

        var secondResult = Layout([secondRequest], scene);
        var second = Assert.Single(secondResult.Decisions);

        Assert.Equal(LayoutDecisionKind.KeepExisting, second.DecisionKind);
        Assert.Equal(first.Placements.Select(item => item.Position), second.Placements.Select(item => item.Position));
        Assert.Equal(0, secondResult.Statistics.CorrectedCount);
        Assert.Equal(0, secondResult.Statistics.UnnecessaryMoveCount);
    }

    private static AnnotationLayoutBatchResult Layout(
        IEnumerable<AnnotationLayoutRequest> requests,
        LayoutScene scene) => new AnnotationLayoutEngine().Layout(requests, scene);

    private static AnnotationLayoutRequest Request(
        string id,
        double anchorX,
        bool includeDiameter = true,
        LayoutVector2? tangent = null,
        double legacyTangentialShift = 0,
        double? existingTangentialShift = null)
    {
        var direction = (tangent ?? new LayoutVector2(1, 0)).Normalized();
        var normal = new LayoutVector2(-direction.Y, direction.X);
        var anchor = new LayoutPoint2(anchorX, 0);
        var depth = new AnnotationTextElement($"{id}:depth", AnnotationTextRole.Depth, "P.1.42", 0.5, 0.1);
        var diameter = includeDiameter
            ? new AnnotationTextElement($"{id}:diameter", AnnotationTextRole.Diameter, "∅600", 0.5, -0.6)
            : null;
        var texts = diameter is null ? new[] { depth } : new[] { depth, diameter };
        var legacyPlacements = Placements(texts, anchor, direction, normal, legacyTangentialShift);
        var legacy = new LegacyAnnotationLayout("v1-real", anchor, direction, legacyPlacements);
        PreviousAnnotationLayout? previous = null;
        if (existingTangentialShift.HasValue)
        {
            previous = new PreviousAnnotationLayout(
                "managed",
                Placements(texts, anchor, direction, normal, existingTangentialShift.Value));
        }

        return new AnnotationLayoutRequest(
            id,
            anchor,
            "water",
            ["WATER"],
            depth,
            diameter,
            previous,
            legacy);
    }

    private static LayoutTextPlacement[] Placements(
        IEnumerable<AnnotationTextElement> texts,
        LayoutPoint2 anchor,
        LayoutVector2 tangent,
        LayoutVector2 normal,
        double tangentialShift)
    {
        var metrics = new ApproximateTextMetricsProvider();
        var rotation = LayoutAngles.NormalizeReadable(Math.Atan2(tangent.Y, tangent.X));
        return texts.Select(text =>
        {
            var position = anchor + (tangent * tangentialShift) + (normal * text.PerpendicularOffset);
            var footprint = TextFootprint.Create(text, position, rotation, metrics.Measure(text));
            return new LayoutTextPlacement(text.ItemId, text.Role, position, rotation, footprint);
        }).ToArray();
    }

    private static LayoutNetworkPath Network(double start, double end) =>
        new("main", "water", "WATER", [new LayoutPoint2(start, 0), new LayoutPoint2(end, 0)]);

    private static LayoutObstacle Obstacle(
        string id,
        LayoutObstacleKind kind,
        double minX,
        double minY,
        double maxX,
        double maxY,
        LayoutObstacleMeasurementSource source = LayoutObstacleMeasurementSource.Unknown) =>
        new(
            id,
            kind,
            new OrientedRect(
                new LayoutPoint2((minX + maxX) / 2, (minY + maxY) / 2),
                (maxX - minX) / 2,
                (maxY - minY) / 2,
                0),
            measurementSource: source);

    private static LayoutScene Scene(LayoutNetworkPath network, params LayoutObstacle[] obstacles) =>
        new([network], obstacles);

    private static string Semantic(AnnotationLayoutBatchResult result) => string.Join(
        "|",
        result.Decisions.Select(decision => string.Join(
            ";",
            decision.AnnotationId,
            decision.DecisionKind,
            decision.CandidateId,
            string.Join(",", decision.Placements.Select(item =>
                FormattableString.Invariant($"{item.Position.X:R}:{item.Position.Y:R}:{item.RotationRadians:R}"))))));

    private static void AssertInvariantMetricsAreZero(AnnotationLayoutStatistics statistics)
    {
        Assert.Equal(0, statistics.OrderInvariantViolations);
        Assert.Equal(0, statistics.RotationInvariantViolations);
        Assert.Equal(0, statistics.SideInvariantViolations);
        Assert.Equal(0, statistics.GroupIntegrityViolations);
        Assert.Equal(0, statistics.UnnecessaryMoves);
    }
}
