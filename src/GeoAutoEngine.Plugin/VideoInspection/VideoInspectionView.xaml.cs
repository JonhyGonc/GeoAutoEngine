using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Plugin.VideoInspection;

public partial class VideoInspectionView : UserControl
{
    private readonly VideoInspectionViewModel _viewModel;
    private bool _removeConfirmationPending;
    private Button? _exportCorrectionsButton;
    private TextBlock? _correctionStatusText;
    private VideoInspectionSectionItemViewModel? _observedSection;

    internal VideoInspectionView(VideoInspectionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        InstallCorrectionControls();
    }

    private void OpenSelectedSection(
        object sender,
        System.Windows.Input.MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(SectionsList, source) is not ListBoxItem selectedItem)
        {
            return;
        }

        var total = Stopwatch.StartNew();
        ListState.Visibility = Visibility.Collapsed;
        DetailState.Visibility = Visibility.Visible;
        ObserveSelectedSection();
        UpdateCorrectionControls();
        var synchronousMilliseconds = total.Elapsed.TotalMilliseconds;
        if (selectedItem.DataContext is VideoInspectionSectionItemViewModel section)
        {
            VideoInspectionPerformanceProbe.CompleteSectionSwitch(
                section.SectionId,
                section.Occurrences.Count,
                total,
                synchronousMilliseconds,
                Dispatcher);
        }
    }

    private void BackToSections(object sender, RoutedEventArgs eventArgs)
    {
        DetailState.Visibility = Visibility.Collapsed;
        ListState.Visibility = Visibility.Visible;
    }

    private void RemoveJsonClick(object sender, RoutedEventArgs eventArgs)
    {
        if (!_removeConfirmationPending)
        {
            _removeConfirmationPending = true;
            RemoveJsonText.Text = "Confirmar remoção";
            RemoveJsonButton.Padding = new Thickness(10, 7, 10, 7);
            RemoveJsonButton.FontWeight = FontWeights.SemiBold;
            RemoveJsonButton.Foreground = new SolidColorBrush(
                Color.FromRgb(239, 68, 68));
            RemoveJsonButton.Background = new SolidColorBrush(
                Color.FromArgb(31, 239, 68, 68));
            RemoveJsonButton.BorderBrush = new SolidColorBrush(
                Color.FromArgb(102, 239, 68, 68));
            return;
        }

        if (!VideoInspectionRuntime.ConfirmDiscardPendingCorrections("remover a inspeção carregada"))
        {
            ResetRemoveJsonButton();
            return;
        }

        ResetRemoveJsonButton();
        DetailState.Visibility = Visibility.Collapsed;
        ListState.Visibility = Visibility.Visible;
        StopObservingSection();
        _viewModel.ClearLoadedInspection();
        UpdateCorrectionControls();
    }

    private void CancelRemoveJson(
        object sender,
        System.Windows.Input.KeyboardFocusChangedEventArgs eventArgs) => ResetRemoveJsonButton();

    private void ResetRemoveJsonButton()
    {
        _removeConfirmationPending = false;
        RemoveJsonText.Text = ".json";
        RemoveJsonButton.ClearValue(Control.PaddingProperty);
        RemoveJsonButton.ClearValue(Control.FontWeightProperty);
        RemoveJsonButton.ClearValue(Control.ForegroundProperty);
        RemoveJsonButton.ClearValue(Control.BackgroundProperty);
        RemoveJsonButton.ClearValue(Control.BorderBrushProperty);
    }

    private void InstallCorrectionControls()
    {
        var footer = DetailState.Children
            .OfType<Border>()
            .FirstOrDefault(control => Grid.GetRow(control) == 2);
        if (footer?.Child is not StackPanel actions)
        {
            return;
        }

        _correctionStatusText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(251, 146, 60)),
            FontSize = 11,
            Margin = new Thickness(2, 0, 0, 7),
            Visibility = Visibility.Collapsed,
        };

        var editButton = new Button
        {
            Content = "Editar anomalias",
            Padding = new Thickness(9),
            Margin = new Thickness(0, 0, 0, 7),
        };
        editButton.SetResourceReference(StyleProperty, "SecondaryButton");
        editButton.Click += EditOccurrencesClick;

        _exportCorrectionsButton = new Button
        {
            Content = "Exportar correções para GeoAutoDocs",
            Padding = new Thickness(9),
            Margin = new Thickness(0, 0, 0, 7),
            IsEnabled = false,
        };
        _exportCorrectionsButton.SetResourceReference(StyleProperty, "SecondaryButton");
        _exportCorrectionsButton.Click += ExportCorrectionsClick;

        actions.Children.Insert(0, _correctionStatusText);
        actions.Children.Insert(1, editButton);
        actions.Children.Insert(2, _exportCorrectionsButton);
    }

    private void EditOccurrencesClick(object sender, RoutedEventArgs eventArgs)
    {
        var selected = _viewModel.SelectedSection;
        var section = VideoInspectionRuntime.CurrentSession?.FindSection(selected?.SectionId ?? Guid.Empty);
        if (section is null)
        {
            MessageBox.Show(
                "Selecione um trecho de inspeção válido.",
                "GeoAutoEngine",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (section.Occurrences.Count == 0)
        {
            MessageBox.Show(
                "Este trecho não possui ocorrências para editar.",
                "GeoAutoEngine",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new VideoInspectionOccurrenceEditDialog(section);
        var owner = Window.GetWindow(this);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (dialog.Edit is not null)
        {
            VideoInspectionCorrectionRuntime.RequestEdit(dialog.Edit);
        }
        else if (dialog.RestoreOccurrenceId is { } occurrenceId)
        {
            VideoInspectionCorrectionRuntime.RequestRestore(section.SectionId, occurrenceId);
        }
    }

    private void ExportCorrectionsClick(object sender, RoutedEventArgs eventArgs) =>
        VideoInspectionCorrectionRuntime.RequestExport();

    private void ObserveSelectedSection()
    {
        if (ReferenceEquals(_observedSection, _viewModel.SelectedSection))
        {
            return;
        }

        StopObservingSection();
        _observedSection = _viewModel.SelectedSection;
        if (_observedSection is not null)
        {
            _observedSection.PropertyChanged += SelectedSectionPropertyChanged;
        }
    }

    private void StopObservingSection()
    {
        if (_observedSection is not null)
        {
            _observedSection.PropertyChanged -= SelectedSectionPropertyChanged;
            _observedSection = null;
        }
    }

    private void SelectedSectionPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) =>
        UpdateCorrectionControls();

    private void UpdateCorrectionControls()
    {
        if (_exportCorrectionsButton is null || _correctionStatusText is null)
        {
            return;
        }

        var session = VideoInspectionRuntime.CurrentSession;
        var count = session?.EditedOccurrenceCount ?? 0;
        _exportCorrectionsButton.IsEnabled = count > 0;
        _exportCorrectionsButton.Content = count > 0
            ? $"Exportar correções para GeoAutoDocs ({count})"
            : "Exportar correções para GeoAutoDocs";
        _correctionStatusText.Text = count == 1
            ? "1 correção local pendente"
            : $"{count} correções locais pendentes";
        _correctionStatusText.Visibility = count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
