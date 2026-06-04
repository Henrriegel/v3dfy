namespace V3dfy.Core.Execution;

public enum ConversionExecutionStatus
{
    NotStarted,
    Ready,
    Blocked,
    Running,
    Canceling,
    Canceled,
    Failed,
    Completed,
}
