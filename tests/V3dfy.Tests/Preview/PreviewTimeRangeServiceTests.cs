using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Core.Preview;

namespace V3dfy.Tests.Preview;

public sealed class PreviewTimeRangeServiceTests
{
    [Fact]
    public void CreateDefaultRange_LongVideoUsesTenMinuteStartAndFifteenSeconds()
    {
        var range = PreviewTimeRangeService.CreateDefaultRange(TimeSpan.FromMinutes(90));

        Assert.Equal(TimeSpan.FromMinutes(10), range.From);
        Assert.Equal(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(15), range.To);
    }

    [Fact]
    public void CreateDefaultRange_ShortVideoStartsAtZeroAndClampsToDuration()
    {
        var range = PreviewTimeRangeService.CreateDefaultRange(TimeSpan.FromSeconds(8));

        Assert.Equal(TimeSpan.Zero, range.From);
        Assert.Equal(TimeSpan.FromSeconds(8), range.To);
    }

    [Fact]
    public void Validate_RangeLongerThanNinetySecondsIsRejected()
    {
        var result = PreviewTimeRangeService.Validate(
            "00:00:00",
            "00:01:31",
            TimeSpan.FromMinutes(5));

        Assert.False(result.IsValid);
        Assert.Equal(PreviewTimeRangeValidationIssue.ExceedsMaximumDuration, result.Issue);
    }

    [Fact]
    public void Validate_RangeExactlyNinetySecondsIsAccepted()
    {
        var result = PreviewTimeRangeService.Validate(
            "00:10:00",
            "00:11:30",
            TimeSpan.FromMinutes(90));

        Assert.True(result.IsValid);
        Assert.Equal(TimeSpan.FromSeconds(90), result.Range?.Duration);
    }

    [Theory]
    [InlineData("00:00:10", "00:00:10")]
    [InlineData("00:00:11", "00:00:10")]
    public void Validate_FromMustBeBeforeTo(string from, string to)
    {
        var result = PreviewTimeRangeService.Validate(from, to, TimeSpan.FromMinutes(5));

        Assert.False(result.IsValid);
        Assert.Equal(PreviewTimeRangeValidationIssue.FromMustBeBeforeTo, result.Issue);
    }

    [Fact]
    public void Validate_ToBeyondAnalyzedDurationIsRejected()
    {
        var result = PreviewTimeRangeService.Validate(
            "00:00:00",
            "00:00:16",
            TimeSpan.FromSeconds(15));

        Assert.False(result.IsValid);
        Assert.Equal(PreviewTimeRangeValidationIssue.ToBeyondSourceDuration, result.Issue);
    }

    [Fact]
    public void CreateConfiguration_ValidRangeSetsPreviewStartAndDuration()
    {
        var validation = PreviewTimeRangeService.Validate(
            "00:10:00",
            "00:10:25",
            TimeSpan.FromMinutes(90));

        var snapshot = PreviewConfigurationSnapshot.Create(
            CreatePlan(),
            TargetDevicePresets.Lg3dFullHd2012,
            validation.Range!);

        Assert.Equal(TimeSpan.FromMinutes(10), snapshot.PreviewStartTime);
        Assert.Equal(TimeSpan.FromSeconds(25), snapshot.PreviewDuration);
    }

    private static VideoConversionPlan CreatePlan() => new(
        SourcePath: @"D:\Videos\Movie.mp4",
        SuggestedOutputPath: @"D:\Videos\Movie.3d.mp4",
        OutputContainer: OutputContainer.MP4,
        VideoCodec: "H.264",
        AudioCodec: "AAC",
        Width: 1920,
        Height: 1080,
        ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
        QualityPreset: AiQualityPreset.Balanced,
        Intensity: ThreeDIntensity.Medium,
        Status: VideoConversionPlanStatus.Ready,
        DryRunReason: ConversionDryRunReason.None,
        Steps: [],
        CommandPreview: "iw3 preview")
    {
        SelectedLocalModel = new(
            "Depth Anything Metric Indoor",
            "hub/checkpoints/depth_anything_metric_depth_indoor.pt",
            LocalModelPlanSource.UnmanagedLocalFile,
            Iw3DepthModelName: "ZoeD_Any_N",
            MappingKey: "depth-anything-metric-indoor"),
    };
}
