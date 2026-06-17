namespace V3dfy.Tests.App;

public sealed class MainWindowModelSelectionUiSourceTests
{
    [Fact]
    public void ConversionTab_ModelSelectorUsesInstalledSelectableCandidatesAndHelpButton()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var selectorBlock = ExtractSourceRange(
            xaml,
            "Text=\"{Binding LocalModelSelectionLabel}\"",
            "Text=\"{Binding LocalModelSelectionStatusText}\"");
        var selectorRow = selectorBlock;

        Assert.True(selectorBlock.IndexOf("Text=\"{Binding LocalModelSelectionLabel}\"", StringComparison.Ordinal) <
            selectorBlock.IndexOf("AutomationProperties.AutomationId=\"VideoModelSelector\"", StringComparison.Ordinal));
        Assert.Contains("AutomationProperties.AutomationId=\"VideoModelSelector\"", selectorBlock);
        Assert.Contains("ItemsSource=\"{Binding LocalModelCandidates}\"", selectorBlock);
        Assert.Contains("SelectedItem=\"{Binding SelectedLocalModelCandidate", selectorBlock);
        Assert.Contains("AutomationProperties.AutomationId=\"ModelHelpButton\"", selectorBlock);
        Assert.Contains("Command=\"{Binding ShowModelHelpCommand}\"", selectorBlock);
        Assert.Contains("IsEnabled=\"{Binding CanShowModelHelp}\"", selectorBlock);
        Assert.Contains("ToolTip=\"{Binding ModelHelpButtonToolTipText}\"", selectorBlock);
        Assert.Contains("Content=\"?\"", selectorBlock);
        Assert.Contains("MinWidth=\"30\"", selectorBlock);
        Assert.Contains("Padding=\"8,5\"", selectorBlock);
        Assert.Contains("Style=\"{StaticResource SecondaryButtonStyle}\"", selectorBlock);
        Assert.Contains("<ColumnDefinition Width=\"*\" />", selectorRow);
        Assert.Contains("<ColumnDefinition Width=\"Auto\" />", selectorRow);
        Assert.Contains("Grid.Column=\"1\"", selectorRow);
        Assert.Contains("VerticalAlignment=\"Center\"", selectorRow);
        Assert.True(selectorRow.IndexOf("AutomationProperties.AutomationId=\"VideoModelSelector\"", StringComparison.Ordinal) <
            selectorRow.IndexOf("AutomationProperties.AutomationId=\"ModelHelpButton\"", StringComparison.Ordinal));
        Assert.DoesNotContain("Content=\"{Binding ModelHelpButtonText}\"", selectorBlock);
        Assert.DoesNotContain("Style=\"{StaticResource IconButtonStyle}\"", selectorBlock);
        Assert.DoesNotContain("RegistryEntries", selectorBlock);
    }

    [Fact]
    public void ModelHelp_UsesStyledModalTableAndDoesNotUseMessageBox()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var modal = ExtractSourceRange(
            xaml,
            "Visibility=\"{Binding ModelHelpModalContentVisibility}\"",
            "Visibility=\"{Binding ModelInventoryModalContentVisibility}\"");
        var footer = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"CloseModelHelpButton\"",
            "AutomationProperties.AutomationId=\"CloseModelInventoryButton\"");

        Assert.Contains("Style=\"{StaticResource V3dfyModalOverlayStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource V3dfyModalCardStyle}\"", xaml);
        Assert.Contains("Text=\"{Binding ModelHelpIntroText}\"", modal);
        Assert.Contains("AutomationProperties.AutomationId=\"ModelHelpModalResponsiveBody\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"ModelHelpContent\"", modal);
        Assert.Contains("ItemsSource=\"{Binding ModelHelpRows}\"", modal);
        Assert.Contains("AlternationCount=\"2\"", modal);
        Assert.Contains("Style=\"{StaticResource TableHeaderBorderStyle}\"", modal);
        Assert.Contains("Style=\"{StaticResource StripedTableRowBorderStyle}\"", modal);
        Assert.Contains("Text=\"{Binding ModelHelpModelHeaderText}\"", modal);
        Assert.Contains("Text=\"{Binding ModelHelpPurposeHeaderText}\"", modal);
        Assert.Contains("Text=\"{Binding ModelHelpUseHeaderText}\"", modal);
        Assert.Contains("Text=\"{Binding ModelHelpSceneHeaderText}\"", modal);
        Assert.Contains("Text=\"{Binding ModelHelpDepthHeaderText}\"", modal);
        Assert.Contains("Text=\"{Binding ModelHelpSizePerformanceHeaderText}\"", modal);
        Assert.Contains("Text=\"{Binding Model}\"", modal);
        Assert.Contains("Text=\"{Binding Purpose}\"", modal);
        Assert.Contains("Text=\"{Binding Use}\"", modal);
        Assert.Contains("Text=\"{Binding Scene}\"", modal);
        Assert.Contains("Text=\"{Binding Depth}\"", modal);
        Assert.Contains("Text=\"{Binding SizePerformance}\"", modal);
        Assert.Contains("x:Name=\"ModelHelpTableViewport\"", modal);
        Assert.Contains("Style=\"{StaticResource ResponsiveTableHorizontalScrollViewerStyle}\"", modal);
        Assert.Contains("MinWidth=\"760\"", modal);
        Assert.Contains("Style=\"{StaticResource ResponsiveModalBodyScrollViewerStyle}\"", modal);
        Assert.Contains("TextWrapping=\"Wrap\"", modal);
        Assert.DoesNotContain("ModelHelpBodyText", modal);
        Assert.DoesNotContain("<TextBox", modal);
        Assert.DoesNotContain("Status", modal);
        Assert.DoesNotContain("Estado", modal);
        Assert.Contains("Command=\"{Binding CloseModelHelpCommand}\"", footer);
        Assert.DoesNotContain("MessageBox", source);
    }

    [Fact]
    public void ModelInventory_SelectableModelsUsesCompactTable()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var modal = ExtractSourceRange(
            xaml,
            "Visibility=\"{Binding ModelInventoryModalContentVisibility}\"",
            "Visibility=\"{Binding ModelPackImportConfirmationModalContentVisibility}\"");
        var selectableSection = ExtractSourceRange(
            modal,
            "AutomationProperties.AutomationId=\"SelectableModelsInventory\"",
            "Text=\"{Binding DiagnosticModelsSectionTitleText}\"");

        Assert.Contains("ItemsSource=\"{Binding SelectableModelInventoryRows}\"", selectableSection);
        Assert.Contains("Text=\"{Binding SelectableModelNameHeaderText}\"", selectableSection);
        Assert.Contains("Text=\"{Binding SelectableModelIw3HeaderText}\"", selectableSection);
        Assert.Contains("Text=\"{Binding SelectableModelCheckpointHeaderText}\"", selectableSection);
        Assert.Contains("Text=\"{Binding SelectableModelTypeHeaderText}\"", selectableSection);
        Assert.Contains("Text=\"{Binding SelectableModelSourceHeaderText}\"", selectableSection);
        Assert.Contains("Text=\"{Binding Iw3DepthModel}\"", selectableSection);
        Assert.Contains("Text=\"{Binding Checkpoint}\"", selectableSection);
        Assert.Contains("Text=\"{Binding Source}\"", selectableSection);
        Assert.Contains("AlternationCount=\"2\"", selectableSection);
        Assert.DoesNotContain("<TextBox", selectableSection);
        Assert.DoesNotContain("SelectableModelsInventoryText", selectableSection);

        Assert.Contains("public sealed record SelectableModelInventoryRow(", source);
        Assert.Contains("public IReadOnlyList<SelectableModelInventoryRow> SelectableModelInventoryRows", source);
        Assert.Contains("public string SelectableModelNameHeaderText => T(LocalizationKeys.ModelInventoryHeaderModel);", source);
        Assert.Contains("public string SelectableModelIw3HeaderText => T(LocalizationKeys.ModelInventoryHeaderIw3DepthModel);", source);
        Assert.Contains("public string SelectableModelCheckpointHeaderText => T(LocalizationKeys.ModelInventoryHeaderCheckpoint);", source);
        Assert.Contains("public string SelectableModelTypeHeaderText => T(LocalizationKeys.ModelInventoryHeaderType);", source);
        Assert.Contains("public string SelectableModelSourceHeaderText => T(LocalizationKeys.ModelInventoryHeaderSource);", source);
    }

    [Fact]
    public void ModelHelpHeaders_AreEnglishAndSpanishAndHaveNoStatusColumn()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var rowRecord = ExtractSourceRange(
            source,
            "public sealed record ModelHelpRow(",
            "public sealed class MainWindowViewModel");

        Assert.Contains("public string ModelHelpModelHeaderText => T(LocalizationKeys.VideoModelHelpModel);", source);
        Assert.Contains("public string ModelHelpPurposeHeaderText => T(LocalizationKeys.VideoModelHelpPurpose);", source);
        Assert.Contains("public string ModelHelpUseHeaderText => T(LocalizationKeys.VideoModelHelpUse);", source);
        Assert.Contains("public string ModelHelpSceneHeaderText => T(LocalizationKeys.VideoModelHelpScene);", source);
        Assert.Contains("public string ModelHelpDepthHeaderText => T(LocalizationKeys.VideoModelHelpDepth);", source);
        Assert.Contains("public string ModelHelpSizePerformanceHeaderText => T(LocalizationKeys.VideoModelHelpSizePerformance);", source);
        Assert.Contains("public IReadOnlyList<ModelHelpRow> ModelHelpRows => _isImageParallaxModelHelpContext", source);
        Assert.Contains("? CreateImageParallaxModelHelpRows()", source);
        Assert.Contains(": CreateModelHelpRows();", source);
        Assert.Contains("public sealed record ModelHelpRow(", source);
        Assert.DoesNotContain("Status", rowRecord);
        Assert.DoesNotContain("ModelHelpStatusHeaderText", source);
    }

    [Fact]
    public void ModelHelpRows_EnumerateOnlyCurrentlySelectableLocalModels()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var method = ExtractSourceRange(
            source,
            "private IReadOnlyList<ModelHelpRow> CreateModelHelpRows()",
            "private IReadOnlyList<ModelHelpRow> CreateImageParallaxModelHelpRows()");

        Assert.Contains("foreach (var candidate in LocalModelCandidates)", method);
        Assert.Contains("rows.Add(new ModelHelpRow(", method);
        Assert.DoesNotContain("CreateSelectableCandidates(", method);
        Assert.DoesNotContain("RegistryEntries", method);
        Assert.DoesNotContain("DepthPro", method);
        Assert.DoesNotContain("VDA_", method);
        Assert.DoesNotContain("download", method, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Status", method);
        Assert.DoesNotContain("Estado", method);
    }

    [Fact]
    public void ImageParallaxModelHelp_UsesSameHelpButtonPatternAndOnlyImageCompatibleCandidates()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var parallaxSetup = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageParallaxScaffold\"",
            "AutomationProperties.AutomationId=\"ImageStereoScaffold\"");
        var stereoSetup = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ImageStereoScaffold\"",
            "AutomationProperties.AutomationId=\"ImageParallaxResultScaffold\"");
        var parallaxSelectorRow = ExtractSourceRange(
            parallaxSetup,
            "Text=\"{Binding ImageModelSelectionLabelText}\"",
            "Text=\"{Binding ImageSelectedModelSummaryText}\"");
        var parallaxNarrowSelectorRow = ExtractSourceRange(
            parallaxSetup,
            "AutomationProperties.AutomationId=\"ImageParallaxModelSelectorNarrow\"",
            "Text=\"{Binding ImageSelectedModelSummaryText}\"");
        var stereoSelectorRow = ExtractSourceRange(
            stereoSetup,
            "AutomationProperties.AutomationId=\"ImageStereoReadinessSummaryCard\"",
            "Text=\"{Binding ImageModelSelectionSharedNoteText}\"");
        var stereoNarrowSelectorRow = ExtractSourceRange(
            stereoSetup,
            "AutomationProperties.AutomationId=\"ImageStereoReadinessSummaryCardNarrow\"",
            "Text=\"{Binding ImageModelSelectionSharedNoteText}\"");
        var parallaxHelpRows = ExtractSourceRange(
            source,
            "private IReadOnlyList<ModelHelpRow> CreateImageParallaxModelHelpRows()",
            "private string GetModelSizePerformanceText");

        Assert.True(parallaxSetup.IndexOf("Text=\"{Binding ImageModelSelectionLabelText}\"", StringComparison.Ordinal) <
            parallaxSetup.IndexOf("AutomationProperties.AutomationId=\"ImageParallaxModelSelector\"", StringComparison.Ordinal));
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxModelHelpButton\"", parallaxSetup);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageParallaxModelHelpButtonNarrow\"", parallaxSetup);
        Assert.Contains("Command=\"{Binding ShowImageParallaxModelHelpCommand}\"", parallaxSetup);
        Assert.Contains("IsEnabled=\"{Binding CanShowImageParallaxModelHelp}\"", parallaxSetup);
        Assert.Contains("ToolTip=\"{Binding ImageParallaxModelHelpButtonToolTipText}\"", parallaxSetup);
        Assert.Contains("Content=\"?\"", parallaxSetup);
        Assert.Contains("MinWidth=\"30\"", parallaxSetup);
        Assert.Contains("Padding=\"8,5\"", parallaxSetup);
        Assert.Contains("Style=\"{StaticResource SecondaryButtonStyle}\"", parallaxSetup);
        Assert.Contains("<ColumnDefinition Width=\"*\" />", parallaxSelectorRow);
        Assert.Contains("<ColumnDefinition Width=\"Auto\" />", parallaxSelectorRow);
        Assert.Contains("Grid.Column=\"1\"", parallaxSelectorRow);
        Assert.Contains("VerticalAlignment=\"Center\"", parallaxSelectorRow);
        Assert.True(parallaxSelectorRow.IndexOf("AutomationProperties.AutomationId=\"ImageParallaxModelSelector\"", StringComparison.Ordinal) <
            parallaxSelectorRow.IndexOf("AutomationProperties.AutomationId=\"ImageParallaxModelHelpButton\"", StringComparison.Ordinal));
        Assert.True(parallaxNarrowSelectorRow.IndexOf("AutomationProperties.AutomationId=\"ImageParallaxModelSelectorNarrow\"", StringComparison.Ordinal) <
            parallaxNarrowSelectorRow.IndexOf("AutomationProperties.AutomationId=\"ImageParallaxModelHelpButtonNarrow\"", StringComparison.Ordinal));
        Assert.Contains("AutomationProperties.AutomationId=\"ImageModelHelpButton\"", stereoSetup);
        Assert.Contains("AutomationProperties.AutomationId=\"ImageModelHelpButtonNarrow\"", stereoSetup);
        Assert.Contains("Command=\"{Binding ShowImageParallaxModelHelpCommand}\"", stereoSetup);
        Assert.Contains("ToolTip=\"{Binding ImageParallaxModelHelpButtonToolTipText}\"", stereoSetup);
        Assert.True(stereoSetup.IndexOf("Text=\"{Binding ImageModelSelectionLabelText}\"", StringComparison.Ordinal) <
            stereoSetup.IndexOf("AutomationProperties.AutomationId=\"ImageModelSelector\"", StringComparison.Ordinal));
        Assert.True(stereoSelectorRow.IndexOf("AutomationProperties.AutomationId=\"ImageModelSelector\"", StringComparison.Ordinal) <
            stereoSelectorRow.IndexOf("AutomationProperties.AutomationId=\"ImageModelHelpButton\"", StringComparison.Ordinal));
        Assert.True(stereoNarrowSelectorRow.IndexOf("AutomationProperties.AutomationId=\"ImageModelSelectorNarrow\"", StringComparison.Ordinal) <
            stereoNarrowSelectorRow.IndexOf("AutomationProperties.AutomationId=\"ImageModelHelpButtonNarrow\"", StringComparison.Ordinal));
        Assert.Contains("<ColumnDefinition Width=\"*\" />", stereoSelectorRow);
        Assert.Contains("<ColumnDefinition Width=\"Auto\" />", stereoSelectorRow);
        Assert.Contains("ItemsSource=\"{Binding ImageParallaxLocalModelCandidates}\"", parallaxSetup);
        Assert.Contains("public IReadOnlyList<LocalModelSelectionCandidate> ImageParallaxLocalModelCandidates", source);
        Assert.Contains(".Where(IsImageParallaxCompatibleCandidate)", source);
        Assert.Contains("Iw3DepthModelMediaCapability.ImageAndVideo or", source);
        Assert.Contains("Iw3DepthModelMediaCapability.ImageOnly", source);
        Assert.Contains("var candidates = ImageParallaxLocalModelCandidates;", parallaxHelpRows);
        Assert.DoesNotContain("foreach (var candidate in LocalModelCandidates)", parallaxHelpRows);
        Assert.DoesNotContain("download", parallaxHelpRows, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModelHelpUseColumn_IncludesPracticalExamples()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var englishLocalization = ReadRepoFile("src", "V3dfy.App", "Localization", "en.json");
        var spanishLocalization = ReadRepoFile("src", "V3dfy.App", "Localization", "es.json");
        var method = ExtractSourceRange(
            source,
            "private string GetModelUseExample(",
            "private string GetSceneScopeText");

        Assert.Contains("Use: GetModelUseExample(candidate, entry),", source);
        Assert.Contains("LocalizationKeys.ModelInventoryHelpUseMetricIndoor", method);
        Assert.Contains("LocalizationKeys.ModelInventoryHelpUseMetricOutdoor", method);
        Assert.Contains("LocalizationKeys.ModelInventoryHelpUseV2Small", method);
        Assert.Contains("LocalizationKeys.ModelInventoryHelpUseDepthAnything3MonoLarge3dTv", method);
        Assert.Contains("LocalizationKeys.ModelInventoryHelpUseDefault", method);
        Assert.Contains("anime", englishLocalization, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sea, boats", englishLocalization);
        Assert.Contains("rooms", englishLocalization, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quick tests", englishLocalization);
        Assert.Contains("TV playback tests", englishLocalization);
        Assert.Contains("habitaciones", spanishLocalization, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mar, barcos", spanishLocalization);
        Assert.Contains("pruebas rapidas", spanishLocalization);
        Assert.DoesNotContain("Status", method);
        Assert.DoesNotContain("download", method, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModelGuidance_UsesCompactSelectedModelGuidanceAndMetadataService()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var guidanceService = ReadRepoFile("src", "V3dfy.Core", "Estimation", "ModelGuidanceService.cs");

        Assert.Contains("SelectedModelGuidanceText", xaml);
        Assert.Contains("private readonly ModelGuidanceService _modelGuidanceService", source);
        Assert.Contains("Recommended first optional model", guidanceService);
        Assert.Contains("LocalizationKeys.VideoModelGuidanceDefault", source);
        Assert.Contains("LocalizationKeys.VideoModelGuidanceFormat", source);
        Assert.Contains("LocalizationKeys.VideoEstimateCompactModelFormat", source);
    }

    [Fact]
    public void ModelInventory_DoesNotExposeFullInternalRegistryAsAvailableModels()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var method = ExtractSourceRange(
            source,
            "private IReadOnlyList<string> CreateModelInventoryTechnicalDetailsLines()",
            "private void AddModelCatalogStatusDetailLines");

        Assert.DoesNotContain("Known iw3 depth model mappings", method);
        Assert.DoesNotContain("Mapeos conocidos de modelos de profundidad iw3", method);
        Assert.DoesNotContain("foreach (var entry in Iw3DepthModelMapper.RegistryEntries)", method);
        Assert.Contains("LocalizationKeys.TechnicalDetailsMappedSelectableModels", method);
        Assert.Contains("LocalizationKeys.TechnicalDetailsUnmappedModelFilesFound", method);
        AssertLocalizationKeyPairExists("TechnicalDetails.MappedSelectableModels");
        AssertLocalizationKeyPairExists("TechnicalDetails.UnmappedModelFilesFound");
    }

    [Fact]
    public void LocalModelRefresh_PreservesSharedCheckpointVariantSelection()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var method = ExtractSourceRange(
            source,
            "private void UpdateLocalModelSelectionCandidates",
            "private void SetSelectedLocalModelCandidate");

        Assert.Contains("previouslySelectedMappingKey", method);
        Assert.Contains("previouslySelectedDepthModelName", method);
        Assert.Contains("candidate.MappingKey", method);
        Assert.Contains("candidate.Iw3DepthModelName", method);
        Assert.Contains("Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey", method);
        Assert.Contains("Iw3DepthModelMapper.ZoeDAnyNDepthModelName", method);
        Assert.DoesNotContain("??\r\n                LocalModelCandidates.FirstOrDefault(candidate => string.Equals(", method);
    }

    [Fact]
    public void LocalModelSelectionSetter_UpdatesWhenRefreshCreatesEqualButDifferentCandidate()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var method = ExtractSourceRange(
            source,
            "private void SetSelectedLocalModelCandidate",
            "private ConversionExecutionStartGateResult EvaluateConversionStartGate");

        Assert.Contains("ReferenceEquals(", method);
        Assert.DoesNotContain("EqualityComparer<LocalModelSelectionCandidate?>.Default.Equals", method);
    }

    private static string ReadRepoFile(params string[] relativePath)
    {
        var candidate = FindRepoPath(relativePath);
        return File.ReadAllText(candidate);
    }

    private static void AssertLocalizationKeyPairExists(string key)
    {
        Assert.Contains($"\"{key}\"", ReadRepoFile("src", "V3dfy.App", "Localization", "en.json"));
        Assert.Contains($"\"{key}\"", ReadRepoFile("src", "V3dfy.App", "Localization", "es.json"));
    }

    private static string ExtractSourceRange(
        string source,
        string startMarker,
        string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find start marker '{startMarker}'.");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find end marker '{endMarker}'.");
        return source[start..end];
    }

    private static string FindRepoPath(params string[] relativePath)
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativePath]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate {Path.Combine(relativePath)}.");
    }
}
