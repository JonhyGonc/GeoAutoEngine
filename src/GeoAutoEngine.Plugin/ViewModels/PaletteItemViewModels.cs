using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using GeoAutoEngine.Application;
using GeoAutoEngine.Domain;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace GeoAutoEngine.Plugin;

internal sealed class SurveyFileViewModel : INotifyPropertyChanged
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Brush ValidBrush = FrozenBrush("#75C84B");
    private static readonly Brush ValidCheckBrush = FrozenBrush("#102414");
    private static readonly Brush WaitingBrush = FrozenBrush("#607482");
    private static readonly Brush ErrorBrush = FrozenBrush("#D95050");
    private static readonly Brush NeutralTagBrush = FrozenBrush("#192B37");
    private static readonly Brush NeutralTagBorderBrush = FrozenBrush("#425663");
    private static readonly Brush NeutralTagTextBrush = FrozenBrush("#B7C5D0");
    private string _pointText = "Aguardando validação";
    private Brush _statusBackground = WaitingBrush;
    private Brush _statusForeground = Brushes.White;
    private string _statusText = "Aguardando";
    private string _networkName = string.Empty;
    private string _networkTooltip = string.Empty;
    private Brush _networkForeground = NeutralTagTextBrush;
    private Brush _networkBackground = NeutralTagBrush;
    private Brush _networkBorder = NeutralTagBorderBrush;
    private Visibility _networkVisibility = Visibility.Collapsed;

    internal SurveyFileViewModel(string fullPath)
    {
        FullPath = fullPath;
        FileName = Path.GetFileName(fullPath);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FullPath { get; }
    public string FileName { get; }
    public string PointText { get => _pointText; private set => Set(ref _pointText, value, nameof(PointText)); }
    public Brush StatusBackground { get => _statusBackground; private set => Set(ref _statusBackground, value, nameof(StatusBackground)); }
    public Brush StatusForeground { get => _statusForeground; private set => Set(ref _statusForeground, value, nameof(StatusForeground)); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value, nameof(StatusText)); }
    public string NetworkName { get => _networkName; private set => Set(ref _networkName, value, nameof(NetworkName)); }
    public string NetworkTooltip { get => _networkTooltip; private set => Set(ref _networkTooltip, value, nameof(NetworkTooltip)); }
    public Brush NetworkForeground { get => _networkForeground; private set => Set(ref _networkForeground, value, nameof(NetworkForeground)); }
    public Brush NetworkBackground { get => _networkBackground; private set => Set(ref _networkBackground, value, nameof(NetworkBackground)); }
    public Brush NetworkBorder { get => _networkBorder; private set => Set(ref _networkBorder, value, nameof(NetworkBorder)); }
    public Visibility NetworkVisibility { get => _networkVisibility; private set => Set(ref _networkVisibility, value, nameof(NetworkVisibility)); }

    internal void Apply(SourceImportSummary? summary, IReadOnlyList<string> sourceNetworks)
    {
        if (summary is null || summary.Status == SourceImportStatus.Rejected)
        {
            PointText = "Não foi possível validar";
            StatusBackground = ErrorBrush;
            StatusForeground = Brushes.White;
            StatusText = "Erro";
            NetworkVisibility = Visibility.Collapsed;
            return;
        }

        PointText = $"{summary.TotalRecordCount.ToString("N0", PtBr)} pontos";
        StatusBackground = ValidBrush;
        StatusForeground = ValidCheckBrush;
        StatusText = "Processável";
        ApplyNetwork(sourceNetworks);
    }

    private void ApplyNetwork(IReadOnlyList<string> sourceNetworks)
    {
        if (sourceNetworks.Count == 0)
        {
            NetworkVisibility = Visibility.Collapsed;
            return;
        }

        NetworkVisibility = Visibility.Visible;
        if (sourceNetworks.Count > 1)
        {
            NetworkName = "Múltiplas redes";
            NetworkTooltip = string.Join(", ", sourceNetworks);
            NetworkForeground = NeutralTagTextBrush;
            NetworkBackground = NeutralTagBrush;
            NetworkBorder = NeutralTagBorderBrush;
            return;
        }

        var identity = NetworkVisualIdentityCatalog.Resolve(sourceNetworks[0]);
        NetworkName = identity.DisplayName;
        NetworkTooltip = $"{identity.LayerName} · {identity.CadColor}";
        NetworkForeground = identity.UiBrush;
        NetworkBackground = identity.UiMutedBrush;
        NetworkBorder = identity.UiBorderBrush;
    }

    private void Set<T>(ref T field, T value, string propertyName)
    {
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static Brush FrozenBrush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }
}

internal sealed class NetworkDistributionViewModel
{
    internal NetworkDistributionViewModel(NetworkVisualIdentity identity, int count, int classifiedCount)
    {
        Identity = identity;
        Count = count;
        CountText = count.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"));
        Percentage = classifiedCount == 0 ? 0 : (double)count / classifiedCount * 100;
        PercentageText = Percentage.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR")) + "%";
        IconGlyph = identity.NetworkId switch
        {
            "agua_potavel" => "●",
            "aguas_pluviais" => "☂",
            "esgoto_sanitario" or "esgoto_industrial" => "◉",
            "rede_eletrica" => "ϟ",
            "fibra_optica" or "telefone" => "⌁",
            "gas" => "◒",
            "rede_incendio" => "△",
            _ => "◇",
        };
    }

    public NetworkVisualIdentity Identity { get; }
    public string Name => Identity.DisplayName.ToUpperInvariant();
    public string LayerTooltip => $"{Identity.LayerName} · {Identity.CadColor}";
    public Brush Color => Identity.UiBrush;
    public Brush MutedColor => Identity.UiMutedBrush;
    public string IconGlyph { get; }
    public int Count { get; }
    public string CountText { get; }
    public double Percentage { get; }
    public string PercentageText { get; }
}

internal sealed record DiagnosticCardViewModel(
    string Glyph,
    string ValueText,
    string Label,
    string PercentageText,
    Brush Accent);

internal sealed record DetailedDiagnosticViewModel(
    string Glyph,
    string SeverityLabel,
    string Code,
    string Message,
    string Source,
    Brush Accent);

internal sealed record ScaleOptionViewModel(int Value, string Label);

internal sealed record PrintedHeightOptionViewModel(double Value, string Label);

internal sealed record BlockScaleOptionViewModel(BlockScaleMode Value, string Label);
