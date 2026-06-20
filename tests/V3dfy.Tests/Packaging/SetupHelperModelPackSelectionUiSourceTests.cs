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
        Assert.Contains("var useSpanish = selectedLanguage == SetupUiLanguage.Spanish;", prepareMethod);
        Assert.Contains("DiscoverOffline(", prepareMethod);
        Assert.Contains("useSpanish)", prepareMethod);
        Assert.Contains("DiscoverWeb(manifest, useSpanish)", prepareMethod);
        Assert.Contains("new InstallerModelPackSelectionPageModel(discovery, uiText)", prepareMethod);
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

        Assert.Contains("uiText.OptionalModelPackCatalogLoadFailed", prepareMethod);
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

        Assert.Contains("uiText.OptionalModelPacksTitle", source);
        Assert.Contains("uiText.OptionalModelPacksBody", source);
        Assert.Contains("uiText.OfflineNoPacksText", source);
        Assert.Contains(InstallerModelPackSelectionPageModel.TitleText, pageModelSource);
        Assert.Contains(InstallerModelPackSelectionPageModel.BodyText, pageModelSource);
        Assert.Contains(InstallerModelPackSelectionPageModel.OfflineNoPacksText, pageModelSource);
        Assert.Contains("DataGridView", createPanel);
        Assert.Contains("DataGridViewCheckBoxColumn", createPanel);
        Assert.Contains("HeaderText = uiText.ModelColumn", createPanel);
        Assert.Contains("HeaderText = uiText.BestUseColumn", createPanel);
        Assert.Contains("HeaderText = uiText.SizeColumn", createPanel);
        Assert.Contains("HeaderText = uiText.SourceStatusColumn", createPanel);
        Assert.Contains("AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells", createPanel);
        Assert.Contains("WrapMode = DataGridViewTriState.True", createPanel);
        Assert.Contains("Text = uiText.ContinueButton", createPanel + source);
        Assert.DoesNotContain("Text = \"Select all\"", source);
        Assert.DoesNotContain("Text = \"Deselect all\"", source);
    }

    [Fact]
    public void SelectionPage_RefreshesLocalizedRowTextAfterLanguageChange()
    {
        var source = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");
        var selectionSource = ReadRepoFile("src", "V3dfy.SetupHelper", "InstallerModelPackSelection.cs");
        var applyLocalizedText = ExtractSourceRange(
            source,
            "private void ApplyLocalizedText()",
            "private void RefreshThemeComboBoxItems()");
        var loadRows = ExtractSourceRange(
            source,
            "private void LoadModelPackRows(InstallerModelPackSelectionPageModel pageModel)",
            "private void UpdateModelPackSelectionControls()");

        Assert.Contains("UpdateModelPackBestUseCells();", applyLocalizedText);
        Assert.Contains("row.GetBestUse(selectedLanguage)", loadRows);
        Assert.Contains("selectionRow.GetBestUse(selectedLanguage)", source);
        Assert.Contains("BestUseEnglish", selectionSource);
        Assert.Contains("BestUseSpanish", selectionSource);
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
    public void UiRunner_HasLanguageAndThemeSelectors()
    {
        var source = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");
        var textSource = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupUiText.cs");
        var themeSource = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupUiThemeDefinition.cs");

        Assert.Contains("languageComboBox", source);
        Assert.Contains("themeComboBox", source);
        Assert.Contains("SetupUiLanguage.English", source);
        Assert.Contains("SetupUiLanguage.Spanish", source);
        Assert.Contains("SetupUiThemeKind.Light", source);
        Assert.Contains("SetupUiThemeKind.Dark", source);
        Assert.Contains("OnLanguageSelectionChanged", source);
        Assert.Contains("OnThemeSelectionChanged", source);
        Assert.Contains("ApplyLocalizedText", source);
        Assert.Contains("ApplyTheme", source);
        Assert.Contains("English", textSource);
        Assert.Contains("Español", textSource);
        Assert.Contains("Light", themeSource);
        Assert.Contains("Dark", themeSource);
    }

    [Fact]
    public void UiRunner_AppliesThemeToVisibleInstallerSurfaces()
    {
        var source = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");
        var applyTheme = ExtractSourceRange(
            source,
            "private void ApplyTheme()",
            "private static void ApplyThemeToControl");

        Assert.Contains("BackColor = windowBackground", applyTheme);
        Assert.Contains("ApplyThemeToControl(root", applyTheme);
        Assert.Contains("ApplyThemeToControl(progressPanel", applyTheme);
        Assert.Contains("ApplyThemeToControl(modelPackSelectionPanel", applyTheme);
        Assert.Contains("overallProgressBar.BackColor", applyTheme);
        Assert.Contains("progressBar.BackColor", applyTheme);
        Assert.Contains("StyleButton(continueButton", applyTheme);
        Assert.Contains("StyleButton(actionButton", applyTheme);
        Assert.Contains("logListBox.BackColor", applyTheme);
        Assert.Contains("modelPackGrid.BackgroundColor", applyTheme);
        Assert.Contains("modelPackGrid.ColumnHeadersDefaultCellStyle", applyTheme);
    }

    [Fact]
    public void UiRunner_CurrentPackageProgressBarLogicRemainsByteBased()
    {
        var source = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");
        var reportProgress = ExtractSourceRange(
            source,
            "public void ReportProgress(SetupProgressEvent progress)",
            "public void AppendLogLine(string message)");

        Assert.Contains("if (progress.Percent is { } percent)", reportProgress);
        Assert.Contains("progressBar.Style = ProgressBarStyle.Continuous;", reportProgress);
        Assert.Contains("progressBar.Value = Math.Clamp((int)Math.Round(percent * 10)", reportProgress);
        Assert.Contains("progress.CurrentBytes", reportProgress);
        Assert.Contains("progress.TotalBytes", reportProgress);
        Assert.Contains("progressBar.Style = ProgressBarStyle.Marquee;", reportProgress);
        Assert.Contains("UpdateOverallProgress(progress);", reportProgress);
    }

    [Fact]
    public void UiRunner_InstallProgressLayoutAlwaysShowsBothProgressBars()
    {
        var source = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");
        var constructor = ExtractSourceRange(
            source,
            "public SetupProgressForm(",
            "private TableLayoutPanel CreateModelPackSelectionPanel()");
        var startMethod = ExtractSourceRange(
            source,
            "private void StartPayloadInstall()",
            "protected override void OnFormClosing");
        var layoutMethod = ExtractSourceRange(
            source,
            "private void ShowInstallProgressLayout()",
            "protected override void OnFormClosing");

        Assert.Contains("overallProgressBar = new ProgressBar", constructor);
        Assert.Contains("progressBar = new ProgressBar", constructor);
        Assert.Contains("AutoSizeMode = AutoSizeMode.GrowAndShrink", constructor);
        Assert.Contains("Dock = DockStyle.Top", constructor);
        Assert.Contains("new RowStyle(SizeType.Absolute, OverallProgressBarHeight)", constructor);
        Assert.Contains("new RowStyle(SizeType.Absolute, CurrentProgressBarHeight)", constructor);
        Assert.Contains("MinimumSize = new Size(0, OverallProgressBarHeight)", constructor);
        Assert.Contains("MinimumSize = new Size(0, CurrentProgressBarHeight)", constructor);
        Assert.Contains("progressPanel.Controls.Add(overallProgressTextLabel, 0, 0)", constructor);
        Assert.Contains("progressPanel.Controls.Add(overallProgressBar, 0, 1)", constructor);
        Assert.Contains("progressPanel.Controls.Add(currentProgressHeaderLabel, 0, 2)", constructor);
        Assert.Contains("progressPanel.Controls.Add(progressBar, 0, 3)", constructor);
        Assert.Contains("progressPanel.Controls.Add(statusLabel, 0, 4)", constructor);
        Assert.Contains("ShowInstallProgressLayout();", startMethod);
        Assert.Contains("progressPanel.Visible = true;", layoutMethod);
        Assert.Contains("overallProgressBar.Visible = true;", layoutMethod);
        Assert.Contains("progressBar.Visible = true;", layoutMethod);
        Assert.DoesNotContain("progressBar.Visible = false", source);
        Assert.DoesNotContain("options.Mode", layoutMethod);
    }

    [Fact]
    public void UiRunner_WebPayloadDownloadKeepsCurrentProgressBarVisible()
    {
        var uiSource = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");
        var installerSource = ReadRepoFile("src", "V3dfy.SetupHelper", "PayloadInstaller.cs");
        var reportProgress = ExtractSourceRange(
            uiSource,
            "public void ReportProgress(SetupProgressEvent progress)",
            "public void AppendLogLine(string message)");
        var layoutMethod = ExtractSourceRange(
            uiSource,
            "private void ShowInstallProgressLayout()",
            "protected override void OnFormClosing");

        Assert.Contains("[SetupProgressPhase.DownloadingPart] = new(200, 2700)", installerSource);
        Assert.Contains("SetupProgressPhase.DownloadingPart", installerSource);
        Assert.Contains("progressBar.Visible = true;", layoutMethod);
        Assert.Contains("progressBar.Style = ProgressBarStyle.Continuous;", reportProgress);
        Assert.Contains("progressBar.Value = Math.Clamp((int)Math.Round(percent * 10)", reportProgress);
        Assert.DoesNotContain("progressBar.Visible = false", uiSource);
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
        Assert.Contains("uiText.FormatNoOptionalModelPacksSelected()", uiSource);
        Assert.Contains("uiText.FormatOptionalModelPacksSelected", uiSource);
        Assert.Contains("StartPayloadInstall();", uiSource);
        Assert.Contains("AcquireSelectedModelPacksAsync", uiSource);
        Assert.Contains("ImportAcquiredModelPacksAsync", uiSource);
        Assert.Contains("new InstallerModelPackAcquisitionService().AcquireAsync", uiSource);
        Assert.Contains("new InstallerModelPackImportService().ImportAsync", uiSource);
        Assert.Contains("uiText.FormatDownloadingVerifyingOptionalModelPacks", uiSource);
        Assert.Contains("uiText.FormatOptionalModelPacksInstalled", uiSource);

        var installMethod = ExtractSourceRange(
            uiSource,
            "private async Task RunInstallAsync()",
            "private void TryDeleteInstalledPayloadAfterFailure");
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

    [Fact]
    public void UiRunner_ReservesOverallProgressForSelectedModelPacks()
    {
        var uiSource = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");
        var trackerSource = ReadRepoFile(
            "src",
            "V3dfy.SetupHelper",
            "SetupOptionalModelPackOverallProgressTracker.cs");
        var installMethod = ExtractSourceRange(
            uiSource,
            "private async Task RunInstallAsync()",
            "private void TryDeleteInstalledPayloadAfterFailure");

        Assert.Contains("ISetupProgress progress = new UiSetupProgress(this);", installMethod);
        Assert.Contains("var hasSelectedModelPacks = selectedModelPackRows.Count > 0;", installMethod);
        Assert.Contains("new SetupOptionalModelPackOverallProgressTracker", installMethod);
        Assert.Contains("new SetupOptionalModelPackProgressItem", installMethod);
        Assert.Contains("PayloadInstaller().InstallAsync", installMethod);
        Assert.Contains("AcquireSelectedModelPacksAsync", installMethod);
        Assert.Contains("ImportAcquiredModelPacksAsync", installMethod);
        Assert.Contains("if (hasSelectedModelPacks)", installMethod);
        Assert.Contains("SetupProgressPhase.Completed", installMethod);
        Assert.Contains("PayloadCompletedUnitsWhenModelPacksSelected = 8500", trackerSource);
        Assert.Contains("DownloadingModelPack => new OverallPhaseRange(8500, 9000)", trackerSource);
        Assert.Contains("OverallCompletedUnits = completedUnits", trackerSource);
        Assert.DoesNotContain("options.Mode", installMethod);

        var wrapperIndex = installMethod.IndexOf(
            "new SetupOptionalModelPackOverallProgressTracker",
            StringComparison.Ordinal);
        var payloadIndex = installMethod.IndexOf("PayloadInstaller().InstallAsync", StringComparison.Ordinal);
        Assert.True(wrapperIndex >= 0);
        Assert.True(payloadIndex > wrapperIndex);
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
