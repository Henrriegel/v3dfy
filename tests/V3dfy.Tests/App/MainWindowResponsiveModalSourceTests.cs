namespace V3dfy.Tests.App;

public sealed class MainWindowResponsiveModalSourceTests
{
    [Fact]
    public void SharedModalShell_ClampsPreferredSizeToViewportAndKeepsFooterFixed()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var appXaml = ReadRepoFile("src", "V3dfy.App", "App.xaml");
        var converter = ReadRepoFile("src", "V3dfy.App", "Converters", "ViewportMaxSizeConverter.cs");
        var overlay = ExtractSourceRange(
            xaml,
            "<Grid x:Name=\"ModalOverlay\"",
            "AutomationProperties.AutomationId=\"GlobalBusyOverlay\"");
        var footer = ExtractSourceRange(
            overlay,
            "AutomationProperties.AutomationId=\"ResponsiveModalFooter\"",
            "</WrapPanel>");
        var bodyBeforeFooter = overlay[..overlay.IndexOf(
            "AutomationProperties.AutomationId=\"ResponsiveModalFooter\"",
            StringComparison.Ordinal)];

        Assert.Contains("ClipToBounds=\"True\"", overlay);
        Assert.Contains("Grid.Row=\"1\"", overlay);
        Assert.Contains("Grid.ColumnSpan=\"2\"", overlay);
        Assert.Contains("Width=\"{Binding ActiveModalWidth}\"", overlay);
        Assert.Contains("Height=\"{Binding ActiveModalHeight}\"", overlay);
        Assert.Contains("MaxWidth=\"{Binding ActualWidth", overlay);
        Assert.Contains("MaxHeight=\"{Binding ActualHeight", overlay);
        Assert.Contains("ElementName=ModalOverlay", overlay);
        Assert.Contains("Converter={StaticResource ViewportMaxSizeConverter}", overlay);
        Assert.Contains("ConverterParameter=48", overlay);
        Assert.Contains("Style=\"{StaticResource ResponsiveModalCardStyle}\"", overlay);
        Assert.Contains("<RowDefinition Height=\"*\" />", overlay);
        Assert.Contains("<RowDefinition Height=\"Auto\" />", overlay);
        Assert.Contains("Style=\"{StaticResource ResponsiveModalFooterWrapPanelStyle}\"", footer);
        Assert.DoesNotContain("CloseSettingsButton", bodyBeforeFooter);
        Assert.DoesNotContain("CancelPreviewButton", bodyBeforeFooter);
        Assert.DoesNotContain("ConfirmModelPackImportButton", bodyBeforeFooter);
        Assert.Contains("AutomationProperties.AutomationId=\"CloseSettingsButton\"", footer);
        Assert.Contains("AutomationProperties.AutomationId=\"CloseModelHelpButton\"", footer);
        Assert.Contains("AutomationProperties.AutomationId=\"CloseModelInventoryButton\"", footer);
        Assert.Contains("AutomationProperties.AutomationId=\"CloseActivityLogButton\"", footer);
        Assert.Contains("AutomationProperties.AutomationId=\"CloseTechnicalDetailsButton\"", footer);
        Assert.Contains("AutomationProperties.AutomationId=\"CloseProfileDetailsButton\"", footer);
        Assert.Contains("AutomationProperties.AutomationId=\"CancelPreviewButton\"", footer);
        Assert.Contains("AutomationProperties.AutomationId=\"ConfirmPreviewInvalidationButton\"", footer);
        Assert.Contains("AutomationProperties.AutomationId=\"ConfirmModelPackImportButton\"", footer);
        Assert.Contains("AutomationProperties.AutomationId=\"AcceptConversionCompletedButton\"", footer);
        Assert.Contains("ResponsiveModalCardStyle", appXaml);
        Assert.Contains("ResponsiveModalBodyScrollViewerStyle", appXaml);
        Assert.Contains("ResponsiveTableContainerStyle", appXaml);
        Assert.Contains("Math.Max(0d, viewportSize - reservedSize)", converter);
    }

    [Fact]
    public void SettingsModal_UsesSideMenuAndContentMinimumsWithInternalScrollRegions()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var settings = ExtractSourceRange(
            xaml,
            "x:Name=\"SettingsModalHorizontalViewport\"",
            "Visibility=\"{Binding TechnicalDetailsModalContentVisibility}\"");
        var settingsTable = ExtractSourceRange(
            settings,
            "AutomationProperties.AutomationId=\"SettingsSelectableModelsTable\"",
            "AutomationProperties.AutomationId=\"SettingsSelectableModelsEmptyState\"");
        var footer = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ResponsiveModalFooter\"",
            "</WrapPanel>");

        Assert.Contains("x:Name=\"SettingsModalHorizontalViewport\"", settings);
        Assert.Contains("Style=\"{StaticResource ResponsiveHorizontalViewportScrollViewerStyle}\"", settings);
        Assert.Contains("AutomationProperties.AutomationId=\"SettingsModalContentGrid\"", settings);
        Assert.Contains("MinWidth=\"620\"", settings);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=SettingsModalHorizontalViewport}\"", settings);
        Assert.Contains("<ColumnDefinition Width=\"220\"", settings);
        Assert.Contains("MinWidth=\"200\"", settings);
        Assert.Contains("<ColumnDefinition Width=\"*\"", settings);
        Assert.Contains("MinWidth=\"380\"", settings);
        Assert.Contains("AutomationProperties.AutomationId=\"SettingsSideMenu\"", settings);
        Assert.Contains("Grid.Column=\"2\"", settings);
        Assert.Contains("Style=\"{StaticResource ResponsiveModalBodyScrollViewerStyle}\"", settings);
        Assert.Contains("AutomationProperties.AutomationId=\"LogsDiagnosticsSettingsSection\"", settings);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", settings);
        Assert.Contains("AutomationProperties.AutomationId=\"AboutLicensesSettingsSection\"", settings);
        Assert.Contains("Style=\"{StaticResource ResponsiveTableContainerStyle}\"", settingsTable);
        Assert.Contains("x:Name=\"SettingsSelectableModelsTableViewport\"", settingsTable);
        Assert.Contains("Style=\"{StaticResource ResponsiveTableHorizontalScrollViewerStyle}\"", settingsTable);
        Assert.Contains("MinWidth=\"660\"", settingsTable);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=SettingsSelectableModelsTableViewport}\"", settingsTable);
        Assert.Contains("Width=\"{Binding ActualWidth", settingsTable);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"CloseSettingsButton\"", settings);
        Assert.Contains("AutomationProperties.AutomationId=\"CloseSettingsButton\"", footer);
    }

    [Fact]
    public void ModelHelpModal_ContainsTableHorizontalScrollAndFixedFooter()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var modal = ExtractSourceRange(
            xaml,
            "Visibility=\"{Binding ModelHelpModalContentVisibility}\"",
            "Visibility=\"{Binding ModelInventoryModalContentVisibility}\"");
        var footer = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ResponsiveModalFooter\"",
            "</WrapPanel>");

        Assert.Contains("AutomationProperties.AutomationId=\"ModelHelpContent\"", modal);
        Assert.Contains("AutomationProperties.AutomationId=\"ModelHelpModalResponsiveBody\"", xaml);
        Assert.Contains("Style=\"{StaticResource ResponsiveTableContainerStyle}\"", modal);
        Assert.Contains("x:Name=\"ModelHelpTableViewport\"", modal);
        Assert.Contains("Style=\"{StaticResource ResponsiveTableHorizontalScrollViewerStyle}\"", modal);
        Assert.Contains("MinWidth=\"760\"", modal);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=ModelHelpTableViewport}\"", modal);
        Assert.Contains("Style=\"{StaticResource ResponsiveModalBodyScrollViewerStyle}\"", modal);
        Assert.Contains("TextWrapping=\"Wrap\"", modal);
        Assert.Contains("Width=\"{Binding ActualWidth", modal);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"CloseModelHelpButton\"", modal);
        Assert.Contains("AutomationProperties.AutomationId=\"CloseModelHelpButton\"", footer);
    }

    [Fact]
    public void ModelInventoryModal_ContainsResponsiveColumnsTableScrollAndFooterActions()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var modal = ExtractSourceRange(
            xaml,
            "x:Name=\"ModelInventoryModalHorizontalViewport\"",
            "Visibility=\"{Binding ModelPackImportConfirmationModalContentVisibility}\"");
        var footer = ExtractSourceRange(
            xaml,
            "AutomationProperties.AutomationId=\"ResponsiveModalFooter\"",
            "</WrapPanel>");

        Assert.Contains("x:Name=\"ModelInventoryModalHorizontalViewport\"", modal);
        Assert.Contains("Style=\"{StaticResource ResponsiveHorizontalViewportScrollViewerStyle}\"", modal);
        Assert.Contains("AutomationProperties.AutomationId=\"ModelInventoryResponsiveContent\"", modal);
        Assert.Contains("MinWidth=\"900\"", modal);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=ModelInventoryModalHorizontalViewport}\"", modal);
        Assert.Contains("<ColumnDefinition Width=\"7*\"", modal);
        Assert.Contains("MinWidth=\"620\"", modal);
        Assert.Contains("<ColumnDefinition Width=\"3*\"", modal);
        Assert.Contains("MinWidth=\"260\"", modal);
        Assert.Contains("<ScrollViewer Grid.Column=\"0\"", modal);
        Assert.Contains("<ScrollViewer Grid.Column=\"2\"", modal);
        Assert.Contains("x:Name=\"SelectableModelsInventoryTableViewport\"", modal);
        Assert.Contains("Style=\"{StaticResource ResponsiveTableHorizontalScrollViewerStyle}\"", modal);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=SelectableModelsInventoryTableViewport}\"", modal);
        Assert.Contains("Width=\"{Binding ActualWidth", modal);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ImportModelPackButton\"", modal);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"OpenModelsFolderButton\"", modal);
        Assert.Contains("AutomationProperties.AutomationId=\"ImportModelPackButton\"", footer);
        Assert.Contains("AutomationProperties.AutomationId=\"OpenModelsFolderButton\"", footer);
        Assert.Contains("AutomationProperties.AutomationId=\"CloseModelInventoryButton\"", footer);
    }

    [Fact]
    public void OtherModalBodies_KeepLongContentScrollableInsideSharedShell()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var overlay = ExtractSourceRange(
            xaml,
            "<Grid x:Name=\"ModalOverlay\"",
            "AutomationProperties.AutomationId=\"GlobalBusyOverlay\"");
        var technical = ExtractElementContaining(
            overlay,
            "Text=\"{Binding TechnicalDetailsBodyText",
            "<TextBox",
            "</TextBox>");
        var profile = ExtractElementContaining(
            overlay,
            "Text=\"{Binding ProfileDetailsBodyText",
            "<TextBox",
            "</TextBox>");
        var activity = ExtractElementContaining(
            overlay,
            "Text=\"{Binding ActivityLogModalText",
            "<TextBox",
            "</TextBox>");
        var previewGenerating = ExtractSourceRange(
            overlay,
            "Visibility=\"{Binding PreviewGeneratingModalContentVisibility}\"",
            "Visibility=\"{Binding PreviewReadyModalContentVisibility}\"");
        var modelPackConfirmation = ExtractElementContaining(
            overlay,
            "AutomationProperties.AutomationId=\"ModelPackImportConfirmationSummary\"",
            "<TextBox",
            "</TextBox>");
        var confirmations = ExtractSourceRange(
            overlay,
            "Visibility=\"{Binding ReplaceVideoConfirmationModalContentVisibility}\"",
            "Visibility=\"{Binding ConversionCompletedModalContentVisibility}\"");
        var conversionCompleted = ExtractSourceRange(
            overlay,
            "Visibility=\"{Binding ConversionCompletedModalContentVisibility}\"",
            "AutomationProperties.AutomationId=\"ResponsiveModalFooter\"");

        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", technical);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", technical);
        Assert.Contains("TextWrapping=\"Wrap\"", profile);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", profile);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", activity);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", activity);
        Assert.Contains("x:Name=\"PreviewGenerationLogList\"", previewGenerating);
        Assert.Contains("ScrollViewer.HorizontalScrollBarVisibility=\"Auto\"", previewGenerating);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility=\"Auto\"", previewGenerating);
        Assert.Contains("AutomationProperties.AutomationId=\"ModelPackImportConfirmationSummary\"", modelPackConfirmation);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", modelPackConfirmation);
        Assert.Contains("TextWrapping=\"Wrap\"", confirmations);
        Assert.Contains("AutomationProperties.AutomationId=\"ConversionCompletedOutputPath\"", conversionCompleted);
        Assert.Contains("TextWrapping=\"Wrap\"", conversionCompleted);
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

    private static string ExtractElementContaining(
        string source,
        string marker,
        string elementStartMarker,
        string elementEndMarker)
    {
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"Could not find marker '{marker}'.");

        var start = source.LastIndexOf(elementStartMarker, markerIndex, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find element start '{elementStartMarker}'.");

        var end = source.IndexOf(elementEndMarker, markerIndex, StringComparison.Ordinal);
        Assert.True(end > markerIndex, $"Could not find element end '{elementEndMarker}'.");

        return source[start..(end + elementEndMarker.Length)];
    }
}
