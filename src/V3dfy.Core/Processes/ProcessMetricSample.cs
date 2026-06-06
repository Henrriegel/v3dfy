namespace V3dfy.Core.Processes;

public sealed record ProcessMetricSample(
    DateTimeOffset CapturedAt,
    double? CpuUsagePercent,
    long? WorkingSetBytes,
    long? PrivateMemoryBytes,
    double? GpuUsagePercent,
    string GpuStatus,
    ProcessGpuMetricScope GpuScope = ProcessGpuMetricScope.Unknown,
    long? GpuDedicatedMemoryBytes = null);
