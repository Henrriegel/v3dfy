using V3dfy.Core.Processes;

namespace V3dfy.Tests.Infrastructure;

public sealed class NvidiaSmiGpuMetricsParserTests
{
    [Fact]
    public void ParseAdapterMetrics_WithCsvOutput_ReturnsGlobalAdapterMetrics()
    {
        var reading = NvidiaSmiGpuMetricsParser.ParseAdapterMetrics("68, 3277");

        Assert.Equal(ProcessGpuMetricScope.Adapter, reading.Scope);
        Assert.Equal(68, reading.UsagePercent);
        Assert.Equal(3277L * 1024 * 1024, reading.DedicatedMemoryBytes);
        Assert.Equal(ProcessGpuMetricReading.NvidiaAdapterMetricsStatus, reading.Status);
    }

    [Fact]
    public void ParseAdapterMetrics_WithMissingOutput_ReturnsUnavailableWithoutThrowing()
    {
        var reading = NvidiaSmiGpuMetricsParser.ParseAdapterMetrics(string.Empty);

        Assert.Null(reading.UsagePercent);
        Assert.Equal(
            ProcessGpuMetricReading.NvidiaMetricsUnavailableStatus,
            reading.Status);
    }
}
