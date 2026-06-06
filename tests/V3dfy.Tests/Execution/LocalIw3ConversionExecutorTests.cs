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
        var files = new FakeConversionOutputFileService();
        var runner = new FakeProcessRunner(
            CompletedProcess(),
            onRun: request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest());

        Assert.True(result.Success);
        Assert.Equal(1, runner.RunCallCount);
        Assert.NotNull(runner.LastRequest);
        Assert.Equal(Paths.PythonExecutable, runner.LastRequest.ExecutablePath);
        Assert.Equal(Paths.NunifRootDirectory, runner.LastRequest.WorkingDirectory);
        Assert.Equal(Paths.Iw3EngineDirectory, runner.LastRequest.AllowedRootDirectory);
        Assert.Contains(Iw3CliContract.HalfTopBottomSwitch, runner.LastRequest.Arguments);
        Assert.Contains(Iw3CliContract.DepthModelSwitch, runner.LastRequest.Arguments);
        Assert.Contains(Iw3DepthModelMapper.ZoeDAnyNDepthModelName, runner.LastRequest.Arguments);
        Assert.DoesNotContain("--model", runner.LastRequest.Arguments);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessExitsZero_PromotesPartialOutputToFinalOutput()
    {
        var files = new FakeConversionOutputFileService();
        var finalOutputPath = @"C:\Videos\Movie.v3dfy.3d.htab.mp4";
        var partialOutputPath = ConversionOutputFinalizer.CreatePartialOutputPath(
            finalOutputPath);
        var runner = new FakeProcessRunner(
            CompletedProcess(),
            onRun: request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(
            outputPath: finalOutputPath));

        Assert.True(result.Success);
        Assert.False(files.Exists(partialOutputPath));
        Assert.True(files.Exists(finalOutputPath));
        Assert.Contains((partialOutputPath, finalOutputPath, true), files.Moves);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains("Final output saved", StringComparison.Ordinal));
        Assert.Equal(partialOutputPath, GetProcessOutputPath(runner.LastRequest!));
    }

    [Fact]
    public async Task ExecuteAsync_WhenLgCompatibilityCopyEnabled_RunsFfmpegAfterPrimaryPromotion()
    {
        var files = new FakeConversionOutputFileService();
        var finalOutputPath = @"C:\Videos\Movie.v3dfy.3d.hsbs.mp4";
        var compatibilityOutputPath =
            LgCompatibilityCopyRequestBuilder.CreateCompatibilityOutputPath(
                finalOutputPath,
                ThreeDOutputFormat.HalfSideBySide);
        var runner = new FakeProcessRunner(
            [CompletedProcess(), CompletedProcess()],
            onRun: request => files.Add(GetRequestedOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(
            outputPath: finalOutputPath,
            outputFormat: ThreeDOutputFormat.HalfSideBySide,
            createLgCompatibilityCopy: true,
            preferLgCompatibilityCopyWhenOpening: true));

        Assert.True(result.Success);
        Assert.True(result.CompatibilityCopySucceeded);
        Assert.Equal(2, runner.RunCallCount);
        Assert.Equal(Paths.PythonExecutable, runner.Requests[0].ExecutablePath);
        Assert.Equal(Paths.FfmpegExecutable, runner.Requests[1].ExecutablePath);
        Assert.Equal(finalOutputPath, GetFfmpegInputPath(runner.Requests[1]));
        Assert.Contains("-vf", runner.Requests[1].Arguments);
        Assert.Contains("-c:a", runner.Requests[1].Arguments);
        Assert.Contains("copy", runner.Requests[1].Arguments);
        Assert.Contains(
            LgCompatibilityCopyRequestBuilder.HalfSideBySideFilter,
            runner.Requests[1].Arguments);
        Assert.DoesNotContain("scale=1920:ih", runner.Requests[1].Arguments);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage == LgCompatibilityCopyRequestBuilder.AudioStrategyEnglish);
        Assert.True(files.Exists(finalOutputPath));
        Assert.True(files.Exists(compatibilityOutputPath));
        Assert.Equal(compatibilityOutputPath, result.CompatibilityOutputPath);
        Assert.Equal(compatibilityOutputPath, result.PreferredOpenOutputPath);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLgCompatibilityCopyFails_KeepsPrimaryOutputSuccessful()
    {
        var files = new FakeConversionOutputFileService();
        var finalOutputPath = @"C:\Videos\Movie.v3dfy.3d.hsbs.mp4";
        var compatibilityOutputPath =
            LgCompatibilityCopyRequestBuilder.CreateCompatibilityOutputPath(
                finalOutputPath,
                ThreeDOutputFormat.HalfSideBySide);
        var runner = new FakeProcessRunner(
            [CompletedProcess(), CompletedProcess(exitCode: 7, standardError: "ffmpeg failed")],
            onRun: request => files.Add(GetRequestedOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(
            outputPath: finalOutputPath,
            outputFormat: ThreeDOutputFormat.HalfSideBySide,
            createLgCompatibilityCopy: true));

        Assert.True(result.Success);
        Assert.False(result.CompatibilityCopySucceeded);
        Assert.Equal(2, runner.RunCallCount);
        Assert.True(files.Exists(finalOutputPath));
        Assert.False(files.Exists(compatibilityOutputPath));
        Assert.Equal(finalOutputPath, result.PreferredOpenOutputPath);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "LG-compatible MP4 copy failed",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_LiveProcessOutputAndMetrics_AreReportedThroughProgress()
    {
        var outputLine = new ProcessOutputLine(
            ProcessOutputStream.StandardError,
            "iw3 progress frame 1",
            DateTimeOffset.UtcNow);
        var metricSample = new ProcessMetricSample(
            CapturedAt: DateTimeOffset.UtcNow,
            CpuUsagePercent: 17.5,
            WorkingSetBytes: 128 * 1024 * 1024,
            PrivateMemoryBytes: 256 * 1024 * 1024,
            GpuUsagePercent: null,
            GpuStatus: ProcessGpuMetricReading.NoProcessGpuEngineCounterStatus);
        var files = new FakeConversionOutputFileService();
        var runner = new FakeProcessRunner(
            CompletedProcess(),
            outputLineToReport: outputLine,
            metricSampleToReport: metricSample,
            onRun: request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);
        var progress = new CapturingProgress();

        var result = await executor.ExecuteAsync(CreateReadyRequest(), progress);

        Assert.True(result.Success);
        Assert.NotNull(runner.LastRequest);
        Assert.NotNull(runner.LastRequest.OutputProgress);
        Assert.NotNull(runner.LastRequest.MetricsProgress);
        Assert.Contains(
            progress.Updates,
            update => update.OutputLine == outputLine &&
                update.DetailEnglish == "stderr: iw3 progress frame 1");
        Assert.Contains(
            progress.Updates,
            update => update.Metrics == metricSample &&
                update.DetailEnglish == "Process metrics updated.");
    }

    [Fact]
    public async Task ExecuteAsync_ProcessExitsZero_ReportsSuccessAndOutputLogs()
    {
        var files = new FakeConversionOutputFileService();
        var runner = new FakeProcessRunner(CompletedProcess(
            outputLines:
            [
                new(
                    ProcessOutputStream.StandardOutput,
                    "iw3 finished",
                    DateTimeOffset.UtcNow),
            ]),
            onRun: request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

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
        var files = new FakeConversionOutputFileService();
        var finalOutputPath = @"C:\Videos\Movie.v3dfy.3d.htab.mp4";
        files.Add(finalOutputPath);
        var runner = new FakeProcessRunner(CompletedProcess(
            exitCode: 7,
            standardError: "iw3 failed"),
            onRun: request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(
            outputPath: finalOutputPath));

        Assert.False(result.Success);
        Assert.False(result.WasCanceled);
        Assert.Equal(7, result.ExitCode);
        Assert.Equal("Local iw3 conversion failed.", result.EnglishSummary);
        Assert.True(files.Exists(finalOutputPath));
        Assert.False(files.Exists(ConversionOutputFinalizer.CreatePartialOutputPath(finalOutputPath)));
        Assert.Empty(files.Moves);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage == "stderr: iw3 failed");
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage == "Conversion failed. Partial output was deleted.");
    }

    [Fact]
    public async Task ExecuteAsync_FakeRunnerReturnsCanceled_ReportsCancellation()
    {
        var files = new FakeConversionOutputFileService();
        var finalOutputPath = @"C:\Videos\Movie.v3dfy.3d.htab.mp4";
        var runner = new FakeProcessRunner(
            CanceledProcess(),
            onRun: request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(
            outputPath: finalOutputPath));

        Assert.False(result.Success);
        Assert.True(result.WasCanceled);
        Assert.Equal("Local iw3 conversion was canceled.", result.EnglishSummary);
        Assert.False(files.Exists(finalOutputPath));
        Assert.False(files.Exists(ConversionOutputFinalizer.CreatePartialOutputPath(finalOutputPath)));
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage == "Conversion canceled. Partial output was deleted.");
        Assert.Equal(1, runner.RunCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationWhileRunning_PassesCancellationToRunner()
    {
        var files = new FakeConversionOutputFileService();
        var runner = new CancelAwareProcessRunner(
            request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);
        using var cancellationTokenSource = new CancellationTokenSource();

        var resultTask = executor.ExecuteAsync(
            CreateReadyRequest(),
            cancellationToken: cancellationTokenSource.Token);
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellationTokenSource.Cancel();
        var result = await resultTask;

        Assert.False(result.Success);
        Assert.True(result.WasCanceled);
        Assert.True(runner.ObservedCancellation);
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
        LocalModelPlanSelection? selectedModel = null,
        ThreeDOutputFormat outputFormat = ThreeDOutputFormat.HalfTopBottom,
        bool createLgCompatibilityCopy = false,
        bool preferLgCompatibilityCopyWhenOpening = false) =>
        CreateRequest(
            sourcePath,
            outputPath,
            selectedModel,
            VideoConversionPlanStatus.Ready,
            ConversionDryRunReason.None,
            isDryRun: false,
            outputFormat: outputFormat,
            createLgCompatibilityCopy: createLgCompatibilityCopy,
            preferLgCompatibilityCopyWhenOpening: preferLgCompatibilityCopyWhenOpening);

    private static ConversionExecutionRequest CreateRequest(
        string sourcePath = @"C:\Videos\Movie.mp4",
        string outputPath = @"C:\Videos\Movie.v3dfy.3d.htab.mp4",
        LocalModelPlanSelection? selectedModel = null,
        VideoConversionPlanStatus planStatus = VideoConversionPlanStatus.DryRun,
        ConversionDryRunReason dryRunReason = ConversionDryRunReason.MissingLocalAiBundle,
        bool isDryRun = true,
        ThreeDOutputFormat outputFormat = ThreeDOutputFormat.HalfTopBottom,
        bool createLgCompatibilityCopy = false,
        bool preferLgCompatibilityCopyWhenOpening = false)
    {
        selectedModel ??= RecognizedDepthModel;

        var options = new VideoConversionPlanOptions(
            OutputContainer: OutputContainer.MP4,
            QualityPreset: AiQualityPreset.Balanced,
            Intensity: ThreeDIntensity.Medium,
            ThreeDOutputFormat: outputFormat,
            CreateLgCompatibilityCopy: createLgCompatibilityCopy,
            PreferLgCompatibilityCopyWhenOpening: preferLgCompatibilityCopyWhenOpening);
        var plan = new VideoConversionPlan(
            SourcePath: sourcePath,
            SuggestedOutputPath: outputPath,
            OutputContainer: OutputContainer.MP4,
            VideoCodec: "H.264",
            AudioCodec: "AAC or AC3",
            Width: 1920,
            Height: 1080,
            ThreeDOutputFormat: outputFormat,
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

    private static string GetProcessOutputPath(ProcessExecutionRequest request)
    {
        var outputSwitchIndex = request.Arguments
            .Select((argument, index) => (argument, index))
            .Single(item => item.argument == Iw3CliContract.OutputSwitch)
            .index;
        return request.Arguments[outputSwitchIndex + 1];
    }

    private static string GetRequestedOutputPath(ProcessExecutionRequest request) =>
        request.Arguments.Contains(Iw3CliContract.OutputSwitch)
            ? GetProcessOutputPath(request)
            : request.Arguments[^1];

    private static string GetFfmpegInputPath(ProcessExecutionRequest request)
    {
        var inputSwitchIndex = request.Arguments
            .Select((argument, index) => (argument, index))
            .Single(item => item.argument == "-i")
            .index;
        return request.Arguments[inputSwitchIndex + 1];
    }

    private sealed class CapturingProgress : IProgress<ConversionExecutionProgressUpdate>
    {
        public List<ConversionExecutionProgressUpdate> Updates { get; } = [];

        public void Report(ConversionExecutionProgressUpdate value) =>
            Updates.Add(value);
    }

    private sealed class FakeProcessRunner : ILocalProcessRunner
    {
        private readonly Queue<ProcessExecutionResult> _results;
        private readonly ProcessOutputLine? _outputLineToReport;
        private readonly ProcessMetricSample? _metricSampleToReport;
        private readonly Action<ProcessExecutionRequest>? _onRun;

        public FakeProcessRunner(
            ProcessExecutionResult result,
            ProcessOutputLine? outputLineToReport = null,
            ProcessMetricSample? metricSampleToReport = null,
            Action<ProcessExecutionRequest>? onRun = null)
            : this([result], outputLineToReport, metricSampleToReport, onRun)
        {
        }

        public FakeProcessRunner(
            IReadOnlyList<ProcessExecutionResult> results,
            ProcessOutputLine? outputLineToReport = null,
            ProcessMetricSample? metricSampleToReport = null,
            Action<ProcessExecutionRequest>? onRun = null)
        {
            _results = new(results);
            _outputLineToReport = outputLineToReport;
            _metricSampleToReport = metricSampleToReport;
            _onRun = onRun;
        }

        public int RunCallCount { get; private set; }

        public List<ProcessExecutionRequest> Requests { get; } = [];

        public ProcessExecutionRequest? LastRequest => Requests.LastOrDefault();

        public Task<ProcessExecutionResult> RunAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            RunCallCount++;
            Requests.Add(request);
            _onRun?.Invoke(request);
            if (_outputLineToReport is not null)
            {
                request.OutputProgress?.Report(_outputLineToReport);
            }

            if (_metricSampleToReport is not null)
            {
                request.MetricsProgress?.Report(_metricSampleToReport);
            }

            return Task.FromResult(
                _results.Count == 0
                    ? CompletedProcess()
                    : _results.Dequeue());
        }
    }

    private sealed class CancelAwareProcessRunner(
        Action<ProcessExecutionRequest>? onRun = null) : ILocalProcessRunner
    {
        public TaskCompletionSource<bool> Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int RunCallCount { get; private set; }

        public bool ObservedCancellation { get; private set; }

        public async Task<ProcessExecutionResult> RunAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            RunCallCount++;
            onRun?.Invoke(request);
            Started.TrySetResult(true);

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ObservedCancellation = true;
                return CanceledProcess();
            }

            return CompletedProcess();
        }
    }

    private sealed class FakeConversionOutputFileService : IConversionOutputFileService
    {
        private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);

        public List<(string SourcePath, string DestinationPath, bool Overwrite)> Moves { get; } = [];

        public bool Exists(string path) => _paths.Contains(path);

        public void DeleteIfExists(string path) => _paths.Remove(path);

        public void Move(string sourcePath, string destinationPath, bool overwrite)
        {
            if (!_paths.Contains(sourcePath))
            {
                throw new FileNotFoundException("Source file was not found.", sourcePath);
            }

            if (!overwrite && _paths.Contains(destinationPath))
            {
                throw new IOException("Destination already exists.");
            }

            _paths.Remove(sourcePath);
            _paths.Add(destinationPath);
            Moves.Add((sourcePath, destinationPath, overwrite));
        }

        public void Add(string path) => _paths.Add(path);
    }
}
