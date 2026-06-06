using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
using V3dfy.Core.Processes;
using V3dfy.Core.Recommendations;
using V3dfy.Core.Readiness;
using V3dfy.Core.Workflow;
using V3dfy.Engine.Iw3.Execution;
using V3dfy.Engine.Iw3.Planning;
using V3dfy.Infrastructure.Analysis;
using V3dfy.Infrastructure.Files;
using V3dfy.Infrastructure.Health;
using V3dfy.Infrastructure.Paths;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
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
    private readonly ConversionPlanOptionState _planOptionState = new();
    private readonly ConversionOutputPathState _outputPathState = new();
    private readonly ConversionWorkflowState _workflowState = new();
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
    private LocalModelSelectionCandidate? _selectedLocalModelCandidate;
    private TargetDevicePreset _selectedOutputPreset = TargetDevicePresets.General3dVideo;
    private string _outputPathText = string.Empty;
    private string _technicalDetailsBodyText = string.Empty;
    private string _lastConversionOutputLine = string.Empty;
    private string _cpuUsageText = "CPU: Detecting...";
    private string _ramUsageText = "RAM: Detecting...";
    private string _gpuUsageText = "GPU: Detecting...";
    private string _vramUsageText = string.Empty;
    private ProcessMetricSample? _lastProcessMetricSample;
    private TaskCompletionSource<bool>? _replaceVideoConfirmationCompletion;
    private CancellationTokenSource? _conversionCancellationTokenSource;
    private bool _isUpdatingOutputPathText;
    private bool _isAnalyzing;
    private bool _isTechnicalDetailsModalOpen;
    private bool _isProfileDetailsModalOpen;
    private bool _isReplaceVideoConfirmationModalOpen;
    private bool _hasLiveConversionOutput;
    private bool _openOutputWhenFinished;

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

        SelectVideoCommand = new RelayCommand(SelectVideo, () => !IsConversionRunning);
        AnalyzeCommand = new AsyncRelayCommand(
            AnalyzeAsync,
            () => !IsAnalyzing && !IsConversionRunning);
        RefreshEngineStatusCommand = new RelayCommand(
            RefreshEngineStatus,
            () => CanUseSystemStatusActions);
        OpenEngineFolderCommand = new RelayCommand(OpenEngineFolder);
        ClearLogsCommand = new RelayCommand(Logs.Clear);
        BrowseOutputFolderCommand = new RelayCommand(
            BrowseOutputFolder,
            () => !IsConversionRunning);
        ResetOutputPathCommand = new RelayCommand(
            ResetOutputPath,
            () => !IsConversionRunning);
        StartConversionCommand = new RelayCommand(
            StartOrCancelConversion,
            () => CanStartOrCancelConversion);
        CancelConversionCommand = new RelayCommand(CancelConversion);
        ShowTechnicalDetailsCommand = new RelayCommand(
            ShowTechnicalDetails,
            () => CanUseSystemStatusActions);
        CloseTechnicalDetailsCommand = new RelayCommand(CloseTechnicalDetails);
        ShowProfileDetailsCommand = new RelayCommand(
            ShowProfileDetails,
            () => !IsConversionRunning);
        CloseProfileDetailsCommand = new RelayCommand(CloseProfileDetails);
        ConfirmReplaceVideoCommand = new RelayCommand(ConfirmReplaceVideo);
        CancelReplaceVideoCommand = new RelayCommand(CancelReplaceVideo);

        _themeService.Apply(_selectedTheme);
        RefreshEngineStatus();
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
                RaiseLocalizedPropertiesChanged();
                UpdateToolStatuses();
                UpdatePlanOptionLanguages();
                UpdateLogLanguages();
                RefreshMetricLanguage();
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
                _themeService.Apply(value);
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
        !IsConversionRunning && IsLgOutputProfileSelected;

    public bool CanPreferLgCompatibilityCopyWhenOpening =>
        !IsConversionRunning &&
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

    public bool CanOpenSystemStatusToolsTab => !IsConversionRunning;

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        private set
        {
            if (SetProperty(ref _isAnalyzing, value))
            {
                AnalyzeCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(AnalysisStatusText));
            }
        }
    }

    public string VideoAnalysisTitle => Text("Video analysis", "Análisis de video");

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

    public LocalModelSelectionCandidate? SelectedLocalModelCandidate
    {
        get => _selectedLocalModelCandidate;
        set => SetSelectedLocalModelCandidate(value, regeneratePlan: true);
    }

    public string LocalModelSelectionStatusText => SelectedLocalModelCandidate is null
        ? Text("No local models detected yet.", "A\u00fan no se detectan modelos locales.")
        : Text(
            $"Selected local model: {SelectedLocalModelCandidate.DisplayName}",
            $"Modelo local seleccionado: {SelectedLocalModelCandidate.DisplayName}");

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
            if (IsConversionRunning)
            {
                OnPropertyChanged();
                return;
            }

            SetProperty(ref _openOutputWhenFinished, value);
        }
    }

    public bool CanChangeOpenOutputWhenFinished => !IsConversionRunning;

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
            $"Local model: {_conversionPlan.SelectedLocalModel.DisplayName} ({_conversionPlan.SelectedLocalModel.RelativePath}, {_conversionPlan.SelectedLocalModel.EnglishSourceText})",
            $"Modelo local: {_conversionPlan.SelectedLocalModel.DisplayName} ({_conversionPlan.SelectedLocalModel.RelativePath}, {_conversionPlan.SelectedLocalModel.SpanishSourceText})");

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

    public string ConversionExecutionDetailText => Text(
        _conversionExecutionState.DetailEnglish,
        _conversionExecutionState.DetailSpanish);

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

    public string ProfileDetailsTitleText => Text(
        "Profile details",
        "Detalles del perfil");

    public string ProfileDetailsButtonText => "?";

    public string ActiveModalTitleText => IsReplaceVideoConfirmationModalOpen
        ? ReplaceSelectedVideoTitleText
        : IsProfileDetailsModalOpen
            ? ProfileDetailsTitleText
            : SystemStatusTechnicalDetailsTitle;

    public bool IsAnyModalOpen =>
        IsTechnicalDetailsModalOpen ||
        IsProfileDetailsModalOpen ||
        IsReplaceVideoConfirmationModalOpen;

    public Visibility ModalOverlayVisibility =>
        IsAnyModalOpen ? Visibility.Visible : Visibility.Collapsed;

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

    public Visibility TechnicalDetailsModalContentVisibility =>
        IsTechnicalDetailsModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ProfileDetailsModalContentVisibility =>
        IsProfileDetailsModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ReplaceVideoConfirmationModalContentVisibility =>
        IsReplaceVideoConfirmationModalOpen ? Visibility.Visible : Visibility.Collapsed;

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

    public string ConversionReadinessStatusText
    {
        get
        {
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
        _dependencyHealth is null || !HasCompletedAnalysis
            ? "-"
            : CreateMissingComponentsSummary();

    public string ConversionReadinessRequiredComponentsText => _conversionReadiness is null
        ? string.Empty
        : Text(
            _conversionReadiness.EnglishRequiredComponentsSummary,
            _conversionReadiness.SpanishRequiredComponentsSummary);

    public string ConversionBlockedReasonText
    {
        get
        {
            var startGate = EvaluateConversionStartGate();
            return startGate.CanStart
                ? string.Empty
                : Text(startGate.EnglishDetail, startGate.SpanishDetail);
        }
    }

    public bool CanStartConversion =>
        _conversionExecutionState.Status is not ConversionExecutionStatus.Running and
            not ConversionExecutionStatus.Canceling &&
        _conversionExecutionFeatureGate.EvaluateStart(
            HasCompletedAnalysis,
            _conversionPlan is not null,
            _conversionReadiness).CanStart &&
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

    public bool CanUseSystemStatusActions => !IsConversionRunning;

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

    public ObservableCollection<LocalModelSelectionCandidate> LocalModelCandidates { get; } = [];

    public RelayCommand SelectVideoCommand { get; }

    public AsyncRelayCommand AnalyzeCommand { get; }

    public RelayCommand RefreshEngineStatusCommand { get; }

    public RelayCommand OpenEngineFolderCommand { get; }

    public RelayCommand ClearLogsCommand { get; }

    public RelayCommand BrowseOutputFolderCommand { get; }

    public RelayCommand ResetOutputPathCommand { get; }

    public RelayCommand StartConversionCommand { get; }

    public RelayCommand CancelConversionCommand { get; }

    public RelayCommand ShowTechnicalDetailsCommand { get; }

    public RelayCommand CloseTechnicalDetailsCommand { get; }

    public RelayCommand ShowProfileDetailsCommand { get; }

    public RelayCommand CloseProfileDetailsCommand { get; }

    public RelayCommand ConfirmReplaceVideoCommand { get; }

    public RelayCommand CancelReplaceVideoCommand { get; }

    public async void SelectDroppedVideo(string path)
    {
        if (IsConversionRunning)
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
        if (IsConversionRunning)
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
        if (IsConversionRunning)
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
        await AnalyzeSelectedVideoAsync();
    }

    private Task<bool> ConfirmReplaceSelectedVideoAsync()
    {
        IsTechnicalDetailsModalOpen = false;
        IsProfileDetailsModalOpen = false;
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

        RefreshEngineStatus();
        IsAnalyzing = true;

        try
        {
            var result = await _videoAnalysisService.AnalyzeAsync(new VideoAnalysisRequest(
                InputPath: inputPath,
                Timeout: TimeSpan.FromSeconds(30)));

            if (result.IsSuccess && result.Analysis is not null)
            {
                _analysis = result.Analysis;
                _conversionRecommendation = _recommendationService.Recommend(
                    _analysis,
                    SelectedOutputPreset);
                ApplyRecommendationDefaultsIfNeeded(_conversionRecommendation);
                RegenerateConversionPlan();
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

    private void RefreshEngineStatus()
    {
        if (IsConversionRunning)
        {
            return;
        }

        _dependencyHealth = _healthChecker.CheckDetailed(_toolPaths);
        _toolHealth = _dependencyHealth.Summary;
        UpdateLocalModelSelectionCandidates();
        UpdateToolStatuses();
        UpdateConversionReadiness();
        AddLog(
            "Internal tool status refreshed.",
            "Estado de herramientas internas actualizado.");
    }

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

    private void ShowTechnicalDetails()
    {
        if (IsConversionRunning ||
            IsProfileDetailsModalOpen ||
            IsReplaceVideoConfirmationModalOpen)
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
            IsTechnicalDetailsModalOpen ||
            IsReplaceVideoConfirmationModalOpen)
        {
            return;
        }

        IsProfileDetailsModalOpen = true;
    }

    private void CloseProfileDetails()
    {
        IsProfileDetailsModalOpen = false;
    }

    private void ConfirmReplaceVideo()
    {
        CompleteReplaceVideoConfirmation(replaceVideo: true);
    }

    private void CancelReplaceVideo()
    {
        CompleteReplaceVideoConfirmation(replaceVideo: false);
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

    private void UpdateLocalModelSelectionCandidates()
    {
        var previouslySelectedPath = SelectedLocalModelCandidate?.RelativePath;
        var candidates = _dependencyHealth?.ModelInventory.SelectionCandidates ?? [];

        LocalModelCandidates.Clear();
        foreach (var candidate in candidates)
        {
            LocalModelCandidates.Add(candidate);
        }

        var selectedCandidate = !string.IsNullOrWhiteSpace(previouslySelectedPath)
            ? LocalModelCandidates.FirstOrDefault(candidate => string.Equals(
                candidate.RelativePath,
                previouslySelectedPath,
                StringComparison.OrdinalIgnoreCase))
            : null;

        SetSelectedLocalModelCandidate(
            selectedCandidate ?? LocalModelCandidates.FirstOrDefault(),
            regeneratePlan: _analysis is not null && _conversionRecommendation is not null);

        OnPropertyChanged(nameof(HasLocalModelSelectionCandidates));
    }

    private void SetSelectedLocalModelCandidate(
        LocalModelSelectionCandidate? candidate,
        bool regeneratePlan = false)
    {
        if (EqualityComparer<LocalModelSelectionCandidate?>.Default.Equals(
            _selectedLocalModelCandidate,
            candidate))
        {
            return;
        }

        _selectedLocalModelCandidate = candidate;
        OnPropertyChanged(nameof(SelectedLocalModelCandidate));
        OnPropertyChanged(nameof(LocalModelSelectionStatusText));
        UpdateConversionReadiness();

        if (!regeneratePlan)
        {
            return;
        }

        ResetConversionExecutionState();
        if (!RegenerateConversionPlan())
        {
            OnPropertyChanged(nameof(ConversionPlanLocalModelText));
        }
    }

    private ConversionExecutionStartGateResult EvaluateConversionStartGate() =>
        _conversionExecutionFeatureGate.EvaluateStart(
            HasCompletedAnalysis,
            _conversionPlan is not null,
            _conversionReadiness);

    private bool CurrentExecutionRequestCanStart()
    {
        if (_conversionPlan is null)
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
    }

    private ToolStatusItemViewModel CreateToolStatus(
        string englishName,
        string spanishName,
        ToolDependencyHealth dependencyHealth,
        ToolStatusComponent component) => new(
        Name: Text(englishName, spanishName),
        StatusText: dependencyHealth.Status == ToolHealthStatus.Found
            ? Text("Found", "Encontrado")
            : Text("Missing", "Faltante"),
        ReasonText: ToolStatusReasonText(dependencyHealth, component),
        DetailText: ToolStatusDetailText(dependencyHealth, component),
        ContextActionText: component == ToolStatusComponent.Iw3Engine
            ? Text("Open", "Abrir")
            : string.Empty,
        ContextActionToolTip: component == ToolStatusComponent.Iw3Engine
            ? OpenEngineFolderText
            : string.Empty,
        ContextActionVisibility: component == ToolStatusComponent.Iw3Engine
            ? Visibility.Visible
            : Visibility.Collapsed);

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

    private void StartOrCancelConversion()
    {
        if (IsConversionRunning)
        {
            CancelConversion();
            return;
        }

        _ = StartConversionAsync();
    }

    private async Task StartConversionAsync()
    {
        if (IsConversionRunning)
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
        ConversionLogs.Clear();
        _lastConversionOutputLine = string.Empty;
        _hasLiveConversionOutput = false;
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
            HandleOpenOutputWhenFinished(result, request.OutputPath);
            _conversionExecutionState = CreateFinishedConversionState(result);
        }
        catch (OperationCanceledException)
        {
            _conversionExecutionState = CreateCanceledConversionState(
                startedAt,
                DateTimeOffset.UtcNow);
            AddLog(
                "Local iw3 conversion was canceled.",
                "La conversion local iw3 fue cancelada.");
            AddConversionLog(
                "Local iw3 conversion was canceled.",
                "La conversion local iw3 fue cancelada.");
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
        if (normalizedUpdate.OutputLine is not null)
        {
            AddConversionOutputLine(normalizedUpdate.OutputLine);
        }

        if (normalizedUpdate.Metrics is not null)
        {
            UpdateMetricText(normalizedUpdate.Metrics);
        }

        _conversionExecutionState = _conversionExecutionState with
        {
            ProgressPercent = normalizedUpdate.ProgressPercent,
            CurrentStep = normalizedUpdate.CurrentStep,
            DetailEnglish = normalizedUpdate.DetailEnglish,
            DetailSpanish = normalizedUpdate.DetailSpanish,
        };
        RaiseConversionExecutionPropertiesChanged();
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
            return;
        }

        UpdateMetricText(_lastProcessMetricSample);
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
            "La conversion local iw3 fue cancelada."),
        DetailEnglish: "Local iw3 conversion was canceled.",
        DetailSpanish: "La conversion local iw3 fue cancelada.",
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
        if (IsConversionRunning)
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
        if (IsConversionRunning)
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
                SelectedThreeDOutputFormat);
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

    private string Text(string english, string spanish) => IsSpanish ? spanish : english;

    private string LabelValue(string englishLabel, string spanishLabel, string? value) =>
        $"{Text(englishLabel, spanishLabel)}: {value ?? "-"}";

    private void AddLog(string englishMessage, string spanishMessage) =>
        Logs.Add(new LogEntryViewModel(
            timestamp: DateTime.Now,
            englishMessage,
            spanishMessage,
            isSpanish: IsSpanish));

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
        RaiseConversionReadinessPropertiesChanged();
        RaiseSystemStatusPropertiesChanged();
        OnPropertyChanged(nameof(ToolStatusTitle));
        OnPropertyChanged(nameof(RefreshText));
        OnPropertyChanged(nameof(OpenEngineFolderText));
        OnPropertyChanged(nameof(ActivityLogTitle));
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
        OnPropertyChanged(nameof(SelectedLocalModelCandidate));
        OnPropertyChanged(nameof(LocalModelSelectionStatusText));
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
        OnPropertyChanged(nameof(ConversionExecutionDetailText));
        OnPropertyChanged(nameof(CanCancelConversion));
        OnPropertyChanged(nameof(CancelConversionText));
        OnPropertyChanged(nameof(CanStartConversion));
        OnPropertyChanged(nameof(CanStartOrCancelConversion));
        OnPropertyChanged(nameof(StartConversionText));
        OnPropertyChanged(nameof(ConversionSummaryCurrentStatusText));
        OnPropertyChanged(nameof(CanChangeOpenOutputWhenFinished));
        OnPropertyChanged(nameof(CanChangeLgCompatibilityCopyOptions));
        OnPropertyChanged(nameof(CanPreferLgCompatibilityCopyWhenOpening));
        StartConversionCommand.RaiseCanExecuteChanged();
        ShowProfileDetailsCommand.RaiseCanExecuteChanged();
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
        SelectVideoCommand.RaiseCanExecuteChanged();
        AnalyzeCommand.RaiseCanExecuteChanged();
        BrowseOutputFolderCommand.RaiseCanExecuteChanged();
        ResetOutputPathCommand.RaiseCanExecuteChanged();
        RefreshEngineStatusCommand.RaiseCanExecuteChanged();
        ShowTechnicalDetailsCommand.RaiseCanExecuteChanged();
        ShowProfileDetailsCommand.RaiseCanExecuteChanged();
        StartConversionCommand.RaiseCanExecuteChanged();
        RaiseSystemStatusPropertiesChanged();
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
        OnPropertyChanged(nameof(ActiveModalTitleText));
        OnPropertyChanged(nameof(TechnicalDetailsBodyText));
        OnPropertyChanged(nameof(ProfileDetailsBodyText));
        OnPropertyChanged(nameof(CanOpenSystemStatusConversionTab));
        OnPropertyChanged(nameof(CanOpenSystemStatusToolsTab));
        OnPropertyChanged(nameof(CanUseSystemStatusActions));
        OnPropertyChanged(nameof(SelectedSystemStatusTabIndex));
        OnPropertyChanged(nameof(ConversionReadinessEmptyText));
        OnPropertyChanged(nameof(VramUsageText));
    }

    private void RaiseModalStatePropertiesChanged()
    {
        OnPropertyChanged(nameof(IsAnyModalOpen));
        OnPropertyChanged(nameof(ModalOverlayVisibility));
        OnPropertyChanged(nameof(ActiveModalTitleText));
        OnPropertyChanged(nameof(TechnicalDetailsModalContentVisibility));
        OnPropertyChanged(nameof(ProfileDetailsModalContentVisibility));
        OnPropertyChanged(nameof(ReplaceVideoConfirmationModalContentVisibility));
    }

    private void RaiseConversionReadinessPropertiesChanged()
    {
        OnPropertyChanged(nameof(ConversionReadinessTitle));
        OnPropertyChanged(nameof(ShowConversionReadinessCard));
        OnPropertyChanged(nameof(ConversionReadinessVisibility));
        OnPropertyChanged(nameof(ConversionReadinessStatusLabel));
        OnPropertyChanged(nameof(ConversionReadinessMissingRequirementsTitle));
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
}
