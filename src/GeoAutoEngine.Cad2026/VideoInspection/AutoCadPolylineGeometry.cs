using Autodesk.AutoCAD.DatabaseServices;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Cad2026.VideoInspection;

public sealed class AutoCadPolylineGeometry : ILinearReferenceGeometry
{
    private readonly Polyline _polyline;

    public AutoCadPolylineGeometry(Polyline polyline)
    {
        _polyline = polyline ?? throw new ArgumentNullException(nameof(polyline));
    }

    public double Length => _polyline.Length;

    public LinearPoint GetPointAtDistance(double distance)
    {
        var point = _polyline.GetPointAtDist(distance);
        return new LinearPoint(point.X, point.Y, point.Z);
    }

    public LinearVector GetTangentAtDistance(double distance)
    {
        var point = _polyline.GetPointAtDist(distance);
        var tangent = _polyline.GetFirstDerivative(point).GetNormal();
        return new LinearVector(tangent.X, tangent.Y, tangent.Z);
    }
}
