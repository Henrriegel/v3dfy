using V3dfy.Core.Processes;
using V3dfy.Infrastructure.Processes;

namespace V3dfy.Tests.Infrastructure;

public sealed class WindowsProcessGpuMetricsCollectorTests
{
    [Fact]
    public void Capture_WithProcessCounters_ReturnsProcessGpuMetrics()
    {
        var collector = new WindowsProcessGpuMetricsCollector(
            new FakeCounterReader(new(
                EngineCounters:
                [
                    new(1234, "pid_1234_engtype_3D", "3D", 30),
                    new(1234, "pid_1234_engtype_CUDA", "CUDA", 20),
                ],
                MemoryCounters:
                [
                    new(1234, "pid_1234_luid_0", 1024 * 1024 * 1024),
                ])),
            new FakeNvidiaReader(ProcessGpuMetricReading.Unavailable(
                ProcessGpuMetricReading.NvidiaMetricsUnavailableStatus)));

        var reading = collector.Capture(1234);

        Assert.Equal(ProcessGpuMetricScope.Process, reading.Scope);
        Assert.Equal(50, reading.UsagePercent);
        Assert.Equal(1024 * 1024 * 1024, reading.DedicatedMemoryBytes);
    }

    [Fact]
    public void Capture_WithoutProcessCounters_UsesAdapterFallback()
    {
        var collector = new WindowsProcessGpuMetricsCollector(
            new FakeCounterReader(new(
                EngineCounters:
                [
                    new(9999, "pid_9999_engtype_3D", "3D", 44),
                ],
                MemoryCounters: [])),
            new FakeNvidiaReader(ProcessGpuMetricReading.Unavailable(
                ProcessGpuMetricReading.NvidiaMetricsUnavailableStatus)));

        var reading = collector.Capture(1234);

        Assert.Equal(ProcessGpuMetricScope.Adapter, reading.Scope);
        Assert.Equal(44, reading.UsagePercent);
        Assert.Equal(ProcessGpuMetricReading.AdapterGpuUsageStatus, reading.Status);
    }

    [Fact]
    public void Capture_WhenAllFallbacksAreUnavailable_ReturnsSpecificReason()
    {
        var collector = new WindowsProcessGpuMetricsCollector(
            new FakeCounterReader(new(
                EngineCounters: [],
                MemoryCounters: [],
                FailureReason:
                    ProcessGpuMetricReading.WindowsMetricsUnavailableStatus)),
            new FakeNvidiaReader(ProcessGpuMetricReading.Unavailable(
                ProcessGpuMetricReading.NvidiaMetricsUnavailableStatus)));

        var reading = collector.Capture(1234);

        Assert.Null(reading.UsagePercent);
        Assert.Equal(
            ProcessGpuMetricReading.WindowsMetricsUnavailableStatus,
            reading.Status);
    }

    private sealed class FakeCounterReader(WindowsGpuCounterSnapshot snapshot)
        : IWindowsGpuCounterReader
    {
        public WindowsGpuCounterSnapshot Read() => snapshot;
    }

    private sealed class FakeNvidiaReader(ProcessGpuMetricReading reading)
        : INvidiaSmiGpuMetricReader
    {
        public ProcessGpuMetricReading ReadAdapterMetrics() => reading;
    }
}
