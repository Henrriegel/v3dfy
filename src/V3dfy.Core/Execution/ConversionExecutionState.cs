namespace V3dfy.Core.Execution;

public sealed record ConversionExecutionState(
    ConversionExecutionStatus Status,
    int ProgressPercent,
    ConversionExecutionStep CurrentStep,
    string DetailEnglish,
    string DetailSpanish,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? FinishedAt = null)
{
    public bool CanCancel => Status == ConversionExecutionStatus.Running;

    public static ConversionExecutionState NotStarted() => new(
        Status: ConversionExecutionStatus.NotStarted,
        ProgressPercent: 0,
        CurrentStep: new(
            "Conversion has not started.",
            "La conversión no ha iniciado."),
        DetailEnglish: string.Empty,
        DetailSpanish: string.Empty);

    public ConversionExecutionState NormalizeProgress() => this with
    {
        ProgressPercent = Math.Clamp(ProgressPercent, 0, 100),
    };
}
