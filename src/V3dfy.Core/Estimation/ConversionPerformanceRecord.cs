namespace V3dfy.Core.Estimation;

public sealed record ConversionPerformanceRecord(
    int SchemaVersion,
    ConversionPerformanceOperationType OperationType,
    string ModelKey,
    string ModelDisplayName,
    string OutputPresetId,
    int Width,
    int Height,
    long FrameCount,
    double DurationSeconds,
    double ElapsedSeconds,
    double EffectiveFramesPerSecond,
    double EffectiveMegapixelFramesPerSecond,
    string DeviceBucket,
    DateTimeOffset TimestampUtc,
    string AppVersion);
