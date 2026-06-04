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

    public static ConversionExecutionState Blocked(
        ConversionExecutionStartGateResult startGateResult)
    {
        ArgumentNullException.ThrowIfNull(startGateResult);

        if (startGateResult.CanStart)
        {
            throw new ArgumentException(
                "A blocked conversion state requires a blocked start gate result.",
                nameof(startGateResult));
        }

        return new(
            Status: ConversionExecutionStatus.Blocked,
            ProgressPercent: 0,
            CurrentStep: new(
                "Conversion did not start.",
                "La conversi\u00f3n no inici\u00f3."),
            DetailEnglish: startGateResult.EnglishLogMessage,
            DetailSpanish: startGateResult.SpanishLogMessage);
    }

    public ConversionExecutionState NormalizeProgress() => this with
    {
        ProgressPercent = Math.Clamp(ProgressPercent, 0, 100),
    };
}
