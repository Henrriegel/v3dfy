using V3dfy.Core.Models;
using V3dfy.Core.Preview;

namespace V3dfy.Tests.Preview;

public sealed class PreviewConversionGateTests
{
    [Fact]
    public void Evaluate_BlocksFinalConversionUntilPreviewIsAcceptedAndCurrent()
    {
        var configuration = CreateConfiguration();
        var readyState = ReadyState(configuration);

        var beforeAcceptance = PreviewConversionGate.Evaluate(readyState, configuration);
        var accepted = readyState.Accept(configuration);
        var afterAcceptance = PreviewConversionGate.Evaluate(accepted, configuration);

        Assert.False(beforeAcceptance.CanStart);
        Assert.Equal("Preview required", beforeAcceptance.EnglishStatus);
        Assert.True(afterAcceptance.CanStart);
        Assert.Equal("Preview accepted", afterAcceptance.EnglishStatus);
    }

    [Fact]
    public void Evaluate_SettingsChangeAfterAcceptanceBlocksFinalConversionAgain()
    {
        var configuration = CreateConfiguration();
        var accepted = ReadyState(configuration).Accept(configuration);
        var changed = configuration with { Intensity = ThreeDIntensity.High };
        var outdated = accepted.MarkOutdatedIfConfigurationChanged(changed);

        var result = PreviewConversionGate.Evaluate(outdated, changed);

        Assert.False(result.CanStart);
        Assert.Equal(PreviewGenerationStatus.Outdated, outdated.Status);
        Assert.Equal("Preview outdated", result.EnglishStatus);
    }

    private static PreviewWorkflowState ReadyState(
        PreviewConfigurationSnapshot configuration) => PreviewWorkflowState
        .NotGenerated(configuration.PreviewStartTime, configuration.PreviewDuration)
        .Generating(configuration, DateTimeOffset.UtcNow)
        .Complete(new(
            Success: true,
            WasCanceled: false,
            Status: PreviewGenerationStatus.Ready,
            PreviewOutputPath: PreviewPaths().PreviewOutputPath,
            CachePaths: PreviewPaths(),
            StartedAt: DateTimeOffset.UtcNow,
            FinishedAt: DateTimeOffset.UtcNow,
            EnglishSummary: "Preview generated successfully.",
            SpanishSummary: "La vista previa se genero correctamente.",
            Logs: []),
            configuration);

    private static PreviewConfigurationSnapshot CreateConfiguration() => new(
        SourcePath: TestPaths.SourceRoot("Movie.mp4"),
        OutputProfileName: "LG 3D Full HD 2012",
        OutputContainer: OutputContainer.MP4,
        QualityPreset: AiQualityPreset.Balanced,
        Intensity: ThreeDIntensity.Medium,
        ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
        ModelKey: "depth-anything-metric-indoor",
        ModelDisplayName: "Depth Anything Metric Indoor",
        ModelRelativePath: "hub/checkpoints/depth_anything_metric_depth_indoor.pt",
        Iw3DepthModelName: "ZoeD_Any_N",
        PreviewStartTime: TimeSpan.FromMinutes(10),
        PreviewDuration: TimeSpan.FromSeconds(15));

    private static PreviewCachePaths PreviewPaths() => new(
        CacheDirectory: TestPaths.PreviewCacheRoot(),
        ShortSourcePath: TestPaths.PreviewCacheRoot("source.mp4"),
        PartialShortSourcePath: TestPaths.PreviewCacheRoot("source.part.mp4"),
        PreviewOutputPath: TestPaths.PreviewCacheRoot("preview.mp4"),
        PartialPreviewOutputPath: TestPaths.PreviewCacheRoot("preview.part.mp4"));
}
