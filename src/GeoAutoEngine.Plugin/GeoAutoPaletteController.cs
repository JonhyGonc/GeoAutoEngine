using System.IO;
using GeoAutoEngine.Application;
using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Plugin;

internal sealed class GeoAutoPaletteController
{
    private readonly GeoAutoPaletteViewModel _viewModel;
    private readonly GeoAutoEngineWorkflow _workflow;
    private readonly List<string> _sourceFiles = [];
    private ImportBatch? _batch;

    internal GeoAutoPaletteController(GeoAutoPaletteViewModel viewModel, GeoAutoEngineWorkflow workflow)
    {
        _viewModel = viewModel;
        _workflow = workflow;
    }

    internal void AddFiles(IReadOnlyList<string> sourceFiles) => Execute("Importação de CSV", () =>
    {
        foreach (var path in sourceFiles.Select(Path.GetFullPath))
        {
            if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase) &&
                !_sourceFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                _sourceFiles.Add(path);
            }
        }

        AnalyzeSelection();
    });

    internal void RemoveFile(string sourcePath) => Execute("Remoção de CSV", () =>
    {
        _sourceFiles.RemoveAll(path => string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase));
        AnalyzeSelection();
    });

    internal void ClearFiles() => Execute("Limpeza da seleção de CSV", () =>
    {
        _sourceFiles.Clear();
        _batch = null;
        _viewModel.SetSources([]);
    });

    internal void Process() => Execute("Processamento do levantamento", () =>
    {
        if (_sourceFiles.Count == 0)
        {
            _viewModel.Fail("Adicione pelo menos um arquivo CSV.");
            return;
        }

        _viewModel.Begin("Processando levantamentos...");
        _batch ??= _workflow.Import(_sourceFiles);
        var processingMode = _viewModel.SelectedProcessingMode;
        var plan = _workflow.BuildPlan(_batch, _viewModel.CreateDrawingPlanBuildOptions());
        var result = _workflow.Apply(plan, processingMode, _viewModel.CurrentDrawingPresentation());
        if (!result.Succeeded)
        {
            var detail = result.Diagnostics
                .FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)?.Message;
            throw new InvalidOperationException(detail ?? "A aplicação no desenho não foi concluída.");
        }

        _viewModel.Complete(result, processingMode);
    });

    internal SurveyPaletteSnapshot ProcessForSmoke(IReadOnlyList<string> sourceFiles)
    {
        AddFiles(sourceFiles);
        Execute("Smoke da Palette", () =>
        {
            if (_batch is null)
            {
                throw new InvalidOperationException("A importação de teste não produziu um lote.");
            }

            _viewModel.Begin("Preparando levantamento...");
            _viewModel.CompletePreparation(_workflow.BuildPlan(_batch, _viewModel.CreateDrawingPlanBuildOptions()));
        });
        return new SurveyPaletteSnapshot(_viewModel.FileCount, _viewModel.StatusMessage, _viewModel.ProcessActionSummary);
    }

    private void AnalyzeSelection()
    {
        _batch = null;
        _viewModel.SetSources(_sourceFiles);
        if (_sourceFiles.Count == 0)
        {
            return;
        }

        _viewModel.Begin("Validando arquivos e interpretando registros...");
        _batch = _workflow.Import(_sourceFiles);
        _viewModel.ApplyBatch(_batch);
    }

    private void Execute(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            PluginLog.Error(operation, exception);
            _viewModel.Fail("Não foi possível processar o levantamento. Ver detalhes.");
        }
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException;
}
