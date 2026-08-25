using System.Windows.Media;
using GeoAutoEngine.Application;
using Brush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace GeoAutoEngine.Plugin;

internal sealed record NetworkVisualIdentity(
    string NetworkId,
    string DisplayName,
    string LayerName,
    string CadColor,
    Brush UiBrush,
    Brush UiMutedBrush,
    Brush UiBorderBrush,
    int DisplayOrder);

internal static class NetworkVisualIdentityCatalog
{
    // UI-only fallback palette. CSV selection must remain a pure in-memory operation:
    // accessing Database/Transaction from a modeless WPF callback can terminate AutoCAD.
    private static readonly IReadOnlyDictionary<string, MediaColor> Colors =
        new Dictionary<string, MediaColor>(StringComparer.Ordinal)
        {
            ["agua_potavel"] = MediaColor.FromRgb(39, 181, 245),
            ["aguas_pluviais"] = MediaColor.FromRgb(124, 91, 207),
            ["esgoto_sanitario"] = MediaColor.FromRgb(117, 200, 75),
            ["esgoto_industrial"] = MediaColor.FromRgb(172, 126, 72),
            ["rede_eletrica"] = MediaColor.FromRgb(255, 190, 40),
            ["telefone"] = MediaColor.FromRgb(231, 120, 55),
            ["fibra_optica"] = MediaColor.FromRgb(45, 211, 190),
            ["rede_incendio"] = MediaColor.FromRgb(255, 65, 78),
            ["gas"] = MediaColor.FromRgb(238, 214, 65),
            ["nao_identificada"] = MediaColor.FromRgb(129, 147, 162),
            ["outra"] = MediaColor.FromRgb(129, 147, 162),
        };

    internal static NetworkVisualIdentity Resolve(string? networkName)
    {
        var definition = NetworkCadCatalog.Resolve(networkName);
        var color = Colors.TryGetValue(definition.NetworkId, out var configured)
            ? configured
            : Colors["nao_identificada"];
        return new NetworkVisualIdentity(
            definition.NetworkId,
            definition.DisplayName,
            definition.TextLayer,
            definition.TextLayer,
            Brush(color, byte.MaxValue),
            Brush(color, 25),
            Brush(color, 80),
            definition.DisplayOrder);
    }

    private static Brush Brush(MediaColor color, byte alpha)
    {
        var brush = new SolidColorBrush(MediaColor.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
