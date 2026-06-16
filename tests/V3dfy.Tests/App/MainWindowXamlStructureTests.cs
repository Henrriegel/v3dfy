using System.Text.RegularExpressions;

namespace V3dfy.Tests.App;

public sealed class MainWindowXamlStructureTests
{
    [Fact]
    public void MainWorkflow_UsesStepperWizardInsteadOfTabNavigation()
    {
        var xaml = ReadMainWindowXaml();
        var wizard = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"MainWorkflowWizard\"",
            "AutomationProperties.AutomationId=\"WizardFooter\"");

        Assert.DoesNotContain("<TabControl", xaml);
        Assert.DoesNotContain("<TabItem", xaml);
        Assert.Contains("SourceAndAnalysisStepTitle", wizard);
        Assert.Contains("ThreeDSetupStepTitle", wizard);
        Assert.Contains("WizardConversionPlanStepTitle", wizard);
        Assert.Contains("SourceAndAnalysisStepState", wizard);
        Assert.Contains("ThreeDSetupStepState", wizard);
        Assert.Contains("ConversionPlanStepState", wizard);
    }

    [Fact]
    public void WizardFooter_HasFixedBackNextOutsideScrollableContent()
    {
        var xaml = ReadMainWindowXaml();
        var wizard = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"MainWorkflowWizard\"",
            "AutomationProperties.AutomationId=\"PreviewConversionStatusCard\"");
        var footerButtonStyle = ExtractSourceRange(
            xaml,
            "x:Key=\"WizardFooterButtonStyle\"",
            "x:Key=\"WizardFooterSecondaryButtonStyle\"");
        var footerSecondaryButtonStyle = ExtractSourceRange(
            xaml,
            "x:Key=\"WizardFooterSecondaryButtonStyle\"",
            "x:Key=\"SettingsMenuListBoxItemStyle\"");
        var scrollContent = ExtractSourceRange(
            wizard,
            "<ScrollViewer Grid.Row=\"1\"",
            "AutomationProperties.AutomationId=\"WizardFooter\"");
        var footer = ExtractSourceRange(
            wizard,
            "AutomationProperties.AutomationId=\"WizardFooter\"",
            "Visibility=\"{Binding ConversionRunningVisibility}\"");
        var backButton = ExtractSourceRange(
            footer,
            "AutomationProperties.AutomationId=\"WizardBackButton\"",
            "AutomationProperties.AutomationId=\"WizardNextButton\"");
        var nextButton = ExtractSourceRange(
            footer,
            "MinWidth=\"110\"",
            "AutomationProperties.AutomationId=\"ContinueWithConversionButton\"");
        var continueButton = ExtractSourceRange(
            footer,
            "MinWidth=\"220\"",
            "</Grid>");

        Assert.DoesNotContain("WizardBackButton", scrollContent);
        Assert.DoesNotContain("WizardNextButton", scrollContent);
        Assert.DoesNotContain("ContinueWithConversionButton", scrollContent);
        Assert.Contains("<Setter Property=\"Height\" Value=\"34\" />", footerButtonStyle);
        Assert.Contains("<Setter Property=\"Padding\" Value=\"10,5\" />", footerButtonStyle);
        Assert.Contains("<Setter Property=\"MinWidth\" Value=\"88\" />", footerButtonStyle);
        Assert.Contains("<Setter Property=\"Height\" Value=\"34\" />", footerSecondaryButtonStyle);
        Assert.Contains("<Setter Property=\"Padding\" Value=\"10,5\" />", footerSecondaryButtonStyle);
        Assert.Contains("<Setter Property=\"MinWidth\" Value=\"88\" />", footerSecondaryButtonStyle);
        Assert.Contains("AutomationProperties.AutomationId=\"WizardBackButton\"", footer);
        Assert.Contains("Command=\"{Binding WizardBackCommand}\"", footer);
        Assert.Contains("Visibility=\"{Binding WizardBackButtonVisibility}\"", footer);
        Assert.Contains("Style=\"{StaticResource WizardFooterSecondaryButtonStyle}\"", backButton);
        Assert.Contains("AutomationProperties.AutomationId=\"WizardNextButton\"", footer);
        Assert.Contains("Command=\"{Binding WizardNextCommand}\"", footer);
        Assert.Contains("IsEnabled=\"{Binding CanMoveWizardNext}\"", footer);
        Assert.Contains("ToolTip=\"{Binding WizardNextToolTipText}\"", footer);
        Assert.Contains("Visibility=\"{Binding WizardNextButtonVisibility}\"", footer);
        Assert.Contains("Style=\"{StaticResource WizardFooterButtonStyle}\"", nextButton);
        Assert.Contains("MinWidth=\"110\"", nextButton);
        Assert.Contains("AutomationProperties.AutomationId=\"ContinueWithConversionButton\"", footer);
        Assert.Contains("Command=\"{Binding ContinueWithConversionCommand}\"", footer);
        Assert.Contains("Visibility=\"{Binding ContinueWithConversionFooterVisibility}\"", footer);
        Assert.Contains("IsEnabled=\"{Binding CanEnterPreviewConversionStage}\"", footer);
        Assert.Contains("Style=\"{StaticResource WizardFooterButtonStyle}\"", continueButton);
        Assert.Contains("MinWidth=\"220\"", continueButton);
        Assert.Contains("HorizontalAlignment=\"Right\"", continueButton);
        Assert.DoesNotContain("HorizontalAlignment=\"Stretch\"", continueButton);
        Assert.DoesNotContain("Padding=", continueButton);
        Assert.DoesNotContain("MinHeight=", footer);
        Assert.DoesNotContain("Style=\"{StaticResource PrimaryCtaButtonStyle}\"", footer);
    }

    [Fact]
    public void SourceAndAnalysisStep_ContainsSelectionAndAnalysisTogether()
    {
        var xaml = ReadMainWindowXaml();
        var step = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"SourceAndAnalysisStepContent\"",
            "AutomationProperties.AutomationId=\"ThreeDSetupStepContent\"");

        Assert.Contains("DropVideoText", step);
        Assert.Contains("SelectedVideoDisplayPath", step);
        Assert.Contains("SelectVideoCommand", step);
        Assert.Contains("AnalyzeCommand", step);
        Assert.Contains("SourceAnalysisEmptyHintText", step);
        Assert.Contains("Visibility=\"{Binding SourceAnalysisEmptyHintVisibility}\"", step);
        Assert.Contains("AutomationProperties.AutomationId=\"VideoAnalysisSection\"", step);
        Assert.Contains("Visibility=\"{Binding VideoAnalysisSectionVisibility}\"", step);
        Assert.Contains("VideoAnalysisPendingStatusText", step);
        Assert.Contains("Visibility=\"{Binding VideoAnalysisPendingStatusVisibility}\"", step);
        Assert.Contains("Visibility=\"{Binding VideoAnalysisResultsVisibility}\"", step);
        Assert.Contains("VideoAnalysisTitle", step);
        Assert.Contains("AnalysisDurationText", step);
        Assert.Contains("AnalysisResolutionText", step);
        Assert.Contains("AnalysisFpsText", step);
        Assert.Contains("AnalysisCodecText", step);
        Assert.Contains("AnalysisContainerText", step);
        Assert.Contains("AnalysisAudioStreamsText", step);
        Assert.Contains("AnalysisSubtitleStreamsText", step);
        Assert.Contains("AnalysisHdrText", step);
        Assert.Contains("AnalysisCompatibilityText", step);
    }

    [Fact]
    public void ThreeDSetupStep_ContainsSetupModelGuidanceAndCompactEstimates()
    {
        var xaml = ReadMainWindowXaml();
        var step = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ThreeDSetupStepContent\"",
            "AutomationProperties.AutomationId=\"ConversionPlanStepContent\"");

        Assert.Contains("SelectedOutputPreset", step);
        Assert.Contains("SelectedOutputContainer", step);
        Assert.Contains("SelectedQualityPreset", step);
        Assert.Contains("SelectedThreeDIntensity", step);
        Assert.Contains("SelectedThreeDOutputFormat", step);
        Assert.Contains("SelectedLocalModelCandidate", step);
        Assert.Contains("CompactEstimateTimeConfidenceText", step);
        Assert.Contains("CompactOutputSizeFreeSpaceText", step);
        Assert.Contains("CompactSelectedModelGuidanceText", step);
        Assert.Contains("CompactPresetGuidanceText", step);
        Assert.Contains("EstimateDetailsTitleText", step);
        Assert.Contains("Style=\"{StaticResource V3dfyExpanderStyle}\"", step);
        Assert.Contains("AutomationProperties.AutomationId=\"EstimateDetailsCard\"", step);
        Assert.Contains("EstimateBasisText", step);
        Assert.Contains("PerformanceHistoryPrivacyText", step);
        Assert.Contains("GuidanceDetailsTitleText", step);
        Assert.Contains("AutomationProperties.AutomationId=\"GuidanceDetailsCard\"", step);
        Assert.DoesNotContain("OutputPathText", step);
    }

    [Fact]
    public void ConversionPlanStep_IsCompactOutputFocusedWithTechnicalDetailsCollapsed()
    {
        var xaml = ReadMainWindowXaml();
        var step = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ConversionPlanStepContent\"",
            "AutomationProperties.AutomationId=\"WizardFooter\"");

        Assert.Contains("OutputPathText", step);
        Assert.Contains("BrowseOutputFolderCommand", step);
        Assert.Contains("ResetOutputPathCommand", step);
        Assert.Contains("OpenOutputWhenFinished", step);
        Assert.Contains("LgCompatibilityOptionsVisibility", step);
        Assert.Contains("ReadyForConversionSummaryText", step);
        Assert.Contains("ConversionPlanPresetText", step);
        Assert.Contains("ConversionPlanLocalModelText", step);
        Assert.Contains("ConversionPlanOutputFormatText", step);
        Assert.Contains("ConversionPlanResolutionText", step);
        Assert.Contains("ConversionPlanThreeDLayoutText", step);
        Assert.Contains("ConversionPlanQualityText", step);
        Assert.Contains("ConversionPlanIntensityText", step);
        Assert.Contains("CompactEstimateTimeConfidenceText", step);
        Assert.Contains("CompactOutputSizeFreeSpaceText", step);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ContinueWithConversionButton\"", step);
        Assert.DoesNotContain("Command=\"{Binding ContinueWithConversionCommand}\"", step);
        Assert.Contains("AutomationProperties.AutomationId=\"ConversionPlanTechnicalDetails\"", step);
        Assert.Contains("Style=\"{StaticResource V3dfyExpanderStyle}\"", step);
        Assert.Contains("AutomationProperties.AutomationId=\"ConversionPlanTechnicalDetailsCard\"", step);
        Assert.Contains("ConversionPlanStepsText", step);
        Assert.Contains("ConversionPlanCommandPreviewText", step);
    }

    [Fact]
    public void RightPanel_UsesContextualPreviewConversionStatusAndKeepsActivityLogBelow()
    {
        var xaml = ReadMainWindowXaml();
        var rightTop = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"PreviewConversionStatusCard\"",
            "AutomationProperties.AutomationId=\"GeneratePreviewPrimaryActionButton\"");

        Assert.DoesNotContain("PreviewConversionPlaceholderCard", xaml);
        Assert.Contains("<RowDefinition Height=\"{Binding PreviewConversionRowHeight}\" />", xaml);
        Assert.Contains("Margin=\"{Binding ActivityLogCardMargin}\"", xaml);
        Assert.Contains("Visibility=\"{Binding PreviewConversionStatusCardVisibility}\"", rightTop);
        Assert.Contains("PreviewConversionStatusTitleText", rightTop);
        Assert.Contains("PreviewConversionStatusText", rightTop);
        Assert.Contains("PreviewConversionStatusDetailText", rightTop);
        Assert.Contains("PreviewRequirementVisibility", rightTop);
        Assert.Contains("ConversionReadySummary", rightTop);
        Assert.Contains("PreviewConversionMissingToolsText", rightTop);
        Assert.Contains("OpenToolsEngineSettingsCommand", rightTop);
        Assert.DoesNotContain("ConversionExecutionStepLabel", rightTop);
        Assert.Equal(1, CountOccurrences(xaml, "Text=\"{Binding ConversionExecutionDetailText}\""));
        Assert.DoesNotContain("ToolStatuses", rightTop);
        Assert.DoesNotContain("RefreshEngineStatusCommand", rightTop);
        Assert.DoesNotContain("ShowTechnicalDetailsCommand", rightTop);
        Assert.DoesNotContain("ConversionReadinessMissingComponentsSummaryText", rightTop);
        Assert.DoesNotContain("ConversionReadinessRequiredComponentsText", rightTop);
        Assert.DoesNotContain("ConversionBlockedReasonText", rightTop);
        Assert.Contains("Grid.Row=\"1\"", xaml);
        Assert.Contains("Text=\"{Binding ActivityLogTitle}\"", xaml);
        Assert.Contains("<RowDefinition Height=\"*\" />", xaml);
    }

    [Fact]
    public void AppShell_UsesSidebarSettingsEntryAndSettingsModalHasSideMenu()
    {
        var xaml = ReadMainWindowXaml();
        var viewModel = ReadSourceFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var sidebar = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"AppSidebar\"",
            "AutomationProperties.AutomationId=\"HomeSection\"");
        var settings = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"SettingsModal\"",
            "Visibility=\"{Binding TechnicalDetailsModalContentVisibility}\"");

        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SettingsButton\"", xaml);
        Assert.DoesNotContain("<Grid Margin=\"0,0,0,18\">", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarBrand\"", sidebar);
        Assert.Contains("Text=\"{Binding AppTitle}\"", sidebar);
        Assert.Contains("Text=\"{Binding ShellTaglineText}\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarHomeButton\"", sidebar);
        Assert.Contains("Command=\"{Binding SelectHomeSectionCommand}\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarImageConversionButton\"", sidebar);
        Assert.Contains("Command=\"{Binding SelectImageConversionSectionCommand}\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarVideoConversionButton\"", sidebar);
        Assert.Contains("Command=\"{Binding SelectVideoConversionSectionCommand}\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarBottomActions\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarToggleStripButton\"", sidebar);
        Assert.Contains("AutomationProperties.AutomationId=\"SidebarSettingsButton\"", sidebar);
        Assert.Contains("Command=\"{Binding OpenSettingsCommand}\"", sidebar);
        Assert.Contains("ToolTip=\"{Binding SettingsText}\"", sidebar);
        Assert.Contains("ShellSidebarNavButtonStyle", xaml);
        Assert.Contains("<Grid x:Name=\"ModalOverlay\"", xaml);
        Assert.Contains("Grid.ColumnSpan=\"2\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"SettingsSideMenu\"", settings);
        Assert.Contains("ItemsSource=\"{Binding SettingsSectionOptions}\"", settings);
        Assert.Contains("SelectedValue=\"{Binding SelectedSettingsSection", settings);
        Assert.Contains("Height=\"{Binding ActiveModalHeight}\"", xaml);
        Assert.Contains("IsSettingsModalOpen || IsModelHelpModalOpen || IsModelInventoryModalOpen", viewModel);
        Assert.Contains("? 650d", viewModel);
        Assert.Contains("<ScrollViewer Grid.Column=\"2\"", settings);
        Assert.Contains("AutomationProperties.AutomationId=\"CloseSettingsButton\"", xaml);
        Assert.Contains("Grid.Row=\"1\"", settings);
    }

    [Fact]
    public void SettingsModal_ContainsVisualToolsAndDiagnosticSections()
    {
        var xaml = ReadMainWindowXaml();
        var settings = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"SettingsModal\"",
            "Visibility=\"{Binding TechnicalDetailsModalContentVisibility}\"");

        Assert.Contains("AutomationProperties.AutomationId=\"VisualSettingsSection\"", settings);
        var visual = ExtractSourceRange(
            settings,
            "AutomationProperties.AutomationId=\"VisualSettingsSection\"",
            "AutomationProperties.AutomationId=\"ModelsSettingsSection\"");
        Assert.Contains("AutomationProperties.AutomationId=\"VisualSettingsRows\"", visual);
        Assert.Contains("ItemsSource=\"{Binding LanguageOptions}\"", settings);
        Assert.Contains("ItemsSource=\"{Binding ThemeOptions}\"", settings);
        Assert.DoesNotContain("<Grid.ColumnDefinitions>", visual);
        Assert.DoesNotContain("Grid.Column=\"2\"", visual);
        Assert.DoesNotContain("PreviewConversionSettingsSection", settings);
        Assert.DoesNotContain("PreviewConversionSettingsTitleText", settings);
        Assert.Contains("AutomationProperties.AutomationId=\"ModelsSettingsHeader\"", settings);
        var models = ExtractSourceRange(
            settings,
            "AutomationProperties.AutomationId=\"ModelsSettingsSection\"",
            "AutomationProperties.AutomationId=\"ToolsEngineSettingsSection\"");
        var modelsHeader = ExtractSourceRange(
            models,
            "AutomationProperties.AutomationId=\"ModelsSettingsHeader\"",
            "Text=\"{Binding ModelsSettingsIntroText}\"");
        Assert.Contains("AutomationProperties.AutomationId=\"SettingsViewModelsButton\"", modelsHeader);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SettingsImportModelPackButton\"", modelsHeader);
        Assert.Equal(1, CountOccurrences(models, "AutomationProperties.AutomationId=\"SettingsViewModelsButton\""));
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SettingsImportModelPackButton\"", models);
        Assert.Contains("AutomationProperties.AutomationId=\"SettingsSelectableModelsTable\"", models);
        Assert.Contains("ItemsSource=\"{Binding SelectableModelInventoryRows}\"", models);
        Assert.Contains("Text=\"{Binding SelectableModelNameHeaderText}\"", models);
        Assert.Contains("Text=\"{Binding SelectableModelIw3HeaderText}\"", models);
        Assert.Contains("Text=\"{Binding SelectableModelCheckpointHeaderText}\"", models);
        Assert.Contains("Text=\"{Binding SelectableModelTypeHeaderText}\"", models);
        Assert.Contains("Text=\"{Binding SelectableModelSourceHeaderText}\"", models);
        Assert.Contains("Text=\"{Binding Iw3DepthModel}\"", models);
        Assert.Contains("Text=\"{Binding Checkpoint}\"", models);
        Assert.Contains("Text=\"{Binding Type}\"", models);
        Assert.Contains("Text=\"{Binding Source}\"", models);
        Assert.Contains("AutomationProperties.AutomationId=\"SettingsSelectableModelsEmptyState\"", models);
        Assert.Contains("Visibility=\"{Binding SettingsSelectableModelsEmptyVisibility}\"", models);
        Assert.Contains("AutomationProperties.AutomationId=\"ToolsEngineSettingsSection\"", settings);
        Assert.Contains("ItemsSource=\"{Binding ToolStatuses}\"", settings);
        Assert.Contains("AutomationProperties.AutomationId=\"SettingsRefreshToolsButton\"", settings);
        Assert.Contains("Command=\"{Binding RefreshEngineStatusCommand}\"", settings);
        Assert.Contains("Command=\"{Binding ContextActionCommand}\"", settings);
        Assert.Contains("MinWidth=\"80\"", settings);
        Assert.Contains("TextAlignment=\"Right\"", settings);
        Assert.Contains("AutomationProperties.AutomationId=\"LogsDiagnosticsSettingsSection\"", settings);
        Assert.Contains("AutomationProperties.AutomationId=\"SettingsCopyLogButton\"", settings);
        Assert.Contains("Command=\"{Binding CopyFullLogCommand}\"", settings);
        Assert.Contains("LogsDiagnosticsTechnicalDetailsText", settings);
        Assert.DoesNotContain("SettingsViewLogButton", settings);
        Assert.DoesNotContain("Command=\"{Binding ViewActivityLogCommand}\"", settings);
        Assert.DoesNotContain("Command=\"{Binding ShowTechnicalDetailsCommand}\"", settings);
        Assert.DoesNotContain("ConversionPlanStepsText", settings);
        Assert.DoesNotContain("ConversionPlanCommandPreviewText", settings);
        Assert.Contains("AutomationProperties.AutomationId=\"AboutLicensesSettingsSection\"", settings);
        Assert.Contains("AboutModelNoticesText", settings);
    }

    [Fact]
    public void PreviewInvalidationConfirmation_UsesStyledModalAndLocalizedActions()
    {
        var xaml = ReadMainWindowXaml();
        var viewModel = ReadSourceFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var modal = ExtractSourceRange(
            xaml,
            "PreviewInvalidationConfirmationBodyText",
            "Visibility=\"{Binding ConversionCompletedModalContentVisibility}\"");
        var footer = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"CancelPreviewInvalidationButton\"",
            "AutomationProperties.AutomationId=\"AcceptConversionCompletedButton\"");

        Assert.Contains("Style=\"{StaticResource V3dfyModalOverlayStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource V3dfyModalCardStyle}\"", xaml);
        Assert.Contains("PreviewInvalidationConfirmationModalContentVisibility", modal);
        Assert.Contains("Command=\"{Binding CancelPreviewInvalidationCommand}\"", footer);
        Assert.Contains("Command=\"{Binding ConfirmPreviewInvalidationCommand}\"", footer);
        Assert.Contains("Content=\"{Binding PreviewInvalidationConfirmText}\"", footer);
        Assert.Contains("\"Changing this setting requires a new preview\"", viewModel);
        Assert.Contains("\"Este cambio requiere generar un nuevo preview\"", viewModel);
        Assert.DoesNotContain("MessageBox", xaml);
        Assert.DoesNotContain("MessageBox", viewModel);
    }

    [Fact]
    public void ConversionPrimaryAction_RemainsInRightPanelSingleSlot()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("GeneratePreviewPrimaryActionButton", xaml);
        Assert.Contains("Visibility=\"{Binding GeneratePreviewPrimaryActionVisibility}\"", xaml);
        Assert.Contains("StartConversionButton", xaml);
        Assert.Contains("Visibility=\"{Binding ConvertPrimaryActionVisibility}\"", xaml);
        Assert.Contains("CancelConversionPrimaryActionButton", xaml);
        Assert.Contains("Visibility=\"{Binding CancelConversionPrimaryActionVisibility}\"", xaml);
        Assert.Equal(1, CountOccurrences(xaml, "Command=\"{Binding GeneratePreviewCommand}\""));
        Assert.Equal(1, CountOccurrences(xaml, "Command=\"{Binding StartConversionCommand}\""));
        Assert.Equal(1, CountOccurrences(xaml, "Command=\"{Binding CancelConversionCommand}\""));
    }

    [Fact]
    public void TechnicalLogs_UseNonWrappingHorizontalScrollTextAreas()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("TechnicalLogScrollBarStyle", xaml);
        Assert.Contains("ActivityLogPanelText", xaml);
        Assert.Contains("ActivityLogModalText", xaml);
        Assert.Contains("PreviewGenerationLogList", xaml);
        Assert.Contains("ItemsSource=\"{Binding PreviewGenerationLogs}\"", xaml);
        Assert.Contains("ConversionPlanCommandPreviewText", xaml);
        Assert.True(CountOccurrences(xaml, "HorizontalScrollBarVisibility=\"Auto\"") >= 5);
        Assert.True(CountOccurrences(xaml, "TextWrapping=\"NoWrap\"") >= 5);
    }

    [Fact]
    public void WpfXaml_DoesNotContainPathlessTwoWayBindings()
    {
        foreach (var (fileName, xaml) in ReadWpfXamlSources())
        {
            var explicitTwoWayBindings = FindPathlessExplicitTwoWayBindings(xaml).ToArray();
            var defaultTwoWayBindings = FindPathlessBindingsOnDefaultTwoWayProperties(xaml).ToArray();

            Assert.True(
                explicitTwoWayBindings.Length == 0,
                $"{fileName} contains pathless explicit TwoWay binding(s): {string.Join(", ", explicitTwoWayBindings)}");
            Assert.True(
                defaultTwoWayBindings.Length == 0,
                $"{fileName} contains pathless binding(s) on default TwoWay properties: {string.Join(", ", defaultTwoWayBindings)}");
        }
    }

    private static string ReadMainWindowXaml()
    {
        return ReadSourceFile("src", "V3dfy.App", "MainWindow.xaml");
    }

    private static IEnumerable<(string FileName, string Xaml)> ReadWpfXamlSources()
    {
        yield return ("MainWindow.xaml", ReadSourceFile("src", "V3dfy.App", "MainWindow.xaml"));
        yield return ("App.xaml", ReadSourceFile("src", "V3dfy.App", "App.xaml"));
    }

    private static string ReadSourceFile(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var pathParts = new string[relativePath.Length + 1];
            pathParts[0] = directory.FullName;
            Array.Copy(relativePath, 0, pathParts, 1, relativePath.Length);

            var candidate = Path.Combine(pathParts);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(relativePath)}.");
    }

    private static IEnumerable<string> FindPathlessExplicitTwoWayBindings(string xaml)
    {
        foreach (Match match in Regex.Matches(xaml, @"\{Binding(?<body>[^}]*)\}", RegexOptions.IgnoreCase))
        {
            var body = match.Groups["body"].Value;
            if (!Regex.IsMatch(body, @"(?:^|,)\s*Mode\s*=\s*TwoWay\b", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var firstSegment = body.Split(',')
                .Select(segment => segment.Trim())
                .FirstOrDefault(segment => segment.Length > 0);
            var hasPositionalPath = firstSegment is not null
                && !firstSegment.Contains('=')
                && !firstSegment.Equals("Mode", StringComparison.OrdinalIgnoreCase);
            var hasNamedPath = Regex.IsMatch(body, @"(?:^|,)\s*(Path|XPath)\s*=", RegexOptions.IgnoreCase);

            if (!hasPositionalPath && !hasNamedPath)
            {
                yield return match.Value;
            }
        }

        foreach (Match match in Regex.Matches(
                     xaml,
                     @"<Binding\b(?<attributes>[^>]*)\bMode\s*=\s*""TwoWay""(?<remaining>[^>]*)>",
                     RegexOptions.IgnoreCase))
        {
            var attributes = match.Groups["attributes"].Value + match.Groups["remaining"].Value;
            if (!Regex.IsMatch(attributes, @"\b(Path|XPath)\s*=", RegexOptions.IgnoreCase))
            {
                yield return match.Value;
            }
        }
    }

    private static IEnumerable<string> FindPathlessBindingsOnDefaultTwoWayProperties(string xaml)
    {
        const string defaultTwoWayProperties =
            "Text|Value|IsChecked|SelectedIndex|SelectedItem|SelectedValue";

        foreach (Match match in Regex.Matches(
                     xaml,
                     $@"\b(?<property>{defaultTwoWayProperties})\s*=\s*""\{{Binding\s*\}}""",
                     RegexOptions.IgnoreCase))
        {
            yield return match.Value;
        }
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

    private static string ExtractSourceRange(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0, $"Could not find start marker '{startMarker}'.");
        Assert.True(end > start, $"Could not find end marker '{endMarker}'.");

        return source[start..end];
    }
}
