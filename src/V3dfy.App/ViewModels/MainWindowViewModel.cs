using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using V3dfy.App.Mvvm;
using V3dfy.App.Services;
using V3dfy.Core.Analysis;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Core.Recommendations;
using V3dfy.Core.Readiness;
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

    private readonly InternalToolPaths _toolPaths;
    private readonly InternalToolsHealthChecker _healthChecker;
    private readonly AppThemeService _themeService;
    private readonly IFfprobeVideoAnalysisService _videoAnalysisService;
    private readonly VideoConversionRecommendationService _recommendationService;
    private readonly VideoConversionPlanService _conversionPlanService;
    private readonly ConversionReadinessService _conversionReadinessService;
    private string? _selectedVideoPath;
    private string _selectedLanguage = "English";
    private string _selectedTheme = "Dark";
    private EngineHealthStatus? _toolHealth;
    private VideoAnalysisResult? _analysis;
    private VideoConversionSetupRecommendation? _conversionRecommendation;
    private VideoConversionPlan? _conversionPlan;
    private ConversionReadiness? _conversionReadiness;
    private TargetDevicePreset _selectedOutputPreset = TargetDevicePresets.General3dVideo;
    private string? _customOutputPath;
    private string _outputPathText = string.Empty;
    private OutputContainer _selectedOutputContainer = OutputContainer.MP4;
    private AiQualityPreset _selectedQualityPreset = AiQualityPreset.Balanced;
    private ThreeDIntensity _selectedThreeDIntensity = ThreeDIntensity.Medium;
    private ThreeDOutputFormat _selectedThreeDOutputFormat = ThreeDOutputFormat.HalfTopBottom;
    private bool _hasCustomizedPlanOptions;
    private bool _isUpdatingOutputPathText;
    private int _selectedWorkflowTabIndex;
    private bool _hasCompletedAnalysis;
    private bool _isAnalyzing;

    public MainWindowViewModel()
    {
        _toolPaths = new InternalToolPathResolver(AppContext.BaseDirectory).Resolve();
        _healthChecker = new InternalToolsHealthChecker();
        _themeService = new AppThemeService();
        _videoAnalysisService = new FfprobeVideoAnalysisService(
            _toolPaths,
            new LocalProcessRunner(),
            new FfprobeJsonParser());
        _recommendationService = new VideoConversionRecommendationService();
        _conversionPlanService = new VideoConversionPlanService();
        _conversionReadinessService = new ConversionReadinessService();

        SelectVideoCommand = new RelayCommand(SelectVideo);
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsAnalyzing);
        RefreshEngineStatusCommand = new RelayCommand(RefreshEngineStatus);
        ClearLogsCommand = new RelayCommand(Logs.Clear);
        BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
        ResetOutputPathCommand = new RelayCommand(ResetOutputPath);
        StartConversionCommand = new RelayCommand(StartConversion, () => CanStartConversion);

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
        get => _hasCompletedAnalysis;
        private set
        {
            if (SetProperty(ref _hasCompletedAnalysis, value))
            {
                if (!value)
                {
                    SelectedWorkflowTabIndex = 0;
                }

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
        get => _selectedOutputContainer;
        set
        {
            if (SetProperty(ref _selectedOutputContainer, value))
            {
                PlanOptionChanged(
                    $"Output container changed to {value}.",
                    $"Contenedor de salida cambiado a {value}.");
            }
        }
    }

    public AiQualityPreset SelectedQualityPreset
    {
        get => _selectedQualityPreset;
        set
        {
            if (SetProperty(ref _selectedQualityPreset, value))
            {
                PlanOptionChanged(
                    $"Quality changed to {QualityPresetText(value, useSpanish: false)}.",
                    $"Calidad cambiada a {QualityPresetText(value, useSpanish: true)}.");
            }
        }
    }

    public ThreeDIntensity SelectedThreeDIntensity
    {
        get => _selectedThreeDIntensity;
        set
        {
            if (SetProperty(ref _selectedThreeDIntensity, value))
            {
                PlanOptionChanged(
                    $"3D intensity changed to {ThreeDIntensityText(value, useSpanish: false)}.",
                    $"Intensidad 3D cambiada a {ThreeDIntensityText(value, useSpanish: true)}.");
            }
        }
    }

    public ThreeDOutputFormat SelectedThreeDOutputFormat
    {
        get => _selectedThreeDOutputFormat;
        set
        {
            if (SetProperty(ref _selectedThreeDOutputFormat, value))
            {
                PlanOptionChanged(
                    $"3D layout changed to {ThreeDOutputFormatText(value, useSpanish: false)}.",
                    $"Diseño 3D cambiado a {ThreeDOutputFormatText(value, useSpanish: true)}.");
            }
        }
    }

    public int SelectedWorkflowTabIndex
    {
        get => _selectedWorkflowTabIndex;
        set => SetProperty(ref _selectedWorkflowTabIndex, value);
    }

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

    public bool CanOpenRecommendedSetupTab => HasCompletedAnalysis;

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

    public bool CanOpenConversionPlanTab => HasCompletedAnalysis;

    public string PlanOptionsTitle => Text("Plan options", "Opciones del plan");

    public string OutputContainerOptionLabel => Text("Output container", "Contenedor de salida");

    public string QualityOptionLabel => Text("Quality", "Calidad");

    public string ThreeDIntensityOptionLabel => Text("3D intensity", "Intensidad 3D");

    public string ThreeDOutputFormatOptionLabel => Text("3D layout", "Diseño 3D");

    public string OutputLocationTitle => Text("Output location", "Ubicación de salida");

    public string OutputPathLabel => Text("Output path", "Ruta de salida");

    public string BrowseOutputFolderText => Text("Browse...", "Examinar...");

    public string ResetOutputPathText => Text("Reset", "Restablecer");

    public bool HasCustomOutputPath => !string.IsNullOrWhiteSpace(_customOutputPath);

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

    public string ConversionReadinessTitle => Text(
        "Conversion readiness",
        "Estado de conversión");

    public Visibility ConversionReadinessVisibility =>
        HasCompletedAnalysis ? Visibility.Visible : Visibility.Collapsed;

    public string ConversionReadinessStatusLabel => Text("Status", "Estado");

    public string ConversionReadinessMissingRequirementsTitle => Text(
        "Missing requirements",
        "Requisitos faltantes");

    public string ConversionReadinessStatusText => Text(
        _conversionReadiness?.EnglishStatus ??
        "Conversion unavailable. Required local components are missing.",
        _conversionReadiness?.SpanishStatus ??
        "Conversión no disponible. Faltan componentes locales requeridos.");

    public string ConversionReadinessIssuesText => _conversionReadiness is null
        ? "-"
        : _conversionReadiness.Issues.Count == 0
            ? Text("No missing requirements.", "No hay requisitos faltantes.")
            : string.Join(
                Environment.NewLine,
                _conversionReadiness.Issues.Select(issue =>
                    $"- {Text(issue.EnglishMessage, issue.SpanishMessage)}"));

    public string ConversionReadinessRequiredComponentsText => _conversionReadiness is null
        ? string.Empty
        : Text(
            _conversionReadiness.EnglishRequiredComponentsSummary,
            _conversionReadiness.SpanishRequiredComponentsSummary);

    public string ConversionBlockedReasonText => CanStartConversion
        ? string.Empty
        : _conversionPlan is null
            ? Text(
                "Prepare a conversion plan before converting.",
                "Prepara un plan de conversión antes de convertir.")
            : Text(
                "This button will become available after the local engine, embedded runtime and models are bundled.",
                "Este botón estará disponible cuando se incluyan el motor local, el runtime embebido y los modelos.");

    public bool CanStartConversion =>
        _conversionPlan is not null &&
        _conversionReadiness?.CanConvert == true;

    public string StartConversionText => Text("Convert", "Convertir");

    public string ToolStatusTitle => Text(
        "Internal tool status",
        "Estado de herramientas internas");

    public string RefreshText => Text("Refresh", "Actualizar");

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

    public RelayCommand ClearLogsCommand { get; }

    public RelayCommand BrowseOutputFolderCommand { get; }

    public RelayCommand ResetOutputPathCommand { get; }

    public RelayCommand StartConversionCommand { get; }

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
        if (replacingVideo && !ConfirmReplaceSelectedVideo())
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

    private bool ConfirmReplaceSelectedVideo()
    {
        var result = System.Windows.MessageBox.Show(
            Text(
                "Selecting a new video will clear the current analysis, recommended setup, conversion plan, selected options and custom output path. Continue?",
                "Seleccionar un nuevo video borrará el análisis actual, la configuración recomendada, el plan de conversión, las opciones seleccionadas y la ruta de salida personalizada. ¿Continuar?"),
            Text(
                "Replace selected video?",
                "¿Reemplazar video seleccionado?"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
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
        _toolHealth = _healthChecker.Check(_toolPaths);
        UpdateToolStatuses();
        UpdateConversionReadiness();
        AddLog(
            "Internal tool status refreshed.",
            "Estado de herramientas internas actualizado.");
    }

    private void UpdateConversionReadiness()
    {
        if (_toolHealth is null)
        {
            return;
        }

        _conversionReadiness = _conversionReadinessService.Evaluate(_toolHealth);
        RaiseConversionReadinessPropertiesChanged();
    }

    private void UpdateToolStatuses()
    {
        if (_toolHealth is null)
        {
            return;
        }

        ToolStatuses.Clear();
        ToolStatuses.Add(CreateToolStatus("FFmpeg", "FFmpeg", _toolHealth.Ffmpeg));
        ToolStatuses.Add(CreateToolStatus("FFprobe", "FFprobe", _toolHealth.Ffprobe));
        ToolStatuses.Add(CreateToolStatus("Python", "Python", _toolHealth.Python));
        ToolStatuses.Add(CreateToolStatus("iw3 engine", "Motor iw3", _toolHealth.Iw3EngineDirectory));
        ToolStatuses.Add(CreateToolStatus("models", "modelos", _toolHealth.ModelsDirectory));
    }

    private ToolStatusItemViewModel CreateToolStatus(
        string englishName,
        string spanishName,
        ToolHealthStatus status) => new(
        Name: Text(englishName, spanishName),
        StatusText: status == ToolHealthStatus.Found
            ? Text("Found", "Encontrado")
            : Text("Missing", "Faltante"));

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
        HasCompletedAnalysis = false;
        _analysis = null;
        _conversionRecommendation = null;
        _conversionPlan = null;

        if (clearOutputPath)
        {
            _customOutputPath = null;
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
        if (!CanStartConversion)
        {
            AddLog(
                "Conversion cannot start because required local components are missing.",
                "La conversión no puede iniciar porque faltan componentes locales requeridos.");
            return;
        }

        AddLog(
            "Conversion execution is not enabled yet.",
            "La ejecución de conversión aún no está habilitada.");
    }

    private static bool IsSupportedVideoFile(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        SupportedVideoExtensions.Contains(Path.GetExtension(path));

    private void PlanOptionChanged(string englishMessage, string spanishMessage)
    {
        _hasCustomizedPlanOptions = true;

        if (RegenerateConversionPlan())
        {
            AddLog(englishMessage, spanishMessage);
        }
    }

    private void CommitOutputPath(string value)
    {
        var normalizedPath = string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

        if (string.Equals(_customOutputPath, normalizedPath, StringComparison.Ordinal))
        {
            return;
        }

        _customOutputPath = normalizedPath;
        OnPropertyChanged(nameof(HasCustomOutputPath));

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
        if (_customOutputPath is null)
        {
            return;
        }

        _customOutputPath = null;
        OnPropertyChanged(nameof(HasCustomOutputPath));

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
        if (string.Equals(_customOutputPath, outputPath, StringComparison.Ordinal))
        {
            return;
        }

        _customOutputPath = outputPath;
        OnPropertyChanged(nameof(HasCustomOutputPath));
        SetOutputPathText(outputPath);

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
        var outputPath = string.IsNullOrWhiteSpace(_customOutputPath)
            ? GetAutomaticOutputPath()
            : _customOutputPath;

        return string.IsNullOrWhiteSpace(outputPath)
            ? null
            : Path.GetDirectoryName(outputPath);
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
        if (_hasCustomizedPlanOptions)
        {
            return;
        }

        SetProperty(
            ref _selectedOutputContainer,
            recommendation.OutputContainer,
            nameof(SelectedOutputContainer));
        SetProperty(
            ref _selectedQualityPreset,
            recommendation.QualityPreset,
            nameof(SelectedQualityPreset));
        SetProperty(
            ref _selectedThreeDIntensity,
            recommendation.Intensity,
            nameof(SelectedThreeDIntensity));
        SetProperty(
            ref _selectedThreeDOutputFormat,
            recommendation.ThreeDOutputFormat,
            nameof(SelectedThreeDOutputFormat));
    }

    private void ApplyPresetDefaults(TargetDevicePreset preset)
    {
        var recommendation = preset.Recommendation;
        _hasCustomizedPlanOptions = false;
        SetProperty(ref _selectedOutputContainer, recommendation.OutputContainer, nameof(SelectedOutputContainer));
        SetProperty(ref _selectedQualityPreset, AiQualityPreset.Balanced, nameof(SelectedQualityPreset));
        SetProperty(ref _selectedThreeDIntensity, ThreeDIntensity.Medium, nameof(SelectedThreeDIntensity));
        SetProperty(ref _selectedThreeDOutputFormat, recommendation.ThreeDOutputFormat, nameof(SelectedThreeDOutputFormat));
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
            new VideoConversionPlanOptions(
                SelectedOutputContainer,
                SelectedQualityPreset,
                SelectedThreeDIntensity,
                SelectedThreeDOutputFormat,
                // Manual paths are preserved exactly across option changes.
                // Use Reset to return to automatic suffix and extension naming.
                _customOutputPath),
            _toolPaths,
            _toolHealth ?? _healthChecker.Check(_toolPaths));
        RaiseConversionPlanPropertiesChanged();
        SetOutputPathText(_conversionPlan.SuggestedOutputPath);
        RaiseConversionReadinessPropertiesChanged();
        return true;
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
        RaiseConversionReadinessPropertiesChanged();
        OnPropertyChanged(nameof(ToolStatusTitle));
        OnPropertyChanged(nameof(RefreshText));
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
        RaiseConversionReadinessPropertiesChanged();
    }

    private void RaiseWorkflowAvailabilityPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasCompletedAnalysis));
        OnPropertyChanged(nameof(CanOpenRecommendedSetupTab));
        OnPropertyChanged(nameof(CanOpenConversionPlanTab));
    }

    private void RaiseConversionReadinessPropertiesChanged()
    {
        OnPropertyChanged(nameof(ConversionReadinessTitle));
        OnPropertyChanged(nameof(ConversionReadinessVisibility));
        OnPropertyChanged(nameof(ConversionReadinessStatusLabel));
        OnPropertyChanged(nameof(ConversionReadinessMissingRequirementsTitle));
        OnPropertyChanged(nameof(ConversionReadinessStatusText));
        OnPropertyChanged(nameof(ConversionReadinessIssuesText));
        OnPropertyChanged(nameof(ConversionReadinessRequiredComponentsText));
        OnPropertyChanged(nameof(ConversionBlockedReasonText));
        OnPropertyChanged(nameof(CanStartConversion));
        OnPropertyChanged(nameof(StartConversionText));
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
