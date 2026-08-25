namespace GeoAutoEngine.Domain;

public static class NumberCompatibility
{
    public static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}
