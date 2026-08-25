using System.Diagnostics;
using GeoAutoEngine.Domain.AnnotationLayout;
using Xunit.Abstractions;

namespace GeoAutoEngine.Tests;

public sealed class AnnotationLayoutBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public AnnotationLayoutBenchmarkTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "BENCHMARK")]
    public void CleanFiveThousand_AllValidLegacyShortCircuits()
    {
        const int count = 5_000;
        var requests = Requests(count);
        var scene = Scene(count, dense: false);
        var result = Measure("CLEAN_HYBRID", new AnnotationLayoutEngine(), requests, scene);

        Assert.Equal(count, result.Statistics.LegacyAcceptedCount);
        Assert.Equal(count, result.Statistics.KeptLegacyCount);
        Assert.Equal(count, result.Statistics.CandidateCount);
        Assert.Equal(0, result.Statistics.CorrectedCount);
        Assert.Equal(0, result.Statistics.UnnecessaryMoveCount);
        Assert.Equal(0, result.Statistics.UnresolvedCount);
    }

    [Fact]
    [Trait("Category", "BENCHMARK")]
    public void DenseOneThousand_ComparisonShowsConservativeHybridPolicy()
    {
        const int count = 1_000;
        var requests = Requests(count);
        var scene = Scene(count, dense: true);
        var before = Measure(
            "DENSE_PHASE_A_FREE_SCORE",
            new AnnotationLayoutEngine(new AnnotationLayoutOptions(strategy: AnnotationLayoutStrategy.PhaseAFreeScore)),
            requests,
            scene);
        var after = Measure("DENSE_HYBRID_SIMPLE", new AnnotationLayoutEngine(), requests, scene);

        Assert.InRange(after.Statistics.LegacyRejectedCount, 200, 400);
        Assert.True(after.Statistics.CorrectedCount > 0);
        Assert.Equal(0, after.Statistics.UnnecessaryMoveCount);
        Assert.True(after.Statistics.CandidateCount < before.Statistics.CandidateCount);
        Assert.True(after.Statistics.MaximumCorrectionDistance <= 0.75 + LayoutGeometryTolerance.Distance);
        Assert.Equal(0, after.Statistics.CorrectionsOver075Count);
    }

    private AnnotationLayoutBatchResult Measure(
        string name,
        AnnotationLayoutEngine engine,
        IReadOnlyList<AnnotationLayoutRequest> requests,
        LayoutScene scene)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = engine.Layout(requests, scene);
        stopwatch.Stop();
        var statistics = result.Statistics;
        _output.WriteLine(
            "{0}; annotations={1}; legacyAccepted={2}; legacyRejected={3}; keptExisting={4}; keptLegacy={5}; " +
            "corrected={6}; requiresReview={7}; unnecessaryMoves={8}; averageMovement={9:F4}; " +
            "maximumMovement={10:F4}; sideChanges={11}; upTo025={12}; from025To050={13}; " +
            "from050To075={14}; over075={15}; candidates={16}; spatialQueries={17}; elapsedMs={18:F3}",
            name,
            statistics.AnnotationCount,
            statistics.LegacyAcceptedCount,
            statistics.LegacyRejectedCount,
            statistics.KeptExistingCount,
            statistics.KeptLegacyCount,
            statistics.CorrectedCount,
            statistics.UnresolvedCount,
            statistics.UnnecessaryMoveCount,
            statistics.AverageCorrectionDistance,
            statistics.MaximumCorrectionDistance,
            statistics.SideChangeCount,
            statistics.CorrectionsUpTo025Count,
            statistics.Corrections025To050Count,
            statistics.Corrections050To075Count,
            statistics.CorrectionsOver075Count,
            statistics.CandidateCount,
            statistics.SpatialQueryCount,
            stopwatch.Elapsed.TotalMilliseconds);
        return result;
    }

    private static IReadOnlyList<AnnotationLayoutRequest> Requests(int count)
    {
        var metrics = new ApproximateTextMetricsProvider();
        return Enumerable.Range(0, count).Select(index =>
        {
            var anchor = new LayoutPoint2(index * 4, 0);
            var depth = new AnnotationTextElement($"depth-{index}", AnnotationTextRole.Depth, "P.1.42", 0.5, 0.1);
            var diameter = new AnnotationTextElement($"diameter-{index}", AnnotationTextRole.Diameter, "∅600", 0.5, -0.6);
            var placements = new[] { depth, diameter }.Select(text =>
            {
                var position = new LayoutPoint2(anchor.X, text.PerpendicularOffset);
                var footprint = TextFootprint.Create(text, position, 0, metrics.Measure(text));
                return new LayoutTextPlacement(text.ItemId, text.Role, position, 0, footprint);
            }).ToArray();
            return new AnnotationLayoutRequest(
                $"annotation-{index:000000}",
                anchor,
                "water",
                ["WATER"],
                depth,
                diameter,
                legacyLayout: new LegacyAnnotationLayout("v1-real", anchor, new LayoutVector2(1, 0), placements));
        }).ToArray();
    }

    private static LayoutScene Scene(int count, bool dense)
    {
        var networks = new List<LayoutNetworkPath>
        {
            new("main", "water", "WATER", [new LayoutPoint2(-1, 0), new LayoutPoint2((count * 4) + 1, 0)]),
        };
        var obstacles = new List<LayoutObstacle>();
        if (dense)
        {
            networks.Add(new LayoutNetworkPath(
                "parallel", "sewer", "SEWER",
                [new LayoutPoint2(-1, 1.2), new LayoutPoint2((count * 4) + 1, 1.2)]));
            for (var index = 0; index < count; index++)
            {
                var x = index * 4;
                if (index % 4 == 0)
                {
                    obstacles.Add(Obstacle(
                        $"block-{index}", LayoutObstacleKind.Block, x + 0.45, 0.05, x + 0.65, 0.20,
                        LayoutObstacleMeasurementSource.PlannedConservativeBounds));
                }
                else if (index % 10 == 1)
                {
                    obstacles.Add(Obstacle(
                        $"existing-text-{index}", LayoutObstacleKind.ExistingText, x + 0.45, 0.05, x + 0.65, 0.20,
                        LayoutObstacleMeasurementSource.ExistingTextGeometricExtents));
                }

                if (index % 10 == 2)
                {
                    networks.Add(new LayoutNetworkPath(
                        $"crossing-{index}", "sewer", "SEWER",
                        [new LayoutPoint2(x + 0.6, -1), new LayoutPoint2(x + 0.6, 1)]));
                }
            }
        }

        return new LayoutScene(networks, obstacles);
    }

    private static LayoutObstacle Obstacle(
        string id,
        LayoutObstacleKind kind,
        double minX,
        double minY,
        double maxX,
        double maxY,
        LayoutObstacleMeasurementSource source) =>
        new(
            id,
            kind,
            new OrientedRect(
                new LayoutPoint2((minX + maxX) / 2, (minY + maxY) / 2),
                (maxX - minX) / 2,
                (maxY - minY) / 2,
                0),
            measurementSource: source);
}
