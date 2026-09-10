using Autodesk.AutoCAD.ApplicationServices;
using GeoAutoEngine.Application.VideoInspection;
using GeoAutoEngine.Cad2026.VideoInspection;
using GeoAutoEngine.Plugin.VideoInspection.Transport;
using AutoCadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace GeoAutoEngine.Plugin.VideoInspection;

internal static class VideoInspectionCorrectionRuntime
{
    private static readonly object Sync = new();
    private static readonly AutoCadInspectionGeometryService GeometryService = new();
    private static readonly AutoCadInspectionRenderer Renderer = new();
    private static InspectionOccurrenceEdit? _pendingEdit;
    private static (Guid SectionId, Guid OccurrenceId)? _pendingRestore;

    internal static void RequestEdit(InspectionOccurrenceEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        lock (Sync)
        {
            _pendingEdit = edit;
        }

        QueueCommand("GEOAUTO3VIEDITOCCURRENCE");
    }

    internal static void RequestRestore(Guid sectionId, Guid occurrenceId)
    {
        lock (Sync)
        {
            _pendingRestore = (sectionId, occurrenceId);
        }

        QueueCommand("GEOAUTO3VIRESTOREOCCURRENCE");
    }

    internal static void RequestExport() => QueueCommand("GEOAUTO3VIEXPORTCORRECTIONS");

    internal static void ApplyPendingEdit()
    {
        InspectionOccurrenceEdit edit;
        lock (Sync)
        {
            edit = _pendingEdit
                ?? throw new InvalidOperationException("Nenhuma edição de ocorrência está pendente.");
            _pendingEdit = null;
        }

        var session = RequiredSession();
        var section = RequiredSection(session, edit.SectionId);
        var wasGenerated = section.Status == InspectionSectionStatus.Generated;
        section.ApplyOccurrenceEdit(edit);
        RefreshPositioningAndDrawing(session, section, wasGenerated);
        GeoAutoEnginePalette.RefreshVideoInspection();

        ActiveDocument().Editor.WriteMessage(
            $"\nGeoAutoEngine: ocorrência {edit.OccurrenceId:D} editada; " +
            $"correções locais pendentes={session.EditedOccurrenceCount}.\n");
    }

    internal static void RestorePendingOccurrence()
    {
        (Guid SectionId, Guid OccurrenceId) pending;
        lock (Sync)
        {
            pending = _pendingRestore
                ?? throw new InvalidOperationException("Nenhuma restauração de ocorrência está pendente.");
            _pendingRestore = null;
        }

        var session = RequiredSession();
        var section = RequiredSection(session, pending.SectionId);
        var wasGenerated = section.Status == InspectionSectionStatus.Generated;
        if (!section.RestoreOccurrence(pending.OccurrenceId))
        {
            ActiveDocument().Editor.WriteMessage("\nGeoAutoEngine: a ocorrência já está com os valores originais.\n");
            return;
        }

        RefreshPositioningAndDrawing(session, section, wasGenerated);
        GeoAutoEnginePalette.RefreshVideoInspection();
        ActiveDocument().Editor.WriteMessage(
            $"\nGeoAutoEngine: ocorrência restaurada; correções locais pendentes={session.EditedOccurrenceCount}.\n");
    }

    internal static void ExportCorrections()
    {
        var session = RequiredSession();
        var push = session.CreateCorrectionPush();
        if (push.Corrections.Count == 0)
        {
            throw new InvalidOperationException("Não existem correções locais para exportar.");
        }

        var server = NamedPipeInspectionServer.Current
            ?? throw new InvalidOperationException("O serviço de integração com o GeoAutoDocs não está ativo.");
        var acknowledgement = server.SendCorrectionsAsync(
                push,
                TimeSpan.FromSeconds(15))
            .GetAwaiter()
            .GetResult();

        var partial = string.Equals(
            acknowledgement.Status,
            "Partial",
            StringComparison.OrdinalIgnoreCase);
        if (!acknowledgement.Accepted && !partial)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(acknowledgement.Message)
                    ? "O GeoAutoDocs recusou as correções."
                    : acknowledgement.Message);
        }

        if (acknowledgement.Accepted && acknowledgement.Results.Count > 0)
        {
            var synchronized = session.AcceptConfirmedCorrectionsAsBaseline(acknowledgement.Results);
            var remaining = session.EditedOccurrenceCount;
            GeoAutoEnginePalette.RefreshVideoInspection();

            if (remaining == 0)
            {
                ActiveDocument().Editor.WriteMessage(
                    $"\nGeoAutoEngine: {synchronized} correção(ões) sincronizada(s) com o GeoAutoDocs. " +
                    $"Confirmadas={acknowledgement.AppliedCount}; rejeitadas={acknowledgement.RejectedCount}. " +
                    $"{acknowledgement.Message}\n");
                return;
            }

            ActiveDocument().Editor.WriteMessage(
                $"\nGeoAutoEngine: sincronização parcial concluída. " +
                $"Confirmadas neste retorno={synchronized}; pendentes={remaining}; " +
                $"rejeitadas={acknowledgement.RejectedCount}. " +
                "Somente as ocorrências não confirmadas permanecem marcadas como [EDITADO]. " +
                $"{acknowledgement.Message}\n");
            return;
        }

        var fullyApplied = acknowledgement.Accepted &&
            acknowledgement.RejectedCount == 0 &&
            acknowledgement.AppliedCount == push.Corrections.Count;
        if (fullyApplied)
        {
            var synchronized = session.AcceptCurrentCorrectionsAsBaseline();
            GeoAutoEnginePalette.RefreshVideoInspection();
            ActiveDocument().Editor.WriteMessage(
                $"\nGeoAutoEngine: {synchronized} correção(ões) sincronizada(s) com o GeoAutoDocs. " +
                $"Aplicadas={acknowledgement.AppliedCount}; rejeitadas=0. " +
                $"{acknowledgement.Message}\n");
            return;
        }

        var status = partial ? "parcialmente recebidas" : "recebidas sem confirmação completa";
        GeoAutoEnginePalette.RefreshVideoInspection();
        ActiveDocument().Editor.WriteMessage(
            $"\nGeoAutoEngine: {push.Corrections.Count} correção(ões) {status} pelo GeoAutoDocs. " +
            $"Aplicadas={acknowledgement.AppliedCount}; rejeitadas={acknowledgement.RejectedCount}. " +
            "O receptor não informou resultados individuais; as correções permanecem pendentes para evitar perda de alterações. " +
            $"{acknowledgement.Message}\n");
    }

    private static void RefreshPositioningAndDrawing(
        VideoInspectionSession session,
        InspectionSectionSession section,
        bool redrawExisting)
    {
        if (section.Association is null)
        {
            return;
        }

        var document = ActiveDocument();
        var positioning = GeometryService.ResolveWithDiagnostics(document, section);
        section.SetPositioning(positioning.PositionedEvents);
        if (!redrawExisting)
        {
            return;
        }

        var result = Renderer.Commit(document, session, section, positioning.PositionedEvents);
        var invalid = positioning.PositionedEvents.Count(item => !item.CanCommit);
        if (invalid == 0)
        {
            section.MarkGenerated(
                $"Atualizado após edição: {result.CreatedCount} novo(s), {result.UpdatedCount} atualizado(s).");
        }
    }

    private static VideoInspectionSession RequiredSession() =>
        VideoInspectionRuntime.CurrentSession
        ?? throw new InvalidOperationException("Nenhuma inspeção está carregada.");

    private static InspectionSectionSession RequiredSection(VideoInspectionSession session, Guid sectionId) =>
        session.FindSection(sectionId)
        ?? throw new InvalidOperationException("O trecho da ocorrência não está mais carregado.");

    private static Document ActiveDocument() =>
        AutoCadApplication.DocumentManager.MdiActiveDocument
        ?? throw new InvalidOperationException("O Civil 3D não possui um desenho ativo.");

    private static void QueueCommand(string command)
    {
        try
        {
            ActiveDocument().SendStringToExecute(
                command + " ",
                activate: true,
                wrapUpInactiveDoc: false,
                echoCommand: false);
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error($"Agendamento do comando {command}", exception);
            throw;
        }
    }
}
