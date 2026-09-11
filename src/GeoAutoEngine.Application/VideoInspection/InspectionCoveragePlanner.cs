namespace GeoAutoEngine.Application.VideoInspection;

public sealed record InspectionCoveragePlan(
    double GeometryLength,
    double InspectedLength,
    double RemainingLength,
    double SplitCadDistance,
    bool RequiresBreak,
    bool IsFullyInspected);

public static class InspectionCoveragePlanner
{
    public const double Tolerance = 1e-6;

    public static InspectionCoveragePlan Create(
        decimal? investigatedMeters,
        double geometryLength,
        InspectionDirection direction,
        IReadOnlyList<PositionedInspectionEvent>? positionedEvents = null)
    {
        if (double.IsNaN(geometryLength) || double.IsInfinity(geometryLength) || geometryLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(geometryLength),
                "O comprimento da geometria deve ser finito e não negativo.");
        }

        var requestedLength = investigatedMeters.HasValue
            ? (double)investigatedMeters.Value
            : FarthestInspectionDistance(positionedEvents);
        if (double.IsNaN(requestedLength) || double.IsInfinity(requestedLength))
        {
            throw new InvalidOperationException("A metragem inspecionada possui valor inválido.");
        }

        var inspectedLength = Math.Max(0.0, Math.Min(requestedLength, geometryLength));
        var remainingLength = Math.Max(0.0, geometryLength - inspectedLength);
        var splitCadDistance = direction == InspectionDirection.Forward
            ? inspectedLength
            : geometryLength - inspectedLength;
        var requiresBreak = inspectedLength > Tolerance && remainingLength > Tolerance;
        var fullyInspected = geometryLength <= Tolerance || remainingLength <= Tolerance;

        return new InspectionCoveragePlan(
            geometryLength,
            inspectedLength,
            remainingLength,
            splitCadDistance,
            requiresBreak,
            fullyInspected);
    }

    private static double FarthestInspectionDistance(
        IReadOnlyList<PositionedInspectionEvent>? positionedEvents)
    {
        if (positionedEvents is null || positionedEvents.Count == 0)
        {
            return 0.0;
        }

        return positionedEvents
            .Where(item => item.CanCommit)
            .SelectMany(item => item.Locations)
            .Select(location => location.InspectionDistance)
            .DefaultIfEmpty(0.0)
            .Max();
    }
}
