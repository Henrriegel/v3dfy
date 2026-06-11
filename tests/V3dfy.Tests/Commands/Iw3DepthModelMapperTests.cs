using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Engine.Iw3.Commands;

namespace V3dfy.Tests.Commands;

public sealed class Iw3DepthModelMapperTests
{
    [Fact]
    public void TryMap_KnownLocalModelMapsToVerifiedIw3DepthModelValue()
    {
        var selectedModel = new LocalModelPlanSelection(
            "Depth Anything Metric Indoor",
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
            LocalModelPlanSource.UnmanagedLocalFile);

        var mapped = Iw3DepthModelMapper.TryMap(selectedModel, out var mapping);

        Assert.True(mapped);
        Assert.NotNull(mapping);
        Assert.Equal(Iw3DepthModelMapper.ZoeDAnyNDepthModelName, mapping.DepthModelName);
        Assert.Equal(
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
            mapping.ModelRelativePath);
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
    public void CreateSelectableCandidates_ImportedUnmappedModelPackModelRemainsDiagnosticOnly()
    {
        var candidates = Iw3DepthModelMapper.CreateSelectableCandidates(
            [ImportedOutdoorModelPackCandidate()],
            useSpanish: false);

        Assert.Empty(candidates);
        var unmapped = Assert.Single(Iw3DepthModelMapper.GetUnmappedCandidates([ImportedOutdoorModelPackCandidate()]));
        Assert.Equal("hub/checkpoints/depth_anything_metric_depth_outdoor.pt", unmapped.RelativePath);
        Assert.Null(unmapped.MappingKey);
        Assert.Null(unmapped.Iw3DepthModelName);
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
        Id: "depth-anything-metric-outdoor",
        DisplayName: "Depth Anything Metric Outdoor",
        RelativePath: "hub/checkpoints/depth_anything_metric_depth_outdoor.pt",
        FileName: "depth_anything_metric_depth_outdoor.pt",
        Extension: ".pt",
        ModelType: "depth",
        Purpose: "outdoor",
        IsCatalogManaged: true);
}
