using System.Text.Json;
using System.Reflection;
using V3dfy.Core.Localization;

namespace V3dfy.Tests.Localization;

public sealed class JsonLocalizationServiceTests
{
    [Fact]
    public void LoadFromDirectory_LoadsBundledEnglishAndSpanishFiles()
    {
        var service = JsonLocalizationService.LoadFromDirectory(
            ReadRepoPath("src", "V3dfy.App", "Localization"),
            "es");

        Assert.Contains(service.AvailableLanguages, language => language.Code == "en");
        Assert.Contains(service.AvailableLanguages, language => language.Code == "es");
        Assert.Equal("Cerrar", service.GetString(LocalizationKeys.CommonClose));
        Assert.Equal("v3dfy", service.GetString(LocalizationKeys.AppTitle));
    }

    [Fact]
    public void LoadFromDirectory_DiscoversFutureVisibleLanguageFiles()
    {
        using var temp = new TempLocalizationDirectory();
        WriteLanguage(temp.DirectoryPath, "en", new Dictionary<string, string>
        {
            [LocalizationKeys.CommonClose] = "Close",
        });
        WriteLanguage(temp.DirectoryPath, "es", new Dictionary<string, string>
        {
            [LocalizationKeys.CommonClose] = "Cerrar",
        });
        WriteLanguage(temp.DirectoryPath, "fr", new Dictionary<string, string>
        {
            [LocalizationKeys.CommonClose] = "Fermer",
        });

        var service = JsonLocalizationService.LoadFromDirectory(temp.DirectoryPath, "fr");

        Assert.Equal(3, service.AvailableLanguages.Count);
        Assert.Contains(service.AvailableLanguages, language => language.Code == "fr");
        Assert.Equal("fr", service.ActiveLanguageCode);
        Assert.Equal("Fermer", service.GetString(LocalizationKeys.CommonClose));
    }

    [Fact]
    public void GetString_FallsBackPerMissingKeyWithoutChangingActiveLanguage()
    {
        using var temp = new TempLocalizationDirectory();
        WriteLanguage(temp.DirectoryPath, "en", new Dictionary<string, string>
        {
            [LocalizationKeys.CommonClose] = "Close",
            [LocalizationKeys.CommonOpen] = "Open",
            [LocalizationKeys.CommonCancel] = "Cancel",
        });
        WriteLanguage(temp.DirectoryPath, "es", new Dictionary<string, string>
        {
            [LocalizationKeys.CommonClose] = "Cerrar",
            [LocalizationKeys.CommonCancel] = "Cancelar",
        });

        var service = JsonLocalizationService.LoadFromDirectory(temp.DirectoryPath, "es");

        Assert.Equal("Cerrar", service.GetString(LocalizationKeys.CommonClose));
        Assert.Equal("Open", service.GetString(LocalizationKeys.CommonOpen));
        Assert.Equal("Cancelar", service.GetString(LocalizationKeys.CommonCancel));
        Assert.Equal("es", service.ActiveLanguageCode);
        Assert.Contains(service.MissingReporter.Entries, entry =>
            entry.Kind == LocalizationMissingKind.SelectedLanguageKeyMissing &&
            entry.LanguageCode == "es" &&
            entry.Key == LocalizationKeys.CommonOpen);
    }

    [Fact]
    public void GetString_ImageKeyFallsBackPerMissingSpanishKeyWithoutChangingActiveLanguage()
    {
        using var temp = new TempLocalizationDirectory();
        WriteLanguage(temp.DirectoryPath, "en", new Dictionary<string, string>
        {
            [LocalizationKeys.ImageWorkflowParallaxTitle] = "2.5D Parallax",
            [LocalizationKeys.ImageWorkflowStereoTitle] = "Stereoscopic image",
        });
        WriteLanguage(temp.DirectoryPath, "es", new Dictionary<string, string>
        {
            [LocalizationKeys.ImageWorkflowStereoTitle] = "Imagen estereoscopica",
        });

        var service = JsonLocalizationService.LoadFromDirectory(temp.DirectoryPath, "es");

        Assert.Equal("2.5D Parallax", service.GetString(LocalizationKeys.ImageWorkflowParallaxTitle));
        Assert.Equal("es", service.ActiveLanguageCode);
        Assert.Equal("Imagen estereoscopica", service.GetString(LocalizationKeys.ImageWorkflowStereoTitle));
        Assert.Contains(service.MissingReporter.Entries, entry =>
            entry.Kind == LocalizationMissingKind.SelectedLanguageKeyMissing &&
            entry.LanguageCode == "es" &&
            entry.Key == LocalizationKeys.ImageWorkflowParallaxTitle);
    }

    [Fact]
    public void GetString_VideoKeyFallsBackPerMissingSpanishKeyWithoutChangingActiveLanguage()
    {
        using var temp = new TempLocalizationDirectory();
        WriteLanguage(temp.DirectoryPath, "en", new Dictionary<string, string>
        {
            [LocalizationKeys.VideoPreviewRequiredTitle] = "Preview required",
            [LocalizationKeys.VideoPreviewGenerate] = "Generate preview",
        });
        WriteLanguage(temp.DirectoryPath, "es", new Dictionary<string, string>
        {
            [LocalizationKeys.VideoPreviewGenerate] = "Generar vista previa",
        });

        var service = JsonLocalizationService.LoadFromDirectory(temp.DirectoryPath, "es");

        Assert.Equal("Preview required", service.GetString(LocalizationKeys.VideoPreviewRequiredTitle));
        Assert.Equal("es", service.ActiveLanguageCode);
        Assert.Equal("Generar vista previa", service.GetString(LocalizationKeys.VideoPreviewGenerate));
        Assert.Contains(service.MissingReporter.Entries, entry =>
            entry.Kind == LocalizationMissingKind.SelectedLanguageKeyMissing &&
            entry.LanguageCode == "es" &&
            entry.Key == LocalizationKeys.VideoPreviewRequiredTitle);
    }

    [Fact]
    public void GetString_WhenEnglishKeyIsMissing_ReturnsVisibleMissingMarkerAndReportsIt()
    {
        using var temp = new TempLocalizationDirectory();
        WriteLanguage(temp.DirectoryPath, "en", new Dictionary<string, string>
        {
            [LocalizationKeys.CommonClose] = "Close",
        });
        WriteLanguage(temp.DirectoryPath, "es", new Dictionary<string, string>
        {
            [LocalizationKeys.CommonClose] = "Cerrar",
        });

        var service = JsonLocalizationService.LoadFromDirectory(temp.DirectoryPath, "es");
        var result = service.GetStringResult("Common.DoesNotExist");

        Assert.Equal("[Missing: Common.DoesNotExist]", result.Value);
        Assert.True(result.MissingInEnglish);
        Assert.Contains(service.MissingReporter.Entries, entry =>
            entry.Kind == LocalizationMissingKind.EnglishKeyMissing &&
            entry.Key == "Common.DoesNotExist");
    }

    [Fact]
    public void SetLanguage_WhenSelectedLanguageFileIsMissing_FallsBackToEnglish()
    {
        using var temp = new TempLocalizationDirectory();
        WriteLanguage(temp.DirectoryPath, "en", new Dictionary<string, string>
        {
            [LocalizationKeys.CommonClose] = "Close",
        });

        var service = JsonLocalizationService.LoadFromDirectory(temp.DirectoryPath, "es");

        Assert.Equal("en", service.ActiveLanguageCode);
        Assert.Equal("Close", service.GetString(LocalizationKeys.CommonClose));
        Assert.Contains(service.MissingReporter.Entries, entry =>
            entry.Kind == LocalizationMissingKind.SelectedLanguageMissing &&
            entry.LanguageCode == "es");
    }

    [Fact]
    public void SetLanguage_WhenSelectedLanguageIsInvalid_FallsBackToEnglish()
    {
        using var temp = new TempLocalizationDirectory();
        WriteLanguage(temp.DirectoryPath, "en", new Dictionary<string, string>
        {
            [LocalizationKeys.CommonClose] = "Close",
        });
        WriteLanguage(temp.DirectoryPath, "es", new Dictionary<string, string>
        {
            [LocalizationKeys.CommonClose] = "Cerrar",
        });

        var service = JsonLocalizationService.LoadFromDirectory(temp.DirectoryPath, "es");
        service.SetLanguage("zz");

        Assert.Equal("en", service.ActiveLanguageCode);
        Assert.Equal("Close", service.GetString(LocalizationKeys.CommonClose));
        Assert.Contains(service.MissingReporter.Entries, entry =>
            entry.Kind == LocalizationMissingKind.SelectedLanguageMissing &&
            entry.LanguageCode == "zz");
    }

    [Fact]
    public void LoadFromDirectory_SkipsInvalidLanguageFilesWithoutRejectingValidFiles()
    {
        using var temp = new TempLocalizationDirectory();
        WriteLanguage(temp.DirectoryPath, "en", new Dictionary<string, string>
        {
            [LocalizationKeys.CommonClose] = "Close",
        });
        File.WriteAllText(Path.Combine(temp.DirectoryPath, "broken.json"), "{");

        var service = JsonLocalizationService.LoadFromDirectory(temp.DirectoryPath);

        Assert.Single(service.AvailableLanguages);
        Assert.Equal("Close", service.GetString(LocalizationKeys.CommonClose));
        Assert.Contains(service.MissingReporter.Entries, entry =>
            entry.Kind == LocalizationMissingKind.InvalidLanguageFile &&
            entry.LanguageCode == "broken");
    }

    [Fact]
    public void BundledLocalizationFiles_HaveMatchingKeysWithoutDuplicatesAndCoverConstants()
    {
        var englishKeys = ReadBundledLocalizationKeys("en.json");
        var spanishKeys = ReadBundledLocalizationKeys("es.json");

        Assert.Equal(englishKeys.Count, englishKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(spanishKeys.Count, spanishKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(englishKeys.Except(spanishKeys, StringComparer.Ordinal));
        Assert.Empty(spanishKeys.Except(englishKeys, StringComparer.Ordinal));

        var declaredKeys = typeof(LocalizationKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        Assert.Empty(declaredKeys.Except(englishKeys, StringComparer.Ordinal));
        Assert.Empty(declaredKeys.Except(spanishKeys, StringComparer.Ordinal));
    }

    [Fact]
    public void AppProject_CopiesLocalizationJsonToOutputAndPublish()
    {
        var project = File.ReadAllText(ReadRepoPath("src", "V3dfy.App", "V3dfy.App.csproj"));

        Assert.Contains("<Content Include=\"Localization\\*.json\">", project);
        Assert.Contains("<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>", project);
        Assert.Contains("<CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>", project);
    }

    [Fact]
    public void PublishedBundleValidation_ChecksLocalizationFiles()
    {
        var script = File.ReadAllText(ReadRepoPath("scripts", "validate-iw3-bundle.ps1"));

        Assert.Contains("function Test-LocalizationFiles", script);
        Assert.Contains("Localization\\en.json", script);
        Assert.Contains("Localization\\es.json", script);
        Assert.Contains("Localization en/es key sets match", script);
        Assert.Contains("Localization publish validation skipped because BundleRoot is not inside a published app layout.", script);
    }

    [Fact]
    public void LocalizationDesign_DocumentsImplementedPerKeyFallbackAndDynamicLanguages()
    {
        var docs = File.ReadAllText(ReadRepoPath("docs", "localization.md"));

        Assert.Contains("per-key English fallback", docs);
        Assert.Contains("Per-key fallback means the selected language does not switch wholesale", docs);
        Assert.Contains("active language remains `es`", docs);
        Assert.Contains("Future languages should be addable", docs);
        Assert.Contains("Do not suggest a commit", docs);
    }

    [Fact]
    public void LocalizationSources_DoNotIntroduceExternalRuntimeDependencies()
    {
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(ReadRepoPath("src", "V3dfy.Core", "Localization"), "*.cs")
                .Select(File.ReadAllText));

        Assert.DoesNotContain("http://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpClient", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WebClient", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DownloadFile", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DownloadString", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\v3dfy-iw3-intake", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GetEnvironmentVariable(\"PATH", source, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteLanguage(
        string directory,
        string code,
        IReadOnlyDictionary<string, string> strings,
        bool visible = true)
    {
        Directory.CreateDirectory(directory);
        var document = new
        {
            meta = new
            {
                code,
                displayName = code.ToUpperInvariant(),
                nativeName = code.ToUpperInvariant(),
                culture = code,
                fallback = "en",
                visible,
            },
            strings,
        };

        File.WriteAllText(
            Path.Combine(directory, $"{code}.json"),
            JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string ReadRepoPath(params string[] relativePath)
    {
        var repoRoot = FindRepoRoot();
        return Path.Combine([repoRoot, .. relativePath]);
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

    private static IReadOnlyList<string> ReadBundledLocalizationKeys(string fileName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            ReadRepoPath("src", "V3dfy.App", "Localization", fileName)));
        return document.RootElement
            .GetProperty("strings")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
    }

    private sealed class TempLocalizationDirectory : IDisposable
    {
        public TempLocalizationDirectory()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                "v3dfy-localization-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
