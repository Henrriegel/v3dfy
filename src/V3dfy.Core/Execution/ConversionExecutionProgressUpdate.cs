namespace V3dfy.Core.Execution;

public sealed record ConversionExecutionProgressUpdate(
    int ProgressPercent,
    ConversionExecutionStep CurrentStep,
    string DetailEnglish,
    string DetailSpanish)
{
    public ConversionExecutionProgressUpdate NormalizeProgress() => this with
    {
        ProgressPercent = Math.Clamp(ProgressPercent, 0, 100),
    };
}
