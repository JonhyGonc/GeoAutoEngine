using Autodesk.AutoCAD.Runtime;
using GeoAutoEngine.Cad2026.VideoInspection;
using AutoCadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(GeoAutoEngine.Plugin.VideoInspection.VideoInspectionAssociationRemovalCommands))]

namespace GeoAutoEngine.Plugin.VideoInspection;

internal static class VideoInspectionAssociationRemovalRuntime
{
    private static readonly AutoCadInspectionGeometryService GeometryService = new();
    private static readonly AutoCadInspectionCoverageAssociationCleaner CoverageCleaner = new();
    private static Guid? _pendingSectionId;

    internal static void Request(Guid sectionId)
    {
        _pendingSectionId = sectionId;
        var document = AutoCadApplication.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("O Civil 3D não possui um desenho ativo.");
        document.SendStringToExecute(
            "GEOAUTO3VIREMOVEASSOCIATION ",
            activate: true,
            wrapUpInactiveDoc: false,
            echoCommand: false);
    }

    internal static void ApplyPending()
    {
        var sectionId = _pendingSectionId
            ?? throw new InvalidOperationException("Selecione um trecho associado na Palette.");
        _pendingSectionId = null;

        var document = AutoCadApplication.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("O Civil 3D não possui um desenho ativo.");
        var session = VideoInspectionRuntime.CurrentSession
            ?? throw new InvalidOperationException("Nenhuma inspeção está carregada.");
        var section = session.FindSection(sectionId)
            ?? throw new InvalidOperationException("O trecho selecionado não existe na inspeção carregada.");
        if (section.Association is null)
        {
            document.Editor.WriteMessage("\nGeoAutoEngine: o trecho já está sem associação de geometria.\n");
            GeoAutoEnginePalette.RefreshVideoInspection();
            return;
        }

        GeometryService.RemoveAssociation(document, session.ReportId, section.SectionId);
        try
        {
            // Remove somente as linhas visuais derivadas e o estado de cobertura.
            // A rede original e qualquer BREAK já realizado permanecem intactos.
            CoverageCleaner.Clear(document, session.ReportId, section.SectionId);
        }
        catch (System.Exception exception) when (
            exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Limpeza da cobertura ao remover associação", exception);
        }

        section.RemoveAssociation();
        GeoAutoEnginePalette.RefreshVideoInspection();
        document.Editor.WriteMessage(
            "\nGeoAutoEngine: associação de geometria removida. A Polyline original não foi apagada nem modificada.\n");
    }
}

public sealed class VideoInspectionAssociationRemovalCommands
{
    [CommandMethod("GEOAUTO3VIREMOVEASSOCIATION", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public void RemoveAssociation()
    {
        try
        {
            VideoInspectionAssociationRemovalRuntime.ApplyPending();
        }
        catch (System.Exception exception) when (
            exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Remoção da associação de geometria da vídeo-inspeção", exception);
            AutoCadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nGeoAutoEngine: {exception.Message}\n");
        }
    }
}
