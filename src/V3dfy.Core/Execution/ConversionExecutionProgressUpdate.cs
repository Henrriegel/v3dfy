using V3dfy.Core.Processes;

namespace V3dfy.Core.Execution;

public sealed record ConversionExecutionProgressUpdate(
    int ProgressPercent,
    ConversionExecutionStep CurrentStep,
    string DetailEnglish,
    string DetailSpanish,
    ProcessOutputLine? OutputLine = null,
    ProcessMetricSample? Metrics = null)
{
    public ConversionExecutionProgressUpdate NormalizeProgress() => this with
    {
        ProgressPercent = Math.Clamp(ProgressPercent, 0, 100),
    };
}
