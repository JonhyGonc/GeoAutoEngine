namespace GeoAutoEngine.Plugin.VideoInspection;

internal sealed class VideoInspectionController : IDisposable
{
    private readonly VideoInspectionViewModel _viewModel;
    private readonly Action _import;
    private readonly Action<Guid> _associate;
    private readonly Action<Guid> _commit;
    private readonly Action<Guid?> _updateTexts;
    private readonly Action _syncNames;

    public VideoInspectionController(
        VideoInspectionViewModel viewModel,
        Action import,
        Action<Guid> associate,
        Action<Guid> commit,
        Action<Guid?> updateTexts,
        Action syncNames)
    {
        _viewModel = viewModel;
        _import = import;
        _associate = associate;
        _commit = commit;
        _updateTexts = updateTexts;
        _syncNames = syncNames;
        _viewModel.ImportRequested += _import;
        _viewModel.AssociateRequested += _associate;
        _viewModel.CommitRequested += _commit;
        _viewModel.UpdateTextsRequested += _updateTexts;
        _viewModel.SyncNamesRequested += _syncNames;
    }

    public void Dispose()
    {
        _viewModel.ImportRequested -= _import;
        _viewModel.AssociateRequested -= _associate;
        _viewModel.CommitRequested -= _commit;
        _viewModel.UpdateTextsRequested -= _updateTexts;
        _viewModel.SyncNamesRequested -= _syncNames;
    }
}
