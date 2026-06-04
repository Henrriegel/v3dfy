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

    [Fact]
    public void EvaluateStart_WhenReadinessIsIncomplete_ReturnsMissingDependenciesBlocker()
    {
        var gate = new ConversionExecutionFeatureGate(isRealConversionExecutionEnabled: true);

        var result = gate.EvaluateStart(
            hasCompletedAnalysis: true,
            hasConversionPlan: true,
            readiness: BlockedReadiness());

        Assert.False(result.CanStart);
        Assert.Equal(ConversionExecutionBlocker.MissingLocalDependencies, result.Blocker);
        Assert.Contains("No Python, iw3, or FFmpeg conversion process was started", result.EnglishDetail);
    }

    [Fact]
    public void EvaluateStart_WhenRealExecutionIsDisabled_ReturnsFeatureDisabledBlocker()
    {
        var gate = new ConversionExecutionFeatureGate();

        var result = gate.EvaluateStart(
            hasCompletedAnalysis: true,
            hasConversionPlan: true,
            readiness: CompleteReadiness());

        Assert.False(result.CanStart);
        Assert.Equal(ConversionExecutionBlocker.FeatureDisabled, result.Blocker);
        Assert.Equal("Conversion execution is not enabled yet.", result.EnglishStatus);
        Assert.Contains("No Python, iw3, or FFmpeg conversion process was started", result.EnglishDetail);
    }

    [Fact]
    public void EvaluateStart_WhenRequirementsAndFeatureGateAreEnabled_ReturnsReady()
    {
        var gate = new ConversionExecutionFeatureGate(isRealConversionExecutionEnabled: true);

        var result = gate.EvaluateStart(
            hasCompletedAnalysis: true,
            hasConversionPlan: true,
            readiness: CompleteReadiness());

        Assert.True(result.CanStart);
        Assert.Equal(ConversionExecutionBlocker.None, result.Blocker);
    }

    [Fact]
    public void EvaluateStart_WhenAnalysisIsMissing_ReturnsAnalysisBlocker()
    {
        var gate = new ConversionExecutionFeatureGate(isRealConversionExecutionEnabled: true);

        var result = gate.EvaluateStart(
            hasCompletedAnalysis: false,
            hasConversionPlan: true,
            readiness: CompleteReadiness());

        Assert.False(result.CanStart);
        Assert.Equal(ConversionExecutionBlocker.NoCompletedAnalysis, result.Blocker);
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
