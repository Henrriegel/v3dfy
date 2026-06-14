using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using V3dfy.App.Mvvm;
using V3dfy.App.Services;
using V3dfy.Core.Analysis;
using V3dfy.Core.Diagnostics;
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

public sealed class MainWindowViewModel : ObservableObject
{
    private const string SetupHelperExecutableName = "V3dfy.SetupHelper.exe";

    private static readonly HashSet<string> SupportedVideoExtensions =
    [
        ".mp4",
        ".mkv",
        ".avi",
        ".mov",
        ".m4v",
        ".webm",
    ];

    private enum ToolStatusComponent
    {
        BundledTool,
        EmbeddedPython,
        Iw3Engine,
        Models,
        Iw3RuntimeDependency,
    }

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
    private PreviewWorkflowState _previewState =
        PreviewWorkflowState.NotGenerated(TimeSpan.Zero, PreviewTimeRangeService.DefaultDuration);
    private LocalModelSelectionCandidate? _selectedLocalModelCandidate;
    private TargetDevicePreset _selectedOutputPreset = TargetDevicePresets.General3dVideo;
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
    private TaskCompletionSource<bool>? _modelPackImportConfirmationCompletion;
    private ModelPackImportConfirmationPrompt? _modelPackImportConfirmationPrompt;
    private bool _reopenModelInventoryAfterImport;
    private CancellationTokenSource? _conversionCancellationTokenSource;
    private CancellationTokenSource? _previewCancellationTokenSource;
    private CancellationTokenSource? _logCopyNotificationCancellationTokenSource;
    private bool _isUpdatingOutputPathText;
    private bool _isAnalyzing;
    private bool _isTechnicalDetailsModalOpen;
    private bool _isProfileDetailsModalOpen;
    private bool _isModelHelpModalOpen;
    private bool _isReplaceVideoConfirmationModalOpen;
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
    private bool _isModelPackImportRunning;
    private bool _isGlobalBusyOverlayVisible;
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
        AcceptConversionCompletedCommand = new RelayCommand(AcceptConversionCompleted);

        _themeService.Apply(_selectedTheme);
        _ = CleanStalePreviewFilesAsync();
        _ = RefreshEngineStatusAsync(logRefresh: true);
        AddLog(
            "Application shell ready. Select a video to begin.",
            "La aplicación está lista. Selecciona un video para comenzar.");
    }

    public string AppTitle => "v3dfy";

    public string? SelectedVideoPath
    {
        get => _selectedVideoPath;
        private set
        {
            if (SetProperty(ref _selectedVideoPath, value))
            {
                OnPropertyChanged(nameof(SelectedVideoDisplayPath));
                OnPropertyChanged(nameof(HasSelectedVideo));
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
                OnPropertyChanged();
                if (selectedTabIndexChanged)
                {
                    OnPropertyChanged(nameof(SelectedWorkflowTabIndex));
                }

                OnPropertyChanged(nameof(CanOpenSystemStatusConversionTab));
                OnPropertyChanged(nameof(SelectedSystemStatusTabIndex));
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

    public string SelectSourceTitle => Text(
        "1. Select source video",
        "1. Selecciona el video de origen");

    public string DropVideoText => Text(
        "Drop one video file here or browse for a file.",
        "Arrastra un archivo de video aquí o selecciona uno.");

    public string NoVideoSelectedText => Text(
        "No video selected yet.",
        "Aún no hay video seleccionado.");

    public string SelectVideoText => Text("Select video", "Seleccionar video");

    public string AnalyzeText => Text("Analyze", "Analizar");

    public IReadOnlyList<LocalizedOptionViewModel<TargetDevicePreset>> OutputPresetOptions { get; } =
    [
        new(TargetDevicePresets.General3dVideo, "General 3D video", "Video 3D general"),
        new(TargetDevicePresets.Lg3dFullHd2012, "LG 3D Full HD 2012", "LG 3D Full HD 2012"),
    ];

    public TargetDevicePreset SelectedOutputPreset
    {
        get => _selectedOutputPreset;
        set
        {
            if (SetProperty(ref _selectedOutputPreset, value))
            {
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
                        : "Copia MP4 compatible con LG desactivada.");
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
                        : "Salida principal seleccionada como destino preferido al abrir.");
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
        get => _workflowState.SelectedTabIndex;
        set
        {
            if (_workflowState.SetSelectedTabIndex(value))
            {
                OnPropertyChanged();
            }
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
            if (_isApplyingUiOnlyRefresh && value is null)
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
        IsConversionRunning || IsPreviewGenerating || IsCurrentPreviewAccepted
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility GeneratePreviewPrimaryActionVisibility =>
        !IsConversionRunning && !IsPreviewGenerating && !IsCurrentPreviewAccepted
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ConvertPrimaryActionVisibility =>
        IsCurrentPreviewAccepted && !IsConversionRunning && !IsPreviewGenerating
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
                return ActivityLogTitle;
            }

            if (IsReplaceVideoConfirmationModalOpen)
            {
                return ReplaceSelectedVideoTitleText;
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
        IsReplaceVideoConfirmationModalOpen ||
        IsModelInventoryModalOpen ||
        IsModelPackImportConfirmationModalOpen ||
        IsConversionCompletedModalOpen ||
        IsActivityLogModalOpen ||
        IsPreviewGeneratingModalOpen ||
        IsPreviewReadyModalOpen;

    public Visibility ModalOverlayVisibility =>
        IsAnyModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public double ActiveModalWidth => IsModelInventoryModalOpen || IsModelHelpModalOpen ? 1000d : 760d;

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

    public Visibility ReplaceVideoConfirmationModalContentVisibility =>
        IsReplaceVideoConfirmationModalOpen ? Visibility.Visible : Visibility.Collapsed;

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

    public bool CanStartConversion =>
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

    public ObservableCollection<LogEntryViewModel> ConversionLogs { get; } = [];

    public ObservableCollection<string> PreviewGenerationLogs { get; } = [];

    public ObservableCollection<LocalModelSelectionCandidate> LocalModelCandidates { get; } = [];

    public RelayCommand SelectVideoCommand { get; }

    public AsyncRelayCommand AnalyzeCommand { get; }

    public AsyncRelayCommand RefreshEngineStatusCommand { get; }

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
        if (replacingVideo && !await ConfirmReplaceSelectedVideoAsync())
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
                SizePerformance: GetSizeClassText(entry, IsSpanish)));
        }

        return rows;
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

    private void ShowModelInventory()
    {
        if (IsConversionRunning ||
            IsPreviewGenerating ||
            IsTechnicalDetailsModalOpen ||
            IsProfileDetailsModalOpen ||
            IsModelHelpModalOpen ||
            IsReplaceVideoConfirmationModalOpen ||
            IsModelPackImportConfirmationModalOpen ||
            IsActivityLogModalOpen ||
            IsPreviewReadyModalOpen ||
            IsConversionCompletedModalOpen)
        {
            return;
        }

        RaiseModelInventoryPropertiesChanged();
        IsModelInventoryModalOpen = true;
    }

    private void CloseModelInventory()
    {
        IsModelInventoryModalOpen = false;
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
        ActivityLogModalText = CreateFullActivityLogText();
        IsActivityLogModalOpen = true;
    }

    private void CopyFullLog()
    {
        var logText = CreateFullActivityLogText();
        ActivityLogModalText = logText;
        CopyLogToClipboard(
            logText,
            englishLogName: "activity log",
            spanishLogName: "registro de actividad",
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

    private void ConfirmReplaceVideo()
    {
        CompleteReplaceVideoConfirmation(replaceVideo: true);
    }

    private void CancelReplaceVideo()
    {
        CompleteReplaceVideoConfirmation(replaceVideo: false);
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
        UpdateConversionReadiness();

        if (!regeneratePlan)
        {
            return;
        }

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
        ResetConversionExecutionState();
        HasCompletedAnalysis = false;
        _analysis = null;
        _conversionRecommendation = null;
        _conversionPlan = null;

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
        SelectedSystemStatusTabIndex = 1;
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
        SelectedWorkflowTabIndex = 0;
        SelectedSystemStatusTabIndex = 0;
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

    private void PlanOptionChanged(string englishMessage, string spanishMessage)
    {
        ResetConversionExecutionState();

        if (RegenerateConversionPlan())
        {
            MarkPreviewOutdatedIfNeeded();
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
        if (DispatchToUiThreadIfNeeded(() => ShowLogCopyNotification(englishText, spanishText)))
        {
            return;
        }

        _logCopyNotificationEnglishText = englishText;
        _logCopyNotificationSpanishText = spanishText;
        _isLogCopyNotificationVisible = true;
        OnPropertyChanged(nameof(LogCopyNotificationText));
        OnPropertyChanged(nameof(LogCopyNotificationVisibility));

        _logCopyNotificationCancellationTokenSource?.Cancel();
        _logCopyNotificationCancellationTokenSource?.Dispose();
        var cancellationTokenSource = new CancellationTokenSource();
        _logCopyNotificationCancellationTokenSource = cancellationTokenSource;
        _ = HideLogCopyNotificationAfterDelayAsync(cancellationTokenSource);
    }

    private async Task HideLogCopyNotificationAfterDelayAsync(
        CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationTokenSource.Token);
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

    private void HideLogCopyNotification(CancellationTokenSource cancellationTokenSource)
    {
        if (!ReferenceEquals(_logCopyNotificationCancellationTokenSource, cancellationTokenSource))
        {
            return;
        }

        _isLogCopyNotificationVisible = false;
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

        OnPropertyChanged(nameof(ActivityLogPanelText));
        if (IsActivityLogModalOpen)
        {
            ActivityLogModalText = CreateFullActivityLogText();
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
    }

    private void RaiseLocalizedPropertiesChanged()
    {
        OnPropertyChanged(nameof(SubtitleText));
        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(ThemeLabel));
        OnPropertyChanged(nameof(SelectSourceTitle));
        OnPropertyChanged(nameof(DropVideoText));
        OnPropertyChanged(nameof(NoVideoSelectedText));
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
        OnPropertyChanged(nameof(GlobalBusyText));
        OnPropertyChanged(nameof(ClearText));
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
    }

    private void RaiseAnalysisPropertiesChanged()
    {
        OnPropertyChanged(nameof(AnalysisStatusText));
        OnPropertyChanged(nameof(AnalysisDurationText));
        OnPropertyChanged(nameof(AnalysisResolutionText));
        OnPropertyChanged(nameof(AnalysisFpsText));
        OnPropertyChanged(nameof(AnalysisCodecText));
        OnPropertyChanged(nameof(AnalysisContainerText));
        OnPropertyChanged(nameof(AnalysisAudioStreamsText));
        OnPropertyChanged(nameof(AnalysisSubtitleStreamsText));
        OnPropertyChanged(nameof(AnalysisHdrText));
        OnPropertyChanged(nameof(AnalysisCompatibilityText));
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

    private void RaisePreviewPropertiesChanged()
    {
        OnPropertyChanged(nameof(PreviewTitleText));
        OnPropertyChanged(nameof(PreviewRequiredTitleText));
        OnPropertyChanged(nameof(PreviewAcceptedTitleText));
        OnPropertyChanged(nameof(PreviewStepTitleText));
        OnPropertyChanged(nameof(GeneratePreviewText));
        OnPropertyChanged(nameof(PreviewRequiredInstructionText));
        OnPropertyChanged(nameof(PreviewRequirementVisibility));
        OnPropertyChanged(nameof(ConversionReadySummaryVisibility));
        OnPropertyChanged(nameof(ConversionMissingRequirementsVisibility));
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
        OnPropertyChanged(nameof(CanOpenRecommendedSetupTab));
        OnPropertyChanged(nameof(CanOpenConversionPlanTab));
        OnPropertyChanged(nameof(CanOpenSystemStatusConversionTab));
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
        OnPropertyChanged(nameof(ConversionCompletedTitleText));
        OnPropertyChanged(nameof(ConversionCompletedBodyText));
        OnPropertyChanged(nameof(ConversionCompletedOutputPathText));
        OnPropertyChanged(nameof(AcceptConversionCompletedText));
        RaiseModelPackImportConfirmationPropertiesChanged();
        OnPropertyChanged(nameof(ActiveModalTitleText));
        OnPropertyChanged(nameof(TechnicalDetailsBodyText));
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
        OnPropertyChanged(nameof(LogCopiedText));
        OnPropertyChanged(nameof(CouldNotCopyLogText));
        OnPropertyChanged(nameof(LogCopyNotificationText));
        RaiseModelPackImportAvailabilityPropertiesChanged();
        ShowModelInventoryCommand.RaiseCanExecuteChanged();
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
        OnPropertyChanged(nameof(ActiveModalTitleText));
        OnPropertyChanged(nameof(TechnicalDetailsModalContentVisibility));
        OnPropertyChanged(nameof(ProfileDetailsModalContentVisibility));
        OnPropertyChanged(nameof(ModelHelpModalContentVisibility));
        OnPropertyChanged(nameof(ReplaceVideoConfirmationModalContentVisibility));
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
        OnPropertyChanged(nameof(DiagnosticModelsSectionTitleText));
        OnPropertyChanged(nameof(DiagnosticModelsInventoryText));
        OnPropertyChanged(nameof(RuntimeDependenciesSectionTitleText));
        OnPropertyChanged(nameof(RuntimeDependenciesInventoryText));
        OnPropertyChanged(nameof(ModelInventoryActionsTitleText));
        OnPropertyChanged(nameof(OpenModelsFolderText));
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
