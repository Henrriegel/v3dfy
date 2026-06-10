using V3dfy.Core.Execution;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Engine.Iw3.Execution;

namespace V3dfy.Tests.Execution;

public sealed class LgCompatibilityCopyRequestBuilderTests
{
    private static readonly InternalToolPaths Paths = TestPaths.InternalToolPaths();

    [Fact]
    public void Create_HalfSideBySide_BuildsBundledFfmpegMp4PostProcessRequest()
    {
        var request = CreateRequest(
            ThreeDOutputFormat.HalfSideBySide,
            createLgCompatibilityCopy: true);
        var builder = new LgCompatibilityCopyRequestBuilder();
        var primaryOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.hsbs.mp4");
        var partialCopyPath = TestPaths.OutputRoot("Movie.v3dfy.3d.hsbs.lg3d.hsbs.v3dfy-partial.mp4");
        var finalCopyPath = TestPaths.OutputRoot("Movie.v3dfy.3d.hsbs.lg3d.hsbs.mp4");

        var result = builder.Create(
            request,
            primaryOutputPath,
            partialCopyPath);

        Assert.True(result.ShouldRun);
        Assert.False(result.Unsupported);
        Assert.Equal(finalCopyPath, result.FinalOutputPath);
        Assert.NotNull(result.ProcessRequest);
        Assert.Equal(Paths.FfmpegExecutable, result.ProcessRequest.ExecutablePath);
        Assert.Equal(
            Path.GetDirectoryName(Paths.FfmpegExecutable),
            result.ProcessRequest.AllowedRootDirectory);
        Assert.Contains("-c:v", result.ProcessRequest.Arguments);
        Assert.Contains("libx264", result.ProcessRequest.Arguments);
        Assert.Contains("+faststart", result.ProcessRequest.Arguments);
        Assert.Contains("yuv420p", result.ProcessRequest.Arguments);
        Assert.Contains("-map", result.ProcessRequest.Arguments);
        Assert.Contains("0:v:0", result.ProcessRequest.Arguments);
        Assert.Contains("0:a?", result.ProcessRequest.Arguments);
        Assert.Contains("-c:a", result.ProcessRequest.Arguments);
        Assert.Contains("copy", result.ProcessRequest.Arguments);
        Assert.DoesNotContain("aac", result.ProcessRequest.Arguments);
        Assert.Contains(
            LgCompatibilityCopyRequestBuilder.HalfSideBySideFilter,
            result.ProcessRequest.Arguments);
        Assert.DoesNotContain("scale=1920:ih", result.ProcessRequest.Arguments);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage == LgCompatibilityCopyRequestBuilder.AudioStrategyEnglish);
    }

    [Fact]
    public void Create_HalfTopBottom_ReturnsSpecificUnsupportedWarning()
    {
        var request = CreateRequest(
            ThreeDOutputFormat.HalfTopBottom,
            createLgCompatibilityCopy: true);
        var builder = new LgCompatibilityCopyRequestBuilder();

        var result = builder.Create(
            request,
            TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4"),
            TestPaths.OutputRoot("Movie.v3dfy.3d.htab.lg3d.htab.v3dfy-partial.mp4"));

        Assert.False(result.ShouldRun);
        Assert.True(result.Unsupported);
        Assert.Null(result.ProcessRequest);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "post-processing has not been verified",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Create_WhenOptionDisabled_DoesNotBuildProcessRequest()
    {
        var request = CreateRequest(
            ThreeDOutputFormat.HalfSideBySide,
            createLgCompatibilityCopy: false);
        var builder = new LgCompatibilityCopyRequestBuilder();

        var result = builder.Create(
            request,
            TestPaths.OutputRoot("Movie.v3dfy.3d.hsbs.mp4"),
            TestPaths.OutputRoot("Movie.v3dfy.3d.hsbs.lg3d.hsbs.v3dfy-partial.mp4"));

        Assert.False(result.ShouldRun);
        Assert.False(result.Unsupported);
        Assert.Null(result.ProcessRequest);
    }

    private static ConversionExecutionRequest CreateRequest(
        ThreeDOutputFormat outputFormat,
        bool createLgCompatibilityCopy)
    {
        var options = new VideoConversionPlanOptions(
            OutputContainer.MP4,
            AiQualityPreset.Balanced,
            ThreeDIntensity.Medium,
            outputFormat,
            CreateLgCompatibilityCopy: createLgCompatibilityCopy);
        var plan = new VideoConversionPlan(
            SourcePath: TestPaths.SourceRoot("Movie.mp4"),
            SuggestedOutputPath: TestPaths.OutputRoot("Movie.v3dfy.3d.hsbs.mp4"),
            OutputContainer: OutputContainer.MP4,
            VideoCodec: "H.264",
            AudioCodec: "AAC or AC3",
            Width: 1920,
            Height: 1080,
            ThreeDOutputFormat: outputFormat,
            QualityPreset: AiQualityPreset.Balanced,
            Intensity: ThreeDIntensity.Medium,
            Status: VideoConversionPlanStatus.Ready,
            DryRunReason: ConversionDryRunReason.None,
            Steps: [],
            CommandPreview: "preview");

        return new(
            Plan: plan,
            SourcePath: plan.SourcePath,
            OutputPath: plan.SuggestedOutputPath,
            SelectedPreset: TargetDevicePresets.Lg3dFullHd2012,
            Options: options,
            ExpectedToolPaths: Paths,
            SelectedLocalModel: null,
            CommandPreview: plan.CommandPreview,
            PlanStatus: plan.Status,
            DryRunReason: plan.DryRunReason,
            IsDryRun: false);
    }
}
