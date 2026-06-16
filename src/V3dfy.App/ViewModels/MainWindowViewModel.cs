using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using V3dfy.App.Mvvm;
using V3dfy.App.Services;
using V3dfy.Core.Analysis;
using V3dfy.Core.Diagnostics;
using V3dfy.Core.Estimation;
using V3dfy.Core.Execution;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Core.Preview;
using V3dfy.Core.Processes;
using V3dfy.Core.Recommendations;
using V3dfy.Core.Readiness;
using V3dfy.Core.Workflow;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Engine.Iw3.Execution;
using V3dfy.Engine.Iw3.Planning;
using V3dfy.Infrastructure.Analysis;
using V3dfy.Infrastructure.Estimation;
using V3dfy.Infrastructure.Files;
using V3dfy.Infrastructure.Health;
using V3dfy.Infrastructure.ModelPacks;
using V3dfy.Infrastructure.Paths;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.App.ViewModels;

public sealed record ModelHelpRow(
    string Model,
    string Purpose,
    string Use,
    string Scene,
    string Depth,
    string SizePerformance);

public sealed record SelectableModelInventoryRow(
    string Model,
    string Iw3DepthModel,
    string Checkpoint,
    string Type,
    string Source);

public enum SettingsSection
{
    VisualSettings,
    Models,
    ToolsEngine,
    LogsDiagnostics,
    AboutLicenses,
}

public enum AppSection
{
    Home,
    ImageConversion,
    VideoConversion,
}

public enum ImageConversionMode
{
    ParallaxPhoto,
    StereoscopicImage,
}

public enum ImageConversionStep
{
    ModeAndSource,
    Setup,
    PreviewAndExport,
}

public enum ImageStereoOutputFormat
{
    SideBySide,
    HalfTopBottom,
    Anaglyph,
    LeftRightPair,
}

public sealed class MainWindowViewModel : ObservableObject
{
    private const string SetupHelperExecutableName = "V3dfy.SetupHelper.exe";

    private enum TopToastKind
    {
        None,
        LogCopy,
        PreviewStageReset,
    }

    private enum ActivityLogModalKind
    {
        General,
        Image,
    }

    private static readonly TimeSpan LogCopyNotificationDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PreviewStageResetNoticeDuration = TimeSpan.FromSeconds(4);

    private static readonly HashSet<string> SupportedVideoExtensions =
    [
        ".mp4",
        ".mkv",
        ".avi",
        ".mov",
        ".m4v",
        ".webm",
    ];

    private static readonly HashSet<string> SupportedImageExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".tif",
        ".tiff",
        ".webp",
    ];

    private enum ToolStatusComponent
    {
        BundledTool,
        EmbeddedPython,
        Iw3Engine,
        Models,
        Iw3RuntimeDependency,
    }

    private sealed record ImageMetadata(
        int Width,
        int Height,
        string WidthText,
        string HeightText,
        string AspectRatio,
        string Format,
        string PixelFormat,
        string FileSizeText);

    private readonly InternalToolPaths _toolPaths;
    private readonly InternalToolsHealthChecker _healthChecker;
    private readonly AppThemeService _themeService;
    private readonly IFfprobeVideoAnalysisService _videoAnalysisService;
    private readonly VideoConversionRecommendationService _recommendationService;
    private readonly VideoConversionPlanService _conversionPlanService;
    private readonly ConversionReadinessService _conversionReadinessService;
    private readonly Iw3ConversionReadinessService _iw3ConversionReadinessService;
    private readonly ConversionExecutionFeatureGate _conversionExecutionFeatureGate;
    private readonly ConversionExecutionRequestFactory _conversionExecutionRequestFactory;
    private readonly ConversionExecutionRequestValidator _conversionExecutionRequestValidator = new();
    private readonly ConversionTimeEstimator _conversionTimeEstimator = new();
    private readonly OutputSizeEstimator _outputSizeEstimator = new();
    private readonly ModelGuidanceService _modelGuidanceService = new();
    private readonly IConversionPerformanceHistoryStore _performanceHistoryStore;
    private readonly IConversionExecutor _conversionExecutor;
    private readonly ConversionOutputOpenService _conversionOutputOpenService;
    private readonly IIw3PreviewExecutor _previewExecutor;
    private readonly PreviewCachePathService _previewCachePathService;
    private readonly PreviewCacheCleaner _previewCacheCleaner;
    private readonly IPreviewCacheFileService _previewCacheFileService;
    private readonly IPreviewCachePathProvider _previewCachePathProvider;
    private readonly PreviewOutputOpenService _previewOutputOpenService;
    private readonly ModelPackAppImportCoordinator _modelPackImportCoordinator;
    private readonly ConversionPlanOptionState _planOptionState = new();
    private readonly ConversionOutputPathState _outputPathState = new();
    private readonly ConversionWorkflowState _workflowState = new();
    private readonly ConversionProgressTimingSmoother _conversionTimingSmoother = new();
    private readonly StringBuilder _previewGenerationLogTextBuilder = new();
    private ActivityLogModalKind _activeActivityLogModalKind;
    private string? _selectedVideoPath;
    private string _selectedLanguage = "English";
    private string _selectedTheme = "Dark";
    private EngineHealthStatus? _toolHealth;
    private EngineDependencyHealth? _dependencyHealth;
    private VideoAnalysisResult? _analysis;
    private VideoConversionSetupRecommendation? _conversionRecommendation;
    private VideoConversionPlan? _conversionPlan;
    private ConversionReadiness? _conversionReadiness;
    private ConversionExecutionState _conversionExecutionState = ConversionExecutionState.NotStarted();
    private IReadOnlyList<ConversionPerformanceRecord> _performanceHistory = [];
    private PreviewWorkflowState _previewState =
        PreviewWorkflowState.NotGenerated(TimeSpan.Zero, PreviewTimeRangeService.DefaultDuration);
    private LocalModelSelectionCandidate? _selectedLocalModelCandidate;
    private TargetDevicePreset _selectedOutputPreset = TargetDevicePresets.Recommended3dTv;
    private string _outputPathText = string.Empty;
    private string _technicalDetailsBodyText = string.Empty;
    private string _activityLogModalText = string.Empty;
    private string _logCopyNotificationEnglishText = string.Empty;
    private string _logCopyNotificationSpanishText = string.Empty;
    private string _previewFromText = PreviewTimeRangeService.Format(TimeSpan.Zero);
    private string _previewToText = PreviewTimeRangeService.Format(PreviewTimeRangeService.DefaultDuration);
    private string _lastConversionOutputLine = string.Empty;
    private string _cpuUsageText = "CPU: Detecting...";
    private string _ramUsageText = "RAM: Detecting...";
    private string _gpuUsageText = "GPU: Detecting...";
    private string _vramUsageText = string.Empty;
    private string _previewCpuUsageText = "CPU: Detecting...";
    private string _previewRamUsageText = "RAM: Detecting...";
    private string _previewGpuUsageText = "GPU: Detecting...";
    private string _previewVramUsageText = "VRAM: Detecting...";
    private string _previewGpuMetricsStatusText = "GPU metrics: Detecting...";
    private string _previewStageEnglishText = "Preparing preview";
    private string _previewStageSpanishText = "Preparando vista previa";
    private AppSection _selectedAppSection = AppSection.Home;
    private SettingsSection _selectedSettingsSection = SettingsSection.VisualSettings;
    private bool _isSidebarPinnedExpanded = true;
    private bool _isSidebarHoverExpanded;
    private string? _selectedImagePath;
    private ImageMetadata? _selectedImageMetadata;
    private ImageConversionMode? _selectedImageConversionMode;
    private ImageConversionStep _selectedImageConversionStep = ImageConversionStep.ModeAndSource;
    private bool _isImageWorkflowChooserExpanded;
    private string _selectedParallaxDepthIntensity = "Medium";
    private string _selectedParallaxMotionDirection = "Left to right";
    private string _selectedParallaxZoomAmplitude = "Subtle";
    private string _selectedParallaxDuration = "6 seconds";
    private string _selectedParallaxSmoothing = "Enabled";
    private string _selectedParallaxLayerBehavior = "Foreground / mid / background";
    private ImageStereoOutputFormat _selectedStereoOutputFormat = ImageStereoOutputFormat.SideBySide;
    private string _selectedStereoEyeSeparation = "4.0%";
    private string _selectedStereoConvergence = "Neutral";
    private bool _imageStereoSwapEyes;
    private string _selectedStereoAnaglyphMode = "Red/Cyan";
    private bool _hasEnteredImagePreviewExportStage;
    private string _modelPackImportStatusEnglishText = "No model pack import has run yet.";
    private string _modelPackImportStatusSpanishText = "Aun no se ha importado ningun paquete de modelos.";
    private string _lastModelPackImportSummaryEnglishText = string.Empty;
    private string _lastModelPackImportSummarySpanishText = string.Empty;
    private string _globalBusyEnglishText = "Loading...";
    private string _globalBusySpanishText = "Cargando...";
    private string _completedConversionOutputPath = string.Empty;
    private ConversionExecutionResult? _completedConversionResult;
    private ConversionProgressTimingEstimate? _conversionTimingEstimate;
    private ProcessMetricSample? _lastProcessMetricSample;
    private ProcessMetricSample? _lastPreviewMetricSample;
    private PreviewCachePaths? _lastPreviewCachePaths;
    private TaskCompletionSource<bool>? _replaceVideoConfirmationCompletion;
    private TaskCompletionSource<bool>? _previewInvalidationConfirmationCompletion;
    private Action? _pendingPreviewInvalidatingChange;
    private TaskCompletionSource<bool>? _modelPackImportConfirmationCompletion;
    private ModelPackImportConfirmationPrompt? _modelPackImportConfirmationPrompt;
    private SettingsSection? _settingsSectionToRestoreAfterChildModal;
    private bool _reopenModelInventoryAfterImport;
    private CancellationTokenSource? _conversionCancellationTokenSource;
    private CancellationTokenSource? _previewCancellationTokenSource;
    private CancellationTokenSource? _logCopyNotificationCancellationTokenSource;
    private TopToastKind _activeTopToastKind;
    private bool _isUpdatingOutputPathText;
    private bool _isAnalyzing;
    private bool _isTechnicalDetailsModalOpen;
    private bool _isProfileDetailsModalOpen;
    private bool _isModelHelpModalOpen;
    private bool _isSettingsModalOpen;
    private bool _isReplaceVideoConfirmationModalOpen;
    private bool _isPreviewInvalidationConfirmationModalOpen;
    private bool _isModelPackImportConfirmationModalOpen;
    private bool _isModelInventoryModalOpen;
    private bool _isActivityLogModalOpen;
    private bool _isPreviewGeneratingModalOpen;
    private bool _isPreviewReadyModalOpen;
    private bool _isConversionCompletedModalOpen;
    private bool _isLogCopyNotificationVisible;
    private bool _hasLiveConversionOutput;
    private bool _openOutputWhenFinished;
    private bool _hasUserEditedPreviewRange;
    private bool _hasLoggedPreviewOfflineDependencyWarning;
    private bool _hasLoggedConversionOfflineDependencyWarning;
    private bool _isPreviewCancellationRequested;
    private bool _hasLoggedPreviewCancellationSummary;
    private bool _isApplyingUiOnlyRefresh;
    private bool _isApplyingPreviewInvalidatingChange;
    private bool _isModelPackImportRunning;
    private bool _isGlobalBusyOverlayVisible;
    private bool _hasEnteredPreviewConversionStage;
    private int _previewProgressPercent;

    public MainWindowViewModel()
    {
        _toolPaths = new InternalToolPathResolver(AppContext.BaseDirectory).Resolve();
        _healthChecker = new InternalToolsHealthChecker();
        _themeService = new AppThemeService();
        _videoAnalysisService = new FfprobeVideoAnalysisService(
            _toolPaths,
            new BundledLocalProcessRunner(
                new LocalProcessRunner(),
                Path.GetDirectoryName(_toolPaths.FfprobeExecutable)),
            new FfprobeJsonParser());
        _recommendationService = new VideoConversionRecommendationService();
        _conversionPlanService = new VideoConversionPlanService();
        _conversionReadinessService = new ConversionReadinessService();
        _iw3ConversionReadinessService = new Iw3ConversionReadinessService();
        _conversionExecutionFeatureGate = new ConversionExecutionFeatureGate();
        _conversionExecutionRequestFactory = new ConversionExecutionRequestFactory();
        _performanceHistoryStore = new FileSystemConversionPerformanceHistoryStore();
        LoadPerformanceHistory();
        _conversionExecutor = new LocalIw3ConversionExecutor(
            processRunner: new BundledLocalProcessRunner(
                new LocalProcessRunner(),
                _toolPaths.Iw3EngineDirectory));
        _conversionOutputOpenService = new(
            new FileSystemConversionOutputFileService(),
            new ShellOutputFileOpenService());
        var previewCacheFileService = new FileSystemPreviewCacheFileService();
        _previewCacheFileService = previewCacheFileService;
        _previewCachePathProvider = new LocalAppDataPreviewCachePathProvider();
        _previewCachePathService = new PreviewCachePathService(_previewCachePathProvider);
        _previewCacheCleaner = new PreviewCacheCleaner(previewCacheFileService);
        _previewExecutor = new LocalIw3PreviewExecutor(
            processRunner: new BundledLocalProcessRunner(new LocalProcessRunner()),
            fileService: previewCacheFileService);
        _previewOutputOpenService = new(
            new FileSystemConversionOutputFileService(),
            new ShellOutputFileOpenService());
        _modelPackImportCoordinator = new ModelPackAppImportCoordinator(
            new ModelPackFilePicker(() => IsSpanish),
            confirmationService: new InAppModelPackImportConfirmationService(ConfirmModelPackImportAsync));

        SelectVideoCommand = new RelayCommand(
            SelectVideo,
            () => !IsConversionRunning && !IsPreviewGenerating && !IsModelPackImportRunning);
        AnalyzeCommand = new AsyncRelayCommand(
            AnalyzeAsync,
            () => !IsAnalyzing && !IsConversionRunning && !IsPreviewGenerating && !IsModelPackImportRunning);
        RefreshEngineStatusCommand = new AsyncRelayCommand(
            () => RefreshEngineStatusWithGlobalBusyAsync(logRefresh: true),
            () => CanUseSystemStatusActions);
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
        SelectHomeSectionCommand = new RelayCommand(() => SelectAppSection(AppSection.Home));
        SelectImageConversionSectionCommand = new RelayCommand(() => SelectAppSection(AppSection.ImageConversion));
        SelectVideoConversionSectionCommand = new RelayCommand(() => SelectAppSection(AppSection.VideoConversion));
        SelectImageParallaxModeCommand = new RelayCommand(() => SelectImageConversionMode(ImageConversionMode.ParallaxPhoto));
        SelectImageStereoModeCommand = new RelayCommand(() => SelectImageConversionMode(ImageConversionMode.StereoscopicImage));
        ToggleImageWorkflowChooserCommand = new RelayCommand(ToggleImageWorkflowChooser);
        SelectImageModeSourceStepCommand = new RelayCommand(() => SelectImageConversionStep(ImageConversionStep.ModeAndSource));
        SelectImageSetupStepCommand = new RelayCommand(
            () => SelectImageConversionStep(ImageConversionStep.Setup),
            () => CanOpenImageSetupStep);
        SelectImagePreviewExportStepCommand = new RelayCommand(
            () => SelectImageConversionStep(ImageConversionStep.PreviewAndExport),
            () => CanOpenImagePreviewExportStep);
        SelectImageCommand = new RelayCommand(SelectImage);
        AnalyzeImageCommand = new RelayCommand(AnalyzeImage, () => CanAnalyzeImage);
        ClearImageLogCommand = new RelayCommand(ClearImageLog, () => ImageLogs.Count > 0);
        ImageWizardBackCommand = new RelayCommand(
            MoveImageWizardBack,
            () => CanMoveImageWizardBack);
        ImageWizardNextCommand = new RelayCommand(
            MoveImageWizardNext,
            () => CanMoveImageWizardNext);
        ContinueWithImageConversionCommand = new RelayCommand(
            ContinueWithImageConversion,
            () => CanContinueWithImageConversion);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        OpenToolsEngineSettingsCommand = new RelayCommand(OpenToolsEngineSettings);
        CloseSettingsCommand = new RelayCommand(CloseSettings);
        WizardBackCommand = new RelayCommand(
            MoveWizardBack,
            () => CanMoveWizardBack);
        WizardNextCommand = new RelayCommand(
            MoveWizardNext,
            () => CanMoveWizardNext);
        ContinueWithConversionCommand = new RelayCommand(
            ContinueWithConversion,
            () => CanEnterPreviewConversionStage);
        OpenEngineFolderCommand = new RelayCommand(OpenEngineFolder);
        OpenModelsFolderCommand = new RelayCommand(OpenModelsFolder);
        ShowModelInventoryCommand = new RelayCommand(
            ShowModelInventory,
            () => CanUseSystemStatusActions);
        CloseModelInventoryCommand = new RelayCommand(CloseModelInventory);
        ShowModelHelpCommand = new RelayCommand(
            ShowModelHelp,
            () => CanShowModelHelp);
        CloseModelHelpCommand = new RelayCommand(CloseModelHelp);
        ClearLogsCommand = new RelayCommand(ClearLogs);
        BrowseOutputFolderCommand = new RelayCommand(
            BrowseOutputFolder,
            () => !IsConversionRunning && !IsPreviewGenerating && !IsModelPackImportRunning);
        ResetOutputPathCommand = new RelayCommand(
            ResetOutputPath,
            () => !IsConversionRunning && !IsPreviewGenerating && !IsModelPackImportRunning);
        StartConversionCommand = new RelayCommand(
            StartOrCancelConversion,
            () => CanStartOrCancelConversion);
        CancelConversionCommand = new RelayCommand(CancelConversion);
        GeneratePreviewCommand = new AsyncRelayCommand(
            GeneratePreviewAsync,
            () => CanGeneratePreview);
        CancelPreviewCommand = new RelayCommand(CancelPreview, () => CanCancelPreview);
        OpenPreviewCommand = new RelayCommand(OpenPreview, () => CanOpenPreview);
        DeletePreviewCommand = new RelayCommand(DeletePreview, () => CanDeletePreview);
        ContinuePreviewCommand = new RelayCommand(ContinuePreview, () => CanContinuePreview);
        ViewActivityLogCommand = new RelayCommand(ViewActivityLog);
        ViewImageActivityLogCommand = new RelayCommand(ViewImageActivityLog);
        CopyFullLogCommand = new RelayCommand(CopyFullLog);
        CopyPreviewLogCommand = new RelayCommand(CopyPreviewLog);
        CloseActivityLogCommand = new RelayCommand(CloseActivityLog);
        ShowTechnicalDetailsCommand = new RelayCommand(
            ShowTechnicalDetails,
            () => CanUseSystemStatusActions);
        CloseTechnicalDetailsCommand = new RelayCommand(CloseTechnicalDetails);
        ShowProfileDetailsCommand = new RelayCommand(
            ShowProfileDetails,
            () => !IsConversionRunning && !IsPreviewGenerating && !IsModelPackImportRunning);
        CloseProfileDetailsCommand = new RelayCommand(CloseProfileDetails);
        ImportModelPackCommand = new AsyncRelayCommand(
            ImportModelPackAsync,
            () => CanImportModelPack);
        ConfirmModelPackImportCommand = new RelayCommand(ConfirmModelPackImport);
        CancelModelPackImportCommand = new RelayCommand(CancelModelPackImport);
        ConfirmReplaceVideoCommand = new RelayCommand(ConfirmReplaceVideo);
        CancelReplaceVideoCommand = new RelayCommand(CancelReplaceVideo);
        ConfirmPreviewInvalidationCommand = new RelayCommand(ConfirmPreviewInvalidation);
        CancelPreviewInvalidationCommand = new RelayCommand(CancelPreviewInvalidation);
        AcceptConversionCompletedCommand = new RelayCommand(AcceptConversionCompleted);

        _themeService.Apply(_selectedTheme);
        _ = CleanStalePreviewFilesAsync();
        _ = RefreshEngineStatusAsync(logRefresh: true);
        AddLog(
            "Application shell ready. Select a video to begin.",
            "La aplicación está lista. Selecciona un video para comenzar.");
        AddImageLog(
            "Image workflow ready. Select an image to begin.",
            "Flujo de imagen listo. Selecciona una imagen para comenzar.");
    }

    public string AppTitle => "v3dfy";

    public string ShellTaglineText => Text("local 2D to 3D", "2D a 3D local");

    public bool IsSidebarExpanded
    {
        get => _isSidebarPinnedExpanded;
        private set
        {
            if (SetProperty(ref _isSidebarPinnedExpanded, value))
            {
                RaiseSidebarPropertiesChanged();
            }
        }
    }

    public bool IsSidebarPinnedExpanded => IsSidebarExpanded;

    public bool IsSidebarHoverExpanded
    {
        get => _isSidebarHoverExpanded;
        private set
        {
            if (SetProperty(ref _isSidebarHoverExpanded, value))
            {
                RaiseSidebarPropertiesChanged();
            }
        }
    }

    public bool IsSidebarEffectivelyExpanded => IsSidebarPinnedExpanded || IsSidebarHoverExpanded;

    public double SidebarExpandedWidth => 208d;

    public double SidebarCollapsedWidth => 64d;

    public double SidebarTargetWidth => IsSidebarEffectivelyExpanded ? SidebarExpandedWidth : SidebarCollapsedWidth;

    public GridLength SidebarColumnWidth =>
        new(SidebarTargetWidth);

    public Thickness SidebarPadding =>
        IsSidebarEffectivelyExpanded ? new Thickness(14d, 18d, 14d, 18d) : new Thickness(6d, 18d, 6d, 18d);

    public Visibility SidebarExpandedContentVisibility =>
        IsSidebarEffectivelyExpanded ? Visibility.Visible : Visibility.Collapsed;

    public System.Windows.HorizontalAlignment SidebarNavContentHorizontalAlignment =>
        IsSidebarEffectivelyExpanded ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Center;

    public string SidebarToggleGlyphText => IsSidebarExpanded ? "\uE72B" : "\uE72A";

    public string SidebarToggleText => Text(
        IsSidebarExpanded ? "Collapse sidebar" : "Expand sidebar",
        IsSidebarExpanded ? "Contraer barra lateral" : "Expandir barra lateral");

    public string SidebarToggleToolTipText => SidebarToggleText;

    public AppSection SelectedAppSection
    {
        get => _selectedAppSection;
        private set
        {
            if (SetProperty(ref _selectedAppSection, value))
            {
                RaiseAppSectionPropertiesChanged();
            }
        }
    }

    public bool IsHomeSectionSelected => SelectedAppSection == AppSection.Home;

    public bool IsImageConversionSectionSelected => SelectedAppSection == AppSection.ImageConversion;

    public bool IsVideoConversionSectionSelected => SelectedAppSection == AppSection.VideoConversion;

    public Visibility HomeSectionVisibility =>
        IsHomeSectionSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ImageConversionSectionVisibility =>
        IsImageConversionSectionSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility VideoConversionSectionVisibility =>
        IsVideoConversionSectionSelected ? Visibility.Visible : Visibility.Collapsed;

    public string HomeNavigationText => Text("Home", "Inicio");

    public string ImageConversionNavigationText => Text("Image conversion", "Conversion de imagen");

    public string VideoConversionNavigationText => Text("Video conversion", "Conversion de video");

    public string HomeTitleText => Text("Welcome to v3dfy", "Bienvenido a v3dfy");

    public string HomeDescriptionText => Text(
        "Local/offline tools for creating 3D media from existing 2D video and image sources.",
        "Herramientas locales/offline para crear medios 3D desde fuentes 2D de video e imagen.");

    public string HomeVideoCardTitleText => Text("Convert videos to 3D", "Convertir videos a 3D");

    public string HomeVideoCardBodyText => Text(
        "Ready now: analyze a video, choose the LG 3D preset, generate a preview, then convert locally.",
        "Listo ahora: analiza un video, elige el perfil LG 3D, genera una vista previa y convierte localmente.");

    public string HomeImageCardTitleText => Text("Convert images to 3D", "Convertir imagenes a 3D");

    public string HomeImageCardBodyText => Text(
        "Coming next: image depth, parallax motion, and 3D still-output formats.",
        "Proximamente: profundidad de imagen, movimiento parallax y formatos 3D para imagen fija.");

    public string HomeSettingsCardTitleText => Text("Manage models/settings", "Administrar modelos/ajustes");

    public string HomeSettingsCardBodyText => Text(
        "Check local tools, models, diagnostics, language, theme, and licenses.",
        "Revisa herramientas locales, modelos, diagnostico, idioma, tema y licencias.");

    public string HomeStatusSummaryText => Text(
        "Video conversion is ready. Image conversion scaffolding is prepared for the next engine work.",
        "La conversion de video esta lista. El scaffolding de imagen queda preparado para el siguiente trabajo de motor.");

    public string HomeLocalOnlyBadgeText => Text("Local/offline", "Local/offline");

    public string HomeVideoStatusText => Text("Video workflow ready", "Flujo de video listo");

    public string HomeImageStatusText => Text("Image module scaffold", "Scaffold de imagen");

    public string HomeModelsStatusText => Text("Models and tools", "Modelos y herramientas");

    public string OpenSectionText => Text("Open", "Abrir");

    public string ReadyNowText => Text("Ready now", "Listo ahora");

    public string ComingNextText => Text("Coming next", "Proximamente");

    public string ImageConversionTitleText => Text("Image conversion", "Conversion de imagen");

    public string ImageConversionIntroText => Text(
        "Select an image, inspect local metadata, choose a 3D image workflow, and prepare a preview/export plan. Engine conversion is not wired yet.",
        "Selecciona una imagen, revisa metadata local, elige un flujo de imagen 3D y prepara un plan de preview/exportacion. La conversion de motor aun no esta conectada.");

    public string ImageParallaxModeTitleText => Text("2.5D Photo", "Foto 2.5D");

    public string ImageParallaxModeBodyText => Text(
        "Planned: generate a depth/parallax effect with configurable depth, movement, zoom, direction, duration, smoothing, layers, and export options.",
        "Planeado: generar un efecto de profundidad/parallax con profundidad, movimiento, zoom, direccion, duracion, suavizado, capas y opciones de exportacion configurables.");

    public string Image3DOutputModeTitleText => Text("Stereoscopic image", "Imagen estereoscopica");

    public string Image3DOutputModeBodyText => Text(
        "Planned: use local depth models to create SBS, Half Top-Bottom, and Anaglyph-style image outputs.",
        "Planeado: usar modelos locales de profundidad para crear salidas de imagen SBS, Half Top-Bottom y estilo anaglifo.");

    public string DisabledPlaceholderText => Text("Disabled until implemented", "Deshabilitado hasta implementarse");

    public string ImageSourcePanelTitleText => Text("Source image", "Imagen de origen");

    public string ImageDepthPanelTitleText => Text(
        "Depth map placeholder / generated later",
        "Placeholder de mapa de profundidad / se generara despues");

    public string ImageParallaxPreviewTitleText => Text("Parallax preview", "Preview parallax");

    public string ImageParameterPanelTitleText => Text("Motion parameters", "Parametros de movimiento");

    public string ImageQuickSummaryTitleText => Text("Quick summary", "Resumen rapido");

    public string ImageScaffoldLogTitleText => Text("Module log", "Log del modulo");

    public string ImageScaffoldLogText => Text(
        "Ready for UI validation. Image selection, model inference, preview rendering, and export commands are not connected yet.",
        "Listo para validacion de UI. Seleccion de imagen, inferencia de modelo, preview y exportacion aun no estan conectados.");

    public string ImageDepthParameterText => Text("Depth: medium", "Profundidad: media");

    public string ImageMotionParameterText => Text("Movement: slow push", "Movimiento: avance suave");

    public string ImageZoomParameterText => Text("Zoom: subtle", "Zoom: sutil");

    public string ImageDirectionParameterText => Text("Direction: left to right", "Direccion: izquierda a derecha");

    public string ImageDurationParameterText => Text("Duration: 6 seconds", "Duracion: 6 segundos");

    public string ImageSmoothingParameterText => Text("Smoothing: enabled", "Suavizado: activado");

    public string ImageLayersParameterText => Text("Layers: foreground / mid / background", "Capas: primer plano / medio / fondo");

    public string ImagePreviewActionText => Text("Preview scaffold", "Scaffold de preview");

    public string ImageExportActionText => Text("Export scaffold", "Scaffold de exportacion");

    public string ImageOpenOutputFolderActionText => Text("Open output folder", "Abrir carpeta de salida");

    public string ImageNewConversionActionText => Text("New conversion", "Nueva conversion");

    public string ImageResultParallaxTitleText => Text("2.5D result/export", "Resultado/exportacion 2.5D");

    public string ImageExportOptionsTitleText => Text("Export options", "Opciones de exportacion");

    public string ImageResultSummaryTitleText => Text("Result summary", "Resumen del resultado");

    public string ImageResultSummaryText => Text(
        "Preview target: 1080p parallax video, loop-friendly motion, local depth model pending.",
        "Objetivo de preview: video parallax 1080p, movimiento para loop, modelo local pendiente.");

    public string ImageStereoPreviewTitleText => Text("Stereo preview", "Preview estereo");

    public string ImageStereoControlsTitleText => Text("Stereo controls", "Controles estereo");

    public string ImageStereoSummaryText => Text(
        "Modes planned: SBS, Half Top-Bottom, Anaglyph, and L/R pair outputs.",
        "Modos planeados: SBS, Half Top-Bottom, anaglifo y salidas par L/R.");

    public string ImageStereoSeparationText => Text("Eye separation: 4.0%", "Separacion ocular: 4.0%");

    public string ImageStereoConvergenceText => Text("Convergence: neutral", "Convergencia: neutral");

    public string ImageStereoAnaglyphText => Text("Anaglyph: red/cyan", "Anaglifo: rojo/cian");

    public string ImageStereoResultTitleText => Text("Stereoscopic results", "Resultados estereoscopicos");

    public string ImageGeneratedFilesTitleText => Text("Generated files", "Archivos generados");

    public string ImageOutputPanelTitleText => Text("Output panel", "Panel de salida");

    public string ImageComparisonTitleText => Text("Comparison views", "Vistas comparativas");

    public ImageConversionMode? SelectedImageConversionMode
    {
        get => _selectedImageConversionMode;
        private set
        {
            if (SetProperty(ref _selectedImageConversionMode, value))
            {
                RaiseImageConversionPropertiesChanged();
            }
        }
    }

    public ImageConversionStep SelectedImageConversionStep
    {
        get => _selectedImageConversionStep;
        private set
        {
            if (SetProperty(ref _selectedImageConversionStep, value))
            {
                RaiseImageConversionPropertiesChanged();
            }
        }
    }

    public bool IsImageParallaxModeSelected => SelectedImageConversionMode == ImageConversionMode.ParallaxPhoto;

    public bool IsImageStereoModeSelected => SelectedImageConversionMode == ImageConversionMode.StereoscopicImage;

    public bool HasSelectedImageMode => SelectedImageConversionMode is not null;

    public string ImageParallaxModeSelectionState => IsImageParallaxModeSelected ? "Active" : "Pending";

    public string ImageStereoModeSelectionState => IsImageStereoModeSelected ? "Active" : "Pending";

    public string ImageParallaxModeCardStatusText => IsImageParallaxModeSelected
        ? Text("Selected", "Seleccionado")
        : Text("Choose workflow", "Elegir flujo");

    public string ImageStereoModeCardStatusText => IsImageStereoModeSelected
        ? Text("Selected", "Seleccionado")
        : Text("Choose workflow", "Elegir flujo");

    public bool IsImageWorkflowChooserExpanded
    {
        get => _isImageWorkflowChooserExpanded;
        private set
        {
            if (SetProperty(ref _isImageWorkflowChooserExpanded, value))
            {
                RaiseImageConversionPropertiesChanged();
            }
        }
    }

    public Visibility ImageWorkflowSummaryVisibility =>
        SelectedImageConversionStep == ImageConversionStep.Setup && HasSelectedImageMode
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ImageWorkflowChooserVisibility =>
        SelectedImageConversionStep == ImageConversionStep.Setup &&
        (!HasSelectedImageMode || IsImageWorkflowChooserExpanded)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string ImageWorkflowChooserChevronText => IsImageWorkflowChooserExpanded ? "v" : ">";

    public string ImageWorkflowSummaryText => Text(
        $"Workflow: {SelectedImageModeName}",
        $"Flujo: {SelectedImageModeName}");

    public string ImageModeSourceStepState => ImageStepState(ImageConversionStep.ModeAndSource);

    public string ImageSetupStepState => ImageStepState(ImageConversionStep.Setup);

    public string ImagePreviewExportStepState => ImageStepState(ImageConversionStep.PreviewAndExport);

    public string ImageModeSourceStepMarkerText => ImageStepMarkerText(ImageConversionStep.ModeAndSource, "1");

    public string ImageSetupStepMarkerText => ImageStepMarkerText(ImageConversionStep.Setup, "2");

    public string ImagePreviewExportStepMarkerText => ImageStepMarkerText(ImageConversionStep.PreviewAndExport, "3");

    public Visibility ImageModeSourceStepVisibility =>
        SelectedImageConversionStep == ImageConversionStep.ModeAndSource ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ImageSetupStepVisibility =>
        SelectedImageConversionStep == ImageConversionStep.Setup ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ImageParallaxSetupStepVisibility =>
        SelectedImageConversionStep == ImageConversionStep.Setup && IsImageParallaxModeSelected
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ImageStereoSetupStepVisibility =>
        SelectedImageConversionStep == ImageConversionStep.Setup && IsImageStereoModeSelected
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ImageNoModeSetupHintVisibility =>
        SelectedImageConversionStep == ImageConversionStep.Setup && !HasSelectedImageMode
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ImageParallaxPreviewExportStepVisibility =>
        SelectedImageConversionStep == ImageConversionStep.PreviewAndExport && IsImageParallaxModeSelected
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ImageStereoPreviewExportStepVisibility =>
        SelectedImageConversionStep == ImageConversionStep.PreviewAndExport && IsImageStereoModeSelected
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string ImageModeSourceStepTitleText => Text("Source & analysis", "Origen y analisis");

    public string ImageSetupStepTitleText => Text("Setup", "Configuracion");

    public string ImagePreviewExportStepTitleText => Text("Preview & export", "Preview y exportacion");

    public string ImageSourceModeStepTitleText => Text("Source & mode", "Origen y modo");

    public string ImageModeSelectionTitleText => Text("Choose an image workflow", "Elige un flujo de imagen");

    public string ChangeImageWorkflowText => Text("Change workflow", "Cambiar flujo");

    public string ImageModeSourceBodyText => Text(
        "Select one local image first, then analyze it locally to unlock setup.",
        "Selecciona primero una imagen local y luego analizala localmente para habilitar configuracion.");

    public string ImageSourceAnalysisTitleText => Text("Source image", "Imagen de origen");

    public string DropImageText => Text(
        "Drop one image file here or browse for a file.",
        "Arrastra un archivo de imagen aqui o selecciona uno.");

    public string ImageNoModeSetupHintText => Text(
        "Choose a workflow to configure image setup.",
        "Elige un flujo para configurar la imagen.");

    public string AnalyzeImageText => Text("Analyze image", "Analizar imagen");

    public string ImageAnalysisTitleText => Text("Image analysis", "Analisis de imagen");

    public string SelectedImageModeName =>
        SelectedImageConversionMode switch
        {
            ImageConversionMode.ParallaxPhoto => ImageParallaxModeTitleText,
            ImageConversionMode.StereoscopicImage => Image3DOutputModeTitleText,
            _ => Text("No mode selected", "Sin modo seleccionado"),
        };

    public string SelectedImageStepName => SelectedImageConversionStep switch
    {
        ImageConversionStep.ModeAndSource => ImageModeSourceStepTitleText,
        ImageConversionStep.Setup => ImageSetupStepTitleText,
        ImageConversionStep.PreviewAndExport => ImagePreviewExportStepTitleText,
        _ => ImageModeSourceStepTitleText,
    };

    public string ImageSummaryStatusTitleText => Text("Image summary", "Resumen de imagen");

    public string ImageSelectedModeSummaryText => Text(
        $"Mode: {SelectedImageModeName}",
        $"Modo: {SelectedImageModeName}");

    public string ImageCurrentStepSummaryText => Text(
        $"Phase: {SelectedImageStepName}",
        $"Fase: {SelectedImageStepName}");

    public string ImageSupportedInputFormatsText => Text(
        "Supported inputs: JPG, JPEG, PNG, BMP, TIF, TIFF, WEBP",
        "Entradas soportadas: JPG, JPEG, PNG, BMP, TIF, TIFF, WEBP");

    public string ImagePlannedOutputFormatsText => IsImageParallaxModeSelected
        ? Text("Outputs planned: MP4 parallax, preview stills, project metadata", "Salidas planeadas: MP4 parallax, imagenes de preview, metadata de proyecto")
        : IsImageStereoModeSelected
            ? Text("Outputs planned: SBS, Half Top-Bottom, Anaglyph, L/R pair", "Salidas planeadas: SBS, Half Top-Bottom, anaglifo, par L/R")
            : Text("Choose a mode to see planned outputs.", "Elige un modo para ver salidas planeadas.");

    public string ImageLocalModelReadinessNoteText => Text(
        "Local depth model readiness will appear here when image processing is implemented.",
        "La disponibilidad del modelo local de profundidad aparecera aqui cuando se implemente imagen.");

    public string ImageNotImplementedStateText => Text(
        "Scaffold only - no image processing command is wired yet.",
        "Solo scaffold: aun no hay comandos de procesamiento de imagen conectados.");

    public string ImageActivityLogTitleText => Text("Image activity log", "Log de actividad de imagen");

    public string ImageActivityLogText =>
        ImageLogs.Count == 0
            ? ImageScaffoldLogText
            : string.Join(Environment.NewLine, ImageLogs.Select(log => log.DisplayText));

    public string SelectImageText => Text("Select image", "Seleccionar imagen");

    public string ImageSelectedTitleText => Text("Selected image", "Imagen seleccionada");

    public string NoImageSelectedText => Text("No image selected yet.", "Aun no hay imagen seleccionada.");

    public string SelectedImageDisplayPath => SelectedImagePath ?? NoImageSelectedText;

    public string SelectedImageFileName => HasSelectedImage
        ? Path.GetFileName(SelectedImagePath) ?? NoImageSelectedText
        : NoImageSelectedText;

    public string? SelectedImagePath
    {
        get => _selectedImagePath;
        private set
        {
            if (SetProperty(ref _selectedImagePath, value))
            {
                RaiseImageConversionPropertiesChanged();
            }
        }
    }

    public bool HasSelectedImage => !string.IsNullOrWhiteSpace(SelectedImagePath);

    public bool HasImageMetadata => _selectedImageMetadata is not null;

    public bool CanAnalyzeImage => HasSelectedImage;

    public bool HasImageWorkflowPrerequisites => HasImageMetadata && HasSelectedImageMode;

    public bool CanOpenImageSetupStep => HasImageMetadata;

    public bool IsImageModeSetupValid => IsImageParallaxModeSelected
        ? !string.IsNullOrWhiteSpace(SelectedParallaxDepthIntensity) &&
            !string.IsNullOrWhiteSpace(SelectedParallaxMotionDirection) &&
            !string.IsNullOrWhiteSpace(SelectedParallaxZoomAmplitude) &&
            !string.IsNullOrWhiteSpace(SelectedParallaxDuration) &&
            !string.IsNullOrWhiteSpace(SelectedParallaxSmoothing) &&
            !string.IsNullOrWhiteSpace(SelectedParallaxLayerBehavior)
        : IsImageStereoModeSelected &&
            !string.IsNullOrWhiteSpace(SelectedStereoEyeSeparation) &&
            !string.IsNullOrWhiteSpace(SelectedStereoConvergence) &&
            !string.IsNullOrWhiteSpace(SelectedStereoAnaglyphMode);

    public bool IsImageSetupValid => HasImageMetadata && HasSelectedImageMode && IsImageModeSetupValid;

    public bool CanOpenImagePreviewExportStep => IsImageSetupValid;

    public bool CanMoveImageWizardBack => SelectedImageConversionStep != ImageConversionStep.ModeAndSource;

    public bool CanMoveImageWizardNext => SelectedImageConversionStep switch
    {
        ImageConversionStep.ModeAndSource => CanOpenImageSetupStep,
        ImageConversionStep.Setup => CanOpenImagePreviewExportStep,
        _ => false,
    };

    public Visibility ImageWizardBackButtonVisibility =>
        CanMoveImageWizardBack ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ImageWizardNextButtonVisibility =>
        SelectedImageConversionStep == ImageConversionStep.PreviewAndExport ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ContinueWithImageConversionFooterVisibility =>
        SelectedImageConversionStep == ImageConversionStep.PreviewAndExport ? Visibility.Visible : Visibility.Collapsed;

    public bool CanContinueWithImageConversion =>
        SelectedImageConversionStep == ImageConversionStep.PreviewAndExport && IsImageSetupValid;

    public string ImageWizardNextToolTipText => CanMoveImageWizardNext
        ? string.Empty
        : SelectedImageConversionStep == ImageConversionStep.ModeAndSource
            ? Text("Select and analyze a readable image before continuing.", "Selecciona y analiza una imagen legible antes de continuar.")
            : Text("Choose an image workflow before continuing.", "Elige un flujo de imagen antes de continuar.");

    public string ContinueWithImageConversionText => Text(
        "Prepare image preview/export plan",
        "Preparar plan de preview/exportacion de imagen");

    public Visibility ImagePreviewExportStatusCardVisibility =>
        _hasEnteredImagePreviewExportStage ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ImageAnalysisResultsVisibility =>
        HasImageMetadata ? Visibility.Visible : Visibility.Collapsed;

    public bool IsSelectedImageVertical =>
        _selectedImageMetadata is { } metadata &&
        metadata.Height > metadata.Width &&
        !IsImageNearlySquare(metadata.Width, metadata.Height);

    public bool IsSelectedImageHorizontalOrSquare => !IsSelectedImageVertical;

    public Visibility ImageParallaxPreviewExportVerticalLayoutVisibility =>
        SelectedImageConversionStep == ImageConversionStep.PreviewAndExport &&
        IsImageParallaxModeSelected &&
        IsSelectedImageVertical
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ImageParallaxPreviewExportWideLayoutVisibility =>
        SelectedImageConversionStep == ImageConversionStep.PreviewAndExport &&
        IsImageParallaxModeSelected &&
        IsSelectedImageHorizontalOrSquare
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ImageStereoPreviewExportVerticalLayoutVisibility =>
        SelectedImageConversionStep == ImageConversionStep.PreviewAndExport &&
        IsImageStereoModeSelected &&
        IsSelectedImageVertical
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ImageStereoPreviewExportWideLayoutVisibility =>
        SelectedImageConversionStep == ImageConversionStep.PreviewAndExport &&
        IsImageStereoModeSelected &&
        IsSelectedImageHorizontalOrSquare
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ImageSummaryVisibility =>
        ImagePreviewExportStatusCardVisibility;

    public GridLength ImageSummaryRowHeight =>
        ImagePreviewExportStatusRowHeight;

    public GridLength ImagePreviewExportStatusRowHeight =>
        _hasEnteredImagePreviewExportStage ? GridLength.Auto : new GridLength(0d);

    public Thickness ImageActivityLogCardMargin =>
        _hasEnteredImagePreviewExportStage ? new Thickness(0d, 14d, 0d, 0d) : new Thickness(0d);

    public string ImagePreviewExportStatusTitleText => Text(
        "Image preview/export plan",
        "Plan de preview/exportacion de imagen");

    public string ImagePreviewExportStatusText => _hasEnteredImagePreviewExportStage
        ? Text("Plan prepared. Engine preview/export is not implemented yet.", "Plan preparado. Preview/exportacion con motor aun no esta implementado.")
        : Text("Prepare the plan in Step 3 to see image preview/export status.", "Prepara el plan en el paso 3 para ver el estado de preview/exportacion de imagen.");

    public string ImageSupportedExtensionsText => Text(
        ".jpg, .jpeg, .png, .bmp, .tif, .tiff, .webp",
        ".jpg, .jpeg, .png, .bmp, .tif, .tiff, .webp");

    public string ImageMetadataTitleText => Text("Image metadata", "Metadata de imagen");

    public string ImageMetadataWidthText => LabelValue("Width", "Ancho", _selectedImageMetadata?.WidthText);

    public string ImageMetadataHeightText => LabelValue("Height", "Alto", _selectedImageMetadata?.HeightText);

    public string ImageMetadataAspectRatioText => LabelValue("Aspect ratio", "Relacion de aspecto", _selectedImageMetadata?.AspectRatio);

    public string ImageMetadataFormatText => LabelValue("Format", "Formato", _selectedImageMetadata?.Format);

    public string ImageMetadataPixelFormatText => LabelValue("Pixel format", "Formato de pixel", _selectedImageMetadata?.PixelFormat);

    public string ImageMetadataFileSizeText => LabelValue("File size", "Tamano de archivo", _selectedImageMetadata?.FileSizeText);

    public string ImageMetadataSummaryText => HasImageMetadata
        ? $"{_selectedImageMetadata!.WidthText} x {_selectedImageMetadata.HeightText} · {_selectedImageMetadata.AspectRatio} · {_selectedImageMetadata.Format} · {_selectedImageMetadata.FileSizeText}"
        : Text("No image metadata yet.", "Aun no hay metadata de imagen.");

    public string ImageSourceStatusText => HasImageMetadata
        ? Text("Image metadata analyzed locally.", "Metadata de imagen analizada localmente.")
        : Text("Select a supported image to unlock setup.", "Selecciona una imagen compatible para habilitar configuracion.");

    public string ImageOutputPlanText => IsImageParallaxModeSelected
        ? Text("Planned output: parallax preview/export next to the source image. Engine export is disabled.", "Salida planeada: preview/exportacion parallax junto a la imagen de origen. Exportacion de motor deshabilitada.")
        : IsImageStereoModeSelected
            ? Text("Planned output: stereoscopic still formats next to the source image. Engine export is disabled.", "Salida planeada: formatos estereoscopicos de imagen fija junto a la imagen de origen. Exportacion de motor deshabilitada.")
            : Text("Choose an image workflow to prepare an output plan.", "Elige un flujo de imagen para preparar un plan de salida.");

    public string ImageSetupSummaryText => IsImageParallaxModeSelected
        ? Text(
            $"2.5D setup: {SelectedParallaxDepthIntensity}, {SelectedParallaxMotionDirection}, {SelectedParallaxZoomAmplitude}, {SelectedParallaxDuration}.",
            $"Configuracion 2.5D: {SelectedParallaxDepthIntensity}, {SelectedParallaxMotionDirection}, {SelectedParallaxZoomAmplitude}, {SelectedParallaxDuration}.")
        : IsImageStereoModeSelected
            ? Text(
                $"Stereo setup: {SelectedStereoOutputFormatDisplayText}, separation {SelectedStereoEyeSeparation}, convergence {SelectedStereoConvergence}.",
                $"Configuracion estereo: {SelectedStereoOutputFormatDisplayText}, separacion {SelectedStereoEyeSeparation}, convergencia {SelectedStereoConvergence}.")
            : Text("Select a workflow mode before configuring image setup.", "Selecciona un modo de flujo antes de configurar imagen.");

    public string ImageDepthIntensityLabelText => Text("Depth intensity", "Intensidad de profundidad");

    public string ImageMotionDirectionLabelText => Text("Motion direction", "Direccion de movimiento");

    public string ImageZoomAmplitudeLabelText => Text("Zoom/amplitude", "Zoom/amplitud");

    public string ImageDurationLabelText => Text("Duration", "Duracion");

    public string ImageSmoothingLabelText => Text("Smoothing", "Suavizado");

    public string ImageLayerBehaviorLabelText => Text("Layer/depth behavior", "Comportamiento de capas/profundidad");

    public string ImageStereoOutputFormatLabelText => Text("Output format", "Formato de salida");

    public string ImageStereoEyeSeparationLabelText => Text("Eye separation", "Separacion ocular");

    public string ImageStereoConvergenceLabelText => Text("Convergence", "Convergencia");

    public string ImageStereoSwapEyesLabelText => Text("Swap eyes", "Intercambiar ojos");

    public string ImageStereoAnaglyphModeLabelText => Text("Anaglyph mode", "Modo anaglifo");

    public IReadOnlyList<string> ParallaxDepthIntensityOptions { get; } = ["Low", "Medium", "High"];

    public IReadOnlyList<string> ParallaxMotionDirectionOptions { get; } =
        ["Left to right", "Right to left", "Push in", "Pull back", "Orbit"];

    public IReadOnlyList<string> ParallaxZoomAmplitudeOptions { get; } = ["Subtle", "Medium", "Strong"];

    public IReadOnlyList<string> ParallaxDurationOptions { get; } = ["4 seconds", "6 seconds", "8 seconds", "12 seconds"];

    public IReadOnlyList<string> ParallaxSmoothingOptions { get; } = ["Enabled", "Balanced", "Strong"];

    public IReadOnlyList<string> ParallaxLayerBehaviorOptions { get; } =
        ["Foreground / mid / background", "Depth slices", "Soft depth ramp"];

    public IReadOnlyList<LocalizedOptionViewModel<ImageStereoOutputFormat>> StereoOutputFormatOptions { get; } =
    [
        new(ImageStereoOutputFormat.SideBySide, "SBS", "SBS"),
        new(ImageStereoOutputFormat.HalfTopBottom, "Half Top-Bottom / TAB", "Half Top-Bottom / TAB"),
        new(ImageStereoOutputFormat.Anaglyph, "Anaglyph", "Anaglifo"),
        new(ImageStereoOutputFormat.LeftRightPair, "L/R pair", "Par L/R"),
    ];

    public IReadOnlyList<string> StereoEyeSeparationOptions { get; } = ["2.0%", "4.0%", "6.0%", "8.0%"];

    public IReadOnlyList<string> StereoConvergenceOptions { get; } = ["Near", "Neutral", "Far"];

    public IReadOnlyList<string> StereoAnaglyphModeOptions { get; } = ["Red/Cyan", "Green/Magenta", "Amber/Blue"];

    public string SelectedParallaxDepthIntensity
    {
        get => _selectedParallaxDepthIntensity;
        set => SetImageSetupString(ref _selectedParallaxDepthIntensity, value);
    }

    public string SelectedParallaxMotionDirection
    {
        get => _selectedParallaxMotionDirection;
        set => SetImageSetupString(ref _selectedParallaxMotionDirection, value);
    }

    public string SelectedParallaxZoomAmplitude
    {
        get => _selectedParallaxZoomAmplitude;
        set => SetImageSetupString(ref _selectedParallaxZoomAmplitude, value);
    }

    public string SelectedParallaxDuration
    {
        get => _selectedParallaxDuration;
        set => SetImageSetupString(ref _selectedParallaxDuration, value);
    }

    public string SelectedParallaxSmoothing
    {
        get => _selectedParallaxSmoothing;
        set => SetImageSetupString(ref _selectedParallaxSmoothing, value);
    }

    public string SelectedParallaxLayerBehavior
    {
        get => _selectedParallaxLayerBehavior;
        set => SetImageSetupString(ref _selectedParallaxLayerBehavior, value);
    }

    public ImageStereoOutputFormat SelectedStereoOutputFormat
    {
        get => _selectedStereoOutputFormat;
        set
        {
            if (SetProperty(ref _selectedStereoOutputFormat, value))
            {
                ApplyImageSetupChanged();
            }
        }
    }

    public string SelectedStereoOutputFormatDisplayText =>
        StereoOutputFormatOptions.First(option => option.Value == SelectedStereoOutputFormat).DisplayName;

    public string SelectedStereoEyeSeparation
    {
        get => _selectedStereoEyeSeparation;
        set => SetImageSetupString(ref _selectedStereoEyeSeparation, value);
    }

    public string SelectedStereoConvergence
    {
        get => _selectedStereoConvergence;
        set => SetImageSetupString(ref _selectedStereoConvergence, value);
    }

    public bool ImageStereoSwapEyes
    {
        get => _imageStereoSwapEyes;
        set
        {
            if (SetProperty(ref _imageStereoSwapEyes, value))
            {
                ApplyImageSetupChanged();
            }
        }
    }

    public string SelectedStereoAnaglyphMode
    {
        get => _selectedStereoAnaglyphMode;
        set => SetImageSetupString(ref _selectedStereoAnaglyphMode, value);
    }

    public string? SelectedVideoPath
    {
        get => _selectedVideoPath;
        private set
        {
            if (SetProperty(ref _selectedVideoPath, value))
            {
                OnPropertyChanged(nameof(SelectedVideoDisplayPath));
                OnPropertyChanged(nameof(HasSelectedVideo));
                OnPropertyChanged(nameof(SourceAnalysisEmptyHintVisibility));
                OnPropertyChanged(nameof(VideoAnalysisSectionVisibility));
                OnPropertyChanged(nameof(VideoAnalysisPendingStatusVisibility));
                OnPropertyChanged(nameof(ConversionReadinessVisibility));
            }
        }
    }

    public string SelectedVideoDisplayPath => SelectedVideoPath ?? NoVideoSelectedText;

    public bool HasSelectedVideo => !string.IsNullOrWhiteSpace(SelectedVideoPath);

    public bool HasCompletedAnalysis
    {
        get => _workflowState.HasCompletedAnalysis;
        private set
        {
            if (_workflowState.SetHasCompletedAnalysis(value, out var selectedTabIndexChanged))
            {
                if (value && _conversionPlan is not null)
                {
                    _workflowState.SetCanOpenConversionPlanStep(true, out _);
                }

                OnPropertyChanged();
                if (selectedTabIndexChanged)
                {
                    OnPropertyChanged(nameof(SelectedWizardStepIndex));
                }

                RaiseWorkflowAvailabilityPropertiesChanged();
                RaiseConversionReadinessPropertiesChanged();
                RaisePreviewPropertiesChanged();
            }
        }
    }

    public IReadOnlyList<string> LanguageOptions { get; } = ["Español", "English"];

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                ApplyUiOnlyRefresh(() =>
                {
                    RaiseLocalizedPropertiesChanged();
                    UpdateToolStatuses();
                    UpdatePlanOptionLanguages();
                    UpdateLocalModelSelectionCandidates(regenerateCurrentPlan: false);
                    UpdateLogLanguages();
                    RefreshMetricLanguage();
                });
                AddLog("Language selected: English.", "Idioma seleccionado: Español.");
            }
        }
    }

    public IReadOnlyList<string> ThemeOptions { get; } = ["Dark", "Light"];

    public IReadOnlyList<LocalizedOptionViewModel<SettingsSection>> SettingsSectionOptions { get; } =
    [
        new(SettingsSection.VisualSettings, "Visual settings", "Ajustes visuales"),
        new(SettingsSection.Models, "Models", "Modelos"),
        new(SettingsSection.ToolsEngine, "Tools & Engine", "Herramientas y motor"),
        new(SettingsSection.LogsDiagnostics, "Logs & Diagnostics", "Logs y diagnostico"),
        new(SettingsSection.AboutLicenses, "About / Licenses", "Acerca de / licencias"),
    ];

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                ApplyUiOnlyRefresh(() => _themeService.Apply(value));
                AddLog($"Theme selected: {value}.", $"Tema seleccionado: {value}.");
            }
        }
    }

    public string SubtitleText => Text(
        "Local offline 2D to 3D video conversion",
        "Conversión local offline de video 2D a 3D");

    public string LanguageLabel => Text("Language", "Idioma");

    public string ThemeLabel => Text("Theme", "Tema");

    public string SettingsText => Text("Settings", "Ajustes");

    public string SettingsTitleText => Text("Settings", "Ajustes");

    public string SettingsSideMenuTitleText => Text("Settings", "Ajustes");

    public string WindowMinimizeToolTipText => Text("Minimize", "Minimizar");

    public string WindowMaximizeToolTipText => Text("Maximize", "Maximizar");

    public string WindowRestoreToolTipText => Text("Restore", "Restaurar");

    public string WindowCloseToolTipText => Text("Close", "Cerrar");

    public SettingsSection SelectedSettingsSection
    {
        get => _selectedSettingsSection;
        set
        {
            if (SetProperty(ref _selectedSettingsSection, value))
            {
                RaiseSettingsSectionPropertiesChanged();
            }
        }
    }

    public Visibility VisualSettingsSectionVisibility =>
        SelectedSettingsSection == SettingsSection.VisualSettings ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ModelsSettingsSectionVisibility =>
        SelectedSettingsSection == SettingsSection.Models ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ToolsEngineSettingsSectionVisibility =>
        SelectedSettingsSection == SettingsSection.ToolsEngine ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LogsDiagnosticsSettingsSectionVisibility =>
        SelectedSettingsSection == SettingsSection.LogsDiagnostics ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AboutLicensesSettingsSectionVisibility =>
        SelectedSettingsSection == SettingsSection.AboutLicenses ? Visibility.Visible : Visibility.Collapsed;

    public string VisualSettingsTitleText => Text("Visual settings", "Ajustes visuales");

    public string ModelsSettingsTitleText => Text("Models", "Modelos");

    public string ToolsEngineSettingsTitleText => Text(
        "Tools & Engine",
        "Herramientas y motor");

    public string LogsDiagnosticsSettingsTitleText => Text(
        "Logs & Diagnostics",
        "Logs y diagnostico");

    public string AboutLicensesSettingsTitleText => Text(
        "About / Licenses",
        "Acerca de / licencias");

    public string ModelsSettingsIntroText => Text(
        "Review detected local models or import a reviewed model pack.",
        "Revisa modelos locales detectados o importa un paquete de modelos revisado.");

    public string ToolsEngineSettingsIntroText => Text(
        "Local tool and engine status for the published runtime layout.",
        "Estado de herramientas y motor locales para el layout publicado.");

    public string LogsDiagnosticsSettingsIntroText => Text(
        "Technical details for local tools, model inventory, and conversion readiness.",
        "Detalles tecnicos de herramientas locales, inventario de modelos y estado de conversion.");

    public string AboutLicensesText => Text(
        $"v3dfy version: {GetCurrentV3dfyVersion()}{Environment.NewLine}Licenses and notices are bundled under licenses/ when release payloads are prepared. Review docs/legal-notes.md before distributing an installer.",
        $"Version de v3dfy: {GetCurrentV3dfyVersion()}{Environment.NewLine}Las licencias y avisos se incluyen en licenses/ cuando se preparan los paquetes de release. Revisa docs/legal-notes.md antes de distribuir un instalador.");

    public string AboutModelNoticesTitleText => Text("Model notices", "Avisos de modelos");

    public string AboutModelNoticesText => CreateModelLicenseNoticeSummaryText();

    public string SourceAndAnalysisStepTitle => Text(
        "Source & analysis",
        "Fuente y analisis");

    public string ThreeDSetupStepTitle => Text(
        "3D setup",
        "Configuracion 3D");

    public string WizardConversionPlanStepTitle => ConversionPlanTitle;

    public int SelectedWizardStepIndex
    {
        get => _workflowState.SelectedStepIndex;
        set
        {
            if (_workflowState.SetSelectedStepIndex(value))
            {
                RaiseWizardPropertiesChanged();
            }
        }
    }

    public bool CanOpenSourceAndAnalysisStep => true;

    public bool CanOpenThreeDSetupStep => _workflowState.CanOpenThreeDSetupStep;

    public bool CanOpenConversionPlanStep => _workflowState.CanOpenConversionPlanStep;

    public bool CanMoveWizardBack => _workflowState.CanGoBack;

    public bool CanMoveWizardNext => _workflowState.CanGoNext;

    public Visibility WizardBackButtonVisibility =>
        CanMoveWizardBack ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WizardNextButtonVisibility =>
        SelectedWizardStepIndex == ConversionWorkflowState.ConversionPlanStepIndex
            ? Visibility.Collapsed
            : Visibility.Visible;

    public string WizardBackText => Text("Back", "Atras");

    public string WizardNextText => Text("Next", "Siguiente");

    public string WizardNextToolTipText => CanMoveWizardNext
        ? string.Empty
        : SelectedWizardStepIndex switch
        {
            ConversionWorkflowState.SourceAndAnalysisStepIndex => Text(
                "Analyze a video before continuing.",
                "Analiza un video antes de continuar."),
            ConversionWorkflowState.ThreeDSetupStepIndex => Text(
                "Complete a valid 3D setup before opening the conversion plan.",
                "Completa una configuracion 3D valida antes de abrir el plan de conversion."),
            _ => string.Empty,
        };

    public Visibility SourceAndAnalysisStepVisibility =>
        _workflowState.IsSourceAndAnalysisStepSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ThreeDSetupStepVisibility =>
        _workflowState.IsThreeDSetupStepSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WizardConversionPlanStepVisibility =>
        _workflowState.IsConversionPlanStepSelected ? Visibility.Visible : Visibility.Collapsed;

    public string SourceAndAnalysisStepState => GetWizardStepState(ConversionWorkflowState.SourceAndAnalysisStepIndex);

    public string ThreeDSetupStepState => GetWizardStepState(ConversionWorkflowState.ThreeDSetupStepIndex);

    public string ConversionPlanStepState => GetWizardStepState(ConversionWorkflowState.ConversionPlanStepIndex);

    public string SourceAndAnalysisStepMarkerText =>
        SelectedWizardStepIndex > ConversionWorkflowState.SourceAndAnalysisStepIndex ? "\u2713" : "1";

    public string ThreeDSetupStepMarkerText =>
        SelectedWizardStepIndex > ConversionWorkflowState.ThreeDSetupStepIndex ? "\u2713" : "2";

    public string ConversionPlanStepMarkerText => "3";

    public string SelectSourceTitle => Text(
        "Source video",
        "Video de origen");

    public string DropVideoText => Text(
        "Drop one video file here or browse for a file.",
        "Arrastra un archivo de video aquí o selecciona uno.");

    public string NoVideoSelectedText => Text(
        "No video selected yet.",
        "Aún no hay video seleccionado.");

    public string SourceAnalysisEmptyHintText => Text(
        "Select a source video to analyze its duration, resolution, streams, and HDR metadata.",
        "Selecciona un video fuente para analizar duracion, resolucion, pistas y metadatos HDR.");

    public Visibility SourceAnalysisEmptyHintVisibility =>
        HasSelectedVideo ? Visibility.Collapsed : Visibility.Visible;

    public string SelectVideoText => Text("Select video", "Seleccionar video");

    public string AnalyzeText => Text("Analyze", "Analizar");

    public IReadOnlyList<LocalizedOptionViewModel<TargetDevicePreset>> OutputPresetOptions { get; } =
    [
        new(TargetDevicePresets.Recommended3dTv, "Recommended 3D TV", "TV 3D recomendada"),
        new(TargetDevicePresets.MaximumCompatibility, "Maximum Compatibility", "Maxima compatibilidad"),
        new(TargetDevicePresets.HighQualityMaster, "High Quality Master", "Master de alta calidad"),
        new(TargetDevicePresets.Lg3dFullHd2012, "Legacy LG 3D TV (2012)", "Legacy LG 3D TV (2012)"),
    ];

    public TargetDevicePreset SelectedOutputPreset
    {
        get => _selectedOutputPreset;
        set
        {
            if (ReferenceEquals(_selectedOutputPreset, value))
            {
                return;
            }

            if (TryDeferPreviewInvalidatingChange(() => SelectedOutputPreset = value))
            {
                OnPropertyChanged();
                return;
            }

            if (SetProperty(ref _selectedOutputPreset, value))
            {
                ResetPreviewConversionStageForPreviewAffectingChange();
                ApplyPresetDefaults(value);
                RegenerateRecommendationAndPlan();
                RaisePresetPropertiesChanged();
                RaiseAnalysisPropertiesChanged();
                AddLog(
                    $"Output profile changed to {value.Name}.",
                    $"Perfil de salida cambiado a {value.SpanishName}.");
            }
        }
    }

    public IReadOnlyList<LocalizedOptionViewModel<OutputContainer>> OutputContainerOptions { get; } =
    [
        new(OutputContainer.MP4, "MP4", "MP4"),
        new(OutputContainer.MKV, "MKV", "MKV"),
    ];

    public IReadOnlyList<LocalizedOptionViewModel<AiQualityPreset>> QualityPresetOptions { get; } =
    [
        new(AiQualityPreset.Fast, "Fast", "Rápida"),
        new(AiQualityPreset.Balanced, "Balanced", "Equilibrada"),
        new(AiQualityPreset.HighQuality, "High quality", "Alta calidad"),
    ];

    public IReadOnlyList<LocalizedOptionViewModel<ThreeDIntensity>> ThreeDIntensityOptions { get; } =
    [
        new(ThreeDIntensity.Low, "Low", "Baja"),
        new(ThreeDIntensity.Medium, "Medium", "Media"),
        new(ThreeDIntensity.High, "High", "Alta"),
    ];

    public IReadOnlyList<LocalizedOptionViewModel<ThreeDOutputFormat>> ThreeDOutputFormatOptions { get; } =
    [
        new(ThreeDOutputFormat.HalfTopBottom, "Half Top-Bottom", "Medio arriba-abajo"),
        new(ThreeDOutputFormat.HalfSideBySide, "Half Side-by-Side", "Medio lado a lado"),
        new(ThreeDOutputFormat.Anaglyph, "Anaglyph", "Anaglifo"),
    ];

    public OutputContainer SelectedOutputContainer
    {
        get => _planOptionState.OutputContainer;
        set
        {
            if (_planOptionState.OutputContainer == value)
            {
                return;
            }

            if (TryDeferPreviewInvalidatingChange(() => SelectedOutputContainer = value))
            {
                OnPropertyChanged();
                return;
            }

            if (_planOptionState.SetOutputContainer(value))
            {
                OnPropertyChanged();
                PlanOptionChanged(
                    $"Output container changed to {value}.",
                    $"Contenedor de salida cambiado a {value}.");
            }
        }
    }

    public AiQualityPreset SelectedQualityPreset
    {
        get => _planOptionState.QualityPreset;
        set
        {
            if (_planOptionState.QualityPreset == value)
            {
                return;
            }

            if (TryDeferPreviewInvalidatingChange(() => SelectedQualityPreset = value))
            {
                OnPropertyChanged();
                return;
            }

            if (_planOptionState.SetQualityPreset(value))
            {
                OnPropertyChanged();
                PlanOptionChanged(
                    $"Quality changed to {QualityPresetText(value, useSpanish: false)}.",
                    $"Calidad cambiada a {QualityPresetText(value, useSpanish: true)}.");
            }
        }
    }

    public ThreeDIntensity SelectedThreeDIntensity
    {
        get => _planOptionState.Intensity;
        set
        {
            if (_planOptionState.Intensity == value)
            {
                return;
            }

            if (TryDeferPreviewInvalidatingChange(() => SelectedThreeDIntensity = value))
            {
                OnPropertyChanged();
                return;
            }

            if (_planOptionState.SetIntensity(value))
            {
                OnPropertyChanged();
                PlanOptionChanged(
                    $"3D intensity changed to {ThreeDIntensityText(value, useSpanish: false)}.",
                    $"Intensidad 3D cambiada a {ThreeDIntensityText(value, useSpanish: true)}.");
            }
        }
    }

    public ThreeDOutputFormat SelectedThreeDOutputFormat
    {
        get => _planOptionState.ThreeDOutputFormat;
        set
        {
            if (_planOptionState.ThreeDOutputFormat == value)
            {
                return;
            }

            if (TryDeferPreviewInvalidatingChange(() => SelectedThreeDOutputFormat = value))
            {
                OnPropertyChanged();
                return;
            }

            if (_planOptionState.SetThreeDOutputFormat(value))
            {
                OnPropertyChanged();
                PlanOptionChanged(
                    $"3D layout changed to {ThreeDOutputFormatText(value, useSpanish: false)}.",
                    $"Diseño 3D cambiado a {ThreeDOutputFormatText(value, useSpanish: true)}.");
            }
        }
    }

    public string CreateLgCompatibilityCopyText => Text(
        "Create LG 3D TV 2012 compatible MP4 copy",
        "Crear copia MP4 compatible con LG 3D TV 2012");

    public bool IsLgOutputProfileSelected =>
        ReferenceEquals(SelectedOutputPreset, TargetDevicePresets.Lg3dFullHd2012);

    public Visibility LgCompatibilityOptionsVisibility =>
        IsLgOutputProfileSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LgCompatibilityCopyPathVisibility =>
        CreateLgCompatibilityCopy ? Visibility.Visible : Visibility.Collapsed;

    public bool CreateLgCompatibilityCopy
    {
        get => _planOptionState.CreateLgCompatibilityCopy;
        set
        {
            if (value && !IsLgOutputProfileSelected)
            {
                OnPropertyChanged();
                return;
            }

            if (_planOptionState.SetCreateLgCompatibilityCopy(value))
            {
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreferLgCompatibilityCopyWhenOpening));
                OnPropertyChanged(nameof(CanPreferLgCompatibilityCopyWhenOpening));
                OnPropertyChanged(nameof(LgCompatibilityCopyPathVisibility));
                PlanOptionChanged(
                    value
                        ? "LG-compatible MP4 copy enabled."
                        : "LG-compatible MP4 copy disabled.",
                    value
                        ? "Copia MP4 compatible con LG activada."
                        : "Copia MP4 compatible con LG desactivada.",
                    affectsPreview: false);
            }
        }
    }

    public string PreferLgCompatibilityCopyWhenOpeningText => Text(
        "Open LG-compatible copy when available",
        "Abrir copia compatible LG cuando exista");

    public bool PreferLgCompatibilityCopyWhenOpening
    {
        get => _planOptionState.PreferLgCompatibilityCopyWhenOpening;
        set
        {
            if (value && !IsLgOutputProfileSelected)
            {
                OnPropertyChanged();
                return;
            }

            if (_planOptionState.SetPreferLgCompatibilityCopyWhenOpening(value))
            {
                OnPropertyChanged();
                PlanOptionChanged(
                    value
                        ? "LG-compatible copy selected as preferred open target."
                        : "Primary output selected as preferred open target.",
                    value
                        ? "Copia compatible con LG seleccionada como destino preferido al abrir."
                        : "Salida principal seleccionada como destino preferido al abrir.",
                    affectsPreview: false);
            }
        }
    }

    public bool CanChangeLgCompatibilityCopyOptions =>
        !IsConversionRunning && !IsPreviewGenerating && !IsModelPackImportRunning && IsLgOutputProfileSelected;

    public bool CanPreferLgCompatibilityCopyWhenOpening =>
        !IsConversionRunning &&
        !IsPreviewGenerating &&
        !IsModelPackImportRunning &&
        IsLgOutputProfileSelected &&
        CreateLgCompatibilityCopy;

    public string LgCompatibilityCopyExplanationText => Text(
        "When enabled, v3dfy creates the primary output first, then a separate LG-compatible MP4 copy for the TV.",
        "Cuando esta opcion esta activada, v3dfy crea primero la salida principal y luego una copia MP4 compatible LG para la TV.");

    public int SelectedWorkflowTabIndex
    {
        get => SelectedWizardStepIndex;
        set
        {
            SelectedWizardStepIndex = value;
            OnPropertyChanged();
        }
    }

    public int SelectedSystemStatusTabIndex
    {
        get => _workflowState.SelectedSystemStatusTabIndex;
        set
        {
            if (IsConversionRunning && value != 1)
            {
                OnPropertyChanged();
                return;
            }

            if (_workflowState.SetSelectedSystemStatusTabIndex(value))
            {
                OnPropertyChanged();
            }
        }
    }

    public bool CanOpenSystemStatusConversionTab =>
        _workflowState.CanOpenSystemStatusConversionTab;

    public bool CanOpenSystemStatusToolsTab => !IsConversionRunning && !IsPreviewGenerating && !IsModelPackImportRunning;

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        private set
        {
            if (SetProperty(ref _isAnalyzing, value))
            {
                AnalyzeCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(AnalysisStatusText));
                OnPropertyChanged(nameof(VideoAnalysisPendingStatusText));
                OnPropertyChanged(nameof(VideoAnalysisPendingStatusVisibility));
                RaiseModelPackImportAvailabilityPropertiesChanged();
            }
        }
    }

    public string VideoAnalysisTitle => Text("Video analysis", "Análisis de video");

    public bool IsModelPackImportRunning
    {
        get => _isModelPackImportRunning;
        private set
        {
            if (SetProperty(ref _isModelPackImportRunning, value))
            {
                OnPropertyChanged(nameof(ModelPackImportStatusText));
                RaiseModelPackImportAvailabilityPropertiesChanged();
                RaisePreviewPropertiesChanged();
                RaiseConversionExecutionPropertiesChanged();
                RaiseSystemStatusPropertiesChanged();
                SelectVideoCommand.RaiseCanExecuteChanged();
                AnalyzeCommand.RaiseCanExecuteChanged();
                BrowseOutputFolderCommand.RaiseCanExecuteChanged();
                ResetOutputPathCommand.RaiseCanExecuteChanged();
                ShowProfileDetailsCommand.RaiseCanExecuteChanged();
                ShowModelHelpCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanImportModelPack =>
        !IsConversionRunning &&
        !IsPreviewGenerating &&
        !IsAnalyzing &&
        !IsModelPackImportRunning;

    public string ImportModelPackText => IsModelPackImportRunning
        ? Text("Importing model pack...", "Importando paquete de modelos...")
        : Text("Import model pack...", "Importar paquete de modelos...");

    public string ModelPackImportInstructionText => Text(
        "Valid model packs are reviewed before Windows asks for administrator permission.",
        "Los paquetes de modelos validos se revisan antes de que Windows pida permiso de administrador.");

    public string ModelPackImportStatusText => Text(
        _modelPackImportStatusEnglishText,
        _modelPackImportStatusSpanishText);

    public string LastModelPackImportSummary => Text(
        _lastModelPackImportSummaryEnglishText,
        _lastModelPackImportSummarySpanishText);

    public Visibility LastModelPackImportSummaryVisibility =>
        string.IsNullOrWhiteSpace(LastModelPackImportSummary)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public bool IsGlobalBusyOverlayVisible
    {
        get => _isGlobalBusyOverlayVisible;
        private set
        {
            if (SetProperty(ref _isGlobalBusyOverlayVisible, value))
            {
                OnPropertyChanged(nameof(GlobalBusyOverlayVisibility));
            }
        }
    }

    public Visibility GlobalBusyOverlayVisibility =>
        IsGlobalBusyOverlayVisible ? Visibility.Visible : Visibility.Collapsed;

    public string GlobalBusyText => Text(
        string.IsNullOrWhiteSpace(_globalBusyEnglishText) ? "Loading..." : _globalBusyEnglishText,
        string.IsNullOrWhiteSpace(_globalBusySpanishText) ? "Cargando..." : _globalBusySpanishText);

    public string AnalysisStatusText => IsAnalyzing
        ? Text("Analyzing video...", "Analizando video...")
        : _analysis is null
            ? Text("No analysis yet.", "Aún no hay análisis.")
            : Text("Analysis completed.", "Análisis completado.");

    public string VideoAnalysisPendingStatusText => IsAnalyzing
        ? Text("Analyzing the selected video...", "Analizando el video seleccionado...")
        : Text("Video selected. Run analysis to continue.", "Video seleccionado. Ejecuta el analisis para continuar.");

    public Visibility VideoAnalysisSectionVisibility =>
        HasSelectedVideo ? Visibility.Visible : Visibility.Collapsed;

    public Visibility VideoAnalysisPendingStatusVisibility =>
        HasSelectedVideo && _analysis is null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility VideoAnalysisResultsVisibility =>
        _analysis is null ? Visibility.Collapsed : Visibility.Visible;

    public string AnalysisDurationText => LabelValue(
        "Duration",
        "Duración",
        _analysis?.File.Duration is { } duration
            ? duration.ToString(@"hh\:mm\:ss")
            : null);

    public string AnalysisResolutionText => LabelValue(
        "Resolution",
        "Resolución",
        _analysis?.Video is { Width: { } width, Height: { } height }
            ? $"{width}x{height}"
            : null);

    public string AnalysisFpsText => LabelValue(
        "FPS",
        "FPS",
        _analysis?.Video?.FrameRate?.ToString("0.###"));

    public string AnalysisCodecText => LabelValue(
        "Video codec",
        "Códec de video",
        _analysis?.Video?.CodecName);

    public string AnalysisContainerText => LabelValue(
        "Container",
        "Contenedor",
        _analysis?.File.FormatName);

    public string AnalysisAudioStreamsText => LabelValue(
        "Audio streams",
        "Pistas de audio",
        _analysis?.AudioStreams.Count.ToString());

    public string AnalysisSubtitleStreamsText => LabelValue(
        "Subtitle streams",
        "Pistas de subtítulos",
        _analysis?.SubtitleStreams.Count.ToString());

    public string AnalysisHdrText => LabelValue(
        "HDR",
        "HDR",
        _analysis is null
            ? null
            : _analysis.Video?.IsHdr == true
                ? Text("Detected", "Detectado")
                : Text("Not detected", "No detectado"));

    public string AnalysisCompatibilityText => _analysis?.Video is
        { Width: { } width, Height: { } height } &&
        (width > SelectedOutputPreset.Recommendation.Width ||
         height > SelectedOutputPreset.Recommendation.Height)
            ? Text(
                "Compatibility note: resolution is higher than the selected preset target.",
                "Nota de compatibilidad: la resolución supera el objetivo del perfil seleccionado.")
            : string.Empty;

    public string RecommendedSetupTitle => Text(
        "Recommended 3D setup",
        "Configuración 3D recomendada");

    public bool CanOpenRecommendedSetupTab => _workflowState.CanOpenRecommendedSetupTab;

    public string RecommendedSetupStatusText => _conversionRecommendation is null
        ? Text("No recommended setup yet.", "Aún no hay configuración recomendada.")
        : Text(
            $"Recommended setup for {SelectedOutputPreset.Name}.",
            $"Configuración recomendada para {SelectedOutputPreset.SpanishName}.");

    public string RecommendedOutputContainerText => RecommendationLabelValue(
        "Output container",
        "Contenedor de salida",
        _conversionRecommendation?.OutputContainer.ToString());

    public string RecommendedVideoCodecText => RecommendationLabelValue(
        "Video codec",
        "Códec de video",
        _conversionRecommendation?.VideoCodec);

    public string RecommendedAudioCodecText => RecommendationLabelValue(
        "Audio",
        "Audio",
        _conversionRecommendation?.AudioCodec);

    public string RecommendedResolutionText => RecommendationLabelValue(
        "Resolution",
        "Resolución",
        _conversionRecommendation is null
            ? null
            : $"{_conversionRecommendation.Width}x{_conversionRecommendation.Height}");

    public string RecommendedThreeDLayoutText => RecommendationLabelValue(
        "3D layout",
        "Diseño 3D",
        _conversionRecommendation?.ThreeDOutputFormat == ThreeDOutputFormat.HalfTopBottom
            ? "Half Top-Bottom"
            : _conversionRecommendation?.ThreeDOutputFormat.ToString());

    public string RecommendedQualityText => RecommendationLabelValue(
        "Quality",
        "Calidad",
        _conversionRecommendation is null
            ? null
            : QualityPresetText(_conversionRecommendation.QualityPreset, IsSpanish));

    public string RecommendedIntensityText => RecommendationLabelValue(
        "3D intensity",
        "Intensidad 3D",
        _conversionRecommendation is null
            ? null
            : ThreeDIntensityText(_conversionRecommendation.Intensity, IsSpanish));

    public string RecommendedNotesTitle => Text(
        "Notes / compatibility warnings",
        "Notas / advertencias de compatibilidad");

    public string RecommendedNotesText => _conversionRecommendation is null
        ? "-"
        : _conversionRecommendation.CompatibilityIssues.Count == 0
            ? Text("No compatibility warnings.", "No hay advertencias de compatibilidad.")
            : string.Join(
                Environment.NewLine,
                _conversionRecommendation.CompatibilityIssues.Select(issue =>
                    $"- {Text(issue.EnglishMessage, issue.SpanishMessage)}"));

    public string ConversionPlanTitle => Text("Conversion plan", "Plan de conversión");

    public bool CanOpenConversionPlanTab => _workflowState.CanOpenConversionPlanTab;

    public string PlanOptionsTitle => Text("Plan options", "Opciones del plan");

    public string OutputContainerOptionLabel => Text("Output container", "Contenedor de salida");

    public string QualityOptionLabel => Text("Quality", "Calidad");

    public string ThreeDIntensityOptionLabel => Text("3D intensity", "Intensidad 3D");

    public string ThreeDOutputFormatOptionLabel => Text("3D layout", "Diseño 3D");

    public string OutputLocationTitle => Text("Output location", "Ubicación de salida");

    public string LocalModelSelectionLabel => Text("Local 3D/depth model", "Modelo local 3D/profundidad");

    public bool HasLocalModelSelectionCandidates => LocalModelCandidates.Count > 0;

    public bool CanShowModelHelp =>
        HasLocalModelSelectionCandidates &&
        !IsConversionRunning &&
        !IsPreviewGenerating &&
        !IsModelPackImportRunning;

    public LocalModelSelectionCandidate? SelectedLocalModelCandidate
    {
        get => _selectedLocalModelCandidate;
        set
        {
            if (ReferenceEquals(_selectedLocalModelCandidate, value))
            {
                return;
            }

            if (_isApplyingUiOnlyRefresh && value is null)
            {
                OnPropertyChanged();
                return;
            }

            if (!_isApplyingUiOnlyRefresh &&
                TryDeferPreviewInvalidatingChange(() => SelectedLocalModelCandidate = value))
            {
                OnPropertyChanged();
                return;
            }

            SetSelectedLocalModelCandidate(value, regeneratePlan: !_isApplyingUiOnlyRefresh);
        }
    }

    public string LocalModelSelectionStatusText => SelectedLocalModelCandidate is null
        ? HasUnmappedLocalModelCandidates
            ? Text(
                "Unmapped model files were found. Add a model catalog entry or mapping before using them.",
                "Se encontraron modelos no mapeados. Agrega una entrada de catalogo o mapeo antes de usarlos.")
            : Text(
                "No mapped local depth models are available yet.",
                "Aun no hay modelos locales de profundidad mapeados disponibles.")
        : Text(
            $"Selected local model: {SelectedLocalModelCandidate.DisplayName}. iw3 depth model: {SelectedLocalModelCandidate.Iw3DepthModelName ?? "-"}",
            $"Modelo local seleccionado: {SelectedLocalModelCandidate.DisplayName}. Modelo de profundidad iw3: {SelectedLocalModelCandidate.Iw3DepthModelName ?? "-"}");

    public string SelectedModelGuidanceText => CreateSelectedModelGuidanceText();

    public string PresetGuidanceText => Text(
        $"Preset guidance: {SelectedOutputPreset.BestFor}",
        $"Guia del perfil: {SelectedOutputPreset.SpanishBestFor}");

    public string EstimatedConversionTimeText
    {
        get
        {
            var estimate = CreateCurrentConversionTimeEstimate();
            if (!estimate.IsAvailable)
            {
                return Text(
                    "Estimated conversion time: Not enough information yet",
                    "Tiempo estimado de conversion: aun no hay suficiente informacion");
            }

            return Text(
                $"Estimated conversion time: {FormatEstimateRange(estimate.Low, estimate.High)}",
                $"Tiempo estimado de conversion: {FormatEstimateRange(estimate.Low, estimate.High)}");
        }
    }

    public string EstimateConfidenceText
    {
        get
        {
            var estimate = CreateCurrentConversionTimeEstimate();
            return Text(
                $"Confidence: {ConfidenceText(estimate.Confidence, useSpanish: false)}",
                $"Confianza: {ConfidenceText(estimate.Confidence, useSpanish: true)}");
        }
    }

    public string EstimateBasisText
    {
        get
        {
            var estimate = CreateCurrentConversionTimeEstimate();
            var basis = IsSpanish
                ? estimate.SpanishBasisItems
                : estimate.EnglishBasisItems;
            return Text("Based on: ", "Basado en: ") + string.Join(", ", basis);
        }
    }

    public string EstimatedOutputSizeText
    {
        get
        {
            var estimate = CreateCurrentOutputSizeEstimate();
            if (!estimate.IsAvailable)
            {
                return Text(
                    "Estimated output size: Output size depends on final encoding settings.",
                    "Tamano estimado de salida: el tamano depende de la configuracion final de codificacion.");
            }

            return Text(
                $"Estimated output size: {FormatByteRange(estimate.LowBytes, estimate.HighBytes)}",
                $"Tamano estimado de salida: {FormatByteRange(estimate.LowBytes, estimate.HighBytes)}");
        }
    }

    public string RecommendedFreeSpaceText
    {
        get
        {
            var estimate = CreateCurrentOutputSizeEstimate();
            if (!estimate.IsAvailable)
            {
                return Text(
                    "Recommended free space: check the destination drive before converting.",
                    "Espacio libre recomendado: revisa la unidad de destino antes de convertir.");
            }

            return Text(
                $"Recommended free space: at least {FormatBytes(estimate.RecommendedFreeBytes)}",
                $"Espacio libre recomendado: al menos {FormatBytes(estimate.RecommendedFreeBytes)}");
        }
    }

    public string PerformanceHistoryPrivacyText => Text(
        "Performance history is stored locally on this device to improve future time estimates. No telemetry or file paths are stored.",
        "El historial de rendimiento se guarda localmente en este equipo para mejorar futuros tiempos estimados. No se guarda telemetria ni rutas de archivos.");

    public string CompactEstimateTimeConfidenceText
    {
        get
        {
            var estimate = CreateCurrentConversionTimeEstimate();
            var estimateText = estimate.IsAvailable
                ? FormatEstimateRange(estimate.Low, estimate.High)
                : Text("not enough information yet", "aun sin informacion suficiente");
            return Text(
                $"Estimated time: {estimateText} \u00b7 Confidence: {ConfidenceText(estimate.Confidence, useSpanish: false)}",
                $"Tiempo estimado: {estimateText} \u00b7 Confianza: {ConfidenceText(estimate.Confidence, useSpanish: true)}");
        }
    }

    public string CompactOutputSizeFreeSpaceText
    {
        get
        {
            var estimate = CreateCurrentOutputSizeEstimate();
            if (!estimate.IsAvailable)
            {
                return Text(
                    "Output size: depends on final encoding settings \u00b7 Free space: check destination drive",
                    "Tamano de salida: depende de la codificacion final \u00b7 Espacio libre: revisa la unidad destino");
            }

            return Text(
                $"Output size: {FormatByteRange(estimate.LowBytes, estimate.HighBytes)} \u00b7 Free space: at least {FormatBytes(estimate.RecommendedFreeBytes)}",
                $"Tamano de salida: {FormatByteRange(estimate.LowBytes, estimate.HighBytes)} \u00b7 Espacio libre: al menos {FormatBytes(estimate.RecommendedFreeBytes)}");
        }
    }

    public string CompactSelectedModelGuidanceText
    {
        get
        {
            if (SelectedLocalModelCandidate is null)
            {
                return Text(
                    "Model: included base model when available \u00b7 Speed: Medium \u00b7 Quality: Usable",
                    "Modelo: modelo base incluido cuando este disponible \u00b7 Velocidad: Media \u00b7 Calidad: Utilizable");
            }

            var entry = FindRegistryEntry(SelectedLocalModelCandidate);
            var guidance = _modelGuidanceService.Create(
                SelectedLocalModelCandidate.MappingKey,
                SelectedLocalModelCandidate.Iw3DepthModelName,
                SelectedLocalModelCandidate.DisplayName,
                entry?.IsEmbeddedBase == true);
            return Text(
                $"Model: {guidance.EnglishHeadline} \u00b7 Speed: {guidance.EnglishSpeed} \u00b7 Quality: {guidance.EnglishQuality}",
                $"Modelo: {guidance.SpanishHeadline} \u00b7 Velocidad: {guidance.SpanishSpeed} \u00b7 Calidad: {guidance.SpanishQuality}");
        }
    }

    public string CompactPresetGuidanceText => Text(
        $"Preset: {SelectedOutputPreset.BestFor}",
        $"Perfil: {SelectedOutputPreset.SpanishBestFor}");

    public string EstimateDetailsTitleText => Text(
        "Estimate details",
        "Detalles de estimacion");

    public string GuidanceDetailsTitleText => Text(
        "Model and preset details",
        "Detalles de modelo y perfil");

    public bool HasUnmappedLocalModelCandidates =>
        Iw3DepthModelMapper.GetUnmappedCandidates(
            _dependencyHealth?.ModelInventory.SelectionCandidates ?? []).Count > 0;

    public string ModelHelpButtonText => "?";

    public string ModelHelpButtonToolTipText => Text(
        "Explain the installed models currently available in this selector.",
        "Explicar los modelos instalados disponibles actualmente en este selector.");

    public string ModelHelpTitleText => Text(
        "Selectable model help",
        "Ayuda de modelos seleccionables");

    public string ModelHelpIntroText => Text(
        "Only installed models that are ready for this conversion flow are shown here.",
        "Aqui solo se muestran modelos instalados que estan listos para este flujo de conversion.");

    public string ModelHelpModelHeaderText => Text("Model", "Modelo");

    public string ModelHelpPurposeHeaderText => Text("Purpose", "Propósito");

    public string ModelHelpUseHeaderText => Text("Use", "Uso");

    public string ModelHelpSceneHeaderText => Text("Scene", "Escena");

    public string ModelHelpDepthHeaderText => Text("Depth", "Profundidad");

    public string ModelHelpSizePerformanceHeaderText => Text(
        "Size/performance",
        "Tamaño/rendimiento");

    public IReadOnlyList<ModelHelpRow> ModelHelpRows => CreateModelHelpRows();

    public string ViewModelsText => Text("View models", "Ver modelos");

    public string ViewModelsToolTipText => Text(
        "View local 3D models and import model packs.",
        "Ver modelos 3D locales e importar paquetes de modelos.");

    public string ModelInventoryTitleText => Text("3D models", "Modelos 3D");

    public string ModelInventoryIntroText => Text(
        "Review local models detected by v3dfy. Only mapped and verified models are selectable for conversion.",
        "Revisa los modelos locales detectados por v3dfy. Solo los modelos mapeados y verificados son seleccionables para convertir.");

    public string ModelInventoryFolderLabelText => Text("Model folder", "Carpeta de modelos");

    public string ModelInventoryFolderPathText => GetCurrentModelInventory().ModelsDirectory;

    public string SelectableModelsSectionTitleText => Text(
        "Selectable models",
        "Modelos seleccionables");

    public string SelectableModelsInventoryText => CreateSelectableModelsInventoryText();

    public string SelectableModelNameHeaderText => Text("Model", "Modelo");

    public string SelectableModelIw3HeaderText => Text("iw3 depth model", "Modelo iw3");

    public string SelectableModelCheckpointHeaderText => Text("Checkpoint", "Checkpoint");

    public string SelectableModelTypeHeaderText => Text("Type", "Tipo");

    public string SelectableModelSourceHeaderText => Text("Source", "Origen");

    public IReadOnlyList<SelectableModelInventoryRow> SelectableModelInventoryRows =>
        CreateSelectableModelInventoryRows();

    public Visibility SettingsSelectableModelsTableVisibility =>
        SelectableModelInventoryRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SettingsSelectableModelsEmptyVisibility =>
        SelectableModelInventoryRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public string SettingsSelectableModelsEmptyText => Text(
        "No selectable models are available yet.",
        "Aun no hay modelos seleccionables disponibles.");

    public string DiagnosticModelsSectionTitleText => Text(
        "Detected but not selectable",
        "Detectados no seleccionables");

    public string DiagnosticModelsInventoryText => CreateDiagnosticModelsInventoryText();

    public string RuntimeDependenciesSectionTitleText => Text(
        "Runtime dependencies",
        "Dependencias de runtime");

    public string RuntimeDependenciesInventoryText => CreateRuntimeDependenciesInventoryText();

    public string ModelInventoryActionsTitleText => Text("Actions", "Acciones");

    public string OpenModelsFolderText => Text(
        "Open models folder",
        "Abrir carpeta de modelos");

    public string OutputPathLabel => Text("Output path", "Ruta de salida");

    public string BrowseOutputFolderText => Text("Browse...", "Examinar...");

    public string ResetOutputPathText => Text("Reset", "Restablecer");

    public string OpenOutputWhenFinishedText => Text(
        "Open video when finished",
        "Abrir video al finalizar");

    public bool OpenOutputWhenFinished
    {
        get => _openOutputWhenFinished;
        set
        {
            if (IsConversionRunning || IsPreviewGenerating || IsModelPackImportRunning)
            {
                OnPropertyChanged();
                return;
            }

            SetProperty(ref _openOutputWhenFinished, value);
        }
    }

    public bool CanChangeOpenOutputWhenFinished => !IsConversionRunning && !IsPreviewGenerating && !IsModelPackImportRunning;

    public bool HasCustomOutputPath => _outputPathState.HasCustomOutputPath;

    public string OutputPathText
    {
        get => _outputPathText;
        set
        {
            if (SetProperty(ref _outputPathText, value) && !_isUpdatingOutputPathText)
            {
                CommitOutputPath(value);
            }
        }
    }

    public string ConversionPlanStatusText => _conversionPlan is null
        ? Text("No conversion plan yet.", "Aún no hay plan de conversión.")
        : _conversionPlan.IsDryRun
            ? Text("Dry-run preview. Conversion is not started.", "Vista previa en seco. La conversión no se ha iniciado.")
            : Text("Ready for conversion.", "Listo para convertir.");

    public string OutputProfileDisplayText => _planOptionState.HasCustomizedOptions
        ? Text(
            $"Custom based on {SelectedOutputPreset.Name}",
            $"Personalizado basado en {SelectedOutputPreset.SpanishName}")
        : Text(SelectedOutputPreset.Name, SelectedOutputPreset.SpanishName);

    public string ConversionPlanPresetText => Text(
        $"Output profile: {OutputProfileDisplayText}",
        $"Perfil de salida: {OutputProfileDisplayText}");

    public string ConversionPlanLocalModelText => _conversionPlan?.SelectedLocalModel is null
        ? Text(
            "Local model: Not selected / not available yet.",
            "Modelo local: No seleccionado / a\u00fan no disponible.")
        : Text(
            $"Local model: {_conversionPlan.SelectedLocalModel.DisplayName} ({_conversionPlan.SelectedLocalModel.RelativePath}, {_conversionPlan.SelectedLocalModel.EnglishSourceText}). iw3 depth model: {_conversionPlan.SelectedLocalModel.Iw3DepthModelName ?? "-"}",
            $"Modelo local: {GetSpanishModelDisplayName(_conversionPlan.SelectedLocalModel)} ({_conversionPlan.SelectedLocalModel.RelativePath}, {_conversionPlan.SelectedLocalModel.SpanishSourceText}). Modelo de profundidad iw3: {_conversionPlan.SelectedLocalModel.Iw3DepthModelName ?? "-"}");

    public string ConversionPlanOutputPathText => ConversionPlanLabelValue(
        "Primary output",
        "Salida principal",
        _conversionPlan?.SuggestedOutputPath);

    public string ConversionPlanLgCompatibilityCopyPathText =>
        ConversionPlanLabelValue(
            "LG-compatible copy",
            "Copia compatible LG",
            GetLgCompatibilityCopyPath());

    public string ConversionPlanOutputFormatText => ConversionPlanLabelValue(
        "Output format",
        "Formato de salida",
        _conversionPlan is null
            ? null
            : $"{_conversionPlan.OutputContainer} / {_conversionPlan.VideoCodec} / {_conversionPlan.AudioCodec}");

    public string ConversionPlanResolutionText => ConversionPlanLabelValue(
        "Target resolution",
        "Resolución objetivo",
        _conversionPlan is null
            ? null
            : $"{_conversionPlan.Width}x{_conversionPlan.Height}");

    public string ConversionPlanThreeDLayoutText => ConversionPlanLabelValue(
        "3D layout",
        "Diseño 3D",
        _conversionPlan is null
            ? null
            : ThreeDOutputFormatText(_conversionPlan.ThreeDOutputFormat, IsSpanish));

    public string ConversionPlanQualityText => ConversionPlanLabelValue(
        "Quality",
        "Calidad",
        _conversionPlan is null
            ? null
            : QualityPresetText(_conversionPlan.QualityPreset, IsSpanish));

    public string ConversionPlanIntensityText => ConversionPlanLabelValue(
        "3D intensity",
        "Intensidad 3D",
        _conversionPlan is null
            ? null
            : ThreeDIntensityText(_conversionPlan.Intensity, IsSpanish));

    public string ConversionPlanDryRunReasonText => _conversionPlan?.DryRunReason switch
    {
        ConversionDryRunReason.MissingLocalAiBundle => Text(
            "Conversion is not available yet because the local AI engine, embedded Python runtime, or models are not bundled.",
            "La conversión aún no está disponible porque todavía no están incluidos el motor local de IA, el runtime de Python embebido o los modelos."),
        ConversionDryRunReason.MissingRequiredTools => Text(
            "Conversion is not available because required bundled tools are missing.",
            "La conversión no está disponible porque faltan herramientas incluidas requeridas."),
        _ => string.Empty,
    };

    public string ConversionPlanStepsTitle => Text("Planned steps", "Pasos previstos");

    public string ConversionPlanStepsText => _conversionPlan is null
        ? "-"
        : string.Join(
            Environment.NewLine,
            _conversionPlan.Steps.Select((step, index) =>
                $"{index + 1}. {Text(step.EnglishText, step.SpanishText)}"));

    public string ConversionPlanCommandPreviewTitle => Text(
        "Future iw3 command preview",
        "Vista previa del futuro comando iw3");

    public string ConversionPlanCommandPreviewText => _conversionPlan?.CommandPreview ?? "-";

    public string ConversionPlanTechnicalDetailsTitleText => Text(
        "Technical details",
        "Detalles tecnicos");

    public string ReadyForConversionSummaryText => Text(
        "Ready for conversion",
        "Listo para convertir");

    public bool HasEnteredPreviewConversionStage => _hasEnteredPreviewConversionStage;

    public bool CanEnterPreviewConversionStage =>
        SelectedWizardStepIndex == ConversionWorkflowState.ConversionPlanStepIndex &&
        CanOpenConversionPlanStep &&
        _conversionPlan is not null &&
        !IsConversionRunning &&
        !IsPreviewGenerating &&
        !IsModelPackImportRunning;

    public Visibility ContinueWithConversionActionVisibility =>
        HasEnteredPreviewConversionStage ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ContinueWithConversionFooterVisibility =>
        SelectedWizardStepIndex == ConversionWorkflowState.ConversionPlanStepIndex &&
        !HasEnteredPreviewConversionStage
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string ContinueWithConversionText => Text(
        "Continue with conversion",
        "Continuar con la conversion");

    public Visibility PreviewConversionStatusCardVisibility =>
        HasEnteredPreviewConversionStage ? Visibility.Visible : Visibility.Collapsed;

    public GridLength PreviewConversionRowHeight =>
        HasEnteredPreviewConversionStage ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public Thickness ActivityLogCardMargin =>
        HasEnteredPreviewConversionStage ? new Thickness(0, 6, 0, 0) : new Thickness(0);

    public string PreviewStageResetNoticeText => Text(
        "Review the conversion plan again, then press Continue with conversion.",
        "Revisa de nuevo el plan de conversion y presiona Continuar con la conversion.");

    public string PreviewConversionStatusTitleText => Text(
        "Preview & conversion",
        "Preview y conversion");

    public string PreviewConversionStatusText
    {
        get
        {
            if (IsConversionRunning)
            {
                return ConversionExecutionStatusText;
            }

            if (IsPreviewGenerating)
            {
                return PreviewStatusValueText;
            }

            return _conversionExecutionState.Status switch
            {
                ConversionExecutionStatus.Completed => Text("Conversion completed", "Conversion finalizada"),
                ConversionExecutionStatus.Canceled => Text("Conversion canceled", "Conversion cancelada"),
                ConversionExecutionStatus.Failed => Text("Conversion failed", "Conversion fallida"),
                _ when IsCurrentPreviewAccepted => ConversionReadyTitleText,
                _ => PreviewStatusValueText,
            };
        }
    }

    public string PreviewConversionStatusDetailText
    {
        get
        {
            if (!HasCompletedAnalysis)
            {
                return Text(
                    "Select and analyze a source video to prepare preview and conversion.",
                    "Selecciona y analiza un video fuente para preparar preview y conversion.");
            }

            if (IsConversionRunning)
            {
                return ConversionExecutionDetailText;
            }

            if (IsPreviewGenerating)
            {
                return PreviewModalDetailText;
            }

            if (IsCurrentPreviewAccepted)
            {
                return ConversionReadyBodyText;
            }

            return PreviewRequiredInstructionText;
        }
    }

    public string PreviewTitleText => Text("Preview", "Vista previa");

    public string PreviewRequiredTitleText => Text("Preview required", "Vista previa requerida");

    public string PreviewAcceptedTitleText => Text("Preview accepted", "Vista previa aceptada");

    public string PreviewStepTitleText =>
        PreviewConversionGate.Evaluate(_previewState, CreateCurrentPreviewConfiguration()).CanStart
            ? PreviewAcceptedTitleText
            : PreviewRequiredTitleText;

    public string GeneratePreviewText => Text("Generate preview", "Generar vista previa");

    public string PreviewRequiredInstructionText => _previewState.Status == PreviewGenerationStatus.Outdated
        ? Text(
            "Settings changed. Generate a new preview for the current settings.",
            "La configuracion cambio. Genera una nueva vista previa para la configuracion actual.")
        : Text(
            "Generate and review a short preview before final conversion.",
            "Genera y revisa una vista previa corta antes de la conversion final.");

    public Visibility PreviewRequirementVisibility =>
        !HasEnteredPreviewConversionStage || IsConversionRunning || IsPreviewGenerating || IsCurrentPreviewAccepted
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility GeneratePreviewPrimaryActionVisibility =>
        HasEnteredPreviewConversionStage && !IsConversionRunning && !IsPreviewGenerating && !IsCurrentPreviewAccepted
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ConvertPrimaryActionVisibility =>
        HasEnteredPreviewConversionStage && IsCurrentPreviewAccepted && !IsConversionRunning && !IsPreviewGenerating
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ConversionReadySummaryVisibility =>
        CanStartConversion ? Visibility.Visible : Visibility.Collapsed;

    public string ConversionReadyTitleText => Text("Conversion ready", "Conversi\u00f3n lista");

    public string ConversionReadyBodyText => Text(
        "Preview accepted. The final video is ready to be generated.",
        "Vista previa aceptada. El video final est\u00e1 listo para generarse.");

    public string ConversionReadySelectedModelText => ConversionPlanLabelValue(
        "Selected model",
        "Modelo seleccionado",
        FormatConversionReadySelectedModel());

    public string ConversionReadyOutputText => ConversionPlanLabelValue(
        "Output",
        "Salida",
        FormatConversionReadyOutput());

    public string ConversionReadyDestinationText => ConversionPlanLabelValue(
        "Destination",
        "Destino",
        _conversionPlan?.SuggestedOutputPath);

    public Visibility CancelConversionPrimaryActionVisibility =>
        IsConversionRunning
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string CancelPreviewText => Text("Cancel preview", "Cancelar vista previa");

    public string OpenPreviewText => Text("Open preview", "Abrir vista previa");

    public string OpenPreviewExternallyText => Text("Open externally", "Abrir externamente");

    public string DeletePreviewText => Text("Delete preview", "Eliminar vista previa");

    public string ContinuePreviewText => Text("Continue", "Continuar");

    public string PreviewGeneratingTitleText => Text("Generating preview", "Generando vista previa");

    public string PreviewGeneratingMessageText => Text(
        "Please wait while v3dfy creates a short 3D preview.",
        "Espera mientras v3dfy crea una vista previa 3D corta.");

    public int PreviewProgressPercent => Math.Clamp(_previewProgressPercent, 0, 100);

    public string PreviewProgressText =>
        PreviewProgressPercent > 0
            ? $"{PreviewProgressPercent}%"
            : Text("Estimating...", "Calculando...");

    public bool PreviewProgressIsIndeterminate =>
        IsPreviewGenerating && PreviewProgressPercent <= 0;

    public string PreviewReadyTitleText => Text("Preview ready", "Vista previa lista");

    public string PreviewReadyMessageText => Text(
        "Review the preview before continuing to final conversion.",
        "Revisa la vista previa antes de continuar con la conversion final.");

    public string PreviewPlaybackFallbackText => Text(
        "If embedded playback is unavailable, use Open externally.",
        "Si la reproduccion integrada no esta disponible, usa Abrir externamente.");

    public string PreviewPlayText => Text("Play", "Reproducir");

    public string PreviewPauseText => Text("Pause", "Pausar");

    public string PreviewReplayText => Text("Replay", "Repetir");

    public string PreviewVolumeText => Text("Volume", "Volumen");

    public string PreviewMutedText => Text("Muted", "Silenciado");

    public string PreviewMuteText => Text("Mute", "Silenciar");

    public string PreviewUnmuteText => Text("Unmute", "Activar sonido");

    public string PreviewEndedText => Text("Preview ended", "Vista previa finalizada");

    public string EmbeddedPlaybackUnavailableText => Text(
        "Embedded playback unavailable",
        "Reproduccion integrada no disponible");

    public Uri? PreviewMediaSource
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_previewState.OutputPath))
            {
                return null;
            }

            return Uri.TryCreate(_previewState.OutputPath, UriKind.Absolute, out var uri)
                ? uri
                : null;
        }
    }

    public Visibility EmbeddedPreviewVisibility =>
        PreviewMediaSource is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility PreviewMetricsHeaderVisibility =>
        IsPreviewGenerating ? Visibility.Visible : Visibility.Collapsed;

    public string PreviewEngineText => Text(
        "Preview engine: FFmpeg source clip + bundled Python/iw3",
        "Motor de vista previa: clip fuente FFmpeg + Python/iw3 incluido");

    public string PreviewRunningWithText => Text(
        "Running with: FFmpeg source clip + bundled Python/iw3",
        "Ejecutando con: clip fuente FFmpeg + Python/iw3 incluido");

    public string PreviewGpuMetricsNoteText => Text(
        "GPU metrics show global adapter activity, not guaranteed per-process attribution.",
        "Las metricas de GPU muestran actividad global del adaptador, no atribucion garantizada por proceso.");

    public string PreviewStageText => ConversionPlanLabelValue(
        "Stage",
        "Etapa",
        Text(_previewStageEnglishText, _previewStageSpanishText));

    public string PreviewCpuUsageText => _previewCpuUsageText;

    public string PreviewRamUsageText => _previewRamUsageText;

    public string PreviewGpuUsageText => _previewGpuUsageText;

    public string PreviewVramUsageText => _previewVramUsageText;

    public string PreviewGpuMetricsStatusText => _previewGpuMetricsStatusText;

    public string PreviewStatusText => ConversionPlanLabelValue(
        "Preview status",
        "Estado de vista previa",
        PreviewStatusValueText);

    public string PreviewGateStatusText
    {
        get
        {
            var gate = PreviewConversionGate.Evaluate(_previewState, CreateCurrentPreviewConfiguration());
            return ConversionPlanLabelValue(
                "Preview status",
                "Estado de vista previa",
                Text(gate.EnglishStatus, gate.SpanishStatus));
        }
    }

    public string PreviewGateDetailText
    {
        get
        {
            var gate = PreviewConversionGate.Evaluate(_previewState, CreateCurrentPreviewConfiguration());
            return Text(gate.EnglishDetail, gate.SpanishDetail);
        }
    }

    public string PreviewDurationText => ConversionPlanLabelValue(
        "Preview duration",
        "Duracion de vista previa",
        CurrentPreviewDuration.ToString(@"hh\:mm\:ss"));

    public string PreviewStartTimeText => ConversionPlanLabelValue(
        "Preview start time",
        "Tiempo de inicio de vista previa",
        CurrentPreviewStartTime.ToString(@"hh\:mm\:ss"));

    public string PreviewFromLabel => Text("From", "Desde");

    public string PreviewToLabel => Text("To", "Hasta");

    public string PreviewFromText
    {
        get => _previewFromText;
        set
        {
            if (string.Equals(_previewFromText, value, StringComparison.Ordinal))
            {
                return;
            }

            if (TryDeferPreviewInvalidatingChange(() => PreviewFromText = value))
            {
                OnPropertyChanged();
                return;
            }

            if (SetProperty(ref _previewFromText, value))
            {
                PreviewTimeRangeChanged();
            }
        }
    }

    public string PreviewToText
    {
        get => _previewToText;
        set
        {
            if (string.Equals(_previewToText, value, StringComparison.Ordinal))
            {
                return;
            }

            if (TryDeferPreviewInvalidatingChange(() => PreviewToText = value))
            {
                OnPropertyChanged();
                return;
            }

            if (SetProperty(ref _previewToText, value))
            {
                PreviewTimeRangeChanged();
            }
        }
    }

    public string PreviewTimeRangeText => ConversionPlanLabelValue(
        "Preview duration",
        "Duracion de vista previa",
        CurrentPreviewTimeRangeValidation.Range is { } range
            ? range.Duration.ToString(@"hh\:mm\:ss")
            : "-");

    public string PreviewMaximumDurationText => Text(
        "Maximum preview duration is 1 minute 30 seconds",
        "La duracion maxima de la vista previa es de 1 minuto 30 segundos");

    public string PreviewTimeRangeValidationText =>
        PreviewTimeRangeValidationMessage(CurrentPreviewTimeRangeValidation.Issue);

    public Visibility PreviewTimeRangeValidationVisibility =>
        CurrentPreviewTimeRangeValidation.IsValid ? Visibility.Collapsed : Visibility.Visible;

    public bool CanEditPreviewTimeRange =>
        HasCompletedAnalysis &&
        _analysis?.File.Duration is not null &&
        !IsConversionRunning &&
        !IsPreviewGenerating &&
        !IsModelPackImportRunning &&
        !IsPreviewRangeEditingBlockedByModal;

    public string PreviewOutdatedText => Text(
        "Preview outdated",
        "La vista previa esta desactualizada");

    public Visibility PreviewOutdatedVisibility =>
        _previewState.Status == PreviewGenerationStatus.Outdated
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string PreviewOutputPathText => string.IsNullOrWhiteSpace(_previewState.OutputPath)
        ? string.Empty
        : ConversionPlanLabelValue(
            "Preview output",
            "Salida de vista previa",
            _previewState.OutputPath);

    public Visibility PreviewOutputPathVisibility =>
        string.IsNullOrWhiteSpace(_previewState.OutputPath)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public bool IsPreviewGenerating => _previewState.IsGenerating;

    public bool CanGeneratePreview =>
        HasEnteredPreviewConversionStage &&
        CanEditPreviewTimeRange &&
        _previewCancellationTokenSource is null &&
        CurrentPreviewTimeRangeValidation.IsValid &&
        _conversionPlan?.SelectedLocalModel is not null &&
        CurrentExecutionRequestCanStart();

    public bool CanCancelPreview => IsPreviewGenerating;

    public bool CanOpenPreview =>
        !IsConversionRunning &&
        !IsModelPackImportRunning &&
        _previewState.Status == PreviewGenerationStatus.Ready &&
        IsPreviewFingerprintCurrent();

    public bool CanDeletePreview =>
        !IsConversionRunning &&
        !IsPreviewGenerating &&
        !IsModelPackImportRunning &&
        _previewState.Status is
            PreviewGenerationStatus.Ready or
            PreviewGenerationStatus.Accepted or
            PreviewGenerationStatus.Failed or
            PreviewGenerationStatus.Canceled or
            PreviewGenerationStatus.Outdated;

    public bool CanContinuePreview =>
        _previewState.Status == PreviewGenerationStatus.Ready &&
        IsPreviewFingerprintCurrent();

    public string PreviewModalDetailText => Text(
        _previewState.EnglishDetail,
        _previewState.SpanishDetail);

    public string PreviewGenerationLogText => _previewGenerationLogTextBuilder.ToString();

    private bool IsCurrentPreviewAccepted =>
        PreviewConversionGate.Evaluate(_previewState, CreateCurrentPreviewConfiguration()).CanStart &&
        PreviewOutputFileExists();

    private PreviewTimeRangeValidationResult CurrentPreviewTimeRangeValidation =>
        PreviewTimeRangeService.Validate(
            PreviewFromText,
            PreviewToText,
            _analysis?.File.Duration);

    private TimeSpan CurrentPreviewDuration =>
        CurrentPreviewTimeRangeValidation.Range?.Duration ??
        PreviewTimeRangeService.DefaultDuration;

    private TimeSpan CurrentPreviewStartTime =>
        CurrentPreviewTimeRangeValidation.Range?.From ?? TimeSpan.Zero;

    private bool IsPreviewRangeEditingBlockedByModal =>
        IsTechnicalDetailsModalOpen ||
        IsProfileDetailsModalOpen ||
        IsModelHelpModalOpen ||
        IsReplaceVideoConfirmationModalOpen ||
        IsPreviewInvalidationConfirmationModalOpen ||
        IsModelInventoryModalOpen ||
        IsModelPackImportConfirmationModalOpen ||
        IsActivityLogModalOpen ||
        IsPreviewGeneratingModalOpen ||
        IsPreviewReadyModalOpen ||
        IsConversionCompletedModalOpen;

    private string PreviewStatusValueText => _previewState.Status switch
    {
        PreviewGenerationStatus.NotGenerated => Text("Preview required", "Vista previa requerida"),
        PreviewGenerationStatus.Generating => Text("Preview generation is running.", "La generacion de vista previa esta en ejecucion."),
        PreviewGenerationStatus.Ready => Text("Preview ready. Review it before continuing.", "Vista previa lista. Revisala antes de continuar."),
        PreviewGenerationStatus.Accepted => Text("Preview accepted", "Vista previa aceptada"),
        PreviewGenerationStatus.Failed => Text("Preview failed.", "Vista previa fallida."),
        PreviewGenerationStatus.Canceled => Text("Preview canceled.", "Vista previa cancelada."),
        PreviewGenerationStatus.Outdated => Text("Preview outdated", "Vista previa desactualizada"),
        _ => _previewState.Status.ToString(),
    };

    public string ConversionProgressTitle => Text(
        "Conversion progress",
        "Progreso de conversión");

    public string ConversionExecutionStatusLabel => Text("Status", "Estado");

    public string ConversionExecutionStatusText => _conversionExecutionState.Status switch
    {
        ConversionExecutionStatus.NotStarted => Text("Not started", "No iniciada"),
        ConversionExecutionStatus.Ready => Text("Ready", "Lista"),
        ConversionExecutionStatus.Blocked => Text("Blocked", "Bloqueada"),
        ConversionExecutionStatus.Running => Text("Running", "En ejecución"),
        ConversionExecutionStatus.Canceling => Text("Canceling", "Cancelando"),
        ConversionExecutionStatus.Canceled => Text("Canceled", "Cancelada"),
        ConversionExecutionStatus.Failed => Text("Failed", "Fallida"),
        ConversionExecutionStatus.Completed => Text("Completed", "Completada"),
        _ => _conversionExecutionState.Status.ToString(),
    };

    public string ConversionExecutionStepLabel => Text("Current step", "Paso actual");

    public string ConversionExecutionStepText => Text(
        _conversionExecutionState.CurrentStep.EnglishText,
        _conversionExecutionState.CurrentStep.SpanishText);

    public string ConversionExecutionProgressLabel => Text("Progress", "Progreso");

    public int ConversionExecutionProgressPercent => _conversionExecutionState.ProgressPercent;

    public string ConversionExecutionProgressText => $"{ConversionExecutionProgressPercent}%";

    public Visibility ConversionProgressBarVisibility =>
        IsConversionRunning ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ConversionRunningStatusVisibility =>
        IsConversionRunning ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ConversionTimingEstimatesVisibility =>
        IsConversionRunning ? Visibility.Visible : Visibility.Collapsed;

    public double ConversionProgressBarValue => ConversionExecutionProgressPercent;

    public string ConversionProgressBarText => ConversionExecutionProgressText;

    public string ConversionExecutionDetailText => Text(
        _conversionExecutionState.DetailEnglish,
        _conversionExecutionState.DetailSpanish);

    public string ConversionElapsedLabelText => Text("Elapsed", "Transcurrido");

    public string ConversionRemainingLabelText => Text("Remaining", "Restante");

    public string ConversionEstimatedTotalLabelText => Text("Estimated total", "Total estimado");

    public string ConversionElapsedValueText => IsConversionRunning
        ? FormatDuration(GetCurrentConversionElapsed())
        : "-";

    public string ConversionRemainingValueText => IsConversionRunning
        ? _conversionTimingEstimate?.Remaining is { } remaining
            ? FormatDuration(remaining)
            : Text("Estimating...", "Calculando...")
        : "-";

    public string ConversionEstimatedTotalValueText => IsConversionRunning
        ? _conversionTimingEstimate?.EstimatedTotal is { } total
            ? FormatDuration(total)
            : Text("Estimating...", "Calculando...")
        : "-";

    public bool CanCancelConversion => _conversionExecutionState.CanCancel;

    public string CancelConversionText => Text("Cancel", "Cancelar");

    public bool IsConversionRunning =>
        _conversionExecutionState.Status is
            ConversionExecutionStatus.Running or
            ConversionExecutionStatus.Canceling;

    public Visibility NormalSetupVisibility =>
        IsConversionRunning ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ConversionRunningVisibility =>
        IsConversionRunning ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ActivityLogVisibility =>
        IsConversionRunning ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ConversionSummaryVisibility =>
        IsConversionRunning ? Visibility.Visible : Visibility.Collapsed;

    public string ConversionRunningTitle => Text(
        "Live conversion",
        "Conversion en vivo");

    public string ConversionRunningStatusText => Text("Converting...", "Convirtiendo...");

    public string ConversionLiveLogEmptyText => Text(
        "Waiting for local iw3 output...",
        "Esperando salida local de iw3...");

    public string ConversionSummaryTitle => Text(
        "Conversion summary",
        "Resumen de conversion");

    public string ConversionSummaryPresetText => ConversionPlanLabelValue(
        "Output profile",
        "Perfil de salida",
        OutputProfileDisplayText);

    public string ConversionSummaryOutputContainerText => ConversionPlanLabelValue(
        "Output container",
        "Contenedor de salida",
        SelectedOutputContainer.ToString());

    public string ConversionSummaryQualityText => ConversionPlanLabelValue(
        "Quality",
        "Calidad",
        QualityPresetText(SelectedQualityPreset, IsSpanish));

    public string ConversionSummaryIntensityText => ConversionPlanLabelValue(
        "3D intensity",
        "Intensidad 3D",
        ThreeDIntensityText(SelectedThreeDIntensity, IsSpanish));

    public string ConversionSummaryLayoutText => ConversionPlanLabelValue(
        "3D layout",
        "Diseno 3D",
        ThreeDOutputFormatText(SelectedThreeDOutputFormat, IsSpanish));

    public string ConversionSummaryLocalModelText => ConversionPlanLabelValue(
        "Local 3D/depth model",
        "Modelo local 3D/profundidad",
        SelectedLocalModelCandidate?.DisplayName);

    public string ConversionSummaryOutputPathText => ConversionPlanLabelValue(
        "Primary output",
        "Salida principal",
        _conversionPlan?.SuggestedOutputPath);

    public string ConversionSummaryLgCompatibilityCopyText =>
        ConversionPlanLabelValue(
            "LG-compatible copy",
            "Copia compatible LG",
            GetLgCompatibilityCopyPath());

    public string ConversionSummaryCurrentStatusText => ConversionPlanLabelValue(
        "Current status",
        "Estado actual",
        IsConversionRunning ? ConversionRunningStatusText : ConversionExecutionStatusText);

    public string CpuUsageText => _cpuUsageText;

    public string RamUsageText => _ramUsageText;

    public string GpuUsageText => _gpuUsageText;

    public string VramUsageText => _vramUsageText;

    public string ConversionReadinessTitle => Text(
        "Conversion readiness",
        "Estado de conversión");

    public string SystemStatusTitle => Text(
        "System status",
        "Estado del sistema");

    public string SystemStatusToolsTabTitle => Text("Tools", "Herramientas");

    public string SystemStatusConversionTabTitle => Text("Conversion", "Conversión");

    public string SystemStatusTechnicalDetailsTitle => Text(
        "Technical details",
        "Detalles técnicos");

    public string SystemStatusDetailsButtonText => Text("Details", "Detalles");

    public string CloseDialogText => Text("Close", "Cerrar");

    public string CancelDialogText => Text("Cancel", "Cancelar");

    public string ReplaceSelectedVideoTitleText => Text(
        "Replace selected video?",
        "¿Reemplazar video seleccionado?");

    public string ReplaceSelectedVideoBodyText => Text(
        "Selecting a new video will clear the current analysis, recommended setup, conversion plan, and custom output path before analyzing the new video. Plan options may update when the new recommendation is prepared.",
        "Seleccionar un nuevo video borrará el análisis actual, la configuración recomendada, el plan de conversión y la ruta de salida personalizada antes de analizar el nuevo video. Las opciones del plan pueden actualizarse cuando se prepare la nueva recomendación.");

    public string ReplaceVideoConfirmText => Text("Replace", "Reemplazar");

    public string PreviewInvalidationConfirmationTitleText => Text(
        "Changing this setting requires a new preview",
        "Este cambio requiere generar un nuevo preview");

    public string PreviewInvalidationConfirmationBodyText => Text(
        "You already generated a preview for the current setup. Changing this setting will invalidate that preview and you will need to generate it again before final conversion.",
        "Ya generaste un preview para la configuracion actual. Si cambias esta opcion, ese preview dejara de ser valido y tendras que generarlo de nuevo antes de la conversion final.");

    public string PreviewInvalidationConfirmText => Text(
        "Change setting",
        "Cambiar configuracion");

    public string ModelPackImportConfirmationTitleText => Text(
        _modelPackImportConfirmationPrompt?.EnglishTitle ?? "Confirm model pack import",
        _modelPackImportConfirmationPrompt?.SpanishTitle ?? "Confirmar importacion de paquete de modelos");

    public string ModelPackImportConfirmationIntroText => Text(
        "Review this model pack before Windows asks for administrator permission.",
        "Revisa este paquete de modelos antes de que Windows pida permiso de administrador.");

    public string ModelPackImportConfirmationMessageText => Text(
        _modelPackImportConfirmationPrompt?.EnglishMessage ?? string.Empty,
        _modelPackImportConfirmationPrompt?.SpanishMessage ?? string.Empty);

    public string ModelPackImportConfirmationContinueText => Text("Continue", "Continuar");

    public string ConversionCompletedTitleText => Text(
        "Conversion complete",
        "Conversi\u00f3n finalizada");

    public string ConversionCompletedBodyText => Text(
        "The 3D video was created successfully.",
        "El video 3D se cre\u00f3 correctamente.");

    public string ConversionCompletedOutputPathText => ConversionPlanLabelValue(
        "Output path",
        "Ruta de salida",
        CompletedConversionOutputPath);

    public string AcceptConversionCompletedText => Text("OK", "Aceptar");

    public string CompletedConversionOutputPath => _completedConversionOutputPath;

    public string ProfileDetailsTitleText => Text(
        "Profile details",
        "Detalles del perfil");

    public string ProfileDetailsButtonText => "?";

    public string ViewLogText => Text("View log", "Ver log");

    public string CopyFullLogText => Text("Copy full log", "Copiar todo el log");

    public string CopyPreviewLogText => Text(
        "Copy preview log",
        "Copiar log de vista previa");

    public string LogCopiedText => Text("Log copied", "Log copiado");

    public string CouldNotCopyLogText => Text(
        "Could not copy log",
        "No se pudo copiar el log");

    public string LogCopyNotificationText =>
        Text(_logCopyNotificationEnglishText, _logCopyNotificationSpanishText);

    public Visibility LogCopyNotificationVisibility =>
        _isLogCopyNotificationVisible ? Visibility.Visible : Visibility.Collapsed;

    public string ActivityLogPanelText => CreateFullActivityLogText();

    public string ActivityLogModalText
    {
        get => _activityLogModalText;
        private set => SetProperty(ref _activityLogModalText, value);
    }

    public string ActiveModalTitleText
    {
        get
        {
            if (IsPreviewGeneratingModalOpen)
            {
                return PreviewGeneratingTitleText;
            }

            if (IsPreviewReadyModalOpen)
            {
                return PreviewReadyTitleText;
            }

            if (IsActivityLogModalOpen)
            {
                return ActiveActivityLogModalTitleText;
            }

            if (IsSettingsModalOpen)
            {
                return SettingsTitleText;
            }

            if (IsReplaceVideoConfirmationModalOpen)
            {
                return ReplaceSelectedVideoTitleText;
            }

            if (IsPreviewInvalidationConfirmationModalOpen)
            {
                return PreviewInvalidationConfirmationTitleText;
            }

            if (IsModelPackImportConfirmationModalOpen)
            {
                return ModelPackImportConfirmationTitleText;
            }

            if (IsConversionCompletedModalOpen)
            {
                return ConversionCompletedTitleText;
            }

            if (IsModelInventoryModalOpen)
            {
                return ModelInventoryTitleText;
            }

            if (IsModelHelpModalOpen)
            {
                return ModelHelpTitleText;
            }

            return IsProfileDetailsModalOpen
                ? ProfileDetailsTitleText
                : SystemStatusTechnicalDetailsTitle;
        }
    }

    public bool IsAnyModalOpen =>
        IsTechnicalDetailsModalOpen ||
        IsProfileDetailsModalOpen ||
        IsModelHelpModalOpen ||
        IsSettingsModalOpen ||
        IsReplaceVideoConfirmationModalOpen ||
        IsPreviewInvalidationConfirmationModalOpen ||
        IsModelInventoryModalOpen ||
        IsModelPackImportConfirmationModalOpen ||
        IsConversionCompletedModalOpen ||
        IsActivityLogModalOpen ||
        IsPreviewGeneratingModalOpen ||
        IsPreviewReadyModalOpen;

    public Visibility ModalOverlayVisibility =>
        IsAnyModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public double ActiveModalWidth => IsModelInventoryModalOpen || IsModelHelpModalOpen || IsSettingsModalOpen ? 1000d : 760d;

    public double ActiveModalHeight => IsSettingsModalOpen ? 650d : double.NaN;

    public bool IsTechnicalDetailsModalOpen
    {
        get => _isTechnicalDetailsModalOpen;
        private set
        {
            if (SetProperty(ref _isTechnicalDetailsModalOpen, value))
            {
                RaiseModalStatePropertiesChanged();
            }
        }
    }

    public bool IsProfileDetailsModalOpen
    {
        get => _isProfileDetailsModalOpen;
        private set
        {
            if (SetProperty(ref _isProfileDetailsModalOpen, value))
            {
                RaiseModalStatePropertiesChanged();
            }
        }
    }

    public bool IsModelHelpModalOpen
    {
        get => _isModelHelpModalOpen;
        private set
        {
            if (SetProperty(ref _isModelHelpModalOpen, value))
            {
                RaiseModalStatePropertiesChanged();
            }
        }
    }

    public bool IsSettingsModalOpen
    {
        get => _isSettingsModalOpen;
        private set
        {
            if (SetProperty(ref _isSettingsModalOpen, value))
            {
                RaiseModalStatePropertiesChanged();
            }
        }
    }

    public bool IsReplaceVideoConfirmationModalOpen
    {
        get => _isReplaceVideoConfirmationModalOpen;
        private set
        {
            if (SetProperty(ref _isReplaceVideoConfirmationModalOpen, value))
            {
                RaiseModalStatePropertiesChanged();
            }
        }
    }

    public bool IsPreviewInvalidationConfirmationModalOpen
    {
        get => _isPreviewInvalidationConfirmationModalOpen;
        private set
        {
            if (SetProperty(ref _isPreviewInvalidationConfirmationModalOpen, value))
            {
                RaiseModalStatePropertiesChanged();
            }
        }
    }

    public bool IsModelPackImportConfirmationModalOpen
    {
        get => _isModelPackImportConfirmationModalOpen;
        private set
        {
            if (SetProperty(ref _isModelPackImportConfirmationModalOpen, value))
            {
                RaiseModalStatePropertiesChanged();
            }
        }
    }

    public bool IsModelInventoryModalOpen
    {
        get => _isModelInventoryModalOpen;
        private set
        {
            if (SetProperty(ref _isModelInventoryModalOpen, value))
            {
                RaiseModalStatePropertiesChanged();
            }
        }
    }

    public bool IsActivityLogModalOpen
    {
        get => _isActivityLogModalOpen;
        private set
        {
            if (SetProperty(ref _isActivityLogModalOpen, value))
            {
                RaiseModalStatePropertiesChanged();
            }
        }
    }

    public bool IsPreviewGeneratingModalOpen
    {
        get => _isPreviewGeneratingModalOpen;
        private set
        {
            if (SetProperty(ref _isPreviewGeneratingModalOpen, value))
            {
                RaiseModalStatePropertiesChanged();
            }
        }
    }

    public bool IsPreviewReadyModalOpen
    {
        get => _isPreviewReadyModalOpen;
        private set
        {
            if (SetProperty(ref _isPreviewReadyModalOpen, value))
            {
                RaiseModalStatePropertiesChanged();
            }
        }
    }

    public bool IsConversionCompletedModalOpen
    {
        get => _isConversionCompletedModalOpen;
        private set
        {
            if (SetProperty(ref _isConversionCompletedModalOpen, value))
            {
                RaiseModalStatePropertiesChanged();
            }
        }
    }

    public Visibility TechnicalDetailsModalContentVisibility =>
        IsTechnicalDetailsModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ProfileDetailsModalContentVisibility =>
        IsProfileDetailsModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ModelHelpModalContentVisibility =>
        IsModelHelpModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SettingsModalContentVisibility =>
        IsSettingsModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ReplaceVideoConfirmationModalContentVisibility =>
        IsReplaceVideoConfirmationModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PreviewInvalidationConfirmationModalContentVisibility =>
        IsPreviewInvalidationConfirmationModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ModelPackImportConfirmationModalContentVisibility =>
        IsModelPackImportConfirmationModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ModelInventoryModalContentVisibility =>
        IsModelInventoryModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ActivityLogModalContentVisibility =>
        IsActivityLogModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PreviewGeneratingModalContentVisibility =>
        IsPreviewGeneratingModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PreviewReadyModalContentVisibility =>
        IsPreviewReadyModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ConversionCompletedModalContentVisibility =>
        IsConversionCompletedModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public string TechnicalDetailsBodyText
    {
        get => _technicalDetailsBodyText;
        private set => SetProperty(ref _technicalDetailsBodyText, value);
    }

    public string LogsDiagnosticsTechnicalDetailsTitleText => Text(
        "Technical details",
        "Detalles tecnicos");

    public string LogsDiagnosticsTechnicalDetailsText => CreateSystemStatusTechnicalDetailsText();

    public string ProfileDetailsBodyText => string.Join(
        Environment.NewLine,
        [
            PresetName,
            PresetDescriptionText,
            PresetBestForText,
            string.Empty,
            PresetTechnicalRecommendationTitle,
            PresetContainerText,
            PresetVideoCodecText,
            PresetAudioCodecText,
            PresetResolutionText,
            PresetThreeDLayoutText,
            PresetAdvancedOutputText,
            string.Empty,
            PresetCompatibilityNoteText,
            string.Empty,
            TvPlaybackTitle,
            TvPlaybackInstructions,
        ]);

    public string ConversionReadinessEmptyText => Text(
        "Analyze a video to see conversion readiness.",
        "Analiza un video para ver el estado de conversión.");

    public bool ShowConversionReadinessCard =>
        _workflowState.ShowConversionReadinessCard(_conversionExecutionState.Status);

    public Visibility ConversionReadinessVisibility =>
        ShowConversionReadinessCard ? Visibility.Visible : Visibility.Collapsed;

    public bool ShowConversionProgressCard =>
        ConversionWorkflowState.ShowConversionProgressCard(_conversionExecutionState.Status);

    public Visibility ConversionProgressVisibility =>
        ShowConversionProgressCard ? Visibility.Visible : Visibility.Collapsed;

    public string ConversionReadinessStatusLabel => Text("Status", "Estado");

    public string ConversionReadinessMissingRequirementsTitle => Text(
        "Missing requirements",
        "Requisitos faltantes");

    public Visibility ConversionMissingRequirementsVisibility =>
        ShouldShowConversionMissingRequirements()
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewConversionMissingToolsVisibility =>
        HasEnteredPreviewConversionStage && ShouldShowConversionMissingRequirements()
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string PreviewConversionMissingToolsText => Text(
        "Open Settings to review missing tools.",
        "Abre Ajustes para revisar herramientas faltantes.");

    public string OpenSettingsForToolsText => Text(
        "Open Settings",
        "Abrir ajustes");

    public string ConversionReadinessStatusText
    {
        get
        {
            if (IsConversionRunning)
            {
                return _conversionExecutionState.Status == ConversionExecutionStatus.Running
                    ? Text("Converting", "Convirtiendo")
                    : ConversionExecutionStatusText;
            }

            if (IsPreviewGenerating)
            {
                return PreviewStatusValueText;
            }

            var startGate = EvaluateConversionStartGate();
            if (!startGate.CanStart)
            {
                return Text(startGate.EnglishStatus, startGate.SpanishStatus);
            }

            return Text(
                _conversionReadiness?.EnglishStatus ??
                "Conversion unavailable. Required local components are missing.",
                _conversionReadiness?.SpanishStatus ??
                "Conversión no disponible. Faltan componentes locales requeridos.");
        }
    }

    public string ConversionReadinessIssuesText => _conversionReadiness is null
        ? "-"
        : _conversionReadiness.Issues.Count == 0
            ? Text("No missing requirements.", "No hay requisitos faltantes.")
            : string.Join(
                Environment.NewLine,
                _conversionReadiness.Issues.Select(issue =>
                    $"- {Text(issue.EnglishMessage, issue.SpanishMessage)}"));

    public string ConversionReadinessMissingComponentsSummaryText =>
        _dependencyHealth is null || !HasCompletedAnalysis || IsConversionRunning || IsPreviewGenerating
            ? "-"
            : CreateMissingComponentsSummary();

    public string ConversionReadinessRequiredComponentsText => _conversionReadiness is null
            || IsConversionRunning
            || IsPreviewGenerating
        ? string.Empty
        : Text(
            _conversionReadiness.EnglishRequiredComponentsSummary,
            _conversionReadiness.SpanishRequiredComponentsSummary);

    public string ConversionBlockedReasonText
    {
        get
        {
            if (IsConversionRunning)
            {
                return ConversionExecutionDetailText;
            }

            if (IsPreviewGenerating)
            {
                return PreviewModalDetailText;
            }

            var startGate = EvaluateConversionStartGate();
            return startGate.CanStart
                ? string.Empty
                : Text(startGate.EnglishDetail, startGate.SpanishDetail);
        }
    }

    private bool ShouldShowConversionMissingRequirements()
    {
        if (IsConversionRunning || IsPreviewGenerating)
        {
            return false;
        }

        var startGate = EvaluateConversionStartGate();
        return !startGate.CanStart &&
            startGate.Blocker != ConversionExecutionBlocker.PreviewRequired;
    }

    private string? FormatConversionReadySelectedModel()
    {
        var selectedModel = _conversionPlan?.SelectedLocalModel;
        if (selectedModel is null)
        {
            return null;
        }

        var displayName = IsSpanish
            ? GetSpanishModelDisplayName(selectedModel)
            : selectedModel.DisplayName;
        return string.IsNullOrWhiteSpace(selectedModel.Iw3DepthModelName)
            ? displayName
            : $"{displayName} / {selectedModel.Iw3DepthModelName}";
    }

    private string? FormatConversionReadyOutput() =>
        _conversionPlan is null
            ? null
            : $"{_conversionPlan.OutputContainer} \u00b7 {ThreeDOutputFormatText(_conversionPlan.ThreeDOutputFormat, IsSpanish)} \u00b7 {_conversionPlan.Width}x{_conversionPlan.Height}";

    private ConversionTimeEstimate CreateCurrentConversionTimeEstimate() =>
        _conversionTimeEstimator.Estimate(
            CreateCurrentConversionEstimateInput(),
            _performanceHistory);

    private OutputSizeEstimate CreateCurrentOutputSizeEstimate()
    {
        if (_analysis is null)
        {
            return _outputSizeEstimator.Estimate(null);
        }

        return _outputSizeEstimator.Estimate(new(
            Duration: _analysis.File.Duration,
            OutputContainer: SelectedOutputContainer,
            QualityPreset: SelectedQualityPreset,
            OutputPresetId: SelectedOutputPreset.Id,
            TargetWidth: _conversionPlan?.Width ?? SelectedOutputPreset.Recommendation.Width,
            TargetHeight: _conversionPlan?.Height ?? SelectedOutputPreset.Recommendation.Height,
            IncludeTemporaryWorkingSpace: true));
    }

    private ConversionEstimateInput? CreateCurrentConversionEstimateInput()
    {
        if (_analysis is null)
        {
            return null;
        }

        var selectedModel = _conversionPlan?.SelectedLocalModel;
        return ConversionEstimateInput.FromAnalysis(
            _analysis,
            selectedModel?.MappingKey ?? SelectedLocalModelCandidate?.MappingKey,
            selectedModel?.DisplayName ?? SelectedLocalModelCandidate?.DisplayName,
            SelectedOutputPreset.Id,
            Text(SelectedOutputPreset.Name, SelectedOutputPreset.SpanishName),
            SelectedOutputContainer,
            SelectedQualityPreset,
            SelectedThreeDOutputFormat,
            GetDeviceCapabilityBucket());
    }

    private string CreateSelectedModelGuidanceText()
    {
        if (SelectedLocalModelCandidate is null)
        {
            return Text(
                "Model guidance: v3dfy includes a usable base model when the bundled base model is selected. Optional model packs can be imported later.",
                "Guia de modelo: v3dfy incluye un modelo base utilizable cuando se selecciona el modelo base incluido. Los paquetes opcionales pueden importarse despues.");
        }

        var entry = FindRegistryEntry(SelectedLocalModelCandidate);
        var guidance = _modelGuidanceService.Create(
            SelectedLocalModelCandidate.MappingKey,
            SelectedLocalModelCandidate.Iw3DepthModelName,
            SelectedLocalModelCandidate.DisplayName,
            entry?.IsEmbeddedBase == true);
        return Text(
            $"Model guidance: {guidance.EnglishHeadline}. Good for: {guidance.EnglishBestFor}. Speed: {guidance.EnglishSpeed}. Quality: {guidance.EnglishQuality}. Size: {guidance.EnglishSize}.",
            $"Guia de modelo: {guidance.SpanishHeadline}. Bueno para: {guidance.SpanishBestFor}. Velocidad: {guidance.SpanishSpeed}. Calidad: {guidance.SpanishQuality}. Tamano: {guidance.SpanishSize}.");
    }

    private string GetDeviceCapabilityBucket()
    {
        if (_lastProcessMetricSample?.GpuUsagePercent is not null ||
            _lastPreviewMetricSample?.GpuUsagePercent is not null)
        {
            return "GPU observed locally";
        }

        return _dependencyHealth?.IsComplete == true || _toolHealth?.IsComplete == true
            ? "local engine ready"
            : "hardware not benchmarked yet";
    }

    private static string ConfidenceText(
        ConversionEstimateConfidence confidence,
        bool useSpanish) => confidence switch
    {
        ConversionEstimateConfidence.High => useSpanish ? "Alta" : "High",
        ConversionEstimateConfidence.Medium => useSpanish ? "Media" : "Medium",
        ConversionEstimateConfidence.Low => useSpanish ? "Baja" : "Low",
        _ => useSpanish ? "No disponible" : "Unavailable",
    };

    private static string FormatEstimateRange(TimeSpan low, TimeSpan high) =>
        $"~{FormatDurationCompact(low)}-{FormatDurationCompact(high)}";

    private static string FormatDurationCompact(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{Math.Max(1, (int)Math.Round(duration.TotalHours))} h";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))} min";
        }

        return $"{Math.Max(1, (int)Math.Round(duration.TotalSeconds))} sec";
    }

    private static string FormatByteRange(long lowBytes, long highBytes) =>
        $"~{FormatBytes(lowBytes)}-{FormatBytes(highBytes)}";

    private static string FormatBytes(long bytes)
    {
        const double gib = 1024d * 1024d * 1024d;
        const double mib = 1024d * 1024d;
        if (bytes >= gib)
        {
            return $"{bytes / gib:0.0} GB";
        }

        return $"{bytes / mib:0} MB";
    }

    public bool CanStartConversion =>
        HasEnteredPreviewConversionStage &&
        _conversionExecutionState.Status is not ConversionExecutionStatus.Running and
            not ConversionExecutionStatus.Canceling &&
        !IsPreviewGenerating &&
        !IsModelPackImportRunning &&
        EvaluateConversionStartGate().CanStart &&
        CurrentExecutionRequestCanStart();

    public bool CanStartOrCancelConversion =>
        IsConversionRunning
            ? _conversionExecutionState.Status == ConversionExecutionStatus.Running
            : CanStartConversion;

    public string StartConversionText => _conversionExecutionState.Status switch
    {
        ConversionExecutionStatus.Running => Text("Cancel", "Cancelar"),
        ConversionExecutionStatus.Canceling => Text("Canceling...", "Cancelando..."),
        _ => Text("Convert", "Convertir"),
    };

    public bool CanUseSystemStatusActions => !IsConversionRunning && !IsPreviewGenerating && !IsModelPackImportRunning;

    public string ToolStatusTitle => Text(
        "Internal tool status",
        "Estado de herramientas internas");

    public string RefreshText => Text("Refresh", "Actualizar");

    public string OpenEngineFolderText => Text(
        "Open engine folder",
        "Abrir carpeta del motor");

    public string ActivityLogTitle => Text("Activity log", "Registro de actividad");

    public string ActiveActivityLogModalTitleText =>
        _activeActivityLogModalKind == ActivityLogModalKind.Image ? ImageActivityLogTitleText : ActivityLogTitle;

    public string ClearText => Text("Clear", "Limpiar");

    public string RecommendedPresetTitle => Text(
        "Output profile details",
        "Detalles del perfil de salida");

    public string OutputPresetLabel => Text("Output profile", "Perfil de salida");

    public string PresetName => Text(SelectedOutputPreset.Name, SelectedOutputPreset.SpanishName);

    public string PresetDescriptionText => Text("Description", "Descripción") +
        $": {Text(SelectedOutputPreset.Description, SelectedOutputPreset.SpanishDescription)}";

    public string PresetBestForText => Text("Best for", "Ideal para") +
        $": {Text(SelectedOutputPreset.BestFor, SelectedOutputPreset.SpanishBestFor)}";

    public string PresetTechnicalRecommendationTitle => Text(
        "Technical recommendation",
        "Recomendación técnica");

    public string PresetContainerText => Text("Recommended container", "Contenedor recomendado") +
        $": {SelectedOutputPreset.Recommendation.OutputContainer}";

    public string PresetVideoCodecText => Text("Codec", "Códec") +
        $": {SelectedOutputPreset.Recommendation.VideoCodec}";

    public string PresetAudioCodecText => Text("Audio", "Audio") +
        $": {SelectedOutputPreset.Recommendation.AudioCodec}";

    public string PresetResolutionText => Text("Target resolution", "Resolución objetivo") +
        $": {SelectedOutputPreset.Recommendation.Width}x" +
        $"{SelectedOutputPreset.Recommendation.Height}";

    public string PresetThreeDLayoutText => Text("Recommended 3D layout", "Diseño 3D recomendado") +
        $": {ThreeDOutputFormatText(SelectedOutputPreset.Recommendation.ThreeDOutputFormat, IsSpanish)}";

    public string PresetAdvancedOutputText => Text(
        "MKV: advanced/master primary output. LG compatibility: optional MP4 copy after the primary output succeeds.",
        "MKV: salida principal avanzada/maestra. Compatibilidad LG: copia MP4 opcional despues de completar la salida principal.");

    public string PresetCompatibilityNoteText => Text("Compatibility note", "Nota de compatibilidad") +
        $": {Text(SelectedOutputPreset.CompatibilityNote, SelectedOutputPreset.SpanishCompatibilityNote)}";

    public string TvPlaybackTitle => Text(
        SelectedOutputPreset.PlaybackTitle,
        SelectedOutputPreset.SpanishPlaybackTitle);

    public string TvPlaybackInstructions => Text(
        SelectedOutputPreset.PlaybackInstructions,
        SelectedOutputPreset.SpanishPlaybackInstructions);

    public ObservableCollection<ToolStatusItemViewModel> ToolStatuses { get; } = [];

    public ObservableCollection<LogEntryViewModel> Logs { get; } = [];

    public ObservableCollection<LogEntryViewModel> ImageLogs { get; } = [];

    public ObservableCollection<LogEntryViewModel> ConversionLogs { get; } = [];

    public ObservableCollection<string> PreviewGenerationLogs { get; } = [];

    public ObservableCollection<LocalModelSelectionCandidate> LocalModelCandidates { get; } = [];

    public RelayCommand SelectVideoCommand { get; }

    public AsyncRelayCommand AnalyzeCommand { get; }

    public AsyncRelayCommand RefreshEngineStatusCommand { get; }

    public RelayCommand ToggleSidebarCommand { get; }

    public RelayCommand SelectHomeSectionCommand { get; }

    public RelayCommand SelectImageConversionSectionCommand { get; }

    public RelayCommand SelectVideoConversionSectionCommand { get; }

    public RelayCommand SelectImageCommand { get; }

    public RelayCommand AnalyzeImageCommand { get; }

    public RelayCommand ClearImageLogCommand { get; }

    public RelayCommand SelectImageParallaxModeCommand { get; }

    public RelayCommand SelectImageStereoModeCommand { get; }

    public RelayCommand ToggleImageWorkflowChooserCommand { get; }

    public RelayCommand SelectImageModeSourceStepCommand { get; }

    public RelayCommand SelectImageSetupStepCommand { get; }

    public RelayCommand SelectImagePreviewExportStepCommand { get; }

    public RelayCommand ImageWizardBackCommand { get; }

    public RelayCommand ImageWizardNextCommand { get; }

    public RelayCommand ContinueWithImageConversionCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand OpenToolsEngineSettingsCommand { get; }

    public RelayCommand CloseSettingsCommand { get; }

    public RelayCommand WizardBackCommand { get; }

    public RelayCommand WizardNextCommand { get; }

    public RelayCommand ContinueWithConversionCommand { get; }

    public RelayCommand OpenEngineFolderCommand { get; }

    public RelayCommand OpenModelsFolderCommand { get; }

    public RelayCommand ShowModelInventoryCommand { get; }

    public RelayCommand CloseModelInventoryCommand { get; }

    public RelayCommand ShowModelHelpCommand { get; }

    public RelayCommand CloseModelHelpCommand { get; }

    public RelayCommand ClearLogsCommand { get; }

    public RelayCommand BrowseOutputFolderCommand { get; }

    public RelayCommand ResetOutputPathCommand { get; }

    public RelayCommand StartConversionCommand { get; }

    public RelayCommand CancelConversionCommand { get; }

    public AsyncRelayCommand GeneratePreviewCommand { get; }

    public RelayCommand CancelPreviewCommand { get; }

    public RelayCommand OpenPreviewCommand { get; }

    public RelayCommand DeletePreviewCommand { get; }

    public RelayCommand ContinuePreviewCommand { get; }

    public RelayCommand ViewActivityLogCommand { get; }

    public RelayCommand ViewImageActivityLogCommand { get; }

    public RelayCommand CopyFullLogCommand { get; }

    public RelayCommand CopyPreviewLogCommand { get; }

    public RelayCommand CloseActivityLogCommand { get; }

    public RelayCommand ShowTechnicalDetailsCommand { get; }

    public RelayCommand CloseTechnicalDetailsCommand { get; }

    public RelayCommand ShowProfileDetailsCommand { get; }

    public RelayCommand CloseProfileDetailsCommand { get; }

    public AsyncRelayCommand ImportModelPackCommand { get; }

    public RelayCommand ConfirmModelPackImportCommand { get; }

    public RelayCommand CancelModelPackImportCommand { get; }

    public RelayCommand ConfirmReplaceVideoCommand { get; }

    public RelayCommand CancelReplaceVideoCommand { get; }

    public RelayCommand ConfirmPreviewInvalidationCommand { get; }

    public RelayCommand CancelPreviewInvalidationCommand { get; }

    public RelayCommand AcceptConversionCompletedCommand { get; }

    public async void SelectDroppedVideo(string path)
    {
        if (IsConversionRunning || IsPreviewGenerating || IsModelPackImportRunning)
        {
            return;
        }

        if (!IsSupportedVideoFile(path))
        {
            AddLog(
                "The dropped file is not a supported video format.",
                "El archivo arrastrado no tiene un formato de video compatible.");
            return;
        }

        await TrySelectVideoAndAnalyzeAsync(path);
    }

    public void SelectDroppedImage(string path)
    {
        if (!IsImageConversionSectionSelected)
        {
            return;
        }

        TrySelectImage(path);
    }

    private bool IsSpanish =>
        string.Equals(SelectedLanguage, "Español", StringComparison.Ordinal);

    private async void SelectVideo()
    {
        if (IsConversionRunning || IsPreviewGenerating || IsModelPackImportRunning)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Text("Select a video", "Selecciona un video"),
            Filter = Text("Video files", "Archivos de video") +
                "|*.mp4;*.mkv;*.avi;*.mov;*.m4v;*.webm|" +
                Text("All files", "Todos los archivos") + "|*.*",
            Multiselect = false,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() == true)
        {
            await TrySelectVideoAndAnalyzeAsync(dialog.FileName);
        }
    }

    private void SelectImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Text("Select an image", "Selecciona una imagen"),
            Filter = Text("Image files", "Archivos de imagen") +
                "|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.webp|" +
                Text("All files", "Todos los archivos") + "|*.*",
            Multiselect = false,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() == true)
        {
            TrySelectImage(dialog.FileName);
        }
    }

    private void TrySelectImage(string path)
    {
        if (!IsSupportedImageFile(path))
        {
            AddImageLog(
                "The selected file is not a supported image format.",
                "El archivo seleccionado no tiene un formato de imagen compatible.");
            return;
        }

        SelectedImagePath = path;
        _selectedImageMetadata = null;
        ResetImageSetupState();
        _hasEnteredImagePreviewExportStage = false;
        SelectedImageConversionStep = ImageConversionStep.ModeAndSource;
        AddImageLog(
            $"Image selected: {Path.GetFileName(path)}",
            $"Imagen seleccionada: {Path.GetFileName(path)}");
        AddImageLog(
            "Analyze image to read metadata and unlock image setup.",
            "Analiza la imagen para leer metadata y habilitar configuracion.");

        RaiseImageConversionPropertiesChanged();
    }

    private void AnalyzeImage()
    {
        if (!HasSelectedImage || SelectedImagePath is null)
        {
            AddImageLog(
                "Select an image before analysis.",
                "Selecciona una imagen antes del analisis.");
            return;
        }

        try
        {
            _selectedImageMetadata = ReadImageMetadata(SelectedImagePath);
            _hasEnteredImagePreviewExportStage = false;
            SelectedImageConversionStep = ImageConversionStep.ModeAndSource;
            AddImageLog(
                $"Image metadata analyzed: {_selectedImageMetadata.WidthText} x {_selectedImageMetadata.HeightText}, {_selectedImageMetadata.Format}.",
                $"Metadata de imagen analizada: {_selectedImageMetadata.WidthText} x {_selectedImageMetadata.HeightText}, {_selectedImageMetadata.Format}.");
        }
        catch (Exception exception)
        {
            _selectedImageMetadata = null;
            AddImageLog(
                $"Could not read image metadata: {exception.Message}",
                $"No se pudo leer la metadata de imagen: {exception.Message}");
        }

        RaiseImageConversionPropertiesChanged();
    }

    private static ImageMetadata ReadImageMetadata(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.IgnoreColorProfile,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.FirstOrDefault() ??
            throw new InvalidOperationException("The image does not contain a readable frame.");
        var width = frame.PixelWidth;
        var height = frame.PixelHeight;
        var fileSize = new FileInfo(path).Length;
        var format = decoder.CodecInfo?.FriendlyName;
        if (string.IsNullOrWhiteSpace(format))
        {
            format = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        }

        var pixelFormat = frame.Format.ToString();
        if (frame.Format.BitsPerPixel > 0)
        {
            pixelFormat = $"{pixelFormat} / {frame.Format.BitsPerPixel} bpp";
        }

        return new ImageMetadata(
            Width: width,
            Height: height,
            WidthText: width.ToString("N0"),
            HeightText: height.ToString("N0"),
            AspectRatio: FormatAspectRatio(width, height),
            Format: format,
            PixelFormat: pixelFormat,
            FileSizeText: FormatImageFileSize(fileSize));
    }

    private async Task AnalyzeAsync()
    {
        if (IsConversionRunning || IsPreviewGenerating || IsModelPackImportRunning)
        {
            return;
        }

        SelectedWorkflowTabIndex = 0;

        if (string.IsNullOrWhiteSpace(SelectedVideoPath))
        {
            AddLog(
                "Select a video before starting analysis.",
                "Selecciona un video antes de iniciar el análisis.");
            return;
        }

        ResetAnalysisState(clearOutputPath: false);
        await Task.Yield();
        await AnalyzeSelectedVideoAsync();
    }

    private async Task TrySelectVideoAndAnalyzeAsync(string path)
    {
        if (string.Equals(SelectedVideoPath, path, StringComparison.OrdinalIgnoreCase))
        {
            SelectedWorkflowTabIndex = 0;
            AddLog(
                "Starting automatic analysis.",
                "Iniciando análisis automático.");
            ResetAnalysisState(clearOutputPath: false);
            await Task.Yield();
            await AnalyzeSelectedVideoAsync();
            return;
        }

        var replacingVideo = HasSelectedVideo;
        if (replacingVideo &&
            ShouldConfirmPreviewInvalidatingChange() &&
            !await ConfirmPreviewInvalidatingChangeAsync())
        {
            AddLog(
                "Video replacement canceled.",
                "Reemplazo de video cancelado.");
            return;
        }

        if (replacingVideo &&
            !ShouldConfirmPreviewInvalidatingChange() &&
            !await ConfirmReplaceSelectedVideoAsync())
        {
            AddLog(
                "Video replacement canceled.",
                "Reemplazo de video cancelado.");
            return;
        }

        SetSelectedVideo(path, replacingVideo);
        AddLog(
            "Starting automatic analysis.",
            "Iniciando análisis automático.");
        await Task.Yield();
        await AnalyzeSelectedVideoAsync();
    }

    private Task<bool> ConfirmReplaceSelectedVideoAsync()
    {
        IsTechnicalDetailsModalOpen = false;
        IsProfileDetailsModalOpen = false;
        IsActivityLogModalOpen = false;
        IsPreviewReadyModalOpen = false;
        _replaceVideoConfirmationCompletion =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IsReplaceVideoConfirmationModalOpen = true;
        return _replaceVideoConfirmationCompletion.Task;
    }

    private async Task AnalyzeSelectedVideoAsync()
    {
        var inputPath = SelectedVideoPath;
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return;
        }

        IsAnalyzing = true;
        await Task.Yield();
        await RefreshEngineStatusAsync(logRefresh: true);

        try
        {
            var result = await _videoAnalysisService.AnalyzeAsync(new VideoAnalysisRequest(
                InputPath: inputPath,
                Timeout: TimeSpan.FromSeconds(30)));

            if (result.IsSuccess && result.Analysis is not null)
            {
                _analysis = result.Analysis;
                SetDefaultPreviewTimeRangeFromAnalysis();
                _conversionRecommendation = _recommendationService.Recommend(
                    _analysis,
                    SelectedOutputPreset);
                ApplyRecommendationDefaultsIfNeeded(_conversionRecommendation);
                RegenerateConversionPlan();
                MarkPreviewOutdatedIfNeeded();
                RaiseAnalysisPropertiesChanged();
                RaiseRecommendationPropertiesChanged();
                RaiseConversionPlanPropertiesChanged();
                HasCompletedAnalysis = true;
                AddLog(
                    "Video analysis completed.",
                    "Análisis de video completado.");
                AddLog(
                    "Recommended 3D setup generated.",
                    "Configuración 3D recomendada generada.");
                AddLog(
                    "Conversion plan prepared.",
                    "Plan de conversión preparado.");
                return;
            }

            LogAnalysisFailure(result.Failure);
        }
        catch (Exception exception)
        {
            AddLog(
                $"Video analysis failed unexpectedly: {exception.Message}",
                $"El análisis de video falló inesperadamente: {exception.Message}");
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    private async Task RefreshEngineStatusAsync(bool logRefresh)
    {
        if (IsConversionRunning || IsPreviewGenerating)
        {
            return;
        }

        var dependencyHealth = await Task
            .Run(() => _healthChecker.CheckDetailed(_toolPaths))
            .ConfigureAwait(true);
        if (IsConversionRunning || IsPreviewGenerating)
        {
            return;
        }

        _dependencyHealth = dependencyHealth;
        _toolHealth = _dependencyHealth.Summary;
        UpdateLocalModelSelectionCandidates();
        UpdateToolStatuses();
        RaiseModelInventoryPropertiesChanged();
        UpdateConversionReadiness();
        if (logRefresh)
        {
            AddLog(
                "Internal tool status refreshed.",
                "Estado de herramientas internas actualizado.");
        }
    }

    private async Task RefreshEngineStatusWithGlobalBusyAsync(bool logRefresh)
    {
        if (IsConversionRunning || IsPreviewGenerating)
        {
            return;
        }

        ShowGlobalBusyOverlay(
            "Refreshing model inventory...",
            "Actualizando inventario de modelos...");
        try
        {
            await RefreshEngineStatusAsync(logRefresh);
        }
        finally
        {
            HideGlobalBusyOverlay();
        }
    }

    private void ShowGlobalBusyOverlay(string englishText, string spanishText)
    {
        _globalBusyEnglishText = string.IsNullOrWhiteSpace(englishText)
            ? "Loading..."
            : englishText;
        _globalBusySpanishText = string.IsNullOrWhiteSpace(spanishText)
            ? "Cargando..."
            : spanishText;
        OnPropertyChanged(nameof(GlobalBusyText));
        IsGlobalBusyOverlayVisible = true;
    }

    private void HideGlobalBusyOverlay()
    {
        IsGlobalBusyOverlayVisible = false;
    }

    private async Task ImportModelPackAsync()
    {
        if (!CanImportModelPack)
        {
            AddLog(
                "Model pack import is unavailable while another operation is running.",
                "La importacion de paquetes de modelos no esta disponible mientras hay otra operacion en ejecucion.");
            return;
        }

        if (IsSettingsModalOpen)
        {
            CaptureSettingsReturnContext();
            IsSettingsModalOpen = false;
        }

        _reopenModelInventoryAfterImport = IsModelInventoryModalOpen;
        IsModelPackImportRunning = true;
        ShowGlobalBusyOverlay(
            "Validating model pack...",
            "Validando paquete de modelos...");
        try
        {
            var result = await _modelPackImportCoordinator.ImportAsync(
                CreateModelPackAppImportRequest());
            ApplyModelPackImportResult(result);
        }
        catch (OperationCanceledException)
        {
            SetModelPackImportStatus(
                "Model pack import was canceled.",
                "La importacion del paquete de modelos fue cancelada.");
            AddLog(
                "Model pack import was canceled.",
                "La importacion del paquete de modelos fue cancelada.");
        }
        catch (Exception exception)
        {
            var errorLogPath = AppErrorLogService.LogRecoverableException(
                "Import model pack",
                exception);
            SetModelPackImportStatus(
                "Model pack import failed unexpectedly.",
                "La importacion del paquete de modelos fallo inesperadamente.");
            SetLastModelPackImportSummary(
                $"Model pack import failed unexpectedly. Details were written to {errorLogPath}. {exception.Message}",
                $"La importacion del paquete de modelos fallo inesperadamente. Los detalles se escribieron en {errorLogPath}. {exception.Message}");
            AddLog(
                $"Model pack import failed unexpectedly: {exception.Message}",
                $"La importacion del paquete de modelos fallo inesperadamente: {exception.Message}");
        }
        finally
        {
            IsModelPackImportRunning = false;
            HideGlobalBusyOverlay();
            if (_reopenModelInventoryAfterImport && !IsAnyModalOpen)
            {
                IsModelInventoryModalOpen = true;
            }
            else if (!_reopenModelInventoryAfterImport && !IsAnyModalOpen)
            {
                RestoreSettingsAfterChildModalIfNeeded();
            }

            _reopenModelInventoryAfterImport = false;
        }
    }

    private ModelPackAppImportRequest CreateModelPackAppImportRequest() => new(
        RuntimeRoot: AppContext.BaseDirectory,
        HelperExecutablePath: Path.Combine(AppContext.BaseDirectory, SetupHelperExecutableName),
        CurrentIw3Version: _dependencyHealth?.Iw3CliCapabilities.BundledIw3Version ?? string.Empty,
        CurrentV3dfyVersion: GetCurrentV3dfyVersion())
    {
        BeforeExecutionAsync = (preparation, _) =>
        {
            RecordValidModelPackPreparation(preparation);
            HideGlobalBusyOverlay();
            return Task.CompletedTask;
        },
        RefreshAfterSuccessfulImportAsync = async _ =>
        {
            ShowGlobalBusyOverlay(
                "Refreshing model inventory...",
                "Actualizando inventario de modelos...");
            await RefreshEngineStatusAsync(logRefresh: true);
        },
    };

    private async Task<bool> ConfirmModelPackImportAsync(
        ModelPackImportConfirmationPrompt prompt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        cancellationToken.ThrowIfCancellationRequested();

        _modelPackImportConfirmationCompletion?.TrySetResult(false);
        ResetModelPackImportConfirmationModal();
        IsModelInventoryModalOpen = false;
        _modelPackImportConfirmationPrompt = prompt;
        _modelPackImportConfirmationCompletion =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        RaiseModelPackImportConfirmationPropertiesChanged();
        IsModelPackImportConfirmationModalOpen = true;

        try
        {
            return await _modelPackImportConfirmationCompletion.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            var completion = _modelPackImportConfirmationCompletion;
            if (completion is not null)
            {
                ResetModelPackImportConfirmationModal();
                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);
                }
                else
                {
                    completion.TrySetResult(false);
                }
            }
        }
    }

    private void ApplyModelPackImportResult(ModelPackAppImportResult result)
    {
        if (result.Canceled)
        {
            if (result.ConfirmationCanceled)
            {
                RecordCanceledModelPackImport(result);
            }

            return;
        }

        if (result.Status == ModelPackAppImportStatus.Invalid)
        {
            RecordInvalidModelPackPreparation(result);
            return;
        }

        if (result.Status == ModelPackAppImportStatus.Failed)
        {
            RecordFailedModelPackImport(result);
            return;
        }

        if (result.Success)
        {
            RecordSuccessfulModelPackImport(result);
        }
    }

    private void RecordValidModelPackPreparation(ModelPackImportPreparationResult preparation)
    {
        SetModelPackImportStatus(
            "Model pack validated. Review the confirmation before Windows asks for administrator permission.",
            "Paquete de modelos validado. Revisa la confirmacion antes de que Windows pida permiso de administrador.");
        var prompt = ModelPackImportConfirmationFormatter.CreatePrompt(preparation);
        SetLastModelPackImportSummary(prompt.EnglishMessage, prompt.SpanishMessage);
        AddLog(
            $"Model pack validated: {GetModelPackDisplayName(preparation)}. Files to install: {preparation.FilesToInstall.Count}. Already installed: {preparation.AlreadyInstalledFiles.Count}.",
            $"Paquete de modelos validado: {GetModelPackDisplayName(preparation)}. Archivos por instalar: {preparation.FilesToInstall.Count}. Ya instalados: {preparation.AlreadyInstalledFiles.Count}.");
        AddLog(
            $"Model pack install target: {preparation.TargetPretrainedModelsRoot}",
            $"Destino de instalacion del paquete de modelos: {preparation.TargetPretrainedModelsRoot}");

        if (preparation.ElevationRequired)
        {
            AddLog(
                "Windows administrator permission is required because the model target is under Program Files.",
                "Se requiere permiso de administrador de Windows porque el destino de modelos esta en Program Files.");
        }
        else
        {
            AddLog(
                "The model target is outside Program Files; administrator permission may not be required for this target.",
                "El destino de modelos esta fuera de Program Files; puede que este destino no requiera permiso de administrador.");
        }

        LogModelPackWarnings(preparation.Warnings);
    }

    private void RecordCanceledModelPackImport(ModelPackAppImportResult result)
    {
        SetModelPackImportStatus(
            "Model pack import canceled before Windows administrator permission.",
            "Importacion del paquete de modelos cancelada antes del permiso de administrador de Windows.");
        SetLastModelPackImportSummary(
            "Model pack import canceled. No files were installed.",
            "Importacion del paquete de modelos cancelada. No se instalo ningun archivo.");
        AddLog(
            $"Model pack import canceled before launching the helper: {Path.GetFileName(result.SelectedModelPackZipPath)}",
            $"Importacion del paquete de modelos cancelada antes de iniciar el helper: {Path.GetFileName(result.SelectedModelPackZipPath)}");
    }

    private void RecordInvalidModelPackPreparation(ModelPackAppImportResult result)
    {
        SetModelPackImportStatus(
            "Model pack validation failed.",
            "La validacion del paquete de modelos fallo.");
        IReadOnlyList<string> errors = result.Errors.Count == 0
            ? ["Model pack import preparation did not produce a launchable helper request."]
            : result.Errors;
        SetLastModelPackImportSummary(
            CreateModelPackErrorSummary("Model pack validation failed.", errors),
            CreateModelPackErrorSummary("La validacion del paquete de modelos fallo.", errors));
        AddLog(
            "Model pack validation failed. Helper was not launched.",
            "La validacion del paquete de modelos fallo. No se inicio el helper.");
        foreach (var error in errors)
        {
            AddLog(
                $"Model pack validation error: {error}",
                $"Error de validacion del paquete de modelos: {error}");
        }

        LogModelPackWarnings(result.Warnings);
    }

    private void RecordFailedModelPackImport(ModelPackAppImportResult result)
    {
        var helperWasNotStarted = result.ExecutionResult?.HelperProcessStarted != true;
        SetModelPackImportStatus(
            helperWasNotStarted
                ? "Model pack import did not start. Windows administrator permission may have been canceled."
                : "Model pack import failed.",
            helperWasNotStarted
                ? "La importacion del paquete de modelos no inicio. Es posible que se haya cancelado el permiso de administrador de Windows."
                : "La importacion del paquete de modelos fallo.");
        IReadOnlyList<string> errors = result.Errors.Count == 0
            ? ["Model pack helper did not report a successful install."]
            : result.Errors;
        SetLastModelPackImportSummary(
            CreateModelPackErrorSummary(
                helperWasNotStarted
                    ? "Model pack import did not start. No files were installed."
                    : "Model pack import failed.",
                errors),
            CreateModelPackErrorSummary(
                helperWasNotStarted
                    ? "La importacion del paquete de modelos no inicio. No se instalo ningun archivo."
                    : "La importacion del paquete de modelos fallo.",
                errors));
        AddLog(
            helperWasNotStarted
                ? "Model pack import did not start. Windows administrator permission may have been canceled."
                : "Model pack import failed.",
            helperWasNotStarted
                ? "La importacion del paquete de modelos no inicio. Es posible que se haya cancelado el permiso de administrador de Windows."
                : "La importacion del paquete de modelos fallo.");
        foreach (var error in errors)
        {
            AddLog(
                $"Model pack import error: {error}",
                $"Error de importacion del paquete de modelos: {error}");
        }

        LogModelPackWarnings(result.ExecutionResult?.HelperResult?.Warnings ?? []);
    }

    private void RecordSuccessfulModelPackImport(ModelPackAppImportResult result)
    {
        SetModelPackImportStatus(
            "Model pack import completed.",
            "Importacion del paquete de modelos completada.");
        SetLastModelPackImportSummary(
            CreateModelPackSuccessSummary(result, useSpanish: false),
            CreateModelPackSuccessSummary(result, useSpanish: true));
        AddLog(
            CreateModelPackSuccessLog(result, useSpanish: false),
            CreateModelPackSuccessLog(result, useSpanish: true));
        if (result.AppRefreshCompleted)
        {
            AddLog(
                "Model inventory refreshed after model pack import.",
                "Inventario de modelos actualizado despues de importar el paquete.");
        }

        LogModelPackWarnings(result.ExecutionResult?.HelperResult?.Warnings ?? []);
    }

    private void SetModelPackImportStatus(string englishText, string spanishText)
    {
        _modelPackImportStatusEnglishText = englishText;
        _modelPackImportStatusSpanishText = spanishText;
        OnPropertyChanged(nameof(ModelPackImportStatusText));
    }

    private void SetLastModelPackImportSummary(string englishText, string spanishText)
    {
        _lastModelPackImportSummaryEnglishText = englishText;
        _lastModelPackImportSummarySpanishText = spanishText;
        OnPropertyChanged(nameof(LastModelPackImportSummary));
        OnPropertyChanged(nameof(LastModelPackImportSummaryVisibility));
    }

    private static string GetCurrentV3dfyVersion() =>
        typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString() ?? "unknown";

    private static string GetModelPackDisplayName(ModelPackImportPreparationResult preparation) =>
        preparation.Manifest?.DisplayName ??
        Path.GetFileName(preparation.ModelPackZipPath);

    private static string CreateModelPackSuccessSummary(
        ModelPackAppImportResult result,
        bool useSpanish)
    {
        var helperResult = result.ExecutionResult?.HelperResult;
        var manifestName = helperResult?.Manifest?.DisplayName ??
            result.LaunchPreparation?.Preparation.Manifest?.DisplayName ??
            Path.GetFileName(result.SelectedModelPackZipPath);
        IReadOnlyList<string> lines = useSpanish
            ?
            [
                $"Paquete importado: {manifestName}",
                $"Archivos instalados: {helperResult?.InstalledFiles.Count ?? 0}",
                $"Archivos ya presentes: {helperResult?.AlreadyInstalledFiles.Count ?? 0}",
                $"Archivos omitidos: {helperResult?.SkippedFiles.Count ?? 0}",
                $"Inventario actualizado: {(result.AppRefreshCompleted ? "si" : "no")}",
                "Modelos seleccionables: solo se muestran para conversion los modelos soportados/mapeados.",
            ]
            :
            [
                $"Imported pack: {manifestName}",
                $"Installed files: {helperResult?.InstalledFiles.Count ?? 0}",
                $"Already present files: {helperResult?.AlreadyInstalledFiles.Count ?? 0}",
                $"Skipped files: {helperResult?.SkippedFiles.Count ?? 0}",
                $"Inventory refreshed: {(result.AppRefreshCompleted ? "yes" : "no")}",
                "Selectable models: only supported/mapped models are shown for conversion.",
            ];
        return string.Join(Environment.NewLine, lines);
    }

    private static string CreateModelPackSuccessLog(
        ModelPackAppImportResult result,
        bool useSpanish)
    {
        var helperResult = result.ExecutionResult?.HelperResult;
        var manifestName = helperResult?.Manifest?.DisplayName ??
            result.LaunchPreparation?.Preparation.Manifest?.DisplayName ??
            Path.GetFileName(result.SelectedModelPackZipPath);
        return useSpanish
            ? $"Paquete de modelos importado: {manifestName}. Archivos instalados: {helperResult?.InstalledFiles.Count ?? 0}."
            : $"Model pack imported: {manifestName}. Installed files: {helperResult?.InstalledFiles.Count ?? 0}.";
    }

    private static string CreateModelPackErrorSummary(
        string heading,
        IReadOnlyList<string> errors)
    {
        var lines = new List<string> { heading };
        lines.AddRange(errors.Select(error => $"- {error}"));
        return string.Join(Environment.NewLine, lines);
    }

    private void LogModelPackWarnings(IReadOnlyList<string> warnings)
    {
        foreach (var warning in warnings)
        {
            AddLog(
                $"Model pack warning: {warning}",
                $"Advertencia de paquete de modelos: {warning}");
        }
    }

    private LocalModelInventory GetCurrentModelInventory() =>
        _dependencyHealth?.ModelInventory ??
        LocalModelInventory.Empty(_toolPaths.ModelsDirectory);

    private string CreateModelLicenseNoticeSummaryText()
    {
        var candidates = GetCurrentModelInventory().SelectionCandidates;
        if (candidates.Count == 0)
        {
            return Text(
                "No installed or imported models were detected.",
                "No se detectaron modelos instalados o importados.");
        }

        var lines = new List<string>();
        foreach (var candidate in candidates)
        {
            var entry = FindRegistryEntry(candidate);
            lines.Add($"- {GetCandidateDisplayName(candidate)}");
            lines.Add(Text(
                $"  File/pack identity: {candidate.RelativePath}",
                $"  Identidad de archivo/paquete: {candidate.RelativePath}"));
            if (entry is not null)
            {
                lines.Add(Text(
                    $"  Catalog notice status: {ModelNoticeStatusText(entry.RedistributionDecision, useSpanish: false)}",
                    $"  Estado de avisos del catalogo: {ModelNoticeStatusText(entry.RedistributionDecision, useSpanish: true)}"));
            }

            var noticeFiles = FindMatchingModelNoticeFiles(candidate, entry);
            lines.Add(noticeFiles.Count > 0
                ? Text(
                    $"  License/notice files: {string.Join(", ", noticeFiles)}",
                    $"  Archivos de licencia/avisos: {string.Join(", ", noticeFiles)}")
                : Text(
                    "  License metadata not available",
                    "  Metadatos de licencia no disponibles"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private IReadOnlyList<string> FindMatchingModelNoticeFiles(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry)
    {
        if (!Directory.Exists(_toolPaths.ModelsDirectory))
        {
            return [];
        }

        var terms = CreateModelNoticeSearchTerms(candidate, entry);
        if (terms.Count == 0)
        {
            return [];
        }

        try
        {
            return Directory
                .EnumerateFiles(_toolPaths.ModelsDirectory, "*", SearchOption.AllDirectories)
                .Where(IsNoticeFileName)
                .Select(path => Path.GetRelativePath(_toolPaths.ModelsDirectory, path))
                .Where(relativePath => terms.Any(term =>
                    NormalizeNoticeSearchText(relativePath).Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToArray();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> CreateModelNoticeSearchTerms(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry)
    {
        var values = new[]
        {
            Path.GetFileNameWithoutExtension(candidate.FileName),
            candidate.MappingKey,
            candidate.Iw3DepthModelName,
            candidate.Id,
            entry?.Key,
            entry?.SharedCheckpointGroupId,
        };

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeNoticeSearchText(value!))
            .Where(value => value.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsNoticeFileName(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.Contains("license", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("notice", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("SOURCE.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("README.md", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("MODEL_CARD.md", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeNoticeSearchText(string value) =>
        value
            .Replace('\\', '/')
            .Replace('_', '-')
            .Replace(' ', '-')
            .ToLowerInvariant();

    private static string ModelNoticeStatusText(
        Iw3DepthModelRedistributionDecision decision,
        bool useSpanish) => decision switch
    {
        Iw3DepthModelRedistributionDecision.SafeForPublicRelease => useSpanish
            ? "apto para release publico"
            : "safe for public release",
        Iw3DepthModelRedistributionDecision.SafeWithNotice => useSpanish
            ? "apto con avisos"
            : "safe with notices",
        Iw3DepthModelRedistributionDecision.UserDownloadOnly => useSpanish
            ? "solo descarga/importacion del usuario"
            : "user download/import only",
        Iw3DepthModelRedistributionDecision.ExcludeNonCommercial => useSpanish
            ? "excluido por restriccion no comercial"
            : "excluded due to non-commercial restriction",
        Iw3DepthModelRedistributionDecision.BlockedUnclearLicense => useSpanish
            ? "bloqueado por licencia no clara"
            : "blocked by unclear license",
        Iw3DepthModelRedistributionDecision.NotAModelPackTarget => useSpanish
            ? "no es objetivo de paquete de modelos"
            : "not a model-pack target",
        _ => useSpanish ? "desconocido" : "unknown",
    };

    private IReadOnlyList<SelectableModelInventoryRow> CreateSelectableModelInventoryRows()
    {
        var candidates = Iw3DepthModelMapper.CreateSelectableCandidates(
            GetCurrentModelInventory().SelectionCandidates,
            IsSpanish);
        if (candidates.Count == 0)
        {
            return [];
        }

        var rows = new List<SelectableModelInventoryRow>();
        foreach (var candidate in candidates)
        {
            var entry = FindRegistryEntry(candidate);
            rows.Add(new SelectableModelInventoryRow(
                Model: GetCandidateDisplayName(candidate),
                Iw3DepthModel: candidate.Iw3DepthModelName ?? "-",
                Checkpoint: candidate.RelativePath,
                Type: GetDepthTypeText(entry, IsSpanish),
                Source: GetModelSourceText(candidate, entry, IsSpanish)));
        }

        return rows;
    }

    private string CreateSelectableModelsInventoryText()
    {
        var candidates = Iw3DepthModelMapper.CreateSelectableCandidates(
            GetCurrentModelInventory().SelectionCandidates,
            IsSpanish);
        if (candidates.Count == 0)
        {
            return Text(
                "No selectable models are available. A verified v3dfy mapping is required before a detected file can be used for conversion.",
                "No hay modelos seleccionables disponibles. Se requiere un mapeo verificado de v3dfy antes de usar un archivo detectado para convertir.");
        }

        var lines = new List<string>();
        foreach (var candidate in candidates)
        {
            lines.Add($"- {GetCandidateDisplayName(candidate)}");
            lines.Add($"  --depth-model {candidate.Iw3DepthModelName ?? "-"}");
            lines.Add($"  {candidate.RelativePath}");
            var note = Text(candidate.EnglishStatusNote, candidate.SpanishStatusNote);
            if (!string.IsNullOrWhiteSpace(note))
            {
                lines.Add($"  {note}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string CreateDiagnosticModelsInventoryText()
    {
        var unmappedCandidates = Iw3DepthModelMapper.GetUnmappedCandidates(
            GetCurrentModelInventory().SelectionCandidates);
        if (unmappedCandidates.Count == 0)
        {
            return Text(
                "No diagnostic-only model files were detected.",
                "No se detectaron modelos solo de diagnostico.");
        }

        var lines = new List<string>();
        foreach (var candidate in unmappedCandidates)
        {
            lines.Add($"- {GetCandidateDisplayName(candidate)}");
            lines.Add($"  {candidate.RelativePath}");
            lines.Add(Text(
                "  Reason: no verified v3dfy mapping / diagnostic only.",
                "  Motivo: sin mapeo verificado de v3dfy / solo diagnostico."));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private IReadOnlyList<ModelHelpRow> CreateModelHelpRows()
    {
        if (LocalModelCandidates.Count == 0)
        {
            return [];
        }

        var rows = new List<ModelHelpRow>();
        foreach (var candidate in LocalModelCandidates)
        {
            var entry = FindRegistryEntry(candidate);
            rows.Add(new ModelHelpRow(
                Model: GetCandidateDisplayName(candidate),
                Purpose: GetModelPurpose(candidate, entry, IsSpanish),
                Use: GetModelUseExample(candidate, entry, IsSpanish),
                Scene: GetSceneScopeText(entry, IsSpanish),
                Depth: GetDepthTypeText(entry, IsSpanish),
                SizePerformance: GetModelSizePerformanceText(candidate, entry)));
        }

        return rows;
    }

    private string GetModelSizePerformanceText(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry)
    {
        var guidance = _modelGuidanceService.Create(
            candidate.MappingKey,
            candidate.Iw3DepthModelName,
            candidate.DisplayName,
            entry?.IsEmbeddedBase == true);
        var sizeClass = GetSizeClassText(entry, IsSpanish);
        return IsSpanish
            ? $"{sizeClass}; velocidad {guidance.SpanishSpeed}; calidad {guidance.SpanishQuality}; tamano {guidance.SpanishSize}"
            : $"{sizeClass}; speed {guidance.EnglishSpeed}; quality {guidance.EnglishQuality}; size {guidance.EnglishSize}";
    }

    private static Iw3DepthModelRegistryEntry? FindRegistryEntry(
        LocalModelSelectionCandidate candidate) =>
        Iw3DepthModelMapper.RegistryEntries.FirstOrDefault(entry =>
            string.Equals(entry.Key, candidate.MappingKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.DepthModelName, candidate.Iw3DepthModelName, StringComparison.OrdinalIgnoreCase));

    private static string GetModelPurpose(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry,
        bool useSpanish)
    {
        var key = entry?.Key ?? candidate.MappingKey ?? string.Empty;
        return key switch
        {
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey => useSpanish
                ? "Modelo metrico para interiores."
                : "Metric indoor model.",
            Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorKey => useSpanish
                ? "Modelo metrico para exteriores."
                : "Metric outdoor model.",
            Iw3DepthModelMapper.DepthAnythingV2SmallKey => useSpanish
                ? "Modelo general ligero."
                : "General-purpose lightweight model.",
            Iw3DepthModelMapper.DepthAnythingSmallKey => useSpanish
                ? "Modelo general relativo pequeno."
                : "Small general relative-depth model.",
            Iw3DepthModelMapper.DepthAnythingBaseKey => useSpanish
                ? "Modelo general relativo balanceado."
                : "Balanced general relative-depth model.",
            Iw3DepthModelMapper.DepthAnythingLargeKey => useSpanish
                ? "Modelo general relativo grande."
                : "Large general relative-depth model.",
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimSmallKey => useSpanish
                ? "Modelo metrico ligero entrenado para escenas tipo interior."
                : "Lightweight metric model tuned for indoor-like scenes.",
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimBaseKey => useSpanish
                ? "Modelo metrico base entrenado para escenas tipo interior."
                : "Base metric model tuned for indoor-like scenes.",
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiSmallKey => useSpanish
                ? "Modelo metrico ligero entrenado para escenas tipo exterior."
                : "Lightweight metric model tuned for outdoor-like scenes.",
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiBaseKey => useSpanish
                ? "Modelo metrico base entrenado para escenas tipo exterior."
                : "Base metric model tuned for outdoor-like scenes.",
            Iw3DepthModelMapper.DistillAnyDepthSmallKey => useSpanish
                ? "Modelo destilado general para profundidad."
                : "General distilled depth model.",
            Iw3DepthModelMapper.DepthAnything3MonoLargeKey => useSpanish
                ? "Modelo monocular grande de Depth Anything 3."
                : "Large Depth Anything 3 monocular model.",
            Iw3DepthModelMapper.DepthAnything3MonoLarge3dTvKey => useSpanish
                ? "Variante de Depth Anything 3 ajustada para TV 3D."
                : "Depth Anything 3 variant tuned for 3D TV output.",
            _ when entry?.RedistributionDecision ==
                Iw3DepthModelRedistributionDecision.BlockedUnclearLicense => useSpanish
                    ? "Checkpoint proporcionado por el usuario."
                    : "User-provided checkpoint.",
            _ => useSpanish
                ? "Modelo de profundidad instalado y mapeado."
                : "Installed mapped depth model.",
        };
    }

    private static string GetModelUseExample(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry,
        bool useSpanish)
    {
        var key = entry?.Key ?? candidate.MappingKey ?? string.Empty;
        return key switch
        {
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey => useSpanish
                ? "Habitaciones, personas en interiores e interiores de peliculas. Ejemplos: dramas, animacion interior, escenas de dialogo, recamaras, oficinas."
                : "Rooms, people indoors, movie interiors. Examples: dramas, animation interiors, dialogue scenes, bedrooms, offices.",
            Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorKey => useSpanish
                ? "Carreteras, paisajes y exteriores. Ejemplos: calles, montanas, playas, mar, barcos, planos exteriores amplios."
                : "Road, landscape, and outdoor scenes. Examples: streets, mountains, beaches, sea, boats, wide exterior shots.",
            Iw3DepthModelMapper.DepthAnythingV2SmallKey => useSpanish
                ? "Buen primer modelo opcional para conversiones rapidas. Ejemplos: peliculas generales, anime, escenas mixtas, pruebas rapidas."
                : "Good first optional model for faster conversions. Examples: general movies, anime, mixed scenes, quick tests.",
            Iw3DepthModelMapper.DepthAnythingSmallKey => useSpanish
                ? "Conversiones ligeras cuando importa el tamano. Ejemplos: pruebas rapidas, poco almacenamiento, equipos antiguos."
                : "Lightweight conversions when size matters. Examples: quick tests, low storage, older machines.",
            Iw3DepthModelMapper.DepthAnythingBaseKey => useSpanish
                ? "Calidad y tamano balanceados para Depth Anything v1. Ejemplos: peliculas generales, animacion, escenas mixtas interior/exterior."
                : "Balanced quality and size for Depth Anything v1. Examples: general movies, animation, mixed indoor/outdoor scenes.",
            Iw3DepthModelMapper.DepthAnythingLargeKey => useSpanish
                ? "Conversiones enfocadas en calidad cuando la velocidad importa menos. Ejemplos: peliculas detalladas, close-ups, escenas complejas."
                : "Quality-focused conversions when speed matters less. Examples: detailed movies, close-ups, complex scenes.",
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimSmallKey => useSpanish
                ? "Escenas metricas interiores o parecidas a interiores. Ejemplos: habitaciones, pasillos, oficinas, interiores CG."
                : "Indoor or indoor-like metric scenes. Examples: rooms, corridors, offices, CG interiors.",
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimBaseKey => useSpanish
                ? "Modelo base metrico para interiores. Ejemplos: interiores detallados, habitaciones, pasillos, interiores CG/animados."
                : "Indoor metric base model. Examples: detailed interiors, rooms, hallways, CG/animated interiors.",
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiSmallKey => useSpanish
                ? "Escenas metricas exteriores. Ejemplos: carreteras, autos, calles de ciudad, paisajes, mar, barcos."
                : "Outdoor metric scenes. Examples: roads, cars, city streets, landscapes, sea, boats.",
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiBaseKey => useSpanish
                ? "Modelo base metrico para exteriores. Ejemplos: escenas de carretera, paisajes, accion exterior, mar/barcos."
                : "Outdoor metric base model. Examples: road scenes, landscapes, outdoor action, sea/boats.",
            Iw3DepthModelMapper.DistillAnyDepthSmallKey => useSpanish
                ? "Modelo de profundidad destilado pequeno. Ejemplos: comparaciones rapidas, pruebas experimentales, escenas generales."
                : "Small distilled depth model. Examples: quick comparisons, experimental tests, general scenes.",
            Iw3DepthModelMapper.DepthAnything3MonoLargeKey => useSpanish
                ? "Escenas generales al probar Depth Anything 3. Ejemplos: peliculas, animacion, escenas mixtas detalladas."
                : "General scenes when trying Depth Anything 3. Examples: movies, animation, detailed mixed scenes.",
            Iw3DepthModelMapper.DepthAnything3MonoLarge3dTvKey => useSpanish
                ? "Variante de Depth Anything 3 orientada a TV 3D/anaglifo. Ejemplos: pruebas en TV 3D, pruebas anaglifo, experimentos de TV 3D."
                : "3D TV/anaglyph-oriented Depth Anything 3 variant. Examples: TV playback tests, anaglyph tests, 3D TV experiments.",
            _ => GetModelBestUse(candidate, entry, useSpanish),
        };
    }

    private static string GetModelBestUse(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry,
        bool useSpanish)
    {
        var key = entry?.Key ?? candidate.MappingKey ?? string.Empty;
        return key switch
        {
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey => useSpanish
                ? "Habitaciones, personas en interiores e interiores de peliculas."
                : "Rooms, people indoors, and movie interiors.",
            Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorKey => useSpanish
                ? "Carreteras, paisajes y escenas exteriores."
                : "Road, landscape, and outdoor scenes.",
            Iw3DepthModelMapper.DepthAnythingV2SmallKey => useSpanish
                ? "Primera opcion opcional para conversiones mas rapidas."
                : "Good first optional model for faster conversions.",
            Iw3DepthModelMapper.DepthAnythingSmallKey => useSpanish
                ? "Conversiones ligeras cuando el tamano importa."
                : "Lightweight conversions when size matters.",
            Iw3DepthModelMapper.DepthAnythingBaseKey => useSpanish
                ? "Equilibrio entre calidad y tamano en Depth Anything v1."
                : "Balanced quality and size for Depth Anything v1.",
            Iw3DepthModelMapper.DepthAnythingLargeKey => useSpanish
                ? "Conversiones enfocadas en calidad cuando la velocidad importa menos."
                : "Quality-focused conversions when speed matters less.",
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimSmallKey => useSpanish
                ? "Escenas interiores o similares a interiores con salida metrica."
                : "Indoor or indoor-like scenes when metric output is useful.",
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimBaseKey => useSpanish
                ? "Interiores cuando se prefiere el modelo base metrico."
                : "Indoor scenes when the metric base model is preferred.",
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiSmallKey => useSpanish
                ? "Exteriores, carreteras y paisajes con salida metrica."
                : "Outdoor, road, and landscape scenes when metric output is useful.",
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiBaseKey => useSpanish
                ? "Exteriores cuando se prefiere el modelo base metrico."
                : "Outdoor scenes when the metric base model is preferred.",
            Iw3DepthModelMapper.DistillAnyDepthSmallKey => useSpanish
                ? "Opcion pequena cuando se quiera probar un modelo destilado."
                : "Small option when trying a distilled model.",
            Iw3DepthModelMapper.DepthAnything3MonoLargeKey => useSpanish
                ? "Escenas generales cuando se quiere probar Depth Anything 3."
                : "General scenes when trying Depth Anything 3.",
            Iw3DepthModelMapper.DepthAnything3MonoLarge3dTvKey => useSpanish
                ? "Salida anaglifo o TV 3D con el escalador alternativo de iw3."
                : "Anaglyph or 3D TV output with iw3's alternate scaler.",
            _ when entry?.RedistributionDecision ==
                Iw3DepthModelRedistributionDecision.BlockedUnclearLicense => useSpanish
                    ? "Solo si tu proporcionaste este checkpoint; no es un paquete oficial de v3dfy."
                    : "Only when you supplied this checkpoint yourself; it is not an official v3dfy pack.",
            _ => useSpanish
                ? "Conversiones locales cuando este modelo este instalado."
                : "Local conversions when this model is installed.",
        };
    }

    private static string GetSceneScopeText(
        Iw3DepthModelRegistryEntry? entry,
        bool useSpanish)
    {
        var category = entry?.Category ?? string.Empty;
        if (category.Contains("indoor/outdoor", StringComparison.OrdinalIgnoreCase))
        {
            return useSpanish ? "interior/exterior" : "indoor/outdoor";
        }

        if (category.Contains("indoor", StringComparison.OrdinalIgnoreCase) ||
            category.Contains("Hypersim", StringComparison.OrdinalIgnoreCase))
        {
            return useSpanish ? "interior" : "indoor";
        }

        if (category.Contains("outdoor", StringComparison.OrdinalIgnoreCase) ||
            category.Contains("VKITTI", StringComparison.OrdinalIgnoreCase))
        {
            return useSpanish ? "exterior" : "outdoor";
        }

        return useSpanish ? "general" : "general";
    }

    private static string GetDepthTypeText(
        Iw3DepthModelRegistryEntry? entry,
        bool useSpanish) =>
        entry?.DepthType switch
        {
            Iw3DepthModelDepthType.Metric => useSpanish ? "metrica" : "metric",
            Iw3DepthModelDepthType.Relative => useSpanish ? "relativa" : "relative",
            Iw3DepthModelDepthType.ForcedDisparity => useSpanish
                ? "disparidad forzada"
                : "forced disparity",
            _ => useSpanish ? "segun el modelo" : "model-defined",
        };

    private static string GetModelSourceText(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry,
        bool useSpanish)
    {
        if (entry?.RedistributionDecision ==
            Iw3DepthModelRedistributionDecision.BlockedUnclearLicense)
        {
            return useSpanish ? "proporcionado por el usuario" : "user-provided";
        }

        if (entry?.Availability == Iw3DepthModelAvailability.EmbeddedBase)
        {
            return useSpanish ? "incluido" : "bundled";
        }

        if (entry?.IsPublicPackEligible == true)
        {
            return useSpanish ? "paquete de modelos" : "model pack";
        }

        return candidate.IsCatalogManaged
            ? useSpanish ? "catalogo local" : "local catalog"
            : useSpanish ? "archivo local" : "local file";
    }

    private static string GetSizeClassText(
        Iw3DepthModelRegistryEntry? entry,
        bool useSpanish)
    {
        var name = entry?.DepthModelName ?? string.Empty;
        var key = entry?.Key ?? string.Empty;
        if (name.EndsWith("_S", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("small", StringComparison.OrdinalIgnoreCase))
        {
            return useSpanish ? "pequeno / mas rapido" : "small / faster";
        }

        if (name.EndsWith("_B", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("base", StringComparison.OrdinalIgnoreCase))
        {
            return useSpanish ? "base / equilibrado" : "base / balanced";
        }

        if (name.EndsWith("_L", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("large", StringComparison.OrdinalIgnoreCase))
        {
            return useSpanish ? "grande / mas pesado" : "large / heavier";
        }

        return useSpanish ? "estandar" : "standard";
    }

    private string CreateRuntimeDependenciesInventoryText()
    {
        var dependency = _dependencyHealth?.Iw3RuntimeDependencies ?? new ToolDependencyHealth(
            ToolHealthStatus.Missing,
            ToolHealthDetailKind.Iw3RuntimeDependenciesMissing,
            _toolPaths.Iw3DefaultStereoRuntimeDependencyFile);
        var statusText = dependency.Status == ToolHealthStatus.Found
            ? Text("Found", "Encontrada")
            : Text("Missing", "Faltante");
        return string.Join(
            Environment.NewLine,
            [
                $"- {Path.GetFileName(dependency.ExpectedPath)}",
                $"  {statusText}",
                $"  {dependency.ExpectedPath}",
                Text(
                    "  Note: runtime dependency, not a selectable model.",
                    "  Nota: dependencia de runtime, no es un modelo seleccionable."),
            ]);
    }

    private string GetCandidateDisplayName(LocalModelSelectionCandidate candidate) =>
        IsSpanish && !string.IsNullOrWhiteSpace(candidate.SpanishDisplayName)
            ? candidate.SpanishDisplayName
            : candidate.DisplayName;

    private void OpenEngineFolder()
    {
        try
        {
            Directory.CreateDirectory(_toolPaths.Iw3EngineDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(_toolPaths.PythonExecutable)!);
            Directory.CreateDirectory(_toolPaths.ModelsDirectory);

            Process.Start(new ProcessStartInfo
            {
                FileName = _toolPaths.Iw3EngineDirectory,
                UseShellExecute = true,
            });

            AddLog(
                $"Opened expected engine folder: {_toolPaths.Iw3EngineDirectory}",
                $"Carpeta esperada del motor abierta: {_toolPaths.Iw3EngineDirectory}");
        }
        catch (Exception exception)
        {
            AddLog(
                $"Could not open expected engine folder: {exception.Message}",
                $"No se pudo abrir la carpeta esperada del motor: {exception.Message}");
        }
    }

    private void OpenModelsFolder()
    {
        try
        {
            Directory.CreateDirectory(_toolPaths.ModelsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _toolPaths.ModelsDirectory,
                UseShellExecute = true,
            });

            AddLog(
                $"Opened expected models folder: {_toolPaths.ModelsDirectory}",
                $"Carpeta esperada de modelos abierta: {_toolPaths.ModelsDirectory}");
        }
        catch (Exception exception)
        {
            AddLog(
                $"Could not open expected models folder: {exception.Message}",
                $"No se pudo abrir la carpeta esperada de modelos: {exception.Message}");
        }
    }

    private void SelectAppSection(AppSection section)
    {
        SelectedAppSection = section;
    }

    public void ExpandSidebarForHover()
    {
        if (!IsSidebarPinnedExpanded)
        {
            IsSidebarHoverExpanded = true;
        }
    }

    public void CollapseSidebarAfterHover()
    {
        if (!IsSidebarPinnedExpanded)
        {
            IsSidebarHoverExpanded = false;
        }
    }

    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
        IsSidebarHoverExpanded = false;
    }

    private void SelectImageConversionMode(ImageConversionMode mode)
    {
        if (SelectedImageConversionMode == mode)
        {
            IsImageWorkflowChooserExpanded = false;
            return;
        }

        SelectedImageConversionMode = mode;
        IsImageWorkflowChooserExpanded = false;
        ApplyImageSetupChanged();
        AddImageLog(
            $"Image workflow mode changed: {SelectedImageModeName}.",
            $"Modo de flujo de imagen cambiado: {SelectedImageModeName}.");
        if (SelectedImageConversionStep == ImageConversionStep.ModeAndSource)
        {
            return;
        }

        RaiseImageConversionPropertiesChanged();
    }

    private void ToggleImageWorkflowChooser()
    {
        if (!HasSelectedImageMode)
        {
            IsImageWorkflowChooserExpanded = true;
            return;
        }

        IsImageWorkflowChooserExpanded = !IsImageWorkflowChooserExpanded;
    }

    private void SelectImageConversionStep(ImageConversionStep step)
    {
        if (step == ImageConversionStep.Setup && !CanOpenImageSetupStep)
        {
            AddImageLog(
                "Analyze a readable image before opening image setup.",
                "Analiza una imagen legible antes de abrir la configuracion.");
            return;
        }

        if (step == ImageConversionStep.PreviewAndExport && !CanOpenImagePreviewExportStep)
        {
            AddImageLog(
                "Choose an image workflow and complete setup before opening the preview/export plan.",
                "Elige un flujo de imagen y completa la configuracion antes de abrir el plan de preview/exportacion.");
            return;
        }

        SelectedImageConversionStep = step;
    }

    private void MoveImageWizardBack()
    {
        if (!CanMoveImageWizardBack)
        {
            return;
        }

        SelectedImageConversionStep = SelectedImageConversionStep switch
        {
            ImageConversionStep.PreviewAndExport => ImageConversionStep.Setup,
            ImageConversionStep.Setup => ImageConversionStep.ModeAndSource,
            _ => ImageConversionStep.ModeAndSource,
        };
    }

    private void MoveImageWizardNext()
    {
        if (!CanMoveImageWizardNext)
        {
            AddImageLog(
                "Image workflow cannot continue until the current phase is ready.",
                "El flujo de imagen no puede continuar hasta que la fase actual este lista.");
            return;
        }

        SelectedImageConversionStep = SelectedImageConversionStep switch
        {
            ImageConversionStep.ModeAndSource => ImageConversionStep.Setup,
            ImageConversionStep.Setup => ImageConversionStep.PreviewAndExport,
            _ => ImageConversionStep.PreviewAndExport,
        };

        if (SelectedImageConversionStep == ImageConversionStep.PreviewAndExport)
        {
            AddImageLog(
                "Image preview/export plan is ready for review.",
                "El plan de preview/exportacion de imagen esta listo para revisar.");
        }
    }

    private void ContinueWithImageConversion()
    {
        if (!CanContinueWithImageConversion)
        {
            AddImageLog(
                "Review image setup before preparing the preview/export plan.",
                "Revisa la configuracion de imagen antes de preparar el plan de preview/exportacion.");
            return;
        }

        _hasEnteredImagePreviewExportStage = true;
        AddImageLog(
            "Image preview/export plan prepared. Engine processing is not implemented yet.",
            "Plan de preview/exportacion de imagen preparado. El procesamiento de motor aun no esta implementado.");
        ShowLogCopyNotification(
            "Image preview/export plan prepared",
            "Plan de preview/exportacion de imagen preparado");
        RaiseImageConversionPropertiesChanged();
    }

    private void ApplyImageSetupChanged()
    {
        if (_hasEnteredImagePreviewExportStage)
        {
            _hasEnteredImagePreviewExportStage = false;
            ShowLogCopyNotification(
                "Image setup changed. Preview/export plan reset.",
                "La configuracion de imagen cambio. Plan de preview/exportacion restablecido.");
            AddImageLog(
                "Image setup changed; preview/export plan reset.",
                "La configuracion de imagen cambio; plan de preview/exportacion restablecido.");
        }
        else
        {
            AddImageLog(
                "Image setup changed.",
                "Configuracion de imagen cambiada.");
        }

        RaiseImageConversionPropertiesChanged();
    }

    private void ResetImageSetupState()
    {
        _selectedImageConversionMode = null;
        _isImageWorkflowChooserExpanded = false;
        _selectedParallaxDepthIntensity = "Medium";
        _selectedParallaxMotionDirection = "Left to right";
        _selectedParallaxZoomAmplitude = "Subtle";
        _selectedParallaxDuration = "6 seconds";
        _selectedParallaxSmoothing = "Enabled";
        _selectedParallaxLayerBehavior = "Foreground / mid / background";
        _selectedStereoOutputFormat = ImageStereoOutputFormat.SideBySide;
        _selectedStereoEyeSeparation = "4.0%";
        _selectedStereoConvergence = "Neutral";
        _imageStereoSwapEyes = false;
        _selectedStereoAnaglyphMode = "Red/Cyan";
    }

    private void SetImageSetupString(
        ref string field,
        string value,
        [CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            ApplyImageSetupChanged();
        }
    }

    private string ImageStepState(ImageConversionStep step)
    {
        if (SelectedImageConversionStep == step)
        {
            if (step == ImageConversionStep.Setup && !CanOpenImageSetupStep)
            {
                return "Locked";
            }

            if (step == ImageConversionStep.PreviewAndExport && !CanOpenImagePreviewExportStep)
            {
                return "Locked";
            }

            return "Active";
        }

        return step switch
        {
            ImageConversionStep.ModeAndSource when
                SelectedImageConversionStep > ImageConversionStep.ModeAndSource && HasImageMetadata => "Completed",
            ImageConversionStep.ModeAndSource => "Pending",
            ImageConversionStep.Setup when !CanOpenImageSetupStep => "Locked",
            ImageConversionStep.Setup when
                SelectedImageConversionStep > ImageConversionStep.Setup && IsImageSetupValid => "Completed",
            ImageConversionStep.Setup => "Pending",
            ImageConversionStep.PreviewAndExport when !CanOpenImagePreviewExportStep => "Locked",
            _ => "Pending",
        };
    }

    private string ImageStepMarkerText(ImageConversionStep step, string pendingText) =>
        ImageStepState(step) == "Completed" ? "\u2713" : pendingText;

    private void OpenSettings()
    {
        if (IsConversionRunning ||
            IsPreviewGenerating ||
            IsReplaceVideoConfirmationModalOpen ||
            IsPreviewInvalidationConfirmationModalOpen ||
            IsModelPackImportConfirmationModalOpen ||
            IsPreviewReadyModalOpen ||
            IsConversionCompletedModalOpen)
        {
            return;
        }

        IsTechnicalDetailsModalOpen = false;
        IsProfileDetailsModalOpen = false;
        IsModelHelpModalOpen = false;
        IsModelInventoryModalOpen = false;
        IsActivityLogModalOpen = false;
        RaiseSettingsSectionPropertiesChanged();
        IsSettingsModalOpen = true;
    }

    private void OpenToolsEngineSettings()
    {
        SelectedSettingsSection = SettingsSection.ToolsEngine;
        OpenSettings();
    }

    private void CloseSettings()
    {
        IsSettingsModalOpen = false;
    }

    private void CaptureSettingsReturnContext()
    {
        if (IsSettingsModalOpen)
        {
            _settingsSectionToRestoreAfterChildModal = SelectedSettingsSection;
        }
    }

    private void RestoreSettingsAfterChildModalIfNeeded()
    {
        if (_settingsSectionToRestoreAfterChildModal is not { } section)
        {
            return;
        }

        _settingsSectionToRestoreAfterChildModal = null;
        SelectedSettingsSection = section;
        RaiseSettingsSectionPropertiesChanged();
        IsSettingsModalOpen = true;
    }

    private void MoveWizardBack()
    {
        if (_workflowState.MoveBack())
        {
            RaiseWizardPropertiesChanged();
        }
    }

    private void MoveWizardNext()
    {
        if (_workflowState.MoveNext())
        {
            RaiseWizardPropertiesChanged();
        }
    }

    private void ContinueWithConversion()
    {
        if (!CanEnterPreviewConversionStage)
        {
            AddLog(
                "Review the conversion plan before generating a preview.",
                "Revisa el plan de conversion antes de generar una vista previa.");
            return;
        }

        SetHasEnteredPreviewConversionStage(true);
        ClearPreviewStageResetNotice();
        AddLog(
            "Preview and conversion controls are ready.",
            "Los controles de preview y conversion estan listos.");
    }

    private void ShowModelInventory()
    {
        if (IsConversionRunning ||
            IsPreviewGenerating ||
            IsTechnicalDetailsModalOpen ||
            IsProfileDetailsModalOpen ||
            IsModelHelpModalOpen ||
            IsReplaceVideoConfirmationModalOpen ||
            IsPreviewInvalidationConfirmationModalOpen ||
            IsModelPackImportConfirmationModalOpen ||
            IsActivityLogModalOpen ||
            IsPreviewReadyModalOpen ||
            IsConversionCompletedModalOpen)
        {
            return;
        }

        RaiseModelInventoryPropertiesChanged();
        CaptureSettingsReturnContext();
        IsSettingsModalOpen = false;
        IsModelInventoryModalOpen = true;
    }

    private void CloseModelInventory()
    {
        IsModelInventoryModalOpen = false;
        RestoreSettingsAfterChildModalIfNeeded();
    }

    private void ShowModelHelp()
    {
        if (!CanShowModelHelp ||
            IsTechnicalDetailsModalOpen ||
            IsProfileDetailsModalOpen ||
            IsModelHelpModalOpen ||
            IsReplaceVideoConfirmationModalOpen ||
            IsModelInventoryModalOpen ||
            IsModelPackImportConfirmationModalOpen ||
            IsActivityLogModalOpen ||
            IsPreviewReadyModalOpen ||
            IsConversionCompletedModalOpen)
        {
            return;
        }

        OnPropertyChanged(nameof(ModelHelpRows));
        IsModelHelpModalOpen = true;
    }

    private void CloseModelHelp()
    {
        IsModelHelpModalOpen = false;
    }

    private void ShowTechnicalDetails()
    {
        if (IsConversionRunning ||
            IsPreviewGenerating ||
            IsProfileDetailsModalOpen ||
            IsModelHelpModalOpen ||
            IsReplaceVideoConfirmationModalOpen ||
            IsModelInventoryModalOpen ||
            IsModelPackImportConfirmationModalOpen ||
            IsActivityLogModalOpen ||
            IsPreviewReadyModalOpen ||
            IsConversionCompletedModalOpen)
        {
            return;
        }

        TechnicalDetailsBodyText = CreateSystemStatusTechnicalDetailsText();
        IsSettingsModalOpen = false;
        IsTechnicalDetailsModalOpen = true;
    }

    private void CloseTechnicalDetails()
    {
        IsTechnicalDetailsModalOpen = false;
    }

    private void ShowProfileDetails()
    {
        if (IsConversionRunning ||
            IsPreviewGenerating ||
            IsTechnicalDetailsModalOpen ||
            IsModelHelpModalOpen ||
            IsReplaceVideoConfirmationModalOpen ||
            IsModelInventoryModalOpen ||
            IsModelPackImportConfirmationModalOpen ||
            IsActivityLogModalOpen ||
            IsPreviewReadyModalOpen ||
            IsConversionCompletedModalOpen)
        {
            return;
        }

        IsProfileDetailsModalOpen = true;
    }

    private void CloseProfileDetails()
    {
        IsProfileDetailsModalOpen = false;
    }

    private void ViewActivityLog()
    {
        _activeActivityLogModalKind = ActivityLogModalKind.General;
        ActivityLogModalText = CreateFullActivityLogText();
        IsSettingsModalOpen = false;
        IsActivityLogModalOpen = true;
    }

    private void ViewImageActivityLog()
    {
        _activeActivityLogModalKind = ActivityLogModalKind.Image;
        ActivityLogModalText = CreateFullImageActivityLogText();
        IsActivityLogModalOpen = true;
    }

    private void CopyFullLog()
    {
        var isImageLogModal = IsActivityLogModalOpen && _activeActivityLogModalKind == ActivityLogModalKind.Image;
        var logText = isImageLogModal ? CreateFullImageActivityLogText() : CreateFullActivityLogText();
        ActivityLogModalText = logText;
        CopyLogToClipboard(
            logText,
            englishLogName: isImageLogModal ? "image activity log" : "activity log",
            spanishLogName: isImageLogModal ? "log de actividad de imagen" : "registro de actividad",
            appendFailureToPreviewLog: false);
    }

    private void CopyPreviewLog()
    {
        CopyLogToClipboard(
            PreviewGenerationLogText,
            englishLogName: "preview log",
            spanishLogName: "log de vista previa",
            appendFailureToPreviewLog: true);
    }

    private void CopyLogToClipboard(
        string logText,
        string englishLogName,
        string spanishLogName,
        bool appendFailureToPreviewLog)
    {
        try
        {
            System.Windows.Clipboard.SetText(logText);
            ShowLogCopySuccessNotification();
        }
        catch (Exception exception)
        {
            ShowLogCopyFailureNotification();
            var englishMessage = $"Could not copy {englishLogName} to clipboard: {exception.Message}";
            var spanishMessage = $"No se pudo copiar el {spanishLogName} al portapapeles: {exception.Message}";
            if (appendFailureToPreviewLog)
            {
                AppendPreviewLogLine(Text(englishMessage, spanishMessage));
            }

            AddLog(englishMessage, spanishMessage);
        }
    }

    private void CloseActivityLog()
    {
        IsActivityLogModalOpen = false;
        _activeActivityLogModalKind = ActivityLogModalKind.General;
    }

    private void ClearLogs()
    {
        Logs.Clear();
        OnPropertyChanged(nameof(ActivityLogPanelText));
        if (IsActivityLogModalOpen)
        {
            ActivityLogModalText = CreateFullActivityLogText();
        }
    }

    private void ClearImageLog()
    {
        ImageLogs.Clear();
        OnPropertyChanged(nameof(ImageActivityLogText));
        if (IsActivityLogModalOpen && _activeActivityLogModalKind == ActivityLogModalKind.Image)
        {
            ActivityLogModalText = CreateFullImageActivityLogText();
        }

        ClearImageLogCommand.RaiseCanExecuteChanged();
    }

    private void ConfirmReplaceVideo()
    {
        CompleteReplaceVideoConfirmation(replaceVideo: true);
    }

    private void CancelReplaceVideo()
    {
        CompleteReplaceVideoConfirmation(replaceVideo: false);
    }

    private void ConfirmPreviewInvalidation()
    {
        var pendingChange = _pendingPreviewInvalidatingChange;
        var completion = _previewInvalidationConfirmationCompletion;
        ResetPreviewInvalidationConfirmationModal();
        completion?.TrySetResult(true);

        if (pendingChange is null)
        {
            return;
        }

        _isApplyingPreviewInvalidatingChange = true;
        try
        {
            pendingChange();
        }
        finally
        {
            _isApplyingPreviewInvalidatingChange = false;
        }
    }

    private void CancelPreviewInvalidation()
    {
        var completion = _previewInvalidationConfirmationCompletion;
        ResetPreviewInvalidationConfirmationModal();
        completion?.TrySetResult(false);
    }

    private void ConfirmModelPackImport()
    {
        CompleteModelPackImportConfirmation(confirmImport: true);
        ShowGlobalBusyOverlay(
            "Importing model pack...",
            "Importando paquete de modelos...");
    }

    private void CancelModelPackImport()
    {
        CompleteModelPackImportConfirmation(confirmImport: false);
    }

    private void CompleteModelPackImportConfirmation(bool confirmImport)
    {
        var completion = _modelPackImportConfirmationCompletion;
        ResetModelPackImportConfirmationModal();
        completion?.TrySetResult(confirmImport);
    }

    private void ResetModelPackImportConfirmationModal()
    {
        IsModelPackImportConfirmationModalOpen = false;
        _modelPackImportConfirmationCompletion = null;
        _modelPackImportConfirmationPrompt = null;
        RaiseModelPackImportConfirmationPropertiesChanged();
    }

    private void CompleteReplaceVideoConfirmation(bool replaceVideo)
    {
        IsReplaceVideoConfirmationModalOpen = false;
        _replaceVideoConfirmationCompletion?.TrySetResult(replaceVideo);
        _replaceVideoConfirmationCompletion = null;
    }

    private bool TryDeferPreviewInvalidatingChange(Action applyChange)
    {
        if (_isApplyingPreviewInvalidatingChange ||
            !ShouldConfirmPreviewInvalidatingChange())
        {
            return false;
        }

        _pendingPreviewInvalidatingChange = applyChange;
        ShowPreviewInvalidationConfirmationModal();
        return true;
    }

    private Task<bool> ConfirmPreviewInvalidatingChangeAsync()
    {
        if (!ShouldConfirmPreviewInvalidatingChange())
        {
            return Task.FromResult(true);
        }

        _pendingPreviewInvalidatingChange = null;
        ShowPreviewInvalidationConfirmationModal();
        return _previewInvalidationConfirmationCompletion!.Task;
    }

    private void ShowPreviewInvalidationConfirmationModal()
    {
        IsTechnicalDetailsModalOpen = false;
        IsProfileDetailsModalOpen = false;
        IsModelHelpModalOpen = false;
        IsSettingsModalOpen = false;
        IsActivityLogModalOpen = false;
        IsPreviewReadyModalOpen = false;
        _previewInvalidationConfirmationCompletion =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IsPreviewInvalidationConfirmationModalOpen = true;
    }

    private void ResetPreviewInvalidationConfirmationModal()
    {
        IsPreviewInvalidationConfirmationModalOpen = false;
        _pendingPreviewInvalidatingChange = null;
        _previewInvalidationConfirmationCompletion = null;
    }

    private bool ShouldConfirmPreviewInvalidatingChange() =>
        !IsConversionRunning &&
        !IsPreviewGenerating &&
        _previewState.Status is PreviewGenerationStatus.Ready or PreviewGenerationStatus.Accepted &&
        IsPreviewFingerprintCurrent();

    private void UpdateConversionReadiness()
    {
        if (_dependencyHealth is null)
        {
            return;
        }

        var baseReadiness = _conversionReadinessService.Evaluate(_dependencyHealth);
        var selectedModel = SelectedLocalModelCandidate is null
            ? null
            : LocalModelPlanSelection.FromCandidate(SelectedLocalModelCandidate);
        _conversionReadiness =
            _iw3ConversionReadinessService.ApplyIw3ExecutionRequirements(
                baseReadiness,
                selectedModel);
        RaiseConversionReadinessPropertiesChanged();
    }

    private void UpdateLocalModelSelectionCandidates(bool regenerateCurrentPlan = true)
    {
        var previouslySelectedPath = SelectedLocalModelCandidate?.RelativePath;
        var previouslySelectedMappingKey = SelectedLocalModelCandidate?.MappingKey;
        var previouslySelectedDepthModelName = SelectedLocalModelCandidate?.Iw3DepthModelName;
        var candidates = Iw3DepthModelMapper.CreateSelectableCandidates(
            _dependencyHealth?.ModelInventory.SelectionCandidates ?? [],
            IsSpanish);

        LocalModelCandidates.Clear();
        foreach (var candidate in candidates)
        {
            LocalModelCandidates.Add(candidate);
        }

        var selectedCandidate = !string.IsNullOrWhiteSpace(previouslySelectedPath)
            ? LocalModelCandidates.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.RelativePath,
                    previouslySelectedPath,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    candidate.MappingKey,
                    previouslySelectedMappingKey,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    candidate.Iw3DepthModelName,
                    previouslySelectedDepthModelName,
                    StringComparison.OrdinalIgnoreCase))
            : null;
        selectedCandidate ??= LocalModelCandidates.FirstOrDefault(candidate =>
            string.Equals(
                candidate.MappingKey,
                Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                candidate.Iw3DepthModelName,
                Iw3DepthModelMapper.ZoeDAnyNDepthModelName,
                StringComparison.OrdinalIgnoreCase));

        SetSelectedLocalModelCandidate(
            selectedCandidate ?? LocalModelCandidates.FirstOrDefault(),
            regeneratePlan:
                regenerateCurrentPlan &&
                _analysis is not null &&
                _conversionRecommendation is not null);

        OnPropertyChanged(nameof(HasLocalModelSelectionCandidates));
        OnPropertyChanged(nameof(HasUnmappedLocalModelCandidates));
        OnPropertyChanged(nameof(LocalModelSelectionStatusText));
        RaiseModelHelpPropertiesChanged();
    }

    private void SetSelectedLocalModelCandidate(
        LocalModelSelectionCandidate? candidate,
        bool regeneratePlan = false)
    {
        if (ReferenceEquals(
            _selectedLocalModelCandidate,
            candidate))
        {
            return;
        }

        _selectedLocalModelCandidate = candidate;
        OnPropertyChanged(nameof(SelectedLocalModelCandidate));
        OnPropertyChanged(nameof(LocalModelSelectionStatusText));
        OnPropertyChanged(nameof(HasUnmappedLocalModelCandidates));
        RaisePreflightEstimatePropertiesChanged();
        UpdateConversionReadiness();

        if (!regeneratePlan)
        {
            return;
        }

        ResetPreviewConversionStageForPreviewAffectingChange();
        ResetConversionExecutionState();
        if (RegenerateConversionPlan())
        {
            MarkPreviewOutdatedIfNeeded();
        }
        else
        {
            OnPropertyChanged(nameof(ConversionPlanLocalModelText));
        }
    }

    private ConversionExecutionStartGateResult EvaluateConversionStartGate()
    {
        var startGate = _conversionExecutionFeatureGate.EvaluateStart(
            HasCompletedAnalysis,
            _conversionPlan is not null,
            _conversionReadiness);
        if (!startGate.CanStart)
        {
            return startGate;
        }

        var previewGate = PreviewConversionGate.Evaluate(
            _previewState,
            CreateCurrentPreviewConfiguration());
        if (previewGate.CanStart && PreviewOutputFileExists())
        {
            return startGate;
        }

        return ConversionExecutionStartGateResult.Blocked(
            ConversionExecutionBlocker.PreviewRequired,
            previewGate.CanStart ? "Preview required" : previewGate.EnglishStatus,
            previewGate.CanStart ? "Vista previa requerida" : previewGate.SpanishStatus,
            previewGate.CanStart
                ? "The accepted preview file was not found. Generate and accept a new preview."
                : previewGate.EnglishDetail,
            previewGate.CanStart
                ? "No se encontro el archivo de vista previa aceptado. Genera y acepta una nueva vista previa."
                : previewGate.SpanishDetail);
    }

    private bool CurrentExecutionRequestCanStart()
    {
        if (_conversionPlan is null)
        {
            return false;
        }

        if (_conversionReadiness?.CanConvert != true)
        {
            return false;
        }

        var request = _conversionExecutionRequestFactory.Create(
            _conversionPlan,
            SelectedOutputPreset,
            _planOptionState.CreatePlanOptions(_outputPathState.CustomOutputPath),
            _toolPaths);
        return _conversionExecutionRequestValidator
            .Validate(request)
            .CanStartLocalProcess;
    }

    private void UpdateToolStatuses()
    {
        if (_dependencyHealth is null)
        {
            return;
        }

        ToolStatuses.Clear();
        ToolStatuses.Add(CreateToolStatus(
            "FFmpeg",
            "FFmpeg",
            _dependencyHealth.Ffmpeg,
            ToolStatusComponent.BundledTool));
        ToolStatuses.Add(CreateToolStatus(
            "FFprobe",
            "FFprobe",
            _dependencyHealth.Ffprobe,
            ToolStatusComponent.BundledTool));
        ToolStatuses.Add(CreateToolStatus(
            "Python",
            "Python",
            _dependencyHealth.Python,
            ToolStatusComponent.EmbeddedPython));
        ToolStatuses.Add(CreateToolStatus(
            "iw3 engine",
            "Motor iw3",
            _dependencyHealth.Iw3EngineDirectory,
            ToolStatusComponent.Iw3Engine));
        ToolStatuses.Add(CreateToolStatus(
            "3D models",
            "modelos 3D",
            _dependencyHealth.ModelsDirectory,
            ToolStatusComponent.Models));
        ToolStatuses.Add(CreateToolStatus(
            "iw3 runtime dependency",
            "dependencia runtime iw3",
            _dependencyHealth.Iw3RuntimeDependencies,
            ToolStatusComponent.Iw3RuntimeDependency));
    }

    private ToolStatusItemViewModel CreateToolStatus(
        string englishName,
        string spanishName,
        ToolDependencyHealth dependencyHealth,
        ToolStatusComponent component)
    {
        var isEngine = component == ToolStatusComponent.Iw3Engine;
        var isModels = component == ToolStatusComponent.Models;
        return new(
            Name: Text(englishName, spanishName),
            StatusText: dependencyHealth.Status == ToolHealthStatus.Found
                ? Text("Found", "Encontrado")
                : Text("Missing", "Faltante"),
            ReasonText: ToolStatusReasonText(dependencyHealth, component),
            DetailText: ToolStatusDetailText(dependencyHealth, component),
            ContextActionText: isEngine
                ? Text("Open", "Abrir")
                : isModels
                    ? ViewModelsText
                    : string.Empty,
            ContextActionToolTip: isEngine
                ? OpenEngineFolderText
                : isModels
                    ? ViewModelsToolTipText
                    : string.Empty,
            ContextActionVisibility: isEngine || isModels
                ? Visibility.Visible
                : Visibility.Collapsed,
            ContextActionCommand: isEngine
                ? OpenEngineFolderCommand
                : isModels
                    ? ShowModelInventoryCommand
                    : null);
    }

    private string ToolStatusReasonText(
        ToolDependencyHealth dependencyHealth,
        ToolStatusComponent component) =>
        dependencyHealth.DetailKind switch
        {
            ToolHealthDetailKind.BundledFileFound => Text(
                component == ToolStatusComponent.EmbeddedPython
                    ? "Embedded Python executable found"
                    : "Bundled executable found",
                component == ToolStatusComponent.EmbeddedPython
                    ? "Ejecutable de Python embebido encontrado"
                    : "Ejecutable incluido encontrado"),
            ToolHealthDetailKind.BundledFileMissing => Text(
                component == ToolStatusComponent.EmbeddedPython
                    ? "Embedded Python executable missing"
                    : "Bundled executable not found",
                component == ToolStatusComponent.EmbeddedPython
                    ? "Falta el ejecutable de Python embebido"
                    : "Ejecutable incluido no encontrado"),
            ToolHealthDetailKind.EngineBundleFound => Text(
                "Required manifest and iw3 entry found",
                "Manifiesto requerido y entrada iw3 encontrados"),
            ToolHealthDetailKind.EngineDirectoryMissing => Text(
                "Engine folder not found",
                "Carpeta del motor no encontrada"),
            ToolHealthDetailKind.EnginePlaceholderOnly => Text(
                "Only placeholder or contract files found",
                "Solo se encontraron marcadores o archivos de contrato"),
            ToolHealthDetailKind.EngineManifestMissing => Text(
                "Engine manifest missing or placeholder",
                "El manifiesto del motor falta o es marcador"),
            ToolHealthDetailKind.EngineEntryFilesMissing => Text(
                "Required iw3 entry file missing",
                "Falta el archivo de entrada iw3 requerido"),
            ToolHealthDetailKind.ModelFilesFound => Text(
                "Compatible model files found",
                "Modelos compatibles encontrados"),
            ToolHealthDetailKind.ModelsDirectoryMissing => Text(
                "Models folder not found",
                "Carpeta de modelos no encontrada"),
            ToolHealthDetailKind.ModelFilesMissing => Text(
                "No compatible model files found",
                "No se encontraron modelos compatibles"),
            ToolHealthDetailKind.Iw3RuntimeDependenciesFound => Text(
                "Required iw3 runtime dependency found",
                "Dependencia de runtime iw3 requerida encontrada"),
            ToolHealthDetailKind.Iw3RuntimeDependenciesMissing => Text(
                "Missing iw3 runtime dependency",
                "Dependencia de runtime iw3 faltante"),
            _ => Text("Local dependency checked", "Dependencia local revisada"),
        };

    private string ToolStatusDetailText(
        ToolDependencyHealth dependencyHealth,
        ToolStatusComponent component) =>
        dependencyHealth.DetailKind switch
        {
            ToolHealthDetailKind.BundledFileFound => Text(
                component == ToolStatusComponent.EmbeddedPython
                    ? $"Embedded Python executable found: {dependencyHealth.ExpectedPath}"
                    : $"Bundled executable found: {dependencyHealth.ExpectedPath}",
                component == ToolStatusComponent.EmbeddedPython
                    ? $"Ejecutable de Python embebido encontrado: {dependencyHealth.ExpectedPath}"
                    : $"Ejecutable incluido encontrado: {dependencyHealth.ExpectedPath}"),
            ToolHealthDetailKind.BundledFileMissing => Text(
                component == ToolStatusComponent.EmbeddedPython
                    ? $"Expected embedded Python executable: {dependencyHealth.ExpectedPath}"
                    : $"Missing bundled executable: {dependencyHealth.ExpectedPath}",
                component == ToolStatusComponent.EmbeddedPython
                    ? $"Ejecutable esperado de Python embebido: {dependencyHealth.ExpectedPath}"
                    : $"Falta el ejecutable incluido: {dependencyHealth.ExpectedPath}"),
            ToolHealthDetailKind.EngineBundleFound => Text(
                $"Local iw3 bundle found under: {dependencyHealth.ExpectedPath}. Required: non-placeholder ENGINE_MANIFEST.json and nunif/iw3/__main__.py.",
                $"Bundle local de iw3 encontrado en: {dependencyHealth.ExpectedPath}. Requerido: ENGINE_MANIFEST.json que no sea marcador y nunif/iw3/__main__.py."),
            ToolHealthDetailKind.EngineDirectoryMissing => Text(
                $"Expected local iw3 engine folder: {dependencyHealth.ExpectedPath}. Required layout: ENGINE_MANIFEST.json, python/python.exe, nunif/iw3/__main__.py, and nunif/iw3/pretrained_models.",
                $"Carpeta esperada del motor iw3 local: {dependencyHealth.ExpectedPath}. Estructura requerida: ENGINE_MANIFEST.json, python/python.exe, nunif/iw3/__main__.py y nunif/iw3/pretrained_models."),
            ToolHealthDetailKind.EnginePlaceholderOnly => Text(
                $"Engine folder exists, but only placeholder or contract files were detected: {dependencyHealth.ExpectedPath}. Add a real nunif bundle with a non-placeholder ENGINE_MANIFEST.json and nunif/iw3/__main__.py.",
                $"La carpeta del motor existe, pero solo contiene marcadores o archivos de contrato: {dependencyHealth.ExpectedPath}. Agrega un bundle real de nunif con ENGINE_MANIFEST.json que no sea marcador y nunif/iw3/__main__.py."),
            ToolHealthDetailKind.EngineManifestMissing => Text(
                $"Engine content exists, but ENGINE_MANIFEST.json is missing or still has version=placeholder: {Path.Combine(dependencyHealth.ExpectedPath, "ENGINE_MANIFEST.json")}",
                $"Hay contenido del motor, pero ENGINE_MANIFEST.json falta o aÃºn tiene version=placeholder: {Path.Combine(dependencyHealth.ExpectedPath, "ENGINE_MANIFEST.json")}"),
            ToolHealthDetailKind.EngineEntryFilesMissing => Text(
                $"Engine manifest exists, but no supported iw3 entry file was found. Expected nunif/iw3/__main__.py under: {dependencyHealth.ExpectedPath}",
                $"El manifiesto del motor existe, pero no se encontrÃ³ un archivo de entrada iw3 compatible. Se esperaba nunif/iw3/__main__.py en: {dependencyHealth.ExpectedPath}"),
            ToolHealthDetailKind.ModelFilesFound => Text(
                $"Local 3D model files found under: {dependencyHealth.ExpectedPath}",
                $"Modelos 3D locales encontrados en: {dependencyHealth.ExpectedPath}"),
            ToolHealthDetailKind.ModelsDirectoryMissing => Text(
                $"Expected local iw3 pretrained models folder: {dependencyHealth.ExpectedPath}",
                $"Carpeta esperada de modelos preentrenados de iw3: {dependencyHealth.ExpectedPath}"),
            ToolHealthDetailKind.ModelFilesMissing => Text(
                $"No supported model files found in: {dependencyHealth.ExpectedPath}",
                $"No se encontraron modelos compatibles en: {dependencyHealth.ExpectedPath}"),
            ToolHealthDetailKind.Iw3RuntimeDependenciesFound => Text(
                $"Bundled iw3 runtime dependency found: {dependencyHealth.ExpectedPath}",
                $"Dependencia de runtime iw3 incluida encontrada: {dependencyHealth.ExpectedPath}"),
            ToolHealthDetailKind.Iw3RuntimeDependenciesMissing => Text(
                $"Missing iw3 runtime dependency: {dependencyHealth.ExpectedPath}. The bundle is not fully offline-ready and iw3 may try to download this file at runtime.",
                $"Dependencia de runtime iw3 faltante: {dependencyHealth.ExpectedPath}. El bundle aun no esta listo para uso offline e iw3 podria intentar descargar este archivo en tiempo de ejecucion."),
            _ => dependencyHealth.ExpectedPath,
        };

    private string CreateSystemStatusTechnicalDetailsText()
    {
        var lines = new List<string>
        {
            Text("Expected local iw3 bundle layout", "Estructura esperada del bundle local iw3"),
            Text($"{Iw3EngineBundleContract.ManifestRelativePath} (version must not be placeholder)",
                $"{Iw3EngineBundleContract.ManifestRelativePath} (version no debe ser placeholder)"),
            Text(Iw3EngineBundleContract.PythonExecutableRelativePath, Iw3EngineBundleContract.PythonExecutableRelativePath),
            Text(Iw3EngineBundleContract.PythonPathFileRelativePath, Iw3EngineBundleContract.PythonPathFileRelativePath),
            Text(Iw3EngineBundleContract.Iw3PackageMainRelativePath, Iw3EngineBundleContract.Iw3PackageMainRelativePath),
            Text(Iw3EngineBundleContract.ModelsDirectoryRelativePath + "/*" + string.Join("|*", Iw3EngineBundleContract.SupportedModelExtensions),
                Iw3EngineBundleContract.ModelsDirectoryRelativePath + "/*" + string.Join("|*", Iw3EngineBundleContract.SupportedModelExtensions)),
            Text(Iw3EngineBundleContract.Iw3DefaultStereoRuntimeDependencyRelativePath,
                Iw3EngineBundleContract.Iw3DefaultStereoRuntimeDependencyRelativePath),
            Text($"{Iw3EngineBundleContract.CliCapabilitiesRelativePath} (optional)",
                $"{Iw3EngineBundleContract.CliCapabilitiesRelativePath} (opcional)"),
            string.Empty,
        };

        lines.AddRange(CreateModelInventoryTechnicalDetailsLines());
        lines.Add(string.Empty);
        lines.AddRange(Iw3CliCapabilitiesDetailsFormatter.CreateLines(
            _dependencyHealth?.Iw3CliCapabilities ??
            Iw3CliCapabilitiesManifest.Missing(_toolPaths.Iw3CliCapabilitiesFile),
            IsSpanish));
        lines.Add(string.Empty);
        lines.Add(SystemStatusToolsTabTitle);
        lines.Add(string.Empty);

        foreach (var toolStatus in ToolStatuses)
        {
            lines.Add(toolStatus.Name);
            lines.Add(toolStatus.DetailText);
            lines.Add(string.Empty);
        }

        lines.Add(SystemStatusConversionTabTitle);
        lines.Add(string.Empty);
        lines.Add($"{ConversionReadinessStatusLabel}: {ConversionReadinessStatusText}");
        lines.Add(string.Empty);
        lines.Add($"{ConversionReadinessMissingRequirementsTitle}:");
        lines.Add(ConversionReadinessIssuesText);
        lines.Add(string.Empty);

        if (!string.IsNullOrWhiteSpace(ConversionReadinessRequiredComponentsText))
        {
            lines.Add(ConversionReadinessRequiredComponentsText);
            lines.Add(string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(ConversionBlockedReasonText))
        {
            lines.Add(ConversionBlockedReasonText);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private IReadOnlyList<string> CreateModelInventoryTechnicalDetailsLines()
    {
        var inventory = _dependencyHealth?.ModelInventory ??
            LocalModelInventory.Empty(_toolPaths.ModelsDirectory);
        var lines = new List<string>
        {
            Text("Local model inventory", "Inventario local de modelos"),
            Text(
                $"Models directory: {inventory.ModelsDirectory}",
                $"Carpeta de modelos: {inventory.ModelsDirectory}"),
            Text(
                $"Supported extensions: {string.Join(", ", inventory.SupportedExtensions)}",
                $"Extensiones compatibles: {string.Join(", ", inventory.SupportedExtensions)}"),
            Text(
                $"Compatible model count: {inventory.CompatibleModelCount}",
                $"Cantidad de modelos compatibles: {inventory.CompatibleModelCount}"),
            Text(
                $"Catalog path: {inventory.Catalog.CatalogPath}",
                $"Ruta del catalogo: {inventory.Catalog.CatalogPath}"),
            Text(
                $"Catalog status: {CatalogStatusText(inventory.Catalog.Status, useSpanish: false)}",
                $"Estado del catalogo: {CatalogStatusText(inventory.Catalog.Status, useSpanish: true)}"),
            Text(
                $"Catalog entries: {inventory.Catalog.EntryCount}",
                $"Entradas del catalogo: {inventory.Catalog.EntryCount}"),
            Text(
                $"Entries with existing compatible files: {inventory.Catalog.EntriesWithExistingCompatibleFiles.Count}",
                $"Entradas con archivos compatibles existentes: {inventory.Catalog.EntriesWithExistingCompatibleFiles.Count}"),
            Text(
                $"Entries referencing missing or unsupported files: {inventory.Catalog.EntriesWithMissingFiles.Count}",
                $"Entradas con archivos faltantes o no compatibles: {inventory.Catalog.EntriesWithMissingFiles.Count}"),
            Text(
                $"Unmanaged compatible model files: {inventory.Catalog.UnmanagedCompatibleModelFiles.Count}",
                $"Modelos compatibles no listados en el catalogo: {inventory.Catalog.UnmanagedCompatibleModelFiles.Count}"),
        };

        AddModelCatalogStatusDetailLines(lines, inventory.Catalog);

        if (!inventory.DirectoryExists)
        {
            lines.Add(Text(
                "Models directory was not found.",
                "No se encontro la carpeta de modelos."));
            return lines;
        }

        if (!inventory.HasCompatibleModels)
        {
            lines.Add(Text(
                "No compatible model files were found.",
                "No se encontraron modelos compatibles."));
            return lines;
        }

        lines.Add(Text(
            "Compatible model files:",
            "Archivos de modelo compatibles:"));
        foreach (var modelFile in inventory.CompatibleModelFiles)
        {
            lines.Add($"- {modelFile.RelativePath}");
        }

        var mappedCandidates = Iw3DepthModelMapper.CreateSelectableCandidates(
            inventory.SelectionCandidates,
            IsSpanish);
        lines.Add(Text(
            "Mapped selectable local models:",
            "Modelos locales mapeados seleccionables:"));
        if (mappedCandidates.Count == 0)
        {
            lines.Add(Text(
                "- None. A verified iw3 depth-model mapping is required before conversion.",
                "- Ninguno. Se requiere un mapeo verificado de depth-model iw3 antes de convertir."));
        }
        else
        {
            foreach (var candidate in mappedCandidates)
            {
                lines.Add(
                    $"- {candidate.DisplayName} -> --depth-model {candidate.Iw3DepthModelName}; source: {candidate.RelativePath}");
            }
        }

        var unmappedCandidates = Iw3DepthModelMapper.GetUnmappedCandidates(inventory.SelectionCandidates);
        if (unmappedCandidates.Count > 0)
        {
            lines.Add(Text(
                "Unmapped model files were found. Add a model catalog entry or mapping before using them.",
                "Se encontraron modelos no mapeados. Agrega una entrada de catalogo o mapeo antes de usarlos."));
            foreach (var candidate in unmappedCandidates)
            {
                lines.Add($"- {candidate.DisplayName} -> {candidate.RelativePath}");
            }
        }

        return lines;
    }

    private void AddModelCatalogStatusDetailLines(
        ICollection<string> lines,
        LocalModelCatalog catalog)
    {
        switch (catalog.Status)
        {
            case LocalModelCatalogStatus.Missing:
                lines.Add(Text(
                    "Model catalog not found. Compatible files are treated as unmanaged local models.",
                    "No se encontro el catalogo de modelos. Los archivos compatibles se tratan como modelos locales no administrados."));
                return;
            case LocalModelCatalogStatus.Invalid:
                lines.Add(Text(
                    $"Model catalog is invalid: {catalog.ErrorMessage}",
                    $"El catalogo de modelos no es valido: {catalog.ErrorMessage}"));
                lines.Add(Text(
                    "Compatible files are treated as unmanaged local models.",
                    "Los archivos compatibles se tratan como modelos locales no administrados."));
                return;
            case LocalModelCatalogStatus.Placeholder:
                lines.Add(Text(
                    "Model catalog is a placeholder and is ignored.",
                    "El catalogo de modelos es un marcador y se ignora."));
                lines.Add(Text(
                    "Compatible files are treated as unmanaged local models.",
                    "Los archivos compatibles se tratan como modelos locales no administrados."));
                return;
        }

        if (catalog.EntriesWithExistingCompatibleFiles.Count > 0)
        {
            lines.Add(Text(
                "Catalog entries with existing compatible files:",
                "Entradas del catalogo con archivos compatibles existentes:"));
            foreach (var entry in catalog.EntriesWithExistingCompatibleFiles)
            {
                lines.Add($"- {CatalogEntryDisplayText(entry)}");
            }
        }

        if (catalog.EntriesWithMissingFiles.Count > 0)
        {
            lines.Add(Text(
                "Catalog entries with missing or unsupported files:",
                "Entradas del catalogo con archivos faltantes o no compatibles:"));
            foreach (var entry in catalog.EntriesWithMissingFiles)
            {
                lines.Add($"- {CatalogEntryDisplayText(entry)}");
            }
        }

        if (catalog.UnmanagedCompatibleModelFiles.Count > 0)
        {
            lines.Add(Text(
                "Unmanaged compatible model files:",
                "Archivos compatibles no listados en el catalogo:"));
            foreach (var modelFile in catalog.UnmanagedCompatibleModelFiles)
            {
                lines.Add($"- {modelFile.RelativePath}");
            }
        }
    }

    private static string CatalogEntryDisplayText(LocalModelCatalogEntry entry)
    {
        var name = string.IsNullOrWhiteSpace(entry.DisplayName)
            ? entry.Id
            : entry.DisplayName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "(unnamed)";
        }

        var file = string.IsNullOrWhiteSpace(entry.File)
            ? "(missing file field)"
            : entry.File;
        var type = string.IsNullOrWhiteSpace(entry.ModelType)
            ? entry.Purpose
            : entry.ModelType;

        return string.IsNullOrWhiteSpace(type)
            ? $"{name} -> {file}"
            : $"{name} [{type}] -> {file}";
    }

    private static string CatalogStatusText(
        LocalModelCatalogStatus status,
        bool useSpanish) => status switch
    {
        LocalModelCatalogStatus.Missing => useSpanish ? "No encontrado" : "Missing",
        LocalModelCatalogStatus.Invalid => useSpanish ? "No valido" : "Invalid",
        LocalModelCatalogStatus.Placeholder => useSpanish ? "Marcador" : "Placeholder",
        LocalModelCatalogStatus.Found => useSpanish ? "Encontrado" : "Found",
        _ => status.ToString(),
    };

    private string CreateMissingComponentsSummary()
    {
        if (_dependencyHealth is null)
        {
            return "-";
        }

        var missingComponents = new List<string>();
        AddMissingComponent(missingComponents, _dependencyHealth.Ffmpeg, "FFmpeg", "FFmpeg");
        AddMissingComponent(missingComponents, _dependencyHealth.Ffprobe, "FFprobe", "FFprobe");
        AddMissingComponent(missingComponents, _dependencyHealth.Python, "Python", "Python");
        AddMissingComponent(missingComponents, _dependencyHealth.Iw3EngineDirectory, "iw3 engine", "motor iw3");
        AddMissingComponent(missingComponents, _dependencyHealth.ModelsDirectory, "3D models", "modelos 3D");
        AddMissingComponent(
            missingComponents,
            _dependencyHealth.Iw3RuntimeDependencies,
            "iw3 runtime dependency",
            "dependencia runtime iw3");

        return missingComponents.Count == 0
            ? Text("No missing components.", "No faltan componentes.")
            : Text("Missing: ", "Faltan: ") + string.Join(", ", missingComponents);
    }

    private void AddMissingComponent(
        ICollection<string> missingComponents,
        ToolDependencyHealth dependencyHealth,
        string englishName,
        string spanishName)
    {
        if (dependencyHealth.Status == ToolHealthStatus.Missing)
        {
            missingComponents.Add(Text(englishName, spanishName));
        }
    }

    private void SetSelectedVideo(string path, bool replacingVideo)
    {
        SelectedWorkflowTabIndex = 0;
        ResetPreviewConversionStageForPreviewAffectingChange(showNoticeWhenNoPreview: false);
        var cleanupPaths = CreateCurrentPreviewCleanupPaths();
        _ = DeletePreviewFilesAsync(cleanupPaths, logDeletion: false);
        SetPreviewTimeRangeText(PreviewTimeRangeService.CreateDefaultRange(null));
        _previewState = _previewState.Deleted(TimeSpan.Zero, PreviewTimeRangeService.DefaultDuration);
        _lastPreviewCachePaths = null;
        _hasUserEditedPreviewRange = false;
        _previewProgressPercent = 0;
        RaisePreviewPropertiesChanged();
        SelectedVideoPath = path;
        ResetAnalysisState(clearOutputPath: true);
        AddLog(
            replacingVideo ? $"Selected video replaced: {path}" : $"Selected video: {path}",
            replacingVideo ? $"Video seleccionado reemplazado: {path}" : $"Video seleccionado: {path}");
    }

    private void ResetAnalysisState(bool clearOutputPath)
    {
        ResetPreviewConversionStageForPreviewAffectingChange(showNoticeWhenNoPreview: false);
        ResetConversionExecutionState();
        HasCompletedAnalysis = false;
        _analysis = null;
        _conversionRecommendation = null;
        _conversionPlan = null;
        SetCanOpenConversionPlanStep(false);

        if (clearOutputPath)
        {
            _outputPathState.ClearCustomOutputPath();
            OnPropertyChanged(nameof(HasCustomOutputPath));
            SetOutputPathText(string.Empty);
        }

        RaiseAnalysisPropertiesChanged();
        RaiseRecommendationPropertiesChanged();
        RaiseConversionPlanPropertiesChanged();
        RaiseConversionReadinessPropertiesChanged();
    }

    private async Task CleanStalePreviewFilesAsync()
    {
        try
        {
            await Task.Run(() =>
                {
                    var cacheDirectory = _previewCachePathProvider.GetPreviewCacheDirectory();
                    _previewCacheCleaner.DeleteStaleFiles(
                        cacheDirectory,
                        TimeSpan.FromHours(24),
                        DateTimeOffset.UtcNow);
                    _previewCacheCleaner.DeletePartialFiles(cacheDirectory);
                })
                .ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            AddLog(
                $"Preview cache cleanup skipped: {exception.Message}",
                $"Limpieza de cache de vista previa omitida: {exception.Message}");
        }
    }

    private async Task CleanStalePreviewPartialFilesBeforeStartAsync()
    {
        var warningCount = 0;
        var deleted = 0;
        try
        {
            deleted = await Task.Run(() =>
                {
                    var cacheDirectory = _previewCachePathProvider.GetPreviewCacheDirectory();
                    return _previewCacheCleaner.DeletePartialFiles(
                        cacheDirectory,
                        (_, _) => warningCount++);
                })
                .ConfigureAwait(true);
        }
        catch
        {
            warningCount++;
        }

        if (deleted > 0)
        {
            AddLog(
                "Stale preview partial file was cleaned.",
                "Se limpi\u00f3 un archivo parcial anterior de vista previa.");
        }

        if (warningCount > 0)
        {
            AddLog(
                "Could not delete stale partial file.",
                "No se pudo eliminar un archivo parcial anterior.");
        }
    }

    private async Task GeneratePreviewAsync()
    {
        using var previewOperation = AppErrorLogService.BeginOperation("Generate preview");
        try
        {
            await GeneratePreviewCoreAsync();
        }
        catch (OperationCanceledException)
        {
            RecordPreviewCanceled();
        }
        catch (Exception exception)
        {
            RecordUnexpectedPreviewFailure(exception);
        }
        finally
        {
            if (_previewCancellationTokenSource is not null ||
                IsPreviewGeneratingModalOpen ||
                IsPreviewGenerating)
            {
                if (_isPreviewCancellationRequested ||
                    _previewState.Status == PreviewGenerationStatus.Canceled)
                {
                    await DeleteCanceledPreviewAttemptFilesAsync(logDeletion: true);
                }

                _previewCancellationTokenSource?.Dispose();
                _previewCancellationTokenSource = null;
                _isPreviewCancellationRequested = false;
                IsPreviewGeneratingModalOpen = false;
                RaisePreviewPropertiesChanged();
                RaiseConversionExecutionPropertiesChanged();
                RaiseConversionRunningModePropertiesChanged();
                RaiseConversionReadinessPropertiesChanged();
            }
        }
    }

    private async Task GeneratePreviewCoreAsync()
    {
        if (!HasEnteredPreviewConversionStage)
        {
            AddLog(
                "Open the conversion plan and continue before generating a preview.",
                "Abre el plan de conversion y continua antes de generar una vista previa.");
            RaisePreviewPropertiesChanged();
            return;
        }

        if (IsConversionRunning)
        {
            AddLog(
                "Preview generation is disabled while final conversion is running.",
                "La generacion de vista previa esta deshabilitada mientras la conversion final esta en ejecucion.");
            return;
        }

        if (IsPreviewGenerating)
        {
            AddLog(
                "A preview is already being generated.",
                "Ya se esta generando una vista previa.");
            return;
        }

        var rangeValidation = CurrentPreviewTimeRangeValidation;
        if (!rangeValidation.IsValid)
        {
            AddLog(
                $"Preview cannot start. {PreviewTimeRangeValidationMessage(rangeValidation.Issue, useSpanish: false)}",
                $"La vista previa no puede iniciar. {PreviewTimeRangeValidationMessage(rangeValidation.Issue, useSpanish: true)}");
            RaisePreviewPropertiesChanged();
            return;
        }

        var configuration = CreateCurrentPreviewConfiguration();
        if (configuration is null ||
            _conversionPlan?.SelectedLocalModel is null)
        {
            AddLog(
                "Preview cannot start until a source video, conversion plan, and mapped local model are selected.",
                "La vista previa no puede iniciar hasta seleccionar un video, un plan de conversion y un modelo local mapeado.");
            return;
        }

        if (!CurrentExecutionRequestCanStart())
        {
            AddLog(
                "Preview cannot start because the selected configuration is not ready for local iw3 execution.",
                "La vista previa no puede iniciar porque la configuracion seleccionada no esta lista para ejecucion local iw3.");
            return;
        }

        await CleanStalePreviewPartialFilesBeforeStartAsync();
        var cleanupPaths = CreateCurrentPreviewCleanupPaths();

        var startedAt = DateTimeOffset.UtcNow;
        var cachePaths = _previewCachePathService.CreatePaths(configuration, startedAt);
        _lastPreviewCachePaths = cachePaths;
        _previewCancellationTokenSource?.Dispose();
        _previewCancellationTokenSource = new CancellationTokenSource();
        _isPreviewCancellationRequested = false;
        _hasLoggedPreviewCancellationSummary = false;
        _previewProgressPercent = 0;
        _previewState = _previewState.Generating(configuration, startedAt);
        _hasLoggedPreviewOfflineDependencyWarning = false;
        ResetPreviewMetricText();
        ResetPreviewGenerationLog();
        AppendPreviewLogLine(Text("Preparing preview...", "Preparando vista previa..."));
        IsPreviewReadyModalOpen = false;
        IsPreviewGeneratingModalOpen = true;
        AppendPreviewLogLine(Text(
            "Preview timing: modal opened.",
            "Tiempo de vista previa: modal abierto."));
        RaisePreviewPropertiesChanged();
        RaiseConversionExecutionPropertiesChanged();
        RaiseConversionRunningModePropertiesChanged();
        AddLog(
            "Starting selected-configuration preview.",
            "Iniciando vista previa de la configuracion seleccionada.");
        await Task.Yield();
        await DeletePreviewFilesAsync(cleanupPaths, logDeletion: false);

        try
        {
            var request = new Iw3PreviewGenerationRequest(
                Configuration: configuration,
                CachePaths: cachePaths,
                ExpectedToolPaths: _toolPaths,
                SelectedLocalModel: _conversionPlan.SelectedLocalModel,
                Iw3CliCapabilities: _dependencyHealth?.Iw3CliCapabilities,
                CancellationToken: _previewCancellationTokenSource.Token);
            var result = await _previewExecutor.ExecuteAsync(
                request,
                new Progress<ConversionExecutionProgressUpdate>(ApplyPreviewProgressUpdate),
                _previewCancellationTokenSource.Token);

            foreach (var log in result.Logs)
            {
                AppendPreviewResultLog(log);
            }

            if (_isPreviewCancellationRequested ||
                result.WasCanceled ||
                _previewCancellationTokenSource?.IsCancellationRequested == true)
            {
                RecordPreviewCanceled();
                return;
            }

            _previewState = _previewState.Complete(result, configuration);
            if (result.Success &&
                _previewState.Status == PreviewGenerationStatus.Ready)
            {
                _previewProgressPercent = 100;
                RecordSuccessfulPerformanceHistory(
                    ConversionPerformanceOperationType.Preview,
                    result.StartedAt,
                    result.FinishedAt,
                    CurrentPreviewDuration);
            }
            else if (_previewState.Status is PreviewGenerationStatus.Canceled or PreviewGenerationStatus.Failed)
            {
                _previewProgressPercent = 0;
            }

            MarkPreviewOutdatedIfNeeded(logChange: false);
            AddLog(result.EnglishSummary, result.SpanishSummary);
            if (_previewState.Status == PreviewGenerationStatus.Ready &&
                IsPreviewFingerprintCurrent())
            {
                IsPreviewReadyModalOpen = true;
                AppendPreviewLogLine(Text(
                    "Preview timing: preview ready modal opened.",
                    "Tiempo de vista previa: modal de vista previa lista abierto."));
            }
        }
        catch (OperationCanceledException)
        {
            RecordPreviewCanceled();
        }
        catch (Exception exception)
        {
            RecordUnexpectedPreviewFailure(exception);
        }
        finally
        {
            if (_isPreviewCancellationRequested ||
                _previewState.Status == PreviewGenerationStatus.Canceled)
            {
                await DeleteCanceledPreviewAttemptFilesAsync(logDeletion: true);
            }

            _previewCancellationTokenSource?.Dispose();
            _previewCancellationTokenSource = null;
            _isPreviewCancellationRequested = false;
            IsPreviewGeneratingModalOpen = false;
            RaisePreviewPropertiesChanged();
            RaiseConversionExecutionPropertiesChanged();
            RaiseConversionRunningModePropertiesChanged();
            RaiseConversionReadinessPropertiesChanged();
        }
    }

    private void RecordPreviewCanceled()
    {
        _previewProgressPercent = 0;
        _previewState = _previewState with
        {
            Status = PreviewGenerationStatus.Canceled,
            OutputPath = null,
            FinishedAt = DateTimeOffset.UtcNow,
            EnglishDetail = "Preview generation was canceled.",
            SpanishDetail = "La generacion de vista previa fue cancelada.",
        };
        if (!_hasLoggedPreviewCancellationSummary)
        {
            AddLog(
                "Preview generation was canceled.",
                "La generacion de vista previa fue cancelada.");
            _hasLoggedPreviewCancellationSummary = true;
        }

        RaisePreviewPropertiesChanged();
        RaiseConversionExecutionPropertiesChanged();
        RaiseConversionRunningModePropertiesChanged();
        RaiseConversionReadinessPropertiesChanged();
    }

    private void RecordUnexpectedPreviewFailure(Exception exception)
    {
        var errorLogPath = AppErrorLogService.LogRecoverableException(
            "Generate preview",
            exception);
        _previewProgressPercent = 0;
        _previewState = _previewState with
        {
            Status = PreviewGenerationStatus.Failed,
            FinishedAt = DateTimeOffset.UtcNow,
            EnglishDetail = $"Preview generation failed unexpectedly: {exception.Message}",
            SpanishDetail = $"La generacion de vista previa fallo inesperadamente: {exception.Message}",
        };
        AppendPreviewLogLine($"Preview generation failed unexpectedly: {exception}");
        AddLog(
            $"Preview generation failed. Details were written to {errorLogPath}. {exception.Message}",
            $"La generacion de vista previa fallo. Los detalles se escribieron en {errorLogPath}. {exception.Message}");
        RaisePreviewPropertiesChanged();
        RaiseConversionReadinessPropertiesChanged();
    }

    private void CancelPreview()
    {
        if (!CanCancelPreview &&
            _previewCancellationTokenSource is null)
        {
            AddLog(
                "There is no active preview to cancel.",
                "No hay una vista previa activa para cancelar.");
            return;
        }

        if (_isPreviewCancellationRequested)
        {
            return;
        }

        _isPreviewCancellationRequested = true;
        _previewCancellationTokenSource?.Cancel();
        IsPreviewGeneratingModalOpen = false;
        RecordPreviewCanceled();
    }

    private void OpenPreview()
    {
        var openResult = _previewOutputOpenService.OpenCurrentPreview(_previewState);
        if (openResult.EnglishWarning is not null &&
            openResult.SpanishWarning is not null)
        {
            AddLog(openResult.EnglishWarning, openResult.SpanishWarning);
            return;
        }

        AddLog(
            "Preview opened with the default video player.",
            "Vista previa abierta con el reproductor de video predeterminado.");
    }

    private void ContinuePreview()
    {
        if (!CanContinuePreview)
        {
            AddLog(
                "Preview cannot be accepted because it does not match the current settings.",
                "La vista previa no puede aceptarse porque no coincide con la configuracion actual.");
            return;
        }

        var configuration = CreateCurrentPreviewConfiguration();
        if (configuration is null)
        {
            AddLog(
                "Preview cannot be accepted until the current configuration is valid.",
                "La vista previa no puede aceptarse hasta que la configuracion actual sea valida.");
            return;
        }

        _previewState = _previewState.Accept(configuration);
        IsPreviewReadyModalOpen = false;
        AddLog(
            "Preview accepted. Final conversion is now available.",
            "Vista previa aceptada. La conversion final ahora esta disponible.");
        RaisePreviewPropertiesChanged();
        RaiseConversionExecutionPropertiesChanged();
        RaiseConversionReadinessPropertiesChanged();
    }

    private void DeletePreview()
    {
        DeleteCurrentPreviewFiles(logDeletion: true);
        _previewProgressPercent = 0;
        _previewState = _previewState.Deleted(CurrentPreviewStartTime, CurrentPreviewDuration);
        IsPreviewReadyModalOpen = false;
        _lastPreviewCachePaths = null;
        RaisePreviewPropertiesChanged();
    }

    private void DeleteCurrentPreviewFiles(bool logDeletion)
    {
        var paths = CreateCurrentPreviewCleanupPaths();
        try
        {
            _previewCacheCleaner.DeletePreviewFiles(
                _previewCachePathProvider.GetPreviewCacheDirectory(),
                paths);
        }
        catch (Exception exception)
        {
            AddLog(
                $"Preview cleanup warning: {exception.Message}",
                $"Advertencia de limpieza de vista previa: {exception.Message}");
        }

        if (logDeletion)
        {
            AddLog(
                "Preview files were deleted.",
                "Los archivos de vista previa fueron eliminados.");
        }
    }

    private IReadOnlyList<string?> CreateCurrentPreviewCleanupPaths()
    {
        var paths = new List<string?>();
        if (_lastPreviewCachePaths is not null)
        {
            paths.AddRange(_lastPreviewCachePaths.AllPaths);
        }

        paths.Add(_previewState.OutputPath);
        return paths;
    }

    private async Task<int> DeletePreviewFilesAsync(
        IReadOnlyList<string?> paths,
        bool logDeletion)
    {
        var deleted = 0;
        try
        {
            deleted = await Task.Run(() => _previewCacheCleaner.DeletePreviewFiles(
                    _previewCachePathProvider.GetPreviewCacheDirectory(),
                    paths))
                .ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            AddLog(
                $"Preview cleanup warning: {exception.Message}",
                $"Advertencia de limpieza de vista previa: {exception.Message}");
        }

        if (logDeletion)
        {
            AddLog(
                "Preview files were deleted.",
                "Los archivos de vista previa fueron eliminados.");
        }

        return deleted;
    }

    private async Task DeleteCanceledPreviewAttemptFilesAsync(bool logDeletion)
    {
        var deleted = 0;
        var paths = _lastPreviewCachePaths?.AllPaths ?? [];
        if (paths.Count > 0)
        {
            deleted += await DeletePreviewFilesAsync(paths, logDeletion: false);
        }

        try
        {
            deleted += await Task.Run(() => _previewCacheCleaner.DeletePartialFiles(
                    _previewCachePathProvider.GetPreviewCacheDirectory()))
                .ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            AddLog(
                $"Preview partial cleanup warning: {exception.Message}",
                $"Advertencia de limpieza de parciales de vista previa: {exception.Message}");
        }

        if (logDeletion && deleted > 0)
        {
            AddLog(
                "Preview partial files were cleaned.",
                "Los archivos parciales de vista previa fueron limpiados.");
        }
    }

    private PreviewConfigurationSnapshot? CreateCurrentPreviewConfiguration()
    {
        if (_conversionPlan is null)
        {
            return null;
        }

        var timeRangeValidation = CurrentPreviewTimeRangeValidation;
        if (!timeRangeValidation.IsValid ||
            timeRangeValidation.Range is null)
        {
            return null;
        }

        return PreviewConfigurationSnapshot.Create(
            _conversionPlan,
            SelectedOutputPreset,
            timeRangeValidation.Range);
    }

    private void SetHasEnteredPreviewConversionStage(bool value)
    {
        if (_hasEnteredPreviewConversionStage == value)
        {
            return;
        }

        _hasEnteredPreviewConversionStage = value;
        RaisePreviewConversionStagePropertiesChanged();
    }

    private void ResetPreviewConversionStageForPreviewAffectingChange(
        bool showNoticeWhenNoPreview = true)
    {
        var shouldShowNotice =
            showNoticeWhenNoPreview &&
            HasEnteredPreviewConversionStage &&
            !ShouldConfirmPreviewInvalidatingChange();

        SetHasEnteredPreviewConversionStage(false);

        if (shouldShowNotice)
        {
            ShowPreviewStageResetNotice();
        }
        else if (!showNoticeWhenNoPreview)
        {
            ClearPreviewStageResetNotice();
        }
    }

    private void ShowPreviewStageResetNotice()
    {
        ShowLogCopyNotification(
            "Review the conversion plan again, then press Continue with conversion.",
            "Revisa de nuevo el plan de conversion y presiona Continuar con la conversion.",
            PreviewStageResetNoticeDuration,
            TopToastKind.PreviewStageReset);
    }

    private void ClearPreviewStageResetNotice()
    {
        HideLogCopyNotification(TopToastKind.PreviewStageReset);
    }

    private void MarkPreviewOutdatedIfNeeded(bool logChange = true)
    {
        var configuration = CreateCurrentPreviewConfiguration();
        if (configuration is null)
        {
            return;
        }

        var updatedState = _previewState.UpdateForCurrentConfiguration(
            configuration,
            PreviewOutputFileExists());
        if (updatedState == _previewState)
        {
            return;
        }

        var restoredAccepted = updatedState.Status == PreviewGenerationStatus.Accepted &&
            _previewState.Status == PreviewGenerationStatus.Outdated;
        _previewState = updatedState;
        if (logChange)
        {
            if (restoredAccepted)
            {
                AddLog(
                    "Accepted preview matches the current settings again.",
                    "La vista previa aceptada vuelve a coincidir con la configuracion actual.");
            }
            else
            {
                AddLog(
                    "Preview is outdated. Regenerate it for the current settings.",
                    "La vista previa esta desactualizada. Regenerala para la configuracion actual.");
            }
        }

        RaisePreviewPropertiesChanged();
    }

    private void MarkPreviewOutdatedForCurrentSettings(bool logChange = true)
    {
        var updatedState = _previewState.MarkOutdated(
            "Preview is outdated. Regenerate it for the current settings.",
            "La vista previa esta desactualizada. Regenerala para la configuracion actual.");
        if (updatedState == _previewState)
        {
            return;
        }

        _previewState = updatedState;
        if (logChange)
        {
            AddLog(
                "Preview is outdated. Regenerate it for the current settings.",
                "La vista previa esta desactualizada. Regenerala para la configuracion actual.");
        }

        RaisePreviewPropertiesChanged();
    }

    private bool IsPreviewFingerprintCurrent()
    {
        var configuration = CreateCurrentPreviewConfiguration();
        return configuration is not null &&
            string.Equals(
                _previewState.ConfigurationFingerprint,
                configuration.Fingerprint,
                StringComparison.Ordinal);
    }

    private bool PreviewOutputFileExists() =>
        !string.IsNullOrWhiteSpace(_previewState.OutputPath) &&
        _previewCacheFileService.Exists(_previewState.OutputPath);

    private void PreviewTimeRangeChanged()
    {
        _hasUserEditedPreviewRange = true;
        ResetPreviewConversionStageForPreviewAffectingChange();
        if (CreateCurrentPreviewConfiguration() is null)
        {
            MarkPreviewOutdatedForCurrentSettings();
        }
        else
        {
            MarkPreviewOutdatedIfNeeded();
        }

        RaisePreviewPropertiesChanged();
        RaiseConversionExecutionPropertiesChanged();
        RaiseConversionReadinessPropertiesChanged();
    }

    private void SetDefaultPreviewTimeRangeFromAnalysis()
    {
        var range = PreviewTimeRangeService.CreateDefaultRange(_analysis?.File.Duration);
        var currentValidation = PreviewTimeRangeService.Validate(
            _previewFromText,
            _previewToText,
            _analysis?.File.Duration);
        if (!_hasUserEditedPreviewRange || !currentValidation.IsValid)
        {
            SetPreviewTimeRangeText(range);
            _hasUserEditedPreviewRange = false;
        }

        _previewState = _previewState.Deleted(CurrentPreviewStartTime, CurrentPreviewDuration);
        RaisePreviewPropertiesChanged();
    }

    private void SetPreviewTimeRangeText(PreviewTimeRange range)
    {
        SetProperty(
            ref _previewFromText,
            PreviewTimeRangeService.Format(range.From),
            nameof(PreviewFromText));
        SetProperty(
            ref _previewToText,
            PreviewTimeRangeService.Format(range.To),
            nameof(PreviewToText));
    }

    private string PreviewTimeRangeValidationMessage(
        PreviewTimeRangeValidationIssue issue) =>
        PreviewTimeRangeValidationMessage(issue, IsSpanish);

    private static string PreviewTimeRangeValidationMessage(
        PreviewTimeRangeValidationIssue issue,
        bool useSpanish) => issue switch
        {
            PreviewTimeRangeValidationIssue.None => string.Empty,
            PreviewTimeRangeValidationIssue.MissingSourceDuration => useSpanish
                ? "Analiza la duracion del video antes de generar una vista previa."
                : "Analyze the video duration before generating a preview.",
            PreviewTimeRangeValidationIssue.MissingValue => useSpanish
                ? "Ingresa Desde y Hasta en formato HH:MM:SS."
                : "Enter From and To in HH:MM:SS format.",
            PreviewTimeRangeValidationIssue.InvalidFormat => useSpanish
                ? "Usa el formato HH:MM:SS para Desde y Hasta."
                : "Use HH:MM:SS format for From and To.",
            PreviewTimeRangeValidationIssue.FromMustBeBeforeTo => useSpanish
                ? "Desde debe ser anterior a Hasta."
                : "From must be before To.",
            PreviewTimeRangeValidationIssue.ExceedsMaximumDuration => useSpanish
                ? "La duracion maxima de la vista previa es de 1 minuto 30 segundos."
                : "Maximum preview duration is 1 minute 30 seconds.",
            PreviewTimeRangeValidationIssue.ToBeyondSourceDuration => useSpanish
                ? "Hasta no puede superar la duracion analizada del video."
                : "To cannot exceed the analyzed video duration.",
            _ => useSpanish
                ? "El rango de vista previa no es valido."
                : "The preview time range is not valid.",
        };

    private void ApplyPreviewProgressUpdate(
        ConversionExecutionProgressUpdate progressUpdate)
    {
        if (DispatchToUiThreadIfNeeded(() => ApplyPreviewProgressUpdate(progressUpdate)))
        {
            return;
        }

        try
        {
            ApplyPreviewProgressUpdateOnUiThread(progressUpdate);
        }
        catch (Exception exception)
        {
            RecordRecoverablePreviewWarning("Preview progress update", exception);
        }
    }

    private void ApplyPreviewProgressUpdateOnUiThread(
        ConversionExecutionProgressUpdate progressUpdate)
    {
        if (_isPreviewCancellationRequested ||
            _previewState.Status == PreviewGenerationStatus.Canceled)
        {
            return;
        }

        var normalizedUpdate = progressUpdate.NormalizeProgress();
        _previewProgressPercent = PreviewProgressResolver.Resolve(
            normalizedUpdate.ProgressPercent,
            normalizedUpdate.OutputLine?.Text);
        _previewState = _previewState with
        {
            EnglishDetail = normalizedUpdate.DetailEnglish,
            SpanishDetail = normalizedUpdate.DetailSpanish,
        };
        UpdatePreviewStage(normalizedUpdate.CurrentStep);
        if (normalizedUpdate.OutputLine is not null)
        {
            var message = FormatOutputLine(normalizedUpdate.OutputLine);
            AppendPreviewLogLine(message);
        }

        AppendPreviewOfflineDependencyWarningIfNeeded(normalizedUpdate);

        if (normalizedUpdate.Metrics is not null)
        {
            UpdatePreviewMetricText(normalizedUpdate.Metrics);
        }

        RaisePreviewPropertiesChanged();
    }

    private void StartOrCancelConversion()
    {
        if (IsConversionRunning)
        {
            CancelConversion();
            return;
        }

        if (IsPreviewGenerating)
        {
            AddLog(
                "Cancel the active preview before starting final conversion.",
                "Cancela la vista previa activa antes de iniciar la conversion final.");
            return;
        }

        _ = StartConversionAsync();
    }

    private async Task StartConversionAsync()
    {
        if (IsConversionRunning || IsPreviewGenerating)
        {
            return;
        }

        if (!HasEnteredPreviewConversionStage)
        {
            AddLog(
                "Open the conversion plan and continue before starting final conversion.",
                "Abre el plan de conversion y continua antes de iniciar la conversion final.");
            return;
        }

        var startGate = EvaluateConversionStartGate();
        if (!startGate.CanStart)
        {
            BlockConversionStart(startGate);
            return;
        }

        if (_conversionPlan is null)
        {
            return;
        }

        _conversionCancellationTokenSource?.Dispose();
        _conversionCancellationTokenSource = new CancellationTokenSource();
        _completedConversionResult = null;
        _completedConversionOutputPath = string.Empty;
        ResetConversionTimingEstimate();
        IsConversionCompletedModalOpen = false;
        OnPropertyChanged(nameof(CompletedConversionOutputPath));
        OnPropertyChanged(nameof(ConversionCompletedOutputPathText));
        ConversionLogs.Clear();
        _lastConversionOutputLine = string.Empty;
        _hasLiveConversionOutput = false;
        _hasLoggedConversionOfflineDependencyWarning = false;
        ResetMetricText();
        var startedAt = DateTimeOffset.UtcNow;
        _conversionExecutionState = new(
            Status: ConversionExecutionStatus.Running,
            ProgressPercent: 0,
            CurrentStep: new(
                "Starting local iw3 conversion.",
                "Iniciando conversion local iw3."),
            DetailEnglish: "Launching bundled local iw3 process.",
            DetailSpanish: "Iniciando el proceso local iw3 incluido.",
            StartedAt: startedAt);
        RaiseConversionExecutionPropertiesChanged();
        RaiseConversionRunningModePropertiesChanged();
        AddLog(
            "Starting local iw3 conversion.",
            "Iniciando conversion local iw3.");
        AddConversionLog(
            "Starting local iw3 conversion.",
            "Iniciando conversion local iw3.");

        try
        {
            var request = _conversionExecutionRequestFactory.Create(
                _conversionPlan,
                SelectedOutputPreset,
                _planOptionState.CreatePlanOptions(_outputPathState.CustomOutputPath),
                _toolPaths,
                _conversionCancellationTokenSource.Token);
            var result = await _conversionExecutor.ExecuteAsync(
                request,
                new Progress<ConversionExecutionProgressUpdate>(ApplyConversionProgressUpdate),
                _conversionCancellationTokenSource.Token);

            foreach (var log in GetConversionResultLogsForLivePanel(result))
            {
                AddConversionLog(log.EnglishMessage, log.SpanishMessage);
            }

            AddConversionResultActivityLogs(result);
            _conversionExecutionState = CreateFinishedConversionState(result);
            ResetConversionTimingEstimate();
            if (result.Success && !result.WasCanceled)
            {
                RecordSuccessfulPerformanceHistory(
                    ConversionPerformanceOperationType.FullConversion,
                    result.StartedAt,
                    result.FinishedAt);
                ShowConversionCompletedModal(result, request.OutputPath);
            }
        }
        catch (OperationCanceledException)
        {
            _conversionExecutionState = CreateCanceledConversionState(
                startedAt,
                DateTimeOffset.UtcNow);
            AddLog(
                "Local iw3 conversion was canceled.",
                "La conversi\u00f3n local iw3 fue cancelada.");
            AddConversionLog(
                "Local iw3 conversion was canceled.",
                "La conversi\u00f3n local iw3 fue cancelada.");
        }
        catch (Exception exception)
        {
            _conversionExecutionState = CreateFailedConversionState(
                startedAt,
                DateTimeOffset.UtcNow,
                $"Local iw3 conversion failed unexpectedly: {exception.Message}",
                $"La conversion local iw3 fallo inesperadamente: {exception.Message}");
            AddLog(
                $"Local iw3 conversion failed. {exception.Message}",
                $"La conversion local iw3 fallo. {exception.Message}");
            AddConversionLog(
                $"Local iw3 conversion failed. {exception.Message}",
                $"La conversion local iw3 fallo. {exception.Message}");
        }
        finally
        {
            _conversionCancellationTokenSource?.Dispose();
            _conversionCancellationTokenSource = null;
            RaiseConversionExecutionPropertiesChanged();
            RaiseConversionRunningModePropertiesChanged();
            RaiseConversionReadinessPropertiesChanged();
        }
    }

    private void BlockConversionStart(ConversionExecutionStartGateResult startGate)
    {
        _conversionExecutionState = ConversionExecutionState.Blocked(startGate);
        RaiseConversionExecutionPropertiesChanged();
        AddLog(startGate.EnglishLogMessage, startGate.SpanishLogMessage);
    }

    private void ApplyConversionProgressUpdate(
        ConversionExecutionProgressUpdate progressUpdate)
    {
        var normalizedUpdate = progressUpdate.NormalizeProgress();
        var progressPercent = UpdateConversionTimingEstimate(normalizedUpdate);
        if (normalizedUpdate.OutputLine is not null)
        {
            AddConversionOutputLine(normalizedUpdate.OutputLine);
        }

        AddConversionOfflineDependencyWarningIfNeeded(normalizedUpdate);

        if (normalizedUpdate.Metrics is not null)
        {
            UpdateMetricText(normalizedUpdate.Metrics);
        }

        _conversionExecutionState = _conversionExecutionState with
        {
            ProgressPercent = progressPercent,
            CurrentStep = normalizedUpdate.CurrentStep,
            DetailEnglish = normalizedUpdate.DetailEnglish,
            DetailSpanish = normalizedUpdate.DetailSpanish,
        };
        RaiseConversionExecutionPropertiesChanged();
    }

    private int UpdateConversionTimingEstimate(
        ConversionExecutionProgressUpdate progressUpdate)
    {
        var now = progressUpdate.OutputLine?.CapturedAt ?? DateTimeOffset.UtcNow;
        var estimate = ConversionProgressTimingEstimator.Estimate(
            progressUpdate.OutputLine?.Text,
            progressUpdate.ProgressPercent,
            _conversionExecutionState.StartedAt,
            now);
        if (estimate is not null)
        {
            _conversionTimingEstimate = _conversionTimingSmoother.Smooth(estimate);
        }

        return progressUpdate.ProgressPercent > 0
            ? progressUpdate.ProgressPercent
            : estimate?.ProgressPercent ?? _conversionExecutionState.ProgressPercent;
    }

    private void ResetConversionTimingEstimate()
    {
        _conversionTimingSmoother.Reset();
        _conversionTimingEstimate = null;
    }

    private void AddConversionOutputLine(ProcessOutputLine outputLine)
    {
        var message = FormatOutputLine(outputLine);
        if (string.Equals(message, _lastConversionOutputLine, StringComparison.Ordinal))
        {
            return;
        }

        _lastConversionOutputLine = message;
        _hasLiveConversionOutput = true;
        AddConversionLog(message, message, outputLine.CapturedAt.LocalDateTime);
    }

    private void AddConversionOfflineDependencyWarningIfNeeded(
        ConversionExecutionProgressUpdate progressUpdate)
    {
        if (_hasLoggedConversionOfflineDependencyWarning ||
            !IsRuntimeDownloadWarning(progressUpdate))
        {
            return;
        }

        _hasLoggedConversionOfflineDependencyWarning = true;
        AddConversionLog(
            Iw3RuntimeDownloadDetector.EnglishWarning,
            Iw3RuntimeDownloadDetector.SpanishWarning);
    }

    private static string FormatOutputLine(ProcessOutputLine outputLine)
    {
        var prefix = outputLine.Stream == ProcessOutputStream.StandardError
            ? "stderr"
            : "stdout";
        return $"{prefix}: {outputLine.Text}";
    }

    private void AddConversionLog(
        string englishMessage,
        string spanishMessage,
        DateTime? timestamp = null)
    {
        ConversionLogs.Add(new LogEntryViewModel(
            timestamp ?? DateTime.Now,
            englishMessage,
            spanishMessage,
            IsSpanish));

        const int maxConversionLogEntries = 1000;
        while (ConversionLogs.Count > maxConversionLogEntries)
        {
            ConversionLogs.RemoveAt(0);
        }
    }

    private void AddConversionResultActivityLogs(ConversionExecutionResult result)
    {
        AddStaleConversionPartialCleanupActivityLogs(result);
        AddCurrentAttemptConversionPartialCleanupActivityLogs(result);
        AddLog(result.EnglishSummary, result.SpanishSummary);
        if (result.Success)
        {
            if (!string.IsNullOrWhiteSpace(result.PrimaryOutputPath))
            {
                AddLog(
                    $"Primary output was generated successfully: {result.PrimaryOutputPath}",
                    $"La salida principal se genero correctamente: {result.PrimaryOutputPath}");
            }

            if (result.CompatibilityCopySucceeded &&
                !string.IsNullOrWhiteSpace(result.CompatibilityOutputPath))
            {
                AddLog(
                    $"LG-compatible copy was generated successfully: {result.CompatibilityOutputPath}",
                    $"La copia compatible LG se genero correctamente: {result.CompatibilityOutputPath}");
            }
            else if (CreateLgCompatibilityCopy)
            {
                AddLog(
                    "LG-compatible copy was not generated. The primary output remains available.",
                    "La copia compatible LG no se genero. La salida principal sigue disponible.");
            }

            return;
        }

        if (result.WasCanceled)
        {
            return;
        }

        var detailLines = result.Logs
            .Select(log => log.EnglishMessage)
            .Where(message =>
                message.StartsWith("stderr:", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("stdout:", StringComparison.OrdinalIgnoreCase))
            .Reverse()
            .Distinct()
            .Take(5)
            .Reverse()
            .ToArray();

        foreach (var detailLine in detailLines)
        {
            AddLog(detailLine, detailLine);
        }
    }

    private void AddCurrentAttemptConversionPartialCleanupActivityLogs(ConversionExecutionResult result)
    {
        if (result.Logs.Any(log => string.Equals(
                log.EnglishMessage,
                "Conversion partial file was cleaned.",
                StringComparison.Ordinal)))
        {
            AddLog(
                "Conversion partial file was cleaned.",
                "El archivo parcial de conversi\u00f3n fue limpiado.");
        }

        if (result.Logs.Any(log => log.EnglishMessage.StartsWith(
                "Could not delete conversion partial file.",
                StringComparison.Ordinal)))
        {
            AddLog(
                "Could not delete conversion partial file.",
                "No se pudo eliminar el archivo parcial de conversi\u00f3n.");
        }
    }

    private void AddStaleConversionPartialCleanupActivityLogs(ConversionExecutionResult result)
    {
        if (result.Logs.Any(log => string.Equals(
                log.EnglishMessage,
                "Stale conversion partial file was cleaned.",
                StringComparison.Ordinal)))
        {
            AddLog(
                "Stale conversion partial file was cleaned.",
                "Se limpi\u00f3 un archivo parcial anterior de conversi\u00f3n.");
        }

        if (result.Logs.Any(log => log.EnglishMessage.StartsWith(
                "Could not delete stale partial file.",
                StringComparison.Ordinal)))
        {
            AddLog(
                "Could not delete stale partial file.",
                "No se pudo eliminar un archivo parcial anterior.");
        }
    }

    private IEnumerable<ConversionExecutionLogEntry> GetConversionResultLogsForLivePanel(
        ConversionExecutionResult result)
    {
        if (!_hasLiveConversionOutput)
        {
            return result.Logs;
        }

        return result.Logs.Where(log => !IsProcessOutputLog(log.EnglishMessage));
    }

    private static bool IsProcessOutputLog(string message) =>
        message.StartsWith("stderr:", StringComparison.OrdinalIgnoreCase) ||
        message.StartsWith("stdout:", StringComparison.OrdinalIgnoreCase);

    private void UpdateMetricText(ProcessMetricSample metrics)
    {
        _lastProcessMetricSample = metrics;
        var displayText = ProcessMetricDisplayFormatter.Format(metrics, IsSpanish);
        _cpuUsageText = displayText.Cpu;
        _ramUsageText = displayText.Ram;
        _gpuUsageText = displayText.Gpu;
        _vramUsageText = displayText.Vram;
        OnPropertyChanged(nameof(CpuUsageText));
        OnPropertyChanged(nameof(RamUsageText));
        OnPropertyChanged(nameof(GpuUsageText));
        OnPropertyChanged(nameof(VramUsageText));
    }

    private void ResetMetricText()
    {
        _lastProcessMetricSample = null;
        var displayText = ProcessMetricDisplayFormatter.Detecting(IsSpanish);
        _cpuUsageText = displayText.Cpu;
        _ramUsageText = displayText.Ram;
        _gpuUsageText = displayText.Gpu;
        _vramUsageText = displayText.Vram;
        OnPropertyChanged(nameof(CpuUsageText));
        OnPropertyChanged(nameof(RamUsageText));
        OnPropertyChanged(nameof(GpuUsageText));
        OnPropertyChanged(nameof(VramUsageText));
    }

    private void RefreshMetricLanguage()
    {
        if (_lastProcessMetricSample is null)
        {
            ResetMetricText();
        }
        else
        {
            UpdateMetricText(_lastProcessMetricSample);
        }

        if (_lastPreviewMetricSample is null)
        {
            ResetPreviewMetricText();
        }
        else
        {
            UpdatePreviewMetricText(_lastPreviewMetricSample);
        }
    }

    private void UpdatePreviewStage(ConversionExecutionStep step)
    {
        if (step.EnglishText.Contains("source clip", StringComparison.OrdinalIgnoreCase))
        {
            _previewStageEnglishText = "FFmpeg source clip";
            _previewStageSpanishText = "clip fuente FFmpeg";
        }
        else if (step.EnglishText.Contains("iw3", StringComparison.OrdinalIgnoreCase))
        {
            _previewStageEnglishText = "Bundled Python/iw3";
            _previewStageSpanishText = "Python/iw3 incluido";
        }
        else if (step.EnglishText.Contains("completed", StringComparison.OrdinalIgnoreCase))
        {
            _previewStageEnglishText = "Preview completed";
            _previewStageSpanishText = "Vista previa completada";
        }
        else
        {
            _previewStageEnglishText = "Preparing preview";
            _previewStageSpanishText = "Preparando vista previa";
        }

        OnPropertyChanged(nameof(PreviewStageText));
    }

    private void UpdatePreviewMetricText(ProcessMetricSample metrics)
    {
        _lastPreviewMetricSample = metrics;
        _previewCpuUsageText = metrics.CpuUsagePercent is null
            ? Text("CPU: Detecting...", "CPU: Detectando...")
            : $"CPU: {metrics.CpuUsagePercent.Value:0.0}%";
        _previewRamUsageText = FormatPreviewMemory(metrics.PrivateMemoryBytes ?? metrics.WorkingSetBytes);
        _previewGpuUsageText = FormatPreviewGpu(metrics);
        _previewVramUsageText = FormatPreviewVram(metrics);
        _previewGpuMetricsStatusText = FormatPreviewGpuMetricsStatus(metrics);
        RaisePreviewMetricPropertiesChanged();
    }

    private void ResetPreviewMetricText()
    {
        _lastPreviewMetricSample = null;
        _previewCpuUsageText = Text("CPU: Detecting...", "CPU: Detectando...");
        _previewRamUsageText = Text("RAM: Detecting...", "RAM: Detectando...");
        _previewGpuUsageText = Text("GPU: Detecting...", "GPU: Detectando...");
        _previewVramUsageText = Text("VRAM: Detecting...", "VRAM: Detectando...");
        _previewGpuMetricsStatusText = Text("GPU metrics: Detecting...", "Metricas GPU: Detectando...");
        _previewStageEnglishText = "Preparing preview";
        _previewStageSpanishText = "Preparando vista previa";
        RaisePreviewMetricPropertiesChanged();
        OnPropertyChanged(nameof(PreviewStageText));
    }

    private void RaisePreviewMetricPropertiesChanged()
    {
        OnPropertyChanged(nameof(PreviewCpuUsageText));
        OnPropertyChanged(nameof(PreviewRamUsageText));
        OnPropertyChanged(nameof(PreviewGpuUsageText));
        OnPropertyChanged(nameof(PreviewVramUsageText));
        OnPropertyChanged(nameof(PreviewGpuMetricsStatusText));
    }

    private string FormatPreviewMemory(long? bytes) =>
        bytes is null
            ? Text("RAM: Detecting...", "RAM: Detectando...")
            : $"RAM: {FormatMetricBytes(bytes.Value)}";

    private string FormatPreviewGpu(ProcessMetricSample metrics)
    {
        if (metrics.GpuUsagePercent is { } gpuUsagePercent)
        {
            var label = metrics.GpuScope == ProcessGpuMetricScope.Adapter
                ? "GPU global"
                : "GPU";
            return $"{label}: {gpuUsagePercent:0.0}%";
        }

        return string.IsNullOrWhiteSpace(metrics.GpuStatus)
            ? Text("GPU: Detecting...", "GPU: Detectando...")
            : Text(
                $"GPU: Unavailable ({metrics.GpuStatus})",
                $"GPU: No disponible ({LocalizePreviewGpuStatus(metrics.GpuStatus)})");
    }

    private string FormatPreviewVram(ProcessMetricSample metrics)
    {
        if (metrics.GpuDedicatedMemoryBytes is not { } bytes)
        {
            return Text("VRAM: Detecting...", "VRAM: Detectando...");
        }

        var label = metrics.GpuScope == ProcessGpuMetricScope.Adapter
            ? "VRAM global"
            : "VRAM";
        return $"{label}: {FormatMetricBytes(bytes)}";
    }

    private string FormatPreviewGpuMetricsStatus(ProcessMetricSample metrics)
    {
        if (metrics.GpuUsagePercent is not null)
        {
            return metrics.GpuScope == ProcessGpuMetricScope.Adapter
                ? Text(
                    "GPU metrics: Windows adapter/global counters",
                    "Metricas GPU: contadores globales del adaptador Windows")
                : Text(
                    "GPU metrics: process counters",
                    "Metricas GPU: contadores del proceso");
        }

        return string.IsNullOrWhiteSpace(metrics.GpuStatus)
            ? Text("GPU metrics: Detecting...", "Metricas GPU: Detectando...")
            : Text(
                $"GPU metrics: Unavailable ({metrics.GpuStatus})",
                $"Metricas GPU: No disponible ({LocalizePreviewGpuStatus(metrics.GpuStatus)})");
    }

    private string LocalizePreviewGpuStatus(string status) => status switch
    {
        ProcessGpuMetricReading.DetectingStatus => "Detectando...",
        ProcessGpuMetricReading.NoProcessGpuEngineCounterStatus =>
            "No se encontro contador GPU Engine para este proceso",
        ProcessGpuMetricReading.PermissionUnavailableStatus => "Permiso no disponible",
        ProcessGpuMetricReading.WindowsMetricsUnavailableStatus =>
            "Metricas no disponibles en esta version/controlador de Windows",
        ProcessGpuMetricReading.NvidiaMetricsUnavailableStatus =>
            "Metricas NVIDIA no disponibles",
        ProcessGpuMetricReading.AdapterGpuUsageStatus =>
            "Uso global del adaptador GPU",
        ProcessGpuMetricReading.ProcessGpuEngineCounterStatus =>
            "Uso GPU del proceso",
        ProcessGpuMetricReading.NvidiaAdapterMetricsStatus =>
            "Metricas globales del adaptador NVIDIA",
        _ => status,
    };

    private static string FormatMetricBytes(long bytes)
    {
        const double gibibyte = 1024 * 1024 * 1024;
        const double mebibyte = 1024 * 1024;

        return bytes >= gibibyte
            ? $"{bytes / gibibyte:0.00} GB"
            : $"{bytes / mebibyte:0.0} MB";
    }

    private TimeSpan GetCurrentConversionElapsed()
    {
        if (_conversionTimingEstimate is not null)
        {
            return _conversionTimingEstimate.Elapsed;
        }

        return _conversionExecutionState.StartedAt is { } startedAt
            ? DateTimeOffset.UtcNow - startedAt
            : TimeSpan.Zero;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        var rounded = TimeSpan.FromSeconds(Math.Round(duration.TotalSeconds));
        return rounded.TotalHours >= 1
            ? $"{(int)rounded.TotalHours}:{rounded.Minutes:00}:{rounded.Seconds:00}"
            : $"{(int)rounded.TotalMinutes:00}:{rounded.Seconds:00}";
    }

    private void HandleOpenOutputWhenFinished(
        ConversionExecutionResult result,
        string finalOutputPath)
    {
        var openResult = _conversionOutputOpenService.OpenAfterSuccessfulConversion(
            result,
            finalOutputPath,
            OpenOutputWhenFinished);

        if (openResult.EnglishWarning is not null &&
            openResult.SpanishWarning is not null)
        {
            AddLog(openResult.EnglishWarning, openResult.SpanishWarning);
            AddConversionLog(openResult.EnglishWarning, openResult.SpanishWarning);
        }
    }

    private void ShowConversionCompletedModal(
        ConversionExecutionResult result,
        string finalOutputPath)
    {
        _completedConversionResult = result;
        _completedConversionOutputPath =
            result.PreferredOpenOutputPath ??
            result.PrimaryOutputPath ??
            finalOutputPath;
        OnPropertyChanged(nameof(CompletedConversionOutputPath));
        OnPropertyChanged(nameof(ConversionCompletedOutputPathText));
        IsConversionCompletedModalOpen = true;
    }

    private void AcceptConversionCompleted()
    {
        var result = _completedConversionResult;
        var finalOutputPath = CompletedConversionOutputPath;
        IsConversionCompletedModalOpen = false;
        _completedConversionResult = null;
        _completedConversionOutputPath = string.Empty;
        OnPropertyChanged(nameof(CompletedConversionOutputPath));
        OnPropertyChanged(nameof(ConversionCompletedOutputPathText));

        if (result is not null)
        {
            HandleOpenOutputWhenFinished(result, finalOutputPath);
        }

        ResetWorkflowAfterSuccessfulConversion();
    }

    private void ResetWorkflowAfterSuccessfulConversion()
    {
        SelectedVideoPath = null;
        HasCompletedAnalysis = false;
        _analysis = null;
        _conversionRecommendation = null;
        _conversionPlan = null;
        _conversionReadiness = null;
        SetCanOpenConversionPlanStep(false);
        _outputPathState.ClearCustomOutputPath();
        OnPropertyChanged(nameof(HasCustomOutputPath));
        SetOutputPathText(string.Empty);
        SetPreviewTimeRangeText(PreviewTimeRangeService.CreateDefaultRange(null));
        _previewState = PreviewWorkflowState.NotGenerated(
            TimeSpan.Zero,
            PreviewTimeRangeService.DefaultDuration);
        _lastPreviewCachePaths = null;
        _hasUserEditedPreviewRange = false;
        _previewProgressPercent = 0;
        _hasLiveConversionOutput = false;
        _lastConversionOutputLine = string.Empty;
        ResetConversionTimingEstimate();
        _conversionExecutionState = ConversionExecutionState.NotStarted();
        SelectedWizardStepIndex = ConversionWorkflowState.SourceAndAnalysisStepIndex;
        UpdateConversionReadiness();
        RaiseAnalysisPropertiesChanged();
        RaiseRecommendationPropertiesChanged();
        RaiseConversionPlanPropertiesChanged();
        RaisePreviewPropertiesChanged();
        RaiseConversionExecutionPropertiesChanged();
        RaiseConversionRunningModePropertiesChanged();
        RaiseConversionReadinessPropertiesChanged();
    }

    private static ConversionExecutionState CreateFinishedConversionState(
        ConversionExecutionResult result)
    {
        if (result.WasCanceled)
        {
            return CreateCanceledConversionState(result.StartedAt, result.FinishedAt);
        }

        return result.Success
            ? new(
                Status: ConversionExecutionStatus.Completed,
                ProgressPercent: 100,
                CurrentStep: new(
                    "Local iw3 conversion completed.",
                    "La conversion local iw3 se completo."),
                DetailEnglish: result.EnglishSummary,
                DetailSpanish: result.SpanishSummary,
                StartedAt: result.StartedAt,
                FinishedAt: result.FinishedAt)
            : CreateFailedConversionState(
                result.StartedAt,
                result.FinishedAt,
                result.EnglishSummary,
                result.SpanishSummary);
    }

    private static ConversionExecutionState CreateCanceledConversionState(
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt) => new(
        Status: ConversionExecutionStatus.Canceled,
        ProgressPercent: 0,
        CurrentStep: new(
            "Local iw3 conversion was canceled.",
            "La conversi\u00f3n local iw3 fue cancelada."),
        DetailEnglish: "Local iw3 conversion was canceled.",
        DetailSpanish: "La conversi\u00f3n local iw3 fue cancelada.",
        StartedAt: startedAt,
        FinishedAt: finishedAt);

    private static ConversionExecutionState CreateFailedConversionState(
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        string englishSummary,
        string spanishSummary) => new(
        Status: ConversionExecutionStatus.Failed,
        ProgressPercent: 0,
        CurrentStep: new(
            "Local iw3 conversion failed.",
            "La conversion local iw3 fallo."),
        DetailEnglish: englishSummary,
        DetailSpanish: spanishSummary,
        StartedAt: startedAt,
        FinishedAt: finishedAt);

    private void CancelConversion()
    {
        if (!CanCancelConversion)
        {
            AddLog(
                "There is no active conversion to cancel.",
                "No hay una conversión activa para cancelar.");
            return;
        }

        _conversionExecutionState = _conversionExecutionState with
        {
            Status = ConversionExecutionStatus.Canceling,
            CurrentStep = new(
                "Canceling local iw3 conversion.",
                "Cancelando conversion local iw3."),
            DetailEnglish = "A cancellation request was sent to the local process.",
            DetailSpanish = "Se envio una solicitud de cancelacion al proceso local.",
        };
        RaiseConversionExecutionPropertiesChanged();
        RaiseConversionRunningModePropertiesChanged();
        _conversionCancellationTokenSource?.Cancel();
        AddLog(
            "Canceling local iw3 conversion.",
            "Cancelando conversion local iw3.");
        AddConversionLog(
            "Canceling local iw3 conversion.",
            "Cancelando conversion local iw3.");
    }

    private static bool IsSupportedVideoFile(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        SupportedVideoExtensions.Contains(Path.GetExtension(path));

    private static bool IsSupportedImageFile(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        SupportedImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    private static string FormatImageFileSize(long bytes)
    {
        const double mib = 1024d * 1024d;
        const double kib = 1024d;
        if (bytes >= mib)
        {
            return $"{bytes / mib:0.0} MB";
        }

        return bytes >= kib ? $"{bytes / kib:0} KB" : $"{bytes} B";
    }

    private static string FormatAspectRatio(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return "-";
        }

        var divisor = GreatestCommonDivisor(width, height);
        return $"{width / divisor}:{height / divisor}";
    }

    private static bool IsImageNearlySquare(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var largerSide = Math.Max(width, height);
        var sideDelta = Math.Abs(width - height);
        return sideDelta <= largerSide * 0.03d;
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        left = Math.Abs(left);
        right = Math.Abs(right);
        while (right != 0)
        {
            var remainder = left % right;
            left = right;
            right = remainder;
        }

        return Math.Max(1, left);
    }

    private void PlanOptionChanged(
        string englishMessage,
        string spanishMessage,
        bool affectsPreview = true)
    {
        ResetConversionExecutionState();
        if (affectsPreview)
        {
            ResetPreviewConversionStageForPreviewAffectingChange();
        }

        if (RegenerateConversionPlan())
        {
            if (affectsPreview)
            {
                MarkPreviewOutdatedIfNeeded();
            }

            AddLog(englishMessage, spanishMessage);
        }
    }

    private void CommitOutputPath(string value)
    {
        if (!_outputPathState.CommitOutputPathText(value, out var normalizedPath))
        {
            return;
        }

        OnPropertyChanged(nameof(HasCustomOutputPath));
        ResetConversionExecutionState();

        if (RegenerateConversionPlan())
        {
            if (normalizedPath is null)
            {
                AddLog(
                    "Output path reset to automatic suggestion.",
                    "Ruta de salida restablecida a la sugerencia automática.");
            }
            else
            {
                AddLog(
                    $"Output path changed to {normalizedPath}.",
                    $"Ruta de salida cambiada a {normalizedPath}.");
            }
        }
    }

    private void BrowseOutputFolder()
    {
        if (IsConversionRunning || IsPreviewGenerating)
        {
            return;
        }

        var automaticPath = GetAutomaticOutputPath();
        if (automaticPath is null)
        {
            AddLog(
                "Select a video before choosing an output folder.",
                "Selecciona un video antes de elegir una carpeta de salida.");
            return;
        }

        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = Text("Choose output folder", "Elige la carpeta de salida"),
            UseDescriptionForTitle = true,
            SelectedPath = GetInitialOutputDirectory() ?? string.Empty,
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK ||
            string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        var outputPath = Path.Combine(dialog.SelectedPath, Path.GetFileName(automaticPath));
        SetCustomOutputPath(outputPath, logChange: true);
    }

    private void ResetOutputPath()
    {
        if (IsConversionRunning || IsPreviewGenerating)
        {
            return;
        }

        if (!_outputPathState.ResetCustomOutputPath())
        {
            return;
        }

        OnPropertyChanged(nameof(HasCustomOutputPath));
        ResetConversionExecutionState();

        if (RegenerateConversionPlan())
        {
            AddLog(
                "Output path reset to automatic suggestion.",
                "Ruta de salida restablecida a la sugerencia automática.");
        }
        else
        {
            SetOutputPathText(GetAutomaticOutputPath() ?? string.Empty);
        }
    }

    private void SetCustomOutputPath(string outputPath, bool logChange)
    {
        if (!_outputPathState.SetCustomOutputPath(outputPath))
        {
            return;
        }

        OnPropertyChanged(nameof(HasCustomOutputPath));
        SetOutputPathText(outputPath);
        ResetConversionExecutionState();

        if (RegenerateConversionPlan() && logChange)
        {
            AddLog(
                $"Output path changed to {outputPath}.",
                $"Ruta de salida cambiada a {outputPath}.");
        }
    }

    private string? GetAutomaticOutputPath()
    {
        var inputPath = _analysis?.InputPath ?? SelectedVideoPath;
        return string.IsNullOrWhiteSpace(inputPath)
            ? null
            : VideoConversionPlanService.CreateSuggestedOutputPath(
                inputPath,
                SelectedOutputContainer,
                SelectedThreeDOutputFormat,
                SelectedLocalModelCandidate);
    }

    private string? GetLgCompatibilityCopyPath()
    {
        if (!CreateLgCompatibilityCopy || _conversionPlan is null)
        {
            return null;
        }

        return LgCompatibilityCopyRequestBuilder.CreateCompatibilityOutputPath(
            _conversionPlan.SuggestedOutputPath,
            _conversionPlan.ThreeDOutputFormat);
    }

    private string? GetInitialOutputDirectory()
    {
        return _outputPathState.GetInitialOutputDirectory(GetAutomaticOutputPath());
    }

    private void SetOutputPathText(string value)
    {
        _isUpdatingOutputPathText = true;
        try
        {
            SetProperty(ref _outputPathText, value, nameof(OutputPathText));
        }
        finally
        {
            _isUpdatingOutputPathText = false;
        }
    }

    private void ApplyRecommendationDefaultsIfNeeded(
        VideoConversionSetupRecommendation recommendation)
    {
        if (_planOptionState.ApplyRecommendationDefaultsIfNeeded(recommendation))
        {
            RaisePlanOptionSelectionPropertiesChanged();
        }
    }

    private void ApplyPresetDefaults(TargetDevicePreset preset)
    {
        ResetConversionExecutionState();
        if (_planOptionState.ApplyPresetDefaults(preset))
        {
            RaisePlanOptionSelectionPropertiesChanged();
        }
    }

    private void RegenerateRecommendationAndPlan()
    {
        if (_analysis is null)
        {
            return;
        }

        _conversionRecommendation = _recommendationService.Recommend(_analysis, SelectedOutputPreset);
        RegenerateConversionPlan();
        MarkPreviewOutdatedIfNeeded();
        RaiseRecommendationPropertiesChanged();
    }

    private bool RegenerateConversionPlan()
    {
        if (_analysis is null || _conversionRecommendation is null)
        {
            return false;
        }

        _conversionPlan = _conversionPlanService.Create(
            _analysis,
            _conversionRecommendation,
            SelectedOutputPreset,
            _planOptionState.CreatePlanOptions(_outputPathState.CustomOutputPath),
            _toolPaths,
            _toolHealth ?? _healthChecker.Check(_toolPaths),
            SelectedLocalModelCandidate);
        SetCanOpenConversionPlanStep(true);
        RaiseConversionPlanPropertiesChanged();
        SetOutputPathText(_conversionPlan.SuggestedOutputPath);
        RaiseConversionReadinessPropertiesChanged();
        return true;
    }

    private void ResetConversionExecutionState()
    {
        if (IsConversionRunning)
        {
            return;
        }

        _conversionExecutionState = ConversionExecutionState.NotStarted();
        RaiseConversionExecutionPropertiesChanged();
    }

    private static string QualityPresetText(AiQualityPreset value, bool useSpanish) => value switch
    {
        AiQualityPreset.Fast => useSpanish ? "Rápida" : "Fast",
        AiQualityPreset.Balanced => useSpanish ? "Equilibrada" : "Balanced",
        AiQualityPreset.HighQuality => useSpanish ? "Alta calidad" : "High quality",
        _ => value.ToString(),
    };

    private static string ThreeDIntensityText(ThreeDIntensity value, bool useSpanish) => value switch
    {
        ThreeDIntensity.Low => useSpanish ? "Baja" : "Low",
        ThreeDIntensity.Medium => useSpanish ? "Media" : "Medium",
        ThreeDIntensity.High => useSpanish ? "Alta" : "High",
        ThreeDIntensity.Custom => useSpanish ? "Personalizada" : "Custom",
        _ => value.ToString(),
    };

    private static string ThreeDOutputFormatText(ThreeDOutputFormat value, bool useSpanish) => value switch
    {
        ThreeDOutputFormat.HalfTopBottom => useSpanish ? "Medio arriba-abajo" : "Half Top-Bottom",
        ThreeDOutputFormat.HalfSideBySide => useSpanish ? "Medio lado a lado" : "Half Side-by-Side",
        ThreeDOutputFormat.FullSideBySide => useSpanish ? "Completo lado a lado" : "Full Side-by-Side",
        ThreeDOutputFormat.Anaglyph => useSpanish ? "Anaglifo" : "Anaglyph",
        _ => value.ToString(),
    };

    private static string GetSpanishModelDisplayName(LocalModelPlanSelection selectedModel) =>
        string.IsNullOrWhiteSpace(selectedModel.SpanishDisplayName)
            ? selectedModel.DisplayName
            : selectedModel.SpanishDisplayName;

    private void ApplyUiOnlyRefresh(Action refresh)
    {
        _isApplyingUiOnlyRefresh = true;
        try
        {
            refresh();
        }
        finally
        {
            _isApplyingUiOnlyRefresh = false;
        }
    }

    private string Text(string english, string spanish) => IsSpanish ? spanish : english;

    private string LabelValue(string englishLabel, string spanishLabel, string? value) =>
        $"{Text(englishLabel, spanishLabel)}: {value ?? "-"}";

    private void AddLog(string englishMessage, string spanishMessage)
    {
        Logs.Add(new LogEntryViewModel(
            timestamp: DateTime.Now,
            englishMessage,
            spanishMessage,
            isSpanish: IsSpanish));
        OnPropertyChanged(nameof(ActivityLogPanelText));
        if (IsActivityLogModalOpen)
        {
            ActivityLogModalText = CreateFullActivityLogText();
        }
    }

    private void AddImageLog(string englishMessage, string spanishMessage)
    {
        ImageLogs.Add(new LogEntryViewModel(
            timestamp: DateTime.Now,
            englishMessage,
            spanishMessage,
            isSpanish: IsSpanish));
        OnPropertyChanged(nameof(ImageActivityLogText));
        if (IsActivityLogModalOpen && _activeActivityLogModalKind == ActivityLogModalKind.Image)
        {
            ActivityLogModalText = CreateFullImageActivityLogText();
        }

        ClearImageLogCommand.RaiseCanExecuteChanged();
    }

    private void LoadPerformanceHistory()
    {
        var result = _performanceHistoryStore.Load();
        _performanceHistory = result.Records;
        if (!string.IsNullOrWhiteSpace(result.Warning))
        {
            AddLog(
                result.Warning,
                $"El historial de rendimiento no se pudo cargar y se ignorara: {result.Warning}");
        }
    }

    private void RecordSuccessfulPerformanceHistory(
        ConversionPerformanceOperationType operationType,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        TimeSpan? operationDuration = null)
    {
        var input = CreateCurrentConversionEstimateInput();
        if (input is null)
        {
            return;
        }

        var elapsed = finishedAt - startedAt;
        var record = ConversionPerformanceHistory.CreateSuccessfulRecord(
            operationType,
            input,
            elapsed,
            GetAppVersion(),
            operationDuration);
        if (record is null)
        {
            return;
        }

        var updatedRecords = ConversionPerformanceHistory.AddBounded(
            _performanceHistory,
            record);
        var saveResult = _performanceHistoryStore.Save(updatedRecords);
        if (!saveResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(saveResult.Warning))
            {
                AddLog(
                    saveResult.Warning,
                    $"El historial de rendimiento no se pudo guardar: {saveResult.Warning}");
            }

            return;
        }

        _performanceHistory = updatedRecords;
        RaisePreflightEstimatePropertiesChanged();
    }

    private static string GetAppVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    private void ResetPreviewGenerationLog()
    {
        if (DispatchToUiThreadIfNeeded(ResetPreviewGenerationLog))
        {
            return;
        }

        PreviewGenerationLogs.Clear();
        _previewGenerationLogTextBuilder.Clear();
        OnPropertyChanged(nameof(PreviewGenerationLogText));
    }

    private void AppendPreviewLogLine(string message)
    {
        if (DispatchToUiThreadIfNeeded(() => AppendPreviewLogLine(message)))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        PreviewGenerationLogs.Add(message);
        if (_previewGenerationLogTextBuilder.Length > 0)
        {
            _previewGenerationLogTextBuilder.AppendLine();
        }

        _previewGenerationLogTextBuilder.Append(message);
    }

    private void ShowLogCopySuccessNotification()
    {
        ShowLogCopyNotification("Log copied", "Log copiado");
    }

    private void ShowLogCopyFailureNotification()
    {
        ShowLogCopyNotification("Could not copy log", "No se pudo copiar el log");
    }

    private void ShowLogCopyNotification(string englishText, string spanishText)
    {
        ShowLogCopyNotification(
            englishText,
            spanishText,
            LogCopyNotificationDuration,
            TopToastKind.LogCopy);
    }

    private void ShowLogCopyNotification(
        string englishText,
        string spanishText,
        TimeSpan duration,
        TopToastKind toastKind)
    {
        if (DispatchToUiThreadIfNeeded(() => ShowLogCopyNotification(englishText, spanishText, duration, toastKind)))
        {
            return;
        }

        _logCopyNotificationEnglishText = englishText;
        _logCopyNotificationSpanishText = spanishText;
        _activeTopToastKind = toastKind;
        _isLogCopyNotificationVisible = true;
        OnPropertyChanged(nameof(LogCopyNotificationText));
        OnPropertyChanged(nameof(LogCopyNotificationVisibility));

        _logCopyNotificationCancellationTokenSource?.Cancel();
        _logCopyNotificationCancellationTokenSource?.Dispose();
        var cancellationTokenSource = new CancellationTokenSource();
        _logCopyNotificationCancellationTokenSource = cancellationTokenSource;
        _ = HideLogCopyNotificationAfterDelayAsync(cancellationTokenSource, duration);
    }

    private async Task HideLogCopyNotificationAfterDelayAsync(
        CancellationTokenSource cancellationTokenSource,
        TimeSpan duration)
    {
        try
        {
            await Task.Delay(duration, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (DispatchToUiThreadIfNeeded(() => HideLogCopyNotification(cancellationTokenSource)))
        {
            return;
        }

        HideLogCopyNotification(cancellationTokenSource);
    }

    private void HideLogCopyNotification(TopToastKind toastKind)
    {
        if (_activeTopToastKind != toastKind)
        {
            return;
        }

        _logCopyNotificationCancellationTokenSource?.Cancel();
        _logCopyNotificationCancellationTokenSource?.Dispose();
        _logCopyNotificationCancellationTokenSource = null;
        _activeTopToastKind = TopToastKind.None;
        _isLogCopyNotificationVisible = false;
        OnPropertyChanged(nameof(LogCopyNotificationVisibility));
    }

    private void HideLogCopyNotification(CancellationTokenSource cancellationTokenSource)
    {
        if (!ReferenceEquals(_logCopyNotificationCancellationTokenSource, cancellationTokenSource))
        {
            return;
        }

        _isLogCopyNotificationVisible = false;
        _activeTopToastKind = TopToastKind.None;
        _logCopyNotificationCancellationTokenSource = null;
        cancellationTokenSource.Dispose();
        OnPropertyChanged(nameof(LogCopyNotificationVisibility));
    }

    private void AppendPreviewResultLog(ConversionExecutionLogEntry log)
    {
        if (string.Equals(
                log.EnglishMessage,
                Iw3RuntimeDownloadDetector.EnglishWarning,
                StringComparison.Ordinal) &&
            _hasLoggedPreviewOfflineDependencyWarning)
        {
            return;
        }

        AppendPreviewLogLine(Text(log.EnglishMessage, log.SpanishMessage));
        if (string.Equals(
                log.EnglishMessage,
                Iw3RuntimeDownloadDetector.EnglishWarning,
                StringComparison.Ordinal))
        {
            _hasLoggedPreviewOfflineDependencyWarning = true;
        }
    }

    private void AppendPreviewOfflineDependencyWarningIfNeeded(
        ConversionExecutionProgressUpdate progressUpdate)
    {
        if (_hasLoggedPreviewOfflineDependencyWarning ||
            !IsRuntimeDownloadWarning(progressUpdate))
        {
            return;
        }

        _hasLoggedPreviewOfflineDependencyWarning = true;
        AppendPreviewLogLine(Text(
            Iw3RuntimeDownloadDetector.EnglishWarning,
            Iw3RuntimeDownloadDetector.SpanishWarning));
    }

    private static bool IsRuntimeDownloadWarning(
        ConversionExecutionProgressUpdate progressUpdate) =>
        string.Equals(
            progressUpdate.DetailEnglish,
            Iw3RuntimeDownloadDetector.EnglishWarning,
            StringComparison.Ordinal);

    private bool DispatchToUiThreadIfNeeded(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            return false;
        }

        try
        {
            dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    AppErrorLogService.LogRecoverableException("Preview UI dispatch", exception);
                }
            }));
        }
        catch (Exception exception)
        {
            AppErrorLogService.LogRecoverableException("Preview UI dispatch", exception);
        }

        return true;
    }

    private void RecordRecoverablePreviewWarning(string operation, Exception exception)
    {
        var errorLogPath = AppErrorLogService.LogRecoverableException(operation, exception);
        AddLog(
            $"{operation} warning. Details were written to {errorLogPath}. {exception.Message}",
            $"Advertencia de {operation}. Los detalles se escribieron en {errorLogPath}. {exception.Message}");
    }

    private string CreateFullActivityLogText() =>
        string.Join(Environment.NewLine, Logs.Select(log => log.DisplayText));

    private string CreateFullImageActivityLogText() =>
        ImageLogs.Count == 0
            ? ImageScaffoldLogText
            : string.Join(Environment.NewLine, ImageLogs.Select(log => log.DisplayText));

    private void UpdateLogLanguages()
    {
        foreach (var log in Logs)
        {
            log.SetLanguage(IsSpanish);
        }

        foreach (var log in ConversionLogs)
        {
            log.SetLanguage(IsSpanish);
        }

        foreach (var log in ImageLogs)
        {
            log.SetLanguage(IsSpanish);
        }

        OnPropertyChanged(nameof(ActivityLogPanelText));
        OnPropertyChanged(nameof(ImageActivityLogText));
        if (IsActivityLogModalOpen)
        {
            ActivityLogModalText = _activeActivityLogModalKind == ActivityLogModalKind.Image
                ? CreateFullImageActivityLogText()
                : CreateFullActivityLogText();
        }
    }

    private void UpdatePlanOptionLanguages()
    {
        foreach (var option in OutputPresetOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in OutputContainerOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in QualityPresetOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in ThreeDIntensityOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in ThreeDOutputFormatOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in StereoOutputFormatOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in SettingsSectionOptions)
        {
            option.SetLanguage(IsSpanish);
        }
    }

    private void RaiseSidebarPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsSidebarExpanded));
        OnPropertyChanged(nameof(IsSidebarPinnedExpanded));
        OnPropertyChanged(nameof(IsSidebarHoverExpanded));
        OnPropertyChanged(nameof(IsSidebarEffectivelyExpanded));
        OnPropertyChanged(nameof(SidebarExpandedWidth));
        OnPropertyChanged(nameof(SidebarCollapsedWidth));
        OnPropertyChanged(nameof(SidebarTargetWidth));
        OnPropertyChanged(nameof(SidebarColumnWidth));
        OnPropertyChanged(nameof(SidebarPadding));
        OnPropertyChanged(nameof(SidebarExpandedContentVisibility));
        OnPropertyChanged(nameof(SidebarNavContentHorizontalAlignment));
        OnPropertyChanged(nameof(SidebarToggleGlyphText));
        OnPropertyChanged(nameof(SidebarToggleText));
        OnPropertyChanged(nameof(SidebarToggleToolTipText));
    }

    private void RaiseImageConversionPropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedImagePath));
        OnPropertyChanged(nameof(SelectedImageDisplayPath));
        OnPropertyChanged(nameof(SelectedImageFileName));
        OnPropertyChanged(nameof(HasSelectedImage));
        OnPropertyChanged(nameof(HasImageMetadata));
        OnPropertyChanged(nameof(CanAnalyzeImage));
        OnPropertyChanged(nameof(HasSelectedImageMode));
        OnPropertyChanged(nameof(HasImageWorkflowPrerequisites));
        OnPropertyChanged(nameof(IsImageWorkflowChooserExpanded));
        OnPropertyChanged(nameof(ImageWorkflowSummaryVisibility));
        OnPropertyChanged(nameof(ImageWorkflowChooserVisibility));
        OnPropertyChanged(nameof(ImageWorkflowChooserChevronText));
        OnPropertyChanged(nameof(ImageWorkflowSummaryText));
        OnPropertyChanged(nameof(CanOpenImageSetupStep));
        OnPropertyChanged(nameof(IsImageModeSetupValid));
        OnPropertyChanged(nameof(IsImageSetupValid));
        OnPropertyChanged(nameof(CanOpenImagePreviewExportStep));
        OnPropertyChanged(nameof(CanMoveImageWizardBack));
        OnPropertyChanged(nameof(CanMoveImageWizardNext));
        OnPropertyChanged(nameof(ImageWizardBackButtonVisibility));
        OnPropertyChanged(nameof(ImageWizardNextButtonVisibility));
        OnPropertyChanged(nameof(ContinueWithImageConversionFooterVisibility));
        OnPropertyChanged(nameof(CanContinueWithImageConversion));
        OnPropertyChanged(nameof(ImageWizardNextToolTipText));
        OnPropertyChanged(nameof(ImagePreviewExportStatusCardVisibility));
        OnPropertyChanged(nameof(ImageAnalysisResultsVisibility));
        OnPropertyChanged(nameof(ImageSummaryVisibility));
        OnPropertyChanged(nameof(ImageSummaryRowHeight));
        OnPropertyChanged(nameof(ImagePreviewExportStatusRowHeight));
        OnPropertyChanged(nameof(ImageActivityLogCardMargin));
        OnPropertyChanged(nameof(ImagePreviewExportStatusText));
        OnPropertyChanged(nameof(IsSelectedImageVertical));
        OnPropertyChanged(nameof(IsSelectedImageHorizontalOrSquare));
        OnPropertyChanged(nameof(ImageParallaxPreviewExportVerticalLayoutVisibility));
        OnPropertyChanged(nameof(ImageParallaxPreviewExportWideLayoutVisibility));
        OnPropertyChanged(nameof(ImageStereoPreviewExportVerticalLayoutVisibility));
        OnPropertyChanged(nameof(ImageStereoPreviewExportWideLayoutVisibility));
        OnPropertyChanged(nameof(ImageMetadataWidthText));
        OnPropertyChanged(nameof(ImageMetadataHeightText));
        OnPropertyChanged(nameof(ImageMetadataAspectRatioText));
        OnPropertyChanged(nameof(ImageMetadataFormatText));
        OnPropertyChanged(nameof(ImageMetadataPixelFormatText));
        OnPropertyChanged(nameof(ImageMetadataFileSizeText));
        OnPropertyChanged(nameof(ImageMetadataSummaryText));
        OnPropertyChanged(nameof(ImageSourceStatusText));
        OnPropertyChanged(nameof(ImageOutputPlanText));
        OnPropertyChanged(nameof(ImageSetupSummaryText));
        OnPropertyChanged(nameof(SelectedImageConversionMode));
        OnPropertyChanged(nameof(SelectedImageConversionStep));
        OnPropertyChanged(nameof(IsImageParallaxModeSelected));
        OnPropertyChanged(nameof(IsImageStereoModeSelected));
        OnPropertyChanged(nameof(ImageParallaxModeSelectionState));
        OnPropertyChanged(nameof(ImageStereoModeSelectionState));
        OnPropertyChanged(nameof(ImageParallaxModeCardStatusText));
        OnPropertyChanged(nameof(ImageStereoModeCardStatusText));
        OnPropertyChanged(nameof(ImageModeSourceStepState));
        OnPropertyChanged(nameof(ImageSetupStepState));
        OnPropertyChanged(nameof(ImagePreviewExportStepState));
        OnPropertyChanged(nameof(ImageModeSourceStepMarkerText));
        OnPropertyChanged(nameof(ImageSetupStepMarkerText));
        OnPropertyChanged(nameof(ImagePreviewExportStepMarkerText));
        OnPropertyChanged(nameof(ImageModeSourceStepVisibility));
        OnPropertyChanged(nameof(ImageSetupStepVisibility));
        OnPropertyChanged(nameof(ImageParallaxSetupStepVisibility));
        OnPropertyChanged(nameof(ImageStereoSetupStepVisibility));
        OnPropertyChanged(nameof(ImageNoModeSetupHintVisibility));
        OnPropertyChanged(nameof(ImageParallaxPreviewExportStepVisibility));
        OnPropertyChanged(nameof(ImageStereoPreviewExportStepVisibility));
        OnPropertyChanged(nameof(SelectedImageModeName));
        OnPropertyChanged(nameof(SelectedImageStepName));
        OnPropertyChanged(nameof(ImageSelectedModeSummaryText));
        OnPropertyChanged(nameof(ImageCurrentStepSummaryText));
        OnPropertyChanged(nameof(ImagePlannedOutputFormatsText));
        OnPropertyChanged(nameof(SelectedStereoOutputFormatDisplayText));
        AnalyzeImageCommand.RaiseCanExecuteChanged();
        SelectImageSetupStepCommand.RaiseCanExecuteChanged();
        SelectImagePreviewExportStepCommand.RaiseCanExecuteChanged();
        ImageWizardBackCommand.RaiseCanExecuteChanged();
        ImageWizardNextCommand.RaiseCanExecuteChanged();
        ContinueWithImageConversionCommand.RaiseCanExecuteChanged();
    }

    private void RaiseAppSectionPropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedAppSection));
        OnPropertyChanged(nameof(IsHomeSectionSelected));
        OnPropertyChanged(nameof(IsImageConversionSectionSelected));
        OnPropertyChanged(nameof(IsVideoConversionSectionSelected));
        OnPropertyChanged(nameof(HomeSectionVisibility));
        OnPropertyChanged(nameof(ImageConversionSectionVisibility));
        OnPropertyChanged(nameof(VideoConversionSectionVisibility));
        RaiseShellTextPropertiesChanged();
    }

    private void RaiseShellTextPropertiesChanged()
    {
        OnPropertyChanged(nameof(ShellTaglineText));
        OnPropertyChanged(nameof(SidebarToggleGlyphText));
        OnPropertyChanged(nameof(SidebarToggleText));
        OnPropertyChanged(nameof(SidebarToggleToolTipText));
        OnPropertyChanged(nameof(HomeNavigationText));
        OnPropertyChanged(nameof(ImageConversionNavigationText));
        OnPropertyChanged(nameof(VideoConversionNavigationText));
        OnPropertyChanged(nameof(WindowMinimizeToolTipText));
        OnPropertyChanged(nameof(WindowMaximizeToolTipText));
        OnPropertyChanged(nameof(WindowRestoreToolTipText));
        OnPropertyChanged(nameof(WindowCloseToolTipText));
        OnPropertyChanged(nameof(HomeTitleText));
        OnPropertyChanged(nameof(HomeDescriptionText));
        OnPropertyChanged(nameof(HomeVideoCardTitleText));
        OnPropertyChanged(nameof(HomeVideoCardBodyText));
        OnPropertyChanged(nameof(HomeImageCardTitleText));
        OnPropertyChanged(nameof(HomeImageCardBodyText));
        OnPropertyChanged(nameof(HomeSettingsCardTitleText));
        OnPropertyChanged(nameof(HomeSettingsCardBodyText));
        OnPropertyChanged(nameof(HomeStatusSummaryText));
        OnPropertyChanged(nameof(HomeLocalOnlyBadgeText));
        OnPropertyChanged(nameof(HomeVideoStatusText));
        OnPropertyChanged(nameof(HomeImageStatusText));
        OnPropertyChanged(nameof(HomeModelsStatusText));
        OnPropertyChanged(nameof(OpenSectionText));
        OnPropertyChanged(nameof(ReadyNowText));
        OnPropertyChanged(nameof(ComingNextText));
        OnPropertyChanged(nameof(ImageConversionTitleText));
        OnPropertyChanged(nameof(ImageConversionIntroText));
        OnPropertyChanged(nameof(ImageParallaxModeTitleText));
        OnPropertyChanged(nameof(ImageParallaxModeBodyText));
        OnPropertyChanged(nameof(Image3DOutputModeTitleText));
        OnPropertyChanged(nameof(Image3DOutputModeBodyText));
        OnPropertyChanged(nameof(DisabledPlaceholderText));
        OnPropertyChanged(nameof(ImageSourcePanelTitleText));
        OnPropertyChanged(nameof(ImageDepthPanelTitleText));
        OnPropertyChanged(nameof(ImageParallaxPreviewTitleText));
        OnPropertyChanged(nameof(ImageParameterPanelTitleText));
        OnPropertyChanged(nameof(ImageQuickSummaryTitleText));
        OnPropertyChanged(nameof(ImageScaffoldLogTitleText));
        OnPropertyChanged(nameof(ImageScaffoldLogText));
        OnPropertyChanged(nameof(ImageDepthParameterText));
        OnPropertyChanged(nameof(ImageMotionParameterText));
        OnPropertyChanged(nameof(ImageZoomParameterText));
        OnPropertyChanged(nameof(ImageDirectionParameterText));
        OnPropertyChanged(nameof(ImageDurationParameterText));
        OnPropertyChanged(nameof(ImageSmoothingParameterText));
        OnPropertyChanged(nameof(ImageLayersParameterText));
        OnPropertyChanged(nameof(ImagePreviewActionText));
        OnPropertyChanged(nameof(ImageExportActionText));
        OnPropertyChanged(nameof(ImageOpenOutputFolderActionText));
        OnPropertyChanged(nameof(ImageNewConversionActionText));
        OnPropertyChanged(nameof(ImageResultParallaxTitleText));
        OnPropertyChanged(nameof(ImageExportOptionsTitleText));
        OnPropertyChanged(nameof(ImageResultSummaryTitleText));
        OnPropertyChanged(nameof(ImageResultSummaryText));
        OnPropertyChanged(nameof(ImageStereoPreviewTitleText));
        OnPropertyChanged(nameof(ImageStereoControlsTitleText));
        OnPropertyChanged(nameof(ImageStereoSummaryText));
        OnPropertyChanged(nameof(ImageStereoSeparationText));
        OnPropertyChanged(nameof(ImageStereoConvergenceText));
        OnPropertyChanged(nameof(ImageStereoAnaglyphText));
        OnPropertyChanged(nameof(ImageStereoResultTitleText));
        OnPropertyChanged(nameof(ImageGeneratedFilesTitleText));
        OnPropertyChanged(nameof(ImageOutputPanelTitleText));
        OnPropertyChanged(nameof(ImageComparisonTitleText));
        OnPropertyChanged(nameof(ImageModeSourceStepTitleText));
        OnPropertyChanged(nameof(ImageSetupStepTitleText));
        OnPropertyChanged(nameof(ImagePreviewExportStepTitleText));
        OnPropertyChanged(nameof(ImageSourceModeStepTitleText));
        OnPropertyChanged(nameof(ImageModeSelectionTitleText));
        OnPropertyChanged(nameof(ChangeImageWorkflowText));
        OnPropertyChanged(nameof(ImageModeSourceBodyText));
        OnPropertyChanged(nameof(ImageSourceAnalysisTitleText));
        OnPropertyChanged(nameof(DropImageText));
        OnPropertyChanged(nameof(ImageNoModeSetupHintText));
        OnPropertyChanged(nameof(AnalyzeImageText));
        OnPropertyChanged(nameof(ImageAnalysisTitleText));
        OnPropertyChanged(nameof(SelectedImageModeName));
        OnPropertyChanged(nameof(SelectedImageStepName));
        OnPropertyChanged(nameof(ImageSummaryStatusTitleText));
        OnPropertyChanged(nameof(ImageSelectedModeSummaryText));
        OnPropertyChanged(nameof(ImageCurrentStepSummaryText));
        OnPropertyChanged(nameof(ImageSupportedInputFormatsText));
        OnPropertyChanged(nameof(ImagePlannedOutputFormatsText));
        OnPropertyChanged(nameof(ImageLocalModelReadinessNoteText));
        OnPropertyChanged(nameof(ImageNotImplementedStateText));
        OnPropertyChanged(nameof(ImageActivityLogTitleText));
        OnPropertyChanged(nameof(ActiveActivityLogModalTitleText));
        OnPropertyChanged(nameof(ImageActivityLogText));
        OnPropertyChanged(nameof(SelectImageText));
        OnPropertyChanged(nameof(ImageSelectedTitleText));
        OnPropertyChanged(nameof(NoImageSelectedText));
        OnPropertyChanged(nameof(SelectedImageDisplayPath));
        OnPropertyChanged(nameof(SelectedImageFileName));
        OnPropertyChanged(nameof(ImageWizardNextToolTipText));
        OnPropertyChanged(nameof(ContinueWithImageConversionText));
        OnPropertyChanged(nameof(ImagePreviewExportStatusTitleText));
        OnPropertyChanged(nameof(ImagePreviewExportStatusText));
        OnPropertyChanged(nameof(ImageSupportedExtensionsText));
        OnPropertyChanged(nameof(ImageMetadataTitleText));
        OnPropertyChanged(nameof(ImageMetadataWidthText));
        OnPropertyChanged(nameof(ImageMetadataHeightText));
        OnPropertyChanged(nameof(ImageMetadataAspectRatioText));
        OnPropertyChanged(nameof(ImageMetadataFormatText));
        OnPropertyChanged(nameof(ImageMetadataPixelFormatText));
        OnPropertyChanged(nameof(ImageMetadataFileSizeText));
        OnPropertyChanged(nameof(ImageMetadataSummaryText));
        OnPropertyChanged(nameof(ImageSourceStatusText));
        OnPropertyChanged(nameof(ImageOutputPlanText));
        OnPropertyChanged(nameof(ImageSetupSummaryText));
        OnPropertyChanged(nameof(ImageDepthIntensityLabelText));
        OnPropertyChanged(nameof(ImageMotionDirectionLabelText));
        OnPropertyChanged(nameof(ImageZoomAmplitudeLabelText));
        OnPropertyChanged(nameof(ImageDurationLabelText));
        OnPropertyChanged(nameof(ImageSmoothingLabelText));
        OnPropertyChanged(nameof(ImageLayerBehaviorLabelText));
        OnPropertyChanged(nameof(ImageStereoOutputFormatLabelText));
        OnPropertyChanged(nameof(ImageStereoEyeSeparationLabelText));
        OnPropertyChanged(nameof(ImageStereoConvergenceLabelText));
        OnPropertyChanged(nameof(ImageStereoSwapEyesLabelText));
        OnPropertyChanged(nameof(ImageStereoAnaglyphModeLabelText));
        OnPropertyChanged(nameof(SelectedStereoOutputFormatDisplayText));
    }

    private void RaiseLocalizedPropertiesChanged()
    {
        RaiseShellTextPropertiesChanged();
        OnPropertyChanged(nameof(SubtitleText));
        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(ThemeLabel));
        OnPropertyChanged(nameof(SettingsText));
        OnPropertyChanged(nameof(SettingsTitleText));
        OnPropertyChanged(nameof(SettingsSideMenuTitleText));
        RaiseSettingsSectionPropertiesChanged();
        RaiseWizardPropertiesChanged();
        OnPropertyChanged(nameof(SelectSourceTitle));
        OnPropertyChanged(nameof(DropVideoText));
        OnPropertyChanged(nameof(NoVideoSelectedText));
        OnPropertyChanged(nameof(SourceAnalysisEmptyHintText));
        OnPropertyChanged(nameof(SourceAnalysisEmptyHintVisibility));
        OnPropertyChanged(nameof(SelectedVideoDisplayPath));
        OnPropertyChanged(nameof(SelectVideoText));
        OnPropertyChanged(nameof(AnalyzeText));
        OnPropertyChanged(nameof(VideoAnalysisTitle));
        RaiseAnalysisPropertiesChanged();
        RaiseRecommendationPropertiesChanged();
        RaiseConversionPlanPropertiesChanged();
        RaiseConversionExecutionPropertiesChanged();
        RaisePreviewPropertiesChanged();
        RaiseConversionReadinessPropertiesChanged();
        RaiseSystemStatusPropertiesChanged();
        OnPropertyChanged(nameof(ToolStatusTitle));
        OnPropertyChanged(nameof(RefreshText));
        OnPropertyChanged(nameof(OpenEngineFolderText));
        OnPropertyChanged(nameof(OpenModelsFolderText));
        OnPropertyChanged(nameof(ViewModelsText));
        OnPropertyChanged(nameof(ViewModelsToolTipText));
        RaiseModelHelpPropertiesChanged();
        OnPropertyChanged(nameof(ActivityLogTitle));
        OnPropertyChanged(nameof(ViewLogText));
        OnPropertyChanged(nameof(CopyFullLogText));
        OnPropertyChanged(nameof(CopyPreviewLogText));
        OnPropertyChanged(nameof(LogCopiedText));
        OnPropertyChanged(nameof(CouldNotCopyLogText));
        OnPropertyChanged(nameof(LogCopyNotificationText));
        OnPropertyChanged(nameof(ActiveActivityLogModalTitleText));
        OnPropertyChanged(nameof(GlobalBusyText));
        OnPropertyChanged(nameof(ClearText));
        OnPropertyChanged(nameof(AboutModelNoticesTitleText));
        OnPropertyChanged(nameof(AboutModelNoticesText));
        OnPropertyChanged(nameof(LogsDiagnosticsTechnicalDetailsTitleText));
        OnPropertyChanged(nameof(LogsDiagnosticsTechnicalDetailsText));
        OnPropertyChanged(nameof(PreviewStageResetNoticeText));
        RaisePresetPropertiesChanged();
    }

    private void RaisePresetPropertiesChanged()
    {
        OnPropertyChanged(nameof(RecommendedPresetTitle));
        OnPropertyChanged(nameof(OutputPresetLabel));
        OnPropertyChanged(nameof(OutputProfileDisplayText));
        OnPropertyChanged(nameof(IsLgOutputProfileSelected));
        OnPropertyChanged(nameof(LgCompatibilityOptionsVisibility));
        OnPropertyChanged(nameof(LgCompatibilityCopyPathVisibility));
        OnPropertyChanged(nameof(LgCompatibilityCopyExplanationText));
        OnPropertyChanged(nameof(CreateLgCompatibilityCopyText));
        OnPropertyChanged(nameof(PreferLgCompatibilityCopyWhenOpeningText));
        OnPropertyChanged(nameof(CanChangeLgCompatibilityCopyOptions));
        OnPropertyChanged(nameof(CanPreferLgCompatibilityCopyWhenOpening));
        OnPropertyChanged(nameof(ConversionPlanPresetText));
        OnPropertyChanged(nameof(ConversionPlanLgCompatibilityCopyPathText));
        OnPropertyChanged(nameof(ConversionSummaryPresetText));
        OnPropertyChanged(nameof(ConversionSummaryLgCompatibilityCopyText));
        OnPropertyChanged(nameof(PresetName));
        OnPropertyChanged(nameof(PresetDescriptionText));
        OnPropertyChanged(nameof(PresetBestForText));
        OnPropertyChanged(nameof(PresetTechnicalRecommendationTitle));
        OnPropertyChanged(nameof(PresetContainerText));
        OnPropertyChanged(nameof(PresetVideoCodecText));
        OnPropertyChanged(nameof(PresetAudioCodecText));
        OnPropertyChanged(nameof(PresetResolutionText));
        OnPropertyChanged(nameof(PresetThreeDLayoutText));
        OnPropertyChanged(nameof(PresetAdvancedOutputText));
        OnPropertyChanged(nameof(PresetCompatibilityNoteText));
        OnPropertyChanged(nameof(TvPlaybackTitle));
        OnPropertyChanged(nameof(TvPlaybackInstructions));
        OnPropertyChanged(nameof(ProfileDetailsBodyText));
        RaisePreflightEstimatePropertiesChanged();
    }

    private void RaiseAnalysisPropertiesChanged()
    {
        OnPropertyChanged(nameof(AnalysisStatusText));
        OnPropertyChanged(nameof(VideoAnalysisPendingStatusText));
        OnPropertyChanged(nameof(VideoAnalysisSectionVisibility));
        OnPropertyChanged(nameof(VideoAnalysisPendingStatusVisibility));
        OnPropertyChanged(nameof(VideoAnalysisResultsVisibility));
        OnPropertyChanged(nameof(AnalysisDurationText));
        OnPropertyChanged(nameof(AnalysisResolutionText));
        OnPropertyChanged(nameof(AnalysisFpsText));
        OnPropertyChanged(nameof(AnalysisCodecText));
        OnPropertyChanged(nameof(AnalysisContainerText));
        OnPropertyChanged(nameof(AnalysisAudioStreamsText));
        OnPropertyChanged(nameof(AnalysisSubtitleStreamsText));
        OnPropertyChanged(nameof(AnalysisHdrText));
        OnPropertyChanged(nameof(AnalysisCompatibilityText));
        RaisePreflightEstimatePropertiesChanged();
    }

    private void RaiseRecommendationPropertiesChanged()
    {
        OnPropertyChanged(nameof(RecommendedSetupTitle));
        OnPropertyChanged(nameof(CanOpenRecommendedSetupTab));
        OnPropertyChanged(nameof(RecommendedSetupStatusText));
        OnPropertyChanged(nameof(RecommendedOutputContainerText));
        OnPropertyChanged(nameof(RecommendedVideoCodecText));
        OnPropertyChanged(nameof(RecommendedAudioCodecText));
        OnPropertyChanged(nameof(RecommendedResolutionText));
        OnPropertyChanged(nameof(RecommendedThreeDLayoutText));
        OnPropertyChanged(nameof(RecommendedQualityText));
        OnPropertyChanged(nameof(RecommendedIntensityText));
        OnPropertyChanged(nameof(RecommendedNotesTitle));
        OnPropertyChanged(nameof(RecommendedNotesText));
        RaisePreflightEstimatePropertiesChanged();
    }

    private string RecommendationLabelValue(string englishLabel, string spanishLabel, string? value) =>
        $"{Text(englishLabel, spanishLabel)}: {value ?? "-"}";

    private void RaisePlanOptionSelectionPropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedOutputContainer));
        OnPropertyChanged(nameof(SelectedQualityPreset));
        OnPropertyChanged(nameof(SelectedThreeDIntensity));
        OnPropertyChanged(nameof(SelectedThreeDOutputFormat));
        OnPropertyChanged(nameof(CreateLgCompatibilityCopy));
        OnPropertyChanged(nameof(PreferLgCompatibilityCopyWhenOpening));
        OnPropertyChanged(nameof(IsLgOutputProfileSelected));
        OnPropertyChanged(nameof(LgCompatibilityOptionsVisibility));
        OnPropertyChanged(nameof(LgCompatibilityCopyPathVisibility));
        OnPropertyChanged(nameof(LgCompatibilityCopyExplanationText));
        OnPropertyChanged(nameof(CanPreferLgCompatibilityCopyWhenOpening));
        OnPropertyChanged(nameof(OutputProfileDisplayText));
        OnPropertyChanged(nameof(ConversionPlanPresetText));
        OnPropertyChanged(nameof(ConversionPlanLgCompatibilityCopyPathText));
        OnPropertyChanged(nameof(ConversionSummaryPresetText));
        OnPropertyChanged(nameof(ConversionSummaryLgCompatibilityCopyText));
        RaisePreflightEstimatePropertiesChanged();
    }

    private void RaisePreflightEstimatePropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedModelGuidanceText));
        OnPropertyChanged(nameof(PresetGuidanceText));
        OnPropertyChanged(nameof(EstimatedConversionTimeText));
        OnPropertyChanged(nameof(EstimateConfidenceText));
        OnPropertyChanged(nameof(EstimateBasisText));
        OnPropertyChanged(nameof(EstimatedOutputSizeText));
        OnPropertyChanged(nameof(RecommendedFreeSpaceText));
        OnPropertyChanged(nameof(PerformanceHistoryPrivacyText));
        OnPropertyChanged(nameof(CompactEstimateTimeConfidenceText));
        OnPropertyChanged(nameof(CompactOutputSizeFreeSpaceText));
        OnPropertyChanged(nameof(CompactSelectedModelGuidanceText));
        OnPropertyChanged(nameof(CompactPresetGuidanceText));
        OnPropertyChanged(nameof(EstimateDetailsTitleText));
        OnPropertyChanged(nameof(GuidanceDetailsTitleText));
    }

    private void RaiseConversionPlanPropertiesChanged()
    {
        OnPropertyChanged(nameof(ConversionPlanTitle));
        OnPropertyChanged(nameof(CanOpenConversionPlanTab));
        OnPropertyChanged(nameof(PlanOptionsTitle));
        OnPropertyChanged(nameof(OutputContainerOptionLabel));
        OnPropertyChanged(nameof(OutputPresetLabel));
        OnPropertyChanged(nameof(ProfileDetailsButtonText));
        OnPropertyChanged(nameof(CreateLgCompatibilityCopyText));
        OnPropertyChanged(nameof(PreferLgCompatibilityCopyWhenOpeningText));
        OnPropertyChanged(nameof(IsLgOutputProfileSelected));
        OnPropertyChanged(nameof(LgCompatibilityOptionsVisibility));
        OnPropertyChanged(nameof(LgCompatibilityCopyPathVisibility));
        OnPropertyChanged(nameof(LgCompatibilityCopyExplanationText));
        OnPropertyChanged(nameof(CanChangeLgCompatibilityCopyOptions));
        OnPropertyChanged(nameof(CanPreferLgCompatibilityCopyWhenOpening));
        OnPropertyChanged(nameof(QualityOptionLabel));
        OnPropertyChanged(nameof(ThreeDIntensityOptionLabel));
        OnPropertyChanged(nameof(ThreeDOutputFormatOptionLabel));
        OnPropertyChanged(nameof(LocalModelSelectionLabel));
        OnPropertyChanged(nameof(HasLocalModelSelectionCandidates));
        OnPropertyChanged(nameof(HasUnmappedLocalModelCandidates));
        OnPropertyChanged(nameof(SelectedLocalModelCandidate));
        OnPropertyChanged(nameof(LocalModelSelectionStatusText));
        RaisePreflightEstimatePropertiesChanged();
        RaiseModelHelpPropertiesChanged();
        OnPropertyChanged(nameof(OutputLocationTitle));
        OnPropertyChanged(nameof(OutputPathLabel));
        OnPropertyChanged(nameof(BrowseOutputFolderText));
        OnPropertyChanged(nameof(ResetOutputPathText));
        OnPropertyChanged(nameof(OpenOutputWhenFinishedText));
        OnPropertyChanged(nameof(CanChangeOpenOutputWhenFinished));
        OnPropertyChanged(nameof(OutputProfileDisplayText));
        OnPropertyChanged(nameof(ConversionPlanStatusText));
        OnPropertyChanged(nameof(ConversionPlanPresetText));
        OnPropertyChanged(nameof(ConversionPlanLocalModelText));
        OnPropertyChanged(nameof(ConversionPlanOutputPathText));
        OnPropertyChanged(nameof(ConversionPlanLgCompatibilityCopyPathText));
        OnPropertyChanged(nameof(ConversionPlanOutputFormatText));
        OnPropertyChanged(nameof(ConversionPlanResolutionText));
        OnPropertyChanged(nameof(ConversionPlanThreeDLayoutText));
        OnPropertyChanged(nameof(ConversionPlanQualityText));
        OnPropertyChanged(nameof(ConversionPlanIntensityText));
        OnPropertyChanged(nameof(ConversionPlanDryRunReasonText));
        OnPropertyChanged(nameof(ConversionPlanStepsTitle));
        OnPropertyChanged(nameof(ConversionPlanStepsText));
        OnPropertyChanged(nameof(ConversionPlanCommandPreviewTitle));
        OnPropertyChanged(nameof(ConversionPlanCommandPreviewText));
        OnPropertyChanged(nameof(ConversionPlanTechnicalDetailsTitleText));
        OnPropertyChanged(nameof(ReadyForConversionSummaryText));
        OnPropertyChanged(nameof(PreviewConversionStatusTitleText));
        OnPropertyChanged(nameof(PreviewConversionStatusText));
        OnPropertyChanged(nameof(PreviewConversionStatusDetailText));
        RaisePreviewPropertiesChanged();
        OnPropertyChanged(nameof(ProfileDetailsBodyText));
        OnPropertyChanged(nameof(ConversionSummaryPresetText));
        OnPropertyChanged(nameof(ConversionSummaryLgCompatibilityCopyText));
        RaiseConversionExecutionPropertiesChanged();
        RaiseConversionReadinessPropertiesChanged();
    }

    private void RaiseConversionExecutionPropertiesChanged()
    {
        OnPropertyChanged(nameof(ShowConversionProgressCard));
        OnPropertyChanged(nameof(PreviewConversionStatusText));
        OnPropertyChanged(nameof(PreviewConversionStatusDetailText));
        OnPropertyChanged(nameof(ConversionProgressVisibility));
        OnPropertyChanged(nameof(ShowConversionReadinessCard));
        OnPropertyChanged(nameof(ConversionReadinessVisibility));
        OnPropertyChanged(nameof(ConversionProgressTitle));
        OnPropertyChanged(nameof(ConversionExecutionStatusLabel));
        OnPropertyChanged(nameof(ConversionExecutionStatusText));
        OnPropertyChanged(nameof(ConversionExecutionStepLabel));
        OnPropertyChanged(nameof(ConversionExecutionStepText));
        OnPropertyChanged(nameof(ConversionExecutionProgressLabel));
        OnPropertyChanged(nameof(ConversionExecutionProgressPercent));
        OnPropertyChanged(nameof(ConversionExecutionProgressText));
        OnPropertyChanged(nameof(ConversionProgressBarVisibility));
        OnPropertyChanged(nameof(ConversionRunningStatusVisibility));
        OnPropertyChanged(nameof(ConversionTimingEstimatesVisibility));
        OnPropertyChanged(nameof(ConversionProgressBarValue));
        OnPropertyChanged(nameof(ConversionProgressBarText));
        OnPropertyChanged(nameof(ConversionExecutionDetailText));
        OnPropertyChanged(nameof(ConversionElapsedLabelText));
        OnPropertyChanged(nameof(ConversionRemainingLabelText));
        OnPropertyChanged(nameof(ConversionEstimatedTotalLabelText));
        OnPropertyChanged(nameof(ConversionElapsedValueText));
        OnPropertyChanged(nameof(ConversionRemainingValueText));
        OnPropertyChanged(nameof(ConversionEstimatedTotalValueText));
        OnPropertyChanged(nameof(ConversionReadinessStatusText));
        OnPropertyChanged(nameof(ConversionBlockedReasonText));
        OnPropertyChanged(nameof(ConversionReadySummaryVisibility));
        OnPropertyChanged(nameof(ConversionReadyTitleText));
        OnPropertyChanged(nameof(ConversionReadyBodyText));
        OnPropertyChanged(nameof(ConversionReadySelectedModelText));
        OnPropertyChanged(nameof(ConversionReadyOutputText));
        OnPropertyChanged(nameof(ConversionReadyDestinationText));
        OnPropertyChanged(nameof(ConversionMissingRequirementsVisibility));
        OnPropertyChanged(nameof(CanCancelConversion));
        OnPropertyChanged(nameof(CancelConversionText));
        OnPropertyChanged(nameof(ConvertPrimaryActionVisibility));
        OnPropertyChanged(nameof(CancelConversionPrimaryActionVisibility));
        OnPropertyChanged(nameof(CanStartConversion));
        OnPropertyChanged(nameof(CanStartOrCancelConversion));
        OnPropertyChanged(nameof(StartConversionText));
        OnPropertyChanged(nameof(ConversionSummaryCurrentStatusText));
        OnPropertyChanged(nameof(CanChangeOpenOutputWhenFinished));
        OnPropertyChanged(nameof(CanChangeLgCompatibilityCopyOptions));
        OnPropertyChanged(nameof(CanPreferLgCompatibilityCopyWhenOpening));
        StartConversionCommand.RaiseCanExecuteChanged();
        ShowProfileDetailsCommand.RaiseCanExecuteChanged();
        RaiseModelPackImportAvailabilityPropertiesChanged();
    }

    private void RaisePreviewConversionStagePropertiesChanged()
    {
        OnPropertyChanged(nameof(HasEnteredPreviewConversionStage));
        OnPropertyChanged(nameof(CanEnterPreviewConversionStage));
        OnPropertyChanged(nameof(ContinueWithConversionActionVisibility));
        OnPropertyChanged(nameof(ContinueWithConversionFooterVisibility));
        OnPropertyChanged(nameof(ContinueWithConversionText));
        OnPropertyChanged(nameof(PreviewConversionStatusCardVisibility));
        OnPropertyChanged(nameof(PreviewConversionRowHeight));
        OnPropertyChanged(nameof(ActivityLogCardMargin));
        OnPropertyChanged(nameof(PreviewStageResetNoticeText));
        OnPropertyChanged(nameof(PreviewConversionMissingToolsVisibility));
        OnPropertyChanged(nameof(PreviewConversionMissingToolsText));
        OnPropertyChanged(nameof(OpenSettingsForToolsText));
        ContinueWithConversionCommand.RaiseCanExecuteChanged();
        RaisePreviewPropertiesChanged();
    }

    private void RaisePreviewPropertiesChanged()
    {
        OnPropertyChanged(nameof(PreviewTitleText));
        OnPropertyChanged(nameof(HasEnteredPreviewConversionStage));
        OnPropertyChanged(nameof(CanEnterPreviewConversionStage));
        OnPropertyChanged(nameof(ContinueWithConversionActionVisibility));
        OnPropertyChanged(nameof(ContinueWithConversionFooterVisibility));
        OnPropertyChanged(nameof(ContinueWithConversionText));
        OnPropertyChanged(nameof(PreviewConversionStatusCardVisibility));
        OnPropertyChanged(nameof(PreviewConversionRowHeight));
        OnPropertyChanged(nameof(ActivityLogCardMargin));
        OnPropertyChanged(nameof(PreviewStageResetNoticeText));
        OnPropertyChanged(nameof(PreviewConversionStatusTitleText));
        OnPropertyChanged(nameof(PreviewConversionStatusText));
        OnPropertyChanged(nameof(PreviewConversionStatusDetailText));
        OnPropertyChanged(nameof(PreviewRequiredTitleText));
        OnPropertyChanged(nameof(PreviewAcceptedTitleText));
        OnPropertyChanged(nameof(PreviewStepTitleText));
        OnPropertyChanged(nameof(GeneratePreviewText));
        OnPropertyChanged(nameof(PreviewRequiredInstructionText));
        OnPropertyChanged(nameof(PreviewRequirementVisibility));
        OnPropertyChanged(nameof(ConversionReadySummaryVisibility));
        OnPropertyChanged(nameof(ConversionMissingRequirementsVisibility));
        OnPropertyChanged(nameof(PreviewConversionMissingToolsVisibility));
        OnPropertyChanged(nameof(PreviewConversionMissingToolsText));
        OnPropertyChanged(nameof(OpenSettingsForToolsText));
        OnPropertyChanged(nameof(GeneratePreviewPrimaryActionVisibility));
        OnPropertyChanged(nameof(ConvertPrimaryActionVisibility));
        OnPropertyChanged(nameof(CancelConversionPrimaryActionVisibility));
        OnPropertyChanged(nameof(CancelPreviewText));
        OnPropertyChanged(nameof(OpenPreviewText));
        OnPropertyChanged(nameof(OpenPreviewExternallyText));
        OnPropertyChanged(nameof(DeletePreviewText));
        OnPropertyChanged(nameof(ContinuePreviewText));
        OnPropertyChanged(nameof(PreviewGeneratingTitleText));
        OnPropertyChanged(nameof(PreviewGeneratingMessageText));
        OnPropertyChanged(nameof(PreviewReadyTitleText));
        OnPropertyChanged(nameof(PreviewReadyMessageText));
        OnPropertyChanged(nameof(PreviewPlaybackFallbackText));
        OnPropertyChanged(nameof(PreviewPlayText));
        OnPropertyChanged(nameof(PreviewPauseText));
        OnPropertyChanged(nameof(PreviewReplayText));
        OnPropertyChanged(nameof(PreviewVolumeText));
        OnPropertyChanged(nameof(PreviewMutedText));
        OnPropertyChanged(nameof(PreviewMuteText));
        OnPropertyChanged(nameof(PreviewUnmuteText));
        OnPropertyChanged(nameof(PreviewEndedText));
        OnPropertyChanged(nameof(EmbeddedPlaybackUnavailableText));
        OnPropertyChanged(nameof(PreviewMediaSource));
        OnPropertyChanged(nameof(EmbeddedPreviewVisibility));
        OnPropertyChanged(nameof(PreviewMetricsHeaderVisibility));
        OnPropertyChanged(nameof(PreviewEngineText));
        OnPropertyChanged(nameof(PreviewRunningWithText));
        OnPropertyChanged(nameof(PreviewGpuMetricsNoteText));
        OnPropertyChanged(nameof(PreviewStageText));
        OnPropertyChanged(nameof(PreviewCpuUsageText));
        OnPropertyChanged(nameof(PreviewRamUsageText));
        OnPropertyChanged(nameof(PreviewGpuUsageText));
        OnPropertyChanged(nameof(PreviewVramUsageText));
        OnPropertyChanged(nameof(PreviewGpuMetricsStatusText));
        OnPropertyChanged(nameof(PreviewStatusText));
        OnPropertyChanged(nameof(PreviewGateStatusText));
        OnPropertyChanged(nameof(PreviewGateDetailText));
        OnPropertyChanged(nameof(PreviewDurationText));
        OnPropertyChanged(nameof(PreviewStartTimeText));
        OnPropertyChanged(nameof(PreviewFromLabel));
        OnPropertyChanged(nameof(PreviewToLabel));
        OnPropertyChanged(nameof(PreviewTimeRangeText));
        OnPropertyChanged(nameof(PreviewMaximumDurationText));
        OnPropertyChanged(nameof(PreviewTimeRangeValidationText));
        OnPropertyChanged(nameof(PreviewTimeRangeValidationVisibility));
        OnPropertyChanged(nameof(CanEditPreviewTimeRange));
        OnPropertyChanged(nameof(PreviewOutdatedText));
        OnPropertyChanged(nameof(PreviewOutdatedVisibility));
        OnPropertyChanged(nameof(PreviewOutputPathText));
        OnPropertyChanged(nameof(PreviewOutputPathVisibility));
        OnPropertyChanged(nameof(IsPreviewGenerating));
        OnPropertyChanged(nameof(CanGeneratePreview));
        OnPropertyChanged(nameof(CanCancelPreview));
        OnPropertyChanged(nameof(CanOpenPreview));
        OnPropertyChanged(nameof(CanDeletePreview));
        OnPropertyChanged(nameof(CanContinuePreview));
        OnPropertyChanged(nameof(PreviewProgressPercent));
        OnPropertyChanged(nameof(PreviewProgressText));
        OnPropertyChanged(nameof(PreviewProgressIsIndeterminate));
        OnPropertyChanged(nameof(PreviewModalDetailText));
        OnPropertyChanged(nameof(PreviewGenerationLogText));
        OnPropertyChanged(nameof(CopyPreviewLogText));
        OnPropertyChanged(nameof(CanStartConversion));
        OnPropertyChanged(nameof(CanStartOrCancelConversion));
        GeneratePreviewCommand.RaiseCanExecuteChanged();
        ContinueWithConversionCommand.RaiseCanExecuteChanged();
        CancelPreviewCommand.RaiseCanExecuteChanged();
        OpenPreviewCommand.RaiseCanExecuteChanged();
        DeletePreviewCommand.RaiseCanExecuteChanged();
        ContinuePreviewCommand.RaiseCanExecuteChanged();
        CopyPreviewLogCommand.RaiseCanExecuteChanged();
        StartConversionCommand.RaiseCanExecuteChanged();
        SelectVideoCommand.RaiseCanExecuteChanged();
        AnalyzeCommand.RaiseCanExecuteChanged();
        BrowseOutputFolderCommand.RaiseCanExecuteChanged();
        ResetOutputPathCommand.RaiseCanExecuteChanged();
        RefreshEngineStatusCommand.RaiseCanExecuteChanged();
        ShowProfileDetailsCommand.RaiseCanExecuteChanged();
        ShowModelHelpCommand.RaiseCanExecuteChanged();
        ShowTechnicalDetailsCommand.RaiseCanExecuteChanged();
        ShowModelInventoryCommand.RaiseCanExecuteChanged();
        RaiseModelPackImportAvailabilityPropertiesChanged();
    }

    private void RaiseConversionRunningModePropertiesChanged()
    {
        OnPropertyChanged(nameof(IsConversionRunning));
        OnPropertyChanged(nameof(NormalSetupVisibility));
        OnPropertyChanged(nameof(ConversionRunningVisibility));
        OnPropertyChanged(nameof(ActivityLogVisibility));
        OnPropertyChanged(nameof(ConversionSummaryVisibility));
        OnPropertyChanged(nameof(ConversionRunningTitle));
        OnPropertyChanged(nameof(ConversionRunningStatusText));
        OnPropertyChanged(nameof(ConversionLiveLogEmptyText));
        OnPropertyChanged(nameof(ConversionSummaryTitle));
        OnPropertyChanged(nameof(ConversionSummaryPresetText));
        OnPropertyChanged(nameof(ConversionSummaryOutputContainerText));
        OnPropertyChanged(nameof(ConversionSummaryQualityText));
        OnPropertyChanged(nameof(ConversionSummaryIntensityText));
        OnPropertyChanged(nameof(ConversionSummaryLayoutText));
        OnPropertyChanged(nameof(ConversionSummaryLocalModelText));
        OnPropertyChanged(nameof(ConversionSummaryOutputPathText));
        OnPropertyChanged(nameof(LgCompatibilityCopyPathVisibility));
        OnPropertyChanged(nameof(ConversionSummaryLgCompatibilityCopyText));
        OnPropertyChanged(nameof(ConversionSummaryCurrentStatusText));
        OnPropertyChanged(nameof(OpenOutputWhenFinishedText));
        OnPropertyChanged(nameof(CanChangeOpenOutputWhenFinished));
        OnPropertyChanged(nameof(CanChangeLgCompatibilityCopyOptions));
        OnPropertyChanged(nameof(CanPreferLgCompatibilityCopyWhenOpening));
        OnPropertyChanged(nameof(LgCompatibilityOptionsVisibility));
        OnPropertyChanged(nameof(LgCompatibilityCopyPathVisibility));
        OnPropertyChanged(nameof(CanUseSystemStatusActions));
        OnPropertyChanged(nameof(CanOpenSystemStatusToolsTab));
        OnPropertyChanged(nameof(CanStartOrCancelConversion));
        OnPropertyChanged(nameof(StartConversionText));
        OnPropertyChanged(nameof(CancelConversionPrimaryActionVisibility));
        RaisePreviewPropertiesChanged();
        SelectVideoCommand.RaiseCanExecuteChanged();
        AnalyzeCommand.RaiseCanExecuteChanged();
        BrowseOutputFolderCommand.RaiseCanExecuteChanged();
        ResetOutputPathCommand.RaiseCanExecuteChanged();
        RefreshEngineStatusCommand.RaiseCanExecuteChanged();
        ShowTechnicalDetailsCommand.RaiseCanExecuteChanged();
        ShowProfileDetailsCommand.RaiseCanExecuteChanged();
        ShowModelHelpCommand.RaiseCanExecuteChanged();
        ShowModelInventoryCommand.RaiseCanExecuteChanged();
        StartConversionCommand.RaiseCanExecuteChanged();
        RaiseSystemStatusPropertiesChanged();
        RaiseModelPackImportAvailabilityPropertiesChanged();
    }

    private void RaiseWorkflowAvailabilityPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasCompletedAnalysis));
        OnPropertyChanged(nameof(CanOpenSourceAndAnalysisStep));
        OnPropertyChanged(nameof(CanOpenThreeDSetupStep));
        OnPropertyChanged(nameof(CanOpenConversionPlanStep));
        OnPropertyChanged(nameof(CanOpenRecommendedSetupTab));
        OnPropertyChanged(nameof(CanOpenConversionPlanTab));
        OnPropertyChanged(nameof(CanOpenSystemStatusConversionTab));
        RaiseWizardPropertiesChanged();
    }

    private void SetCanOpenConversionPlanStep(bool value)
    {
        if (_workflowState.SetCanOpenConversionPlanStep(value, out var selectedStepIndexChanged))
        {
            if (selectedStepIndexChanged)
            {
                OnPropertyChanged(nameof(SelectedWizardStepIndex));
                OnPropertyChanged(nameof(SelectedWorkflowTabIndex));
            }

            RaiseWorkflowAvailabilityPropertiesChanged();
        }
    }

    private void RaiseWizardPropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedWizardStepIndex));
        OnPropertyChanged(nameof(SelectedWorkflowTabIndex));
        OnPropertyChanged(nameof(SourceAndAnalysisStepTitle));
        OnPropertyChanged(nameof(ThreeDSetupStepTitle));
        OnPropertyChanged(nameof(WizardConversionPlanStepTitle));
        OnPropertyChanged(nameof(CanOpenSourceAndAnalysisStep));
        OnPropertyChanged(nameof(CanOpenThreeDSetupStep));
        OnPropertyChanged(nameof(CanOpenConversionPlanStep));
        OnPropertyChanged(nameof(CanMoveWizardBack));
        OnPropertyChanged(nameof(CanMoveWizardNext));
        OnPropertyChanged(nameof(WizardBackButtonVisibility));
        OnPropertyChanged(nameof(WizardNextButtonVisibility));
        OnPropertyChanged(nameof(WizardBackText));
        OnPropertyChanged(nameof(WizardNextText));
        OnPropertyChanged(nameof(WizardNextToolTipText));
        OnPropertyChanged(nameof(SourceAndAnalysisStepVisibility));
        OnPropertyChanged(nameof(ThreeDSetupStepVisibility));
        OnPropertyChanged(nameof(WizardConversionPlanStepVisibility));
        OnPropertyChanged(nameof(SourceAndAnalysisStepState));
        OnPropertyChanged(nameof(ThreeDSetupStepState));
        OnPropertyChanged(nameof(ConversionPlanStepState));
        OnPropertyChanged(nameof(SourceAndAnalysisStepMarkerText));
        OnPropertyChanged(nameof(ThreeDSetupStepMarkerText));
        OnPropertyChanged(nameof(ConversionPlanStepMarkerText));
        OnPropertyChanged(nameof(CanEnterPreviewConversionStage));
        OnPropertyChanged(nameof(ContinueWithConversionActionVisibility));
        OnPropertyChanged(nameof(ContinueWithConversionFooterVisibility));
        OnPropertyChanged(nameof(ContinueWithConversionText));
        OnPropertyChanged(nameof(PreviewStageResetNoticeText));
        WizardBackCommand.RaiseCanExecuteChanged();
        WizardNextCommand.RaiseCanExecuteChanged();
        ContinueWithConversionCommand.RaiseCanExecuteChanged();
    }

    private string GetWizardStepState(int stepIndex)
    {
        if (SelectedWizardStepIndex == stepIndex)
        {
            return "Active";
        }

        if (SelectedWizardStepIndex > stepIndex)
        {
            return "Completed";
        }

        return stepIndex switch
        {
            ConversionWorkflowState.SourceAndAnalysisStepIndex => "Pending",
            ConversionWorkflowState.ThreeDSetupStepIndex when CanOpenThreeDSetupStep => "Pending",
            ConversionWorkflowState.ConversionPlanStepIndex when CanOpenConversionPlanStep => "Pending",
            _ => "Locked",
        };
    }

    private void RaiseSystemStatusPropertiesChanged()
    {
        OnPropertyChanged(nameof(SystemStatusTitle));
        OnPropertyChanged(nameof(SystemStatusToolsTabTitle));
        OnPropertyChanged(nameof(SystemStatusConversionTabTitle));
        OnPropertyChanged(nameof(SystemStatusTechnicalDetailsTitle));
        OnPropertyChanged(nameof(SystemStatusDetailsButtonText));
        OnPropertyChanged(nameof(ProfileDetailsTitleText));
        OnPropertyChanged(nameof(ProfileDetailsButtonText));
        OnPropertyChanged(nameof(CloseDialogText));
        OnPropertyChanged(nameof(CancelDialogText));
        OnPropertyChanged(nameof(ReplaceSelectedVideoTitleText));
        OnPropertyChanged(nameof(ReplaceSelectedVideoBodyText));
        OnPropertyChanged(nameof(ReplaceVideoConfirmText));
        OnPropertyChanged(nameof(PreviewInvalidationConfirmationTitleText));
        OnPropertyChanged(nameof(PreviewInvalidationConfirmationBodyText));
        OnPropertyChanged(nameof(PreviewInvalidationConfirmText));
        OnPropertyChanged(nameof(ConversionCompletedTitleText));
        OnPropertyChanged(nameof(ConversionCompletedBodyText));
        OnPropertyChanged(nameof(ConversionCompletedOutputPathText));
        OnPropertyChanged(nameof(AcceptConversionCompletedText));
        RaiseModelPackImportConfirmationPropertiesChanged();
        OnPropertyChanged(nameof(ActiveModalTitleText));
        OnPropertyChanged(nameof(TechnicalDetailsBodyText));
        OnPropertyChanged(nameof(LogsDiagnosticsTechnicalDetailsText));
        OnPropertyChanged(nameof(ProfileDetailsBodyText));
        OnPropertyChanged(nameof(CanOpenSystemStatusConversionTab));
        OnPropertyChanged(nameof(CanOpenSystemStatusToolsTab));
        OnPropertyChanged(nameof(CanUseSystemStatusActions));
        OnPropertyChanged(nameof(SelectedSystemStatusTabIndex));
        OnPropertyChanged(nameof(ConversionReadinessEmptyText));
        OnPropertyChanged(nameof(VramUsageText));
        RaiseModelInventoryPropertiesChanged();
        OnPropertyChanged(nameof(ViewLogText));
        OnPropertyChanged(nameof(CopyFullLogText));
        OnPropertyChanged(nameof(CopyPreviewLogText));
        OnPropertyChanged(nameof(ImportModelPackText));
        OnPropertyChanged(nameof(ModelPackImportInstructionText));
        OnPropertyChanged(nameof(ModelPackImportStatusText));
        OnPropertyChanged(nameof(LastModelPackImportSummary));
        OnPropertyChanged(nameof(LastModelPackImportSummaryVisibility));
        OnPropertyChanged(nameof(AboutModelNoticesText));
        OnPropertyChanged(nameof(LogCopiedText));
        OnPropertyChanged(nameof(CouldNotCopyLogText));
        OnPropertyChanged(nameof(LogCopyNotificationText));
        RaiseModelPackImportAvailabilityPropertiesChanged();
        ShowModelInventoryCommand.RaiseCanExecuteChanged();
    }

    private void RaiseSettingsSectionPropertiesChanged()
    {
        OnPropertyChanged(nameof(SettingsSectionOptions));
        OnPropertyChanged(nameof(SelectedSettingsSection));
        OnPropertyChanged(nameof(VisualSettingsSectionVisibility));
        OnPropertyChanged(nameof(ModelsSettingsSectionVisibility));
        OnPropertyChanged(nameof(ToolsEngineSettingsSectionVisibility));
        OnPropertyChanged(nameof(LogsDiagnosticsSettingsSectionVisibility));
        OnPropertyChanged(nameof(AboutLicensesSettingsSectionVisibility));
        OnPropertyChanged(nameof(VisualSettingsTitleText));
        OnPropertyChanged(nameof(ModelsSettingsTitleText));
        OnPropertyChanged(nameof(ToolsEngineSettingsTitleText));
        OnPropertyChanged(nameof(LogsDiagnosticsSettingsTitleText));
        OnPropertyChanged(nameof(AboutLicensesSettingsTitleText));
        OnPropertyChanged(nameof(ModelsSettingsIntroText));
        OnPropertyChanged(nameof(ToolsEngineSettingsIntroText));
        OnPropertyChanged(nameof(LogsDiagnosticsSettingsIntroText));
        OnPropertyChanged(nameof(AboutLicensesText));
        OnPropertyChanged(nameof(AboutModelNoticesTitleText));
        OnPropertyChanged(nameof(AboutModelNoticesText));
        OnPropertyChanged(nameof(LogsDiagnosticsTechnicalDetailsTitleText));
        OnPropertyChanged(nameof(LogsDiagnosticsTechnicalDetailsText));
        OnPropertyChanged(nameof(ToolStatusTitle));
        OnPropertyChanged(nameof(RefreshText));
        OnPropertyChanged(nameof(ViewModelsText));
        OnPropertyChanged(nameof(ViewModelsToolTipText));
        OnPropertyChanged(nameof(ModelPackImportInstructionText));
        OnPropertyChanged(nameof(ModelPackImportStatusText));
        OnPropertyChanged(nameof(LastModelPackImportSummary));
        OnPropertyChanged(nameof(LastModelPackImportSummaryVisibility));
        OnPropertyChanged(nameof(ConversionPlanTechnicalDetailsTitleText));
    }

    private void RaiseModelPackImportAvailabilityPropertiesChanged()
    {
        OnPropertyChanged(nameof(CanImportModelPack));
        OnPropertyChanged(nameof(ImportModelPackText));
        ImportModelPackCommand.RaiseCanExecuteChanged();
        RaiseModelHelpPropertiesChanged();
    }

    private void RaiseModelHelpPropertiesChanged()
    {
        OnPropertyChanged(nameof(CanShowModelHelp));
        OnPropertyChanged(nameof(ModelHelpButtonText));
        OnPropertyChanged(nameof(ModelHelpButtonToolTipText));
        OnPropertyChanged(nameof(ModelHelpTitleText));
        OnPropertyChanged(nameof(ModelHelpIntroText));
        OnPropertyChanged(nameof(ModelHelpModelHeaderText));
        OnPropertyChanged(nameof(ModelHelpPurposeHeaderText));
        OnPropertyChanged(nameof(ModelHelpUseHeaderText));
        OnPropertyChanged(nameof(ModelHelpSceneHeaderText));
        OnPropertyChanged(nameof(ModelHelpDepthHeaderText));
        OnPropertyChanged(nameof(ModelHelpSizePerformanceHeaderText));
        OnPropertyChanged(nameof(ModelHelpRows));
        ShowModelHelpCommand.RaiseCanExecuteChanged();
    }

    private void RaiseModalStatePropertiesChanged()
    {
        OnPropertyChanged(nameof(IsAnyModalOpen));
        OnPropertyChanged(nameof(ModalOverlayVisibility));
        OnPropertyChanged(nameof(ActiveModalWidth));
        OnPropertyChanged(nameof(ActiveModalHeight));
        OnPropertyChanged(nameof(ActiveModalTitleText));
        OnPropertyChanged(nameof(TechnicalDetailsModalContentVisibility));
        OnPropertyChanged(nameof(ProfileDetailsModalContentVisibility));
        OnPropertyChanged(nameof(ModelHelpModalContentVisibility));
        OnPropertyChanged(nameof(SettingsModalContentVisibility));
        OnPropertyChanged(nameof(ReplaceVideoConfirmationModalContentVisibility));
        OnPropertyChanged(nameof(PreviewInvalidationConfirmationModalContentVisibility));
        OnPropertyChanged(nameof(ModelPackImportConfirmationModalContentVisibility));
        OnPropertyChanged(nameof(ConversionCompletedModalContentVisibility));
        OnPropertyChanged(nameof(ModelInventoryModalContentVisibility));
        OnPropertyChanged(nameof(ActivityLogModalContentVisibility));
        OnPropertyChanged(nameof(PreviewGeneratingModalContentVisibility));
        OnPropertyChanged(nameof(PreviewReadyModalContentVisibility));
        OnPropertyChanged(nameof(CanEditPreviewTimeRange));
        OnPropertyChanged(nameof(CanGeneratePreview));
        GeneratePreviewCommand.RaiseCanExecuteChanged();
    }

    private void RaiseModelInventoryPropertiesChanged()
    {
        OnPropertyChanged(nameof(ModelInventoryTitleText));
        OnPropertyChanged(nameof(ModelInventoryIntroText));
        OnPropertyChanged(nameof(ModelInventoryFolderLabelText));
        OnPropertyChanged(nameof(ModelInventoryFolderPathText));
        OnPropertyChanged(nameof(SelectableModelsSectionTitleText));
        OnPropertyChanged(nameof(SelectableModelsInventoryText));
        OnPropertyChanged(nameof(SelectableModelNameHeaderText));
        OnPropertyChanged(nameof(SelectableModelIw3HeaderText));
        OnPropertyChanged(nameof(SelectableModelCheckpointHeaderText));
        OnPropertyChanged(nameof(SelectableModelTypeHeaderText));
        OnPropertyChanged(nameof(SelectableModelSourceHeaderText));
        OnPropertyChanged(nameof(SelectableModelInventoryRows));
        OnPropertyChanged(nameof(SettingsSelectableModelsTableVisibility));
        OnPropertyChanged(nameof(SettingsSelectableModelsEmptyVisibility));
        OnPropertyChanged(nameof(SettingsSelectableModelsEmptyText));
        OnPropertyChanged(nameof(DiagnosticModelsSectionTitleText));
        OnPropertyChanged(nameof(DiagnosticModelsInventoryText));
        OnPropertyChanged(nameof(RuntimeDependenciesSectionTitleText));
        OnPropertyChanged(nameof(RuntimeDependenciesInventoryText));
        OnPropertyChanged(nameof(ModelInventoryActionsTitleText));
        OnPropertyChanged(nameof(OpenModelsFolderText));
        OnPropertyChanged(nameof(AboutModelNoticesText));
        OnPropertyChanged(nameof(ActiveModalTitleText));
    }

    private void RaiseModelPackImportConfirmationPropertiesChanged()
    {
        OnPropertyChanged(nameof(ModelPackImportConfirmationTitleText));
        OnPropertyChanged(nameof(ModelPackImportConfirmationIntroText));
        OnPropertyChanged(nameof(ModelPackImportConfirmationMessageText));
        OnPropertyChanged(nameof(ModelPackImportConfirmationContinueText));
        OnPropertyChanged(nameof(ActiveModalTitleText));
    }

    private void RaiseConversionReadinessPropertiesChanged()
    {
        OnPropertyChanged(nameof(ConversionReadinessTitle));
        OnPropertyChanged(nameof(ShowConversionReadinessCard));
        OnPropertyChanged(nameof(ConversionReadinessVisibility));
        OnPropertyChanged(nameof(ConversionReadinessStatusLabel));
        OnPropertyChanged(nameof(ConversionReadinessMissingRequirementsTitle));
        OnPropertyChanged(nameof(ConversionMissingRequirementsVisibility));
        OnPropertyChanged(nameof(ConversionReadinessStatusText));
        OnPropertyChanged(nameof(ConversionReadinessIssuesText));
        OnPropertyChanged(nameof(ConversionReadinessMissingComponentsSummaryText));
        OnPropertyChanged(nameof(ConversionReadinessRequiredComponentsText));
        OnPropertyChanged(nameof(ConversionBlockedReasonText));
        OnPropertyChanged(nameof(CanStartConversion));
        OnPropertyChanged(nameof(CanStartOrCancelConversion));
        OnPropertyChanged(nameof(StartConversionText));
        RaiseSystemStatusPropertiesChanged();
        StartConversionCommand.RaiseCanExecuteChanged();
    }

    private string ConversionPlanLabelValue(string englishLabel, string spanishLabel, string? value) =>
        $"{Text(englishLabel, spanishLabel)}: {value ?? "-"}";

    private void LogAnalysisFailure(VideoAnalysisFailure? failure)
    {
        switch (failure?.Kind)
        {
            case VideoAnalysisFailureKind.MissingFfprobe:
                AddLog(
                    "Bundled FFprobe is not available yet. Add " +
                    "tools/ffmpeg/win-x64/ffprobe.exe to enable video analysis.",
                    "FFprobe incluido aún no está disponible. Agrega " +
                    "tools/ffmpeg/win-x64/ffprobe.exe para habilitar el análisis de video.");
                break;
            case VideoAnalysisFailureKind.ProcessFailed:
                AddLog(
                    $"FFprobe analysis failed. {failure.StandardError}",
                    $"El análisis con FFprobe falló. {failure.StandardError}");
                break;
            case VideoAnalysisFailureKind.EmptyOutput:
                AddLog(
                    "FFprobe returned no analysis data.",
                    "FFprobe no devolvió datos de análisis.");
                break;
            case VideoAnalysisFailureKind.InvalidJson:
                AddLog(
                    "FFprobe returned invalid analysis data.",
                    "FFprobe devolvió datos de análisis no válidos.");
                break;
            case VideoAnalysisFailureKind.TimedOut:
                AddLog(
                    "FFprobe analysis timed out.",
                    "El análisis con FFprobe agotó el tiempo de espera.");
                break;
            case VideoAnalysisFailureKind.Canceled:
                AddLog(
                    "FFprobe analysis was canceled.",
                    "El análisis con FFprobe fue cancelado.");
                break;
            default:
                AddLog(
                    "Video analysis failed.",
                    "El análisis de video falló.");
                break;
        }
    }

    private sealed class InAppModelPackImportConfirmationService(
        Func<ModelPackImportConfirmationPrompt, CancellationToken, Task<bool>> confirmAsync)
        : IModelPackImportConfirmationService
    {
        public Task<bool> ConfirmAsync(
            ModelPackImportConfirmationPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            return confirmAsync(prompt, cancellationToken);
        }
    }
}
