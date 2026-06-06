namespace V3dfy.Core.Processes;

public sealed record ProcessGpuMetricReading(
    double? UsagePercent,
    ProcessGpuMetricScope Scope,
    long? DedicatedMemoryBytes,
    string Status)
{
    public const string DetectingStatus = "Detecting...";
    public const string ProcessGpuEngineCounterStatus = "Process GPU engine counters";
    public const string AdapterGpuUsageStatus = "Adapter/global GPU usage";
    public const string NoProcessGpuEngineCounterStatus =
        "No GPU engine counter found for this process";
    public const string PermissionUnavailableStatus = "Permission unavailable";
    public const string WindowsMetricsUnavailableStatus =
        "Metrics unavailable on this Windows build/driver";
    public const string NvidiaMetricsUnavailableStatus = "NVIDIA metrics unavailable";
    public const string NvidiaAdapterMetricsStatus = "NVIDIA adapter metrics";

    public static ProcessGpuMetricReading Detecting() => new(
        UsagePercent: null,
        Scope: ProcessGpuMetricScope.Unknown,
        DedicatedMemoryBytes: null,
        Status: DetectingStatus);

    public static ProcessGpuMetricReading Unavailable(string status) => new(
        UsagePercent: null,
        Scope: ProcessGpuMetricScope.Unknown,
        DedicatedMemoryBytes: null,
        Status: string.IsNullOrWhiteSpace(status)
            ? WindowsMetricsUnavailableStatus
            : status);
}
