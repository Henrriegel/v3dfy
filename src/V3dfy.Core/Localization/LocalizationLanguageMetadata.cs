namespace V3dfy.Core.Localization;

public sealed record LocalizationLanguageMetadata(
    string Code,
    string DisplayName,
    string NativeName,
    string Culture,
    string Fallback,
    bool Visible);
