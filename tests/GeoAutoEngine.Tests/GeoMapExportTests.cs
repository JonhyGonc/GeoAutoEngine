using System.Text.Json;
using GeoAutoEngine.Application.GeoMapExport;

namespace GeoAutoEngine.Tests;

public sealed class GeoMapExportTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void Serialize_OpenPolyline_ProducesGeoMapVersion01Contract()
    {
        var json = GeoMapExportSerializer.Serialize(CreateRequest(
            new GeoMapExportFeature(
                "2A7F",
                "GF-Rede Pluvial",
                [new(688100.25, 7086400.75), new(688112.5, 7086411)],
                false,
                0,
                false)));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("0.1", root.GetProperty("contractVersion").GetString());
        Assert.Equal("Civil 3D", root.GetProperty("sourceSystem").GetString());
        Assert.Equal("EPSG:31982", root.GetProperty("sourceCrs").GetString());
        Assert.Equal("metre", root.GetProperty("units").GetString());
        Assert.EndsWith("Z", root.GetProperty("exportedAt").GetString(), StringComparison.Ordinal);

        var features = root.GetProperty("features");
        Assert.Equal(1, features.GetArrayLength());
        var feature = features[0];
        Assert.Equal("2A7F", feature.GetProperty("sourceId").GetString());
        Assert.Equal("Polyline", feature.GetProperty("sourceType").GetString());
        Assert.Equal("GF-Rede Pluvial", feature.GetProperty("sourceLayer").GetString());
        Assert.Equal("LineString", feature.GetProperty("geometry").GetProperty("type").GetString());
        Assert.Equal(2, feature.GetProperty("geometry").GetProperty("coordinates").GetArrayLength());
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Serialize_ClosedPolyline_AppendsFirstPositionAndPreservesProjectionMetadata()
    {
        var json = GeoMapExportSerializer.Serialize(CreateRequest(
            new GeoMapExportFeature(
                "30",
                "REDE-DRENAGEM",
                [new(0, 0), new(10, 0), new(10, 10)],
                true,
                5.5,
                true)));

        using var document = JsonDocument.Parse(json);
        var feature = document.RootElement.GetProperty("features")[0];
        var coordinates = feature.GetProperty("geometry").GetProperty("coordinates");
        Assert.Equal(4, coordinates.GetArrayLength());
        Assert.Equal(coordinates[0].GetRawText(), coordinates[3].GetRawText());
        Assert.Equal(3, feature.GetProperty("properties").GetProperty("vertexCount").GetInt32());
        Assert.True(feature.GetProperty("properties").GetProperty("closed").GetBoolean());
        Assert.True(feature.GetProperty("properties").GetProperty("projectedTo2D").GetBoolean());
        Assert.Equal(5.5, feature.GetProperty("properties").GetProperty("cadElevation").GetDouble());
    }

    [Theory]
    [InlineData("31982")]
    [InlineData("EPSG:")]
    [InlineData("SRID:31982")]
    [Trait("Category", "FAST")]
    public void Serialize_InvalidCrs_IsRejected(string sourceCrs)
    {
        var request = CreateRequest(
            new GeoMapExportFeature("1", "0", [new(0, 0), new(1, 1)], false, 0, false)) with
        {
            SourceCrs = sourceCrs,
        };

        var error = Assert.Throws<InvalidOperationException>(() => GeoMapExportSerializer.Serialize(request));
        Assert.Contains("EPSG", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Serialize_DuplicateCadHandle_IsRejected()
    {
        var feature = new GeoMapExportFeature("1A", "0", [new(0, 0), new(1, 1)], false, 0, false);
        var request = CreateRequest(feature) with { Features = [feature, feature] };

        var error = Assert.Throws<InvalidOperationException>(() => GeoMapExportSerializer.Serialize(request));
        Assert.Contains("mais de uma vez", error.Message, StringComparison.Ordinal);
    }

    private static GeoMapExportRequest CreateRequest(params GeoMapExportFeature[] features) => new(
        "EPSG:31982",
        "metre",
        "project-001",
        "Projeto exemplo",
        "project-001:aguas_pluviais",
        "Águas pluviais",
        "stormwater",
        new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
        features);
}
