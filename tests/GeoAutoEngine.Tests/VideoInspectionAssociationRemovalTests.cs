using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Tests;

public sealed class VideoInspectionAssociationRemovalTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void RemoveAssociationReturnsSectionToPendingWithoutChangingInspectionData()
    {
        var sectionId = Guid.NewGuid();
        var occurrence = new InspectionOccurrenceSession(
            Guid.NewGuid(),
            0,
            InspectionLocationMode.Point,
            "Point",
            12m,
            null,
            Array.Empty<decimal>(),
            null,
            null,
            null,
            "ANOMALIA",
            false,
            false,
            null,
            null);
        var section = new InspectionSectionSession(
            sectionId,
            Guid.NewGuid(),
            "PV1",
            "TRECHO 01",
            null,
            150m,
            "PV1",
            "PV2",
            null,
            78m,
            0,
            Array.Empty<InspectionVideoReferenceSession>(),
            [occurrence]);
        var association = new CadSectionAssociation(
            Guid.NewGuid(),
            sectionId,
            "ABC",
            InspectionDirection.Forward,
            100.0);

        section.Associate(association);
        section.RemoveAssociation();

        Assert.Null(section.Association);
        Assert.Empty(section.PositionedEvents);
        Assert.Equal(InspectionSectionStatus.Pending, section.Status);
        Assert.Equal("Associação de geometria removida.", section.StatusMessage);
        Assert.Equal(78m, section.InvestigatedMeters);
        Assert.Single(section.Occurrences);
        Assert.Equal(12m, section.Occurrences[0].Meterage);
    }
}
