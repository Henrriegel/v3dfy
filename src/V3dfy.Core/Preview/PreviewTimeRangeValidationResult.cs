namespace V3dfy.Core.Preview;

public sealed record PreviewTimeRangeValidationResult(
    bool IsValid,
    PreviewTimeRange? Range,
    PreviewTimeRangeValidationIssue Issue)
{
    public static PreviewTimeRangeValidationResult Valid(PreviewTimeRange range) => new(
        IsValid: true,
        Range: range,
        Issue: PreviewTimeRangeValidationIssue.None);

    public static PreviewTimeRangeValidationResult Invalid(
        PreviewTimeRangeValidationIssue issue) => new(
        IsValid: false,
        Range: null,
        Issue: issue);
}
