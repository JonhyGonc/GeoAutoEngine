using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(GeoAutoEngine.Plugin.VideoInspection.VideoInspectionScaleCommands))]

namespace GeoAutoEngine.Plugin.VideoInspection;

public sealed class VideoInspectionScaleCommands
{
    [CommandMethod("GEOAUTO3VIAPPLYSCALE", CommandFlags.Modal)]
    public void ApplyScale()
    {
        try
        {
            VideoInspectionScaleRuntime.ApplyToCurrentDrawing();
        }
        catch (System.Exception exception) when (
            exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Aplicação da escala da vídeo-inspeção", exception);
            Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nGeoAutoEngine: {exception.Message}\n");
        }
    }
}
