using V3dfy.Core.Readiness;

namespace V3dfy.Core.Execution;

public sealed class ConversionExecutionFeatureGate(bool isRealConversionExecutionEnabled = false)
{
    public bool IsRealConversionExecutionEnabled { get; } = isRealConversionExecutionEnabled;

    public bool CanStartConversion(bool hasCompletedAnalysis, bool hasConversionPlan, ConversionReadiness? readiness)
    {
        // Bundled Python, iw3, and models are necessary but not sufficient.
        // Real conversion stays blocked until the local execution runner is intentionally connected.
        return hasCompletedAnalysis &&
            hasConversionPlan &&
            readiness?.CanConvert == true &&
            IsRealConversionExecutionEnabled;
    }
}
