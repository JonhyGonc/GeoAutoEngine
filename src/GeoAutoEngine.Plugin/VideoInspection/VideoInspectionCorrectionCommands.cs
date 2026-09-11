using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(GeoAutoEngine.Plugin.VideoInspection.VideoInspectionCorrectionCommands))]

namespace GeoAutoEngine.Plugin.VideoInspection;

public sealed class VideoInspectionCorrectionCommands
{
    [CommandMethod("GEOAUTO3VIEDITOCCURRENCE", CommandFlags.Modal)]
    public void ApplyOccurrenceEdit() => Execute(
        "Edição de ocorrência da vídeo-inspeção",
        VideoInspectionCorrectionRuntime.ApplyPendingEdit);

    [CommandMethod("GEOAUTO3VIRESTOREOCCURRENCE", CommandFlags.Modal)]
    public void RestoreOccurrence() => Execute(
        "Restauração de ocorrência da vídeo-inspeção",
        VideoInspectionCorrectionRuntime.RestorePendingOccurrence);

    [CommandMethod("GEOAUTO3VIEXPORTCORRECTIONS", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public void ExportCorrections() => Execute(
        "Exportação de correções para o GeoAutoDocs",
        VideoInspectionCorrectionRuntime.ExportCorrections);

    [CommandMethod("GEOAUTO3VIAPPLYSCALE", CommandFlags.Modal)]
    public void ApplyScale() => Execute(
        "Aplicação da escala da vídeo-inspeção",
        VideoInspectionScaleRuntime.ApplyToCurrentDrawing);

    private static void Execute(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (System.Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error(operation, exception);
            Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nGeoAutoEngine: {exception.Message}\n");
        }
    }
}
