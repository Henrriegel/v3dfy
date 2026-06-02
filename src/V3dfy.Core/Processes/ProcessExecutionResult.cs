namespace V3dfy.Core.Processes;

public sealed record ProcessExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    IReadOnlyList<ProcessOutputLine> OutputLines,
    ProcessExecutionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt)
{
    public bool WasCanceled => Status == ProcessExecutionStatus.Canceled;

    public bool TimedOut => Status == ProcessExecutionStatus.TimedOut;

    public TimeSpan Duration => EndedAt - StartedAt;
}
