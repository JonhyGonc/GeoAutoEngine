using GeoAutoEngine.Domain.AnnotationLayout;

namespace GeoAutoEngine.Tests;

public sealed class AnnotationLayoutEngineTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void HorizontalNetworkWithoutObstacles_UsesPreferredLayout()
    {
        var result = Layout([Request("A", new LayoutPoint2(0, 0))], Scene(Horizontal()));
        var decision = Assert.Single(result.Decisions);

        Assert.Equal(LayoutStatus.Resolved, decision.Status);
        Assert.Equal("legacy:synthetic", decision.CandidateId);
        Assert.Equal(LayoutDecisionKind.KeepLegacy, decision.DecisionKind);
        Assert.All(decision.Placements, placement => Assert.Equal(0, placement.RotationRadians, 10));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void VerticalNetwork_ProducesReadableVerticalRotation()
    {
        var network = Path("vertical", "water", new LayoutPoint2(0, -10), new LayoutPoint2(0, 10));
        var decision = Assert.Single(Layout([Request("A", new LayoutPoint2(0, 0))], Scene(network)).Decisions);

        Assert.Equal(Math.PI / 2, decision.Placements[0].RotationRadians, 10);
        Assert.Equal(LayoutStatus.Resolved, decision.Status);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void AlmostVerticalNetwork_IsNumericallyStable()
    {
        var angle = 89.9 * Math.PI / 180;
        var direction = new LayoutVector2(Math.Cos(angle), Math.Sin(angle));
        var network = Path("almost-vertical", "water", new LayoutPoint2(0, 0), new LayoutPoint2(direction.X * 20, direction.Y * 20));
        var first = Assert.Single(Layout([Request("A", new LayoutPoint2(direction.X * 5, direction.Y * 5))], Scene(network)).Decisions);
        var second = Assert.Single(Layout([Request("A", new LayoutPoint2(direction.X * 5, direction.Y * 5))], Scene(network)).Decisions);

        Assert.InRange(first.Placements[0].RotationRadians, angle - 1e-10, angle + 1e-10);
        Assert.Equal(first.Placements[0].RotationRadians, second.Placements[0].RotationRadians, 12);
        Assert.Equal(first.CandidateId, second.CandidateId);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void DiagonalNetwork_PreservesLocalReferenceFrame()
    {
        var network = Path("diagonal", "water", new LayoutPoint2(-10, -10), new LayoutPoint2(10, 10));
        var decision = Assert.Single(Layout([Request("A", new LayoutPoint2(0, 0))], Scene(network)).Decisions);

        Assert.Equal(Math.PI / 4, decision.Placements[0].RotationRadians, 10);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void CurvedNetwork_UsesTangentOfNearestRegion()
    {
        var vertices = Enumerable.Range(0, 9)
            .Select(index => index * Math.PI / 16)
            .Select(angle => new LayoutPoint2(10 * Math.Cos(angle), 10 * Math.Sin(angle)))
            .ToArray();
        var network = new LayoutNetworkPath("curve", "water", "WATER", vertices);
        var anchor = new LayoutPoint2(10 * Math.Cos(Math.PI / 4), 10 * Math.Sin(Math.PI / 4));
        var decision = Assert.Single(Layout([Request("A", anchor)], Scene(network)).Decisions);
        var rotation = decision.Placements[0].RotationRadians;

        Assert.InRange(Math.Abs(rotation), (Math.PI / 4) - 0.15, (Math.PI / 4) + 0.15);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ExistingTextBlockingPreferredPosition_SelectsAnotherCandidate()
    {
        var obstacle = Obstacle("existing", LayoutObstacleKind.ExistingText, 0.45, 0.05, 0.65, 0.20);
        var decision = Assert.Single(Layout([Request("A", new LayoutPoint2(0, 0))], Scene(Horizontal(), obstacle)).Decisions);

        Assert.Equal(LayoutStatus.Resolved, decision.Status);
        Assert.NotEqual("preferred", decision.CandidateId);
        Assert.DoesNotContain(decision.Score.Reasons, reason => reason.StartsWith("HARD:", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void StructuralBlockRequiringBroadMovement_IsMarkedForReview()
    {
        var obstacle = Obstacle("pv", LayoutObstacleKind.Block, -0.2, -0.8, 1.0, 0.8);
        var decision = Assert.Single(Layout([Request("A", new LayoutPoint2(0, 0))], Scene(Horizontal(), obstacle)).Decisions);

        Assert.Equal(LayoutStatus.RequiresReview, decision.Status);
        Assert.Equal(LayoutDecisionKind.RequiresReview, decision.DecisionKind);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void NearbyPlannedAnnotations_NeverForceABroadAutomaticMove()
    {
        var result = Layout(
            [Request("A", new LayoutPoint2(0, 0)), Request("B", new LayoutPoint2(0.2, 0))],
            Scene(Horizontal()));
        var first = result.Decisions.Single(item => item.AnnotationId == "A");
        var second = result.Decisions.Single(item => item.AnnotationId == "B");

        Assert.Equal(LayoutStatus.RequiresReview, second.Status);
        Assert.Equal(LayoutDecisionKind.KeepLegacy, first.DecisionKind);
        Assert.True(second.MovementDistance <= 0.75 + LayoutGeometryTolerance.Distance);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void FullyBlockedRegion_IsExplicitlyMarkedForReview()
    {
        var obstacle = Obstacle("structure", LayoutObstacleKind.Structure, -10, -10, 10, 10);
        var decision = Assert.Single(Layout([Request("A", new LayoutPoint2(0, 0))], Scene(Horizontal(), obstacle)).Decisions);

        Assert.Equal(LayoutStatus.RequiresReview, decision.Status);
        Assert.Contains("ANNOTATION_LAYOUT_UNRESOLVED", decision.Diagnostics);
        Assert.True(decision.Score.HardConflictCount > 0);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void SameInputTwice_ProducesSameSemanticDecision()
    {
        var requests = new[] { Request("A", new LayoutPoint2(0, 0)), Request("B", new LayoutPoint2(0.2, 0)) };
        var scene = Scene(Horizontal());
        var first = Layout(requests, scene);
        var second = Layout(requests, scene);

        Assert.Equal(Semantic(first), Semantic(second));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void DistantObstacle_DoesNotChangeLocalDecision()
    {
        var request = Request("A", new LayoutPoint2(0, 0));
        var first = Assert.Single(Layout([request], Scene(Horizontal())).Decisions);
        var previous = new PreviousAnnotationLayout(first.CandidateId, first.Placements);
        var withPrevious = Request("A", new LayoutPoint2(0, 0), previous);
        var distant = Obstacle("far-away", LayoutObstacleKind.Block, 1000, 1000, 1010, 1010);
        var second = Assert.Single(Layout([withPrevious], Scene(Horizontal(), distant)).Decisions);

        Assert.StartsWith("existing:", second.CandidateId, StringComparison.Ordinal);
        Assert.Equal(LayoutDecisionKind.KeepExisting, second.DecisionKind);
        Assert.Equal(first.Placements.Select(item => item.Position), second.Placements.Select(item => item.Position));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void DepthAndDiameter_ArePlacedAsOneLogicalUnitWithDepthAbove()
    {
        var decision = Assert.Single(Layout([Request("A", new LayoutPoint2(0, 0))], Scene(Horizontal())).Decisions);
        var depth = decision.Placements.Single(item => item.Role == AnnotationTextRole.Depth);
        var diameter = decision.Placements.Single(item => item.Role == AnnotationTextRole.Diameter);

        Assert.True(depth.Position.Y > diameter.Position.Y);
        Assert.Equal(depth.RotationRadians, diameter.RotationRadians, 12);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void DecorativeClosedGeometryCannotProvideNetworkOrientation()
    {
        var decorative = Obstacle("REC", LayoutObstacleKind.Other, -1, -1, 1, 1);
        var decision = Assert.Single(Layout([Request("A", new LayoutPoint2(0, 0))], new LayoutScene([], [decorative])).Decisions);

        Assert.Equal(LayoutStatus.RequiresReview, decision.Status);
        Assert.Null(decision.AssociatedNetworkId);
        Assert.Contains("ANNOTATION_LAYOUT_NETWORK_NOT_FOUND", decision.Diagnostics);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void CloserCurveFromAnotherFamily_CannotDefineOrientation()
    {
        var wrong = new LayoutNetworkPath(
            "wrong", "sewer", "SEWER", [new LayoutPoint2(0, -10), new LayoutPoint2(0, 10)]);
        var correct = Path("correct", "water", new LayoutPoint2(-10, 0.2), new LayoutPoint2(10, 0.2));
        var scene = new LayoutScene([wrong, correct], []);
        var decision = Assert.Single(Layout([Request("A", new LayoutPoint2(0, 0))], scene).Decisions);

        Assert.Equal("water", decision.AssociatedNetworkId);
        Assert.Equal(0, decision.Placements[0].RotationRadians, 10);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void AnotherPathOfSameFamily_IsNotMistakenForAssociatedPath()
    {
        var associated = Horizontal();
        var crossing = Path("same-family-crossing", "water", new LayoutPoint2(1, -10), new LayoutPoint2(1, 10));
        var decision = Assert.Single(Layout(
            [Request("A", new LayoutPoint2(0, 0))],
            new LayoutScene([associated, crossing], [])).Decisions);

        Assert.NotEqual("preferred", decision.CandidateId);
        Assert.Equal(LayoutStatus.Resolved, decision.Status);
    }

    private static AnnotationLayoutBatchResult Layout(
        IEnumerable<AnnotationLayoutRequest> requests,
        LayoutScene scene) => new AnnotationLayoutEngine().Layout(requests, scene);

    private static AnnotationLayoutRequest Request(
        string id,
        LayoutPoint2 anchor,
        PreviousAnnotationLayout? previous = null) =>
        new(
            id,
            anchor,
            "water",
            ["WATER"],
            new AnnotationTextElement($"{id}:depth", AnnotationTextRole.Depth, "P.1.42", 0.5, 0.1),
            new AnnotationTextElement($"{id}:diameter", AnnotationTextRole.Diameter, "∅600", 0.5, -0.6),
            previous);

    private static LayoutNetworkPath Horizontal() =>
        Path("horizontal", "water", new LayoutPoint2(-100, 0), new LayoutPoint2(100, 0));

    private static LayoutNetworkPath Path(string id, string semanticId, params LayoutPoint2[] vertices) =>
        new(id, semanticId, "WATER", vertices);

    private static LayoutObstacle Obstacle(
        string id,
        LayoutObstacleKind kind,
        double minX,
        double minY,
        double maxX,
        double maxY) =>
        new(
            id,
            kind,
            new OrientedRect(
                new LayoutPoint2((minX + maxX) / 2, (minY + maxY) / 2),
                (maxX - minX) / 2,
                (maxY - minY) / 2,
                0));

    private static LayoutScene Scene(LayoutNetworkPath network, params LayoutObstacle[] obstacles) =>
        new([network], obstacles);

    private static string Semantic(AnnotationLayoutBatchResult result) => string.Join(
        "|",
        result.Decisions.Select(decision => string.Join(
            ";",
            decision.AnnotationId,
            decision.Status,
            decision.CandidateId,
            decision.Score.Total.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            string.Join(",", decision.Placements.Select(item =>
                FormattableString.Invariant($"{item.ItemId}:{item.Position.X:R}:{item.Position.Y:R}:{item.RotationRadians:R}"))))));
}
