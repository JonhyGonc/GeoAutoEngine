using System.IO;
using System.Text.Json;
using GeoAutoEngine.Application;

namespace GeoAutoEngine.Plugin;

internal sealed class PaletteSettingsData
{
    public bool OpenPaletteOnStartup { get; set; } = true;
}

internal static class PaletteSettingsStore
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GeoAutoEngine3");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "ui-settings.json");

    internal static DrawingPresentationSettingsStore DrawingPresentation { get; } = new(
        Path.Combine(SettingsDirectory, "drawing-presentation.json"));

    internal static PaletteSettingsData Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<PaletteSettingsData>(File.ReadAllText(SettingsPath)) ?? new()
                : new();
        }
        catch (Exception exception)
        {
            PluginLog.Error("Leitura das configurações da interface", exception);
            return new();
        }
    }

    internal static void Save(PaletteSettingsData settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
        }
        catch (Exception exception)
        {
            PluginLog.Error("Gravação das configurações da interface", exception);
        }
    }

    internal static DrawingPlanBuildOptions LoadDrawingPlanBuildOptions()
    {
        var settings = DrawingPresentation.Load();
        return new DrawingPlanBuildOptions(
            textHeight: settings.TextHeightMeters,
            showRevisionMarks: settings.ShowRevisionMarks);
    }
}
