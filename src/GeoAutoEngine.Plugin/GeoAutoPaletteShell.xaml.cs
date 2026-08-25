using System.Windows;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using UserControl = System.Windows.Controls.UserControl;
using GeoAutoEngine.Plugin.VideoInspection;

namespace GeoAutoEngine.Plugin;

internal sealed partial class GeoAutoPaletteShell : UserControl
{
    private readonly SurveyPaletteView _surveyView;

    internal GeoAutoPaletteShell(
        SurveyPaletteView surveyView,
        VideoInspectionView inspectionView)
    {
        _surveyView = surveyView;
        InitializeComponent();
        SurveyHost.Content = surveyView;
        InspectionHost.Content = inspectionView;
    }

    internal void SelectInspection() => ShowInspectionCore();

    internal void SelectSurvey() => ShowSurveyCore();

    private void ShowSurvey(object sender, RoutedEventArgs eventArgs) => ShowSurveyCore();

    private void ShowInspection(object sender, RoutedEventArgs eventArgs) => ShowInspectionCore();

    private void ShowSettings(object sender, RoutedEventArgs eventArgs)
    {
        ShowSurveyCore();
        _surveyView.ShowSettingsPage();
    }

    private void ShowSurveyCore()
    {
        SurveyHost.Visibility = Visibility.Visible;
        InspectionHost.Visibility = Visibility.Collapsed;
        SurveyTabIndicator.Visibility = Visibility.Visible;
        InspectionTabIndicator.Visibility = Visibility.Collapsed;
        SurveyTabButton.Foreground = Brush("#E8F0FE");
        InspectionTabButton.Foreground = Brush("#4A6A96");
    }

    private void ShowInspectionCore()
    {
        SurveyHost.Visibility = Visibility.Collapsed;
        InspectionHost.Visibility = Visibility.Visible;
        SurveyTabIndicator.Visibility = Visibility.Collapsed;
        InspectionTabIndicator.Visibility = Visibility.Visible;
        SurveyTabButton.Foreground = Brush("#4A6A96");
        InspectionTabButton.Foreground = Brush("#E8F0FE");
    }

    private static Brush Brush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }
}
