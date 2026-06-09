using System.Reflection;
using System.IO;

namespace V3dfy.App.Services;

public static class AppErrorLogService
{
    private static readonly object SyncRoot = new();

    public static string LogsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "v3dfy",
        "logs");

    public static string AppErrorLogPath => Path.Combine(LogsDirectory, "app-error.log");

    public static string CrashLogPath => Path.Combine(LogsDirectory, "crash.log");

    public static string? CurrentOperation { get; private set; }

    public static IDisposable BeginOperation(string operation)
    {
        var previousOperation = CurrentOperation;
        CurrentOperation = operation;
        return new OperationScope(previousOperation);
    }

    public static string LogRecoverableException(
        string operation,
        Exception exception) =>
        WriteException(AppErrorLogPath, operation, exception);

    public static string LogUnhandledException(
        string operation,
        Exception exception) =>
        WriteException(CrashLogPath, operation, exception);

    public static string LogUnhandledException(
        string operation,
        object? exceptionObject) =>
        exceptionObject is Exception exception
            ? LogUnhandledException(operation, exception)
            : WriteText(CrashLogPath, operation, exceptionObject?.ToString() ?? "Unknown unhandled exception.");

    private static string WriteException(
        string path,
        string operation,
        Exception exception) =>
        WriteText(path, operation, exception.ToString());

    private static string WriteText(
        string path,
        string operation,
        string detail)
    {
        try
        {
            Directory.CreateDirectory(LogsDirectory);
            var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
            var effectiveOperation = string.IsNullOrWhiteSpace(operation)
                ? CurrentOperation ?? "unknown"
                : operation;
            var entry = string.Join(
                Environment.NewLine,
                [
                    $"[{DateTimeOffset.Now:O}]",
                    $"Version: {version}",
                    $"Operation: {effectiveOperation}",
                    detail,
                    string.Empty,
                ]);

            lock (SyncRoot)
            {
                File.AppendAllText(path, entry);
            }
        }
        catch
        {
            // Last-resort diagnostic logging must never crash the app.
        }

        return path;
    }

    private sealed class OperationScope(string? previousOperation) : IDisposable
    {
        public void Dispose() => CurrentOperation = previousOperation;
    }
}
