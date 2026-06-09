namespace V3dfy.Core.Preview;

public static class PreviewTimeRangeService
{
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan PreferredStartTime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromSeconds(90);

    public static PreviewTimeRange CreateDefaultRange(TimeSpan? sourceDuration)
    {
        if (sourceDuration is null || sourceDuration <= TimeSpan.Zero)
        {
            return new(TimeSpan.Zero, DefaultDuration);
        }

        if (sourceDuration >= PreferredStartTime + DefaultDuration)
        {
            return new(PreferredStartTime, PreferredStartTime + DefaultDuration);
        }

        var to = sourceDuration.Value < DefaultDuration
            ? sourceDuration.Value
            : DefaultDuration;
        return new(TimeSpan.Zero, to);
    }

    public static PreviewTimeRangeValidationResult Validate(
        string? fromText,
        string? toText,
        TimeSpan? sourceDuration)
    {
        if (sourceDuration is null || sourceDuration <= TimeSpan.Zero)
        {
            return PreviewTimeRangeValidationResult.Invalid(
                PreviewTimeRangeValidationIssue.MissingSourceDuration);
        }

        if (string.IsNullOrWhiteSpace(fromText) ||
            string.IsNullOrWhiteSpace(toText))
        {
            return PreviewTimeRangeValidationResult.Invalid(
                PreviewTimeRangeValidationIssue.MissingValue);
        }

        if (!TryParseClockTime(fromText, out var from) ||
            !TryParseClockTime(toText, out var to))
        {
            return PreviewTimeRangeValidationResult.Invalid(
                PreviewTimeRangeValidationIssue.InvalidFormat);
        }

        if (from >= to)
        {
            return PreviewTimeRangeValidationResult.Invalid(
                PreviewTimeRangeValidationIssue.FromMustBeBeforeTo);
        }

        var range = new PreviewTimeRange(from, to);
        if (range.Duration > MaximumDuration)
        {
            return PreviewTimeRangeValidationResult.Invalid(
                PreviewTimeRangeValidationIssue.ExceedsMaximumDuration);
        }

        if (to > sourceDuration.Value)
        {
            return PreviewTimeRangeValidationResult.Invalid(
                PreviewTimeRangeValidationIssue.ToBeyondSourceDuration);
        }

        return PreviewTimeRangeValidationResult.Valid(range);
    }

    public static string Format(TimeSpan value) => value.ToString(@"hh\:mm\:ss");

    private static bool TryParseClockTime(string text, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        var parts = text.Trim().Split(':');
        if (parts.Length != 3 ||
            parts.Any(part => part.Length != 2 || !part.All(char.IsDigit)))
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var hours) ||
            !int.TryParse(parts[1], out var minutes) ||
            !int.TryParse(parts[2], out var seconds) ||
            minutes > 59 ||
            seconds > 59)
        {
            return false;
        }

        value = new TimeSpan(hours, minutes, seconds);
        return true;
    }
}
