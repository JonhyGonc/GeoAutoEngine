using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Tests;

public sealed class InspectionCoveragePlannerTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void ForwardCoverageBreaksAtInvestigatedMeterage()
    {
        var plan = InspectionCoveragePlanner.Create(
            68m,
            95.0,
            InspectionDirection.Forward);

        Assert.Equal(68.0, plan.InspectedLength, 8);
        Assert.Equal(27.0, plan.RemainingLength, 8);
        Assert.Equal(68.0, plan.SplitCadDistance, 8);
        Assert.True(plan.RequiresBreak);
        Assert.False(plan.IsFullyInspected);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ReverseCoverageBreaksFromOppositePolylineEnd()
    {
        var plan = InspectionCoveragePlanner.Create(
            68m,
            95.0,
            InspectionDirection.Reverse);

        Assert.Equal(68.0, plan.InspectedLength, 8);
        Assert.Equal(27.0, plan.RemainingLength, 8);
        Assert.Equal(27.0, plan.SplitCadDistance, 8);
        Assert.True(plan.RequiresBreak);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void InvestigatedLengthGreaterThanGeometryIsClampedToFullInspection()
    {
        var plan = InspectionCoveragePlanner.Create(
            120m,
            95.0,
            InspectionDirection.Forward);

        Assert.Equal(95.0, plan.InspectedLength, 8);
        Assert.Equal(0.0, plan.RemainingLength, 8);
        Assert.False(plan.RequiresBreak);
        Assert.True(plan.IsFullyInspected);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ZeroInvestigatedLengthDoesNotBreakOriginalGeometry()
    {
        var plan = InspectionCoveragePlanner.Create(
            0m,
            95.0,
            InspectionDirection.Forward);

        Assert.Equal(0.0, plan.InspectedLength, 8);
        Assert.Equal(95.0, plan.RemainingLength, 8);
        Assert.False(plan.RequiresBreak);
        Assert.False(plan.IsFullyInspected);
    }
}
