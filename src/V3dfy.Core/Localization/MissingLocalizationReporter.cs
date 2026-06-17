using System.Collections.ObjectModel;

namespace V3dfy.Core.Localization;

public sealed class MissingLocalizationReporter
{
    private readonly List<MissingLocalizationEntry> _entries = [];
    private readonly HashSet<string> _reportedKeys = new(StringComparer.Ordinal);

    public IReadOnlyList<MissingLocalizationEntry> Entries => new ReadOnlyCollection<MissingLocalizationEntry>(_entries);

    public void Report(
        LocalizationMissingKind kind,
        string languageCode,
        string key,
        string detail)
    {
        var entryKey = $"{kind}|{languageCode}|{key}|{detail}";
        if (!_reportedKeys.Add(entryKey))
        {
            return;
        }

        _entries.Add(new MissingLocalizationEntry(kind, languageCode, key, detail));
    }
}
