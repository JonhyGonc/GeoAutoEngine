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
                System.Windows.Controls.ListBoxItem)
        {
            return;
        }

        ListState.Visibility = System.Windows.Visibility.Collapsed;
        DetailState.Visibility = System.Windows.Visibility.Visible;
    }

    private void BackToSections(object sender, System.Windows.RoutedEventArgs eventArgs)
    {
        DetailState.Visibility = System.Windows.Visibility.Collapsed;
        ListState.Visibility = System.Windows.Visibility.Visible;
    }
}
