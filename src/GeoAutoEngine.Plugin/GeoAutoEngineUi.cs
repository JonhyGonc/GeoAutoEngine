using System.Drawing;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.Windows;
using Autodesk.Windows;
using GeoAutoEngine.Plugin.VideoInspection;
using AutoCadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace GeoAutoEngine.Plugin;

internal static class GeoAutoEngineRibbon
{
    private const string TabId = "GEOAUTOENGINE3_RIBBON_TAB";
    private const string StartupMarkerEnvironmentVariable = "GEOAUTOENGINE_STARTUP_SMOKE_MARKER";
    private static readonly PaletteCommandHandler OpenHandler = new(GeoAutoEnginePalette.RequestShow);
    private static readonly PaletteCommandHandler AddCsvHandler = new(GeoAutoEnginePalette.RequestCsvSelection);
    private static readonly PaletteCommandHandler ProcessHandler = new(GeoAutoEnginePalette.RequestProcessing);
    private static readonly PaletteCommandHandler VideoInspectionHandler = new(GeoAutoEnginePalette.RequestVideoShow);
    private static readonly PaletteCommandHandler DiagnosticHandler = new(GeoAutoEnginePalette.ShowDiagnostic);
    private static readonly PaletteCommandHandler SettingsHandler = new(GeoAutoEnginePalette.ShowSettings);
    private static bool _waitingForIdle;
    private static bool _waitingForRibbonItem;

    internal static void Initialize()
    {
        if (TryCreate())
        {
            return;
        }

        _waitingForIdle = true;
        AutoCadApplication.Idle += OnIdle;
        _waitingForRibbonItem = true;
        ComponentManager.ItemInitialized += OnRibbonItemInitialized;
    }

    internal static void Terminate()
    {
        if (_waitingForIdle)
        {
            AutoCadApplication.Idle -= OnIdle;
            _waitingForIdle = false;
        }

        if (_waitingForRibbonItem)
        {
            ComponentManager.ItemInitialized -= OnRibbonItemInitialized;
            _waitingForRibbonItem = false;
        }
    }

    internal static bool IsCreated => ComponentManager.Ribbon?.Tabs.Any(
        tab => string.Equals(tab.Id, TabId, StringComparison.Ordinal)) == true;

    private static void OnIdle(object? sender, EventArgs eventArgs)
    {
        if (TryCreate())
        {
            StopWaiting();
        }
    }

    private static void OnRibbonItemInitialized(object? sender, RibbonItemEventArgs eventArgs)
    {
        if (TryCreate())
        {
            StopWaiting();
        }
    }

    private static void StopWaiting()
    {
        if (_waitingForIdle)
        {
            AutoCadApplication.Idle -= OnIdle;
            _waitingForIdle = false;
        }

        if (_waitingForRibbonItem)
        {
            ComponentManager.ItemInitialized -= OnRibbonItemInitialized;
            _waitingForRibbonItem = false;
        }
    }

    private static bool TryCreate()
    {
        var ribbon = ComponentManager.Ribbon;
        if (ribbon is null)
        {
            return false;
        }

        if (ribbon.Tabs.Any(tab => string.Equals(tab.Id, TabId, StringComparison.Ordinal)))
        {
            MarkStartupReady();
            return true;
        }

        var tab = new RibbonTab { Id = TabId, Title = "GeoAutoEngine" };
        tab.Panels.Add(CreatePanel(
            "GeoAutoEngine",
            CreateButton(
                "GEOAUTOENGINE3_OPEN_PALETTE",
                "Abrir\nGeoAutoEngine",
                OpenHandler,
                LoadLogo(32),
                RibbonItemSize.Large,
                vertical: true,
                "Abrir a Palette principal do GeoAutoEngine")));
        tab.Panels.Add(CreatePanel(
            "Levantamentos",
            CreateButton(
                "GEOAUTOENGINE3_ADD_CSV",
                "Adicionar CSV",
                AddCsvHandler,
                CreateStrokeIcon("M8,2 L8,14 M2,8 L14,8", "#27B5F5"),
                RibbonItemSize.Standard,
                vertical: false,
                "Selecionar um ou vários arquivos CSV"),
            CreateButton(
                "GEOAUTOENGINE3_PROCESS",
                "Processar",
                ProcessHandler,
                CreateFillIcon("M3,2 L14,8 L3,14 Z", "#75C84B"),
                RibbonItemSize.Standard,
                vertical: false,
                "Processar os levantamentos carregados no desenho ativo")));
        tab.Panels.Add(CreatePanel(
            "Vídeo-inspeção",
            CreateButton(
                "GEOAUTOENGINE3_VIDEO_INSPECTION",
                "Vídeo-inspeção",
                VideoInspectionHandler,
                CreateStrokeIcon("M2,4 L11,4 L11,12 L2,12 Z M11,6 L15,3 L15,13 L11,10 Z", "#75C84B"),
                RibbonItemSize.Large,
                vertical: true,
                "Receber, associar e posicionar inspeções do GeoAutoDocs")));
        tab.Panels.Add(CreatePanel(
            "Ferramentas",
            CreateButton(
                "GEOAUTOENGINE3_DIAGNOSTIC",
                "Diagnóstico",
                DiagnosticHandler,
                CreateStrokeIcon("M3,3 L13,3 M3,8 L13,8 M3,13 L13,13", "#FFB347"),
                RibbonItemSize.Standard,
                vertical: false,
                "Abrir o diagnóstico detalhado do último lote"),
            CreateButton(
                "GEOAUTOENGINE3_SETTINGS",
                "Configurações",
                SettingsHandler,
                CreateStrokeIcon("M3,3 L13,3 L13,13 L3,13 Z M6,6 L10,6 L10,10 L6,10 Z", "#A9B8C5"),
                RibbonItemSize.Standard,
                vertical: false,
                "Abrir as configurações do GeoAutoEngine")));
        ribbon.Tabs.Add(tab);
        MarkStartupReady();
        return true;
    }

    private static RibbonPanel CreatePanel(string title, params RibbonItem[] items)
    {
        var source = new RibbonPanelSource { Title = title };
        foreach (var item in items)
        {
            source.Items.Add(item);
        }

        return new RibbonPanel { Source = source };
    }

    private static RibbonButton CreateButton(
        string id,
        string text,
        ICommand handler,
        ImageSource image,
        RibbonItemSize size,
        bool vertical,
        string toolTip) => new()
        {
            Id = id,
            Text = text,
            CommandHandler = handler,
            Image = image,
            LargeImage = image,
            Size = size,
            ShowImage = true,
            ShowText = true,
            Orientation = vertical
                ? System.Windows.Controls.Orientation.Vertical
                : System.Windows.Controls.Orientation.Horizontal,
            ToolTip = toolTip,
        };

    private static BitmapImage LoadLogo(int pixelWidth)
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(GeoAutoEngineRibbon).Assembly.Location)!;
        var assetPath = Path.GetFullPath(Path.Combine(
            assemblyDirectory,
            "..",
            "Assets",
            "Branding",
            "logo_icone.png"));
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = new MemoryStream(File.ReadAllBytes(assetPath));
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = pixelWidth;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static DrawingImage CreateStrokeIcon(string geometry, string color)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        var pen = new System.Windows.Media.Pen(brush, 1.8)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        brush.Freeze();
        pen.Freeze();
        var image = new DrawingImage(new GeometryDrawing(null, pen, Geometry.Parse(geometry)));
        image.Freeze();
        return image;
    }

    private static DrawingImage CreateFillIcon(string geometry, string color)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        brush.Freeze();
        var image = new DrawingImage(new GeometryDrawing(brush, null, Geometry.Parse(geometry)));
        image.Freeze();
        return image;
    }

    private static void MarkStartupReady()
    {
        var markerPath = Environment.GetEnvironmentVariable(StartupMarkerEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(markerPath))
        {
            File.WriteAllText(markerPath, "GeoAutoEngine Ribbon ready");
        }
    }

    private sealed class PaletteCommandHandler(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}

internal static class GeoAutoEnginePalette
{
    // New identity resets the narrow docked layout persisted by earlier prototypes.
    private static readonly Guid PaletteId = new("F25CF142-81B3-4E95-9022-3FC9769DA582");
    private static PaletteSet? _palette;
    private static SurveyPaletteView? _content;
    private static GeoAutoPaletteController? _controller;
    private static VideoInspectionView? _videoContent;
    private static GeoAutoPaletteShell? _shell;
    private static VideoInspectionViewModel? _videoViewModel;
    private static VideoInspectionController? _videoController;
    private static IReadOnlyList<string> _pendingCsvFiles = [];
    private static string? _pendingRemovalPath;
    private static Guid? _pendingVideoSectionId;
    private static System.Windows.Threading.Dispatcher? _uiDispatcher;
    private static bool _waitingForStartupShow;

    internal static bool IsVisible => _palette?.Visible == true;

    internal static void Initialize()
    {
        var automatedSmoke = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("GEOAUTOENGINE_INITIALIZE_SMOKE_MARKER"));
        if (automatedSmoke || !PaletteSettingsStore.Load().OpenPaletteOnStartup || _waitingForStartupShow)
        {
            return;
        }

        _waitingForStartupShow = true;
        AutoCadApplication.Idle += OnStartupIdle;
    }

    internal static void Hide()
    {
        if (_palette is not null)
        {
            _palette.Visible = false;
        }
    }

    internal static void Terminate()
    {
        StopWaitingForStartupShow();
        if (_uiDispatcher is not null)
        {
            _uiDispatcher.UnhandledException -= OnDispatcherUnhandledException;
            _uiDispatcher = null;
        }

        _palette = null;
        _content = null;
        _controller = null;
        _videoController?.Dispose();
        _videoController = null;
        _videoContent = null;
        _shell = null;
        _videoViewModel = null;
        _pendingCsvFiles = [];
        _pendingRemovalPath = null;
        _pendingVideoSectionId = null;
    }

    private static void OnStartupIdle(object? sender, EventArgs eventArgs)
    {
        if (AutoCadApplication.DocumentManager.MdiActiveDocument is null)
        {
            return;
        }

        StopWaitingForStartupShow();
        try
        {
            Show();
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Abertura automática da Palette", exception);
        }
    }

    private static void StopWaitingForStartupShow()
    {
        if (!_waitingForStartupShow)
        {
            return;
        }

        AutoCadApplication.Idle -= OnStartupIdle;
        _waitingForStartupShow = false;
    }

    internal static void Show()
    {
        if (_palette is null)
        {
            EnsureUiExceptionBoundary();
            var viewModel = new GeoAutoPaletteViewModel();
            using (VideoInspectionPerformanceProbe.Measure("Survey Palette construction"))
            {
                _controller = new GeoAutoPaletteController(viewModel, new GeoAutoEngineWorkflow());
                _content = new SurveyPaletteView(viewModel);
            }

            _videoViewModel = new VideoInspectionViewModel();
            using (VideoInspectionPerformanceProbe.Measure("Controller/event registration"))
            {
                _videoController = new VideoInspectionController(
                    _videoViewModel,
                    RequestVideoImport,
                    RequestVideoAssociation,
                    RequestVideoCommit);
            }

            using (VideoInspectionPerformanceProbe.Measure("VideoInspectionView construction"))
            {
                _videoContent = new VideoInspectionView(_videoViewModel);
            }

            using (VideoInspectionPerformanceProbe.Measure("Palette shell/host construction"))
            {
                _shell = new GeoAutoPaletteShell(_content, _videoContent);
                _palette = new PaletteSet("GeoAutoEngine 3", PaletteId)
                {
                    DockEnabled = DockSides.Left | DockSides.Right,
                    MinimumSize = new Size(320, 520),
                    Size = new Size(420, 760),
                    Style = PaletteSetStyles.ShowAutoHideButton |
                        PaletteSetStyles.ShowCloseButton |
                        PaletteSetStyles.ShowPropertiesMenu,
                };
                _palette.AddVisual("GeoAutoEngine", _shell);
            }
            if (VideoInspectionRuntime.CurrentSession is not null)
            {
                _videoViewModel.Load(VideoInspectionRuntime.CurrentSession, "Inspeção carregada");
            }
        }

        _palette.Visible = true;
    }

    internal static SurveyPaletteSnapshot ProcessForSmoke(IReadOnlyList<string> sourceFiles)
    {
        Show();
        return _controller!.ProcessForSmoke(sourceFiles);
    }

    internal static void RequestShow() => QueueCommand("GEOAUTO3PALETTE");

    internal static void RequestVideoShow() => QueueCommand("GEOAUTO3VIDEO");

    internal static void ShowVideo()
    {
        Show();
        _shell!.SelectInspection();
    }

    internal static void UpdateVideoInspectionSession(
        GeoAutoEngine.Application.VideoInspection.VideoInspectionSession session,
        string notification)
    {
        if (_videoViewModel is not null)
        {
            _videoViewModel.Load(session, notification);
        }
    }

    internal static void RefreshVideoInspection() => _videoViewModel?.Refresh();

    internal static void RequestVideoImport() => QueueCommand("GEOAUTO3VIIMPORT");

    internal static void RequestVideoAssociation(Guid sectionId)
    {
        _pendingVideoSectionId = sectionId;
        QueueCommand("GEOAUTO3VIASSOCIATE");
    }

    internal static void RequestVideoCommit(Guid sectionId)
    {
        _pendingVideoSectionId = sectionId;
        QueueCommand("GEOAUTO3VICOMMIT");
    }

    internal static Guid TakePendingVideoSectionId()
    {
        var sectionId = _pendingVideoSectionId ?? _videoViewModel?.SelectedSection?.SectionId;
        _pendingVideoSectionId = null;
        return sectionId ?? throw new InvalidOperationException("Selecione um trecho de inspeção na Palette.");
    }

    internal static void RequestCsvSelection() => QueueCommand("GEOAUTO3ADDCSV");

    internal static void RequestCsvImport(IReadOnlyList<string> sourceFiles)
    {
        _pendingCsvFiles = sourceFiles.ToArray();
        QueueCommand("GEOAUTO3COMMITCSV");
    }

    internal static void RequestProcessing() => QueueCommand("GEOAUTO3PROCESS");

    internal static void RequestCsvRemoval(string sourcePath)
    {
        _pendingRemovalPath = sourcePath;
        QueueCommand("GEOAUTO3REMOVECSV");
    }

    internal static void ClearSelection()
    {
        Show();
        _controller!.ClearFiles();
    }

    internal static void ShowDiagnostic()
    {
        Show();
        _shell!.SelectSurvey();
        _content!.ShowDiagnosticPage();
    }

    internal static void ShowSettings()
    {
        Show();
        _shell!.SelectSurvey();
        _content!.ShowSettingsPage();
    }

    internal static void AddFilesFromCommand(IReadOnlyList<string> sourceFiles)
    {
        Show();
        _controller!.AddFiles(sourceFiles);
    }

    internal static void CommitPendingCsvFiles()
    {
        var files = _pendingCsvFiles;
        _pendingCsvFiles = [];
        AddFilesFromCommand(files);
    }

    internal static void ProcessFromCommand()
    {
        Show();
        _controller!.Process();
    }

    internal static void RemovePendingCsvFile()
    {
        var path = _pendingRemovalPath;
        _pendingRemovalPath = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            Show();
            _controller!.RemoveFile(path!);
        }
    }

    private static void QueueCommand(string command)
    {
        try
        {
            var document = AutoCadApplication.DocumentManager.MdiActiveDocument
                ?? throw new InvalidOperationException("O Civil 3D não possui um desenho ativo.");
            document.SendStringToExecute(command + " ", activate: true, wrapUpInactiveDoc: false, echoCommand: false);
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error($"Agendamento do comando {command}", exception);
        }
    }

    private static void EnsureUiExceptionBoundary()
    {
        if (_uiDispatcher is not null)
        {
            return;
        }

        _uiDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        _uiDispatcher.UnhandledException += OnDispatcherUnhandledException;
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        PluginLog.Error("Exceção não tratada na interface WPF", eventArgs.Exception);
        eventArgs.Handled = true;
    }

}
