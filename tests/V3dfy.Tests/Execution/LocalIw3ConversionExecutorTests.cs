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
    private static readonly InternalToolPaths Paths = TestPaths.InternalToolPaths();
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
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");
        var partialOutputPath = ConversionOutputFinalizer.CreatePartialOutputPath(
            finalOutputPath);
        var internalTempPartialPath = CreateInternalTempPartialOutputPath(partialOutputPath);
        var runner = new FakeProcessRunner(
            CompletedProcess(),
            onRun: request =>
            {
                files.Add(GetProcessOutputPath(request));
                files.Add(internalTempPartialPath);
            });
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(
            outputPath: finalOutputPath));

        Assert.True(result.Success);
        Assert.False(files.Exists(partialOutputPath));
        Assert.False(files.Exists(internalTempPartialPath));
        Assert.True(files.Exists(finalOutputPath));
        Assert.Contains((partialOutputPath, finalOutputPath, true), files.Moves);
        Assert.Contains(internalTempPartialPath, files.DeletedPaths);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains("Final output saved", StringComparison.Ordinal));
        Assert.Equal(partialOutputPath, GetProcessOutputPath(runner.LastRequest!));
    }

    [Fact]
    public void CreatePartialOutputPath_UsesTrackedTmpPartialBesideSelectedFinalOutput()
    {
        var finalOutputPath = TestPaths.OutputRoot(
            "converted",
            "Movie.v3dfy.3d.htab.mp4");

        var partialOutputPath = ConversionOutputFinalizer.CreatePartialOutputPath(
            finalOutputPath);

        Assert.Equal(
            TestPaths.OutputRoot(
                "converted",
                "_tmp_Movie.v3dfy.3d.htab.v3dfy-partial.mp4"),
            partialOutputPath);
    }

    [Fact]
    public void GetCurrentAttemptPartialCleanupCandidates_IncludesTrackedDoubleTempAndLegacyOnly()
    {
        var finalOutputPath = TestPaths.OutputRoot(
            "converted",
            "Movie.v3dfy.3d.htab.mp4");
        var trackedPartialPath = ConversionOutputFinalizer.CreatePartialOutputPath(
            finalOutputPath);

        var candidates = ConversionOutputFinalizer.GetCurrentAttemptPartialCleanupCandidates(
            finalOutputPath,
            trackedPartialPath);

        Assert.Contains(trackedPartialPath, candidates);
        Assert.Contains(CreateInternalTempPartialOutputPath(trackedPartialPath), candidates);
        Assert.Contains(
            TestPaths.OutputRoot(
                "converted",
                "Movie.v3dfy.3d.htab.v3dfy-partial.mp4"),
            candidates);
        Assert.DoesNotContain(finalOutputPath, candidates);
        Assert.DoesNotContain(
            TestPaths.OutputRoot(
                "other",
                "_tmp__tmp_Movie.v3dfy.3d.htab.v3dfy-partial.mp4"),
            candidates);
    }

    [Fact]
    public async Task ExecuteAsync_FinalConversionStillUsesSelectedOutputDirectoryForPartialOutput()
    {
        var files = new FakeConversionOutputFileService();
        var finalOutputPath = TestPaths.OutputRoot("converted", "Movie.v3dfy.3d.htab.mp4");
        var partialOutputPath = ConversionOutputFinalizer.CreatePartialOutputPath(finalOutputPath);
        var runner = new FakeProcessRunner(
            CompletedProcess(),
            onRun: request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(outputPath: finalOutputPath));

        Assert.True(result.Success);
        Assert.Equal(finalOutputPath, result.PrimaryOutputPath);
        Assert.Equal(partialOutputPath, GetProcessOutputPath(runner.LastRequest!));
        Assert.StartsWith(
            TestPaths.OutputRoot("converted") + Path.DirectorySeparatorChar,
            partialOutputPath,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\AppData\Local\v3dfy\previews\", partialOutputPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_CleansStaleConversionPartialsMatchingSelectedOutputBeforeProcessStarts()
    {
        var files = new FakeConversionOutputFileService();
        var sourcePath = TestPaths.SourceRoot("Movie.mp4");
        var finalOutputPath = TestPaths.OutputRoot("converted", "Movie.v3dfy.3d.htab.mp4");
        var staleCurrentPatternPartialPath =
            ConversionOutputFinalizer.CreatePartialOutputPath(finalOutputPath);
        var staleLegacyPartialPath = TestPaths.OutputRoot(
            "converted",
            "Movie.v3dfy.3d.htab.v3dfy-partial.mp4");
        var staleTempPartialPath = TestPaths.OutputRoot(
            "converted",
            "_tmp_old_Movie.v3dfy.3d.htab.v3dfy-partial.mp4");
        var staleDoubleTempPartialPath =
            CreateInternalTempPartialOutputPath(staleCurrentPatternPartialPath);
        files.Add(sourcePath);
        files.Add(staleCurrentPatternPartialPath);
        files.Add(staleLegacyPartialPath);
        files.Add(staleTempPartialPath);
        files.Add(staleDoubleTempPartialPath);
        var staleFilesDeletedBeforeRun = false;
        var runner = new FakeProcessRunner(
            CompletedProcess(),
            onRun: request =>
            {
                staleFilesDeletedBeforeRun =
                    !files.Exists(staleCurrentPatternPartialPath) &&
                    !files.Exists(staleLegacyPartialPath) &&
                    !files.Exists(staleTempPartialPath) &&
                    !files.Exists(staleDoubleTempPartialPath);
                files.Add(GetProcessOutputPath(request));
            });
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(
            sourcePath: sourcePath,
            outputPath: finalOutputPath));

        Assert.True(result.Success);
        Assert.True(staleFilesDeletedBeforeRun);
        Assert.Contains(staleCurrentPatternPartialPath, files.DeletedPaths);
        Assert.Contains(staleLegacyPartialPath, files.DeletedPaths);
        Assert.Contains(staleTempPartialPath, files.DeletedPaths);
        Assert.Contains(staleDoubleTempPartialPath, files.DeletedPaths);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage == "Stale conversion partial file was cleaned.");
    }

    [Fact]
    public async Task ExecuteAsync_StaleConversionCleanupDoesNotDeleteSourceFinalOrUnrelatedFiles()
    {
        var files = new FakeConversionOutputFileService();
        var sourcePath = TestPaths.SourceRoot("Movie.mp4");
        var finalOutputPath = TestPaths.OutputRoot("converted", "Movie.v3dfy.3d.htab.mp4");
        var unrelatedSameFolder = TestPaths.OutputRoot("converted", "Unrelated.v3dfy-partial.mp4");
        var unrelatedDifferentOutputBase = TestPaths.OutputRoot(
            "converted",
            "_tmp_OtherMovie.v3dfy.3d.htab.v3dfy-partial.mp4");
        var nestedStalePartial = TestPaths.OutputRoot(
            "converted",
            "nested",
            "Movie.v3dfy.3d.htab.v3dfy-partial.mp4");
        files.Add(sourcePath);
        files.Add(finalOutputPath);
        files.Add(unrelatedSameFolder);
        files.Add(unrelatedDifferentOutputBase);
        files.Add(nestedStalePartial);
        var runner = new FakeProcessRunner(
            CompletedProcess(),
            onRun: request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(
            sourcePath: sourcePath,
            outputPath: finalOutputPath));

        Assert.True(result.Success);
        Assert.True(files.Exists(sourcePath));
        Assert.True(files.Exists(unrelatedSameFolder));
        Assert.True(files.Exists(unrelatedDifferentOutputBase));
        Assert.True(files.Exists(nestedStalePartial));
        Assert.DoesNotContain(sourcePath, files.DeletedPaths);
        Assert.DoesNotContain(finalOutputPath, files.DeletedPaths);
        Assert.DoesNotContain(unrelatedSameFolder, files.DeletedPaths);
        Assert.DoesNotContain(unrelatedDifferentOutputBase, files.DeletedPaths);
        Assert.DoesNotContain(nestedStalePartial, files.DeletedPaths);
    }

    [Fact]
    public void CleanStalePartialOutputs_SkipsActiveCurrentAttemptPartial()
    {
        var files = new FakeConversionOutputFileService();
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");
        var activePartialPath = ConversionOutputFinalizer.CreatePartialOutputPath(finalOutputPath);
        var activeInternalTempPartialPath = CreateInternalTempPartialOutputPath(activePartialPath);
        files.Add(activePartialPath);
        files.Add(activeInternalTempPartialPath);
        var finalizer = new ConversionOutputFinalizer(files);

        var logs = finalizer.CleanStalePartialOutputs(finalOutputPath, activePartialPath);

        Assert.True(files.Exists(activePartialPath));
        Assert.True(files.Exists(activeInternalTempPartialPath));
        Assert.DoesNotContain(activePartialPath, files.DeletedPaths);
        Assert.DoesNotContain(activeInternalTempPartialPath, files.DeletedPaths);
        Assert.Empty(logs);
    }

    [Fact]
    public void CleanStalePartialOutputs_LockedStalePartialLogsWarningAndDoesNotThrow()
    {
        var files = new FakeConversionOutputFileService();
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");
        var stalePartialPath = ConversionOutputFinalizer.CreatePartialOutputPath(finalOutputPath);
        files.Add(stalePartialPath);
        files.FailDeletes(stalePartialPath, new IOException("locked"));
        var finalizer = new ConversionOutputFinalizer(files);

        var logs = finalizer.CleanStalePartialOutputs(finalOutputPath);

        Assert.True(files.Exists(stalePartialPath));
        Assert.Contains(
            logs,
            log => log.EnglishMessage.StartsWith(
                "Could not delete stale partial file.",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenLgCompatibilityCopyEnabled_RunsFfmpegAfterPrimaryPromotion()
    {
        var files = new FakeConversionOutputFileService();
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.hsbs.mp4");
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
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.hsbs.mp4");
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
    public async Task ExecuteAsync_WhenLgCompatibilityCopyCancellationThrows_CleansCompatibilityPartial()
    {
        var files = new FakeConversionOutputFileService();
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.hsbs.mp4");
        var compatibilityOutputPath =
            LgCompatibilityCopyRequestBuilder.CreateCompatibilityOutputPath(
                finalOutputPath,
                ThreeDOutputFormat.HalfSideBySide);
        var compatibilityPartialPath =
            ConversionOutputFinalizer.CreatePartialOutputPath(compatibilityOutputPath);
        var untrackedOutput = TestPaths.OutputRoot("untracked-lg-copy.v3dfy-partial.mp4");
        files.Add(untrackedOutput);
        var runner = new ScriptedProcessRunner(
            [
                CompletedProcess(),
                new OperationCanceledException("LG copy was canceled."),
            ],
            request => files.Add(GetRequestedOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(
            outputPath: finalOutputPath,
            outputFormat: ThreeDOutputFormat.HalfSideBySide,
            createLgCompatibilityCopy: true));

        Assert.False(result.Success);
        Assert.True(result.WasCanceled);
        Assert.Equal(2, runner.RunCallCount);
        Assert.True(files.Exists(finalOutputPath));
        Assert.False(files.Exists(compatibilityOutputPath));
        Assert.False(files.Exists(compatibilityPartialPath));
        Assert.True(files.Exists(untrackedOutput));
        Assert.Contains(compatibilityPartialPath, files.DeletedPaths);
        Assert.DoesNotContain(finalOutputPath, files.DeletedPaths);
        Assert.DoesNotContain(compatibilityOutputPath, files.DeletedPaths);
        Assert.DoesNotContain(untrackedOutput, files.DeletedPaths);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage == "Conversion partial file was cleaned.");
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
    public async Task ExecuteAsync_RuntimeDownloadFromIw3Output_AddsOfflineWarning()
    {
        var files = new FakeConversionOutputFileService();
        var runner = new FakeProcessRunner(
            CompletedProcess(
                standardError:
                    "Downloading: \"https://github.com/nagadomi/nunif/releases/download/0.0.0/iw3_row_flow_v3_20250627\""),
            onRun: request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest());

        Assert.True(result.Success);
        Assert.Contains(Iw3RuntimeDownloadDetector.EnglishWarning, result.EnglishSummary);
        Assert.Contains(Iw3RuntimeDownloadDetector.EnglishWarning, result.Logs.Select(log => log.EnglishMessage));
        Assert.Contains(Iw3RuntimeDownloadDetector.EnglishTimingNote, result.Logs.Select(log => log.EnglishMessage));
    }

    [Fact]
    public async Task ExecuteAsync_LogsCommandAndTimingDiagnostics()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var files = new FakeConversionOutputFileService();
        var processResult = new ProcessExecutionResult(
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
            EndedAt: startedAt.AddMinutes(2));
        var runner = new FakeProcessRunner(
            processResult,
            onRun: request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest());

        Assert.True(result.Success);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "Convert iw3 diagnostics: sanitized command line:",
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
                "Convert iw3 timing diagnostics:",
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
    public async Task ExecuteAsync_LiveRuntimeDownloadLine_ReportsOfflineWarningThroughProgress()
    {
        var outputLine = new ProcessOutputLine(
            ProcessOutputStream.StandardError,
            "Downloading: \"https://github.com/nagadomi/nunif/releases/download/0.0.0/runtime.zip\"",
            DateTimeOffset.UtcNow);
        var files = new FakeConversionOutputFileService();
        var runner = new FakeProcessRunner(
            CompletedProcess(),
            outputLineToReport: outputLine,
            onRun: request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);
        var progress = new CapturingProgress();

        var result = await executor.ExecuteAsync(CreateReadyRequest(), progress);

        Assert.True(result.Success);
        Assert.Contains(
            progress.Updates,
            update => update.OutputLine == outputLine &&
                update.DetailEnglish == Iw3RuntimeDownloadDetector.EnglishWarning);
        Assert.Contains(Iw3RuntimeDownloadDetector.EnglishWarning, result.EnglishSummary);
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
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");
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
            log => log.EnglishMessage == "Conversion partial file was cleaned.");
    }

    [Fact]
    public async Task ExecuteAsync_FakeRunnerReturnsCanceled_ReportsCancellation()
    {
        var files = new FakeConversionOutputFileService();
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");
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
            log => log.EnglishMessage == "Conversion partial file was cleaned.");
        Assert.Equal(1, runner.RunCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessRunnerThrowsCancellation_CleansTrackedPartialAndReturnsCanceled()
    {
        var files = new FakeConversionOutputFileService();
        var sourcePath = TestPaths.SourceRoot("Movie.mp4");
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");
        var partialOutputPath = ConversionOutputFinalizer.CreatePartialOutputPath(finalOutputPath);
        var untrackedOutput = TestPaths.OutputRoot("other.v3dfy-partial.mp4");
        files.Add(sourcePath);
        files.Add(finalOutputPath);
        files.Add(untrackedOutput);
        var runner = new ScriptedProcessRunner(
            [new OperationCanceledException("Process runner surfaced cancellation.")],
            request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(
            sourcePath: sourcePath,
            outputPath: finalOutputPath));

        Assert.False(result.Success);
        Assert.True(result.WasCanceled);
        Assert.Equal("Local iw3 conversion was canceled.", result.EnglishSummary);
        Assert.Equal(1, runner.RunCallCount);
        Assert.Equal(partialOutputPath, GetProcessOutputPath(runner.Requests.Single()));
        Assert.False(files.Exists(partialOutputPath));
        Assert.True(files.Exists(sourcePath));
        Assert.True(files.Exists(finalOutputPath));
        Assert.True(files.Exists(untrackedOutput));
        Assert.Contains(partialOutputPath, files.DeletedPaths);
        Assert.DoesNotContain(sourcePath, files.DeletedPaths);
        Assert.DoesNotContain(finalOutputPath, files.DeletedPaths);
        Assert.DoesNotContain(untrackedOutput, files.DeletedPaths);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage == "Conversion partial file was cleaned.");
        Assert.DoesNotContain(
            result.Logs,
            log => log.EnglishMessage.StartsWith("stderr:", StringComparison.OrdinalIgnoreCase) ||
                log.EnglishMessage.StartsWith("stdout:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_CanceledConversionDeletesTrackedAndInternalDoubleTempCurrentAttemptPartials()
    {
        var files = new FakeConversionOutputFileService();
        var sourcePath = TestPaths.SourceRoot("Movie.mp4");
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");
        var partialOutputPath = ConversionOutputFinalizer.CreatePartialOutputPath(finalOutputPath);
        var internalTempPartialPath = CreateInternalTempPartialOutputPath(partialOutputPath);
        var untrackedOutput = TestPaths.OutputRoot("untracked.v3dfy-partial.mp4");
        var unrelatedOtherMovie = TestPaths.OutputRoot(
            "_tmp__tmp_OtherMovie.v3dfy.3d.htab.v3dfy-partial.mp4");
        var unrelatedPartialName = TestPaths.OutputRoot("Movie.partial.tmp");
        var nestedPartial = TestPaths.OutputRoot(
            "nested",
            Path.GetFileName(internalTempPartialPath));
        var otherDirectoryPartial = TestPaths.OutputRoot(
            "other",
            Path.GetFileName(internalTempPartialPath));
        files.Add(sourcePath);
        files.Add(finalOutputPath);
        files.Add(untrackedOutput);
        files.Add(unrelatedOtherMovie);
        files.Add(unrelatedPartialName);
        files.Add(nestedPartial);
        files.Add(otherDirectoryPartial);
        var runner = new FakeProcessRunner(
            CanceledProcess(),
            onRun: request =>
            {
                files.Add(GetProcessOutputPath(request));
                files.Add(internalTempPartialPath);
            });
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(
            sourcePath: sourcePath,
            outputPath: finalOutputPath));

        Assert.False(result.Success);
        Assert.True(result.WasCanceled);
        Assert.False(files.Exists(partialOutputPath));
        Assert.False(files.Exists(internalTempPartialPath));
        Assert.True(files.Exists(sourcePath));
        Assert.True(files.Exists(finalOutputPath));
        Assert.True(files.Exists(untrackedOutput));
        Assert.True(files.Exists(unrelatedOtherMovie));
        Assert.True(files.Exists(unrelatedPartialName));
        Assert.True(files.Exists(nestedPartial));
        Assert.True(files.Exists(otherDirectoryPartial));
        Assert.Equal(partialOutputPath, GetProcessOutputPath(runner.LastRequest!));
        Assert.Contains(partialOutputPath, files.DeletedPaths);
        Assert.Contains(internalTempPartialPath, files.DeletedPaths);
        Assert.DoesNotContain(sourcePath, files.DeletedPaths);
        Assert.DoesNotContain(finalOutputPath, files.DeletedPaths);
        Assert.DoesNotContain(untrackedOutput, files.DeletedPaths);
        Assert.DoesNotContain(unrelatedOtherMovie, files.DeletedPaths);
        Assert.DoesNotContain(unrelatedPartialName, files.DeletedPaths);
        Assert.DoesNotContain(nestedPartial, files.DeletedPaths);
        Assert.DoesNotContain(otherDirectoryPartial, files.DeletedPaths);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage == "Conversion partial file was cleaned.");
    }

    [Fact]
    public async Task ExecuteAsync_FailedConversionDeletesTrackedAndInternalDoubleTempCurrentAttemptPartials()
    {
        var files = new FakeConversionOutputFileService();
        var sourcePath = TestPaths.SourceRoot("Movie.mp4");
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");
        var partialOutputPath = ConversionOutputFinalizer.CreatePartialOutputPath(finalOutputPath);
        var internalTempPartialPath = CreateInternalTempPartialOutputPath(partialOutputPath);
        var untrackedOutput = TestPaths.OutputRoot("untracked.v3dfy-partial.mp4");
        files.Add(sourcePath);
        files.Add(finalOutputPath);
        files.Add(untrackedOutput);
        var runner = new FakeProcessRunner(
            CompletedProcess(exitCode: 2, standardError: "iw3 failed"),
            onRun: request =>
            {
                files.Add(GetProcessOutputPath(request));
                files.Add(internalTempPartialPath);
            });
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(
            sourcePath: sourcePath,
            outputPath: finalOutputPath));

        Assert.False(result.Success);
        Assert.False(result.WasCanceled);
        Assert.False(files.Exists(partialOutputPath));
        Assert.False(files.Exists(internalTempPartialPath));
        Assert.True(files.Exists(sourcePath));
        Assert.True(files.Exists(finalOutputPath));
        Assert.True(files.Exists(untrackedOutput));
        Assert.Contains(partialOutputPath, files.DeletedPaths);
        Assert.Contains(internalTempPartialPath, files.DeletedPaths);
        Assert.DoesNotContain(sourcePath, files.DeletedPaths);
        Assert.DoesNotContain(finalOutputPath, files.DeletedPaths);
        Assert.DoesNotContain(untrackedOutput, files.DeletedPaths);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationCleanupWaitsForProcessResultBeforeDeletingPartial()
    {
        var files = new FakeConversionOutputFileService();
        using var cancellationTokenSource = new CancellationTokenSource();
        var runner = new CancelAwareProcessRunner(
            request => files.Add(GetProcessOutputPath(request)));
        var deleteObservedAfterProcessReturned = false;
        files.OnDelete = path =>
        {
            if (path == ConversionOutputFinalizer.CreatePartialOutputPath(
                    TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4")))
            {
                deleteObservedAfterProcessReturned = runner.HasReturned;
            }
        };
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var resultTask = executor.ExecuteAsync(
            CreateReadyRequest(),
            cancellationToken: cancellationTokenSource.Token);
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellationTokenSource.Cancel();
        var result = await resultTask;

        Assert.True(result.WasCanceled);
        Assert.True(deleteObservedAfterProcessReturned);
    }

    [Fact]
    public async Task ExecuteAsync_CleanupRetriesTemporarilyLockedPartialFile()
    {
        var files = new FakeConversionOutputFileService();
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");
        var partialOutputPath = ConversionOutputFinalizer.CreatePartialOutputPath(finalOutputPath);
        var runner = new FakeProcessRunner(
            CanceledProcess(),
            onRun: request =>
            {
                files.Add(GetProcessOutputPath(request));
                files.FailNextDelete(
                    GetProcessOutputPath(request),
                    new IOException("file is temporarily locked"));
            });
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(outputPath: finalOutputPath));

        Assert.True(result.WasCanceled);
        Assert.False(files.Exists(partialOutputPath));
        Assert.True(files.DeleteAttemptCounts[partialOutputPath] >= 2);
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage == "Conversion partial file was cleaned.");
    }

    [Fact]
    public async Task ExecuteAsync_CleanupFailureLogsWarningAndKeepsAppUsableResult()
    {
        var files = new FakeConversionOutputFileService();
        var finalOutputPath = TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");
        var partialOutputPath = ConversionOutputFinalizer.CreatePartialOutputPath(finalOutputPath);
        var internalTempPartialPath = CreateInternalTempPartialOutputPath(partialOutputPath);
        var runner = new FakeProcessRunner(
            CanceledProcess(),
            onRun: request =>
            {
                files.Add(GetProcessOutputPath(request));
                files.Add(internalTempPartialPath);
                files.FailDeletes(
                    internalTempPartialPath,
                    new IOException("file remained locked"));
            });
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest(outputPath: finalOutputPath));

        Assert.False(result.Success);
        Assert.True(result.WasCanceled);
        Assert.False(files.Exists(partialOutputPath));
        Assert.True(files.Exists(internalTempPartialPath));
        Assert.Contains(
            result.Logs,
            log => log.EnglishMessage.Contains(
                "Could not delete conversion partial file.",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.Logs,
            log => log.EnglishMessage == "Conversion partial file was cleaned.");
    }

    [Fact]
    public async Task ExecuteAsync_CanceledResultDoesNotReplayBufferedProcessOutput()
    {
        var files = new FakeConversionOutputFileService();
        var runner = new FakeProcessRunner(
            new ProcessExecutionResult(
                ExitCode: -1,
                StandardOutput: "buffered stdout",
                StandardError: "buffered stderr",
                OutputLines:
                [
                    new(
                        ProcessOutputStream.StandardError,
                        "buffered line",
                        DateTimeOffset.UtcNow),
                ],
                Status: ProcessExecutionStatus.Canceled,
                StartedAt: DateTimeOffset.UtcNow,
                EndedAt: DateTimeOffset.UtcNow),
            onRun: request => files.Add(GetProcessOutputPath(request)));
        var executor = new LocalIw3ConversionExecutor(
            processRunner: runner,
            outputFileService: files);

        var result = await executor.ExecuteAsync(CreateReadyRequest());

        Assert.True(result.WasCanceled);
        Assert.DoesNotContain(
            result.Logs,
            log => log.EnglishMessage.StartsWith("stderr:", StringComparison.OrdinalIgnoreCase) ||
                log.EnglishMessage.StartsWith("stdout:", StringComparison.OrdinalIgnoreCase));
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
        string? sourcePath = null,
        string? outputPath = null,
        LocalModelPlanSelection? selectedModel = null,
        ThreeDOutputFormat outputFormat = ThreeDOutputFormat.HalfTopBottom,
        bool createLgCompatibilityCopy = false,
        bool preferLgCompatibilityCopyWhenOpening = false) =>
        CreateRequest(
            sourcePath ?? TestPaths.SourceRoot("Movie.mp4"),
            outputPath ?? TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4"),
            selectedModel,
            VideoConversionPlanStatus.Ready,
            ConversionDryRunReason.None,
            isDryRun: false,
            outputFormat: outputFormat,
            createLgCompatibilityCopy: createLgCompatibilityCopy,
            preferLgCompatibilityCopyWhenOpening: preferLgCompatibilityCopyWhenOpening);

    private static ConversionExecutionRequest CreateRequest(
        string? sourcePath = null,
        string? outputPath = null,
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
        var resolvedSourcePath = sourcePath ?? TestPaths.SourceRoot("Movie.mp4");
        var resolvedOutputPath = outputPath ?? TestPaths.OutputRoot("Movie.v3dfy.3d.htab.mp4");
        var plan = new VideoConversionPlan(
            SourcePath: resolvedSourcePath,
            SuggestedOutputPath: resolvedOutputPath,
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
            SourcePath: resolvedSourcePath,
            OutputPath: resolvedOutputPath,
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

    private static string CreateInternalTempPartialOutputPath(string trackedPartialOutputPath) =>
        Path.Combine(
            Path.GetDirectoryName(trackedPartialOutputPath)!,
            $"_tmp_{Path.GetFileName(trackedPartialOutputPath)}");

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

    private sealed class ScriptedProcessRunner(
        IReadOnlyList<object> outcomes,
        Action<ProcessExecutionRequest>? onRun = null) : ILocalProcessRunner
    {
        private readonly Queue<object> _outcomes = new(outcomes);

        public int RunCallCount { get; private set; }

        public List<ProcessExecutionRequest> Requests { get; } = [];

        public Task<ProcessExecutionResult> RunAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            RunCallCount++;
            Requests.Add(request);
            onRun?.Invoke(request);

            if (_outcomes.Count == 0)
            {
                return Task.FromResult(CompletedProcess());
            }

            var outcome = _outcomes.Dequeue();
            return outcome switch
            {
                ProcessExecutionResult result => Task.FromResult(result),
                Exception exception => Task.FromException<ProcessExecutionResult>(exception),
                _ => throw new InvalidOperationException("Unsupported scripted process outcome."),
            };
        }
    }

    private sealed class CancelAwareProcessRunner(
        Action<ProcessExecutionRequest>? onRun = null) : ILocalProcessRunner
    {
        public TaskCompletionSource<bool> Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int RunCallCount { get; private set; }

        public bool ObservedCancellation { get; private set; }

        public bool HasReturned { get; private set; }

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
                HasReturned = true;
                return CanceledProcess();
            }

            HasReturned = true;
            return CompletedProcess();
        }
    }

    private sealed class FakeConversionOutputFileService : IConversionOutputFileService
    {
        private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Queue<Exception>> _deleteFailures = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Exception> _persistentDeleteFailures = new(StringComparer.OrdinalIgnoreCase);

        public List<(string SourcePath, string DestinationPath, bool Overwrite)> Moves { get; } = [];

        public List<string> DeletedPaths { get; } = [];

        public Dictionary<string, int> DeleteAttemptCounts { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Action<string>? OnDelete { get; set; }

        public bool Exists(string path) => _paths.Contains(path);

        public void DeleteIfExists(string path)
        {
            DeleteAttemptCounts[path] = DeleteAttemptCounts.TryGetValue(path, out var count)
                ? count + 1
                : 1;
            OnDelete?.Invoke(path);

            if (_deleteFailures.TryGetValue(path, out var failures) &&
                failures.Count > 0)
            {
                throw failures.Dequeue();
            }

            if (_persistentDeleteFailures.TryGetValue(path, out var exception))
            {
                throw exception;
            }

            DeletedPaths.Add(path);
            _paths.Remove(path);
        }

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

        public IReadOnlyList<string> EnumerateFiles(string directory) =>
            _paths
                .Where(path => string.Equals(
                    Path.GetDirectoryName(Path.GetFullPath(path)),
                    Path.GetFullPath(directory),
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

        public void Add(string path) => _paths.Add(path);

        public void FailNextDelete(string path, params Exception[] exceptions) =>
            _deleteFailures[path] = new Queue<Exception>(exceptions);

        public void FailDeletes(string path, Exception exception) =>
            _persistentDeleteFailures[path] = exception;
    }
}
