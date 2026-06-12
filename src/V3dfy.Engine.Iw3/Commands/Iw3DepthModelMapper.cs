using V3dfy.Core.Models;
using V3dfy.Core.Planning;

namespace V3dfy.Engine.Iw3.Commands;

public sealed record Iw3DepthModelMapping(
    string Key,
    string EnglishName,
    string SpanishName,
    string ModelRelativePath,
    string DepthModelName,
    string Category,
    Iw3DepthModelAvailability Availability,
    string EnglishStatusNote,
    string SpanishStatusNote,
    bool IsReadySelectable);

public enum Iw3DepthModelAvailability
{
    EmbeddedBase,
    OptionalImportable,
}

public sealed record Iw3DepthModelRegistryEntry(
    string Key,
    string EnglishName,
    string SpanishName,
    string DepthModelName,
    string Category,
    Iw3DepthModelAvailability Availability,
    IReadOnlyList<string> ExpectedRelativePaths,
    IReadOnlyList<string> ExpectedFileNames,
    IReadOnlyList<string> CatalogIdentifiers,
    string EnglishStatusNote,
    string SpanishStatusNote,
    bool IsReadySelectable,
    bool RequiresLocalFile)
{
    public bool IsEmbeddedBase =>
        Availability == Iw3DepthModelAvailability.EmbeddedBase;

    public bool IsOptionalImportable =>
        Availability == Iw3DepthModelAvailability.OptionalImportable;

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
        Category,
        Availability,
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

    public const string DepthAnythingMetricDepthOutdoorKey =
        "depth-anything-metric-outdoor";
    public const string DepthAnythingMetricDepthOutdoorRelativePath =
        "hub/checkpoints/depth_anything_metric_depth_outdoor.pt";
    public const string ZoeDAnyKDepthModelName = "ZoeD_Any_K";
    public const string DepthAnythingMetricDepthOutdoorEnglishName =
        "Depth Anything Metric Outdoor";
    public const string DepthAnythingMetricDepthOutdoorSpanishName =
        "Depth Anything Metric Outdoor";

    public const string ZoeDepthIndoorKey = "zoedepth-indoor";
    public const string ZoeDepthIndoorRelativePath =
        "hub/checkpoints/ZoeD_M12_N.pt";
    public const string ZoeDIndoorDepthModelName = "ZoeD_N";
    public const string ZoeDepthIndoorEnglishName = "ZoeDepth Indoor";
    public const string ZoeDepthIndoorSpanishName = "ZoeDepth Indoor";

    public const string ZoeDepthOutdoorKey = "zoedepth-outdoor";
    public const string ZoeDepthOutdoorRelativePath =
        "hub/checkpoints/ZoeD_M12_K.pt";
    public const string ZoeDOutdoorDepthModelName = "ZoeD_K";
    public const string ZoeDepthOutdoorEnglishName = "ZoeDepth Outdoor";
    public const string ZoeDepthOutdoorSpanishName = "ZoeDepth Outdoor";

    public const string ZoeDepthIndoorOutdoorKey = "zoedepth-indoor-outdoor";
    public const string ZoeDepthIndoorOutdoorRelativePath =
        "hub/checkpoints/ZoeD_M12_NK.pt";
    public const string ZoeDIndoorOutdoorDepthModelName = "ZoeD_NK";
    public const string ZoeDepthIndoorOutdoorEnglishName =
        "ZoeDepth Indoor Outdoor";
    public const string ZoeDepthIndoorOutdoorSpanishName =
        "ZoeDepth Indoor Outdoor";

    public const string DepthAnythingSmallKey = "depth-anything-small";
    public const string DepthAnythingSmallRelativePath =
        "hub/checkpoints/depth_anything_vits14.pth";
    public const string AnySDepthModelName = "Any_S";
    public const string DepthAnythingSmallEnglishName = "Depth Anything Small";
    public const string DepthAnythingSmallSpanishName = "Depth Anything Small";

    public const string DepthAnythingBaseKey = "depth-anything-base";
    public const string DepthAnythingBaseRelativePath =
        "hub/checkpoints/depth_anything_vitb14.pth";
    public const string AnyBDepthModelName = "Any_B";
    public const string DepthAnythingBaseEnglishName = "Depth Anything Base";
    public const string DepthAnythingBaseSpanishName = "Depth Anything Base";

    public const string DepthAnythingV2SmallKey = "depth-anything-v2-small";
    public const string DepthAnythingV2SmallRelativePath =
        "hub/checkpoints/depth_anything_v2_vits.pth";
    public const string AnyV2SDepthModelName = "Any_V2_S";
    public const string DepthAnythingV2SmallEnglishName =
        "Depth Anything V2 Small";
    public const string DepthAnythingV2SmallSpanishName =
        "Depth Anything V2 Small";

    public static IReadOnlyList<Iw3DepthModelRegistryEntry> RegistryEntries { get; } =
    [
        CreateRegistryEntry(
            key: DepthAnythingMetricDepthIndoorKey,
            englishName: DepthAnythingMetricDepthIndoorEnglishName,
            spanishName: DepthAnythingMetricDepthIndoorSpanishName,
            expectedRelativePath: DepthAnythingMetricDepthIndoorRelativePath,
            depthModelName: ZoeDAnyNDepthModelName,
            category: "Metric indoor depth",
            availability: Iw3DepthModelAvailability.EmbeddedBase,
            additionalCatalogIdentifiers:
            [
                "depth-anything-metric-depth-indoor",
                "zoed-any-n",
            ]),
        CreateRegistryEntry(
            key: DepthAnythingMetricDepthOutdoorKey,
            englishName: DepthAnythingMetricDepthOutdoorEnglishName,
            spanishName: DepthAnythingMetricDepthOutdoorSpanishName,
            expectedRelativePath: DepthAnythingMetricDepthOutdoorRelativePath,
            depthModelName: ZoeDAnyKDepthModelName,
            category: "Metric outdoor depth",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            additionalCatalogIdentifiers:
            [
                "depth-anything-metric-depth-outdoor",
                "zoed-any-k",
            ]),
        CreateRegistryEntry(
            key: ZoeDepthIndoorKey,
            englishName: ZoeDepthIndoorEnglishName,
            spanishName: ZoeDepthIndoorSpanishName,
            expectedRelativePath: ZoeDepthIndoorRelativePath,
            depthModelName: ZoeDIndoorDepthModelName,
            category: "ZoeDepth indoor",
            availability: Iw3DepthModelAvailability.OptionalImportable),
        CreateRegistryEntry(
            key: ZoeDepthOutdoorKey,
            englishName: ZoeDepthOutdoorEnglishName,
            spanishName: ZoeDepthOutdoorSpanishName,
            expectedRelativePath: ZoeDepthOutdoorRelativePath,
            depthModelName: ZoeDOutdoorDepthModelName,
            category: "ZoeDepth outdoor",
            availability: Iw3DepthModelAvailability.OptionalImportable),
        CreateRegistryEntry(
            key: ZoeDepthIndoorOutdoorKey,
            englishName: ZoeDepthIndoorOutdoorEnglishName,
            spanishName: ZoeDepthIndoorOutdoorSpanishName,
            expectedRelativePath: ZoeDepthIndoorOutdoorRelativePath,
            depthModelName: ZoeDIndoorOutdoorDepthModelName,
            category: "ZoeDepth indoor/outdoor",
            availability: Iw3DepthModelAvailability.OptionalImportable),
        CreateRegistryEntry(
            key: DepthAnythingSmallKey,
            englishName: DepthAnythingSmallEnglishName,
            spanishName: DepthAnythingSmallSpanishName,
            expectedRelativePath: DepthAnythingSmallRelativePath,
            depthModelName: AnySDepthModelName,
            category: "Depth Anything v1 small",
            availability: Iw3DepthModelAvailability.OptionalImportable),
        CreateRegistryEntry(
            key: DepthAnythingBaseKey,
            englishName: DepthAnythingBaseEnglishName,
            spanishName: DepthAnythingBaseSpanishName,
            expectedRelativePath: DepthAnythingBaseRelativePath,
            depthModelName: AnyBDepthModelName,
            category: "Depth Anything v1 base",
            availability: Iw3DepthModelAvailability.OptionalImportable),
        CreateRegistryEntry(
            key: DepthAnythingV2SmallKey,
            englishName: DepthAnythingV2SmallEnglishName,
            spanishName: DepthAnythingV2SmallSpanishName,
            expectedRelativePath: DepthAnythingV2SmallRelativePath,
            depthModelName: AnyV2SDepthModelName,
            category: "Depth Anything V2 small",
            availability: Iw3DepthModelAvailability.OptionalImportable),
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

    private static Iw3DepthModelRegistryEntry CreateRegistryEntry(
        string key,
        string englishName,
        string spanishName,
        string expectedRelativePath,
        string depthModelName,
        string category,
        Iw3DepthModelAvailability availability,
        IReadOnlyList<string>? additionalCatalogIdentifiers = null)
    {
        var normalizedPath = NormalizeRelativePath(expectedRelativePath);
        var statusNotes = GetStatusNotes(availability);
        var identifiers = new List<string>
        {
            key,
            depthModelName,
        };
        identifiers.AddRange(additionalCatalogIdentifiers ?? []);

        return new(
            Key: key,
            EnglishName: englishName,
            SpanishName: spanishName,
            DepthModelName: depthModelName,
            Category: category,
            Availability: availability,
            ExpectedRelativePaths: [normalizedPath],
            ExpectedFileNames: [GetFileName(normalizedPath)],
            CatalogIdentifiers: identifiers
                .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            EnglishStatusNote: statusNotes.English,
            SpanishStatusNote: statusNotes.Spanish,
            IsReadySelectable: true,
            RequiresLocalFile: true);
    }

    private static (string English, string Spanish) GetStatusNotes(
        Iw3DepthModelAvailability availability) =>
        availability == Iw3DepthModelAvailability.EmbeddedBase
            ? (
                "Ready when the verified local checkpoint exists in the bundled iw3 pretrained_models folder.",
                "Listo cuando el checkpoint local verificado existe en la carpeta pretrained_models incluida de iw3.")
            : (
                "Selectable when this optional checkpoint exists in the iw3 pretrained_models folder; install it with a v3dfy model pack.",
                "Seleccionable cuando este checkpoint opcional existe en la carpeta pretrained_models de iw3; instalelo con un paquete de modelos de v3dfy.");

    private static string GetFileName(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        return normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? string.Empty;
    }
}
