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
    string ModelFamily,
    Iw3DepthModelAvailability Availability,
    Iw3DepthModelRedistributionDecision RedistributionDecision,
    string SharedCheckpointGroupId,
    Iw3DepthModelDepthType DepthType,
    Iw3DepthModelMediaCapability MediaCapability,
    int ReleasePriority,
    bool IsUserVisibleInSelector,
    string EnglishStatusNote,
    string SpanishStatusNote,
    bool IsReadySelectable);

public enum Iw3DepthModelAvailability
{
    EmbeddedBase,
    OptionalImportable,
}

public enum Iw3DepthModelRedistributionDecision
{
    SafeForPublicRelease,
    SafeWithNotice,
    UserDownloadOnly,
    ExcludeNonCommercial,
    BlockedUnclearLicense,
    NotAModelPackTarget,
}

public enum Iw3DepthModelDepthType
{
    Relative,
    Metric,
    ForcedDisparity,
    Unknown,
}

public enum Iw3DepthModelMediaCapability
{
    ImageAndVideo,
    ImageOnly,
    VideoOnly,
    Stream,
}

public sealed record Iw3DepthModelRegistryEntry(
    string Key,
    string EnglishName,
    string SpanishName,
    string DepthModelName,
    string Category,
    string ModelFamily,
    Iw3DepthModelAvailability Availability,
    Iw3DepthModelRedistributionDecision RedistributionDecision,
    string SharedCheckpointGroupId,
    Iw3DepthModelDepthType DepthType,
    Iw3DepthModelMediaCapability MediaCapability,
    int ReleasePriority,
    bool IsUserVisibleInSelector,
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

    public bool IsPublicPackEligible =>
        RedistributionDecision is
            Iw3DepthModelRedistributionDecision.SafeForPublicRelease or
            Iw3DepthModelRedistributionDecision.SafeWithNotice;

    public bool HasSharedCheckpointGroup =>
        !string.IsNullOrWhiteSpace(SharedCheckpointGroupId);

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
        ModelFamily,
        Availability,
        RedistributionDecision,
        SharedCheckpointGroupId,
        DepthType,
        MediaCapability,
        ReleasePriority,
        IsUserVisibleInSelector,
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

    public const string DepthAnythingLargeKey = "depth-anything-large";
    public const string DepthAnythingLargeRelativePath =
        "hub/checkpoints/depth_anything_vitl14.pth";
    public const string AnyLDepthModelName = "Any_L";
    public const string DepthAnythingLargeEnglishName = "Depth Anything Large";
    public const string DepthAnythingLargeSpanishName = "Depth Anything Large";

    public const string DepthAnythingV2MetricHypersimSmallKey =
        "depth-anything-v2-metric-hypersim-small";
    public const string DepthAnythingV2MetricHypersimSmallRelativePath =
        "hub/checkpoints/depth_anything_v2_metric_hypersim_vits.pth";
    public const string AnyV2NSDepthModelName = "Any_V2_N_S";
    public const string DepthAnythingV2MetricHypersimSmallEnglishName =
        "Depth Anything V2 Metric Hypersim Small";
    public const string DepthAnythingV2MetricHypersimSmallSpanishName =
        "Depth Anything V2 Metric Hypersim Small";

    public const string DepthAnythingV2MetricHypersimBaseKey =
        "depth-anything-v2-metric-hypersim-base";
    public const string DepthAnythingV2MetricHypersimBaseRelativePath =
        "hub/checkpoints/depth_anything_v2_metric_hypersim_vitb.pth";
    public const string AnyV2NBDepthModelName = "Any_V2_N_B";
    public const string DepthAnythingV2MetricHypersimBaseEnglishName =
        "Depth Anything V2 Metric Hypersim Base";
    public const string DepthAnythingV2MetricHypersimBaseSpanishName =
        "Depth Anything V2 Metric Hypersim Base";

    public const string DepthAnythingV2MetricVkittiSmallKey =
        "depth-anything-v2-metric-vkitti-small";
    public const string DepthAnythingV2MetricVkittiSmallRelativePath =
        "hub/checkpoints/depth_anything_v2_metric_vkitti_vits.pth";
    public const string AnyV2KSDepthModelName = "Any_V2_K_S";
    public const string DepthAnythingV2MetricVkittiSmallEnglishName =
        "Depth Anything V2 Metric VKITTI Small";
    public const string DepthAnythingV2MetricVkittiSmallSpanishName =
        "Depth Anything V2 Metric VKITTI Small";

    public const string DepthAnythingV2MetricVkittiBaseKey =
        "depth-anything-v2-metric-vkitti-base";
    public const string DepthAnythingV2MetricVkittiBaseRelativePath =
        "hub/checkpoints/depth_anything_v2_metric_vkitti_vitb.pth";
    public const string AnyV2KBDepthModelName = "Any_V2_K_B";
    public const string DepthAnythingV2MetricVkittiBaseEnglishName =
        "Depth Anything V2 Metric VKITTI Base";
    public const string DepthAnythingV2MetricVkittiBaseSpanishName =
        "Depth Anything V2 Metric VKITTI Base";

    public const string DistillAnyDepthSmallKey = "distill-any-depth-small";
    public const string DistillAnyDepthSmallRelativePath =
        "hub/checkpoints/distill_any_depth_vits.safetensors";
    public const string DistillAnySDepthModelName = "Distill_Any_S";
    public const string DistillAnyDepthSmallEnglishName =
        "Distill Any Depth Small";
    public const string DistillAnyDepthSmallSpanishName =
        "Distill Any Depth Small";

    public const string DepthAnything3MonoLargeKey =
        "depth-anything-3-mono-large";
    public const string DepthAnything3MonoLarge3dTvKey =
        "depth-anything-3-mono-large-3d-tv";
    public const string DepthAnything3MonoLargeRelativePath =
        "hub/checkpoints/da3mono-large.safetensors";
    public const string AnyV3MonoDepthModelName = "Any_V3_Mono";
    public const string AnyV3Mono01DepthModelName = "Any_V3_Mono_01";
    public const string DepthAnything3MonoLargeEnglishName =
        "Depth Anything 3 Mono Large";
    public const string DepthAnything3MonoLargeSpanishName =
        "Depth Anything 3 Mono Large";
    public const string DepthAnything3MonoLarge3dTvEnglishName =
        "Depth Anything 3 Mono Large 3D TV";
    public const string DepthAnything3MonoLarge3dTvSpanishName =
        "Depth Anything 3 Mono Large 3D TV";
    public const string DepthAnything3MonoLargeSharedCheckpointGroup =
        "depth-anything-3-mono-large";

    public const string DepthProKey = "depth-pro";
    public const string DepthProSmallResolutionKey =
        "depth-pro-small-resolution";
    public const string DepthProRelativePath = "hub/checkpoints/depth_pro.pt";
    public const string DepthProDepthModelName = "DepthPro";
    public const string DepthProSDepthModelName = "DepthPro_S";
    public const string DepthProEnglishName = "Depth Pro";
    public const string DepthProSpanishName = "Depth Pro";
    public const string DepthProSmallResolutionEnglishName =
        "Depth Pro Small Resolution";
    public const string DepthProSmallResolutionSpanishName =
        "Depth Pro Small Resolution";
    public const string DepthProSharedCheckpointGroup = "depth-pro";

    public const string VideoDepthAnythingSmallKey =
        "video-depth-anything-small";
    public const string VideoDepthAnythingStreamSmallKey =
        "video-depth-anything-stream-small";
    public const string VideoDepthAnythingSmallRelativePath =
        "hub/checkpoints/video_depth_anything_vits.pth";
    public const string VdaSDepthModelName = "VDA_S";
    public const string VdaStreamSDepthModelName = "VDA_Stream_S";
    public const string VideoDepthAnythingSmallEnglishName =
        "Video Depth Anything Small";
    public const string VideoDepthAnythingSmallSpanishName =
        "Video Depth Anything Small";
    public const string VideoDepthAnythingStreamSmallEnglishName =
        "Video Depth Anything Stream Small";
    public const string VideoDepthAnythingStreamSmallSpanishName =
        "Video Depth Anything Stream Small";
    public const string VideoDepthAnythingSmallSharedCheckpointGroup =
        "video-depth-anything-small";

    public const string MetricVideoDepthAnythingSmallKey =
        "metric-video-depth-anything-small";
    public const string MetricVideoDepthAnythingStreamSmallKey =
        "metric-video-depth-anything-stream-small";
    public const string MetricVideoDepthAnythingSmallRelativePath =
        "hub/checkpoints/metric_video_depth_anything_vits.pth";
    public const string VdaMetricSDepthModelName = "VDA_Metric_S";
    public const string VdaStreamMetricSDepthModelName =
        "VDA_Stream_Metric_S";
    public const string MetricVideoDepthAnythingSmallEnglishName =
        "Metric Video Depth Anything Small";
    public const string MetricVideoDepthAnythingSmallSpanishName =
        "Metric Video Depth Anything Small";
    public const string MetricVideoDepthAnythingStreamSmallEnglishName =
        "Metric Video Depth Anything Stream Small";
    public const string MetricVideoDepthAnythingStreamSmallSpanishName =
        "Metric Video Depth Anything Stream Small";
    public const string MetricVideoDepthAnythingSmallSharedCheckpointGroup =
        "metric-video-depth-anything-small";

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
            modelFamily: "Depth Anything metric",
            depthType: Iw3DepthModelDepthType.Metric,
            releasePriority: 1,
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
            modelFamily: "Depth Anything metric",
            depthType: Iw3DepthModelDepthType.Metric,
            releasePriority: 2,
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
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "ZoeDepth",
            redistributionDecision:
                Iw3DepthModelRedistributionDecision.BlockedUnclearLicense,
            depthType: Iw3DepthModelDepthType.Metric),
        CreateRegistryEntry(
            key: ZoeDepthOutdoorKey,
            englishName: ZoeDepthOutdoorEnglishName,
            spanishName: ZoeDepthOutdoorSpanishName,
            expectedRelativePath: ZoeDepthOutdoorRelativePath,
            depthModelName: ZoeDOutdoorDepthModelName,
            category: "ZoeDepth outdoor",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "ZoeDepth",
            redistributionDecision:
                Iw3DepthModelRedistributionDecision.BlockedUnclearLicense,
            depthType: Iw3DepthModelDepthType.Metric),
        CreateRegistryEntry(
            key: ZoeDepthIndoorOutdoorKey,
            englishName: ZoeDepthIndoorOutdoorEnglishName,
            spanishName: ZoeDepthIndoorOutdoorSpanishName,
            expectedRelativePath: ZoeDepthIndoorOutdoorRelativePath,
            depthModelName: ZoeDIndoorOutdoorDepthModelName,
            category: "ZoeDepth indoor/outdoor",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "ZoeDepth",
            redistributionDecision:
                Iw3DepthModelRedistributionDecision.BlockedUnclearLicense,
            depthType: Iw3DepthModelDepthType.Metric),
        CreateRegistryEntry(
            key: DepthAnythingSmallKey,
            englishName: DepthAnythingSmallEnglishName,
            spanishName: DepthAnythingSmallSpanishName,
            expectedRelativePath: DepthAnythingSmallRelativePath,
            depthModelName: AnySDepthModelName,
            category: "Depth Anything v1 small",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Depth Anything v1",
            releasePriority: 20),
        CreateRegistryEntry(
            key: DepthAnythingBaseKey,
            englishName: DepthAnythingBaseEnglishName,
            spanishName: DepthAnythingBaseSpanishName,
            expectedRelativePath: DepthAnythingBaseRelativePath,
            depthModelName: AnyBDepthModelName,
            category: "Depth Anything v1 base",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Depth Anything v1",
            releasePriority: 30),
        CreateRegistryEntry(
            key: DepthAnythingLargeKey,
            englishName: DepthAnythingLargeEnglishName,
            spanishName: DepthAnythingLargeSpanishName,
            expectedRelativePath: DepthAnythingLargeRelativePath,
            depthModelName: AnyLDepthModelName,
            category: "Depth Anything v1 large",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Depth Anything v1",
            releasePriority: 60),
        CreateRegistryEntry(
            key: DepthAnythingV2SmallKey,
            englishName: DepthAnythingV2SmallEnglishName,
            spanishName: DepthAnythingV2SmallSpanishName,
            expectedRelativePath: DepthAnythingV2SmallRelativePath,
            depthModelName: AnyV2SDepthModelName,
            category: "Depth Anything V2 small",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Depth Anything V2 relative",
            releasePriority: 10),
        CreateRegistryEntry(
            key: DepthAnythingV2MetricHypersimSmallKey,
            englishName: DepthAnythingV2MetricHypersimSmallEnglishName,
            spanishName: DepthAnythingV2MetricHypersimSmallSpanishName,
            expectedRelativePath: DepthAnythingV2MetricHypersimSmallRelativePath,
            depthModelName: AnyV2NSDepthModelName,
            category: "Depth Anything V2 metric Hypersim small",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Depth Anything V2 metric",
            depthType: Iw3DepthModelDepthType.Metric,
            releasePriority: 35),
        CreateRegistryEntry(
            key: DepthAnythingV2MetricHypersimBaseKey,
            englishName: DepthAnythingV2MetricHypersimBaseEnglishName,
            spanishName: DepthAnythingV2MetricHypersimBaseSpanishName,
            expectedRelativePath: DepthAnythingV2MetricHypersimBaseRelativePath,
            depthModelName: AnyV2NBDepthModelName,
            category: "Depth Anything V2 metric Hypersim base",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Depth Anything V2 metric",
            depthType: Iw3DepthModelDepthType.Metric,
            releasePriority: 70),
        CreateRegistryEntry(
            key: DepthAnythingV2MetricVkittiSmallKey,
            englishName: DepthAnythingV2MetricVkittiSmallEnglishName,
            spanishName: DepthAnythingV2MetricVkittiSmallSpanishName,
            expectedRelativePath: DepthAnythingV2MetricVkittiSmallRelativePath,
            depthModelName: AnyV2KSDepthModelName,
            category: "Depth Anything V2 metric VKITTI small",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Depth Anything V2 metric",
            depthType: Iw3DepthModelDepthType.Metric,
            releasePriority: 36),
        CreateRegistryEntry(
            key: DepthAnythingV2MetricVkittiBaseKey,
            englishName: DepthAnythingV2MetricVkittiBaseEnglishName,
            spanishName: DepthAnythingV2MetricVkittiBaseSpanishName,
            expectedRelativePath: DepthAnythingV2MetricVkittiBaseRelativePath,
            depthModelName: AnyV2KBDepthModelName,
            category: "Depth Anything V2 metric VKITTI base",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Depth Anything V2 metric",
            depthType: Iw3DepthModelDepthType.Metric,
            releasePriority: 71),
        CreateRegistryEntry(
            key: DistillAnyDepthSmallKey,
            englishName: DistillAnyDepthSmallEnglishName,
            spanishName: DistillAnyDepthSmallSpanishName,
            expectedRelativePath: DistillAnyDepthSmallRelativePath,
            depthModelName: DistillAnySDepthModelName,
            category: "Distill Any Depth small",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Distill Any Depth",
            depthType: Iw3DepthModelDepthType.Unknown,
            releasePriority: 75),
        CreateRegistryEntry(
            key: DepthAnything3MonoLargeKey,
            englishName: DepthAnything3MonoLargeEnglishName,
            spanishName: DepthAnything3MonoLargeSpanishName,
            expectedRelativePath: DepthAnything3MonoLargeRelativePath,
            depthModelName: AnyV3MonoDepthModelName,
            category: "Depth Anything 3 mono large",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Depth Anything 3 monocular",
            sharedCheckpointGroupId: DepthAnything3MonoLargeSharedCheckpointGroup,
            releasePriority: 80),
        CreateRegistryEntry(
            key: DepthAnything3MonoLarge3dTvKey,
            englishName: DepthAnything3MonoLarge3dTvEnglishName,
            spanishName: DepthAnything3MonoLarge3dTvSpanishName,
            expectedRelativePath: DepthAnything3MonoLargeRelativePath,
            depthModelName: AnyV3Mono01DepthModelName,
            category: "Depth Anything 3 mono large 3D TV scaler",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Depth Anything 3 monocular",
            sharedCheckpointGroupId: DepthAnything3MonoLargeSharedCheckpointGroup,
            releasePriority: 81),
        CreateRegistryEntry(
            key: DepthProKey,
            englishName: DepthProEnglishName,
            spanishName: DepthProSpanishName,
            expectedRelativePath: DepthProRelativePath,
            depthModelName: DepthProDepthModelName,
            category: "Depth Pro",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Depth Pro",
            sharedCheckpointGroupId: DepthProSharedCheckpointGroup,
            depthType: Iw3DepthModelDepthType.ForcedDisparity,
            mediaCapability: Iw3DepthModelMediaCapability.ImageOnly,
            releasePriority: 90,
            isReadySelectable: false),
        CreateRegistryEntry(
            key: DepthProSmallResolutionKey,
            englishName: DepthProSmallResolutionEnglishName,
            spanishName: DepthProSmallResolutionSpanishName,
            expectedRelativePath: DepthProRelativePath,
            depthModelName: DepthProSDepthModelName,
            category: "Depth Pro small resolution",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Depth Pro",
            sharedCheckpointGroupId: DepthProSharedCheckpointGroup,
            depthType: Iw3DepthModelDepthType.ForcedDisparity,
            mediaCapability: Iw3DepthModelMediaCapability.ImageOnly,
            releasePriority: 91,
            isReadySelectable: false),
        CreateRegistryEntry(
            key: VideoDepthAnythingSmallKey,
            englishName: VideoDepthAnythingSmallEnglishName,
            spanishName: VideoDepthAnythingSmallSpanishName,
            expectedRelativePath: VideoDepthAnythingSmallRelativePath,
            depthModelName: VdaSDepthModelName,
            category: "Video Depth Anything small",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Video Depth Anything",
            sharedCheckpointGroupId: VideoDepthAnythingSmallSharedCheckpointGroup,
            mediaCapability: Iw3DepthModelMediaCapability.VideoOnly,
            releasePriority: 40,
            isReadySelectable: false),
        CreateRegistryEntry(
            key: VideoDepthAnythingStreamSmallKey,
            englishName: VideoDepthAnythingStreamSmallEnglishName,
            spanishName: VideoDepthAnythingStreamSmallSpanishName,
            expectedRelativePath: VideoDepthAnythingSmallRelativePath,
            depthModelName: VdaStreamSDepthModelName,
            category: "Video Depth Anything stream small",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Video Depth Anything stream",
            sharedCheckpointGroupId: VideoDepthAnythingSmallSharedCheckpointGroup,
            mediaCapability: Iw3DepthModelMediaCapability.Stream,
            releasePriority: 41,
            isReadySelectable: false),
        CreateRegistryEntry(
            key: MetricVideoDepthAnythingSmallKey,
            englishName: MetricVideoDepthAnythingSmallEnglishName,
            spanishName: MetricVideoDepthAnythingSmallSpanishName,
            expectedRelativePath: MetricVideoDepthAnythingSmallRelativePath,
            depthModelName: VdaMetricSDepthModelName,
            category: "Metric Video Depth Anything small",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Video Depth Anything metric",
            sharedCheckpointGroupId: MetricVideoDepthAnythingSmallSharedCheckpointGroup,
            depthType: Iw3DepthModelDepthType.ForcedDisparity,
            mediaCapability: Iw3DepthModelMediaCapability.VideoOnly,
            releasePriority: 45,
            isReadySelectable: false),
        CreateRegistryEntry(
            key: MetricVideoDepthAnythingStreamSmallKey,
            englishName: MetricVideoDepthAnythingStreamSmallEnglishName,
            spanishName: MetricVideoDepthAnythingStreamSmallSpanishName,
            expectedRelativePath: MetricVideoDepthAnythingSmallRelativePath,
            depthModelName: VdaStreamMetricSDepthModelName,
            category: "Metric Video Depth Anything stream small",
            availability: Iw3DepthModelAvailability.OptionalImportable,
            modelFamily: "Video Depth Anything metric stream",
            sharedCheckpointGroupId: MetricVideoDepthAnythingSmallSharedCheckpointGroup,
            depthType: Iw3DepthModelDepthType.ForcedDisparity,
            mediaCapability: Iw3DepthModelMediaCapability.Stream,
            releasePriority: 46,
            isReadySelectable: false),
    ];

    public static IReadOnlyList<LocalModelSelectionCandidate> CreateSelectableCandidates(
        IEnumerable<LocalModelSelectionCandidate> candidates,
        bool useSpanish)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .SelectMany(candidate => FindMatchingEntries(candidate)
                .Where(entry => entry.IsReadySelectable && entry.IsUserVisibleInSelector)
                .Select(entry => CreateSelectableCandidate(
                    candidate,
                    entry.CreateMapping(NormalizeRelativePath(candidate.RelativePath)),
                    useSpanish)))
            .OrderBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<LocalModelSelectionCandidate> GetUnmappedCandidates(
        IEnumerable<LocalModelSelectionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Where(candidate => !FindMatchingEntries(candidate).Any())
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

        var entries = FindMatchingEntries(candidate)
            .Where(entry => entry.IsReadySelectable && entry.IsUserVisibleInSelector)
            .ToArray();
        var entry = SelectRequestedEntry(
            entries,
            candidate.Iw3DepthModelName,
            candidate.MappingKey);
        if (entry is null)
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

        var entries = FindMatchingEntries(selectedModel)
            .Where(entry => entry.IsReadySelectable && entry.IsUserVisibleInSelector)
            .ToArray();
        var entry = SelectRequestedEntry(
            entries,
            selectedModel.Iw3DepthModelName,
            selectedModel.MappingKey);
        if (entry is null)
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

    private static IEnumerable<Iw3DepthModelRegistryEntry> FindMatchingEntries(
        LocalModelSelectionCandidate candidate) =>
        RegistryEntries.Where(entry => entry.MatchesCandidate(candidate));

    private static IEnumerable<Iw3DepthModelRegistryEntry> FindMatchingEntries(
        LocalModelPlanSelection selectedModel) =>
        RegistryEntries.Where(entry => entry.MatchesPlanSelection(selectedModel));

    private static Iw3DepthModelRegistryEntry? SelectRequestedEntry(
        IReadOnlyList<Iw3DepthModelRegistryEntry> entries,
        string? depthModelName,
        string? mappingKey)
    {
        if (entries.Count == 0)
        {
            return null;
        }

        var hasRequestedDepthModel = !string.IsNullOrWhiteSpace(depthModelName);
        var hasRequestedMappingKey = !string.IsNullOrWhiteSpace(mappingKey);
        if (!hasRequestedDepthModel && !hasRequestedMappingKey)
        {
            return entries[0];
        }

        var matches = entries.AsEnumerable();
        if (hasRequestedMappingKey)
        {
            matches = matches.Where(entry => string.Equals(
                entry.Key,
                mappingKey,
                StringComparison.OrdinalIgnoreCase));
        }

        if (hasRequestedDepthModel)
        {
            matches = matches.Where(entry => string.Equals(
                entry.DepthModelName,
                depthModelName,
                StringComparison.OrdinalIgnoreCase));
        }

        return matches.FirstOrDefault();
    }

    private static LocalModelSelectionCandidate CreateSelectableCandidate(
        LocalModelSelectionCandidate candidate,
        Iw3DepthModelMapping mapping,
        bool useSpanish)
    {
        var displayName = GetUserVisibleName(mapping, useSpanish);
        var spanishName = GetUserVisibleName(mapping, useSpanish: true);

        return candidate with
        {
            Id = mapping.Key,
            DisplayName = displayName,
            SpanishDisplayName = spanishName,
            Iw3DepthModelName = mapping.DepthModelName,
            MappingKey = mapping.Key,
            EnglishStatusNote = mapping.EnglishStatusNote,
            SpanishStatusNote = mapping.SpanishStatusNote,
        };
    }

    private static string GetUserVisibleName(
        Iw3DepthModelMapping mapping,
        bool useSpanish)
    {
        var name = useSpanish
            ? mapping.SpanishName
            : mapping.EnglishName;
        if (mapping.RedistributionDecision !=
            Iw3DepthModelRedistributionDecision.BlockedUnclearLicense)
        {
            return name;
        }

        return useSpanish
            ? $"{name} (proporcionado por el usuario)"
            : $"{name} (user-provided)";
    }

    private static Iw3DepthModelRegistryEntry CreateRegistryEntry(
        string key,
        string englishName,
        string spanishName,
        string expectedRelativePath,
        string depthModelName,
        string category,
        Iw3DepthModelAvailability availability,
        string modelFamily = "",
        Iw3DepthModelRedistributionDecision redistributionDecision =
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
        string sharedCheckpointGroupId = "",
        Iw3DepthModelDepthType depthType = Iw3DepthModelDepthType.Relative,
        Iw3DepthModelMediaCapability mediaCapability =
            Iw3DepthModelMediaCapability.ImageAndVideo,
        int releasePriority = 100,
        bool isReadySelectable = true,
        bool isUserVisibleInSelector = true,
        IReadOnlyList<string>? additionalCatalogIdentifiers = null)
    {
        var normalizedPath = NormalizeRelativePath(expectedRelativePath);
        var statusNotes = GetStatusNotes(
            availability,
            redistributionDecision,
            isReadySelectable);
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
            ModelFamily: string.IsNullOrWhiteSpace(modelFamily) ? category : modelFamily,
            Availability: availability,
            RedistributionDecision: redistributionDecision,
            SharedCheckpointGroupId: sharedCheckpointGroupId,
            DepthType: depthType,
            MediaCapability: mediaCapability,
            ReleasePriority: releasePriority,
            IsUserVisibleInSelector: isReadySelectable && isUserVisibleInSelector,
            ExpectedRelativePaths: [normalizedPath],
            ExpectedFileNames: [GetFileName(normalizedPath)],
            CatalogIdentifiers: identifiers
                .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            EnglishStatusNote: statusNotes.English,
            SpanishStatusNote: statusNotes.Spanish,
            IsReadySelectable: isReadySelectable,
            RequiresLocalFile: true);
    }

    private static (string English, string Spanish) GetStatusNotes(
        Iw3DepthModelAvailability availability,
        Iw3DepthModelRedistributionDecision redistributionDecision,
        bool isReadySelectable)
    {
        if (!isReadySelectable)
        {
            return (
                "Known iw3 model, but not selectable until v3dfy verifies this provider for the current preview and conversion flow.",
                "Modelo iw3 conocido, pero no seleccionable hasta que v3dfy verifique este proveedor para la vista previa y conversion actuales.");
        }

        if (redistributionDecision == Iw3DepthModelRedistributionDecision.BlockedUnclearLicense)
        {
            return (
                "Selectable when this user-provided checkpoint exists, but not eligible for public v3dfy model packs because weight redistribution is not cleared.",
                "Seleccionable cuando existe este checkpoint proporcionado por el usuario, pero no elegible para paquetes publicos de v3dfy porque la redistribucion de pesos no esta aclarada.");
        }

        return availability == Iw3DepthModelAvailability.EmbeddedBase
            ? (
                "Ready when the verified local checkpoint exists in the bundled iw3 pretrained_models folder.",
                "Listo cuando el checkpoint local verificado existe en la carpeta pretrained_models incluida de iw3.")
            : (
                "Selectable when this optional checkpoint exists in the iw3 pretrained_models folder; install it with a v3dfy model pack.",
                "Seleccionable cuando este checkpoint opcional existe en la carpeta pretrained_models de iw3; instalelo con un paquete de modelos de v3dfy.");
    }

    private static string GetFileName(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        return normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? string.Empty;
    }
}
