namespace V3dfy.Core.Execution;

public sealed record ConversionProgressTimingEstimate(
    TimeSpan Elapsed,
    TimeSpan? Remaining,
    TimeSpan? EstimatedTotal,
    int? ProgressPercent);
