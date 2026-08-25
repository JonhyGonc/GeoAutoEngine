using GeoAutoEngine.Domain.AnnotationLayout;

namespace GeoAutoEngine.Tests;

public sealed class LayoutRealityPolicyTests
{
    [Theory]
    [Trait("Category", "FAST")]
    [InlineData("AcDbText")]
    [InlineData("AcDbMText")]
    [InlineData("AcDbAttribute")]
    [InlineData("AeccDbPointLabel")]
    public void VisualTextTypes_AreObstacles(string entityType)
    {
        var classification = LayoutRealityPolicy.Classify(entityType);

        Assert.Equal(LayoutObstacleKind.ExistingText, classification.ObstacleKind);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void AnnotatedSurveyBlock_IsAnObstacle()
    {
        var classification = LayoutRealityPolicy.Classify("AcDbBlockReference", isAnnotatedBlock: true);

        Assert.Equal(LayoutObstacleKind.Block, classification.ObstacleKind);
        Assert.Equal("AnnotatedBlockGeometricExtents", classification.Reason);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void CogoPoint_IsAVisualSymbolObstacle()
    {
        var classification = LayoutRealityPolicy.Classify("AeccDbCogoPoint");

        Assert.Equal(LayoutObstacleKind.Other, classification.ObstacleKind);
    }

    [Theory]
    [Trait("Category", "FAST")]
    [InlineData("AcDbBlockReference", true, false, "XrefNotCapturedAsSingleObstacle")]
    [InlineData("AcDbProxyEntity", false, false, "UnsupportedProxyEntity")]
    [InlineData("AeccDbSurface", false, false, "UnsupportedCivilEntityType")]
    public void BroadOrUnsupportedEntities_AreNotBlindlyCaptured(
        string entityType,
        bool isXref,
        bool isAnnotated,
        string reason)
    {
        var classification = LayoutRealityPolicy.Classify(entityType, isXref, isAnnotated);

        Assert.False(classification.IsObstacle);
        Assert.Equal(reason, classification.Reason);
    }
}
