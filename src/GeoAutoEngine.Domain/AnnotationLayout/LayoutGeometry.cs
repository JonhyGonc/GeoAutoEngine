namespace GeoAutoEngine.Domain.AnnotationLayout;

public static class LayoutGeometryTolerance
{
    public const double Distance = 1.0e-9;
    public const double Angle = 1.0e-10;
    public const double Score = 1.0e-8;
}

public readonly record struct LayoutPoint2
{
    public LayoutPoint2(double x, double y)
    {
        if (!NumberCompatibility.IsFinite(x) || !NumberCompatibility.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Layout coordinates must be finite.");
        }

        X = x;
        Y = y;
    }

    public double X { get; }

    public double Y { get; }

    public static LayoutPoint2 operator +(LayoutPoint2 point, LayoutVector2 vector) =>
        new(point.X + vector.X, point.Y + vector.Y);

    public static LayoutVector2 operator -(LayoutPoint2 left, LayoutPoint2 right) =>
        new(left.X - right.X, left.Y - right.Y);
}

public readonly record struct LayoutVector2
{
    public LayoutVector2(double x, double y)
    {
        if (!NumberCompatibility.IsFinite(x) || !NumberCompatibility.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Layout vector components must be finite.");
        }

        X = x;
        Y = y;
    }

    public double X { get; }

    public double Y { get; }

    public double Length => Math.Sqrt((X * X) + (Y * Y));

    public LayoutVector2 Normalized()
    {
        var length = Length;
        if (length <= LayoutGeometryTolerance.Distance)
        {
            throw new InvalidOperationException("A zero-length vector cannot be normalized.");
        }

        return new LayoutVector2(X / length, Y / length);
    }

    public static double Dot(LayoutVector2 left, LayoutVector2 right) =>
        (left.X * right.X) + (left.Y * right.Y);

    public static LayoutVector2 operator +(LayoutVector2 left, LayoutVector2 right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static LayoutVector2 operator *(LayoutVector2 vector, double scalar) =>
        new(vector.X * scalar, vector.Y * scalar);
}

public readonly record struct LayoutAabb
{
    public LayoutAabb(double minX, double minY, double maxX, double maxY)
    {
        if (!NumberCompatibility.IsFinite(minX) || !NumberCompatibility.IsFinite(minY) ||
            !NumberCompatibility.IsFinite(maxX) || !NumberCompatibility.IsFinite(maxY) ||
            minX > maxX || minY > maxY)
        {
            throw new ArgumentOutOfRangeException(nameof(minX), "Layout bounds must be finite and ordered.");
        }

        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public double MinX { get; }

    public double MinY { get; }

    public double MaxX { get; }

    public double MaxY { get; }

    public LayoutAabb Expand(double amount)
    {
        if (!NumberCompatibility.IsFinite(amount) || amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        return new LayoutAabb(MinX - amount, MinY - amount, MaxX + amount, MaxY + amount);
    }

    public LayoutAabb Union(LayoutAabb other) =>
        new(Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY), Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY));

    public bool Intersects(LayoutAabb other, double tolerance = LayoutGeometryTolerance.Distance) =>
        MinX <= other.MaxX + tolerance && MaxX + tolerance >= other.MinX &&
        MinY <= other.MaxY + tolerance && MaxY + tolerance >= other.MinY;

    public double DistanceTo(LayoutAabb other)
    {
        var dx = Math.Max(0, Math.Max(other.MinX - MaxX, MinX - other.MaxX));
        var dy = Math.Max(0, Math.Max(other.MinY - MaxY, MinY - other.MaxY));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

public readonly record struct OrientedRect
{
    public OrientedRect(LayoutPoint2 center, double halfWidth, double halfHeight, double rotationRadians)
    {
        if (!NumberCompatibility.IsFinite(halfWidth) || !NumberCompatibility.IsFinite(halfHeight) ||
            !NumberCompatibility.IsFinite(rotationRadians) || halfWidth <= 0 || halfHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(halfWidth), "Footprint dimensions must be finite and positive.");
        }

        Center = center;
        HalfWidth = halfWidth;
        HalfHeight = halfHeight;
        RotationRadians = LayoutAngles.NormalizeSigned(rotationRadians);
    }

    public LayoutPoint2 Center { get; }

    public double HalfWidth { get; }

    public double HalfHeight { get; }

    public double RotationRadians { get; }

    public LayoutVector2 WidthAxis => new(Math.Cos(RotationRadians), Math.Sin(RotationRadians));

    public LayoutVector2 HeightAxis => new(-Math.Sin(RotationRadians), Math.Cos(RotationRadians));

    public IReadOnlyList<LayoutPoint2> Corners
    {
        get
        {
            var width = WidthAxis * HalfWidth;
            var height = HeightAxis * HalfHeight;
            return
            [
                Center + (width + height),
                Center + (width + (height * -1)),
                Center + ((width * -1) + (height * -1)),
                Center + ((width * -1) + height),
            ];
        }
    }

    public LayoutAabb Bounds
    {
        get
        {
            var corners = Corners;
            return new LayoutAabb(
                corners.Min(point => point.X),
                corners.Min(point => point.Y),
                corners.Max(point => point.X),
                corners.Max(point => point.Y));
        }
    }

    public OrientedRect Expand(double amount)
    {
        if (!NumberCompatibility.IsFinite(amount) || amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        return new OrientedRect(Center, HalfWidth + amount, HalfHeight + amount, RotationRadians);
    }

    public OrientedRect Inset(double amount)
    {
        if (!NumberCompatibility.IsFinite(amount) || amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        return new OrientedRect(
            Center,
            Math.Max(LayoutGeometryTolerance.Distance, HalfWidth - amount),
            Math.Max(LayoutGeometryTolerance.Distance, HalfHeight - amount),
            RotationRadians);
    }

    public bool Intersects(OrientedRect other, double tolerance = LayoutGeometryTolerance.Distance)
    {
        var centerDelta = other.Center - Center;
        var axes = new[] { WidthAxis, HeightAxis, other.WidthAxis, other.HeightAxis };
        foreach (var axis in axes)
        {
            var centerProjection = Math.Abs(LayoutVector2.Dot(centerDelta, axis));
            var thisRadius = ProjectionRadius(this, axis);
            var otherRadius = ProjectionRadius(other, axis);
            if (centerProjection > thisRadius + otherRadius + tolerance)
            {
                return false;
            }
        }

        return true;
    }

    public bool IntersectsSegment(LayoutPoint2 start, LayoutPoint2 end, double tolerance = LayoutGeometryTolerance.Distance)
    {
        var startLocal = ToLocal(start);
        var endLocal = ToLocal(end);
        var dx = endLocal.X - startLocal.X;
        var dy = endLocal.Y - startLocal.Y;
        var minimum = 0.0;
        var maximum = 1.0;
        return Clip(-dx, startLocal.X + HalfWidth + tolerance, ref minimum, ref maximum) &&
               Clip(dx, HalfWidth + tolerance - startLocal.X, ref minimum, ref maximum) &&
               Clip(-dy, startLocal.Y + HalfHeight + tolerance, ref minimum, ref maximum) &&
               Clip(dy, HalfHeight + tolerance - startLocal.Y, ref minimum, ref maximum);
    }

    private static double ProjectionRadius(OrientedRect rectangle, LayoutVector2 axis) =>
        (rectangle.HalfWidth * Math.Abs(LayoutVector2.Dot(rectangle.WidthAxis, axis))) +
        (rectangle.HalfHeight * Math.Abs(LayoutVector2.Dot(rectangle.HeightAxis, axis)));

    private LayoutPoint2 ToLocal(LayoutPoint2 point)
    {
        var delta = point - Center;
        return new LayoutPoint2(
            LayoutVector2.Dot(delta, WidthAxis),
            LayoutVector2.Dot(delta, HeightAxis));
    }

    private static bool Clip(double coefficient, double limit, ref double minimum, ref double maximum)
    {
        if (Math.Abs(coefficient) <= LayoutGeometryTolerance.Distance)
        {
            return limit >= -LayoutGeometryTolerance.Distance;
        }

        var ratio = limit / coefficient;
        if (coefficient < 0)
        {
            if (ratio > maximum)
            {
                return false;
            }

            minimum = Math.Max(minimum, ratio);
        }
        else
        {
            if (ratio < minimum)
            {
                return false;
            }

            maximum = Math.Min(maximum, ratio);
        }

        return true;
    }
}

public static class LayoutAngles
{
    public static double NormalizeSigned(double angle)
    {
        if (!NumberCompatibility.IsFinite(angle))
        {
            throw new ArgumentOutOfRangeException(nameof(angle));
        }

        return Math.Atan2(Math.Sin(angle), Math.Cos(angle));
    }

    public static double NormalizeReadable(double angle)
    {
        var normalized = NormalizeSigned(angle);
        if (normalized > Math.PI / 2 || normalized <= -Math.PI / 2)
        {
            normalized += normalized > 0 ? -Math.PI : Math.PI;
        }

        normalized = NormalizeSigned(normalized);
        return Math.Abs(normalized + (Math.PI / 2)) <= LayoutGeometryTolerance.Angle
            ? Math.PI / 2
            : normalized;
    }
}
