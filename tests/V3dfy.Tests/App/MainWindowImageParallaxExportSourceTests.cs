namespace V3dfy.Tests.App;

public sealed class MainWindowImageParallaxExportSourceTests
{
    [Fact]
    public void ImageParallaxConversion_UsesRealIw3DepthExporterAndGenericConvertCommand()
    {
        var viewModel = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var exporter = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Execution", "Iw3ImageParallaxExporter.cs");

        Assert.Contains("private readonly IImageParallaxExporter _imageParallaxExporter;", viewModel);
        Assert.Contains("_imageParallaxExporter = new Iw3ImageParallaxExporter(", viewModel);
        Assert.Contains("ConvertImageCommand = new AsyncRelayCommand(", viewModel);
        Assert.Contains("private Task ConvertImageAsync()", viewModel);
        Assert.Contains("private async Task ConvertParallaxImageAsync()", viewModel);
        Assert.Contains("public sealed class Iw3ImageParallaxExporter : IImageParallaxExporter", exporter);
        Assert.Contains("Iw3CliContract.ExportSwitch", exporter);
        Assert.Contains("Iw3CliContract.ExportDepthOnlySwitch", exporter);
        Assert.Contains("Iw3CliContract.ExportDepthFitSwitch", exporter);
        Assert.DoesNotContain("LocalImageStereoExporter", viewModel);
        Assert.DoesNotContain("Basic non-depth", viewModel);
    }

    [Fact]
    public void ImageParallaxConversion_UsesBundledResourcesOnly()
    {
        var exporter = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Execution", "Iw3ImageParallaxExporter.cs");
        var depthBuilder = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Commands", "Iw3ImageDepthExportCommandBuilder.cs");
        var frameBuilder = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Commands", "V3dfyParallaxFrameCommandBuilder.cs");
        var ffmpegBuilder = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Commands", "FfmpegParallaxVideoCommandBuilder.cs");

        Assert.Contains("request.ExpectedToolPaths.PythonExecutable", exporter);
        Assert.Contains("request.ExpectedToolPaths.FfmpegExecutable", exporter);
        Assert.Contains("request.ExpectedToolPaths.V3dfyParallaxHelperScript", exporter);
        Assert.Contains("Iw3BundledRuntimeEnvironment.Create(request.ExpectedToolPaths)", exporter);
        Assert.Contains("AllowedRootDirectory: request.ExpectedToolPaths.Iw3EngineDirectory", exporter);
        Assert.Contains("AllowedRootDirectory: Path.GetDirectoryName(request.ExpectedToolPaths.FfmpegExecutable)", exporter);
        Assert.Contains("request.ExpectedToolPaths.PythonExecutable", depthBuilder);
        Assert.Contains("request.ExpectedToolPaths.PythonExecutable", frameBuilder);
        Assert.Contains("request.ExpectedToolPaths.FfmpegExecutable", ffmpegBuilder);
        Assert.DoesNotContain("C:\\v3dfy-iw3-intake", exporter, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Environment.GetEnvironmentVariable", exporter);
        Assert.DoesNotContain("UseShellExecute", exporter);
        Assert.DoesNotContain("pip", exporter, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("download", exporter, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImageParallaxStep2_HasModelReadinessOutputAndNoFakePreviewButton()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var parallaxSetup = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageParallaxScaffold\"",
            "AutomationProperties.AutomationId=\"ImageStereoScaffold\"");

        Assert.DoesNotContain("Source=\"{Binding SelectedImagePath}\"", parallaxSetup);
        Assert.DoesNotContain("Text=\"{Binding ImageDepthMapGenerationText}\"", parallaxSetup);
        Assert.DoesNotContain("Text=\"{Binding ImageSourcePanelTitleText}\"", parallaxSetup);
        Assert.DoesNotContain("Text=\"{Binding ImageDepthPanelTitleText}\"", parallaxSetup);
        Assert.DoesNotContain("Text=\"{Binding ImageParallaxPreviewTitleText}\"", parallaxSetup);
        Assert.DoesNotContain("Source=\"{Binding ImageParallaxPreviewImagePath}\"", parallaxSetup);
        Assert.Contains("Text=\"{Binding ImageParameterPanelTitleText}\"", parallaxSetup);
        Assert.Contains("ItemsSource=\"{Binding ParallaxDepthIntensityOptions}\"", parallaxSetup);
        Assert.Contains("ItemsSource=\"{Binding ParallaxMotionDirectionOptions}\"", parallaxSetup);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxModelSelector\"", parallaxSetup);
        Assert.Contains("ItemsSource=\"{Binding ImageParallaxLocalModelCandidates}\"", parallaxSetup);
        Assert.Contains("IsEnabled=\"{Binding ImageParallaxModelSelectorEnabled}\"", parallaxSetup);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxModelHelpButton\"", parallaxSetup);
        Assert.Contains("Command=\"{Binding ShowImageParallaxModelHelpCommand}\"", parallaxSetup);
        Assert.Contains("ToolTip=\"{Binding ImageParallaxModelHelpButtonToolTipText}\"", parallaxSetup);
        Assert.Contains("Content=\"?\"", parallaxSetup);
        Assert.Contains("Text=\"{Binding ImageExpectedOutputFileText}\"", parallaxSetup);
        Assert.Contains("Text=\"{Binding ImageSaveLocationText}\"", parallaxSetup);
        Assert.Contains("Text=\"{Binding ImageParallaxQualityGuidanceText}\"", parallaxSetup);
        Assert.Contains("Text=\"{Binding ImageParallaxModelGuidanceText}\"", parallaxSetup);
        Assert.Contains("Text=\"{Binding ImageNotImplementedStateText}\"", parallaxSetup);
        Assert.DoesNotContain("IsEnabled=\"False\"", ExtractSourceRange(
            parallaxSetup,
            "AutomationProperties.AutomationId=\"ImageParallaxScaffold\"",
            "</Grid>"));
        Assert.DoesNotContain("Preview scaffold", parallaxSetup);
        Assert.DoesNotContain("Export scaffold", parallaxSetup);
    }

    [Fact]
    public void ImageParallaxStep3_LeftSideIsVisualOnlyAndOutputPanelOwnsConvertActions()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var parallaxResult = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"",
            "AutomationProperties.AutomationId=\"ImageStereoResultScaffold\"");
        var rightOutputPanel = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageOutputPanelCard\"",
            "x:Name=\"ImageActivityLogCard\"");

        Assert.Contains("Source=\"{Binding ImageParallaxPreviewImagePath}\"", parallaxResult);
        Assert.Contains("Visibility=\"{Binding ImageParallaxSourcePreviewVisibility}\"", parallaxResult);
        Assert.Contains("Source=\"{Binding ImageParallaxVideoMediaSource}\"", parallaxResult);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxGeneratedVideoPreview\"", parallaxResult);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxGeneratedVideoControls\"", parallaxResult);
        Assert.Contains("ImageParallaxVerticalPreviewPlayPauseButton", parallaxResult);
        Assert.Contains("ImageParallaxVerticalPreviewTimelineSlider", parallaxResult);
        Assert.Contains("ImageParallaxVerticalPreviewTimeText", parallaxResult);
        Assert.Contains("ImageParallaxVerticalPreviewVolumeSlider", parallaxResult);
        Assert.Contains("ImageParallaxVerticalPreviewMuteToggleButton", parallaxResult);
        Assert.Contains("ImageParallaxWidePreviewPlayPauseButton", parallaxResult);
        Assert.Contains("ImageParallaxWidePreviewTimelineSlider", parallaxResult);
        Assert.Contains("ImageParallaxWidePreviewTimeText", parallaxResult);
        Assert.Contains("ImageParallaxWidePreviewVolumeSlider", parallaxResult);
        Assert.Contains("ImageParallaxWidePreviewMuteToggleButton", parallaxResult);
        Assert.Contains("PreviewMouseLeftButtonDown=\"OnImageParallaxPreviewVolumeSliderPreviewMouseLeftButtonDown\"", parallaxResult);
        Assert.Contains("ValueChanged=\"OnImageParallaxPreviewVolumeValueChanged\"", parallaxResult);
        Assert.Contains("Visibility=\"{Binding ImageExportProgressVisibility}\"", parallaxResult);
        Assert.DoesNotContain("ImageParallaxVideoMaximizeButton", parallaxResult);
        Assert.DoesNotContain("ToggleImageParallaxVideoMaximizeCommand", parallaxResult);
        Assert.DoesNotContain("ImageParallaxVideoMaximizeGlyphText", parallaxResult);
        Assert.DoesNotContain("Command=\"{Binding ConvertImageCommand}\"", parallaxResult);
        Assert.DoesNotContain("OpenImageOutputFolderButton", parallaxResult);
        Assert.DoesNotContain("NewImageConversionButton", parallaxResult);
        Assert.DoesNotContain("ImageParallaxWideExportOptionsSummaryRow", parallaxResult);
        Assert.Contains("AutomationProperties.AutomationId=\"ConvertImageButton\"", rightOutputPanel);
        Assert.Contains("Command=\"{Binding ConvertImageCommand}\"", rightOutputPanel);
        Assert.Contains("Content=\"{Binding ImageConvertActionText}\"", rightOutputPanel);
        Assert.Contains("IsEnabled=\"{Binding CanConvertImage}\"", rightOutputPanel);
        Assert.Contains("Visibility=\"{Binding ImageConvertButtonVisibility}\"", rightOutputPanel);
        Assert.Contains("Visibility=\"{Binding ImageOpenOutputFolderButtonVisibility}\"", rightOutputPanel);
        Assert.Contains("Visibility=\"{Binding ImageNewConversionButtonVisibility}\"", rightOutputPanel);
        Assert.Contains("Text=\"{Binding ImageConvertDisabledReasonText}\"", rightOutputPanel);
    }

    [Fact]
    public void ImageParallaxOutputPathAndLifecycleAreWired()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("ImageParallaxExportPathBuilder.CreateOutputPaths", source);
        Assert.Contains("CreateExpectedImageParallaxOutputPath()", source);
        Assert.Contains("CreateImageParallaxExportReadinessText()", source);
        Assert.Contains("ImageParallaxExportReadinessCanExport", source);
        Assert.Contains("ApplyImageSetupChanged(", source);
        Assert.Contains("SelectedParallaxDepthIntensity", source);
        Assert.Contains("SelectedParallaxMotionDirection", source);
        Assert.Contains("SelectedParallaxZoomAmplitude", source);
        Assert.Contains("SelectedParallaxDuration", source);
        Assert.Contains("SelectedParallaxSmoothing", source);
        Assert.Contains("SelectedParallaxLayerBehavior", source);
        Assert.Contains("IsImageExportRunning || HasCurrentImageConversionOutput ? Visibility.Collapsed : Visibility.Visible", source);
        Assert.Contains("public Visibility ImageOpenOutputFolderButtonVisibility", source);
        Assert.Contains("public Visibility ImageNewConversionButtonVisibility", source);
        Assert.Contains("IsImageExportRunning ? Visibility.Collapsed : Visibility.Visible", source);
    }

    [Fact]
    public void ImageParallaxSuccessState_ExposesGeneratedMp4PreviewAndClearsOnInvalidation()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("public bool IsImageParallaxVideoPreviewAvailable", source);
        Assert.Contains("Path.GetExtension(_lastImageExportPrimaryPath)", source);
        Assert.Contains("\".mp4\"", source);
        Assert.Contains("public Uri? ImageParallaxVideoMediaSource", source);
        Assert.Contains("public Visibility ImageParallaxVideoPreviewVisibility", source);
        Assert.Contains("public Visibility ImageParallaxSourcePreviewVisibility", source);
        Assert.DoesNotContain("ToggleImageParallaxVideoMaximizeCommand", source);
        Assert.DoesNotContain("ImageParallaxVideoMaximizeGlyphText", source);
        Assert.DoesNotContain("ImageParallaxVideoMaximizeButtonText", source);
        Assert.DoesNotContain("_isImageParallaxVideoPreviewMaximized", source);
    }

    [Fact]
    public void ImageParallaxRunningState_LocksShellWorkflowAndSetupControls()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var normalizedSource = source.Replace("\r\n", "\n");
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var imageSection = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageConversionSection\"",
            "AutomationProperties.AutomationId=\"VideoConversionSection\"");

        Assert.Contains("public bool CanUseShellNavigation =>\n        !IsAnyModalOpen &&\n        !IsImageExportRunning;", normalizedSource);
        Assert.Contains("public bool ShellToolTipsEnabled => !IsAnyModalOpen;", source);
        Assert.Contains("public bool CanInteractWithImageWorkflow =>\n        !IsAnyModalOpen &&\n        !IsImageExportRunning;", normalizedSource);
        Assert.Contains("public bool ImageSetupControlsEnabled =>\n        !IsAnyModalOpen &&\n        !IsImageExportRunning;", normalizedSource);
        Assert.Contains("public bool ImageWorkflowCardsEnabled =>\n        !IsAnyModalOpen &&\n        !IsImageExportRunning;", normalizedSource);
        Assert.Contains("public bool CanUseImageStepNavigation =>\n        !IsAnyModalOpen &&\n        !IsImageExportRunning;", normalizedSource);
        Assert.Contains("ToggleSidebarCommand = new RelayCommand(ToggleSidebar, () => CanUseShellNavigation);", source);
        Assert.Contains("OpenSettingsCommand = new RelayCommand(OpenSettings, () => CanOpenSettings);", source);
        Assert.Contains("SelectImageCommand = new RelayCommand(SelectImage, () => CanInteractWithImageWorkflow);", source);
        Assert.Contains("if (!CanInteractWithImageWorkflow)", source);
        Assert.Contains("if (!CanUseImageStepNavigation)", source);
        Assert.Contains("IsEnabled=\"{Binding CanUseShellNavigation}\"", xaml);
        Assert.Contains("IsEnabled=\"{Binding CanOpenSettings}\"", xaml);
        Assert.Contains("IsEnabled=\"{Binding CanUseImageStepNavigation}\"", imageSection);
        Assert.Contains("IsEnabled=\"{Binding CanInteractWithImageWorkflow}\"", imageSection);
        Assert.Contains("IsEnabled=\"{Binding ImageWorkflowCardsEnabled}\"", imageSection);
        Assert.Contains("IsEnabled=\"{Binding ImageSetupControlsEnabled}\"", imageSection);
        Assert.Contains("IsEnabled=\"{Binding ImageParallaxModelSelectorEnabled}\"", imageSection);
        Assert.Contains("IsEnabled=\"{Binding ImageModelSelectorEnabled}\"", imageSection);
        Assert.Contains("IsImageExportRunning ? Visibility.Collapsed : Visibility.Visible", source);
        Assert.Contains("ClearImageLogCommand = new RelayCommand(ClearImageLog, () => ImageLogs.Count > 0 && !IsImageExportRunning && !IsAnyModalOpen);", source);
        Assert.Contains("if (IsImageExportRunning)", source);
    }

    [Fact]
    public void ImageSetupChanges_LogSpecificOldAndNewValues()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("{englishLabel} changed: {previous} -> {field}.", source);
        Assert.Contains("\"Depth intensity\"", source);
        Assert.Contains("Motion direction", source);
        Assert.Contains("Zoom/amplitude", source);
        Assert.Contains("Duration", source);
        Assert.Contains("Smoothing", source);
        Assert.Contains("Layer/depth behavior", source);
        Assert.Contains("Stereo output format changed:", source);
        Assert.Contains("Eye separation", source);
        Assert.Contains("Convergence", source);
        Assert.Contains("Swap eyes changed:", source);
        Assert.Contains("Anaglyph mode changed:", source);
        Assert.Contains("Image workflow mode changed:", source);
        Assert.Contains("Model changed:", source);
        Assert.Contains("if (SetProperty(ref field, value, propertyName))", source);
        Assert.Contains("EnsureSentence(englishChange)", source);
        Assert.DoesNotContain("Image setup changed; previous image conversion output is outdated. Prepare conversion again.", source);
    }

    [Fact]
    public void ImageParallaxModelHelp_UsesOnlyImageCompatibleLocalCandidates()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var parallaxSetup = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageParallaxScaffold\"",
            "AutomationProperties.AutomationId=\"ImageStereoScaffold\"");

        Assert.Contains("public IReadOnlyList<LocalModelSelectionCandidate> ImageParallaxLocalModelCandidates", source);
        Assert.Contains(".Where(IsImageParallaxCompatibleCandidate)", source);
        Assert.Contains("Iw3DepthModelMediaCapability.ImageAndVideo or", source);
        Assert.Contains("Iw3DepthModelMediaCapability.ImageOnly", source);
        Assert.Contains("private IReadOnlyList<ModelHelpRow> CreateImageParallaxModelHelpRows()", source);
        Assert.Contains("var candidates = ImageParallaxLocalModelCandidates;", source);
        Assert.Contains("Compatible with image depth for 2.5D Parallax", source);
        Assert.Contains("ShowImageParallaxModelHelpCommand", source);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxModelHelpButton\"", parallaxSetup);
        Assert.Contains("ItemsSource=\"{Binding ImageParallaxLocalModelCandidates}\"", parallaxSetup);
        Assert.DoesNotContain("ItemsSource=\"{Binding LocalModelCandidates}\"", parallaxSetup);
    }

    [Fact]
    public void ImageWorkflowCards_RemovedInnerSelectedAndChooseButtons()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var setupStep = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageSetupStepContent\"",
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"");

        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxModeCard\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoModeCard\"", setupStep);
        Assert.Contains("Command=\"{Binding SelectImageParallaxModeCommand}\"", setupStep);
        Assert.Contains("Command=\"{Binding SelectImageStereoModeCommand}\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxModeSelectedMarker\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoModeSelectedMarker\"", setupStep);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageParallaxModeSelectedBadge\"", setupStep);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageStereoModeSelectedBadge\"", setupStep);
        Assert.DoesNotContain("Text=\"{Binding ImageParallaxModeCardStatusText}\"", setupStep);
        Assert.DoesNotContain("Text=\"{Binding ImageStereoModeCardStatusText}\"", setupStep);
        Assert.DoesNotContain("Content=\"{Binding ImageParallaxModeCardStatusText}\"", setupStep);
        Assert.DoesNotContain("Content=\"{Binding ImageStereoModeCardStatusText}\"", setupStep);
    }

    [Fact]
    public void ParallaxMotionParameters_ArePassedToHelperAndHaveDistinctMappings()
    {
        var request = ReadRepoFile("src", "V3dfy.Core", "Image", "ImageParallaxExportRequest.cs");
        var exporter = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Execution", "Iw3ImageParallaxExporter.cs");
        var commandBuilder = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Commands", "V3dfyParallaxFrameCommandBuilder.cs");
        var helper = ReadRepoFile("engine", "iw3", "v3dfy", "parallax2d.py");

        Assert.Contains("DepthIntensity", request);
        Assert.Contains("MotionDirection", request);
        Assert.Contains("ZoomAmplitude", request);
        Assert.Contains("Duration", request);
        Assert.Contains("Smoothing", request);
        Assert.Contains("LayerBehavior", request);
        Assert.Contains("request.DepthIntensity", commandBuilder);
        Assert.Contains("request.MotionDirection", commandBuilder);
        Assert.Contains("request.ZoomAmplitude", commandBuilder);
        Assert.Contains("request.Smoothing", commandBuilder);
        Assert.Contains("request.LayerBehavior", commandBuilder);
        Assert.Contains("ParseDurationSeconds(request.Duration)", exporter);
        Assert.Contains("def map_intensity", helper);
        Assert.Contains("base * 0.02", helper);
        Assert.Contains("base * 0.035", helper);
        Assert.Contains("base * 0.055", helper);
        Assert.Contains("def map_zoom", helper);
        Assert.Contains("return 0.02", helper);
        Assert.Contains("return 0.035", helper);
        Assert.Contains("return 0.055", helper);
        Assert.Contains("if normalized == \"right to left\"", helper);
        Assert.Contains("if normalized == \"orbit\"", helper);
        Assert.Contains("offset_y", helper);
        Assert.Contains("depth = smooth_depth_array(depth, smoothing)", helper);
        Assert.Contains("if \"depth slices\" in behavior", helper);
        Assert.Contains("elif \"foreground\" in behavior", helper);
    }

    [Fact]
    public void ImageParallaxDefaultsAndGuidanceAreConservative()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");

        Assert.Contains("private string _selectedParallaxDepthIntensity = \"Low\";", source);
        Assert.Contains("_selectedParallaxDepthIntensity = \"Low\";", source);
        Assert.Contains("Use Low or Medium depth and Subtle motion", source);
        Assert.Contains("SelectedLocalModelCandidate.DisplayName", source);
        Assert.Contains("Text=\"{Binding ImageParallaxQualityGuidanceText}\"", xaml);
        Assert.Contains("Text=\"{Binding ImageParallaxModelGuidanceText}\"", xaml);
        Assert.DoesNotContain("MiDaS", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("download", ExtractSourceRange(
            source,
            "public string ImageParallaxModelGuidanceText",
            "public string ImageDepthIntensityLabelText"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImageParallaxProgressLogsExposeDepthFramesAndFfmpegPhases()
    {
        var exporter = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Execution", "Iw3ImageParallaxExporter.cs");
        var viewModel = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("Starting bundled iw3 depth export", exporter);
        Assert.Contains("Expected depth output", exporter);
        Assert.Contains("Depth export completed in", exporter);
        Assert.Contains("Generating {frameCount} 2.5D parallax frames", exporter);
        Assert.Contains("Parallax frame directory", exporter);
        Assert.Contains("ReportFrameGenerationProgressAsync", exporter);
        Assert.Contains("Parallax frames: {boundedFrameCount} / {expectedFrameCount}", exporter);
        Assert.Contains("expectedFrameCount / 12", exporter);
        Assert.Contains("Directory.EnumerateFiles(framesDirectory, \"frame_*.png\")", exporter);
        Assert.Contains("Parallax frame generation completed", exporter);
        Assert.Contains("Starting bundled FFmpeg MP4 encoding", exporter);
        Assert.Contains("FFmpeg output path", exporter);
        Assert.Contains("FFmpeg encoding completed in", exporter);
        Assert.Contains("High-resolution 2.5D conversion can take a while", viewModel);
    }

    [Fact]
    public void ImageProgressUpdates_AreMarshaledToCapturedUiContext()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var inlineProgress = ExtractSourceRange(
            source,
            "private sealed class InlineProgress<T>",
            "private static readonly TimeSpan LogCopyNotificationDuration");
        var parallaxConversion = ExtractSourceRange(
            source,
            "private async Task ConvertParallaxImageAsync()",
            "private void AddProcessExitLog");
        var parallaxProgressMethod = ExtractSourceRange(
            source,
            "private void ApplyImageParallaxExportProgress",
            "private void OpenImageOutputFolder()");

        Assert.Contains("private readonly SynchronizationContext? _synchronizationContext;", inlineProgress);
        Assert.Contains("_synchronizationContext = SynchronizationContext.Current;", inlineProgress);
        Assert.Contains("_synchronizationContext.Post", inlineProgress);
        Assert.Contains("ReportOnCapturedContext", inlineProgress);
        Assert.Contains("AppErrorLogService.LogRecoverableException(\"Image conversion progress dispatch\", exception)", inlineProgress);
        Assert.Contains("AppErrorLogService.LogRecoverableException(\"Image conversion progress update\", exception)", inlineProgress);
        Assert.Contains("new InlineProgress<ImageParallaxExportProgress>(ApplyImageParallaxExportProgress)", parallaxConversion);
        Assert.Contains("AddImageLog(progress.EnglishMessage, progress.SpanishMessage);", parallaxProgressMethod);
        Assert.Contains("RaiseImageExportPropertiesChanged();", parallaxProgressMethod);
    }

    [Fact]
    public void PublishStaging_OverlaysV3dfyParallaxHelperAndDepthCapabilityTokens()
    {
        var script = ReadRepoFile("scripts", "stage-iw3-bundle.ps1");
        var helper = ReadRepoFile("engine", "iw3", "v3dfy", "parallax2d.py");

        Assert.Contains("Copy-V3dfyIw3Extensions", script);
        Assert.Contains("parallax2d.py", script);
        Assert.Contains("--export", script);
        Assert.Contains("--export-depth-only", script);
        Assert.Contains("--export-depth-fit", script);
        Assert.Contains("IW3_CLI_CAPABILITIES.json", script);
        Assert.Contains("def main():", helper);
        Assert.Contains("load_depth", helper);
        Assert.Contains("render_frame", helper);
        Assert.DoesNotContain("subprocess", helper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pip", helper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("download", helper, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParallaxHelper_NormalizesDepthBeforeBlurringToSupportIw3SixteenBitPngs()
    {
        var helper = ReadRepoFile("engine", "iw3", "v3dfy", "parallax2d.py");
        var loadDepth = ExtractSourceRange(
            helper,
            "def load_depth",
            "def map_intensity");
        var smoothDepth = ExtractSourceRange(
            helper,
            "def smooth_depth_array",
            "def load_depth");

        Assert.Contains("def normalize_depth_array", helper);
        Assert.Contains("np.asarray(depth", helper);
        Assert.Contains("dtype=np.float32", helper);
        Assert.Contains("depth = normalize_depth_array(depth_image)", loadDepth);
        Assert.Contains("depth = smooth_depth_array(depth, smoothing)", loadDepth);
        Assert.Contains("mode=\"L\"", smoothDepth);
        Assert.Contains("ImageFilter.GaussianBlur", smoothDepth);
        Assert.DoesNotContain("depth_image = image.convert(\"I\")", loadDepth);
        Assert.DoesNotContain("depth_image.filter(ImageFilter.GaussianBlur", loadDepth);
        Assert.DoesNotContain("np.zeros", helper);
        Assert.DoesNotContain("np.ones", helper);
        Assert.Contains("centered_depth = depth - 0.5", helper);
    }

    [Fact]
    public void ParallaxHelper_ReportsClearInputAndFrameValidationFailures()
    {
        var helper = ReadRepoFile("engine", "iw3", "v3dfy", "parallax2d.py");

        Assert.Contains("Source image does not exist", helper);
        Assert.Contains("Depth image does not exist", helper);
        Assert.Contains("--frame-count must be greater than zero", helper);
        Assert.Contains("frames_dir.mkdir(parents=True, exist_ok=True)", helper);
    }

    private static string ReadRepoFile(params string[] relativeSegments)
    {
        var repoRoot = FindRepoRoot();
        return File.ReadAllText(Path.Combine([repoRoot, .. relativeSegments]));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string ExtractSourceRange(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start marker not found: {startMarker}");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"End marker not found after {startMarker}: {endMarker}");
        return source[start..end];
    }
}
