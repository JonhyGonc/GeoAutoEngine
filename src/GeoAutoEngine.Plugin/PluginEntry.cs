using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using GeoAutoEngine.Application;
using GeoAutoEngine.Cad2026;
using GeoAutoEngine.Csv;
using GeoAutoEngine.Domain;
using GeoAutoEngine.Plugin.VideoInspection;
using AutoCadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: ExtensionApplication(typeof(GeoAutoEngine.Plugin.PluginEntry))]
[assembly: CommandClass(typeof(GeoAutoEngine.Plugin.PluginCommands))]

namespace GeoAutoEngine.Plugin;

public sealed class PluginEntry : IExtensionApplication
{
    private const string InitializeMarkerEnvironmentVariable = "GEOAUTOENGINE_INITIALIZE_SMOKE_MARKER";
    internal static int? DatabaseModificationStateAtInitialize { get; private set; }

    public void Initialize()
    {
        // AutoCAD 2024 hosts plugins on .NET Framework and cannot receive binding
        // redirects in acad.exe.config. Resolve the bundled JSON dependencies before
        // any Palette/settings code is initialized.
        NetFrameworkDependencyResolver.Register();
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        PluginLog.Information(
            $"Plugin inicializado: {typeof(PluginEntry).Assembly.GetName().Version}; " +
            $"Host alvo: Civil 3D {TargetHostVersion}; Runtime: {Environment.Version}");

        // Read-only observation used by the CAD gate; no transaction or drawing write occurs.
        try
        {
            DatabaseModificationStateAtInitialize = AutoCadHostProbe.Capture().DatabaseModificationState;
        }
        catch (InvalidOperationException)
        {
            DatabaseModificationStateAtInitialize = null;
        }

        var initializeMarker = Environment.GetEnvironmentVariable(InitializeMarkerEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(initializeMarker))
        {
            File.WriteAllText(initializeMarker, "GeoAutoEngine initialized");
        }

        GeoAutoEngineRibbon.Initialize();
        GeoAutoEnginePalette.Initialize();
        VideoInspectionRuntime.Initialize(TargetHostVersion);
    }

    private static string TargetHostVersion =>
#if GEOAUTO_CAD_2024
        "2024";
#elif GEOAUTO_CAD_2025
        "2025";
#else
        "2026";
#endif

    public void Terminate()
    {
        VideoInspectionRuntime.Terminate();
        GeoAutoEnginePalette.Terminate();
        GeoAutoEngineRibbon.Terminate();
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        if (eventArgs.ExceptionObject is System.Exception exception)
        {
            PluginLog.Error("Exceção não tratada no AppDomain", exception);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        PluginLog.Error("Exceção não observada em Task", eventArgs.Exception);
        eventArgs.SetObserved();
    }

}

public sealed class PluginCommands
{
    private const string SmokeMarkerEnvironmentVariable = "GEOAUTOENGINE_STAGE1_SMOKE_MARKER";
    private const string Stage4InputsEnvironmentVariable = "GEOAUTOENGINE_STAGE4_INPUTS";
    private const string Stage4MarkerEnvironmentVariable = "GEOAUTOENGINE_STAGE4_SMOKE_MARKER";
    private const string Stage4FailureEnvironmentVariable = "GEOAUTOENGINE_STAGE4_INJECT_FAILURE_AFTER";
    private const string ProcessingModeEnvironmentVariable = "GEOAUTOENGINE_PROCESSING_MODE";
    private const string UiSmokeInputsEnvironmentVariable = "GEOAUTOENGINE_UI_SMOKE_INPUTS";
    private const string UiSmokeMarkerEnvironmentVariable = "GEOAUTOENGINE_UI_SMOKE_MARKER";

    [CommandMethod("GEOAUTO3PING", CommandFlags.NoHistory | CommandFlags.NoUndoMarker)]
    public void Ping()
    {
        var snapshot = AutoCadHostProbe.Capture();
        var document = AutoCadApplication.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("AutoCAD has no active document.");
        document.Editor.WriteMessage(
            $"\nGeoAutoEngine 3.0 loaded. ACADVER={snapshot.AcadVersion}; DBMOD={snapshot.DatabaseModificationState}.\n");

        var markerPath = Environment.GetEnvironmentVariable(SmokeMarkerEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(markerPath))
        {
            var marker = new
            {
                plugin = "GeoAutoEngine.Plugin",
                command = "GEOAUTO3PING",
                snapshot.AcadVersion,
                PluginEntry.DatabaseModificationStateAtInitialize,
                DatabaseModificationStateAtCommand = snapshot.DatabaseModificationState,
                DatabaseModificationStateUnchanged =
                    PluginEntry.DatabaseModificationStateAtInitialize == snapshot.DatabaseModificationState,
                snapshot.DrawingName,
            };
            File.WriteAllText(markerPath, JsonSerializer.Serialize(marker));
        }
    }

    [CommandMethod("GEOAUTO3PALETTE", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public void ShowPalette()
    {
        try
        {
            GeoAutoEnginePalette.Show();
        }
        catch (System.Exception exception)
        {
            PluginLog.Error("Falha ao abrir a Palette", exception);
            AutoCadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nGeoAutoEngine: não foi possível abrir a interface. {exception.Message}\n");
            return;
        }

        var markerPath = Environment.GetEnvironmentVariable(UiSmokeMarkerEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(markerPath))
        {
            return;
        }

        var configuredInputs = Environment.GetEnvironmentVariable(UiSmokeInputsEnvironmentVariable);
        var inputs = string.IsNullOrWhiteSpace(configuredInputs)
            ? []
            : configuredInputs.Split([Path.PathSeparator], StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .ToArray();
        var snapshot = GeoAutoEnginePalette.ProcessForSmoke(inputs);
        File.WriteAllText(markerPath, JsonSerializer.Serialize(new
        {
            command = "GEOAUTO3PALETTE",
            ribbonCreated = GeoAutoEngineRibbon.IsCreated,
            paletteVisible = GeoAutoEnginePalette.IsVisible,
            snapshot.SelectedFileCount,
            snapshot.Status,
            snapshot.Summary,
        }));
    }

    [CommandMethod("GEOAUTO3ADDCSV", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public void AddCsvToPalette()
    {
        try
        {
            var inputs = SelectInputsFromDialog();
            if (inputs.Count > 0)
            {
                GeoAutoEnginePalette.AddFilesFromCommand(inputs);
            }
        }
        catch (System.Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Comando nativo de seleção de CSV", exception);
            WritePaletteError("Não foi possível selecionar o levantamento.");
        }
    }

    [CommandMethod("GEOAUTO3VIDEO", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public void ShowVideoInspection()
    {
        ExecuteVideoAction("Abertura do módulo de vídeo-inspeção", () =>
        {
            VideoInspectionRuntime.DeliverPending();
            GeoAutoEnginePalette.ShowVideo();
        });
    }

    [CommandMethod("GEOAUTO3VIIMPORT", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public void ImportVideoInspection()
    {
        ExecuteVideoAction("Importação de inspeção", () =>
        {
            using var dialog = new OpenFileDialog
            {
                CheckFileExists = true,
                Filter = "Arquivo de inspeção (*.geoadocs.json)|*.geoadocs.json|Todos os arquivos (*.json)|*.json",
                Multiselect = false,
                RestoreDirectory = true,
                Title = "Importar arquivo de inspeção do GeoAutoDocs",
            };
            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            VideoInspectionRuntime.ImportFile(dialog.FileName);
            GeoAutoEnginePalette.ShowVideo();
            AutoCadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nGeoAutoEngine: inspeção importada com sucesso.\n");
        });
    }

    [CommandMethod("GEOAUTO3VIASSOCIATE", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public void AssociateVideoInspectionSection()
    {
        ExecuteVideoAction("Associação de geometria", () =>
            VideoInspectionRuntime.Associate(GeoAutoEnginePalette.TakePendingVideoSectionId()));
    }

    [CommandMethod("GEOAUTO3VICOMMIT", CommandFlags.Modal)]
    public void CommitVideoInspectionSection()
    {
        ExecuteVideoAction("Geração de vídeo-inspeção", () =>
            VideoInspectionRuntime.Commit(GeoAutoEnginePalette.TakePendingVideoSectionId()));
    }

    [CommandMethod("GEOAUTO3VIUPDATETEXTS", CommandFlags.Modal)]
    public void UpdateVideoInspectionTexts()
    {
        ExecuteVideoAction("Atualização de textos da vídeo-inspeção", () =>
            VideoInspectionRuntime.UpdateTexts(GeoAutoEnginePalette.TakePendingVideoTextSectionId()));
    }

    [CommandMethod("GEOAUTO3COMMITCSV", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public void CommitDroppedCsvToPalette()
    {
        try
        {
            GeoAutoEnginePalette.CommitPendingCsvFiles();
        }
        catch (System.Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Comando nativo de validação de CSV", exception);
            WritePaletteError("Não foi possível validar o levantamento.");
        }
    }

    [CommandMethod("GEOAUTO3PROCESS", CommandFlags.Modal)]
    public void ProcessPaletteSurveys()
    {
        try
        {
            GeoAutoEnginePalette.ProcessFromCommand();
        }
        catch (System.Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Comando nativo de processamento", exception);
            WritePaletteError("Não foi possível processar o levantamento.");
        }
    }

    [CommandMethod("GEOAUTO3REMOVECSV", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public void RemoveCsvFromPalette()
    {
        try
        {
            GeoAutoEnginePalette.RemovePendingCsvFile();
        }
        catch (System.Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Comando nativo de remoção de CSV", exception);
            WritePaletteError("Não foi possível remover o levantamento.");
        }
    }

    [CommandMethod("GEOAUTO3IMPORT", CommandFlags.Modal)]
    public void ImportAndApply()
    {
        var document = AutoCadApplication.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("AutoCAD has no active document.");
        var inputs = SelectInputs();
        if (inputs.Count == 0)
        {
            document.Editor.WriteMessage("\nGeoAutoEngine: importação cancelada.\n");
            return;
        }

        try
        {
            var service = new ImportBatchService(new CsvSourceImporter(), new SpatialDuplicateDetector());
            var batch = service.ImportAsync(CsvImportSources.FromFiles(inputs)).AsTask().GetAwaiter().GetResult();
            var presentation = PaletteSettingsStore.DrawingPresentation.Load();
            var plan = new DrawingPlanBuilder(new DrawingPlanBuildOptions(
                textHeight: presentation.TextHeightMeters,
                showRevisionMarks: presentation.ShowRevisionMarks)).Build(batch);
            var options = new AutoCadDrawingPlanApplyOptions(
                ResolveBlockAssetDirectory(),
                ReadInjectedFailureCount(),
                processingMode: ReadProcessingMode());
            var result = new AutoCadDrawingPlanApplier(options).ApplyAsync(plan).AsTask().GetAwaiter().GetResult();
            if (result.Succeeded && presentation.AddStandardSheet)
            {
                AutoCadStandardSheetService.Ensure(
                    document,
                    new AutoCadStandardSheetOptions(
                        ResolveStandardSheetPath(),
                        presentation.ScaleDenominator));
            }
            var drawing = AutoCadGeneratedDrawingInspector.Capture();
            AppendStage4Evidence(new Stage4Evidence(
                "GEOAUTO3IMPORT",
                inputs.Select(path => Path.GetFileName(path) ?? path).ToArray(),
                batch.Summary,
                plan.Summary,
                result,
                drawing,
                null));
            document.Editor.WriteMessage(
                $"\nGeoAutoEngine: criados={result.CreatedItemCount}; existentes={result.SkippedExistingItemCount}; " +
                $"pendentes={result.PendingItemCount}; falhas={result.FailedItemCount}.\n");
        }
        catch (System.Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            AppendStage4Evidence(new Stage4Evidence(
                "GEOAUTO3IMPORT",
                inputs.Select(path => Path.GetFileName(path) ?? path).ToArray(),
                null,
                null,
                null,
                TryCaptureDrawing(),
                exception.ToString()));
            document.Editor.WriteMessage($"\nGeoAutoEngine: falha na importação: {exception.Message}\n");
        }
    }

    [CommandMethod("GEOAUTO3VERIFY", CommandFlags.NoHistory | CommandFlags.NoUndoMarker)]
    public void VerifyGeneratedEntities()
    {
        var drawing = AutoCadGeneratedDrawingInspector.Capture();
        AppendStage4Evidence(new Stage4Evidence(
            "GEOAUTO3VERIFY",
            [],
            null,
            null,
            null,
            drawing,
            null));
        var document = AutoCadApplication.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("AutoCAD has no active document.");
        document.Editor.WriteMessage($"\nGeoAutoEngine: entidades identificadas={drawing.GeneratedEntityCount}.\n");
    }

    private static IReadOnlyList<string> SelectInputs()
    {
        var configured = Environment.GetEnvironmentVariable(Stage4InputsEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Split([Path.PathSeparator], StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Select(Path.GetFullPath)
                .ToArray();
        }

        return SelectInputsFromDialog();
    }

    private static IReadOnlyList<string> SelectInputsFromDialog()
    {
        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "Arquivos CSV (*.csv)|*.csv",
            Multiselect = true,
            RestoreDirectory = true,
            Title = "Selecionar levantamento(s) do GeoAutoEngine",
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileNames : [];
    }

    private static void WritePaletteError(string message)
    {
        var document = AutoCadApplication.DocumentManager.MdiActiveDocument;
        document?.Editor.WriteMessage($"\nGeoAutoEngine: {message}\n");
    }

    private static void ExecuteVideoAction(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (System.Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error(operation, exception);
            AutoCadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nGeoAutoEngine: {exception.Message}\n");
        }
    }

    private static string ResolveBlockAssetDirectory()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(PluginEntry).Assembly.Location)
            ?? throw new InvalidOperationException("Não foi possível resolver a pasta do plugin.");
        return Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "Assets", "Blocks"));
    }

    private static string ResolveStandardSheetPath()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(PluginEntry).Assembly.Location)
            ?? throw new InvalidOperationException("Não foi possível resolver a pasta do plugin.");
        return Path.GetFullPath(Path.Combine(
            assemblyDirectory,
            "..",
            "Assets",
            "Templates",
            "folha_padrao.dwt"));
    }

    private static int? ReadInjectedFailureCount()
    {
        var value = Environment.GetEnvironmentVariable(Stage4FailureEnvironmentVariable);
        return int.TryParse(value, out var count) && count > 0 ? count : null;
    }

    private static DrawingPlanProcessingMode ReadProcessingMode()
    {
        var value = Environment.GetEnvironmentVariable(ProcessingModeEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
        {
            value = File.ReadAllText(value).Trim();
        }

        return Enum.TryParse<DrawingPlanProcessingMode>(value, ignoreCase: true, out var mode)
            ? mode
            : DrawingPlanProcessingMode.Complete;
    }

    private static AutoCadGeneratedDrawingSnapshot? TryCaptureDrawing()
    {
        try
        {
            return AutoCadGeneratedDrawingInspector.Capture();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static void AppendStage4Evidence(Stage4Evidence evidence)
    {
        var markerPath = Environment.GetEnvironmentVariable(Stage4MarkerEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(markerPath))
        {
            return;
        }

        // Test evidence only. The production pipeline never serializes DrawingPlan to an intermediate file.
        var existing = File.Exists(markerPath)
            ? JsonNode.Parse(File.ReadAllText(markerPath)) as JsonArray ?? []
            : [];
        existing.Add(JsonSerializer.SerializeToNode(evidence));
        File.WriteAllText(markerPath, existing.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record Stage4Evidence(
        string Command,
        IReadOnlyList<string> Inputs,
        ImportBatchSummary? BatchSummary,
        DrawingPlanSummary? PlanSummary,
        DrawingPlanApplicationResult? ApplicationResult,
        AutoCadGeneratedDrawingSnapshot? Drawing,
        string? Error);
}
