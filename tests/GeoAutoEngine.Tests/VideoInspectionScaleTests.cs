using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Tests;

public sealed class VideoInspectionScaleTests
{
    [Theory]
    [InlineData(125, 0.25, 1.00)]
    [InlineData(200, 0.40, 1.60)]
    [InlineData(250, 0.50, 2.00)]
    [InlineData(500, 1.00, 4.00)]
    [InlineData(750, 1.50, 6.00)]
    [Trait("Category", "FAST")]
    public void SupportedScaleUsesConfiguredTextHeightAndBaseLeaderLength(
        int denominator,
        double expectedTextHeight,
        double expectedLeaderLength)
    {
        var scale = InspectionCalloutScaleProfile.Resolve(denominator);

        Assert.Equal(denominator, scale.ScaleDenominator);
        Assert.Equal(expectedTextHeight, scale.TextHeight, 8);
        Assert.Equal(expectedLeaderLength, scale.BaseCalloutLength, 8);
    }

    [Theory]
    [InlineData(125)]
    [InlineData(200)]
    [InlineData(250)]
    [InlineData(500)]
    [InlineData(750)]
    [Trait("Category", "FAST")]
    public void LayoutStartsWithTheLeaderLengthDefinedBySelectedScale(int denominator)
    {
        var scale = InspectionCalloutScaleProfile.Resolve(denominator);
        var location = new PositionedInspectionLocation(
            0,
            PositionedLocationRole.Point,
            10,
            10,
            new LinearPoint(10, 0, 0),
            new LinearVector(1, 0, 0));
        var call = new InspectionAnnotationCall(
            Guid.NewGuid(),
            InspectionLocationMode.Point,
            location,
            "10,00 m",
            "ANOMALIA");

        var placement = Assert.Single(InspectionCalloutLayoutEngine.Arrange([call], scale));
        var deltaX = placement.LeaderEndPoint.X - placement.LeaderStartPoint.X;
        var deltaY = placement.LeaderEndPoint.Y - placement.LeaderStartPoint.Y;
        var leaderLength = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

        Assert.Equal(scale.BaseCalloutLength, leaderLength, 8);
        Assert.InRange(leaderLength, 0, scale.MaxCalloutLength);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void UnsupportedScaleFallsBackToDefaultOneToTwoHundredFifty()
    {
        var scale = InspectionCalloutScaleProfile.Resolve(333);

        Assert.Equal(250, scale.ScaleDenominator);
        Assert.Equal(0.50, scale.TextHeight, 8);
        Assert.Equal(2.00, scale.BaseCalloutLength, 8);
    }
}
