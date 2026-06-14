using V3dfy.Core.Execution;

namespace V3dfy.Tests.Execution;

public sealed class ConversionProgressTimingEstimatorTests
{
    [Fact]
    public void TryParseOutputLine_ParsesTqdmElapsedRemainingAndTotal()
    {
        var estimate = ConversionProgressTimingEstimator.TryParseOutputLine(
            "4763/14429 [1:00:20<2:00:53, 1.33it/s]");

        Assert.NotNull(estimate);
        Assert.Equal(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(20), estimate.Elapsed);
        Assert.Equal(TimeSpan.FromHours(2) + TimeSpan.FromSeconds(53), estimate.Remaining);
        Assert.Equal(
            TimeSpan.FromHours(3) + TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(13),
            estimate.EstimatedTotal);
        Assert.Equal(33, estimate.ProgressPercent);
    }

    [Fact]
    public void TryParseOutputLine_ParsesMinuteSecondTiming()
    {
        var estimate = ConversionProgressTimingEstimator.TryParseOutputLine(
            "120/240 [01:20<01:20, 1.50it/s]");

        Assert.NotNull(estimate);
        Assert.Equal(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(20), estimate.Elapsed);
        Assert.Equal(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(20), estimate.Remaining);
        Assert.Equal(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(40), estimate.EstimatedTotal);
        Assert.Equal(50, estimate.ProgressPercent);
    }

    [Fact]
    public void Estimate_ComputesRemainingFromPercentAndElapsedFallback()
    {
        var startedAt = new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);
        var now = startedAt.AddMinutes(5);

        var estimate = ConversionProgressTimingEstimator.Estimate(
            outputText: null,
            progressPercent: 25,
            startedAt,
            now);

        Assert.NotNull(estimate);
        Assert.Equal(TimeSpan.FromMinutes(5), estimate.Elapsed);
        Assert.Equal(TimeSpan.FromMinutes(15), estimate.Remaining);
        Assert.Equal(TimeSpan.FromMinutes(20), estimate.EstimatedTotal);
        Assert.Equal(25, estimate.ProgressPercent);
    }

    [Fact]
    public void Estimate_InvalidOrInsufficientDataReturnsNull()
    {
        var startedAt = new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);

        Assert.Null(ConversionProgressTimingEstimator.TryParseOutputLine("not progress"));
        Assert.Null(ConversionProgressTimingEstimator.Estimate(
            "bad [x<y]",
            progressPercent: 0,
            startedAt,
            startedAt.AddMinutes(1)));
        Assert.Null(ConversionProgressTimingEstimator.Estimate(
            outputText: null,
            progressPercent: 0,
            startedAt,
            startedAt.AddMinutes(1)));
        Assert.Null(ConversionProgressTimingEstimator.Estimate(
            outputText: null,
            progressPercent: 100,
            startedAt,
            startedAt.AddMinutes(1)));
    }

    [Fact]
    public void Smoother_AveragesRemainingAndEstimatedTotalButKeepsElapsedLive()
    {
        var smoother = new ConversionProgressTimingSmoother(alpha: 0.5);

        var first = smoother.Smooth(new(
            Elapsed: TimeSpan.FromSeconds(10),
            Remaining: TimeSpan.FromSeconds(100),
            EstimatedTotal: TimeSpan.FromSeconds(110),
            ProgressPercent: 9));
        var second = smoother.Smooth(new(
            Elapsed: TimeSpan.FromSeconds(20),
            Remaining: TimeSpan.FromSeconds(20),
            EstimatedTotal: TimeSpan.FromSeconds(40),
            ProgressPercent: 50));

        Assert.Equal(TimeSpan.FromSeconds(10), first.Elapsed);
        Assert.Equal(TimeSpan.FromSeconds(100), first.Remaining);
        Assert.Equal(TimeSpan.FromSeconds(110), first.EstimatedTotal);
        Assert.Equal(TimeSpan.FromSeconds(20), second.Elapsed);
        Assert.Equal(TimeSpan.FromSeconds(60), second.Remaining);
        Assert.Equal(TimeSpan.FromSeconds(75), second.EstimatedTotal);
    }

    [Fact]
    public void Smoother_ResetStartsNextEstimateFromFreshSample()
    {
        var smoother = new ConversionProgressTimingSmoother(alpha: 0.5);
        _ = smoother.Smooth(new(
            Elapsed: TimeSpan.FromSeconds(10),
            Remaining: TimeSpan.FromSeconds(100),
            EstimatedTotal: TimeSpan.FromSeconds(110),
            ProgressPercent: 9));

        smoother.Reset();
        var estimate = smoother.Smooth(new(
            Elapsed: TimeSpan.FromSeconds(20),
            Remaining: TimeSpan.FromSeconds(20),
            EstimatedTotal: TimeSpan.FromSeconds(40),
            ProgressPercent: 50));

        Assert.Equal(TimeSpan.FromSeconds(20), estimate.Remaining);
        Assert.Equal(TimeSpan.FromSeconds(40), estimate.EstimatedTotal);
    }
}
