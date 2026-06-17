using V3dfy.Core.Localization;

namespace V3dfy.App.ViewModels;

public sealed record AppLanguageOptionViewModel(
    string Code,
    string DisplayName,
    string NativeName,
    string Culture)
{
    public string Label =>
        string.IsNullOrWhiteSpace(NativeName) ? DisplayName : NativeName;

    public static AppLanguageOptionViewModel FromMetadata(LocalizationLanguageMetadata metadata) =>
        new(
            metadata.Code,
            metadata.DisplayName,
            metadata.NativeName,
            metadata.Culture);

    public override string ToString() => Label;
}
