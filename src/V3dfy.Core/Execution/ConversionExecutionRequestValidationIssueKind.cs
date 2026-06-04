namespace V3dfy.Core.Execution;

public enum ConversionExecutionRequestValidationIssueKind
{
    RequestMissing,
    PlanMissing,
    SourcePathMissing,
    OutputPathMissing,
    SourceAndOutputPathMatch,
    SelectedPresetMissing,
    OptionsMissing,
    ExpectedToolPathsMissing,
    ExpectedToolPathMissing,
    ExpectedToolPathNotAbsolute,
    CommandPreviewMissing,
    SelectedModelDisplayNameMissing,
    SelectedModelRelativePathMissing,
    SelectedModelPathMustBeRelative,
    SelectedModelPathContainsParentTraversal,
}
