using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GeoAutoEngine.Application.VideoInspection;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace GeoAutoEngine.Plugin.VideoInspection;

internal sealed class VideoInspectionViewModel : INotifyPropertyChanged
{
    private VideoInspectionSession? _session;
    private VideoInspectionSectionItemViewModel? _selectedSection;
    private string _notification = string.Empty;

    public VideoInspectionViewModel()
    {
        ImportCommand = new VideoInspectionCommand(() => ImportRequested?.Invoke());
        AssociateCommand = new VideoInspectionCommand(
            () => AssociateRequested?.Invoke(SelectedSection!.SectionId),
            () => SelectedSection is not null);
        CommitCommand = new VideoInspectionCommand(
            () => CommitRequested?.Invoke(SelectedSection!.SectionId),
            () => SelectedSection?.HasAssociation == true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? ImportRequested;
    public event Action<Guid>? AssociateRequested;
    public event Action<Guid>? CommitRequested;

    public ObservableCollection<VideoInspectionSectionItemViewModel> Sections { get; } = [];
    public ICommand ImportCommand { get; }
    public ICommand AssociateCommand { get; }
    public ICommand CommitCommand { get; }
    public Visibility EmptyVisibility => _session is null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility InspectionVisibility => _session is null ? Visibility.Collapsed : Visibility.Visible;
    public string ReportCode => ValueOrFallback(_session?.ReportCode, _session?.ReportName, "Inspeção");
    public string ReportName => _session?.ReportName ?? string.Empty;
    public string Client => _session?.Client ?? string.Empty;
    public string Area => _session?.Area ?? string.Empty;
    public string ReportSummary => _session is null
        ? string.Empty
        : $"{_session.Sections.Count} trecho(s)  •  {_session.OccurrenceCount} ocorrência(s)";
    public int SectionCount => _session?.Sections.Count ?? 0;
    public int OccurrenceCount => _session?.OccurrenceCount ?? 0;
    public int AssociatedCount => Sections.Count(item =>
        item.Status is InspectionSectionStatus.Associated or InspectionSectionStatus.Generated);
    public int PendingCount => Sections.Count(item => item.Status == InspectionSectionStatus.Pending);
    public int AttentionCount => Sections.Count(item => item.Status == InspectionSectionStatus.Attention);
    public int ErrorCount => Sections.Count(item =>
        item.Status is InspectionSectionStatus.Error or InspectionSectionStatus.AssociationLost);
    public Visibility AssociatedCountVisibility => CountVisibility(AssociatedCount);
    public Visibility PendingCountVisibility => CountVisibility(PendingCount);
    public Visibility AttentionCountVisibility => CountVisibility(AttentionCount);
    public Visibility ErrorCountVisibility => CountVisibility(ErrorCount);

    public string Notification
    {
        get => _notification;
        private set => Set(ref _notification, value);
    }

    public VideoInspectionSectionItemViewModel? SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (!Set(ref _selectedSection, value))
            {
                return;
            }

            Raise(nameof(SelectedSectionVisibility));
            Raise(nameof(PositioningSummary));
            RaiseCommandStates();
        }
    }

    public Visibility SelectedSectionVisibility => SelectedSection is null
        ? Visibility.Collapsed
        : Visibility.Visible;

    public string PositioningSummary
    {
        get
        {
            var section = _session?.FindSection(SelectedSection?.SectionId ?? Guid.Empty);
            if (section is null || section.PositionedEvents.Count == 0)
            {
                return string.Empty;
            }

            var positioned = section.PositionedEvents.Count(item => item.CanCommit && item.Locations.Count > 0);
            var attention = section.PositionedEvents.Count(item => !item.CanCommit);
            return $"✓ {positioned} posicionada(s)    ⚠ {attention} requer(em) atenção";
        }
    }

    public void Load(VideoInspectionSession session, string notification)
    {
        VideoInspectionPerformanceProbe.Count("VideoInspectionViewModel.Load");
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        Notification = notification;
        RefreshCore(null);
        var notifications = Stopwatch.StartNew();
        Raise(nameof(EmptyVisibility));
        Raise(nameof(InspectionVisibility));
        Raise(nameof(ReportCode));
        Raise(nameof(ReportName));
        Raise(nameof(Client));
        Raise(nameof(Area));
        Raise(nameof(ReportSummary));
        Raise(nameof(SectionCount));
        Raise(nameof(OccurrenceCount));
        notifications.Stop();
        VideoInspectionPerformanceProbe.Record(
            "PropertyChanged / summary binding",
            notifications.Elapsed.TotalMilliseconds);
    }

    public void Refresh()
    {
        if (_session is null)
        {
            return;
        }

        var sessionSections = _session.Sections;
        var sameSections = Sections.Count == sessionSections.Count;
        for (var index = 0; sameSections && index < Sections.Count; index++)
        {
            sameSections = Sections[index].SectionId == sessionSections[index].SectionId;
        }

        if (!sameSections)
        {
            RefreshCore(SelectedSection?.SectionId);
            return;
        }

        for (var index = 0; index < Sections.Count; index++)
        {
            Sections[index].Refresh(sessionSections[index]);
        }

        RaiseStatusCounts();
        Raise(nameof(PositioningSummary));
        RaiseCommandStates();
    }

    private void RefreshCore(Guid? selectedSectionId)
    {
        // Clearing the bound collection temporarily clears ListBox.SelectedItem,
        // so restore the requested section after rebuilding the items.
        var collectionUpdate = Stopwatch.StartNew();
        Sections.Clear();
        collectionUpdate.Stop();
        VideoInspectionPerformanceProbe.Record(
            "ObservableCollection notifications",
            collectionUpdate.Elapsed.TotalMilliseconds);

        foreach (var section in _session!.Sections)
        {
            var viewModelCreation = Stopwatch.StartNew();
            var item = new VideoInspectionSectionItemViewModel(section);
            viewModelCreation.Stop();
            VideoInspectionPerformanceProbe.Record(
                "Section/occurrence ViewModels",
                viewModelCreation.Elapsed.TotalMilliseconds);

            collectionUpdate.Restart();
            Sections.Add(item);
            collectionUpdate.Stop();
            VideoInspectionPerformanceProbe.Record(
                "ObservableCollection notifications",
                collectionUpdate.Elapsed.TotalMilliseconds);
        }

        var notifications = Stopwatch.StartNew();
        SelectedSection = selectedSectionId.HasValue
            ? Sections.FirstOrDefault(item => item.SectionId == selectedSectionId.Value)
            : Sections.FirstOrDefault();

        RaiseStatusCounts();
        Raise(nameof(PositioningSummary));
        notifications.Stop();
        VideoInspectionPerformanceProbe.Record(
            "PropertyChanged / summary binding",
            notifications.Elapsed.TotalMilliseconds);
    }

    private void RaiseStatusCounts()
    {
        Raise(nameof(AssociatedCount));
        Raise(nameof(PendingCount));
        Raise(nameof(AttentionCount));
        Raise(nameof(ErrorCount));
        Raise(nameof(AssociatedCountVisibility));
        Raise(nameof(PendingCountVisibility));
        Raise(nameof(AttentionCountVisibility));
        Raise(nameof(ErrorCountVisibility));
    }

    private void RaiseCommandStates()
    {
        ((VideoInspectionCommand)AssociateCommand).RaiseCanExecuteChanged();
        ((VideoInspectionCommand)CommitCommand).RaiseCanExecuteChanged();
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(propertyName);
        return true;
    }

    private void Raise([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string ValueOrFallback(string? first, string? second, string fallback) =>
        !string.IsNullOrWhiteSpace(first) ? first! : !string.IsNullOrWhiteSpace(second) ? second! : fallback;

    private static Visibility CountVisibility(int count) =>
        count > 0 ? Visibility.Visible : Visibility.Collapsed;
}

internal sealed class VideoInspectionSectionItemViewModel : INotifyPropertyChanged
{
    private static readonly Brush SuccessBrush = FrozenBrush("#22C55E");
    private static readonly Brush PendingBrush = FrozenBrush("#F59E0B");
    private static readonly Brush AttentionBrush = FrozenBrush("#F97316");
    private static readonly Brush ErrorBrush = FrozenBrush("#EF4444");
    private static readonly Brush SuccessBackground = FrozenBrush("#1A22C55E");
    private static readonly Brush PendingBackground = FrozenBrush("#1AF59E0B");
    private static readonly Brush AttentionBackground = FrozenBrush("#1AF97316");
    private static readonly Brush ErrorBackground = FrozenBrush("#1AEF4444");
    private static readonly Brush SuccessBorder = FrozenBrush("#3322C55E");
    private static readonly Brush PendingBorder = FrozenBrush("#33F59E0B");
    private static readonly Brush AttentionBorder = FrozenBrush("#33F97316");
    private static readonly Brush ErrorBorder = FrozenBrush("#33EF4444");

    public VideoInspectionSectionItemViewModel(InspectionSectionSession section)
    {
        SectionId = section.SectionId;
        Refresh(section);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid SectionId { get; }
    public string Name { get; private set; } = string.Empty;
    public string Route { get; private set; } = string.Empty;
    public string Diameter { get; private set; } = string.Empty;
    public string Reference { get; private set; } = string.Empty;
    public string InvestigatedMeters { get; private set; } = string.Empty;
    public string OccurrenceSummary { get; private set; } = string.Empty;
    public string OccurrenceAttentionSummary { get; private set; } = string.Empty;
    public bool HasAssociation { get; private set; }
    public string Direction { get; private set; } = string.Empty;
    public string CadLength { get; private set; } = string.Empty;
    public string PositionedOccurrences { get; private set; } = string.Empty;
    public string AttentionOccurrences { get; private set; } = string.Empty;
    public string StatusText { get; private set; } = string.Empty;
    public string StatusMessage { get; private set; } = string.Empty;
    public InspectionSectionStatus Status { get; private set; }
    public string StatusLabel { get; private set; } = string.Empty;
    public Brush StatusBrush { get; private set; } = PendingBrush;
    public Brush StatusBackground { get; private set; } = PendingBackground;
    public Brush StatusBorder { get; private set; } = PendingBorder;
    public Visibility AlertVisibility => string.IsNullOrWhiteSpace(AlertMessage)
        ? Visibility.Collapsed
        : Visibility.Visible;
    public string AlertMessage { get; private set; } = string.Empty;
    public int PositionedCount { get; private set; }
    public int AttentionCount { get; private set; }
    public IReadOnlyList<VideoInspectionOccurrenceItemViewModel> Occurrences { get; private set; } = [];
    public Visibility OccurrencesEmptyVisibility => Occurrences.Count == 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public void Refresh(InspectionSectionSession section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (section.SectionId != SectionId)
        {
            throw new InvalidOperationException("Não é possível atualizar o item com outro trecho.");
        }

        Name = VideoInspectionViewModelValue(section.Name, section.Description, section.Reference, "Trecho");
        Route = !string.IsNullOrWhiteSpace(section.StartPoint) || !string.IsNullOrWhiteSpace(section.EndPoint)
            ? $"{section.StartPoint ?? "?"} → {section.EndPoint ?? "?"}"
            : section.LocationName;
        Diameter = section.Diameter.HasValue
            ? $"DN {section.Diameter.Value.ToString("0.##", CultureInfo.CurrentCulture)}"
            : string.Empty;
        Reference = section.Reference ?? string.Empty;
        InvestigatedMeters = section.InvestigatedMeters.HasValue
            ? $"{section.InvestigatedMeters.Value.ToString("0.##", CultureInfo.CurrentCulture)} m"
            : "Não informada";
        OccurrenceSummary = section.Occurrences.Count == 1
            ? "1 ocorrência"
            : $"{section.Occurrences.Count} ocorrências";
        HasAssociation = section.Association is not null;
        Direction = section.Association?.Direction.ToString() ?? "Não definida";
        CadLength = section.Association is null
            ? "Não associado"
            : $"{section.Association.LastKnownGeometryLength.ToString("0.00", CultureInfo.CurrentCulture)} m";
        PositionedCount = section.PositionedEvents.Count(item => item.CanCommit && item.Locations.Count > 0);
        AttentionCount = section.PositionedEvents.Count(item => !item.CanCommit);
        OccurrenceAttentionSummary = AttentionCount > 0
            ? $"{OccurrenceSummary} · {AttentionCount} atenção"
            : OccurrenceSummary;
        PositionedOccurrences = PositionedCount.ToString(CultureInfo.CurrentCulture);
        AttentionOccurrences = AttentionCount.ToString(CultureInfo.CurrentCulture);
        Status = section.Status;
        StatusLabel = section.Status switch
        {
            InspectionSectionStatus.Pending => "Pendente",
            InspectionSectionStatus.Associated => "Associado",
            InspectionSectionStatus.Generated => "Gerado",
            InspectionSectionStatus.Attention => "Atenção",
            InspectionSectionStatus.AssociationLost => "Erro",
            _ => "Erro",
        };
        StatusText = StatusLabel;
        (StatusBrush, StatusBackground, StatusBorder) = section.Status switch
        {
            InspectionSectionStatus.Associated or InspectionSectionStatus.Generated =>
                (SuccessBrush, SuccessBackground, SuccessBorder),
            InspectionSectionStatus.Pending => (PendingBrush, PendingBackground, PendingBorder),
            InspectionSectionStatus.Attention => (AttentionBrush, AttentionBackground, AttentionBorder),
            _ => (ErrorBrush, ErrorBackground, ErrorBorder),
        };
        AlertMessage = section.Status switch
        {
            InspectionSectionStatus.Pending => "Aguardando associação de geometria.",
            InspectionSectionStatus.Attention =>
                $"{AttentionCount} ocorrência(s) requer(em) atenção antes de gerar no desenho.",
            InspectionSectionStatus.AssociationLost =>
                "A associação da geometria foi perdida. Selecione novamente o trecho no DWG.",
            InspectionSectionStatus.Error => string.IsNullOrWhiteSpace(section.StatusMessage)
                ? "Não foi possível processar este trecho."
                : section.StatusMessage,
            _ => string.Empty,
        };
        StatusMessage = section.StatusMessage ?? string.Empty;
        Occurrences = Array.AsReadOnly(section.Occurrences
            .Select(occurrence => new VideoInspectionOccurrenceItemViewModel(occurrence))
            .ToArray());
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private static string VideoInspectionViewModelValue(
        string? first,
        string? second,
        string? third,
        string fallback) => !string.IsNullOrWhiteSpace(first)
            ? first!
            : !string.IsNullOrWhiteSpace(second) ? second!
            : !string.IsNullOrWhiteSpace(third) ? third!
            : fallback;

    private static Brush FrozenBrush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }
}

internal sealed class VideoInspectionOccurrenceItemViewModel
{
    public VideoInspectionOccurrenceItemViewModel(InspectionOccurrenceSession occurrence)
    {
        Position = occurrence.LocationMode switch
        {
            InspectionLocationMode.Point when occurrence.Meterage.HasValue => Meter(occurrence.Meterage.Value),
            InspectionLocationMode.MultiPoint => string.Join(", ", occurrence.MeteragePoints.Select(Meter)),
            InspectionLocationMode.Range when occurrence.Meterage.HasValue && occurrence.EndMeterage.HasValue =>
                $"{Meter(occurrence.Meterage.Value)} → {Meter(occurrence.EndMeterage.Value)}",
            InspectionLocationMode.From when occurrence.Meterage.HasValue => $"A partir de {Meter(occurrence.Meterage.Value)}",
            InspectionLocationMode.EntireSection => "Todo trecho",
            InspectionLocationMode.NotApplicable => "Sem posição CAD",
            _ => "Posição incompleta",
        };
        Description = string.IsNullOrWhiteSpace(occurrence.EffectiveDescription)
            ? "Ocorrência sem descrição"
            : occurrence.EffectiveDescription;
        Code = occurrence.WireLocationMode;
        RequiresAttention = occurrence.IsSevere;
    }

    public string Position { get; }
    public string Description { get; }
    public string Code { get; }
    public bool RequiresAttention { get; }

    private static string Meter(decimal value) =>
        $"{value.ToString("0.##", CultureInfo.CurrentCulture)} m";
}

internal sealed class VideoInspectionCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
