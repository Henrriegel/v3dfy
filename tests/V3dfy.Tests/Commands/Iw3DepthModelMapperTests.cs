using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Engine.Iw3.Commands;

namespace V3dfy.Tests.Commands;

public sealed class Iw3DepthModelMapperTests
{
    private static readonly KnownModelDefinition[] KnownModels =
    [
        new(
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey,
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorEnglishName,
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
            Iw3DepthModelMapper.ZoeDAnyNDepthModelName,
            Iw3DepthModelAvailability.EmbeddedBase,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Metric,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorKey,
            Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorEnglishName,
            Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorRelativePath,
            Iw3DepthModelMapper.ZoeDAnyKDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Metric,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.ZoeDepthIndoorKey,
            Iw3DepthModelMapper.ZoeDepthIndoorEnglishName,
            Iw3DepthModelMapper.ZoeDepthIndoorRelativePath,
            Iw3DepthModelMapper.ZoeDIndoorDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.BlockedUnclearLicense,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Metric,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.ZoeDepthOutdoorKey,
            Iw3DepthModelMapper.ZoeDepthOutdoorEnglishName,
            Iw3DepthModelMapper.ZoeDepthOutdoorRelativePath,
            Iw3DepthModelMapper.ZoeDOutdoorDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.BlockedUnclearLicense,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Metric,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.ZoeDepthIndoorOutdoorKey,
            Iw3DepthModelMapper.ZoeDepthIndoorOutdoorEnglishName,
            Iw3DepthModelMapper.ZoeDepthIndoorOutdoorRelativePath,
            Iw3DepthModelMapper.ZoeDIndoorOutdoorDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.BlockedUnclearLicense,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Metric,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.DepthAnythingSmallKey,
            Iw3DepthModelMapper.DepthAnythingSmallEnglishName,
            Iw3DepthModelMapper.DepthAnythingSmallRelativePath,
            Iw3DepthModelMapper.AnySDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Relative,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.DepthAnythingBaseKey,
            Iw3DepthModelMapper.DepthAnythingBaseEnglishName,
            Iw3DepthModelMapper.DepthAnythingBaseRelativePath,
            Iw3DepthModelMapper.AnyBDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Relative,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.DepthAnythingLargeKey,
            Iw3DepthModelMapper.DepthAnythingLargeEnglishName,
            Iw3DepthModelMapper.DepthAnythingLargeRelativePath,
            Iw3DepthModelMapper.AnyLDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Relative,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.DepthAnythingV2SmallKey,
            Iw3DepthModelMapper.DepthAnythingV2SmallEnglishName,
            Iw3DepthModelMapper.DepthAnythingV2SmallRelativePath,
            Iw3DepthModelMapper.AnyV2SDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Relative,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimSmallKey,
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimSmallEnglishName,
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimSmallRelativePath,
            Iw3DepthModelMapper.AnyV2NSDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Metric,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimBaseKey,
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimBaseEnglishName,
            Iw3DepthModelMapper.DepthAnythingV2MetricHypersimBaseRelativePath,
            Iw3DepthModelMapper.AnyV2NBDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Metric,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiSmallKey,
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiSmallEnglishName,
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiSmallRelativePath,
            Iw3DepthModelMapper.AnyV2KSDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Metric,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiBaseKey,
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiBaseEnglishName,
            Iw3DepthModelMapper.DepthAnythingV2MetricVkittiBaseRelativePath,
            Iw3DepthModelMapper.AnyV2KBDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Metric,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.DistillAnyDepthSmallKey,
            Iw3DepthModelMapper.DistillAnyDepthSmallEnglishName,
            Iw3DepthModelMapper.DistillAnyDepthSmallRelativePath,
            Iw3DepthModelMapper.DistillAnySDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Unknown,
            Iw3DepthModelMediaCapability.ImageAndVideo),
        new(
            Iw3DepthModelMapper.DepthAnything3MonoLargeKey,
            Iw3DepthModelMapper.DepthAnything3MonoLargeEnglishName,
            Iw3DepthModelMapper.DepthAnything3MonoLargeRelativePath,
            Iw3DepthModelMapper.AnyV3MonoDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Relative,
            Iw3DepthModelMediaCapability.ImageAndVideo,
            Iw3DepthModelMapper.DepthAnything3MonoLargeSharedCheckpointGroup),
        new(
            Iw3DepthModelMapper.DepthAnything3MonoLarge3dTvKey,
            Iw3DepthModelMapper.DepthAnything3MonoLarge3dTvEnglishName,
            Iw3DepthModelMapper.DepthAnything3MonoLargeRelativePath,
            Iw3DepthModelMapper.AnyV3Mono01DepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: true,
            Iw3DepthModelDepthType.Relative,
            Iw3DepthModelMediaCapability.ImageAndVideo,
            Iw3DepthModelMapper.DepthAnything3MonoLargeSharedCheckpointGroup),
        new(
            Iw3DepthModelMapper.DepthProKey,
            Iw3DepthModelMapper.DepthProEnglishName,
            Iw3DepthModelMapper.DepthProRelativePath,
            Iw3DepthModelMapper.DepthProDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: false,
            Iw3DepthModelDepthType.ForcedDisparity,
            Iw3DepthModelMediaCapability.ImageOnly,
            Iw3DepthModelMapper.DepthProSharedCheckpointGroup),
        new(
            Iw3DepthModelMapper.DepthProSmallResolutionKey,
            Iw3DepthModelMapper.DepthProSmallResolutionEnglishName,
            Iw3DepthModelMapper.DepthProRelativePath,
            Iw3DepthModelMapper.DepthProSDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: false,
            Iw3DepthModelDepthType.ForcedDisparity,
            Iw3DepthModelMediaCapability.ImageOnly,
            Iw3DepthModelMapper.DepthProSharedCheckpointGroup),
        new(
            Iw3DepthModelMapper.VideoDepthAnythingSmallKey,
            Iw3DepthModelMapper.VideoDepthAnythingSmallEnglishName,
            Iw3DepthModelMapper.VideoDepthAnythingSmallRelativePath,
            Iw3DepthModelMapper.VdaSDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: false,
            Iw3DepthModelDepthType.Relative,
            Iw3DepthModelMediaCapability.VideoOnly,
            Iw3DepthModelMapper.VideoDepthAnythingSmallSharedCheckpointGroup),
        new(
            Iw3DepthModelMapper.VideoDepthAnythingStreamSmallKey,
            Iw3DepthModelMapper.VideoDepthAnythingStreamSmallEnglishName,
            Iw3DepthModelMapper.VideoDepthAnythingSmallRelativePath,
            Iw3DepthModelMapper.VdaStreamSDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: false,
            Iw3DepthModelDepthType.Relative,
            Iw3DepthModelMediaCapability.Stream,
            Iw3DepthModelMapper.VideoDepthAnythingSmallSharedCheckpointGroup),
        new(
            Iw3DepthModelMapper.MetricVideoDepthAnythingSmallKey,
            Iw3DepthModelMapper.MetricVideoDepthAnythingSmallEnglishName,
            Iw3DepthModelMapper.MetricVideoDepthAnythingSmallRelativePath,
            Iw3DepthModelMapper.VdaMetricSDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: false,
            Iw3DepthModelDepthType.ForcedDisparity,
            Iw3DepthModelMediaCapability.VideoOnly,
            Iw3DepthModelMapper.MetricVideoDepthAnythingSmallSharedCheckpointGroup),
        new(
            Iw3DepthModelMapper.MetricVideoDepthAnythingStreamSmallKey,
            Iw3DepthModelMapper.MetricVideoDepthAnythingStreamSmallEnglishName,
            Iw3DepthModelMapper.MetricVideoDepthAnythingSmallRelativePath,
            Iw3DepthModelMapper.VdaStreamMetricSDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable,
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            IsReadySelectable: false,
            Iw3DepthModelDepthType.ForcedDisparity,
            Iw3DepthModelMediaCapability.Stream,
            Iw3DepthModelMapper.MetricVideoDepthAnythingSmallSharedCheckpointGroup),
    ];

    private static readonly string[] ExcludedNonCommercialDepthModelNames =
    [
        "Any_V2_B",
        "Any_V2_L",
        "Any_V2_N",
        "Any_V2_K",
        "Any_V2_N_L",
        "Any_V2_K_L",
        "Distill_Any_B",
        "Distill_Any_L",
        "VDA_B",
        "VDA_L",
        "VDA_Metric",
        "VDA_Metric_B",
        "VDA_Metric_L",
        "VDA_Stream_B",
        "VDA_Stream_L",
        "VDA_Stream_Metric_B",
        "VDA_Stream_Metric_L",
    ];

    public static TheoryData<
        string,
        string,
        string,
        string,
        Iw3DepthModelAvailability,
        Iw3DepthModelRedistributionDecision,
        bool,
        Iw3DepthModelDepthType,
        Iw3DepthModelMediaCapability> KnownModelData
    {
        get
        {
            var data = new TheoryData<
                string,
                string,
                string,
                string,
                Iw3DepthModelAvailability,
                Iw3DepthModelRedistributionDecision,
                bool,
                Iw3DepthModelDepthType,
                Iw3DepthModelMediaCapability>();
            foreach (var model in KnownModels)
            {
                data.Add(
                    model.Key,
                    model.EnglishName,
                    model.ExpectedRelativePath,
                    model.DepthModelName,
                    model.Availability,
                    model.RedistributionDecision,
                    model.IsReadySelectable,
                    model.DepthType,
                    model.MediaCapability);
            }

            return data;
        }
    }

    public static TheoryData<string, string, string, Iw3DepthModelAvailability> ReadyKnownModelData
    {
        get
        {
            var data = new TheoryData<string, string, string, Iw3DepthModelAvailability>();
            foreach (var model in KnownModels.Where(static model => model.IsReadySelectable))
            {
                data.Add(
                    model.Key,
                    model.ExpectedRelativePath,
                    model.DepthModelName,
                    model.Availability);
            }

            return data;
        }
    }

    [Fact]
    public void RegistryEntries_ContainAllMappedSafeAndBlockedIw3DepthModels()
    {
        Assert.Equal(22, Iw3DepthModelMapper.RegistryEntries.Count);
        Assert.Equal(
            KnownModels.Select(model => model.Key).Order(StringComparer.OrdinalIgnoreCase),
            Iw3DepthModelMapper.RegistryEntries
                .Select(entry => entry.Key)
                .Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void RegistryEntries_ContainExpectedSafeWithNoticeCounts()
    {
        var safeEntries = Iw3DepthModelMapper.RegistryEntries
            .Where(static entry =>
                entry.RedistributionDecision ==
                Iw3DepthModelRedistributionDecision.SafeWithNotice)
            .ToArray();

        Assert.Equal(19, safeEntries.Length);
        Assert.Equal(15, safeEntries
            .SelectMany(entry => entry.ExpectedRelativePaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count());
        Assert.All(safeEntries, entry => Assert.True(entry.IsPublicPackEligible));
    }

    [Theory]
    [MemberData(nameof(KnownModelData))]
    public void RegistryEntries_MapKnownModelIdsToExpectedPathsIw3ValuesAndMetadata(
        string key,
        string englishName,
        string expectedRelativePath,
        string depthModelName,
        Iw3DepthModelAvailability availability,
        Iw3DepthModelRedistributionDecision redistributionDecision,
        bool isReadySelectable,
        Iw3DepthModelDepthType depthType,
        Iw3DepthModelMediaCapability mediaCapability)
    {
        var entry = Assert.Single(
            Iw3DepthModelMapper.RegistryEntries,
            entry => entry.Key == key);

        Assert.Equal(englishName, entry.EnglishName);
        Assert.Equal(depthModelName, entry.DepthModelName);
        Assert.Equal(availability, entry.Availability);
        Assert.Equal(redistributionDecision, entry.RedistributionDecision);
        Assert.Equal(isReadySelectable, entry.IsReadySelectable);
        Assert.Equal(depthType, entry.DepthType);
        Assert.Equal(mediaCapability, entry.MediaCapability);
        Assert.Equal(isReadySelectable, entry.IsUserVisibleInSelector);
        Assert.Equal(
            redistributionDecision ==
            Iw3DepthModelRedistributionDecision.SafeWithNotice,
            entry.IsPublicPackEligible);
        Assert.True(entry.RequiresLocalFile);
        Assert.Contains(expectedRelativePath, entry.ExpectedRelativePaths);
        Assert.Contains(FileName(expectedRelativePath), entry.ExpectedFileNames);
        Assert.Contains(key, entry.CatalogIdentifiers);
        Assert.Contains(depthModelName, entry.CatalogIdentifiers);
    }

    [Theory]
    [MemberData(nameof(ReadyKnownModelData))]
    public void TryMap_ReadyKnownLocalModelMapsToVerifiedIw3DepthModelValue(
        string key,
        string expectedRelativePath,
        string depthModelName,
        Iw3DepthModelAvailability availability)
    {
        var selectedModel = new LocalModelPlanSelection(
            FileName(expectedRelativePath),
            expectedRelativePath,
            LocalModelPlanSource.UnmanagedLocalFile,
            Iw3DepthModelName: depthModelName,
            MappingKey: key);

        var mapped = Iw3DepthModelMapper.TryMap(selectedModel, out var mapping);

        Assert.True(mapped);
        Assert.NotNull(mapping);
        Assert.Equal(key, mapping.Key);
        Assert.Equal(depthModelName, mapping.DepthModelName);
        Assert.Equal(expectedRelativePath, mapping.ModelRelativePath);
        Assert.Equal(availability, mapping.Availability);
    }

    [Fact]
    public void CreateSelectableCandidates_UnknownCompatibleModelIsNotSelectable()
    {
        var candidates = Iw3DepthModelMapper.CreateSelectableCandidates(
            [UnknownCandidate()],
            useSpanish: false);

        Assert.Empty(candidates);
        var unmapped = Assert.Single(Iw3DepthModelMapper.GetUnmappedCandidates([UnknownCandidate()]));
        Assert.Equal("unknown-depth.pt", unmapped.RelativePath);
    }

    [Fact]
    public void CreateSelectableCandidates_ImportedMappedModelPackModelBecomesSelectable()
    {
        var candidate = Assert.Single(Iw3DepthModelMapper.CreateSelectableCandidates(
            [ImportedOutdoorModelPackCandidate()],
            useSpanish: false));

        Assert.Equal(Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorKey, candidate.Id);
        Assert.Equal(Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorKey, candidate.MappingKey);
        Assert.Equal(Iw3DepthModelMapper.ZoeDAnyKDepthModelName, candidate.Iw3DepthModelName);
        Assert.Equal("hub/checkpoints/depth_anything_metric_depth_outdoor.pt", candidate.RelativePath);
        Assert.Empty(Iw3DepthModelMapper.GetUnmappedCandidates([ImportedOutdoorModelPackCandidate()]));
    }

    [Fact]
    public void CreateSelectableCandidates_WithAllReadyKnownFiles_AllReadyEntriesAreSelectable()
    {
        var candidates = Iw3DepthModelMapper.CreateSelectableCandidates(
            KnownModels
                .Where(static model => model.IsReadySelectable)
                .Select(static model => model.ExpectedRelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(relativePath => Candidate(
                    id: relativePath,
                    displayName: FileName(relativePath),
                    relativePath: relativePath,
                    isCatalogManaged: false)),
            useSpanish: false);
        var expectedReadyModels = KnownModels
            .Where(static model => model.IsReadySelectable)
            .ToArray();

        Assert.Equal(16, candidates.Count);
        Assert.Equal(
            expectedReadyModels.Select(model => model.Key).Order(StringComparer.OrdinalIgnoreCase),
            candidates.Select(candidate => candidate.MappingKey).Order(StringComparer.OrdinalIgnoreCase));
        Assert.Equal(
            expectedReadyModels.Select(model => model.DepthModelName).Order(StringComparer.OrdinalIgnoreCase),
            candidates.Select(candidate => candidate.Iw3DepthModelName).Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateSelectableCandidates_WithAllSafeWithNoticeFiles_OnlyOperationReadyEntriesAreSelectable()
    {
        var safeEntries = Iw3DepthModelMapper.RegistryEntries
            .Where(static entry =>
                entry.RedistributionDecision ==
                Iw3DepthModelRedistributionDecision.SafeWithNotice)
            .ToArray();
        var candidates = Iw3DepthModelMapper.CreateSelectableCandidates(
            safeEntries
                .SelectMany(entry => entry.ExpectedRelativePaths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(relativePath => Candidate(
                    id: relativePath,
                    displayName: FileName(relativePath),
                    relativePath: relativePath,
                    isCatalogManaged: false)),
            useSpanish: false);

        Assert.Equal(13, candidates.Count);
        Assert.Equal(
            safeEntries
                .Where(static entry => entry.IsReadySelectable)
                .Select(entry => entry.Key)
                .Order(StringComparer.OrdinalIgnoreCase),
            candidates
                .Select(candidate => candidate.MappingKey)
                .Order(StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain(candidates, candidate =>
            candidate.MappingKey is Iw3DepthModelMapper.DepthProKey or
                Iw3DepthModelMapper.VideoDepthAnythingSmallKey);
    }

    [Fact]
    public void CreateSelectableCandidates_SharedReadyCheckpointCreatesDistinctSelectableVariants()
    {
        var candidates = Iw3DepthModelMapper.CreateSelectableCandidates(
            [Candidate(
                id: Iw3DepthModelMapper.DepthAnything3MonoLargeRelativePath,
                displayName: "da3mono-large.safetensors",
                relativePath: Iw3DepthModelMapper.DepthAnything3MonoLargeRelativePath,
                isCatalogManaged: false)],
            useSpanish: false);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, candidate =>
            candidate.MappingKey == Iw3DepthModelMapper.DepthAnything3MonoLargeKey &&
            candidate.Iw3DepthModelName == Iw3DepthModelMapper.AnyV3MonoDepthModelName);
        Assert.Contains(candidates, candidate =>
            candidate.MappingKey == Iw3DepthModelMapper.DepthAnything3MonoLarge3dTvKey &&
            candidate.Iw3DepthModelName == Iw3DepthModelMapper.AnyV3Mono01DepthModelName);
    }

    [Fact]
    public void TryMap_SharedCheckpointPlanSelectionUsesRequestedVariant()
    {
        var selectedModel = new LocalModelPlanSelection(
            Iw3DepthModelMapper.DepthAnything3MonoLarge3dTvEnglishName,
            Iw3DepthModelMapper.DepthAnything3MonoLargeRelativePath,
            LocalModelPlanSource.UnmanagedLocalFile,
            Iw3DepthModelName: Iw3DepthModelMapper.AnyV3Mono01DepthModelName,
            MappingKey: Iw3DepthModelMapper.DepthAnything3MonoLarge3dTvKey);

        var mapped = Iw3DepthModelMapper.TryMap(selectedModel, out var mapping);

        Assert.True(mapped);
        Assert.NotNull(mapping);
        Assert.Equal(Iw3DepthModelMapper.DepthAnything3MonoLarge3dTvKey, mapping.Key);
        Assert.Equal(Iw3DepthModelMapper.AnyV3Mono01DepthModelName, mapping.DepthModelName);
    }

    [Fact]
    public void SharedCheckpointGroups_AreRepresentedOncePerShortName()
    {
        AssertSharedGroup(
            Iw3DepthModelMapper.DepthAnything3MonoLargeSharedCheckpointGroup,
            Iw3DepthModelMapper.DepthAnything3MonoLargeRelativePath,
            Iw3DepthModelMapper.DepthAnything3MonoLargeKey,
            Iw3DepthModelMapper.DepthAnything3MonoLarge3dTvKey);
        AssertSharedGroup(
            Iw3DepthModelMapper.DepthProSharedCheckpointGroup,
            Iw3DepthModelMapper.DepthProRelativePath,
            Iw3DepthModelMapper.DepthProKey,
            Iw3DepthModelMapper.DepthProSmallResolutionKey);
        AssertSharedGroup(
            Iw3DepthModelMapper.VideoDepthAnythingSmallSharedCheckpointGroup,
            Iw3DepthModelMapper.VideoDepthAnythingSmallRelativePath,
            Iw3DepthModelMapper.VideoDepthAnythingSmallKey,
            Iw3DepthModelMapper.VideoDepthAnythingStreamSmallKey);
        AssertSharedGroup(
            Iw3DepthModelMapper.MetricVideoDepthAnythingSmallSharedCheckpointGroup,
            Iw3DepthModelMapper.MetricVideoDepthAnythingSmallRelativePath,
            Iw3DepthModelMapper.MetricVideoDepthAnythingSmallKey,
            Iw3DepthModelMapper.MetricVideoDepthAnythingStreamSmallKey);
    }

    [Fact]
    public void GatedImageOnlyAndVideoOnlyProvidersAreKnownButNotReadySelectable()
    {
        var gatedEntries = Iw3DepthModelMapper.RegistryEntries
            .Where(static entry => entry.MediaCapability is
                Iw3DepthModelMediaCapability.ImageOnly or
                Iw3DepthModelMediaCapability.VideoOnly or
                Iw3DepthModelMediaCapability.Stream)
            .ToArray();

        Assert.Equal(6, gatedEntries.Length);
        Assert.All(gatedEntries, entry =>
        {
            Assert.True(entry.IsPublicPackEligible);
            Assert.False(entry.IsReadySelectable);
            Assert.False(entry.IsUserVisibleInSelector);
        });

        var depthProCandidate = Candidate(
            id: Iw3DepthModelMapper.DepthProRelativePath,
            displayName: "depth_pro.pt",
            relativePath: Iw3DepthModelMapper.DepthProRelativePath,
            isCatalogManaged: false);
        Assert.Empty(Iw3DepthModelMapper.CreateSelectableCandidates([depthProCandidate], useSpanish: false));
        Assert.Empty(Iw3DepthModelMapper.GetUnmappedCandidates([depthProCandidate]));
    }

    [Fact]
    public void CreateSelectableCandidates_BlockedZoeDepthModelIsLabeledUserProvided()
    {
        var candidate = Assert.Single(Iw3DepthModelMapper.CreateSelectableCandidates(
            [Candidate(
                id: Iw3DepthModelMapper.ZoeDepthIndoorKey,
                displayName: "ZoeDepth Indoor",
                relativePath: Iw3DepthModelMapper.ZoeDepthIndoorRelativePath,
                isCatalogManaged: false)],
            useSpanish: false));

        Assert.Equal(Iw3DepthModelMapper.ZoeDepthIndoorKey, candidate.MappingKey);
        Assert.Equal(Iw3DepthModelMapper.ZoeDIndoorDepthModelName, candidate.Iw3DepthModelName);
        Assert.Contains("user-provided", candidate.DisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not eligible for public v3dfy model packs", candidate.EnglishStatusNote);
    }

    [Fact]
    public void GatedProviderPlanSelectionsAreRejectedByTryMap()
    {
        var selectedModel = new LocalModelPlanSelection(
            Iw3DepthModelMapper.DepthProEnglishName,
            Iw3DepthModelMapper.DepthProRelativePath,
            LocalModelPlanSource.UnmanagedLocalFile,
            Iw3DepthModelName: Iw3DepthModelMapper.DepthProDepthModelName,
            MappingKey: Iw3DepthModelMapper.DepthProKey);

        Assert.False(Iw3DepthModelMapper.TryMap(selectedModel, out _));
    }

    [Fact]
    public void ExcludedNonCommercialShortNames_AreNotPublicPackEligibleMappings()
    {
        foreach (var depthModelName in ExcludedNonCommercialDepthModelNames)
        {
            Assert.DoesNotContain(
                Iw3DepthModelMapper.RegistryEntries,
                entry => entry.DepthModelName == depthModelName && entry.IsPublicPackEligible);
        }
    }

    [Fact]
    public void ZoeDepthBlockedEntries_AreNotPublicPackEligible()
    {
        var blocked = Iw3DepthModelMapper.RegistryEntries
            .Where(static entry =>
                entry.RedistributionDecision ==
                Iw3DepthModelRedistributionDecision.BlockedUnclearLicense)
            .ToArray();

        Assert.Equal(3, blocked.Length);
        Assert.Equal(
            new[]
            {
                Iw3DepthModelMapper.ZoeDIndoorDepthModelName,
                Iw3DepthModelMapper.ZoeDOutdoorDepthModelName,
                Iw3DepthModelMapper.ZoeDIndoorOutdoorDepthModelName,
            }.Order(StringComparer.OrdinalIgnoreCase),
            blocked.Select(entry => entry.DepthModelName).Order(StringComparer.OrdinalIgnoreCase));
        Assert.All(blocked, entry =>
        {
            Assert.False(entry.IsPublicPackEligible);
            Assert.True(entry.IsReadySelectable);
        });
    }

    [Fact]
    public void CreateSelectableCandidates_KnownModelUsesFriendlyNameAndMappingMetadata()
    {
        var candidate = Assert.Single(Iw3DepthModelMapper.CreateSelectableCandidates(
            [KnownCandidate()],
            useSpanish: false));

        Assert.Equal("Depth Anything Metric Indoor", candidate.DisplayName);
        Assert.Equal(Iw3DepthModelMapper.ZoeDAnyNDepthModelName, candidate.Iw3DepthModelName);
        Assert.Equal(Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey, candidate.MappingKey);
        Assert.Equal(Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey, candidate.Id);
        Assert.Contains("verified local checkpoint", candidate.EnglishStatusNote);
    }

    [Fact]
    public void CreateSelectableCandidates_SpanishDisplayUsesFriendlySpanishName()
    {
        var candidate = Assert.Single(Iw3DepthModelMapper.CreateSelectableCandidates(
            [KnownCandidate()],
            useSpanish: true));

        Assert.Equal("Depth Anything Metric Indoor", candidate.DisplayName);
        Assert.Equal("Depth Anything Metric Indoor", candidate.SpanishDisplayName);
    }

    [Fact]
    public void TryMap_PlanSelectionWithMismatchedDepthModelNameIsRejected()
    {
        var selectedModel = new LocalModelPlanSelection(
            "Depth Anything Metric Indoor",
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
            LocalModelPlanSource.UnmanagedLocalFile,
            Iw3DepthModelName: "UnverifiedValue");

        Assert.False(Iw3DepthModelMapper.TryMap(selectedModel, out _));
    }

    [Fact]
    public void ProtectedIw3RuntimeDependencyIsNotSelectable()
    {
        var runtimeDependency = Candidate(
            id: "hub/checkpoints/iw3_row_flow_v3_20250627.pth",
            displayName: "iw3_row_flow_v3_20250627.pth",
            relativePath: "hub/checkpoints/iw3_row_flow_v3_20250627.pth",
            isCatalogManaged: false);

        Assert.Empty(Iw3DepthModelMapper.CreateSelectableCandidates([runtimeDependency], useSpanish: false));
        Assert.False(Iw3DepthModelMapper.TryMap(
            LocalModelPlanSelection.FromCandidate(runtimeDependency),
            out _));
    }

    private static void AssertSharedGroup(
        string sharedGroupId,
        string expectedRelativePath,
        params string[] expectedKeys)
    {
        var entries = Iw3DepthModelMapper.RegistryEntries
            .Where(entry => entry.SharedCheckpointGroupId == sharedGroupId)
            .ToArray();

        Assert.Equal(expectedKeys.Order(StringComparer.OrdinalIgnoreCase),
            entries.Select(entry => entry.Key).Order(StringComparer.OrdinalIgnoreCase));
        Assert.All(entries, entry =>
        {
            Assert.True(entry.HasSharedCheckpointGroup);
            Assert.Equal(expectedRelativePath, Assert.Single(entry.ExpectedRelativePaths));
        });
    }

    private static LocalModelSelectionCandidate KnownCandidate() => new(
        Id: Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
        DisplayName: "depth_anything_metric_depth_indoor.pt",
        RelativePath: Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
        FileName: "depth_anything_metric_depth_indoor.pt",
        Extension: ".pt",
        ModelType: string.Empty,
        Purpose: string.Empty,
        IsCatalogManaged: false);

    private static LocalModelSelectionCandidate UnknownCandidate() => new(
        Id: "unknown-depth.pt",
        DisplayName: "unknown-depth.pt",
        RelativePath: "unknown-depth.pt",
        FileName: "unknown-depth.pt",
        Extension: ".pt",
        ModelType: string.Empty,
        Purpose: string.Empty,
        IsCatalogManaged: false);

    private static LocalModelSelectionCandidate ImportedOutdoorModelPackCandidate() => new(
        Id: Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorKey,
        DisplayName: "Depth Anything Metric Outdoor",
        RelativePath: Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorRelativePath,
        FileName: "depth_anything_metric_depth_outdoor.pt",
        Extension: ".pt",
        ModelType: "depth",
        Purpose: "outdoor",
        IsCatalogManaged: true);

    private static LocalModelSelectionCandidate Candidate(
        string id,
        string displayName,
        string relativePath,
        bool isCatalogManaged) => new(
        Id: id,
        DisplayName: displayName,
        RelativePath: relativePath,
        FileName: FileName(relativePath),
        Extension: Path.GetExtension(FileName(relativePath)),
        ModelType: isCatalogManaged ? "depth" : string.Empty,
        Purpose: isCatalogManaged ? "2D to 3D depth generation" : string.Empty,
        IsCatalogManaged: isCatalogManaged);

    private static string FileName(string relativePath) =>
        relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Last();

    private sealed record KnownModelDefinition(
        string Key,
        string EnglishName,
        string ExpectedRelativePath,
        string DepthModelName,
        Iw3DepthModelAvailability Availability,
        Iw3DepthModelRedistributionDecision RedistributionDecision,
        bool IsReadySelectable,
        Iw3DepthModelDepthType DepthType,
        Iw3DepthModelMediaCapability MediaCapability,
        string SharedCheckpointGroupId = "");
}
