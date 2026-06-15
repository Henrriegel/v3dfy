namespace V3dfy.Core.Estimation;

public static class ConversionPerformanceHistory
{
    public const int CurrentSchemaVersion = 1;
    public const int DefaultMaximumRecords = 100;

    public static ConversionPerformanceRecord? CreateSuccessfulRecord(
        ConversionPerformanceOperationType operationType,
        ConversionEstimateInput input,
        TimeSpan elapsed,
        string appVersion,
        TimeSpan? operationDuration = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (elapsed <= TimeSpan.Zero ||
            !TryGetVideoWork(
                input,
                operationDuration,
                out var duration,
                out var frameCount,
                out var width,
                out var height,
                out var megapixelFrames))
        {
            return null;
        }

        var elapsedSeconds = elapsed.TotalSeconds;
        return new(
            SchemaVersion: CurrentSchemaVersion,
            OperationType: operationType,
            ModelKey: NormalizeKey(input.ModelKey),
            ModelDisplayName: input.ModelDisplayName ?? string.Empty,
            OutputPresetId: NormalizeKey(input.OutputPresetId),
            Width: width,
            Height: height,
            FrameCount: frameCount,
            DurationSeconds: duration.TotalSeconds,
            ElapsedSeconds: elapsedSeconds,
            EffectiveFramesPerSecond: frameCount / elapsedSeconds,
            EffectiveMegapixelFramesPerSecond: megapixelFrames / elapsedSeconds,
            DeviceBucket: NormalizeKey(input.DeviceBucket),
            TimestampUtc: DateTimeOffset.UtcNow,
            AppVersion: appVersion ?? string.Empty);
    }

    public static IReadOnlyList<ConversionPerformanceRecord> AddBounded(
        IEnumerable<ConversionPerformanceRecord> records,
        ConversionPerformanceRecord record,
        int maximumRecords = DefaultMaximumRecords)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(record);

        return records
            .Where(IsUsable)
            .Append(record)
            .OrderByDescending(item => item.TimestampUtc)
            .Take(Math.Max(1, maximumRecords))
            .OrderBy(item => item.TimestampUtc)
            .ToArray();
    }

    public static IReadOnlyList<ConversionPerformanceRecord> Sanitize(
        IEnumerable<ConversionPerformanceRecord>? records,
        int maximumRecords = DefaultMaximumRecords)
    {
        if (records is null)
        {
            return [];
        }

        return records
            .Where(IsUsable)
            .OrderByDescending(item => item.TimestampUtc)
            .Take(Math.Max(1, maximumRecords))
            .OrderBy(item => item.TimestampUtc)
            .ToArray();
    }

    internal static bool TryGetVideoWork(
        ConversionEstimateInput input,
        TimeSpan? operationDuration,
        out TimeSpan duration,
        out long frameCount,
        out int width,
        out int height,
        out double megapixelFrames)
    {
        duration = operationDuration ?? input.Duration ?? TimeSpan.Zero;
        width = input.SourceWidth ?? 0;
        height = input.SourceHeight ?? 0;
        var frameRate = input.FrameRate ?? 0;
        frameCount = duration > TimeSpan.Zero && frameRate > 0
            ? Math.Max(1L, (long)Math.Round(duration.TotalSeconds * frameRate))
            : 0;
        megapixelFrames = width > 0 && height > 0 && frameCount > 0
            ? frameCount * (width * height / 1_000_000d)
            : 0;

        return duration > TimeSpan.Zero &&
            frameRate > 0 &&
            width > 0 &&
            height > 0 &&
            frameCount > 0 &&
            megapixelFrames > 0;
    }

    private static bool IsUsable(ConversionPerformanceRecord record) =>
        record.SchemaVersion == CurrentSchemaVersion &&
        record.Width > 0 &&
        record.Height > 0 &&
        record.FrameCount > 0 &&
        record.ElapsedSeconds > 0 &&
        record.EffectiveMegapixelFramesPerSecond > 0 &&
        !ContainsPathLikeValue(record.ModelKey) &&
        !ContainsPathLikeValue(record.ModelDisplayName) &&
        !ContainsPathLikeValue(record.OutputPresetId);

    private static bool ContainsPathLikeValue(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Contains('\\') || value.Contains('/') || value.Contains(':'));

    private static string NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
}
