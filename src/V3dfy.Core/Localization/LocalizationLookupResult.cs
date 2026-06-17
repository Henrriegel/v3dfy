namespace V3dfy.Core.Localization;

public sealed record LocalizationLookupResult(
    string Key,
    string Value,
    string RequestedLanguageCode,
    string ResolvedLanguageCode,
    bool UsedFallback,
    bool MissingInSelectedLanguage,
    bool MissingInEnglish);
