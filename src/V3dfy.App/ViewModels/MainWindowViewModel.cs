using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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
using V3dfy.Core.Image;
using V3dfy.Core.Localization;
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
    private const string SupportedStereoAnaglyphMode = "Red/Cyan";

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

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;
        private readonly SynchronizationContext? _synchronizationContext;

        public InlineProgress(Action<T> report)
        {
            ArgumentNullException.ThrowIfNull(report);

            _report = report;
            _synchronizationContext = SynchronizationContext.Current;
        }

        public void Report(T value)
        {
            if (_synchronizationContext is null ||
                ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
            {
                ReportOnCapturedContext(value);
                return;
            }

            try
            {
                _synchronizationContext.Post(
                    static state =>
                    {
                        var (progress, reportedValue) = ((InlineProgress<T>, T))state!;
                        progress.ReportOnCapturedContext(reportedValue);
                    },
                    (this, value));
            }
            catch (Exception exception)
            {
                AppErrorLogService.LogRecoverableException("Image conversion progress dispatch", exception);
            }
        }

        private void ReportOnCapturedContext(T value)
        {
            try
            {
                _report(value);
            }
            catch (Exception exception)
            {
                AppErrorLogService.LogRecoverableException("Image conversion progress update", exception);
            }
        }
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
    private readonly IImageStereoExporter _imageStereoExporter;
    private readonly IImageParallaxExporter _imageParallaxExporter;
    private readonly ModelPackAppImportCoordinator _modelPackImportCoordinator;
    private readonly ILocalizationService _localizationService;
    private readonly ConversionPlanOptionState _planOptionState = new();
    private readonly ConversionOutputPathState _outputPathState = new();
    private readonly ConversionWorkflowState _workflowState = new();
    private readonly ConversionProgressTimingSmoother _conversionTimingSmoother = new();
    private readonly StringBuilder _previewGenerationLogTextBuilder = new();
    private ActivityLogModalKind _activeActivityLogModalKind;
    private string? _selectedVideoPath;
    private string _selectedLanguage = LocalizationCatalog.EnglishLanguageCode;
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
    private string _cpuUsageText = string.Empty;
    private string _ramUsageText = string.Empty;
    private string _gpuUsageText = string.Empty;
    private string _vramUsageText = string.Empty;
    private string _previewCpuUsageText = string.Empty;
    private string _previewRamUsageText = string.Empty;
    private string _previewGpuUsageText = string.Empty;
    private string _previewVramUsageText = string.Empty;
    private string _previewGpuMetricsStatusText = string.Empty;
    private string? _previewStageKey = LocalizationKeys.VideoPreviewStagePreparing;
    private string _previewStageEnglishText = string.Empty;
    private string _previewStageSpanishText = string.Empty;
    private AppSection _selectedAppSection = AppSection.Home;
    private SettingsSection _selectedSettingsSection = SettingsSection.VisualSettings;
    private bool _isSidebarPinnedExpanded = true;
    private bool _isSidebarHoverExpanded;
    private string? _selectedImagePath;
    private ImageMetadata? _selectedImageMetadata;
    private ImageConversionMode? _selectedImageConversionMode;
    private ImageConversionStep _selectedImageConversionStep = ImageConversionStep.ModeAndSource;
    private bool _isImageWorkflowChooserExpanded;
    private string _selectedParallaxDepthIntensity = "Low";
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
    private bool _isImageExportRunning;
    private int _imageExportProgressPercent;
    private string? _imageExportProgressKey = LocalizationKeys.ImageProgressNotStarted;
    private string _imageExportProgressEnglishText = string.Empty;
    private string _imageExportProgressSpanishText = string.Empty;
    private string _lastImageExportPrimaryPath = string.Empty;
    private string _lastImageExportOutputDirectory = string.Empty;
    private IReadOnlyList<string> _lastImageExportGeneratedFiles = [];
    private string _lastImageExportErrorEnglishText = string.Empty;
    private string _lastImageExportErrorSpanishText = string.Empty;
    private bool _isImageExportOutputOutdated;
    private bool _isImageParallaxModelHelpContext;
    private readonly IReadOnlyList<LocalizedOptionViewModel<TargetDevicePreset>> _outputPresetOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<OutputContainer>> _outputContainerOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<AiQualityPreset>> _qualityPresetOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<ThreeDIntensity>> _threeDIntensityOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<ThreeDOutputFormat>> _threeDOutputFormatOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<string>> _parallaxDepthIntensityOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<string>> _parallaxMotionDirectionOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<string>> _parallaxZoomAmplitudeOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<string>> _parallaxDurationOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<string>> _parallaxSmoothingOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<string>> _parallaxLayerBehaviorOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<string>> _stereoEyeSeparationOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<string>> _stereoConvergenceOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<string>> _stereoAnaglyphModeOptions;
    private readonly IReadOnlyList<LocalizedOptionViewModel<ImageStereoOutputFormat>> _allStereoOutputFormatOptions;

    private bool HasAnyImageConversionOutput => !string.IsNullOrWhiteSpace(_lastImageExportPrimaryPath);

    private bool HasCurrentImageConversionOutput => HasAnyImageConversionOutput && !IsImageExportOutputOutdated;

    private string _modelPackImportStatusEnglishText = string.Empty;
    private string _modelPackImportStatusSpanishText = string.Empty;
    private string _lastModelPackImportSummaryEnglishText = string.Empty;
    private string _lastModelPackImportSummarySpanishText = string.Empty;
    private string _globalBusyEnglishText = string.Empty;
    private string _globalBusySpanishText = string.Empty;
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
        _localizationService = CreateLocalizationService();
        LanguageOptions = CreateLanguageOptions(_localizationService.AvailableLanguages);
        _selectedLanguage = _localizationService.ActiveLanguageCode;
        _outputPresetOptions =
        [
            new(TargetDevicePresets.Recommended3dTv, LocalizationKeys.VideoOptionOutputProfileRecommended, _localizationService),
            new(TargetDevicePresets.MaximumCompatibility, LocalizationKeys.VideoOptionOutputProfileMaximumCompatibility, _localizationService),
            new(TargetDevicePresets.HighQualityMaster, LocalizationKeys.VideoOptionOutputProfileHighQualityMaster, _localizationService),
            new(TargetDevicePresets.Lg3dFullHd2012, LocalizationKeys.VideoOptionOutputProfileLegacyLg2012, _localizationService),
        ];
        _outputContainerOptions =
        [
            new(OutputContainer.MP4, "MP4", "MP4"),
            new(OutputContainer.MKV, "MKV", "MKV"),
        ];
        _qualityPresetOptions =
        [
            new(AiQualityPreset.Fast, LocalizationKeys.VideoOptionQualityFast, _localizationService),
            new(AiQualityPreset.Balanced, LocalizationKeys.VideoOptionQualityBalanced, _localizationService),
            new(AiQualityPreset.HighQuality, LocalizationKeys.VideoOptionQualityHighQuality, _localizationService),
        ];
        _threeDIntensityOptions =
        [
            new(ThreeDIntensity.Low, LocalizationKeys.VideoOptionIntensityLow, _localizationService),
            new(ThreeDIntensity.Medium, LocalizationKeys.VideoOptionIntensityMedium, _localizationService),
            new(ThreeDIntensity.High, LocalizationKeys.VideoOptionIntensityHigh, _localizationService),
        ];
        _threeDOutputFormatOptions =
        [
            new(ThreeDOutputFormat.HalfTopBottom, LocalizationKeys.VideoOptionOutputFormatHalfTopBottom, _localizationService),
            new(ThreeDOutputFormat.HalfSideBySide, LocalizationKeys.VideoOptionOutputFormatHalfSideBySide, _localizationService),
            new(ThreeDOutputFormat.Anaglyph, LocalizationKeys.VideoOptionOutputFormatAnaglyph, _localizationService),
        ];
        _parallaxDepthIntensityOptions = CreateLocalizedStringOptions(new (string Value, string Key)[]
        {
            ("Low", LocalizationKeys.ImageParallaxDepthIntensityLow),
            ("Medium", LocalizationKeys.ImageParallaxDepthIntensityMedium),
            ("High", LocalizationKeys.ImageParallaxDepthIntensityHigh),
        });
        _parallaxMotionDirectionOptions = CreateLocalizedStringOptions(new (string Value, string Key)[]
        {
            ("Left to right", LocalizationKeys.ImageParallaxMotionDirectionLeftToRight),
            ("Right to left", LocalizationKeys.ImageParallaxMotionDirectionRightToLeft),
            ("Push in", LocalizationKeys.ImageParallaxMotionDirectionPushIn),
            ("Pull back", LocalizationKeys.ImageParallaxMotionDirectionPullBack),
            ("Orbit", LocalizationKeys.ImageParallaxMotionDirectionOrbit),
        });
        _parallaxZoomAmplitudeOptions = CreateLocalizedStringOptions(new (string Value, string Key)[]
        {
            ("Subtle", LocalizationKeys.ImageParallaxZoomAmplitudeSubtle),
            ("Medium", LocalizationKeys.ImageParallaxZoomAmplitudeMedium),
            ("Strong", LocalizationKeys.ImageParallaxZoomAmplitudeStrong),
        });
        _parallaxDurationOptions = CreateLocalizedStringOptions(new (string Value, string Key)[]
        {
            ("4 seconds", LocalizationKeys.ImageParallaxDuration4Seconds),
            ("6 seconds", LocalizationKeys.ImageParallaxDuration6Seconds),
            ("8 seconds", LocalizationKeys.ImageParallaxDuration8Seconds),
            ("12 seconds", LocalizationKeys.ImageParallaxDuration12Seconds),
        });
        _parallaxSmoothingOptions = CreateLocalizedStringOptions(new (string Value, string Key)[]
        {
            ("Enabled", LocalizationKeys.ImageParallaxSmoothingEnabled),
            ("Balanced", LocalizationKeys.ImageParallaxSmoothingBalanced),
            ("Strong", LocalizationKeys.ImageParallaxSmoothingStrong),
        });
        _parallaxLayerBehaviorOptions = CreateLocalizedStringOptions(new (string Value, string Key)[]
        {
            ("Foreground / mid / background", LocalizationKeys.ImageParallaxLayerForegroundMidBackground),
            ("Depth slices", LocalizationKeys.ImageParallaxLayerDepthSlices),
            ("Soft depth ramp", LocalizationKeys.ImageParallaxLayerSoftDepthRamp),
        });
        _stereoEyeSeparationOptions = CreateLocalizedStringOptions(new (string Value, string Key)[]
        {
            ("2.0%", LocalizationKeys.ImageStereoEyeSeparation2Percent),
            ("4.0%", LocalizationKeys.ImageStereoEyeSeparation4Percent),
            ("6.0%", LocalizationKeys.ImageStereoEyeSeparation6Percent),
            ("8.0%", LocalizationKeys.ImageStereoEyeSeparation8Percent),
        });
        _stereoConvergenceOptions = CreateLocalizedStringOptions(new (string Value, string Key)[]
        {
            ("Near", LocalizationKeys.ImageStereoConvergenceNear),
            ("Neutral", LocalizationKeys.ImageStereoConvergenceNeutral),
            ("Far", LocalizationKeys.ImageStereoConvergenceFar),
        });
        _stereoAnaglyphModeOptions = CreateLocalizedStringOptions(new (string Value, string Key)[]
        {
            (SupportedStereoAnaglyphMode, LocalizationKeys.ImageStereoAnaglyphModeRedCyan),
        });
        _allStereoOutputFormatOptions =
        [
            new(ImageStereoOutputFormat.SideBySide, LocalizationKeys.ImageStereoOutputFormatSbs, _localizationService),
            new(ImageStereoOutputFormat.HalfTopBottom, LocalizationKeys.ImageStereoOutputFormatHalfTopBottom, _localizationService),
            new(ImageStereoOutputFormat.Anaglyph, LocalizationKeys.ImageStereoOutputFormatAnaglyph, _localizationService),
        ];
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
        _imageStereoExporter = new Iw3ImageStereoExporter(
            processRunner: new BundledLocalProcessRunner(
                new LocalProcessRunner(),
                _toolPaths.Iw3EngineDirectory));
        _imageParallaxExporter = new Iw3ImageParallaxExporter(
            processRunner: new BundledLocalProcessRunner(
                new LocalProcessRunner(),
                _toolPaths.Iw3EngineDirectory));
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
            () => CanUseSettingsSystemStatusActions);
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar, () => CanUseShellNavigation);
        SelectHomeSectionCommand = new RelayCommand(
            () => SelectAppSection(AppSection.Home),
            () => CanUseShellNavigation);
        SelectImageConversionSectionCommand = new RelayCommand(
            () => SelectAppSection(AppSection.ImageConversion),
            () => CanUseShellNavigation);
        SelectVideoConversionSectionCommand = new RelayCommand(
            () => SelectAppSection(AppSection.VideoConversion),
            () => CanUseShellNavigation);
        SelectImageParallaxModeCommand = new RelayCommand(
            () => SelectImageConversionMode(ImageConversionMode.ParallaxPhoto),
            () => CanInteractWithImageWorkflow);
        SelectImageStereoModeCommand = new RelayCommand(
            () => SelectImageConversionMode(ImageConversionMode.StereoscopicImage),
            () => CanInteractWithImageWorkflow);
        ToggleImageWorkflowChooserCommand = new RelayCommand(
            ToggleImageWorkflowChooser,
            () => CanInteractWithImageWorkflow);
        SelectImageModeSourceStepCommand = new RelayCommand(
            () => SelectImageConversionStep(ImageConversionStep.ModeAndSource),
            () => CanUseImageStepNavigation);
        SelectImageSetupStepCommand = new RelayCommand(
            () => SelectImageConversionStep(ImageConversionStep.Setup),
            () => CanOpenImageSetupStep);
        SelectImagePreviewExportStepCommand = new RelayCommand(
            () => SelectImageConversionStep(ImageConversionStep.PreviewAndExport),
            () => CanOpenImagePreviewExportStep);
        SelectImageCommand = new RelayCommand(SelectImage, () => CanInteractWithImageWorkflow);
        AnalyzeImageCommand = new RelayCommand(AnalyzeImage, () => CanAnalyzeImage);
        ClearImageLogCommand = new RelayCommand(ClearImageLog, () => ImageLogs.Count > 0 && !IsImageExportRunning && !IsAnyModalOpen);
        ImageWizardBackCommand = new RelayCommand(
            MoveImageWizardBack,
            () => CanMoveImageWizardBack);
        ImageWizardNextCommand = new RelayCommand(
            MoveImageWizardNext,
            () => CanMoveImageWizardNext);
        ContinueWithImageConversionCommand = new RelayCommand(
            ContinueWithImageConversion,
            () => CanContinueWithImageConversion);
        ConvertImageCommand = new AsyncRelayCommand(
            ConvertImageAsync,
            () => CanConvertImage);
        ShowImageParallaxModelHelpCommand = new RelayCommand(
            ShowImageParallaxModelHelp,
            () => CanShowImageParallaxModelHelp);
        ExportStereoscopicImageCommand = new AsyncRelayCommand(
            ExportStereoscopicImageAsync,
            () => CanExportStereoscopicImage);
        OpenImageOutputFolderCommand = new RelayCommand(
            OpenImageOutputFolder,
            () => CanOpenImageOutputFolder);
        NewImageConversionCommand = new RelayCommand(
            StartNewImageConversion,
            () => CanStartNewImageConversion);
        OpenSettingsCommand = new RelayCommand(OpenSettings, () => CanOpenSettings);
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
            () => CanUseSettingsSystemStatusActions);
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
            () => CanUseSettingsSystemStatusActions);
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

        ResetMetricText();
        ResetPreviewMetricText();
        _themeService.Apply(_selectedTheme);
        _ = CleanStalePreviewFilesAsync();
        _ = RefreshEngineStatusAsync(logRefresh: true);
        AddVideoLogResolved(T(LocalizationKeys.VideoLogShellReady));
        AddImageLogResolved(T(LocalizationKeys.ImageLogWorkflowReady));
    }

    public string AppTitle => T(LocalizationKeys.AppTitle);

    public string ShellTaglineText => T(LocalizationKeys.ShellTagline);

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

    public string SidebarToggleText => T(
        IsSidebarExpanded ? LocalizationKeys.SidebarToggleCollapse : LocalizationKeys.SidebarToggleExpand);

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

    public string HomeNavigationText => T(LocalizationKeys.SidebarHome);

    public string ImageConversionNavigationText => T(LocalizationKeys.SidebarImageConversion);

    public string VideoConversionNavigationText => T(LocalizationKeys.SidebarVideoConversion);

    public string HomeTitleText => T(LocalizationKeys.HomeTitle);

    public string HomeDescriptionText => T(LocalizationKeys.HomeDescription);

    public string HomeVideoCardTitleText => T(LocalizationKeys.VideoHomeCardTitle);

    public string HomeVideoCardBodyText => T(LocalizationKeys.VideoHomeCardBody);

    public string HomeImageCardTitleText => T(LocalizationKeys.ImageHomeCardTitle);

    public string HomeImageCardBodyText => T(LocalizationKeys.ImageHomeCardBody);

    public string HomeSettingsCardTitleText => T(LocalizationKeys.HomeSettingsCardTitle);

    public string HomeSettingsCardBodyText => T(LocalizationKeys.HomeSettingsCardBody);

    public string HomeStatusSummaryText => T(LocalizationKeys.VideoHomeStatusSummary);

    public string HomeLocalOnlyBadgeText => T(LocalizationKeys.HomeLocalOnlyBadge);

    public string HomeVideoStatusText => T(LocalizationKeys.VideoHomeStatus);

    public string HomeImageStatusText => T(LocalizationKeys.ImageHomeStatus);

    public string HomeModelsStatusText => T(LocalizationKeys.HomeModelsStatus);

    public string OpenSectionText => T(LocalizationKeys.CommonOpen);

    public string ReadyNowText => T(LocalizationKeys.HomeReadyNow);

    public string ComingNextText => T(LocalizationKeys.HomeComingNext);

    public string ImageConversionTitleText => T(LocalizationKeys.ImageTitle);

    public string ImageConversionIntroText => T(LocalizationKeys.ImageIntro);

    public string ImageParallaxModeTitleText => T(LocalizationKeys.ImageWorkflowParallaxTitle);

    public string ImageParallaxModeBodyText => T(LocalizationKeys.ImageWorkflowParallaxDescription);

    public string Image3DOutputModeTitleText => T(LocalizationKeys.ImageWorkflowStereoTitle);

    public string Image3DOutputModeBodyText => T(LocalizationKeys.ImageWorkflowStereoDescription);

    public string DisabledPlaceholderText => T(LocalizationKeys.ImageDisabledPlaceholder);

    public string ImageSourcePanelTitleText => T(LocalizationKeys.ImageSourceTitle);

    public string ImageDepthPanelTitleText => T(LocalizationKeys.ImageParallaxDepthPanelTitle);

    public string ImageParallaxPreviewTitleText => T(LocalizationKeys.ImageParallaxPreviewTitle);

    public string ImageDepthMapGenerationText => T(LocalizationKeys.ImageParallaxDepthMapGeneration);

    public string ImageParameterPanelTitleText => T(LocalizationKeys.ImageParallaxMotionParametersTitle);

    public string ImageQuickSummaryTitleText => T(LocalizationKeys.ImageParallaxQuickSummaryTitle);

    public string ImageScaffoldLogTitleText => T(LocalizationKeys.ImageLogTitle);

    public string ImageScaffoldLogText => T(LocalizationKeys.ImageLogEmpty);

    public string ImageDepthParameterText => T(LocalizationKeys.ImageParallaxDepthParameter);

    public string ImageMotionParameterText => T(LocalizationKeys.ImageParallaxMotionParameter);

    public string ImageZoomParameterText => T(LocalizationKeys.ImageParallaxZoomParameter);

    public string ImageDirectionParameterText => T(LocalizationKeys.ImageParallaxDirectionParameter);

    public string ImageDurationParameterText => T(LocalizationKeys.ImageParallaxDurationParameter);

    public string ImageSmoothingParameterText => T(LocalizationKeys.ImageParallaxSmoothingParameter);

    public string ImageLayersParameterText => T(LocalizationKeys.ImageParallaxLayersParameter);

    public string ImagePreviewActionText => T(LocalizationKeys.ImageParallaxPreviewAction);

    public string ImageExportActionText => T(LocalizationKeys.CommonConvert);

    public string ImageConvertActionText => IsImageExportRunning
        ? T(LocalizationKeys.ImageProgressConverting)
        : T(LocalizationKeys.CommonConvert);

    public string ImageStereoExportActionText => ImageConvertActionText;

    public string ImageOpenOutputFolderActionText => T(LocalizationKeys.ModalOpenOutputFolder);

    public string ImageNewConversionActionText => T(LocalizationKeys.ImageResultNewConversion);

    public string ImageNoOutputYetText => T(LocalizationKeys.ImageOutputNoOutputYet);

    public string ImageGeneratedFilesText => _lastImageExportGeneratedFiles.Count == 0
        ? ImageNoOutputYetText
        : string.Join(Environment.NewLine, _lastImageExportGeneratedFiles.Select(Path.GetFileName));

    public string ImageLastExportedPathText => string.IsNullOrWhiteSpace(_lastImageExportPrimaryPath)
        ? ImageNoOutputYetText
        : _lastImageExportPrimaryPath;

    public string ImageExportProgressText => _imageExportProgressKey is not null
        ? T(_imageExportProgressKey)
        : _imageExportProgressEnglishText;

    public string ImageExportOverlayText => T(LocalizationKeys.ImageProgressConverting);

    public string ImageExportStatusText => IsImageExportRunning
        ? T(LocalizationKeys.ImageProgressConverting)
        : ImageExportProgressText;

    public int ImageExportProgressPercent => _imageExportProgressPercent;

    public Visibility ImageExportProgressVisibility => IsImageExportRunning ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ImageExportSuccessVisibility =>
        !IsImageExportRunning && HasCurrentImageConversionOutput
            ? Visibility.Visible
            : Visibility.Collapsed;

    public bool IsImageExportOutputOutdated => _isImageExportOutputOutdated;

    public string ImageExportOutdatedText => T(LocalizationKeys.ImageOutputOutdated);

    public Visibility ImageExportOutdatedVisibility =>
        !IsImageExportRunning && IsImageExportOutputOutdated
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ImageConvertButtonVisibility =>
        IsImageExportRunning || HasCurrentImageConversionOutput ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ImageStereoConvertButtonVisibility => ImageConvertButtonVisibility;

    public Visibility ImageOpenOutputFolderButtonVisibility =>
        IsImageExportRunning ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ImageNewConversionButtonVisibility =>
        IsImageExportRunning ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ImageExportFailureVisibility =>
        !IsImageExportRunning && !string.IsNullOrWhiteSpace(_lastImageExportErrorEnglishText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string ImageStereoPreviewImagePath =>
        HasCurrentImageConversionOutput
            ? _lastImageExportPrimaryPath
            : SelectedImagePath ?? string.Empty;

    public string ImageParallaxPreviewImagePath => SelectedImagePath ?? string.Empty;

    public bool IsImageParallaxVideoPreviewAvailable =>
        IsImageParallaxModeSelected &&
        HasCurrentImageConversionOutput &&
        !string.IsNullOrWhiteSpace(_lastImageExportPrimaryPath) &&
        string.Equals(
            Path.GetExtension(_lastImageExportPrimaryPath),
            ".mp4",
            StringComparison.OrdinalIgnoreCase) &&
        File.Exists(_lastImageExportPrimaryPath);

    public string ImageParallaxGeneratedVideoPath =>
        IsImageParallaxVideoPreviewAvailable ? _lastImageExportPrimaryPath : string.Empty;

    public Uri? ImageParallaxVideoMediaSource =>
        IsImageParallaxVideoPreviewAvailable
            ? new Uri(_lastImageExportPrimaryPath, UriKind.Absolute)
            : null;

    public Visibility ImageParallaxVideoPreviewVisibility =>
        IsImageParallaxVideoPreviewAvailable && !IsImageExportRunning
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ImageParallaxSourcePreviewVisibility =>
        ImageParallaxVideoPreviewVisibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;

    public string ImageParallaxVideoPreviewTitleText => T(LocalizationKeys.ImageParallaxGeneratedVideoTitle);

    public string ImageParallaxPreviewBadgeText => IsImageParallaxVideoPreviewAvailable
        ? ImageParallaxVideoPreviewTitleText
        : ImageParallaxModeTitleText;

    public string ImageExportErrorText =>
        string.IsNullOrWhiteSpace(_lastImageExportErrorEnglishText)
            ? string.Empty
            : _lastImageExportErrorEnglishText;

    public string ImageConvertDisabledReasonText
    {
        get
        {
            if (CanConvertImage)
            {
                return string.Empty;
            }

            if (IsImageExportRunning)
            {
                return T(LocalizationKeys.ImageReadinessRunning);
            }

            if (!HasSelectedImage || !HasImageMetadata)
            {
                return T(LocalizationKeys.ImageReadinessMissingImage);
            }

            if (!HasSelectedImageMode)
            {
                return T(LocalizationKeys.ImageReadinessMissingWorkflow);
            }

            if (!IsImageSetupValid)
            {
                return IsImageParallaxModeSelected
                    ? T(LocalizationKeys.ImageReadinessMissingParallaxSetup)
                    : T(LocalizationKeys.ImageReadinessMissingStereoSetup);
            }

            if (SelectedImageConversionStep != ImageConversionStep.PreviewAndExport)
            {
                return T(LocalizationKeys.ImageReadinessWrongStep);
            }

            if (!_hasEnteredImagePreviewExportStage)
            {
                return T(LocalizationKeys.ImageReadinessNotPrepared);
            }

            return IsImageParallaxModeSelected
                ? CreateImageParallaxExportReadinessText()
                : CreateImageStereoExportReadinessText();
        }
    }

    public string ImageStereoExportDisabledReasonText => ImageConvertDisabledReasonText;

    public Visibility ImageConvertDisabledReasonVisibility =>
        CanConvertImage || HasCurrentImageConversionOutput ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ImageStereoExportDisabledReasonVisibility => ImageConvertDisabledReasonVisibility;

    public string ImageResultParallaxTitleText => T(LocalizationKeys.ImageParallaxResultTitle);

    public string ImageExportOptionsTitleText => T(LocalizationKeys.ImageOutputOptionsTitle);

    public string ImageResultSummaryTitleText => T(LocalizationKeys.ImageOutputResultSummaryTitle);

    public string ImageResultSummaryText => T(LocalizationKeys.ImageParallaxResultSummary);

    public string ImageParallaxSummaryText => T(
        LocalizationKeys.ImageParallaxSummaryFormat,
        ("depthIntensity", GetOptionDisplayName(ParallaxDepthIntensityOptions, SelectedParallaxDepthIntensity)),
        ("motionDirection", GetOptionDisplayName(ParallaxMotionDirectionOptions, SelectedParallaxMotionDirection)),
        ("zoomAmplitude", GetOptionDisplayName(ParallaxZoomAmplitudeOptions, SelectedParallaxZoomAmplitude)),
        ("duration", GetOptionDisplayName(ParallaxDurationOptions, SelectedParallaxDuration)));

    public string ImageStereoPreviewTitleText => T(LocalizationKeys.ImageStereoPreviewTitle);

    public string ImageStereoControlsTitleText => T(LocalizationKeys.ImageStereoControlsTitle);

    public string ImageModelSelectionLabelText => T(LocalizationKeys.ImageModelSelectorLabel);

    public string ImageModelSelectionSharedNoteText => T(LocalizationKeys.ImageModelSelectionSharedNote);

    public string ImageStereoSummaryText => T(
        LocalizationKeys.ImageStereoSummaryFormat,
        ("outputFormat", SelectedStereoOutputFormatDisplayText));

    public string ImageStereoSeparationText => T(LocalizationKeys.ImageStereoSeparationSummary);

    public string ImageStereoConvergenceText => T(LocalizationKeys.ImageStereoConvergenceSummary);

    public string ImageStereoAnaglyphText => T(LocalizationKeys.ImageStereoAnaglyphSummary);

    public string ImageStereoResultTitleText => T(LocalizationKeys.ImageStereoResultTitle);

    public string ImageGeneratedFilesTitleText => T(LocalizationKeys.ImageOutputGeneratedFilesTitle);

    public string ImageOutputPanelTitleText => T(LocalizationKeys.ImageOutputPanelTitle);

    public string ImageComparisonTitleText => T(LocalizationKeys.ImageOutputComparisonTitle);

    public string ImageMp41080pBadgeText => T(LocalizationKeys.ImageBadgeMp41080p);

    public string ImageLoopFriendlyMotionBadgeText => T(LocalizationKeys.ImageBadgeLoopFriendlyMotion);

    public string ImageProjectMetadataBadgeText => T(LocalizationKeys.ImageBadgeProjectMetadata);

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

    public string ImageWorkflowSummaryText => T(
        LocalizationKeys.ImageWorkflowSummaryFormat,
        ("workflow", SelectedImageModeName));

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

    public string ImageModeSourceStepTitleText => T(LocalizationKeys.ImageStepSourceTitle);

    public string ImageSetupStepTitleText => T(LocalizationKeys.ImageStepSetupTitle);

    public string ImagePreviewExportStepTitleText => T(LocalizationKeys.ImageStepExportTitle);

    public string ImageSourceModeStepTitleText => T(LocalizationKeys.ImageStepSourceModeTitle);

    public string ImageModeSelectionTitleText => T(LocalizationKeys.ImageWorkflowTitle);

    public string ChangeImageWorkflowText => T(LocalizationKeys.ImageWorkflowChange);

    public string ImageModeSourceBodyText => T(LocalizationKeys.ImageSourceBody);

    public string ImageSourceAnalysisTitleText => T(LocalizationKeys.ImageSourceTitle);

    public string DropImageText => T(LocalizationKeys.ImageSourceDrop);

    public string ImageNoModeSetupHintText => T(LocalizationKeys.ImageSetupNoWorkflowHint);

    public string AnalyzeImageText => T(LocalizationKeys.ImageSourceReanalyzeButton);

    public string ImageAnalysisTitleText => T(LocalizationKeys.ImageMetadataAnalysisTitle);

    public string SelectedImageModeName =>
        SelectedImageConversionMode switch
        {
            ImageConversionMode.ParallaxPhoto => ImageParallaxModeTitleText,
            ImageConversionMode.StereoscopicImage => Image3DOutputModeTitleText,
            _ => T(LocalizationKeys.ImageWorkflowNone),
        };

    public string SelectedImageStepName => SelectedImageConversionStep switch
    {
        ImageConversionStep.ModeAndSource => ImageModeSourceStepTitleText,
        ImageConversionStep.Setup => ImageSetupStepTitleText,
        ImageConversionStep.PreviewAndExport => ImagePreviewExportStepTitleText,
        _ => ImageModeSourceStepTitleText,
    };

    public string ImageSummaryStatusTitleText => T(LocalizationKeys.ImageSummaryTitle);

    public string ImageSelectedModeSummaryText => T(
        LocalizationKeys.ImageSummaryModeFormat,
        ("mode", SelectedImageModeName));

    public string ImageCurrentStepSummaryText => T(
        LocalizationKeys.ImageStepCurrentFormat,
        ("step", SelectedImageStepName));

    public string ImageSupportedInputFormatsText => T(LocalizationKeys.ImageSummarySupportedInputs);

    public string ImagePlannedOutputFormatsText => IsImageParallaxModeSelected
        ? T(LocalizationKeys.ImageOutputPlannedParallax)
        : IsImageStereoModeSelected
            ? T(LocalizationKeys.ImageOutputPlannedStereoFormat, ("outputFormat", SelectedStereoOutputFormatDisplayText))
            : T(LocalizationKeys.ImageOutputPlannedChoose);

    public string ImageLocalModelReadinessNoteText => T(LocalizationKeys.ImageSetupLocalModelReadinessNote);

    public string ImageNotImplementedStateText => IsImageStereoModeSelected
        ? CreateImageStereoExportReadinessText()
        : CreateImageParallaxExportReadinessText();

    public string ImageActivityLogTitleText => T(LocalizationKeys.ImageLogTitle);

    public string ImageActivityLogText =>
        ImageLogs.Count == 0
            ? ImageScaffoldLogText
            : string.Join(Environment.NewLine, ImageLogs.Select(log => log.DisplayText));

    public string SelectImageText => T(LocalizationKeys.ImageSourceSelectButton);

    public string ImageSelectedTitleText => T(LocalizationKeys.ImageSourceSelectedTitle);

    public string NoImageSelectedText => T(LocalizationKeys.ImageSourceNoneSelected);

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

    public bool CanAnalyzeImage => HasSelectedImage && CanInteractWithImageWorkflow;

    public bool HasImageWorkflowPrerequisites => HasImageMetadata && HasSelectedImageMode;

    public bool CanOpenImageSetupStep => HasImageMetadata && CanUseImageStepNavigation;

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

    public bool CanOpenImagePreviewExportStep => IsImageSetupValid && CanUseImageStepNavigation;

    public bool CanMoveImageWizardBack =>
        CanUseImageStepNavigation &&
        SelectedImageConversionStep != ImageConversionStep.ModeAndSource;

    public bool CanMoveImageWizardNext => SelectedImageConversionStep switch
    {
        ImageConversionStep.ModeAndSource => CanUseImageStepNavigation && CanOpenImageSetupStep,
        ImageConversionStep.Setup => CanUseImageStepNavigation && CanOpenImagePreviewExportStep,
        _ => false,
    };

    public Visibility ImageWizardBackButtonVisibility =>
        CanMoveImageWizardBack ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ImageWizardNextButtonVisibility =>
        SelectedImageConversionStep == ImageConversionStep.PreviewAndExport ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ContinueWithImageConversionFooterVisibility =>
        SelectedImageConversionStep == ImageConversionStep.PreviewAndExport && !_hasEnteredImagePreviewExportStage
            ? Visibility.Visible
            : Visibility.Collapsed;

    public bool CanContinueWithImageConversion =>
        SelectedImageConversionStep == ImageConversionStep.PreviewAndExport &&
        !IsImageExportRunning &&
        IsImageSetupValid &&
        !_hasEnteredImagePreviewExportStage;

    public bool IsImageExportRunning
    {
        get => _isImageExportRunning;
        private set
        {
            if (SetProperty(ref _isImageExportRunning, value))
            {
                RaiseImageExportPropertiesChanged();
            }
        }
    }

    public bool CanExportStereoscopicImage =>
        IsImageStereoModeSelected &&
        CanConvertImage;

    public bool CanConvertImage =>
        !IsImageExportRunning &&
        !HasCurrentImageConversionOutput &&
        SelectedImageConversionStep == ImageConversionStep.PreviewAndExport &&
        _hasEnteredImagePreviewExportStage &&
        HasSelectedImage &&
        HasImageMetadata &&
        HasSelectedImageMode &&
        IsImageSetupValid &&
        (IsImageParallaxModeSelected
            ? ImageParallaxExportReadinessCanExport
            : ImageStereoExportReadinessCanExport);

    public bool CanOpenImageOutputFolder =>
        !IsImageExportRunning &&
        HasCurrentImageConversionOutput &&
        !string.IsNullOrWhiteSpace(_lastImageExportOutputDirectory) &&
        Directory.Exists(_lastImageExportOutputDirectory);

    public bool CanStartNewImageConversion =>
        !IsImageExportRunning &&
        (HasSelectedImage ||
            HasImageMetadata ||
            HasSelectedImageMode ||
            _hasEnteredImagePreviewExportStage ||
            !string.IsNullOrWhiteSpace(_lastImageExportPrimaryPath) ||
            !string.IsNullOrWhiteSpace(_lastImageExportErrorEnglishText) ||
            IsImageExportOutputOutdated);

    public bool ImageStereoExportReadinessCanExport =>
        EvaluateCurrentImageStereoExportReadiness()?.CanExport == true;

    public bool ImageParallaxExportReadinessCanExport =>
        EvaluateCurrentImageParallaxExportReadiness()?.CanExport == true;

    public string ImageWizardNextToolTipText => CanMoveImageWizardNext
        ? string.Empty
        : SelectedImageConversionStep == ImageConversionStep.ModeAndSource
            ? T(LocalizationKeys.ImageTooltipNextMissingImage)
            : T(LocalizationKeys.ImageTooltipNextMissingWorkflow);

    public string ContinueWithImageConversionText => T(LocalizationKeys.ImageActionPrepareConversion);

    public Visibility ImagePreviewExportStatusCardVisibility =>
        SelectedImageConversionStep != ImageConversionStep.ModeAndSource &&
        (_hasEnteredImagePreviewExportStage ||
            IsImageExportRunning ||
            HasCurrentImageConversionOutput)
            ? Visibility.Visible
            : Visibility.Collapsed;

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
        SelectedImageConversionStep != ImageConversionStep.ModeAndSource &&
        (_hasEnteredImagePreviewExportStage ||
            IsImageExportRunning ||
            HasCurrentImageConversionOutput)
            ? GridLength.Auto
            : new GridLength(0d);

    public Thickness ImageActivityLogCardMargin =>
        SelectedImageConversionStep != ImageConversionStep.ModeAndSource &&
        (_hasEnteredImagePreviewExportStage ||
            IsImageExportRunning ||
            HasCurrentImageConversionOutput)
            ? new Thickness(0d, 14d, 0d, 0d)
            : new Thickness(0d);

    public string ImagePreviewExportStatusTitleText => ImageOutputPanelTitleText;

    public string ImagePreviewExportStatusText => _hasEnteredImagePreviewExportStage
        ? CreateImagePreviewExportStatusText()
        : T(LocalizationKeys.ImageOutputStatusPrompt);

    public string ImageSupportedExtensionsText => T(LocalizationKeys.ImageSourceSupportedExtensions);

    public string ImageMetadataTitleText => T(LocalizationKeys.ImageMetadataTitle);

    public string ImageMetadataWidthText => LabelValue(LocalizationKeys.ImageMetadataWidth, _selectedImageMetadata?.WidthText);

    public string ImageMetadataHeightText => LabelValue(LocalizationKeys.ImageMetadataHeight, _selectedImageMetadata?.HeightText);

    public string ImageMetadataAspectRatioText => LabelValue(LocalizationKeys.ImageMetadataAspectRatio, _selectedImageMetadata?.AspectRatio);

    public string ImageMetadataFormatText => LabelValue(LocalizationKeys.ImageMetadataFormat, _selectedImageMetadata?.Format);

    public string ImageMetadataPixelFormatText => LabelValue(LocalizationKeys.ImageMetadataPixelFormat, _selectedImageMetadata?.PixelFormat);

    public string ImageMetadataFileSizeText => LabelValue(LocalizationKeys.ImageMetadataFileSize, _selectedImageMetadata?.FileSizeText);

    public string ImageMetadataSummaryText => HasImageMetadata
        ? $"{_selectedImageMetadata!.WidthText} x {_selectedImageMetadata.HeightText} · {_selectedImageMetadata.AspectRatio} · {_selectedImageMetadata.Format} · {_selectedImageMetadata.FileSizeText}"
        : T(LocalizationKeys.ImageMetadataNone);

    public string ImageSourceStatusText => HasImageMetadata
        ? T(LocalizationKeys.ImageSourceStatusAnalyzed)
        : T(LocalizationKeys.ImageSourceStatusSelectSupported);

    public string ImageOutputPlanText => IsImageParallaxModeSelected
        ? T(LocalizationKeys.ImageSetupOutputPlanParallax)
        : IsImageStereoModeSelected
            ? T(LocalizationKeys.ImageSetupOutputPlanStereo)
            : T(LocalizationKeys.ImageSetupOutputPlanChoose);

    public string ImageSetupSummaryText => IsImageParallaxModeSelected
        ? T(
            LocalizationKeys.ImageSetupSummaryParallaxFormat,
            ("depthIntensity", GetOptionDisplayName(ParallaxDepthIntensityOptions, SelectedParallaxDepthIntensity)),
            ("motionDirection", GetOptionDisplayName(ParallaxMotionDirectionOptions, SelectedParallaxMotionDirection)),
            ("zoomAmplitude", GetOptionDisplayName(ParallaxZoomAmplitudeOptions, SelectedParallaxZoomAmplitude)),
            ("duration", GetOptionDisplayName(ParallaxDurationOptions, SelectedParallaxDuration)))
        : IsImageStereoModeSelected
            ? T(
                LocalizationKeys.ImageSetupSummaryStereoFormat,
                ("outputFormat", SelectedStereoOutputFormatDisplayText),
                ("eyeSeparation", GetOptionDisplayName(StereoEyeSeparationOptions, SelectedStereoEyeSeparation)),
                ("convergence", GetOptionDisplayName(StereoConvergenceOptions, SelectedStereoConvergence)))
            : T(LocalizationKeys.ImageSetupSummaryNoWorkflow);

    public string ImageStereoReadinessSummaryTitleText => T(LocalizationKeys.ImageStereoReadinessSummaryTitle);

    public string ImageSelectedModelSummaryText => SelectedLocalModelCandidate is null
        ? T(LocalizationKeys.ImageModelSelectedNone)
        : T(
            LocalizationKeys.ImageModelSelectedFormat,
            ("model", SelectedLocalModelCandidate.DisplayName),
            ("iw3Model", SelectedLocalModelCandidate.Iw3DepthModelName ?? T(LocalizationKeys.ImageModelIw3Pending)));

    public string ImageExpectedOutputFileText => SelectedImagePath is null
        ? T(LocalizationKeys.ImageOutputExpectedFileMissing)
        : T(
            LocalizationKeys.ImageOutputExpectedFileFormat,
            ("fileName", Path.GetFileName(CreateExpectedImageOutputPath())));

    public string ImageExpectedOutputPathText => SelectedImagePath is null
        ? T(LocalizationKeys.ImageOutputExpectedPathMissing)
        : T(
            LocalizationKeys.ImageOutputExpectedPathFormat,
            ("path", CreateExpectedImageOutputPath()));

    public string ImageSaveLocationText => SelectedImagePath is null
        ? T(LocalizationKeys.ImageOutputSaveLocationMissing)
        : T(
            LocalizationKeys.ImageOutputSaveLocationFormat,
            ("path", GetDefaultImageExportDirectory(SelectedImagePath)));

    public string ImageSupportedStereoFormatsText
    {
        get
        {
            var supportedOptions = StereoOutputFormatOptions;
            if (supportedOptions.Count == 0)
            {
                return T(LocalizationKeys.ImageStereoSupportedFormatsNone);
            }

            var displayNames = string.Join(", ", supportedOptions.Select(option => option.DisplayName));
            return T(LocalizationKeys.ImageStereoSupportedFormatsFormat, ("formats", displayNames));
        }
    }

    public string ImageBundledIw3StereoNoteText => T(LocalizationKeys.ImageStereoBundledIw3Note);

    public string ImageParallaxQualityGuidanceText => T(LocalizationKeys.ImageParallaxQualityGuidance);

    public string ImageParallaxModelGuidanceText => SelectedLocalModelCandidate is null
        ? T(LocalizationKeys.ImageParallaxModelGuidanceNone)
        : T(LocalizationKeys.ImageParallaxModelGuidanceFormat, ("model", SelectedLocalModelCandidate.DisplayName));

    public string ImageDepthIntensityLabelText => T(LocalizationKeys.ImageParallaxDepthIntensityLabel);

    public string ImageMotionDirectionLabelText => T(LocalizationKeys.ImageParallaxMotionDirectionLabel);

    public string ImageZoomAmplitudeLabelText => T(LocalizationKeys.ImageParallaxZoomAmplitudeLabel);

    public string ImageDurationLabelText => T(LocalizationKeys.ImageParallaxDurationLabel);

    public string ImageSmoothingLabelText => T(LocalizationKeys.ImageParallaxSmoothingLabel);

    public string ImageLayerBehaviorLabelText => T(LocalizationKeys.ImageParallaxLayerBehaviorLabel);

    public string ImageStereoOutputFormatLabelText => T(LocalizationKeys.ImageStereoOutputFormatLabel);

    public string ImageStereoEyeSeparationLabelText => T(LocalizationKeys.ImageStereoEyeSeparationLabel);

    public string ImageStereoConvergenceLabelText => T(LocalizationKeys.ImageStereoConvergenceLabel);

    public string ImageStereoSwapEyesLabelText => T(LocalizationKeys.ImageStereoSwapEyesLabel);

    public string ImageStereoAnaglyphModeLabelText => T(LocalizationKeys.ImageStereoAnaglyphModeLabel);

    public IReadOnlyList<LocalizedOptionViewModel<string>> ParallaxDepthIntensityOptions =>
        _parallaxDepthIntensityOptions;

    public IReadOnlyList<LocalizedOptionViewModel<string>> ParallaxMotionDirectionOptions =>
        _parallaxMotionDirectionOptions;

    public IReadOnlyList<LocalizedOptionViewModel<string>> ParallaxZoomAmplitudeOptions =>
        _parallaxZoomAmplitudeOptions;

    public IReadOnlyList<LocalizedOptionViewModel<string>> ParallaxDurationOptions =>
        _parallaxDurationOptions;

    public IReadOnlyList<LocalizedOptionViewModel<string>> ParallaxSmoothingOptions =>
        _parallaxSmoothingOptions;

    public IReadOnlyList<LocalizedOptionViewModel<string>> ParallaxLayerBehaviorOptions =>
        _parallaxLayerBehaviorOptions;

    public IReadOnlyList<LocalizedOptionViewModel<ImageStereoOutputFormat>> StereoOutputFormatOptions =>
        _allStereoOutputFormatOptions
            .Where(option => IsImageStereoOutputFormatSelectable(option.Value))
            .ToArray();

    public IReadOnlyList<LocalizedOptionViewModel<string>> StereoEyeSeparationOptions =>
        _stereoEyeSeparationOptions;

    public IReadOnlyList<LocalizedOptionViewModel<string>> StereoConvergenceOptions =>
        _stereoConvergenceOptions;

    public IReadOnlyList<LocalizedOptionViewModel<string>> StereoAnaglyphModeOptions =>
        _stereoAnaglyphModeOptions;

    public string SelectedParallaxDepthIntensity
    {
        get => _selectedParallaxDepthIntensity;
        set => SetImageSetupString(
            ref _selectedParallaxDepthIntensity,
            value,
            LocalizationKeys.ImageParallaxDepthIntensityLabel,
            ParallaxDepthIntensityOptions);
    }

    public string SelectedParallaxMotionDirection
    {
        get => _selectedParallaxMotionDirection;
        set => SetImageSetupString(
            ref _selectedParallaxMotionDirection,
            value,
            LocalizationKeys.ImageParallaxMotionDirectionLabel,
            ParallaxMotionDirectionOptions);
    }

    public string SelectedParallaxZoomAmplitude
    {
        get => _selectedParallaxZoomAmplitude;
        set => SetImageSetupString(
            ref _selectedParallaxZoomAmplitude,
            value,
            LocalizationKeys.ImageParallaxZoomAmplitudeLabel,
            ParallaxZoomAmplitudeOptions);
    }

    public string SelectedParallaxDuration
    {
        get => _selectedParallaxDuration;
        set => SetImageSetupString(
            ref _selectedParallaxDuration,
            value,
            LocalizationKeys.ImageParallaxDurationLabel,
            ParallaxDurationOptions);
    }

    public string SelectedParallaxSmoothing
    {
        get => _selectedParallaxSmoothing;
        set => SetImageSetupString(
            ref _selectedParallaxSmoothing,
            value,
            LocalizationKeys.ImageParallaxSmoothingLabel,
            ParallaxSmoothingOptions);
    }

    public string SelectedParallaxLayerBehavior
    {
        get => _selectedParallaxLayerBehavior;
        set => SetImageSetupString(
            ref _selectedParallaxLayerBehavior,
            value,
            LocalizationKeys.ImageParallaxLayerBehaviorLabel,
            ParallaxLayerBehaviorOptions);
    }

    public ImageStereoOutputFormat SelectedStereoOutputFormat
    {
        get => _selectedStereoOutputFormat;
        set
        {
            var previous = _selectedStereoOutputFormat;
            if (SetProperty(ref _selectedStereoOutputFormat, value))
            {
                NormalizeSelectedStereoAnaglyphMode();
                ApplyImageSetupChanged(T(
                    LocalizationKeys.ImageLogStereoOutputFormatChangedFormat,
                    ("previous", GetStereoOutputFormatDisplayText(previous)),
                    ("next", SelectedStereoOutputFormatDisplayText)));
            }
        }
    }

    public LocalizedOptionViewModel<ImageStereoOutputFormat>? SelectedStereoOutputFormatOption
    {
        get => _allStereoOutputFormatOptions.FirstOrDefault(option => option.Value == SelectedStereoOutputFormat);
        set
        {
            if (value is not null)
            {
                SelectedStereoOutputFormat = value.Value;
            }
        }
    }

    public string SelectedStereoOutputFormatDisplayText =>
        GetStereoOutputFormatDisplayText(SelectedStereoOutputFormat);

    public bool IsAnaglyphOutputSelected => SelectedStereoOutputFormat == ImageStereoOutputFormat.Anaglyph;

    public Visibility ImageStereoAnaglyphModeVisibility =>
        IsAnaglyphOutputSelected ? Visibility.Visible : Visibility.Collapsed;

    public string SelectedStereoEyeSeparation
    {
        get => _selectedStereoEyeSeparation;
        set => SetImageSetupString(
            ref _selectedStereoEyeSeparation,
            value,
            LocalizationKeys.ImageStereoEyeSeparationLabel,
            StereoEyeSeparationOptions);
    }

    public string SelectedStereoConvergence
    {
        get => _selectedStereoConvergence;
        set => SetImageSetupString(
            ref _selectedStereoConvergence,
            value,
            LocalizationKeys.ImageStereoConvergenceLabel,
            StereoConvergenceOptions);
    }

    public bool ImageStereoSwapEyes
    {
        get => _imageStereoSwapEyes;
        set
        {
            var previous = _imageStereoSwapEyes;
            if (SetProperty(ref _imageStereoSwapEyes, value))
            {
                ApplyImageSetupChanged(T(
                    LocalizationKeys.ImageLogSwapEyesChangedFormat,
                    ("previous", GetToggleText(previous)),
                    ("next", GetToggleText(_imageStereoSwapEyes))));
            }
        }
    }

    public string SelectedStereoAnaglyphMode
    {
        get => _selectedStereoAnaglyphMode;
        set
        {
            var normalized = NormalizeStereoAnaglyphMode(value);
            var previous = _selectedStereoAnaglyphMode;
            if (SetProperty(ref _selectedStereoAnaglyphMode, normalized))
            {
                ApplyImageSetupChanged(T(
                    LocalizationKeys.ImageLogAnaglyphModeChangedFormat,
                    ("previous", GetOptionDisplayName(StereoAnaglyphModeOptions, previous)),
                    ("next", GetOptionDisplayName(StereoAnaglyphModeOptions, normalized))));
            }
        }
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

    public IReadOnlyList<AppLanguageOptionViewModel> LanguageOptions { get; private set; } = [];

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            _localizationService.SetLanguage(value);
            var activeLanguageCode = _localizationService.ActiveLanguageCode;
            if (SetProperty(ref _selectedLanguage, activeLanguageCode))
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
                AddLogResolved(T(
                    LocalizationKeys.ActivityLogLanguageSelectedFormat,
                    ("language", GetSelectedLanguageLogDisplayName(activeLanguageCode))));
            }
        }
    }

    public IReadOnlyList<string> ThemeOptions { get; } = ["Dark", "Light"];

    public IReadOnlyList<LocalizedOptionViewModel<SettingsSection>> SettingsSectionOptions =>
    [
        CreateSettingsSectionOption(SettingsSection.VisualSettings, LocalizationKeys.SettingsAppearanceTitle),
        CreateSettingsSectionOption(SettingsSection.Models, LocalizationKeys.SettingsModelsTitle),
        CreateSettingsSectionOption(SettingsSection.ToolsEngine, LocalizationKeys.SettingsToolsEngineTitle),
        CreateSettingsSectionOption(SettingsSection.LogsDiagnostics, LocalizationKeys.SettingsLogsDiagnosticsTitle),
        new(SettingsSection.AboutLicenses, AboutLicensesSettingsTitleText, AboutLicensesSettingsTitleText),
    ];

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                ApplyUiOnlyRefresh(() => _themeService.Apply(value));
                AddLogResolved(T(
                    LocalizationKeys.ActivityLogThemeSelectedFormat,
                    ("theme", GetSelectedThemeLogDisplayName(value))));
            }
        }
    }

    public string SubtitleText => T(LocalizationKeys.VideoSubtitle);

    public string LanguageLabel => T(LocalizationKeys.SettingsLanguageLabel);

    public string ThemeLabel => T(LocalizationKeys.SettingsAppearanceTheme);

    public string SettingsText => T(LocalizationKeys.SidebarSettings);

    public string SettingsTitleText => T(LocalizationKeys.SettingsTitle);

    public string SettingsSideMenuTitleText => T(LocalizationKeys.SettingsTitle);

    public string WindowMinimizeToolTipText => T(LocalizationKeys.CommonMinimize);

    public string WindowMaximizeToolTipText => T(LocalizationKeys.CommonMaximize);

    public string WindowRestoreToolTipText => T(LocalizationKeys.CommonRestore);

    public string WindowCloseToolTipText => T(LocalizationKeys.CommonClose);

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

    public string VisualSettingsTitleText => T(LocalizationKeys.SettingsAppearanceTitle);

    public string ModelsSettingsTitleText => T(LocalizationKeys.SettingsModelsTitle);

    public string ToolsEngineSettingsTitleText => T(LocalizationKeys.SettingsToolsEngineTitle);

    public string LogsDiagnosticsSettingsTitleText => T(LocalizationKeys.SettingsLogsDiagnosticsTitle);

    public string AboutLicensesSettingsTitleText =>
        $"{T(LocalizationKeys.SettingsAboutTitle)} / {T(LocalizationKeys.SettingsLicensesTitle)}";

    public string ModelsSettingsIntroText => T(LocalizationKeys.SettingsModelsIntro);

    public string ToolsEngineSettingsIntroText => T(LocalizationKeys.SettingsToolsEngineIntro);

    public string LogsDiagnosticsSettingsIntroText => T(LocalizationKeys.SettingsLogsDiagnosticsIntro);

    public string AboutLicensesText => T(
        LocalizationKeys.SettingsAboutLicensesTextFormat,
        ("version", GetCurrentV3dfyVersion()));

    public string AboutModelNoticesTitleText => T(LocalizationKeys.SettingsAboutModelNoticesTitle);

    public string AboutModelNoticesText => CreateModelLicenseNoticeSummaryText();

    public string SourceAndAnalysisStepTitle => T(LocalizationKeys.VideoStepSourceTitle);

    public string ThreeDSetupStepTitle => T(LocalizationKeys.VideoStepSetupTitle);

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

    public string WizardBackText => T(LocalizationKeys.CommonBack);

    public string WizardNextText => T(LocalizationKeys.CommonNext);

    public string WizardNextToolTipText => CanMoveWizardNext
        ? string.Empty
        : SelectedWizardStepIndex switch
        {
            ConversionWorkflowState.SourceAndAnalysisStepIndex => T(LocalizationKeys.VideoTooltipNextAnalyze),
            ConversionWorkflowState.ThreeDSetupStepIndex => T(LocalizationKeys.VideoTooltipNextSetup),
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

    public string SelectSourceTitle => T(LocalizationKeys.VideoSourceTitle);

    public string DropVideoText => T(LocalizationKeys.VideoSourceDrop);

    public string NoVideoSelectedText => T(LocalizationKeys.VideoSourceNoneSelected);

    public string SourceAnalysisEmptyHintText => T(LocalizationKeys.VideoSourceEmptyHint);

    public Visibility SourceAnalysisEmptyHintVisibility =>
        HasSelectedVideo ? Visibility.Collapsed : Visibility.Visible;

    public string SelectVideoText => T(LocalizationKeys.VideoSourceSelectButton);

    public string AnalyzeText => T(LocalizationKeys.VideoSourceAnalyzeButton);

    public IReadOnlyList<LocalizedOptionViewModel<TargetDevicePreset>> OutputPresetOptions => _outputPresetOptions;

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
                AddVideoLogResolved(T(
                    LocalizationKeys.VideoLogOutputProfileChangedFormat,
                    ("profile", TargetPresetName(value))));
            }
        }
    }

    public IReadOnlyList<LocalizedOptionViewModel<OutputContainer>> OutputContainerOptions => _outputContainerOptions;

    public IReadOnlyList<LocalizedOptionViewModel<AiQualityPreset>> QualityPresetOptions => _qualityPresetOptions;

    public IReadOnlyList<LocalizedOptionViewModel<ThreeDIntensity>> ThreeDIntensityOptions => _threeDIntensityOptions;

    public IReadOnlyList<LocalizedOptionViewModel<ThreeDOutputFormat>> ThreeDOutputFormatOptions => _threeDOutputFormatOptions;

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
                PlanOptionChanged(T(
                    LocalizationKeys.VideoLogOutputContainerChangedFormat,
                    ("container", value)));
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
                PlanOptionChanged(T(
                    LocalizationKeys.VideoLogQualityChangedFormat,
                    ("quality", QualityPresetText(value))));
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
                PlanOptionChanged(T(
                    LocalizationKeys.VideoLogIntensityChangedFormat,
                    ("intensity", ThreeDIntensityText(value))));
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
                PlanOptionChanged(T(
                    LocalizationKeys.VideoLogLayoutChangedFormat,
                    ("layout", ThreeDOutputFormatText(value))));
            }
        }
    }

    public string CreateLgCompatibilityCopyText => T(LocalizationKeys.VideoLgCompatibilityCopyCreate);

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
                        ? T(LocalizationKeys.VideoLogLgCopyEnabled)
                        : T(LocalizationKeys.VideoLogLgCopyDisabled),
                    affectsPreview: false);
            }
        }
    }

    public string PreferLgCompatibilityCopyWhenOpeningText => T(LocalizationKeys.VideoLgCompatibilityCopyPrefer);

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
                        ? T(LocalizationKeys.VideoLogLgCopyPreferred)
                        : T(LocalizationKeys.VideoLogPrimaryOutputPreferred),
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

    public string LgCompatibilityCopyExplanationText => T(LocalizationKeys.VideoLgCompatibilityCopyExplanation);

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

    public string VideoAnalysisTitle => T(LocalizationKeys.VideoAnalysisTitle);

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
        ? T(LocalizationKeys.ModelPackImportingAction)
        : T(LocalizationKeys.ModelPackImportAction);

    public string ModelPackImportInstructionText => T(LocalizationKeys.ModelPackInstruction);

    public string ModelPackImportStatusText =>
        string.IsNullOrWhiteSpace(_modelPackImportStatusEnglishText)
            ? T(LocalizationKeys.ModelPackStatusNotRun)
            : _modelPackImportStatusEnglishText;

    public string LastModelPackImportSummary => _lastModelPackImportSummaryEnglishText;

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

    public string GlobalBusyText =>
        string.IsNullOrWhiteSpace(_globalBusyEnglishText) ? T(LocalizationKeys.CommonLoading) : _globalBusyEnglishText;

    public string AnalysisStatusText => IsAnalyzing
        ? T(LocalizationKeys.VideoAnalysisStatusAnalyzing)
        : _analysis is null
            ? T(LocalizationKeys.VideoAnalysisStatusNone)
            : T(LocalizationKeys.VideoAnalysisStatusCompleted);

    public string VideoAnalysisPendingStatusText => IsAnalyzing
        ? T(LocalizationKeys.VideoAnalysisPendingAnalyzing)
        : T(LocalizationKeys.VideoAnalysisPendingSelected);

    public Visibility VideoAnalysisSectionVisibility =>
        HasSelectedVideo ? Visibility.Visible : Visibility.Collapsed;

    public Visibility VideoAnalysisPendingStatusVisibility =>
        HasSelectedVideo && _analysis is null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility VideoAnalysisResultsVisibility =>
        _analysis is null ? Visibility.Collapsed : Visibility.Visible;

    public string AnalysisDurationText => LabelValue(
        LocalizationKeys.VideoAnalysisDuration,
        _analysis?.File.Duration is { } duration
            ? duration.ToString(@"hh\:mm\:ss")
            : null);

    public string AnalysisResolutionText => LabelValue(
        LocalizationKeys.VideoAnalysisResolution,
        _analysis?.Video is { Width: { } width, Height: { } height }
            ? $"{width}x{height}"
            : null);

    public string AnalysisFpsText => LabelValue(
        LocalizationKeys.VideoAnalysisFps,
        _analysis?.Video?.FrameRate?.ToString("0.###"));

    public string AnalysisCodecText => LabelValue(
        LocalizationKeys.VideoAnalysisCodec,
        _analysis?.Video?.CodecName);

    public string AnalysisContainerText => LabelValue(
        LocalizationKeys.VideoAnalysisContainer,
        _analysis?.File.FormatName);

    public string AnalysisAudioStreamsText => LabelValue(
        LocalizationKeys.VideoAnalysisAudioStreams,
        _analysis?.AudioStreams.Count.ToString());

    public string AnalysisSubtitleStreamsText => LabelValue(
        LocalizationKeys.VideoAnalysisSubtitleStreams,
        _analysis?.SubtitleStreams.Count.ToString());

    public string AnalysisHdrText => LabelValue(
        LocalizationKeys.VideoAnalysisHdr,
        _analysis is null
            ? null
            : _analysis.Video?.IsHdr == true
                ? T(LocalizationKeys.VideoAnalysisHdrDetected)
                : T(LocalizationKeys.VideoAnalysisHdrNotDetected));

    public string AnalysisCompatibilityText => _analysis?.Video is
        { Width: { } width, Height: { } height } &&
        (width > SelectedOutputPreset.Recommendation.Width ||
         height > SelectedOutputPreset.Recommendation.Height)
            ? T(LocalizationKeys.VideoAnalysisCompatibilityHighResolution)
            : string.Empty;

    public string RecommendedSetupTitle => T(LocalizationKeys.VideoRecommendationTitle);

    public bool CanOpenRecommendedSetupTab => _workflowState.CanOpenRecommendedSetupTab;

    public string RecommendedSetupStatusText => _conversionRecommendation is null
        ? T(LocalizationKeys.VideoRecommendationStatusNone)
        : T(
            LocalizationKeys.VideoRecommendationStatusForFormat,
            ("profile", TargetPresetName(SelectedOutputPreset)));

    public string RecommendedOutputContainerText => LabelValue(
        LocalizationKeys.VideoRecommendationOutputContainer,
        _conversionRecommendation?.OutputContainer.ToString());

    public string RecommendedVideoCodecText => LabelValue(
        LocalizationKeys.VideoRecommendationVideoCodec,
        _conversionRecommendation?.VideoCodec);

    public string RecommendedAudioCodecText => LabelValue(
        LocalizationKeys.VideoRecommendationAudio,
        _conversionRecommendation?.AudioCodec);

    public string RecommendedResolutionText => LabelValue(
        LocalizationKeys.VideoRecommendationResolution,
        _conversionRecommendation is null
            ? null
            : $"{_conversionRecommendation.Width}x{_conversionRecommendation.Height}");

    public string RecommendedThreeDLayoutText => LabelValue(
        LocalizationKeys.VideoRecommendationThreeDLayout,
        _conversionRecommendation?.ThreeDOutputFormat == ThreeDOutputFormat.HalfTopBottom
            ? ThreeDOutputFormatText(ThreeDOutputFormat.HalfTopBottom)
            : _conversionRecommendation?.ThreeDOutputFormat is { } outputFormat
                ? ThreeDOutputFormatText(outputFormat)
                : null);

    public string RecommendedQualityText => LabelValue(
        LocalizationKeys.VideoRecommendationQuality,
        _conversionRecommendation is null
            ? null
            : QualityPresetText(_conversionRecommendation.QualityPreset));

    public string RecommendedIntensityText => LabelValue(
        LocalizationKeys.VideoRecommendationIntensity,
        _conversionRecommendation is null
            ? null
            : ThreeDIntensityText(_conversionRecommendation.Intensity));

    public string RecommendedNotesTitle => T(LocalizationKeys.VideoRecommendationNotesTitle);

    public string RecommendedNotesText => _conversionRecommendation is null
        ? "-"
        : _conversionRecommendation.CompatibilityIssues.Count == 0
            ? T(LocalizationKeys.VideoRecommendationNoWarnings)
            : string.Join(
                Environment.NewLine,
                _conversionRecommendation.CompatibilityIssues.Select(issue =>
                    $"- {LocalizeRecommendationCompatibilityIssue(issue)}"));

    public string ConversionPlanTitle => T(LocalizationKeys.VideoConversionPlanTitle);

    public bool CanOpenConversionPlanTab => _workflowState.CanOpenConversionPlanTab;

    public string PlanOptionsTitle => T(LocalizationKeys.VideoSetupPlanOptionsTitle);

    public string OutputContainerOptionLabel => T(LocalizationKeys.VideoSetupOutputContainerLabel);

    public string QualityOptionLabel => T(LocalizationKeys.VideoSetupQualityLabel);

    public string ThreeDIntensityOptionLabel => T(LocalizationKeys.VideoSetupThreeDIntensityLabel);

    public string ThreeDOutputFormatOptionLabel => T(LocalizationKeys.VideoSetupThreeDOutputFormatLabel);

    public string OutputLocationTitle => T(LocalizationKeys.VideoSetupOutputLocationTitle);

    public string LocalModelSelectionLabel => T(LocalizationKeys.VideoModelSelectorLabel);

    public bool HasLocalModelSelectionCandidates => LocalModelCandidates.Count > 0;

    public IReadOnlyList<LocalModelSelectionCandidate> ImageParallaxLocalModelCandidates =>
        LocalModelCandidates
            .Where(IsImageParallaxCompatibleCandidate)
            .ToArray();

    public bool HasImageParallaxLocalModelCandidates =>
        ImageParallaxLocalModelCandidates.Count > 0;

    public bool ImageModelSelectorEnabled =>
        HasLocalModelSelectionCandidates &&
        ImageSetupControlsEnabled;

    public bool ImageParallaxModelSelectorEnabled =>
        HasImageParallaxLocalModelCandidates &&
        ImageSetupControlsEnabled;

    public bool CanShowModelHelp =>
        HasLocalModelSelectionCandidates &&
        !IsAnyModalOpen &&
        !IsImageExportRunning &&
        !IsConversionRunning &&
        !IsPreviewGenerating &&
        !IsModelPackImportRunning;

    public bool CanShowImageParallaxModelHelp =>
        HasImageParallaxLocalModelCandidates &&
        !IsAnyModalOpen &&
        !IsImageExportRunning &&
        !IsConversionRunning &&
        !IsPreviewGenerating &&
        !IsModelPackImportRunning;

    public bool CanUseShellNavigation =>
        !IsAnyModalOpen &&
        !IsImageExportRunning;

    public bool ShellToolTipsEnabled => !IsAnyModalOpen;

    public bool CanOpenSettings =>
        CanUseShellNavigation &&
        !IsConversionRunning &&
        !IsPreviewGenerating &&
        !IsModelPackImportRunning;

    public bool CanInteractWithImageWorkflow =>
        !IsAnyModalOpen &&
        !IsImageExportRunning;

    public bool IsImageWorkflowLockedByConversion => IsImageExportRunning;

    public bool ImageSetupControlsEnabled =>
        !IsAnyModalOpen &&
        !IsImageExportRunning;

    public bool ImageWorkflowCardsEnabled =>
        !IsAnyModalOpen &&
        !IsImageExportRunning;

    public bool CanUseImageStepNavigation =>
        !IsAnyModalOpen &&
        !IsImageExportRunning;

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
            ? T(LocalizationKeys.VideoModelStatusUnmapped)
            : T(LocalizationKeys.VideoModelStatusNone)
        : T(
            LocalizationKeys.VideoModelStatusSelectedFormat,
            ("model", SelectedLocalModelCandidate.DisplayName),
            ("iw3Model", SelectedLocalModelCandidate.Iw3DepthModelName ?? "-"));

    public string SelectedModelGuidanceText => CreateSelectedModelGuidanceText();

    public string PresetGuidanceText => T(
        LocalizationKeys.VideoEstimatePresetGuidanceFormat,
        ("bestFor", TargetPresetBestFor(SelectedOutputPreset)));

    public string EstimatedConversionTimeText
    {
        get
        {
            var estimate = CreateCurrentConversionTimeEstimate();
            if (!estimate.IsAvailable)
            {
                return T(LocalizationKeys.VideoEstimateTimeUnavailable);
            }

            return T(
                LocalizationKeys.VideoEstimateTimeFormat,
                ("range", FormatEstimateRange(estimate.Low, estimate.High)));
        }
    }

    public string EstimateConfidenceText
    {
        get
        {
            var estimate = CreateCurrentConversionTimeEstimate();
            return T(
                LocalizationKeys.VideoEstimateConfidenceFormat,
                ("confidence", ConfidenceText(estimate.Confidence)));
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
            return T(LocalizationKeys.VideoEstimateBasisPrefix) + string.Join(", ", basis);
        }
    }

    public string EstimatedOutputSizeText
    {
        get
        {
            var estimate = CreateCurrentOutputSizeEstimate();
            if (!estimate.IsAvailable)
            {
                return T(LocalizationKeys.VideoEstimateOutputSizeUnavailable);
            }

            return T(
                LocalizationKeys.VideoEstimateOutputSizeFormat,
                ("range", FormatByteRange(estimate.LowBytes, estimate.HighBytes)));
        }
    }

    public string RecommendedFreeSpaceText
    {
        get
        {
            var estimate = CreateCurrentOutputSizeEstimate();
            if (!estimate.IsAvailable)
            {
                return T(LocalizationKeys.VideoEstimateFreeSpaceUnavailable);
            }

            return T(
                LocalizationKeys.VideoEstimateFreeSpaceFormat,
                ("space", FormatBytes(estimate.RecommendedFreeBytes)));
        }
    }

    public string PerformanceHistoryPrivacyText => T(LocalizationKeys.VideoEstimatePrivacy);

    public string CompactEstimateTimeConfidenceText
    {
        get
        {
            var estimate = CreateCurrentConversionTimeEstimate();
            var estimateText = estimate.IsAvailable
                ? FormatEstimateRange(estimate.Low, estimate.High)
                : T(LocalizationKeys.VideoEstimateNotEnoughInfo);
            return T(
                LocalizationKeys.VideoEstimateCompactTimeConfidenceFormat,
                ("time", estimateText),
                ("confidence", ConfidenceText(estimate.Confidence)));
        }
    }

    public string CompactOutputSizeFreeSpaceText
    {
        get
        {
            var estimate = CreateCurrentOutputSizeEstimate();
            if (!estimate.IsAvailable)
            {
                return T(LocalizationKeys.VideoEstimateCompactOutputUnavailable);
            }

            return T(
                LocalizationKeys.VideoEstimateCompactOutputFormat,
                ("range", FormatByteRange(estimate.LowBytes, estimate.HighBytes)),
                ("space", FormatBytes(estimate.RecommendedFreeBytes)));
        }
    }

    public string CompactSelectedModelGuidanceText
    {
        get
        {
            if (SelectedLocalModelCandidate is null)
            {
                return T(LocalizationKeys.VideoEstimateCompactModelDefault);
            }

            var entry = FindRegistryEntry(SelectedLocalModelCandidate);
            var guidance = _modelGuidanceService.Create(
                SelectedLocalModelCandidate.MappingKey,
                SelectedLocalModelCandidate.Iw3DepthModelName,
                SelectedLocalModelCandidate.DisplayName,
                entry?.IsEmbeddedBase == true);
            return T(
                LocalizationKeys.VideoEstimateCompactModelFormat,
                ("headline", LocalizeModelGuidanceHeadline(SelectedLocalModelCandidate, entry, guidance)),
                ("speed", LocalizeModelGuidanceSpeed(guidance.EnglishSpeed)),
                ("quality", LocalizeModelGuidanceQuality(guidance.EnglishQuality)));
        }
    }

    public string CompactPresetGuidanceText => T(
        LocalizationKeys.VideoEstimateCompactPresetFormat,
        ("bestFor", TargetPresetBestFor(SelectedOutputPreset)));

    public string EstimateDetailsTitleText => T(LocalizationKeys.VideoEstimateDetailsTitle);

    public string GuidanceDetailsTitleText => T(LocalizationKeys.VideoGuidanceDetailsTitle);

    public bool HasUnmappedLocalModelCandidates =>
        Iw3DepthModelMapper.GetUnmappedCandidates(
            _dependencyHealth?.ModelInventory.SelectionCandidates ?? []).Count > 0;

    public string ModelHelpButtonText => "?";

    public string ModelHelpButtonToolTipText => T(LocalizationKeys.VideoModelHelpTooltip);

    public string ImageParallaxModelHelpButtonToolTipText => T(LocalizationKeys.ImageModelHelpTooltip);

    public string ModelHelpTitleText => _isImageParallaxModelHelpContext
        ? T(LocalizationKeys.ImageModelHelpTitle)
        : T(LocalizationKeys.VideoModelHelpTitle);

    public string ModelHelpIntroText => _isImageParallaxModelHelpContext
        ? T(LocalizationKeys.ImageModelHelpDescription)
        : T(LocalizationKeys.VideoModelHelpDescription);

    public string ModelHelpModelHeaderText => T(LocalizationKeys.VideoModelHelpModel);

    public string ModelHelpPurposeHeaderText => T(LocalizationKeys.VideoModelHelpPurpose);

    public string ModelHelpUseHeaderText => T(LocalizationKeys.VideoModelHelpUse);

    public string ModelHelpSceneHeaderText => T(LocalizationKeys.VideoModelHelpScene);

    public string ModelHelpDepthHeaderText => T(LocalizationKeys.VideoModelHelpDepth);

    public string ModelHelpSizePerformanceHeaderText => T(LocalizationKeys.VideoModelHelpSizePerformance);

    public IReadOnlyList<ModelHelpRow> ModelHelpRows => _isImageParallaxModelHelpContext
        ? CreateImageParallaxModelHelpRows()
        : CreateModelHelpRows();

    public string ViewModelsText => T(LocalizationKeys.ModalViewModels);

    public string ViewModelsToolTipText => T(LocalizationKeys.VideoModelViewModelsTooltip);

    public string ModelInventoryTitleText => T(LocalizationKeys.ModelInventoryTitle);

    public string ModelInventoryIntroText => T(LocalizationKeys.ModelInventoryIntro);

    public string ModelInventoryFolderLabelText => T(LocalizationKeys.ModelInventoryFolderLabel);

    public string ModelInventoryFolderPathText => GetCurrentModelInventory().ModelsDirectory;

    public string SelectableModelsSectionTitleText => T(LocalizationKeys.ModelInventorySelectableSectionTitle);

    public string SelectableModelsInventoryText => CreateSelectableModelsInventoryText();

    public string SelectableModelNameHeaderText => T(LocalizationKeys.ModelInventoryHeaderModel);

    public string SelectableModelIw3HeaderText => T(LocalizationKeys.ModelInventoryHeaderIw3DepthModel);

    public string SelectableModelCheckpointHeaderText => T(LocalizationKeys.ModelInventoryHeaderCheckpoint);

    public string SelectableModelTypeHeaderText => T(LocalizationKeys.ModelInventoryHeaderType);

    public string SelectableModelSourceHeaderText => T(LocalizationKeys.ModelInventoryHeaderSource);

    public IReadOnlyList<SelectableModelInventoryRow> SelectableModelInventoryRows =>
        CreateSelectableModelInventoryRows();

    public Visibility SettingsSelectableModelsTableVisibility =>
        SelectableModelInventoryRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SettingsSelectableModelsEmptyVisibility =>
        SelectableModelInventoryRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public string SettingsSelectableModelsEmptyText => T(LocalizationKeys.ModelInventorySelectableEmpty);

    public string DiagnosticModelsSectionTitleText => T(LocalizationKeys.ModelInventoryDiagnosticSectionTitle);

    public string DiagnosticModelsInventoryText => CreateDiagnosticModelsInventoryText();

    public string RuntimeDependenciesSectionTitleText => T(LocalizationKeys.ModelInventoryRuntimeDependenciesTitle);

    public string RuntimeDependenciesInventoryText => CreateRuntimeDependenciesInventoryText();

    public string ModelInventoryActionsTitleText => T(LocalizationKeys.ModelInventoryActionsTitle);

    public string OpenModelsFolderText => T(LocalizationKeys.ModelInventoryOpenModelsFolder);

    public string OutputPathLabel => T(LocalizationKeys.VideoOutputPathLabel);

    public string BrowseOutputFolderText => T(LocalizationKeys.CommonBrowse);

    public string ResetOutputPathText => T(LocalizationKeys.VideoOutputResetPath);

    public string OpenOutputWhenFinishedText => T(LocalizationKeys.VideoOutputOpenWhenFinished);

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
        ? T(LocalizationKeys.VideoConversionPlanStatusNone)
        : _conversionPlan.IsDryRun
            ? T(LocalizationKeys.VideoConversionPlanStatusDryRun)
            : T(LocalizationKeys.VideoConversionPlanStatusReady);

    public string OutputProfileDisplayText => _planOptionState.HasCustomizedOptions
        ? T(
            LocalizationKeys.VideoConversionPlanOutputProfileCustomFormat,
            ("profile", TargetPresetName(SelectedOutputPreset)))
        : TargetPresetName(SelectedOutputPreset);

    public string ConversionPlanPresetText => T(
        LocalizationKeys.VideoConversionPlanPresetFormat,
        ("profile", OutputProfileDisplayText));

    public string ConversionPlanLocalModelText => _conversionPlan?.SelectedLocalModel is null
        ? T(LocalizationKeys.VideoConversionPlanLocalModelNone)
        : T(
            LocalizationKeys.VideoConversionPlanLocalModelFormat,
            ("model", IsSpanish ? GetSpanishModelDisplayName(_conversionPlan.SelectedLocalModel) : _conversionPlan.SelectedLocalModel.DisplayName),
            ("relativePath", _conversionPlan.SelectedLocalModel.RelativePath),
            ("source", IsSpanish ? _conversionPlan.SelectedLocalModel.SpanishSourceText : _conversionPlan.SelectedLocalModel.EnglishSourceText),
            ("iw3Model", _conversionPlan.SelectedLocalModel.Iw3DepthModelName ?? "-"));

    public string ConversionPlanOutputPathText => LabelValue(
        LocalizationKeys.VideoOutputPrimary,
        _conversionPlan?.SuggestedOutputPath);

    public string ConversionPlanLgCompatibilityCopyPathText =>
        LabelValue(
            LocalizationKeys.VideoOutputLgCompatibilityCopy,
            GetLgCompatibilityCopyPath());

    public string ConversionPlanOutputFormatText => LabelValue(
        LocalizationKeys.VideoOutputFormat,
        _conversionPlan is null
            ? null
            : $"{_conversionPlan.OutputContainer} / {_conversionPlan.VideoCodec} / {_conversionPlan.AudioCodec}");

    public string ConversionPlanResolutionText => LabelValue(
        LocalizationKeys.VideoOutputTargetResolution,
        _conversionPlan is null
            ? null
            : $"{_conversionPlan.Width}x{_conversionPlan.Height}");

    public string ConversionPlanThreeDLayoutText => LabelValue(
        LocalizationKeys.VideoSetupThreeDOutputFormatLabel,
        _conversionPlan is null
            ? null
            : ThreeDOutputFormatText(_conversionPlan.ThreeDOutputFormat));

    public string ConversionPlanQualityText => LabelValue(
        LocalizationKeys.VideoSetupQualityLabel,
        _conversionPlan is null
            ? null
            : QualityPresetText(_conversionPlan.QualityPreset));

    public string ConversionPlanIntensityText => LabelValue(
        LocalizationKeys.VideoSetupThreeDIntensityLabel,
        _conversionPlan is null
            ? null
            : ThreeDIntensityText(_conversionPlan.Intensity));

    public string ConversionPlanDryRunReasonText => _conversionPlan?.DryRunReason switch
    {
        ConversionDryRunReason.MissingLocalAiBundle => T(LocalizationKeys.VideoConversionPlanDryRunMissingLocalAiBundle),
        ConversionDryRunReason.MissingRequiredTools => T(LocalizationKeys.VideoConversionPlanDryRunMissingRequiredTools),
        _ => string.Empty,
    };

    public string ConversionPlanStepsTitle => T(LocalizationKeys.VideoConversionPlanStepsTitle);

    public string ConversionPlanStepsText => _conversionPlan is null
        ? "-"
        : string.Join(
            Environment.NewLine,
            _conversionPlan.Steps.Select((step, index) =>
                $"{index + 1}. {LocalizeConversionPlanStep(step)}"));

    public string ConversionPlanCommandPreviewTitle => T(LocalizationKeys.VideoConversionPlanCommandPreviewTitle);

    public string ConversionPlanCommandPreviewText => _conversionPlan?.CommandPreview ?? "-";

    public string ConversionPlanTechnicalDetailsTitleText => T(LocalizationKeys.VideoConversionPlanTechnicalDetailsTitle);

    public string ReadyForConversionSummaryText => T(LocalizationKeys.VideoConversionReadySummary);

    public bool HasEnteredPreviewConversionStage => _hasEnteredPreviewConversionStage;

    private bool ShouldShowPreviewConversionStatusCard =>
        HasEnteredPreviewConversionStage &&
        _conversionExecutionState.Status != ConversionExecutionStatus.Completed;

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

    public string ContinueWithConversionText => T(LocalizationKeys.VideoConversionContinue);

    public Visibility PreviewConversionStatusCardVisibility =>
        ShouldShowPreviewConversionStatusCard ? Visibility.Visible : Visibility.Collapsed;

    public GridLength PreviewConversionRowHeight =>
        ShouldShowPreviewConversionStatusCard ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public Thickness ActivityLogCardMargin =>
        ShouldShowPreviewConversionStatusCard ? new Thickness(0, 6, 0, 0) : new Thickness(0);

    public string PreviewStageResetNoticeText => T(LocalizationKeys.VideoConversionStageResetNotice);

    public string PreviewConversionStatusTitleText => T(LocalizationKeys.VideoConversionPreviewStatusTitle);

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
                ConversionExecutionStatus.Completed => T(LocalizationKeys.VideoConversionStatusCompleted),
                ConversionExecutionStatus.Canceled => T(LocalizationKeys.VideoConversionStatusCanceled),
                ConversionExecutionStatus.Failed => T(LocalizationKeys.VideoConversionStatusFailed),
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
                return T(LocalizationKeys.VideoSourceEmptyHint);
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

    public string PreviewTitleText => T(LocalizationKeys.VideoPreviewTitle);

    public string PreviewRequiredTitleText => T(LocalizationKeys.VideoPreviewRequiredTitle);

    public string PreviewAcceptedTitleText => T(LocalizationKeys.VideoPreviewAcceptedTitle);

    public string PreviewStepTitleText =>
        PreviewConversionGate.Evaluate(_previewState, CreateCurrentPreviewConfiguration()).CanStart
            ? PreviewAcceptedTitleText
            : PreviewRequiredTitleText;

    public string GeneratePreviewText => T(LocalizationKeys.VideoPreviewGenerate);

    public string PreviewRequiredInstructionText => _previewState.Status == PreviewGenerationStatus.Outdated
        ? T(LocalizationKeys.VideoPreviewOutdatedInstruction)
        : T(LocalizationKeys.VideoPreviewRequiredInstruction);

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

    public string ConversionReadyTitleText => T(LocalizationKeys.VideoConversionReadyTitle);

    public string ConversionReadyBodyText => T(LocalizationKeys.VideoConversionReadyBody);

    public string ConversionReadySelectedModelText => LabelValue(
        LocalizationKeys.VideoConversionReadySelectedModel,
        FormatConversionReadySelectedModel());

    public string ConversionReadyOutputText => LabelValue(
        LocalizationKeys.VideoConversionReadyOutput,
        FormatConversionReadyOutput());

    public string ConversionReadyDestinationText => LabelValue(
        LocalizationKeys.VideoOutputDestination,
        _conversionPlan?.SuggestedOutputPath);

    public Visibility CancelConversionPrimaryActionVisibility =>
        IsConversionRunning
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string CancelPreviewText => T(LocalizationKeys.VideoPreviewCancel);

    public string OpenPreviewText => T(LocalizationKeys.VideoPreviewOpen);

    public string OpenPreviewExternallyText => T(LocalizationKeys.VideoPreviewOpenExternally);

    public string DeletePreviewText => T(LocalizationKeys.VideoPreviewDelete);

    public string ContinuePreviewText => T(LocalizationKeys.VideoPreviewContinue);

    public string PreviewGeneratingTitleText => T(LocalizationKeys.VideoPreviewGeneratingTitle);

    public string PreviewGeneratingMessageText => T(LocalizationKeys.VideoPreviewGeneratingMessage);

    public int PreviewProgressPercent => Math.Clamp(_previewProgressPercent, 0, 100);

    public string PreviewProgressText =>
        PreviewProgressPercent > 0
            ? $"{PreviewProgressPercent}%"
            : T(LocalizationKeys.VideoPreviewProgressEstimating);

    public bool PreviewProgressIsIndeterminate =>
        IsPreviewGenerating && PreviewProgressPercent <= 0;

    public string PreviewReadyTitleText => T(LocalizationKeys.VideoPreviewReadyTitle);

    public string PreviewReadyMessageText => T(LocalizationKeys.VideoPreviewReadyMessage);

    public string PreviewPlaybackFallbackText => T(LocalizationKeys.VideoPlayerFallback);

    public string PreviewPlayText => T(LocalizationKeys.VideoPlayerPlay);

    public string PreviewPauseText => T(LocalizationKeys.VideoPlayerPause);

    public string PreviewReplayText => T(LocalizationKeys.VideoPlayerReplay);

    public string PreviewVolumeText => T(LocalizationKeys.VideoPlayerVolume);

    public string PreviewMutedText => T(LocalizationKeys.VideoPlayerMuted);

    public string PreviewMuteText => T(LocalizationKeys.VideoPlayerMute);

    public string PreviewUnmuteText => T(LocalizationKeys.VideoPlayerUnmute);

    public string PreviewEndedText => T(LocalizationKeys.VideoPlayerEnded);

    public string EmbeddedPlaybackUnavailableText => T(LocalizationKeys.VideoPlayerEmbeddedUnavailable);

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

    public string PreviewEngineText => T(LocalizationKeys.VideoPreviewEngine);

    public string PreviewRunningWithText => T(LocalizationKeys.VideoPreviewRunningWith);

    public string PreviewGpuMetricsNoteText => T(LocalizationKeys.VideoPreviewGpuMetricsNote);

    public string PreviewStageText => LabelValue(
        LocalizationKeys.VideoPreviewStage,
        _previewStageKey is null ? _previewStageEnglishText : T(_previewStageKey));

    public string PreviewCpuUsageText => _previewCpuUsageText;

    public string PreviewRamUsageText => _previewRamUsageText;

    public string PreviewGpuUsageText => _previewGpuUsageText;

    public string PreviewVramUsageText => _previewVramUsageText;

    public string PreviewGpuMetricsStatusText => _previewGpuMetricsStatusText;

    public string PreviewStatusText => LabelValue(
        LocalizationKeys.VideoPreviewStatusLabel,
        PreviewStatusValueText);

    public string PreviewGateStatusText
    {
        get
        {
            var gate = PreviewConversionGate.Evaluate(_previewState, CreateCurrentPreviewConfiguration());
            return LabelValue(
                LocalizationKeys.VideoPreviewStatusLabel,
                LocalizePreviewGateStatus(gate));
        }
    }

    public string PreviewGateDetailText
    {
        get
        {
            var gate = PreviewConversionGate.Evaluate(_previewState, CreateCurrentPreviewConfiguration());
            return LocalizePreviewGateDetail(gate);
        }
    }

    public string PreviewDurationText => LabelValue(
        LocalizationKeys.VideoPreviewDuration,
        CurrentPreviewDuration.ToString(@"hh\:mm\:ss"));

    public string PreviewStartTimeText => LabelValue(
        LocalizationKeys.VideoPreviewStartTime,
        CurrentPreviewStartTime.ToString(@"hh\:mm\:ss"));

    public string PreviewFromLabel => T(LocalizationKeys.VideoPreviewFrom);

    public string PreviewToLabel => T(LocalizationKeys.VideoPreviewTo);

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

    public string PreviewTimeRangeText => LabelValue(
        LocalizationKeys.VideoPreviewDuration,
        CurrentPreviewTimeRangeValidation.Range is { } range
            ? range.Duration.ToString(@"hh\:mm\:ss")
            : "-");

    public string PreviewMaximumDurationText => T(LocalizationKeys.VideoPreviewMaximumDuration);

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

    public string PreviewOutdatedText => T(LocalizationKeys.VideoPreviewOutdated);

    public Visibility PreviewOutdatedVisibility =>
        _previewState.Status == PreviewGenerationStatus.Outdated
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string PreviewOutputPathText => string.IsNullOrWhiteSpace(_previewState.OutputPath)
        ? string.Empty
        : LabelValue(
            LocalizationKeys.VideoPreviewOutput,
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

    public string PreviewModalDetailText => LocalizePreviewStateDetail(_previewState);

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
        PreviewGenerationStatus.NotGenerated => T(LocalizationKeys.VideoPreviewStatusRequired),
        PreviewGenerationStatus.Generating => T(LocalizationKeys.VideoPreviewStatusGenerating),
        PreviewGenerationStatus.Ready => T(LocalizationKeys.VideoPreviewStatusReady),
        PreviewGenerationStatus.Accepted => T(LocalizationKeys.VideoPreviewStatusAccepted),
        PreviewGenerationStatus.Failed => T(LocalizationKeys.VideoPreviewStatusFailed),
        PreviewGenerationStatus.Canceled => T(LocalizationKeys.VideoPreviewStatusCanceled),
        PreviewGenerationStatus.Outdated => T(LocalizationKeys.VideoPreviewStatusOutdated),
        _ => _previewState.Status.ToString(),
    };

    public string ConversionProgressTitle => T(LocalizationKeys.VideoConversionProgressTitle);

    public string ConversionExecutionStatusLabel => T(LocalizationKeys.VideoConversionExecutionStatusLabel);

    public string ConversionExecutionStatusText => _conversionExecutionState.Status switch
    {
        ConversionExecutionStatus.NotStarted => T(LocalizationKeys.VideoConversionStatusNotStarted),
        ConversionExecutionStatus.Ready => T(LocalizationKeys.VideoConversionStatusReady),
        ConversionExecutionStatus.Blocked => T(LocalizationKeys.VideoConversionStatusBlocked),
        ConversionExecutionStatus.Running => T(LocalizationKeys.VideoConversionStatusRunning),
        ConversionExecutionStatus.Canceling => T(LocalizationKeys.VideoConversionStatusCanceling),
        ConversionExecutionStatus.Canceled => T(LocalizationKeys.VideoConversionStatusCanceledShort),
        ConversionExecutionStatus.Failed => T(LocalizationKeys.VideoConversionStatusFailedShort),
        ConversionExecutionStatus.Completed => T(LocalizationKeys.VideoConversionStatusCompletedShort),
        _ => _conversionExecutionState.Status.ToString(),
    };

    public string ConversionExecutionStepLabel => T(LocalizationKeys.VideoConversionExecutionStepLabel);

    public string ConversionExecutionStepText => LocalizeConversionExecutionStep(_conversionExecutionState);

    public string ConversionExecutionProgressLabel => T(LocalizationKeys.VideoConversionExecutionProgressLabel);

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

    public string ConversionExecutionDetailText => LocalizeConversionExecutionDetail(_conversionExecutionState);

    public string ConversionElapsedLabelText => T(LocalizationKeys.VideoConversionElapsedLabel);

    public string ConversionRemainingLabelText => T(LocalizationKeys.VideoConversionRemainingLabel);

    public string ConversionEstimatedTotalLabelText => T(LocalizationKeys.VideoConversionEstimatedTotalLabel);

    public string ConversionElapsedValueText => IsConversionRunning
        ? FormatDuration(GetCurrentConversionElapsed())
        : "-";

    public string ConversionRemainingValueText => IsConversionRunning
        ? _conversionTimingEstimate?.Remaining is { } remaining
            ? FormatDuration(remaining)
            : T(LocalizationKeys.VideoPreviewProgressEstimating)
        : "-";

    public string ConversionEstimatedTotalValueText => IsConversionRunning
        ? _conversionTimingEstimate?.EstimatedTotal is { } total
            ? FormatDuration(total)
            : T(LocalizationKeys.VideoPreviewProgressEstimating)
        : "-";

    public bool CanCancelConversion => _conversionExecutionState.CanCancel;

    public string CancelConversionText => T(LocalizationKeys.VideoConversionCancel);

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

    public string ConversionRunningTitle => T(LocalizationKeys.VideoConversionRunningTitle);

    public string ConversionRunningStatusText => T(LocalizationKeys.VideoConversionRunningStatus);

    public string ConversionLiveLogEmptyText => T(LocalizationKeys.VideoConversionLiveLogEmpty);

    public string ConversionSummaryTitle => T(LocalizationKeys.VideoConversionSummaryTitle);

    public string ConversionSummaryPresetText => LabelValue(
        LocalizationKeys.VideoOutputProfileLabel,
        OutputProfileDisplayText);

    public string ConversionSummaryOutputContainerText => LabelValue(
        LocalizationKeys.VideoSetupOutputContainerLabel,
        SelectedOutputContainer.ToString());

    public string ConversionSummaryQualityText => LabelValue(
        LocalizationKeys.VideoSetupQualityLabel,
        QualityPresetText(SelectedQualityPreset));

    public string ConversionSummaryIntensityText => LabelValue(
        LocalizationKeys.VideoSetupThreeDIntensityLabel,
        ThreeDIntensityText(SelectedThreeDIntensity));

    public string ConversionSummaryLayoutText => LabelValue(
        LocalizationKeys.VideoSetupThreeDOutputFormatLabel,
        ThreeDOutputFormatText(SelectedThreeDOutputFormat));

    public string ConversionSummaryLocalModelText => LabelValue(
        LocalizationKeys.VideoModelSelectorLabel,
        SelectedLocalModelCandidate?.DisplayName);

    public string ConversionSummaryOutputPathText => LabelValue(
        LocalizationKeys.VideoOutputPrimary,
        _conversionPlan?.SuggestedOutputPath);

    public string ConversionSummaryLgCompatibilityCopyText =>
        LabelValue(
            LocalizationKeys.VideoOutputLgCompatibilityCopy,
            GetLgCompatibilityCopyPath());

    public string ConversionSummaryCurrentStatusText => LabelValue(
        LocalizationKeys.VideoConversionSummaryCurrentStatus,
        IsConversionRunning ? ConversionRunningStatusText : ConversionExecutionStatusText);

    public string CpuUsageText => _cpuUsageText;

    public string RamUsageText => _ramUsageText;

    public string GpuUsageText => _gpuUsageText;

    public string VramUsageText => _vramUsageText;

    public string ConversionReadinessTitle => T(LocalizationKeys.VideoReadinessTitle);

    public string SystemStatusTitle => T(LocalizationKeys.SystemStatusTitle);

    public string SystemStatusToolsTabTitle => T(LocalizationKeys.SystemStatusToolsTabTitle);

    public string SystemStatusConversionTabTitle => T(LocalizationKeys.VideoConversionPlanTitle);

    public string SystemStatusTechnicalDetailsTitle => T(LocalizationKeys.SystemStatusTechnicalDetailsTitle);

    public string SystemStatusDetailsButtonText => T(LocalizationKeys.SystemStatusDetailsButton);

    public string CloseDialogText => T(LocalizationKeys.ModalClose);

    public string CancelDialogText => T(LocalizationKeys.ModalCancel);

    public string ReplaceSelectedVideoTitleText => T(LocalizationKeys.VideoDialogReplaceTitle);

    public string ReplaceSelectedVideoBodyText => T(LocalizationKeys.VideoDialogReplaceBody);

    public string ReplaceVideoConfirmText => T(LocalizationKeys.VideoDialogReplaceConfirm);

    public string PreviewInvalidationConfirmationTitleText => T(LocalizationKeys.VideoDialogPreviewInvalidationTitle);

    public string PreviewInvalidationConfirmationBodyText => T(LocalizationKeys.VideoDialogPreviewInvalidationBody);

    public string PreviewInvalidationConfirmText => T(LocalizationKeys.VideoDialogPreviewInvalidationConfirm);

    public string ModelPackImportConfirmationTitleText => T(LocalizationKeys.ModelPackConfirmationTitle);

    public string ModelPackImportConfirmationIntroText => T(LocalizationKeys.ModelPackConfirmationIntro);

    public string ModelPackImportConfirmationMessageText =>
        _modelPackImportConfirmationPrompt is null
            ? string.Empty
            : CreateModelPackConfirmationMessage(_modelPackImportConfirmationPrompt.Preparation);

    public string ModelPackImportConfirmationContinueText => T(LocalizationKeys.CommonContinue);

    public string ConversionCompletedTitleText => T(LocalizationKeys.VideoConversionCompletedTitle);

    public string ConversionCompletedBodyText => T(LocalizationKeys.VideoConversionCompletedBody);

    public string ConversionCompletedOutputPathText => LabelValue(
        LocalizationKeys.VideoOutputPathLabel,
        CompletedConversionOutputPath);

    public string AcceptConversionCompletedText => T(LocalizationKeys.VideoConversionCompletedAccept);

    public string CompletedConversionOutputPath => _completedConversionOutputPath;

    public string ProfileDetailsTitleText => T(LocalizationKeys.VideoProfileDetailsTitle);

    public string ProfileDetailsButtonText => "?";

    public string ViewLogText => T(LocalizationKeys.CommonViewLog);

    public string CommonCopyText => T(LocalizationKeys.CommonCopy);

    public string CommonSelectAllText => T(LocalizationKeys.CommonSelectAll);

    public string CopyFullLogText => T(LocalizationKeys.ModalCopyFullLog);

    public string CopyPreviewLogText => T(LocalizationKeys.VideoLogCopyPreview);

    public string LogCopiedText => T(LocalizationKeys.CommonLogCopied);

    public string CouldNotCopyLogText => T(LocalizationKeys.CommonCouldNotCopyLog);

    public string LogCopyNotificationText =>
        _logCopyNotificationEnglishText;

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

    public double ActiveModalHeight =>
        IsSettingsModalOpen || IsModelHelpModalOpen || IsModelInventoryModalOpen
            ? 650d
            : double.NaN;

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

    public string LogsDiagnosticsTechnicalDetailsTitleText => T(LocalizationKeys.SystemStatusTechnicalDetailsTitle);

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

    public string ConversionReadinessEmptyText => T(LocalizationKeys.VideoReadinessEmpty);

    public bool ShowConversionReadinessCard =>
        _workflowState.ShowConversionReadinessCard(_conversionExecutionState.Status);

    public Visibility ConversionReadinessVisibility =>
        ShowConversionReadinessCard ? Visibility.Visible : Visibility.Collapsed;

    public bool ShowConversionProgressCard =>
        ConversionWorkflowState.ShowConversionProgressCard(_conversionExecutionState.Status);

    public Visibility ConversionProgressVisibility =>
        ShowConversionProgressCard ? Visibility.Visible : Visibility.Collapsed;

    public string ConversionReadinessStatusLabel => T(LocalizationKeys.VideoReadinessStatusLabel);

    public string ConversionReadinessMissingRequirementsTitle => T(LocalizationKeys.VideoReadinessMissingRequirementsTitle);

    public Visibility ConversionMissingRequirementsVisibility =>
        ShouldShowConversionMissingRequirements()
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewConversionMissingToolsVisibility =>
        HasEnteredPreviewConversionStage && ShouldShowConversionMissingRequirements()
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string PreviewConversionMissingToolsText => T(LocalizationKeys.VideoReadinessMissingTools);

    public string OpenSettingsForToolsText => T(LocalizationKeys.VideoReadinessOpenSettings);

    public string ConversionReadinessStatusText
    {
        get
        {
            if (IsConversionRunning)
            {
                return _conversionExecutionState.Status == ConversionExecutionStatus.Running
                    ? T(LocalizationKeys.VideoConversionStatusConverting)
                    : ConversionExecutionStatusText;
            }

            if (IsPreviewGenerating)
            {
                return PreviewStatusValueText;
            }

            var startGate = EvaluateConversionStartGate();
            if (!startGate.CanStart)
            {
                return LocalizeConversionStartGateStatus(startGate);
            }

            return _conversionReadiness is null
                ? T(LocalizationKeys.VideoConversionStatusUnavailableMissingComponents)
                : LocalizeConversionReadinessStatus(_conversionReadiness);
        }
    }

    public string ConversionReadinessIssuesText => _conversionReadiness is null
        ? "-"
        : _conversionReadiness.Issues.Count == 0
            ? T(LocalizationKeys.VideoReadinessNoMissingRequirements)
            : string.Join(
                Environment.NewLine,
                _conversionReadiness.Issues.Select(issue =>
                    $"- {LocalizeConversionReadinessIssue(issue)}"));

    public string ConversionReadinessMissingComponentsSummaryText =>
        _dependencyHealth is null || !HasCompletedAnalysis || IsConversionRunning || IsPreviewGenerating
            ? "-"
            : CreateMissingComponentsSummary();

    public string ConversionReadinessRequiredComponentsText => _conversionReadiness is null
            || IsConversionRunning
            || IsPreviewGenerating
        ? string.Empty
        : T(LocalizationKeys.VideoReadinessRequiredComponents);

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
                : LocalizeConversionStartGateDetail(startGate);
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
            : $"{_conversionPlan.OutputContainer} \u00b7 {ThreeDOutputFormatText(_conversionPlan.ThreeDOutputFormat)} \u00b7 {_conversionPlan.Width}x{_conversionPlan.Height}";

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
            TargetPresetName(SelectedOutputPreset),
            SelectedOutputContainer,
            SelectedQualityPreset,
            SelectedThreeDOutputFormat,
            GetDeviceCapabilityBucket());
    }

    private string LocalizeRecommendationCompatibilityIssue(VideoCompatibilityIssue issue)
    {
        var message = issue.EnglishMessage;
        if (message.Contains("LG Full HD target", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoRecommendationCompatibilityResolutionLg);
        }

        if (message.StartsWith("Source resolution is higher than the selected preset target", StringComparison.OrdinalIgnoreCase))
        {
            return T(
                LocalizationKeys.VideoRecommendationCompatibilityResolutionPresetFormat,
                ("width", _conversionRecommendation?.Width.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                ("height", _conversionRecommendation?.Height.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
        }

        if (message.Contains("HDR was detected", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("older 3D TVs", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoRecommendationCompatibilityHdrLg);
        }

        if (message.Contains("HDR was detected", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoRecommendationCompatibilityHdrGeneric);
        }

        if (message.StartsWith("MKV is a good master/archive source", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("older LG 3D TVs", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoRecommendationCompatibilityMkvLg);
        }

        if (message.StartsWith("MKV is a good master/archive source", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoRecommendationCompatibilityMkvGeneric);
        }

        if (message.StartsWith("No audio streams were detected", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoRecommendationCompatibilityNoAudio);
        }

        if (message.StartsWith("Subtitle handling will be configured", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoRecommendationCompatibilitySubtitlesLater);
        }

        return message;
    }

    private string CreateSelectedModelGuidanceText()
    {
        if (SelectedLocalModelCandidate is null)
        {
            return T(LocalizationKeys.VideoModelGuidanceDefault);
        }

        var entry = FindRegistryEntry(SelectedLocalModelCandidate);
        var guidance = _modelGuidanceService.Create(
            SelectedLocalModelCandidate.MappingKey,
            SelectedLocalModelCandidate.Iw3DepthModelName,
            SelectedLocalModelCandidate.DisplayName,
            entry?.IsEmbeddedBase == true);
        return T(
            LocalizationKeys.VideoModelGuidanceFormat,
            ("headline", LocalizeModelGuidanceHeadline(SelectedLocalModelCandidate, entry, guidance)),
            ("bestFor", LocalizeModelGuidanceBestFor(SelectedLocalModelCandidate, entry, guidance)),
            ("speed", LocalizeModelGuidanceSpeed(guidance.EnglishSpeed)),
            ("quality", LocalizeModelGuidanceQuality(guidance.EnglishQuality)),
            ("size", LocalizeModelGuidanceSize(guidance.EnglishSize)));
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

    private string ConfidenceText(ConversionEstimateConfidence confidence) => confidence switch
    {
        ConversionEstimateConfidence.High => T(LocalizationKeys.VideoOptionConfidenceHigh),
        ConversionEstimateConfidence.Medium => T(LocalizationKeys.VideoOptionConfidenceMedium),
        ConversionEstimateConfidence.Low => T(LocalizationKeys.VideoOptionConfidenceLow),
        _ => T(LocalizationKeys.VideoOptionConfidenceUnavailable),
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
        ConversionExecutionStatus.Running => T(LocalizationKeys.VideoConversionCancel),
        ConversionExecutionStatus.Canceling => T(LocalizationKeys.VideoConversionCanceling),
        _ => T(LocalizationKeys.VideoConversionStart),
    };

    public bool CanUseSystemStatusActions =>
        !IsAnyModalOpen &&
        !IsImageExportRunning &&
        !IsConversionRunning &&
        !IsPreviewGenerating &&
        !IsModelPackImportRunning;

    public bool CanUseSettingsSystemStatusActions =>
        !IsImageExportRunning &&
        !IsConversionRunning &&
        !IsPreviewGenerating &&
        !IsModelPackImportRunning;

    public string ToolStatusTitle => T(LocalizationKeys.SystemStatusTitle);

    public string RefreshText => T(LocalizationKeys.CommonRefresh);

    public string OpenEngineFolderText => T(LocalizationKeys.SystemOpenEngineFolder);

    public string ActivityLogTitle => T(LocalizationKeys.VideoLogTitle);

    public string ActiveActivityLogModalTitleText =>
        _activeActivityLogModalKind == ActivityLogModalKind.Image ? ImageActivityLogTitleText : ActivityLogTitle;

    public string ClearText => T(LocalizationKeys.CommonClear);

    public string RecommendedPresetTitle => T(LocalizationKeys.VideoProfileDetailsTitle);

    public string OutputPresetLabel => T(LocalizationKeys.VideoOutputProfileLabel);

    public string PresetName => TargetPresetName(SelectedOutputPreset);

    public string PresetDescriptionText => LabelValue(
        LocalizationKeys.VideoProfileDescriptionLabel,
        TargetPresetDescription(SelectedOutputPreset));

    public string PresetBestForText => LabelValue(
        LocalizationKeys.VideoProfileBestForLabel,
        TargetPresetBestFor(SelectedOutputPreset));

    public string PresetTechnicalRecommendationTitle => T(LocalizationKeys.VideoProfileTechnicalRecommendationTitle);

    public string PresetContainerText => LabelValue(
        LocalizationKeys.VideoProfileRecommendedContainer,
        SelectedOutputPreset.Recommendation.OutputContainer.ToString());

    public string PresetVideoCodecText => LabelValue(
        LocalizationKeys.VideoProfileCodec,
        SelectedOutputPreset.Recommendation.VideoCodec);

    public string PresetAudioCodecText => LabelValue(
        LocalizationKeys.VideoProfileAudio,
        SelectedOutputPreset.Recommendation.AudioCodec);

    public string PresetResolutionText => LabelValue(
        LocalizationKeys.VideoProfileTargetResolution,
        $"{SelectedOutputPreset.Recommendation.Width}x{SelectedOutputPreset.Recommendation.Height}");

    public string PresetThreeDLayoutText => LabelValue(
        LocalizationKeys.VideoProfileRecommendedThreeDLayout,
        ThreeDOutputFormatText(SelectedOutputPreset.Recommendation.ThreeDOutputFormat));

    public string PresetAdvancedOutputText => T(LocalizationKeys.VideoProfileAdvancedOutput);

    public string PresetCompatibilityNoteText => LabelValue(
        LocalizationKeys.VideoProfileCompatibilityNote,
        TargetPresetCompatibilityNote(SelectedOutputPreset));

    public string TvPlaybackTitle => TargetPresetPlaybackTitle(SelectedOutputPreset);

    public string TvPlaybackInstructions => TargetPresetPlaybackInstructions(SelectedOutputPreset);

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

    public RelayCommand ShowImageParallaxModelHelpCommand { get; }

    public RelayCommand SelectImageModeSourceStepCommand { get; }

    public RelayCommand SelectImageSetupStepCommand { get; }

    public RelayCommand SelectImagePreviewExportStepCommand { get; }

    public RelayCommand ImageWizardBackCommand { get; }

    public RelayCommand ImageWizardNextCommand { get; }

    public RelayCommand ContinueWithImageConversionCommand { get; }

    public AsyncRelayCommand ConvertImageCommand { get; }

    public AsyncRelayCommand ExportStereoscopicImageCommand { get; }

    public RelayCommand OpenImageOutputFolderCommand { get; }

    public RelayCommand NewImageConversionCommand { get; }

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
            AddVideoLogResolved(T(LocalizationKeys.VideoLogUnsupportedDroppedFile));
            return;
        }

        await TrySelectVideoAndAnalyzeAsync(path);
    }

    public void SelectDroppedImage(string path)
    {
        if (!IsImageConversionSectionSelected ||
            !CanInteractWithImageWorkflow)
        {
            return;
        }

        TrySelectImage(path);
    }

    private bool IsSpanish =>
        string.Equals(_localizationService.ActiveLanguageCode, "es", StringComparison.OrdinalIgnoreCase);

    private async void SelectVideo()
    {
        if (IsConversionRunning || IsPreviewGenerating || IsModelPackImportRunning)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = T(LocalizationKeys.VideoSourceDialogTitle),
            Filter = T(LocalizationKeys.VideoSourceDialogFilesFilter) +
                "|*.mp4;*.mkv;*.avi;*.mov;*.m4v;*.webm|" +
                T(LocalizationKeys.VideoSourceDialogAllFilesFilter) + "|*.*",
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
        if (!CanInteractWithImageWorkflow)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = T(LocalizationKeys.ImageDialogSelectTitle),
            Filter = T(LocalizationKeys.ImageDialogFilesFilter) +
                "|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.webp|" +
                T(LocalizationKeys.ImageDialogAllFilesFilter) + "|*.*",
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
        if (!CanInteractWithImageWorkflow)
        {
            return;
        }

        if (!IsSupportedImageFile(path))
        {
            AddImageLogResolved(T(LocalizationKeys.ImageLogUnsupportedFormat));
            return;
        }

        SelectedImagePath = path;
        _selectedImageMetadata = null;
        ResetImageSetupState();
        ResetImageExportState();
        _hasEnteredImagePreviewExportStage = false;
        SelectedImageConversionStep = ImageConversionStep.ModeAndSource;
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogSelectedFormat,
            ("fileName", Path.GetFileName(path))));
        AnalyzeImage();
    }

    private void AnalyzeImage()
    {
        if (!CanInteractWithImageWorkflow)
        {
            return;
        }

        if (!HasSelectedImage || SelectedImagePath is null)
        {
            AddImageLogResolved(T(LocalizationKeys.ImageLogSelectBeforeAnalysis));
            return;
        }

        try
        {
            _selectedImageMetadata = ReadImageMetadata(SelectedImagePath);
            ResetImageExportState();
            _hasEnteredImagePreviewExportStage = false;
            SelectedImageConversionStep = ImageConversionStep.ModeAndSource;
            AddImageLogResolved(T(
                LocalizationKeys.ImageLogMetadataAnalyzedFormat,
                ("width", _selectedImageMetadata.WidthText),
                ("height", _selectedImageMetadata.HeightText),
                ("format", _selectedImageMetadata.Format)));
        }
        catch (Exception exception)
        {
            _selectedImageMetadata = null;
            ResetImageExportState();
            AddImageLogResolved(T(
                LocalizationKeys.ImageLogMetadataFailedFormat,
                ("message", exception.Message)));
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
            AddVideoLogResolved(T(LocalizationKeys.VideoLogSelectBeforeAnalysis));
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
            AddVideoLogResolved(T(LocalizationKeys.VideoLogAnalysisStarted));
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
            AddVideoLogResolved(T(LocalizationKeys.VideoLogReplacementCanceled));
            return;
        }

        if (replacingVideo &&
            !ShouldConfirmPreviewInvalidatingChange() &&
            !await ConfirmReplaceSelectedVideoAsync())
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogReplacementCanceled));
            return;
        }

        SetSelectedVideo(path, replacingVideo);
        AddVideoLogResolved(T(LocalizationKeys.VideoLogAnalysisStarted));
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
                AddVideoLogResolved(T(LocalizationKeys.VideoLogAnalysisCompleted));
                AddVideoLogResolved(T(LocalizationKeys.VideoLogRecommendationGenerated));
                AddVideoLogResolved(T(LocalizationKeys.VideoLogConversionPlanPrepared));
                return;
            }

            LogAnalysisFailure(result.Failure);
        }
        catch (Exception exception)
        {
            AddVideoLogResolved(T(
                LocalizationKeys.VideoLogAnalysisFailedUnexpectedFormat,
                ("message", exception.Message)));
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
        EnsureSelectedStereoOutputFormatIsSupported();
        RaiseModelInventoryPropertiesChanged();
        UpdateConversionReadiness();
        RaiseImageConversionPropertiesChanged();
        if (logRefresh)
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogToolsRefreshed));
        }
    }

    private async Task RefreshEngineStatusWithGlobalBusyAsync(bool logRefresh)
    {
        if (IsConversionRunning || IsPreviewGenerating)
        {
            return;
        }

        ShowGlobalBusyOverlay(LocalizationKeys.BusyRefreshingModelInventory);
        try
        {
            await RefreshEngineStatusAsync(logRefresh);
        }
        finally
        {
            HideGlobalBusyOverlay();
        }
    }

    private void ShowGlobalBusyOverlay(string key) =>
        ShowGlobalBusyOverlayResolved(T(key));

    private void ShowGlobalBusyOverlayResolved(string text)
    {
        _globalBusyEnglishText = string.IsNullOrWhiteSpace(text)
            ? T(LocalizationKeys.CommonLoading)
            : text;
        _globalBusySpanishText = _globalBusyEnglishText;
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
            AddLogResolved(T(LocalizationKeys.ModelPackLogUnavailableBusy));
            return;
        }

        if (IsSettingsModalOpen)
        {
            CaptureSettingsReturnContext();
            IsSettingsModalOpen = false;
        }

        _reopenModelInventoryAfterImport = IsModelInventoryModalOpen;
        IsModelPackImportRunning = true;
        ShowGlobalBusyOverlay(LocalizationKeys.BusyValidatingModelPack);
        try
        {
            var result = await _modelPackImportCoordinator.ImportAsync(
                CreateModelPackAppImportRequest());
            ApplyModelPackImportResult(result);
        }
        catch (OperationCanceledException)
        {
            SetModelPackImportStatusResolved(T(LocalizationKeys.ModelPackStatusCanceled));
            AddLogResolved(T(LocalizationKeys.ModelPackLogCanceled));
        }
        catch (Exception exception)
        {
            var errorLogPath = AppErrorLogService.LogRecoverableException(
                "Import model pack",
                exception);
            SetModelPackImportStatusResolved(T(LocalizationKeys.ModelPackStatusUnexpectedFailure));
            SetLastModelPackImportSummaryResolved(T(
                LocalizationKeys.ModelPackSummaryUnexpectedFailureFormat,
                ("path", errorLogPath),
                ("message", exception.Message)));
            AddLogResolved(T(LocalizationKeys.ModelPackLogUnexpectedFailureFormat, ("message", exception.Message)));
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
            ShowGlobalBusyOverlay(LocalizationKeys.BusyRefreshingModelInventory);
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
        SetModelPackImportStatusResolved(T(LocalizationKeys.ModelPackStatusValidated));
        var prompt = ModelPackImportConfirmationFormatter.CreatePrompt(preparation);
        SetLastModelPackImportSummaryResolved(CreateModelPackConfirmationMessage(preparation));
        AddLogResolved(T(
            LocalizationKeys.ModelPackLogValidatedFormat,
            ("name", GetModelPackDisplayName(preparation)),
            ("filesToInstall", preparation.FilesToInstall.Count),
            ("alreadyInstalled", preparation.AlreadyInstalledFiles.Count)));
        AddLogResolved(T(
            LocalizationKeys.ModelPackLogInstallTargetFormat,
            ("path", preparation.TargetPretrainedModelsRoot)));

        if (preparation.ElevationRequired)
        {
            AddLogResolved(T(LocalizationKeys.ModelPackLogAdminRequired));
        }
        else
        {
            AddLogResolved(T(LocalizationKeys.ModelPackLogAdminMaybeNotRequired));
        }

        LogModelPackWarnings(preparation.Warnings);
    }

    private void RecordCanceledModelPackImport(ModelPackAppImportResult result)
    {
        SetModelPackImportStatusResolved(T(LocalizationKeys.ModelPackStatusCanceledBeforeAdmin));
        SetLastModelPackImportSummaryResolved(T(LocalizationKeys.ModelPackSummaryCanceledNoFiles));
        AddLogResolved(T(
            LocalizationKeys.ModelPackLogCanceledBeforeHelperFormat,
            ("file", Path.GetFileName(result.SelectedModelPackZipPath))));
    }

    private void RecordInvalidModelPackPreparation(ModelPackAppImportResult result)
    {
        SetModelPackImportStatusResolved(T(LocalizationKeys.ModelPackStatusValidationFailed));
        IReadOnlyList<string> errors = result.Errors.Count == 0
            ? [T(LocalizationKeys.ModelPackSummaryPreparationMissingRequest)]
            : result.Errors;
        SetLastModelPackImportSummaryResolved(CreateModelPackErrorSummary(
            T(LocalizationKeys.ModelPackSummaryValidationFailedHeading),
            errors));
        AddLogResolved(T(LocalizationKeys.ModelPackLogValidationFailed));
        foreach (var error in errors)
        {
            AddLogResolved(T(LocalizationKeys.ModelPackLogValidationErrorFormat, ("error", error)));
        }

        LogModelPackWarnings(result.Warnings);
    }

    private void RecordFailedModelPackImport(ModelPackAppImportResult result)
    {
        var helperWasNotStarted = result.ExecutionResult?.HelperProcessStarted != true;
        SetModelPackImportStatusResolved(T(
            helperWasNotStarted
                ? LocalizationKeys.ModelPackStatusImportDidNotStart
                : LocalizationKeys.ModelPackStatusImportFailed));
        IReadOnlyList<string> errors = result.Errors.Count == 0
            ? [T(LocalizationKeys.ModelPackSummaryHelperFailed)]
            : result.Errors;
        SetLastModelPackImportSummaryResolved(CreateModelPackErrorSummary(
            helperWasNotStarted
                ? T(LocalizationKeys.ModelPackSummaryImportDidNotStartNoFiles)
                : T(LocalizationKeys.ModelPackStatusImportFailed),
            errors));
        AddLogResolved(T(
            helperWasNotStarted
                ? LocalizationKeys.ModelPackLogImportDidNotStart
                : LocalizationKeys.ModelPackLogImportFailed));
        foreach (var error in errors)
        {
            AddLogResolved(T(LocalizationKeys.ModelPackLogImportErrorFormat, ("error", error)));
        }

        LogModelPackWarnings(result.ExecutionResult?.HelperResult?.Warnings ?? []);
    }

    private void RecordSuccessfulModelPackImport(ModelPackAppImportResult result)
    {
        SetModelPackImportStatusResolved(T(LocalizationKeys.ModelPackStatusCompleted));
        SetLastModelPackImportSummaryResolved(CreateModelPackSuccessSummary(result));
        AddLogResolved(CreateModelPackSuccessLog(result));
        if (result.AppRefreshCompleted)
        {
            AddLogResolved(T(LocalizationKeys.ModelPackLogInventoryRefreshed));
        }

        LogModelPackWarnings(result.ExecutionResult?.HelperResult?.Warnings ?? []);
    }

    private void SetModelPackImportStatus(string englishText, string spanishText)
    {
        _modelPackImportStatusEnglishText = englishText;
        _modelPackImportStatusSpanishText = spanishText;
        OnPropertyChanged(nameof(ModelPackImportStatusText));
    }

    private void SetModelPackImportStatusResolved(string text) =>
        SetModelPackImportStatus(text, text);

    private void SetLastModelPackImportSummary(string englishText, string spanishText)
    {
        _lastModelPackImportSummaryEnglishText = englishText;
        _lastModelPackImportSummarySpanishText = spanishText;
        OnPropertyChanged(nameof(LastModelPackImportSummary));
        OnPropertyChanged(nameof(LastModelPackImportSummaryVisibility));
    }

    private void SetLastModelPackImportSummaryResolved(string text) =>
        SetLastModelPackImportSummary(text, text);

    private static string GetCurrentV3dfyVersion() =>
        typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString() ?? "unknown";

    private static string GetModelPackDisplayName(ModelPackImportPreparationResult preparation) =>
        preparation.Manifest?.DisplayName ??
        Path.GetFileName(preparation.ModelPackZipPath);

    private string CreateModelPackConfirmationMessage(ModelPackImportPreparationResult preparation)
    {
        IReadOnlyList<string> lines =
        [
            T(LocalizationKeys.ModelPackConfirmationPackFormat, ("name", GetModelPackDisplayName(preparation))),
            T(LocalizationKeys.ModelPackConfirmationFilesToInstallFormat, ("count", preparation.FilesToInstall.Count)),
            T(LocalizationKeys.ModelPackConfirmationAlreadyInstalledFormat, ("count", preparation.AlreadyInstalledFiles.Count)),
            T(LocalizationKeys.ModelPackConfirmationConflictsFormat, ("count", preparation.Conflicts.Count)),
            T(LocalizationKeys.ModelPackConfirmationTargetFolderFormat, ("path", preparation.TargetPretrainedModelsRoot)),
            preparation.ElevationRequired
                ? T(LocalizationKeys.ModelPackConfirmationAdminRequired)
                : T(LocalizationKeys.ModelPackConfirmationAdminNotExpected),
            T(LocalizationKeys.ModelPackConfirmationModelAvailability),
            string.Empty,
            T(LocalizationKeys.ModelPackConfirmationTrustInstruction),
        ];

        return string.Join(Environment.NewLine, lines);
    }

    private string CreateModelPackSuccessSummary(ModelPackAppImportResult result)
    {
        var helperResult = result.ExecutionResult?.HelperResult;
        var manifestName = helperResult?.Manifest?.DisplayName ??
            result.LaunchPreparation?.Preparation.Manifest?.DisplayName ??
            Path.GetFileName(result.SelectedModelPackZipPath);
        IReadOnlyList<string> lines =
        [
            T(LocalizationKeys.ModelPackSummaryImportedPackFormat, ("name", manifestName)),
            T(LocalizationKeys.ModelPackSummaryInstalledFilesFormat, ("count", helperResult?.InstalledFiles.Count ?? 0)),
            T(LocalizationKeys.ModelPackSummaryAlreadyPresentFilesFormat, ("count", helperResult?.AlreadyInstalledFiles.Count ?? 0)),
            T(LocalizationKeys.ModelPackSummarySkippedFilesFormat, ("count", helperResult?.SkippedFiles.Count ?? 0)),
            T(LocalizationKeys.ModelPackSummaryInventoryRefreshedFormat, ("value", result.AppRefreshCompleted ? T(LocalizationKeys.CommonYes) : T(LocalizationKeys.CommonNo))),
            T(LocalizationKeys.ModelPackSummarySelectableModels),
        ];
        return string.Join(Environment.NewLine, lines);
    }

    private string CreateModelPackSuccessLog(ModelPackAppImportResult result)
    {
        var helperResult = result.ExecutionResult?.HelperResult;
        var manifestName = helperResult?.Manifest?.DisplayName ??
            result.LaunchPreparation?.Preparation.Manifest?.DisplayName ??
            Path.GetFileName(result.SelectedModelPackZipPath);
        return T(
            LocalizationKeys.ModelPackLogImportedFormat,
            ("name", manifestName),
            ("count", helperResult?.InstalledFiles.Count ?? 0));
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
            AddLogResolved(T(LocalizationKeys.ModelPackLogWarningFormat, ("warning", warning)));
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
            return T(LocalizationKeys.ModelInventoryNoticeNoModels);
        }

        var lines = new List<string>();
        foreach (var candidate in candidates)
        {
            var entry = FindRegistryEntry(candidate);
            lines.Add($"- {GetCandidateDisplayName(candidate)}");
            lines.Add(T(
                LocalizationKeys.ModelInventoryNoticeFilePackIdentityFormat,
                ("path", candidate.RelativePath)));
            if (entry is not null)
            {
                lines.Add(T(
                    LocalizationKeys.ModelInventoryNoticeCatalogStatusFormat,
                    ("status", ModelNoticeStatusText(entry.RedistributionDecision))));
            }

            var noticeFiles = FindMatchingModelNoticeFiles(candidate, entry);
            lines.Add(noticeFiles.Count > 0
                ? T(
                    LocalizationKeys.ModelInventoryNoticeLicenseFilesFormat,
                    ("files", string.Join(", ", noticeFiles)))
                : T(LocalizationKeys.ModelInventoryNoticeLicenseMetadataUnavailable));
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

    private string ModelNoticeStatusText(Iw3DepthModelRedistributionDecision decision) => decision switch
    {
        Iw3DepthModelRedistributionDecision.SafeForPublicRelease => T(LocalizationKeys.ModelInventoryNoticeStatusSafePublic),
        Iw3DepthModelRedistributionDecision.SafeWithNotice => T(LocalizationKeys.ModelInventoryNoticeStatusSafeWithNotice),
        Iw3DepthModelRedistributionDecision.UserDownloadOnly => T(LocalizationKeys.ModelInventoryNoticeStatusUserDownloadOnly),
        Iw3DepthModelRedistributionDecision.ExcludeNonCommercial => T(LocalizationKeys.ModelInventoryNoticeStatusExcludeNonCommercial),
        Iw3DepthModelRedistributionDecision.BlockedUnclearLicense => T(LocalizationKeys.ModelInventoryNoticeStatusBlockedUnclearLicense),
        Iw3DepthModelRedistributionDecision.NotAModelPackTarget => T(LocalizationKeys.ModelInventoryNoticeStatusNotModelPackTarget),
        _ => T(LocalizationKeys.ModelInventoryNoticeStatusUnknown),
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
                Type: GetDepthTypeText(entry),
                Source: GetModelSourceText(candidate, entry)));
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
            return T(LocalizationKeys.ModelInventorySelectableEmpty);
        }

        var lines = new List<string>();
        foreach (var candidate in candidates)
        {
            lines.Add($"- {GetCandidateDisplayName(candidate)}");
            lines.Add($"  --depth-model {candidate.Iw3DepthModelName ?? "-"}");
            lines.Add($"  {candidate.RelativePath}");
            var note = GetSelectionStatusNoteText(candidate);
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
            return T(LocalizationKeys.ModelInventoryDiagnosticEmpty);
        }

        var lines = new List<string>();
        foreach (var candidate in unmappedCandidates)
        {
            lines.Add($"- {GetCandidateDisplayName(candidate)}");
            lines.Add($"  {candidate.RelativePath}");
            lines.Add($"  {T(LocalizationKeys.ModelInventoryDiagnosticReason)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string GetSelectionStatusNoteText(LocalModelSelectionCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.EnglishStatusNote))
        {
            return string.Empty;
        }

        if (candidate.EnglishStatusNote.Contains(
                "not eligible for public v3dfy model packs",
                StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ModelInventorySelectableStatusUserProvidedNotPackEligible);
        }

        if (candidate.EnglishStatusNote.Contains(
                "verified local checkpoint",
                StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ModelInventorySelectableStatusVerifiedLocalCheckpoint);
        }

        return string.Empty;
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
                Purpose: GetModelPurpose(candidate, entry),
                Use: GetModelUseExample(candidate, entry),
                Scene: GetSceneScopeText(entry),
                Depth: GetDepthTypeText(entry),
                SizePerformance: GetModelSizePerformanceText(candidate, entry)));
        }

        return rows;
    }

    private IReadOnlyList<ModelHelpRow> CreateImageParallaxModelHelpRows()
    {
        var candidates = ImageParallaxLocalModelCandidates;
        if (candidates.Count == 0)
        {
            return [];
        }

        var rows = new List<ModelHelpRow>();
        foreach (var candidate in candidates)
        {
            var entry = FindRegistryEntry(candidate);
            rows.Add(new ModelHelpRow(
                Model: GetCandidateDisplayName(candidate),
                Purpose: GetImageParallaxModelPurpose(candidate, entry),
                Use: GetImageParallaxModelUse(candidate, entry),
                Scene: GetSceneScopeText(entry),
                Depth: GetDepthTypeText(entry),
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
        var sizeClass = GetSizeClassText(entry);
        return T(
            LocalizationKeys.ModelInventoryGuidanceFormat,
            ("sizeClass", sizeClass),
            ("speed", LocalizeModelGuidanceSpeed(guidance.EnglishSpeed)),
            ("quality", LocalizeModelGuidanceQuality(guidance.EnglishQuality)),
            ("size", LocalizeModelGuidanceSize(guidance.EnglishSize)));
    }

    private string LocalizeModelGuidanceHeadline(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry,
        ModelGuidance guidance) =>
        T(GetModelGuidanceHeadlineKey(candidate, entry, guidance));

    private string LocalizeModelGuidanceBestFor(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry,
        ModelGuidance guidance) =>
        T(GetModelGuidanceBestForKey(candidate, entry, guidance));

    private string LocalizeModelGuidanceSpeed(string englishValue) => englishValue switch
    {
        "Fast" => T(LocalizationKeys.ModelInventoryGuidanceSpeedFast),
        "Medium" => T(LocalizationKeys.ModelInventoryGuidanceSpeedMedium),
        "Slow" => T(LocalizationKeys.ModelInventoryGuidanceSpeedSlow),
        _ => englishValue,
    };

    private string LocalizeModelGuidanceQuality(string englishValue) => englishValue switch
    {
        "Good" => T(LocalizationKeys.ModelInventoryGuidanceQualityGood),
        "High" => T(LocalizationKeys.ModelInventoryGuidanceQualityHigh),
        "Better" => T(LocalizationKeys.ModelInventoryGuidanceQualityBetter),
        "Experimental" => T(LocalizationKeys.ModelInventoryGuidanceQualityExperimental),
        "Usable" => T(LocalizationKeys.ModelInventoryGuidanceQualityUsable),
        _ => englishValue,
    };

    private string LocalizeModelGuidanceSize(string englishValue) => englishValue switch
    {
        "Small" => T(LocalizationKeys.ModelInventoryGuidanceSizeSmall),
        "Large" => T(LocalizationKeys.ModelInventoryGuidanceSizeLarge),
        "Base" => T(LocalizationKeys.ModelInventoryGuidanceSizeBase),
        "Small/Base" => T(LocalizationKeys.ModelInventoryGuidanceSizeSmallBase),
        _ => englishValue,
    };

    private static string GetModelGuidanceHeadlineKey(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry,
        ModelGuidance guidance)
    {
        var source = NormalizeModelGuidanceSource(candidate, entry);
        if (source.Contains("any_v2_s") ||
            source.Contains("depth-anything-v2-small"))
        {
            return LocalizationKeys.ModelInventoryGuidanceHeadlineRecommendedFirst;
        }

        if (source.Contains("3-mono") ||
            source.Contains("depth-anything-3"))
        {
            return LocalizationKeys.ModelInventoryGuidanceHeadlineExperimentalLarge;
        }

        if (guidance.IsBaseModel)
        {
            return LocalizationKeys.ModelInventoryGuidanceHeadlineIncludedBase;
        }

        if (source.Contains("indoor") || source.Contains("hypersim") || source.Contains("any_n"))
        {
            return LocalizationKeys.ModelInventoryGuidanceHeadlineIndoorRoom;
        }

        if (source.Contains("outdoor") || source.Contains("vkitti") || source.Contains("any_k"))
        {
            return LocalizationKeys.ModelInventoryGuidanceHeadlineOutdoorRoad;
        }

        if (source.Contains("large"))
        {
            return LocalizationKeys.ModelInventoryGuidanceHeadlineLargeQuality;
        }

        if (source.Contains("base"))
        {
            return LocalizationKeys.ModelInventoryGuidanceHeadlineBalanced;
        }

        return LocalizationKeys.ModelInventoryGuidanceHeadlineSmallGeneral;
    }

    private static string GetModelGuidanceBestForKey(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry,
        ModelGuidance guidance)
    {
        var headlineKey = GetModelGuidanceHeadlineKey(candidate, entry, guidance);
        return headlineKey switch
        {
            LocalizationKeys.ModelInventoryGuidanceHeadlineRecommendedFirst => LocalizationKeys.ModelInventoryGuidanceBestForRecommendedFirst,
            LocalizationKeys.ModelInventoryGuidanceHeadlineExperimentalLarge => LocalizationKeys.ModelInventoryGuidanceBestForExperimentalLarge,
            LocalizationKeys.ModelInventoryGuidanceHeadlineIndoorRoom => LocalizationKeys.ModelInventoryGuidanceBestForIndoorRoom,
            LocalizationKeys.ModelInventoryGuidanceHeadlineOutdoorRoad => LocalizationKeys.ModelInventoryGuidanceBestForOutdoorRoad,
            LocalizationKeys.ModelInventoryGuidanceHeadlineLargeQuality => LocalizationKeys.ModelInventoryGuidanceBestForLargeQuality,
            LocalizationKeys.ModelInventoryGuidanceHeadlineIncludedBase => LocalizationKeys.ModelInventoryGuidanceBestForIncludedBase,
            LocalizationKeys.ModelInventoryGuidanceHeadlineBalanced => LocalizationKeys.ModelInventoryGuidanceBestForBalanced,
            _ => LocalizationKeys.ModelInventoryGuidanceBestForSmallGeneral,
        };
    }

    private static string NormalizeModelGuidanceSource(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry) =>
        $"{entry?.Key ?? candidate.MappingKey} {candidate.Iw3DepthModelName} {candidate.DisplayName}"
            .Trim()
            .ToLowerInvariant();

    private static Iw3DepthModelRegistryEntry? FindRegistryEntry(
        LocalModelSelectionCandidate candidate) =>
        Iw3DepthModelMapper.RegistryEntries.FirstOrDefault(entry =>
            string.Equals(entry.Key, candidate.MappingKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.DepthModelName, candidate.Iw3DepthModelName, StringComparison.OrdinalIgnoreCase));

    private static bool IsImageParallaxCompatibleCandidate(
        LocalModelSelectionCandidate candidate)
    {
        var entry = FindRegistryEntry(candidate);
        return entry?.MediaCapability is
            Iw3DepthModelMediaCapability.ImageAndVideo or
            Iw3DepthModelMediaCapability.ImageOnly;
    }

    private string GetImageParallaxModelPurpose(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry)
    {
        var basePurpose = GetModelPurpose(candidate, entry);
        return $"{basePurpose} {T(LocalizationKeys.ImageModelHelpPurposeSuffix)}";
    }

    private string GetImageParallaxModelUse(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry)
    {
        var baseUse = GetModelUseExample(candidate, entry);
        return $"{baseUse} {T(LocalizationKeys.ImageModelHelpUseSuffix)}";
    }

    private string GetModelPurpose(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry)
    {
        var key = entry?.Key ?? candidate.MappingKey ?? string.Empty;
        return key switch
        {
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey => T(LocalizationKeys.ModelInventoryHelpPurposeMetricIndoor),
            Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorKey => T(LocalizationKeys.ModelInventoryHelpPurposeMetricOutdoor),
            Iw3DepthModelMapper.DepthAnythingV2SmallKey => T(LocalizationKeys.ModelInventoryHelpPurposeV2Small),
            Iw3DepthModelMapper.DepthAnythingSmallKey => T(LocalizationKeys.ModelInventoryHelpPurposeSmall),
            Iw3DepthModelMapper.DepthAnythingBaseKey => T(LocalizationKeys.ModelInventoryHelpPurposeBase),
            Iw3DepthModelMapper.DepthAnythingLargeKey => T(LocalizationKeys.ModelInventoryHelpPurposeLarge),
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimSmallKey => T(LocalizationKeys.ModelInventoryHelpPurposeMetricHypersimSmall),
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimBaseKey => T(LocalizationKeys.ModelInventoryHelpPurposeMetricHypersimBase),
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiSmallKey => T(LocalizationKeys.ModelInventoryHelpPurposeMetricVkittiSmall),
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiBaseKey => T(LocalizationKeys.ModelInventoryHelpPurposeMetricVkittiBase),
            Iw3DepthModelMapper.DistillAnyDepthSmallKey => T(LocalizationKeys.ModelInventoryHelpPurposeDistillAnyDepthSmall),
            Iw3DepthModelMapper.DepthAnything3MonoLargeKey => T(LocalizationKeys.ModelInventoryHelpPurposeDepthAnything3MonoLarge),
            Iw3DepthModelMapper.DepthAnything3MonoLarge3dTvKey => T(LocalizationKeys.ModelInventoryHelpPurposeDepthAnything3MonoLarge3dTv),
            _ when entry?.RedistributionDecision ==
                Iw3DepthModelRedistributionDecision.BlockedUnclearLicense => T(LocalizationKeys.ModelInventoryHelpPurposeUserProvided),
            _ => T(LocalizationKeys.ModelInventoryHelpPurposeDefault),
        };
    }

    private string GetModelUseExample(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry)
    {
        var key = entry?.Key ?? candidate.MappingKey ?? string.Empty;
        return key switch
        {
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey => T(LocalizationKeys.ModelInventoryHelpUseMetricIndoor),
            Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorKey => T(LocalizationKeys.ModelInventoryHelpUseMetricOutdoor),
            Iw3DepthModelMapper.DepthAnythingV2SmallKey => T(LocalizationKeys.ModelInventoryHelpUseV2Small),
            Iw3DepthModelMapper.DepthAnythingSmallKey => T(LocalizationKeys.ModelInventoryHelpUseSmall),
            Iw3DepthModelMapper.DepthAnythingBaseKey => T(LocalizationKeys.ModelInventoryHelpUseBase),
            Iw3DepthModelMapper.DepthAnythingLargeKey => T(LocalizationKeys.ModelInventoryHelpUseLarge),
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimSmallKey => T(LocalizationKeys.ModelInventoryHelpUseMetricHypersimSmall),
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimBaseKey => T(LocalizationKeys.ModelInventoryHelpUseMetricHypersimBase),
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiSmallKey => T(LocalizationKeys.ModelInventoryHelpUseMetricVkittiSmall),
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiBaseKey => T(LocalizationKeys.ModelInventoryHelpUseMetricVkittiBase),
            Iw3DepthModelMapper.DistillAnyDepthSmallKey => T(LocalizationKeys.ModelInventoryHelpUseDistillAnyDepthSmall),
            Iw3DepthModelMapper.DepthAnything3MonoLargeKey => T(LocalizationKeys.ModelInventoryHelpUseDepthAnything3MonoLarge),
            Iw3DepthModelMapper.DepthAnything3MonoLarge3dTvKey => T(LocalizationKeys.ModelInventoryHelpUseDepthAnything3MonoLarge3dTv),
            _ when entry?.RedistributionDecision ==
                Iw3DepthModelRedistributionDecision.BlockedUnclearLicense => T(LocalizationKeys.ModelInventoryHelpUseUserProvided),
            _ => T(LocalizationKeys.ModelInventoryHelpUseDefault),
        };
    }

    private string GetSceneScopeText(Iw3DepthModelRegistryEntry? entry)
    {
        var category = entry?.Category ?? string.Empty;
        if (category.Contains("indoor/outdoor", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ModelInventorySceneIndoorOutdoor);
        }

        if (category.Contains("indoor", StringComparison.OrdinalIgnoreCase) ||
            category.Contains("Hypersim", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ModelInventorySceneIndoor);
        }

        if (category.Contains("outdoor", StringComparison.OrdinalIgnoreCase) ||
            category.Contains("VKITTI", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ModelInventorySceneOutdoor);
        }

        return T(LocalizationKeys.ModelInventorySceneGeneral);
    }

    private string GetDepthTypeText(Iw3DepthModelRegistryEntry? entry) =>
        entry?.DepthType switch
        {
            Iw3DepthModelDepthType.Metric => T(LocalizationKeys.ModelInventoryDepthTypeMetric),
            Iw3DepthModelDepthType.Relative => T(LocalizationKeys.ModelInventoryDepthTypeRelative),
            Iw3DepthModelDepthType.ForcedDisparity => T(LocalizationKeys.ModelInventoryDepthTypeForcedDisparity),
            _ => T(LocalizationKeys.ModelInventoryDepthTypeModelDefined),
        };

    private string GetModelSourceText(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelRegistryEntry? entry)
    {
        if (entry?.RedistributionDecision ==
            Iw3DepthModelRedistributionDecision.BlockedUnclearLicense)
        {
            return T(LocalizationKeys.ModelInventorySourceUserProvided);
        }

        if (entry?.Availability == Iw3DepthModelAvailability.EmbeddedBase)
        {
            return T(LocalizationKeys.ModelInventorySourceBundled);
        }

        if (entry?.IsPublicPackEligible == true)
        {
            return T(LocalizationKeys.ModelInventorySourceModelPack);
        }

        return candidate.IsCatalogManaged
            ? T(LocalizationKeys.ModelInventorySourceLocalCatalog)
            : T(LocalizationKeys.ModelInventorySourceLocalFile);
    }

    private string GetSizeClassText(Iw3DepthModelRegistryEntry? entry)
    {
        var name = entry?.DepthModelName ?? string.Empty;
        var key = entry?.Key ?? string.Empty;
        if (name.EndsWith("_S", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("small", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ModelInventorySizeSmallFaster);
        }

        if (name.EndsWith("_B", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("base", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ModelInventorySizeBaseBalanced);
        }

        if (name.EndsWith("_L", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("large", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ModelInventorySizeLargeHeavier);
        }

        return T(LocalizationKeys.ModelInventorySizeStandard);
    }

    private string CreateRuntimeDependenciesInventoryText()
    {
        var dependency = _dependencyHealth?.Iw3RuntimeDependencies ?? new ToolDependencyHealth(
            ToolHealthStatus.Missing,
            ToolHealthDetailKind.Iw3RuntimeDependenciesMissing,
            _toolPaths.Iw3DefaultStereoRuntimeDependencyFile);
        var statusText = dependency.Status == ToolHealthStatus.Found
            ? T(LocalizationKeys.CommonFound)
            : T(LocalizationKeys.CommonMissing);
        return string.Join(
            Environment.NewLine,
            [
                $"- {Path.GetFileName(dependency.ExpectedPath)}",
                $"  {statusText}",
                $"  {dependency.ExpectedPath}",
                $"  {T(LocalizationKeys.ModelInventoryRuntimeDependencyNote)}",
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

            AddLogResolved(T(
                LocalizationKeys.SystemLogOpenedEngineFolderFormat,
                ("path", _toolPaths.Iw3EngineDirectory)));
        }
        catch (Exception exception)
        {
            AddLogResolved(T(
                LocalizationKeys.SystemLogOpenEngineFolderFailedFormat,
                ("message", exception.Message)));
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

            AddLogResolved(T(
                LocalizationKeys.SystemLogOpenedModelsFolderFormat,
                ("path", _toolPaths.ModelsDirectory)));
        }
        catch (Exception exception)
        {
            AddLogResolved(T(
                LocalizationKeys.SystemLogOpenModelsFolderFailedFormat,
                ("message", exception.Message)));
        }
    }

    private void SelectAppSection(AppSection section)
    {
        if (!CanUseShellNavigation)
        {
            return;
        }

        SelectedAppSection = section;
    }

    public void ExpandSidebarForHover()
    {
        if (IsImageExportRunning)
        {
            return;
        }

        if (!IsSidebarPinnedExpanded)
        {
            IsSidebarHoverExpanded = true;
        }
    }

    public void CollapseSidebarAfterHover()
    {
        if (IsImageExportRunning)
        {
            return;
        }

        if (!IsSidebarPinnedExpanded)
        {
            IsSidebarHoverExpanded = false;
        }
    }

    private void ToggleSidebar()
    {
        if (!CanUseShellNavigation)
        {
            return;
        }

        IsSidebarExpanded = !IsSidebarExpanded;
        IsSidebarHoverExpanded = false;
    }

    private void SelectImageConversionMode(ImageConversionMode mode)
    {
        if (!CanInteractWithImageWorkflow)
        {
            return;
        }

        if (SelectedImageConversionMode == mode)
        {
            IsImageWorkflowChooserExpanded = false;
            return;
        }

        var previousMode = GetImageModeDisplayText(SelectedImageConversionMode);
        SelectedImageConversionMode = mode;
        IsImageWorkflowChooserExpanded = false;
        ApplyImageSetupChanged(T(
            LocalizationKeys.ImageLogWorkflowChangedFormat,
            ("previous", previousMode),
            ("next", GetImageModeDisplayText(mode))));
        if (SelectedImageConversionStep == ImageConversionStep.ModeAndSource)
        {
            return;
        }

        RaiseImageConversionPropertiesChanged();
    }

    private void ToggleImageWorkflowChooser()
    {
        if (!CanInteractWithImageWorkflow)
        {
            return;
        }

        if (!HasSelectedImageMode)
        {
            IsImageWorkflowChooserExpanded = true;
            return;
        }

        IsImageWorkflowChooserExpanded = !IsImageWorkflowChooserExpanded;
    }

    private void SelectImageConversionStep(ImageConversionStep step)
    {
        if (!CanUseImageStepNavigation)
        {
            return;
        }

        if (step == ImageConversionStep.Setup && !CanOpenImageSetupStep)
        {
            AddImageLogResolved(T(LocalizationKeys.ImageLogAnalyzeBeforeSetup));
            return;
        }

        if (step == ImageConversionStep.PreviewAndExport && !CanOpenImagePreviewExportStep)
        {
            AddImageLogResolved(T(LocalizationKeys.ImageLogCompleteSetupBeforeConversion));
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
            AddImageLogResolved(T(LocalizationKeys.ImageLogCannotContinue));
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
            AddImageLogResolved(T(LocalizationKeys.ImageLogSetupReadyForReview));
        }
    }

    private void ContinueWithImageConversion()
    {
        if (!CanContinueWithImageConversion)
        {
            AddImageLogResolved(T(LocalizationKeys.ImageLogReviewSetupBeforePreparing));
            return;
        }

        _hasEnteredImagePreviewExportStage = true;
        if (IsImageStereoModeSelected)
        {
            AddImageLogResolved(T(LocalizationKeys.ImageLogPreparedStereo));
        }
        else
        {
            AddImageLogResolved(T(LocalizationKeys.ImageLogPreparedParallax));
        }

        ShowLogCopyNotification(
            T(LocalizationKeys.ImageLogPreparedToast),
            T(LocalizationKeys.ImageLogPreparedToast));
        RaiseImageConversionPropertiesChanged();
    }

    private string CreateImagePreviewExportStatusText()
    {
        if (IsImageExportRunning)
        {
            return ImageExportStatusText;
        }

        if (!string.IsNullOrWhiteSpace(_lastImageExportErrorEnglishText))
        {
            return ImageExportErrorText;
        }

        if (IsImageExportOutputOutdated)
        {
            return ImageExportOutdatedText;
        }

        if (HasCurrentImageConversionOutput)
        {
            var fileName = Path.GetFileName(_lastImageExportPrimaryPath);
            return T(LocalizationKeys.ImageOutputCompletedFormat, ("fileName", fileName));
        }

        return IsImageStereoModeSelected
            ? $"{T(LocalizationKeys.ImageOutputPrepared)} {CreateImageStereoExportReadinessText()}"
            : $"{T(LocalizationKeys.ImageOutputPrepared)} {CreateImageParallaxExportReadinessText()}";
    }

    private Iw3ImageStereoExportReadiness? EvaluateCurrentImageStereoExportReadiness()
    {
        if (SelectedImagePath is null || _dependencyHealth is null)
        {
            return null;
        }

        var outputDirectory = GetDefaultImageExportDirectory(SelectedImagePath);
        var request = CreateImageStereoExportRequest(outputDirectory);
        return Iw3ImageStereoExporter.EvaluateReadiness(request);
    }

    private string CreateImageStereoExportReadinessText()
    {
        if (_dependencyHealth is null)
        {
            return T(LocalizationKeys.ImageReadinessRefreshSystemStatusStereo);
        }

        if (SelectedImagePath is null)
        {
            return T(LocalizationKeys.ImageReadinessMissingImageBeforeExportStereo);
        }

        var readiness = EvaluateCurrentImageStereoExportReadiness();
        if (readiness?.CanExport == true)
        {
            return T(LocalizationKeys.ImageReadinessReady);
        }

        var firstIssue = readiness?.Issues.FirstOrDefault();
        return firstIssue is null
            ? T(LocalizationKeys.ImageReadinessStereoNotReady)
            : LocalizeImageExportReadinessIssue(firstIssue.EnglishMessage);
    }

    private Iw3ImageParallaxExportReadiness? EvaluateCurrentImageParallaxExportReadiness()
    {
        if (SelectedImagePath is null || _dependencyHealth is null)
        {
            return null;
        }

        var outputDirectory = GetDefaultImageExportDirectory(SelectedImagePath);
        var request = CreateImageParallaxExportRequest(outputDirectory);
        return Iw3ImageParallaxExporter.EvaluateReadiness(request);
    }

    private string CreateImageParallaxExportReadinessText()
    {
        if (_dependencyHealth is null)
        {
            return T(LocalizationKeys.ImageReadinessRefreshSystemStatusParallax);
        }

        if (SelectedImagePath is null)
        {
            return T(LocalizationKeys.ImageReadinessMissingImageBeforeExportParallax);
        }

        var readiness = EvaluateCurrentImageParallaxExportReadiness();
        if (readiness?.CanExport == true)
        {
            return T(LocalizationKeys.ImageReadinessReadyParallax);
        }

        var firstIssue = readiness?.Issues.FirstOrDefault();
        return firstIssue is null
            ? T(LocalizationKeys.ImageReadinessParallaxNotReady)
            : LocalizeImageExportReadinessIssue(firstIssue.EnglishMessage);
    }

    private Task ConvertImageAsync()
    {
        if (IsImageParallaxModeSelected)
        {
            return ConvertParallaxImageAsync();
        }

        return ExportStereoscopicImageAsync();
    }

    private async Task ExportStereoscopicImageAsync()
    {
        if (!CanExportStereoscopicImage || SelectedImagePath is null)
        {
            AddImageLogResolved(T(
                LocalizationKeys.ImageLogStereoBlockedFormat,
                ("reason", ImageStereoExportDisabledReasonText)));
            return;
        }

        ResetImageExportState();
        IsImageExportRunning = true;
        _imageExportProgressPercent = 0;
        _imageExportProgressKey = LocalizationKeys.ImageProgressPreparingStereo;
        RaiseImageExportPropertiesChanged();

        var outputDirectory = GetDefaultImageExportDirectory(SelectedImagePath);
        var outputFormat = MapImageStereoExportFormat(SelectedStereoOutputFormat);
        var selectedModel = CreateSelectedImageLocalModelSelection();
        var plannedOutputPaths = ImageStereoExportPathBuilder.CreateOutputPaths(
            SelectedImagePath,
            outputDirectory,
            outputFormat,
            selectedModel?.MappingKey ?? selectedModel?.DisplayName,
            File.Exists);
        AddImageLogResolved(T(LocalizationKeys.ImageLogStereoStarted));
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogSourceImageFormat,
            ("fileName", Path.GetFileName(SelectedImagePath))));
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogSelectedModelFormat,
            ("model", selectedModel?.DisplayName ?? T(LocalizationKeys.ImageModelNone)),
            ("iw3Model", selectedModel?.Iw3DepthModelName ?? "-")));
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogStereoFormatFormat,
            ("format", SelectedStereoOutputFormatDisplayText)));
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogOutputPathFormat,
            ("path", plannedOutputPaths.PrimaryOutputPath)));
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogBundledPythonFormat,
            ("path", _toolPaths.PythonExecutable)));
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogBundledIw3PackageFormat,
            ("path", _toolPaths.Iw3PackageDirectory)));

        await Task.Yield();

        try
        {
            var request = CreateImageStereoExportRequest(outputDirectory);
            var progress = new InlineProgress<ImageStereoExportProgress>(ApplyImageExportProgress);
            var result = await _imageStereoExporter.ExportAsync(request, progress);

            if (!string.IsNullOrWhiteSpace(result.CommandPreview))
            {
                AddImageLogResolved(T(LocalizationKeys.ImageLogIw3CommandFormat, ("command", result.CommandPreview)));
            }

            if (result.ExitCode.HasValue)
            {
                AddImageLogResolved(T(
                    LocalizationKeys.ImageLogProcessExitCodeFormat,
                    ("processName", "iw3"),
                    ("exitCode", result.ExitCode.Value)));
            }

            if (!string.IsNullOrWhiteSpace(result.StandardOutputSummary))
            {
                AddImageLogResolved(T(LocalizationKeys.ImageLogIw3StdoutFormat, ("detail", result.StandardOutputSummary)));
            }

            if (!string.IsNullOrWhiteSpace(result.StandardErrorSummary))
            {
                AddImageLogResolved(T(LocalizationKeys.ImageLogIw3StderrFormat, ("detail", result.StandardErrorSummary)));
            }

            if (result.Success)
            {
                var summary = LocalizeImageStereoExportSummary(result);
                _lastImageExportPrimaryPath = result.PrimaryOutputPath ?? string.Empty;
                _lastImageExportOutputDirectory = result.OutputDirectory;
                _lastImageExportGeneratedFiles = result.GeneratedFiles;
                _lastImageExportErrorEnglishText = string.Empty;
                _lastImageExportErrorSpanishText = string.Empty;
                _isImageExportOutputOutdated = false;
                AddImageLogResolved(summary);
                foreach (var generatedFile in result.GeneratedFiles)
                {
                    AddImageLogResolved(T(LocalizationKeys.ImageLogGeneratedImageFileFormat, ("path", generatedFile)));
                }
            }
            else
            {
                var summary = LocalizeImageStereoExportSummary(result);
                _lastImageExportErrorEnglishText = summary;
                _lastImageExportErrorSpanishText = summary;
                _lastImageExportGeneratedFiles = [];
                _lastImageExportPrimaryPath = string.Empty;
                _isImageExportOutputOutdated = false;
                AddImageLogResolved(summary);
                if (!string.IsNullOrWhiteSpace(result.TechnicalDetail))
                {
                    AddImageLogResolved(T(LocalizationKeys.ImageLogTechnicalDetailFormat, ("detail", result.TechnicalDetail)));
                }
            }
        }
        catch (Exception exception)
        {
            _lastImageExportErrorEnglishText = T(LocalizationKeys.ImageErrorExportFailedFormat, ("message", exception.Message));
            _lastImageExportErrorSpanishText = _lastImageExportErrorEnglishText;
            _lastImageExportGeneratedFiles = [];
            _lastImageExportPrimaryPath = string.Empty;
            _isImageExportOutputOutdated = false;
            AddImageLogResolved(_lastImageExportErrorEnglishText);
            AddImageLogResolved(T(LocalizationKeys.ImageLogTechnicalDetailFormat, ("detail", exception)));
        }
        finally
        {
            IsImageExportRunning = false;
            RaiseImageExportPropertiesChanged();
        }
    }

    private async Task ConvertParallaxImageAsync()
    {
        if (!CanConvertImage || !IsImageParallaxModeSelected || SelectedImagePath is null)
        {
            AddImageLogResolved(T(
                LocalizationKeys.ImageLogParallaxBlockedFormat,
                ("reason", ImageConvertDisabledReasonText)));
            return;
        }

        ResetImageExportState();
        IsImageExportRunning = true;
        _imageExportProgressPercent = 0;
        _imageExportProgressKey = LocalizationKeys.ImageProgressPreparingParallax;
        RaiseImageExportPropertiesChanged();

        var outputDirectory = GetDefaultImageExportDirectory(SelectedImagePath);
        var selectedModel = CreateSelectedImageLocalModelSelection();
        var plannedOutputPaths = ImageParallaxExportPathBuilder.CreateOutputPaths(
            SelectedImagePath,
            outputDirectory,
            selectedModel?.MappingKey ?? selectedModel?.DisplayName,
            SelectedParallaxDuration,
            File.Exists);
        AddImageLogResolved(T(LocalizationKeys.ImageLogParallaxStarted));
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogSourceImageFormat,
            ("fileName", Path.GetFileName(SelectedImagePath))));
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogSelectedModelFormat,
            ("model", selectedModel?.DisplayName ?? T(LocalizationKeys.ImageModelNone)),
            ("iw3Model", selectedModel?.Iw3DepthModelName ?? "-")));
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogParallaxSettingsFormat,
            ("depthIntensity", GetOptionDisplayName(ParallaxDepthIntensityOptions, SelectedParallaxDepthIntensity)),
            ("motionDirection", GetOptionDisplayName(ParallaxMotionDirectionOptions, SelectedParallaxMotionDirection)),
            ("zoomAmplitude", GetOptionDisplayName(ParallaxZoomAmplitudeOptions, SelectedParallaxZoomAmplitude)),
            ("duration", GetOptionDisplayName(ParallaxDurationOptions, SelectedParallaxDuration)),
            ("smoothing", GetOptionDisplayName(ParallaxSmoothingOptions, SelectedParallaxSmoothing)),
            ("layerBehavior", GetOptionDisplayName(ParallaxLayerBehaviorOptions, SelectedParallaxLayerBehavior))));
        AddImageLogResolved(T(LocalizationKeys.ImageLogHighResolutionParallaxWarning));
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogOutputPathFormat,
            ("path", plannedOutputPaths.PrimaryOutputPath)));
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogBundledPythonFormat,
            ("path", _toolPaths.PythonExecutable)));
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogBundledFfmpegFormat,
            ("path", _toolPaths.FfmpegExecutable)));
        AddImageLogResolved(T(
            LocalizationKeys.ImageLogParallaxHelperFormat,
            ("path", _toolPaths.V3dfyParallaxHelperScript)));

        await Task.Yield();

        try
        {
            var request = CreateImageParallaxExportRequest(outputDirectory);
            var progress = new InlineProgress<ImageParallaxExportProgress>(ApplyImageParallaxExportProgress);
            var result = await _imageParallaxExporter.ExportAsync(request, progress);

            if (!string.IsNullOrWhiteSpace(result.DepthExportCommandPreview))
            {
                AddImageLogResolved(T(
                    LocalizationKeys.ImageLogIw3DepthCommandFormat,
                    ("command", result.DepthExportCommandPreview)));
            }

            if (!string.IsNullOrWhiteSpace(result.FrameGenerationCommandPreview))
            {
                AddImageLogResolved(T(
                    LocalizationKeys.ImageLogParallaxFrameCommandFormat,
                    ("command", result.FrameGenerationCommandPreview)));
            }

            if (!string.IsNullOrWhiteSpace(result.FfmpegCommandPreview))
            {
                AddImageLogResolved(T(
                    LocalizationKeys.ImageLogFfmpegCommandFormat,
                    ("command", result.FfmpegCommandPreview)));
            }

            AddProcessExitLog("iw3 depth", result.DepthExportExitCode);
            AddProcessExitLog("parallax frames", result.FrameGenerationExitCode);
            AddProcessExitLog("FFmpeg", result.FfmpegExitCode);

            if (!string.IsNullOrWhiteSpace(result.StandardOutputSummary))
            {
                AddImageLogResolved(T(LocalizationKeys.ImageLogParallaxStdoutFormat, ("detail", result.StandardOutputSummary)));
            }

            if (!string.IsNullOrWhiteSpace(result.StandardErrorSummary))
            {
                AddImageLogResolved(T(LocalizationKeys.ImageLogParallaxStderrFormat, ("detail", result.StandardErrorSummary)));
            }

            if (result.Success)
            {
                var summary = LocalizeImageParallaxExportSummary(result);
                _lastImageExportPrimaryPath = result.PrimaryOutputPath ?? string.Empty;
                _lastImageExportOutputDirectory = result.OutputDirectory;
                _lastImageExportGeneratedFiles = result.GeneratedFiles;
                _lastImageExportErrorEnglishText = string.Empty;
                _lastImageExportErrorSpanishText = string.Empty;
                _isImageExportOutputOutdated = false;
                AddImageLogResolved(summary);
                foreach (var generatedFile in result.GeneratedFiles)
                {
                    AddImageLogResolved(T(LocalizationKeys.ImageLogGeneratedParallaxFileFormat, ("path", generatedFile)));
                }
            }
            else
            {
                var summary = LocalizeImageParallaxExportSummary(result);
                _lastImageExportErrorEnglishText = summary;
                _lastImageExportErrorSpanishText = summary;
                _lastImageExportGeneratedFiles = [];
                _lastImageExportPrimaryPath = string.Empty;
                _isImageExportOutputOutdated = false;
                AddImageLogResolved(summary);
                if (!string.IsNullOrWhiteSpace(result.TechnicalDetail))
                {
                    AddImageLogResolved(T(LocalizationKeys.ImageLogTechnicalDetailFormat, ("detail", result.TechnicalDetail)));
                }
            }
        }
        catch (Exception exception)
        {
            _lastImageExportErrorEnglishText = T(LocalizationKeys.ImageErrorParallaxExportFailedFormat, ("message", exception.Message));
            _lastImageExportErrorSpanishText = _lastImageExportErrorEnglishText;
            _lastImageExportGeneratedFiles = [];
            _lastImageExportPrimaryPath = string.Empty;
            _isImageExportOutputOutdated = false;
            AddImageLogResolved(_lastImageExportErrorEnglishText);
            AddImageLogResolved(T(LocalizationKeys.ImageLogTechnicalDetailFormat, ("detail", exception)));
        }
        finally
        {
            IsImageExportRunning = false;
            RaiseImageExportPropertiesChanged();
        }
    }

    private void AddProcessExitLog(string processName, int? exitCode)
    {
        if (!exitCode.HasValue)
        {
            return;
        }

        AddImageLogResolved(T(
            LocalizationKeys.ImageLogProcessExitCodeFormat,
            ("processName", processName),
            ("exitCode", exitCode.Value)));
    }

    private string LocalizeImageExportReadinessIssue(string englishMessage)
    {
        if (string.IsNullOrWhiteSpace(englishMessage))
        {
            return string.Empty;
        }

        if (TrySplitPrefixedLabelPath(
                englishMessage,
                "Missing bundled ",
                out var label,
                out var path))
        {
            var key = label.Contains("directory", StringComparison.OrdinalIgnoreCase)
                ? LocalizationKeys.ImageReadinessMissingBundledDirectoryFormat
                : LocalizationKeys.ImageReadinessMissingBundledFileFormat;
            return T(key, ("label", label), ("path", path));
        }

        if (englishMessage.Contains("Select a bundled local depth model", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ImageReadinessSelectDepthModel);
        }

        if (englishMessage.Contains("not mapped to a verified iw3 depth model", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ImageReadinessModelUnmapped);
        }

        if (englishMessage.Contains("not verified for image input", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ImageReadinessModelNotImageVerified);
        }

        if (TrySplitAfterPrefix(
                englishMessage,
                "Selected bundled model file is missing: ",
                out var missingModelPath))
        {
            return T(LocalizationKeys.ImageReadinessSelectedModelFileMissingFormat, ("path", missingModelPath));
        }

        if (englishMessage.Contains("selected anaglyph mode is not supported", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ImageReadinessUnsupportedAnaglyphMode);
        }

        if (englishMessage.Contains("Swap eyes requires", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ImageReadinessSwapEyesRequiresCrossEyed);
        }

        if (TrySplitAfterPrefix(
                englishMessage,
                "Bundled iw3 image export is blocked because IW3_CLI_CAPABILITIES.json does not verify ",
                out var missingOptions))
        {
            return T(
                LocalizationKeys.ImageReadinessIw3CapabilitiesMissingFormat,
                ("options", missingOptions.TrimEnd('.')));
        }

        return englishMessage;
    }

    private string LocalizeImageStereoExportSummary(ImageStereoExportResult result)
    {
        if (result.Success)
        {
            return T(LocalizationKeys.ImageExportSummaryStereoCompleted);
        }

        return result.WasBlocked
            ? T(LocalizationKeys.ImageExportSummaryStereoBlocked)
            : T(LocalizationKeys.ImageExportSummaryStereoFailed);
    }

    private string LocalizeImageParallaxExportSummary(ImageParallaxExportResult result)
    {
        if (result.Success)
        {
            return T(LocalizationKeys.ImageExportSummaryParallaxCompleted);
        }

        return result.WasBlocked
            ? T(LocalizationKeys.ImageExportSummaryParallaxBlocked)
            : T(LocalizationKeys.ImageExportSummaryParallaxFailed);
    }

    private string LocalizeImageStereoExportProgress(ImageStereoExportProgress progress)
    {
        var message = progress.EnglishMessage;
        if (message.Contains("Starting bundled iw3 image stereo export", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ImageExportProgressStereoStarting);
        }

        if (message.Contains("Bundled iw3 image stereo export completed", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ImageExportProgressStereoCompleted);
        }

        if (TryExtractAfter(message, "exit code ", out var exitCode))
        {
            return T(
                LocalizationKeys.ImageExportProgressStereoFailedFormat,
                ("exitCode", exitCode.TrimEnd('.')));
        }

        return message;
    }

    private string LocalizeImageParallaxExportProgress(ImageParallaxExportProgress progress)
    {
        var message = progress.EnglishMessage;
        if (message.Contains("Starting bundled iw3 depth export", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ImageExportProgressParallaxDepthStarting);
        }

        if (TrySplitAfterPrefix(message, "Expected depth output: ", out var depthOutputPath))
        {
            return T(LocalizationKeys.ImageExportProgressParallaxDepthOutputFormat, ("path", depthOutputPath));
        }

        if (message.Contains("Bundled iw3 depth export failed", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ImageExportProgressParallaxDepthFailed);
        }

        if (TrySplitAfterPrefix(message, "Expected depth map was not created: ", out var missingDepthPath))
        {
            return T(LocalizationKeys.ImageExportProgressExpectedDepthMapMissingFormat, ("path", missingDepthPath));
        }

        if (TryBetween(message, "Depth export completed in ", ".", out var depthElapsed))
        {
            return T(LocalizationKeys.ImageExportProgressParallaxDepthCompletedFormat, ("elapsed", depthElapsed));
        }

        if (TryBetween(message, "Generating ", " 2.5D parallax frames", out var frameCount))
        {
            return T(LocalizationKeys.ImageExportProgressParallaxFramesGeneratingFormat, ("frameCount", frameCount));
        }

        if (TrySplitAfterPrefix(message, "Parallax frame directory: ", out var frameDirectory))
        {
            return T(LocalizationKeys.ImageExportProgressParallaxFrameDirectoryFormat, ("path", frameDirectory));
        }

        if (message.Contains("2.5D parallax frame generation failed", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ImageExportProgressParallaxFramesFailed);
        }

        if (TrySplitAfterPrefix(message, "No parallax frames were created under: ", out var noFramesPath))
        {
            return T(LocalizationKeys.ImageExportProgressNoParallaxFramesFormat, ("path", noFramesPath));
        }

        if (TryBetween(message, "Parallax frame generation completed: ", " frame(s) in ", out var completedFrames) &&
            TryBetween(message, " frame(s) in ", ".", out var framesElapsed))
        {
            return T(
                LocalizationKeys.ImageExportProgressParallaxFramesCompletedFormat,
                ("frameCount", completedFrames),
                ("elapsed", framesElapsed));
        }

        if (message.Contains("Starting bundled FFmpeg MP4 encoding", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ImageExportProgressFfmpegStarting);
        }

        if (TrySplitAfterPrefix(message, "FFmpeg output path: ", out var ffmpegOutputPath))
        {
            return T(LocalizationKeys.ImageExportProgressFfmpegOutputFormat, ("path", ffmpegOutputPath));
        }

        if (message.Contains("Bundled FFmpeg parallax encoding failed", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.ImageExportProgressFfmpegFailed);
        }

        if (TrySplitAfterPrefix(message, "Expected MP4 output was not created: ", out var missingMp4Path))
        {
            return T(LocalizationKeys.ImageExportProgressExpectedMp4MissingFormat, ("path", missingMp4Path));
        }

        if (TryBetween(message, "FFmpeg encoding completed in ", ".", out var ffmpegElapsed))
        {
            return T(LocalizationKeys.ImageExportProgressFfmpegCompletedFormat, ("elapsed", ffmpegElapsed));
        }

        if (TrySplitAfterPrefix(message, "2.5D parallax conversion completed: ", out var completedPath))
        {
            return T(LocalizationKeys.ImageExportProgressParallaxCompletedFormat, ("path", completedPath));
        }

        return message;
    }

    private ImageStereoExportRequest CreateImageStereoExportRequest(string outputDirectory)
    {
        if (SelectedImagePath is null)
        {
            throw new InvalidOperationException("A source image must be selected before image export.");
        }

        if (_dependencyHealth is null)
        {
            throw new InvalidOperationException("System status must be refreshed before image export.");
        }

        return new(
            SourcePath: SelectedImagePath,
            OutputDirectory: outputDirectory,
            OutputFormat: MapImageStereoExportFormat(SelectedStereoOutputFormat),
            EyeSeparationPercent: ParseStereoEyeSeparationPercent(SelectedStereoEyeSeparation),
            Convergence: SelectedStereoConvergence,
            SwapEyes: ImageStereoSwapEyes,
            AnaglyphMode: NormalizeStereoAnaglyphMode(SelectedStereoAnaglyphMode),
            ExpectedToolPaths: _toolPaths,
            DependencyHealth: _dependencyHealth,
            SelectedLocalModel: CreateSelectedImageLocalModelSelection());
    }

    private ImageParallaxExportRequest CreateImageParallaxExportRequest(string outputDirectory)
    {
        if (SelectedImagePath is null)
        {
            throw new InvalidOperationException("A source image must be selected before image conversion.");
        }

        if (_dependencyHealth is null)
        {
            throw new InvalidOperationException("System status must be refreshed before image conversion.");
        }

        return new(
            SourcePath: SelectedImagePath,
            OutputDirectory: outputDirectory,
            DepthIntensity: SelectedParallaxDepthIntensity,
            MotionDirection: SelectedParallaxMotionDirection,
            ZoomAmplitude: SelectedParallaxZoomAmplitude,
            Duration: SelectedParallaxDuration,
            Smoothing: SelectedParallaxSmoothing,
            LayerBehavior: SelectedParallaxLayerBehavior,
            FramesPerSecond: 24,
            ExpectedToolPaths: _toolPaths,
            DependencyHealth: _dependencyHealth,
            SelectedLocalModel: CreateSelectedImageLocalModelSelection());
    }

    private LocalModelPlanSelection? CreateSelectedImageLocalModelSelection() =>
        SelectedLocalModelCandidate is null
            ? null
            : LocalModelPlanSelection.FromCandidate(SelectedLocalModelCandidate);

    private void ApplyImageExportProgress(ImageStereoExportProgress progress)
    {
        _imageExportProgressPercent = Math.Clamp(progress.ProgressPercent, 0, 100);
        _imageExportProgressKey = null;
        var message = LocalizeImageStereoExportProgress(progress);
        _imageExportProgressEnglishText = message;
        _imageExportProgressSpanishText = message;
        AddImageLogResolved(message);
        RaiseImageExportPropertiesChanged();
    }

    private void ApplyImageParallaxExportProgress(ImageParallaxExportProgress progress)
    {
        _imageExportProgressPercent = Math.Clamp(progress.ProgressPercent, 0, 100);
        _imageExportProgressKey = null;
        var message = LocalizeImageParallaxExportProgress(progress);
        _imageExportProgressEnglishText = message;
        _imageExportProgressSpanishText = message;
        AddImageLogResolved(message);
        RaiseImageExportPropertiesChanged();
    }

    private void OpenImageOutputFolder()
    {
        if (!CanOpenImageOutputFolder)
        {
            AddImageLogResolved(T(LocalizationKeys.ImageLogOpenOutputFolderUnavailable));
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _lastImageExportOutputDirectory,
                UseShellExecute = true,
            });

            AddImageLogResolved(T(
                LocalizationKeys.ImageLogOpenedOutputFolderFormat,
                ("path", _lastImageExportOutputDirectory)));
        }
        catch (Exception exception)
        {
            AddImageLogResolved(T(
                LocalizationKeys.ImageLogOpenOutputFolderFailedFormat,
                ("message", exception.Message)));
        }
    }

    private void StartNewImageConversion()
    {
        if (!CanStartNewImageConversion)
        {
            return;
        }

        SelectedImagePath = null;
        _selectedImageMetadata = null;
        ResetImageSetupState();
        ResetImageExportState();
        _hasEnteredImagePreviewExportStage = false;
        SelectedImageConversionStep = ImageConversionStep.ModeAndSource;
        AddImageLogResolved(T(LocalizationKeys.ImageLogReset));
        RaiseImageConversionPropertiesChanged();
    }

    private void ResetImageExportState()
    {
        _imageExportProgressPercent = 0;
        _imageExportProgressKey = LocalizationKeys.ImageProgressNotStarted;
        _imageExportProgressEnglishText = string.Empty;
        _imageExportProgressSpanishText = string.Empty;
        _lastImageExportPrimaryPath = string.Empty;
        _lastImageExportOutputDirectory = string.Empty;
        _lastImageExportGeneratedFiles = [];
        _lastImageExportErrorEnglishText = string.Empty;
        _lastImageExportErrorSpanishText = string.Empty;
        _isImageExportOutputOutdated = false;
        RaiseImageExportPropertiesChanged();
    }

    private void MarkImageExportOutputOutdated()
    {
        _imageExportProgressPercent = 0;
        _imageExportProgressKey = LocalizationKeys.ImageOutputOutdated;
        _imageExportProgressEnglishText = string.Empty;
        _imageExportProgressSpanishText = string.Empty;
        _lastImageExportErrorEnglishText = string.Empty;
        _lastImageExportErrorSpanishText = string.Empty;
        _isImageExportOutputOutdated = true;
        RaiseImageExportPropertiesChanged();
    }

    private static string GetDefaultImageExportDirectory(string sourceImagePath)
    {
        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceImagePath));
        if (!string.IsNullOrWhiteSpace(sourceDirectory))
        {
            return sourceDirectory;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }

    private string CreateExpectedImageOutputPath()
    {
        if (IsImageParallaxModeSelected)
        {
            return CreateExpectedImageParallaxOutputPath();
        }

        return CreateExpectedImageStereoOutputPath();
    }

    private string CreateExpectedImageStereoOutputPath()
    {
        if (SelectedImagePath is null)
        {
            return string.Empty;
        }

        var selectedModel = CreateSelectedImageLocalModelSelection();
        var outputPaths = ImageStereoExportPathBuilder.CreateOutputPaths(
            SelectedImagePath,
            GetDefaultImageExportDirectory(SelectedImagePath),
            MapImageStereoExportFormat(SelectedStereoOutputFormat),
            selectedModel?.MappingKey ?? selectedModel?.DisplayName,
            _ => false);
        return outputPaths.PrimaryOutputPath;
    }

    private string CreateExpectedImageParallaxOutputPath()
    {
        if (SelectedImagePath is null)
        {
            return string.Empty;
        }

        var selectedModel = CreateSelectedImageLocalModelSelection();
        var outputPaths = ImageParallaxExportPathBuilder.CreateOutputPaths(
            SelectedImagePath,
            GetDefaultImageExportDirectory(SelectedImagePath),
            selectedModel?.MappingKey ?? selectedModel?.DisplayName,
            SelectedParallaxDuration,
            _ => false);
        return outputPaths.PrimaryOutputPath;
    }

    private bool IsImageStereoOutputFormatSelectable(ImageStereoOutputFormat format)
    {
        if (format == ImageStereoOutputFormat.LeftRightPair)
        {
            return false;
        }

        if (_dependencyHealth is null)
        {
            return false;
        }

        return Iw3ImageStereoExporter.GetMissingImageCapabilityOptions(
            _dependencyHealth.Iw3CliCapabilities,
            MapImageStereoExportFormat(format)).Count == 0;
    }

    private void EnsureSelectedStereoOutputFormatIsSupported()
    {
        var normalizedAnaglyphMode = NormalizeSelectedStereoAnaglyphMode();
        if (IsImageStereoOutputFormatSelectable(SelectedStereoOutputFormat))
        {
            if (normalizedAnaglyphMode)
            {
                ResetImageExportState();
            }

            return;
        }

        var firstSupportedOption = StereoOutputFormatOptions.FirstOrDefault();
        if (firstSupportedOption is null)
        {
            return;
        }

        _selectedStereoOutputFormat = firstSupportedOption.Value;
        ResetImageExportState();
        OnPropertyChanged(nameof(SelectedStereoOutputFormat));
        OnPropertyChanged(nameof(SelectedStereoOutputFormatOption));
        OnPropertyChanged(nameof(SelectedStereoOutputFormatDisplayText));
        OnPropertyChanged(nameof(ImageStereoSummaryText));
        OnPropertyChanged(nameof(IsAnaglyphOutputSelected));
        OnPropertyChanged(nameof(ImageStereoAnaglyphModeVisibility));
    }

    private static string NormalizeStereoAnaglyphMode(string? value) =>
        string.Equals(value?.Trim(), SupportedStereoAnaglyphMode, StringComparison.OrdinalIgnoreCase)
            ? SupportedStereoAnaglyphMode
            : SupportedStereoAnaglyphMode;

    private bool NormalizeSelectedStereoAnaglyphMode()
    {
        var normalized = NormalizeStereoAnaglyphMode(_selectedStereoAnaglyphMode);
        if (string.Equals(_selectedStereoAnaglyphMode, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        _selectedStereoAnaglyphMode = normalized;
        OnPropertyChanged(nameof(SelectedStereoAnaglyphMode));
        return true;
    }

    private static ImageStereoExportFormat MapImageStereoExportFormat(ImageStereoOutputFormat format) =>
        format switch
        {
            ImageStereoOutputFormat.SideBySide => ImageStereoExportFormat.SideBySide,
            ImageStereoOutputFormat.HalfTopBottom => ImageStereoExportFormat.HalfTopBottom,
            ImageStereoOutputFormat.Anaglyph => ImageStereoExportFormat.Anaglyph,
            ImageStereoOutputFormat.LeftRightPair => ImageStereoExportFormat.LeftRightPair,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };

    private string GetStereoOutputFormatDisplayText(ImageStereoOutputFormat format) =>
        _allStereoOutputFormatOptions.FirstOrDefault(option => option.Value == format)?.DisplayName ??
        format.ToString();

    private string GetImageModeDisplayText(ImageConversionMode? mode) =>
        mode switch
        {
            ImageConversionMode.ParallaxPhoto => T(LocalizationKeys.ImageWorkflowParallaxTitle),
            ImageConversionMode.StereoscopicImage => T(LocalizationKeys.ImageWorkflowStereoTitle),
            null => T(LocalizationKeys.ImageWorkflowNone),
            _ => mode.Value.ToString(),
        };

    private string GetToggleText(bool value) =>
        value
            ? T(LocalizationKeys.ImageStereoToggleOn)
            : T(LocalizationKeys.ImageStereoToggleOff);

    private string GetModelDisplayName(LocalModelSelectionCandidate? candidate) =>
        candidate?.DisplayName ?? T(LocalizationKeys.ImageModelNone);

    private static double ParseStereoEyeSeparationPercent(string value)
    {
        var normalized = value.Replace("%", string.Empty, StringComparison.Ordinal).Trim();
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 4d;
    }

    private void ApplyImageSetupChanged(string? change = null)
    {
        var hadImageConversionOutput = HasAnyImageConversionOutput;
        change = string.IsNullOrWhiteSpace(change)
            ? T(LocalizationKeys.ImageLogSetupChangedDefault)
            : change;

        if (HasAnyImageConversionOutput)
        {
            MarkImageExportOutputOutdated();
        }
        else
        {
            ResetImageExportState();
        }

        if (_hasEnteredImagePreviewExportStage)
        {
            _hasEnteredImagePreviewExportStage = false;
            ShowLogCopyNotification(
                T(LocalizationKeys.ImageOutputOutdated),
                T(LocalizationKeys.ImageOutputOutdated));
            AddImageLogResolved(T(
                LocalizationKeys.ImageLogSetupChangedOutdatedPrepareFormat,
                ("change", EnsureSentence(change))));
        }
        else
        {
            AddImageLogResolved(
                hadImageConversionOutput
                    ? T(
                        LocalizationKeys.ImageLogSetupChangedOutdatedFormat,
                        ("change", EnsureSentence(change)))
                    : change);
        }

        RaiseImageConversionPropertiesChanged();
    }

    private static string EnsureSentence(string value)
    {
        var trimmed = value.Trim();
        return trimmed.EndsWith(".", StringComparison.Ordinal) ? trimmed : $"{trimmed}.";
    }

    private void ResetImageSetupState()
    {
        _selectedImageConversionMode = null;
        _isImageWorkflowChooserExpanded = false;
        _selectedParallaxDepthIntensity = "Low";
        _selectedParallaxMotionDirection = "Left to right";
        _selectedParallaxZoomAmplitude = "Subtle";
        _selectedParallaxDuration = "6 seconds";
        _selectedParallaxSmoothing = "Enabled";
        _selectedParallaxLayerBehavior = "Foreground / mid / background";
        _selectedStereoOutputFormat = ImageStereoOutputFormat.SideBySide;
        _selectedStereoEyeSeparation = "4.0%";
        _selectedStereoConvergence = "Neutral";
        _imageStereoSwapEyes = false;
        _selectedStereoAnaglyphMode = SupportedStereoAnaglyphMode;
    }

    private void SetImageSetupString(
        ref string field,
        string value,
        string labelKey,
        IReadOnlyList<LocalizedOptionViewModel<string>> options,
        [CallerMemberName] string? propertyName = null)
    {
        var previous = field;
        if (SetProperty(ref field, value, propertyName))
        {
            ApplyImageSetupChanged(T(
                LocalizationKeys.ImageLogSetupStringChangedFormat,
                ("label", T(labelKey)),
                ("previous", GetOptionDisplayName(options, previous)),
                ("next", GetOptionDisplayName(options, field))));
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
            AddVideoLogResolved(T(LocalizationKeys.VideoConversionOpenPlanBeforePreview));
            return;
        }

        SetHasEnteredPreviewConversionStage(true);
        ClearPreviewStageResetNotice();
        AddVideoLogResolved(T(LocalizationKeys.VideoConversionControlsReady));
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

        _isImageParallaxModelHelpContext = false;
        OnPropertyChanged(nameof(ModelHelpRows));
        OnPropertyChanged(nameof(ModelHelpTitleText));
        OnPropertyChanged(nameof(ModelHelpIntroText));
        IsModelHelpModalOpen = true;
    }

    private void ShowImageParallaxModelHelp()
    {
        if (!CanShowImageParallaxModelHelp ||
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

        _isImageParallaxModelHelpContext = true;
        OnPropertyChanged(nameof(ModelHelpRows));
        OnPropertyChanged(nameof(ModelHelpTitleText));
        OnPropertyChanged(nameof(ModelHelpIntroText));
        IsModelHelpModalOpen = true;
    }

    private void CloseModelHelp()
    {
        IsModelHelpModalOpen = false;
        _isImageParallaxModelHelpContext = false;
        RaiseModelHelpPropertiesChanged();
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
            logNameKey: isImageLogModal ? LocalizationKeys.ActivityLogImageName : LocalizationKeys.ActivityLogName,
            appendFailureToPreviewLog: false);
    }

    private void CopyPreviewLog()
    {
        CopyLogToClipboard(
            PreviewGenerationLogText,
            logNameKey: LocalizationKeys.ActivityLogPreviewName,
            appendFailureToPreviewLog: true);
    }

    private void CopyLogToClipboard(
        string logText,
        string logNameKey,
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
            var message = T(
                LocalizationKeys.ActivityLogCopyFailedFormat,
                ("logName", T(logNameKey)),
                ("message", exception.Message));
            if (appendFailureToPreviewLog)
            {
                AppendPreviewLogLine(message);
            }

            AddLogResolved(message);
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
        if (IsImageExportRunning)
        {
            return;
        }

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
        ShowGlobalBusyOverlay(LocalizationKeys.BusyImportingModelPack);
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

        var previousImageModel = GetModelDisplayName(_selectedLocalModelCandidate);
        var nextImageModel = GetModelDisplayName(candidate);
        _selectedLocalModelCandidate = candidate;
        OnPropertyChanged(nameof(SelectedLocalModelCandidate));
        OnPropertyChanged(nameof(LocalModelSelectionStatusText));
        OnPropertyChanged(nameof(ImageSelectedModelSummaryText));
        OnPropertyChanged(nameof(ImageExpectedOutputFileText));
        OnPropertyChanged(nameof(ImageExpectedOutputPathText));
        OnPropertyChanged(nameof(ImageParallaxModelGuidanceText));
        OnPropertyChanged(nameof(HasUnmappedLocalModelCandidates));
        RaisePreflightEstimatePropertiesChanged();
        UpdateConversionReadiness();
        if (!_isApplyingUiOnlyRefresh)
        {
            if (HasAnyImageConversionOutput)
            {
                MarkImageExportOutputOutdated();
            }
            else
            {
                ResetImageExportState();
            }

            if (_hasEnteredImagePreviewExportStage)
            {
                _hasEnteredImagePreviewExportStage = false;
                AddImageLogResolved(T(
                    LocalizationKeys.ImageLogModelChangedOutdatedPrepareFormat,
                    ("previous", previousImageModel),
                    ("next", nextImageModel)));
            }
            else if (HasAnyImageConversionOutput)
            {
                AddImageLogResolved(T(
                    LocalizationKeys.ImageLogModelChangedOutdatedFormat,
                    ("previous", previousImageModel),
                    ("next", nextImageModel)));
            }
            else if (!string.Equals(previousImageModel, T(LocalizationKeys.ImageModelNone), StringComparison.Ordinal))
            {
                AddImageLogResolved(T(
                    LocalizationKeys.ImageLogModelChangedFormat,
                    ("previous", previousImageModel),
                    ("next", nextImageModel)));
            }
        }
        else
        {
            RaiseImageExportPropertiesChanged();
        }

        RaiseImageConversionPropertiesChanged();

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

        var previewStatus = previewGate.CanStart
            ? T(LocalizationKeys.VideoPreviewRequiredTitle)
            : LocalizePreviewGateStatus(previewGate);
        var previewDetail = previewGate.CanStart
            ? T(LocalizationKeys.VideoReadinessAcceptedPreviewFileMissing)
            : LocalizePreviewGateDetail(previewGate);
        return ConversionExecutionStartGateResult.Blocked(
            ConversionExecutionBlocker.PreviewRequired,
            previewStatus,
            previewStatus,
            previewDetail,
            previewDetail);
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

    private string LocalizePreviewGateStatus(PreviewConversionGateResult gate)
    {
        if (gate.CanStart)
        {
            return T(LocalizationKeys.VideoPreviewStatusAccepted);
        }

        return _previewState.Status == PreviewGenerationStatus.Outdated ||
            gate.EnglishStatus.Contains("outdated", StringComparison.OrdinalIgnoreCase)
            ? T(LocalizationKeys.VideoPreviewStatusOutdated)
            : T(LocalizationKeys.VideoPreviewStatusRequired);
    }

    private string LocalizePreviewGateDetail(PreviewConversionGateResult gate)
    {
        if (gate.CanStart)
        {
            return T(LocalizationKeys.VideoPreviewGateAcceptedDetail);
        }

        if (_previewState.Status == PreviewGenerationStatus.Outdated ||
            gate.EnglishStatus.Contains("outdated", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoPreviewGateOutdatedDetail);
        }

        return T(_previewState.Status switch
        {
            PreviewGenerationStatus.Generating => LocalizationKeys.VideoPreviewGateRequiredGenerating,
            PreviewGenerationStatus.Ready => LocalizationKeys.VideoPreviewGateRequiredReady,
            PreviewGenerationStatus.Failed => LocalizationKeys.VideoPreviewGateRequiredFailed,
            PreviewGenerationStatus.Canceled => LocalizationKeys.VideoPreviewGateRequiredCanceled,
            PreviewGenerationStatus.NotGenerated => HasCompletedAnalysis
                ? LocalizationKeys.VideoPreviewGateRequiredAfterAnalysis
                : LocalizationKeys.VideoPreviewGateRequiredDefault,
            _ => LocalizationKeys.VideoPreviewGateRequiredDefault,
        });
    }

    private string LocalizePreviewStateDetail(PreviewWorkflowState state) => state.Status switch
    {
        PreviewGenerationStatus.NotGenerated => T(LocalizationKeys.VideoPreviewStateNotGenerated),
        PreviewGenerationStatus.Generating => T(LocalizationKeys.VideoPreviewStatePreparing),
        PreviewGenerationStatus.Ready => T(LocalizationKeys.VideoPreviewSummaryCompleted),
        PreviewGenerationStatus.Accepted => T(LocalizationKeys.VideoPreviewStateAcceptedUnlocked),
        PreviewGenerationStatus.Outdated => T(LocalizationKeys.VideoPreviewStateOutdatedForCurrentSettings),
        PreviewGenerationStatus.Canceled => T(LocalizationKeys.VideoPreviewSummaryCanceled),
        PreviewGenerationStatus.Failed => T(LocalizationKeys.VideoPreviewSummaryFailed),
        _ => state.Status.ToString(),
    };

    private string LocalizeConversionStartGateStatus(ConversionExecutionStartGateResult startGate) =>
        startGate.CanStart
            ? T(LocalizationKeys.VideoConversionGateReady)
            : T(startGate.Blocker switch
            {
                ConversionExecutionBlocker.NoCompletedAnalysis => LocalizationKeys.VideoConversionGateNoCompletedAnalysisStatus,
                ConversionExecutionBlocker.MissingConversionPlan => LocalizationKeys.VideoConversionGateMissingPlanStatus,
                ConversionExecutionBlocker.ReadinessUnknown => LocalizationKeys.VideoConversionGateReadinessUnknownStatus,
                ConversionExecutionBlocker.MissingLocalDependencies => LocalizationKeys.VideoConversionGateMissingDependenciesStatus,
                ConversionExecutionBlocker.FeatureDisabled => LocalizationKeys.VideoConversionGateFeatureDisabledStatus,
                ConversionExecutionBlocker.PreviewRequired => LocalizationKeys.VideoPreviewRequiredTitle,
                _ => LocalizationKeys.VideoConversionGateMissingDependenciesStatus,
            });

    private string LocalizeConversionStartGateDetail(ConversionExecutionStartGateResult startGate)
    {
        if (startGate.CanStart)
        {
            return string.Empty;
        }

        if (startGate.Blocker == ConversionExecutionBlocker.PreviewRequired)
        {
            var previewGate = PreviewConversionGate.Evaluate(_previewState, CreateCurrentPreviewConfiguration());
            return previewGate.CanStart && !PreviewOutputFileExists()
                ? T(LocalizationKeys.VideoReadinessAcceptedPreviewFileMissing)
                : LocalizePreviewGateDetail(previewGate);
        }

        return T(startGate.Blocker switch
        {
            ConversionExecutionBlocker.NoCompletedAnalysis => LocalizationKeys.VideoConversionGateNoCompletedAnalysisDetail,
            ConversionExecutionBlocker.MissingConversionPlan => LocalizationKeys.VideoConversionGateMissingPlanDetail,
            ConversionExecutionBlocker.ReadinessUnknown => LocalizationKeys.VideoConversionGateReadinessUnknownDetail,
            ConversionExecutionBlocker.MissingLocalDependencies => LocalizationKeys.VideoConversionGateMissingDependenciesDetail,
            ConversionExecutionBlocker.FeatureDisabled => LocalizationKeys.VideoConversionGateFeatureDisabledDetail,
            _ => LocalizationKeys.VideoConversionGateMissingDependenciesDetail,
        });
    }

    private string LocalizeConversionStartGateLog(ConversionExecutionStartGateResult startGate)
    {
        var status = LocalizeConversionStartGateStatus(startGate);
        var detail = LocalizeConversionStartGateDetail(startGate);
        return string.IsNullOrWhiteSpace(detail) ? status : $"{status} {detail}";
    }

    private string LocalizeConversionReadinessStatus(ConversionReadiness readiness)
    {
        if (readiness.CanConvert)
        {
            return T(LocalizationKeys.VideoReadinessReadyStatus);
        }

        return readiness.EnglishStatus.Contains("Selected local model", StringComparison.OrdinalIgnoreCase)
            ? T(LocalizationKeys.VideoReadinessUnmappedModelStatus)
            : T(LocalizationKeys.VideoReadinessBlockedStatus);
    }

    private string LocalizeConversionReadinessIssue(ConversionReadinessIssue issue)
    {
        var message = issue.EnglishMessage;
        if (message.StartsWith("FFmpeg is missing.", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoReadinessIssueFfmpegMissingFormat, ("detail", string.Empty)).TrimEnd();
        }

        if (message.StartsWith("FFprobe is missing.", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoReadinessIssueFfprobeMissingFormat, ("detail", string.Empty)).TrimEnd();
        }

        if (message.StartsWith("Embedded Python runtime is missing.", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoReadinessIssuePythonMissingFormat, ("detail", string.Empty)).TrimEnd();
        }

        if (message.StartsWith("Local iw3 engine is missing.", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoReadinessIssueIw3EngineMissingFormat, ("detail", string.Empty)).TrimEnd();
        }

        if (message.StartsWith("Local 3D models are missing.", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoReadinessIssueModelsMissingFormat, ("detail", string.Empty)).TrimEnd();
        }

        if (message.StartsWith("Missing iw3 runtime dependency.", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoReadinessIssueIw3RuntimeDependencyMissingFormat, ("detail", string.Empty)).TrimEnd();
        }

        if (message.StartsWith("Selected local model is not mapped", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoReadinessIssueSelectedModelUnmapped);
        }

        return message;
    }

    private string LocalizeConversionExecutionStep(ConversionExecutionState state)
    {
        if (state.Status == ConversionExecutionStatus.Canceling)
        {
            return T(LocalizationKeys.VideoConversionCanceling);
        }

        return state.Status switch
        {
            ConversionExecutionStatus.NotStarted => T(LocalizationKeys.VideoConversionStepNotStarted),
            ConversionExecutionStatus.Blocked => T(LocalizationKeys.VideoConversionStepDidNotStart),
            ConversionExecutionStatus.Completed => T(LocalizationKeys.VideoConversionStepCompleted),
            ConversionExecutionStatus.Canceled => T(LocalizationKeys.VideoConversionStepCanceled),
            ConversionExecutionStatus.Failed => T(LocalizationKeys.VideoConversionStepFailed),
            ConversionExecutionStatus.Running => LocalizeRunningConversionStep(state.CurrentStep.EnglishText),
            _ => state.CurrentStep.EnglishText,
        };
    }

    private string LocalizeRunningConversionStep(string englishText)
    {
        if (englishText.Contains("Starting", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionStepStarting);
        }

        if (englishText.Contains("LG-compatible", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionStepLgCopy);
        }

        if (englishText.Contains("Process metrics", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionStepProcessMetricsUpdated);
        }

        return T(LocalizationKeys.VideoConversionStepRunning);
    }

    private string LocalizeConversionExecutionDetail(ConversionExecutionState state)
    {
        if (state.Status == ConversionExecutionStatus.Blocked)
        {
            var startGate = EvaluateConversionStartGate();
            return LocalizeConversionStartGateLog(startGate);
        }

        if (state.Status == ConversionExecutionStatus.Canceling)
        {
            return T(LocalizationKeys.VideoConversionDetailCancelRequested);
        }

        return state.Status switch
        {
            ConversionExecutionStatus.NotStarted => string.Empty,
            ConversionExecutionStatus.Completed => T(LocalizationKeys.VideoConversionSummaryCompleted),
            ConversionExecutionStatus.Canceled => T(LocalizationKeys.VideoConversionSummaryCanceled),
            ConversionExecutionStatus.Failed => LocalizeConversionSummaryText(state.DetailEnglish),
            ConversionExecutionStatus.Running => LocalizeRunningConversionDetail(state.DetailEnglish),
            _ => state.DetailEnglish,
        };
    }

    private string LocalizeRunningConversionDetail(string englishDetail)
    {
        if (englishDetail.Contains("Launching bundled", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionDetailLaunchingBundledIw3);
        }

        if (englishDetail.Contains("LG-compatible", StringComparison.OrdinalIgnoreCase) ||
            englishDetail.Contains("Post-processing", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionDetailLgCopyPostProcessing);
        }

        return englishDetail;
    }

    private string LocalizeConversionSummaryText(string englishSummary)
    {
        if (englishSummary.Contains("dry run", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionSummaryDryRunBlocked);
        }

        if (englishSummary.Contains("not mapped", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionSummaryUnmappedModel);
        }

        if (englishSummary.Contains("request is invalid", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionSummaryInvalidRequest);
        }

        if (englishSummary.Contains("partial", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionSummaryPartialPreparationFailed);
        }

        if (englishSummary.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionSummaryTimedOut);
        }

        if (englishSummary.Contains("canceled", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionSummaryCanceled);
        }

        if (englishSummary.Contains("completed", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionSummaryCompleted);
        }

        if (TrySplitAfterPrefix(englishSummary, "Local iw3 conversion failed unexpectedly: ", out var unexpectedMessage))
        {
            return T(LocalizationKeys.VideoConversionDetailFailedUnexpectedFormat, ("message", unexpectedMessage));
        }

        return T(LocalizationKeys.VideoConversionSummaryFailed);
    }

    private string LocalizePreviewGenerationSummary(string englishSummary)
    {
        var runtimeWarning = englishSummary.Contains(
            Iw3RuntimeDownloadDetector.EnglishWarning,
            StringComparison.OrdinalIgnoreCase)
                ? " " + T(LocalizationKeys.VideoLogRuntimeDownloadWarning)
                : string.Empty;

        if (englishSummary.Contains("canceled", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoPreviewSummaryCanceled) + runtimeWarning;
        }

        if (englishSummary.Contains("successfully", StringComparison.OrdinalIgnoreCase) ||
            englishSummary.Contains("Preview generated", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoPreviewSummaryCompleted) + runtimeWarning;
        }

        return T(LocalizationKeys.VideoPreviewSummaryFailed) + runtimeWarning;
    }

    private string LocalizePreviewOpenWarning(string englishWarning)
    {
        if (englishWarning.StartsWith("Open preview skipped because no current preview is ready", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoOutputOpenPreviewSkippedNoReady);
        }

        if (englishWarning.StartsWith("Open preview skipped because the preview file was not found", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoOutputOpenPreviewFileMissing);
        }

        if (TrySplitAfterPrefix(englishWarning, "Open preview failed. ", out var message))
        {
            return T(LocalizationKeys.VideoOutputOpenPreviewFailedFormat, ("message", message));
        }

        return englishWarning;
    }

    private string LocalizeConversionOutputOpenWarning(string englishWarning)
    {
        if (englishWarning.StartsWith("Open video skipped because the final output file was not found", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoOutputOpenFinalFileMissing);
        }

        if (TrySplitAfterPrefix(englishWarning, "Conversion completed, but opening the video failed: ", out var message))
        {
            return T(LocalizationKeys.VideoOutputOpenFinalFailedFormat, ("message", message));
        }

        return englishWarning;
    }

    private string LocalizeConversionExecutionLog(ConversionExecutionLogEntry log)
    {
        var message = log.EnglishMessage;
        if (string.Equals(message, Iw3RuntimeDownloadDetector.EnglishWarning, StringComparison.Ordinal))
        {
            return T(LocalizationKeys.VideoLogRuntimeDownloadWarning);
        }

        if (string.Equals(message, Iw3RuntimeDownloadDetector.EnglishTimingNote, StringComparison.Ordinal))
        {
            return T(LocalizationKeys.VideoLogRuntimeDownloadTimingNote);
        }

        if (TrySplitAfterPrefix(message, "Preview saved to ", out var previewPath))
        {
            return T(LocalizationKeys.VideoLogPreviewSavedFormat, ("path", previewPath));
        }

        if (TrySplitAfterPrefix(message, "Final output saved to ", out var finalOutputPath))
        {
            return T(LocalizationKeys.VideoLogConversionPrimaryOutputGeneratedFormat, ("path", finalOutputPath));
        }

        if (TrySplitAfterPrefix(message, "LG-compatible MP4 copy saved to ", out var compatibilityPath))
        {
            return T(LocalizationKeys.VideoLogConversionLgCopyGeneratedFormat, ("path", compatibilityPath.TrimEnd('.')));
        }

        if (message.StartsWith("LG-compatible MP4 copy failed", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoLogConversionLgCopyMissing);
        }

        if (message.StartsWith("Conversion partial file was cleaned", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoLogConversionPartialFileCleaned);
        }

        if (message.StartsWith("Stale conversion partial file was cleaned", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoLogConversionStalePartialFileCleaned);
        }

        if (message.StartsWith("Could not delete conversion partial file", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoLogConversionPartialFileDeleteFailed);
        }

        if (message.StartsWith("Could not delete stale partial file", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoLogConversionStalePartialFileDeleteFailed);
        }

        if (message.StartsWith("stdout:", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("stderr:", StringComparison.OrdinalIgnoreCase) ||
            message.Contains(" diagnostics:", StringComparison.OrdinalIgnoreCase) ||
            message.Contains(" timing diagnostics:", StringComparison.OrdinalIgnoreCase))
        {
            return message;
        }

        return message;
    }

    private string LocalizeConversionPlanStep(VideoConversionPlanStep step)
    {
        if (_conversionPlan is null)
        {
            return step.EnglishText;
        }

        var englishText = step.EnglishText;
        if (englishText.StartsWith("Read the analyzed source video", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionPlanStepReadSource);
        }

        if (englishText.StartsWith("Plan selected local model", StringComparison.OrdinalIgnoreCase) &&
            _conversionPlan.SelectedLocalModel is { } selectedModel)
        {
            var depthModelDetail = string.IsNullOrWhiteSpace(selectedModel.Iw3DepthModelName)
                ? string.Empty
                : T(
                    LocalizationKeys.VideoConversionPlanStepSelectedLocalModelDepthModelDetailFormat,
                    ("model", selectedModel.Iw3DepthModelName));
            return T(
                LocalizationKeys.VideoConversionPlanStepSelectedLocalModelFormat,
                ("model", selectedModel.DisplayName),
                ("path", selectedModel.RelativePath),
                ("depthModelDetail", depthModelDetail));
        }

        if (englishText.StartsWith("Generate ", StringComparison.OrdinalIgnoreCase))
        {
            return T(
                LocalizationKeys.VideoConversionPlanStepGenerateFramesFormat,
                ("layout", ThreeDOutputFormatText(_conversionPlan.ThreeDOutputFormat)));
        }

        if (englishText.StartsWith("Prepare the ", StringComparison.OrdinalIgnoreCase))
        {
            return T(
                LocalizationKeys.VideoConversionPlanStepPrepareOutputFormat,
                ("width", _conversionPlan.Width.ToString(CultureInfo.InvariantCulture)),
                ("height", _conversionPlan.Height.ToString(CultureInfo.InvariantCulture)),
                ("codec", _conversionPlan.VideoCodec),
                ("preset", TargetPresetName(SelectedOutputPreset)));
        }

        if (englishText.StartsWith("After the primary iw3 output succeeds", StringComparison.OrdinalIgnoreCase))
        {
            return T(LocalizationKeys.VideoConversionPlanStepLgCompatibilityHalfSbs);
        }

        if (englishText.StartsWith("LG 3D TV 2012 MP4 copy is selected", StringComparison.OrdinalIgnoreCase))
        {
            return T(
                LocalizationKeys.VideoConversionPlanStepLgCompatibilityUnsupportedFormat,
                ("layout", ThreeDOutputFormatText(_conversionPlan.ThreeDOutputFormat)));
        }

        if (englishText.StartsWith("Write the converted video to ", StringComparison.OrdinalIgnoreCase))
        {
            return T(
                LocalizationKeys.VideoConversionPlanStepWriteOutputFormat,
                ("path", _conversionPlan.SuggestedOutputPath));
        }

        return englishText;
    }

    private void UpdateToolStatuses()
    {
        if (_dependencyHealth is null)
        {
            return;
        }

        ToolStatuses.Clear();
        ToolStatuses.Add(CreateToolStatus(
            LocalizationKeys.SystemToolFfmpeg,
            _dependencyHealth.Ffmpeg,
            ToolStatusComponent.BundledTool));
        ToolStatuses.Add(CreateToolStatus(
            LocalizationKeys.SystemToolFfprobe,
            _dependencyHealth.Ffprobe,
            ToolStatusComponent.BundledTool));
        ToolStatuses.Add(CreateToolStatus(
            LocalizationKeys.SystemToolPython,
            _dependencyHealth.Python,
            ToolStatusComponent.EmbeddedPython));
        ToolStatuses.Add(CreateToolStatus(
            LocalizationKeys.SystemToolIw3Engine,
            _dependencyHealth.Iw3EngineDirectory,
            ToolStatusComponent.Iw3Engine));
        ToolStatuses.Add(CreateToolStatus(
            LocalizationKeys.SystemToolModels,
            _dependencyHealth.ModelsDirectory,
            ToolStatusComponent.Models));
        ToolStatuses.Add(CreateToolStatus(
            LocalizationKeys.SystemToolIw3RuntimeDependency,
            _dependencyHealth.Iw3RuntimeDependencies,
            ToolStatusComponent.Iw3RuntimeDependency));
    }

    private ToolStatusItemViewModel CreateToolStatus(
        string nameKey,
        ToolDependencyHealth dependencyHealth,
        ToolStatusComponent component)
    {
        var isEngine = component == ToolStatusComponent.Iw3Engine;
        var isModels = component == ToolStatusComponent.Models;
        return new(
            Name: T(nameKey),
            StatusText: dependencyHealth.Status == ToolHealthStatus.Found
                ? T(LocalizationKeys.CommonFound)
                : T(LocalizationKeys.CommonMissing),
            ReasonText: ToolStatusReasonText(dependencyHealth, component),
            DetailText: ToolStatusDetailText(dependencyHealth, component),
            ContextActionText: isEngine
                ? T(LocalizationKeys.CommonOpen)
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
            ToolHealthDetailKind.BundledFileFound => component == ToolStatusComponent.EmbeddedPython
                ? T(LocalizationKeys.SystemToolReasonEmbeddedPythonFound)
                : T(LocalizationKeys.SystemToolReasonBundledExecutableFound),
            ToolHealthDetailKind.BundledFileMissing => component == ToolStatusComponent.EmbeddedPython
                ? T(LocalizationKeys.SystemToolReasonEmbeddedPythonMissing)
                : T(LocalizationKeys.SystemToolReasonBundledExecutableMissing),
            ToolHealthDetailKind.EngineBundleFound => T(LocalizationKeys.SystemToolReasonEngineBundleFound),
            ToolHealthDetailKind.EngineDirectoryMissing => T(LocalizationKeys.SystemToolReasonEngineFolderMissing),
            ToolHealthDetailKind.EnginePlaceholderOnly => T(LocalizationKeys.SystemToolReasonEnginePlaceholderOnly),
            ToolHealthDetailKind.EngineManifestMissing => T(LocalizationKeys.SystemToolReasonEngineManifestMissing),
            ToolHealthDetailKind.EngineEntryFilesMissing => T(LocalizationKeys.SystemToolReasonEngineEntryMissing),
            ToolHealthDetailKind.ModelFilesFound => T(LocalizationKeys.SystemToolReasonModelFilesFound),
            ToolHealthDetailKind.ModelsDirectoryMissing => T(LocalizationKeys.SystemToolReasonModelsFolderMissing),
            ToolHealthDetailKind.ModelFilesMissing => T(LocalizationKeys.SystemToolReasonModelFilesMissing),
            ToolHealthDetailKind.Iw3RuntimeDependenciesFound => T(LocalizationKeys.SystemToolReasonIw3RuntimeDependencyFound),
            ToolHealthDetailKind.Iw3RuntimeDependenciesMissing => T(LocalizationKeys.SystemToolReasonIw3RuntimeDependencyMissing),
            _ => T(LocalizationKeys.SystemToolReasonLocalDependencyChecked),
        };

    private string ToolStatusDetailText(
        ToolDependencyHealth dependencyHealth,
        ToolStatusComponent component) =>
        dependencyHealth.DetailKind switch
        {
            ToolHealthDetailKind.BundledFileFound => component == ToolStatusComponent.EmbeddedPython
                ? T(LocalizationKeys.SystemToolDetailEmbeddedPythonFoundFormat, ("path", dependencyHealth.ExpectedPath))
                : T(LocalizationKeys.SystemToolDetailBundledExecutableFoundFormat, ("path", dependencyHealth.ExpectedPath)),
            ToolHealthDetailKind.BundledFileMissing => component == ToolStatusComponent.EmbeddedPython
                ? T(LocalizationKeys.SystemToolDetailEmbeddedPythonMissingFormat, ("path", dependencyHealth.ExpectedPath))
                : T(LocalizationKeys.SystemToolDetailBundledExecutableMissingFormat, ("path", dependencyHealth.ExpectedPath)),
            ToolHealthDetailKind.EngineBundleFound => T(
                LocalizationKeys.SystemToolDetailEngineBundleFoundFormat,
                ("path", dependencyHealth.ExpectedPath)),
            ToolHealthDetailKind.EngineDirectoryMissing => T(
                LocalizationKeys.SystemToolDetailEngineFolderMissingFormat,
                ("path", dependencyHealth.ExpectedPath)),
            ToolHealthDetailKind.EnginePlaceholderOnly => T(
                LocalizationKeys.SystemToolDetailEnginePlaceholderOnlyFormat,
                ("path", dependencyHealth.ExpectedPath)),
            ToolHealthDetailKind.EngineManifestMissing => T(
                LocalizationKeys.SystemToolDetailEngineManifestMissingFormat,
                ("path", Path.Combine(dependencyHealth.ExpectedPath, "ENGINE_MANIFEST.json"))),
            ToolHealthDetailKind.EngineEntryFilesMissing => T(
                LocalizationKeys.SystemToolDetailEngineEntryMissingFormat,
                ("path", dependencyHealth.ExpectedPath)),
            ToolHealthDetailKind.ModelFilesFound => T(
                LocalizationKeys.SystemToolDetailModelFilesFoundFormat,
                ("path", dependencyHealth.ExpectedPath)),
            ToolHealthDetailKind.ModelsDirectoryMissing => T(
                LocalizationKeys.SystemToolDetailModelsFolderMissingFormat,
                ("path", dependencyHealth.ExpectedPath)),
            ToolHealthDetailKind.ModelFilesMissing => T(
                LocalizationKeys.SystemToolDetailModelFilesMissingFormat,
                ("path", dependencyHealth.ExpectedPath)),
            ToolHealthDetailKind.Iw3RuntimeDependenciesFound => T(
                LocalizationKeys.SystemToolDetailIw3RuntimeDependencyFoundFormat,
                ("path", dependencyHealth.ExpectedPath)),
            ToolHealthDetailKind.Iw3RuntimeDependenciesMissing => T(
                LocalizationKeys.SystemToolDetailIw3RuntimeDependencyMissingFormat,
                ("path", dependencyHealth.ExpectedPath)),
            _ => dependencyHealth.ExpectedPath,
        };
    private string CreateSystemStatusTechnicalDetailsText()
    {
        var supportedModelsPattern = Iw3EngineBundleContract.ModelsDirectoryRelativePath +
            "/*" +
            string.Join("|*", Iw3EngineBundleContract.SupportedModelExtensions);
        var lines = new List<string>
        {
            T(LocalizationKeys.TechnicalDetailsExpectedIw3BundleLayout),
            T(
                LocalizationKeys.TechnicalDetailsManifestVersionRequirementFormat,
                ("path", Iw3EngineBundleContract.ManifestRelativePath)),
            Iw3EngineBundleContract.PythonExecutableRelativePath,
            Iw3EngineBundleContract.PythonPathFileRelativePath,
            Iw3EngineBundleContract.Iw3PackageMainRelativePath,
            supportedModelsPattern,
            Iw3EngineBundleContract.Iw3DefaultStereoRuntimeDependencyRelativePath,
            T(LocalizationKeys.TechnicalDetailsOptionalFormat, ("path", Iw3EngineBundleContract.CliCapabilitiesRelativePath)),
            string.Empty,
        };

        lines.AddRange(CreateModelInventoryTechnicalDetailsLines());
        lines.Add(string.Empty);
        lines.AddRange(Iw3CliCapabilitiesDetailsFormatter.CreateLines(
            _dependencyHealth?.Iw3CliCapabilities ??
            Iw3CliCapabilitiesManifest.Missing(_toolPaths.Iw3CliCapabilitiesFile),
            LocalizeCore));
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
            T(LocalizationKeys.TechnicalDetailsLocalModelInventory),
            T(LocalizationKeys.TechnicalDetailsModelsDirectoryFormat, ("path", inventory.ModelsDirectory)),
            T(LocalizationKeys.TechnicalDetailsSupportedExtensionsFormat, ("extensions", string.Join(", ", inventory.SupportedExtensions))),
            T(LocalizationKeys.TechnicalDetailsCompatibleModelCountFormat, ("count", inventory.CompatibleModelCount.ToString(CultureInfo.InvariantCulture))),
            T(LocalizationKeys.TechnicalDetailsCatalogPathFormat, ("path", inventory.Catalog.CatalogPath)),
            T(LocalizationKeys.TechnicalDetailsCatalogStatusFormat, ("status", CatalogStatusText(inventory.Catalog.Status))),
            T(LocalizationKeys.TechnicalDetailsCatalogEntriesFormat, ("count", inventory.Catalog.EntryCount.ToString(CultureInfo.InvariantCulture))),
            T(
                LocalizationKeys.TechnicalDetailsCatalogExistingEntriesFormat,
                ("count", inventory.Catalog.EntriesWithExistingCompatibleFiles.Count.ToString(CultureInfo.InvariantCulture))),
            T(
                LocalizationKeys.TechnicalDetailsCatalogMissingEntriesFormat,
                ("count", inventory.Catalog.EntriesWithMissingFiles.Count.ToString(CultureInfo.InvariantCulture))),
            T(
                LocalizationKeys.TechnicalDetailsUnmanagedModelFilesFormat,
                ("count", inventory.Catalog.UnmanagedCompatibleModelFiles.Count.ToString(CultureInfo.InvariantCulture))),
        };

        AddModelCatalogStatusDetailLines(lines, inventory.Catalog);

        if (!inventory.DirectoryExists)
        {
            lines.Add(T(LocalizationKeys.TechnicalDetailsModelsDirectoryMissing));
            return lines;
        }

        if (!inventory.HasCompatibleModels)
        {
            lines.Add(T(LocalizationKeys.TechnicalDetailsNoCompatibleModelFiles));
            return lines;
        }

        lines.Add(T(LocalizationKeys.TechnicalDetailsCompatibleModelFiles));
        foreach (var modelFile in inventory.CompatibleModelFiles)
        {
            lines.Add($"- {modelFile.RelativePath}");
        }

        var mappedCandidates = Iw3DepthModelMapper.CreateSelectableCandidates(
            inventory.SelectionCandidates,
            IsSpanish);
        lines.Add(T(LocalizationKeys.TechnicalDetailsMappedSelectableModels));
        if (mappedCandidates.Count == 0)
        {
            lines.Add(T(LocalizationKeys.TechnicalDetailsNoMappedSelectableModels));
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
            lines.Add(T(LocalizationKeys.TechnicalDetailsUnmappedModelFilesFound));
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
                lines.Add(T(LocalizationKeys.TechnicalDetailsCatalogMissing));
                return;
            case LocalModelCatalogStatus.Invalid:
                lines.Add(T(
                    LocalizationKeys.TechnicalDetailsCatalogInvalidFormat,
                    ("message", catalog.ErrorMessage ?? string.Empty)));
                lines.Add(T(LocalizationKeys.TechnicalDetailsCatalogUnmanagedFallback));
                return;
            case LocalModelCatalogStatus.Placeholder:
                lines.Add(T(LocalizationKeys.TechnicalDetailsCatalogPlaceholder));
                lines.Add(T(LocalizationKeys.TechnicalDetailsCatalogUnmanagedFallback));
                return;
        }

        if (catalog.EntriesWithExistingCompatibleFiles.Count > 0)
        {
            lines.Add(T(LocalizationKeys.TechnicalDetailsCatalogExistingFiles));
            foreach (var entry in catalog.EntriesWithExistingCompatibleFiles)
            {
                lines.Add($"- {CatalogEntryDisplayText(entry)}");
            }
        }

        if (catalog.EntriesWithMissingFiles.Count > 0)
        {
            lines.Add(T(LocalizationKeys.TechnicalDetailsCatalogMissingFiles));
            foreach (var entry in catalog.EntriesWithMissingFiles)
            {
                lines.Add($"- {CatalogEntryDisplayText(entry)}");
            }
        }

        if (catalog.UnmanagedCompatibleModelFiles.Count > 0)
        {
            lines.Add(T(LocalizationKeys.TechnicalDetailsCatalogUnmanagedFiles));
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

    private string CatalogStatusText(LocalModelCatalogStatus status) => status switch
    {
        LocalModelCatalogStatus.Missing => T(LocalizationKeys.TechnicalDetailsCatalogStatusMissing),
        LocalModelCatalogStatus.Invalid => T(LocalizationKeys.TechnicalDetailsCatalogStatusInvalid),
        LocalModelCatalogStatus.Placeholder => T(LocalizationKeys.TechnicalDetailsCatalogStatusPlaceholder),
        LocalModelCatalogStatus.Found => T(LocalizationKeys.TechnicalDetailsCatalogStatusFound),
        _ => status.ToString(),
    };

    private string CreateMissingComponentsSummary()
    {
        if (_dependencyHealth is null)
        {
            return "-";
        }

        var missingComponents = new List<string>();
        AddMissingComponent(missingComponents, _dependencyHealth.Ffmpeg, LocalizationKeys.SystemToolFfmpeg);
        AddMissingComponent(missingComponents, _dependencyHealth.Ffprobe, LocalizationKeys.SystemToolFfprobe);
        AddMissingComponent(missingComponents, _dependencyHealth.Python, LocalizationKeys.SystemToolPython);
        AddMissingComponent(missingComponents, _dependencyHealth.Iw3EngineDirectory, LocalizationKeys.SystemToolIw3Engine);
        AddMissingComponent(missingComponents, _dependencyHealth.ModelsDirectory, LocalizationKeys.SystemToolModels);
        AddMissingComponent(
            missingComponents,
            _dependencyHealth.Iw3RuntimeDependencies,
            LocalizationKeys.SystemToolIw3RuntimeDependency);

        return missingComponents.Count == 0
            ? T(LocalizationKeys.VideoReadinessNoMissingComponents)
            : T(LocalizationKeys.VideoReadinessMissingComponentsPrefix) + string.Join(", ", missingComponents);
    }

    private void AddMissingComponent(
        ICollection<string> missingComponents,
        ToolDependencyHealth dependencyHealth,
        string nameKey)
    {
        if (dependencyHealth.Status == ToolHealthStatus.Missing)
        {
            missingComponents.Add(T(nameKey));
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
        AddVideoLogResolved(T(
            replacingVideo
                ? LocalizationKeys.VideoLogSelectedFileReplacedFormat
                : LocalizationKeys.VideoLogSelectedFileFormat,
            ("path", path)));
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
            AddVideoLogResolved(T(
                LocalizationKeys.VideoConversionLogPreviewCacheCleanupSkippedFormat,
                ("message", exception.Message)));
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
            AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewStalePartialFileCleaned));
        }

        if (warningCount > 0)
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewStalePartialFileDeleteFailed));
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
            AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewOpenPlanFirst));
            RaisePreviewPropertiesChanged();
            return;
        }

        if (IsConversionRunning)
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewDisabledDuringConversion));
            return;
        }

        if (IsPreviewGenerating)
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewAlreadyGenerating));
            return;
        }

        var rangeValidation = CurrentPreviewTimeRangeValidation;
        if (!rangeValidation.IsValid)
        {
            AddVideoLogResolved(T(
                LocalizationKeys.VideoLogPreviewCannotStartFormat,
                ("reason", PreviewTimeRangeValidationMessage(rangeValidation.Issue))));
            RaisePreviewPropertiesChanged();
            return;
        }

        var configuration = CreateCurrentPreviewConfiguration();
        if (configuration is null ||
            _conversionPlan?.SelectedLocalModel is null)
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewConfigurationNotReady));
            return;
        }

        if (!CurrentExecutionRequestCanStart())
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewExecutionNotReady));
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
        AppendPreviewLogLine(T(LocalizationKeys.VideoPreviewStagePreparing));
        IsPreviewReadyModalOpen = false;
        IsPreviewGeneratingModalOpen = true;
        AppendPreviewLogLine(T(LocalizationKeys.VideoLogPreviewTimingModalOpened));
        RaisePreviewPropertiesChanged();
        RaiseConversionExecutionPropertiesChanged();
        RaiseConversionRunningModePropertiesChanged();
        AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewStarted));
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
            AddVideoLogResolved(LocalizePreviewGenerationSummary(result.EnglishSummary));
            if (_previewState.Status == PreviewGenerationStatus.Ready &&
                IsPreviewFingerprintCurrent())
            {
                IsPreviewReadyModalOpen = true;
                AppendPreviewLogLine(T(LocalizationKeys.VideoLogPreviewTimingReadyModalOpened));
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
        var message = T(LocalizationKeys.VideoLogPreviewCanceled);
        _previewProgressPercent = 0;
        _previewState = _previewState with
        {
            Status = PreviewGenerationStatus.Canceled,
            OutputPath = null,
            FinishedAt = DateTimeOffset.UtcNow,
            EnglishDetail = message,
            SpanishDetail = message,
        };
        if (!_hasLoggedPreviewCancellationSummary)
        {
            AddVideoLogResolved(message);
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
        var detail = T(
            LocalizationKeys.VideoErrorPreviewUnexpectedFormat,
            ("message", exception.Message));
        _previewProgressPercent = 0;
        _previewState = _previewState with
        {
            Status = PreviewGenerationStatus.Failed,
            FinishedAt = DateTimeOffset.UtcNow,
            EnglishDetail = detail,
            SpanishDetail = detail,
        };
        AppendPreviewLogLine(T(
            LocalizationKeys.VideoErrorPreviewUnexpectedFormat,
            ("message", exception.ToString())));
        AddVideoLogResolved(T(
            LocalizationKeys.VideoLogPreviewFailedFormat,
            ("logPath", errorLogPath),
            ("message", exception.Message)));
        RaisePreviewPropertiesChanged();
        RaiseConversionReadinessPropertiesChanged();
    }

    private void CancelPreview()
    {
        if (!CanCancelPreview &&
            _previewCancellationTokenSource is null)
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogNoPreviewToCancel));
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
            AddVideoLogResolved(LocalizePreviewOpenWarning(openResult.EnglishWarning));
            return;
        }

        AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewOpened));
    }

    private void ContinuePreview()
    {
        if (!CanContinuePreview)
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewAcceptInvalid));
            return;
        }

        var configuration = CreateCurrentPreviewConfiguration();
        if (configuration is null)
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewAcceptConfigInvalid));
            return;
        }

        _previewState = _previewState.Accept(configuration);
        IsPreviewReadyModalOpen = false;
        AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewAccepted));
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
            AddVideoLogResolved(T(
                LocalizationKeys.VideoLogPreviewCleanupWarningFormat,
                ("message", exception.Message)));
        }

        if (logDeletion)
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewFilesDeleted));
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
            AddVideoLogResolved(T(
                LocalizationKeys.VideoLogPreviewCleanupWarningFormat,
                ("message", exception.Message)));
        }

        if (logDeletion)
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewFilesDeleted));
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
            AddVideoLogResolved(T(
                LocalizationKeys.VideoLogPreviewPartialCleanupWarningFormat,
                ("message", exception.Message)));
        }

        if (logDeletion && deleted > 0)
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewPartialFilesCleaned));
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
                AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewAcceptedRestored));
            }
            else
            {
                AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewOutdated));
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
            AddVideoLogResolved(T(LocalizationKeys.VideoLogPreviewOutdated));
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
        PreviewTimeRangeValidationIssue issue) => issue switch
        {
            PreviewTimeRangeValidationIssue.None => string.Empty,
            PreviewTimeRangeValidationIssue.MissingSourceDuration => T(LocalizationKeys.VideoPreviewValidationMissingSourceDuration),
            PreviewTimeRangeValidationIssue.MissingValue => T(LocalizationKeys.VideoPreviewValidationMissingValue),
            PreviewTimeRangeValidationIssue.InvalidFormat => T(LocalizationKeys.VideoPreviewValidationInvalidFormat),
            PreviewTimeRangeValidationIssue.FromMustBeBeforeTo => T(LocalizationKeys.VideoPreviewValidationFromMustBeBeforeTo),
            PreviewTimeRangeValidationIssue.ExceedsMaximumDuration => T(LocalizationKeys.VideoPreviewValidationExceedsMaximumDuration),
            PreviewTimeRangeValidationIssue.ToBeyondSourceDuration => T(LocalizationKeys.VideoPreviewValidationToBeyondSourceDuration),
            _ => T(LocalizationKeys.VideoPreviewValidationInvalid),
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
            RecordRecoverablePreviewWarning(LocalizationKeys.VideoPreviewOperationProgressUpdate, exception);
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
            AddVideoLogResolved(T(LocalizationKeys.VideoLogCancelActivePreviewBeforeConversion));
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
            AddVideoLogResolved(T(LocalizationKeys.VideoConversionOpenPlanBeforeFinal));
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
        AddVideoLogResolved(T(LocalizationKeys.VideoLogConversionStarted));
        AddConversionLog(
            T(LocalizationKeys.VideoLogConversionStarted),
            T(LocalizationKeys.VideoLogConversionStarted));

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
                AddConversionLog(
                    LocalizeConversionExecutionLog(log),
                    LocalizeConversionExecutionLog(log),
                    log.Timestamp.LocalDateTime);
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
            AddVideoLogResolved(T(LocalizationKeys.VideoLogConversionCanceled));
            AddConversionLog(
                T(LocalizationKeys.VideoLogConversionCanceled),
                T(LocalizationKeys.VideoLogConversionCanceled));
        }
        catch (Exception exception)
        {
            _conversionExecutionState = CreateFailedConversionState(
                startedAt,
                DateTimeOffset.UtcNow,
                $"Local iw3 conversion failed unexpectedly: {exception.Message}",
                $"La conversion local iw3 fallo inesperadamente: {exception.Message}");
            AddVideoLogResolved(T(
                LocalizationKeys.VideoLogConversionFailedFormat,
                ("message", exception.Message)));
            AddConversionLog(
                T(LocalizationKeys.VideoLogConversionFailedFormat, ("message", exception.Message)),
                T(LocalizationKeys.VideoLogConversionFailedFormat, ("message", exception.Message)));
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
        AddVideoLogResolved(LocalizeConversionStartGateLog(startGate));
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
            T(LocalizationKeys.VideoLogRuntimeDownloadWarning),
            T(LocalizationKeys.VideoLogRuntimeDownloadWarning));
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
        AddVideoLogResolved(LocalizeConversionSummaryText(result.EnglishSummary));
        if (result.Success)
        {
            if (!string.IsNullOrWhiteSpace(result.PrimaryOutputPath))
            {
                AddVideoLogResolved(T(
                    LocalizationKeys.VideoLogConversionPrimaryOutputGeneratedFormat,
                    ("path", result.PrimaryOutputPath)));
            }

            if (result.CompatibilityCopySucceeded &&
                !string.IsNullOrWhiteSpace(result.CompatibilityOutputPath))
            {
                AddVideoLogResolved(T(
                    LocalizationKeys.VideoLogConversionLgCopyGeneratedFormat,
                    ("path", result.CompatibilityOutputPath)));
            }
            else if (CreateLgCompatibilityCopy)
            {
                AddVideoLogResolved(T(LocalizationKeys.VideoLogConversionLgCopyMissing));
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
            AddVideoLogResolved(T(LocalizationKeys.VideoLogConversionPartialFileCleaned));
        }

        if (result.Logs.Any(log => log.EnglishMessage.StartsWith(
                "Could not delete conversion partial file.",
                StringComparison.Ordinal)))
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogConversionPartialFileDeleteFailed));
        }
    }

    private void AddStaleConversionPartialCleanupActivityLogs(ConversionExecutionResult result)
    {
        if (result.Logs.Any(log => string.Equals(
                log.EnglishMessage,
                "Stale conversion partial file was cleaned.",
                StringComparison.Ordinal)))
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogConversionStalePartialFileCleaned));
        }

        if (result.Logs.Any(log => log.EnglishMessage.StartsWith(
                "Could not delete stale partial file.",
                StringComparison.Ordinal)))
        {
            AddVideoLogResolved(T(LocalizationKeys.VideoLogConversionStalePartialFileDeleteFailed));
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
        var displayText = ProcessMetricDisplayFormatter.Format(metrics, LocalizeCore);
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
        var displayText = ProcessMetricDisplayFormatter.Detecting(LocalizeCore);
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
            _previewStageKey = LocalizationKeys.VideoPreviewStageSourceClip;
            _previewStageEnglishText = string.Empty;
            _previewStageSpanishText = string.Empty;
        }
        else if (step.EnglishText.Contains("iw3", StringComparison.OrdinalIgnoreCase))
        {
            _previewStageKey = LocalizationKeys.VideoPreviewStageIw3;
            _previewStageEnglishText = string.Empty;
            _previewStageSpanishText = string.Empty;
        }
        else if (step.EnglishText.Contains("completed", StringComparison.OrdinalIgnoreCase))
        {
            _previewStageKey = LocalizationKeys.VideoPreviewStageCompleted;
            _previewStageEnglishText = string.Empty;
            _previewStageSpanishText = string.Empty;
        }
        else
        {
            _previewStageKey = LocalizationKeys.VideoPreviewStagePreparing;
            _previewStageEnglishText = string.Empty;
            _previewStageSpanishText = string.Empty;
        }

        OnPropertyChanged(nameof(PreviewStageText));
    }

    private void UpdatePreviewMetricText(ProcessMetricSample metrics)
    {
        _lastPreviewMetricSample = metrics;
        _previewCpuUsageText = metrics.CpuUsagePercent is null
            ? T(LocalizationKeys.VideoPreviewCpuDetecting)
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
        _previewCpuUsageText = T(LocalizationKeys.VideoPreviewCpuDetecting);
        _previewRamUsageText = T(LocalizationKeys.VideoPreviewRamDetecting);
        _previewGpuUsageText = T(LocalizationKeys.VideoPreviewGpuDetecting);
        _previewVramUsageText = T(LocalizationKeys.VideoPreviewVramDetecting);
        _previewGpuMetricsStatusText = T(LocalizationKeys.VideoPreviewGpuMetricsDetecting);
        _previewStageKey = LocalizationKeys.VideoPreviewStagePreparing;
        _previewStageEnglishText = string.Empty;
        _previewStageSpanishText = string.Empty;
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
            ? T(LocalizationKeys.VideoPreviewRamDetecting)
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
            ? T(LocalizationKeys.VideoPreviewGpuDetecting)
            : T(
                LocalizationKeys.VideoPreviewGpuUnavailableFormat,
                ("status", LocalizePreviewGpuStatus(metrics.GpuStatus)));
    }

    private string FormatPreviewVram(ProcessMetricSample metrics)
    {
        if (metrics.GpuDedicatedMemoryBytes is not { } bytes)
        {
            return T(LocalizationKeys.VideoPreviewVramDetecting);
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
                ? T(LocalizationKeys.VideoPreviewGpuMetricsGlobal)
                : T(LocalizationKeys.VideoPreviewGpuMetricsProcess);
        }

        return string.IsNullOrWhiteSpace(metrics.GpuStatus)
            ? T(LocalizationKeys.VideoPreviewGpuMetricsDetecting)
            : T(
                LocalizationKeys.VideoPreviewGpuMetricsUnavailableFormat,
                ("status", LocalizePreviewGpuStatus(metrics.GpuStatus)));
    }

    private string LocalizePreviewGpuStatus(string status) => status switch
    {
        ProcessGpuMetricReading.DetectingStatus => T(LocalizationKeys.ProcessMetricsStatusDetecting),
        ProcessGpuMetricReading.NoProcessGpuEngineCounterStatus =>
            T(LocalizationKeys.ProcessMetricsStatusNoProcessGpuEngineCounter),
        ProcessGpuMetricReading.PermissionUnavailableStatus =>
            T(LocalizationKeys.ProcessMetricsStatusPermissionUnavailable),
        ProcessGpuMetricReading.WindowsMetricsUnavailableStatus =>
            T(LocalizationKeys.ProcessMetricsStatusWindowsMetricsUnavailable),
        ProcessGpuMetricReading.NvidiaMetricsUnavailableStatus =>
            T(LocalizationKeys.ProcessMetricsStatusNvidiaMetricsUnavailable),
        ProcessGpuMetricReading.AdapterGpuUsageStatus =>
            T(LocalizationKeys.ProcessMetricsStatusAdapterGpuUsage),
        ProcessGpuMetricReading.ProcessGpuEngineCounterStatus =>
            T(LocalizationKeys.ProcessMetricsStatusProcessGpuEngineCounter),
        ProcessGpuMetricReading.NvidiaAdapterMetricsStatus =>
            T(LocalizationKeys.ProcessMetricsStatusNvidiaAdapterMetrics),
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
            var message = LocalizeConversionOutputOpenWarning(openResult.EnglishWarning);
            AddVideoLogResolved(message);
            AddConversionLog(message, message);
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
            AddVideoLogResolved(T(LocalizationKeys.VideoLogNoActiveConversionToCancel));
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
        AddVideoLogResolved(T(LocalizationKeys.VideoLogConversionCanceling));
        AddConversionLog(
            T(LocalizationKeys.VideoLogConversionCanceling),
            T(LocalizationKeys.VideoLogConversionCanceling));
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
        string message,
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

            AddVideoLogResolved(message);
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
                AddVideoLogResolved(T(LocalizationKeys.VideoLogOutputPathReset));
            }
            else
            {
                AddVideoLogResolved(T(
                    LocalizationKeys.VideoLogOutputPathChangedFormat,
                    ("path", normalizedPath)));
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
            AddVideoLogResolved(T(LocalizationKeys.VideoLogSelectVideoBeforeOutputFolder));
            return;
        }

        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = T(LocalizationKeys.VideoOutputChooseFolder),
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
            AddVideoLogResolved(T(LocalizationKeys.VideoLogOutputPathReset));
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
            AddVideoLogResolved(T(
                LocalizationKeys.VideoLogOutputPathChangedFormat,
                ("path", outputPath)));
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

    private string QualityPresetText(AiQualityPreset value) => value switch
    {
        AiQualityPreset.Fast => T(LocalizationKeys.VideoOptionQualityFast),
        AiQualityPreset.Balanced => T(LocalizationKeys.VideoOptionQualityBalanced),
        AiQualityPreset.HighQuality => T(LocalizationKeys.VideoOptionQualityHighQuality),
        _ => value.ToString(),
    };

    private string ThreeDIntensityText(ThreeDIntensity value) => value switch
    {
        ThreeDIntensity.Low => T(LocalizationKeys.VideoOptionIntensityLow),
        ThreeDIntensity.Medium => T(LocalizationKeys.VideoOptionIntensityMedium),
        ThreeDIntensity.High => T(LocalizationKeys.VideoOptionIntensityHigh),
        ThreeDIntensity.Custom => T(LocalizationKeys.VideoOptionIntensityCustom),
        _ => value.ToString(),
    };

    private string ThreeDOutputFormatText(ThreeDOutputFormat value) => value switch
    {
        ThreeDOutputFormat.HalfTopBottom => T(LocalizationKeys.VideoOptionOutputFormatHalfTopBottom),
        ThreeDOutputFormat.HalfSideBySide => T(LocalizationKeys.VideoOptionOutputFormatHalfSideBySide),
        ThreeDOutputFormat.FullSideBySide => T(LocalizationKeys.VideoOptionOutputFormatFullSideBySide),
        ThreeDOutputFormat.Anaglyph => T(LocalizationKeys.VideoOptionOutputFormatAnaglyph),
        _ => value.ToString(),
    };

    private string TargetPresetName(TargetDevicePreset preset) => preset.Id switch
    {
        "recommended-3d-tv" => T(LocalizationKeys.VideoOptionOutputProfileRecommended),
        "maximum-compatibility" => T(LocalizationKeys.VideoOptionOutputProfileMaximumCompatibility),
        "high-quality-master" => T(LocalizationKeys.VideoOptionOutputProfileHighQualityMaster),
        "legacy-lg-3d-tv-2012" => T(LocalizationKeys.VideoOptionOutputProfileLegacyLg2012),
        _ => preset.Name,
    };

    private string TargetPresetDescription(TargetDevicePreset preset) => preset.Id switch
    {
        "recommended-3d-tv" => T(LocalizationKeys.VideoProfileDescriptionRecommended),
        "maximum-compatibility" => T(LocalizationKeys.VideoProfileDescriptionMaximumCompatibility),
        "high-quality-master" => T(LocalizationKeys.VideoProfileDescriptionHighQualityMaster),
        "legacy-lg-3d-tv-2012" => T(LocalizationKeys.VideoProfileDescriptionLegacyLg2012),
        _ => preset.Description,
    };

    private string TargetPresetBestFor(TargetDevicePreset preset) => preset.Id switch
    {
        "recommended-3d-tv" => T(LocalizationKeys.VideoProfileBestForRecommended),
        "maximum-compatibility" => T(LocalizationKeys.VideoProfileBestForMaximumCompatibility),
        "high-quality-master" => T(LocalizationKeys.VideoProfileBestForHighQualityMaster),
        "legacy-lg-3d-tv-2012" => T(LocalizationKeys.VideoProfileBestForLegacyLg2012),
        _ => preset.BestFor,
    };

    private string TargetPresetCompatibilityNote(TargetDevicePreset preset) => preset.Id switch
    {
        "recommended-3d-tv" => T(LocalizationKeys.VideoProfileCompatibilityRecommended),
        "maximum-compatibility" => T(LocalizationKeys.VideoProfileCompatibilityMaximumCompatibility),
        "high-quality-master" => T(LocalizationKeys.VideoProfileCompatibilityHighQualityMaster),
        "legacy-lg-3d-tv-2012" => T(LocalizationKeys.VideoProfileCompatibilityLegacyLg2012),
        _ => preset.CompatibilityNote,
    };

    private string TargetPresetPlaybackTitle(TargetDevicePreset preset) => preset.Id switch
    {
        "recommended-3d-tv" => T(LocalizationKeys.VideoProfilePlaybackTitleRecommended),
        "maximum-compatibility" => T(LocalizationKeys.VideoProfilePlaybackTitleMaximumCompatibility),
        "high-quality-master" => T(LocalizationKeys.VideoProfilePlaybackTitleHighQualityMaster),
        "legacy-lg-3d-tv-2012" => T(LocalizationKeys.VideoProfilePlaybackTitleLegacyLg2012),
        _ => preset.PlaybackTitle,
    };

    private string TargetPresetPlaybackInstructions(TargetDevicePreset preset) => preset.Id switch
    {
        "recommended-3d-tv" => T(LocalizationKeys.VideoProfilePlaybackInstructionsRecommended),
        "maximum-compatibility" => T(LocalizationKeys.VideoProfilePlaybackInstructionsMaximumCompatibility),
        "high-quality-master" => T(LocalizationKeys.VideoProfilePlaybackInstructionsHighQualityMaster),
        "legacy-lg-3d-tv-2012" => T(LocalizationKeys.VideoProfilePlaybackInstructionsLegacyLg2012),
        _ => preset.PlaybackInstructions,
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

    private static ILocalizationService CreateLocalizationService() =>
        JsonLocalizationService.LoadFromDirectory(
            Path.Combine(AppContext.BaseDirectory, "Localization"),
            LocalizationCatalog.EnglishLanguageCode);

    private static IReadOnlyList<AppLanguageOptionViewModel> CreateLanguageOptions(
        IReadOnlyList<LocalizationLanguageMetadata> languages)
    {
        var options = languages
            .Select(AppLanguageOptionViewModel.FromMetadata)
            .ToArray();

        return options.Length > 0
            ? options
            : [new AppLanguageOptionViewModel(
                LocalizationCatalog.EnglishLanguageCode,
                "English",
                "English",
                "en")];
    }

    private string GetSelectedLanguageLogDisplayName(string languageCode) =>
        LanguageOptions.FirstOrDefault(language =>
            string.Equals(language.Code, languageCode, StringComparison.OrdinalIgnoreCase))?.Label ??
        languageCode;

    private string GetSelectedThemeLogDisplayName(string theme) =>
        theme switch
        {
            "Dark" => T(LocalizationKeys.SettingsThemeDark),
            "Light" => T(LocalizationKeys.SettingsThemeLight),
            _ => theme,
        };

    private IReadOnlyList<LocalizedOptionViewModel<string>> CreateLocalizedStringOptions(
        IEnumerable<(string Value, string Key)> options) =>
        options
            .Select(option => new LocalizedOptionViewModel<string>(
                option.Value,
                option.Key,
                _localizationService))
            .ToArray();

    private string T(string key) => _localizationService.GetString(key);

    private string T(
        string key,
        params (string Key, object? Value)[] placeholders) =>
        ApplyPlaceholders(T(key), placeholders);

    private string LocalizeCore(
        string key,
        params (string Key, object? Value)[] placeholders) =>
        T(key, placeholders);

    private static string ApplyPlaceholders(
        string format,
        params (string Key, object? Value)[] placeholders)
    {
        var result = format;
        foreach (var placeholder in placeholders)
        {
            result = result.Replace(
                "{" + placeholder.Key + "}",
                Convert.ToString(placeholder.Value, CultureInfo.CurrentCulture) ?? string.Empty,
                StringComparison.Ordinal);
        }

        return result;
    }

    private string LabelValue(string labelKey, string? value) =>
        $"{T(labelKey)}: {value ?? "-"}";

    private static bool TrySplitAfterPrefix(string value, string prefix, out string result)
    {
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            result = value[prefix.Length..].Trim();
            return true;
        }

        result = string.Empty;
        return false;
    }

    private static bool TryExtractAfter(string value, string marker, out string result)
    {
        var markerIndex = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            result = value[(markerIndex + marker.Length)..].Trim();
            return true;
        }

        result = string.Empty;
        return false;
    }

    private static bool TryBetween(string value, string start, string end, out string result)
    {
        var startIndex = value.IndexOf(start, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            result = string.Empty;
            return false;
        }

        var valueStart = startIndex + start.Length;
        var endIndex = value.IndexOf(end, valueStart, StringComparison.OrdinalIgnoreCase);
        if (endIndex < 0)
        {
            result = string.Empty;
            return false;
        }

        result = value[valueStart..endIndex].Trim();
        return true;
    }

    private static bool TrySplitPrefixedLabelPath(
        string value,
        string prefix,
        out string label,
        out string path)
    {
        label = string.Empty;
        path = string.Empty;
        if (!TrySplitAfterPrefix(value, prefix, out var remainder))
        {
            return false;
        }

        var separatorIndex = remainder.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return false;
        }

        label = remainder[..separatorIndex].Trim();
        path = remainder[(separatorIndex + 2)..].Trim();
        return label.Length > 0 && path.Length > 0;
    }

    private static string GetOptionDisplayName(
        IEnumerable<LocalizedOptionViewModel<string>> options,
        string value) =>
        options.FirstOrDefault(option => string.Equals(
            option.Value,
            value,
            StringComparison.Ordinal))?.DisplayName ?? value;

    private LocalizedOptionViewModel<SettingsSection> CreateSettingsSectionOption(
        SettingsSection section,
        string key)
    {
        var label = T(key);
        return new LocalizedOptionViewModel<SettingsSection>(section, label, label);
    }

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

    private void AddLogResolved(string message) =>
        AddLog(message, message);

    private void AddVideoLogResolved(string message) =>
        AddLogResolved(message);

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

    private void AddImageLogResolved(string message) =>
        AddImageLog(message, message);

    private void LoadPerformanceHistory()
    {
        var result = _performanceHistoryStore.Load();
        _performanceHistory = result.Records;
        if (!string.IsNullOrWhiteSpace(result.Warning))
        {
            AddVideoLogResolved(T(
                LocalizationKeys.DiagnosticsPerformanceHistoryLoadWarningFormat,
                ("warning", result.Warning)));
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
                AddVideoLogResolved(T(
                    LocalizationKeys.DiagnosticsPerformanceHistorySaveWarningFormat,
                    ("warning", saveResult.Warning)));
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
        var message = T(LocalizationKeys.CommonLogCopied);
        ShowLogCopyNotification(message, message);
    }

    private void ShowLogCopyFailureNotification()
    {
        var message = T(LocalizationKeys.CommonCouldNotCopyLog);
        ShowLogCopyNotification(message, message);
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

        AppendPreviewLogLine(LocalizeConversionExecutionLog(log));
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
        AppendPreviewLogLine(T(LocalizationKeys.VideoLogRuntimeDownloadWarning));
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

    private void RecordRecoverablePreviewWarning(string operationKey, Exception exception)
    {
        var operation = T(operationKey);
        var errorLogPath = AppErrorLogService.LogRecoverableException(operation, exception);
        AddVideoLogResolved(T(
            LocalizationKeys.DiagnosticsOperationWarningFormat,
            ("operation", operation),
            ("path", errorLogPath),
            ("message", exception.Message)));
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

        foreach (var option in _allStereoOutputFormatOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in _parallaxDepthIntensityOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in _parallaxMotionDirectionOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in _parallaxZoomAmplitudeOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in _parallaxDurationOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in _parallaxSmoothingOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in _parallaxLayerBehaviorOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in _stereoEyeSeparationOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in _stereoConvergenceOptions)
        {
            option.SetLanguage(IsSpanish);
        }

        foreach (var option in _stereoAnaglyphModeOptions)
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
        OnPropertyChanged(nameof(CanInteractWithImageWorkflow));
        OnPropertyChanged(nameof(IsImageWorkflowLockedByConversion));
        OnPropertyChanged(nameof(ImageSetupControlsEnabled));
        OnPropertyChanged(nameof(ImageWorkflowCardsEnabled));
        OnPropertyChanged(nameof(CanUseImageStepNavigation));
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
        OnPropertyChanged(nameof(ImageParallaxSummaryText));
        OnPropertyChanged(nameof(ImageStereoReadinessSummaryTitleText));
        OnPropertyChanged(nameof(ImageModelSelectionLabelText));
        OnPropertyChanged(nameof(ImageModelSelectionSharedNoteText));
        OnPropertyChanged(nameof(ImageParallaxLocalModelCandidates));
        OnPropertyChanged(nameof(HasImageParallaxLocalModelCandidates));
        OnPropertyChanged(nameof(ImageModelSelectorEnabled));
        OnPropertyChanged(nameof(ImageParallaxModelSelectorEnabled));
        OnPropertyChanged(nameof(CanShowImageParallaxModelHelp));
        OnPropertyChanged(nameof(ImageParallaxModelHelpButtonToolTipText));
        OnPropertyChanged(nameof(ImageSelectedModelSummaryText));
        OnPropertyChanged(nameof(ImageExpectedOutputFileText));
        OnPropertyChanged(nameof(ImageExpectedOutputPathText));
        OnPropertyChanged(nameof(ImageSaveLocationText));
        OnPropertyChanged(nameof(ImageSupportedStereoFormatsText));
        OnPropertyChanged(nameof(ImageBundledIw3StereoNoteText));
        OnPropertyChanged(nameof(ImageParallaxQualityGuidanceText));
        OnPropertyChanged(nameof(ImageParallaxModelGuidanceText));
        OnPropertyChanged(nameof(StereoOutputFormatOptions));
        OnPropertyChanged(nameof(CanConvertImage));
        OnPropertyChanged(nameof(ImageParallaxExportReadinessCanExport));
        OnPropertyChanged(nameof(ImageConvertActionText));
        OnPropertyChanged(nameof(ImageConvertButtonVisibility));
        OnPropertyChanged(nameof(ImageOpenOutputFolderButtonVisibility));
        OnPropertyChanged(nameof(ImageNewConversionButtonVisibility));
        OnPropertyChanged(nameof(ImageConvertDisabledReasonText));
        OnPropertyChanged(nameof(ImageConvertDisabledReasonVisibility));
        OnPropertyChanged(nameof(ImageParallaxPreviewImagePath));
        OnPropertyChanged(nameof(IsImageParallaxVideoPreviewAvailable));
        OnPropertyChanged(nameof(ImageParallaxGeneratedVideoPath));
        OnPropertyChanged(nameof(ImageParallaxVideoMediaSource));
        OnPropertyChanged(nameof(ImageParallaxVideoPreviewVisibility));
        OnPropertyChanged(nameof(ImageParallaxSourcePreviewVisibility));
        OnPropertyChanged(nameof(ImageParallaxVideoPreviewTitleText));
        OnPropertyChanged(nameof(ImageParallaxPreviewBadgeText));
        OnPropertyChanged(nameof(ImageStereoSummaryText));
        OnPropertyChanged(nameof(IsAnaglyphOutputSelected));
        OnPropertyChanged(nameof(ImageStereoAnaglyphModeVisibility));
        OnPropertyChanged(nameof(SelectedImageConversionMode));
        OnPropertyChanged(nameof(SelectedImageConversionStep));
        OnPropertyChanged(nameof(IsImageParallaxModeSelected));
        OnPropertyChanged(nameof(IsImageStereoModeSelected));
        OnPropertyChanged(nameof(ImageParallaxModeSelectionState));
        OnPropertyChanged(nameof(ImageStereoModeSelectionState));
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
        OnPropertyChanged(nameof(SelectedStereoOutputFormatOption));
        OnPropertyChanged(nameof(SelectedStereoOutputFormatDisplayText));
        AnalyzeImageCommand.RaiseCanExecuteChanged();
        SelectImageCommand.RaiseCanExecuteChanged();
        SelectImageModeSourceStepCommand.RaiseCanExecuteChanged();
        SelectImageParallaxModeCommand.RaiseCanExecuteChanged();
        SelectImageStereoModeCommand.RaiseCanExecuteChanged();
        ToggleImageWorkflowChooserCommand.RaiseCanExecuteChanged();
        SelectImageSetupStepCommand.RaiseCanExecuteChanged();
        SelectImagePreviewExportStepCommand.RaiseCanExecuteChanged();
        ImageWizardBackCommand.RaiseCanExecuteChanged();
        ImageWizardNextCommand.RaiseCanExecuteChanged();
        ContinueWithImageConversionCommand.RaiseCanExecuteChanged();
        ShowImageParallaxModelHelpCommand.RaiseCanExecuteChanged();
        RaiseImageExportPropertiesChanged();
    }

    private void RaiseImageExportPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsImageExportRunning));
        OnPropertyChanged(nameof(CanUseShellNavigation));
        OnPropertyChanged(nameof(CanOpenSettings));
        OnPropertyChanged(nameof(CanInteractWithImageWorkflow));
        OnPropertyChanged(nameof(IsImageWorkflowLockedByConversion));
        OnPropertyChanged(nameof(ImageSetupControlsEnabled));
        OnPropertyChanged(nameof(ImageWorkflowCardsEnabled));
        OnPropertyChanged(nameof(CanUseImageStepNavigation));
        OnPropertyChanged(nameof(ImageModelSelectorEnabled));
        OnPropertyChanged(nameof(ImageParallaxModelSelectorEnabled));
        OnPropertyChanged(nameof(CanExportStereoscopicImage));
        OnPropertyChanged(nameof(ImageStereoExportReadinessCanExport));
        OnPropertyChanged(nameof(CanConvertImage));
        OnPropertyChanged(nameof(ImageParallaxExportReadinessCanExport));
        OnPropertyChanged(nameof(CanOpenImageOutputFolder));
        OnPropertyChanged(nameof(CanStartNewImageConversion));
        OnPropertyChanged(nameof(ImageConvertActionText));
        OnPropertyChanged(nameof(ImageStereoExportActionText));
        OnPropertyChanged(nameof(ImageGeneratedFilesText));
        OnPropertyChanged(nameof(ImageLastExportedPathText));
        OnPropertyChanged(nameof(ImageExportProgressText));
        OnPropertyChanged(nameof(ImageExportOverlayText));
        OnPropertyChanged(nameof(ImageExportStatusText));
        OnPropertyChanged(nameof(ImageExportProgressPercent));
        OnPropertyChanged(nameof(ImageExportProgressVisibility));
        OnPropertyChanged(nameof(ImageExportSuccessVisibility));
        OnPropertyChanged(nameof(IsImageExportOutputOutdated));
        OnPropertyChanged(nameof(ImageExportOutdatedText));
        OnPropertyChanged(nameof(ImageExportOutdatedVisibility));
        OnPropertyChanged(nameof(ImageConvertButtonVisibility));
        OnPropertyChanged(nameof(ImageStereoConvertButtonVisibility));
        OnPropertyChanged(nameof(ImageOpenOutputFolderButtonVisibility));
        OnPropertyChanged(nameof(ImageNewConversionButtonVisibility));
        OnPropertyChanged(nameof(ImageExportFailureVisibility));
        OnPropertyChanged(nameof(ImageExportErrorText));
        OnPropertyChanged(nameof(ImageConvertDisabledReasonText));
        OnPropertyChanged(nameof(ImageConvertDisabledReasonVisibility));
        OnPropertyChanged(nameof(ImageStereoExportDisabledReasonText));
        OnPropertyChanged(nameof(ImageStereoExportDisabledReasonVisibility));
        OnPropertyChanged(nameof(ImageNotImplementedStateText));
        OnPropertyChanged(nameof(ImagePreviewExportStatusText));
        OnPropertyChanged(nameof(ImagePreviewExportStatusCardVisibility));
        OnPropertyChanged(nameof(ImagePreviewExportStatusRowHeight));
        OnPropertyChanged(nameof(ImageActivityLogCardMargin));
        OnPropertyChanged(nameof(ImageExpectedOutputFileText));
        OnPropertyChanged(nameof(ImageExpectedOutputPathText));
        OnPropertyChanged(nameof(ImageSupportedStereoFormatsText));
        OnPropertyChanged(nameof(SelectedStereoOutputFormatOption));
        OnPropertyChanged(nameof(SelectedStereoOutputFormatDisplayText));
        OnPropertyChanged(nameof(ImageStereoSummaryText));
        OnPropertyChanged(nameof(IsAnaglyphOutputSelected));
        OnPropertyChanged(nameof(ImageStereoAnaglyphModeVisibility));
        OnPropertyChanged(nameof(SelectedStereoAnaglyphMode));
        OnPropertyChanged(nameof(StereoAnaglyphModeOptions));
        OnPropertyChanged(nameof(ImageStereoPreviewImagePath));
        OnPropertyChanged(nameof(ImageParallaxPreviewImagePath));
        OnPropertyChanged(nameof(IsImageParallaxVideoPreviewAvailable));
        OnPropertyChanged(nameof(ImageParallaxGeneratedVideoPath));
        OnPropertyChanged(nameof(ImageParallaxVideoMediaSource));
        OnPropertyChanged(nameof(ImageParallaxVideoPreviewVisibility));
        OnPropertyChanged(nameof(ImageParallaxSourcePreviewVisibility));
        OnPropertyChanged(nameof(ImageParallaxVideoPreviewTitleText));
        OnPropertyChanged(nameof(ImageParallaxPreviewBadgeText));
        OnPropertyChanged(nameof(ImageParallaxQualityGuidanceText));
        OnPropertyChanged(nameof(ImageParallaxModelGuidanceText));
        ToggleSidebarCommand.RaiseCanExecuteChanged();
        SelectHomeSectionCommand.RaiseCanExecuteChanged();
        SelectImageConversionSectionCommand.RaiseCanExecuteChanged();
        SelectVideoConversionSectionCommand.RaiseCanExecuteChanged();
        OpenSettingsCommand.RaiseCanExecuteChanged();
        SelectImageCommand.RaiseCanExecuteChanged();
        AnalyzeImageCommand.RaiseCanExecuteChanged();
        SelectImageModeSourceStepCommand.RaiseCanExecuteChanged();
        SelectImageSetupStepCommand.RaiseCanExecuteChanged();
        SelectImagePreviewExportStepCommand.RaiseCanExecuteChanged();
        ImageWizardBackCommand.RaiseCanExecuteChanged();
        ImageWizardNextCommand.RaiseCanExecuteChanged();
        ContinueWithImageConversionCommand.RaiseCanExecuteChanged();
        SelectImageParallaxModeCommand.RaiseCanExecuteChanged();
        SelectImageStereoModeCommand.RaiseCanExecuteChanged();
        ToggleImageWorkflowChooserCommand.RaiseCanExecuteChanged();
        ShowImageParallaxModelHelpCommand.RaiseCanExecuteChanged();
        ShowModelHelpCommand.RaiseCanExecuteChanged();
        ClearImageLogCommand.RaiseCanExecuteChanged();
        ConvertImageCommand.RaiseCanExecuteChanged();
        ExportStereoscopicImageCommand.RaiseCanExecuteChanged();
        OpenImageOutputFolderCommand.RaiseCanExecuteChanged();
        NewImageConversionCommand.RaiseCanExecuteChanged();
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
        OnPropertyChanged(nameof(CanUseShellNavigation));
        OnPropertyChanged(nameof(CanOpenSettings));
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
        OnPropertyChanged(nameof(ImageDepthMapGenerationText));
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
        OnPropertyChanged(nameof(ImageConvertActionText));
        OnPropertyChanged(nameof(ImageStereoExportActionText));
        OnPropertyChanged(nameof(ImageOpenOutputFolderActionText));
        OnPropertyChanged(nameof(ImageNewConversionActionText));
        OnPropertyChanged(nameof(ImageNoOutputYetText));
        OnPropertyChanged(nameof(ImageGeneratedFilesText));
        OnPropertyChanged(nameof(ImageLastExportedPathText));
        OnPropertyChanged(nameof(ImageExportProgressText));
        OnPropertyChanged(nameof(ImageExportOverlayText));
        OnPropertyChanged(nameof(ImageExportStatusText));
        OnPropertyChanged(nameof(ImageExportOutdatedText));
        OnPropertyChanged(nameof(ImageExportErrorText));
        OnPropertyChanged(nameof(ImageConvertDisabledReasonText));
        OnPropertyChanged(nameof(ImageStereoExportDisabledReasonText));
        OnPropertyChanged(nameof(ImageResultParallaxTitleText));
        OnPropertyChanged(nameof(ImageExportOptionsTitleText));
        OnPropertyChanged(nameof(ImageResultSummaryTitleText));
        OnPropertyChanged(nameof(ImageResultSummaryText));
        OnPropertyChanged(nameof(ImageParallaxSummaryText));
        OnPropertyChanged(nameof(ImageStereoPreviewTitleText));
        OnPropertyChanged(nameof(ImageStereoControlsTitleText));
        OnPropertyChanged(nameof(ImageModelSelectionLabelText));
        OnPropertyChanged(nameof(ImageModelSelectionSharedNoteText));
        OnPropertyChanged(nameof(ImageStereoSummaryText));
        OnPropertyChanged(nameof(ImageStereoSeparationText));
        OnPropertyChanged(nameof(ImageStereoConvergenceText));
        OnPropertyChanged(nameof(ImageStereoAnaglyphText));
        OnPropertyChanged(nameof(ImageStereoResultTitleText));
        OnPropertyChanged(nameof(ImageGeneratedFilesTitleText));
        OnPropertyChanged(nameof(ImageOutputPanelTitleText));
        OnPropertyChanged(nameof(ImageComparisonTitleText));
        OnPropertyChanged(nameof(ImageMp41080pBadgeText));
        OnPropertyChanged(nameof(ImageLoopFriendlyMotionBadgeText));
        OnPropertyChanged(nameof(ImageProjectMetadataBadgeText));
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
        OnPropertyChanged(nameof(ImageStereoReadinessSummaryTitleText));
        OnPropertyChanged(nameof(ImageSelectedModelSummaryText));
        OnPropertyChanged(nameof(ImageExpectedOutputFileText));
        OnPropertyChanged(nameof(ImageExpectedOutputPathText));
        OnPropertyChanged(nameof(ImageSaveLocationText));
        OnPropertyChanged(nameof(ImageSupportedStereoFormatsText));
        OnPropertyChanged(nameof(ImageBundledIw3StereoNoteText));
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
        OnPropertyChanged(nameof(SelectedStereoOutputFormatOption));
        OnPropertyChanged(nameof(SelectedStereoOutputFormatDisplayText));
        OnPropertyChanged(nameof(IsAnaglyphOutputSelected));
        OnPropertyChanged(nameof(ImageStereoAnaglyphModeVisibility));
        OnPropertyChanged(nameof(SelectedStereoAnaglyphMode));
        OnPropertyChanged(nameof(StereoAnaglyphModeOptions));
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
        OnPropertyChanged(nameof(CommonCopyText));
        OnPropertyChanged(nameof(CommonSelectAllText));
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
        OnPropertyChanged(nameof(PreviewConversionStatusCardVisibility));
        OnPropertyChanged(nameof(PreviewConversionRowHeight));
        OnPropertyChanged(nameof(ActivityLogCardMargin));
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
        OnPropertyChanged(nameof(CanUseSettingsSystemStatusActions));
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
        OnPropertyChanged(nameof(CanUseSettingsSystemStatusActions));
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
        OnPropertyChanged(nameof(CanShowImageParallaxModelHelp));
        OnPropertyChanged(nameof(ModelHelpButtonText));
        OnPropertyChanged(nameof(ModelHelpButtonToolTipText));
        OnPropertyChanged(nameof(ImageParallaxModelHelpButtonToolTipText));
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
        ShowImageParallaxModelHelpCommand.RaiseCanExecuteChanged();
    }

    private void RaiseModalStatePropertiesChanged()
    {
        OnPropertyChanged(nameof(IsAnyModalOpen));
        OnPropertyChanged(nameof(ModalOverlayVisibility));
        OnPropertyChanged(nameof(ShellToolTipsEnabled));
        OnPropertyChanged(nameof(CanUseShellNavigation));
        OnPropertyChanged(nameof(CanOpenSettings));
        OnPropertyChanged(nameof(CanInteractWithImageWorkflow));
        OnPropertyChanged(nameof(CanUseSystemStatusActions));
        OnPropertyChanged(nameof(CanUseSettingsSystemStatusActions));
        OnPropertyChanged(nameof(CanShowModelHelp));
        OnPropertyChanged(nameof(CanShowImageParallaxModelHelp));
        OnPropertyChanged(nameof(ImageSetupControlsEnabled));
        OnPropertyChanged(nameof(ImageWorkflowCardsEnabled));
        OnPropertyChanged(nameof(CanUseImageStepNavigation));
        OnPropertyChanged(nameof(ImageModelSelectorEnabled));
        OnPropertyChanged(nameof(ImageParallaxModelSelectorEnabled));
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
        ToggleSidebarCommand.RaiseCanExecuteChanged();
        SelectHomeSectionCommand.RaiseCanExecuteChanged();
        SelectImageConversionSectionCommand.RaiseCanExecuteChanged();
        SelectVideoConversionSectionCommand.RaiseCanExecuteChanged();
        OpenSettingsCommand.RaiseCanExecuteChanged();
        RefreshEngineStatusCommand.RaiseCanExecuteChanged();
        OpenEngineFolderCommand.RaiseCanExecuteChanged();
        ShowTechnicalDetailsCommand.RaiseCanExecuteChanged();
        ShowModelInventoryCommand.RaiseCanExecuteChanged();
        ShowModelHelpCommand.RaiseCanExecuteChanged();
        ShowImageParallaxModelHelpCommand.RaiseCanExecuteChanged();
        SelectImageCommand.RaiseCanExecuteChanged();
        AnalyzeImageCommand.RaiseCanExecuteChanged();
        SelectImageModeSourceStepCommand.RaiseCanExecuteChanged();
        SelectImageSetupStepCommand.RaiseCanExecuteChanged();
        SelectImagePreviewExportStepCommand.RaiseCanExecuteChanged();
        SelectImageParallaxModeCommand.RaiseCanExecuteChanged();
        SelectImageStereoModeCommand.RaiseCanExecuteChanged();
        ToggleImageWorkflowChooserCommand.RaiseCanExecuteChanged();
        ImageWizardBackCommand.RaiseCanExecuteChanged();
        ImageWizardNextCommand.RaiseCanExecuteChanged();
        ContinueWithImageConversionCommand.RaiseCanExecuteChanged();
        ClearImageLogCommand.RaiseCanExecuteChanged();
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

    private void LogAnalysisFailure(VideoAnalysisFailure? failure)
    {
        switch (failure?.Kind)
        {
            case VideoAnalysisFailureKind.MissingFfprobe:
                AddVideoLogResolved(T(LocalizationKeys.VideoErrorAnalysisMissingFfprobe));
                break;
            case VideoAnalysisFailureKind.ProcessFailed:
                AddVideoLogResolved(T(
                    LocalizationKeys.VideoErrorAnalysisProcessFailedFormat,
                    ("detail", failure.StandardError)));
                break;
            case VideoAnalysisFailureKind.EmptyOutput:
                AddVideoLogResolved(T(LocalizationKeys.VideoErrorAnalysisEmptyOutput));
                break;
            case VideoAnalysisFailureKind.InvalidJson:
                AddVideoLogResolved(T(LocalizationKeys.VideoErrorAnalysisInvalidJson));
                break;
            case VideoAnalysisFailureKind.TimedOut:
                AddVideoLogResolved(T(LocalizationKeys.VideoErrorAnalysisTimedOut));
                break;
            case VideoAnalysisFailureKind.Canceled:
                AddVideoLogResolved(T(LocalizationKeys.VideoErrorAnalysisCanceled));
                break;
            default:
                AddVideoLogResolved(T(LocalizationKeys.VideoErrorAnalysisFailed));
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
