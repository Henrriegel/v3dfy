using V3dfy.Core.Execution;

namespace V3dfy.Tests.Execution;

public sealed class ConversionExecutionStateTests
{
    [Fact]
    public void NotStarted_DefaultsToZeroProgressAndCannotCancel()
    {
        var state = ConversionExecutionState.NotStarted();

        Assert.Equal(ConversionExecutionStatus.NotStarted, state.Status);
        Assert.Equal(0, state.ProgressPercent);
        Assert.False(state.CanCancel);
        Assert.Equal("Conversion has not started.", state.CurrentStep.EnglishText);
        Assert.Equal("La conversión no ha iniciado.", state.CurrentStep.SpanishText);
    }

    [Fact]
    public void Running_AllowsCancellation()
    {
        var state = new ConversionExecutionState(
            Status: ConversionExecutionStatus.Running,
            ProgressPercent: 42,
            CurrentStep: new("Converting frames.", "Convirtiendo cuadros."),
            DetailEnglish: "Processing source video.",
            DetailSpanish: "Procesando video de origen.",
            StartedAt: DateTimeOffset.UtcNow);

        Assert.True(state.CanCancel);
    }

    [Fact]
    public void Blocked_FromGateResult_DoesNotTransitionToRunning()
    {
        var gateResult = ConversionExecutionStartGateResult.Blocked(
            ConversionExecutionBlocker.FeatureDisabled,
            "Conversion execution is not enabled yet.",
            "La ejecuci\u00f3n de conversi\u00f3n a\u00fan no est\u00e1 habilitada.",
            "No Python, iw3, or FFmpeg conversion process was started.",
            "No se inici\u00f3 ning\u00fan proceso de Python, iw3 ni conversi\u00f3n con FFmpeg.");

        var state = ConversionExecutionState.Blocked(gateResult);

        Assert.Equal(ConversionExecutionStatus.Blocked, state.Status);
        Assert.NotEqual(ConversionExecutionStatus.Running, state.Status);
        Assert.Equal(0, state.ProgressPercent);
        Assert.False(state.CanCancel);
        Assert.Equal("Conversion did not start.", state.CurrentStep.EnglishText);
        Assert.Contains("No Python, iw3, or FFmpeg conversion process was started", state.DetailEnglish);
    }

    [Fact]
    public void Blocked_WithReadyGateResult_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ConversionExecutionState.Blocked(ConversionExecutionStartGateResult.Ready()));

        Assert.Equal("startGateResult", exception.ParamName);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(150, 100)]
    [InlineData(50, 50)]
    public void NormalizeProgress_ClampsProgressPercent(int input, int expected)
    {
        var state = new ConversionExecutionState(
            Status: ConversionExecutionStatus.Running,
            ProgressPercent: input,
            CurrentStep: new("Converting frames.", "Convirtiendo cuadros."),
            DetailEnglish: string.Empty,
            DetailSpanish: string.Empty);

        Assert.Equal(expected, state.NormalizeProgress().ProgressPercent);
    }
}
