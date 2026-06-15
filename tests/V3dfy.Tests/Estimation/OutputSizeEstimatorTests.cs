using V3dfy.Core.Estimation;
using V3dfy.Core.Models;

namespace V3dfy.Tests.Estimation;

public sealed class OutputSizeEstimatorTests
{
    private readonly OutputSizeEstimator estimator = new();

    [Fact]
    public void Estimate_UsesDurationAndBitrateRange()
    {
        var estimate = estimator.Estimate(CreateInput(duration: TimeSpan.FromHours(1)));

        Assert.True(estimate.IsAvailable);
        Assert.True(estimate.LowBytes > 0);
        Assert.True(estimate.HighBytes > estimate.LowBytes);
    }

    [Fact]
    public void Estimate_BiggerBitrateAndDurationIncreaseSize()
    {
        var small = estimator.Estimate(CreateInput(
            duration: TimeSpan.FromMinutes(30),
            presetId: "maximum-compatibility",
            qualityPreset: AiQualityPreset.Fast));
        var large = estimator.Estimate(CreateInput(
            duration: TimeSpan.FromHours(2),
            presetId: "high-quality-master",
            outputContainer: OutputContainer.MKV,
            qualityPreset: AiQualityPreset.HighQuality));

        Assert.True(large.HighBytes > small.HighBytes);
    }

    [Fact]
    public void Estimate_RecommendedFreeSpaceIncludesTemporaryOverhead()
    {
        var estimate = estimator.Estimate(CreateInput());

        Assert.True(estimate.RecommendedFreeBytes > estimate.HighBytes);
    }

    [Fact]
    public void Estimate_NoDurationReturnsUnavailable()
    {
        var estimate = estimator.Estimate(new(
            Duration: null,
            OutputContainer: OutputContainer.MP4,
            QualityPreset: AiQualityPreset.Balanced,
            OutputPresetId: "recommended-3d-tv",
            TargetWidth: 1920,
            TargetHeight: 1080,
            IncludeTemporaryWorkingSpace: true));

        Assert.False(estimate.IsAvailable);
        Assert.Contains("depends", estimate.EnglishBasisItems[0]);
    }

    private static OutputSizeEstimateInput CreateInput(
        TimeSpan? duration = null,
        string presetId = "recommended-3d-tv",
        OutputContainer outputContainer = OutputContainer.MP4,
        AiQualityPreset qualityPreset = AiQualityPreset.Balanced) => new(
        Duration: duration ?? TimeSpan.FromMinutes(90),
        OutputContainer: outputContainer,
        QualityPreset: qualityPreset,
        OutputPresetId: presetId,
        TargetWidth: 1920,
        TargetHeight: 1080,
        IncludeTemporaryWorkingSpace: true);
}
