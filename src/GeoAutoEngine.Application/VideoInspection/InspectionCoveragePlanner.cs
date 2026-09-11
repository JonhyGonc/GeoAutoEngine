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

        var positionedLength = FarthestInspectionDistance(positionedEvents);
        var reportedLength = investigatedMeters.HasValue
            ? (double)investigatedMeters.Value
            : 0.0;
        if (double.IsNaN(reportedLength) || double.IsInfinity(reportedLength))
        {
            throw new InvalidOperationException("A metragem inspecionada possui valor inválido.");
        }

        // Alguns relatórios antigos podem trazer InvestigatedMeters como zero ou com um
        // valor anterior à última ocorrência realmente posicionada. A cobertura visual
        // nunca deve terminar antes de um ponto que o próprio posicionamento CAD confirmou.
        var requestedLength = Math.Max(Math.Max(0.0, reportedLength), positionedLength);
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
            .Where(distance => !double.IsNaN(distance) && !double.IsInfinity(distance) && distance >= 0)
            .DefaultIfEmpty(0.0)
            .Max();
    }
}