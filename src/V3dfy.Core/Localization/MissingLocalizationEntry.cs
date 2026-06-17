namespace V3dfy.Core.Localization;

public sealed record MissingLocalizationEntry(
    LocalizationMissingKind Kind,
    string LanguageCode,
    string Key,
    string Detail);
