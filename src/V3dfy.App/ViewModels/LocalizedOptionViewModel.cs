using V3dfy.App.Mvvm;
using V3dfy.Core.Localization;

namespace V3dfy.App.ViewModels;

public sealed class LocalizedOptionViewModel<T> : ObservableObject
{
    private readonly string? _englishDisplayName;
    private readonly string? _spanishDisplayName;
    private readonly ILocalizationService? _localizationService;
    private bool _isSpanish;

    public LocalizedOptionViewModel(
        T value,
        string englishDisplayName,
        string spanishDisplayName)
    {
        Value = value;
        _englishDisplayName = englishDisplayName;
        _spanishDisplayName = spanishDisplayName;
    }

    public LocalizedOptionViewModel(
        T value,
        string localizationKey,
        ILocalizationService localizationService)
    {
        Value = value;
        LocalizationKey = localizationKey;
        _localizationService = localizationService;
    }

    public T Value { get; }

    public string? LocalizationKey { get; }

    public string DisplayName => LocalizationKey is not null && _localizationService is not null
        ? _localizationService.GetString(LocalizationKey)
        : _isSpanish ? _spanishDisplayName ?? string.Empty : _englishDisplayName ?? string.Empty;

    public override string ToString() => DisplayName;

    public void SetLanguage(bool useSpanish)
    {
        if (_isSpanish != useSpanish)
        {
            _isSpanish = useSpanish;
            OnPropertyChanged(nameof(DisplayName));
        }
    }
}
