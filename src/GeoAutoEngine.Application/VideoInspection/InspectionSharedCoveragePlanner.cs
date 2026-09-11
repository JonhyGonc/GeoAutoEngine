namespace GeoAutoEngine.Application.VideoInspection;

public sealed record InspectionCoverageContribution(
    Guid SectionId,
    InspectionDirection Direction,
    double InspectedLength);

public readonly record struct InspectionCoverageInterval(
    double StartCadDistance,
    double EndCadDistance)
{
    public double Length => Math.Max(0.0, EndCadDistance - StartCadDistance);
}

public sealed record InspectionSharedCoveragePlan(
    double GeometryLength,
    IReadOnlyList<InspectionCoverageInterval> InspectedIntervals,
    IReadOnlyList<InspectionCoverageInterval> RemainingIntervals,
    IReadOnlyList<double> BreakCadDistances,
    double InspectedLength,
    double RemainingLength);

public static class InspectionSharedCoveragePlanner
{
    public static InspectionSharedCoveragePlan Create(
        double geometryLength,
        IReadOnlyList<InspectionCoverageContribution> contributions)
    {
        if (double.IsNaN(geometryLength) || double.IsInfinity(geometryLength) || geometryLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(geometryLength),
                "O comprimento da geometria deve ser finito e não negativo.");
        }

        ArgumentNullException.ThrowIfNull(contributions);
        var rawIntervals = new List<InspectionCoverageInterval>();
        var breakDistances = new List<double>();
        foreach (var contribution in contributions)
        {
            if (double.IsNaN(contribution.InspectedLength) ||
                double.IsInfinity(contribution.InspectedLength))
            {
                throw new InvalidOperationException("A metragem de uma inspeção possui valor inválido.");
            }

            var length = Math.Max(0.0, Math.Min(contribution.InspectedLength, geometryLength));
            if (length <= InspectionCoveragePlanner.Tolerance)
            {
                continue;
            }

            var start = contribution.Direction == InspectionDirection.Forward
                ? 0.0
                : geometryLength - length;
            var end = contribution.Direction == InspectionDirection.Forward
                ? length
                : geometryLength;
            rawIntervals.Add(new InspectionCoverageInterval(start, end));

            var endpoint = contribution.Direction == InspectionDirection.Forward ? end : start;
            if (endpoint > InspectionCoveragePlanner.Tolerance &&
                endpoint < geometryLength - InspectionCoveragePlanner.Tolerance)
            {
                breakDistances.Add(endpoint);
            }
        }

        var inspected = Merge(rawIntervals);
        var remaining = Complement(geometryLength, inspected);
        var breaks = breakDistances
            .OrderBy(value => value)
            .Aggregate(new List<double>(), (values, value) =>
            {
                if (values.Count == 0 ||
                    Math.Abs(values[^1] - value) > InspectionCoveragePlanner.Tolerance)
                {
                    values.Add(value);
                }

                return values;
            });
        var inspectedLength = inspected.Sum(interval => interval.Length);
        var remainingLength = Math.Max(0.0, geometryLength - inspectedLength);
        return new InspectionSharedCoveragePlan(
            geometryLength,
            Array.AsReadOnly(inspected.ToArray()),
            Array.AsReadOnly(remaining.ToArray()),
            Array.AsReadOnly(breaks.ToArray()),
            inspectedLength,
            remainingLength);
    }

    private static List<InspectionCoverageInterval> Merge(
        IReadOnlyList<InspectionCoverageInterval> intervals)
    {
        var ordered = intervals
            .Where(interval => interval.Length > InspectionCoveragePlanner.Tolerance)
            .OrderBy(interval => interval.StartCadDistance)
            .ThenBy(interval => interval.EndCadDistance)
            .ToArray();
        var merged = new List<InspectionCoverageInterval>();
        foreach (var interval in ordered)
        {
            if (merged.Count == 0)
            {
                merged.Add(interval);
                continue;
            }

            var previous = merged[^1];
            if (interval.StartCadDistance <= previous.EndCadDistance + InspectionCoveragePlanner.Tolerance)
            {
                merged[^1] = previous with
                {
                    EndCadDistance = Math.Max(previous.EndCadDistance, interval.EndCadDistance),
                };
            }
            else
            {
                merged.Add(interval);
            }
        }

        return merged;
    }

    private static List<InspectionCoverageInterval> Complement(
        double geometryLength,
        IReadOnlyList<InspectionCoverageInterval> inspected)
    {
        var remaining = new List<InspectionCoverageInterval>();
        var cursor = 0.0;
        foreach (var interval in inspected)
        {
            if (interval.StartCadDistance > cursor + InspectionCoveragePlanner.Tolerance)
            {
                remaining.Add(new InspectionCoverageInterval(cursor, interval.StartCadDistance));
            }

            cursor = Math.Max(cursor, interval.EndCadDistance);
        }

        if (geometryLength > cursor + InspectionCoveragePlanner.Tolerance)
        {
            remaining.Add(new InspectionCoverageInterval(cursor, geometryLength));
        }

        return remaining;
    }
}
