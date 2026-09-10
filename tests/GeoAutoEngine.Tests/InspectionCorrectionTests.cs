using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Tests;

public sealed class InspectionCorrectionTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void PointEditPreservesIdentityAndOriginalForRestore()
    {
        var occurrenceId = Guid.NewGuid();
        var section = Section(Occurrence(
            occurrenceId,
            InspectionLocationMode.Point,
            "Point",
            meterage: 12.50m,
            description: "Texto original"));

        section.ApplyOccurrenceEdit(new InspectionOccurrenceEdit(
            section.SectionId,
            occurrenceId,
            13.75m,
            null,
            [],
            "Texto corrigido"));

        var edited = Assert.Single(section.Occurrences);
        Assert.Equal(occurrenceId, edited.OccurrenceId);
        Assert.Equal(13.75m, edited.Meterage);
        Assert.Equal("Texto corrigido", edited.EffectiveDescription);
        Assert.True(section.IsOccurrenceEdited(occurrenceId));
        Assert.Equal(1, section.EditedOccurrenceCount);

        Assert.True(section.RestoreOccurrence(occurrenceId));
        var restored = Assert.Single(section.Occurrences);
        Assert.Equal(12.50m, restored.Meterage);
        Assert.Equal("Texto original", restored.EffectiveDescription);
        Assert.False(section.IsOccurrenceEdited(occurrenceId));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RangeEditUpdatesBothLimitsAndRejectsInvertedRange()
    {
        var occurrenceId = Guid.NewGuid();
        var section = Section(new InspectionOccurrenceSession(
            occurrenceId,
            0,
            InspectionLocationMode.Range,
            "Range",
            10m,
            20m,
            [],
            null,
            null,
            null,
            "Fissura",
            false,
            false,
            null,
            null));

        section.ApplyOccurrenceEdit(new InspectionOccurrenceEdit(
            section.SectionId,
            occurrenceId,
            11.25m,
            22.50m,
            [],
            "Fissura corrigida"));

        var edited = Assert.Single(section.Occurrences);
        Assert.Equal(11.25m, edited.Meterage);
        Assert.Equal(22.50m, edited.EndMeterage);
        Assert.Equal("Fissura corrigida", edited.EffectiveDescription);

        var exception = Assert.Throws<ArgumentException>(() => section.ApplyOccurrenceEdit(
            new InspectionOccurrenceEdit(
                section.SectionId,
                occurrenceId,
                30m,
                25m,
                [],
                "Intervalo inválido")));

        Assert.Contains("final", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(11.25m, Assert.Single(section.Occurrences).Meterage);
        Assert.Equal(22.50m, Assert.Single(section.Occurrences).EndMeterage);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void FromEditChangesStartMeterageAndDescription()
    {
        var occurrenceId = Guid.NewGuid();
        var section = Section(new InspectionOccurrenceSession(
            occurrenceId,
            0,
            InspectionLocationMode.From,
            "From",
            35m,
            null,
            [],
            null,
            null,
            null,
            "Infiltração",
            false,
            false,
            null,
            null));

        section.ApplyOccurrenceEdit(new InspectionOccurrenceEdit(
            section.SectionId,
            occurrenceId,
            38.40m,
            null,
            [],
            "Infiltração corrigida"));

        var edited = Assert.Single(section.Occurrences);
        Assert.Equal(38.40m, edited.Meterage);
        Assert.Null(edited.EndMeterage);
        Assert.Equal("Infiltração corrigida", edited.EffectiveDescription);
        Assert.True(section.IsOccurrenceEdited(occurrenceId));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void EntireSectionEditChangesOnlyDisplayedDescription()
    {
        var occurrenceId = Guid.NewGuid();
        var section = Section(new InspectionOccurrenceSession(
            occurrenceId,
            0,
            InspectionLocationMode.EntireSection,
            "EntireSection",
            null,
            null,
            [],
            null,
            null,
            null,
            "Deslocamento das juntas em todo trecho filmado",
            false,
            false,
            null,
            null));

        section.ApplyOccurrenceEdit(new InspectionOccurrenceEdit(
            section.SectionId,
            occurrenceId,
            999m,
            1000m,
            [1m, 2m],
            "Deslocamento corrigido em todo trecho filmado"));

        var edited = Assert.Single(section.Occurrences);
        Assert.Null(edited.Meterage);
        Assert.Null(edited.EndMeterage);
        Assert.Empty(edited.MeteragePoints);
        Assert.Equal("Deslocamento corrigido em todo trecho filmado", edited.EffectiveDescription);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void CorrectionPushContainsOnlyEditedOccurrences()
    {
        var editedId = Guid.NewGuid();
        var untouchedId = Guid.NewGuid();
        var section = Section(
            Occurrence(editedId, InspectionLocationMode.Point, "Point", 10m, "Primeira"),
            Occurrence(untouchedId, InspectionLocationMode.Point, "Point", 20m, "Segunda"));
        var reportId = Guid.NewGuid();
        var session = Session(reportId, section);

        section.ApplyOccurrenceEdit(new InspectionOccurrenceEdit(
            section.SectionId,
            editedId,
            11.25m,
            null,
            [],
            "Primeira corrigida"));

        var push = session.CreateCorrectionPush();

        Assert.Equal(reportId, push.ReportId);
        Assert.Equal(1, session.EditedOccurrenceCount);
        var correction = Assert.Single(push.Corrections);
        Assert.Equal(section.SectionId, correction.SectionId);
        Assert.Equal(editedId, correction.OccurrenceId);
        Assert.Equal("Point", correction.LocationMode);
        Assert.Equal(11.25m, correction.Meterage);
        Assert.Equal("Primeira corrigida", correction.Description);
        Assert.DoesNotContain(push.Corrections, item => item.OccurrenceId == untouchedId);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void AcceptedCorrectionsBecomeNewSynchronizedBaseline()
    {
        var occurrenceId = Guid.NewGuid();
        var section = Section(Occurrence(
            occurrenceId,
            InspectionLocationMode.Point,
            "Point",
            10m,
            "Texto original"));
        var session = Session(Guid.NewGuid(), section);

        section.ApplyOccurrenceEdit(new InspectionOccurrenceEdit(
            section.SectionId,
            occurrenceId,
            12.50m,
            null,
            [],
            "Texto sincronizado"));

        Assert.Equal(1, session.EditedOccurrenceCount);
        Assert.Single(session.CreateCorrectionPush().Corrections);

        var accepted = session.AcceptCurrentCorrectionsAsBaseline();

        Assert.Equal(1, accepted);
        Assert.Equal(0, session.EditedOccurrenceCount);
        Assert.Empty(session.CreateCorrectionPush().Corrections);
        var baseline = section.FindOriginalOccurrence(occurrenceId);
        Assert.NotNull(baseline);
        Assert.Equal(12.50m, baseline!.Meterage);
        Assert.Equal("Texto sincronizado", baseline.EffectiveDescription);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RestoreAfterSynchronizationReturnsToLastAcceptedValues()
    {
        var occurrenceId = Guid.NewGuid();
        var section = Section(Occurrence(
            occurrenceId,
            InspectionLocationMode.Point,
            "Point",
            10m,
            "Primeira versão"));

        section.ApplyOccurrenceEdit(new InspectionOccurrenceEdit(
            section.SectionId,
            occurrenceId,
            12m,
            null,
            [],
            "Versão aceita"));
        section.AcceptCurrentCorrectionsAsBaseline();

        section.ApplyOccurrenceEdit(new InspectionOccurrenceEdit(
            section.SectionId,
            occurrenceId,
            15m,
            null,
            [],
            "Nova alteração local"));

        Assert.True(section.RestoreOccurrence(occurrenceId));
        var restored = Assert.Single(section.Occurrences);
        Assert.Equal(12m, restored.Meterage);
        Assert.Equal("Versão aceita", restored.EffectiveDescription);
        Assert.False(section.IsOccurrenceEdited(occurrenceId));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void MultiPointEditCannotChangeNumberOfTechnicalPoints()
    {
        var occurrenceId = Guid.NewGuid();
        var section = Section(new InspectionOccurrenceSession(
            occurrenceId,
            0,
            InspectionLocationMode.MultiPoint,
            "MultiPoint",
            null,
            null,
            [2m, 4m],
            null,
            null,
            null,
            "Juntas",
            false,
            false,
            null,
            null));

        var exception = Assert.Throws<ArgumentException>(() => section.ApplyOccurrenceEdit(
            new InspectionOccurrenceEdit(
                section.SectionId,
                occurrenceId,
                null,
                null,
                [2m, 4m, 6m],
                "Juntas corrigidas")));

        Assert.Contains("mesma quantidade", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(section.IsOccurrenceEdited(occurrenceId));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task CorrectionMessageTypesRoundTripThroughGadxHeader()
    {
        await using var stream = new MemoryStream();
        var push = new InspectionCorrectionPush
        {
            ReportId = Guid.NewGuid(),
            GeneratedAtUtc = DateTime.UtcNow,
            Corrections = [],
        };

        await InspectionPipeProtocol.WriteFrameAsync(
            stream,
            InspectionPipeMessageType.CorrectionPush,
            901,
            InspectionPipeProtocol.SerializePayload(push));
        stream.Position = 0;

        var frame = await InspectionPipeProtocol.ReadFrameAsync(stream);

        Assert.Equal(InspectionPipeMessageType.CorrectionPush, frame.MessageType);
        Assert.Equal(901, frame.RequestId);
        Assert.Equal(push.ReportId, InspectionPipeProtocol.DeserializePayload<InspectionCorrectionPush>(frame.Payload).ReportId);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task CorrectionAcknowledgementRoundTripsAppliedAndRejectedCounts()
    {
        await using var stream = new MemoryStream();
        var acknowledgement = new InspectionCorrectionAcknowledgement
        {
            Accepted = true,
            Status = "Partial",
            Message = "Uma correção aplicada e uma rejeitada.",
            AppliedCount = 1,
            RejectedCount = 1,
        };

        await InspectionPipeProtocol.WriteFrameAsync(
            stream,
            InspectionPipeMessageType.CorrectionAck,
            902,
            InspectionPipeProtocol.SerializePayload(acknowledgement));
        stream.Position = 0;

        var frame = await InspectionPipeProtocol.ReadFrameAsync(stream);
        var received = InspectionPipeProtocol.DeserializePayload<InspectionCorrectionAcknowledgement>(frame.Payload);

        Assert.Equal(InspectionPipeMessageType.CorrectionAck, frame.MessageType);
        Assert.Equal(902, frame.RequestId);
        Assert.True(received.Accepted);
        Assert.Equal("Partial", received.Status);
        Assert.Equal(1, received.AppliedCount);
        Assert.Equal(1, received.RejectedCount);
    }

    private static VideoInspectionSession Session(Guid reportId, InspectionSectionSession section) => new(
        Guid.NewGuid(),
        "Projeto",
        reportId,
        "REL-01",
        "Relatório",
        "Cliente",
        "Área",
        "Contrato",
        null,
        DateTime.UtcNow,
        [section]);

    private static InspectionSectionSession Section(params InspectionOccurrenceSession[] occurrences) => new(
        Guid.NewGuid(),
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
        occurrences);

    private static InspectionOccurrenceSession Occurrence(
        Guid occurrenceId,
        InspectionLocationMode mode,
        string wireMode,
        decimal? meterage,
        string description) => new(
            occurrenceId,
            0,
            mode,
            wireMode,
            meterage,
            null,
            [],
            null,
            null,
            null,
            description,
            false,
            false,
            null,
            null);
}
