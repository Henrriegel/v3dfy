using System.Text.RegularExpressions;

namespace V3dfy.Tests.App;

public sealed class MainWindowXamlStructureTests
{
    [Fact]
    public void ConversionPlanTab_ContainsOutputProfileSelectorBeforeOutputContainer()
    {
        var xaml = ReadMainWindowXaml();

        var profileSelectorIndex = xaml.IndexOf(
            "SelectedOutputPreset",
            StringComparison.Ordinal);
        var outputContainerIndex = xaml.IndexOf(
            "SelectedOutputContainer",
            StringComparison.Ordinal);

        Assert.True(profileSelectorIndex >= 0);
        Assert.True(outputContainerIndex >= 0);
        Assert.True(profileSelectorIndex < outputContainerIndex);
        Assert.Contains("ShowProfileDetailsCommand", xaml);
        Assert.Contains("ProfileDetailsBodyText", xaml);
    }

    [Fact]
    public void MainWindow_DoesNotKeepPermanentSelectedOutputPresetCard()
    {
        var xaml = ReadMainWindowXaml();

        Assert.DoesNotContain("RecommendedPresetTitle", xaml);
        Assert.DoesNotContain("Grid.Row=\"4\" Style=\"{StaticResource CardStyle}\"", xaml);
    }

    [Fact]
    public void ConversionPlanTab_HidesLgOptionsBehindProfileVisibilityBinding()
    {
        var xaml = ReadMainWindowXaml();
        var appResources = ReadAppXaml();

        Assert.Contains("Visibility=\"{Binding LgCompatibilityOptionsVisibility}\"", xaml);
        Assert.Contains("CreateLgCompatibilityCopyText", xaml);
        Assert.Contains("PreferLgCompatibilityCopyWhenOpeningText", xaml);
        Assert.Contains("LgCompatibilityCopyExplanationText", xaml);
        Assert.Contains("InputBackgroundBrush", xaml);
        Assert.Contains("CardBorderBrush", xaml);
        Assert.Contains("BorderThickness=\"1\"", xaml);
        Assert.Contains("<Style TargetType=\"CheckBox\">", appResources);
        Assert.Contains("PrimaryTextBrush", appResources);
    }

    [Fact]
    public void SystemStatusConversionTab_ContainsRequiredSelectedConfigurationPreviewStep()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("PreviewStepTitleText", xaml);
        Assert.Contains("PreviewRequiredInstructionText", xaml);
        Assert.Contains("GeneratePreviewCommand", xaml);
        Assert.Contains("PreviewFromText", xaml);
        Assert.Contains("PreviewToText", xaml);
        Assert.Contains("PreviewTimeRangeValidationText", xaml);
        Assert.Contains("StartConversionButton", xaml);
        Assert.Contains("PreviewRequirementVisibility", xaml);
        Assert.DoesNotContain("PreviewMaximumDurationText", xaml);
        Assert.DoesNotContain("PreviewGateStatusText", xaml);
        Assert.DoesNotContain("PreviewGateDetailText", xaml);
        Assert.DoesNotContain("DeletePreviewCommand", xaml);
        Assert.DoesNotContain("PreviewAll", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ComparisonTable", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConversionPrimaryAction_UsesOneSlotForGeneratePreviewOrConvert()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("GeneratePreviewPrimaryActionButton", xaml);
        Assert.Contains("Visibility=\"{Binding GeneratePreviewPrimaryActionVisibility}\"", xaml);
        Assert.Contains("Visibility=\"{Binding ConvertPrimaryActionVisibility}\"", xaml);
        Assert.Contains("CancelConversionPrimaryActionButton", xaml);
        Assert.Contains("Visibility=\"{Binding CancelConversionPrimaryActionVisibility}\"", xaml);
        Assert.Contains("Grid.Row=\"1\"", xaml);
        Assert.Equal(1, CountOccurrences(xaml, "Command=\"{Binding GeneratePreviewCommand}\""));
        Assert.Equal(1, CountOccurrences(xaml, "Command=\"{Binding StartConversionCommand}\""));
        Assert.Equal(1, CountOccurrences(xaml, "Command=\"{Binding CancelConversionCommand}\""));
    }

    [Fact]
    public void PreviewRequirementCard_CollapsesAfterPreviewAcceptance()
    {
        var xaml = ReadMainWindowXaml();

        var previewRequirementIndex = xaml.IndexOf(
            "Visibility=\"{Binding PreviewRequirementVisibility}\"",
            StringComparison.Ordinal);
        var previewFromIndex = xaml.IndexOf("PreviewFromText", StringComparison.Ordinal);
        var generatePrimaryIndex = xaml.IndexOf(
            "GeneratePreviewPrimaryActionButton",
            StringComparison.Ordinal);

        Assert.True(previewRequirementIndex >= 0);
        Assert.True(previewFromIndex > previewRequirementIndex);
        Assert.True(generatePrimaryIndex > previewFromIndex);
    }

    [Fact]
    public void PreviewRequirementCard_ContainsOnlyEssentialRangeAndValidationControls()
    {
        var xaml = ReadMainWindowXaml();
        var previewCard = ExtractSourceRange(
            xaml,
            "Visibility=\"{Binding PreviewRequirementVisibility}\"",
            "Text=\"{Binding ConversionReadinessStatusLabel}\"");

        Assert.Contains("PreviewStepTitleText", previewCard);
        Assert.Contains("PreviewRequiredInstructionText", previewCard);
        Assert.Contains("PreviewFromText", previewCard);
        Assert.Contains("PreviewToText", previewCard);
        Assert.Contains("PreviewTimeRangeValidationText", previewCard);
        Assert.DoesNotContain("PreviewMaximumDurationText", previewCard);
        Assert.DoesNotContain("PreviewGateStatusText", previewCard);
        Assert.DoesNotContain("PreviewGateDetailText", previewCard);
        Assert.DoesNotContain("PreviewDurationText", previewCard);
        Assert.DoesNotContain("PreviewStartTimeText", previewCard);
    }

    [Fact]
    public void PreviewRangeTextBoxes_BindEditabilityToCanEditPreviewTimeRange()
    {
        var xaml = ReadMainWindowXaml();
        var previewCard = ExtractSourceRange(
            xaml,
            "Visibility=\"{Binding PreviewRequirementVisibility}\"",
            "Text=\"{Binding ConversionReadinessStatusLabel}\"");

        Assert.Contains("Text=\"{Binding PreviewFromText,", previewCard);
        Assert.Contains("Text=\"{Binding PreviewToText,", previewCard);
        Assert.Equal(2, CountOccurrences(previewCard, "IsEnabled=\"{Binding CanEditPreviewTimeRange}\""));
    }

    [Fact]
    public void PreviewActions_MoveReviewActionsToModals()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("CancelPreviewCommand", xaml);
        Assert.Contains("OpenPreviewCommand", xaml);
        Assert.Contains("ContinuePreviewCommand", xaml);
        Assert.Contains("PreviewGeneratingModalContentVisibility", xaml);
        Assert.Contains("PreviewReadyModalContentVisibility", xaml);
        Assert.Contains("OpenPreviewExternallyText", xaml);
    }

    [Fact]
    public void ActivityLog_ContainsCopyableViewLogModal()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("ViewActivityLogCommand", xaml);
        Assert.Contains("ViewLogText", xaml);
        Assert.Contains("ActivityLogModalText", xaml);
        Assert.Contains("CopyFullLogCommand", xaml);
        Assert.Contains("CopyFullLogText", xaml);
        Assert.Contains("CloseActivityLogCommand", xaml);
        Assert.Contains("<TextBox Grid.Row=\"1\"", xaml);
        Assert.Contains("IsReadOnly=\"True\"", xaml);
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
        Assert.Contains("Text=\"{Binding ., Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("Text=\"{Binding PreviewGenerationLogText", xaml);
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

    [Fact]
    public void PreviewGeneratingModal_ShowsStageMetricsEngineAndAppendFriendlyLog()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("PreviewMetricsHeaderVisibility", xaml);
        Assert.Contains("PreviewCpuUsageText", xaml);
        Assert.Contains("PreviewRamUsageText", xaml);
        Assert.Contains("PreviewGpuUsageText", xaml);
        Assert.Contains("PreviewVramUsageText", xaml);
        Assert.Contains("PreviewStageText", xaml);
        Assert.Contains("PreviewEngineText", xaml);
        Assert.Contains("PreviewRunningWithText", xaml);
        Assert.Contains("PreviewGpuMetricsNoteText", xaml);
        Assert.Contains("PreviewGpuMetricsStatusText", xaml);
        Assert.Contains("PreviewGenerationLogList", xaml);
        Assert.Contains("ScrollViewer.HorizontalScrollBarVisibility=\"Auto\"", xaml);
        Assert.Contains("CopyPreviewLogButton", xaml);
        Assert.Contains("CopyPreviewLogCommand", xaml);
        Assert.Contains("CopyPreviewLogText", xaml);
        Assert.Contains("CancelPreviewCommand", xaml);
    }

    [Fact]
    public void PreviewGeneratingModalFooter_UsesDistinctCopyAndCancelStyles()
    {
        var xaml = ReadMainWindowXaml();
        var footer = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"CopyFullLogButton\"",
            "AutomationProperties.AutomationId=\"ContinuePreviewButton\"");
        var appResources = ReadAppXaml();

        var copyPreviewIndex = footer.IndexOf(
            "AutomationProperties.AutomationId=\"CopyPreviewLogButton\"",
            StringComparison.Ordinal);
        var cancelPreviewIndex = footer.IndexOf(
            "AutomationProperties.AutomationId=\"CancelPreviewButton\"",
            StringComparison.Ordinal);
        var copyPreviewBlock = ExtractSourceRange(
            footer,
            "AutomationProperties.AutomationId=\"CopyPreviewLogButton\"",
            "AutomationProperties.AutomationId=\"CancelPreviewButton\"");
        var cancelPreviewBlock = ExtractSourceRange(
            footer,
            "AutomationProperties.AutomationId=\"CancelPreviewButton\"",
            "AutomationProperties.AutomationId=\"OpenPreviewExternallyButton\"");

        Assert.True(copyPreviewIndex >= 0);
        Assert.True(cancelPreviewIndex > copyPreviewIndex);
        Assert.Contains("Style=\"{StaticResource SecondaryButtonStyle}\"", copyPreviewBlock);
        Assert.DoesNotContain("Style=\"{StaticResource DestructiveButtonStyle}\"", copyPreviewBlock);
        Assert.Contains("Style=\"{StaticResource DestructiveButtonStyle}\"", cancelPreviewBlock);
        Assert.DoesNotContain("Style=\"{StaticResource SecondaryButtonStyle}\"", cancelPreviewBlock);
        Assert.Contains("x:Key=\"DestructiveButtonStyle\"", appResources);
        Assert.Contains("DestructiveBrush", appResources);
        Assert.Contains("DestructiveHoverBrush", appResources);
    }

    [Fact]
    public void ConversionPrimaryAction_UsesDestructiveStyleForLiveCancelOnly()
    {
        var xaml = ReadMainWindowXaml();
        var appResources = ReadAppXaml();
        var startConversionBlock = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"StartConversionButton\"",
            "AutomationProperties.AutomationId=\"CancelConversionPrimaryActionButton\"");
        var cancelConversionBlock = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"CancelConversionPrimaryActionButton\"",
            "</Grid>");

        Assert.Contains("Style=\"{StaticResource PrimaryCtaButtonStyle}\"", startConversionBlock);
        Assert.DoesNotContain("Style=\"{StaticResource DestructiveButtonStyle}\"", startConversionBlock);
        Assert.Contains("Style=\"{StaticResource DestructiveButtonStyle}\"", cancelConversionBlock);
        Assert.Contains("CancelConversionCommand", cancelConversionBlock);
        Assert.Contains("x:Key=\"DestructiveButtonStyle\"", appResources);
        Assert.Contains("DestructiveHoverBrush", appResources);
        Assert.Contains("DestructivePressedBrush", appResources);
        var destructiveStyle = ExtractSourceRange(
            appResources,
            "x:Key=\"DestructiveButtonStyle\"",
            "x:Key=\"PrimaryCtaButtonStyle\"");
        Assert.Contains("DestructiveHoverBrush", destructiveStyle);
        Assert.Contains("DestructivePressedBrush", destructiveStyle);
        Assert.DoesNotContain("AccentHoverBrush", destructiveStyle);
        Assert.DoesNotContain("AccentPressedBrush", destructiveStyle);
        Assert.DoesNotContain("DataTrigger Binding=\"{Binding IsConversionRunning}\"", startConversionBlock);
    }

    [Fact]
    public void LogCopyNotification_IsTopLevelAndModalSafe()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("AutomationProperties.AutomationId=\"LogCopyNotification\"", xaml);
        Assert.Contains("Panel.ZIndex=\"20\"", xaml);
        Assert.Contains("IsHitTestVisible=\"False\"", xaml);
        Assert.Contains("Visibility=\"{Binding LogCopyNotificationVisibility}\"", xaml);
        Assert.Contains("Text=\"{Binding LogCopyNotificationText}\"", xaml);
    }

    [Fact]
    public void PreviewReadyModal_EmbedsPreviewAndExternalFallback()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("<MediaElement", xaml);
        Assert.Contains("LoadedBehavior=\"Manual\"", xaml);
        Assert.Contains("UnloadedBehavior=\"Manual\"", xaml);
        Assert.Contains("ScrubbingEnabled=\"True\"", xaml);
        Assert.Contains("Source=\"{Binding PreviewMediaSource}\"", xaml);
        Assert.Contains("MediaOpened=\"OnPreviewMediaOpened\"", xaml);
        Assert.Contains("MediaEnded=\"OnPreviewMediaEnded\"", xaml);
        Assert.Contains("MediaFailed=\"OnPreviewMediaFailed\"", xaml);
        Assert.Contains("PreviewPlaybackFallbackText", xaml);
        Assert.Contains("PreviewPlayPauseButton", xaml);
        Assert.Contains("Style=\"{StaticResource IconButtonStyle}\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding PreviewPlayText}\"", xaml);
        Assert.Contains("ToolTip=\"{Binding PreviewPlayText}\"", xaml);
        Assert.Contains("PreviewTimelineSlider", xaml);
        Assert.Contains("Style=\"{StaticResource PreviewSliderStyle}\"", xaml);
        Assert.Contains("PreviewVolumeIcon", xaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding PreviewVolumeText}\"", xaml);
        Assert.Contains("PreviewVolumeSlider", xaml);
        Assert.Contains("PreviewMuteToggleButton", xaml);
        Assert.Contains("Style=\"{StaticResource IconToggleButtonStyle}\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding PreviewMuteText}\"", xaml);
        Assert.Contains("ToolTip=\"{Binding PreviewMuteText}\"", xaml);
        Assert.DoesNotContain("PreviewMuteCheckBox", xaml);
        Assert.DoesNotContain("OnPreviewReplayClicked", xaml);
        Assert.DoesNotContain("Content=\"{Binding PreviewReplayText}\"", xaml);
        Assert.DoesNotContain("Text=\"{Binding PreviewVolumeText}\"", xaml);
        Assert.DoesNotContain("Content=\"{Binding PreviewMutedText}\"", xaml);
        Assert.Contains("PreviewPlaybackStatusText", xaml);
        Assert.Contains("OpenPreviewExternallyButton", xaml);
        Assert.Contains("OpenPreviewExternallyText", xaml);
    }

    [Fact]
    public void PreviewSliders_UseAppPreviewSliderStyle()
    {
        var xaml = ReadMainWindowXaml();
        var appResources = ReadAppXaml();
        var readyModal = ExtractSourceRange(
            xaml,
            "Visibility=\"{Binding PreviewReadyModalContentVisibility}\"",
            "PreviewPlaybackStatusText");
        var sliderStyle = ExtractSourceRange(
            appResources,
            "x:Key=\"PreviewSliderStyle\"",
            "<Style TargetType=\"CheckBox\">");

        Assert.Equal(2, CountOccurrences(readyModal, "Style=\"{StaticResource PreviewSliderStyle}\""));
        Assert.Contains("PreviewTimelineSlider", readyModal);
        Assert.Contains("PreviewVolumeSlider", readyModal);
        Assert.Contains("PreviewSliderThumbStyle", appResources);
        Assert.Contains("PreviewSliderTrackButtonStyle", appResources);
        Assert.Contains("PART_Track", sliderStyle);
        Assert.Contains("SliderFocusRing", sliderStyle);
        Assert.Contains("IsMouseOver", sliderStyle);
        Assert.Contains("IsKeyboardFocusWithin", sliderStyle);
        Assert.Contains("IsEnabled", sliderStyle);
    }

    [Fact]
    public void PreviewVolumeSlider_HasTrackClickHandlerWithoutChangingTimeline()
    {
        var xaml = ReadMainWindowXaml();
        var readyModal = ExtractSourceRange(
            xaml,
            "Visibility=\"{Binding PreviewReadyModalContentVisibility}\"",
            "PreviewPlaybackStatusText");
        var timelineSlider = ExtractSourceRange(
            readyModal,
            "x:Name=\"PreviewTimelineSlider\"",
            "x:Name=\"PreviewTimeText\"");
        var volumeSlider = ExtractSourceRange(
            readyModal,
            "x:Name=\"PreviewVolumeSlider\"",
            "x:Name=\"PreviewMuteToggleButton\"");

        Assert.Contains("PreviewMouseLeftButtonDown=\"OnPreviewVolumeSliderPreviewMouseLeftButtonDown\"", volumeSlider);
        Assert.Contains("Style=\"{StaticResource PreviewSliderStyle}\"", volumeSlider);
        Assert.Contains("Style=\"{StaticResource PreviewSliderStyle}\"", timelineSlider);
        Assert.DoesNotContain("OnPreviewVolumeSliderPreviewMouseLeftButtonDown", timelineSlider);
    }

    [Fact]
    public void CheckBoxStyle_IsAppStyledForInteractiveStates()
    {
        var appResources = ReadAppXaml();
        var checkBoxStyle = ExtractSourceRange(
            appResources,
            "<Style TargetType=\"CheckBox\">",
            "<Style TargetType=\"ComboBox\">");
        var mainWindow = ReadMainWindowXaml();

        Assert.Contains("CheckBoxBox", checkBoxStyle);
        Assert.Contains("CheckMark", checkBoxStyle);
        Assert.Contains("BorderThickness=\"1\"", checkBoxStyle);
        Assert.Contains("InputBackgroundBrush", checkBoxStyle);
        Assert.Contains("CardBorderBrush", checkBoxStyle);
        Assert.Contains("IsChecked", checkBoxStyle);
        Assert.Contains("IsMouseOver", checkBoxStyle);
        Assert.Contains("IsKeyboardFocused", checkBoxStyle);
        Assert.Contains("IsEnabled", checkBoxStyle);
        Assert.Contains("AccentBrush", checkBoxStyle);
        Assert.Contains("AccentHoverBrush", checkBoxStyle);
        Assert.Contains("DisabledBackgroundBrush", checkBoxStyle);
        Assert.DoesNotContain("<Style TargetType=\"CheckBox\">", mainWindow);
        Assert.Contains("CreateLgCompatibilityCopyText", mainWindow);
        Assert.Contains("OpenOutputWhenFinishedText", mainWindow);
    }

    private static string ReadMainWindowXaml()
    {
        return ReadSourceFile("src", "V3dfy.App", "MainWindow.xaml");
    }

    private static string ReadAppXaml()
    {
        return ReadSourceFile("src", "V3dfy.App", "App.xaml");
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

        Assert.True(start >= 0);
        Assert.True(end > start);

        return source[start..end];
    }
}
