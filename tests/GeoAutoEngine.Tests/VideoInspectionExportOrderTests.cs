using System.Text.Json;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Tests;

public sealed class VideoInspectionExportOrderTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void ImportedSectionsPreserveJsonExportSequenceEvenWhenOrderValuesAreStale()
    {
        var locationId = Guid.NewGuid();
        var document = new InspectionExchangeDocument
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Project = new InspectionExchangeProject
            {
                ProjectId = Guid.NewGuid(),
                Name = "Projeto",
            },
            Report = new InspectionExchangeReport
            {
                ReportId = Guid.NewGuid(),
                ReportCode = "REL-01",
                Name = "Relatório",
            },
            Locations =
            [
                new InspectionExchangeLocation
                {
                    LocationId = locationId,
                    Name = "Local",
                    Order = 0,
                },
            ],
            Sections =
            [
                Section(locationId, "Primeiro exportado", 50),
                Section(locationId, "Segundo exportado", 1),
                Section(locationId, "Terceiro exportado", 20),
            ],
        };

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var imported = new InspectionExchangeSerializer().Deserialize(json);
        var session = VideoInspectionSessionMapper.Map(imported);

        Assert.Equal(
            ["Primeiro exportado", "Segundo exportado", "Terceiro exportado"],
            session.Sections.Select(section => section.Name));
        Assert.Equal([0, 1, 2], session.Sections.Select(section => section.Order));
    }

    private static InspectionExchangeSection Section(Guid locationId, string name, int order) => new()
    {
        SectionId = Guid.NewGuid(),
        LocationId = locationId,
        Name = name,
        Order = order,
    };
}
