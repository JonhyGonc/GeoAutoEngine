using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using GeoAutoEngine.Application;
using GeoAutoEngine.Application.VideoInspection;
using GeoAutoEngine.Domain;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace GeoAutoEngine.Plugin;

internal sealed class GeoAutoPaletteViewModel : INotifyPropertyChanged
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Brush AccentBrush = FrozenBrush("#27B5F5");
    private static readonly Brush SuccessBrush = FrozenBrush("#75C84B");
    private static readonly Brush WarningBrush = FrozenBrush("#FF8A18");
    private static readonly Brush ErrorBrush = FrozenBrush("#FF414E");
    private static readonly Brush NeutralBrush = FrozenBrush("#8193A2");
    private bool _isBusy;
    private string _processButtonText = "PROCESSAR LEVANTAMENTOS";
    private string _processActionSummary = "Nenhum levantamento selecionado";
    private string _statusMessage = "Adicione um ou mais arquivos CSV para começar.";
    private string _statusTitle = "Aguardando levantamentos";
    private string _statusDetail = "Adicione um ou mais arquivos CSV para começar.";
    private string _statusGlyph = "○";
    private Brush _statusAccent = NeutralBrush;
    private string _emptyDiagnosticText = "Adicione CSVs para visualizar a distribuição.";
    private string _detailedDiagnosticSummary = "Nenhum lote analisado.";
    private DrawingPlanProcessingMode _selectedProcessingMode = DrawingPlanProcessingMode.Complete;
    private bool _openPaletteOnStartup;
    private int _videoInspectionScaleDenominator;
    private int _plantScaleDenominator;
    private double _printedTextHeightMillimeters;
    private BlockScaleMode _blockScaleMode;
    private bool _showRevisionMarks;
    private bool _addStandardSheet;
    private bool _suspendPresentationPersistence;
    private ImportBatch? _batch;

    internal GeoAutoPaletteViewModel()
    {
        var uiSettings = PaletteSettingsStore.Load();
        _openPaletteOnStartup = uiSettings.OpenPaletteOnStartup;
        _videoInspectionScaleDenominator = uiSettings.VideoInspectionScaleDenominator;
        var presentation = PaletteSettingsStore.DrawingPresentation.Load();
        _plantScaleDenominator = presentation.ScaleDenominator;
        _printedTextHeightMillimeters = presentation.PrintedTextHeightMillimeters;
        _blockScaleMode = presentation.BlockScaleMode;
        _showRevisionMarks = presentation.ShowRevisionMarks;
        _addStandardSheet = presentation.AddStandardSheet;
        ResetDiagnostics();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SurveyFileViewModel> Files { get; } = [];
    public ObservableCollection<NetworkDistributionViewModel> Networks { get; } = [];
    public ObservableCollection<DiagnosticCardViewModel> DiagnosticCards { get; } = [];
    public ObservableCollection<DetailedDiagnosticViewModel> DetailedDiagnostics { get; } = [];
    public IReadOnlyList<ScaleOptionViewModel> PlantScaleOptions { get; } =
    [
        new(5, "1:5"),
        new(10, "1:10"),
        new(20, "1:20"),
        new(25, "1:25"),
        new(50, "1:50"),
        new(75, "1:75"),
        new(100, "1:100"),
        new(125, "1:125"),
        new(200, "1:200"),
        new(250, "1:250"),
        new(500, "1:500"),
        new(750, "1:750"),
        new(1000, "1:1000"),
    ];
    public IReadOnlyList<ScaleOptionViewModel> VideoInspectionScaleOptions { get; } =
    [
        new(125, "1:125"),
        new(200, "1:200"),
        new(250, "1:250"),
        new(500, "1:500"),
        new(750, "1:750"),
    ];
    public IReadOnlyList<PrintedHeightOptionViewModel> PrintedHeightOptions { get; } =
    [
        new(1.5, "1,5 mm"),
        new(2.0, "2,0 mm"),
        new(2.5, "2,5 mm"),
        new(3.0, "3,0 mm"),
    ];
    public IReadOnlyList<BlockScaleOptionViewModel> BlockScaleOptions { get; } =
    [
        new(BlockScaleMode.Automatic, "Automática"),
    ];
    public IReadOnlyList<ProcessingModeOptionViewModel> ProcessingModeOptions { get; } =
    [
        new(DrawingPlanProcessingMode.Complete, "Completo"),
        new(DrawingPlanProcessingMode.BlocksOnly, "Somente blocos"),
        new(DrawingPlanProcessingMode.TextsOnly, "Somente textos"),
    ];
    public int FileCount => Files.Count;
    public bool CanProcess => Files.Count > 0 && !_isBusy;
    public Visibility LoadedFilesVisibility => Files.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    public string ProcessButtonText { get => _processButtonText; private set => SetField(ref _processButtonText, value); }
    public string ProcessActionSummary { get => _processActionSummary; private set => SetField(ref _processActionSummary, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string StatusTitle { get => _statusTitle; private set => SetField(ref _statusTitle, value); }
    public string StatusDetail { get => _statusDetail; private set => SetField(ref _statusDetail, value); }
    public string StatusGlyph { get => _statusGlyph; private set => SetField(ref _statusGlyph, value); }
    public Brush StatusAccent { get => _statusAccent; private set => SetField(ref _statusAccent, value); }
    public string EmptyDiagnosticText { get => _emptyDiagnosticText; private set => SetField(ref _emptyDiagnosticText, value); }
    public string DetailedDiagnosticSummary { get => _detailedDiagnosticSummary; private set => SetField(ref _detailedDiagnosticSummary, value); }
    public Visibility DetailedDiagnosticEmptyVisibility => DetailedDiagnostics.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DetailedDiagnosticListVisibility => DetailedDiagnostics.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    public DrawingPlanProcessingMode SelectedProcessingMode
    {
        get => _selectedProcessingMode;
        set => SetField(ref _selectedProcessingMode, value);
    }
    public bool OpenPaletteOnStartup
    {
        get => _openPaletteOnStartup;
        set
        {
            if (SetField(ref _openPaletteOnStartup, value))
            {
                var settings = PaletteSettingsStore.Load();
                settings.OpenPaletteOnStartup = value;
                PaletteSettingsStore.Save(settings);
            }
        }
    }
    public int VideoInspectionScaleDenominator
    {
        get => _videoInspectionScaleDenominator;
        set
        {
            var normalized = InspectionCalloutScaleProfile.Resolve(value).ScaleDenominator;
            if (SetField(ref _videoInspectionScaleDenominator, normalized))
            {
                OnPropertyChanged(nameof(VideoInspectionScaleSummary));
                var settings = PaletteSettingsStore.Load();
                settings.VideoInspectionScaleDenominator = normalized;
                PaletteSettingsStore.Save(settings);
            }
        }
    }
    public string VideoInspectionScaleSummary
    {
        get
        {
            var scale = InspectionCalloutScaleProfile.Resolve(VideoInspectionScaleDenominator);
            return $"Texto: {scale.TextHeight.ToString("0.00", PtBr)} m  ·  " +
                $"Linha: {scale.BaseCalloutLength.ToString("0.00", PtBr)} m";
        }
    }
    public int PlantScaleDenominator
    {
        get => _plantScaleDenominator;
        set
        {
            if (SetField(ref _plantScaleDenominator, value))
            {
                OnPropertyChanged(nameof(CalculatedTextHeight));
                PersistDrawingPresentation();
            }
        }
    }
    public double PrintedTextHeightMillimeters
    {
        get => _printedTextHeightMillimeters;
        set
        {
            if (SetField(ref _printedTextHeightMillimeters, value))
            {
                OnPropertyChanged(nameof(CalculatedTextHeight));
                PersistDrawingPresentation();
            }
        }
    }
    public BlockScaleMode SelectedBlockScaleMode
    {
        get => _blockScaleMode;
        set
        {
            if (SetField(ref _blockScaleMode, value))
            {
                PersistDrawingPresentation();
            }
        }
    }
    public bool ShowRevisionMarks
    {
        get => _showRevisionMarks;
        set
        {
            if (SetField(ref _showRevisionMarks, value))
            {
                PersistDrawingPresentation();
            }
        }
    }
    public bool AddStandardSheet
    {
        get => _addStandardSheet;
        set
        {
            if (SetField(ref _addStandardSheet, value))
            {
                PersistDrawingPresentation();
            }
        }
    }
    public string CalculatedTextHeight => CurrentDrawingPresentation().TextHeightMeters.ToString("0.00 'm'", PtBr);

    internal DrawingPlanBuildOptions CreateDrawingPlanBuildOptions()
    {
        var settings = CurrentDrawingPresentation();
        return new DrawingPlanBuildOptions(
            textHeight: settings.TextHeightMeters,
            showRevisionMarks: settings.ShowRevisionMarks);
    }

    internal void RestoreDrawingPresentationDefaults()
    {
        var defaults = DrawingPresentationSettings.Defaults();
        _suspendPresentationPersistence = true;
        try
        {
            PlantScaleDenominator = defaults.ScaleDenominator;
            PrintedTextHeightMillimeters = defaults.PrintedTextHeightMillimeters;
            SelectedBlockScaleMode = defaults.BlockScaleMode;
            ShowRevisionMarks = defaults.ShowRevisionMarks;
            AddStandardSheet = defaults.AddStandardSheet;
        }
        finally
        {
            _suspendPresentationPersistence = false;
        }

        PersistDrawingPresentation();
    }

    internal void SetSources(IReadOnlyList<string> sourceFiles)
    {
        var existing = Files.ToDictionary(file => file.FullPath, StringComparer.OrdinalIgnoreCase);
        Files.Clear();
        foreach (var path in sourceFiles)
        {
            Files.Add(existing.TryGetValue(path, out var file) ? file : new SurveyFileViewModel(path));
        }

        _batch = null;
        UpdateFileDependentProperties();
        if (Files.Count == 0)
        {
            ResetDiagnostics();
        }
    }

    internal void Begin(string message)
    {
        SetBusy(true);
        SetStatus("Processando...", message, "◌", AccentBrush);
    }

    internal void ApplyBatch(ImportBatch batch)
    {
        _batch = batch;
        foreach (var file in Files)
        {
            var source = batch.SourceFiles.FirstOrDefault(candidate =>
                string.Equals(candidate.CanonicalPath, file.FullPath, StringComparison.OrdinalIgnoreCase));
            var summary = source is null
                ? null
                : batch.Summary.Sources.First(candidate => candidate.SourceFileId == source.Id);
            var sourceNetworks = source is null
                ? []
                : batch.ImportedRecords
                    .Where(record => record.Origin.SourceFileId == source.Id && record.Interpretation.Recognized)
                    .Select(record => record.Interpretation.Network)
                    .Where(network => !string.IsNullOrWhiteSpace(network))
                    .Select(network => network!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(network => NetworkCadCatalog.Resolve(network).DisplayOrder)
                    .ToArray();
            file.Apply(summary, sourceNetworks);
        }

        BuildDiagnosticCards(batch);
        BuildDetailedDiagnostics(batch);
        Networks.Clear();
        var classifiedCount = batch.Summary.RecordsByNetwork.Values.Sum();
        foreach (var item in batch.Summary.RecordsByNetwork
                     .Select(pair => (Pair: pair, Identity: NetworkVisualIdentityCatalog.Resolve(pair.Key)))
                     .Where(item => item.Pair.Value > 0)
                     .OrderBy(item => item.Identity.DisplayOrder))
        {
            Networks.Add(new NetworkDistributionViewModel(item.Identity, item.Pair.Value, classifiedCount));
        }

        EmptyDiagnosticText = Networks.Count == 0 ? "Nenhuma rede reconhecida neste lote." : string.Empty;
        UpdateFileDependentProperties();
        if (batch.Summary.RejectedSourceCount > 0)
        {
            ShowError($"{batch.Summary.RejectedSourceCount} arquivo(s) precisam de correção.");
        }
        else
        {
            SetStatus(
                "Pronto para processar",
                $"{Format(batch.Summary.ProcessableRecordCount)} registro(s) prontos para gerar o desenho.",
                "✓",
                SuccessBrush);
        }

        SetBusy(false);
    }

    internal void Complete(
        DrawingPlanApplicationResult result,
        DrawingPlanProcessingMode processingMode)
    {
        SetBusy(false);
        AppendDetailedDiagnostics(result.Diagnostics, _batch);
        var title = processingMode switch
        {
            DrawingPlanProcessingMode.BlocksOnly => "Blocos processados",
            DrawingPlanProcessingMode.TextsOnly => "Textos processados",
            _ => "Processamento concluído",
        };
        var detail = processingMode switch
        {
            DrawingPlanProcessingMode.BlocksOnly => "As estruturas foram atualizadas no desenho. Os CSVs continuam carregados.",
            DrawingPlanProcessingMode.TextsOnly => "As anotações foram atualizadas no desenho. Os CSVs continuam carregados.",
            _ => "O levantamento foi atualizado no desenho. Os CSVs continuam carregados.",
        };

        SetStatus(title, detail, "✓", SuccessBrush);
    }

    internal void CompletePreparation(DrawingPlan plan)
    {
        var preparedItems = plan.Summary.TextCount + plan.Summary.BlockCount +
            plan.Summary.RainStructureCount + plan.Summary.BoxStructureCount + plan.Summary.CircleAlertCount;
        SetBusy(false);
        SetStatus("Processamento concluído", $"{Format(preparedItems)} itens preparados.", "✓", SuccessBrush);
    }

    internal void Fail(string message)
    {
        SetBusy(false);
        ShowError(message);
    }

    private void BuildDiagnosticCards(ImportBatch batch)
    {
        var total = batch.Summary.TotalRecordCount;
        var processable = batch.Summary.ProcessableRecordCount;
        var warnings = batch.Diagnostics.Count(diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Warning &&
            !string.Equals(diagnostic.Code, "CSV_DUPLICATE_CANDIDATE", StringComparison.Ordinal));
        var errors = batch.Summary.RejectedRecordCount;
        DiagnosticCards.Clear();
        DiagnosticCards.Add(new DiagnosticCardViewModel("▤", Format(total), "Total de registros", string.Empty, AccentBrush));
        DiagnosticCards.Add(new DiagnosticCardViewModel("✓", Format(processable), "Processáveis", Percentage(processable, total), SuccessBrush));
        DiagnosticCards.Add(new DiagnosticCardViewModel("△", Format(warnings), "Avisos", Percentage(warnings, total), WarningBrush));
        DiagnosticCards.Add(new DiagnosticCardViewModel("×", Format(errors), "Erros", Percentage(errors, total), ErrorBrush));
    }

    private void ResetDiagnostics()
    {
        _batch = null;
        Networks.Clear();
        DetailedDiagnostics.Clear();
        DiagnosticCards.Clear();
        DiagnosticCards.Add(new DiagnosticCardViewModel("▤", "0", "Total de registros", string.Empty, AccentBrush));
        DiagnosticCards.Add(new DiagnosticCardViewModel("✓", "0", "Processáveis", "0,00%", SuccessBrush));
        DiagnosticCards.Add(new DiagnosticCardViewModel("△", "0", "Avisos", "0,00%", WarningBrush));
        DiagnosticCards.Add(new DiagnosticCardViewModel("×", "0", "Erros", "0,00%", ErrorBrush));
        EmptyDiagnosticText = "Adicione CSVs para visualizar a distribuição.";
        DetailedDiagnosticSummary = "Nenhum lote analisado.";
        NotifyDetailedDiagnosticVisibility();
        SetStatus("Aguardando levantamentos", "Adicione um ou mais arquivos CSV para começar.", "○", NeutralBrush);
        UpdateFileDependentProperties();
    }

    private void SetBusy(bool value)
    {
        _isBusy = value;
        ProcessButtonText = value ? "PROCESSANDO..." : "PROCESSAR LEVANTAMENTOS";
        UpdateFileDependentProperties();
    }

    private void ShowError(string message)
    {
        SetStatus("Não foi possível concluir", message, "×", ErrorBrush);
    }

    private void BuildDetailedDiagnostics(ImportBatch batch)
    {
        DetailedDiagnostics.Clear();
        AppendDetailedDiagnostics(batch.Diagnostics, batch);
    }

    private void AppendDetailedDiagnostics(IEnumerable<Diagnostic> diagnostics, ImportBatch? batch)
    {
        foreach (var diagnostic in diagnostics.Where(item =>
                     !string.Equals(item.Code, "CSV_DUPLICATE_CANDIDATE", StringComparison.Ordinal)))
        {
            var accent = diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => ErrorBrush,
                DiagnosticSeverity.Warning => WarningBrush,
                _ => AccentBrush,
            };
            var glyph = diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => "×",
                DiagnosticSeverity.Warning => "!",
                _ => "i",
            };
            var originText = diagnostic.Origins.Count > 0
                ? string.Join(" · ", diagnostic.Origins.Take(3).Select(origin =>
                    $"{origin.SourceFileName}, linha {origin.SourceLineNumber}"))
                : ResolveSourceName(batch, diagnostic.SourceFileId);
            if (diagnostic.Origins.Count > 3)
            {
                originText += $" · +{diagnostic.Origins.Count - 3} ocorrência(s)";
            }

            DetailedDiagnostics.Add(new DetailedDiagnosticViewModel(
                glyph,
                diagnostic.Severity switch
                {
                    DiagnosticSeverity.Error => "Erro",
                    DiagnosticSeverity.Warning => "Aviso",
                    _ => "Informação",
                },
                diagnostic.Code,
                diagnostic.Message,
                originText,
                accent));
        }

        DetailedDiagnosticSummary = DetailedDiagnostics.Count == 0
            ? "Nenhum problema que exija atenção foi encontrado."
            : $"{Format(DetailedDiagnostics.Count)} ocorrência(s) registrada(s).";
        NotifyDetailedDiagnosticVisibility();
    }

    private static string ResolveSourceName(ImportBatch? batch, SourceFileId? sourceFileId)
    {
        if (batch is null || sourceFileId is null)
        {
            return "Diagnóstico geral";
        }

        return batch.SourceFiles.FirstOrDefault(source => source.Id == sourceFileId)?.DisplayName
            ?? "Diagnóstico geral";
    }

    private void NotifyDetailedDiagnosticVisibility()
    {
        OnPropertyChanged(nameof(DetailedDiagnosticEmptyVisibility));
        OnPropertyChanged(nameof(DetailedDiagnosticListVisibility));
    }

    private void SetStatus(string title, string detail, string glyph, Brush accent)
    {
        StatusTitle = title;
        StatusDetail = detail;
        StatusGlyph = glyph;
        StatusAccent = accent;
        StatusMessage = $"{glyph} {title} · {detail}";
    }

    internal DrawingPresentationSettings CurrentDrawingPresentation() => new(
        PlantScaleDenominator,
        PrintedTextHeightMillimeters,
        SelectedBlockScaleMode,
        ShowRevisionMarks,
        AddStandardSheet);

    private void PersistDrawingPresentation()
    {
        if (_suspendPresentationPersistence)
        {
            return;
        }

        try
        {
            PaletteSettingsStore.DrawingPresentation.Save(CurrentDrawingPresentation());
        }
        catch (Exception exception)
        {
            PluginLog.Error("Gravação das configurações de apresentação", exception);
        }
    }

    private void UpdateFileDependentProperties()
    {
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(CanProcess));
        OnPropertyChanged(nameof(LoadedFilesVisibility));
        var total = _batch?.Summary.TotalRecordCount ?? 0;
        var surveyLabel = Files.Count == 1 ? "1 levantamento selecionado" : $"{Files.Count} levantamentos selecionados";
        ProcessActionSummary = Files.Count == 0
            ? "Nenhum levantamento selecionado"
            : $"{surveyLabel}   |   {Format(total)} registros";
    }

    private static string Format(int number) => number.ToString("N0", PtBr);

    private static string Percentage(int count, int total) =>
        (total == 0 ? 0 : (double)count / total * 100).ToString("0.00", PtBr) + "%";

    private static Brush FrozenBrush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed record ProcessingModeOptionViewModel(DrawingPlanProcessingMode Value, string Label);
