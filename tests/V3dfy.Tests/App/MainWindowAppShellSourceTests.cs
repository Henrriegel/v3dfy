namespace V3dfy.Tests.App;

public sealed class MainWindowAppShellSourceTests
{
    [Fact]
    public void Sidebar_ContainsBrandNavigationAndBottomSettings()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var codeBehind = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml.cs");
        var sidebar = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"AppSidebar\"",
            "AutomationProperties.AutomationId=\"HomeSection\"");
        var brand = ExtractSourceRange(
            sidebar,
            "AutomationProperties.AutomationId=\"SidebarBrand\"",
            "AutomationProperties.AutomationId=\"SidebarNavigation\"");
        var bottomActions = ExtractSourceRange(
            sidebar,
            "AutomationProperties.AutomationId=\"SidebarBottomActions\"",
            "</Border>");

        Assert.Contains("x:Name=\"SidebarColumn\"", xaml);
        Assert.Contains("MinWidth=\"64\"", xaml);
        Assert.Contains("MouseEnter=\"OnSidebarMouseEnter\"", sidebar);
        Assert.Contains("MouseLeave=\"OnSidebarMouseLeave\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarBrand\"", sidebar);
        Assert.Contains("v3dfy-icon-left-square-transparent.png", sidebar);
        Assert.Contains("HorizontalAlignment=\"{Binding SidebarNavContentHorizontalAlignment}\"", brand);
        Assert.Contains("Orientation=\"Horizontal\"", brand);
        Assert.Contains("HorizontalAlignment=\"Center\"", brand);
        Assert.Contains("Text=\"{Binding AppTitle}\"", sidebar);
        Assert.Contains("Text=\"{Binding ShellTaglineText}\"", sidebar);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SidebarToggleButton\"", sidebar);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SidebarToggleStripButton\"", brand);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarToggleStripButton\"", bottomActions);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarSettingsButton\"", bottomActions);
        Assert.True(
            bottomActions.IndexOf("AutomationProperties.AutomationId=\"SidebarToggleStripButton\"", StringComparison.Ordinal) <
            bottomActions.IndexOf("AutomationProperties.AutomationId=\"SidebarSettingsButton\"", StringComparison.Ordinal));
        Assert.Contains("Command=\"{Binding ToggleSidebarCommand}\"", bottomActions);
        Assert.Contains("Style=\"{StaticResource SidebarToggleStripButtonStyle}\"", bottomActions);
        Assert.Contains("Text=\"{Binding SidebarToggleGlyphText}\"", bottomActions);
        Assert.Contains("Text=\"{Binding SidebarToggleText}\"", bottomActions);
        Assert.Contains("ToolTip=\"{Binding SidebarToggleToolTipText}\"", bottomActions);
        Assert.Contains("Visibility=\"{Binding SidebarExpandedContentVisibility}\"", sidebar);
        Assert.Contains("HorizontalAlignment=\"{Binding SidebarNavContentHorizontalAlignment}\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarHomeButton\"", sidebar);
        Assert.Contains("Text=\"{Binding HomeNavigationText}\"", sidebar);
        Assert.Contains("Command=\"{Binding SelectHomeSectionCommand}\"", sidebar);
        Assert.Contains("Tag=\"{Binding IsHomeSectionSelected}\"", sidebar);
        Assert.Contains("ToolTip=\"{Binding HomeNavigationText}\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarImageConversionButton\"", sidebar);
        Assert.Contains("Text=\"{Binding ImageConversionNavigationText}\"", sidebar);
        Assert.Contains("Command=\"{Binding SelectImageConversionSectionCommand}\"", sidebar);
        Assert.Contains("Tag=\"{Binding IsImageConversionSectionSelected}\"", sidebar);
        Assert.Contains("ToolTip=\"{Binding ImageConversionNavigationText}\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarVideoConversionButton\"", sidebar);
        Assert.Contains("Text=\"{Binding VideoConversionNavigationText}\"", sidebar);
        Assert.Contains("Command=\"{Binding SelectVideoConversionSectionCommand}\"", sidebar);
        Assert.Contains("Tag=\"{Binding IsVideoConversionSectionSelected}\"", sidebar);
        Assert.Contains("ToolTip=\"{Binding VideoConversionNavigationText}\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarBottomActions\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarSettingsButton\"", sidebar);
        Assert.Contains("Command=\"{Binding OpenSettingsCommand}\"", sidebar);
        Assert.Contains("Text=\"{Binding SettingsText}\"", sidebar);
        Assert.True(CountOccurrences(sidebar, "Style=\"{StaticResource ShellSidebarIconTextStyle}\"") >= 5);
        Assert.DoesNotContain("TextBlock Width=\"24\"", sidebar);
        Assert.Contains("Style=\"{StaticResource ShellSidebarNavButtonStyle}\"", sidebar);
        var sidebarIconStyle = ExtractSourceRange(
            xaml,
            "x:Key=\"ShellSidebarIconTextStyle\"",
            "x:Key=\"SettingsMenuListBoxItemStyle\"");
        Assert.Contains("<Setter Property=\"Width\" Value=\"28\" />", sidebarIconStyle);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Center\" />", sidebarIconStyle);
        Assert.Contains("<Setter Property=\"VerticalAlignment\" Value=\"Center\" />", sidebarIconStyle);
        Assert.Contains("<Setter Property=\"TextAlignment\" Value=\"Center\" />", sidebarIconStyle);
        Assert.Contains("<Setter Property=\"FontSize\" Value=\"18\" />", sidebarIconStyle);
        Assert.Contains("Value=\"{DynamicResource AccentBrush}\"", ExtractSourceRange(
            xaml,
            "x:Key=\"ShellSidebarNavButtonStyle\"",
            "x:Key=\"SettingsMenuListBoxItemStyle\""));
        Assert.Contains("public string ShellTaglineText => Text(", source);
        Assert.Contains("private bool _isSidebarPinnedExpanded = true;", source);
        Assert.Contains("private bool _isSidebarHoverExpanded;", source);
        Assert.Contains("public bool IsSidebarPinnedExpanded", source);
        Assert.Contains("public bool IsSidebarHoverExpanded", source);
        Assert.Contains("public bool IsSidebarEffectivelyExpanded", source);
        Assert.Contains("public double SidebarTargetWidth", source);
        Assert.Contains("public GridLength SidebarColumnWidth", source);
        Assert.Contains("public double SidebarExpandedWidth => 208d;", source);
        Assert.Contains("public double SidebarCollapsedWidth => 64d;", source);
        Assert.Contains("new Thickness(6d, 18d, 6d, 18d)", source);
        Assert.Contains("public void ExpandSidebarForHover()", source);
        Assert.Contains("public void CollapseSidebarAfterHover()", source);
        Assert.Contains("SidebarColumn.BeginAnimation", codeBehind);
        Assert.Contains("ColumnDefinition.WidthProperty", codeBehind);
        Assert.Contains("private sealed class GridLengthAnimation", codeBehind);
        Assert.Contains("TimeSpan.FromMilliseconds(150)", codeBehind);
        Assert.DoesNotContain(">>", sidebar);
        Assert.DoesNotContain("<<", sidebar);
    }

    [Fact]
    public void MainContent_RemovesOldHeaderBrandAndGear()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var mainContent = ExtractSourceRange(
            xaml,
            "<Grid Grid.Column=\"1\"",
            "AutomationProperties.AutomationId=\"LogCopyNotification\"");

        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SettingsButton\"", xaml);
        Assert.DoesNotContain("<Grid Margin=\"0,0,0,18\">", xaml);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SettingsButton\"", mainContent);
        Assert.DoesNotContain("Text=\"{Binding AppTitle}\"", mainContent);
        Assert.DoesNotContain("Source=\"Assets/Branding/v3dfy-icon-left-square-transparent.png\"", mainContent);
    }

    [Fact]
    public void HomeSection_ProvidesNonEmptyModuleOverview()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var home = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"HomeSection\"",
            "AutomationProperties.AutomationId=\"ImageConversionSection\"");

        Assert.Contains("Visibility=\"{Binding HomeSectionVisibility}\"", home);
        Assert.Contains("Text=\"{Binding HomeTitleText}\"", home);
        Assert.Contains("Text=\"{Binding HomeDescriptionText}\"", home);
        Assert.Contains("AutomationProperties.AutomationId=\"HomeQuickCards\"", home);
        Assert.Contains("AutomationProperties.AutomationId=\"HomeOverviewPanel\"", home);
        Assert.Contains("Text=\"{Binding HomeStatusSummaryText}\"", home);
        Assert.Contains("AutomationProperties.AutomationId=\"HomeVideoConversionCard\"", home);
        Assert.Contains("Command=\"{Binding SelectVideoConversionSectionCommand}\"", home);
        Assert.Contains("AutomationProperties.AutomationId=\"HomeImageConversionCard\"", home);
        Assert.Contains("Command=\"{Binding SelectImageConversionSectionCommand}\"", home);
        Assert.Contains("AutomationProperties.AutomationId=\"HomeSettingsCard\"", home);
        Assert.Contains("Command=\"{Binding OpenSettingsCommand}\"", home);
        Assert.DoesNotContain("ActivityLog", home);
        Assert.DoesNotContain("ActivityLogPanelText", home);
        Assert.Contains("Text=\"{Binding HomeVideoCardTitleText}\"", home);
        Assert.Contains("Text=\"{Binding HomeImageCardTitleText}\"", home);
        Assert.Contains("Text=\"{Binding HomeSettingsCardTitleText}\"", home);
        Assert.Contains("Text=\"{Binding ReadyNowText}\"", home);
        Assert.Contains("Text=\"{Binding ComingNextText}\"", home);
        Assert.Contains("public string HomeVideoCardBodyText => Text(", source);
        Assert.Contains("Ready now", source);
        Assert.Contains("Coming next", source);
    }

    [Fact]
    public void SharedConversionModuleUiRules_ArePersistedInAgentInstructions()
    {
        var instructions = ReadRepoFile("docs", "development-workflow.md");

        Assert.Contains("Main conversion module UI rules", instructions);
        Assert.Contains("source/analyze step first", instructions);
        Assert.Contains("setup step second", instructions);
        Assert.Contains("conversion/preview plan step third", instructions);
        Assert.Contains("fixed footer buttons outside scrollable content", instructions);
        Assert.Contains("standardized Back/Next/Continue button sizing", instructions);
        Assert.Contains("right panel behavior modeled after Video conversion", instructions);
        Assert.Contains("module activity logs use the same visual pattern", instructions);
        Assert.Contains("module logs provide View log, Copy full log, and Clear actions", instructions);
        Assert.Contains("summary/status cards appear only after the state they summarize is valid", instructions);
        Assert.Contains("avoid duplicating the same data on the left and right panels", instructions);
        Assert.Contains("do not invent a separate Image conversion flow", instructions);
    }

    [Fact]
    public void ImageConversionSection_ProvidesFunctionalWorkflowFoundation()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var codeBehind = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml.cs");
        var imageSection = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageConversionSection\"",
            "AutomationProperties.AutomationId=\"ImageConversionLegacyScaffoldHidden\"");
        var hiddenLegacySection = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageConversionLegacyScaffoldHidden\"",
            "AutomationProperties.AutomationId=\"VideoConversionSection\"");
        var constructor = ExtractSourceRange(
            source,
            "public MainWindowViewModel()",
            "public string AppTitle => \"v3dfy\";");
        var selectImageMethod = ExtractSourceRange(
            source,
            "private void TrySelectImage(string path)",
            "private void AnalyzeImage()");
        var analyzeImageMethod = ExtractSourceRange(
            source,
            "private void AnalyzeImage()",
            "private static ImageMetadata ReadImageMetadata");
        var selectModeMethod = ExtractSourceRange(
            source,
            "private void SelectImageConversionMode(",
            "private void SelectImageConversionStep(");
        var selectStepMethod = ExtractSourceRange(
            source,
            "private void SelectImageConversionStep(",
            "private void MoveImageWizardBack()");
        var moveNextMethod = ExtractSourceRange(
            source,
            "private void MoveImageWizardNext()",
            "private void ContinueWithImageConversion()");
        var setupChangedMethod = ExtractSourceRange(
            source,
            "private void ApplyImageSetupChanged()",
            "private void SetImageSetupString(");
        var sourceStep = ExtractSourceRange(
            imageSection,
            "AutomationProperties.AutomationId=\"ImageModeSourceStepContent\"",
            "AutomationProperties.AutomationId=\"ImageSetupStepContent\"");
        var setupStep = ExtractSourceRange(
            imageSection,
            "AutomationProperties.AutomationId=\"ImageSetupStepContent\"",
            "AutomationProperties.AutomationId=\"ImageParallaxScaffold\"");
        var rightPanel = ExtractSourceRange(
            xaml,
            "<RowDefinition Height=\"{Binding ImagePreviewExportStatusRowHeight}\" />",
            "AutomationProperties.AutomationId=\"ImageConversionLegacyScaffoldHidden\"");
        var imageFooter = ExtractSourceRange(
            imageSection,
            "AutomationProperties.AutomationId=\"ImageWizardFooter\"",
            "AutomationProperties.AutomationId=\"ImagePreviewExportStatusCard\"");
        var copyFullLogMethod = ExtractSourceRange(
            source,
            "private void CopyFullLog()",
            "private void CopyPreviewLog()");

        Assert.Contains("Visibility=\"{Binding ImageConversionSectionVisibility}\"", imageSection);
        Assert.Contains("<ColumnDefinition Width=\"3*\" />", imageSection);
        Assert.Contains("<ColumnDefinition Width=\"2*\" />", imageSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageConversionWizard\"", imageSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageConversionStepper\"", imageSection);
        Assert.True(
            imageSection.IndexOf("AutomationProperties.AutomationId=\"ImageConversionStepper\"", StringComparison.Ordinal) <
            imageSection.IndexOf("AutomationProperties.AutomationId=\"ImageModeSourceStepContent\"", StringComparison.Ordinal));
        Assert.DoesNotContain("Text=\"{Binding ImageConversionTitleText}\"", imageSection);
        Assert.DoesNotContain("Text=\"{Binding ImageConversionIntroText}\"", imageSection);
        Assert.Contains("Style=\"{StaticResource ImageWizardStepButtonStyle}\"", imageSection);
        Assert.DoesNotContain("System.Windows.Controls.StackPanel", imageSection);
        Assert.Contains("Command=\"{Binding SelectImageModeSourceStepCommand}\"", imageSection);
        Assert.Contains("Command=\"{Binding SelectImageSetupStepCommand}\"", imageSection);
        Assert.Contains("Command=\"{Binding SelectImagePreviewExportStepCommand}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageModeSourceStepTitleText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageSetupStepTitleText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImagePreviewExportStepTitleText}\"", imageSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageModeSourceStepContent\"", sourceStep);
        Assert.Contains("Visibility=\"{Binding ImageModeSourceStepVisibility}\"", sourceStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageSourceSelectionPanel\"", sourceStep);
        Assert.Contains("Text=\"{Binding ImageSourceAnalysisTitleText}\"", sourceStep);
        Assert.Contains("Text=\"{Binding DropImageText}\"", sourceStep);
        Assert.Contains("AutomationProperties.AutomationId=\"SelectImageButton\"", sourceStep);
        Assert.Contains("Command=\"{Binding SelectImageCommand}\"", sourceStep);
        Assert.Contains("AutomationProperties.AutomationId=\"AnalyzeImageButton\"", sourceStep);
        Assert.Contains("Command=\"{Binding AnalyzeImageCommand}\"", sourceStep);
        Assert.Contains("IsEnabled=\"{Binding CanAnalyzeImage}\"", sourceStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageMetadataPanel\"", sourceStep);
        Assert.Contains("Visibility=\"{Binding ImageAnalysisResultsVisibility}\"", sourceStep);
        Assert.Contains("Text=\"{Binding ImageAnalysisTitleText}\"", sourceStep);
        Assert.Contains("Text=\"{Binding ImageMetadataWidthText}\"", sourceStep);
        Assert.Contains("Text=\"{Binding ImageMetadataHeightText}\"", sourceStep);
        Assert.Contains("Text=\"{Binding ImageMetadataAspectRatioText}\"", sourceStep);
        Assert.Contains("Text=\"{Binding ImageMetadataFormatText}\"", sourceStep);
        Assert.Contains("Text=\"{Binding ImageMetadataPixelFormatText}\"", sourceStep);
        Assert.Contains("Text=\"{Binding ImageMetadataFileSizeText}\"", sourceStep);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageConversionModeCards\"", sourceStep);
        Assert.DoesNotContain("Text=\"{Binding ImageSourcePanelTitleText}\"", sourceStep);
        Assert.DoesNotContain("Text=\"{Binding ImageDepthPanelTitleText}\"", sourceStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageSetupStepContent\"", setupStep);
        Assert.Contains("Visibility=\"{Binding ImageSetupStepVisibility}\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageConversionModeCards\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxModeCard\"", setupStep);
        Assert.Contains("Command=\"{Binding SelectImageParallaxModeCommand}\"", setupStep);
        Assert.Contains("Style=\"{StaticResource ImageWorkflowOptionCardButtonStyle}\"", setupStep);
        Assert.Contains("Text=\"{Binding ImageParallaxModeTitleText}\"", setupStep);
        Assert.Contains("Text=\"{Binding ImageParallaxModeBodyText}\"", setupStep);
        Assert.Contains("Text=\"{Binding ImageParallaxModeCardStatusText}\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoModeCard\"", setupStep);
        Assert.Contains("Command=\"{Binding SelectImageStereoModeCommand}\"", setupStep);
        Assert.Contains("Text=\"{Binding Image3DOutputModeTitleText}\"", setupStep);
        Assert.Contains("Text=\"{Binding Image3DOutputModeBodyText}\"", setupStep);
        Assert.Contains("Text=\"{Binding ImageStereoModeCardStatusText}\"", setupStep);
        Assert.DoesNotContain("StartConversionCommand", imageSection);
        Assert.DoesNotContain("GeneratePreviewCommand", imageSection);
        Assert.DoesNotContain("SelectVideoCommand", imageSection);
        Assert.DoesNotContain("ActivityLogPanelText", imageSection);
        Assert.DoesNotContain("ItemsSource=\"{Binding Logs}\"", imageSection);
        Assert.DoesNotContain("ItemsSource=\"{Binding ConversionLogs}\"", imageSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxScaffold\"", imageSection);
        Assert.Contains("Visibility=\"{Binding ImageParallaxSetupStepVisibility}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding ParallaxDepthIntensityOptions}\"", imageSection);
        Assert.Contains("SelectedItem=\"{Binding SelectedParallaxDepthIntensity}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding ParallaxMotionDirectionOptions}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding ParallaxZoomAmplitudeOptions}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding ParallaxDurationOptions}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding ParallaxSmoothingOptions}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding ParallaxLayerBehaviorOptions}\"", imageSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"", imageSection);
        Assert.Contains("Visibility=\"{Binding ImageParallaxPreviewExportStepVisibility}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageOutputPlanText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageSetupSummaryText}\"", imageSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoScaffold\"", imageSection);
        Assert.Contains("Visibility=\"{Binding ImageStereoSetupStepVisibility}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding StereoOutputFormatOptions}\"", imageSection);
        Assert.Contains("SelectedValue=\"{Binding SelectedStereoOutputFormat}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding StereoEyeSeparationOptions}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding StereoConvergenceOptions}\"", imageSection);
        Assert.Contains("IsChecked=\"{Binding ImageStereoSwapEyes}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding StereoAnaglyphModeOptions}\"", imageSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoResultScaffold\"", imageSection);
        Assert.Contains("Visibility=\"{Binding ImageStereoPreviewExportStepVisibility}\"", imageSection);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageConversionSummaryCard\"", imageSection);
        Assert.Contains("<RowDefinition Height=\"{Binding ImagePreviewExportStatusRowHeight}\" />", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"ImagePreviewExportStatusCard\"", rightPanel);
        Assert.Contains("Visibility=\"{Binding ImagePreviewExportStatusCardVisibility}\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImagePreviewExportStatusTitleText}\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImageSelectedModeSummaryText}\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImagePreviewExportStatusText}\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImagePlannedOutputFormatsText}\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImageLocalModelReadinessNoteText}\"", rightPanel);
        Assert.DoesNotContain("Text=\"{Binding SelectedImageFileName}\"", rightPanel);
        Assert.DoesNotContain("Text=\"{Binding ImageMetadataSummaryText}\"", rightPanel);
        Assert.DoesNotContain("Text=\"{Binding ImageCurrentStepSummaryText}\"", rightPanel);
        Assert.DoesNotContain("Text=\"{Binding ImageSupportedInputFormatsText}\"", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageScaffoldLog\"", rightPanel);
        Assert.Contains("ClipToBounds=\"True\"", rightPanel);
        Assert.Contains("Margin=\"{Binding ImageActivityLogCardMargin}\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImageActivityLogTitleText}\"", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"ViewImageActivityLogButton\"", rightPanel);
        Assert.Contains("Command=\"{Binding ViewImageActivityLogCommand}\"", rightPanel);
        Assert.Contains("Command=\"{Binding ClearImageLogCommand}\"", rightPanel);
        Assert.Contains("Content=\"{Binding ClearText}\"", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageActivityLogText\"", rightPanel);
        Assert.Contains("Background=\"{DynamicResource LogBackgroundBrush}\"", rightPanel);
        Assert.Contains("FontFamily=\"Consolas\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImageActivityLogText, Mode=OneWay}\"", rightPanel);
        Assert.Contains("TextWrapping=\"NoWrap\"", rightPanel);
        Assert.Contains("TechnicalLogScrollBarStyle", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageWizardBackButton\"", imageFooter);
        Assert.Contains("Style=\"{StaticResource WizardFooterSecondaryButtonStyle}\"", imageFooter);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageWizardNextButton\"", imageFooter);
        Assert.Contains("MinWidth=\"110\"", imageFooter);
        Assert.Contains("Style=\"{StaticResource WizardFooterButtonStyle}\"", imageFooter);
        Assert.Contains("AutomationProperties.AutomationId=\"ContinueWithImageConversionButton\"", imageFooter);
        Assert.Contains("MinWidth=\"220\"", imageFooter);
        Assert.Contains("HorizontalAlignment=\"Right\"", imageFooter);
        Assert.DoesNotContain("Padding=", imageFooter);
        Assert.DoesNotContain("MinHeight=", imageFooter);
        Assert.Contains("Text=\"{Binding ImageSourcePanelTitleText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageDepthPanelTitleText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageParallaxPreviewTitleText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageParameterPanelTitleText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageExportOptionsTitleText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageStereoPreviewTitleText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageStereoControlsTitleText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageGeneratedFilesTitleText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageOutputPanelTitleText}\"", imageSection);
        Assert.Contains("IsEnabled=\"False\"", imageSection);
        Assert.Contains("Visibility=\"Collapsed\"", hiddenLegacySection);
        Assert.Contains("public enum ImageConversionMode", source);
        Assert.Contains("public enum ImageConversionStep", source);
        Assert.Contains("public enum ImageStereoOutputFormat", source);
        Assert.Contains("private static readonly HashSet<string> SupportedImageExtensions", source);
        Assert.Contains("\".jpg\"", source);
        Assert.Contains("\".jpeg\"", source);
        Assert.Contains("\".png\"", source);
        Assert.Contains("\".bmp\"", source);
        Assert.Contains("\".tif\"", source);
        Assert.Contains("\".tiff\"", source);
        Assert.Contains("\".webp\"", source);
        Assert.Contains("private string? _selectedImagePath;", source);
        Assert.Contains("private ImageMetadata? _selectedImageMetadata;", source);
        Assert.Contains("private ImageConversionMode? _selectedImageConversionMode;", source);
        Assert.Contains("private ImageConversionStep _selectedImageConversionStep = ImageConversionStep.ModeAndSource;", source);
        Assert.Contains("public RelayCommand SelectImageCommand", source);
        Assert.Contains("public RelayCommand AnalyzeImageCommand", source);
        Assert.Contains("public RelayCommand ClearImageLogCommand", source);
        Assert.Contains("public RelayCommand ViewImageActivityLogCommand", source);
        Assert.Contains("private void SelectImage()", source);
        Assert.Contains("private void TrySelectImage(string path)", source);
        Assert.Contains("private void AnalyzeImage()", source);
        Assert.Contains("public void SelectDroppedImage(string path)", source);
        Assert.Contains("viewModel.SelectDroppedImage(files[0]);", codeBehind);
        Assert.DoesNotContain("ReadImageMetadata", selectImageMethod);
        Assert.Contains("_selectedImageMetadata = null;", selectImageMethod);
        Assert.Contains("ResetImageSetupState();", selectImageMethod);
        Assert.Contains("Analyze image to read metadata", selectImageMethod);
        Assert.Contains("ReadImageMetadata(SelectedImagePath)", analyzeImageMethod);
        Assert.Contains("BitmapDecoder.Create", source);
        Assert.Contains("PixelWidth", source);
        Assert.Contains("PixelHeight", source);
        Assert.Contains("FormatAspectRatio", source);
        Assert.Contains("FormatImageFileSize", source);
        Assert.Contains("public ObservableCollection<LogEntryViewModel> ImageLogs", source);
        Assert.Contains("private void AddImageLog", source);
        Assert.Contains("private void ClearImageLog()", source);
        Assert.Contains("ClearImageLogCommand = new RelayCommand(ClearImageLog, () => ImageLogs.Count > 0);", source);
        Assert.Contains("ViewImageActivityLogCommand = new RelayCommand(ViewImageActivityLog);", constructor);
        Assert.Contains("private void ViewImageActivityLog()", source);
        Assert.Contains("private string CreateFullImageActivityLogText()", source);
        Assert.Contains("ActivityLogModalKind.Image", source);
        Assert.Contains("CreateFullImageActivityLogText()", copyFullLogMethod);
        Assert.Contains("image activity log", copyFullLogMethod);
        Assert.Contains("public string ImageActivityLogText", source);
        Assert.Contains("public RelayCommand SelectImageParallaxModeCommand", source);
        Assert.Contains("public RelayCommand SelectImageStereoModeCommand", source);
        Assert.Contains("public RelayCommand ImageWizardBackCommand", source);
        Assert.Contains("public RelayCommand ImageWizardNextCommand", source);
        Assert.Contains("public RelayCommand ContinueWithImageConversionCommand", source);
        Assert.Contains("SelectImageSetupStepCommand = new RelayCommand(", constructor);
        Assert.Contains("() => CanOpenImageSetupStep", constructor);
        Assert.Contains("SelectImagePreviewExportStepCommand = new RelayCommand(", constructor);
        Assert.Contains("() => CanOpenImagePreviewExportStep", constructor);
        Assert.Contains("ImageWizardNextCommand = new RelayCommand(", constructor);
        Assert.Contains("() => CanMoveImageWizardNext", constructor);
        Assert.Contains("ContinueWithImageConversionCommand = new RelayCommand(", constructor);
        Assert.Contains("() => CanContinueWithImageConversion", constructor);
        Assert.Contains("AnalyzeImageCommand = new RelayCommand(AnalyzeImage, () => CanAnalyzeImage);", constructor);
        Assert.Contains("public bool CanAnalyzeImage => HasSelectedImage;", source);
        Assert.Contains("public bool HasSelectedImageMode => SelectedImageConversionMode is not null;", source);
        Assert.Contains("public bool HasImageWorkflowPrerequisites => HasImageMetadata && HasSelectedImageMode;", source);
        Assert.Contains("public bool CanOpenImageSetupStep => HasImageMetadata;", source);
        Assert.Contains("public bool IsImageModeSetupValid", source);
        Assert.Contains("public bool IsImageSetupValid => HasImageMetadata && HasSelectedImageMode && IsImageModeSetupValid;", source);
        Assert.Contains("public bool CanOpenImagePreviewExportStep => IsImageSetupValid;", source);
        Assert.Contains("ImageConversionStep.ModeAndSource => CanOpenImageSetupStep", source);
        Assert.Contains("ImageConversionStep.Setup => CanOpenImagePreviewExportStep", source);
        Assert.Contains("public Visibility ImageAnalysisResultsVisibility", source);
        Assert.Contains("HasImageMetadata ? Visibility.Visible : Visibility.Collapsed;", source);
        Assert.Contains("public GridLength ImagePreviewExportStatusRowHeight", source);
        Assert.Contains("_hasEnteredImagePreviewExportStage ? GridLength.Auto : new GridLength(0d);", source);
        Assert.Contains("public Thickness ImageActivityLogCardMargin", source);
        Assert.Contains("_hasEnteredImagePreviewExportStage ? new Thickness(0d, 14d, 0d, 0d) : new Thickness(0d);", source);
        Assert.Contains("public Visibility ImageSetupStepVisibility", source);
        Assert.Contains("public Visibility ImageParallaxSetupStepVisibility", source);
        Assert.Contains("public Visibility ImageStereoSetupStepVisibility", source);
        Assert.Contains("public Visibility ImagePreviewExportStatusCardVisibility", source);
        Assert.Contains("ApplyImageSetupChanged", source);
        Assert.Contains("_hasEnteredImagePreviewExportStage = false;", source);
        Assert.Contains("SelectedImageConversionStep = ImageConversionStep.ModeAndSource;", selectImageMethod);
        Assert.Contains("_hasEnteredImagePreviewExportStage = false;", selectImageMethod);
        Assert.Contains("ApplyImageSetupChanged();", selectModeMethod);
        Assert.Contains("SelectedImageConversionStep == ImageConversionStep.ModeAndSource", selectModeMethod);
        Assert.Contains("if (step == ImageConversionStep.Setup && !CanOpenImageSetupStep)", selectStepMethod);
        Assert.Contains("if (step == ImageConversionStep.PreviewAndExport && !CanOpenImagePreviewExportStep)", selectStepMethod);
        Assert.Contains("if (!CanMoveImageWizardNext)", moveNextMethod);
        Assert.Contains("_hasEnteredImagePreviewExportStage = false;", setupChangedMethod);
        Assert.Contains("ShowLogCopyNotification(", setupChangedMethod);
        Assert.Contains("2.5D Photo", source);
        Assert.Contains("Stereoscopic image", source);
        Assert.Contains("SBS, Half Top-Bottom, and Anaglyph-style", source);
        Assert.DoesNotContain("StartImageConversionCommand", source);
        Assert.DoesNotContain("RunImageConversionCommand", source);
        Assert.DoesNotContain("GenerateImagePreviewCommand", source);
        Assert.DoesNotContain("ExportImageCommand", source);
        Assert.DoesNotContain("StartImageExportCommand", source);
    }

    [Fact]
    public void ExistingVideoWorkflow_IsNestedUnderVideoConversionSection()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var videoSection = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"VideoConversionSection\"",
            "AutomationProperties.AutomationId=\"LogCopyNotification\"");

        Assert.Contains("Visibility=\"{Binding VideoConversionSectionVisibility}\"", videoSection);
        Assert.Contains("AutomationProperties.AutomationId=\"MainWorkflowWizard\"", videoSection);
        Assert.Contains("AutomationProperties.AutomationId=\"SourceAndAnalysisStepContent\"", videoSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ThreeDSetupStepContent\"", videoSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ConversionPlanStepContent\"", videoSection);
        Assert.Contains("AutomationProperties.AutomationId=\"PreviewConversionStatusCard\"", videoSection);
        Assert.Contains("AutomationProperties.AutomationId=\"GeneratePreviewPrimaryActionButton\"", videoSection);
        Assert.Contains("AutomationProperties.AutomationId=\"StartConversionButton\"", videoSection);
        Assert.Contains("Text=\"{Binding ActivityLogTitle}\"", videoSection);
        Assert.Contains("ActivityLogPanelText", videoSection);
    }

    [Fact]
    public void AppSectionSwitching_DoesNotResetVideoWorkflowState()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var constructor = ExtractSourceRange(
            source,
            "public MainWindowViewModel()",
            "public string AppTitle => \"v3dfy\";");
        var toggleMethod = ExtractSourceRange(
            source,
            "private void ToggleSidebar()",
            "private void SelectImageConversionMode(");
        var hoverMethods = ExtractSourceRange(
            source,
            "public void ExpandSidebarForHover()",
            "private void ToggleSidebar()");
        var selectMethod = ExtractSourceRange(
            source,
            "private void SelectAppSection(AppSection section)",
            "public void ExpandSidebarForHover()");
        var openSettingsMethod = ExtractSourceRange(
            source,
            "private void OpenSettings()",
            "private void OpenToolsEngineSettings()");

        Assert.Contains("private AppSection _selectedAppSection = AppSection.Home;", source);
        Assert.Contains("SelectHomeSectionCommand = new RelayCommand(() => SelectAppSection(AppSection.Home));", constructor);
        Assert.Contains("SelectImageConversionSectionCommand = new RelayCommand(() => SelectAppSection(AppSection.ImageConversion));", constructor);
        Assert.Contains("SelectVideoConversionSectionCommand = new RelayCommand(() => SelectAppSection(AppSection.VideoConversion));", constructor);
        Assert.Contains("ToggleSidebarCommand = new RelayCommand(ToggleSidebar);", constructor);
        Assert.Contains("SelectImageCommand = new RelayCommand(SelectImage);", constructor);
        Assert.Contains("SelectImageParallaxModeCommand = new RelayCommand(() => SelectImageConversionMode(ImageConversionMode.ParallaxPhoto));", constructor);
        Assert.Contains("SelectImageStereoModeCommand = new RelayCommand(() => SelectImageConversionMode(ImageConversionMode.StereoscopicImage));", constructor);
        Assert.Contains("ImageWizardBackCommand = new RelayCommand(", constructor);
        Assert.Contains("ImageWizardNextCommand = new RelayCommand(", constructor);
        Assert.Contains("ContinueWithImageConversionCommand = new RelayCommand(", constructor);
        Assert.Contains("IsSidebarExpanded = !IsSidebarExpanded;", toggleMethod);
        Assert.Contains("IsSidebarHoverExpanded = false;", toggleMethod);
        Assert.Contains("IsSidebarHoverExpanded = true;", hoverMethods);
        Assert.Contains("IsSidebarHoverExpanded = false;", hoverMethods);
        Assert.Contains("SelectedAppSection = section;", selectMethod);
        Assert.DoesNotContain("SelectedVideoPath", selectMethod);
        Assert.DoesNotContain("_workflowState", selectMethod);
        Assert.DoesNotContain("_previewState", selectMethod);
        Assert.DoesNotContain("Reset", selectMethod);
        Assert.DoesNotContain("SelectedOutputPreset", selectMethod);
        Assert.DoesNotContain("SelectedLocalModelCandidate", selectMethod);
        Assert.DoesNotContain("SelectedAppSection", openSettingsMethod);
        Assert.Contains("IsSettingsModalOpen = true;", openSettingsMethod);
    }

    private static string ReadRepoFile(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativePath]);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(relativePath)}.");
    }

    private static string ExtractSourceRange(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find start marker '{startMarker}'.");

        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find end marker '{endMarker}'.");

        return source[start..end];
    }

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}
