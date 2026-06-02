using V3dfy.App.Mvvm;

namespace V3dfy.App.ViewModels;

public sealed class LocalizedOptionViewModel<T>(
    T value,
    string englishDisplayName,
    string spanishDisplayName) : ObservableObject
{
    private bool _isSpanish;

    public T Value { get; } = value;

    public string DisplayName => _isSpanish ? spanishDisplayName : englishDisplayName;

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
