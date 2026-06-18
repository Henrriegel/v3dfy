namespace V3dfy.Tests.App;

public sealed class MainWindowPreviewInvalidationSourceTests
{
    [Fact]
    public void PreviewAffectingSettings_DeferThroughInvalidationConfirmation()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("TryDeferPreviewInvalidatingChange(() => SelectedOutputPreset = value)", source);
        Assert.Contains("TryDeferPreviewInvalidatingChange(() => SelectedOutputContainer = value)", source);
        Assert.Contains("TryDeferPreviewInvalidatingChange(() => SelectedQualityPreset = value)", source);
        Assert.Contains("TryDeferPreviewInvalidatingChange(() => SelectedThreeDIntensity = value)", source);
        Assert.Contains("TryDeferPreviewInvalidatingChange(() => SelectedThreeDOutputFormat = value)", source);
        Assert.Contains("TryDeferPreviewInvalidatingChange(() => SelectedLocalModelCandidate = value)", source);
        Assert.Contains("TryDeferPreviewInvalidatingChange(() => PreviewFromText = value)", source);
        Assert.Contains("TryDeferPreviewInvalidatingChange(() => PreviewToText = value)", source);
        Assert.Contains("ConfirmPreviewInvalidatingChangeAsync", source);
        Assert.Contains("ShouldConfirmPreviewInvalidatingChange", source);
        Assert.Contains("_previewState.Status is PreviewGenerationStatus.Ready or PreviewGenerationStatus.Accepted", source);
        Assert.Contains("IsPreviewFingerprintCurrent()", source);
    }

    [Fact]
    public void PreviewInvalidationConfirmation_CancelRestoresAndConfirmAppliesPendingChange()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var deferMethod = ExtractSourceRange(
            source,
            "private bool TryDeferPreviewInvalidatingChange",
            "private Task<bool> ConfirmPreviewInvalidatingChangeAsync");
        var confirmMethod = ExtractSourceRange(
            source,
            "private void ConfirmPreviewInvalidation",
            "private void CancelPreviewInvalidation");
        var cancelMethod = ExtractSourceRange(
            source,
            "private void CancelPreviewInvalidation",
            "private void ConfirmModelPackImport");
        var resetMethod = ExtractSourceRange(
            source,
            "private void ResetPreviewInvalidationConfirmationModal",
            "private bool ShouldConfirmPreviewInvalidatingChange");

        Assert.Contains("_pendingPreviewInvalidatingChange = applyChange;", deferMethod);
        Assert.Contains("ShowPreviewInvalidationConfirmationModal();", deferMethod);
        Assert.Contains("_isApplyingPreviewInvalidatingChange = true;", confirmMethod);
        Assert.Contains("pendingChange();", confirmMethod);
        Assert.Contains("_isApplyingPreviewInvalidatingChange = false;", confirmMethod);
        Assert.Contains("completion?.TrySetResult(true);", confirmMethod);
        Assert.Contains("completion?.TrySetResult(false);", cancelMethod);
        Assert.Contains("_pendingPreviewInvalidatingChange = null;", resetMethod);
    }

    [Fact]
    public void LanguageThemeOutputPathAndLgCopy_DoNotTriggerPreviewInvalidationConfirmation()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var languageProperty = ExtractSourceRange(
            source,
            "public string SelectedLanguage",
            "public IReadOnlyList<string> ThemeOptions");
        var themeProperty = ExtractSourceRange(
            source,
            "public string SelectedTheme",
            "public string SubtitleText");
        var outputPathMethods = ExtractSourceRange(
            source,
            "private void CommitOutputPath",
            "private string? GetAutomaticOutputPath");
        var lgCopyProperty = ExtractSourceRange(
            source,
            "public bool CreateLgCompatibilityCopy",
            "public string PreferLgCompatibilityCopyWhenOpeningText");

        Assert.DoesNotContain("TryDeferPreviewInvalidatingChange", languageProperty);
        Assert.DoesNotContain("TryDeferPreviewInvalidatingChange", themeProperty);
        Assert.DoesNotContain("TryDeferPreviewInvalidatingChange", outputPathMethods);
        Assert.DoesNotContain("TryDeferPreviewInvalidatingChange", lgCopyProperty);
        Assert.DoesNotContain("ResetPreviewConversionStageForPreviewAffectingChange", languageProperty);
        Assert.DoesNotContain("ResetPreviewConversionStageForPreviewAffectingChange", themeProperty);
        Assert.DoesNotContain("ResetPreviewConversionStageForPreviewAffectingChange", outputPathMethods);
        Assert.DoesNotContain("PreviewStageResetNotice", languageProperty);
        Assert.DoesNotContain("PreviewStageResetNotice", themeProperty);
        Assert.DoesNotContain("PreviewStageResetNotice", outputPathMethods);
        Assert.Contains("affectsPreview: false", lgCopyProperty);
        Assert.Contains("ApplyUiOnlyRefresh", languageProperty);
        Assert.Contains("ApplyUiOnlyRefresh(() => _themeService.Apply(value))", themeProperty);
    }

    [Fact]
    public void PreviewConversionStage_GatesPreviewActionsAndResetsForPreviewAffectingChanges()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var canGeneratePreview = ExtractSourceRange(
            source,
            "public bool CanGeneratePreview =>",
            "public bool CanCancelPreview");
        var canStartConversion = ExtractSourceRange(
            source,
            "public bool CanStartConversion =>",
            "public bool CanStartOrCancelConversion");
        var footerVisibility = ExtractSourceRange(
            source,
            "public Visibility ContinueWithConversionFooterVisibility =>",
            "public string ContinueWithConversionText");
        var planOptionChanged = ExtractSourceRange(
            source,
            "private void PlanOptionChanged(",
            "private void CommitOutputPath");
        var previewRangeChanged = ExtractSourceRange(
            source,
            "private void PreviewTimeRangeChanged()",
            "private void SetDefaultPreviewTimeRangeFromAnalysis");
        var setSelectedVideo = ExtractSourceRange(
            source,
            "private void SetSelectedVideo",
            "private void ResetAnalysisState");
        var selectedModelChange = ExtractSourceRange(
            source,
            "private void SetSelectedLocalModelCandidate",
            "private ConversionExecutionStartGateResult EvaluateConversionStartGate");

        Assert.Contains("public bool HasEnteredPreviewConversionStage", source);
        Assert.Contains("public bool CanEnterPreviewConversionStage", source);
        Assert.Contains("ContinueWithConversionCommand = new RelayCommand", source);
        Assert.Contains("private void ContinueWithConversion()", source);
        Assert.Contains("SetHasEnteredPreviewConversionStage(true);", source);
        Assert.Contains("ClearPreviewStageResetNotice();", source);
        Assert.Contains("SelectedWizardStepIndex == ConversionWorkflowState.ConversionPlanStepIndex", footerVisibility);
        Assert.Contains("!HasEnteredPreviewConversionStage", footerVisibility);
        Assert.Contains("HasEnteredPreviewConversionStage &&", canGeneratePreview);
        Assert.Contains("HasEnteredPreviewConversionStage &&", canStartConversion);
        Assert.Contains("ResetPreviewConversionStageForPreviewAffectingChange();", planOptionChanged);
        Assert.Contains("bool affectsPreview = true", planOptionChanged);
        Assert.Contains("ResetPreviewConversionStageForPreviewAffectingChange();", previewRangeChanged);
        Assert.Contains("ResetPreviewConversionStageForPreviewAffectingChange(showNoticeWhenNoPreview: false);", setSelectedVideo);
        Assert.Contains("ResetPreviewConversionStageForPreviewAffectingChange();", selectedModelChange);
    }

    [Fact]
    public void PreviewStageResetNotice_IsNonModalAndOnlyForNoCurrentPreview()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var resetMethod = ExtractSourceRange(
            source,
            "private void ResetPreviewConversionStageForPreviewAffectingChange(",
            "private void MarkPreviewOutdatedIfNeeded");
        var showNoticeMethod = ExtractSourceRange(
            source,
            "private void ShowPreviewStageResetNotice()",
            "private void ClearPreviewStageResetNotice()");
        var clearNoticeMethod = ExtractSourceRange(
            source,
            "private void ClearPreviewStageResetNotice()",
            "private void MarkPreviewOutdatedIfNeeded");
        var topToastBlock = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"LogCopyNotification\"",
            "<Grid x:Name=\"ModalOverlay\"");
        var modalMethod = ExtractSourceRange(
            source,
            "private void ShowPreviewInvalidationConfirmationModal",
            "private void ResetPreviewInvalidationConfirmationModal");

        Assert.Contains("HasEnteredPreviewConversionStage", resetMethod);
        Assert.Contains("!ShouldConfirmPreviewInvalidatingChange()", resetMethod);
        Assert.Contains("ShowPreviewStageResetNotice();", resetMethod);
        Assert.Contains("ClearPreviewStageResetNotice();", resetMethod);
        Assert.Contains("public string PreviewStageResetNoticeText => T(LocalizationKeys.VideoConversionStageResetNotice);", source);
        Assert.Contains("TimeSpan.FromSeconds(4)", source);
        Assert.Contains("LocalizationKeys.VideoConversionStageResetNotice", source);
        Assert.Contains("ShowLogCopyNotification(", showNoticeMethod);
        Assert.Contains("PreviewStageResetNoticeDuration", showNoticeMethod);
        Assert.Contains("TopToastKind.PreviewStageReset", showNoticeMethod);
        Assert.Contains("HideLogCopyNotification(TopToastKind.PreviewStageReset);", clearNoticeMethod);
        Assert.Contains("Visibility=\"{Binding LogCopyNotificationVisibility}\"", topToastBlock);
        Assert.Contains("Text=\"{Binding LogCopyNotificationText}\"", topToastBlock);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"PreviewStageResetNotice\"", xaml);
        Assert.DoesNotContain("PreviewStageResetNoticeVisibility", source);
        Assert.DoesNotContain("ShowPreviewInvalidationConfirmationModal", resetMethod);
        Assert.Contains("IsPreviewInvalidationConfirmationModalOpen = true;", modalMethod);
    }

    [Fact]
    public void SettingsModal_UsesVisualSettingsWithoutResettingWorkflowState()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var resetMethod = ExtractSourceRange(
            source,
            "private void ResetWorkflowAfterSuccessfulConversion",
            "private static ConversionExecutionState CreateFinishedConversionState");

        Assert.Contains("public IReadOnlyList<LocalizedOptionViewModel<SettingsSection>> SettingsSectionOptions", source);
        Assert.Contains("SettingsSection.VisualSettings", source);
        Assert.DoesNotContain("SettingsSection.PreviewConversion", source);
        Assert.DoesNotContain("PreviewConversionSettingsSection", xaml);
        Assert.Contains("SelectedSettingsSection", source);
        Assert.Contains("SettingsModalContentVisibility", source);
        Assert.Contains("AutomationProperties.AutomationId=\"VisualSettingsSection\"", xaml);
        Assert.Contains("SelectedValue=\"{Binding SelectedLanguage}\"", xaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedTheme}\"", xaml);
        Assert.Contains("IsChecked=\"{Binding IsSidebarCompact,", xaml);
        Assert.DoesNotContain("SelectedLanguage", resetMethod);
        Assert.DoesNotContain("SelectedTheme", resetMethod);
        Assert.DoesNotContain("IsSidebarCompact", resetMethod);
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
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0, $"Could not find start marker '{startMarker}'.");
        Assert.True(end > start, $"Could not find end marker '{endMarker}'.");
        return source[start..end];
    }
}
