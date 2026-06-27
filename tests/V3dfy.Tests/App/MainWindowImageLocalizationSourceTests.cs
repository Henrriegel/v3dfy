using System.Text.Json;
using V3dfy.Core.Localization;

namespace V3dfy.Tests.App;

public sealed class MainWindowImageLocalizationSourceTests
{
    [Fact]
    public void ImageLocalizationJson_ContainsMigratedImageKeySet()
    {
        var englishKeys = ReadLocalizationKeys("en.json");
        var spanishKeys = ReadLocalizationKeys("es.json");

        foreach (var key in RequiredImageKeys)
        {
            Assert.Contains(key, englishKeys);
            Assert.Contains(key, spanishKeys);
        }
    }

    [Fact]
    public void ImageParallaxUserFacingText_MarksParallaxExperimentalOnly()
    {
        var english = ReadLocalizationStrings("en.json");
        var spanish = ReadLocalizationStrings("es.json");
        var readme = ReadRepoFile("README.md");

        Assert.Equal("2.5D Photo / Parallax (Experimental)", english[LocalizationKeys.ImageWorkflowParallaxTitle]);
        Assert.Equal("Foto 2.5D / Parallax (Experimental)", spanish[LocalizationKeys.ImageWorkflowParallaxTitle]);
        Assert.Contains("Experimental 2.5D / Parallax", english[LocalizationKeys.ImageWorkflowParallaxDescription]);
        Assert.Contains("2.5D / Parallax experimental", spanish[LocalizationKeys.ImageWorkflowParallaxDescription]);
        Assert.Contains("source image, depth map quality, and scene structure", english[LocalizationKeys.ImageParallaxQualityGuidance]);
        Assert.Contains("imagen origen, la calidad del mapa de profundidad y la estructura de la escena", spanish[LocalizationKeys.ImageParallaxQualityGuidance]);
        Assert.Contains("Experimental 2.5D / parallax image-to-video export", readme);
        Assert.DoesNotContain("Experimental", english[LocalizationKeys.ImageWorkflowStereoTitle], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Experimental", spanish[LocalizationKeys.ImageWorkflowStereoTitle], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Experimental", english[LocalizationKeys.VideoTitle], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Experimental", spanish[LocalizationKeys.VideoTitle], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImageViewModelUserFacingProperties_UseLocalizationKeys()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        foreach (var snippet in new[]
        {
            "public string ImageConversionTitleText => T(LocalizationKeys.ImageTitle);",
            "public string ImageParallaxModeTitleText => T(LocalizationKeys.ImageWorkflowParallaxTitle);",
            "public string Image3DOutputModeTitleText => T(LocalizationKeys.ImageWorkflowStereoTitle);",
            "public string ImageModeSourceStepTitleText => T(LocalizationKeys.ImageStepSourceTitle);",
            "public string ImageSetupStepTitleText => T(LocalizationKeys.ImageStepSetupTitle);",
            "public string ImagePreviewExportStepTitleText => T(LocalizationKeys.ImageStepExportTitle);",
            "public string ImageModelSelectionLabelText => T(LocalizationKeys.ImageModelSelectorLabel);",
            "public string ImageDepthIntensityLabelText => T(LocalizationKeys.ImageParallaxDepthIntensityLabel);",
            "public string ImageStereoOutputFormatLabelText => T(LocalizationKeys.ImageStereoOutputFormatLabel);",
            "public string ImageConvertActionText => IsImageExportRunning",
            "public string ContinueWithImageConversionText => T(LocalizationKeys.ImageActionPrepareConversion);",
            "public string ImageExportOutdatedText => T(LocalizationKeys.ImageOutputOutdated);",
            "public string ImageNewConversionActionText => T(LocalizationKeys.ImageResultNewConversion);",
            "public string ImageParallaxModelHelpButtonToolTipText => T(LocalizationKeys.ImageModelHelpTooltip);",
        })
        {
            Assert.Contains(snippet, source);
        }

        var imageProperties = ExtractSourceRange(
            source,
            "public string ImageConversionTitleText",
            "public string? SelectedVideoPath");

        Assert.DoesNotContain("Text(\"", imageProperties);
    }

    [Fact]
    public void ImageOptionLabels_AreKeyBasedAndBoundThroughDisplayName()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var optionModel = ReadRepoFile("src", "V3dfy.App", "ViewModels", "LocalizedOptionViewModel.cs");
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var imageSetup = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageSetupStepContent\"",
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"");

        Assert.Contains("public string? LocalizationKey { get; }", optionModel);
        Assert.Contains("_localizationService.GetString(LocalizationKey)", optionModel);
        Assert.Contains("CreateLocalizedStringOptions(new (string Value, string Key)[]", source);
        Assert.Contains("(\"Low\", LocalizationKeys.ImageParallaxDepthIntensityLow)", source);
        Assert.Contains("(\"Left to right\", LocalizationKeys.ImageParallaxMotionDirectionLeftToRight)", source);
        Assert.Contains("(\"Subtle\", LocalizationKeys.ImageParallaxZoomAmplitudeSubtle)", source);
        Assert.Contains("(\"6 seconds\", LocalizationKeys.ImageParallaxDuration6Seconds)", source);
        Assert.Contains("(\"Enabled\", LocalizationKeys.ImageParallaxSmoothingEnabled)", source);
        Assert.Contains("(\"Foreground / mid / background\", LocalizationKeys.ImageParallaxLayerForegroundMidBackground)", source);
        Assert.Contains("(\"4.0%\", LocalizationKeys.ImageStereoEyeSeparation4Percent)", source);
        Assert.Contains("(\"Neutral\", LocalizationKeys.ImageStereoConvergenceNeutral)", source);
        Assert.Contains("(SupportedStereoAnaglyphMode, LocalizationKeys.ImageStereoAnaglyphModeRedCyan)", source);
        Assert.Contains("new(ImageStereoOutputFormat.SideBySide, LocalizationKeys.ImageStereoOutputFormatSbs", source);
        Assert.Contains("new(ImageStereoOutputFormat.HalfTopBottom, LocalizationKeys.ImageStereoOutputFormatHalfTopBottom", source);
        Assert.Contains("new(ImageStereoOutputFormat.Anaglyph, LocalizationKeys.ImageStereoOutputFormatAnaglyph", source);
        Assert.DoesNotContain("public IReadOnlyList<string> ParallaxDepthIntensityOptions", source);
        Assert.DoesNotContain("public IReadOnlyList<string> StereoAnaglyphModeOptions", source);

        Assert.True(CountOccurrences(imageSetup, "DisplayMemberPath=\"DisplayName\"") >= 10);
        Assert.True(CountOccurrences(imageSetup, "SelectedValuePath=\"Value\"") >= 9);
        Assert.Contains("SelectedValue=\"{Binding SelectedParallaxDepthIntensity}\"", imageSetup);
        Assert.Contains("SelectedValue=\"{Binding SelectedStereoEyeSeparation}\"", imageSetup);
        Assert.Contains("SelectedValue=\"{Binding SelectedStereoAnaglyphMode}\"", imageSetup);
    }

    [Fact]
    public void ImageLogsAndProgress_UseLocalizationKeysExceptEngineDtoBoundaries()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var imageMethods = ExtractSourceRange(
            source,
            "private void SelectImageConversionMode",
            "private void OpenSettings()");
        var imageExportMethods = ExtractSourceRange(
            source,
            "private async Task ExportStereoscopicImageAsync()",
            "private void OpenImageOutputFolder()");

        foreach (var keySnippet in new[]
        {
            "LocalizationKeys.ImageLogWorkflowReady",
            "LocalizationKeys.ImageLogWorkflowChangedFormat",
            "LocalizationKeys.ImageLogSetupStringChangedFormat",
            "LocalizationKeys.ImageLogPreparedStereo",
            "LocalizationKeys.ImageLogPreparedParallax",
            "LocalizationKeys.ImageLogStereoStarted",
            "LocalizationKeys.ImageLogParallaxStarted",
            "LocalizationKeys.ImageLogGeneratedImageFileFormat",
            "LocalizationKeys.ImageLogGeneratedParallaxFileFormat",
            "LocalizationKeys.ImageProgressPreparingStereo",
            "LocalizationKeys.ImageProgressPreparingParallax",
            "LocalizationKeys.ImageErrorExportFailedFormat",
            "LocalizationKeys.ImageErrorParallaxExportFailedFormat",
        })
        {
            Assert.Contains(keySnippet, source);
        }

        Assert.DoesNotContain("AddImageLog(\"", imageMethods);
        Assert.DoesNotContain("Text(\"", imageMethods);

        Assert.Contains("LocalizeImageExportReadinessIssue(firstIssue.EnglishMessage)", imageMethods);
        Assert.Contains("LocalizeImageStereoExportSummary(result)", imageExportMethods);
        Assert.Contains("LocalizeImageParallaxExportSummary(result)", imageExportMethods);
        Assert.Contains("LocalizeImageStereoExportProgress(progress)", imageExportMethods);
        Assert.Contains("LocalizeImageParallaxExportProgress(progress)", imageExportMethods);
        Assert.DoesNotContain("AddImageLog(result.EnglishSummary, result.SpanishSummary);", imageExportMethods);
        Assert.DoesNotContain("AddImageLog(progress.EnglishMessage, progress.SpanishMessage);", imageExportMethods);
    }

    [Fact]
    public void SelectedLanguage_DoesNotResetImageStateAndRefreshesImageOptionLabels()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var property = ExtractSourceRange(
            source,
            "public string SelectedLanguage",
            "public IReadOnlyList<string> ThemeOptions");
        var refresh = ExtractSourceRange(
            source,
            "private void UpdatePlanOptionLanguages()",
            "private void RaiseLocalizedPropertiesChanged()");

        Assert.Contains("UpdatePlanOptionLanguages();", property);
        Assert.Contains("UpdateLogLanguages();", property);
        Assert.Contains("SetLanguage(IsSpanish)", refresh);
        Assert.Contains("_parallaxDepthIntensityOptions", refresh);
        Assert.Contains("_stereoAnaglyphModeOptions", refresh);
        Assert.DoesNotContain("SelectedImagePath = null", property);
        Assert.DoesNotContain("ResetImageSetupState", property);
        Assert.DoesNotContain("ResetImageExportState", property);
        Assert.DoesNotContain("ImageLogs.Clear", property);
        Assert.DoesNotContain("_selectedImageConversionMode", property);
        Assert.DoesNotContain("_lastImageExportPrimaryPath", property);
        Assert.DoesNotContain("_selectedLocalModelCandidate = null", property);
    }

    private static readonly string[] RequiredImageKeys =
    [
        LocalizationKeys.ImageTitle,
        LocalizationKeys.ImageHomeCardTitle,
        LocalizationKeys.ImageHomeCardBody,
        LocalizationKeys.ImageHomeStatus,
        LocalizationKeys.ImageWorkflowTitle,
        LocalizationKeys.ImageWorkflowParallaxTitle,
        LocalizationKeys.ImageWorkflowParallaxDescription,
        LocalizationKeys.ImageWorkflowStereoTitle,
        LocalizationKeys.ImageWorkflowStereoDescription,
        LocalizationKeys.ImageStepSourceTitle,
        LocalizationKeys.ImageStepSetupTitle,
        LocalizationKeys.ImageStepExportTitle,
        LocalizationKeys.ImageSourceSelectButton,
        LocalizationKeys.ImageSourceReanalyzeButton,
        LocalizationKeys.ImageSourceSupportedExtensions,
        LocalizationKeys.ImageMetadataTitle,
        LocalizationKeys.ImageMetadataWidth,
        LocalizationKeys.ImageMetadataHeight,
        LocalizationKeys.ImageParallaxMotionParametersTitle,
        LocalizationKeys.ImageParallaxDepthIntensityLabel,
        LocalizationKeys.ImageParallaxMotionDirectionLabel,
        LocalizationKeys.ImageParallaxZoomAmplitudeLabel,
        LocalizationKeys.ImageParallaxDurationLabel,
        LocalizationKeys.ImageParallaxSmoothingLabel,
        LocalizationKeys.ImageParallaxLayerBehaviorLabel,
        LocalizationKeys.ImageParallaxQualityGuidance,
        LocalizationKeys.ImageStereoOutputFormatLabel,
        LocalizationKeys.ImageStereoEyeSeparationLabel,
        LocalizationKeys.ImageStereoConvergenceLabel,
        LocalizationKeys.ImageStereoSwapEyesLabel,
        LocalizationKeys.ImageStereoAnaglyphModeLabel,
        LocalizationKeys.ImageModelSelectorLabel,
        LocalizationKeys.ImageModelHelpTitle,
        LocalizationKeys.ImageModelHelpDescription,
        LocalizationKeys.ImageOutputExpectedFileMissing,
        LocalizationKeys.ImageOutputSaveLocationMissing,
        LocalizationKeys.ImageResultOpenOutputFolder,
        LocalizationKeys.ImageResultNewConversion,
        LocalizationKeys.ImageReadinessReady,
        LocalizationKeys.ImageReadinessMissingImage,
        LocalizationKeys.ImageReadinessNotPrepared,
        LocalizationKeys.ImageTooltipNextMissingImage,
        LocalizationKeys.ImageLogWorkflowReady,
        LocalizationKeys.ImageLogSetupStringChangedFormat,
        LocalizationKeys.ImageLogParallaxSettingsFormat,
        LocalizationKeys.ImageErrorExportFailedFormat,
        LocalizationKeys.ImageProgressPreparingParallax,
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

    private static Dictionary<string, string> ReadLocalizationStrings(string fileName)
    {
        using var document = JsonDocument.Parse(ReadRepoFile("src", "V3dfy.App", "Localization", fileName));
        return document.RootElement
            .GetProperty("strings")
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value.GetString() ?? string.Empty,
                StringComparer.Ordinal);
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
