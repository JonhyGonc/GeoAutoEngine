namespace GeoAutoEngine.Plugin.VideoInspection;

public partial class VideoInspectionView : System.Windows.Controls.UserControl
{
    private readonly VideoInspectionViewModel _viewModel;
    private bool _removeConfirmationPending;

    internal VideoInspectionView(VideoInspectionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
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

    private void RemoveJsonClick(object sender, System.Windows.RoutedEventArgs eventArgs)
    {
        if (!_removeConfirmationPending)
        {
            _removeConfirmationPending = true;
            RemoveJsonText.Text = "Confirmar remoção";
            RemoveJsonButton.Padding = new System.Windows.Thickness(10, 7, 10, 7);
            RemoveJsonButton.FontWeight = System.Windows.FontWeights.SemiBold;
            RemoveJsonButton.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(239, 68, 68));
            RemoveJsonButton.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(31, 239, 68, 68));
            RemoveJsonButton.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(102, 239, 68, 68));
            return;
        }

        ResetRemoveJsonButton();
        DetailState.Visibility = System.Windows.Visibility.Collapsed;
        ListState.Visibility = System.Windows.Visibility.Visible;
        _viewModel.ClearLoadedInspection();
    }

    private void CancelRemoveJson(
        object sender,
        System.Windows.Input.KeyboardFocusChangedEventArgs eventArgs) => ResetRemoveJsonButton();

    private void ResetRemoveJsonButton()
    {
        _removeConfirmationPending = false;
        RemoveJsonText.Text = ".json";
        RemoveJsonButton.ClearValue(System.Windows.Controls.Control.PaddingProperty);
        RemoveJsonButton.ClearValue(System.Windows.Controls.Control.FontWeightProperty);
        RemoveJsonButton.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
        RemoveJsonButton.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
        RemoveJsonButton.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
    }
}
