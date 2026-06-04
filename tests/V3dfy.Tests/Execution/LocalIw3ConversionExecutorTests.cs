using V3dfy.Core.Execution;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Execution;

namespace V3dfy.Tests.Execution;

public sealed class LocalIw3ConversionExecutorTests
{
    private static readonly InternalToolPaths Paths = new(
        FfmpegExecutable: @"C:\v3dfy\tools\ffmpeg\win-x64\ffmpeg.exe",
        FfprobeExecutable: @"C:\v3dfy\tools\ffmpeg\win-x64\ffprobe.exe",
        PythonExecutable: @"C:\v3dfy\engine\iw3\python\python.exe",
        Iw3EngineDirectory: @"C:\v3dfy\engine\iw3",
        ModelsDirectory: @"C:\v3dfy\engine\iw3\nunif\iw3\pretrained_models");

    [Fact]
    public void LocalIw3ConversionExecutor_ImplementsConversionExecutor()
    {
        var executor = new LocalIw3ConversionExecutor();

        Assert.IsAssignableFrom<IConversionExecutor>(executor);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsValidationFailureBeforeCancellation()
    {
        var processRequestBuilder = new SpyProcessRequestBuilder();
        var executor = new LocalIw3ConversionExecutor(processRequestBuilder: processRequestBuilder);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var result = await executor.ExecuteAsync(
            CreateRequest(sourcePath: string.Empty),
            cancellationToken: cancellationTokenSource.Token);

        Assert.False(result.Success);
        Assert.False(result.WasCanceled);
        Assert.Contains("invalid", result.EnglishSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains("SourcePath", StringComparison.Ordinal));
        AssertLogsNoProcessStarted(result);
        Assert.Equal(0, processRequestBuilder.BuildCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_ReturnsValidationFailure()
    {
        var executor = new LocalIw3ConversionExecutor();

        var result = await executor.ExecuteAsync(null!);

        Assert.False(result.Success);
        Assert.False(result.WasCanceled);
        Assert.Contains("invalid", result.EnglishSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains("request", StringComparison.OrdinalIgnoreCase));
        AssertLogsNoProcessStarted(result);
    }

    [Fact]
    public async Task ExecuteAsync_DryRunRequest_DoesNotReportProgressOrStartProcess()
    {
        var processRequestBuilder = new SpyProcessRequestBuilder();
        var executor = new LocalIw3ConversionExecutor(processRequestBuilder: processRequestBuilder);
        var progress = new CapturingProgress();

        var result = await executor.ExecuteAsync(CreateRequest(), progress);

        Assert.False(result.Success);
        Assert.False(result.WasCanceled);
        Assert.Contains("dry-run", result.EnglishSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(progress.Updates);
        AssertLogsNoProcessStarted(result);
        Assert.Equal(0, processRequestBuilder.BuildCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_NotImplementedRequest_PreparesProcessRequestWithoutReportingProgressOrStartingProcess()
    {
        var processRequestBuilder = new SpyProcessRequestBuilder();
        var executor = new LocalIw3ConversionExecutor(processRequestBuilder: processRequestBuilder);
        var progress = new CapturingProgress();
        var request = CreateRequest(
            planStatus: VideoConversionPlanStatus.Ready,
            dryRunReason: ConversionDryRunReason.None,
            isDryRun: false);

        var result = await executor.ExecuteAsync(request, progress);

        Assert.False(result.Success);
        Assert.False(result.WasCanceled);
        Assert.Contains("not implemented", result.EnglishSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(progress.Updates);
        AssertLogsNoProcessStarted(result);
        Assert.Equal(1, processRequestBuilder.BuildCallCount);
        Assert.Same(request, processRequestBuilder.LastRequest);
        Assert.NotNull(processRequestBuilder.LastProcessRequest);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "Future iw3 process request was prepared but not executed.",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_CanceledReadyRequest_ReturnsCanceledBeforeFutureExecution()
    {
        var processRequestBuilder = new SpyProcessRequestBuilder();
        var executor = new LocalIw3ConversionExecutor(processRequestBuilder: processRequestBuilder);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var result = await executor.ExecuteAsync(
            CreateRequest(
                planStatus: VideoConversionPlanStatus.Ready,
                dryRunReason: ConversionDryRunReason.None,
                isDryRun: false),
            cancellationToken: cancellationTokenSource.Token);

        Assert.False(result.Success);
        Assert.True(result.WasCanceled);
        Assert.Contains("canceled", result.EnglishSummary, StringComparison.OrdinalIgnoreCase);
        AssertLogsNoProcessStarted(result);
        Assert.Equal(0, processRequestBuilder.BuildCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_SelectedModelMetadataIsAcceptedButNotExecuted()
    {
        var processRequestBuilder = new SpyProcessRequestBuilder();
        var executor = new LocalIw3ConversionExecutor(processRequestBuilder: processRequestBuilder);
        var request = CreateRequest(
            selectedModel: new(
                "Default depth model",
                "depth/default-depth.onnx",
                LocalModelPlanSource.CatalogMetadata),
            planStatus: VideoConversionPlanStatus.Ready,
            dryRunReason: ConversionDryRunReason.None,
            isDryRun: false);

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Contains("not implemented", result.EnglishSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            result.Logs,
            log => log.EnglishMessage.Contains("depth/default-depth.onnx", StringComparison.Ordinal));
        AssertLogsNoProcessStarted(result);
        Assert.Equal(1, processRequestBuilder.BuildCallCount);
        Assert.NotNull(processRequestBuilder.LastProcessRequest);
        Assert.DoesNotContain(
            processRequestBuilder.LastProcessRequest.Arguments,
            argument => argument.Contains("depth/default-depth.onnx", StringComparison.Ordinal));
        Assert.DoesNotContain("--model", processRequestBuilder.LastProcessRequest.Arguments);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSelectedModelMetadata_ReturnsValidationFailure()
    {
        var executor = new LocalIw3ConversionExecutor();

        var result = await executor.ExecuteAsync(CreateRequest(
            selectedModel: new(
                "Default depth model",
                @"..\depth.onnx",
                LocalModelPlanSource.CatalogMetadata)));

        Assert.False(result.Success);
        Assert.Contains("invalid", result.EnglishSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains("parent traversal", StringComparison.OrdinalIgnoreCase));
        AssertLogsNoProcessStarted(result);
    }

    [Fact]
    public void LocalIw3ConversionExecutor_DoesNotExposeProcessRunnerDependency()
    {
        var constructorParameterTypes = typeof(LocalIw3ConversionExecutor)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name);

        Assert.DoesNotContain(
            constructorParameterTypes,
            parameterTypeName => parameterTypeName.Contains("ProcessRunner", StringComparison.Ordinal));
    }

    [Fact]
    public void LocalIw3ConversionExecutor_ExposesProcessRequestBuilderForPreflightOnly()
    {
        var constructorParameterTypes = typeof(LocalIw3ConversionExecutor)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType);

        Assert.Contains(typeof(LocalIw3ProcessRequestBuilder), constructorParameterTypes);
    }

    private static ConversionExecutionRequest CreateRequest(
        string sourcePath = @"C:\Videos\Movie.mp4",
        string outputPath = @"C:\Videos\Movie.v3dfy.3d.htab.mp4",
        LocalModelPlanSelection? selectedModel = null,
        VideoConversionPlanStatus planStatus = VideoConversionPlanStatus.DryRun,
        ConversionDryRunReason dryRunReason = ConversionDryRunReason.MissingLocalAiBundle,
        bool isDryRun = true)
    {
        var options = new VideoConversionPlanOptions(
            OutputContainer: OutputContainer.MP4,
            QualityPreset: AiQualityPreset.Balanced,
            Intensity: ThreeDIntensity.Medium,
            ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom);
        var plan = new VideoConversionPlan(
            SourcePath: sourcePath,
            SuggestedOutputPath: outputPath,
            OutputContainer: OutputContainer.MP4,
            VideoCodec: "H.264",
            AudioCodec: "AAC or AC3",
            Width: 1920,
            Height: 1080,
            ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
            QualityPreset: AiQualityPreset.Balanced,
            Intensity: ThreeDIntensity.Medium,
            Status: planStatus,
            DryRunReason: dryRunReason,
            Steps:
            [
                new("Read the analyzed source video.", "Leer el video de origen analizado."),
            ],
            CommandPreview: "iw3 local engine dry-run preview")
        {
            SelectedLocalModel = selectedModel,
        };

        return new(
            Plan: plan,
            SourcePath: sourcePath,
            OutputPath: outputPath,
            SelectedPreset: TargetDevicePresets.General3dVideo,
            Options: options,
            ExpectedToolPaths: Paths,
            SelectedLocalModel: selectedModel,
            CommandPreview: plan.CommandPreview,
            PlanStatus: planStatus,
            DryRunReason: dryRunReason,
            IsDryRun: isDryRun);
    }

    private static void AssertLogsNoProcessStarted(ConversionExecutionResult result) =>
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "No Python, iw3, FFmpeg conversion, or model process was started.",
                StringComparison.Ordinal));

    private sealed class CapturingProgress : IProgress<ConversionExecutionProgressUpdate>
    {
        public List<ConversionExecutionProgressUpdate> Updates { get; } = [];

        public void Report(ConversionExecutionProgressUpdate value) =>
            Updates.Add(value);
    }

    private sealed class SpyProcessRequestBuilder : LocalIw3ProcessRequestBuilder
    {
        public int BuildCallCount { get; private set; }

        public ConversionExecutionRequest? LastRequest { get; private set; }

        public ProcessExecutionRequest? LastProcessRequest { get; private set; }

        public override ProcessExecutionRequest Build(ConversionExecutionRequest request)
        {
            BuildCallCount++;
            LastRequest = request;
            LastProcessRequest = base.Build(request);
            return LastProcessRequest;
        }
    }
}
