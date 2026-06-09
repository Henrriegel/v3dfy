using V3dfy.Core.Models;
using V3dfy.Core.Planning;

namespace V3dfy.Engine.Iw3.Commands;

public sealed record Iw3DepthModelMapping(
    string Key,
    string EnglishName,
    string SpanishName,
    string ModelRelativePath,
    string DepthModelName,
    string EnglishStatusNote,
    string SpanishStatusNote,
    bool IsReadySelectable);

public sealed record Iw3DepthModelRegistryEntry(
    string Key,
    string EnglishName,
    string SpanishName,
    string DepthModelName,
    IReadOnlyList<string> ExpectedRelativePaths,
    IReadOnlyList<string> ExpectedFileNames,
    IReadOnlyList<string> CatalogIdentifiers,
    string EnglishStatusNote,
    string SpanishStatusNote,
    bool IsReadySelectable,
    bool RequiresLocalFile)
{
    public bool MatchesCandidate(LocalModelSelectionCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var normalizedPath = Iw3DepthModelMapper.NormalizeRelativePath(candidate.RelativePath);
        if (!string.IsNullOrWhiteSpace(normalizedPath) &&
            ExpectedRelativePaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return candidate.IsCatalogManaged &&
            CatalogIdentifiers.Contains(candidate.Id, StringComparer.OrdinalIgnoreCase) &&
            ExpectedFileNames.Contains(candidate.FileName, StringComparer.OrdinalIgnoreCase);
    }

    public bool MatchesPlanSelection(LocalModelPlanSelection selectedModel)
    {
        ArgumentNullException.ThrowIfNull(selectedModel);

        var normalizedPath = Iw3DepthModelMapper.NormalizeRelativePath(selectedModel.RelativePath);
        if (!string.IsNullOrWhiteSpace(normalizedPath) &&
            ExpectedRelativePaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return selectedModel.Source == LocalModelPlanSource.CatalogMetadata &&
            CatalogIdentifiers.Contains(selectedModel.Id, StringComparer.OrdinalIgnoreCase) &&
            ExpectedFileNames.Contains(selectedModel.FileName, StringComparer.OrdinalIgnoreCase);
    }

    public Iw3DepthModelMapping CreateMapping(string modelRelativePath) => new(
        Key,
        EnglishName,
        SpanishName,
        modelRelativePath,
        DepthModelName,
        EnglishStatusNote,
        SpanishStatusNote,
        IsReadySelectable);
}

public static class Iw3DepthModelMapper
{
    public const string DepthAnythingMetricDepthIndoorKey =
        "depth-anything-metric-indoor";
    public const string DepthAnythingMetricDepthIndoorRelativePath =
        "hub/checkpoints/depth_anything_metric_depth_indoor.pt";
    public const string ZoeDAnyNDepthModelName = "ZoeD_Any_N";
    public const string DepthAnythingMetricDepthIndoorEnglishName =
        "Depth Anything Metric Indoor";
    public const string DepthAnythingMetricDepthIndoorSpanishName =
        "Depth Anything Metric Indoor";

    public static IReadOnlyList<Iw3DepthModelRegistryEntry> RegistryEntries { get; } =
    [
        new(
            Key: DepthAnythingMetricDepthIndoorKey,
            EnglishName: DepthAnythingMetricDepthIndoorEnglishName,
            SpanishName: DepthAnythingMetricDepthIndoorSpanishName,
            DepthModelName: ZoeDAnyNDepthModelName,
            ExpectedRelativePaths: [DepthAnythingMetricDepthIndoorRelativePath],
            ExpectedFileNames: ["depth_anything_metric_depth_indoor.pt"],
            CatalogIdentifiers:
            [
                DepthAnythingMetricDepthIndoorKey,
                "depth-anything-metric-depth-indoor",
                "zoed-any-n",
                ZoeDAnyNDepthModelName,
            ],
            EnglishStatusNote:
                "Ready when the verified local checkpoint exists in the bundled iw3 pretrained_models folder.",
            SpanishStatusNote:
                "Listo cuando el checkpoint local verificado existe en la carpeta pretrained_models incluida de iw3.",
            IsReadySelectable: true,
            RequiresLocalFile: true),
    ];

    public static IReadOnlyList<LocalModelSelectionCandidate> CreateSelectableCandidates(
        IEnumerable<LocalModelSelectionCandidate> candidates,
        bool useSpanish)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Select(candidate => TryMap(candidate, out var mapping) && mapping is not null
                ? CreateSelectableCandidate(candidate, mapping, useSpanish)
                : null)
            .Where(candidate => candidate is not null)
            .Cast<LocalModelSelectionCandidate>()
            .OrderBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<LocalModelSelectionCandidate> GetUnmappedCandidates(
        IEnumerable<LocalModelSelectionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Where(candidate => !TryMap(candidate, out _))
            .ToArray();
    }

    public static bool TryMap(
        LocalModelSelectionCandidate? candidate,
        out Iw3DepthModelMapping? mapping)
    {
        mapping = null;
        if (candidate is null)
        {
            return false;
        }

        var entry = RegistryEntries.FirstOrDefault(entry => entry.MatchesCandidate(candidate));
        if (entry is null || !entry.IsReadySelectable)
        {
            return false;
        }

        mapping = entry.CreateMapping(NormalizeRelativePath(candidate.RelativePath));
        return true;
    }

    public static bool TryMap(
        LocalModelPlanSelection? selectedModel,
        out Iw3DepthModelMapping? mapping)
    {
        mapping = null;
        if (selectedModel is null)
        {
            return false;
        }

        var entry = RegistryEntries.FirstOrDefault(entry => entry.MatchesPlanSelection(selectedModel));
        if (entry is null || !entry.IsReadySelectable)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selectedModel.Iw3DepthModelName) &&
            !string.Equals(
                selectedModel.Iw3DepthModelName,
                entry.DepthModelName,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        mapping = entry.CreateMapping(NormalizeRelativePath(selectedModel.RelativePath));
        return true;
    }

    public static string NormalizeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathFullyQualified(relativePath) ||
            Path.IsPathRooted(relativePath))
        {
            return string.Empty;
        }

        var segments = relativePath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            return string.Empty;
        }

        return string.Join('/', segments);
    }

    private static LocalModelSelectionCandidate CreateSelectableCandidate(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelMapping mapping,
        bool useSpanish)
    {
        var displayName = useSpanish
            ? mapping.SpanishName
            : mapping.EnglishName;

        return candidate with
        {
            DisplayName = displayName,
            SpanishDisplayName = mapping.SpanishName,
            Iw3DepthModelName = mapping.DepthModelName,
            MappingKey = mapping.Key,
            EnglishStatusNote = mapping.EnglishStatusNote,
            SpanishStatusNote = mapping.SpanishStatusNote,
        };
    }
}
