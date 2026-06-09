namespace V3dfy.Core.Execution;

public enum ConversionExecutionBlocker
{
    None,
    NoCompletedAnalysis,
    MissingConversionPlan,
    ReadinessUnknown,
    MissingLocalDependencies,
    FeatureDisabled,
    PreviewRequired,
}
