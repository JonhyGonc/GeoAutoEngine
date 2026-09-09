using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeoAutoEngine.Application.GeoMapExport;

public static class GeoMapExportSerializer
{
    public const string ContractVersion = "0.1";
    public const string SourceSystem = "Civil 3D";
    public const int MaximumFeatures = 10_000;
    public const int MaximumVerticesPerFeature = 100_000;

    private static readonly HashSet<string> SupportedUnits =
        new(StringComparer.Ordinal)
        {
            "metre",
            "foot",
            "us-survey-foot",
            "degree",
        };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public static string Serialize(GeoMapExportRequest request)
    {
        Validate(request);

        var document = new DocumentDto(
            ContractVersion,
            SourceSystem,
            request.SourceCrs,
            request.Units,
            new ProjectDto(request.ProjectExternalId, request.ProjectName),
            new NetworkDto(request.NetworkExternalId, request.NetworkName, request.NetworkType.Trim().ToLowerInvariant()),
            request.ExportedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            request.Features.Select(CreateFeature).ToArray());

        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    private static FeatureDto CreateFeature(GeoMapExportFeature feature)
    {
        var coordinates = feature.Vertices
            .Select(vertex => new[] { vertex.X, vertex.Y })
            .ToList();

        if (feature.IsClosed && !SamePosition(coordinates[0], coordinates[coordinates.Count - 1]))
        {
            coordinates.Add(new[] { coordinates[0][0], coordinates[0][1] });
        }

        return new(
            feature.SourceId,
            "Polyline",
            feature.SourceLayer,
            new GeometryDto("LineString", coordinates),
            new PropertiesDto(
                feature.SourceId,
                "Polyline",
                feature.Vertices.Count,
                feature.IsClosed,
                feature.Elevation,
                feature.ProjectedTo2D));
    }

    private static bool SamePosition(double[] left, double[] right) =>
        left[0].Equals(right[0]) && left[1].Equals(right[1]);

    private static void Validate(GeoMapExportRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        Required(request.SourceCrs, 16, "O CRS de origem");
        if (!request.SourceCrs.StartsWith("EPSG:", StringComparison.Ordinal) ||
            !int.TryParse(
                request.SourceCrs.Substring("EPSG:".Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var epsg) ||
            epsg <= 0)
        {
            throw new InvalidOperationException("O CRS deve usar o formato EPSG:<código>, por exemplo EPSG:31982.");
        }

        if (!SupportedUnits.Contains(request.Units))
        {
            throw new InvalidOperationException("A unidade deve ser metre, foot, us-survey-foot ou degree.");
        }

        Required(request.ProjectExternalId, 255, "O identificador do projeto");
        Required(request.ProjectName, 200, "O nome do projeto");
        Required(request.NetworkExternalId, 255, "O identificador da rede");
        Required(request.NetworkName, 200, "O nome da rede");
        Required(request.NetworkType, 100, "O tipo da rede");

        if (request.Features is null || request.Features.Count == 0)
        {
            throw new InvalidOperationException("Selecione pelo menos uma Polyline válida para exportar.");
        }

        if (request.Features.Count > MaximumFeatures)
        {
            throw new InvalidOperationException($"O contrato v0.1 aceita no máximo {MaximumFeatures:N0} feições por arquivo.");
        }

        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var feature in request.Features)
        {
            Required(feature.SourceId, 255, "O handle da Polyline");
            Required(feature.SourceLayer, 255, "O layer da Polyline");
            if (!identities.Add(feature.SourceId))
            {
                throw new InvalidOperationException($"O handle {feature.SourceId} aparece mais de uma vez na seleção.");
            }

            if (feature.Vertices is null || feature.Vertices.Count < 2)
            {
                throw new InvalidOperationException($"A Polyline {feature.SourceId} precisa ter pelo menos dois vértices.");
            }

            var exportedVertexCount = feature.Vertices.Count +
                (feature.IsClosed && feature.Vertices[0] != feature.Vertices[feature.Vertices.Count - 1] ? 1 : 0);
            if (exportedVertexCount > MaximumVerticesPerFeature)
            {
                throw new InvalidOperationException(
                    $"A Polyline {feature.SourceId} excede {MaximumVerticesPerFeature:N0} vértices no contrato v0.1.");
            }

            if (!IsFinite(feature.Elevation) ||
                feature.Vertices.Any(vertex => !IsFinite(vertex.X) || !IsFinite(vertex.Y)))
            {
                throw new InvalidOperationException($"A Polyline {feature.SourceId} contém coordenadas não finitas.");
            }
        }
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static void Required(string? value, int maximumLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label} é obrigatório.");
        }

        if (value.Length > maximumLength)
        {
            throw new InvalidOperationException($"{label} excede {maximumLength} caracteres.");
        }
    }

    private sealed record DocumentDto(
        [property: JsonPropertyName("contractVersion")] string ContractVersion,
        [property: JsonPropertyName("sourceSystem")] string SourceSystem,
        [property: JsonPropertyName("sourceCrs")] string SourceCrs,
        [property: JsonPropertyName("units")] string Units,
        [property: JsonPropertyName("project")] ProjectDto Project,
        [property: JsonPropertyName("network")] NetworkDto Network,
        [property: JsonPropertyName("exportedAt")] string ExportedAt,
        [property: JsonPropertyName("features")] IReadOnlyList<FeatureDto> Features);

    private sealed record ProjectDto(
        [property: JsonPropertyName("externalId")] string ExternalId,
        [property: JsonPropertyName("name")] string Name);

    private sealed record NetworkDto(
        [property: JsonPropertyName("externalId")] string ExternalId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type);

    private sealed record FeatureDto(
        [property: JsonPropertyName("sourceId")] string SourceId,
        [property: JsonPropertyName("sourceType")] string SourceType,
        [property: JsonPropertyName("sourceLayer")] string SourceLayer,
        [property: JsonPropertyName("geometry")] GeometryDto Geometry,
        [property: JsonPropertyName("properties")] PropertiesDto Properties);

    private sealed record GeometryDto(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("coordinates")] IReadOnlyList<double[]> Coordinates);

    private sealed record PropertiesDto(
        [property: JsonPropertyName("cadHandle")] string CadHandle,
        [property: JsonPropertyName("cadEntityType")] string CadEntityType,
        [property: JsonPropertyName("vertexCount")] int VertexCount,
        [property: JsonPropertyName("closed")] bool Closed,
        [property: JsonPropertyName("cadElevation")] double CadElevation,
        [property: JsonPropertyName("projectedTo2D")] bool ProjectedTo2D);
}
