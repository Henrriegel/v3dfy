namespace V3dfy.Core.Localization;

public interface ILocalizationService
{
    IReadOnlyList<LocalizationLanguageMetadata> AvailableLanguages { get; }

    string ActiveLanguageCode { get; }

    MissingLocalizationReporter MissingReporter { get; }

    void SetLanguage(string languageCode);

    string GetString(string key);

    LocalizationLookupResult GetStringResult(string key);
}
