using V3dfy.Core.Readiness;
using V3dfy.Core.Execution;

namespace V3dfy.Tests.Execution;

public sealed class ConversionExecutionFeatureGateTests
{
    [Fact]
    public void CanStartConversion_WhenReadinessIsCompleteButRealExecutionIsDisabled_ReturnsFalse()
    {
        var gate = new ConversionExecutionFeatureGate();

        var canStart = gate.CanStartConversion(
            hasCompletedAnalysis: true,
            hasConversionPlan: true,
            readiness: CompleteReadiness());

        Assert.False(canStart);
    }

    [Fact]
    public void CanStartConversion_WhenReadinessIsIncomplete_ReturnsFalse()
    {
        var gate = new ConversionExecutionFeatureGate(isRealConversionExecutionEnabled: true);

        var canStart = gate.CanStartConversion(
            hasCompletedAnalysis: true,
            hasConversionPlan: true,
            readiness: BlockedReadiness());

        Assert.False(canStart);
    }

    [Fact]
    public void CanStartConversion_WhenPlanIsMissing_ReturnsFalse()
    {
        var gate = new ConversionExecutionFeatureGate(isRealConversionExecutionEnabled: true);

        var canStart = gate.CanStartConversion(
            hasCompletedAnalysis: true,
            hasConversionPlan: false,
            readiness: CompleteReadiness());

        Assert.False(canStart);
    }

    [Fact]
    public void CanStartConversion_WhenAllRequirementsAndFeatureGateAreEnabled_ReturnsTrue()
    {
        var gate = new ConversionExecutionFeatureGate(isRealConversionExecutionEnabled: true);

        var canStart = gate.CanStartConversion(
            hasCompletedAnalysis: true,
            hasConversionPlan: true,
            readiness: CompleteReadiness());

        Assert.True(canStart);
    }

    private static ConversionReadiness CompleteReadiness() => new(
        CanConvert: true,
        EnglishStatus: "ready",
        SpanishStatus: "listo",
        Issues: [],
        EnglishRequiredComponentsSummary: "required",
        SpanishRequiredComponentsSummary: "requeridos");

    private static ConversionReadiness BlockedReadiness() => new(
        CanConvert: false,
        EnglishStatus: "blocked",
        SpanishStatus: "bloqueado",
        Issues: [new("missing", "faltante")],
        EnglishRequiredComponentsSummary: "required",
        SpanishRequiredComponentsSummary: "requeridos");
}
