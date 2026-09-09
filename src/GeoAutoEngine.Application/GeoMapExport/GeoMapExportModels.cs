namespace GeoAutoEngine.Application.GeoMapExport;

public readonly record struct GeoMapCoordinate(double X, double Y);

public sealed record GeoMapExportFeature(
    string SourceId,
    string SourceLayer,
    IReadOnlyList<GeoMapCoordinate> Vertices,
    bool IsClosed,
    double Elevation,
    bool ProjectedTo2D);

public sealed record GeoMapExportRequest(
    string SourceCrs,
    string Units,
    string ProjectExternalId,
    string ProjectName,
    string NetworkExternalId,
    string NetworkName,
    string NetworkType,
    DateTimeOffset ExportedAt,
    IReadOnlyList<GeoMapExportFeature> Features);
