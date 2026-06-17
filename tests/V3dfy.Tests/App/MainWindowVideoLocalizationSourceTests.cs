using System.Text.Json;
using V3dfy.Core.Localization;

namespace V3dfy.Tests.App;

public sealed class MainWindowVideoLocalizationSourceTests
{
    [Fact]
    public void VideoLocalizationJson_ContainsMigratedVideoKeySet()
    {
        var englishKeys = ReadLocalizationKeys("en.json");
        var spanishKeys = ReadLocalizationKeys("es.json");

        foreach (var key in RequiredVideoKeys)
        {
            Assert.Contains(key, englishKeys);
            Assert.Contains(key, spanishKeys);
        }
    }

    [Fact]
    public void VideoViewModelUserFacingProperties_UseLocalizationKeys()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        foreach (var snippet in new[]
        {
            "public string HomeVideoCardTitleText => T(LocalizationKeys.VideoHomeCardTitle);",
            "public string HomeVideoCardBodyText => T(LocalizationKeys.VideoHomeCardBody);",
            "public string SubtitleText => T(LocalizationKeys.VideoSubtitle);",
            "public string SelectSourceTitle => T(LocalizationKeys.VideoSourceTitle);",
            "public string DropVideoText => T(LocalizationKeys.VideoSourceDrop);",
            "public string VideoAnalysisTitle => T(LocalizationKeys.VideoAnalysisTitle);",
            "public string RecommendedSetupTitle => T(LocalizationKeys.VideoRecommendationTitle);",
            "public string PlanOptionsTitle => T(LocalizationKeys.VideoSetupPlanOptionsTitle);",
            "public string LocalModelSelectionLabel => T(LocalizationKeys.VideoModelSelectorLabel);",
            "public string PreviewRequiredTitleText => T(LocalizationKeys.VideoPreviewRequiredTitle);",
            "public string GeneratePreviewText => T(LocalizationKeys.VideoPreviewGenerate);",
            "public string PreviewPlayText => T(LocalizationKeys.VideoPlayerPlay);",
            "public string ConversionRunningTitle => T(LocalizationKeys.VideoConversionRunningTitle);",
            "public string ConversionSummaryTitle => T(LocalizationKeys.VideoConversionSummaryTitle);",
            "public string ConversionReadinessTitle => T(LocalizationKeys.VideoReadinessTitle);",
            "public string ActivityLogTitle => T(LocalizationKeys.VideoLogTitle);",
        })
        {
            Assert.Contains(snippet, source);
        }

        foreach (var literal in MigratedVideoLiteralNeedles)
        {
            Assert.DoesNotContain(literal, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void VideoOptionLabels_AreKeyBasedAndBoundThroughDisplayName()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var optionModel = ReadRepoFile("src", "V3dfy.App", "ViewModels", "LocalizedOptionViewModel.cs");
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var setupStep = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ThreeDSetupStepContent\"",
            "AutomationProperties.AutomationId=\"ConversionPlanStepContent\"");

        Assert.Contains("public string? LocalizationKey { get; }", optionModel);
        Assert.Contains("_outputPresetOptions =", source);
        Assert.Contains("new(TargetDevicePresets.Recommended3dTv, LocalizationKeys.VideoOptionOutputProfileRecommended", source);
        Assert.Contains("new(TargetDevicePresets.MaximumCompatibility, LocalizationKeys.VideoOptionOutputProfileMaximumCompatibility", source);
        Assert.Contains("new(AiQualityPreset.Fast, LocalizationKeys.VideoOptionQualityFast", source);
        Assert.Contains("new(ThreeDIntensity.Low, LocalizationKeys.VideoOptionIntensityLow", source);
        Assert.Contains("new(ThreeDOutputFormat.HalfTopBottom, LocalizationKeys.VideoOptionOutputFormatHalfTopBottom", source);
        Assert.Contains("private string QualityPresetText(AiQualityPreset value) => value switch", source);
        Assert.Contains("AiQualityPreset.Fast => T(LocalizationKeys.VideoOptionQualityFast)", source);
        Assert.Contains("private string TargetPresetName(TargetDevicePreset preset) => preset.Id switch", source);
        Assert.Contains("T(LocalizationKeys.VideoOptionOutputProfileRecommended)", source);
        Assert.DoesNotContain("QualityPresetText(AiQualityPreset value, bool useSpanish)", source);
        Assert.DoesNotContain("ThreeDIntensityText(ThreeDIntensity value, bool useSpanish)", source);
        Assert.DoesNotContain("ThreeDOutputFormatText(ThreeDOutputFormat value, bool useSpanish)", source);

        Assert.Contains("DisplayMemberPath=\"DisplayName\"", setupStep);
        Assert.Contains("SelectedValuePath=\"Value\"", setupStep);
        Assert.Contains("SelectedValue=\"{Binding SelectedOutputPreset}\"", setupStep);
        Assert.Contains("SelectedValue=\"{Binding SelectedQualityPreset}\"", setupStep);
        Assert.Contains("SelectedValue=\"{Binding SelectedThreeDIntensity}\"", setupStep);
        Assert.Contains("SelectedValue=\"{Binding SelectedThreeDOutputFormat}\"", setupStep);
    }

    [Fact]
    public void VideoLogsAndProgress_MapDtoBoundariesThroughLocalizationKeys()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        foreach (var keySnippet in new[]
        {
            "LocalizationKeys.VideoLogShellReady",
            "LocalizationKeys.VideoLogSelectedFileFormat",
            "LocalizationKeys.VideoLogAnalysisStarted",
            "LocalizationKeys.VideoLogAnalysisCompleted",
            "LocalizationKeys.VideoLogRecommendationGenerated",
            "LocalizationKeys.VideoLogPreviewStarted",
            "LocalizationKeys.VideoLogPreviewCanceled",
            "LocalizationKeys.VideoLogPreviewFailedFormat",
            "LocalizationKeys.VideoLogConversionStarted",
            "LocalizationKeys.VideoLogConversionFailedFormat",
            "LocalizationKeys.VideoLogConversionPrimaryOutputGeneratedFormat",
            "LocalizationKeys.VideoLogConversionPartialFileCleaned",
            "LocalizationKeys.VideoLogRuntimeDownloadWarning",
            "LocalizationKeys.VideoLogPreviewSavedFormat",
            "LocalizationKeys.VideoOutputOpenPreviewSkippedNoReady",
            "LocalizationKeys.VideoOutputOpenFinalFileMissing",
            "LocalizationKeys.VideoErrorAnalysisFailed",
            "LocalizationKeys.VideoErrorPreviewUnexpectedFormat",
            "LocalizationKeys.VideoPreviewValidationInvalidFormat",
        })
        {
            Assert.Contains(keySnippet, source);
        }

        Assert.Contains("private void AddVideoLogResolved(string message) =>", source);
        Assert.Contains("AddLog(message, message);", source);

        Assert.Contains("private string LocalizePreviewGateStatus(PreviewConversionGateResult gate)", source);
        Assert.Contains("private string LocalizeConversionStartGateStatus(ConversionExecutionStartGateResult startGate)", source);
        Assert.Contains("private string LocalizeConversionReadinessIssue(ConversionReadinessIssue issue)", source);
        Assert.Contains("private string LocalizeConversionExecutionLog(ConversionExecutionLogEntry log)", source);
        Assert.Contains("AddVideoLogResolved(LocalizePreviewGenerationSummary(result.EnglishSummary));", source);
        Assert.Contains("AddVideoLogResolved(LocalizeConversionStartGateLog(startGate));", source);
        Assert.DoesNotContain("Text(gate.EnglishStatus, gate.SpanishStatus)", source);
        Assert.DoesNotContain("Text(_conversionReadiness.EnglishStatus, _conversionReadiness.SpanishStatus)", source);
        Assert.DoesNotContain("Text(log.EnglishMessage, log.SpanishMessage)", source);
        Assert.DoesNotContain("AddLog(result.EnglishSummary, result.SpanishSummary);", source);
        Assert.DoesNotContain("AddLog(startGate.EnglishLogMessage, startGate.SpanishLogMessage);", source);
    }

    [Fact]
    public void SelectedLanguage_DoesNotResetVideoStateAndRefreshesVideoOptionLabels()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var property = ExtractSourceRange(
            source,
            "public string SelectedLanguage",
            "public IReadOnlyList<string> ThemeOptions");
        var refresh = ExtractSourceRange(
            source,
            "private void UpdatePlanOptionLanguages()",
            "private void RaiseSidebarPropertiesChanged()");

        Assert.Contains("ApplyUiOnlyRefresh", property);
        Assert.Contains("RaiseLocalizedPropertiesChanged();", property);
        Assert.Contains("UpdatePlanOptionLanguages();", property);
        Assert.Contains("UpdateLogLanguages();", property);
        Assert.Contains("RefreshMetricLanguage();", property);
        Assert.Contains("UpdateLocalModelSelectionCandidates(regenerateCurrentPlan: false)", property);
        Assert.Contains("foreach (var option in OutputPresetOptions)", refresh);
        Assert.Contains("foreach (var option in QualityPresetOptions)", refresh);
        Assert.Contains("foreach (var option in ThreeDIntensityOptions)", refresh);
        Assert.Contains("foreach (var option in ThreeDOutputFormatOptions)", refresh);

        foreach (var forbidden in new[]
        {
            "SelectedVideoPath = null",
            "_analysis = null",
            "_conversionPlan = null",
            "_conversionRecommendation = null",
            "_conversionReadiness = null",
            "PreviewWorkflowState.NotGenerated",
            "Logs.Clear",
            "_completedConversionResult = null",
            "_conversionExecutionState = ConversionExecutionState.NotStarted();",
        })
        {
            Assert.DoesNotContain(forbidden, property);
        }
    }

    private static readonly string[] RequiredVideoKeys =
    [
        LocalizationKeys.VideoTitle,
        LocalizationKeys.VideoIntro,
        LocalizationKeys.VideoHomeCardTitle,
        LocalizationKeys.VideoHomeCardBody,
        LocalizationKeys.VideoSourceTitle,
        LocalizationKeys.VideoSourceSelectButton,
        LocalizationKeys.VideoSourceAnalyzeButton,
        LocalizationKeys.VideoAnalysisTitle,
        LocalizationKeys.VideoAnalysisDuration,
        LocalizationKeys.VideoAnalysisResolution,
        LocalizationKeys.VideoAnalysisFps,
        LocalizationKeys.VideoRecommendationTitle,
        LocalizationKeys.VideoSetupPlanOptionsTitle,
        LocalizationKeys.VideoModelSelectorLabel,
        LocalizationKeys.VideoModelHelpTitle,
        LocalizationKeys.VideoOutputPathLabel,
        LocalizationKeys.VideoOutputProfileLabel,
        LocalizationKeys.VideoConversionPlanTitle,
        LocalizationKeys.VideoConversionStart,
        LocalizationKeys.VideoConversionCancel,
        LocalizationKeys.VideoConversionRunningTitle,
        LocalizationKeys.VideoConversionCompletedTitle,
        LocalizationKeys.VideoPreviewRequiredTitle,
        LocalizationKeys.VideoPreviewGenerate,
        LocalizationKeys.VideoPreviewCancel,
        LocalizationKeys.VideoPreviewReadyTitle,
        LocalizationKeys.VideoPreviewStatusReady,
        LocalizationKeys.VideoPreviewMaximumDuration,
        LocalizationKeys.VideoPlayerPlay,
        LocalizationKeys.VideoPlayerPause,
        LocalizationKeys.VideoPlayerMute,
        LocalizationKeys.VideoPlayerUnmute,
        LocalizationKeys.VideoReadinessTitle,
        LocalizationKeys.VideoReadinessMissingTools,
        LocalizationKeys.VideoReadinessAcceptedPreviewFileMissing,
        LocalizationKeys.VideoLogTitle,
        LocalizationKeys.VideoLogEmpty,
        LocalizationKeys.VideoLogCopyPreview,
        LocalizationKeys.VideoLogSelectedFileFormat,
        LocalizationKeys.VideoLogAnalysisStarted,
        LocalizationKeys.VideoLogPreviewStarted,
        LocalizationKeys.VideoLogConversionStarted,
        LocalizationKeys.VideoLogRuntimeDownloadWarning,
        LocalizationKeys.VideoLogPreviewSavedFormat,
        LocalizationKeys.VideoErrorAnalysisFailed,
        LocalizationKeys.VideoErrorPreviewUnexpectedFormat,
        LocalizationKeys.VideoOptionOutputProfileRecommended,
        LocalizationKeys.VideoOptionQualityBalanced,
        LocalizationKeys.VideoOptionIntensityMedium,
        LocalizationKeys.VideoOptionOutputFormatHalfTopBottom,
        LocalizationKeys.CommonViewLog,
        LocalizationKeys.CommonLogCopied,
        LocalizationKeys.CommonCouldNotCopyLog,
    ];

    private static readonly string[] MigratedVideoLiteralNeedles =
    [
        "\"Preview required\"",
        "\"Cancel preview\"",
        "\"Preview ready\"",
        "\"Copy preview log\"",
        "\"Live conversion\"",
        "\"Conversion summary\"",
        "\"Output profile\"",
        "\"Maximum preview duration is 1 minute 30 seconds\"",
        "\"Preview generation was canceled.\"",
        "\"Stale preview partial file was cleaned.\"",
    ];

    private static HashSet<string> ReadLocalizationKeys(string fileName)
    {
        using var document = JsonDocument.Parse(ReadRepoFile("src", "V3dfy.App", "Localization", fileName));
        return document.RootElement
            .GetProperty("strings")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string ReadRepoFile(params string[] relativePath)
    {
        var repoRoot = FindRepoRoot();
        return File.ReadAllText(Path.Combine([repoRoot, .. relativePath]));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "v3dfy.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
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
