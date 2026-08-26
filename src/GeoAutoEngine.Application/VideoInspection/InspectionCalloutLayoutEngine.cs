namespace GeoAutoEngine.Application.VideoInspection;

public sealed record InspectionCalloutPlacement(
    InspectionAnnotationCall Call,
    LinearPoint LeaderEndPoint,
    LinearPoint TextPoint,
    bool AttachAtRightEdge,
    int Side,
    int Level,
    int LateralSlot,
    double TextRotationRadians);

public readonly record struct InspectionLayoutObstacle(
    double MinimumX,
    double MinimumY,
    double MaximumX,
    double MaximumY);

public static class InspectionCalloutLayoutEngine
{
    public const double TextHeight = 0.5;
    public const int MaxCharactersPerLine = 32;
    public const double MarkerRadius = 0.30;
    public const double BaseCalloutLength = 5.00;
    public const double LevelSpacing = 1.25;
    public const double LateralSpacing = 1.50;
    public const double TextGap = 0.15;
    public const double CalloutSpacing = 0.25;
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
            var maximumLevel = ordered.Length + obstacles.Count + 2;
            for (var level = 0; level <= maximumLevel && selected is null; level++)
            {
                foreach (var side in new[] { preferredSide, -preferredSide })
                {
                    foreach (var lateralSlot in LateralSlots(level))
                    {
                        var candidate = Candidate(
                            item.Call,
                            tangent.X,
                            tangent.Y,
                            normalX,
                            normalY,
                            side,
                            level,
                            lateralSlot);
                        if (IsClear(candidate, placed, obstacles))
                        {
                            selected = candidate;
                            break;
                        }
                    }

                    if (selected is not null)
                    {
                        break;
                    }
                }
            }

            selected ??= Candidate(
                item.Call,
                tangent.X,
                tangent.Y,
                normalX,
                normalY,
                preferredSide,
                maximumLevel + 1,
                0);
            placed.Add(selected);
        }

        return Array.AsReadOnly(placed.Select(item => item.Placement).ToArray());
    }

    public static IReadOnlyList<string> FormatTextLines(InspectionAnnotationCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var lines = new List<string>();
        lines.AddRange(call.LocationLine
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()));
        lines.AddRange(WrapWords(call.Cause, MaxCharactersPerLine));
        if (!string.IsNullOrWhiteSpace(call.PositionLine))
        {
            lines.AddRange(WrapWords(call.PositionLine!, MaxCharactersPerLine));
        }

        return Array.AsReadOnly(lines.ToArray());
    }

    private static IReadOnlyList<string> WrapWords(string value, int maximumCharacters)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return [string.Empty];
        }

        var lines = new List<string>();
        var current = words[0];
        for (var index = 1; index < words.Length; index++)
        {
            var word = words[index];
            if (current.Length + 1 + word.Length <= maximumCharacters)
            {
                current += " " + word;
                continue;
            }

            lines.Add(current);
            current = word;
        }

        lines.Add(current);
        return Array.AsReadOnly(lines.ToArray());
    }

    private static bool IsClear(
        PlacedCallout candidate,
        IReadOnlyList<PlacedCallout> placed,
        IReadOnlyList<InspectionLayoutObstacle> obstacles)
    {
        foreach (var existing in placed)
        {
            if (candidate.Bounds.Intersects(existing.Bounds, CalloutSpacing) ||
                SegmentIntersectsBounds(
                    candidate.LeaderStart,
                    candidate.LeaderEnd,
                    existing.Bounds,
                    CalloutSpacing) ||
                SegmentIntersectsBounds(
                    existing.LeaderStart,
                    existing.LeaderEnd,
                    candidate.Bounds,
                    CalloutSpacing) ||
                existing.Bounds.Contains(
                    candidate.LeaderStart,
                    MarkerRadius + CalloutSpacing) ||
                candidate.Bounds.Contains(
                    existing.LeaderStart,
                    MarkerRadius + CalloutSpacing))
            {
                return false;
            }
        }

        return obstacles.All(obstacle =>
            !candidate.Bounds.Intersects(obstacle, CalloutSpacing) &&
            !SegmentIntersectsObstacle(
                candidate.LeaderStart,
                candidate.LeaderEnd,
                obstacle,
                CalloutSpacing));
    }

    private static PlacedCallout Candidate(
        InspectionAnnotationCall call,
        double tangentX,
        double tangentY,
        double normalX,
        double normalY,
        int side,
        int level,
        int lateralSlot)
    {
        var length = BaseCalloutLength + level * LevelSpacing;
        var lateralOffset = lateralSlot * LateralSpacing;
        var leaderDeltaX = normalX * side * length + tangentX * lateralOffset;
        var leaderDeltaY = normalY * side * length + tangentY * lateralOffset;
        var leaderLength = Math.Sqrt(
            leaderDeltaX * leaderDeltaX + leaderDeltaY * leaderDeltaY);
        var outwardX = leaderDeltaX / leaderLength;
        var outwardY = leaderDeltaY / leaderLength;
        var anchor = call.Anchor.Position;
        var leaderEndPoint = new LinearPoint(
            anchor.X + leaderDeltaX,
            anchor.Y + leaderDeltaY,
            anchor.Z);
        var textPoint = new LinearPoint(
            leaderEndPoint.X + outwardX * TextGap,
            leaderEndPoint.Y + outwardY * TextGap,
            leaderEndPoint.Z);
        var rotation = NormalizeReadableRotation(Math.Atan2(outwardY, outwardX));
        var textDirectionX = Math.Cos(rotation);
        var textDirectionY = Math.Sin(rotation);
        var attachAtRightEdge = outwardX * textDirectionX + outwardY * textDirectionY < 0;
        var placement = new InspectionCalloutPlacement(
            call,
            leaderEndPoint,
            textPoint,
            attachAtRightEdge,
            side,
            level,
            lateralSlot,
            rotation);
        return new PlacedCallout(
            placement,
            anchor,
            leaderEndPoint,
            TextBounds.For(call, textPoint, attachAtRightEdge, rotation));
    }

    private static IEnumerable<int> LateralSlots(int level)
    {
        yield return 0;
        for (var slot = 1; slot <= level; slot++)
        {
            yield return slot;
            yield return -slot;
        }
    }

    private static double NormalizeReadableRotation(double rotation)
    {
        while (rotation > Math.PI / 2 + NumericTolerance)
        {
            rotation -= Math.PI;
        }

        while (rotation <= -Math.PI / 2 + NumericTolerance)
        {
            rotation += Math.PI;
        }

        return rotation;
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

    private static bool SegmentIntersectsObstacle(
        LinearPoint start,
        LinearPoint end,
        InspectionLayoutObstacle obstacle,
        double margin) => SegmentIntersectsRectangle(
            start,
            end,
            obstacle.MinimumX - margin,
            obstacle.MinimumY - margin,
            obstacle.MaximumX + margin,
            obstacle.MaximumY + margin);

    private static bool SegmentIntersectsBounds(
        LinearPoint start,
        LinearPoint end,
        TextBounds bounds,
        double margin) => SegmentIntersectsRectangle(
            start,
            end,
            bounds.MinimumX - margin,
            bounds.MinimumY - margin,
            bounds.MaximumX + margin,
            bounds.MaximumY + margin);

    private static bool SegmentIntersectsRectangle(
        LinearPoint start,
        LinearPoint end,
        double minimumX,
        double minimumY,
        double maximumX,
        double maximumY)
    {
        if (Contains(start, minimumX, minimumY, maximumX, maximumY) ||
            Contains(end, minimumX, minimumY, maximumX, maximumY))
        {
            return true;
        }

        var bottomLeft = new LinearPoint(minimumX, minimumY, 0);
        var bottomRight = new LinearPoint(maximumX, minimumY, 0);
        var topRight = new LinearPoint(maximumX, maximumY, 0);
        var topLeft = new LinearPoint(minimumX, maximumY, 0);
        return SegmentsIntersect(start, end, bottomLeft, bottomRight) ||
            SegmentsIntersect(start, end, bottomRight, topRight) ||
            SegmentsIntersect(start, end, topRight, topLeft) ||
            SegmentsIntersect(start, end, topLeft, bottomLeft);
    }

    private static bool Contains(
        LinearPoint point,
        double minimumX,
        double minimumY,
        double maximumX,
        double maximumY) => point.X >= minimumX &&
        point.X <= maximumX &&
        point.Y >= minimumY &&
        point.Y <= maximumY;

    private static bool SegmentsIntersect(
        LinearPoint firstStart,
        LinearPoint firstEnd,
        LinearPoint secondStart,
        LinearPoint secondEnd)
    {
        if (Math.Max(firstStart.X, firstEnd.X) + NumericTolerance <
                Math.Min(secondStart.X, secondEnd.X) ||
            Math.Max(secondStart.X, secondEnd.X) + NumericTolerance <
                Math.Min(firstStart.X, firstEnd.X) ||
            Math.Max(firstStart.Y, firstEnd.Y) + NumericTolerance <
                Math.Min(secondStart.Y, secondEnd.Y) ||
            Math.Max(secondStart.Y, secondEnd.Y) + NumericTolerance <
                Math.Min(firstStart.Y, firstEnd.Y))
        {
            return false;
        }

        var d1 = Cross(firstStart, firstEnd, secondStart);
        var d2 = Cross(firstStart, firstEnd, secondEnd);
        var d3 = Cross(secondStart, secondEnd, firstStart);
        var d4 = Cross(secondStart, secondEnd, firstEnd);
        return d1 * d2 <= NumericTolerance && d3 * d4 <= NumericTolerance;
    }

    private static double Cross(LinearPoint start, LinearPoint end, LinearPoint point) =>
        (end.X - start.X) * (point.Y - start.Y) -
        (end.Y - start.Y) * (point.X - start.X);

    private sealed record PlacedCallout(
        InspectionCalloutPlacement Placement,
        LinearPoint LeaderStart,
        LinearPoint LeaderEnd,
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
            bool attachAtRightEdge,
            double rotation)
        {
            var lines = FormatTextLines(call);
            var width = Math.Max(
                TextHeight,
                lines.Max(line => line.Length) * TextHeight * CharacterWidthFactor);
            var height = lines.Count * TextHeight * LineHeightFactor;
            var localMinimumX = attachAtRightEdge ? -width : 0;
            var localMaximumX = attachAtRightEdge ? 0 : width;
            var localMinimumY = -height / 2;
            var localMaximumY = height / 2;
            var cosine = Math.Cos(rotation);
            var sine = Math.Sin(rotation);
            var corners = new[]
            {
                Transform(localMinimumX, localMinimumY),
                Transform(localMinimumX, localMaximumY),
                Transform(localMaximumX, localMinimumY),
                Transform(localMaximumX, localMaximumY),
            };
            return new TextBounds(
                corners.Min(point => point.X),
                corners.Min(point => point.Y),
                corners.Max(point => point.X),
                corners.Max(point => point.Y));

            LinearPoint Transform(double x, double y) => new(
                textPoint.X + x * cosine - y * sine,
                textPoint.Y + x * sine + y * cosine,
                textPoint.Z);
        }

        public bool Contains(LinearPoint point, double margin) =>
            point.X >= MinimumX - margin &&
            point.X <= MaximumX + margin &&
            point.Y >= MinimumY - margin &&
            point.Y <= MaximumY + margin;

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
