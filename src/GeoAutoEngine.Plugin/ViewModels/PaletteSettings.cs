using System.IO;
using System.Text.Json;
using GeoAutoEngine.Application;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Plugin;

internal sealed class PaletteSettingsData
{
    public bool OpenPaletteOnStartup { get; set; } = true;
    public int VideoInspectionScaleDenominator { get; set; } = 250;
    public bool AddVideoInspectionCoverageLines { get; set; } = true;
    public bool ApplyVideoInspectionBreak { get; set; } = true;
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
            var settings = File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<PaletteSettingsData>(File.ReadAllText(SettingsPath)) ?? new()
                : new();
            if (!InspectionCalloutScaleProfile.IsSupported(settings.VideoInspectionScaleDenominator))
            {
                settings.VideoInspectionScaleDenominator = InspectionCalloutScaleProfile.Default.ScaleDenominator;
            }

            return settings;
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
            if (!InspectionCalloutScaleProfile.IsSupported(settings.VideoInspectionScaleDenominator))
            {
                settings.VideoInspectionScaleDenominator = InspectionCalloutScaleProfile.Default.ScaleDenominator;
            }

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

    internal static InspectionCalloutScaleProfile LoadVideoInspectionScaleProfile() =>
        InspectionCalloutScaleProfile.Resolve(Load().VideoInspectionScaleDenominator);

    internal static DrawingPlanBuildOptions LoadDrawingPlanBuildOptions()
    {
        var settings = DrawingPresentation.Load();
        return new DrawingPlanBuildOptions(
            textHeight: settings.TextHeightMeters,
            showRevisionMarks: settings.ShowRevisionMarks);
    }
}
