using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Cad2026.VideoInspection;

public sealed class AutoCadInspectionGeometryService
{
    private const double CoverageSelectionTolerance = 1e-4;
    private readonly AutoCadInspectionAssociationStore _associationStore = new();
    private readonly LinearReferenceEngine _linearReferenceEngine = new();

    public CadSectionAssociation? PromptAssociation(
        Document document,
        Guid reportId,
        Guid sectionId)
    {
        ArgumentNullException.ThrowIfNull(document);
        var options = new PromptEntityOptions("\nSelecione a Polyline correspondente ao trecho: ");
        options.SetRejectMessage("\nVídeo-inspeção aceita somente Polyline 2D.");
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

            var physical = ResolvePhysicalPolylineFromSelection(
                document.Database,
                transaction,
                selectedPolyline);
            if (physical is null)
            {
                document.Editor.WriteMessage(
                    "\nGeoAutoEngine: não foi possível localizar a Polyline física correspondente à cobertura selecionada.\n");
                return null;
            }

            handle = physical.Handle.ToString();
            length = physical.Length;
            start = new LinearPoint(physical.StartPoint.X, physical.StartPoint.Y, physical.StartPoint.Z);
            end = new LinearPoint(physical.EndPoint.X, physical.EndPoint.Y, physical.EndPoint.Z);
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
        Guid sectionId) => _associationStore.Load(document.Database, reportId, sectionId);

    public bool RemoveAssociation(Document document, Guid reportId, Guid sectionId)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var documentLock = document.LockDocument();
        return _associationStore.Remove(document.Database, reportId, sectionId);
    }

    public bool GeometryExists(Document document, CadSectionAssociation association)
    {
        using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
        var exists = AutoCadInspectionAssociationStore.TryResolvePolyline(
            document.Database,
            transaction,
            association.GeometryHandle,
            OpenMode.ForRead,
            out _);
        transaction.Commit();
        return exists;
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
        if (!AutoCadInspectionAssociationStore.TryResolvePolyline(
                document.Database,
                transaction,
                association.GeometryHandle,
                OpenMode.ForRead,
                out var polyline))
        {
            throw new InvalidOperationException("A geometria associada não existe mais.");
        }

        var positionedEvents = _linearReferenceEngine.ResolveSection(
            section,
            new AutoCadPolylineGeometry(polyline!),
            association.Direction)
            .Select(positionedEvent => PositionEntireSection(
                section,
                polyline!,
                association.Direction,
                positionedEvent))
            .ToArray();
        var geometryLength = polyline!.Length;
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

    private static Polyline? ResolvePhysicalPolylineFromSelection(
        Database database,
        Transaction transaction,
        Polyline selected)
    {
        if (AutoCadInspectionCoverageController.TryGetPhysicalSourceHandle(selected, out var sourceHandle) &&
            AutoCadInspectionAssociationStore.TryResolvePolyline(
                database,
                transaction,
                sourceHandle,
                OpenMode.ForRead,
                out var physicalFromMetadata))
        {
            return physicalFromMetadata;
        }

        if (!IsCoverageLayer(selected.Layer))
        {
            return selected;
        }

        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(
            blockTable[BlockTableRecord.ModelSpace],
            OpenMode.ForRead);
        foreach (ObjectId objectId in modelSpace)
        {
            if (objectId == selected.ObjectId ||
                transaction.GetObject(objectId, OpenMode.ForRead, openErased: false) is not Polyline candidate ||
                IsCoverageLayer(candidate.Layer))
            {
                continue;
            }

            if (Math.Abs(candidate.Length - selected.Length) > CoverageSelectionTolerance)
            {
                continue;
            }

            var sameDirection =
                candidate.StartPoint.DistanceTo(selected.StartPoint) <= CoverageSelectionTolerance &&
                candidate.EndPoint.DistanceTo(selected.EndPoint) <= CoverageSelectionTolerance;
            var reverseDirection =
                candidate.StartPoint.DistanceTo(selected.EndPoint) <= CoverageSelectionTolerance &&
                candidate.EndPoint.DistanceTo(selected.StartPoint) <= CoverageSelectionTolerance;
            if (sameDirection || reverseDirection)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsCoverageLayer(string? layerName) =>
        string.Equals(
            layerName,
            AutoCadInspectionCoverageController.InspectedLayerName,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            layerName,
            AutoCadInspectionCoverageController.InterruptedLayerName,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            layerName,
            "GF-VideoInspecao-NaoInspecionado",
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
