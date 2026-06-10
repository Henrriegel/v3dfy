using V3dfy.Core.Execution;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;

namespace V3dfy.Tests.Execution;

public sealed class DisabledConversionExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsNotEnabledFailure()
    {
        var executor = new DisabledConversionExecutor();

        var result = await executor.ExecuteAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.False(result.WasCanceled);
        Assert.Null(result.ExitCode);
        Assert.Equal("Conversion execution is not enabled yet.", result.EnglishSummary);
        Assert.Equal("La ejecución de conversión aún no está habilitada.", result.SpanishSummary);
        Assert.Single(result.Logs);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotReportProgress()
    {
        var executor = new DisabledConversionExecutor();
        var progress = new CapturingProgress();

        await executor.ExecuteAsync(CreateRequest(), progress);

        Assert.Empty(progress.Updates);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationTokenIsCanceled_ReturnsCanceledResult()
    {
        var executor = new DisabledConversionExecutor();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var result = await executor.ExecuteAsync(
            CreateRequest(),
            cancellationToken: cancellationTokenSource.Token);

        Assert.False(result.Success);
        Assert.True(result.WasCanceled);
        Assert.Equal("Conversion was canceled before it started.", result.EnglishSummary);
        Assert.Equal("La conversión fue cancelada antes de iniciar.", result.SpanishSummary);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestCancellationTokenIsCanceled_ReturnsCanceledResult()
    {
        var executor = new DisabledConversionExecutor();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var request = CreateRequest(cancellationTokenSource.Token);

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.True(result.WasCanceled);
    }

    [Fact]
    public void Request_StoresPlanSourceOutputPresetAndOptions()
    {
        var request = CreateRequest();

        Assert.Equal(TestPaths.SourceRoot("Movie.mp4"), request.Plan.SourcePath);
        Assert.Equal(TestPaths.SourceRoot("Movie.mp4"), request.SourcePath);
        Assert.Equal(TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4"), request.OutputPath);
        Assert.Equal(TargetDevicePresets.General3dVideo, request.SelectedPreset);
        Assert.Equal(OutputContainer.MP4, request.Options.OutputContainer);
        Assert.Contains("iw3", request.CommandPreview);
        Assert.True(request.IsDryRun);
    }

    private static ConversionExecutionRequest CreateRequest(
        CancellationToken cancellationToken = default)
    {
        var plan = new VideoConversionPlan(
            SourcePath: TestPaths.SourceRoot("Movie.mp4"),
            SuggestedOutputPath: TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4"),
            OutputContainer: OutputContainer.MP4,
            VideoCodec: "H.264",
            AudioCodec: "AAC",
            Width: 1920,
            Height: 1080,
            ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
            QualityPreset: AiQualityPreset.Balanced,
            Intensity: ThreeDIntensity.Medium,
            Status: VideoConversionPlanStatus.DryRun,
            DryRunReason: ConversionDryRunReason.MissingLocalAiBundle,
            Steps:
            [
                new("Read the analyzed source video.", "Leer el video de origen analizado."),
            ],
            CommandPreview: "iw3 local engine dry-run preview");
        var options = new VideoConversionPlanOptions(
            OutputContainer: OutputContainer.MP4,
            QualityPreset: AiQualityPreset.Balanced,
            Intensity: ThreeDIntensity.Medium,
            ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom);

        return new(
            Plan: plan,
            SourcePath: plan.SourcePath,
            OutputPath: plan.SuggestedOutputPath,
            SelectedPreset: TargetDevicePresets.General3dVideo,
            Options: options,
            ExpectedToolPaths: Paths,
            SelectedLocalModel: plan.SelectedLocalModel,
            CommandPreview: plan.CommandPreview,
            PlanStatus: plan.Status,
            DryRunReason: plan.DryRunReason,
            IsDryRun: plan.IsDryRun,
            CancellationToken: cancellationToken);
    }

    private static readonly InternalToolPaths Paths = TestPaths.InternalToolPaths();

    private sealed class CapturingProgress : IProgress<ConversionExecutionProgressUpdate>
    {
        public List<ConversionExecutionProgressUpdate> Updates { get; } = [];

        public void Report(ConversionExecutionProgressUpdate value) =>
            Updates.Add(value);
    }
}
