namespace V3dfy.Core.Analysis;

public enum VideoAnalysisFailureKind
{
    MissingFfprobe,
    ProcessFailed,
    EmptyOutput,
    InvalidJson,
    TimedOut,
    Canceled,
}
