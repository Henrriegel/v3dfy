namespace V3dfy.Core.Processes;

public static class ProcessExecutionRequestValidator
{
    public static void ValidateBundledToolRequest(ProcessExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExecutablePath);
        ArgumentNullException.ThrowIfNull(request.Arguments);

        if (!Path.IsPathFullyQualified(request.ExecutablePath))
        {
            throw new ArgumentException(
                "Executable path must be fully qualified. PATH lookup is not allowed.",
                nameof(request));
        }

        if (request.Timeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Timeout must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.AllowedRootDirectory))
        {
            return;
        }

        if (!Path.IsPathFullyQualified(request.AllowedRootDirectory))
        {
            throw new ArgumentException(
                "Allowed root directory must be fully qualified.",
                nameof(request));
        }

        var executablePath = NormalizePath(request.ExecutablePath);
        var allowedRoot = NormalizeDirectory(request.AllowedRootDirectory);

        if (!executablePath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Executable path must be inside the allowed bundled tool root.",
                nameof(request));
        }
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string NormalizeDirectory(string path) =>
        Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
        Path.DirectorySeparatorChar;
}
