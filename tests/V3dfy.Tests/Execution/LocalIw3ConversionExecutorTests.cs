using V3dfy.Core.Execution;
using V3dfy.Core.Models;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Engine.Iw3.Execution;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Tests.Execution;

public sealed class LocalIw3ConversionExecutorTests
{
    private static readonly InternalToolPaths Paths = new(
        FfmpegExecutable: @"C:\v3dfy\tools\ffmpeg\win-x64\ffmpeg.exe",
        FfprobeExecutable: @"C:\v3dfy\tools\ffmpeg\win-x64\ffprobe.exe",
        PythonExecutable: @"C:\v3dfy\engine\iw3\python\python.exe",
        Iw3EngineDirectory: @"C:\v3dfy\engine\iw3",
        ModelsDirectory: @"C:\v3dfy\engine\iw3\nunif\iw3\pretrained_models");
    private static readonly LocalModelPlanSelection RecognizedDepthModel = new(
        "depth_anything_metric_depth_indoor.pt",
        Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
        LocalModelPlanSource.UnmanagedLocalFile);

    [Fact]
    public void LocalIw3ConversionExecutor_ImplementsConversionExecutor()
    {
        var executor = new LocalIw3ConversionExecutor();

        Assert.IsAssignableFrom<IConversionExecutor>(executor);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_DoesNotStartProcess()
    {
        var runner = new FakeProcessRunner(CompletedProcess());
        var executor = new LocalIw3ConversionExecutor(processRunner: runner);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var result = await executor.ExecuteAsync(
            CreateReadyRequest(sourcePath: string.Empty),
            cancellationToken: cancellationTokenSource.Token);

        Assert.False(result.Success);
        Assert.False(result.WasCanceled);
        Assert.Contains("invalid", result.EnglishSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains("SourcePath", StringComparison.Ordinal));
        AssertLogsNoProcessStarted(result);
        Assert.Equal(0, runner.RunCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_ReturnsValidationFailureWithoutStartingProcess()
    {
        var runner = new FakeProcessRunner(CompletedProcess());
        var executor = new LocalIw3ConversionExecutor(processRunner: runner);

        var result = await executor.ExecuteAsync(null!);

        Assert.False(result.Success);
        Assert.False(result.WasCanceled);
        Assert.Contains("invalid", result.EnglishSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains("request", StringComparison.OrdinalIgnoreCase));
        AssertLogsNoProcessStarted(result);
        Assert.Equal(0, runner.RunCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_DryRunRequest_DoesNotStartProcess()
    {
        var runner = new FakeProcessRunner(CompletedProcess());
        var executor = new LocalIw3ConversionExecutor(processRunner: runner);
        var progress = new CapturingProgress();

        var result = await executor.ExecuteAsync(CreateDryRunRequest(), progress);

        Assert.False(result.Success);
        Assert.False(result.WasCanceled);
        Assert.Contains("dry-run", result.EnglishSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(progress.Updates);
        AssertLogsNoProcessStarted(result);
        Assert.Equal(0, runner.RunCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnmappedSelectedModel_DoesNotStartProcess()
    {
        var runner = new FakeProcessRunner(CompletedProcess());
        var executor = new LocalIw3ConversionExecutor(processRunner: runner);

        var result = await executor.ExecuteAsync(CreateReadyRequest(
            selectedModel: new(
                "Default depth model",
                "depth/default-depth.onnx",
                LocalModelPlanSource.CatalogMetadata)));

        Assert.False(result.Success);
        Assert.False(result.WasCanceled);
        Assert.Contains("not mapped", result.EnglishSummary, StringComparison.OrdinalIgnoreCase);
        AssertLogsNoProcessStarted(result);
        Assert.Equal(0, runner.RunCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ValidReadyRequest_StartsFakeProcess()
    {
        var runner = new FakeProcessRunner(CompletedProcess());
        var executor = new LocalIw3ConversionExecutor(processRunner: runner);

        var result = await executor.ExecuteAsync(CreateReadyRequest());

        Assert.True(result.Success);
        Assert.Equal(1, runner.RunCallCount);
        Assert.NotNull(runner.LastRequest);
        Assert.Equal(Paths.PythonExecutable, runner.LastRequest.ExecutablePath);
        Assert.Equal(Paths.NunifRootDirectory, runner.LastRequest.WorkingDirectory);
        Assert.Equal(Paths.Iw3EngineDirectory, runner.LastRequest.AllowedRootDirectory);
        Assert.Contains(Iw3CliContract.DepthModelSwitch, runner.LastRequest.Arguments);
        Assert.Contains(Iw3DepthModelMapper.ZoeDAnyNDepthModelName, runner.LastRequest.Arguments);
        Assert.DoesNotContain("--model", runner.LastRequest.Arguments);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessExitsZero_ReportsSuccessAndOutputLogs()
    {
        var runner = new FakeProcessRunner(CompletedProcess(
            outputLines:
            [
                new(
                    ProcessOutputStream.StandardOutput,
                    "iw3 finished",
                    DateTimeOffset.UtcNow),
            ]));
        var executor = new LocalIw3ConversionExecutor(processRunner: runner);

        var result = await executor.ExecuteAsync(CreateReadyRequest());

        Assert.True(result.Success);
        Assert.False(result.WasCanceled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Local iw3 conversion completed.", result.EnglishSummary);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage == "stdout: iw3 finished");
    }

    [Fact]
    public async Task ExecuteAsync_ProcessExitsNonZero_ReportsFailureAndStderrLogs()
    {
        var runner = new FakeProcessRunner(CompletedProcess(
            exitCode: 7,
            standardError: "iw3 failed"));
        var executor = new LocalIw3ConversionExecutor(processRunner: runner);

        var result = await executor.ExecuteAsync(CreateReadyRequest());

        Assert.False(result.Success);
        Assert.False(result.WasCanceled);
        Assert.Equal(7, result.ExitCode);
        Assert.Equal("Local iw3 conversion failed.", result.EnglishSummary);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage == "stderr: iw3 failed");
    }

    [Fact]
    public async Task ExecuteAsync_FakeRunnerReturnsCanceled_ReportsCancellation()
    {
        var runner = new FakeProcessRunner(CanceledProcess());
        var executor = new LocalIw3ConversionExecutor(processRunner: runner);

        var result = await executor.ExecuteAsync(CreateReadyRequest());

        Assert.False(result.Success);
        Assert.True(result.WasCanceled);
        Assert.Equal("Local iw3 conversion was canceled.", result.EnglishSummary);
        Assert.Equal(1, runner.RunCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CanceledReadyRequest_DoesNotStartProcess()
    {
        var runner = new FakeProcessRunner(CompletedProcess());
        var executor = new LocalIw3ConversionExecutor(processRunner: runner);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var result = await executor.ExecuteAsync(
            CreateReadyRequest(),
            cancellationToken: cancellationTokenSource.Token);

        Assert.False(result.Success);
        Assert.True(result.WasCanceled);
        AssertLogsNoProcessStarted(result);
        Assert.Equal(0, runner.RunCallCount);
    }

    [Fact]
    public void LocalIw3ConversionExecutor_ExposesProcessRunnerDependencyForTestability()
    {
        var constructorParameterTypes = typeof(LocalIw3ConversionExecutor)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name);

        Assert.Contains(
            constructorParameterTypes,
            parameterTypeName => parameterTypeName.Contains("ProcessRunner", StringComparison.Ordinal));
    }

    private static ConversionExecutionRequest CreateDryRunRequest() =>
        CreateRequest(
            planStatus: VideoConversionPlanStatus.DryRun,
            dryRunReason: ConversionDryRunReason.MissingLocalAiBundle,
            isDryRun: true);

    private static ConversionExecutionRequest CreateReadyRequest(
        string sourcePath = @"C:\Videos\Movie.mp4",
        string outputPath = @"C:\Videos\Movie.v3dfy.3d.htab.mp4",
        LocalModelPlanSelection? selectedModel = null) =>
        CreateRequest(
            sourcePath,
            outputPath,
            selectedModel,
            VideoConversionPlanStatus.Ready,
            ConversionDryRunReason.None,
            isDryRun: false);

    private static ConversionExecutionRequest CreateRequest(
        string sourcePath = @"C:\Videos\Movie.mp4",
        string outputPath = @"C:\Videos\Movie.v3dfy.3d.htab.mp4",
        LocalModelPlanSelection? selectedModel = null,
        VideoConversionPlanStatus planStatus = VideoConversionPlanStatus.DryRun,
        ConversionDryRunReason dryRunReason = ConversionDryRunReason.MissingLocalAiBundle,
        bool isDryRun = true)
    {
        selectedModel ??= RecognizedDepthModel;

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
            CommandPreview: "iw3 local engine command preview")
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

    private static ProcessExecutionResult CompletedProcess(
        int exitCode = 0,
        string standardOutput = "",
        string standardError = "",
        IReadOnlyList<ProcessOutputLine>? outputLines = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new(
            ExitCode: exitCode,
            StandardOutput: standardOutput,
            StandardError: standardError,
            OutputLines: outputLines ?? [],
            Status: ProcessExecutionStatus.Completed,
            StartedAt: now,
            EndedAt: now);
    }

    private static ProcessExecutionResult CanceledProcess()
    {
        var now = DateTimeOffset.UtcNow;
        return new(
            ExitCode: -1,
            StandardOutput: string.Empty,
            StandardError: string.Empty,
            OutputLines: [],
            Status: ProcessExecutionStatus.Canceled,
            StartedAt: now,
            EndedAt: now);
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

    private sealed class FakeProcessRunner(ProcessExecutionResult result) : ILocalProcessRunner
    {
        public int RunCallCount { get; private set; }

        public ProcessExecutionRequest? LastRequest { get; private set; }

        public Task<ProcessExecutionResult> RunAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            RunCallCount++;
            LastRequest = request;
            return Task.FromResult(result);
        }
    }
}
