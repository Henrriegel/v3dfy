namespace V3dfy.Tests.App;

public sealed class LocalizationDesignDocumentationTests
{
    [Fact]
    public void LocalizationDesign_DocumentsFileBasedNLanguageLayout()
    {
        var docs = ReadRepoFile("docs", "localization.md");

        Assert.Contains("src/V3dfy.App/Localization/", docs);
        Assert.Contains("en.json", docs);
        Assert.Contains("es.json", docs);
        Assert.Contains("fr.json", docs);
        Assert.Contains("de.json", docs);
        Assert.Contains("pt-BR.json", docs);
        Assert.Contains("ja.json", docs);
        Assert.Contains("adding bundled JSON files", docs);
        Assert.Contains("rather than redesigning", docs);
    }

    [Fact]
    public void LocalizationDesign_DocumentsRuntimeServicesAndDynamicDiscovery()
    {
        var docs = ReadRepoFile("docs", "localization.md");

        Assert.Contains("ILocalizationService", docs);
        Assert.Contains("LocalizationCatalog", docs);
        Assert.Contains("JsonLocalizationCatalogLoader", docs);
        Assert.Contains("JsonLocalizationService", docs);
        Assert.Contains("LocalizationKeys", docs);
        Assert.Contains("Enumerate `Localization/*.json`", docs);
        Assert.Contains("<app root>/Localization/*.json", docs);
    }

    [Fact]
    public void LocalizationDesign_DocumentsFallbackMissingKeysAndOfflineBundling()
    {
        var docs = ReadRepoFile("docs", "localization.md");

        Assert.Contains("fall back to English", docs);
        Assert.Contains("[Missing: Key.Name]", docs);
        Assert.Contains("CopyToPublishDirectory", docs);
        Assert.Contains("Localization/en.json", docs);
        Assert.Contains("Localization/es.json", docs);
        Assert.Contains("must not use", docs);
        Assert.Contains("internet downloads", docs);
    }

    [Fact]
    public void LocalizationDesign_DocumentsLanguageChangeStatePreservation()
    {
        var docs = ReadRepoFile("docs", "localization.md");

        Assert.Contains("Changing language must be a UI-only change", docs);
        Assert.Contains("selected video", docs);
        Assert.Contains("selected image", docs);
        Assert.Contains("selected image workflow", docs);
        Assert.Contains("selected model", docs);
        Assert.Contains("prepared conversion plan", docs);
        Assert.Contains("generated output state", docs);
        Assert.Contains("image or video logs", docs);
        Assert.Contains("modal state", docs);
    }

    [Fact]
    public void LocalizationDesign_DocumentsMigrationPhasesAndTests()
    {
        var docs = ReadRepoFile("docs", "localization.md");

        Assert.Contains("P10A", docs);
        Assert.Contains("P10B", docs);
        Assert.Contains("P10C", docs);
        Assert.Contains("P10D", docs);
        Assert.Contains("P10E", docs);
        Assert.Contains("P10F", docs);
        Assert.Contains("completeness across bundled visible languages", docs);
    }

    [Fact]
    public void LocalizationDesign_DocumentsP10CSelectorP10DImageP10EVideoAndRemainingScope()
    {
        var docs = ReadRepoFile("docs", "localization.md");

        Assert.Contains("P10C connects the WPF language selector", docs);
        Assert.Contains("LanguageOptions` is built from localization metadata", docs);
        Assert.Contains("The ComboBox displays each option label from metadata", docs);
        Assert.Contains("P10C migrated scope", docs);
        Assert.Contains("P10D migrated scope", docs);
        Assert.Contains("Image conversion feature-specific ViewModel and XAML text is", docs);
        Assert.Contains("Image option labels are key-backed", docs);
        Assert.Contains("P10E migrated scope", docs);
        Assert.Contains("Video option labels are key-backed", docs);
        Assert.Contains("P10E follows the same policy for Video logs", docs);
        Assert.Contains("engine DTO", docs);
        Assert.Contains("engine/core DTO summaries", docs);
        Assert.Contains("P10F: focus on global completeness", docs);
    }

    [Fact]
    public void LocalizationDesign_DocumentsFutureTextChangeRules()
    {
        var docs = ReadRepoFile("docs", "localization.md");

        Assert.Contains("## Project rules for future text changes", docs);
        Assert.Contains("avoid adding hardcoded strings", docs);
        Assert.Contains("src/V3dfy.App/Localization/", docs);
        Assert.Contains("en.json", docs);
        Assert.Contains("es.json", docs);
        Assert.Contains("future languages", docs);
        Assert.Contains("Preserve app state", docs);
        Assert.Contains("bundled/offline", docs);
        Assert.Contains("Add or update localization tests", docs);
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
}
