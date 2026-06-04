namespace V3dfy.Core.Execution;

public sealed record ConversionExecutionRequestValidationResult(
    IReadOnlyList<ConversionExecutionRequestValidationIssue> Issues,
    bool IsDryRun,
    bool CanStartLocalProcess,
    ConversionExecutionRequestModelState ModelState)
{
    public bool IsValid => Issues.Count == 0;
}
