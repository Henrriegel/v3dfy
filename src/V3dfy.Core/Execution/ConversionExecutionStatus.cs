namespace V3dfy.Core.Execution;

public enum ConversionExecutionStatus
{
    NotStarted,
    Ready,
    Running,
    Canceling,
    Canceled,
    Failed,
    Completed,
}
