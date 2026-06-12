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
            Iw3DepthModelAvailability.EmbeddedBase),
        new(
            Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorKey,
            Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorEnglishName,
            Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorRelativePath,
            Iw3DepthModelMapper.ZoeDAnyKDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable),
        new(
            Iw3DepthModelMapper.ZoeDepthIndoorKey,
            Iw3DepthModelMapper.ZoeDepthIndoorEnglishName,
            Iw3DepthModelMapper.ZoeDepthIndoorRelativePath,
            Iw3DepthModelMapper.ZoeDIndoorDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable),
        new(
            Iw3DepthModelMapper.ZoeDepthOutdoorKey,
            Iw3DepthModelMapper.ZoeDepthOutdoorEnglishName,
            Iw3DepthModelMapper.ZoeDepthOutdoorRelativePath,
            Iw3DepthModelMapper.ZoeDOutdoorDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable),
        new(
            Iw3DepthModelMapper.ZoeDepthIndoorOutdoorKey,
            Iw3DepthModelMapper.ZoeDepthIndoorOutdoorEnglishName,
            Iw3DepthModelMapper.ZoeDepthIndoorOutdoorRelativePath,
            Iw3DepthModelMapper.ZoeDIndoorOutdoorDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable),
        new(
            Iw3DepthModelMapper.DepthAnythingSmallKey,
            Iw3DepthModelMapper.DepthAnythingSmallEnglishName,
            Iw3DepthModelMapper.DepthAnythingSmallRelativePath,
            Iw3DepthModelMapper.AnySDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable),
        new(
            Iw3DepthModelMapper.DepthAnythingBaseKey,
            Iw3DepthModelMapper.DepthAnythingBaseEnglishName,
            Iw3DepthModelMapper.DepthAnythingBaseRelativePath,
            Iw3DepthModelMapper.AnyBDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable),
        new(
            Iw3DepthModelMapper.DepthAnythingV2SmallKey,
            Iw3DepthModelMapper.DepthAnythingV2SmallEnglishName,
            Iw3DepthModelMapper.DepthAnythingV2SmallRelativePath,
            Iw3DepthModelMapper.AnyV2SDepthModelName,
            Iw3DepthModelAvailability.OptionalImportable),
    ];

    public static TheoryData<string, string, string, string, Iw3DepthModelAvailability> KnownModelData
    {
        get
        {
            var data = new TheoryData<string, string, string, string, Iw3DepthModelAvailability>();
            foreach (var model in KnownModels)
            {
                data.Add(
                    model.Key,
                    model.EnglishName,
                    model.ExpectedRelativePath,
                    model.DepthModelName,
                    model.Availability);
            }

            return data;
        }
    }

    [Fact]
    public void RegistryEntries_ContainTheFirstEightSafeIw3DepthModels()
    {
        Assert.Equal(8, Iw3DepthModelMapper.RegistryEntries.Count);
        Assert.Equal(
            KnownModels.Select(model => model.Key).Order(StringComparer.OrdinalIgnoreCase),
            Iw3DepthModelMapper.RegistryEntries
                .Select(entry => entry.Key)
                .Order(StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(KnownModelData))]
    public void RegistryEntries_MapKnownModelIdsToExpectedPathsAndIw3Values(
        string key,
        string englishName,
        string expectedRelativePath,
        string depthModelName,
        Iw3DepthModelAvailability availability)
    {
        var entry = Assert.Single(
            Iw3DepthModelMapper.RegistryEntries,
            entry => entry.Key == key);

        Assert.Equal(englishName, entry.EnglishName);
        Assert.Equal(depthModelName, entry.DepthModelName);
        Assert.Equal(availability, entry.Availability);
        Assert.True(entry.RequiresLocalFile);
        Assert.True(entry.IsReadySelectable);
        Assert.Contains(expectedRelativePath, entry.ExpectedRelativePaths);
        Assert.Contains(FileName(expectedRelativePath), entry.ExpectedFileNames);
        Assert.Contains(key, entry.CatalogIdentifiers);
        Assert.Contains(depthModelName, entry.CatalogIdentifiers);
    }

    [Theory]
    [MemberData(nameof(KnownModelData))]
    public void TryMap_KnownLocalModelMapsToVerifiedIw3DepthModelValue(
        string key,
        string englishName,
        string expectedRelativePath,
        string depthModelName,
        Iw3DepthModelAvailability availability)
    {
        var selectedModel = new LocalModelPlanSelection(
            englishName,
            expectedRelativePath,
            LocalModelPlanSource.UnmanagedLocalFile);

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

        Assert.Equal(Iw3DepthModelMapper.DepthAnythingMetricDepthOutdoorKey, candidate.MappingKey);
        Assert.Equal(Iw3DepthModelMapper.ZoeDAnyKDepthModelName, candidate.Iw3DepthModelName);
        Assert.Equal("hub/checkpoints/depth_anything_metric_depth_outdoor.pt", candidate.RelativePath);
        Assert.Empty(Iw3DepthModelMapper.GetUnmappedCandidates([ImportedOutdoorModelPackCandidate()]));
    }

    [Fact]
    public void CreateSelectableCandidates_AllEightKnownFilesAreSelectable()
    {
        var candidates = Iw3DepthModelMapper.CreateSelectableCandidates(
            KnownModels.Select(model => Candidate(
                id: model.Key,
                displayName: model.EnglishName,
                relativePath: model.ExpectedRelativePath,
                isCatalogManaged: false)),
            useSpanish: false);

        Assert.Equal(8, candidates.Count);
        Assert.Equal(
            KnownModels.Select(model => model.Key).Order(StringComparer.OrdinalIgnoreCase),
            candidates.Select(candidate => candidate.MappingKey).Order(StringComparer.OrdinalIgnoreCase));
        Assert.Equal(
            KnownModels.Select(model => model.DepthModelName).Order(StringComparer.OrdinalIgnoreCase),
            candidates.Select(candidate => candidate.Iw3DepthModelName).Order(StringComparer.OrdinalIgnoreCase));
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
        Iw3DepthModelAvailability Availability);
}
