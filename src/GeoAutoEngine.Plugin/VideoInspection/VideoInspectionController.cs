namespace GeoAutoEngine.Plugin.VideoInspection;

internal sealed class VideoInspectionController : IDisposable
{
    private readonly VideoInspectionViewModel _viewModel;
    private readonly Action _import;
    private readonly Action<Guid> _associate;
    private readonly Action<Guid> _commit;

    public VideoInspectionController(
        VideoInspectionViewModel viewModel,
        Action import,
        Action<Guid> associate,
        Action<Guid> commit)
    {
        _viewModel = viewModel;
        _import = import;
        _associate = associate;
        _commit = commit;
        _viewModel.ImportRequested += _import;
        _viewModel.AssociateRequested += _associate;
        _viewModel.CommitRequested += _commit;
    }

    public void Dispose()
    {
        _viewModel.ImportRequested -= _import;
        _viewModel.AssociateRequested -= _associate;
        _viewModel.CommitRequested -= _commit;
    }
}
