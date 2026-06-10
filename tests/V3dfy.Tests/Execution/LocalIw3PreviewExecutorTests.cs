using V3dfy.Core.Models;
using V3dfy.Core.Execution;
using V3dfy.Core.Planning;
using V3dfy.Core.Presets;
using V3dfy.Core.Preview;
using V3dfy.Core.Processes;
using V3dfy.Engine.Iw3.Commands;
using V3dfy.Engine.Iw3.Execution;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Tests.Execution;

public sealed class LocalIw3PreviewExecutorTests
{
    private static readonly InternalToolPaths Paths = TestPaths.InternalToolPaths();

    [Fact]
    public async Task ExecuteAsync_BuildsShortSourceFfmpegRequestBeforeIw3PreviewRequest()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(files);
        var executor = new LocalIw3PreviewExecutor(runner, files);

        var result = await executor.ExecuteAsync(CreateRequest());

        Assert.True(result.Success);
        Assert.Equal(2, runner.Requests.Count);
        Assert.Equal(Paths.FfmpegExecutable, runner.Requests[0].ExecutablePath);
        Assert.Equal(Paths.PythonExecutable, runner.Requests[1].ExecutablePath);
        Assert.Contains("-ss", runner.Requests[0].Arguments);
        Assert.Contains("00:10:00", runner.Requests[0].Arguments);
        Assert.Contains("-t", runner.Requests[0].Arguments);
        Assert.Contains("00:00:15", runner.Requests[0].Arguments);
        Assert.DoesNotContain("0", runner.Requests[0].Arguments);
        Assert.Contains("0:v:0", runner.Requests[0].Arguments);
        Assert.Contains("0:a:0?", runner.Requests[0].Arguments);
        Assert.Contains("-sn", runner.Requests[0].Arguments);
        Assert.Contains("-dn", runner.Requests[0].Arguments);
        Assert.Contains("copy", runner.Requests[0].Arguments);
        Assert.Contains("make_zero", runner.Requests[0].Arguments);
        Assert.Contains(Iw3CliContract.DepthModelSwitch, runner.Requests[1].Arguments);
        Assert.Contains(Iw3DepthModelMapper.ZoeDAnyNDepthModelName, runner.Requests[1].Arguments);
        Assert.DoesNotContain("--preset", runner.Requests[1].Arguments);
        Assert.DoesNotContain("--crf", runner.Requests[1].Arguments);
    }

    [Fact]
    public async Task ExecuteAsync_AttachesMetricsProgressToPreviewProcesses()
    {
        var metricSample = new ProcessMetricSample(
            CapturedAt: DateTimeOffset.UtcNow,
            CpuUsagePercent: 42.5,
            WorkingSetBytes: 512 * 1024 * 1024,
            PrivateMemoryBytes: 384 * 1024 * 1024,
            GpuUsagePercent: 31.2,
            GpuStatus: ProcessGpuMetricReading.AdapterGpuUsageStatus,
            GpuScope: ProcessGpuMetricScope.Adapter,
            GpuDedicatedMemoryBytes: 2L * 1024 * 1024 * 1024);
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(
            files,
            metricSampleToReport: metricSample);
        var executor = new LocalIw3PreviewExecutor(runner, files);
        var progress = new CapturingProgress();

        await executor.ExecuteAsync(CreateRequest(), progress);

        Assert.All(runner.Requests, request => Assert.NotNull(request.MetricsProgress));
        Assert.Contains(progress.Updates, update => update.Metrics == metricSample);
        Assert.Contains(progress.Updates, update =>
            update.CurrentStep.EnglishText.Contains("source clip", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(progress.Updates, update =>
            update.CurrentStep.EnglishText.Contains("iw3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_RuntimeDownloadFromIw3Output_AddsOfflineWarning()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(
            files,
            secondStandardError:
                "Downloading: \"https://github.com/nagadomi/nunif/releases/download/0.0.0/iw3_row_flow_v3_20250627\"");
        var executor = new LocalIw3PreviewExecutor(runner, files);

        var result = await executor.ExecuteAsync(CreateRequest());

        Assert.True(result.Success);
        Assert.Contains(Iw3RuntimeDownloadDetector.EnglishWarning, result.EnglishSummary);
        Assert.Contains(Iw3RuntimeDownloadDetector.EnglishWarning, result.Logs.Select(log => log.EnglishMessage));
        Assert.Contains(Iw3RuntimeDownloadDetector.EnglishTimingNote, result.Logs.Select(log => log.EnglishMessage));
        Assert.Contains("Runtime download detected during iw3 startup", result.EnglishSummary);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsPreviewTimingCheckpointsInOrder()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(
            files,
            secondStandardError: "loading model\r\nframe=1 fps=0.5");
        var executor = new LocalIw3PreviewExecutor(runner, files);

        var result = await executor.ExecuteAsync(CreateRequest());

        var messages = result.Logs.Select(log => log.EnglishMessage).ToArray();
        AssertContainsInOrder(
            messages,
            "Timing checkpoint: source clip generation started.",
            "Timing checkpoint: source clip generation completed.",
            "Timing checkpoint: iw3 process starting.",
            "Timing checkpoint: first iw3 output received.",
            "Timing checkpoint: first iw3 frame/progress line received.",
            "Timing checkpoint: iw3 preview completed.",
            "Preview timings:");
        Assert.Contains("Preview timings: source clip", result.EnglishSummary);
        Assert.Contains("iw3 startup", result.EnglishSummary);
        Assert.Contains("conversion", result.EnglishSummary);
    }

    [Fact]
    public async Task ExecuteAsync_FfmpegSourceClipRequestExcludesUnsupportedStreams()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(files);
        var executor = new LocalIw3PreviewExecutor(runner, files);

        var result = await executor.ExecuteAsync(CreateRequest());

        var ffmpegArguments = runner.Requests[0].Arguments;
        AssertDoesNotContainAdjacentArguments(ffmpegArguments, "-map", "0");
        AssertContainsAdjacentArguments(ffmpegArguments, "-map", "0:v:0");
        AssertContainsAdjacentArguments(ffmpegArguments, "-map", "0:a:0?");
        Assert.Contains("-sn", ffmpegArguments);
        Assert.Contains("-dn", ffmpegArguments);
        AssertContainsAdjacentArguments(ffmpegArguments, "-map_metadata", "-1");
        AssertContainsAdjacentArguments(ffmpegArguments, "-map_chapters", "-1");
        AssertContainsAdjacentArguments(ffmpegArguments, "-c", "copy");
        AssertContainsAdjacentArguments(ffmpegArguments, "-avoid_negative_ts", "make_zero");
        Assert.DoesNotContain("libx264", ffmpegArguments);
        Assert.DoesNotContain("aac", ffmpegArguments);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "Preview source clip diagnostics: mode fast stream-copy",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "uses input seeking before -i",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_FfmpegSourceClipFallsBackToSafeReencodeWhenFastCopyFails()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(
            files,
            results:
            [
                CompletedProcess(exitCode: 1, standardError: "stream copy failed"),
                CompletedProcess(),
                CompletedProcess(),
            ]);
        var executor = new LocalIw3PreviewExecutor(runner, files);

        var result = await executor.ExecuteAsync(CreateRequest());

        Assert.True(result.Success);
        Assert.Equal(3, runner.Requests.Count);
        Assert.Equal(Paths.FfmpegExecutable, runner.Requests[0].ExecutablePath);
        Assert.Equal(Paths.FfmpegExecutable, runner.Requests[1].ExecutablePath);
        Assert.Equal(Paths.PythonExecutable, runner.Requests[2].ExecutablePath);
        var fallbackArguments = runner.Requests[1].Arguments;
        AssertContainsAdjacentArguments(fallbackArguments, "-map", "0:v:0");
        AssertContainsAdjacentArguments(fallbackArguments, "-map", "0:a:0?");
        AssertContainsAdjacentArguments(fallbackArguments, "-c:v", "libx264");
        AssertContainsAdjacentArguments(fallbackArguments, "-preset", "veryfast");
        AssertContainsAdjacentArguments(fallbackArguments, "-crf", "23");
        AssertContainsAdjacentArguments(fallbackArguments, "-pix_fmt", "yuv420p");
        AssertContainsAdjacentArguments(fallbackArguments, "-c:a", "aac");
        AssertContainsAdjacentArguments(fallbackArguments, "-b:a", "160k");
        AssertContainsAdjacentArguments(fallbackArguments, "-ac", "2");
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains("retrying with safe reencode", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_Iw3PreviewRequestUsesSameBundledRuntimeSetupAsFinalConversion()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(files);
        var executor = new LocalIw3PreviewExecutor(runner, files);

        await executor.ExecuteAsync(CreateRequest());

        var previewIw3Request = runner.Requests.Single(request =>
            request.ExecutablePath == Paths.PythonExecutable);
        var finalIw3Request = new LocalIw3ProcessRequestBuilder().Build(
            CreateReadyConversionRequest());

        Assert.Equal(finalIw3Request.ExecutablePath, previewIw3Request.ExecutablePath);
        Assert.Equal(finalIw3Request.WorkingDirectory, previewIw3Request.WorkingDirectory);
        Assert.Equal(finalIw3Request.AllowedRootDirectory, previewIw3Request.AllowedRootDirectory);
        Assert.Equal(finalIw3Request.EnvironmentVariables, previewIw3Request.EnvironmentVariables);
        Assert.Equal(Paths.NunifRootDirectory, previewIw3Request.EnvironmentVariables?["NUNIF_HOME"]);
        Assert.Equal("1", previewIw3Request.EnvironmentVariables?["PYTHONNOUSERSITE"]);
        Assert.Equal(Paths.ModelsDirectory, previewIw3Request.EnvironmentVariables?["TORCH_HOME"]);
    }

    [Fact]
    public async Task ExecuteAsync_Iw3PreviewRequestKeepsCommandFlagsEquivalentToFinalConversion()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(files);
        var executor = new LocalIw3PreviewExecutor(runner, files);

        await executor.ExecuteAsync(CreateRequest());

        var previewIw3Request = runner.Requests.Single(request =>
            request.ExecutablePath == Paths.PythonExecutable);
        var finalIw3Request = new LocalIw3ProcessRequestBuilder().Build(
            CreateReadyConversionRequest());

        Assert.Equal(
            NormalizeInputOutputArguments(finalIw3Request.Arguments),
            NormalizeInputOutputArguments(previewIw3Request.Arguments));
    }

    [Fact]
    public async Task ExecuteAsync_Iw3PreviewRequestDoesNotAddPreviewOnlySpeedFlagsFromCapabilities()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(files);
        var executor = new LocalIw3PreviewExecutor(runner, files);

        await executor.ExecuteAsync(CreateRequest(
            capabilities: new(
                ManifestPath: Paths.Iw3CliCapabilitiesFile,
                Status: Iw3CliCapabilitiesStatus.Found,
                ErrorMessage: null,
                BundledIw3Version: "nunif-d23721f1",
                VerifiedBaseCommand: true,
                VerifiedOptions: ["-i", "-o", "--preset", "--crf"],
                UnverifiedOptions: [],
                VerificationSource: "python -m iw3 -h",
                VerifiedAtUtc: "2026-06-04T00:00:00Z",
                Notes: "Verified during bundle preparation.")));

        var iw3Arguments = runner.Requests.Single(request =>
            request.ExecutablePath == Paths.PythonExecutable).Arguments;
        Assert.DoesNotContain("--preset", iw3Arguments);
        Assert.DoesNotContain("--crf", iw3Arguments);
        Assert.DoesNotContain("--max-fps", iw3Arguments);
    }

    [Fact]
    public async Task ExecuteAsync_Iw3PreviewLogsCommandAndTimingDiagnostics()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(
            files,
            results:
            [
                CompletedProcess(),
                new(
                    ExitCode: 0,
                    StandardOutput: string.Empty,
                    StandardError: string.Empty,
                    OutputLines:
                    [
                        new(
                            ProcessOutputStream.StandardError,
                            "1/367 [00:05<34:00, 5.67s/it]",
                            startedAt.AddSeconds(6)),
                    ],
                    Status: ProcessExecutionStatus.Completed,
                    StartedAt: startedAt,
                    EndedAt: startedAt.AddMinutes(2)),
            ]);
        var executor = new LocalIw3PreviewExecutor(runner, files);

        var result = await executor.ExecuteAsync(CreateRequest());

        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "Preview iw3 diagnostics: sanitized command line:",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                $"Python executable path: {Paths.PythonExecutable}",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                $"working directory: {Paths.NunifRootDirectory}",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains("NUNIF_HOME=", StringComparison.Ordinal) &&
                log.EnglishMessage.Contains("TORCH_HOME=", StringComparison.Ordinal) &&
                log.EnglishMessage.Contains("PYTHONNOUSERSITE=1", StringComparison.Ordinal));
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "iw3 depth model value: ZoeD_Any_N",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "layout flag: --half-tb",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "encoder flags set by v3dfy: none",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "Preview iw3 timing diagnostics:",
                StringComparison.Ordinal) &&
                log.EnglishMessage.Contains(
                    "first process output",
                    StringComparison.Ordinal) &&
                log.EnglishMessage.Contains(
                    "parsed frame count 367",
                    StringComparison.Ordinal) &&
                log.EnglishMessage.Contains(
                "average throughput",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_PreviewProcessOutputPathsStayInsidePreviewCache()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(files);
        var executor = new LocalIw3PreviewExecutor(runner, files);
        var request = CreateRequest();

        await executor.ExecuteAsync(request);

        var ffmpegRequest = runner.Requests.Single(processRequest =>
            processRequest.ExecutablePath == Paths.FfmpegExecutable);
        var iw3Request = runner.Requests.Single(processRequest =>
            processRequest.ExecutablePath == Paths.PythonExecutable);
        var ffmpegOutputPath = ffmpegRequest.Arguments.Last();
        var iw3OutputPath = GetArgumentAfter(iw3Request.Arguments, Iw3CliContract.OutputSwitch);

        Assert.True(PreviewCachePathSafety.IsPathInsideRoot(request.CachePaths.CacheDirectory, ffmpegOutputPath));
        Assert.True(PreviewCachePathSafety.IsPathInsideRoot(request.CachePaths.CacheDirectory, iw3OutputPath));
        Assert.All(
            new[] { ffmpegOutputPath, iw3OutputPath },
            path =>
            {
                Assert.False(PreviewCachePathSafety.IsPathInsideRoot(TestPaths.SourceRoot(), path));
                Assert.False(PreviewCachePathSafety.IsPathInsideRoot(TestPaths.OutputRoot(), path));
                Assert.False(PreviewCachePathSafety.IsPathInsideRoot(Directory.GetCurrentDirectory(), path));
            });
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotTouchSourceFolderForPreviewTempGeneration()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(files);
        var executor = new LocalIw3PreviewExecutor(runner, files);
        var request = CreateRequest();

        await executor.ExecuteAsync(request);

        Assert.All(files.DeletedPaths, path =>
            Assert.False(PreviewCachePathSafety.IsPathInsideRoot(TestPaths.SourceRoot(), path)));
        Assert.All(files.Moves.SelectMany(move => new[] { move.SourcePath, move.DestinationPath }), path =>
            Assert.True(PreviewCachePathSafety.IsPathInsideRoot(request.CachePaths.CacheDirectory, path)));
    }

    [Fact]
    public async Task ExecuteAsync_RejectsPreviewPathsOutsideCacheBeforeStartingProcesses()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(files);
        var executor = new LocalIw3PreviewExecutor(runner, files);
        var request = CreateRequest(
            cachePaths: new(
                CacheDirectory: TestPaths.PreviewCacheRoot(),
                ShortSourcePath: TestPaths.PreviewCacheRoot("Movie.source.mkv"),
                PartialShortSourcePath: TestPaths.PreviewCacheRoot("Movie.source.v3dfy-partial.mkv"),
                PreviewOutputPath: TestPaths.PreviewCacheRoot("Movie.preview.mp4"),
                PartialPreviewOutputPath: TestPaths.OutputRoot("Movie.v3dfy.3d.htab.v3dfy-partial.mp4")));

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Empty(runner.Requests);
        Assert.Empty(files.DeletedPaths);
        Assert.Empty(files.Moves);
        Assert.Contains("outside the preview cache", result.EnglishSummary);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                TestPaths.OutputRoot("Movie.v3dfy.3d.htab.v3dfy-partial.mp4"),
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_FfmpegSourceClipFailureLogsStderrAndSkipsIw3()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(
            files,
            results:
            [
                CompletedProcess(exitCode: 1, standardError:
                "Could not find tag for codec subrip in stream #3\r\n" +
                "Could not write header: Invalid argument"),
                CompletedProcess(exitCode: 1, standardError: "safe reencode failed"),
            ]);
        var executor = new LocalIw3PreviewExecutor(runner, files);

        var result = await executor.ExecuteAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(2, runner.Requests.Count);
        Assert.Contains("FFmpeg stderr was logged", result.EnglishSummary);
        Assert.Contains(result.Logs, log => log.EnglishMessage.Contains("stderr: Could not find tag"));
        Assert.Contains(result.Logs, log => log.EnglishMessage.Contains("stderr: Could not write header"));
    }

    [Fact]
    public async Task ExecuteAsync_PromotesPreviewOutputOnlyAfterIw3Success()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(files);
        var executor = new LocalIw3PreviewExecutor(runner, files);
        var request = CreateRequest();

        var result = await executor.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Equal(request.CachePaths.PreviewOutputPath, result.PreviewOutputPath);
        Assert.Contains(
            (request.CachePaths.PartialShortSourcePath, request.CachePaths.ShortSourcePath, true),
            files.Moves);
        Assert.Contains(
            (request.CachePaths.PartialPreviewOutputPath, request.CachePaths.PreviewOutputPath, true),
            files.Moves);
        Assert.False(files.Exists(request.CachePaths.PartialPreviewOutputPath));
    }

    [Fact]
    public async Task ExecuteAsync_SuccessDeletesCurrentAttemptPreviewPartialsAndTempFiles()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(files);
        var executor = new LocalIw3PreviewExecutor(runner, files);
        var request = CreateRequest();
        var tempPreviewPartialPath =
            TestPaths.PreviewCacheRoot("_tmp_success.preview.v3dfy-partial.mp4");
        files.AddEnumerated(tempPreviewPartialPath);

        var result = await executor.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.True(files.Exists(request.CachePaths.PreviewOutputPath));
        Assert.Contains(request.CachePaths.ShortSourcePath, files.DeletedPaths);
        Assert.Contains(request.CachePaths.PartialShortSourcePath, files.DeletedPaths);
        Assert.Contains(request.CachePaths.PartialPreviewOutputPath, files.DeletedPaths);
        Assert.Contains(tempPreviewPartialPath, files.DeletedPaths);
        Assert.DoesNotContain(request.CachePaths.PreviewOutputPath, files.DeletedExistingPaths);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotDeleteOldUnrelatedPreviewPartialDuringAttemptCleanup()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(
            files,
            secondResultStatus: ProcessExecutionStatus.Canceled);
        var executor = new LocalIw3PreviewExecutor(runner, files);
        var request = CreateRequest();
        var stalePartialPath =
            TestPaths.PreviewCacheRoot("_tmp_old.preview.v3dfy-partial.mp4");
        files.AddEnumerated(stalePartialPath, DateTimeOffset.UtcNow.AddHours(-1));

        var result = await executor.ExecuteAsync(request);

        Assert.True(result.WasCanceled);
        Assert.DoesNotContain(stalePartialPath, files.DeletedPaths);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDeletesPartialAndTempPreviewFiles()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(
            files,
            secondResultStatus: ProcessExecutionStatus.Canceled);
        var executor = new LocalIw3PreviewExecutor(runner, files);
        var request = CreateRequest();
        var iw3TempPartialPath =
            TestPaths.PreviewCacheRoot("_tmp_123.preview.v3dfy-partial.mp4");
        files.AddEnumerated(iw3TempPartialPath);

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.True(result.WasCanceled);
        Assert.Contains(request.CachePaths.PartialShortSourcePath, files.DeletedPaths);
        Assert.Contains(request.CachePaths.PartialPreviewOutputPath, files.DeletedPaths);
        Assert.Contains(request.CachePaths.ShortSourcePath, files.DeletedPaths);
        Assert.Contains(iw3TempPartialPath, files.DeletedPaths);
        Assert.DoesNotContain(request.CachePaths.PreviewOutputPath, files.Moves.Select(move => move.DestinationPath));
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringSourceClipDeletesSourcePartial()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(
            files,
            firstResultStatus: ProcessExecutionStatus.Canceled);
        var executor = new LocalIw3PreviewExecutor(runner, files);
        var request = CreateRequest();

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.True(result.WasCanceled);
        Assert.Single(runner.Requests);
        Assert.Contains(request.CachePaths.PartialShortSourcePath, files.DeletedPaths);
        Assert.DoesNotContain(request.CachePaths.PreviewOutputPath, files.Moves.Select(move => move.DestinationPath));
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancelableTokenToSourceClipAndIw3Processes()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(files);
        var executor = new LocalIw3PreviewExecutor(runner, files);
        var request = CreateRequest();
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await executor.ExecuteAsync(
            request,
            cancellationToken: cancellationTokenSource.Token);

        Assert.True(result.Success);
        Assert.Equal(2, runner.CancellationTokens.Count);
        Assert.All(runner.CancellationTokens, token => Assert.True(token.CanBeCanceled));
    }

    [Fact]
    public async Task ExecuteAsync_UnmappedSelectedModelDoesNotRunProcesses()
    {
        var files = new FakePreviewCacheFileService();
        var runner = new FakePreviewProcessRunner(files);
        var executor = new LocalIw3PreviewExecutor(runner, files);
        var request = CreateRequest(selectedModel: new(
            "Unknown model",
            "unknown.pt",
            LocalModelPlanSource.UnmanagedLocalFile));

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Empty(runner.Requests);
        Assert.Contains("not mapped", result.EnglishSummary, StringComparison.OrdinalIgnoreCase);
    }

    private static Iw3PreviewGenerationRequest CreateRequest(
        LocalModelPlanSelection? selectedModel = null,
        Iw3CliCapabilitiesManifest? capabilities = null,
        PreviewCachePaths? cachePaths = null)
    {
        selectedModel ??= new(
            "Depth Anything Metric Indoor",
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
            LocalModelPlanSource.UnmanagedLocalFile,
            Id: Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
            FileName: "depth_anything_metric_depth_indoor.pt",
            Iw3DepthModelName: Iw3DepthModelMapper.ZoeDAnyNDepthModelName,
            MappingKey: Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey);

        return new(
            Configuration: new(
                SourcePath: TestPaths.SourceRoot("Movie.mp4"),
                OutputProfileName: "LG 3D Full HD 2012",
                OutputContainer: OutputContainer.MP4,
                QualityPreset: AiQualityPreset.Balanced,
                Intensity: ThreeDIntensity.Medium,
                ThreeDOutputFormat: ThreeDOutputFormat.HalfTopBottom,
                ModelKey: Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey,
                ModelDisplayName: "Depth Anything Metric Indoor",
                ModelRelativePath: Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
                Iw3DepthModelName: Iw3DepthModelMapper.ZoeDAnyNDepthModelName,
                PreviewStartTime: TimeSpan.FromMinutes(10),
                PreviewDuration: TimeSpan.FromSeconds(15)),
            CachePaths: cachePaths ?? new(
                CacheDirectory: TestPaths.PreviewCacheRoot(),
                ShortSourcePath: TestPaths.PreviewCacheRoot("Movie.source.mkv"),
                PartialShortSourcePath: TestPaths.PreviewCacheRoot("Movie.source.v3dfy-partial.mkv"),
                PreviewOutputPath: TestPaths.PreviewCacheRoot("Movie.preview.mp4"),
                PartialPreviewOutputPath: TestPaths.PreviewCacheRoot("Movie.preview.v3dfy-partial.mp4")),
            ExpectedToolPaths: Paths,
            SelectedLocalModel: selectedModel,
            Iw3CliCapabilities: capabilities);
    }

    private static ConversionExecutionRequest CreateReadyConversionRequest()
    {
        var selectedModel = new LocalModelPlanSelection(
            "Depth Anything Metric Indoor",
            Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
            LocalModelPlanSource.UnmanagedLocalFile,
            Id: Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorRelativePath,
            FileName: "depth_anything_metric_depth_indoor.pt",
            Iw3DepthModelName: Iw3DepthModelMapper.ZoeDAnyNDepthModelName,
            MappingKey: Iw3DepthModelMapper.DepthAnythingMetricDepthIndoorKey);
        var options = new VideoConversionPlanOptions(
            OutputContainer.MP4,
            AiQualityPreset.Balanced,
            ThreeDIntensity.Medium,
            ThreeDOutputFormat.HalfTopBottom);
        var plan = new VideoConversionPlan(
            SourcePath: TestPaths.SourceRoot("Movie.mp4"),
            SuggestedOutputPath: TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4"),
            OutputContainer: options.OutputContainer,
            VideoCodec: "H.264",
            AudioCodec: "AAC or AC3",
            Width: 1920,
            Height: 1080,
            ThreeDOutputFormat: options.ThreeDOutputFormat,
            QualityPreset: options.QualityPreset,
            Intensity: options.Intensity,
            Status: VideoConversionPlanStatus.Ready,
            DryRunReason: ConversionDryRunReason.None,
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
            SourcePath: plan.SourcePath,
            OutputPath: plan.SuggestedOutputPath,
            SelectedPreset: TargetDevicePresets.General3dVideo,
            Options: options,
            ExpectedToolPaths: Paths,
            SelectedLocalModel: selectedModel,
            CommandPreview: plan.CommandPreview,
            PlanStatus: plan.Status,
            DryRunReason: plan.DryRunReason,
            IsDryRun: false);
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

    private static void AssertContainsAdjacentArguments(
        IReadOnlyList<string> arguments,
        string name,
        string value)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (arguments[index] == name && arguments[index + 1] == value)
            {
                return;
            }
        }

        Assert.Fail(
            $"Expected adjacent arguments '{name} {value}' in: {string.Join(' ', arguments)}");
    }

    private static void AssertDoesNotContainAdjacentArguments(
        IReadOnlyList<string> arguments,
        string name,
        string value)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            Assert.False(
                arguments[index] == name && arguments[index + 1] == value,
                $"Did not expect adjacent arguments '{name} {value}' in: {string.Join(' ', arguments)}");
        }
    }

    private static IReadOnlyList<string> NormalizeInputOutputArguments(
        IReadOnlyList<string> arguments)
    {
        var normalized = arguments.ToArray();
        for (var index = 0; index < normalized.Length - 1; index++)
        {
            if (normalized[index] == Iw3CliContract.InputSwitch)
            {
                normalized[index + 1] = "<input>";
            }
            else if (normalized[index] == Iw3CliContract.OutputSwitch)
            {
                normalized[index + 1] = "<output>";
            }
        }

        return normalized;
    }

    private static string GetArgumentAfter(IReadOnlyList<string> arguments, string name)
    {
        var index = arguments
            .Select((argument, argumentIndex) => (argument, argumentIndex))
            .Single(item => item.argument == name)
            .argumentIndex;
        return arguments[index + 1];
    }

    private static void AssertContainsInOrder(
        IReadOnlyList<string> messages,
        params string[] expectedFragments)
    {
        var startIndex = 0;
        foreach (var expectedFragment in expectedFragments)
        {
            var foundIndex = -1;
            for (var index = startIndex; index < messages.Count; index++)
            {
                if (messages[index].Contains(expectedFragment, StringComparison.Ordinal))
                {
                    foundIndex = index;
                    break;
                }
            }

            Assert.True(
                foundIndex >= 0,
                $"Expected to find '{expectedFragment}' after index {startIndex} in: {string.Join(Environment.NewLine, messages)}");
            startIndex = foundIndex + 1;
        }
    }

    private sealed class FakePreviewProcessRunner : ILocalProcessRunner
    {
        private readonly FakePreviewCacheFileService _files;
        private readonly Queue<ProcessExecutionResult> _results;
        private readonly ProcessMetricSample? _metricSampleToReport;

        public FakePreviewProcessRunner(
            FakePreviewCacheFileService files,
            IReadOnlyList<ProcessExecutionResult>? results = null,
            ProcessExecutionStatus firstResultStatus = ProcessExecutionStatus.Completed,
            ProcessExecutionStatus secondResultStatus = ProcessExecutionStatus.Completed,
            int firstResultExitCode = 0,
            int secondResultExitCode = 0,
            string firstStandardError = "",
            string secondStandardError = "",
            ProcessMetricSample? metricSampleToReport = null)
        {
            _files = files;
            _metricSampleToReport = metricSampleToReport;
            _results = new(results ?? [
                CreateProcessResult(firstResultStatus, firstResultExitCode, firstStandardError),
                CreateProcessResult(secondResultStatus, secondResultExitCode, secondStandardError),
            ]);
        }

        public List<ProcessExecutionRequest> Requests { get; } = [];

        public List<CancellationToken> CancellationTokens { get; } = [];

        public Task<ProcessExecutionResult> RunAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            CancellationTokens.Add(cancellationToken);
            if (_metricSampleToReport is not null)
            {
                request.MetricsProgress?.Report(_metricSampleToReport);
            }

            var result = _results.Count == 0
                ? CompletedProcess()
                : _results.Dequeue();
            if (result.Status == ProcessExecutionStatus.Completed &&
                result.ExitCode == 0 &&
                string.Equals(request.ExecutablePath, Paths.FfmpegExecutable, StringComparison.OrdinalIgnoreCase))
            {
                _files.AddExisting(request.Arguments.Last());
            }
            else if (result.Status == ProcessExecutionStatus.Completed &&
                result.ExitCode == 0 &&
                string.Equals(request.ExecutablePath, Paths.PythonExecutable, StringComparison.OrdinalIgnoreCase))
            {
                _files.AddExisting(GetArgumentAfter(request.Arguments, Iw3CliContract.OutputSwitch));
            }

            return Task.FromResult(result);
        }

        private static ProcessExecutionResult CreateProcessResult(
            ProcessExecutionStatus status,
            int exitCode,
            string standardError)
        {
            var now = DateTimeOffset.UtcNow;
            return new(
                ExitCode: status == ProcessExecutionStatus.Completed ? exitCode : -1,
                StandardOutput: string.Empty,
                StandardError: standardError,
                OutputLines: [],
                Status: status,
                StartedAt: now,
                EndedAt: now);
        }

        private static string GetArgumentAfter(IReadOnlyList<string> arguments, string name)
        {
            var index = arguments
                .Select((argument, argumentIndex) => (argument, argumentIndex))
                .Single(item => item.argument == name)
                .argumentIndex;
            return arguments[index + 1];
        }
    }

    private sealed class CapturingProgress : IProgress<ConversionExecutionProgressUpdate>
    {
        public List<ConversionExecutionProgressUpdate> Updates { get; } = [];

        public void Report(ConversionExecutionProgressUpdate value) => Updates.Add(value);
    }

    private sealed class FakePreviewCacheFileService : IPreviewCacheFileService
    {
        private readonly HashSet<string> _existingPaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<PreviewCacheFile> _enumeratedFiles = [];

        public List<string> DeletedPaths { get; } = [];

        public List<string> DeletedExistingPaths { get; } = [];

        public List<(string SourcePath, string DestinationPath, bool Overwrite)> Moves { get; } = [];

        public void AddExisting(string path) => _existingPaths.Add(path);

        public void AddEnumerated(string path, DateTimeOffset? lastWriteTimeUtc = null) =>
            _enumeratedFiles.Add(new(path, lastWriteTimeUtc ?? DateTimeOffset.UtcNow));

        public void EnsureDirectory(string directory)
        {
        }

        public bool Exists(string path) => _existingPaths.Contains(path);

        public void DeleteIfExists(string path)
        {
            DeletedPaths.Add(path);
            if (_existingPaths.Remove(path))
            {
                DeletedExistingPaths.Add(path);
            }
        }

        public void Move(string sourcePath, string destinationPath, bool overwrite)
        {
            Moves.Add((sourcePath, destinationPath, overwrite));
            _existingPaths.Remove(sourcePath);
            _existingPaths.Add(destinationPath);
        }

        public IReadOnlyList<PreviewCacheFile> EnumerateFiles(string directory) => _enumeratedFiles;
    }
}
