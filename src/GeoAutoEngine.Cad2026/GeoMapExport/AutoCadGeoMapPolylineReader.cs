using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using GeoAutoEngine.Application.GeoMapExport;

namespace GeoAutoEngine.Cad2026.GeoMapExport;

public static class AutoCadGeoMapPolylineReader
{
    private const double Tolerance = 1e-10;

    public static IReadOnlyList<GeoMapExportFeature> Read(
        Document document,
        IReadOnlyCollection<ObjectId> objectIds)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (objectIds is null || objectIds.Count == 0)
        {
            throw new InvalidOperationException("Selecione pelo menos uma Polyline 2D.");
        }

        var features = new List<GeoMapExportFeature>(objectIds.Count);
        using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
        foreach (var objectId in objectIds)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not Polyline polyline)
            {
                throw new InvalidOperationException("A seleção contém uma entidade que não é Polyline 2D.");
            }

            var handle = polyline.Handle.ToString();
            if (polyline.NumberOfVertices < 2)
            {
                throw new InvalidOperationException($"A Polyline {handle} possui menos de dois vértices.");
            }

            RejectCurvedSegments(polyline, handle);

            var vertices = new List<GeoMapCoordinate>(polyline.NumberOfVertices);
            var projectedTo2D = false;
            for (var index = 0; index < polyline.NumberOfVertices; index++)
            {
                var point = polyline.GetPoint3dAt(index);
                vertices.Add(new GeoMapCoordinate(point.X, point.Y));
                projectedTo2D |= Math.Abs(point.Z) > Tolerance;
            }

            features.Add(new GeoMapExportFeature(
                handle,
                polyline.Layer,
                vertices,
                polyline.Closed,
                polyline.Elevation,
                projectedTo2D));
        }

        return features;
    }

    private static void RejectCurvedSegments(Polyline polyline, string handle)
    {
        var segmentCount = polyline.Closed
            ? polyline.NumberOfVertices
            : polyline.NumberOfVertices - 1;
        for (var index = 0; index < segmentCount; index++)
        {
            if (Math.Abs(polyline.GetBulgeAt(index)) > Tolerance)
            {
                throw new InvalidOperationException(
                    $"A Polyline {handle} contém arco. O exportador GeoMap v0.1 aceita somente segmentos retos.");
            }
        }
    }
}
