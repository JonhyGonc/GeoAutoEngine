using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Tests;

public sealed class InspectionSharedCoveragePlannerTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void OppositeInspectionsLeaveOnlyCentralGapPending()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var plan = InspectionSharedCoveragePlanner.Create(
            100.0,
            [
                new InspectionCoverageContribution(first, InspectionDirection.Forward, 78.0),
                new InspectionCoverageContribution(second, InspectionDirection.Reverse, 17.0),
            ]);

        Assert.Collection(
            plan.InspectedIntervals,
            interval =>
            {
                Assert.Equal(0.0, interval.StartCadDistance, 8);
                Assert.Equal(78.0, interval.EndCadDistance, 8);
            },
            interval =>
            {
                Assert.Equal(83.0, interval.StartCadDistance, 8);
                Assert.Equal(100.0, interval.EndCadDistance, 8);
            });
        Assert.Collection(
            plan.RemainingIntervals,
            interval =>
            {
                Assert.Equal(78.0, interval.StartCadDistance, 8);
                Assert.Equal(83.0, interval.EndCadDistance, 8);
            });
        Assert.Equal([78.0, 83.0], plan.BreakCadDistances);
        Assert.Equal(95.0, plan.InspectedLength, 8);
        Assert.Equal(5.0, plan.RemainingLength, 8);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void OppositeInspectionsThatMeetCoverWholeGeometry()
    {
        var plan = InspectionSharedCoveragePlanner.Create(
            95.0,
            [
                new InspectionCoverageContribution(Guid.NewGuid(), InspectionDirection.Forward, 78.0),
                new InspectionCoverageContribution(Guid.NewGuid(), InspectionDirection.Reverse, 17.0),
            ]);

        var interval = Assert.Single(plan.InspectedIntervals);
        Assert.Equal(0.0, interval.StartCadDistance, 8);
        Assert.Equal(95.0, interval.EndCadDistance, 8);
        Assert.Empty(plan.RemainingIntervals);
        Assert.Single(plan.BreakCadDistances);
        Assert.Equal(78.0, plan.BreakCadDistances[0], 8);
        Assert.Equal(95.0, plan.InspectedLength, 8);
        Assert.Equal(0.0, plan.RemainingLength, 8);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void OverlappingOppositeInspectionsDoNotDoubleCountCoverage()
    {
        var plan = InspectionSharedCoveragePlanner.Create(
            90.0,
            [
                new InspectionCoverageContribution(Guid.NewGuid(), InspectionDirection.Forward, 78.0),
                new InspectionCoverageContribution(Guid.NewGuid(), InspectionDirection.Reverse, 17.0),
            ]);

        var interval = Assert.Single(plan.InspectedIntervals);
        Assert.Equal(0.0, interval.StartCadDistance, 8);
        Assert.Equal(90.0, interval.EndCadDistance, 8);
        Assert.Empty(plan.RemainingIntervals);
        Assert.Equal([73.0, 78.0], plan.BreakCadDistances);
        Assert.Equal(90.0, plan.InspectedLength, 8);
    }
}
