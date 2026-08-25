using System.Collections.Concurrent;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using GeoAutoEngine.Application.VideoInspection;
using GeoAutoEngine.Cad2026.VideoInspection;
using GeoAutoEngine.Plugin.VideoInspection.Transport;
using AutoCadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace GeoAutoEngine.Plugin.VideoInspection;

internal static class VideoInspectionRuntime
{
    private const string SmokeMarkerEnvironmentVariable = "GEOAUTOENGINE_VIDEO_SMOKE_MARKER";
    private static readonly ConcurrentQueue<ReceivedInspection> Inbox = new();
    private static readonly InspectionExchangeSerializer Serializer = new();
    private static readonly AutoCadInspectionGeometryService GeometryService = new();
    private static readonly AutoCadInspectionRenderer Renderer = new();
    private static NamedPipeInspectionServer? _server;
    private static Database? _activeDatabase;

    internal static VideoInspectionSession? CurrentSession { get; private set; }

    internal static void Initialize(string targetHostVersion)
    {
        if (_server is not null)
        {
            return;
        }

        var version = typeof(VideoInspectionRuntime).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        _server = new NamedPipeInspectionServer(
            Serializer,
            received => Inbox.Enqueue(received),
            version,
            "AutoCAD/Civil 3D",
            targetHostVersion);
        _server.Start();
        AutoCadApplication.Idle += OnIdle;
    }

    internal static void Terminate()
    {
        AutoCadApplication.Idle -= OnIdle;
        _server?.Dispose();
        _server = null;
        _activeDatabase = null;
        CurrentSession = null;
        while (Inbox.TryDequeue(out _))
        {
        }
    }

    internal static void ImportFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        VideoInspectionPerformanceProbe.BeginImport(path);
        VideoInspectionPerformanceProbe.Count("ImportFile");
        try
        {
            string payload;
            using (VideoInspectionPerformanceProbe.Measure("JSON read"))
            {
                payload = File.ReadAllText(path, System.Text.Encoding.UTF8);
            }

            VideoInspectionPerformanceProbe.Count("InspectionExchangeSerializer.Deserialize");
            var document = Serializer.Deserialize(payload, out var serializerMetrics);
            VideoInspectionPerformanceProbe.Record(
                "JsonSerializer.Deserialize",
                serializerMetrics.DeserializeMilliseconds);
            VideoInspectionPerformanceProbe.Record(
                "InspectionExchangeSerializer.Validate",
                serializerMetrics.ValidationMilliseconds);

            Activate(document, "Arquivo de inspeção");
            var session = CurrentSession!;
            VideoInspectionPerformanceProbe.CompleteImport(
                session.Sections.Count,
                session.OccurrenceCount,
                System.Windows.Threading.Dispatcher.CurrentDispatcher);
        }
        catch (Exception exception)
        {
            VideoInspectionPerformanceProbe.FailImport(exception);
            throw;
        }
    }

    internal static void Associate(Guid sectionId)
    {
        var document = ActiveDocument();
        var section = RequiredSection(sectionId);
        var association = GeometryService.PromptAssociation(document, CurrentSession!.ReportId, section.SectionId);
        if (association is null)
        {
            document.Editor.WriteMessage("\nGeoAutoEngine: associação cancelada.\n");
            return;
        }

        section.Associate(association);
        var positioning = Position(document, section);
        GeoAutoEnginePalette.RefreshVideoInspection();
        document.Editor.WriteMessage(
            $"\nGeoAutoEngine: trecho associado à Polyline {association.GeometryHandle} " +
            $"({association.Direction}); {PositioningSummary(positioning.PositionedEvents)}. " +
            "Nenhuma entidade foi criada no desenho.\n");
    }

    internal static void Commit(Guid sectionId)
    {
        var document = ActiveDocument();
        var section = RequiredSection(sectionId);
        var positioning = Position(document, section);
        var positioned = positioning.PositionedEvents;
        var result = Renderer.Commit(document, CurrentSession!, section, positioned);
        var invalid = positioned.Count(item => !item.CanCommit);
        if (invalid == 0)
        {
            section.MarkGenerated(
                $"Gerado: {result.CreatedCount} novo(s), {result.UpdatedCount} atualizado(s).");
        }

        GeoAutoEnginePalette.RefreshVideoInspection();
        foreach (var entity in result.Entities)
        {
            var commitDiagnostic =
                $"VI COMMIT: Occurrence={entity.OccurrenceId:D}; Entity={entity.EntityType}; " +
                $"ObjectId={entity.ObjectId}; Handle={entity.Handle}; " +
                $"ObjectId.IsNull={entity.ObjectIdIsNull}; ObjectId.IsValid={entity.ObjectIdIsValid}; " +
                $"Operation={(entity.Created ? "Created" : "Updated")}.";
            PluginLog.Information(commitDiagnostic);
            document.Editor.WriteMessage("\n" + commitDiagnostic);
        }

        var valid = positioned.Count(item => item.CanCommit && item.Locations.Count > 0);
        var pipeline =
            $"VI PIPELINE: SectionsLoaded={CurrentSession!.Sections.Count}; GeometryResolved=true; " +
            $"Direction={section.Association!.Direction}; OccurrencesInput={section.Occurrences.Count}; " +
            $"OccurrencesPositioned={valid}; " +
            $"PermanentEntitiesCreated={result.CreatedCount}; " +
            $"PermanentEntitiesUpdated={result.UpdatedCount}; " +
            $"TransactionCommitted={result.TransactionCommitted}; CurrentSpace={result.CurrentSpaceName}; " +
            $"CurrentSpaceIsModelSpace={result.CurrentSpaceIsModelSpace}; TargetSpace={result.TargetSpaceName}.";
        PluginLog.Information(pipeline);
        document.Editor.WriteMessage("\n" + pipeline);
        WriteSmokeMarker("committed", CurrentSession!, result);
        document.Editor.WriteMessage(
            $"\nGeoAutoEngine: vídeo-inspeção criada={result.CreatedCount}; atualizada={result.UpdatedCount}; " +
            $"sem posição/atenção={result.SkippedCount}.\n");
    }

    internal static void DeliverPending()
    {
        while (Inbox.TryDequeue(out var received))
        {
            Activate(received.Document, "Inspeção recebida");
        }
    }

    private static void OnIdle(object? sender, EventArgs eventArgs)
    {
        var document = AutoCadApplication.DocumentManager.MdiActiveDocument;
        if (document is null || !string.IsNullOrWhiteSpace(document.CommandInProgress))
        {
            return;
        }

        if (!ReferenceEquals(_activeDatabase, document.Database))
        {
            _activeDatabase = document.Database;
            if (CurrentSession is not null)
            {
                RestoreAssociations(document, CurrentSession);
                GeoAutoEnginePalette.RefreshVideoInspection();
            }
        }

        DeliverPending();
    }

    private static void Activate(InspectionExchangeDocument document, string source)
    {
        VideoInspectionPerformanceProbe.Count("Activate");
        VideoInspectionSession session;
        using (VideoInspectionPerformanceProbe.Measure("VideoInspectionSessionMapper.Map"))
        {
            session = VideoInspectionSessionMapper.Map(document);
        }

        var activeDocument = AutoCadApplication.DocumentManager.MdiActiveDocument;
        if (activeDocument is not null)
        {
            RestoreAssociations(activeDocument, session);
            _activeDatabase = activeDocument.Database;
        }

        CurrentSession = session;
        GeoAutoEnginePalette.UpdateVideoInspectionSession(session, source);
        using (VideoInspectionPerformanceProbe.Measure("Import bookkeeping/log"))
        {
            PluginLog.Information(
                $"{source}: ReportId={session.ReportId:D}; trechos={session.Sections.Count}; " +
                $"ocorrências={session.OccurrenceCount}.");
            WriteSmokeMarker("received", session, null);
        }
    }

    private static void RestoreAssociations(Document document, VideoInspectionSession session)
    {
        foreach (var section in session.Sections)
        {
            VideoInspectionPerformanceProbe.Count("RestoreAssociation");
            CadSectionAssociation? association;
            using (VideoInspectionPerformanceProbe.Measure("CAD association lookup"))
            {
                association = GeometryService.RestoreAssociation(
                    document,
                    session.ReportId,
                    section.SectionId);
            }

            if (association is null)
            {
                continue;
            }

            bool geometryExists;
            using (VideoInspectionPerformanceProbe.Measure("CAD association lookup"))
            {
                geometryExists = GeometryService.GeometryExists(document, association);
            }

            if (geometryExists)
            {
                VideoInspectionPerformanceProbe.Count("RestorePosition");
                section.Associate(association);
                using (VideoInspectionPerformanceProbe.Measure("Restored anomaly positioning"))
                {
                    Position(document, section);
                }
            }
            else
            {
                section.MarkAssociationLost("A geometria associada não existe mais.");
            }
        }
    }

    private static AutoCadInspectionPositioningResult Position(
        Document document,
        InspectionSectionSession section)
    {
        var positioning = GeometryService.ResolveWithDiagnostics(document, section);
        WritePositioningDiagnostics(document, positioning);
        section.SetPositioning(positioning.PositionedEvents);
        return positioning;
    }

    private static string PositioningSummary(IReadOnlyList<PositionedInspectionEvent> positionedEvents)
    {
        var positioned = positionedEvents.Count(item => item.CanCommit && item.Locations.Count > 0);
        var attention = positionedEvents.Count(item => !item.CanCommit);
        return $"{positioned} ocorrência(s) posicionada(s); {attention} requer(em) atenção";
    }

    private static Document ActiveDocument() =>
        AutoCadApplication.DocumentManager.MdiActiveDocument
        ?? throw new InvalidOperationException("O Civil 3D não possui um desenho ativo.");

    private static InspectionSectionSession RequiredSection(Guid sectionId) =>
        CurrentSession?.FindSection(sectionId)
        ?? throw new InvalidOperationException("Selecione um trecho de inspeção válido.");

    private static void WritePositioningDiagnostics(
        Document document,
        AutoCadInspectionPositioningResult positioning)
    {
        foreach (var location in positioning.Locations)
        {
            var message = string.Join(string.Empty,
                "[VI DEBUG] ",
                FormattableString.Invariant(
                    $"Occurrence={location.OccurrenceId:D}; LocationIndex={location.LocationIndex}; "),
                FormattableString.Invariant(
                    $"InspectionDistance={location.InspectionDistance:F3}; CadDistance={location.CadDistance:F3}; "),
                FormattableString.Invariant(
                    $"GeometryLength={location.GeometryLength:F3}; Direction={location.Direction}; "),
                FormattableString.Invariant(
                    $"Point=({location.Point.X:F3}, {location.Point.Y:F3}, {location.Point.Z:F3}); "),
                FormattableString.Invariant(
                    $"Polyline.GetPointAtDist=({location.PolylinePoint.X:F3}, "),
                FormattableString.Invariant(
                    $"{location.PolylinePoint.Y:F3}, {location.PolylinePoint.Z:F3}); "),
                FormattableString.Invariant(
                    $"Deviation={location.PolylineDeviation:G6}; Document={location.DocumentName}; "),
                $"Database={location.DatabaseFilename}; IsMdiActiveDocument={location.IsMdiActiveDocument}.");
            PluginLog.Information(message);
            document.Editor.WriteMessage("\n" + message);
        }
    }

    private static void WriteSmokeMarker(
        string phase,
        VideoInspectionSession session,
        AutoCadInspectionCommitResult? commitResult)
    {
        var path = Environment.GetEnvironmentVariable(SmokeMarkerEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new
        {
            phase,
            reportId = session.ReportId,
            sections = session.Sections.Count,
            occurrences = session.OccurrenceCount,
            created = commitResult?.CreatedCount,
            updated = commitResult?.UpdatedCount,
            skipped = commitResult?.SkippedCount,
        }));
    }
}
