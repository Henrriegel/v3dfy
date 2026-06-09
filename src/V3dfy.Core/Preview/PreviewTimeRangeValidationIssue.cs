namespace V3dfy.Core.Preview;

public enum PreviewTimeRangeValidationIssue
{
    None,
    MissingSourceDuration,
    MissingValue,
    InvalidFormat,
    FromMustBeBeforeTo,
    ExceedsMaximumDuration,
    ToBeyondSourceDuration,
}
