using V3dfy.Core.Processes;

namespace V3dfy.Tests.Infrastructure;

public sealed class ProcessMetricDisplayFormatterTests
{
    [Fact]
    public void Detecting_ProducesSafeInitialText()
    {
        var text = ProcessMetricDisplayFormatter.Detecting(useSpanish: false);

        Assert.Equal("CPU: Detecting...", text.Cpu);
        Assert.Equal("RAM: Detecting...", text.Ram);
        Assert.Equal("GPU: Detecting...", text.Gpu);
    }

    [Fact]
    public void Format_WithCpuAndRam_ProducesReadableProcessMetrics()
    {
        var text = ProcessMetricDisplayFormatter.Format(
            new(
                CapturedAt: DateTimeOffset.UtcNow,
                CpuUsagePercent: 12.345,
                WorkingSetBytes: 128 * 1024 * 1024,
                PrivateMemoryBytes: 256 * 1024 * 1024,
                GpuUsagePercent: null,
                GpuStatus: ProcessGpuMetricReading.NoProcessGpuEngineCounterStatus),
            useSpanish: false);

        Assert.Equal("CPU: 12.3%", text.Cpu);
        Assert.Equal("RAM: 256.0 MB", text.Ram);
        Assert.Equal("GPU: No GPU engine counter found for this process", text.Gpu);
        Assert.Equal(string.Empty, text.Vram);
    }

    [Fact]
    public void Format_WithUnavailableValues_DoesNotFail()
    {
        var text = ProcessMetricDisplayFormatter.Format(
            new(
                CapturedAt: DateTimeOffset.UtcNow,
                CpuUsagePercent: null,
                WorkingSetBytes: null,
                PrivateMemoryBytes: null,
                GpuUsagePercent: null,
                GpuStatus: string.Empty),
            useSpanish: false);

        Assert.Equal("CPU: Detecting...", text.Cpu);
        Assert.Equal("RAM: Detecting...", text.Ram);
        Assert.Equal("GPU: Detecting...", text.Gpu);
    }

    [Fact]
    public void Format_WithGpuUsage_UsesPercentage()
    {
        var text = ProcessMetricDisplayFormatter.Format(
            new(
                CapturedAt: DateTimeOffset.UtcNow,
                CpuUsagePercent: 50,
                WorkingSetBytes: 64 * 1024 * 1024,
                PrivateMemoryBytes: null,
                GpuUsagePercent: 25.75,
                GpuStatus: ProcessGpuMetricReading.ProcessGpuEngineCounterStatus,
                GpuScope: ProcessGpuMetricScope.Process),
            useSpanish: false);

        Assert.Equal("GPU: 25.8%", text.Gpu);
    }

    [Fact]
    public void Format_WithAdapterGpuUsage_LabelsGlobalScope()
    {
        var text = ProcessMetricDisplayFormatter.Format(
            new(
                CapturedAt: DateTimeOffset.UtcNow,
                CpuUsagePercent: 50,
                WorkingSetBytes: 64 * 1024 * 1024,
                PrivateMemoryBytes: null,
                GpuUsagePercent: 68,
                GpuStatus: ProcessGpuMetricReading.AdapterGpuUsageStatus,
                GpuScope: ProcessGpuMetricScope.Adapter),
            useSpanish: false);

        Assert.Equal("GPU: 68.0% global", text.Gpu);
    }

    [Fact]
    public void Format_WithVramUsage_ShowsVramText()
    {
        var text = ProcessMetricDisplayFormatter.Format(
            new(
                CapturedAt: DateTimeOffset.UtcNow,
                CpuUsagePercent: 50,
                WorkingSetBytes: 64 * 1024 * 1024,
                PrivateMemoryBytes: null,
                GpuUsagePercent: 25,
                GpuStatus: ProcessGpuMetricReading.ProcessGpuEngineCounterStatus,
                GpuScope: ProcessGpuMetricScope.Process,
                GpuDedicatedMemoryBytes: 3L * 1024 * 1024 * 1024),
            useSpanish: false);

        Assert.Equal("VRAM: 3.00 GB", text.Vram);
    }

    [Fact]
    public void Format_WithSpanishUnavailableValues_UsesLocalizedSafeText()
    {
        var text = ProcessMetricDisplayFormatter.Format(
            new(
                CapturedAt: DateTimeOffset.UtcNow,
                CpuUsagePercent: null,
                WorkingSetBytes: null,
                PrivateMemoryBytes: null,
                GpuUsagePercent: null,
                GpuStatus: ProcessGpuMetricReading.WindowsMetricsUnavailableStatus),
            useSpanish: true);

        Assert.Equal("CPU: Detectando...", text.Cpu);
        Assert.Equal("RAM: Detectando...", text.Ram);
        Assert.Equal(
            "GPU: Metricas no disponibles en esta version/controlador de Windows",
            text.Gpu);
    }
}
