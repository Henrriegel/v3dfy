using V3dfy.App.Mvvm;

namespace V3dfy.App.ViewModels;

public sealed class LogEntryViewModel(
    DateTime timestamp,
    string englishMessage,
    string spanishMessage,
    bool isSpanish) : ObservableObject
{
    private bool _isSpanish = isSpanish;

    public string DisplayText =>
        $"[{timestamp:HH:mm:ss}] {(_isSpanish ? spanishMessage : englishMessage)}";

    public void SetLanguage(bool useSpanish)
    {
        if (_isSpanish != useSpanish)
        {
            _isSpanish = useSpanish;
            OnPropertyChanged(nameof(DisplayText));
        }
    }
}
