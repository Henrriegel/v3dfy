namespace V3dfy.Core.Processes;

public static class ProcessGpuMetricAggregator
{
    private static readonly string[] RelevantEngineTypeKeys =
    [
        "3d",
        "compute",
        "cuda",
        "copy",
        "videodecode",
        "videoencode",
    ];

    public static ProcessGpuMetricReading FromProcessCounters(
        int processId,
        IEnumerable<GpuEngineCounterSample> engineCounters,
        IEnumerable<GpuProcessMemoryCounterSample> memoryCounters)
    {
        ArgumentNullException.ThrowIfNull(engineCounters);
        ArgumentNullException.ThrowIfNull(memoryCounters);

        var processEngineCounters = engineCounters
            .Where(counter =>
                counter.ProcessId == processId &&
                IsRelevantEngineType(counter.EngineType))
            .ToArray();

        if (processEngineCounters.Length == 0)
        {
            return ProcessGpuMetricReading.Unavailable(
                ProcessGpuMetricReading.NoProcessGpuEngineCounterStatus);
        }

        var dedicatedMemoryBytes = SumDedicatedMemory(
            memoryCounters.Where(counter => counter.ProcessId == processId));

        return new(
            UsagePercent: SumUsage(processEngineCounters),
            Scope: ProcessGpuMetricScope.Process,
            DedicatedMemoryBytes: dedicatedMemoryBytes,
            Status: ProcessGpuMetricReading.ProcessGpuEngineCounterStatus);
    }

    public static ProcessGpuMetricReading FromAdapterCounters(
        IEnumerable<GpuEngineCounterSample> engineCounters,
        IEnumerable<GpuProcessMemoryCounterSample> memoryCounters)
    {
        ArgumentNullException.ThrowIfNull(engineCounters);
        ArgumentNullException.ThrowIfNull(memoryCounters);

        var adapterEngineCounters = engineCounters
            .Where(counter => IsRelevantEngineType(counter.EngineType))
            .ToArray();

        if (adapterEngineCounters.Length == 0)
        {
            return ProcessGpuMetricReading.Unavailable(
                ProcessGpuMetricReading.WindowsMetricsUnavailableStatus);
        }

        return new(
            UsagePercent: SumUsage(adapterEngineCounters),
            Scope: ProcessGpuMetricScope.Adapter,
            DedicatedMemoryBytes: SumDedicatedMemory(memoryCounters),
            Status: ProcessGpuMetricReading.AdapterGpuUsageStatus);
    }

    public static bool IsRelevantEngineType(string engineType)
    {
        if (string.IsNullOrWhiteSpace(engineType))
        {
            return false;
        }

        var normalized = new string(
            engineType
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());

        return RelevantEngineTypeKeys.Any(normalized.Contains);
    }

    private static double SumUsage(IEnumerable<GpuEngineCounterSample> counters) =>
        counters.Sum(counter => Math.Max(0, counter.UtilizationPercent));

    private static long? SumDedicatedMemory(IEnumerable<GpuProcessMemoryCounterSample> counters)
    {
        var total = counters.Sum(counter => Math.Max(0, counter.DedicatedUsageBytes));
        return total > 0 ? total : null;
    }
}
