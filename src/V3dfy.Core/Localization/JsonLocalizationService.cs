namespace V3dfy.Core.Localization;

public sealed class JsonLocalizationService : ILocalizationService
{
    private readonly LocalizationCatalog _catalog;
    private readonly string _fallbackLanguageCode;
    private string _activeLanguageCode;

    public JsonLocalizationService(
        LocalizationCatalog catalog,
        string initialLanguageCode = LocalizationCatalog.EnglishLanguageCode,
        MissingLocalizationReporter? missingReporter = null)
    {
        _catalog = catalog;
        MissingReporter = missingReporter ?? new MissingLocalizationReporter();
        _fallbackLanguageCode = _catalog.HasLanguage(LocalizationCatalog.EnglishLanguageCode)
            ? LocalizationCatalog.EnglishLanguageCode
            : LocalizationCatalog.NormalizeLanguageCode(initialLanguageCode);
        _activeLanguageCode = ResolveActiveLanguage(initialLanguageCode);
    }

    public IReadOnlyList<LocalizationLanguageMetadata> AvailableLanguages => _catalog.AvailableLanguages;

    public string ActiveLanguageCode => _activeLanguageCode;

    public MissingLocalizationReporter MissingReporter { get; }

    public static JsonLocalizationService LoadFromDirectory(
        string localizationDirectory,
        string initialLanguageCode = LocalizationCatalog.EnglishLanguageCode)
    {
        var reporter = new MissingLocalizationReporter();
        var catalog = JsonLocalizationCatalogLoader.LoadFromDirectory(localizationDirectory, reporter);
        return new JsonLocalizationService(catalog, initialLanguageCode, reporter);
    }

    public void SetLanguage(string languageCode)
    {
        _activeLanguageCode = ResolveActiveLanguage(languageCode);
    }

    public string GetString(string key) => GetStringResult(key).Value;

    public LocalizationLookupResult GetStringResult(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var activeLanguageStrings = _catalog.GetStrings(_activeLanguageCode);
        if (activeLanguageStrings.TryGetValue(key, out var activeValue) &&
            !string.IsNullOrWhiteSpace(activeValue))
        {
            return new LocalizationLookupResult(
                Key: key,
                Value: activeValue,
                RequestedLanguageCode: _activeLanguageCode,
                ResolvedLanguageCode: _activeLanguageCode,
                UsedFallback: false,
                MissingInSelectedLanguage: false,
                MissingInEnglish: false);
        }

        var missingInSelectedLanguage = !string.Equals(
            _activeLanguageCode,
            _fallbackLanguageCode,
            StringComparison.OrdinalIgnoreCase);

        if (missingInSelectedLanguage)
        {
            MissingReporter.Report(
                LocalizationMissingKind.SelectedLanguageKeyMissing,
                _activeLanguageCode,
                key,
                $"Missing key '{key}' in selected language '{_activeLanguageCode}'.");
        }

        var fallbackStrings = _catalog.GetStrings(_fallbackLanguageCode);
        if (fallbackStrings.TryGetValue(key, out var fallbackValue) &&
            !string.IsNullOrWhiteSpace(fallbackValue))
        {
            return new LocalizationLookupResult(
                Key: key,
                Value: fallbackValue,
                RequestedLanguageCode: _activeLanguageCode,
                ResolvedLanguageCode: _fallbackLanguageCode,
                UsedFallback: true,
                MissingInSelectedLanguage: missingInSelectedLanguage,
                MissingInEnglish: false);
        }

        MissingReporter.Report(
            LocalizationMissingKind.EnglishKeyMissing,
            _fallbackLanguageCode,
            key,
            $"Missing fallback key '{key}' in English localization.");

        return new LocalizationLookupResult(
            Key: key,
            Value: $"[Missing: {key}]",
            RequestedLanguageCode: _activeLanguageCode,
            ResolvedLanguageCode: _fallbackLanguageCode,
            UsedFallback: true,
            MissingInSelectedLanguage: missingInSelectedLanguage,
            MissingInEnglish: true);
    }

    private string ResolveActiveLanguage(string languageCode)
    {
        var normalizedLanguageCode = LocalizationCatalog.NormalizeLanguageCode(languageCode);
        if (_catalog.HasLanguage(normalizedLanguageCode))
        {
            return normalizedLanguageCode;
        }

        MissingReporter.Report(
            LocalizationMissingKind.SelectedLanguageMissing,
            normalizedLanguageCode,
            normalizedLanguageCode,
            $"Selected language '{normalizedLanguageCode}' is not loaded.");

        return _catalog.HasLanguage(_fallbackLanguageCode)
            ? _fallbackLanguageCode
            : normalizedLanguageCode;
    }
}
