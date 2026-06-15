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
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", modal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", modal);
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
        Assert.Contains("public string SelectableModelNameHeaderText => Text(\"Model\", \"Modelo\")", source);
        Assert.Contains("public string SelectableModelIw3HeaderText => Text(\"iw3 depth model\", \"Modelo iw3\")", source);
        Assert.Contains("public string SelectableModelCheckpointHeaderText => Text(\"Checkpoint\", \"Checkpoint\")", source);
        Assert.Contains("public string SelectableModelTypeHeaderText => Text(\"Type\", \"Tipo\")", source);
        Assert.Contains("public string SelectableModelSourceHeaderText => Text(\"Source\", \"Origen\")", source);
    }

    [Fact]
    public void ModelHelpHeaders_AreEnglishAndSpanishAndHaveNoStatusColumn()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var rowRecord = ExtractSourceRange(
            source,
            "public sealed record ModelHelpRow(",
            "public sealed class MainWindowViewModel");

        Assert.Contains("public string ModelHelpModelHeaderText => Text(\"Model\", \"Modelo\")", source);
        Assert.Contains("public string ModelHelpPurposeHeaderText => Text(\"Purpose\", \"Propósito\")", source);
        Assert.Contains("public string ModelHelpUseHeaderText => Text(\"Use\", \"Uso\")", source);
        Assert.Contains("public string ModelHelpSceneHeaderText => Text(\"Scene\", \"Escena\")", source);
        Assert.Contains("public string ModelHelpDepthHeaderText => Text(\"Depth\", \"Profundidad\")", source);
        Assert.Contains("\"Size/performance\"", source);
        Assert.Contains("\"Tamaño/rendimiento\"", source);
        Assert.Contains("public IReadOnlyList<ModelHelpRow> ModelHelpRows => CreateModelHelpRows();", source);
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
            "private static Iw3DepthModelRegistryEntry? FindRegistryEntry");

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
    public void ModelHelpUseColumn_IncludesPracticalExamples()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var method = ExtractSourceRange(
            source,
            "private static string GetModelUseExample",
            "private static string GetModelBestUse");

        Assert.Contains("Use: GetModelUseExample(candidate, entry, IsSpanish)", source);
        Assert.Contains("anime", method, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sea, boats", method);
        Assert.Contains("rooms", method, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quick tests", method);
        Assert.Contains("TV playback tests", method);
        Assert.Contains("habitaciones", method, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mar, barcos", method);
        Assert.Contains("pruebas rapidas", method);
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
        Assert.Contains("v3dfy includes a usable base model", source);
        Assert.Contains("Good for:", source);
        Assert.Contains("Speed:", source);
        Assert.Contains("Quality:", source);
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
        Assert.Contains("Mapped selectable local models", method);
        Assert.Contains("Unmapped model files were found", method);
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
