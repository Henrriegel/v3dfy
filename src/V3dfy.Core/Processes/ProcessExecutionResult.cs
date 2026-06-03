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

    public string EnglishSummary => Status switch
    {
        ProcessExecutionStatus.Completed => $"Process exited with code {ExitCode}.",
        ProcessExecutionStatus.Canceled => "Process execution was canceled.",
        ProcessExecutionStatus.TimedOut => "Process execution timed out.",
        _ => Status.ToString(),
    };

    public string SpanishSummary => Status switch
    {
        ProcessExecutionStatus.Completed => $"El proceso termin\u00f3 con c\u00f3digo {ExitCode}.",
        ProcessExecutionStatus.Canceled => "La ejecuci\u00f3n del proceso fue cancelada.",
        ProcessExecutionStatus.TimedOut => "La ejecuci\u00f3n del proceso agot\u00f3 el tiempo de espera.",
        _ => Status.ToString(),
    };
}
