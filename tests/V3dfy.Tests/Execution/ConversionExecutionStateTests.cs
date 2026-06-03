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
