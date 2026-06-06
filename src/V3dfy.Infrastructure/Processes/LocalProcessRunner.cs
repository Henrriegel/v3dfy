using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using V3dfy.Core.Processes;

namespace V3dfy.Infrastructure.Processes;

public sealed class LocalProcessRunner : ILocalProcessRunner
{
    private static readonly TimeSpan DefaultMetricsInterval = TimeSpan.FromSeconds(1);
    private readonly IProcessGpuMetricsCollector _gpuMetricsCollector;

    public LocalProcessRunner(IProcessGpuMetricsCollector? gpuMetricsCollector = null)
    {
        _gpuMetricsCollector =
            gpuMetricsCollector ?? new WindowsProcessGpuMetricsCollector();
    }

    public async Task<ProcessExecutionResult> RunAsync(
        ProcessExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process
        {
            StartInfo = CreateStartInfo(request),
        };

        var startedAt = DateTimeOffset.UtcNow;
        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Failed to start executable '{request.ExecutablePath}'.");
        }

        var outputLines = new ConcurrentQueue<ProcessOutputLine>();
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        var standardOutputTask = ReadLinesAsync(
            process.StandardOutput,
            ProcessOutputStream.StandardOutput,
            standardOutput,
            outputLines,
            request.OutputProgress);
        var standardErrorTask = request.CaptureStandardError
            ? ReadLinesAsync(
                process.StandardError,
                ProcessOutputStream.StandardError,
                standardError,
                outputLines,
                request.OutputProgress)
            : Task.CompletedTask;
        using var metricsCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var metricsTask = request.MetricsProgress is null
            ? Task.CompletedTask
            : CollectMetricsAsync(
                process,
                request.MetricsProgress,
                request.MetricsInterval ?? DefaultMetricsInterval,
                metricsCancellationTokenSource.Token);

        var status = await WaitForExitAsync(process, request.Timeout, cancellationToken);
        metricsCancellationTokenSource.Cancel();
        await Task.WhenAll(standardOutputTask, standardErrorTask);
        await SuppressMetricsCancellationAsync(metricsTask);
        var endedAt = DateTimeOffset.UtcNow;

        return new ProcessExecutionResult(
            ExitCode: process.ExitCode,
            StandardOutput: standardOutput.ToString(),
            StandardError: standardError.ToString(),
            OutputLines: outputLines.OrderBy(line => line.CapturedAt).ToArray(),
            Status: status,
            StartedAt: startedAt,
            EndedAt: endedAt);
    }

    private static void Validate(ProcessExecutionRequest request)
    {
        ProcessExecutionRequestValidator.ValidateBundledToolRequest(request);

        if (request.MetricsInterval is { } metricsInterval && metricsInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Metrics interval must be greater than zero.");
        }
    }

    private static ProcessStartInfo CreateStartInfo(ProcessExecutionRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = request.CaptureStandardError,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (request.EnvironmentVariables is not null)
        {
            foreach (var environmentVariable in request.EnvironmentVariables)
            {
                startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
            }
        }

        return startInfo;
    }

    private static async Task<ProcessExecutionStatus> WaitForExitAsync(
        Process process,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        var exitTask = process.WaitForExitAsync();
        var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        var timeoutTask = timeout is null
            ? Task.Delay(Timeout.InfiniteTimeSpan)
            : Task.Delay(timeout.Value);
        var completedTask = await Task.WhenAny(exitTask, cancellationTask, timeoutTask);

        if (completedTask == exitTask)
        {
            await exitTask;
            return ProcessExecutionStatus.Completed;
        }

        TryKillProcessTree(process);
        await process.WaitForExitAsync();

        return completedTask == cancellationTask
            ? ProcessExecutionStatus.Canceled
            : ProcessExecutionStatus.TimedOut;
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        ProcessOutputStream stream,
        StringBuilder text,
        ConcurrentQueue<ProcessOutputLine> outputLines,
        IProgress<ProcessOutputLine>? outputProgress)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            text.AppendLine(line);
            var outputLine = new ProcessOutputLine(
                Stream: stream,
                Text: line,
                CapturedAt: DateTimeOffset.UtcNow);
            outputLines.Enqueue(outputLine);
            outputProgress?.Report(outputLine);
        }
    }

    private async Task CollectMetricsAsync(
        Process process,
        IProgress<ProcessMetricSample> metricsProgress,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        TimeSpan? previousProcessorTime = null;
        DateTimeOffset? previousSampleTime = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var sample = CreateMetricSample(
                process,
                _gpuMetricsCollector,
                previousProcessorTime,
                previousSampleTime,
                out var currentProcessorTime,
                out var currentSampleTime);
            metricsProgress.Report(sample);
            previousProcessorTime = currentProcessorTime;
            previousSampleTime = currentSampleTime;

            if (process.HasExited)
            {
                return;
            }

            await Task.Delay(interval, cancellationToken);
        }
    }

    private static ProcessMetricSample CreateMetricSample(
        Process process,
        IProcessGpuMetricsCollector gpuMetricsCollector,
        TimeSpan? previousProcessorTime,
        DateTimeOffset? previousSampleTime,
        out TimeSpan? currentProcessorTime,
        out DateTimeOffset currentSampleTime)
    {
        currentProcessorTime = null;
        currentSampleTime = DateTimeOffset.UtcNow;

        try
        {
            process.Refresh();
            currentProcessorTime = process.TotalProcessorTime;
            var cpuUsage = CalculateCpuUsagePercent(
                previousProcessorTime,
                previousSampleTime,
                currentProcessorTime.Value,
                currentSampleTime);
            var gpuMetrics = CaptureGpuMetrics(gpuMetricsCollector, process.Id);

            return new(
                CapturedAt: currentSampleTime,
                CpuUsagePercent: cpuUsage,
                WorkingSetBytes: process.WorkingSet64,
                PrivateMemoryBytes: process.PrivateMemorySize64,
                GpuUsagePercent: gpuMetrics.UsagePercent,
                GpuStatus: gpuMetrics.Status,
                GpuScope: gpuMetrics.Scope,
                GpuDedicatedMemoryBytes: gpuMetrics.DedicatedMemoryBytes);
        }
        catch (InvalidOperationException)
        {
            return CreateUnavailableMetricSample(currentSampleTime);
        }
        catch (Win32Exception)
        {
            return CreateUnavailableMetricSample(currentSampleTime);
        }
    }

    private static ProcessGpuMetricReading CaptureGpuMetrics(
        IProcessGpuMetricsCollector gpuMetricsCollector,
        int processId)
    {
        try
        {
            return gpuMetricsCollector.Capture(processId);
        }
        catch (UnauthorizedAccessException)
        {
            return ProcessGpuMetricReading.Unavailable(
                ProcessGpuMetricReading.PermissionUnavailableStatus);
        }
        catch
        {
            return ProcessGpuMetricReading.Unavailable(
                ProcessGpuMetricReading.WindowsMetricsUnavailableStatus);
        }
    }

    private static double? CalculateCpuUsagePercent(
        TimeSpan? previousProcessorTime,
        DateTimeOffset? previousSampleTime,
        TimeSpan currentProcessorTime,
        DateTimeOffset currentSampleTime)
    {
        if (previousProcessorTime is null || previousSampleTime is null)
        {
            return null;
        }

        var elapsedMilliseconds = (currentSampleTime - previousSampleTime.Value)
            .TotalMilliseconds;
        if (elapsedMilliseconds <= 0)
        {
            return null;
        }

        var processorMilliseconds = (currentProcessorTime - previousProcessorTime.Value)
            .TotalMilliseconds;
        var cpuUsage = processorMilliseconds /
            elapsedMilliseconds /
            Environment.ProcessorCount *
            100;

        return Math.Clamp(cpuUsage, 0, 100);
    }

    private static ProcessMetricSample CreateUnavailableMetricSample(
        DateTimeOffset capturedAt) => new(
        CapturedAt: capturedAt,
        CpuUsagePercent: null,
        WorkingSetBytes: null,
        PrivateMemoryBytes: null,
        GpuUsagePercent: null,
        GpuStatus: ProcessGpuMetricReading.WindowsMetricsUnavailableStatus);

    private static async Task SuppressMetricsCancellationAsync(Task metricsTask)
    {
        try
        {
            await metricsTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited while cancellation or timeout was being handled.
        }
        catch (Win32Exception)
        {
            // Process-tree termination is best effort when the OS rejects the request.
        }
    }
}
