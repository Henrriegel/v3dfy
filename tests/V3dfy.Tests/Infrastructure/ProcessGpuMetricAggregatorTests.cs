using V3dfy.Core.Processes;

namespace V3dfy.Tests.Infrastructure;

public sealed class ProcessGpuMetricAggregatorTests
{
    [Fact]
    public void FromProcessCounters_SumsRelevantEnginesForProcess()
    {
        var reading = ProcessGpuMetricAggregator.FromProcessCounters(
            processId: 1234,
            engineCounters:
            [
                new(1234, "pid_1234_engtype_3D", "3D", 12.5),
                new(1234, "pid_1234_engtype_Compute", "Compute", 25),
                new(1234, "pid_1234_engtype_Overlay", "Overlay", 80),
                new(9999, "pid_9999_engtype_3D", "3D", 60),
            ],
            memoryCounters:
            [
                new(1234, "pid_1234_luid_0", 2L * 1024 * 1024 * 1024),
            ]);

        Assert.Equal(ProcessGpuMetricScope.Process, reading.Scope);
        Assert.Equal(37.5, reading.UsagePercent);
        Assert.Equal(2L * 1024 * 1024 * 1024, reading.DedicatedMemoryBytes);
        Assert.Equal(
            ProcessGpuMetricReading.ProcessGpuEngineCounterStatus,
            reading.Status);
    }

    [Fact]
    public void FromProcessCounters_WithoutMatchingCounters_ReturnsSpecificReason()
    {
        var reading = ProcessGpuMetricAggregator.FromProcessCounters(
            processId: 1234,
            engineCounters:
            [
                new(9999, "pid_9999_engtype_3D", "3D", 60),
            ],
            memoryCounters: []);

        Assert.Null(reading.UsagePercent);
        Assert.Equal(ProcessGpuMetricScope.Unknown, reading.Scope);
        Assert.Equal(
            ProcessGpuMetricReading.NoProcessGpuEngineCounterStatus,
            reading.Status);
    }

    [Fact]
    public void FromAdapterCounters_SumsRelevantEnginesAndLabelsAdapterScope()
    {
        var reading = ProcessGpuMetricAggregator.FromAdapterCounters(
            engineCounters:
            [
                new(1234, "pid_1234_engtype_3D", "3D", 20),
                new(9999, "pid_9999_engtype_VideoEncode", "Video Encode", 15),
                new(null, "luid_0_engtype_Copy", "Copy", 5),
            ],
            memoryCounters:
            [
                new(1234, "pid_1234_luid_0", 512 * 1024 * 1024),
                new(9999, "pid_9999_luid_0", 256 * 1024 * 1024),
            ]);

        Assert.Equal(ProcessGpuMetricScope.Adapter, reading.Scope);
        Assert.Equal(40, reading.UsagePercent);
        Assert.Equal(768 * 1024 * 1024, reading.DedicatedMemoryBytes);
        Assert.Equal(ProcessGpuMetricReading.AdapterGpuUsageStatus, reading.Status);
    }
}
