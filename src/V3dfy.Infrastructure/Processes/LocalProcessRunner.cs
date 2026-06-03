using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using V3dfy.Core.Processes;

namespace V3dfy.Infrastructure.Processes;

public sealed class LocalProcessRunner : ILocalProcessRunner
{
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
            outputLines);
        var standardErrorTask = request.CaptureStandardError
            ? ReadLinesAsync(
                process.StandardError,
                ProcessOutputStream.StandardError,
                standardError,
                outputLines)
            : Task.CompletedTask;

        var status = await WaitForExitAsync(process, request.Timeout, cancellationToken);
        await Task.WhenAll(standardOutputTask, standardErrorTask);
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
        ConcurrentQueue<ProcessOutputLine> outputLines)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            text.AppendLine(line);
            outputLines.Enqueue(new ProcessOutputLine(
                Stream: stream,
                Text: line,
                CapturedAt: DateTimeOffset.UtcNow));
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
