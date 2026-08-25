using System.Text.Json;
using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Application;

public enum BlockScaleMode
{
    Automatic,
}

public sealed record DrawingPresentationSettings
{
    public const int DefaultScaleDenominator = 250;
    public const double DefaultPrintedTextHeightMillimeters = 2.0;

    public DrawingPresentationSettings(
        int scaleDenominator = DefaultScaleDenominator,
        double printedTextHeightMillimeters = DefaultPrintedTextHeightMillimeters,
        BlockScaleMode blockScaleMode = BlockScaleMode.Automatic,
        bool showRevisionMarks = true,
        bool addStandardSheet = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(scaleDenominator, 1);
        if (!NumberCompatibility.IsFinite(printedTextHeightMillimeters) || printedTextHeightMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(printedTextHeightMillimeters));
        }

        ScaleDenominator = scaleDenominator;
        PrintedTextHeightMillimeters = printedTextHeightMillimeters;
        BlockScaleMode = blockScaleMode;
        ShowRevisionMarks = showRevisionMarks;
        AddStandardSheet = addStandardSheet;
    }

    public int ScaleDenominator { get; }

    public double PrintedTextHeightMillimeters { get; }

    public BlockScaleMode BlockScaleMode { get; }

    public bool ShowRevisionMarks { get; }

    public bool AddStandardSheet { get; }

    public double TextHeightMeters => PrintedTextHeightMillimeters * ScaleDenominator / 1000d;

    public static DrawingPresentationSettings Defaults() => new();

    public DrawingPresentationSettings RestoreDefaults() => Defaults();
}

public sealed class DrawingPresentationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };
    private readonly string _path;

    public DrawingPresentationSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public DrawingPresentationSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return DrawingPresentationSettings.Defaults();
            }

            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<DrawingPresentationSettings>(json, JsonOptions)
                ?? DrawingPresentationSettings.Defaults();
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(nameof(DrawingPresentationSettings.AddStandardSheet), out _)
                ? settings
                : new DrawingPresentationSettings(
                    settings.ScaleDenominator,
                    settings.PrintedTextHeightMillimeters,
                    settings.BlockScaleMode,
                    settings.ShowRevisionMarks,
                    addStandardSheet: true);
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return DrawingPresentationSettings.Defaults();
        }
    }

    public void Save(DrawingPresentationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
