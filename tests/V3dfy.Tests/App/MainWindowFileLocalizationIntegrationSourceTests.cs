namespace V3dfy.Tests.App;

public sealed class MainWindowFileLocalizationIntegrationSourceTests
{
    [Fact]
    public void ViewModel_LoadsFileBasedLocalizationFromAppLocalRuntimeFolder()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("private readonly ILocalizationService _localizationService;", source);
        Assert.Contains("JsonLocalizationService.LoadFromDirectory", source);
        Assert.Contains("Path.Combine(AppContext.BaseDirectory, \"Localization\")", source);
        Assert.DoesNotContain("C:\\v3dfy-iw3-intake", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Environment.GetEnvironmentVariable(\"PATH", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LanguageSelector_IsDrivenByLocalizationMetadataInsteadOfHardcodedStringList()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var optionModel = ReadRepoFile("src", "V3dfy.App", "ViewModels", "AppLanguageOptionViewModel.cs");
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");

        Assert.Contains("public IReadOnlyList<AppLanguageOptionViewModel> LanguageOptions", source);
        Assert.Contains("CreateLanguageOptions(_localizationService.AvailableLanguages)", source);
        Assert.Contains("AppLanguageOptionViewModel.FromMetadata", source);
        Assert.Contains("string Code,", optionModel);
        Assert.Contains("public string Label", optionModel);
        Assert.Contains("DisplayMemberPath=\"Label\"", xaml);
        Assert.Contains("SelectedValuePath=\"Code\"", xaml);
        Assert.Contains("SelectedValue=\"{Binding SelectedLanguage}\"", xaml);
        Assert.DoesNotContain("public IReadOnlyList<string> LanguageOptions", source);
        Assert.DoesNotContain("[\"Español\", \"English\"]", source);
    }

    [Fact]
    public void SelectedLanguage_ActivatesLocalizationServiceWithoutResettingState()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var property = ExtractSourceRange(
            source,
            "public string SelectedLanguage",
            "public IReadOnlyList<string> ThemeOptions");

        Assert.Contains("_localizationService.SetLanguage(value);", property);
        Assert.Contains("_localizationService.ActiveLanguageCode", property);
        Assert.Contains("ApplyUiOnlyRefresh", property);
        Assert.Contains("RaiseLocalizedPropertiesChanged();", property);
        Assert.Contains("UpdateLocalModelSelectionCandidates(regenerateCurrentPlan: false)", property);
        Assert.DoesNotContain("ResetConversionExecutionState", property);
        Assert.DoesNotContain("ResetImageExportState", property);
        Assert.DoesNotContain("ResetPreviewConversionStageForPreviewAffectingChange", property);
    }

    [Fact]
    public void LanguageAndThemeActivityLogs_UseLocalizedDynamicValues()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");
        var optionModel = ReadRepoFile("src", "V3dfy.App", "ViewModels", "AppLanguageOptionViewModel.cs");
        var localizationKeys = ReadRepoFile("src", "V3dfy.Core", "Localization", "LocalizationKeys.cs");
        var english = ReadRepoFile("src", "V3dfy.App", "Localization", "en.json");
        var spanish = ReadRepoFile("src", "V3dfy.App", "Localization", "es.json");
        var languageProperty = ExtractSourceRange(
            source,
            "public string SelectedLanguage",
            "public IReadOnlyList<string> ThemeOptions");
        var themeProperty = ExtractSourceRange(
            source,
            "public string SelectedTheme",
            "public string SubtitleText");
        var languageHelper = ExtractSourceRange(
            source,
            "private string GetSelectedLanguageLogDisplayName",
            "private string GetSelectedThemeLogDisplayName");
        var themeHelper = ExtractSourceRange(
            source,
            "private string GetSelectedThemeLogDisplayName",
            "private IReadOnlyList<LocalizedOptionViewModel<string>> CreateLocalizedStringOptions");

        Assert.Contains("GetSelectedLanguageLogDisplayName(activeLanguageCode)", languageProperty);
        Assert.DoesNotContain("?.DisplayName ?? activeLanguageCode", languageProperty);
        Assert.DoesNotContain("\"Spanish\"", languageProperty);
        Assert.Contains("?.Label ??", languageHelper);
        Assert.Contains("string.IsNullOrWhiteSpace(NativeName) ? DisplayName : NativeName", optionModel);

        Assert.Contains("GetSelectedThemeLogDisplayName(value)", themeProperty);
        Assert.DoesNotContain("(\"theme\", value)", themeProperty);
        Assert.Contains("\"Dark\" => T(LocalizationKeys.SettingsThemeDark)", themeHelper);
        Assert.Contains("\"Light\" => T(LocalizationKeys.SettingsThemeLight)", themeHelper);
        Assert.Contains("public const string SettingsThemeDark = \"Settings.Theme.Dark\";", localizationKeys);
        Assert.Contains("public const string SettingsThemeLight = \"Settings.Theme.Light\";", localizationKeys);

        Assert.Contains("\"ActivityLog.LanguageSelected.Format\": \"Language selected: {language}.\"", english);
        Assert.Contains("\"ActivityLog.ThemeSelected.Format\": \"Theme selected: {theme}.\"", english);
        Assert.Contains("\"ActivityLog.LanguageSelected.Format\": \"Idioma seleccionado: {language}.\"", spanish);
        Assert.Contains("\"ActivityLog.ThemeSelected.Format\": \"Tema seleccionado: {theme}.\"", spanish);
        Assert.Contains("\"Settings.Theme.Dark\": \"Dark\"", english);
        Assert.Contains("\"Settings.Theme.Light\": \"Light\"", english);
        Assert.Contains("\"Settings.Theme.Dark\": \"Oscuro\"", spanish);
        Assert.Contains("\"Settings.Theme.Light\": \"Claro\"", spanish);
    }

    [Fact]
    public void MigratedShellCommonSettingsAndModalChrome_UseLocalizationKeys()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("public string AppTitle => T(LocalizationKeys.AppTitle);", source);
        Assert.Contains("public string ShellTaglineText => T(LocalizationKeys.ShellTagline);", source);
        Assert.Contains("public string HomeNavigationText => T(LocalizationKeys.SidebarHome);", source);
        Assert.Contains("public string ImageConversionNavigationText => T(LocalizationKeys.SidebarImageConversion);", source);
        Assert.Contains("public string VideoConversionNavigationText => T(LocalizationKeys.SidebarVideoConversion);", source);
        Assert.Contains("LocalizationKeys.SidebarToggleCollapse", source);
        Assert.Contains("LocalizationKeys.SidebarToggleExpand", source);
        Assert.Contains("public string SettingsText => T(LocalizationKeys.SidebarSettings);", source);
        Assert.Contains("public string SettingsTitleText => T(LocalizationKeys.SettingsTitle);", source);
        Assert.Contains("public string LanguageLabel => T(LocalizationKeys.SettingsLanguageLabel);", source);
        Assert.Contains("public string ThemeLabel => T(LocalizationKeys.SettingsAppearanceTheme);", source);
        Assert.Contains("public string VisualSettingsTitleText => T(LocalizationKeys.SettingsAppearanceTitle);", source);
        Assert.Contains("public string ModelsSettingsTitleText => T(LocalizationKeys.SettingsModelsTitle);", source);
        Assert.Contains("public string ToolsEngineSettingsTitleText => T(LocalizationKeys.SettingsToolsEngineTitle);", source);
        Assert.Contains("public string LogsDiagnosticsSettingsTitleText => T(LocalizationKeys.SettingsLogsDiagnosticsTitle);", source);
        Assert.Contains("public string WizardBackText => T(LocalizationKeys.CommonBack);", source);
        Assert.Contains("public string WizardNextText => T(LocalizationKeys.CommonNext);", source);
        Assert.Contains("public string CloseDialogText => T(LocalizationKeys.ModalClose);", source);
        Assert.Contains("public string CancelDialogText => T(LocalizationKeys.ModalCancel);", source);
        Assert.Contains("public string CopyFullLogText => T(LocalizationKeys.ModalCopyFullLog);", source);
        Assert.Contains("public string ImageOpenOutputFolderActionText => T(LocalizationKeys.ModalOpenOutputFolder);", source);
    }

    [Fact]
    public void MainWindowXaml_ContextMenuTextUsesLocalizedBindings()
    {
        var xaml = ReadRepoFile("src", "V3dfy.App", "MainWindow.xaml");
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("public string CommonCopyText => T(LocalizationKeys.CommonCopy);", source);
        Assert.Contains("public string CommonSelectAllText => T(LocalizationKeys.CommonSelectAll);", source);
        Assert.Contains("PlacementTarget.DataContext.CommonCopyText", xaml);
        Assert.Contains("PlacementTarget.DataContext.CommonSelectAllText", xaml);
        Assert.DoesNotContain("Header=\"Copy\"", xaml);
        Assert.DoesNotContain("Header=\"Select all\"", xaml);
    }

    [Fact]
    public void LegacyTextHelper_IsRemovedFromMainWindowViewModel()
    {
        var source = ReadRepoFile("src", "V3dfy.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("private string T(string key) => _localizationService.GetString(key);", source);
        Assert.DoesNotContain("private string Text(string english, string spanish)", source);
        Assert.DoesNotContain("Text(\"", source);
        Assert.Contains("private bool IsSpanish =>", source);
        Assert.Contains("_localizationService.ActiveLanguageCode", source);
        Assert.Contains("\"es\"", source);
    }

    [Fact]
    public void LocalizationJsonFiles_ContainP10CMigratedKeySet()
    {
        var english = ReadRepoFile("src", "V3dfy.App", "Localization", "en.json");
        var spanish = ReadRepoFile("src", "V3dfy.App", "Localization", "es.json");

        foreach (var key in new[]
        {
            "Common.Back",
            "Common.Next",
            "Common.Convert",
            "Shell.Tagline",
            "Sidebar.Toggle.Expand",
            "Sidebar.Toggle.Collapse",
            "Sidebar.Settings",
            "Settings.Appearance.Title",
            "Settings.Appearance.Theme",
            "Settings.Models.Title",
            "Settings.ToolsEngine.Title",
            "Settings.LogsDiagnostics.Title",
            "Settings.About.Title",
            "Settings.Licenses.Title",
            "Modal.Close",
            "Modal.Cancel",
            "Modal.CopyFullLog",
            "Modal.OpenOutputFolder",
            "Modal.ViewModels",
        })
        {
            Assert.Contains($"\"{key}\"", english);
            Assert.Contains($"\"{key}\"", spanish);
        }
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
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);

        return source[start..end];
    }
}
