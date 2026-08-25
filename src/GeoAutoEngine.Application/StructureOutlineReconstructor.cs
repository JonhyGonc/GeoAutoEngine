using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Application;

internal static class StructureOutlineReconstructor
{
    private const double MinimumLength = 1e-9;
    private const double OrthogonalityCosineTolerance = 0.25;
    private const int RecoveryLookAhead = 12;

    public static StructureOutlineReconstruction Reconstruct(
        IReadOnlyList<SurveyRecord> records,
        bool orthogonalize)
    {
        ArgumentNullException.ThrowIfNull(records);

        var remaining = records
            .Select((record, ordinal) => new IndexedRecord(record, ordinal))
            .ToList();
        var matches = new List<StructureOutlineMatch>();

        while (remaining.Count >= 3)
        {
            var candidate = FindBestCandidate(remaining, orthogonalize);
            if (candidate is null)
            {
                break;
            }

            matches.Add(new StructureOutlineMatch(
                candidate.Records.Select(item => item.Record).ToArray(),
                candidate.Vertices));

            var selectedOrdinals = new HashSet<int>(candidate.Records
                .Select(item => item.OriginalOrdinal));
            remaining.RemoveAll(item => selectedOrdinals.Contains(item.OriginalOrdinal));
        }

        return new StructureOutlineReconstruction(
            matches,
            remaining.Select(item => item.Record).ToArray());
    }

    private static Candidate? FindBestCandidate(
        IReadOnlyList<IndexedRecord> records,
        bool orthogonalize)
    {
        Candidate? best = null;
        for (var first = 0; first < records.Count - 2; first++)
        {
            var limit = Math.Min(records.Count, first + RecoveryLookAhead);
            for (var second = first + 1; second < limit - 1; second++)
            {
                for (var third = second + 1; third < limit; third++)
                {
                    var selected = new[] { records[first], records[second], records[third] };
                    var geometry = CalculateRectangle(selected, orthogonalize);
                    if (geometry is null)
                    {
                        continue;
                    }

                    var candidate = new Candidate(
                        selected,
                        geometry.Vertices,
                        third - first,
                        first,
                        geometry.OrthogonalityError,
                        geometry.EdgeLengthSum);
                    if (candidate.Span == 2 && candidate.Start == 0)
                    {
                        return candidate;
                    }

                    if (best is null || candidate.IsBetterThan(best))
                    {
                        best = candidate;
                    }
                }
            }
        }

        return best;
    }

    private static RectangleGeometry? CalculateRectangle(
        IReadOnlyList<IndexedRecord> records,
        bool orthogonalize)
    {
        RectangleGeometry? best = null;
        for (var cornerIndex = 0; cornerIndex < records.Count; cornerIndex++)
        {
            var adjacentIndices = Enumerable.Range(0, records.Count)
                .Where(index => index != cornerIndex)
                .ToArray();
            var corner = Point(records[cornerIndex].Record);
            var firstAdjacent = Point(records[adjacentIndices[0]].Record);
            var secondAdjacent = Point(records[adjacentIndices[1]].Record);
            var firstVector = firstAdjacent - corner;
            var secondVector = secondAdjacent - corner;
            var firstLength = firstVector.Length;
            var secondLength = secondVector.Length;
            if (firstLength <= MinimumLength || secondLength <= MinimumLength)
            {
                continue;
            }

            var cosine = Math.Abs(Point2.Dot(firstVector, secondVector) / (firstLength * secondLength));
            if (cosine > OrthogonalityCosineTolerance)
            {
                continue;
            }

            var vertices = orthogonalize
                ? Orthogonalize(corner, firstAdjacent, secondAdjacent, firstVector, secondVector, firstLength, secondLength)
                : Parallelogram(corner, firstAdjacent, secondAdjacent);
            if (vertices is null)
            {
                continue;
            }

            var geometry = new RectangleGeometry(vertices, cosine, firstLength + secondLength);
            if (best is null || geometry.OrthogonalityError < best.OrthogonalityError)
            {
                best = geometry;
            }
        }

        return best;
    }

    private static Point2[] Parallelogram(Point2 corner, Point2 firstAdjacent, Point2 secondAdjacent) =>
        [corner, firstAdjacent, firstAdjacent + secondAdjacent - corner, secondAdjacent];

    private static Point2[]? Orthogonalize(
        Point2 corner,
        Point2 firstAdjacent,
        Point2 secondAdjacent,
        Point2 firstVector,
        Point2 secondVector,
        double firstLength,
        double secondLength)
    {
        var primaryPoint = firstLength >= secondLength ? firstAdjacent : secondAdjacent;
        var primaryVector = firstLength >= secondLength ? firstVector : secondVector;
        var secondaryVector = firstLength >= secondLength ? secondVector : firstVector;
        var primaryLength = Math.Max(firstLength, secondLength);
        var primaryUnit = primaryVector / primaryLength;
        var perpendicularUnit = new Point2(-primaryUnit.Y, primaryUnit.X);
        var perpendicularDistance = Point2.Dot(secondaryVector, perpendicularUnit);
        if (Math.Abs(perpendicularDistance) <= MinimumLength)
        {
            return null;
        }

        var secondaryPoint = corner + (perpendicularUnit * perpendicularDistance);
        var fourth = primaryPoint + (perpendicularUnit * perpendicularDistance);
        return [corner, primaryPoint, fourth, secondaryPoint];
    }

    private static Point2 Point(SurveyRecord record) => new(record.Point.Easting, record.Point.Northing);

    private sealed record Candidate(
        IReadOnlyList<IndexedRecord> Records,
        IReadOnlyList<Point2> Vertices,
        int Span,
        int Start,
        double OrthogonalityError,
        double EdgeLengthSum)
    {
        public bool IsBetterThan(Candidate other)
        {
            var comparison = Span.CompareTo(other.Span);
            if (comparison != 0)
            {
                return comparison < 0;
            }

            comparison = Start.CompareTo(other.Start);
            if (comparison != 0)
            {
                return comparison < 0;
            }

            comparison = OrthogonalityError.CompareTo(other.OrthogonalityError);
            return comparison != 0
                ? comparison < 0
                : EdgeLengthSum < other.EdgeLengthSum;
        }
    }

    private sealed record RectangleGeometry(
        IReadOnlyList<Point2> Vertices,
        double OrthogonalityError,
        double EdgeLengthSum);

    private sealed record IndexedRecord(SurveyRecord Record, int OriginalOrdinal);

    internal readonly record struct Point2(double X, double Y)
    {
        public double Length => Math.Sqrt((X * X) + (Y * Y));

        public static double Dot(Point2 left, Point2 right) => (left.X * right.X) + (left.Y * right.Y);

        public static Point2 operator +(Point2 left, Point2 right) => new(left.X + right.X, left.Y + right.Y);

        public static Point2 operator -(Point2 left, Point2 right) => new(left.X - right.X, left.Y - right.Y);

        public static Point2 operator *(Point2 point, double scalar) => new(point.X * scalar, point.Y * scalar);

        public static Point2 operator /(Point2 point, double scalar) => new(point.X / scalar, point.Y / scalar);
    }
}

internal sealed record StructureOutlineReconstruction(
    IReadOnlyList<StructureOutlineMatch> Matches,
    IReadOnlyList<SurveyRecord> UnmatchedRecords);

internal sealed record StructureOutlineMatch(
    IReadOnlyList<SurveyRecord> Records,
    IReadOnlyList<StructureOutlineReconstructor.Point2> Vertices);
