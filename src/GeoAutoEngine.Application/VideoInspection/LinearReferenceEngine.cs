namespace GeoAutoEngine.Application.VideoInspection;

public readonly record struct LinearPoint(double X, double Y, double Z)
{
    public double DistanceTo(LinearPoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}

public readonly record struct LinearVector(double X, double Y, double Z);

public interface ILinearReferenceGeometry
{
    double Length { get; }
    LinearPoint GetPointAtDistance(double distance);
    LinearVector GetTangentAtDistance(double distance);
}

public enum PositionedLocationRole
{
    Point,
    Start,
    End,
}

public sealed record PositionedInspectionLocation(
    int LocationIndex,
    PositionedLocationRole Role,
    double InspectionDistance,
    double CadDistance,
    LinearPoint Position,
    LinearVector Tangent);

public sealed record PositionedInspectionEvent(
    Guid OccurrenceId,
    InspectionLocationMode LocationMode,
    string Description,
    IReadOnlyList<PositionedInspectionLocation> Locations,
    IReadOnlyList<string> Warnings,
    bool CanCommit);

public static class DirectionResolver
{
    public static InspectionDirection Resolve(
        LinearPoint indicatedPoint,
        LinearPoint geometricStart,
        LinearPoint geometricEnd) =>
        indicatedPoint.DistanceTo(geometricStart) <= indicatedPoint.DistanceTo(geometricEnd)
            ? InspectionDirection.Forward
            : InspectionDirection.Reverse;

    public static double ToCadDistance(
        double inspectionDistance,
        double geometryLength,
        InspectionDirection direction) => direction == InspectionDirection.Forward
            ? inspectionDistance
            : geometryLength - inspectionDistance;
}

public sealed class LinearReferenceEngine
{
    private const double NumericTolerance = 1e-8;

    public PositionedInspectionEvent Resolve(
        InspectionOccurrenceSession occurrence,
        decimal? investigatedMeters,
        ILinearReferenceGeometry geometry,
        InspectionDirection direction)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(geometry);
        if (!IsFinite(geometry.Length) || geometry.Length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(geometry), "O comprimento da geometria é inválido.");
        }

        var warnings = new List<string>();
        var locations = new List<PositionedInspectionLocation>();
        switch (occurrence.LocationMode)
        {
            case InspectionLocationMode.Point:
                AddPoint(occurrence.Meterage, 0, PositionedLocationRole.Point, geometry, direction, locations, warnings);
                break;
            case InspectionLocationMode.MultiPoint:
                if (occurrence.MeteragePoints.Count == 0)
                {
                    warnings.Add("A ocorrência multiponto não possui metragens.");
                }
                else
                {
                    for (var index = 0; index < occurrence.MeteragePoints.Count; index++)
                    {
                        AddPoint(
                            occurrence.MeteragePoints[index],
                            index,
                            PositionedLocationRole.Point,
                            geometry,
                            direction,
                            locations,
                            warnings);
                    }
                }

                break;
            case InspectionLocationMode.Range:
                AddRange(
                    occurrence.Meterage,
                    occurrence.EndMeterage,
                    geometry,
                    direction,
                    locations,
                    warnings);
                break;
            case InspectionLocationMode.From:
                AddOpenRange(
                    occurrence.Meterage,
                    investigatedMeters,
                    geometry,
                    direction,
                    locations,
                    warnings,
                    "Metragem final da inspeção não informada; o limite visual deriva do CAD.");
                break;
            case InspectionLocationMode.EntireSection:
                AddOpenRange(
                    0m,
                    investigatedMeters,
                    geometry,
                    direction,
                    locations,
                    warnings,
                    "Metragem investigada não informada; o limite visual deriva do CAD.");
                break;
            case InspectionLocationMode.NotApplicable:
                warnings.Add("Sem posição CAD.");
                break;
            default:
                warnings.Add($"Modo de localização não suportado: '{occurrence.WireLocationMode}'.");
                break;
        }

        var expectedLocationCount = occurrence.LocationMode switch
        {
            InspectionLocationMode.Point => 1,
            InspectionLocationMode.MultiPoint => occurrence.MeteragePoints.Count,
            InspectionLocationMode.Range or InspectionLocationMode.From or InspectionLocationMode.EntireSection => 2,
            _ => 0,
        };
        var canCommit = occurrence.LocationMode == InspectionLocationMode.NotApplicable ||
            expectedLocationCount > 0 && locations.Count == expectedLocationCount;
        return new PositionedInspectionEvent(
            occurrence.OccurrenceId,
            occurrence.LocationMode,
            occurrence.EffectiveDescription,
            Array.AsReadOnly(locations.ToArray()),
            Array.AsReadOnly(warnings.ToArray()),
            canCommit);
    }

    public IReadOnlyList<PositionedInspectionEvent> ResolveSection(
        InspectionSectionSession section,
        ILinearReferenceGeometry geometry,
        InspectionDirection direction) => Array.AsReadOnly(section.Occurrences
        .Select(occurrence => Resolve(occurrence, section.InvestigatedMeters, geometry, direction))
        .ToArray());

    private static void AddRange(
        decimal? start,
        decimal? end,
        ILinearReferenceGeometry geometry,
        InspectionDirection direction,
        ICollection<PositionedInspectionLocation> locations,
        ICollection<string> warnings)
    {
        if (!start.HasValue || !end.HasValue)
        {
            warnings.Add("O intervalo não possui metragem inicial e final.");
            return;
        }

        if (end.Value < start.Value)
        {
            warnings.Add("A metragem final do intervalo é menor que a inicial.");
            return;
        }

        AddPoint(start, 0, PositionedLocationRole.Start, geometry, direction, locations, warnings);
        AddPoint(end, 1, PositionedLocationRole.End, geometry, direction, locations, warnings);
    }

    private static void AddOpenRange(
        decimal? start,
        decimal? investigatedMeters,
        ILinearReferenceGeometry geometry,
        InspectionDirection direction,
        ICollection<PositionedInspectionLocation> locations,
        ICollection<string> warnings,
        string derivedLimitWarning)
    {
        if (!start.HasValue)
        {
            warnings.Add("A metragem inicial não foi informada.");
            return;
        }

        var end = investigatedMeters ?? Convert.ToDecimal(geometry.Length);
        if (!investigatedMeters.HasValue)
        {
            warnings.Add(derivedLimitWarning);
        }

        if (end < start.Value)
        {
            warnings.Add("A metragem final do trecho é menor que a inicial da ocorrência.");
            return;
        }

        AddPoint(start, 0, PositionedLocationRole.Start, geometry, direction, locations, warnings);
        AddPoint(end, 1, PositionedLocationRole.End, geometry, direction, locations, warnings);
    }

    private static void AddPoint(
        decimal? distance,
        int index,
        PositionedLocationRole role,
        ILinearReferenceGeometry geometry,
        InspectionDirection direction,
        ICollection<PositionedInspectionLocation> locations,
        ICollection<string> warnings)
    {
        if (!distance.HasValue)
        {
            warnings.Add("A metragem da ocorrência não foi informada.");
            return;
        }

        var inspectionDistance = Convert.ToDouble(distance.Value);
        if (!IsFinite(inspectionDistance) || inspectionDistance < -NumericTolerance)
        {
            warnings.Add($"Metragem inválida: {distance.Value} m.");
            return;
        }

        if (inspectionDistance > geometry.Length + NumericTolerance)
        {
            warnings.Add($"Ocorrência em {distance.Value} m fora da geometria associada ({geometry.Length:F2} m).");
            return;
        }

        inspectionDistance = Math.Max(0, Math.Min(geometry.Length, inspectionDistance));
        var cadDistance = DirectionResolver.ToCadDistance(inspectionDistance, geometry.Length, direction);
        locations.Add(new PositionedInspectionLocation(
            index,
            role,
            inspectionDistance,
            cadDistance,
            geometry.GetPointAtDistance(cadDistance),
            geometry.GetTangentAtDistance(cadDistance)));
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

public static class VideoInspectionStableKey
{
    public static string Create(
        Guid reportId,
        Guid sectionId,
        Guid occurrenceId,
        int locationIndex,
        PositionedLocationRole role)
    {
        if (reportId == Guid.Empty || sectionId == Guid.Empty || occurrenceId == Guid.Empty)
        {
            throw new ArgumentException("A chave CAD requer identidades estáveis válidas.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(locationIndex);
        return string.Concat(
            "VI:", reportId.ToString("D"), ":",
            sectionId.ToString("D"), ":",
            occurrenceId.ToString("D"), ":",
            locationIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), ":",
            role.ToString().ToUpperInvariant());
    }
}
