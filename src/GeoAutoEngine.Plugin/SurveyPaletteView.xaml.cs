using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using IDataObject = System.Windows.IDataObject;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using UserControl = System.Windows.Controls.UserControl;

namespace GeoAutoEngine.Plugin;

internal sealed record SurveyPaletteSnapshot(int SelectedFileCount, string Status, string Summary);

internal sealed partial class SurveyPaletteView : UserControl
{
    private readonly GeoAutoPaletteViewModel _viewModel;

    internal SurveyPaletteView(GeoAutoPaletteViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void AddCsvFiles(object sender, RoutedEventArgs eventArgs) =>
        GeoAutoEnginePalette.RequestCsvSelection();

    private void AddCsvFromDropZone(object sender, MouseButtonEventArgs eventArgs) =>
        GeoAutoEnginePalette.RequestCsvSelection();

    private void CsvDragEnter(object sender, DragEventArgs eventArgs)
    {
        var csvFiles = GetDroppedCsvFiles(eventArgs.Data);
        eventArgs.Effects = csvFiles.Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        DropZone.Background = Brush(csvFiles.Length > 0 ? "#111D35" : "#0D1528");
        DropZone.BorderBrush = Brush(csvFiles.Length > 0 ? "#0EA5E9" : "#2A3F60");
        eventArgs.Handled = true;
    }

    private void CsvDragLeave(object sender, DragEventArgs eventArgs) => ResetDropZone();

    private void CsvDrop(object sender, DragEventArgs eventArgs)
    {
        var csvFiles = GetDroppedCsvFiles(eventArgs.Data);
        ResetDropZone();
        if (csvFiles.Length > 0)
        {
            GeoAutoEnginePalette.RequestCsvImport(csvFiles);
        }
    }

    private void ProcessSurveys(object sender, RoutedEventArgs eventArgs) =>
        GeoAutoEnginePalette.RequestProcessing();

    private void RemoveFileButton(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: SurveyFileViewModel file })
        {
            GeoAutoEnginePalette.RequestCsvRemoval(file.FullPath);
        }
    }

    private void ClearAllFiles(object sender, RoutedEventArgs eventArgs) =>
        GeoAutoEnginePalette.ClearSelection();

    private void ShowSummary(object sender, RoutedEventArgs eventArgs)
        => ShowSummaryPage();

    internal void ShowSummaryPage()
    {
        SummaryView.Visibility = Visibility.Visible;
        DiagnosticView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        SummaryTabButton.Foreground = Brush("#0EA5E9");
        DiagnosticTabButton.Foreground = Brush("#4A6A96");
        SummaryIndicator.Visibility = Visibility.Visible;
        DiagnosticIndicator.Visibility = Visibility.Collapsed;
    }

    private void ShowDiagnostic(object sender, RoutedEventArgs eventArgs)
        => ShowDiagnosticPage();

    internal void ShowDiagnosticPage()
    {
        SummaryView.Visibility = Visibility.Collapsed;
        DiagnosticView.Visibility = Visibility.Visible;
        SettingsView.Visibility = Visibility.Collapsed;
        SummaryTabButton.Foreground = Brush("#4A6A96");
        DiagnosticTabButton.Foreground = Brush("#0EA5E9");
        SummaryIndicator.Visibility = Visibility.Collapsed;
        DiagnosticIndicator.Visibility = Visibility.Visible;
    }

    private void ShowSettings(object sender, RoutedEventArgs eventArgs)
        => ShowSettingsPage();

    internal void ShowSettingsPage()
    {
        SummaryView.Visibility = Visibility.Collapsed;
        DiagnosticView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Visible;
        SummaryTabButton.Foreground = Brush("#4A6A96");
        DiagnosticTabButton.Foreground = Brush("#4A6A96");
        SummaryIndicator.Visibility = Visibility.Collapsed;
        DiagnosticIndicator.Visibility = Visibility.Collapsed;
    }

    private void RestoreDrawingPresentationDefaults(object sender, RoutedEventArgs eventArgs) =>
        _viewModel.RestoreDrawingPresentationDefaults();

    private void ClosePalette(object sender, RoutedEventArgs eventArgs) => GeoAutoEnginePalette.Hide();

    private static string[] GetDroppedCsvFiles(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] paths
            ? paths.Where(path => File.Exists(path) &&
                string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase)).ToArray()
            : [];

    private void ResetDropZone()
    {
        DropZone.Background = Brush("#0D1528");
        DropZone.BorderBrush = Brush("#2A3F60");
    }

    private void PaletteSizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        if (FindVisualChild<UniformGrid>(DiagnosticCardsControl) is { } panel)
        {
            panel.Columns = ActualWidth >= 480 ? 4 : 2;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            if (FindVisualChild<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static Brush Brush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }
}
