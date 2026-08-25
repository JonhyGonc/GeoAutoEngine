using System.IO;
using GeoAutoEngine.Application;
using GeoAutoEngine.Cad2026;
using GeoAutoEngine.Csv;
using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Plugin;

internal sealed class GeoAutoEngineWorkflow
{
    private readonly ImportBatchService _importer = new(new CsvSourceImporter(), new SpatialDuplicateDetector());
    private readonly DrawingPlanBuilder _planBuilder = new();

    internal ImportBatch Import(IReadOnlyList<string> sourceFiles)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        if (sourceFiles.Count == 0)
        {
            throw new ArgumentException("Selecione pelo menos um arquivo CSV.", nameof(sourceFiles));
        }

        return _importer.ImportAsync(CsvImportSources.FromFiles(sourceFiles))
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    internal DrawingPlan BuildPlan(ImportBatch batch, DrawingPlanBuildOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return options is null ? _planBuilder.Build(batch) : new DrawingPlanBuilder(options).Build(batch);
    }

    internal DrawingPlanApplicationResult Apply(
        DrawingPlan plan,
        DrawingPlanProcessingMode processingMode,
        DrawingPresentationSettings presentation)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var applier = new AutoCadDrawingPlanApplier(
            new AutoCadDrawingPlanApplyOptions(
                ResolveBlockAssetDirectory(),
                processingMode: processingMode));
        var result = applier.ApplyAsync(plan).AsTask().GetAwaiter().GetResult();
        if (result.Succeeded && presentation.AddStandardSheet)
        {
            var document = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument
                ?? throw new InvalidOperationException("O AutoCAD não possui documento ativo.");
            AutoCadStandardSheetService.Ensure(
                document,
                new AutoCadStandardSheetOptions(
                    ResolveStandardSheetPath(),
                    presentation.ScaleDenominator));
        }

        return result;
    }

    private static string ResolveBlockAssetDirectory()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(GeoAutoEngineWorkflow).Assembly.Location)
            ?? throw new InvalidOperationException("Não foi possível resolver a pasta do plugin.");
        return Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "Assets", "Blocks"));
    }

    private static string ResolveStandardSheetPath()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(GeoAutoEngineWorkflow).Assembly.Location)
            ?? throw new InvalidOperationException("Não foi possível resolver a pasta do plugin.");
        return Path.GetFullPath(Path.Combine(
            assemblyDirectory,
            "..",
            "Assets",
            "Templates",
            "folha_padrao.dwt"));
    }
}
