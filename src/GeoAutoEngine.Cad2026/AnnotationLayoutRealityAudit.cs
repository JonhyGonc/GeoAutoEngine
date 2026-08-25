using GeoAutoEngine.Domain.AnnotationLayout;

namespace GeoAutoEngine.Cad2026;

internal sealed record LayoutRealityBounds(double MinX, double MinY, double MaxX, double MaxY)
{
    internal double DistanceTo(LayoutPoint2 point)
    {
        var dx = Math.Max(0, Math.Max(MinX - point.X, point.X - MaxX));
        var dy = Math.Max(0, Math.Max(MinY - point.Y, point.Y - MaxY));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

internal sealed record LayoutRealityEntity(
    string EntityType,
    string Layer,
    string Handle,
    string BlockPath,
    LayoutRealityBounds? Bounds,
    bool IncludedInLayoutScene,
    LayoutObstacleKind? ObstacleKind,
    string Reason);

internal sealed record AnnotationRealityItem(
    string AnnotationId,
    string SourceId,
    LayoutPoint2 Anchor,
    IReadOnlyList<LayoutRealityEntity> NearbyCadEntities,
    IReadOnlyList<LayoutRealityEntity> RecognizedObstacles,
    IReadOnlyList<LayoutRealityEntity> IgnoredEntities,
    bool LegacyCollision,
    IReadOnlyList<string> CollisionWith,
    LayoutDecisionKind FinalDecision,
    double MovementDistance);

internal sealed record AnnotationRealityAudit(
    DateTimeOffset CapturedAt,
    int ScannedEntityCount,
    int RecognizedObstacleCount,
    int IgnoredEntityCount,
    AnnotationLayoutStatistics Metrics,
    IReadOnlyList<AnnotationRealityItem> Annotations);
