namespace GeoAutoEngine.Application.VideoInspection;

public sealed record InspectionCalloutPlacement(
    InspectionAnnotationCall Call,
    LinearPoint TextPoint,
    bool AttachAtRightEdge,
    int Side,
    int Level,
    double TextRotationRadians);

public readonly record struct InspectionLayoutObstacle(
    double MinimumX,
    double MinimumY,
    double MaximumX,
    double MaximumY);

public static class InspectionCalloutLayoutEngine
{
    public const double TextHeight = 0.5;
    public const double BaseOffset = 1.20;
    public const double LevelSpacing = 1.25;
    public const double CollisionMargin = 0.25;
    private const double CharacterWidthFactor = 0.62;
    private const double LineHeightFactor = 1.25;
    private const double NumericTolerance = 1e-9;

    public static IReadOnlyList<InspectionCalloutPlacement> Arrange(
        IReadOnlyList<InspectionAnnotationCall> calls,
        IReadOnlyList<InspectionLayoutObstacle>? obstacles = null)
    {
        ArgumentNullException.ThrowIfNull(calls);
        obstacles ??= Array.Empty<InspectionLayoutObstacle>();
        if (calls.Count == 0)
        {
            return Array.Empty<InspectionCalloutPlacement>();
        }

        var ordered = calls
            .Select((call, originalIndex) => new { Call = call, OriginalIndex = originalIndex })
            .OrderBy(item => item.Call.Anchor.CadDistance)
            .ThenBy(item => item.OriginalIndex)
            .ToArray();
        var placed = new List<PlacedCallout>(ordered.Length);
        foreach (var item in ordered)
        {
            var tangent = ReadablePlanarTangent(item.Call.Anchor.Tangent);
            var normalX = -tangent.Y;
            var normalY = tangent.X;
            var preferredSide = placed.Count % 2 == 0 ? 1 : -1;
            PlacedCallout? selected = null;
            for (var level = 0; level <= ordered.Length && selected is null; level++)
            {
                foreach (var side in new[] { preferredSide, -preferredSide })
                {
                    var candidate = Candidate(item.Call, normalX, normalY, side, level);
                    if (placed.All(existing => !candidate.Bounds.Intersects(
                            existing.Bounds,
                            CollisionMargin)) &&
                        obstacles.All(obstacle => !candidate.Bounds.Intersects(
                            obstacle,
                            CollisionMargin)))
                    {
                        selected = candidate;
                        break;
                    }
                }
            }

            selected ??= Candidate(
                item.Call,
                normalX,
                normalY,
                preferredSide,
                ordered.Length + 1);
            placed.Add(selected);
        }

        return Array.AsReadOnly(placed.Select(item => item.Placement).ToArray());
    }

    private static PlacedCallout Candidate(
        InspectionAnnotationCall call,
        double normalX,
        double normalY,
        int side,
        int level)
    {
        var offset = BaseOffset + level * LevelSpacing;
        var anchor = call.Anchor.Position;
        var textPoint = new LinearPoint(
            anchor.X + normalX * side * offset,
            anchor.Y + normalY * side * offset,
            anchor.Z);
        var deltaX = textPoint.X - anchor.X;
        var attachAtRightEdge = deltaX < -NumericTolerance ||
            Math.Abs(deltaX) <= NumericTolerance && side < 0;
        var placement = new InspectionCalloutPlacement(
            call,
            textPoint,
            attachAtRightEdge,
            side,
            level,
            0);
        return new PlacedCallout(
            placement,
            TextBounds.For(call, textPoint, attachAtRightEdge));
    }

    private static PlanarVector ReadablePlanarTangent(LinearVector value)
    {
        var length = Math.Sqrt(value.X * value.X + value.Y * value.Y);
        if (length <= NumericTolerance)
        {
            return new PlanarVector(1, 0);
        }

        var x = value.X / length;
        var y = value.Y / length;
        if (x < -NumericTolerance ||
            Math.Abs(x) <= NumericTolerance && y < 0)
        {
            x = -x;
            y = -y;
        }

        return new PlanarVector(x, y);
    }

    private sealed record PlacedCallout(
        InspectionCalloutPlacement Placement,
        TextBounds Bounds);

    private readonly record struct PlanarVector(double X, double Y);

    private readonly record struct TextBounds(
        double MinimumX,
        double MinimumY,
        double MaximumX,
        double MaximumY)
    {
        public static TextBounds For(
            InspectionAnnotationCall call,
            LinearPoint textPoint,
            bool attachAtRightEdge)
        {
            var lines = string.IsNullOrWhiteSpace(call.PositionLine)
                ? new[] { call.LocationLine, call.Cause }
                : new[] { call.LocationLine, call.Cause, call.PositionLine! };
            var width = Math.Max(
                TextHeight,
                lines.Max(line => line.Length) * TextHeight * CharacterWidthFactor);
            var height = lines.Length * TextHeight * LineHeightFactor;
            var minimumX = attachAtRightEdge ? textPoint.X - width : textPoint.X;
            return new TextBounds(
                minimumX,
                textPoint.Y - height / 2,
                minimumX + width,
                textPoint.Y + height / 2);
        }

        public bool Intersects(TextBounds other, double margin) =>
            MinimumX - margin < other.MaximumX &&
            MaximumX + margin > other.MinimumX &&
            MinimumY - margin < other.MaximumY &&
            MaximumY + margin > other.MinimumY;

        public bool Intersects(InspectionLayoutObstacle other, double margin) =>
            MinimumX - margin < other.MaximumX &&
            MaximumX + margin > other.MinimumX &&
            MinimumY - margin < other.MaximumY &&
            MaximumY + margin > other.MinimumY;
    }
}
