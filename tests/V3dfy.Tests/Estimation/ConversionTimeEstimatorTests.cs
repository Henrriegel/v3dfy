using V3dfy.Core.Estimation;
using V3dfy.Core.Models;

namespace V3dfy.Tests.Estimation;

public sealed class ConversionTimeEstimatorTests
{
    private readonly ConversionTimeEstimator estimator = new();

    [Fact]
    public void Estimate_NoVideoAnalysis_ReturnsUnavailable()
    {
        var estimate = estimator.Estimate(null, []);

        Assert.False(estimate.IsAvailable);
        Assert.Equal(ConversionEstimateConfidence.Unavailable, estimate.Confidence);
    }

    [Fact]
    public void Estimate_NoHistoryUsesLowConfidenceFallbackRange()
    {
        var estimate = estimator.Estimate(CreateInput(), []);

        Assert.True(estimate.IsAvailable);
        Assert.Equal(ConversionEstimateConfidence.Low, estimate.Confidence);
        Assert.True(estimate.High > estimate.Low);
        Assert.False(estimate.UsedLocalHistory);
    }

    [Fact]
    public void Estimate_SameModelResolutionFullHistoryUsesHighConfidence()
    {
        var input = CreateInput(modelKey: "depth-anything-v2-small");
        var history = new[]
        {
            CreateRecord(
                ConversionPerformanceOperationType.FullConversion,
                "depth-anything-v2-small",
                width: 1920,
                height: 1080,
                mpfs: 42),
        };

        var estimate = estimator.Estimate(input, history);

        Assert.Equal(ConversionEstimateConfidence.High, estimate.Confidence);
        Assert.True(estimate.UsedLocalHistory);
    }

    [Fact]
    public void Estimate_SimilarModelHistoryUsesMediumConfidence()
    {
        var input = CreateInput(modelKey: "depth-anything-v2-small");
        var history = new[]
        {
            CreateRecord(
                ConversionPerformanceOperationType.Preview,
                "depth-anything-small",
                width: 1600,
                height: 900,
                mpfs: 32),
        };

        var estimate = estimator.Estimate(input, history);

        Assert.Equal(ConversionEstimateConfidence.Medium, estimate.Confidence);
        Assert.True(estimate.UsedLocalHistory);
    }

    [Fact]
    public void Estimate_LargeModelIsSlowerThanSmallModel()
    {
        var small = estimator.Estimate(
            CreateInput(modelKey: "depth-anything-v2-small"),
            []);
        var large = estimator.Estimate(
            CreateInput(modelKey: "depth-anything-3-mono-large"),
            []);

        Assert.True(large.Low > small.Low);
        Assert.True(large.High > small.High);
    }

    [Fact]
    public void Estimate_HigherResolutionAndFrameCountEstimateLonger()
    {
        var shortLowRes = estimator.Estimate(
            CreateInput(duration: TimeSpan.FromMinutes(5), width: 1280, height: 720),
            []);
        var longHighRes = estimator.Estimate(
            CreateInput(duration: TimeSpan.FromMinutes(20), width: 3840, height: 2160),
            []);

        Assert.True(longHighRes.Low > shortLowRes.Low);
        Assert.True(longHighRes.High > shortLowRes.High);
    }

    private static ConversionEstimateInput CreateInput(
        string modelKey = "depth-anything-v2-small",
        TimeSpan? duration = null,
        int width = 1920,
        int height = 1080) => new(
        Duration: duration ?? TimeSpan.FromMinutes(12),
        FrameRate: 24,
        SourceWidth: width,
        SourceHeight: height,
        ModelKey: modelKey,
        ModelDisplayName: "Depth Anything V2 Small",
        OutputPresetId: "recommended-3d-tv",
        OutputPresetName: "Recommended 3D TV",
        OutputContainer: OutputContainer.MP4,
        QualityPreset: AiQualityPreset.Balanced,
        ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
        DeviceBucket: "local engine ready");

    private static ConversionPerformanceRecord CreateRecord(
        ConversionPerformanceOperationType operationType,
        string modelKey,
        int width,
        int height,
        double mpfs) => new(
        SchemaVersion: ConversionPerformanceHistory.CurrentSchemaVersion,
        OperationType: operationType,
        ModelKey: modelKey,
        ModelDisplayName: modelKey,
        OutputPresetId: "recommended-3d-tv",
        Width: width,
        Height: height,
        FrameCount: 2400,
        DurationSeconds: 100,
        ElapsedSeconds: 120,
        EffectiveFramesPerSecond: 20,
        EffectiveMegapixelFramesPerSecond: mpfs,
        DeviceBucket: "local engine ready",
        TimestampUtc: DateTimeOffset.UtcNow,
        AppVersion: "test");
}
