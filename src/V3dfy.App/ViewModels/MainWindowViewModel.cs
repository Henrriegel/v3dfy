using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using V3dfy.App.Mvvm;
using V3dfy.App.Services;
using V3dfy.Core.Models;
using V3dfy.Core.Presets;
using V3dfy.Infrastructure.Health;
using V3dfy.Infrastructure.Paths;

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
    private string? _selectedVideoPath;
    private string _selectedLanguage = "English";
    private string _selectedTheme = "Dark";
    private EngineHealthStatus? _toolHealth;

    public MainWindowViewModel()
    {
        _toolPaths = new InternalToolPathResolver(AppContext.BaseDirectory).Resolve();
        _healthChecker = new InternalToolsHealthChecker();
        _themeService = new AppThemeService();

        SelectVideoCommand = new RelayCommand(SelectVideo);
        AnalyzeCommand = new RelayCommand(Analyze);
        RefreshEngineStatusCommand = new RelayCommand(RefreshEngineStatus);
        ClearLogsCommand = new RelayCommand(Logs.Clear);

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
            }
        }
    }

    public string SelectedVideoDisplayPath => SelectedVideoPath ?? NoVideoSelectedText;

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

    public string ToolStatusTitle => Text(
        "Internal tool status",
        "Estado de herramientas internas");

    public string RefreshText => Text("Refresh", "Actualizar");

    public string ActivityLogTitle => Text("Activity log", "Registro de actividad");

    public string ClearText => Text("Clear", "Limpiar");

    public string RecommendedPresetTitle => Text(
        "Recommended TV preset",
        "Perfil recomendado para TV");

    public string PresetName => TargetDevicePresets.Lg3dFullHd2012.Name;

    public string PresetContainerText => Text("Container", "Contenedor") +
        $": {TargetDevicePresets.Lg3dFullHd2012.Recommendation.OutputContainer}";

    public string PresetVideoCodecText => Text("Codec", "Códec") +
        $": {TargetDevicePresets.Lg3dFullHd2012.Recommendation.VideoCodec}";

    public string PresetAudioCodecText => Text("Audio", "Audio") +
        $": {TargetDevicePresets.Lg3dFullHd2012.Recommendation.AudioCodec}";

    public string PresetResolutionText => Text("Resolution", "Resolución") +
        $": {TargetDevicePresets.Lg3dFullHd2012.Recommendation.Width}x" +
        $"{TargetDevicePresets.Lg3dFullHd2012.Recommendation.Height}";

    public string PresetThreeDLayoutText => Text("3D layout", "Diseño 3D") +
        ": Half Top-Bottom";

    public string PresetAdvancedOutputText => Text(
        "MKV: advanced/master output",
        "MKV: salida avanzada/maestra");

    public string TvPlaybackTitle => Text(
        "How to watch on the TV",
        "Cómo verlo en la TV");

    public string TvPlaybackInstructions => Text(
        """
        1. Copy the converted video to a USB drive or play it from your media player/server.
        2. Open the video on the LG 3D TV.
        3. Enable the TV 3D mode.
        4. Select Top & Bottom mode.
        5. Use your passive 3D glasses.
        """,
        """
        1. Copia el video convertido a una USB o reprodúcelo desde tu servidor/reproductor.
        2. Abre el video en la TV LG 3D.
        3. Activa el modo 3D de la TV.
        4. Selecciona Top & Bottom / Arriba-Abajo.
        5. Usa tus lentes 3D pasivos.
        """);

    public ObservableCollection<ToolStatusItemViewModel> ToolStatuses { get; } = [];

    public ObservableCollection<LogEntryViewModel> Logs { get; } = [];

    public RelayCommand SelectVideoCommand { get; }

    public RelayCommand AnalyzeCommand { get; }

    public RelayCommand RefreshEngineStatusCommand { get; }

    public RelayCommand ClearLogsCommand { get; }

    public void SelectDroppedVideo(string path)
    {
        if (!IsSupportedVideoFile(path))
        {
            AddLog(
                "The dropped file is not a supported video format.",
                "El archivo arrastrado no tiene un formato de video compatible.");
            return;
        }

        SetSelectedVideo(path);
    }

    private bool IsSpanish =>
        string.Equals(SelectedLanguage, "Español", StringComparison.Ordinal);

    private void SelectVideo()
    {
        var dialog = new OpenFileDialog
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
            SetSelectedVideo(dialog.FileName);
        }
    }

    private void Analyze()
    {
        if (string.IsNullOrWhiteSpace(SelectedVideoPath))
        {
            AddLog(
                "Select a video before starting analysis.",
                "Selecciona un video antes de iniciar el análisis.");
            return;
        }

        RefreshEngineStatus();
        if (_toolHealth?.Ffprobe == ToolHealthStatus.Missing)
        {
            AddLog(
                "Bundled FFprobe is not available yet. Analysis will be enabled " +
                "after tools/ffmpeg/win-x64/ffprobe.exe is bundled.",
                "FFprobe incluido aún no está disponible. El análisis se habilitará " +
                "cuando se incluya tools/ffmpeg/win-x64/ffprobe.exe.");
            return;
        }

        AddLog(
            "Bundled FFprobe detected. Video analysis integration is the next step.",
            "FFprobe incluido detectado. La integración del análisis de video es el siguiente paso.");
    }

    private void RefreshEngineStatus()
    {
        _toolHealth = _healthChecker.Check(_toolPaths);
        UpdateToolStatuses();
        AddLog(
            "Internal tool status refreshed.",
            "Estado de herramientas internas actualizado.");
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

    private void SetSelectedVideo(string path)
    {
        SelectedVideoPath = path;
        AddLog($"Selected video: {path}", $"Video seleccionado: {path}");
    }

    private static bool IsSupportedVideoFile(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        SupportedVideoExtensions.Contains(Path.GetExtension(path));

    private string Text(string english, string spanish) => IsSpanish ? spanish : english;

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
        OnPropertyChanged(nameof(ToolStatusTitle));
        OnPropertyChanged(nameof(RefreshText));
        OnPropertyChanged(nameof(ActivityLogTitle));
        OnPropertyChanged(nameof(ClearText));
        OnPropertyChanged(nameof(RecommendedPresetTitle));
        OnPropertyChanged(nameof(PresetContainerText));
        OnPropertyChanged(nameof(PresetVideoCodecText));
        OnPropertyChanged(nameof(PresetAudioCodecText));
        OnPropertyChanged(nameof(PresetResolutionText));
        OnPropertyChanged(nameof(PresetThreeDLayoutText));
        OnPropertyChanged(nameof(PresetAdvancedOutputText));
        OnPropertyChanged(nameof(TvPlaybackTitle));
        OnPropertyChanged(nameof(TvPlaybackInstructions));
    }
}
