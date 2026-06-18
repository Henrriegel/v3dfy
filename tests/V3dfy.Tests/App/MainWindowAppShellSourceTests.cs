namespace V3dfy.Tests.App;

public sealed class MainWindowAppShellSourceTests
{
    [Fact]
    public void MainWindow_UsesWindowChromeCustomTitleBarWithNativeWindowControls()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var codeBehind = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml.cs");
        var chrome = ExtractSourceRange(
            xaml,
            "<shell:WindowChrome.WindowChrome>",
            "</shell:WindowChrome.WindowChrome>");
        var titleBarStyles = ExtractSourceRange(
            xaml,
            "x:Key=\"TitleBarWindowButtonStyle\"",
            "x:Key=\"WizardStepPillStyle\"");
        var titleBar = ExtractSourceRange(
            xaml,
            "<Grid Grid.Row=\"0\"",
            "AutomationProperties.AutomationId=\"AppSidebar\"");

        Assert.Contains("xmlns:shell=\"clr-namespace:System.Windows.Shell;assembly=PresentationFramework\"", xaml);
        Assert.Contains("WindowStyle=\"SingleBorderWindow\"", xaml);
        Assert.Contains("ResizeMode=\"CanResize\"", xaml);
        Assert.Contains("StateChanged=\"OnWindowStateChanged\"", xaml);
        Assert.Contains("<shell:WindowChrome CaptionHeight=\"40\"", chrome);
        Assert.Contains("GlassFrameThickness=\"0\"", chrome);
        Assert.Contains("ResizeBorderThickness=\"6\"", chrome);
        Assert.Contains("UseAeroCaptionButtons=\"False\"", chrome);
        Assert.DoesNotContain("WindowStyle=\"None\"", xaml);
        Assert.DoesNotContain("AllowsTransparency=\"True\"", xaml);

        Assert.Contains("x:Name=\"AppChromeRoot\"", xaml);
        Assert.Contains("<RowDefinition Height=\"40\" />", xaml);
        Assert.Contains("<RowDefinition Height=\"*\" />", xaml);
        Assert.Contains("Grid.ColumnSpan=\"2\"", titleBar);
        Assert.Contains("AutomationProperties.AutomationId=\"AppTitleBar\"", titleBar);
        Assert.Contains("AutomationProperties.AutomationId=\"AppTitleBarCaptionArea\"", titleBar);
        Assert.Contains("Background=\"{DynamicResource WindowBackgroundBrush}\"", titleBar);
        Assert.Contains("BorderBrush=\"{DynamicResource CardBorderBrush}\"", titleBar);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"AppTitleBarLeadingArea\"", titleBar);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"AppTitleBarIcon\"", titleBar);
        Assert.DoesNotContain("v3dfy-icon-left-square-transparent.png", titleBar);
        Assert.DoesNotContain("Text=\"{Binding AppTitle}\"", titleBar);
        Assert.DoesNotContain("ShellTaglineText", titleBar);
        Assert.DoesNotContain("HomeNavigationText", titleBar);
        Assert.DoesNotContain("ImageConversionNavigationText", titleBar);
        Assert.DoesNotContain("VideoConversionNavigationText", titleBar);
        Assert.DoesNotContain("Text=\"v3dfy\"", titleBar);
        Assert.DoesNotContain("<Image", titleBar);
        Assert.Equal(3, CountOccurrences(titleBar, "<Button "));

        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarMinimizeButton\"", titleBar);
        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarMaximizeRestoreButton\"", titleBar);
        Assert.Contains("AutomationProperties.AutomationId=\"TitleBarCloseButton\"", titleBar);
        Assert.True(CountOccurrences(titleBar, "shell:WindowChrome.IsHitTestVisibleInChrome=\"True\"") >= 4);
        Assert.Contains("ToolTip=\"{Binding WindowMinimizeToolTipText}\"", titleBar);
        Assert.Contains("Style=\"{StaticResource TitleBarMaximizeRestoreButtonStyle}\"", titleBar);
        Assert.Contains("Style=\"{StaticResource TitleBarCloseButtonStyle}\"", titleBar);
        Assert.Contains("Click=\"OnMinimizeWindowButtonClick\"", titleBar);
        Assert.Contains("Click=\"OnMaximizeRestoreWindowButtonClick\"", titleBar);
        Assert.Contains("Click=\"OnCloseWindowButtonClick\"", titleBar);

        Assert.Contains("x:Key=\"TitleBarMaximizeRestoreGlyphStyle\"", titleBarStyles);
        Assert.Contains("Value=\"&#xE922;\"", titleBarStyles);
        Assert.Contains("Value=\"&#xE923;\"", titleBarStyles);
        Assert.Contains("RelativeSource={RelativeSource AncestorType=Window}", titleBarStyles);
        Assert.Contains("Value=\"Maximized\"", titleBarStyles);
        Assert.Contains("Value=\"{DynamicResource ComboBoxHoverBrush}\"", titleBarStyles);
        Assert.Contains("Value=\"{DynamicResource InputBackgroundBrush}\"", titleBarStyles);
        Assert.Contains("Value=\"{DynamicResource DestructiveBrush}\"", titleBarStyles);
        Assert.Contains("Value=\"{DynamicResource DestructivePressedBrush}\"", titleBarStyles);
        Assert.Contains("Value=\"{DynamicResource PrimaryTextBrush}\"", titleBarStyles);
        Assert.Contains("Value=\"{DynamicResource ButtonForegroundBrush}\"", titleBarStyles);
        Assert.DoesNotContain("Color=\"#", titleBarStyles);
        Assert.DoesNotContain("Background=\"#", titleBar);

        Assert.Contains("private void OnMinimizeWindowButtonClick", codeBehind);
        Assert.Contains("WindowState = WindowState.Minimized;", codeBehind);
        Assert.Contains("private void OnMaximizeRestoreWindowButtonClick", codeBehind);
        Assert.Contains("WindowState == WindowState.Maximized", codeBehind);
        Assert.Contains("WindowState.Normal", codeBehind);
        Assert.Contains("private void OnCloseWindowButtonClick", codeBehind);
        Assert.Contains("Close();", codeBehind);
        Assert.Contains("private void OnWindowStateChanged", codeBehind);
        Assert.Contains("UpdateChromeContentMargin();", codeBehind);
        Assert.Contains("SystemParameters.WindowResizeBorderThickness", codeBehind);

        Assert.Contains("public string WindowMinimizeToolTipText", source);
        Assert.Contains("public string WindowMaximizeToolTipText", source);
        Assert.Contains("public string WindowRestoreToolTipText", source);
        Assert.Contains("public string WindowCloseToolTipText", source);
        Assert.Contains("OnPropertyChanged(nameof(WindowMinimizeToolTipText));", source);
        Assert.Contains("OnPropertyChanged(nameof(WindowMaximizeToolTipText));", source);
        Assert.Contains("OnPropertyChanged(nameof(WindowRestoreToolTipText));", source);
        Assert.Contains("OnPropertyChanged(nameof(WindowCloseToolTipText));", source);
    }

    [Fact]
    public void CustomChrome_KeepsSidebarContentAndModalsOutsideCaptionHitTesting()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var titleBar = ExtractSourceRange(
            xaml,
            "<Grid Grid.Row=\"0\"",
            "AutomationProperties.AutomationId=\"AppSidebar\"");
        var sidebar = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"AppSidebar\"",
            "AutomationProperties.AutomationId=\"HomeSection\"");
        var modalOverlay = ExtractSourceRange(
            xaml,
            "<Grid x:Name=\"ModalOverlay\"",
            "AutomationProperties.AutomationId=\"GlobalBusyOverlay\"");

        Assert.Contains("Grid.Row=\"1\"", sidebar);
        Assert.Contains("Grid.Row=\"1\"", ExtractSourceRange(
            xaml,
            "<Grid Grid.Column=\"1\"",
            "AutomationProperties.AutomationId=\"LogCopyNotification\""));
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SidebarHomeButton\"", titleBar);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SidebarSettingsButton\"", titleBar);
        Assert.DoesNotContain("shell:WindowChrome.IsHitTestVisibleInChrome", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarHomeButton\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarImageConversionButton\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarVideoConversionButton\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarSettingsButton\"", sidebar);

        Assert.Contains("Grid.Row=\"1\"", modalOverlay);
        Assert.Contains("Grid.Column=\"0\"", modalOverlay);
        Assert.Contains("Grid.ColumnSpan=\"2\"", modalOverlay);
        Assert.Contains("Panel.ZIndex=\"25\"", modalOverlay);
        Assert.Contains("IsHitTestVisible=\"True\"", modalOverlay);
        Assert.Contains("Style=\"{StaticResource V3dfyModalOverlayStyle}\"", modalOverlay);
        Assert.Contains("ElementName=ModalOverlay", modalOverlay);
        Assert.Contains("AutomationProperties.AutomationId=\"SettingsModal\"", modalOverlay);
        Assert.Contains("AutomationProperties.AutomationId=\"ResponsiveModalFooter\"", modalOverlay);
        Assert.DoesNotContain("TitleBarMinimizeButton", modalOverlay);
        Assert.DoesNotContain("TitleBarCloseButton", modalOverlay);
    }

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
        Assert.Contains("ToolTipService.IsEnabled=\"{Binding ShellToolTipsEnabled}\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarBrand\"", sidebar);
        Assert.Contains("v3dfy-icon-left-square-transparent.png", sidebar);
        Assert.Contains("HorizontalAlignment=\"{Binding SidebarNavContentHorizontalAlignment}\"", brand);
        Assert.Contains("Orientation=\"Horizontal\"", brand);
        Assert.Contains("HorizontalAlignment=\"Center\"", brand);
        Assert.Contains("Text=\"{Binding AppTitle}\"", sidebar);
        Assert.Contains("Text=\"{Binding ShellTaglineText}\"", sidebar);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SidebarToggleButton\"", sidebar);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SidebarToggleStripButton\"", sidebar);
        Assert.DoesNotContain("Command=\"{Binding ToggleSidebarCommand}\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarSettingsButton\"", bottomActions);
        Assert.Contains("Visibility=\"{Binding SidebarExpandedContentVisibility}\"", sidebar);
        Assert.Contains("HorizontalAlignment=\"{Binding SidebarNavContentHorizontalAlignment}\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarHomeButton\"", sidebar);
        Assert.Contains("Text=\"{Binding HomeNavigationText}\"", sidebar);
        Assert.Contains("Command=\"{Binding SelectHomeSectionCommand}\"", sidebar);
        Assert.Contains("Tag=\"{Binding IsHomeSectionSelected}\"", sidebar);
        Assert.Contains("ToolTip=\"{Binding HomeNavigationText}\"", sidebar);
        Assert.Contains("ToolTipService.IsEnabled=\"{Binding ShellToolTipsEnabled}\"", ExtractSourceRange(
            sidebar,
            "AutomationProperties.AutomationId=\"SidebarHomeButton\"",
            "AutomationProperties.AutomationId=\"SidebarImageConversionButton\""));
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarImageConversionButton\"", sidebar);
        Assert.Contains("Text=\"{Binding ImageConversionNavigationText}\"", sidebar);
        Assert.Contains("Command=\"{Binding SelectImageConversionSectionCommand}\"", sidebar);
        Assert.Contains("Tag=\"{Binding IsImageConversionSectionSelected}\"", sidebar);
        Assert.Contains("ToolTip=\"{Binding ImageConversionNavigationText}\"", sidebar);
        Assert.Contains("ToolTipService.IsEnabled=\"{Binding ShellToolTipsEnabled}\"", ExtractSourceRange(
            sidebar,
            "AutomationProperties.AutomationId=\"SidebarImageConversionButton\"",
            "AutomationProperties.AutomationId=\"SidebarVideoConversionButton\""));
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarVideoConversionButton\"", sidebar);
        Assert.Contains("Text=\"{Binding VideoConversionNavigationText}\"", sidebar);
        Assert.Contains("Command=\"{Binding SelectVideoConversionSectionCommand}\"", sidebar);
        Assert.Contains("Tag=\"{Binding IsVideoConversionSectionSelected}\"", sidebar);
        Assert.Contains("ToolTip=\"{Binding VideoConversionNavigationText}\"", sidebar);
        Assert.Contains("ToolTipService.IsEnabled=\"{Binding ShellToolTipsEnabled}\"", ExtractSourceRange(
            sidebar,
            "AutomationProperties.AutomationId=\"SidebarVideoConversionButton\"",
            "AutomationProperties.AutomationId=\"SidebarBottomActions\""));
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarBottomActions\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarSettingsButton\"", sidebar);
        Assert.Contains("Command=\"{Binding OpenSettingsCommand}\"", sidebar);
        Assert.Contains("Text=\"{Binding SettingsText}\"", sidebar);
        Assert.Contains("ToolTipService.IsEnabled=\"{Binding ShellToolTipsEnabled}\"", bottomActions);
        Assert.True(CountOccurrences(sidebar, "Style=\"{StaticResource ShellSidebarIconTextStyle}\"") >= 4);
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
        Assert.Contains("public string ShellTaglineText => T(LocalizationKeys.ShellTagline);", source);
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
    public void ModalState_BlocksShellNavigationTooltipsAndBackgroundCommands()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var normalizedSource = source.Replace("\r\n", "\n");
        var sidebar = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"AppSidebar\"",
            "AutomationProperties.AutomationId=\"HomeSection\"");
        var modalOverlay = ExtractSourceRange(
            xaml,
            "<Grid x:Name=\"ModalOverlay\"",
            "AutomationProperties.AutomationId=\"GlobalBusyOverlay\"");
        var modalStateMethod = ExtractSourceRange(
            source,
            "private void RaiseModalStatePropertiesChanged()",
            "private void RaiseModelInventoryPropertiesChanged()");

        Assert.Contains("Grid.Row=\"1\"", modalOverlay);
        Assert.Contains("Grid.Column=\"0\"", modalOverlay);
        Assert.Contains("Grid.ColumnSpan=\"2\"", modalOverlay);
        Assert.Contains("Panel.ZIndex=\"25\"", modalOverlay);
        Assert.Contains("IsHitTestVisible=\"True\"", modalOverlay);
        Assert.Contains("Visibility=\"{Binding ModalOverlayVisibility}\"", modalOverlay);
        Assert.Contains("ToolTipService.IsEnabled=\"{Binding ShellToolTipsEnabled}\"", sidebar);
        Assert.Contains("ToolTip=\"{Binding ImageConversionNavigationText}\"", sidebar);
        Assert.Contains("ToolTipService.IsEnabled=\"{Binding ShellToolTipsEnabled}\"", ExtractSourceRange(
            sidebar,
            "AutomationProperties.AutomationId=\"SidebarImageConversionButton\"",
            "AutomationProperties.AutomationId=\"SidebarVideoConversionButton\""));

        Assert.Contains("public bool ShellToolTipsEnabled => !IsAnyModalOpen;", source);
        Assert.Contains("public bool CanUseShellNavigation =>\n        !IsAnyModalOpen &&\n        !IsImageExportRunning;", normalizedSource);
        Assert.Contains("public bool CanOpenSettings =>\n        CanUseShellNavigation &&", normalizedSource);
        Assert.Contains("public bool CanInteractWithImageWorkflow =>\n        !IsAnyModalOpen &&\n        !IsImageExportRunning;", normalizedSource);
        Assert.Contains("public bool CanUseSystemStatusActions =>\n        !IsAnyModalOpen &&", normalizedSource);
        Assert.Contains("public bool CanUseSettingsSystemStatusActions =>", source);
        Assert.Contains("public bool CanUseSettingsSystemStatusActions =>\n        !IsImageExportRunning &&\n        !IsConversionRunning &&", normalizedSource);
        Assert.Contains("public bool CanShowModelHelp =>", source);
        Assert.Contains("public bool CanShowImageParallaxModelHelp =>", source);
        Assert.Contains("ClearImageLogCommand = new RelayCommand(ClearImageLog, () => ImageLogs.Count > 0 && !IsImageExportRunning && !IsAnyModalOpen);", source);

        Assert.Contains("OnPropertyChanged(nameof(ShellToolTipsEnabled));", modalStateMethod);
        Assert.Contains("OnPropertyChanged(nameof(CanUseShellNavigation));", modalStateMethod);
        Assert.Contains("OnPropertyChanged(nameof(CanOpenSettings));", modalStateMethod);
        Assert.Contains("OnPropertyChanged(nameof(CanInteractWithImageWorkflow));", modalStateMethod);
        Assert.Contains("OnPropertyChanged(nameof(CanUseSystemStatusActions));", modalStateMethod);
        Assert.Contains("OnPropertyChanged(nameof(CanUseSettingsSystemStatusActions));", modalStateMethod);
        Assert.Contains("OnPropertyChanged(nameof(CanShowModelHelp));", modalStateMethod);
        Assert.Contains("OnPropertyChanged(nameof(CanShowImageParallaxModelHelp));", modalStateMethod);
        Assert.Contains("ToggleSidebarCommand.RaiseCanExecuteChanged();", modalStateMethod);
        Assert.Contains("SelectImageConversionSectionCommand.RaiseCanExecuteChanged();", modalStateMethod);
        Assert.Contains("SelectVideoConversionSectionCommand.RaiseCanExecuteChanged();", modalStateMethod);
        Assert.Contains("OpenSettingsCommand.RaiseCanExecuteChanged();", modalStateMethod);
        Assert.Contains("RefreshEngineStatusCommand.RaiseCanExecuteChanged();", modalStateMethod);
        Assert.Contains("ShowTechnicalDetailsCommand.RaiseCanExecuteChanged();", modalStateMethod);
        Assert.Contains("ShowModelHelpCommand.RaiseCanExecuteChanged();", modalStateMethod);
        Assert.Contains("ShowImageParallaxModelHelpCommand.RaiseCanExecuteChanged();", modalStateMethod);
        Assert.Contains("ClearImageLogCommand.RaiseCanExecuteChanged();", modalStateMethod);
    }

    [Fact]
    public void ModalContentActions_UseModalSafePredicatesInsteadOfBackgroundShellGates()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var normalizedSource = source.Replace("\r\n", "\n");
        var settingsModal = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"SettingsModal\"",
            "Visibility=\"{Binding TechnicalDetailsModalContentVisibility}\"");
        var modelsHeader = ExtractSourceRange(
            settingsModal,
            "AutomationProperties.AutomationId=\"SettingsViewModelsButton\"",
            "Text=\"{Binding ModelsSettingsIntroText}\"");
        var toolsHeader = ExtractSourceRange(
            settingsModal,
            "AutomationProperties.AutomationId=\"ToolsEngineSettingsSection\"",
            "Text=\"{Binding ToolsEngineSettingsIntroText}\"");
        var toolStatusAction = ExtractSourceRange(
            settingsModal,
            "Command=\"{Binding ContextActionCommand}\"",
            "Visibility=\"{Binding ContextActionVisibility}\"");
        var modalFooter = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ResponsiveModalFooter\"",
            "</WrapPanel>");
        var closeSettingsButton = ExtractSourceRange(
            modalFooter,
            "AutomationProperties.AutomationId=\"CloseSettingsButton\"",
            "Visibility=\"{Binding SettingsModalContentVisibility}\"");
        var closeModelHelpButton = ExtractSourceRange(
            modalFooter,
            "AutomationProperties.AutomationId=\"CloseModelHelpButton\"",
            "Visibility=\"{Binding ModelHelpModalContentVisibility}\"");
        var copyFullLogButton = ExtractSourceRange(
            modalFooter,
            "AutomationProperties.AutomationId=\"CopyFullLogButton\"",
            "Visibility=\"{Binding ActivityLogModalContentVisibility}\"");
        var settingsActionsProperty = ExtractSourceRange(
            source,
            "public bool CanUseSettingsSystemStatusActions =>",
            "public string ToolStatusTitle");
        var createToolStatusMethod = ExtractSourceRange(
            source,
            "private ToolStatusItemViewModel CreateToolStatus",
            "private string ToolStatusReasonText");
        var showModelInventoryMethod = ExtractSourceRange(
            source,
            "private void ShowModelInventory()",
            "private void CloseModelInventory()");
        var showTechnicalDetailsMethod = ExtractSourceRange(
            source,
            "private void ShowTechnicalDetails()",
            "private void CloseTechnicalDetails()");

        Assert.Contains("Command=\"{Binding ShowModelInventoryCommand}\"", modelsHeader);
        Assert.Contains("IsEnabled=\"{Binding CanUseSettingsSystemStatusActions}\"", modelsHeader);
        Assert.DoesNotContain("CanUseSystemStatusActions", modelsHeader);
        Assert.DoesNotContain("CanUseShellNavigation", modelsHeader);

        Assert.Contains("Command=\"{Binding RefreshEngineStatusCommand}\"", toolsHeader);
        Assert.Contains("IsEnabled=\"{Binding CanUseSettingsSystemStatusActions}\"", toolsHeader);
        Assert.DoesNotContain("CanUseSystemStatusActions", toolsHeader);
        Assert.DoesNotContain("CanUseShellNavigation", toolsHeader);
        Assert.Contains("Command=\"{Binding ContextActionCommand}\"", toolStatusAction);

        Assert.Contains("RefreshEngineStatusCommand = new AsyncRelayCommand(\n            () => RefreshEngineStatusWithGlobalBusyAsync(logRefresh: true),\n            () => CanUseSettingsSystemStatusActions);", normalizedSource);
        Assert.Contains("ShowModelInventoryCommand = new RelayCommand(\n            ShowModelInventory,\n            () => CanUseSettingsSystemStatusActions);", normalizedSource);
        Assert.Contains("ShowTechnicalDetailsCommand = new RelayCommand(\n            ShowTechnicalDetails,\n            () => CanUseSettingsSystemStatusActions);", normalizedSource);
        Assert.Contains("!IsImageExportRunning", settingsActionsProperty);
        Assert.Contains("!IsConversionRunning", settingsActionsProperty);
        Assert.Contains("!IsPreviewGenerating", settingsActionsProperty);
        Assert.Contains("!IsModelPackImportRunning", settingsActionsProperty);
        Assert.DoesNotContain("IsAnyModalOpen", settingsActionsProperty);
        Assert.Contains("public bool CanUseSystemStatusActions =>\n        !IsAnyModalOpen &&", normalizedSource);
        Assert.Contains("OpenEngineFolderCommand = new RelayCommand(OpenEngineFolder);", source);
        Assert.Contains("? OpenEngineFolderCommand", createToolStatusMethod);
        Assert.Contains("? ShowModelInventoryCommand", createToolStatusMethod);
        Assert.Contains("CaptureSettingsReturnContext();", showModelInventoryMethod);
        Assert.Contains("IsSettingsModalOpen = false;", showModelInventoryMethod);
        Assert.Contains("IsModelInventoryModalOpen = true;", showModelInventoryMethod);
        Assert.Contains("IsSettingsModalOpen = false;", showTechnicalDetailsMethod);
        Assert.Contains("IsTechnicalDetailsModalOpen = true;", showTechnicalDetailsMethod);

        Assert.Contains("Command=\"{Binding CloseSettingsCommand}\"", closeSettingsButton);
        Assert.DoesNotContain("IsEnabled=", closeSettingsButton);
        Assert.Contains("Command=\"{Binding CloseModelHelpCommand}\"", closeModelHelpButton);
        Assert.DoesNotContain("IsEnabled=", closeModelHelpButton);
        Assert.Contains("Command=\"{Binding CopyFullLogCommand}\"", copyFullLogButton);
        Assert.DoesNotContain("IsEnabled=\"{Binding CanUseShellNavigation}\"", copyFullLogButton);
        Assert.DoesNotContain("IsEnabled=\"{Binding CanUseSystemStatusActions}\"", copyFullLogButton);

        Assert.DoesNotContain("CanUseShellNavigation", settingsModal);
        Assert.DoesNotContain("CanOpenSettings", settingsModal);
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
        Assert.DoesNotContain("Text=\"{Binding ReadyNowText}\"", home);
        Assert.DoesNotContain("Text=\"{Binding ComingNextText}\"", home);
        Assert.DoesNotContain("Text=\"{Binding OpenSectionText}\"", home);
        Assert.DoesNotContain("Text=\"{Binding HomeModelsStatusText}\"", home);
        Assert.DoesNotContain("Text=\"{Binding SettingsText}\"", home);
        Assert.Contains("public string HomeVideoCardBodyText => T(LocalizationKeys.VideoHomeCardBody);", source);
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
            "public string AppTitle =>");
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
            "private void ApplyImageSetupChanged(",
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
            "AutomationProperties.AutomationId=\"ImageOutputPanelCard\"");
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
        Assert.Contains("IsEnabled=\"{Binding ImageWorkflowCardsEnabled}\"", setupStep);
        Assert.Contains("Text=\"{Binding ImageParallaxModeTitleText}\"", setupStep);
        Assert.Contains("Text=\"{Binding ImageParallaxModeBodyText}\"", setupStep);
        Assert.DoesNotContain("Text=\"{Binding ImageParallaxModeCardStatusText}\"", setupStep);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoModeCard\"", setupStep);
        Assert.Contains("Command=\"{Binding SelectImageStereoModeCommand}\"", setupStep);
        Assert.Contains("Text=\"{Binding Image3DOutputModeTitleText}\"", setupStep);
        Assert.Contains("Text=\"{Binding Image3DOutputModeBodyText}\"", setupStep);
        Assert.DoesNotContain("Text=\"{Binding ImageStereoModeCardStatusText}\"", setupStep);
        Assert.DoesNotContain("StartConversionCommand", imageSection);
        Assert.DoesNotContain("GeneratePreviewCommand", imageSection);
        Assert.DoesNotContain("SelectVideoCommand", imageSection);
        Assert.DoesNotContain("ActivityLogPanelText", imageSection);
        Assert.DoesNotContain("ItemsSource=\"{Binding Logs}\"", imageSection);
        Assert.DoesNotContain("ItemsSource=\"{Binding ConversionLogs}\"", imageSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxScaffold\"", imageSection);
        Assert.Contains("Visibility=\"{Binding ImageParallaxSetupStepVisibility}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding ParallaxDepthIntensityOptions}\"", imageSection);
        Assert.Contains("SelectedValuePath=\"Value\"", imageSection);
        Assert.Contains("SelectedValue=\"{Binding SelectedParallaxDepthIntensity}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding ParallaxMotionDirectionOptions}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding ParallaxZoomAmplitudeOptions}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding ParallaxDurationOptions}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding ParallaxSmoothingOptions}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding ParallaxLayerBehaviorOptions}\"", imageSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"", imageSection);
        Assert.Contains("Visibility=\"{Binding ImageParallaxPreviewExportStepVisibility}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageParallaxSummaryText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageExpectedOutputFileText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageNotImplementedStateText}\"", imageSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoScaffold\"", imageSection);
        Assert.Contains("Visibility=\"{Binding ImageStereoSetupStepVisibility}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding StereoOutputFormatOptions}\"", imageSection);
        Assert.Contains("SelectedItem=\"{Binding SelectedStereoOutputFormatOption,", imageSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageModelSelector\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding ImageParallaxLocalModelCandidates}\"", imageSection);
        Assert.Contains("SelectedItem=\"{Binding SelectedLocalModelCandidate,", imageSection);
        Assert.Contains("ItemsSource=\"{Binding StereoEyeSeparationOptions}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding StereoConvergenceOptions}\"", imageSection);
        Assert.Contains("IsChecked=\"{Binding ImageStereoSwapEyes}\"", imageSection);
        Assert.Contains("ItemsSource=\"{Binding StereoAnaglyphModeOptions}\"", imageSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageStereoResultScaffold\"", imageSection);
        Assert.Contains("Visibility=\"{Binding ImageStereoPreviewExportStepVisibility}\"", imageSection);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImageConversionSummaryCard\"", imageSection);
        Assert.Contains("<RowDefinition Height=\"{Binding ImagePreviewExportStatusRowHeight}\" />", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageOutputPanelCard\"", rightPanel);
        Assert.Contains("Visibility=\"{Binding ImagePreviewExportStatusCardVisibility}\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImageOutputPanelTitleText}\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImageSelectedModeSummaryText}\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImagePreviewExportStatusText}\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImagePlannedOutputFormatsText}\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImageSelectedModelSummaryText}\"", rightPanel);
        Assert.Contains("Text=\"{Binding ImageExpectedOutputPathText}\"", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"ConvertImageButton\"", rightPanel);
        Assert.Contains("Command=\"{Binding ConvertImageCommand}\"", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"OpenImageOutputFolderButton\"", rightPanel);
        Assert.Contains("Command=\"{Binding OpenImageOutputFolderCommand}\"", rightPanel);
        Assert.Contains("AutomationProperties.AutomationId=\"NewImageConversionButton\"", rightPanel);
        Assert.Contains("Command=\"{Binding NewImageConversionCommand}\"", rightPanel);
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
        Assert.DoesNotContain("Text=\"{Binding ImageSourcePanelTitleText}\"", setupStep);
        Assert.DoesNotContain("Text=\"{Binding ImageDepthPanelTitleText}\"", setupStep);
        Assert.DoesNotContain("Text=\"{Binding ImageParallaxPreviewTitleText}\"", setupStep);
        Assert.DoesNotContain("Text=\"{Binding ImageStereoPreviewTitleText}\"", setupStep);
        Assert.Contains("Text=\"{Binding ImageParameterPanelTitleText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageParallaxSummaryText}\"", imageSection);
        Assert.Contains("Source=\"{Binding ImageStereoPreviewImagePath}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageStereoControlsTitleText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageGeneratedFilesTitleText}\"", imageSection);
        Assert.Contains("Text=\"{Binding ImageOutputPanelTitleText}\"", imageSection);
        Assert.Contains("AutomationProperties.AutomationId=\"ConvertImageButton\"", imageSection);
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
        Assert.Contains("public LocalizedOptionViewModel<ImageStereoOutputFormat>? SelectedStereoOutputFormatOption", source);
        Assert.Contains("public RelayCommand SelectImageCommand", source);
        Assert.Contains("public RelayCommand AnalyzeImageCommand", source);
        Assert.Contains("public RelayCommand ClearImageLogCommand", source);
        Assert.Contains("public RelayCommand ViewImageActivityLogCommand", source);
        Assert.Contains("private void SelectImage()", source);
        Assert.Contains("private void TrySelectImage(string path)", source);
        Assert.Contains("private void AnalyzeImage()", source);
        Assert.Contains("public void SelectDroppedImage(string path)", source);
        Assert.Contains("viewModel.SelectDroppedImage(files[0]);", codeBehind);
        Assert.Contains("_selectedImageMetadata = null;", selectImageMethod);
        Assert.Contains("ResetImageSetupState();", selectImageMethod);
        Assert.Contains("AnalyzeImage();", selectImageMethod);
        Assert.DoesNotContain("ReadImageMetadata", selectImageMethod);
        Assert.Contains("ReadImageMetadata(SelectedImagePath)", analyzeImageMethod);
        Assert.Contains("BitmapDecoder.Create", source);
        Assert.Contains("PixelWidth", source);
        Assert.Contains("PixelHeight", source);
        Assert.Contains("FormatAspectRatio", source);
        Assert.Contains("FormatImageFileSize", source);
        Assert.Contains("public ObservableCollection<LogEntryViewModel> ImageLogs", source);
        Assert.Contains("private void AddImageLog", source);
        Assert.Contains("private void ClearImageLog()", source);
        Assert.Contains("ClearImageLogCommand = new RelayCommand(ClearImageLog, () => ImageLogs.Count > 0 && !IsImageExportRunning && !IsAnyModalOpen);", source);
        Assert.Contains("ViewImageActivityLogCommand = new RelayCommand(ViewImageActivityLog);", constructor);
        Assert.Contains("private void ViewImageActivityLog()", source);
        Assert.Contains("private string CreateFullImageActivityLogText()", source);
        Assert.Contains("ActivityLogModalKind.Image", source);
        Assert.Contains("CreateFullImageActivityLogText()", copyFullLogMethod);
        Assert.Contains("LocalizationKeys.ActivityLogImageName", copyFullLogMethod);
        AssertLocalizationKeyPairExists("ActivityLog.ImageName");
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
        Assert.Contains("public bool CanAnalyzeImage => HasSelectedImage && CanInteractWithImageWorkflow;", source);
        Assert.Contains("public bool HasSelectedImageMode => SelectedImageConversionMode is not null;", source);
        Assert.Contains("public bool HasImageWorkflowPrerequisites => HasImageMetadata && HasSelectedImageMode;", source);
        Assert.Contains("public bool CanOpenImageSetupStep => HasImageMetadata && CanUseImageStepNavigation;", source);
        Assert.Contains("public bool IsImageModeSetupValid", source);
        Assert.Contains("public bool IsImageSetupValid => HasImageMetadata && HasSelectedImageMode && IsImageModeSetupValid;", source);
        Assert.Contains("public bool CanOpenImagePreviewExportStep => IsImageSetupValid && CanUseImageStepNavigation;", source);
        Assert.Contains("ImageConversionStep.ModeAndSource => CanUseImageStepNavigation && CanOpenImageSetupStep", source);
        Assert.Contains("ImageConversionStep.Setup => CanUseImageStepNavigation && CanOpenImagePreviewExportStep", source);
        Assert.Contains("public Visibility ImageAnalysisResultsVisibility", source);
        Assert.Contains("HasImageMetadata ? Visibility.Visible : Visibility.Collapsed;", source);
        Assert.Contains("public GridLength ImagePreviewExportStatusRowHeight", source);
        Assert.Contains("SelectedImageConversionStep != ImageConversionStep.ModeAndSource", source);
        Assert.Contains("HasCurrentImageConversionOutput", source);
        Assert.Contains("? GridLength.Auto", source);
        Assert.Contains("public Thickness ImageActivityLogCardMargin", source);
        Assert.Contains("? new Thickness(0d, 14d, 0d, 0d)", source);
        Assert.Contains("public Visibility ImageSetupStepVisibility", source);
        Assert.Contains("public Visibility ImageParallaxSetupStepVisibility", source);
        Assert.Contains("public Visibility ImageStereoSetupStepVisibility", source);
        Assert.Contains("public Visibility ImagePreviewExportStatusCardVisibility", source);
        Assert.Contains("ApplyImageSetupChanged", source);
        Assert.Contains("_hasEnteredImagePreviewExportStage = false;", source);
        Assert.Contains("SelectedImageConversionStep = ImageConversionStep.ModeAndSource;", selectImageMethod);
        Assert.Contains("_hasEnteredImagePreviewExportStage = false;", selectImageMethod);
        Assert.Contains("LocalizationKeys.ImageLogWorkflowChangedFormat", selectModeMethod);
        Assert.Contains("ApplyImageSetupChanged(", selectModeMethod);
        Assert.Contains("SelectedImageConversionStep == ImageConversionStep.ModeAndSource", selectModeMethod);
        Assert.Contains("if (step == ImageConversionStep.Setup && !CanOpenImageSetupStep)", selectStepMethod);
        Assert.Contains("if (step == ImageConversionStep.PreviewAndExport && !CanOpenImagePreviewExportStep)", selectStepMethod);
        Assert.Contains("if (!CanMoveImageWizardNext)", moveNextMethod);
        Assert.Contains("_hasEnteredImagePreviewExportStage = false;", setupChangedMethod);
        Assert.Contains("ShowLogCopyNotification(", setupChangedMethod);
        Assert.Contains("LocalizationKeys.ImageWorkflowParallaxTitle", source);
        Assert.Contains("LocalizationKeys.ImageWorkflowStereoTitle", source);
        Assert.Contains("LocalizationKeys.ImageIntro", source);
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
            "public string AppTitle =>");
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
        Assert.Contains("SelectHomeSectionCommand = new RelayCommand(", constructor);
        Assert.Contains("() => SelectAppSection(AppSection.Home)", constructor);
        Assert.Contains("SelectImageConversionSectionCommand = new RelayCommand(", constructor);
        Assert.Contains("() => SelectAppSection(AppSection.ImageConversion)", constructor);
        Assert.Contains("SelectVideoConversionSectionCommand = new RelayCommand(", constructor);
        Assert.Contains("() => SelectAppSection(AppSection.VideoConversion)", constructor);
        Assert.Contains("ToggleSidebarCommand = new RelayCommand(ToggleSidebar, () => CanUseShellNavigation);", constructor);
        Assert.Contains("SelectImageCommand = new RelayCommand(SelectImage, () => CanInteractWithImageWorkflow);", constructor);
        Assert.Contains("SelectImageParallaxModeCommand = new RelayCommand(", constructor);
        Assert.Contains("() => SelectImageConversionMode(ImageConversionMode.ParallaxPhoto)", constructor);
        Assert.Contains("SelectImageStereoModeCommand = new RelayCommand(", constructor);
        Assert.Contains("() => SelectImageConversionMode(ImageConversionMode.StereoscopicImage)", constructor);
        Assert.Contains("() => CanUseShellNavigation", constructor);
        Assert.Contains("() => CanInteractWithImageWorkflow", constructor);
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

    private static void AssertLocalizationKeyPairExists(string key)
    {
        Assert.Contains($"\"{key}\"", ReadRepoFile("src", "V3dfy.App", "Localization", "en.json"));
        Assert.Contains($"\"{key}\"", ReadRepoFile("src", "V3dfy.App", "Localization", "es.json"));
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
