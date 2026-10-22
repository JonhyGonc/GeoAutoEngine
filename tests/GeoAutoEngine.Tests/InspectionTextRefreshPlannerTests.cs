using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Tests;

public sealed class InspectionTextRefreshPlannerTests
{
    private static readonly Guid ReportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SectionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    [Trait("Category", "FAST")]
    public void ChangedAnomalyName_UpdatesOnlyTheText()
    {
        var occurrenceId = Guid.NewGuid();
        var drawn = Single(Event(occurrenceId, InspectionLocationMode.Point, "Trinca", Location(0, 12)));
        var current = Single(Event(occurrenceId, InspectionLocationMode.Point, "Fissura", Location(0, 12)));

        var plan = Plan([current], [Existing(drawn)]);

        var item = Assert.Single(plan.Items);
        Assert.Equal(InspectionTextRefreshOutcome.Updated, item.Outcome);
        Assert.Equal("12,00 m\\PFISSURA", item.DesiredContents);
        Assert.Equal(1, plan.UpdatedCount);
        Assert.Empty(plan.OrphanedKeys);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void SameText_IsUnchanged()
    {
        var call = Single(Event(Guid.NewGuid(), InspectionLocationMode.Point, "Trinca", Location(0, 12)));

        var plan = Plan([call], [Existing(call)]);

        Assert.Equal(InspectionTextRefreshOutcome.Unchanged, Assert.Single(plan.Items).Outcome);
        Assert.Equal(0, plan.UpdatedCount);
        Assert.Equal(1, plan.UnchangedCount);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ChangedMeterage_RequiresRegenerationInsteadOfUpdatingText()
    {
        var occurrenceId = Guid.NewGuid();
        var drawn = Single(Event(occurrenceId, InspectionLocationMode.Point, "Trinca", Location(0, 12)));
        var current = Single(Event(occurrenceId, InspectionLocationMode.Point, "Fissura", Location(0, 14.5)));

        var plan = Plan([current], [Existing(drawn)]);

        Assert.Equal(InspectionTextRefreshOutcome.PositionChanged, Assert.Single(plan.Items).Outcome);
        Assert.Equal(1, plan.PositionChangedCount);
        Assert.Equal(0, plan.UpdatedCount);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void MeterageWithinTolerance_StillUpdatesText()
    {
        var occurrenceId = Guid.NewGuid();
        var drawn = Single(Event(occurrenceId, InspectionLocationMode.Point, "Trinca", Location(0, 12)));
        var current = Single(Event(occurrenceId, InspectionLocationMode.Point, "Fissura", Location(0, 12.004)));

        var plan = Plan([current], [Existing(drawn)]);

        Assert.Equal(InspectionTextRefreshOutcome.Updated, Assert.Single(plan.Items).Outcome);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ChangedRangeEnd_RequiresRegeneration()
    {
        var occurrenceId = Guid.NewGuid();
        var drawn = Single(Event(
            occurrenceId,
            InspectionLocationMode.Range,
            "Trinca",
            Location(0, 9, PositionedLocationRole.Start),
            Location(1, 11, PositionedLocationRole.End)));
        var current = Single(Event(
            occurrenceId,
            InspectionLocationMode.Range,
            "Fissura",
            Location(0, 9, PositionedLocationRole.Start),
            Location(1, 13, PositionedLocationRole.End)));

        var plan = Plan([current], [Existing(drawn)]);

        Assert.Equal(InspectionTextRefreshOutcome.PositionChanged, Assert.Single(plan.Items).Outcome);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void NewAndRemovedOccurrences_AreReportedWithoutBeingTouched()
    {
        var removed = Single(Event(Guid.NewGuid(), InspectionLocationMode.Point, "Raiz", Location(0, 5)));
        var added = Single(Event(Guid.NewGuid(), InspectionLocationMode.Point, "Fissura", Location(0, 20)));

        var plan = Plan([added], [Existing(removed)]);

        Assert.Equal(InspectionTextRefreshOutcome.MissingInDrawing, Assert.Single(plan.Items).Outcome);
        Assert.Equal(1, plan.MissingInDrawingCount);
        Assert.Equal(
            InspectionTextRefreshPlanner.StableKey(ReportId, SectionId, removed),
            Assert.Single(plan.OrphanedKeys));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void SyntheticFilmingStart_IsNotCountedAsAnOccurrence()
    {
        var filmingStart = Location(0, 0, PositionedLocationRole.Start);
        var calls = InspectionAnnotationPlanner.Create(
            [Event(Guid.NewGuid(), InspectionLocationMode.Point, "Trinca", Location(0, 12))],
            filmingStart);

        var plan = Plan(calls, []);

        Assert.Equal(2, plan.Items.Count);
        Assert.Equal(1, plan.MissingInDrawingCount);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ContentsAreEscapedLikeTheRenderer()
    {
        Assert.Equal("A\\\\B \\{C\\}", InspectionTextRefreshPlanner.EscapeMText("A\\B {C}"));
    }

    private static InspectionTextRefreshPlan Plan(
        IReadOnlyList<InspectionAnnotationCall> calls,
        IReadOnlyList<InspectionExistingCalloutText> existing) =>
        InspectionTextRefreshPlanner.Plan(ReportId, SectionId, calls, existing);

    private static InspectionExistingCalloutText Existing(InspectionAnnotationCall call) => new(
        InspectionTextRefreshPlanner.StableKey(ReportId, SectionId, call),
        InspectionTextRefreshPlanner.MTextContents(call),
        call.Anchor.InspectionDistance,
        call.IntervalEnd?.InspectionDistance);

    private static InspectionAnnotationCall Single(PositionedInspectionEvent positionedEvent) =>
        Assert.Single(InspectionAnnotationPlanner.Create([positionedEvent]));

    private static PositionedInspectionEvent Event(
        Guid occurrenceId,
        InspectionLocationMode mode,
        string description,
        params PositionedInspectionLocation[] locations) => new(
            occurrenceId,
            mode,
            description,
            locations,
            [],
            true);

    private static PositionedInspectionLocation Location(
        int index,
        double inspectionDistance,
        PositionedLocationRole role = PositionedLocationRole.Point) => new(
            index,
            role,
            inspectionDistance,
            inspectionDistance,
            new LinearPoint(inspectionDistance, 0, 0),
            new LinearVector(1, 0, 0));
}
