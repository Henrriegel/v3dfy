using V3dfy.Core.Processes;

namespace V3dfy.Infrastructure.Processes;

public sealed class WindowsProcessGpuMetricsCollector : IProcessGpuMetricsCollector
{
    private readonly IWindowsGpuCounterReader _counterReader;
    private readonly INvidiaSmiGpuMetricReader _nvidiaSmiReader;

    public WindowsProcessGpuMetricsCollector(
        IWindowsGpuCounterReader? counterReader = null,
        INvidiaSmiGpuMetricReader? nvidiaSmiReader = null)
    {
        _counterReader = counterReader ?? new WindowsPerformanceCounterGpuCounterReader();
        _nvidiaSmiReader = nvidiaSmiReader ?? new NvidiaSmiGpuMetricReader();
    }

    public ProcessGpuMetricReading Capture(int processId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return TryNvidiaFallback(
                ProcessGpuMetricReading.WindowsMetricsUnavailableStatus);
        }

        WindowsGpuCounterSnapshot snapshot;
        try
        {
            snapshot = _counterReader.Read();
        }
        catch (UnauthorizedAccessException)
        {
            return TryNvidiaFallback(
                ProcessGpuMetricReading.PermissionUnavailableStatus);
        }
        catch (Exception)
        {
            return TryNvidiaFallback(
                ProcessGpuMetricReading.WindowsMetricsUnavailableStatus);
        }

        var processReading = ProcessGpuMetricAggregator.FromProcessCounters(
            processId,
            snapshot.EngineCounters,
            snapshot.MemoryCounters);
        if (processReading.UsagePercent is not null)
        {
            return processReading;
        }

        var adapterReading = ProcessGpuMetricAggregator.FromAdapterCounters(
            snapshot.EngineCounters,
            snapshot.MemoryCounters);
        if (adapterReading.UsagePercent is not null)
        {
            return adapterReading;
        }

        var fallbackReason = string.IsNullOrWhiteSpace(snapshot.FailureReason)
            ? processReading.Status
            : snapshot.FailureReason;

        return TryNvidiaFallback(fallbackReason);
    }

    private ProcessGpuMetricReading TryNvidiaFallback(string fallbackReason)
    {
        try
        {
            var nvidiaReading = _nvidiaSmiReader.ReadAdapterMetrics();
            return nvidiaReading.UsagePercent is not null
                ? nvidiaReading
                : ProcessGpuMetricReading.Unavailable(fallbackReason);
        }
        catch (Exception)
        {
            return ProcessGpuMetricReading.Unavailable(fallbackReason);
        }
    }
}
