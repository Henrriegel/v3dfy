namespace V3dfy.Tests.App;

public sealed class MainWindowImageStereoExportSourceTests
{
    [Fact]
    public void ImageStereoExport_UsesIw3ExporterAndRemovesBasicFallback()
    {
        var viewModel = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var exporter = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Execution", "Iw3ImageStereoExporter.cs");

        Assert.Contains("private readonly IImageStereoExporter _imageStereoExporter;", viewModel);
        Assert.Contains("_imageStereoExporter = new Iw3ImageStereoExporter(", viewModel);
        Assert.Contains("new BundledLocalProcessRunner(", viewModel);
        Assert.Contains("ExportStereoscopicImageCommand = new AsyncRelayCommand(", viewModel);
        Assert.Contains("private async Task ExportStereoscopicImageAsync()", viewModel);
        Assert.Contains("public sealed class Iw3ImageStereoExporter : IImageStereoExporter", exporter);
        Assert.False(RepoFileExists("src", "V3dfy.App", "Services", "LocalImageStereoExporter.cs"));
        Assert.DoesNotContain("LocalImageStereoExporter", viewModel);
        Assert.DoesNotContain("Basic non-depth", viewModel);
        Assert.DoesNotContain("PngBitmapEncoder", viewModel);
        Assert.DoesNotContain("RenderTargetBitmap", viewModel);
    }

    [Fact]
    public void ImageStereoExport_ViewModelExposesProgressSuccessFailureState()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("public bool IsImageExportRunning", source);
        Assert.Contains("public bool CanExportStereoscopicImage", source);
        Assert.Contains("public bool ImageStereoExportReadinessCanExport", source);
        Assert.Contains("public bool CanOpenImageOutputFolder", source);
        Assert.Contains("public string ImageStereoExportActionText", source);
        Assert.Contains("public Visibility ImageStereoConvertButtonVisibility", source);
        Assert.Contains("public string ImageExportProgressText", source);
        Assert.Contains("public string ImageExportOverlayText", source);
        Assert.Contains("public string ImageExportStatusText", source);
        Assert.Contains("public bool IsImageExportOutputOutdated", source);
        Assert.Contains("public string ImageExportOutdatedText", source);
        Assert.Contains("public Visibility ImageExportOutdatedVisibility", source);
        Assert.Contains("public int ImageExportProgressPercent", source);
        Assert.Contains("public Visibility ImageExportProgressVisibility", source);
        Assert.Contains("public Visibility ImageExportSuccessVisibility", source);
        Assert.Contains("public Visibility ImageExportFailureVisibility", source);
        Assert.Contains("public string ImageGeneratedFilesText", source);
        Assert.Contains("public string ImageLastExportedPathText", source);
        Assert.Contains("public string ImageExportErrorText", source);
        Assert.Contains("ExportStereoscopicImageCommand.RaiseCanExecuteChanged();", source);
        Assert.Contains("OpenImageOutputFolderCommand.RaiseCanExecuteChanged();", source);
    }

    [Fact]
    public void ImageStereoExport_CommandRequiresPreparedStereoStepAndIw3Readiness()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var canExport = ExtractSourceRange(
            source,
            "public bool CanExportStereoscopicImage =>",
            "public bool CanOpenImageOutputFolder =>");

        Assert.Contains("!IsImageExportRunning", canExport);
        Assert.Contains("!HasCurrentImageConversionOutput", canExport);
        Assert.Contains("SelectedImageConversionStep == ImageConversionStep.PreviewAndExport", canExport);
        Assert.Contains("_hasEnteredImagePreviewExportStage", canExport);
        Assert.Contains("HasSelectedImage", canExport);
        Assert.Contains("HasImageMetadata", canExport);
        Assert.Contains("IsImageStereoModeSelected", canExport);
        Assert.Contains("IsImageSetupValid", canExport);
        Assert.Contains("ImageStereoExportReadinessCanExport", canExport);
        Assert.Contains("CreateImageStereoExportReadinessText()", source);
        Assert.Contains("LocalizeImageExportReadinessIssue(firstIssue.EnglishMessage)", source);
        Assert.Contains("LocalizationKeys.ImageReadinessNotPrepared", source);
    }

    [Fact]
    public void ImageStereoExport_ExporterUsesBundledResourcesOnly()
    {
        var exporter = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Execution", "Iw3ImageStereoExporter.cs");
        var commandBuilder = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Commands", "Iw3ImageStereoCommandBuilder.cs");

        Assert.Contains("request.ExpectedToolPaths.PythonExecutable", exporter);
        Assert.Contains("request.ExpectedToolPaths.NunifRootDirectory", exporter);
        Assert.Contains("request.ExpectedToolPaths.Iw3EngineDirectory", exporter);
        Assert.Contains("request.ExpectedToolPaths.ModelsDirectory", exporter);
        Assert.Contains("Iw3BundledRuntimeEnvironment.Create(request.ExpectedToolPaths)", exporter);
        Assert.Contains("AllowedRootDirectory: request.ExpectedToolPaths.Iw3EngineDirectory", exporter);
        Assert.Contains("Iw3DepthModelMapper.TryMap", exporter);
        Assert.Contains("ResolveModelPath(request.ExpectedToolPaths.ModelsDirectory", exporter);
        Assert.Contains("ExecutablePath: request.ExpectedToolPaths.PythonExecutable", commandBuilder);
        Assert.Contains("Iw3CliContract.DivergenceSwitch", commandBuilder);
        Assert.Contains("Iw3CliContract.ConvergenceSwitch", commandBuilder);
        Assert.DoesNotContain("Iw3CliContract.FormatSwitch", commandBuilder);
        Assert.DoesNotContain("Iw3CliContract.PngFormat", commandBuilder);
        Assert.DoesNotContain("C:\\v3dfy-iw3-intake", exporter);
        Assert.DoesNotContain("Environment.GetEnvironmentVariable", exporter);
        Assert.DoesNotContain("UseShellExecute", exporter);
        Assert.DoesNotContain("pip", exporter.ToLowerInvariant());
        Assert.DoesNotContain("download", exporter.ToLowerInvariant());
    }

    [Fact]
    public void ImageStereoExport_ExporterBlocksMissingEngineModelAndCapability()
    {
        var exporter = ReadRepoFile("src", "V3dfy.Engine.Iw3", "Execution", "Iw3ImageStereoExporter.cs");

        Assert.Contains("EvaluateReadiness(", exporter);
        Assert.Contains("source image file", exporter);
        Assert.Contains("embedded Python executable", exporter);
        Assert.Contains("Selected local model is not mapped to a verified iw3 depth model.", exporter);
        Assert.Contains("Selected bundled model file is missing", exporter);
        Assert.Contains("IW3_CLI_CAPABILITIES.json does not verify", exporter);
        Assert.Contains("Iw3CliContract.InputSwitch", exporter);
        Assert.Contains("Iw3CliContract.InputLongSwitch", exporter);
        Assert.Contains("Iw3CliContract.OutputSwitch", exporter);
        Assert.Contains("Iw3CliContract.OutputLongSwitch", exporter);
        Assert.Contains("AddMissingOption(missing, capabilities, requiredLayoutSwitch);", exporter);
        Assert.DoesNotContain("HasVerifiedOption(capabilities, Iw3CliContract.FormatSwitch)", exporter);
        Assert.Contains("WasBlocked: true", exporter);
        Assert.Contains("CreateBlockedResult", exporter);
    }

    [Fact]
    public void ImageStereoExport_UiBindsIw3CommandAndShowsSelectedFormatOnly()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var viewModel = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var stereoResult = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageStereoResultScaffold\"",
            "AutomationProperties.AutomationId=\"ImageWizardFooter\"");
        var rightOutputPanel = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageOutputPanelCard\"",
            "AutomationProperties.AutomationId=\"ImageScaffoldLog\"");
        var parallaxResult = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"",
            "AutomationProperties.AutomationId=\"ImageStereoResultScaffold\"");

        Assert.Contains("Source=\"{Binding ImageStereoPreviewImagePath}\"", stereoResult);
        Assert.Contains("Text=\"{Binding SelectedStereoOutputFormatDisplayText}\"", stereoResult);
        Assert.Contains("Visibility=\"{Binding ImageExportProgressVisibility}\"", stereoResult);
        Assert.Contains("Text=\"{Binding ImageExportOverlayText}\"", stereoResult);
        Assert.DoesNotContain("Text=\"{Binding ImageExportProgressText}\"", stereoResult);
        Assert.Contains("Value=\"{Binding ImageExportProgressPercent, Mode=OneWay}\"", stereoResult);
        Assert.DoesNotContain("Text=\"{Binding ImageGeneratedFilesText}\"", stereoResult);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ExportStereoscopicImageButton\"", stereoResult);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"OpenImageOutputFolderButton\"", stereoResult);
        Assert.Contains("AutomationProperties.AutomationId=\"ConvertImageButton\"", rightOutputPanel);
        Assert.Contains("Command=\"{Binding ConvertImageCommand}\"", rightOutputPanel);
        Assert.Contains("Content=\"{Binding ImageConvertActionText}\"", rightOutputPanel);
        Assert.Contains("IsEnabled=\"{Binding CanConvertImage}\"", rightOutputPanel);
        Assert.Contains("Visibility=\"{Binding ImageConvertButtonVisibility}\"", rightOutputPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"OpenImageOutputFolderButton\"", rightOutputPanel);
        Assert.Contains("Command=\"{Binding OpenImageOutputFolderCommand}\"", rightOutputPanel);
        Assert.Contains("IsEnabled=\"{Binding CanOpenImageOutputFolder}\"", rightOutputPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"NewImageConversionButton\"", rightOutputPanel);
        Assert.Contains("Command=\"{Binding NewImageConversionCommand}\"", rightOutputPanel);
        Assert.Contains("IsEnabled=\"{Binding CanStartNewImageConversion}\"", rightOutputPanel);
        Assert.Contains("Text=\"{Binding ImageGeneratedFilesText}\"", rightOutputPanel);
        Assert.Contains("Text=\"{Binding ImageExportOutdatedText}\"", rightOutputPanel);
        Assert.Contains("Visibility=\"{Binding ImageExportOutdatedVisibility}\"", rightOutputPanel);
        Assert.Contains("Text=\"{Binding ImageExportStatusText}\"", rightOutputPanel);
        Assert.Contains("Text=\"{Binding ImageLastExportedPathText}\"", rightOutputPanel);
        Assert.Contains("Text=\"{Binding ImageExportErrorText}\"", rightOutputPanel);
        Assert.Contains("public string ImageStereoPreviewImagePath", viewModel);
        Assert.Contains("public LocalizedOptionViewModel<ImageStereoOutputFormat>? SelectedStereoOutputFormatOption", viewModel);
        Assert.Contains("SelectedStereoOutputFormat = value.Value;", viewModel);
        Assert.Contains("new(ImageStereoOutputFormat.SideBySide", viewModel);
        Assert.Contains("new(ImageStereoOutputFormat.HalfTopBottom", viewModel);
        Assert.Contains("new(ImageStereoOutputFormat.Anaglyph", viewModel);
        Assert.Contains("Iw3ImageStereoExporter.GetMissingImageCapabilityOptions", viewModel);
        Assert.DoesNotContain("new(ImageStereoOutputFormat.LeftRightPair", viewModel);
        Assert.DoesNotContain("Text=\"SBS\"", stereoResult);
        Assert.DoesNotContain("Text=\"TAB\"", stereoResult);
        Assert.DoesNotContain("Text=\"Anaglyph\"", stereoResult);
        Assert.DoesNotContain("Text=\"L / R\"", stereoResult);
        Assert.DoesNotContain("ImageStereoLegacyResultScaffold", xaml);
        Assert.DoesNotContain("Content=\"{Binding ImageExportActionText}\"", parallaxResult);
        Assert.DoesNotContain("IsEnabled=\"False\"", parallaxResult);
        Assert.DoesNotContain("Command=\"{Binding ConvertImageCommand}\"", parallaxResult);
        Assert.DoesNotContain("Command=\"{Binding ExportStereoscopicImageCommand}\"", parallaxResult);
    }

    [Fact]
    public void ImageStereoExport_PrimaryActionUsesConvertWording()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("public string ImageConvertActionText => IsImageExportRunning", source);
        Assert.Contains("public string ImageStereoExportActionText => ImageConvertActionText", source);
        Assert.Contains("? T(LocalizationKeys.ImageProgressConverting)", source);
        Assert.Contains(": T(LocalizationKeys.CommonConvert)", source);
        Assert.Contains("public string ImageExportOverlayText => T(LocalizationKeys.ImageProgressConverting);", source);
        Assert.DoesNotContain("Export with bundled iw3", source);
        Assert.DoesNotContain("Exporting...\", \"Exportando...", source);
        Assert.DoesNotContain("Export completed:", source);
    }

    [Fact]
    public void ImageStereoExport_OverlayUsesCleanUserFacingText()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var stereoResult = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageStereoResultScaffold\"",
            "AutomationProperties.AutomationId=\"ImageWizardFooter\"");
        var progressMethod = ExtractSourceRange(
            source,
            "private void ApplyImageExportProgress",
            "private void OpenImageOutputFolder()");

        Assert.Contains("public string ImageExportOverlayText => T(LocalizationKeys.ImageProgressConverting);", source);
        Assert.Contains("public string ImageExportStatusText => IsImageExportRunning", source);
        Assert.Contains("T(LocalizationKeys.ImageProgressConverting)", source);
        Assert.Contains("Text=\"{Binding ImageExportOverlayText}\"", stereoResult);
        Assert.DoesNotContain("Text=\"{Binding ImageExportProgressText}\"", stereoResult);
        Assert.DoesNotContain("CommandPreview", stereoResult);
        Assert.DoesNotContain("iw3 command:", stereoResult);
        Assert.Contains("LocalizeImageStereoExportProgress(progress)", progressMethod);
        Assert.DoesNotContain("AddImageLog(progress.EnglishMessage, progress.SpanishMessage);", progressMethod);
    }

    [Fact]
    public void ImageStereoExport_OutputFolderAndLoggingUseBundledIw3()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var exportMethod = ExtractSourceRange(
            source,
            "private async Task ExportStereoscopicImageAsync()",
            "private void ApplyImageExportProgress");
        var outputDirectoryMethod = ExtractSourceRange(
            source,
            "private static string GetDefaultImageExportDirectory",
            "private static ImageStereoExportFormat MapImageStereoExportFormat");

        Assert.Contains("GetDefaultImageExportDirectory(SelectedImagePath)", exportMethod);
        Assert.Contains("CreateImageStereoExportRequest(outputDirectory)", exportMethod);
        Assert.Contains("LocalizationKeys.ImageLogStereoStarted", exportMethod);
        Assert.Contains("LocalizationKeys.ImageLogSelectedModelFormat", exportMethod);
        Assert.Contains("iw3Model", exportMethod);
        Assert.Contains("LocalizationKeys.ImageLogOutputPathFormat", exportMethod);
        Assert.Contains("LocalizationKeys.ImageLogBundledPythonFormat", exportMethod);
        Assert.Contains("LocalizationKeys.ImageLogBundledIw3PackageFormat", exportMethod);
        Assert.Contains("LocalizationKeys.ImageLogProcessExitCodeFormat", exportMethod);
        Assert.Contains("LocalizationKeys.ImageLogGeneratedImageFileFormat", exportMethod);
        Assert.Contains("LocalizationKeys.ImageLogTechnicalDetailFormat", exportMethod);
        Assert.Contains("return sourceDirectory;", outputDirectoryMethod);
        Assert.DoesNotContain("v3dfy-image-exports", outputDirectoryMethod);
        Assert.DoesNotContain("Directory.CreateDirectory(outputDirectory);", exportMethod);
    }

    [Fact]
    public void ImageStereoExport_ResetRulesClearResultsWithoutLanguageThemeReset()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var selectImageMethod = ExtractSourceRange(
            source,
            "private void TrySelectImage(string path)",
            "private void AnalyzeImage()");
        var analyzeImageMethod = ExtractSourceRange(
            source,
            "private void AnalyzeImage()",
            "private static ImageMetadata ReadImageMetadata");
        var setupChangedMethod = ExtractSourceRange(
            source,
            "private void ApplyImageSetupChanged(",
            "private void ResetImageSetupState()");
        var selectedModelMethod = ExtractSourceRange(
            source,
            "private void SetSelectedLocalModelCandidate(",
            "private ConversionExecutionStartGateResult EvaluateConversionStartGate()");
        var localizedRefresh = ExtractSourceRange(
            source,
            "private void UpdateLogLanguages()",
            "private void RaiseSidebarPropertiesChanged()");

        Assert.Contains("ResetImageExportState();", selectImageMethod);
        Assert.Contains("ResetImageExportState();", analyzeImageMethod);
        Assert.Contains("ResetImageExportState();", setupChangedMethod);
        Assert.Contains("ResetImageExportState();", selectedModelMethod);
        Assert.Contains("MarkImageExportOutputOutdated();", setupChangedMethod);
        Assert.Contains("MarkImageExportOutputOutdated();", selectedModelMethod);
        Assert.Contains("_hasEnteredImagePreviewExportStage = false;", selectedModelMethod);
        Assert.Contains("LocalizationKeys.ImageLogSetupChangedOutdated", setupChangedMethod);
        Assert.Contains("LocalizationKeys.ImageLogModelChangedOutdated", selectedModelMethod);
        Assert.DoesNotContain("ResetImageExportState();", localizedRefresh);
    }

    [Fact]
    public void ImageStereoExport_AutoAnalyzesSelectedImagesAndProvidesNewConversionReset()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var selectImageMethod = ExtractSourceRange(
            source,
            "private void TrySelectImage(string path)",
            "private void AnalyzeImage()");
        var analyzeImageMethod = ExtractSourceRange(
            source,
            "private void AnalyzeImage()",
            "private static ImageMetadata ReadImageMetadata");
        var newConversionMethod = ExtractSourceRange(
            source,
            "private void StartNewImageConversion()",
            "private void ResetImageExportState()");

        Assert.Contains("public string AnalyzeImageText => T(LocalizationKeys.ImageSourceReanalyzeButton);", source);
        Assert.Contains("AnalyzeImageCommand = new RelayCommand(AnalyzeImage, () => CanAnalyzeImage);", source);
        Assert.Contains("AnalyzeImage();", selectImageMethod);
        Assert.DoesNotContain("Analyze image to read metadata", selectImageMethod);
        Assert.Contains("_selectedImageMetadata = ReadImageMetadata(SelectedImagePath);", analyzeImageMethod);
        Assert.Contains("_selectedImageMetadata = null;", analyzeImageMethod);
        Assert.Contains("public RelayCommand NewImageConversionCommand", source);
        Assert.Contains("public bool CanStartNewImageConversion", source);
        Assert.Contains("NewImageConversionCommand = new RelayCommand(", source);
        Assert.Contains("SelectedImagePath = null;", newConversionMethod);
        Assert.Contains("_selectedImageMetadata = null;", newConversionMethod);
        Assert.Contains("ResetImageSetupState();", newConversionMethod);
        Assert.Contains("ResetImageExportState();", newConversionMethod);
        Assert.Contains("_isImageExportOutputOutdated = false;", source);
        Assert.Contains("_hasEnteredImagePreviewExportStage = false;", newConversionMethod);
        Assert.Contains("SelectedImageConversionStep = ImageConversionStep.ModeAndSource;", newConversionMethod);
    }

    [Fact]
    public void ImageStereoExport_PostSuccessHidesConvertAndStaleShowsConvertAgain()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var canExport = ExtractSourceRange(
            source,
            "public bool CanExportStereoscopicImage =>",
            "public bool CanOpenImageOutputFolder =>");
        var canOpen = ExtractSourceRange(
            source,
            "public bool CanOpenImageOutputFolder =>",
            "public bool CanStartNewImageConversion =>");
        var visibilityProperties = ExtractSourceRange(
            source,
            "public Visibility ImageExportSuccessVisibility =>",
            "public Visibility ImageExportFailureVisibility =>");
        var resetState = ExtractSourceRange(
            source,
            "private void ResetImageExportState()",
            "private static string GetDefaultImageExportDirectory");
        var statusMethod = ExtractSourceRange(
            source,
            "private string CreateImagePreviewExportStatusText()",
            "private Iw3ImageStereoExportReadiness? EvaluateCurrentImageStereoExportReadiness()");
        var outputPanelVisibility = ExtractSourceRange(
            source,
            "public Visibility ImagePreviewExportStatusCardVisibility =>",
            "public Visibility ImageAnalysisResultsVisibility =>");
        var rightOutputPanel = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageOutputPanelCard\"",
            "AutomationProperties.AutomationId=\"ImageScaffoldLog\"");

        Assert.Contains("private bool HasCurrentImageConversionOutput", source);
        Assert.Contains("!HasCurrentImageConversionOutput", canExport);
        Assert.Contains("HasCurrentImageConversionOutput", canOpen);
        Assert.Contains("IsImageExportRunning || HasCurrentImageConversionOutput ? Visibility.Collapsed : Visibility.Visible", visibilityProperties);
        Assert.Contains("!IsImageExportRunning && HasCurrentImageConversionOutput", visibilityProperties);
        Assert.Contains("IsImageExportOutputOutdated", visibilityProperties);
        Assert.Contains("LocalizationKeys.ImageOutputOutdated", source);
        Assert.Contains("_isImageExportOutputOutdated = false;", resetState);
        Assert.Contains("MarkImageExportOutputOutdated", resetState);
        Assert.Contains("return ImageExportOutdatedText;", statusMethod);
        Assert.Contains("LocalizationKeys.ImageOutputCompletedFormat", statusMethod);
        Assert.Contains("SelectedImageConversionStep != ImageConversionStep.ModeAndSource", outputPanelVisibility);
        Assert.Contains("_hasEnteredImagePreviewExportStage", outputPanelVisibility);
        Assert.Contains("HasCurrentImageConversionOutput", outputPanelVisibility);
        Assert.DoesNotContain("IsImageExportOutputOutdated", outputPanelVisibility);
        Assert.Contains("Visibility=\"{Binding ImageConvertButtonVisibility}\"", rightOutputPanel);
    }

    [Fact]
    public void ImageStereoExport_Step2ReadinessAndVerifiedFormatsAreWired()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var stereoSetup = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageStereoScaffold\"",
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"");
        var stereoResult = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageStereoResultScaffold\"",
            "Grid.Column=\"2\"");
        var selectableFormatMethod = ExtractSourceRange(
            source,
            "private bool IsImageStereoOutputFormatSelectable",
            "private void EnsureSelectedStereoOutputFormatIsSupported()");
        var wideControls = ExtractSourceRange(
            stereoSetup,
            "Visibility=\"{Binding ActualWidth,",
            "AutomationProperties.AutomationId=\"ImageStereoSetupNarrowLayout\"");
        var narrowControls = ExtractSourceRange(
            stereoSetup,
            "AutomationProperties.AutomationId=\"ImageStereoSetupNarrowLayout\"",
            "AutomationProperties.AutomationId=\"ImageStereoReadinessSummaryCardNarrow\"");
        var conversionChanged = ExtractSourceRange(
            source,
            "private void RaiseImageConversionPropertiesChanged()",
            "private void RaiseImageExportPropertiesChanged()");

        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoReadinessSummaryCard\"", stereoSetup);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoReadinessSummaryCardNarrow\"", stereoSetup);
        Assert.DoesNotContain("Source=\"{Binding SelectedImagePath}\"", stereoSetup);
        Assert.Contains("Text=\"{Binding ImageStereoControlsTitleText}\"", stereoSetup);
        Assert.Contains("Source=\"{Binding ImageStereoPreviewImagePath}\"", stereoResult);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageModelSelector\"", stereoSetup);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageModelSelectorNarrow\"", stereoSetup);
        Assert.Contains("ItemsSource=\"{Binding LocalModelCandidates}\"", stereoSetup);
        Assert.Contains("SelectedItem=\"{Binding SelectedLocalModelCandidate,", stereoSetup);
        Assert.Contains("Text=\"{Binding ImageModelSelectionSharedNoteText}\"", stereoSetup);
        Assert.Contains("Text=\"{Binding ImageSelectedModelSummaryText}\"", stereoSetup);
        Assert.Contains("Text=\"{Binding ImageExpectedOutputFileText}\"", stereoSetup);
        Assert.Contains("Text=\"{Binding ImageSaveLocationText}\"", stereoSetup);
        Assert.Contains("Text=\"{Binding ImageSupportedStereoFormatsText}\"", stereoSetup);
        Assert.Contains("Text=\"{Binding ImageBundledIw3StereoNoteText}\"", stereoSetup);
        Assert.DoesNotContain("Content=\"{Binding ImagePreviewActionText}\"", stereoSetup);
        Assert.DoesNotContain("Text=\"{Binding ImageComparisonTitleText}\"", stereoSetup);
        Assert.Contains("SelectedItem=\"{Binding SelectedStereoOutputFormatOption,", stereoSetup);
        Assert.DoesNotContain("SelectedValue=\"{Binding SelectedStereoOutputFormat}\"", stereoSetup);
        Assert.Contains("ItemsSource=\"{Binding StereoAnaglyphModeOptions}\"", stereoSetup);
        Assert.Contains("SelectedValuePath=\"Value\"", stereoSetup);
        Assert.Contains("SelectedValue=\"{Binding SelectedStereoAnaglyphMode}\"", stereoSetup);
        Assert.Contains("Visibility=\"{Binding ImageStereoAnaglyphModeVisibility}\"", wideControls);
        Assert.Contains("Visibility=\"{Binding ImageStereoAnaglyphModeVisibility}\"", narrowControls);
        Assert.Contains("public bool IsAnaglyphOutputSelected", source);
        Assert.Contains("public Visibility ImageStereoAnaglyphModeVisibility", source);
        Assert.Contains("_allStereoOutputFormatOptions", source);
        Assert.Contains(".Where(option => IsImageStereoOutputFormatSelectable(option.Value))", source);
        Assert.Contains("format == ImageStereoOutputFormat.LeftRightPair", selectableFormatMethod);
        Assert.Contains("return false;", selectableFormatMethod);
        Assert.Contains("Iw3ImageStereoExporter.GetMissingImageCapabilityOptions", selectableFormatMethod);
        Assert.Contains("EnsureSelectedStereoOutputFormatIsSupported();", source);
        Assert.Contains("OnPropertyChanged(nameof(ImageStereoSummaryText));", conversionChanged);
        Assert.Contains("OnPropertyChanged(nameof(IsAnaglyphOutputSelected));", conversionChanged);
        Assert.Contains("OnPropertyChanged(nameof(ImageStereoAnaglyphModeVisibility));", conversionChanged);
    }

    [Fact]
    public void ImageStereoExport_AnaglyphModeOptionsAreSupportedAndNormalized()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var optionsRange = ExtractSourceRange(
            source,
            "private const string SupportedStereoAnaglyphMode",
            "public string SelectedParallaxDepthIntensity");
        var anaglyphSetter = ExtractSourceRange(
            source,
            "public string SelectedStereoAnaglyphMode",
            "public string? SelectedVideoPath");
        var createRequest = ExtractSourceRange(
            source,
            "private ImageStereoExportRequest CreateImageStereoExportRequest",
            "private LocalModelPlanSelection? CreateSelectedImageLocalModelSelection()");
        var resetSetup = ExtractSourceRange(
            source,
            "private void ResetImageSetupState()",
            "private void SetImageSetupString(");

        Assert.Contains("private const string SupportedStereoAnaglyphMode = \"Red/Cyan\";", source);
        Assert.Contains("public IReadOnlyList<LocalizedOptionViewModel<string>> StereoAnaglyphModeOptions", optionsRange);
        Assert.Contains("(SupportedStereoAnaglyphMode, LocalizationKeys.ImageStereoAnaglyphModeRedCyan)", source);
        Assert.DoesNotContain("\"Green/Magenta\"", optionsRange);
        Assert.DoesNotContain("\"Amber/Blue\"", optionsRange);
        Assert.Contains("NormalizeStereoAnaglyphMode(value)", anaglyphSetter);
        Assert.Contains("LocalizationKeys.ImageLogAnaglyphModeChangedFormat", anaglyphSetter);
        Assert.Contains("ApplyImageSetupChanged(", anaglyphSetter);
        Assert.Contains("NormalizeSelectedStereoAnaglyphMode();", source);
        Assert.Contains("AnaglyphMode: NormalizeStereoAnaglyphMode(SelectedStereoAnaglyphMode)", createRequest);
        Assert.Contains("_selectedStereoAnaglyphMode = SupportedStereoAnaglyphMode;", resetSetup);
    }

    [Fact]
    public void ImageStereoExport_PrepareWordingIsConversionFocused()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("LocalizationKeys.ImageActionPrepareConversion", source);
        Assert.Contains("LocalizationKeys.ImageLogPreparedToast", source);
        Assert.Contains("LocalizationKeys.ImageOutputStatusPrompt", source);
        Assert.Contains("LocalizationKeys.ImageLogSetupChangedOutdatedPrepareFormat", source);
        Assert.DoesNotContain("Prepare image preview/export plan", source);
        Assert.DoesNotContain("Image preview/export plan prepared", source);
        Assert.DoesNotContain("preview/export plan", source);
    }

    [Fact]
    public void ImageStereoExport_PrepareFooterHidesAfterPlanIsPrepared()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var footerVisibility = ExtractSourceRange(
            source,
            "public Visibility ContinueWithImageConversionFooterVisibility =>",
            "public bool CanContinueWithImageConversion =>");
        var canContinue = ExtractSourceRange(
            source,
            "public bool CanContinueWithImageConversion =>",
            "public bool IsImageExportRunning");
        var continueMethod = ExtractSourceRange(
            source,
            "private void ContinueWithImageConversion()",
            "private string CreateImagePreviewExportStatusText()");
        var setupChangedMethod = ExtractSourceRange(
            source,
            "private void ApplyImageSetupChanged(",
            "private void ResetImageSetupState()");

        Assert.Contains("!_hasEnteredImagePreviewExportStage", footerVisibility);
        Assert.Contains("!_hasEnteredImagePreviewExportStage", canContinue);
        Assert.Contains("_hasEnteredImagePreviewExportStage = true;", continueMethod);
        Assert.Contains("_hasEnteredImagePreviewExportStage = false;", setupChangedMethod);
    }

    private static string ReadRepoFile(params string[] relativeParts)
    {
        var path = FindRepoPath(relativeParts);
        if (File.Exists(path))
        {
            return File.ReadAllText(path);
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(relativeParts)}.");
    }

    private static bool RepoFileExists(params string[] relativeParts) =>
        File.Exists(FindRepoPath(relativeParts));

    private static string FindRepoPath(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.Combine(relativeParts);
    }

    private static string ExtractSourceRange(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);

        return source[start..end];
    }
}
