using System.Globalization;
using System.Text.RegularExpressions;

namespace GeoAutoEngine.Application.VideoInspection;

public sealed record InspectionAnnotationCall(
    Guid OccurrenceId,
    InspectionLocationMode LocationMode,
    PositionedInspectionLocation Anchor,
    string LocationLine,
    string Cause);

public static class InspectionAnnotationPlanner
{
    private static readonly CultureInfo AnnotationCulture = CultureInfo.GetCultureInfo("pt-BR");

    public static IReadOnlyList<InspectionAnnotationCall> Create(
        IReadOnlyList<PositionedInspectionEvent> positionedEvents)
    {
        ArgumentNullException.ThrowIfNull(positionedEvents);
        var calls = new List<InspectionAnnotationCall>();
        foreach (var positionedEvent in positionedEvents)
        {
            if (!positionedEvent.CanCommit ||
                positionedEvent.LocationMode == InspectionLocationMode.NotApplicable)
            {
                continue;
            }

            var cause = Cause(positionedEvent.Description);
            switch (positionedEvent.LocationMode)
            {
                case InspectionLocationMode.Point:
                case InspectionLocationMode.MultiPoint:
                    foreach (var location in positionedEvent.Locations)
                    {
                        calls.Add(Call(positionedEvent, location, Meter(location.InspectionDistance), cause));
                    }

                    break;
                case InspectionLocationMode.Range when positionedEvent.Locations.Count >= 2:
                    calls.Add(Call(
                        positionedEvent,
                        positionedEvent.Locations[0],
                        $"{Meter(positionedEvent.Locations[0].InspectionDistance)} – " +
                        Meter(positionedEvent.Locations[1].InspectionDistance),
                        cause));
                    break;
                case InspectionLocationMode.From when positionedEvent.Locations.Count >= 1:
                    calls.Add(Call(
                        positionedEvent,
                        positionedEvent.Locations[0],
                        $"A PARTIR DE {Meter(positionedEvent.Locations[0].InspectionDistance)}",
                        cause));
                    break;
                case InspectionLocationMode.EntireSection when positionedEvent.Locations.Count >= 1:
                    calls.Add(Call(
                        positionedEvent,
                        positionedEvent.Locations[0],
                        "TODO O TRECHO",
                        cause));
                    break;
            }
        }

        return Array.AsReadOnly(calls.ToArray());
    }

    public static double EntireSectionAnnotationOffset(decimal? investigatedMeters, double geometryLength)
    {
        if (!IsFinite(geometryLength) || geometryLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(geometryLength),
                "O comprimento CAD deve ser finito e não negativo.");
        }

        var effectiveLength = geometryLength;
        if (investigatedMeters is > 0)
        {
            effectiveLength = Math.Min(Convert.ToDouble(investigatedMeters.Value), geometryLength);
        }

        return Math.Min(2.0, effectiveLength * 0.25);
    }

    public static double EntireSectionCadDistance(
        decimal? investigatedMeters,
        double geometryLength,
        InspectionDirection direction)
    {
        var offset = EntireSectionAnnotationOffset(investigatedMeters, geometryLength);
        return direction == InspectionDirection.Forward ? offset : geometryLength - offset;
    }

    private static InspectionAnnotationCall Call(
        PositionedInspectionEvent positionedEvent,
        PositionedInspectionLocation location,
        string locationLine,
        string cause) => new(
            positionedEvent.OccurrenceId,
            positionedEvent.LocationMode,
            location,
            locationLine,
            cause);

    private static string Meter(double value) =>
        $"{value.ToString("0.00", AnnotationCulture)} m";

    private static string Cause(string value) => string.IsNullOrWhiteSpace(value)
        ? "OCORRÊNCIA"
        : Regex.Replace(value.Trim(), @"\s+", " ").ToUpper(AnnotationCulture);

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
