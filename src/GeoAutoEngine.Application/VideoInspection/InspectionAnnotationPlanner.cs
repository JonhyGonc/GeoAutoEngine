using System.Globalization;
using System.Text.RegularExpressions;

namespace GeoAutoEngine.Application.VideoInspection;

public sealed record InspectionAnnotationCall(
    Guid OccurrenceId,
    InspectionLocationMode LocationMode,
    PositionedInspectionLocation Anchor,
    string LocationLine,
    string Cause,
    string? PositionLine = null,
    PositionedInspectionLocation? IntervalEnd = null,
    bool IsFilmingStart = false,
    bool IsFilmingEnd = false,
    bool IsSyntheticStart = false);

public sealed record InspectionSectionIdentification(
    string SectionLine,
    string DiameterLine);

public static class InspectionAnnotationPlanner
{
    private static readonly CultureInfo AnnotationCulture = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Regex FilmingDistanceDetail = new(
        @"(?:\s*[-–—,:;]?\s*)\b(?:(?:ENTRE|DE)\s+\d+(?:[.,]\d+)?\s+(?:E|A|AT[ÉE])\s+\d+(?:[.,]\d+)?|A\s+PARTIR\s+DE\s+\d+(?:[.,]\d+)?|A(?:OS?)?\s+\d+(?:[.,]\d+)?|\d+(?:[.,]\d+)?)\s*(?:M|METROS?)\s+(?:DO\s+)?IN[ÍI]CIO\s+DA\s+FILMAGEM\b\.?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex EntireSectionDetail = new(
        @"(?:\s*[-–—,:;]?\s*)\b(?:AO\s+LONGO\s+DE\s+TODA\s+A\s+FILMAGEM|TODO\s+O\s+TRECHO)\b\.?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<InspectionAnnotationCall> Create(
        IReadOnlyList<PositionedInspectionEvent> positionedEvents,
        PositionedInspectionLocation? filmingStart = null)
    {
        ArgumentNullException.ThrowIfNull(positionedEvents);
        var calls = new List<InspectionAnnotationCall>();
        var filmingStartWasShared = false;
        foreach (var positionedEvent in positionedEvents)
        {
            if (!positionedEvent.CanCommit ||
                positionedEvent.LocationMode == InspectionLocationMode.NotApplicable)
            {
                continue;
            }

            var cause = Cause(positionedEvent);
            var positionLine = PositionLine(cause, positionedEvent.Position);
            switch (positionedEvent.LocationMode)
            {
                case InspectionLocationMode.Point:
                case InspectionLocationMode.MultiPoint:
                    foreach (var location in positionedEvent.Locations)
                    {
                        calls.Add(Call(
                            positionedEvent,
                            location,
                            Meter(location.InspectionDistance),
                            cause,
                            positionLine));
                    }

                    break;
                case InspectionLocationMode.Range when positionedEvent.Locations.Count >= 2:
                    calls.Add(Call(
                        positionedEvent,
                        positionedEvent.Locations[0],
                        $"{Meter(positionedEvent.Locations[0].InspectionDistance)}\n" +
                        Meter(positionedEvent.Locations[1].InspectionDistance),
                        cause,
                        positionLine,
                        positionedEvent.Locations[1]));
                    break;
                case InspectionLocationMode.From when positionedEvent.Locations.Count >= 2:
                    calls.Add(Call(
                        positionedEvent,
                        positionedEvent.Locations[0],
                        $"{Meter(positionedEvent.Locations[0].InspectionDistance)}\n" +
                        Meter(positionedEvent.Locations[1].InspectionDistance),
                        cause,
                        positionLine,
                        positionedEvent.Locations[1]));
                    break;
                case InspectionLocationMode.EntireSection when positionedEvent.Locations.Count >= 1:
                    var sharesFilmingStart = filmingStart is not null && !filmingStartWasShared;
                    calls.Add(Call(
                        positionedEvent,
                        sharesFilmingStart ? filmingStart! : positionedEvent.Locations[0],
                        string.Empty,
                        cause,
                        positionLine) with
                    {
                        IsFilmingStart = sharesFilmingStart,
                    });
                    filmingStartWasShared |= sharesFilmingStart;
                    break;
            }
        }

        if (filmingStart is null)
        {
            return Array.AsReadOnly(calls.ToArray());
        }

        if (calls.Count > 0)
        {
            calls[^1] = calls[^1] with { IsFilmingEnd = true };
        }

        var sharedStartIndex = calls.FindIndex(call => call.IsFilmingStart);
        if (sharedStartIndex >= 0)
        {
            var sharedStart = calls[sharedStartIndex];
            calls.RemoveAt(sharedStartIndex);
            calls.Insert(0, sharedStart);
        }
        else
        {
            calls.Insert(0, new InspectionAnnotationCall(
                Guid.Empty,
                InspectionLocationMode.Point,
                filmingStart,
                string.Empty,
                string.Empty,
                IsFilmingStart: true,
                IsSyntheticStart: true));
        }

        return Array.AsReadOnly(calls.ToArray());
    }

    public static InspectionSectionIdentification CreateSectionIdentification(
        InspectionSectionSession section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var sectionLine = string.IsNullOrWhiteSpace(section.Name)
            ? $"TRECHO {Math.Max(1, section.Order + 1):00}"
            : section.Name.Trim();
        var diameter = section.Diameter is { } value
            ? value.ToString("0.##", AnnotationCulture)
            : "N/I";
        return new InspectionSectionIdentification(
            sectionLine,
            $"Ø{diameter}");
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
        string cause,
        string? positionLine,
        PositionedInspectionLocation? intervalEnd = null) => new(
            positionedEvent.OccurrenceId,
            positionedEvent.LocationMode,
            location,
            locationLine,
            cause,
            positionLine,
            intervalEnd);

    private static string Meter(double value) =>
        $"{value.ToString("0.00", AnnotationCulture)} m";

    private static string Cause(PositionedInspectionEvent positionedEvent)
    {
        if (string.IsNullOrWhiteSpace(positionedEvent.Description))
        {
            return "OCORRÊNCIA";
        }

        var value = FilmingDistanceDetail.Replace(
            Normalize(positionedEvent.Description),
            " ");
        if (positionedEvent.LocationMode == InspectionLocationMode.EntireSection)
        {
            value = EntireSectionDetail.Replace(value, " ");
        }

        value = Normalize(value).Trim(' ', '-', '–', '—', ',', ';', ':', '.');
        return string.IsNullOrWhiteSpace(value) ? "OCORRÊNCIA" : value;
    }

    private static string? PositionLine(string cause, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = Normalize(value!);
        return cause.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0
            ? null
            : normalized;
    }

    private static string Normalize(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ").ToUpper(AnnotationCulture);

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}