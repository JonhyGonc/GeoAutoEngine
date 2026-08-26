using System.Security.Cryptography;
using System.Xml.Linq;

namespace GeoAutoEngine.Tests;

public sealed class ArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void ProjectDependencyDirection_IsExactlyTheApprovedMinimum()
    {
        Assert.Equal([], ProjectReferences("GeoAutoEngine.Domain"));
        Assert.Equal(["GeoAutoEngine.Domain"], ProjectReferences("GeoAutoEngine.Application"));
        Assert.Equal(["GeoAutoEngine.Application"], ProjectReferences("GeoAutoEngine.Csv"));
        Assert.Equal(["GeoAutoEngine.Application"], ProjectReferences("GeoAutoEngine.Cad2026"));
        Assert.Equal(["GeoAutoEngine.Cad2026", "GeoAutoEngine.Csv"], ProjectReferences("GeoAutoEngine.Plugin"));
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void AutodeskReferences_AreIsolatedAndNeverCopiedLocally()
    {
        var projects = Directory.GetFiles(Path.Combine(RepositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories);
        var autodeskProjects = projects
            .Where(path => XDocument.Load(path).Descendants("UseAutodeskAutoCAD").Any(element => element.Value == "true"))
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["GeoAutoEngine.Cad2026", "GeoAutoEngine.Plugin"], autodeskProjects);

        var targets = XDocument.Load(Path.Combine(RepositoryRoot, "Directory.Build.targets"));
        var references = targets.Descendants("Reference").ToArray();
        Assert.Equal(
            ["AcCoreMgd", "AcDbMgd", "AcMgd", "AcWindows", "AdWindows"],
            references.Select(reference => reference.Attribute("Include")!.Value));
        Assert.All(references, reference => Assert.Equal("false", reference.Element("Private")!.Value));
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void NativeFoundation_ContainsNoCivilSpecificReference()
    {
        var roots = new[]
        {
            Path.Combine(RepositoryRoot, "src"),
            Path.Combine(RepositoryRoot, "build"),
        };
        var text = string.Join(
            Environment.NewLine,
            roots.SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".xml")
                .Select(File.ReadAllText));

        Assert.DoesNotContain("AeccDbMgd", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void BundleManifest_TargetsAutoCadReleases243250And251()
    {
        var manifest = XDocument.Load(Path.Combine(
            RepositoryRoot,
            "build",
            "GeoAutoEngine.bundle",
            "PackageContents.xml"));
        Assert.Equal(
            ["R24.3", "R25.0", "R25.1"],
            manifest.Descendants("RuntimeRequirements")
                .Select(requirement => requirement.Attribute("SeriesMin")!.Value));
        Assert.All(
            manifest.Descendants("RuntimeRequirements"),
            requirement => Assert.Equal(
                requirement.Attribute("SeriesMin")!.Value,
                requirement.Attribute("SeriesMax")!.Value));

        var expectedCommands = new[]
        {
                "GEOAUTO3ADDCSV",
                "GEOAUTO3COMMITCSV",
                "GEOAUTO3IMPORT",
                "GEOAUTO3PALETTE",
                "GEOAUTO3PING",
                "GEOAUTO3PROCESS",
                "GEOAUTO3REMOVECSV",
                "GEOAUTO3VERIFY",
                "GEOAUTO3VIASSOCIATE",
                "GEOAUTO3VICOMMIT",
                "GEOAUTO3VIDEO",
                "GEOAUTO3VIIMPORT",
        };
        Assert.All(manifest.Descendants("ComponentEntry"), component =>
        {
            Assert.Null(component.Attribute("LoadReasons"));
            Assert.Equal("True", component.Attribute("LoadOnAutoCADStartup")!.Value);
            Assert.Equal("True", component.Attribute("LoadOnCommandInvocation")!.Value);
            Assert.Equal(
                expectedCommands,
                component.Descendants("Command")
                    .Select(command => command.Attribute("Global")!.Value)
                    .Order(StringComparer.Ordinal));
        });
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void DomainHasNoExternalOrProjectDependencies()
    {
        var project = LoadProject("GeoAutoEngine.Domain");

        Assert.Empty(project.Descendants("ProjectReference"));
        var packages = project.Descendants("PackageReference").ToArray();
        var package = Assert.Single(packages);
        Assert.Equal("System.Text.Json", package.Attribute("Include")!.Value);
        Assert.Contains("netstandard2.0", package.Parent!.Attribute("Condition")!.Value, StringComparison.Ordinal);
        Assert.Empty(project.Descendants("Reference"));
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void NativeBlockAssets_AreExactlyTheSevenFrozenCatalogDefinitions()
    {
        var directory = Path.Combine(RepositoryRoot, "assets", "blocks");
        var expected = File.ReadAllLines(Path.Combine(directory, "MANIFEST.sha256"))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(parts => parts[1], parts => parts[0], StringComparer.Ordinal);
        var actualFiles = Directory.GetFiles(directory, "*.dwg")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actualFiles.Select(Path.GetFileName));
        foreach (var path in actualFiles)
        {
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            Assert.Equal(expected[Path.GetFileName(path)], hash);
        }
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void NativeProductionPath_ContainsNoAutoLispOrScrRuntime()
    {
        var sourceRoot = Path.Combine(RepositoryRoot, "src");

        Assert.Empty(Directory.GetFiles(sourceRoot, "*.lsp", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(sourceRoot, "*.scr", SearchOption.AllDirectories));
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void PaletteBoundary_HasNoAsyncOrApplicationCurrentCallbacks()
    {
        var pluginRoot = Path.Combine(RepositoryRoot, "src", "GeoAutoEngine.Plugin");
        var source = string.Join(
            Environment.NewLine,
            Directory.GetFiles(pluginRoot, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.DoesNotContain("async void", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Application.Current", source, StringComparison.Ordinal);
        Assert.Contains("SendStringToExecute", source, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void PaletteProgressBinding_IsExplicitlyOneWay()
    {
        var xaml = XDocument.Load(Path.Combine(
            RepositoryRoot,
            "src",
            "GeoAutoEngine.Plugin",
            "SurveyPaletteView.xaml"));
        var progress = Assert.Single(
            xaml.Descendants(),
            element => element.Name.LocalName == "ProgressBar");

        Assert.Equal("{Binding Percentage, Mode=OneWay}", progress.Attribute("Value")!.Value);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Palette_UsesOfficialLogoAndHasNoFeaturePlaceholders()
    {
        var pluginRoot = Path.Combine(RepositoryRoot, "src", "GeoAutoEngine.Plugin");
        var xaml = File.ReadAllText(Path.Combine(pluginRoot, "SurveyPaletteView.xaml"));
        var ribbon = File.ReadAllText(Path.Combine(pluginRoot, "GeoAutoEngineUi.cs"));
        var pluginLogo = File.ReadAllBytes(Path.Combine(pluginRoot, "Assets", "logo_icone.png"));
        var legacyLogo = File.ReadAllBytes(Path.Combine(
            Directory.GetParent(RepositoryRoot)!.FullName,
            "GeoAutoEngine.Legacy",
            "assets",
            "branding",
            "logo_icone.png"));

        Assert.Equal(SHA256.HashData(legacyLogo), SHA256.HashData(pluginLogo));
        Assert.Contains("Assets/logo_icone.png", xaml, StringComparison.Ordinal);
        Assert.Contains("\"Levantamentos\"", ribbon, StringComparison.Ordinal);
        Assert.Contains("\"Ferramentas\"", ribbon, StringComparison.Ordinal);
        Assert.Contains("ShowImage = true", ribbon, StringComparison.Ordinal);
        Assert.Contains("\"Branding\"", ribbon, StringComparison.Ordinal);
        Assert.Contains("DetailedDiagnostics", xaml, StringComparison.Ordinal);
        Assert.Contains("PlantScaleOptions", xaml, StringComparison.Ordinal);
        Assert.Contains("PresentationComboBox", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowRevisionMarks", xaml, StringComparison.Ordinal);
        Assert.Contains("AddStandardSheet", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Altura impressa", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("será habilitado em uma etapa futura", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("estarão disponíveis em uma etapa futura", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void OfficialSheetTemplate_IsPackagedWithoutAnAbsoluteUserPath()
    {
        var project = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "GeoAutoEngine.Plugin",
            "GeoAutoEngine.Plugin.csproj"));
        var workflow = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "GeoAutoEngine.Plugin",
            "GeoAutoEngineWorkflow.cs"));
        var template = Path.Combine(RepositoryRoot, "assets", "templates", "folha_padrao.dwt");

        Assert.True(File.Exists(template));
        Assert.True(new FileInfo(template).Length > 0);
        Assert.Contains("BundleTemplateAssets", project, StringComparison.Ordinal);
        Assert.Contains("Assets", workflow, StringComparison.Ordinal);
        Assert.Contains("Templates", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users\\", workflow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void SuccessfulPaletteProcessing_PreservesTheCsvSessionAndOffersExplicitClearing()
    {
        var pluginRoot = Path.Combine(RepositoryRoot, "src", "GeoAutoEngine.Plugin");
        var controller = File.ReadAllText(Path.Combine(pluginRoot, "GeoAutoPaletteController.cs"));
        var viewModel = File.ReadAllText(Path.Combine(pluginRoot, "ViewModels", "GeoAutoPaletteViewModel.cs"));
        var xaml = File.ReadAllText(Path.Combine(pluginRoot, "SurveyPaletteView.xaml"));

        var processBody = controller[controller.IndexOf("internal void Process()", StringComparison.Ordinal)..
            controller.IndexOf("internal SurveyPaletteSnapshot", StringComparison.Ordinal)];
        Assert.DoesNotContain("_sourceFiles.Clear()", processBody, StringComparison.Ordinal);
        Assert.Contains("Os CSVs continuam carregados", viewModel, StringComparison.Ordinal);
        Assert.Contains("SelectedProcessingMode", controller, StringComparison.Ordinal);
        Assert.Contains("Limpar todos", xaml, StringComparison.Ordinal);
        Assert.Contains("RemoveFileButton", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Delete", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Move", controller, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void PaletteStartupPreference_IsPersistentAndDefaultsToEnabled()
    {
        var pluginRoot = Path.Combine(RepositoryRoot, "src", "GeoAutoEngine.Plugin");
        var settings = File.ReadAllText(Path.Combine(pluginRoot, "ViewModels", "PaletteSettings.cs"));
        var palette = File.ReadAllText(Path.Combine(pluginRoot, "GeoAutoEngineUi.cs"));
        var xaml = File.ReadAllText(Path.Combine(pluginRoot, "SurveyPaletteView.xaml"));

        Assert.Contains("OpenPaletteOnStartup { get; set; } = true", settings, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Serialize", settings, StringComparison.Ordinal);
        Assert.Contains("AutoCadApplication.Idle += OnStartupIdle", palette, StringComparison.Ordinal);
        Assert.Contains("Abrir GeoAutoEngine ao iniciar o Civil 3D", xaml, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void CadWriter_AssignsResolvedLayerObjectIds()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "GeoAutoEngine.Cad2026",
            "AutoCadDrawingPlanApplier.cs"));

        Assert.DoesNotContain("Layer =", source, StringComparison.Ordinal);
        Assert.Contains("LayerId = layerId", source, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void VideoInspection_HasNoTransientPreviewOrAutomaticDrawingPath()
    {
        var pluginRoot = Path.Combine(RepositoryRoot, "src", "GeoAutoEngine.Plugin");
        var runtime = File.ReadAllText(Path.Combine(
            pluginRoot,
            "VideoInspection",
            "VideoInspectionRuntime.cs"));
        var xaml = File.ReadAllText(Path.Combine(
            pluginRoot,
            "VideoInspection",
            "VideoInspectionView.xaml"));
        var videoRoots = new[]
        {
            Path.Combine(RepositoryRoot, "src", "GeoAutoEngine.Application", "VideoInspection"),
            Path.Combine(RepositoryRoot, "src", "GeoAutoEngine.Cad2026", "VideoInspection"),
            Path.Combine(pluginRoot, "VideoInspection"),
        };
        var source = string.Join(Environment.NewLine, videoRoots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            .Where(path => Path.GetExtension(path) is ".cs" or ".xaml")
            .Select(File.ReadAllText));

        Assert.DoesNotContain("TransientManager", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddTransient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EraseTransient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PREVIEW", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CANCELAR", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("var positioning = Position(document, section);", runtime, StringComparison.Ordinal);
        var associateBody = runtime[
            runtime.IndexOf("internal static void Associate", StringComparison.Ordinal)..
            runtime.IndexOf("internal static void Commit", StringComparison.Ordinal)];
        Assert.DoesNotContain("Renderer.Commit", associateBody, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void VideoInspection_AssociationRestoreIsEventDrivenAndDoesNotReprocessOnIdle()
    {
        var runtime = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "GeoAutoEngine.Plugin",
            "VideoInspection",
            "VideoInspectionRuntime.cs"));

        var idleBody = runtime[
            runtime.IndexOf("private static void OnIdle", StringComparison.Ordinal)..
            runtime.IndexOf("private static void OnDocumentBecameCurrent", StringComparison.Ordinal)];

        Assert.Contains(
            "DocumentBecameCurrent += OnDocumentBecameCurrent",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains("if (_restoringAssociations)", runtime, StringComparison.Ordinal);
        Assert.Contains(
            "ReferenceEquals(_restoredDocument, document)",
            runtime,
            StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreAssociations", idleBody, StringComparison.Ordinal);
        Assert.DoesNotContain("_activeDatabase", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "document.Editor.WriteMessage(\"\\n\" + message)",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "GEOAUTOENGINE_VIDEO_DETAILED_DIAGNOSTICS",
            runtime,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void VideoInspectionCommit_TargetsModelSpaceAndValidatesCommittedObjectIds()
    {
        var renderer = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "GeoAutoEngine.Cad2026",
            "VideoInspection",
            "AutoCadInspectionRenderer.cs"));

        Assert.Contains("using var documentLock = document.LockDocument()", renderer, StringComparison.Ordinal);
        Assert.Contains("StartTransaction()", renderer, StringComparison.Ordinal);
        Assert.Contains("blockTable[BlockTableRecord.ModelSpace]", renderer, StringComparison.Ordinal);
        Assert.Contains("EventLayerName = \"GF-Chamado-Estaqueamento\"", renderer, StringComparison.Ordinal);
        Assert.Contains("EventLayerColorIndex = 2", renderer, StringComparison.Ordinal);
        Assert.Contains("linetypes[\"Continuous\"]", renderer, StringComparison.Ordinal);
        Assert.Contains("modelSpace.AppendEntity(marker)", renderer, StringComparison.Ordinal);
        Assert.Contains("modelSpace.AppendEntity(leader)", renderer, StringComparison.Ordinal);
        Assert.Contains("modelSpace.AppendEntity(text)", renderer, StringComparison.Ordinal);
        Assert.Contains("marker.Center = anchor", renderer, StringComparison.Ordinal);
        Assert.Contains("leader.StartPoint = anchor", renderer, StringComparison.Ordinal);
        Assert.Contains("UpsertSectionIdentification", renderer, StringComparison.Ordinal);
        Assert.Contains("SectionNameComponent", renderer, StringComparison.Ordinal);
        Assert.Contains("SectionDiameterComponent", renderer, StringComparison.Ordinal);
        Assert.Contains("Value(\"LOCATION_MODE\", call.LocationMode.ToString())", renderer, StringComparison.Ordinal);
        Assert.Contains("transaction.Commit()", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentSpaceId,\n            OpenMode.ForWrite", renderer, StringComparison.Ordinal);
        Assert.Contains("pending.ObjectId.IsValid", renderer, StringComparison.Ordinal);
        Assert.Contains("TransactionCommitted", renderer, StringComparison.Ordinal);
    }

    private static string[] ProjectReferences(string projectName) =>
        LoadProject(projectName)
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static XDocument LoadProject(string projectName) => XDocument.Load(Path.Combine(
        RepositoryRoot,
        "src",
        projectName,
        $"{projectName}.csproj"));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GeoAutoEngine.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the GeoAutoEngine repository root.");
    }
}
