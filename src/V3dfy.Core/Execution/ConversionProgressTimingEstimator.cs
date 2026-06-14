using System.Globalization;
using System.Text.RegularExpressions;

namespace V3dfy.Core.Execution;

public static partial class ConversionProgressTimingEstimator
{
    public static ConversionProgressTimingEstimate? Estimate(
        string? outputText,
        int progressPercent,
        DateTimeOffset? startedAt,
        DateTimeOffset now)
    {
        var parsed = TryParseOutputLine(outputText);
        if (parsed is not null)
        {
            return parsed;
        }

        if (startedAt is null || progressPercent <= 0 || progressPercent >= 100)
        {
            return null;
        }

        var elapsed = now - startedAt.Value;
        if (elapsed <= TimeSpan.Zero)
        {
            return null;
        }

        var progressFraction = progressPercent / 100d;
        var totalTicks = elapsed.Ticks / progressFraction;
        if (double.IsNaN(totalTicks) ||
            double.IsInfinity(totalTicks) ||
            totalTicks <= elapsed.Ticks)
        {
            return null;
        }

        var total = TimeSpan.FromTicks((long)Math.Round(totalTicks));
        var remaining = total - elapsed;
        return new(
            Elapsed: elapsed,
            Remaining: remaining,
            EstimatedTotal: total,
            ProgressPercent: progressPercent);
    }

    public static ConversionProgressTimingEstimate? TryParseOutputLine(string? outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return null;
        }

        var timingMatch = TqdmTimingRegex().Match(outputText);
        if (!timingMatch.Success ||
            !TryParseClock(timingMatch.Groups["elapsed"].Value, out var elapsed) ||
            !TryParseClock(timingMatch.Groups["remaining"].Value, out var remaining))
        {
            return null;
        }

        var percent = TryParseFrameProgressPercent(outputText);
        return new(
            Elapsed: elapsed,
            Remaining: remaining,
            EstimatedTotal: elapsed + remaining,
            ProgressPercent: percent);
    }

    private static int? TryParseFrameProgressPercent(string outputText)
    {
        var frameMatch = FrameProgressRegex().Match(outputText);
        if (!frameMatch.Success ||
            !long.TryParse(frameMatch.Groups["done"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var done) ||
            !long.TryParse(frameMatch.Groups["total"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var total) ||
            total <= 0)
        {
            return null;
        }

        var percent = (int)Math.Round(done * 100d / total);
        return Math.Clamp(percent, 0, 100);
    }

    private static bool TryParseClock(string value, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        var parts = value.Split(':');
        if (parts.Length is not (2 or 3))
        {
            return false;
        }

        if (parts.Any(part => !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            return false;
        }

        var values = parts
            .Select(part => int.Parse(part, NumberStyles.None, CultureInfo.InvariantCulture))
            .ToArray();
        result = values.Length == 3
            ? new TimeSpan(values[0], values[1], values[2])
            : new TimeSpan(0, values[0], values[1]);
        return true;
    }

    [GeneratedRegex(@"\[(?<elapsed>\d{1,2}:\d{2}(?::\d{2})?)<(?<remaining>\d{1,2}:\d{2}(?::\d{2})?),")]
    private static partial Regex TqdmTimingRegex();

    [GeneratedRegex(@"(?<!\d)(?<done>\d+)\s*/\s*(?<total>\d+)(?!\d)")]
    private static partial Regex FrameProgressRegex();
}
