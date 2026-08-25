namespace GeoAutoEngine.Plugin.VideoInspection;

public partial class VideoInspectionView : System.Windows.Controls.UserControl
{
    internal VideoInspectionView(VideoInspectionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
