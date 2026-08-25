using GeoAutoEngine.Domain.AnnotationLayout;

namespace GeoAutoEngine.Tests;

public sealed class AnnotationGroupInvariantTests
{
    [Theory]
    [Trait("Category", "FAST")]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(89.9)]
    [InlineData(90)]
    public void RigidTranslation_PreservesAllLegacyInvariants(double degrees)
    {
        var fixture = Group(degrees);
        var translation = fixture.Tangent * 0.25;
        var candidate = fixture.Legacy.Select(item => Translate(item, translation)).ToArray();

        var result = AnnotationGroupInvariantValidator.Validate(fixture.Anchor, fixture.Legacy, candidate);

        Assert.True(result.IsValid);
        Assert.Equal(AnnotationGroupInvariantViolation.None, result.Violations);
        Assert.Empty(result.Details);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ExactCadFootprints_DoNotChangeTheVisualGroupIdentity()
    {
        var fixture = Group(45);
        var translation = fixture.Tangent * 0.25;
        var candidate = fixture.Legacy.Select(item =>
        {
            var position = item.Position + translation;
            return new LayoutTextPlacement(
                item.ItemId,
                item.Role,
                position,
                item.RotationRadians,
                new TextFootprint(
                    item.ItemId,
                    new OrientedRect(position, 0.83, 0.31, 0),
                    approximate: false));
        }).ToArray();

        var result = AnnotationGroupInvariantValidator.Validate(fixture.Anchor, fixture.Legacy, candidate);

        Assert.True(result.IsValid);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void SwappingDepthAndDiameterPositions_ViolatesOrder()
    {
        var fixture = Group(45);
        var depth = fixture.Legacy.Single(item => item.Role == AnnotationTextRole.Depth);
        var diameter = fixture.Legacy.Single(item => item.Role == AnnotationTextRole.Diameter);
        var candidate = new[]
        {
            At(depth, diameter.Position),
            At(diameter, depth.Position),
        };

        var result = AnnotationGroupInvariantValidator.Validate(fixture.Anchor, fixture.Legacy, candidate);

        Assert.True(result.Violations.HasFlag(AnnotationGroupInvariantViolation.Order));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RotatingOneMember_ViolatesRotation()
    {
        var fixture = Group(0);
        var candidate = fixture.Legacy.Select((item, index) => index == 0
            ? new LayoutTextPlacement(item.ItemId, item.Role, item.Position, item.RotationRadians + 0.1, item.Footprint)
            : item).ToArray();

        var result = AnnotationGroupInvariantValidator.Validate(fixture.Anchor, fixture.Legacy, candidate);

        Assert.True(result.Violations.HasFlag(AnnotationGroupInvariantViolation.Rotation));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void MovingOneMemberSeparately_ViolatesGroupIntegrity()
    {
        var fixture = Group(0);
        var candidate = fixture.Legacy.Select((item, index) =>
            index == 0 ? Translate(item, fixture.Tangent * 0.1) : item).ToArray();

        var result = AnnotationGroupInvariantValidator.Validate(fixture.Anchor, fixture.Legacy, candidate);

        Assert.True(result.Violations.HasFlag(AnnotationGroupInvariantViolation.GroupIntegrity));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RigidTranslationAcrossAnchor_ViolatesSide()
    {
        var fixture = Group(89.9);
        var translation = fixture.Normal * -0.9;
        var candidate = fixture.Legacy.Select(item => Translate(item, translation)).ToArray();

        var result = AnnotationGroupInvariantValidator.Validate(fixture.Anchor, fixture.Legacy, candidate);

        Assert.True(result.Violations.HasFlag(AnnotationGroupInvariantViolation.Side));
        Assert.False(result.Violations.HasFlag(AnnotationGroupInvariantViolation.GroupIntegrity));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void MovingAMemberOntoTheAnchorAxis_ViolatesSide()
    {
        var fixture = Group(0);
        var translation = fixture.Normal * -0.4;
        var candidate = fixture.Legacy.Select(item => Translate(item, translation)).ToArray();

        var result = AnnotationGroupInvariantValidator.Validate(fixture.Anchor, fixture.Legacy, candidate);

        Assert.True(result.Violations.HasFlag(AnnotationGroupInvariantViolation.Side));
    }

    private static GroupFixture Group(double degrees)
    {
        var rotation = degrees * Math.PI / 180;
        var tangent = new LayoutVector2(Math.Cos(rotation), Math.Sin(rotation));
        var normal = new LayoutVector2(-tangent.Y, tangent.X);
        var anchor = new LayoutPoint2(10, -4);
        return new GroupFixture(
            anchor,
            tangent,
            normal,
            [
                Placement("depth", AnnotationTextRole.Depth, anchor + (normal * 0.4), rotation),
                Placement("diameter", AnnotationTextRole.Diameter, anchor + (normal * -0.4), rotation),
            ]);
    }

    private static LayoutTextPlacement Placement(
        string id,
        AnnotationTextRole role,
        LayoutPoint2 position,
        double rotation) =>
        new(
            id,
            role,
            position,
            rotation,
            new TextFootprint(id, new OrientedRect(position, 0.5, 0.25, rotation), approximate: false));

    private static LayoutTextPlacement Translate(LayoutTextPlacement placement, LayoutVector2 translation)
    {
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
    }

    private static LayoutTextPlacement At(LayoutTextPlacement placement, LayoutPoint2 position)
    {
        var translation = position - placement.Position;
        return Translate(placement, translation);
    }

    private sealed record GroupFixture(
        LayoutPoint2 Anchor,
        LayoutVector2 Tangent,
        LayoutVector2 Normal,
        IReadOnlyList<LayoutTextPlacement> Legacy);
}
