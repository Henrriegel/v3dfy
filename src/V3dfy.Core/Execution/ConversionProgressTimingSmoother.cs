namespace V3dfy.Core.Execution;

public sealed class ConversionProgressTimingSmoother
{
    private readonly double _alpha;
    private TimeSpan? _smoothedRemaining;
    private TimeSpan? _smoothedEstimatedTotal;

    public ConversionProgressTimingSmoother(double alpha = 0.25)
    {
        if (alpha <= 0d || alpha > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be greater than 0 and less than or equal to 1.");
        }

        _alpha = alpha;
    }

    public ConversionProgressTimingEstimate Smooth(ConversionProgressTimingEstimate sample)
    {
        var remaining = SmoothTimeSpan(sample.Remaining, _smoothedRemaining);
        var estimatedTotal = SmoothTimeSpan(sample.EstimatedTotal, _smoothedEstimatedTotal);
        _smoothedRemaining = remaining;
        _smoothedEstimatedTotal = estimatedTotal;

        return sample with
        {
            Remaining = remaining,
            EstimatedTotal = estimatedTotal,
        };
    }

    public void Reset()
    {
        _smoothedRemaining = null;
        _smoothedEstimatedTotal = null;
    }

    private TimeSpan? SmoothTimeSpan(TimeSpan? sample, TimeSpan? previous)
    {
        if (sample is null)
        {
            return null;
        }

        if (sample.Value < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        if (previous is null)
        {
            return sample.Value;
        }

        var smoothedTicks = previous.Value.Ticks + ((sample.Value.Ticks - previous.Value.Ticks) * _alpha);
        if (double.IsNaN(smoothedTicks) || double.IsInfinity(smoothedTicks) || smoothedTicks <= 0d)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromTicks((long)Math.Round(smoothedTicks));
    }
}
