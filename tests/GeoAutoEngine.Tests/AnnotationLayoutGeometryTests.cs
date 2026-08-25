using GeoAutoEngine.Domain.AnnotationLayout;

namespace GeoAutoEngine.Tests;

public sealed class AnnotationLayoutGeometryTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void OrientedRect_DetectsRotatedIntersectionAndSeparation()
    {
        var first = new OrientedRect(new LayoutPoint2(0, 0), 2, 0.5, Math.PI / 4);
        var touching = new OrientedRect(new LayoutPoint2(1.4, 1.4), 1, 0.5, -Math.PI / 4);
        var distant = new OrientedRect(new LayoutPoint2(10, 10), 1, 1, 0);

        Assert.True(first.Intersects(touching));
        Assert.False(first.Intersects(distant));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void OrientedRect_TreatsEdgeTouchAsCollision()
    {
        var left = new OrientedRect(new LayoutPoint2(0, 0), 1, 1, 0);
        var right = new OrientedRect(new LayoutPoint2(2, 0), 1, 1, 0);

        Assert.True(left.Intersects(right));
        Assert.True(left.Expand(0.1).Intersects(right));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void OrientedRect_DetectsSmallAndLargeOverlapAtDifferentAngles()
    {
        var horizontal = new OrientedRect(new LayoutPoint2(0, 0), 2, 0.5, 0);
        var small = new OrientedRect(new LayoutPoint2(2.45, 0), 0.5, 0.5, Math.PI / 12);
        var large = new OrientedRect(new LayoutPoint2(0, 0), 1, 1, Math.PI / 2);

        Assert.True(horizontal.Intersects(small));
        Assert.True(horizontal.Intersects(large));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void OrientedRect_DetectsSegmentCrossingAfterRotation()
    {
        var rectangle = new OrientedRect(new LayoutPoint2(0, 0), 2, 0.5, Math.PI / 4);

        Assert.True(rectangle.IntersectsSegment(new LayoutPoint2(-3, -3), new LayoutPoint2(3, 3)));
        Assert.False(rectangle.IntersectsSegment(new LayoutPoint2(-3, 3), new LayoutPoint2(-2, 2)));
    }

    [Theory]
    [Trait("Category", "FAST")]
    [InlineData(0, 0)]
    [InlineData(3.141592653589793, 0)]
    [InlineData(-1.5707963267948966, 1.5707963267948966)]
    [InlineData(4.71238898038469, 1.5707963267948966)]
    public void ReadableAngle_IsCentralizedAndNeverUpsideDown(double input, double expected)
    {
        Assert.Equal(expected, LayoutAngles.NormalizeReadable(input), 10);
    }
}
