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

    private static void Execute(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error(operation, exception);
            Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nGeoAutoEngine: {exception.Message}\n");
        }
    }
}
