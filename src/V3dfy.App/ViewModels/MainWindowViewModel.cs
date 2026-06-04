using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using V3dfy.App.Mvvm;
using V3dfy.App.Services;
using V3dfy.Core.Analysis;
using V3dfy.Core.Execution;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Core.Recommendations;
using V3dfy.Core.Readiness;
using V3dfy.Core.Workflow;
using V3dfy.Engine.Iw3.Planning;
using V3dfy.Infrastructure.Analysis;
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
    private readonly ConversionExecutionFeatureGate _conversionExecutionFeatureGate;
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
    private TargetDevicePreset _selectedOutputPreset = TargetDevicePresets.General3dVideo;
    private string _outputPathText = string.Empty;
    private string _technicalDetailsBodyText = string.Empty;
    private TaskCompletionSource<bool>? _replaceVideoConfirmationCompletion;
    private bool _isUpdatingOutputPathText;
    private bool _isAnalyzing;
    private bool _isTechnicalDetailsModalOpen;
    private bool _isReplaceVideoConfirmationModalOpen;

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
        _conversionExecutionFeatureGate = new ConversionExecutionFeatureGate();

        SelectVideoCommand = new RelayCommand(SelectVideo);
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsAnalyzing);
        RefreshEngineStatusCommand = new RelayCommand(RefreshEngineStatus);
        OpenEngineFolderCommand = new RelayCommand(OpenEngineFolder);
        ClearLogsCommand = new RelayCommand(Logs.Clear);
        BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
        ResetOutputPathCommand = new RelayCommand(ResetOutputPath);
        StartConversionCommand = new RelayCommand(StartConversion, () => CanStartConversion);
        CancelConversionCommand = new RelayCommand(CancelConversion);
        ShowTechnicalDetailsCommand = new RelayCommand(ShowTechnicalDetails);
        CloseTechnicalDetailsCommand = new RelayCommand(CloseTechnicalDetails);
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
                    $"Output preset changed to {value.Name}.",
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
        new(ThreeDOutputFormat.HalfTopBottom, "Half Top-Bottom", "Medio Arriba-Abajo"),
        new(ThreeDOutputFormat.HalfSideBySide, "Half Side-by-Side", "Medio Lado a Lado"),
        new(ThreeDOutputFormat.FullSideBySide, "Full Side-by-Side", "Completo Lado a Lado"),
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
            if (_workflowState.SetSelectedSystemStatusTabIndex(value))
            {
                OnPropertyChanged();
            }
        }
    }

    public bool CanOpenSystemStatusConversionTab =>
        _workflowState.CanOpenSystemStatusConversionTab;

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

    public string OutputPathLabel => Text("Output path", "Ruta de salida");

    public string BrowseOutputFolderText => Text("Browse...", "Examinar...");

    public string ResetOutputPathText => Text("Reset", "Restablecer");

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
            : Text("Ready for conversion when execution is enabled.", "Listo para convertir cuando se habilite la ejecución.");

    public string ConversionPlanPresetText => Text(
        $"Based on preset: {SelectedOutputPreset.Name}",
        $"Basado en el perfil: {SelectedOutputPreset.SpanishName}");

    public string ConversionPlanOutputPathText => ConversionPlanLabelValue(
        "Output path",
        "Ruta de salida",
        _conversionPlan?.SuggestedOutputPath);

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

    public string ActiveModalTitleText => IsReplaceVideoConfirmationModalOpen
        ? ReplaceSelectedVideoTitleText
        : SystemStatusTechnicalDetailsTitle;

    public bool IsAnyModalOpen =>
        IsTechnicalDetailsModalOpen || IsReplaceVideoConfirmationModalOpen;

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

    public Visibility ReplaceVideoConfirmationModalContentVisibility =>
        IsReplaceVideoConfirmationModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public string TechnicalDetailsBodyText
    {
        get => _technicalDetailsBodyText;
        private set => SetProperty(ref _technicalDetailsBodyText, value);
    }

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
        _conversionExecutionFeatureGate.EvaluateStart(
            HasCompletedAnalysis,
            _conversionPlan is not null,
            _conversionReadiness).CanStart;

    public string StartConversionText => Text("Convert", "Convertir");

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
        "Selected output preset",
        "Perfil de salida seleccionado");

    public string OutputPresetLabel => Text("Output preset", "Perfil de salida");

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
        "MKV: advanced/master output",
        "MKV: salida avanzada/maestra");

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

    public RelayCommand ConfirmReplaceVideoCommand { get; }

    public RelayCommand CancelReplaceVideoCommand { get; }

    public async void SelectDroppedVideo(string path)
    {
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
        _dependencyHealth = _healthChecker.CheckDetailed(_toolPaths);
        _toolHealth = _dependencyHealth.Summary;
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
        if (IsReplaceVideoConfirmationModalOpen)
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

        _conversionReadiness = _conversionReadinessService.Evaluate(_dependencyHealth);
        RaiseConversionReadinessPropertiesChanged();
    }

    private ConversionExecutionStartGateResult EvaluateConversionStartGate() =>
        _conversionExecutionFeatureGate.EvaluateStart(
            HasCompletedAnalysis,
            _conversionPlan is not null,
            _conversionReadiness);

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
                $"Local iw3 bundle found under: {dependencyHealth.ExpectedPath}. Required: non-placeholder ENGINE_MANIFEST.json and iw3.py or iw3/__main__.py.",
                $"Bundle local de iw3 encontrado en: {dependencyHealth.ExpectedPath}. Requerido: ENGINE_MANIFEST.json que no sea marcador e iw3.py o iw3/__main__.py."),
            ToolHealthDetailKind.EngineDirectoryMissing => Text(
                $"Expected local iw3 engine folder: {dependencyHealth.ExpectedPath}. Required layout: ENGINE_MANIFEST.json, python/python.exe, iw3.py or iw3/__main__.py, and models.",
                $"Carpeta esperada del motor iw3 local: {dependencyHealth.ExpectedPath}. Estructura requerida: ENGINE_MANIFEST.json, python/python.exe, iw3.py o iw3/__main__.py y modelos."),
            ToolHealthDetailKind.EnginePlaceholderOnly => Text(
                $"Engine folder exists, but only placeholder or contract files were detected: {dependencyHealth.ExpectedPath}. Add a real iw3 bundle with a non-placeholder ENGINE_MANIFEST.json and iw3.py or iw3/__main__.py.",
                $"La carpeta del motor existe, pero solo contiene marcadores o archivos de contrato: {dependencyHealth.ExpectedPath}. Agrega un bundle real de iw3 con ENGINE_MANIFEST.json que no sea marcador e iw3.py o iw3/__main__.py."),
            ToolHealthDetailKind.EngineManifestMissing => Text(
                $"Engine content exists, but ENGINE_MANIFEST.json is missing or still has version=placeholder: {Path.Combine(dependencyHealth.ExpectedPath, "ENGINE_MANIFEST.json")}",
                $"Hay contenido del motor, pero ENGINE_MANIFEST.json falta o aÃºn tiene version=placeholder: {Path.Combine(dependencyHealth.ExpectedPath, "ENGINE_MANIFEST.json")}"),
            ToolHealthDetailKind.EngineEntryFilesMissing => Text(
                $"Engine manifest exists, but no supported iw3 entry file was found. Expected iw3.py or iw3/__main__.py under: {dependencyHealth.ExpectedPath}",
                $"El manifiesto del motor existe, pero no se encontrÃ³ un archivo de entrada iw3 compatible. Se esperaba iw3.py o iw3/__main__.py en: {dependencyHealth.ExpectedPath}"),
            ToolHealthDetailKind.ModelFilesFound => Text(
                $"Local 3D model files found under: {dependencyHealth.ExpectedPath}",
                $"Modelos 3D locales encontrados en: {dependencyHealth.ExpectedPath}"),
            ToolHealthDetailKind.ModelsDirectoryMissing => Text(
                $"Expected local 3D models folder: {dependencyHealth.ExpectedPath}",
                $"Carpeta esperada de modelos 3D locales: {dependencyHealth.ExpectedPath}"),
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
            Text(Iw3EngineBundleContract.EngineDirectoryRelativePath, Iw3EngineBundleContract.EngineDirectoryRelativePath),
            Text($"  {Path.GetFileName(Iw3EngineBundleContract.ManifestRelativePath)} (version must not be placeholder)",
                $"  {Path.GetFileName(Iw3EngineBundleContract.ManifestRelativePath)} (version no debe ser placeholder)"),
            Text("  python/python.exe", "  python/python.exe"),
            Text("  iw3.py or iw3/__main__.py", "  iw3.py o iw3/__main__.py"),
            Text("  models/*" + string.Join("|*", Iw3EngineBundleContract.SupportedModelExtensions),
                "  models/*" + string.Join("|*", Iw3EngineBundleContract.SupportedModelExtensions)),
            string.Empty,
            SystemStatusToolsTabTitle,
            string.Empty,
        };

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

    private void StartConversion()
    {
        var startGate = EvaluateConversionStartGate();
        if (!startGate.CanStart)
        {
            BlockConversionStart(startGate);
            return;
        }

        var disabledStartGate = ConversionExecutionStartGateResult.Blocked(
            ConversionExecutionBlocker.FeatureDisabled,
            "Conversion execution is not enabled yet.",
            "La ejecuci\u00f3n de conversi\u00f3n a\u00fan no est\u00e1 habilitada.",
            "The local execution runner is not connected in this build. No Python, iw3, or FFmpeg conversion process was started.",
            "El ejecutor local no est\u00e1 conectado en esta compilaci\u00f3n. No se inici\u00f3 ning\u00fan proceso de Python, iw3 ni conversi\u00f3n con FFmpeg.");
        BlockConversionStart(disabledStartGate);
    }

    private void BlockConversionStart(ConversionExecutionStartGateResult startGate)
    {
        _conversionExecutionState = ConversionExecutionState.Blocked(startGate);
        RaiseConversionExecutionPropertiesChanged();
        AddLog(startGate.EnglishLogMessage, startGate.SpanishLogMessage);
    }

    private void CancelConversion()
    {
        if (!CanCancelConversion)
        {
            AddLog(
                "There is no active conversion to cancel.",
                "No hay una conversión activa para cancelar.");
            return;
        }

        AddLog(
            "Conversion cancellation is not enabled yet.",
            "La cancelación de conversión aún no está habilitada.");
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
            _toolHealth ?? _healthChecker.Check(_toolPaths));
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
        ThreeDOutputFormat.HalfTopBottom => useSpanish ? "Medio Arriba-Abajo" : "Half Top-Bottom",
        ThreeDOutputFormat.HalfSideBySide => useSpanish ? "Medio Lado a Lado" : "Half Side-by-Side",
        ThreeDOutputFormat.FullSideBySide => useSpanish ? "Completo Lado a Lado" : "Full Side-by-Side",
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
    }

    private void RaiseConversionPlanPropertiesChanged()
    {
        OnPropertyChanged(nameof(ConversionPlanTitle));
        OnPropertyChanged(nameof(CanOpenConversionPlanTab));
        OnPropertyChanged(nameof(PlanOptionsTitle));
        OnPropertyChanged(nameof(OutputContainerOptionLabel));
        OnPropertyChanged(nameof(QualityOptionLabel));
        OnPropertyChanged(nameof(ThreeDIntensityOptionLabel));
        OnPropertyChanged(nameof(ThreeDOutputFormatOptionLabel));
        OnPropertyChanged(nameof(OutputLocationTitle));
        OnPropertyChanged(nameof(OutputPathLabel));
        OnPropertyChanged(nameof(BrowseOutputFolderText));
        OnPropertyChanged(nameof(ResetOutputPathText));
        OnPropertyChanged(nameof(ConversionPlanStatusText));
        OnPropertyChanged(nameof(ConversionPlanPresetText));
        OnPropertyChanged(nameof(ConversionPlanOutputPathText));
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
        OnPropertyChanged(nameof(CloseDialogText));
        OnPropertyChanged(nameof(CancelDialogText));
        OnPropertyChanged(nameof(ReplaceSelectedVideoTitleText));
        OnPropertyChanged(nameof(ReplaceSelectedVideoBodyText));
        OnPropertyChanged(nameof(ReplaceVideoConfirmText));
        OnPropertyChanged(nameof(ActiveModalTitleText));
        OnPropertyChanged(nameof(TechnicalDetailsBodyText));
        OnPropertyChanged(nameof(CanOpenSystemStatusConversionTab));
        OnPropertyChanged(nameof(SelectedSystemStatusTabIndex));
        OnPropertyChanged(nameof(ConversionReadinessEmptyText));
    }

    private void RaiseModalStatePropertiesChanged()
    {
        OnPropertyChanged(nameof(IsAnyModalOpen));
        OnPropertyChanged(nameof(ModalOverlayVisibility));
        OnPropertyChanged(nameof(ActiveModalTitleText));
        OnPropertyChanged(nameof(TechnicalDetailsModalContentVisibility));
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
