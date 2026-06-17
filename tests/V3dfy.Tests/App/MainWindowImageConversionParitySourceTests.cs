namespace V3dfy.Tests.App;

public sealed class MainWindowImageConversionParitySourceTests
{
    [Fact]
    public void ImageStep1_UsesVideoSourceSelectionCardPattern()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var imageStep = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageModeSourceStepContent\"",
            "AutomationProperties.AutomationId=\"ImageSetupStepContent\"");
        var imageSourceCard = ExtractSourceRange(
            imageStep,
            "AutomationProperties.AutomationId=\"ImageSourceSelectionPanel\"",
            "AutomationProperties.AutomationId=\"ImageMetadataPanel\"");

        Assert.Contains("public string ImageSourceAnalysisTitleText => Text(\"Source image\"", source);
        Assert.Contains("Drop one image file here or browse for a file.", source);
        Assert.Contains("\".jpg, .jpeg, .png, .bmp, .tif, .tiff, .webp\"", source);
        Assert.Contains("Padding=\"16\"", imageSourceCard);
        Assert.Contains("Background=\"{DynamicResource InputBackgroundBrush}\"", imageSourceCard);
        Assert.Contains("BorderBrush=\"{DynamicResource CardBorderBrush}\"", imageSourceCard);
        Assert.Contains("BorderThickness=\"1\"", imageSourceCard);
        Assert.Contains("CornerRadius=\"6\"", imageSourceCard);
        Assert.Contains("Text=\"{Binding DropImageText}\"", imageSourceCard);
        Assert.Contains("Text=\"{Binding ImageSupportedExtensionsText}\"", imageSourceCard);
        Assert.Contains("Text=\"{Binding SelectedImageDisplayPath}\"", imageSourceCard);
        Assert.Contains("<StackPanel Orientation=\"Horizontal\">", imageSourceCard);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectImageButton\"", imageSourceCard);
        Assert.Contains("Command=\"{Binding SelectImageCommand}\"", imageSourceCard);
        Assert.Contains("AutomationProperties.AutomationId=\"AnalyzeImageButton\"", imageSourceCard);
        Assert.Contains("Command=\"{Binding AnalyzeImageCommand}\"", imageSourceCard);
        Assert.Contains("IsEnabled=\"{Binding CanAnalyzeImage}\"", imageSourceCard);
        Assert.DoesNotContain("<Grid.ColumnDefinitions>", imageSourceCard);
        Assert.True(IndexOf(imageSourceCard, "Text=\"{Binding DropImageText}\"") <
            IndexOf(imageSourceCard, "Text=\"{Binding ImageSupportedExtensionsText}\""));
        Assert.True(IndexOf(imageSourceCard, "Text=\"{Binding ImageSupportedExtensionsText}\"") <
            IndexOf(imageSourceCard, "Text=\"{Binding SelectedImageDisplayPath}\""));
        Assert.True(IndexOf(imageSourceCard, "Text=\"{Binding SelectedImageDisplayPath}\"") <
            IndexOf(imageSourceCard, "AutomationProperties.AutomationId=\"SelectImageButton\""));
    }

    [Fact]
    public void ImageStep1_DoesNotShowModeCardsOrPreviewSetupCards()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var imageStep = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageModeSourceStepContent\"",
            "AutomationProperties.AutomationId=\"ImageSetupStepContent\"");

        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageConversionModeCards\"", imageStep);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageParallaxModeCard\"", imageStep);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageStereoModeCard\"", imageStep);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageParallaxScaffold\"", imageStep);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageStereoScaffold\"", imageStep);
        Assert.DoesNotContain("Text=\"{Binding ImageSourcePanelTitleText}\"", imageStep);
        Assert.DoesNotContain("Text=\"{Binding ImageDepthPanelTitleText}\"", imageStep);
        Assert.DoesNotContain("Text=\"{Binding ImageParallaxPreviewTitleText}\"", imageStep);
    }

    [Fact]
    public void ImageAnalysisMetadata_IsHiddenUntilAnalysisSucceeds()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var imageStep = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageModeSourceStepContent\"",
            "AutomationProperties.AutomationId=\"ImageSetupStepContent\"");
        var selectImageMethod = ExtractSourceRange(
            source,
            "private void TrySelectImage(string path)",
            "private void AnalyzeImage()");
        var analyzeImageMethod = ExtractSourceRange(
            source,
            "private void AnalyzeImage()",
            "private static ImageMetadata ReadImageMetadata");

        Assert.Contains("AutomationProperties.AutomationId=\"ImageMetadataPanel\"", imageStep);
        Assert.Contains("Visibility=\"{Binding ImageAnalysisResultsVisibility}\"", imageStep);
        Assert.Contains("public Visibility ImageAnalysisResultsVisibility", source);
        Assert.Contains("HasImageMetadata ? Visibility.Visible : Visibility.Collapsed;", source);
        Assert.Contains("_selectedImageMetadata = null;", selectImageMethod);
        Assert.Contains("AnalyzeImage();", selectImageMethod);
        Assert.DoesNotContain("ReadImageMetadata", selectImageMethod);
        Assert.Contains("_selectedImageMetadata = ReadImageMetadata(SelectedImagePath);", analyzeImageMethod);
    }

    [Fact]
    public void ImageStep2_ModeCardsRequireAnalysisAndSetupContentFlowsBelowCards()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var constructor = ExtractSourceRange(
            source,
            "public MainWindowViewModel()",
            "public string AppTitle => \"v3dfy\";");
        var setupStep = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageSetupStepContent\"",
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"");

        Assert.Contains("Visibility=\"{Binding ImageSetupStepVisibility}\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageConversionModeCards\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxModeCard\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoModeCard\"", setupStep);
        Assert.Contains("public bool CanOpenImageSetupStep => HasImageMetadata && CanUseImageStepNavigation;", source);
        Assert.Contains("SelectImageSetupStepCommand = new RelayCommand(", constructor);
        Assert.Contains("() => CanOpenImageSetupStep", constructor);
        Assert.Contains("ImageConversionStep.ModeAndSource => CanUseImageStepNavigation && CanOpenImageSetupStep", source);
        Assert.True(IndexOf(setupStep, "AutomationProperties.AutomationId=\"ImageConversionModeCards\"") <
            IndexOf(setupStep, "AutomationProperties.AutomationId=\"ImageNoModeSetupHint\""));
        Assert.True(IndexOf(setupStep, "AutomationProperties.AutomationId=\"ImageConversionModeCards\"") <
            IndexOf(setupStep, "AutomationProperties.AutomationId=\"ImageParallaxScaffold\""));
        Assert.True(IndexOf(setupStep, "AutomationProperties.AutomationId=\"ImageParallaxScaffold\"") <
            IndexOf(setupStep, "AutomationProperties.AutomationId=\"ImageStereoScaffold\""));
        Assert.DoesNotContain("Margin=\"-", setupStep);
        Assert.DoesNotContain("Canvas.", setupStep);
        Assert.DoesNotContain("RenderTransform", setupStep);
    }

    [Fact]
    public void ImageStep2_NoModeHintAndMutuallyExclusiveSetupPanelsArePresent()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var setupStep = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageSetupStepContent\"",
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"");
        var parallaxVisibility = ExtractSourceRange(
            source,
            "public Visibility ImageParallaxSetupStepVisibility",
            "public Visibility ImageStereoSetupStepVisibility");
        var stereoVisibility = ExtractSourceRange(
            source,
            "public Visibility ImageStereoSetupStepVisibility",
            "public Visibility ImageNoModeSetupHintVisibility");
        var noModeVisibility = ExtractSourceRange(
            source,
            "public Visibility ImageNoModeSetupHintVisibility",
            "public Visibility ImageParallaxPreviewExportStepVisibility");
        var stereoSetup = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageStereoScaffold\"",
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"");

        Assert.Contains("AutomationProperties.AutomationId=\"ImageNoModeSetupHint\"", setupStep);
        Assert.Contains("Visibility=\"{Binding ImageNoModeSetupHintVisibility}\"", setupStep);
        Assert.Contains("Text=\"{Binding ImageNoModeSetupHintText}\"", setupStep);
        Assert.Contains("Choose a workflow to configure image setup.", source);
        Assert.Contains("SelectedImageConversionStep == ImageConversionStep.Setup && !HasSelectedImageMode", noModeVisibility);
        Assert.Contains("SelectedImageConversionStep == ImageConversionStep.Setup && IsImageParallaxModeSelected", parallaxVisibility);
        Assert.Contains("SelectedImageConversionStep == ImageConversionStep.Setup && IsImageStereoModeSelected", stereoVisibility);
        Assert.Contains("Visibility=\"{Binding ImageParallaxSetupStepVisibility}\"", setupStep);
        Assert.Contains("Visibility=\"{Binding ImageStereoSetupStepVisibility}\"", setupStep);
        Assert.DoesNotContain("Text=\"{Binding ImageDepthPanelTitleText}\"", stereoSetup);
    }

    [Fact]
    public void ImageWorkflowCards_HaveClearSelectedVisualStateFromActiveTag()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var workflowStyle = ExtractSourceRange(
            xaml,
            "x:Key=\"ImageWorkflowOptionCardButtonStyle\"",
            "x:Key=\"ShellSidebarIconTextStyle\"");
        var setupStep = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageSetupStepContent\"",
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"");

        Assert.Contains("Style=\"{StaticResource ImageWorkflowOptionCardButtonStyle}\"", setupStep);
        Assert.Contains("Tag=\"{Binding ImageParallaxModeSelectionState}\"", setupStep);
        Assert.Contains("Tag=\"{Binding ImageStereoModeSelectionState}\"", setupStep);
        Assert.Contains("IsEnabled=\"{Binding ImageWorkflowCardsEnabled}\"", setupStep);
        Assert.Contains("Binding Tag, RelativeSource={RelativeSource TemplatedParent}", workflowStyle);
        Assert.Contains("Value=\"Active\"", workflowStyle);
        Assert.Contains("Value=\"{DynamicResource AccentBrush}\"", workflowStyle);
        Assert.Contains("Value=\"{DynamicResource TabSelectedBackgroundBrush}\"", workflowStyle);
        Assert.Contains("Value=\"{DynamicResource ComboBoxHoverBrush}\"", workflowStyle);
        Assert.Contains("Value=\"{DynamicResource AccentHoverBrush}\"", workflowStyle);
        Assert.Contains("Property=\"BorderThickness\"", workflowStyle);
        Assert.Contains("Value=\"2\"", workflowStyle);
        Assert.Contains("x:Key=\"ImageWorkflowSelectedMarkerStyle\"", workflowStyle);
        Assert.Contains("x:Key=\"ImageWorkflowOptionIconTextStyle\"", workflowStyle);
        Assert.Contains("x:Key=\"ImageWorkflowOptionTitleTextStyle\"", workflowStyle);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxModeSelectedMarker\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoModeSelectedMarker\"", setupStep);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageParallaxModeSelectedBadge\"", setupStep);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageStereoModeSelectedBadge\"", setupStep);
        Assert.DoesNotContain("Text=\"{Binding ImageParallaxModeCardStatusText}\"", setupStep);
        Assert.DoesNotContain("Text=\"{Binding ImageStereoModeCardStatusText}\"", setupStep);
        Assert.DoesNotContain("#", workflowStyle);
        Assert.Contains("SelectedImageConversionMode == ImageConversionMode.ParallaxPhoto", source);
        Assert.Contains("SelectedImageConversionMode == ImageConversionMode.StereoscopicImage", source);
        Assert.Contains("public string ImageParallaxModeSelectionState => IsImageParallaxModeSelected ? \"Active\" : \"Pending\";", source);
        Assert.Contains("public string ImageStereoModeSelectionState => IsImageStereoModeSelected ? \"Active\" : \"Pending\";", source);
    }

    [Fact]
    public void ImageWorkflowChooser_CollapsesAfterSelectionAndTogglesFromSummary()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var constructor = ExtractSourceRange(
            source,
            "public MainWindowViewModel()",
            "public string AppTitle => \"v3dfy\";");
        var setupStep = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageSetupStepContent\"",
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"");
        var selectModeMethod = ExtractSourceRange(
            source,
            "private void SelectImageConversionMode(ImageConversionMode mode)",
            "private void ToggleImageWorkflowChooser()");
        var toggleMethod = ExtractSourceRange(
            source,
            "private void ToggleImageWorkflowChooser()",
            "private void SelectImageConversionStep");

        Assert.Contains("private bool _isImageWorkflowChooserExpanded;", source);
        Assert.Contains("public bool IsImageWorkflowChooserExpanded", source);
        Assert.Contains("public Visibility ImageWorkflowSummaryVisibility", source);
        Assert.Contains("SelectedImageConversionStep == ImageConversionStep.Setup && HasSelectedImageMode", source);
        Assert.Contains("public Visibility ImageWorkflowChooserVisibility", source);
        Assert.Contains("(!HasSelectedImageMode || IsImageWorkflowChooserExpanded)", source);
        Assert.Contains("ToggleImageWorkflowChooserCommand = new RelayCommand(", constructor);
        Assert.Contains("ToggleImageWorkflowChooser,", constructor);
        Assert.Contains("() => CanInteractWithImageWorkflow", constructor);
        Assert.Contains("public RelayCommand ToggleImageWorkflowChooserCommand { get; }", source);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageWorkflowSummaryBar\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageWorkflowSummaryWideLayout\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageWorkflowSummaryNarrowLayout\"", setupStep);
        Assert.Contains("Text=\"{Binding ImageWorkflowSummaryText}\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ChangeImageWorkflowButton\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ChangeImageWorkflowButtonNarrow\"", setupStep);
        Assert.Contains("Command=\"{Binding ToggleImageWorkflowChooserCommand}\"", setupStep);
        Assert.Contains("Text=\"{Binding ChangeImageWorkflowText}\"", setupStep);
        Assert.Contains("Text=\"{Binding ImageWorkflowChooserChevronText}\"", setupStep);
        Assert.Contains("ConverterParameter=GreaterOrEqual:360", setupStep);
        Assert.Contains("ConverterParameter=LessThan:360", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageWorkflowChooser\"", setupStep);
        Assert.Contains("Visibility=\"{Binding ImageWorkflowChooserVisibility}\"", setupStep);
        Assert.Contains("IsImageWorkflowChooserExpanded = false;", selectModeMethod);
        Assert.Contains("IsImageWorkflowChooserExpanded = !IsImageWorkflowChooserExpanded;", toggleMethod);
        Assert.True(IndexOf(setupStep, "AutomationProperties.AutomationId=\"ImageWorkflowSummaryBar\"") <
            IndexOf(setupStep, "AutomationProperties.AutomationId=\"ImageWorkflowChooser\""));
        Assert.True(IndexOf(setupStep, "AutomationProperties.AutomationId=\"ImageWorkflowChooser\"") <
            IndexOf(setupStep, "AutomationProperties.AutomationId=\"ImageConversionModeCards\""));
        Assert.True(IndexOf(setupStep, "AutomationProperties.AutomationId=\"ImageConversionModeCards\"") <
            IndexOf(setupStep, "AutomationProperties.AutomationId=\"ImageParallaxScaffold\""));
        Assert.DoesNotContain("Margin=\"-", setupStep);
        Assert.DoesNotContain("Canvas.", setupStep);
        Assert.DoesNotContain("RenderTransform", setupStep);
    }

    [Fact]
    public void ImageWorkflowSummary_UsesContentPresenterAndKeepsChevronVisibleAtNarrowWidth()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var appXaml = ReadRepoFile("src", "V3dfy.App", "App.xaml");
        var converter = ReadRepoFile("src", "V3dfy.App", "Converters", "ViewportThresholdVisibilityConverter.cs");
        var setupStep = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageSetupStepContent\"",
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"");
        var summary = ExtractSourceRange(
            setupStep,
            "AutomationProperties.AutomationId=\"ImageWorkflowSummaryBar\"",
            "AutomationProperties.AutomationId=\"ImageWorkflowChooser\"");

        Assert.Contains("<ContentPresenter HorizontalAlignment=\"Center\"", appXaml);
        Assert.DoesNotContain("Text=\"{Binding Content", appXaml);
        Assert.DoesNotContain("System.Windows.Controls.StackPanel", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageWorkflowSummaryWideLayout\"", summary);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageWorkflowSummaryNarrowLayout\"", summary);
        Assert.Contains("AutomationProperties.AutomationId=\"ChangeImageWorkflowButton\"", summary);
        Assert.Contains("AutomationProperties.AutomationId=\"ChangeImageWorkflowButtonNarrow\"", summary);
        Assert.Contains("<ColumnDefinition Width=\"Auto\" />", summary);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", summary);
        Assert.True(CountOccurrences(summary, "Text=\"{Binding ImageWorkflowChooserChevronText}\"") >= 2);
        Assert.Contains("LessThan", converter);
        Assert.Contains("GreaterOrEqual", converter);
    }

    [Fact]
    public void ActivityLogHeaders_UseResponsiveSharedWideAndNarrowStructure()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var imageLog = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageScaffoldLog\"",
            "AutomationProperties.AutomationId=\"ImageActivityLogText\"");
        var videoLog = ExtractSourceRange(
            xaml,
            "x:Name=\"VideoActivityLogCard\"",
            "Text=\"{Binding ActivityLogPanelText, Mode=OneWay}\"");
        var logButtonStyle = ExtractSourceRange(
            xaml,
            "x:Key=\"ResponsiveLogHeaderButtonStyle\"",
            "x:Key=\"WizardStepPillStyle\"");

        Assert.Contains("BasedOn=\"{StaticResource SecondaryButtonStyle}\"", logButtonStyle);
        Assert.Contains("<Setter Property=\"MinWidth\" Value=\"0\" />", logButtonStyle);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Stretch\" />", logButtonStyle);

        Assert.Contains("AutomationProperties.AutomationId=\"ImageActivityLogResponsiveHeader\"", imageLog);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageActivityLogWideHeader\"", imageLog);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageActivityLogNarrowHeader\"", imageLog);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageActivityLogNarrowButtons\"", imageLog);
        Assert.Contains("<UniformGrid Columns=\"2\"", imageLog);
        Assert.Contains("AutomationProperties.AutomationId=\"ViewImageActivityLogButton\"", imageLog);
        Assert.Contains("AutomationProperties.AutomationId=\"ViewImageActivityLogButtonNarrow\"", imageLog);
        Assert.Contains("AutomationProperties.AutomationId=\"ClearImageLogButton\"", imageLog);
        Assert.Contains("AutomationProperties.AutomationId=\"ClearImageLogButtonNarrow\"", imageLog);
        Assert.Contains("Command=\"{Binding ViewImageActivityLogCommand}\"", imageLog);
        Assert.Contains("Command=\"{Binding ClearImageLogCommand}\"", imageLog);
        Assert.True(IndexOf(imageLog, "AutomationProperties.AutomationId=\"ImageActivityLogWideHeader\"") <
            IndexOf(imageLog, "AutomationProperties.AutomationId=\"ImageActivityLogNarrowHeader\""));

        Assert.Contains("AutomationProperties.AutomationId=\"VideoActivityLogResponsiveHeader\"", videoLog);
        Assert.Contains("AutomationProperties.AutomationId=\"VideoActivityLogWideHeader\"", videoLog);
        Assert.Contains("AutomationProperties.AutomationId=\"VideoActivityLogNarrowHeader\"", videoLog);
        Assert.Contains("AutomationProperties.AutomationId=\"VideoActivityLogNarrowButtons\"", videoLog);
        Assert.Contains("<UniformGrid Columns=\"2\"", videoLog);
        Assert.Contains("AutomationProperties.AutomationId=\"ViewActivityLogButton\"", videoLog);
        Assert.Contains("AutomationProperties.AutomationId=\"ViewActivityLogButtonNarrow\"", videoLog);
        Assert.Contains("AutomationProperties.AutomationId=\"ClearActivityLogButton\"", videoLog);
        Assert.Contains("AutomationProperties.AutomationId=\"ClearActivityLogButtonNarrow\"", videoLog);
        Assert.Contains("Command=\"{Binding ViewActivityLogCommand}\"", videoLog);
        Assert.Contains("Command=\"{Binding ClearLogsCommand}\"", videoLog);
    }

    [Fact]
    public void ImageSetupScaffolds_KeepParallaxAndStereoSetupOnlyLayouts()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var parallaxSetup = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageParallaxScaffold\"",
            "AutomationProperties.AutomationId=\"ImageStereoScaffold\"");
        var stereoSetup = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageStereoScaffold\"",
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"");
        var parallaxNarrow = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageParallaxSetupNarrowLayout\"",
            "AutomationProperties.AutomationId=\"ImageStereoScaffold\"");
        var parallaxWide = ExtractSourceRange(
            parallaxSetup,
            "AutomationProperties.AutomationId=\"ImageParallaxSetupWideLayout\"",
            "AutomationProperties.AutomationId=\"ImageParallaxSetupNarrowLayout\"");
        var stereoNarrow = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageStereoSetupNarrowLayout\"",
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"");
        var stereoWide = ExtractSourceRange(
            stereoSetup,
            "AutomationProperties.AutomationId=\"ImageStereoSetupWideLayout\"",
            "AutomationProperties.AutomationId=\"ImageStereoSetupNarrowLayout\"");

        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxSetupWideLayout\"", parallaxSetup);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxSetupNarrowLayout\"", parallaxSetup);
        Assert.Contains("ConverterParameter=GreaterOrEqual:620", parallaxSetup);
        Assert.Contains("ConverterParameter=LessThan:620", parallaxSetup);
        Assert.Contains("<StackPanel AutomationProperties.AutomationId=\"ImageParallaxSetupWideLayout\"", parallaxSetup);
        Assert.DoesNotContain("Grid.Column=\"2\"", parallaxWide);
        Assert.DoesNotContain("<ColumnDefinition Width=\"14\"", parallaxWide);
        Assert.DoesNotContain("<ColumnDefinition Width=\"1*\"", parallaxWide);
        Assert.True(IndexOf(parallaxWide, "Text=\"{Binding ImageParameterPanelTitleText}\"") <
            IndexOf(parallaxWide, "Text=\"{Binding ImageQuickSummaryTitleText}\""));
        Assert.True(IndexOf(parallaxNarrow, "Text=\"{Binding ImageParameterPanelTitleText}\"") <
            IndexOf(parallaxNarrow, "Text=\"{Binding ImageQuickSummaryTitleText}\""));
        Assert.DoesNotContain("Text=\"{Binding ImageSourcePanelTitleText}\"", parallaxSetup);
        Assert.DoesNotContain("Text=\"{Binding ImageDepthPanelTitleText}\"", parallaxSetup);
        Assert.DoesNotContain("Text=\"{Binding ImageDepthMapGenerationText}\"", parallaxSetup);
        Assert.DoesNotContain("Text=\"{Binding ImageParallaxPreviewTitleText}\"", parallaxSetup);
        Assert.DoesNotContain("Source=\"{Binding SelectedImagePath}\"", parallaxSetup);
        Assert.DoesNotContain("Source=\"{Binding ImageParallaxPreviewImagePath}\"", parallaxSetup);
        Assert.Contains("ItemsSource=\"{Binding ParallaxDepthIntensityOptions}\"", parallaxSetup);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxModelSelector\"", parallaxSetup);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxModelSelectorNarrow\"", parallaxSetup);
        Assert.Contains("Text=\"{Binding ImageParallaxQualityGuidanceText}\"", parallaxSetup);
        Assert.Contains("Text=\"{Binding ImageParallaxModelGuidanceText}\"", parallaxSetup);
        Assert.Contains("Text=\"{Binding ImageExpectedOutputFileText}\"", parallaxSetup);
        Assert.Contains("Text=\"{Binding ImageSaveLocationText}\"", parallaxSetup);

        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoSetupWideLayout\"", stereoSetup);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoSetupNarrowLayout\"", stereoSetup);
        Assert.Contains("ConverterParameter=GreaterOrEqual:620", stereoSetup);
        Assert.Contains("ConverterParameter=LessThan:620", stereoSetup);
        Assert.Contains("<StackPanel AutomationProperties.AutomationId=\"ImageStereoSetupWideLayout\"", stereoSetup);
        Assert.DoesNotContain("Grid.Column=\"2\"", stereoWide);
        Assert.DoesNotContain("<ColumnDefinition Width=\"14\"", stereoWide);
        Assert.DoesNotContain("<ColumnDefinition Width=\"1*\"", stereoWide);
        Assert.True(IndexOf(stereoWide, "Text=\"{Binding ImageStereoControlsTitleText}\"") <
            IndexOf(stereoWide, "Text=\"{Binding ImageStereoReadinessSummaryTitleText}\""));
        Assert.True(IndexOf(stereoNarrow, "Text=\"{Binding ImageStereoControlsTitleText}\"") <
            IndexOf(stereoNarrow, "Text=\"{Binding ImageStereoReadinessSummaryTitleText}\""));
        Assert.DoesNotContain("Text=\"{Binding ImageStereoPreviewTitleText}\"", stereoSetup);
        Assert.DoesNotContain("Source=\"{Binding SelectedImagePath}\"", stereoSetup);
        Assert.DoesNotContain("Text=\"{Binding ImageComparisonTitleText}\"", stereoSetup);
        Assert.Contains("Text=\"{Binding ImageStereoControlsTitleText}\"", stereoSetup);
        Assert.Contains("SelectedItem=\"{Binding SelectedStereoOutputFormatOption,", stereoSetup);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoReadinessSummaryCard\"", stereoSetup);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageModelSelector\"", stereoSetup);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageModelSelectorNarrow\"", stereoSetup);
        Assert.DoesNotContain("Canvas.", parallaxSetup);
        Assert.DoesNotContain("Canvas.", stereoSetup);
        Assert.DoesNotContain("Margin=\"-", parallaxSetup);
        Assert.DoesNotContain("Margin=\"-", stereoSetup);
        Assert.DoesNotContain("RenderTransform", parallaxSetup);
        Assert.DoesNotContain("RenderTransform", stereoSetup);
    }

    [Fact]
    public void DescriptiveTextWrapsWhileLogsAndPathsKeepTechnicalBehavior()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var sectionTitleStyle = ExtractSourceRange(
            xaml,
            "x:Key=\"SectionTitleStyle\"",
            "x:Key=\"MutedTextStyle\"");
        var mutedTextStyle = ExtractSourceRange(
            xaml,
            "x:Key=\"MutedTextStyle\"",
            "x:Key=\"TableHeaderBorderStyle\"");
        var home = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"HomeSection\"",
            "AutomationProperties.AutomationId=\"ImageConversionSection\"");
        var settings = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"SettingsModal\"",
            "x:Name=\"ModelInventoryModalHorizontalViewport\"");
        var imageSetup = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageSetupStepContent\"",
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"");
        var imageLog = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageActivityLogText\"",
            "</TextBox>");

        Assert.Contains("<Setter Property=\"TextWrapping\" Value=\"Wrap\" />", sectionTitleStyle);
        Assert.Contains("<Setter Property=\"TextWrapping\" Value=\"Wrap\" />", mutedTextStyle);
        Assert.Contains("Style=\"{StaticResource MutedTextStyle}\"", home);
        Assert.Contains("Style=\"{StaticResource MutedTextStyle}\"", settings);
        Assert.Contains("TextWrapping=\"Wrap\"", imageSetup);
        Assert.Contains("TextWrapping=\"NoWrap\"", imageLog);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", imageLog);
        Assert.Contains("FontFamily=\"Consolas\"", imageLog);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml);
    }

    [Fact]
    public void ImageOrientationState_IsDerivedFromAnalyzedMetadata()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("public bool IsSelectedImageVertical", source);
        Assert.Contains("_selectedImageMetadata is { } metadata", source);
        Assert.Contains("metadata.Height > metadata.Width", source);
        Assert.Contains("!IsImageNearlySquare(metadata.Width, metadata.Height)", source);
        Assert.Contains("public bool IsSelectedImageHorizontalOrSquare => !IsSelectedImageVertical;", source);
        Assert.Contains("private static bool IsImageNearlySquare(int width, int height)", source);
        Assert.Contains("sideDelta <= largerSide * 0.03d", source);
        Assert.Contains("public Visibility ImageParallaxPreviewExportVerticalLayoutVisibility", source);
        Assert.Contains("public Visibility ImageParallaxPreviewExportWideLayoutVisibility", source);
        Assert.Contains("public Visibility ImageStereoPreviewExportVerticalLayoutVisibility", source);
        Assert.Contains("public Visibility ImageStereoPreviewExportWideLayoutVisibility", source);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxPreviewExportVerticalLayout\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxPreviewExportWideLayout\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoPreviewExportVerticalLayout\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoPreviewExportWideLayout\"", xaml);
        Assert.Contains("Visibility=\"{Binding ImageParallaxPreviewExportVerticalLayoutVisibility}\"", xaml);
        Assert.Contains("Visibility=\"{Binding ImageParallaxPreviewExportWideLayoutVisibility}\"", xaml);
        Assert.Contains("Visibility=\"{Binding ImageStereoPreviewExportVerticalLayoutVisibility}\"", xaml);
        Assert.Contains("Visibility=\"{Binding ImageStereoPreviewExportWideLayoutVisibility}\"", xaml);
    }

    [Fact]
    public void ImageWidePreviewExportLayouts_PutPreviewAboveOptionsAndSummaryRow()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var parallaxWideLayout = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageParallaxPreviewExportWideLayout\"",
            "AutomationProperties.AutomationId=\"ImageStereoResultScaffold\"");
        var stereoWideLayout = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageStereoPreviewExportWideLayout\"",
            "AutomationProperties.AutomationId=\"ImageWizardFooter\"");

        Assert.True(IndexOf(parallaxWideLayout, "Text=\"{Binding ImageResultParallaxTitleText}\"") <
            IndexOf(parallaxWideLayout, "Source=\"{Binding ImageParallaxPreviewImagePath}\""));
        Assert.Contains("Visibility=\"{Binding ImageExportProgressVisibility}\"", parallaxWideLayout);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageParallaxWideExportOptionsSummaryRow\"", parallaxWideLayout);
        Assert.DoesNotContain("Text=\"{Binding ImageExportOptionsTitleText}\"", parallaxWideLayout);
        Assert.DoesNotContain("Text=\"{Binding ImageResultSummaryTitleText}\"", parallaxWideLayout);
        Assert.True(IndexOf(stereoWideLayout, "Text=\"{Binding ImageStereoResultTitleText}\"") <
            IndexOf(stereoWideLayout, "Source=\"{Binding ImageStereoPreviewImagePath}\""));
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageStereoWideExportOptionsSummaryRow\"", stereoWideLayout);
        Assert.DoesNotContain("Text=\"{Binding ImageGeneratedFilesTitleText}\"", stereoWideLayout);
        Assert.DoesNotContain("Text=\"{Binding ImageOutputPanelTitleText}\"", stereoWideLayout);
        Assert.DoesNotContain("StartImageConversionCommand", source);
        Assert.DoesNotContain("RunImageConversionCommand", source);
        Assert.DoesNotContain("GenerateImagePreviewCommand", source);
        Assert.DoesNotContain("ExportImageCommand", source);
        Assert.DoesNotContain("StartImageExportCommand", source);
    }

    [Fact]
    public void ImageStereoResultExportLayouts_AreOrientationAwareAndStructurallyComplete()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var stereoResult = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageStereoResultScaffold\"",
            "AutomationProperties.AutomationId=\"ImageWizardFooter\"");
        var verticalLayout = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageStereoPreviewExportVerticalLayout\"",
            "AutomationProperties.AutomationId=\"ImageStereoPreviewExportWideLayout\"");
        var wideLayout = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageStereoPreviewExportWideLayout\"",
            "AutomationProperties.AutomationId=\"ImageWizardFooter\"");

        Assert.Equal(1, CountOccurrences(xaml, "AutomationProperties.AutomationId=\"ImageStereoScaffold\""));
        Assert.Equal(1, CountOccurrences(xaml, "AutomationProperties.AutomationId=\"ImageStereoResultScaffold\""));
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoLegacyScaffold\"", xaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageStereoLegacyResultScaffold\"", xaml);

        Assert.Contains("Visibility=\"{Binding ImageStereoPreviewExportVerticalLayoutVisibility}\"", verticalLayout);
        Assert.Contains("Source=\"{Binding ImageStereoPreviewImagePath}\"", verticalLayout);
        Assert.Contains("Text=\"{Binding SelectedStereoOutputFormatDisplayText}\"", verticalLayout);
        Assert.Contains("Visibility=\"{Binding ImageExportProgressVisibility}\"", verticalLayout);
        Assert.Contains("Text=\"{Binding ImageExportOverlayText}\"", verticalLayout);
        Assert.DoesNotContain("Text=\"{Binding ImageExportProgressText}\"", verticalLayout);
        Assert.Contains("Value=\"{Binding ImageExportProgressPercent, Mode=OneWay}\"", verticalLayout);
        Assert.DoesNotContain("Text=\"SBS\"", verticalLayout);
        Assert.DoesNotContain("Text=\"TAB\"", verticalLayout);
        Assert.DoesNotContain("Text=\"Anaglyph\"", verticalLayout);
        Assert.DoesNotContain("Text=\"L / R\"", verticalLayout);
        Assert.DoesNotContain("Text=\"{Binding ImageGeneratedFilesText}\"", verticalLayout);
        Assert.DoesNotContain("Text=\"{Binding ImageGeneratedFilesTitleText}\"", verticalLayout);
        Assert.DoesNotContain("Text=\"{Binding ImageOutputPanelTitleText}\"", verticalLayout);

        Assert.Contains("Visibility=\"{Binding ImageStereoPreviewExportWideLayoutVisibility}\"", wideLayout);
        Assert.Contains("Source=\"{Binding ImageStereoPreviewImagePath}\"", wideLayout);
        Assert.Contains("Text=\"{Binding SelectedStereoOutputFormatDisplayText}\"", wideLayout);
        Assert.Contains("Visibility=\"{Binding ImageExportProgressVisibility}\"", wideLayout);
        Assert.Contains("Text=\"{Binding ImageExportOverlayText}\"", wideLayout);
        Assert.DoesNotContain("Text=\"{Binding ImageExportProgressText}\"", wideLayout);
        Assert.Contains("Value=\"{Binding ImageExportProgressPercent, Mode=OneWay}\"", wideLayout);
        Assert.True(IndexOf(wideLayout, "Text=\"{Binding ImageStereoResultTitleText}\"") <
            IndexOf(wideLayout, "Source=\"{Binding ImageStereoPreviewImagePath}\""));
        Assert.DoesNotContain("Text=\"SBS\"", wideLayout);
        Assert.DoesNotContain("Text=\"TAB\"", wideLayout);
        Assert.DoesNotContain("Text=\"Anaglyph\"", wideLayout);
        Assert.DoesNotContain("Text=\"L / R\"", wideLayout);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageStereoWideExportOptionsSummaryRow\"", wideLayout);
        Assert.DoesNotContain("Text=\"{Binding ImageGeneratedFilesText}\"", wideLayout);
        Assert.DoesNotContain("ExportImageCommand", source);
        Assert.DoesNotContain("StartImageExportCommand", source);
    }

    [Fact]
    public void ImageRightColumn_LogOnlyBeforeContinueAndLogActionsRemain()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var rightPanel = ExtractSourceRange(
            xaml,
            "<RowDefinition Height=\"{Binding ImagePreviewExportStatusRowHeight}\" />",
            "AutomationProperties.AutomationId=\"ImageConversionLegacyScaffoldHidden\"");
        var copyFullLogMethod = ExtractSourceRange(
            source,
            "private void CopyFullLog()",
            "private void CopyPreviewLog()");

        Assert.Contains("AutomationProperties.AutomationId=\"ImageOutputPanelCard\"", rightPanel);
        Assert.Contains("Visibility=\"{Binding ImagePreviewExportStatusCardVisibility}\"", rightPanel);
        Assert.Contains("public Visibility ImagePreviewExportStatusCardVisibility", source);
        Assert.Contains("SelectedImageConversionStep != ImageConversionStep.ModeAndSource", source);
        Assert.Contains("_hasEnteredImagePreviewExportStage", source);
        Assert.Contains("HasCurrentImageConversionOutput", source);
        Assert.Contains("? Visibility.Visible", source);
        Assert.Contains("? GridLength.Auto", source);
        Assert.Contains("Text=\"{Binding ImageOutputPanelTitleText}\"", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"ConvertImageButton\"", rightPanel);
        Assert.Contains("Command=\"{Binding ConvertImageCommand}\"", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"OpenImageOutputFolderButton\"", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"NewImageConversionButton\"", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageScaffoldLog\"", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"ViewImageActivityLogButton\"", rightPanel);
        Assert.Contains("Command=\"{Binding ViewImageActivityLogCommand}\"", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"ClearImageLogButton\"", rightPanel);
        Assert.Contains("Command=\"{Binding ClearImageLogCommand}\"", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageActivityLogText\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImageActivityLogText, Mode=OneWay}\"", rightPanel);
        Assert.Contains("CreateFullImageActivityLogText()", copyFullLogMethod);
        Assert.Contains("image activity log", copyFullLogMethod);
        Assert.DoesNotContain("Text=\"{Binding SelectedImageFileName}\"", rightPanel);
        Assert.DoesNotContain("Text=\"{Binding ImageMetadataSummaryText}\"", rightPanel);
    }

    [Fact]
    public void ImageFooter_UsesSharedVideoFooterStylesAndSizing()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var imageSection = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageConversionSection\"",
            "AutomationProperties.AutomationId=\"ImageConversionLegacyScaffoldHidden\"");
        var imageFooter = ExtractSourceRange(
            imageSection,
            "AutomationProperties.AutomationId=\"ImageWizardFooter\"",
            "AutomationProperties.AutomationId=\"ImageOutputPanelCard\"");

        Assert.Contains("Style=\"{StaticResource WizardFooterSecondaryButtonStyle}\"", imageFooter);
        Assert.Contains("Style=\"{StaticResource WizardFooterButtonStyle}\"", imageFooter);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageWizardBackButton\"", imageFooter);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageWizardNextButton\"", imageFooter);
        Assert.Contains("MinWidth=\"110\"", imageFooter);
        Assert.Contains("AutomationProperties.AutomationId=\"ContinueWithImageConversionButton\"", imageFooter);
        Assert.Contains("MinWidth=\"220\"", imageFooter);
        Assert.Contains("HorizontalAlignment=\"Right\"", imageFooter);
        Assert.DoesNotContain("HorizontalAlignment=\"Stretch\"", imageFooter);
        Assert.DoesNotContain("MinHeight=", imageFooter);
        Assert.DoesNotContain("Style=\"{StaticResource PrimaryCtaButtonStyle}\"", imageFooter);
    }

    [Fact]
    public void ImageStepper_UsesVideoCompletedCheckPattern()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var imageStepper = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageConversionStepper\"",
            "<ScrollViewer Grid.Row=\"1\"");
        var videoStepper = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"MainWorkflowWizard\"",
            "<ScrollViewer Grid.Row=\"1\"");
        var imageStepStateMethod = ExtractSourceRange(
            source,
            "private string ImageStepState(ImageConversionStep step)",
            "private void OpenSettings()");
        var videoStepStateMethod = ExtractSourceRange(
            source,
            "private string GetWizardStepState(int stepIndex)",
            "private void RaiseSystemStatusPropertiesChanged()");

        Assert.Contains("Text=\"{Binding ImageModeSourceStepMarkerText}\"", imageStepper);
        Assert.Contains("Text=\"{Binding ImageSetupStepMarkerText}\"", imageStepper);
        Assert.Contains("Text=\"{Binding ImagePreviewExportStepMarkerText}\"", imageStepper);
        Assert.Contains("Text=\"{Binding SourceAndAnalysisStepMarkerText}\"", videoStepper);
        Assert.Contains("Text=\"{Binding ThreeDSetupStepMarkerText}\"", videoStepper);
        Assert.Contains("public string ImageModeSourceStepMarkerText => ImageStepMarkerText(ImageConversionStep.ModeAndSource, \"1\");", source);
        Assert.Contains("public string ImageSetupStepMarkerText => ImageStepMarkerText(ImageConversionStep.Setup, \"2\");", source);
        Assert.Contains("private string ImageStepMarkerText(ImageConversionStep step, string pendingText) =>", source);
        Assert.Contains("ImageStepState(step) == \"Completed\" ? \"\\u2713\" : pendingText;", source);
        Assert.Contains("return \"Completed\";", videoStepStateMethod);
        Assert.Contains("_ => \"Locked\"", videoStepStateMethod);
        Assert.Contains("=> \"Locked\"", imageStepStateMethod);
    }

    [Fact]
    public void ImageStepper_CompletionAndResetRulesAreStateDriven()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var selectImageMethod = ExtractSourceRange(
            source,
            "private void TrySelectImage(string path)",
            "private void AnalyzeImage()");
        var imageStepStateMethod = ExtractSourceRange(
            source,
            "private string ImageStepState(ImageConversionStep step)",
            "private void OpenSettings()");
        var setupChangedMethod = ExtractSourceRange(
            source,
            "private void ApplyImageSetupChanged(",
            "private void ResetImageSetupState()");

        Assert.Contains("SelectedImageConversionStep > ImageConversionStep.ModeAndSource && HasImageMetadata => \"Completed\"", imageStepStateMethod);
        Assert.Contains("SelectedImageConversionStep > ImageConversionStep.Setup && IsImageSetupValid => \"Completed\"", imageStepStateMethod);
        Assert.Contains("ImageConversionStep.Setup when !CanOpenImageSetupStep => \"Locked\"", imageStepStateMethod);
        Assert.Contains("ImageConversionStep.PreviewAndExport when !CanOpenImagePreviewExportStep => \"Locked\"", imageStepStateMethod);
        Assert.Contains("_selectedImageMetadata = null;", selectImageMethod);
        Assert.Contains("ResetImageSetupState();", selectImageMethod);
        Assert.Contains("SelectedImageConversionStep = ImageConversionStep.ModeAndSource;", selectImageMethod);
        Assert.Contains("_hasEnteredImagePreviewExportStage = false;", selectImageMethod);
        Assert.Contains("_hasEnteredImagePreviewExportStage = false;", setupChangedMethod);
    }

    private static string ReadRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(relativeParts)}.");
    }

    private static string ExtractSourceRange(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);

        return source[start..end];
    }

    private static int IndexOf(string source, string value)
    {
        var index = source.IndexOf(value, StringComparison.Ordinal);

        Assert.True(index >= 0);

        return index;
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
