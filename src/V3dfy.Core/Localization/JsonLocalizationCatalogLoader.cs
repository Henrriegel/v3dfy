using System.Text.Json;
using System.Text.Json.Serialization;

namespace V3dfy.Core.Localization;

public static class JsonLocalizationCatalogLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
    };

    public static LocalizationCatalog LoadFromDirectory(
        string localizationDirectory,
        MissingLocalizationReporter? reporter = null)
    {
        var metadata = new List<LocalizationLanguageMetadata>();
        var stringsByLanguage = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(localizationDirectory))
        {
            reporter?.Report(
                LocalizationMissingKind.SelectedLanguageMissing,
                LocalizationCatalog.EnglishLanguageCode,
                localizationDirectory,
                "Localization directory is missing.");

            return new LocalizationCatalog(metadata, stringsByLanguage);
        }

        foreach (var filePath in Directory.EnumerateFiles(localizationDirectory, "*.json"))
        {
            try
            {
                var document = JsonSerializer.Deserialize<LocalizationFileDocument>(
                    File.ReadAllText(filePath),
                    SerializerOptions);

                if (document?.Meta is null)
                {
                    reporter?.Report(
                        LocalizationMissingKind.InvalidLanguageFile,
                        Path.GetFileNameWithoutExtension(filePath),
                        filePath,
                        "Localization file is missing meta.");
                    continue;
                }

                var code = LocalizationCatalog.NormalizeLanguageCode(document.Meta.Code);
                var language = new LocalizationLanguageMetadata(
                    Code: code,
                    DisplayName: UseFallbackText(document.Meta.DisplayName, code),
                    NativeName: UseFallbackText(document.Meta.NativeName, document.Meta.DisplayName, code),
                    Culture: UseFallbackText(document.Meta.Culture, code),
                    Fallback: LocalizationCatalog.NormalizeLanguageCode(document.Meta.Fallback),
                    Visible: document.Meta.Visible ?? true);

                metadata.Add(language);
                stringsByLanguage[code] = new Dictionary<string, string>(
                    document.Strings ?? new Dictionary<string, string>(StringComparer.Ordinal),
                    StringComparer.Ordinal);
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                reporter?.Report(
                    LocalizationMissingKind.InvalidLanguageFile,
                    Path.GetFileNameWithoutExtension(filePath),
                    filePath,
                    exception.Message);
            }
        }

        return new LocalizationCatalog(metadata, stringsByLanguage);
    }

    private static string UseFallbackText(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private sealed class LocalizationFileDocument
    {
        [JsonPropertyName("meta")]
        public LocalizationFileMetadata? Meta { get; set; }

        [JsonPropertyName("strings")]
        public Dictionary<string, string>? Strings { get; set; }
    }

    private sealed class LocalizationFileMetadata
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("nativeName")]
        public string? NativeName { get; set; }

        [JsonPropertyName("culture")]
        public string? Culture { get; set; }

        [JsonPropertyName("fallback")]
        public string? Fallback { get; set; }

        [JsonPropertyName("visible")]
        public bool? Visible { get; set; }
    }
}
