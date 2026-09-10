using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Tests;

public sealed class InspectionCorrectionCadIdentityTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void PointEditMovesCalloutWithoutChangingStableCadIdentity()
    {
        var reportId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var section = Section(
            sectionId,
            Occurrence(
                occurrenceId,
                InspectionLocationMode.Point,
                "Point",
                12.50m,
                null,
                [],
                "Texto original"));
        var geometry = new StraightGeometry(100);
        var engine = new LinearReferenceEngine();

        var before = engine.Resolve(
            Assert.Single(section.Occurrences),
            section.InvestigatedMeters,
            geometry,
            InspectionDirection.Forward);
        var beforeCall = Assert.Single(InspectionAnnotationPlanner.Create([before]));
        var beforeKey = StableKey(reportId, sectionId, beforeCall.Anchor, occurrenceId);

        section.ApplyOccurrenceEdit(new InspectionOccurrenceEdit(
            sectionId,
            occurrenceId,
            13.75m,
            null,
            [],
            "Texto corrigido"));

        var after = engine.Resolve(
            Assert.Single(section.Occurrences),
            section.InvestigatedMeters,
            geometry,
            InspectionDirection.Forward);
        var afterCall = Assert.Single(InspectionAnnotationPlanner.Create([after]));
        var afterKey = StableKey(reportId, sectionId, afterCall.Anchor, occurrenceId);

        Assert.Equal(beforeKey, afterKey);
        Assert.Equal(12.50, beforeCall.Anchor.CadDistance, 8);
        Assert.Equal(13.75, afterCall.Anchor.CadDistance, 8);
        Assert.Equal("13,75 m", afterCall.LocationLine);
        Assert.Equal("TEXTO CORRIGIDO", afterCall.Cause);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RangeEditPreservesCallIdentityAndUpdatesBothTechnicalLimits()
    {
        var reportId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var section = Section(
            sectionId,
            Occurrence(
                occurrenceId,
                InspectionLocationMode.Range,
                "Range",
                10m,
                20m,
                [],
                "Fissura"));
        var geometry = new StraightGeometry(100);
        var engine = new LinearReferenceEngine();

        var before = engine.Resolve(
            Assert.Single(section.Occurrences),
            section.InvestigatedMeters,
            geometry,
            InspectionDirection.Forward);
        var beforeCall = Assert.Single(InspectionAnnotationPlanner.Create([before]));
        var beforeKey = StableKey(reportId, sectionId, beforeCall.Anchor, occurrenceId);

        section.ApplyOccurrenceEdit(new InspectionOccurrenceEdit(
            sectionId,
            occurrenceId,
            11.25m,
            22.50m,
            [],
            "Fissura corrigida"));

        var after = engine.Resolve(
            Assert.Single(section.Occurrences),
            section.InvestigatedMeters,
            geometry,
            InspectionDirection.Forward);
        var afterCall = Assert.Single(InspectionAnnotationPlanner.Create([after]));
        var afterKey = StableKey(reportId, sectionId, afterCall.Anchor, occurrenceId);

        Assert.Equal(beforeKey, afterKey);
        Assert.Equal("11,25 m\n22,50 m", afterCall.LocationLine);
        Assert.NotNull(afterCall.IntervalEnd);
        Assert.Equal(22.50, afterCall.IntervalEnd!.CadDistance, 8);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void MultiPointEditKeepsOneStableIdentityPerOriginalPoint()
    {
        var reportId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var section = Section(
            sectionId,
            Occurrence(
                occurrenceId,
                InspectionLocationMode.MultiPoint,
                "MultiPoint",
                null,
                null,
                [5m, 10m, 15m],
                "Juntas"));
        var geometry = new StraightGeometry(100);
        var engine = new LinearReferenceEngine();

        var before = engine.Resolve(
            Assert.Single(section.Occurrences),
            section.InvestigatedMeters,
            geometry,
            InspectionDirection.Forward);
        var beforeKeys = InspectionAnnotationPlanner.Create([before])
            .Select(call => StableKey(reportId, sectionId, call.Anchor, occurrenceId))
            .ToArray();

        section.ApplyOccurrenceEdit(new InspectionOccurrenceEdit(
            sectionId,
            occurrenceId,
            null,
            null,
            [6m, 12m, 18m],
            "Juntas corrigidas"));

        var after = engine.Resolve(
            Assert.Single(section.Occurrences),
            section.InvestigatedMeters,
            geometry,
            InspectionDirection.Forward);
        var afterCalls = InspectionAnnotationPlanner.Create([after]);
        var afterKeys = afterCalls
            .Select(call => StableKey(reportId, sectionId, call.Anchor, occurrenceId))
            .ToArray();

        Assert.Equal(beforeKeys, afterKeys);
        Assert.Equal([6d, 12d, 18d], afterCalls.Select(call => call.Anchor.CadDistance));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void EditedPointOutsideAssociatedGeometryProducesNoDesiredCallout()
    {
        var sectionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var section = Section(
            sectionId,
            Occurrence(
                occurrenceId,
                InspectionLocationMode.Point,
                "Point",
                10m,
                null,
                [],
                "Ocorrência"));
        var geometry = new StraightGeometry(100);
        var engine = new LinearReferenceEngine();

        section.ApplyOccurrenceEdit(new InspectionOccurrenceEdit(
            sectionId,
            occurrenceId,
            120m,
            null,
            [],
            "Ocorrência reposicionada"));

        var positioned = engine.Resolve(
            Assert.Single(section.Occurrences),
            section.InvestigatedMeters,
            geometry,
            InspectionDirection.Forward);
        var calls = InspectionAnnotationPlanner.Create([positioned]);

        Assert.False(positioned.CanCommit);
        Assert.Empty(calls);
        Assert.Contains(
            positioned.Warnings,
            warning => warning.Contains("fora da geometria", StringComparison.OrdinalIgnoreCase));
    }

    private static string StableKey(
        Guid reportId,
        Guid sectionId,
        PositionedInspectionLocation location,
        Guid occurrenceId) => VideoInspectionStableKey.Create(
            reportId,
            sectionId,
            occurrenceId,
            location.LocationIndex,
            location.Role);

    private static InspectionSectionSession Section(
        Guid sectionId,
        InspectionOccurrenceSession occurrence) => new(
            sectionId,
            Guid.NewGuid(),
            "Local",
            "Trecho",
            null,
            300m,
            null,
            null,
            null,
            100m,
            0,
            [],
            [occurrence]);

    private static InspectionOccurrenceSession Occurrence(
        Guid occurrenceId,
        InspectionLocationMode locationMode,
        string wireLocationMode,
        decimal? meterage,
        decimal? endMeterage,
        IReadOnlyList<decimal> meteragePoints,
        string description) => new(
            occurrenceId,
            0,
            locationMode,
            wireLocationMode,
            meterage,
            endMeterage,
            meteragePoints,
            null,
            null,
            null,
            description,
            false,
            false,
            null,
            null);

    private sealed class StraightGeometry(double length) : ILinearReferenceGeometry
    {
        public double Length { get; } = length;
        public LinearPoint GetPointAtDistance(double distance) => new(distance, 0, 0);
        public LinearVector GetTangentAtDistance(double distance) => new(1, 0, 0);
    }
}
