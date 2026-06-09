using V3dfy.Core.Execution;
using V3dfy.Core.Processes;

namespace V3dfy.Engine.Iw3.Execution;

public static class Iw3RuntimeDownloadDetector
{
    public const string EnglishWarning =
        "iw3 attempted to download a runtime dependency. This bundle is not fully offline-ready.";
    public const string SpanishWarning =
        "iw3 intento descargar una dependencia en tiempo de ejecucion. Este bundle aun no esta completamente listo para uso offline.";
    public const string EnglishTimingNote =
        "Runtime download detected during iw3 startup.";
    public const string SpanishTimingNote =
        "Descarga en tiempo de ejecucion detectada durante el inicio de iw3.";

    public static bool IsRuntimeDownloadLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Downloading:", StringComparison.OrdinalIgnoreCase) ||
            (ContainsHttpUrl(text) &&
                text.Contains("github.com", StringComparison.OrdinalIgnoreCase));
    }

    public static bool ContainsRuntimeDownload(ProcessExecutionResult result)
    {
        if (result.OutputLines.Any(line => IsRuntimeDownloadLine(line.Text)))
        {
            return true;
        }

        return SplitProcessText(result.StandardOutput).Any(IsRuntimeDownloadLine) ||
            SplitProcessText(result.StandardError).Any(IsRuntimeDownloadLine);
    }

    public static ConversionExecutionLogEntry CreateWarningLog(DateTimeOffset timestamp) =>
        new(timestamp, EnglishWarning, SpanishWarning);

    public static ConversionExecutionLogEntry CreateTimingNoteLog(DateTimeOffset timestamp) =>
        new(timestamp, EnglishTimingNote, SpanishTimingNote);

    private static bool ContainsHttpUrl(string text) =>
        text.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("http://", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SplitProcessText(string text) =>
        text.Split(
                ["\r\n", "\n", "\r"],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd());
}
