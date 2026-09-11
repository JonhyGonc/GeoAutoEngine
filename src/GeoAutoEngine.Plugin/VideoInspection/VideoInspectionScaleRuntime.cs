using Autodesk.AutoCAD.ApplicationServices;
using GeoAutoEngine.Cad2026.VideoInspection;
using AutoCadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace GeoAutoEngine.Plugin.VideoInspection;

internal static class VideoInspectionScaleRuntime
{
    private static readonly AutoCadInspectionGeometryService GeometryService = new();
    private static readonly AutoCadInspectionRenderer Renderer = new();
    private static readonly AutoCadInspectionRenderGeometryBridge RenderGeometryBridge = new();
    private static readonly AutoCadInspectionManagedEntityProbe ManagedEntityProbe = new();

    internal static void RequestApply() => QueueCommand("GEOAUTO3VIAPPLYSCALE");

    internal static void ApplyToCurrentDrawing()
    {
        var session = VideoInspectionRuntime.CurrentSession
            ?? throw new InvalidOperationException(
                "Carregue uma inspeção antes de aplicar a escala ao desenho.");
        var document = ActiveDocument();
        var scale = PaletteSettingsStore.LoadVideoInspectionScaleProfile();
        var updatedSections = 0;
        var failedSections = 0;
        var updatedCalls = 0;

        foreach (var section in session.Sections)
        {
            if (section.Association is null ||
                !ManagedEntityProbe.HasManagedEntities(
                    document,
                    session.ReportId,
                    section.SectionId))
            {
                continue;
            }

            try
            {
                var positioning = GeometryService.ResolveWithDiagnostics(document, section);
                section.SetPositioning(positioning.PositionedEvents);
                AutoCadInspectionCommitResult result;
                using (RenderGeometryBridge.Prepare(
                           document,
                           session.ReportId,
                           section.Association.GeometryHandle))
                {
                    result = Renderer.Commit(
                        document,
                        session,
                        section,
                        positioning.PositionedEvents,
                        scale);
                }
                var invalid = positioning.PositionedEvents.Count(item => !item.CanCommit);
                if (invalid == 0)
                {
                    section.MarkGenerated(
                        $"Escala 1:{scale.ScaleDenominator} aplicada: " +
                        $"{result.UpdatedCount} atualizado(s).");
                }

                updatedSections++;
                updatedCalls += result.UpdatedCount + result.CreatedCount;
            }
            catch (System.Exception exception) when (
                exception is not StackOverflowException and not OutOfMemoryException)
            {
                failedSections++;
                section.MarkError($"Falha ao aplicar escala 1:{scale.ScaleDenominator}: {exception.Message}");
                PluginLog.Error(
                    $"Aplicação da escala 1:{scale.ScaleDenominator} no trecho {section.SectionId:D}",
                    exception);
            }
        }

        GeoAutoEnginePalette.RefreshVideoInspection();
        if (updatedSections == 0 && failedSections == 0)
        {
            document.Editor.WriteMessage(
                $"\nGeoAutoEngine: escala 1:{scale.ScaleDenominator} salva. " +
                "Nenhum estaqueamento já gerado foi encontrado para atualizar.\n");
            return;
        }

        document.Editor.WriteMessage(
            $"\nGeoAutoEngine: escala 1:{scale.ScaleDenominator} aplicada em " +
            $"{updatedSections} trecho(s); chamados atualizados={updatedCalls}; " +
            $"falhas={failedSections}.\n");
    }

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
        catch (System.Exception exception) when (
            exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error($"Agendamento do comando {command}", exception);
            throw;
        }
    }
}
