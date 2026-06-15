namespace V3dfy.Core.Estimation;

public sealed record ConversionPerformanceHistoryLoadResult(
    IReadOnlyList<ConversionPerformanceRecord> Records,
    string? Warning);

public sealed record ConversionPerformanceHistorySaveResult(
    bool Success,
    string? Warning);
