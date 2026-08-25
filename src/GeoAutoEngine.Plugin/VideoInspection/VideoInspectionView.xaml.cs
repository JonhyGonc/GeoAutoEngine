using System.Diagnostics;

namespace GeoAutoEngine.Plugin.VideoInspection;

public partial class VideoInspectionView : System.Windows.Controls.UserControl
{
    internal VideoInspectionView(VideoInspectionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OpenSelectedSection(
        object sender,
        System.Windows.Input.MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.OriginalSource is not System.Windows.DependencyObject source ||
            System.Windows.Controls.ItemsControl.ContainerFromElement(SectionsList, source) is not
                System.Windows.Controls.ListBoxItem selectedItem)
        {
            return;
        }

        var total = Stopwatch.StartNew();
        ListState.Visibility = System.Windows.Visibility.Collapsed;
        DetailState.Visibility = System.Windows.Visibility.Visible;
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

    private void BackToSections(object sender, System.Windows.RoutedEventArgs eventArgs)
    {
        DetailState.Visibility = System.Windows.Visibility.Collapsed;
        ListState.Visibility = System.Windows.Visibility.Visible;
    }
}
