using V3dfy.SetupHelper;

namespace V3dfy.Tests.Packaging;

public sealed class SetupHelperModelPackSelectionUiSourceTests
{
    [Fact]
    public void UiRunner_ShowsModelPackSelectionOnlyWhenManifestPathIsProvided()
    {
        var source = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");

        Assert.Contains("BeginSetupFlowAsync", source);
        Assert.Contains("PrepareModelPackSelectionPageAsync", source);
        Assert.Contains("options.ModelPacksManifestPath", source);
        Assert.Contains("string.IsNullOrWhiteSpace(options.ModelPacksManifestPath)", source);
        Assert.Contains("ShowModelPackSelectionPage(selectionPage);", source);
        Assert.Contains("StartPayloadInstall();", source);

        var beginFlow = ExtractSourceRange(
            source,
            "private async Task BeginSetupFlowAsync()",
            "private async Task<InstallerModelPackSelectionPageModel?> PrepareModelPackSelectionPageAsync()");
        Assert.True(
            beginFlow.IndexOf("PrepareModelPackSelectionPageAsync()", StringComparison.Ordinal) <
            beginFlow.LastIndexOf("StartPayloadInstall();", StringComparison.Ordinal));
    }

    [Fact]
    public void UiRunner_LoadsWebAndOfflineDiscoveryRows()
    {
        var source = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");
        var prepareMethod = ExtractSourceRange(
            source,
            "private async Task<InstallerModelPackSelectionPageModel?> PrepareModelPackSelectionPageAsync()",
            "private void ShowModelPackSelectionPage");

        Assert.Contains("InstallerModelPackManifest.LoadAsync", prepareMethod);
        Assert.Contains("PayloadInstallMode.Offline", prepareMethod);
        Assert.Contains("InstallerModelPackDiscovery.DiscoverOffline", prepareMethod);
        Assert.Contains("options.ModelPacksSourceDirectory ?? options.PartsDirectory", prepareMethod);
        Assert.Contains("InstallerModelPackDiscovery.DiscoverWeb", prepareMethod);
        Assert.Contains("new InstallerModelPackSelectionPageModel(discovery)", prepareMethod);
    }

    [Fact]
    public void UiRunner_ManifestLoadFailureWarnsAndContinuesInstall()
    {
        var source = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");
        var prepareMethod = ExtractSourceRange(
            source,
            "private async Task<InstallerModelPackSelectionPageModel?> PrepareModelPackSelectionPageAsync()",
            "private void ShowModelPackSelectionPage");
        var beginFlow = ExtractSourceRange(
            source,
            "private async Task BeginSetupFlowAsync()",
            "private async Task<InstallerModelPackSelectionPageModel?> PrepareModelPackSelectionPageAsync()");

        Assert.Contains(
            "Optional model-pack catalog could not be loaded. v3dfy will continue installing with its base model.",
            prepareMethod);
        Assert.Contains("pendingSetupWarningMessages.Add(warning);", prepareMethod);
        Assert.Contains("return null;", prepareMethod);
        Assert.Contains("StartPayloadInstall();", beginFlow);
    }

    [Fact]
    public void SelectionPage_UsesCompactTableWithRequiredColumns()
    {
        var source = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");
        var pageModelSource = ReadRepoFile("src", "V3dfy.SetupHelper", "InstallerModelPackSelectionPageModel.cs");
        var createPanel = ExtractSourceRange(
            source,
            "private TableLayoutPanel CreateModelPackSelectionPanel()",
            "protected override void OnShown");

        Assert.Contains("InstallerModelPackSelectionPageModel.TitleText", source);
        Assert.Contains("InstallerModelPackSelectionPageModel.BodyText", source);
        Assert.Contains("InstallerModelPackSelectionPageModel.OfflineNoPacksText", source);
        Assert.Contains(InstallerModelPackSelectionPageModel.TitleText, pageModelSource);
        Assert.Contains(InstallerModelPackSelectionPageModel.BodyText, pageModelSource);
        Assert.Contains(InstallerModelPackSelectionPageModel.OfflineNoPacksText, pageModelSource);
        Assert.Contains("DataGridView", createPanel);
        Assert.Contains("DataGridViewCheckBoxColumn", createPanel);
        Assert.Contains("HeaderText = \"Model\"", createPanel);
        Assert.Contains("HeaderText = \"Best use\"", createPanel);
        Assert.Contains("HeaderText = \"Size\"", createPanel);
        Assert.Contains("HeaderText = \"Source / Status\"", createPanel);
        Assert.Contains("AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells", createPanel);
        Assert.Contains("WrapMode = DataGridViewTriState.True", createPanel);
        Assert.Contains("Text = \"Continue\"", createPanel + source);
        Assert.DoesNotContain("Text = \"Select all\"", source);
        Assert.DoesNotContain("Text = \"Deselect all\"", source);
    }

    [Fact]
    public void SelectionPage_TopCheckboxAndRowsUpdateSharedSelectionState()
    {
        var source = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");

        Assert.Contains("modelPackTopCheckBox = new CheckBox", source);
        Assert.Contains("ThreeState = true", source);
        Assert.Contains("OnModelPackTopCheckBoxClick", source);
        Assert.Contains("SelectionState.ApplyTopCheckboxAction();", source);
        Assert.Contains("OnModelPackGridCellValueChanged", source);
        Assert.Contains("SelectionState.SetSelected(selectionRow.PackId, selected);", source);
        Assert.Contains("UpdateModelPackSelectionControls();", source);
        Assert.Contains("InstallerModelPackTopSelectionState.Indeterminate", source);
    }

    [Fact]
    public void SelectionPage_StoresSelectedRowsAndImportsOnlyAfterVerifiedAcquisition()
    {
        var uiSource = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");
        var payloadInstallerSource = ReadRepoFile("src", "V3dfy.SetupHelper", "PayloadInstaller.cs");
        var acquisitionSource = ReadRepoFile("src", "V3dfy.SetupHelper", "InstallerModelPackAcquisition.cs");
        var importSource = ReadRepoFile("src", "V3dfy.SetupHelper", "InstallerModelPackImport.cs");

        Assert.Contains("SelectedModelPackRows", uiSource);
        Assert.Contains("AcquiredModelPackFiles", uiSource);
        Assert.Contains("ImportedModelPackFiles", uiSource);
        Assert.Contains("selectedModelPackRows = modelPackSelectionPageModel.SelectionState.SelectedRows;", uiSource);
        Assert.Contains("No optional model packs selected.", uiSource);
        Assert.Contains("Optional model packs selected for download, verification, and install after app payload", uiSource);
        Assert.Contains("StartPayloadInstall();", uiSource);
        Assert.Contains("AcquireSelectedModelPacksAsync", uiSource);
        Assert.Contains("ImportAcquiredModelPacksAsync", uiSource);
        Assert.Contains("new InstallerModelPackAcquisitionService().AcquireAsync", uiSource);
        Assert.Contains("new InstallerModelPackImportService().ImportAsync", uiSource);
        Assert.Contains("Downloading/verifying optional model packs", uiSource);
        Assert.Contains("Optional model packs installed", uiSource);

        var installMethod = ExtractSourceRange(
            uiSource,
            "private async Task RunInstallAsync()",
            "private async Task<InstallerModelPackAcquisitionResult?> AcquireSelectedModelPacksAsync(");
        var payloadIndex = installMethod.IndexOf("PayloadInstaller().InstallAsync", StringComparison.Ordinal);
        var acquisitionIndex = installMethod.IndexOf("AcquireSelectedModelPacksAsync", StringComparison.Ordinal);
        var importIndex = installMethod.IndexOf("ImportAcquiredModelPacksAsync", StringComparison.Ordinal);
        Assert.True(payloadIndex >= 0);
        Assert.True(acquisitionIndex > payloadIndex);
        Assert.True(importIndex > acquisitionIndex);

        Assert.DoesNotContain("ModelPackInstallExecutor", acquisitionSource);
        Assert.DoesNotContain("pretrained_models", uiSource);
        Assert.DoesNotContain("pretrained_models", acquisitionSource);
        Assert.Contains("ModelPackInstallExecutor", importSource);
        Assert.Contains("GetDefaultTargetPretrainedModelsRoot", importSource);
        Assert.Contains("options.TargetDirectory", uiSource);
        Assert.Contains("options.WorkDirectory", uiSource);
        Assert.DoesNotContain("ModelPackInstallExecutor", payloadInstallerSource);
        Assert.DoesNotContain("SelectedModelPackRows", payloadInstallerSource);
        Assert.DoesNotContain("InstallerModelPackSelectionRow", payloadInstallerSource);
    }

    private static string ReadRepoFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([RepositoryRoot(), .. segments]));

    private static string RepositoryRoot()
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

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static string ExtractSourceRange(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0, $"Start marker not found: {startMarker}");
        Assert.True(end > start, $"End marker not found after {startMarker}: {endMarker}");

        return source[start..end];
    }
}
