namespace V3dfy.Core.Processes;

public sealed record ProcessExecutionRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string?>? EnvironmentVariables = null,
    TimeSpan? Timeout = null,
    bool CaptureStandardError = true);
