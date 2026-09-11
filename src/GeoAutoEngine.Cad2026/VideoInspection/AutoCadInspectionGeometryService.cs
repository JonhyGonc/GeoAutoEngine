using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Cad2026.VideoInspection;

public sealed class AutoCadInspectionGeometryService
{
    private const double GeometryTolerance = 1e-6;
    private const string LegacyNotInspectedLayerName = "GF-VideoInspecao-NaoInspecionado";

    private readonly AutoCadInspectionAssociationStore _associationStore = new();
    private readonly LinearReferenceEngine _linearReferenceEngine = new();

    public CadSectionAssociation? PromptAssociation(
        Document document,
        Guid reportId,
        Guid sectionId)
    {
        ArgumentNullException.ThrowIfNull(document);
        var options = new PromptEntityOptions("\nSelecione a Polyline correspondente ao trecho: ");
        options.SetRejectMessage("\nVídeo-inspeção V0.1 aceita somente Polyline 2D.");
        options.AddAllowedClass(typeof(Polyline), exactMatch: true);
        var selected = document.Editor.GetEntity(options);
        if (selected.Status != PromptStatus.OK)
        {
            return null;
        }

        string handle;
        double length;
        LinearPoint start;
        LinearPoint end;
        using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
        {
            if (transaction.GetObject(selected.ObjectId, OpenMode.ForRead) is not Polyline selectedPolyline)
            {
                document.Editor.WriteMessage("\nGeoAutoEngine: a entidade selecionada não é uma Polyline 2D.\n");
                return null;
            }

            var physicalPolyline = ResolvePhysicalSelection(
                document.Database,
                transaction,
                selectedPolyline);
            handle = physicalPolyline.Handle.ToString();
            length = physicalPolyline.Length;
            start = new LinearPoint(
                physicalPolyline.StartPoint.X,
                physicalPolyline.StartPoint.Y,
                physicalPolyline.StartPoint.Z);
            end = new LinearPoint(
                physicalPolyline.EndPoint.X,
                physicalPolyline.EndPoint.Y,
                physicalPolyline.EndPoint.Z);
            transaction.Commit();
        }

        var pointResult = document.Editor.GetPoint(new PromptPointOptions(
            "\nIndique a extremidade correspondente ao início da filmagem: "));
        if (pointResult.Status != PromptStatus.OK)
        {
            return null;
        }

        var indicated = new LinearPoint(pointResult.Value.X, pointResult.Value.Y, pointResult.Value.Z);
        var association = new CadSectionAssociation(
            reportId,
            sectionId,
            handle,
            DirectionResolver.Resolve(indicated, start, end),
            length);
        using var documentLock = document.LockDocument();
        _associationStore.Save(document.Database, association);
        return association;
    }

    public CadSectionAssociation? RestoreAssociation(
        Document document,
        Guid reportId,
        Guid sectionId)
    {
        var association = _associationStore.Load(document.Database, reportId, sectionId);
        if (association is null)
        {
            return null;
        }

        using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
        var geometry = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
            document.Database,
            transaction,
            association.GeometryHandle,
            OpenMode.ForRead);
        if (geometry is null)
        {
            transaction.Commit();
            return association;
        }

        var restored = association with
        {
            LastKnownGeometryLength = geometry.Length,
        };
        transaction.Commit();
        return restored;
    }

    public bool RemoveAssociation(Document document, Guid reportId, Guid sectionId)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var documentLock = document.LockDocument();
        return _associationStore.Remove(document.Database, reportId, sectionId);
    }

    public bool GeometryExists(Document document, CadSectionAssociation association)
    {
        try
        {
            using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
            var geometry = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
                document.Database,
                transaction,
                association.GeometryHandle,
                OpenMode.ForRead);
            var exists = geometry is not null && geometry.NumberOfVertices > 0;
            transaction.Commit();
            return exists;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public IReadOnlyList<PositionedInspectionEvent> Resolve(
        Document document,
        InspectionSectionSession section)
        => ResolveWithDiagnostics(document, section).PositionedEvents;

    public AutoCadInspectionPositioningResult ResolveWithDiagnostics(
        Document document,
        InspectionSectionSession section)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(section);
        var association = section.Association
            ?? throw new InvalidOperationException("O trecho ainda não possui geometria associada.");
        using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
        var polyline = AutoCadInspectionSharedCoverageStore.ResolvePolyline(
            document.Database,
            transaction,
            association.GeometryHandle,
            OpenMode.ForRead)
            ?? throw new InvalidOperationException(
                "O segmento físico associado ao trecho não existe mais. Remova a associação e selecione a geometria novamente.");

        var positionedEvents = _linearReferenceEngine.ResolveSection(
            section,
            new AutoCadPolylineGeometry(polyline),
            association.Direction)
            .Select(positionedEvent => PositionEntireSection(
                section,
                polyline,
                association.Direction,
                positionedEvent))
            .ToArray();
        var geometryLength = polyline.Length;
        ValidateFinite(geometryLength, "GeometryLength");
        var diagnostics = positionedEvents
            .Where(positionedEvent => positionedEvent.CanCommit)
            .SelectMany(positionedEvent => positionedEvent.Locations.Select(location =>
                CreateDiagnostic(
                    document,
                    polyline,
                    association.Direction,
                    geometryLength,
                    positionedEvent,
                    location)))
            .ToArray();
        transaction.Commit();
        return new AutoCadInspectionPositioningResult(
            positionedEvents,
            Array.AsReadOnly(diagnostics));
    }

    private static Polyline ResolvePhysicalSelection(
        Database database,
        Transaction transaction,
        Polyline selectedPolyline)
    {
        if (!IsCoverageOverlayLayer(selectedPolyline.Layer))
        {
            return selectedPolyline;
        }

        var physical = FindCoincidentPhysicalPolyline(database, transaction, selectedPolyline);
        if (physical is not null)
        {
            return physical;
        }

        throw new InvalidOperationException(
            "A linha selecionada é uma cobertura visual da vídeo inspeção, mas o segmento físico " +
            "correspondente não foi encontrado. Selecione diretamente a Polyline da rede por baixo da cobertura.");
    }

    private static Polyline? FindCoincidentPhysicalPolyline(
        Database database,
        Transaction transaction,
        Polyline overlay)
    {
        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(
            blockTable[BlockTableRecord.ModelSpace],
            OpenMode.ForRead);
        foreach (ObjectId objectId in modelSpace)
        {
            if (objectId == overlay.ObjectId ||
                transaction.GetObject(objectId, OpenMode.ForRead, openErased: false) is not Polyline candidate ||
                IsCoverageOverlayLayer(candidate.Layer))
            {
                continue;
            }

            if (Math.Abs(candidate.Length - overlay.Length) > GeometryTolerance)
            {
                continue;
            }

            var sameDirection =
                candidate.StartPoint.DistanceTo(overlay.StartPoint) <= GeometryTolerance &&
                candidate.EndPoint.DistanceTo(overlay.EndPoint) <= GeometryTolerance;
            var oppositeDirection =
                candidate.StartPoint.DistanceTo(overlay.EndPoint) <= GeometryTolerance &&
                candidate.EndPoint.DistanceTo(overlay.StartPoint) <= GeometryTolerance;
            if (sameDirection || oppositeDirection)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsCoverageOverlayLayer(string? layerName) =>
        string.Equals(
            layerName,
            AutoCadInspectionCoverageRenderer.InspectedLayerName,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            layerName,
            AutoCadInspectionCoverageRenderer.NotInspectedLayerName,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            layerName,
            LegacyNotInspectedLayerName,
            StringComparison.OrdinalIgnoreCase);

    private static PositionedInspectionEvent PositionEntireSection(
        InspectionSectionSession section,
        Polyline polyline,
        InspectionDirection direction,
        PositionedInspectionEvent positionedEvent)
    {
        if (positionedEvent.LocationMode != InspectionLocationMode.EntireSection)
        {
            return positionedEvent;
        }

        var cadDistance = InspectionAnnotationPlanner.EntireSectionCadDistance(
            section.InvestigatedMeters,
            polyline.Length,
            direction);
        var point = polyline.GetPointAtDist(cadDistance);
        var derivative = polyline.GetFirstDerivative(point);
        var location = new PositionedInspectionLocation(
            0,
            PositionedLocationRole.Start,
            0,
            cadDistance,
            new LinearPoint(point.X, point.Y, point.Z),
            new LinearVector(derivative.X, derivative.Y, derivative.Z));
        return positionedEvent with
        {
            Locations = Array.AsReadOnly([location]),
            Warnings = Array.Empty<string>(),
            CanCommit = true,
        };
    }

    private static AutoCadInspectionLocationDiagnostic CreateDiagnostic(
        Document document,
        Polyline polyline,
        InspectionDirection direction,
        double geometryLength,
        PositionedInspectionEvent positionedEvent,
        PositionedInspectionLocation location)
    {
        ValidateFinite(location.InspectionDistance, "InspectionDistance");
        ValidateFinite(location.CadDistance, "CadDistance");
        ValidateFinite(location.Position.X, "Point.X");
        ValidateFinite(location.Position.Y, "Point.Y");
        ValidateFinite(location.Position.Z, "Point.Z");
        ValidateFinite(location.Tangent.X, "Tangent.X");
        ValidateFinite(location.Tangent.Y, "Tangent.Y");
        ValidateFinite(location.Tangent.Z, "Tangent.Z");

        var testPoint = polyline.GetPointAtDist(location.CadDistance);
        ValidateFinite(testPoint.X, "PolylinePoint.X");
        ValidateFinite(testPoint.Y, "PolylinePoint.Y");
        ValidateFinite(testPoint.Z, "PolylinePoint.Z");
        var resolvedPoint = new Point3d(location.Position.X, location.Position.Y, location.Position.Z);
        var deviation = resolvedPoint.DistanceTo(testPoint);
        ValidateFinite(deviation, "PolylineDeviation");
        if (deviation > 1e-6)
        {
            throw new InvalidOperationException(
                $"A ocorrência {positionedEvent.OccurrenceId:D} não pertence à Polyline associada " +
                $"(desvio WCS {deviation:G6}).");
        }

        return new AutoCadInspectionLocationDiagnostic(
            positionedEvent.OccurrenceId,
            location.LocationIndex,
            location.InspectionDistance,
            location.CadDistance,
            geometryLength,
            direction,
            location.Position,
            new LinearPoint(testPoint.X, testPoint.Y, testPoint.Z),
            deviation,
            document.Name,
            document.Database.Filename ?? string.Empty,
            ReferenceEquals(
                Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument,
                document));
    }

    private static void ValidateFinite(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new InvalidOperationException($"{name} possui valor CAD inválido: {value}.");
        }
    }
}

public sealed record AutoCadInspectionPositioningResult(
    IReadOnlyList<PositionedInspectionEvent> PositionedEvents,
    IReadOnlyList<AutoCadInspectionLocationDiagnostic> Locations);

public sealed record AutoCadInspectionLocationDiagnostic(
    Guid OccurrenceId,
    int LocationIndex,
    double InspectionDistance,
    double CadDistance,
    double GeometryLength,
    InspectionDirection Direction,
    LinearPoint Point,
    LinearPoint PolylinePoint,
    double PolylineDeviation,
    string DocumentName,
    string DatabaseFilename,
    bool IsMdiActiveDocument);
