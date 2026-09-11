using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using GeoAutoEngine.Application.VideoInspection;
using GeoAutoEngine.Cad2026.VideoInspection;
using GeoAutoEngine.Plugin.VideoInspection.Transport;
using AutoCadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using MessageBox = System.Windows.MessageBox;

namespace GeoAutoEngine.Plugin.VideoInspection;

internal static class VideoInspectionRuntime
{
    private const string SmokeMarkerEnvironmentVariable = "GEOAUTOENGINE_VIDEO_SMOKE_MARKER";
    private const string DetailedDiagnosticsEnvironmentVariable =
        "GEOAUTOENGINE_VIDEO_DETAILED_DIAGNOSTICS";
    private static readonly ConcurrentQueue<ReceivedInspection> Inbox = new();
    private static readonly InspectionExchangeSerializer Serializer = new();
    private static readonly AutoCadInspectionGeometryService GeometryService = new();
    private static readonly AutoCadInspectionRenderer Renderer = new();
    private static readonly AutoCadInspectionSharedCoverageService CoverageController = new();
    private static NamedPipeInspectionServer? _server;
    private static Document? _restoredDocument;
    private static Guid? _restoredReportId;
    private static bool _restoringAssociations;

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
        AutoCadApplication.DocumentManager.DocumentBecameCurrent += OnDocumentBecameCurrent;
        AutoCadApplication.Idle += OnIdle;
    }

    internal static void Terminate()
    {
        AutoCadApplication.Idle -= OnIdle;
        AutoCadApplication.DocumentManager.DocumentBecameCurrent -= OnDocumentBecameCurrent;
        _server?.Dispose();
        _server = null;
        _restoredDocument = null;
        _restoredReportId = null;
        _restoringAssociations = false;
        CurrentSession = null;
        while (Inbox.TryDequeue(out _))
        {
        }
    }

    internal static void ImportFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!ConfirmDiscardPendingCorrections("importar outro arquivo de inspeção"))
        {
            return;
        }

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

    internal static bool ConfirmDiscardPendingCorrections(string action)
    {
        var pending = CurrentSession?.EditedOccurrenceCount ?? 0;
        if (pending == 0)
        {
            return true;
        }

        var result = MessageBox.Show(
            $"Existem {pending} correção(ões) local(is) ainda não sincronizada(s) com o GeoAutoDocs.\n\n" +
            $"Deseja {action} e descartar essas correções?",
            "Correções pendentes — GeoAutoEngine",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
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
        MarkAssociationsRestored(document, CurrentSession!.ReportId);
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
        var settings = PaletteSettingsStore.Load();
        var result = Renderer.Commit(
            document,
            CurrentSession!,
            section,
            positioned,
            InspectionCalloutScaleProfile.Resolve(settings.VideoInspectionScaleDenominator));
        var coverage = CoverageController.Apply(
            document,
            CurrentSession!,
            section,
            positioned,
            new AutoCadInspectionCoverageOptions(
                settings.AddVideoInspectionCoverageLines,
                settings.ApplyVideoInspectionBreak));
        var invalid = positioned.Count(item => !item.CanCommit);
        if (invalid == 0)
        {
            section.MarkGenerated(
                $"Gerado: {result.CreatedCount} novo(s), {result.UpdatedCount} atualizado(s). " +
                $"Cobertura: {coverage.InspectedLength:0.##} m inspecionados; " +
                $"{coverage.RemainingLength:0.##} m pendentes.");
        }

        GeoAutoEnginePalette.RefreshVideoInspection();
        if (DetailedDiagnosticsEnabled)
        {
            foreach (var entity in result.Entities)
            {
                var commitDiagnostic =
                    $"VI COMMIT: Occurrence={entity.OccurrenceId:D}; Entity={entity.EntityType}; " +
                    $"ObjectId={entity.ObjectId}; Handle={entity.Handle}; " +
                    $"ObjectId.IsNull={entity.ObjectIdIsNull}; ObjectId.IsValid={entity.ObjectIdIsValid}; " +
                    $"Operation={(entity.Created ? "Created" : "Updated")}.";
                PluginLog.Information(commitDiagnostic);
            }
        }

        if (DetailedDiagnosticsEnabled)
        {
            var valid = positioned.Count(item => item.CanCommit && item.Locations.Count > 0);
            var pipeline =
                $"VI PIPELINE: SectionsLoaded={CurrentSession!.Sections.Count}; GeometryResolved=true; " +
                $"Direction={section.Association!.Direction}; OccurrencesInput={section.Occurrences.Count}; " +
                $"OccurrencesPositioned={valid}; " +
                $"PermanentEntitiesCreated={result.CreatedCount}; " +
                $"PermanentEntitiesUpdated={result.UpdatedCount}; " +
                $"TransactionCommitted={result.TransactionCommitted}; CurrentSpace={result.CurrentSpaceName}; " +
                $"CurrentSpaceIsModelSpace={result.CurrentSpaceIsModelSpace}; TargetSpace={result.TargetSpaceName}; " +
                $"CoverageLinesEnabled={settings.AddVideoInspectionCoverageLines}; " +
                $"CoverageBreakEnabled={settings.ApplyVideoInspectionBreak}; " +
                $"CoverageInspected={coverage.InspectedLength:F3}; " +
                $"CoverageRemaining={coverage.RemainingLength:F3}; " +
                $"CoverageBreakApplied={coverage.BreakApplied}.";
            PluginLog.Information(pipeline);
        }
        WriteSmokeMarker("committed", CurrentSession!, result);
        document.Editor.WriteMessage(
            $"\nGeoAutoEngine: vídeo-inspeção criada={result.CreatedCount}; atualizada={result.UpdatedCount}; " +
            $"sem posição/atenção={result.SkippedCount}; " +
            $"inspecionado={coverage.InspectedLength:0.##} m; " +
            $"não inspecionado={coverage.RemainingLength:0.##} m.\n");
    }

    internal static void DeliverPending()
    {
        if ((CurrentSession?.EditedOccurrenceCount ?? 0) > 0)
        {
            return;
        }

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

        DeliverPending();
    }

    private static void OnDocumentBecameCurrent(
        object sender,
        DocumentCollectionEventArgs eventArgs)
    {
        var document = eventArgs.Document;
        var session = CurrentSession;
        if (document is null || session is null)
        {
            return;
        }

        if (RestoreAssociationsForDocument(document, session))
        {
            GeoAutoEnginePalette.RefreshVideoInspection();
        }
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
            RestoreAssociationsForDocument(activeDocument, session, force: true);
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

    private static bool RestoreAssociationsForDocument(
        Document document,
        VideoInspectionSession session,
        bool force = false)
    {
        if (_restoringAssociations)
        {
            return false;
        }

        if (!force && ReferenceEquals(_restoredDocument, document) &&
            _restoredReportId == session.ReportId)
        {
            return false;
        }

        _restoringAssociations = true;
        try
        {
            RestoreAssociations(document, session);
            MarkAssociationsRestored(document, session.ReportId);
            return true;
        }
        finally
        {
            _restoringAssociations = false;
        }
    }

    private static void MarkAssociationsRestored(Document document, Guid reportId)
    {
        _restoredDocument = document;
        _restoredReportId = reportId;
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
        WritePositioningDiagnostics(positioning);
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

    private static void WritePositioningDiagnostics(AutoCadInspectionPositioningResult positioning)
    {
        if (!DetailedDiagnosticsEnabled)
        {
            return;
        }

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
        }
    }

    private static bool DetailedDiagnosticsEnabled => string.Equals(
        Environment.GetEnvironmentVariable(DetailedDiagnosticsEnvironmentVariable),
        "1",
        StringComparison.Ordinal);

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
