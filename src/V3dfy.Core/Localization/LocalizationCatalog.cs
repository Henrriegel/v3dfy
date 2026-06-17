using System.Collections.ObjectModel;

namespace V3dfy.Core.Localization;

public sealed class LocalizationCatalog
{
    public const string EnglishLanguageCode = "en";

    private readonly Dictionary<string, LocalizationLanguageMetadata> _languages;
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _stringsByLanguage;

    public LocalizationCatalog(
        IEnumerable<LocalizationLanguageMetadata> languages,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> stringsByLanguage)
    {
        _languages = languages
            .Where(language => !string.IsNullOrWhiteSpace(language.Code))
            .GroupBy(language => NormalizeLanguageCode(language.Code), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First() with { Code = group.Key },
                StringComparer.OrdinalIgnoreCase);

        _stringsByLanguage = stringsByLanguage
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                pair => NormalizeLanguageCode(pair.Key),
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<LocalizationLanguageMetadata> AvailableLanguages =>
        _languages.Values
            .Where(language => language.Visible)
            .OrderBy(language => language.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(language => language.Code, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public bool HasLanguage(string languageCode) =>
        _languages.ContainsKey(NormalizeLanguageCode(languageCode));

    public LocalizationLanguageMetadata? GetLanguage(string languageCode)
    {
        _languages.TryGetValue(NormalizeLanguageCode(languageCode), out var language);
        return language;
    }

    public IReadOnlyDictionary<string, string> GetStrings(string languageCode)
    {
        return _stringsByLanguage.TryGetValue(NormalizeLanguageCode(languageCode), out var strings)
            ? strings
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
    }

    public static string NormalizeLanguageCode(string? languageCode) =>
        string.IsNullOrWhiteSpace(languageCode)
            ? EnglishLanguageCode
            : languageCode.Trim();
}
