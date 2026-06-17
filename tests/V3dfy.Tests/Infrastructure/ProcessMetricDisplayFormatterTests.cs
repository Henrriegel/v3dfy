using V3dfy.Core.Localization;
using V3dfy.Core.Processes;

namespace V3dfy.Tests.Infrastructure;

public sealed class ProcessMetricDisplayFormatterTests
{
    [Fact]
    public void Detecting_ProducesSafeInitialText()
    {
        var text = ProcessMetricDisplayFormatter.Detecting(English);

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
            English);

        Assert.Equal("CPU: 12.3%", text.Cpu);
        Assert.Equal("RAM: 256.0 MB", text.Ram);
        Assert.Equal("GPU: No GPU Engine counter was found for this process", text.Gpu);
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
            English);

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
            English);

        Assert.Equal("GPU: 25.8%", text.Gpu);
    }

    [Fact]
    public void Format_WithAdapterGpuUsage_LabelsGlobalScopeFromLocalization()
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
            English);

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
            English);

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
            Spanish);

        Assert.Equal("CPU: Detectando...", text.Cpu);
        Assert.Equal("RAM: Detectando...", text.Ram);
        Assert.Equal(
            "GPU: Metricas no disponibles en esta version/controlador de Windows",
            text.Gpu);
    }

    private static string English(
        string key,
        params (string Key, object? Value)[] placeholders) =>
        Localize(EnglishStrings, key, placeholders);

    private static string Spanish(
        string key,
        params (string Key, object? Value)[] placeholders) =>
        Localize(SpanishStrings, key, placeholders);

    private static string Localize(
        IReadOnlyDictionary<string, string> strings,
        string key,
        params (string Key, object? Value)[] placeholders)
    {
        var value = strings.TryGetValue(key, out var text)
            ? text
            : $"[Missing: {key}]";

        foreach (var placeholder in placeholders)
        {
            value = value.Replace(
                "{" + placeholder.Key + "}",
                Convert.ToString(placeholder.Value) ?? string.Empty,
                StringComparison.Ordinal);
        }

        return value;
    }

    private static readonly IReadOnlyDictionary<string, string> EnglishStrings =
        new Dictionary<string, string>
        {
            [LocalizationKeys.ProcessMetricsCpuDetecting] = "CPU: Detecting...",
            [LocalizationKeys.ProcessMetricsRamDetecting] = "RAM: Detecting...",
            [LocalizationKeys.ProcessMetricsGpuDetecting] = "GPU: Detecting...",
            [LocalizationKeys.ProcessMetricsGpuUnavailableFormat] = "GPU: {status}",
            [LocalizationKeys.ProcessMetricsGpuGlobalFormat] = "GPU: {value}% global",
            [LocalizationKeys.ProcessMetricsVramGlobalFormat] = "VRAM: {value} global",
            [LocalizationKeys.ProcessMetricsStatusDetecting] = "Detecting...",
            [LocalizationKeys.ProcessMetricsStatusNoProcessGpuEngineCounter] =
                "No GPU Engine counter was found for this process",
            [LocalizationKeys.ProcessMetricsStatusPermissionUnavailable] = "Permission unavailable",
            [LocalizationKeys.ProcessMetricsStatusWindowsMetricsUnavailable] =
                "Metrics unavailable on this Windows version/driver",
            [LocalizationKeys.ProcessMetricsStatusNvidiaMetricsUnavailable] = "NVIDIA metrics unavailable",
            [LocalizationKeys.ProcessMetricsStatusAdapterGpuUsage] = "Global GPU adapter usage",
            [LocalizationKeys.ProcessMetricsStatusProcessGpuEngineCounter] = "Process GPU usage",
            [LocalizationKeys.ProcessMetricsStatusNvidiaAdapterMetrics] = "Global NVIDIA adapter metrics",
        };

    private static readonly IReadOnlyDictionary<string, string> SpanishStrings =
        new Dictionary<string, string>
        {
            [LocalizationKeys.ProcessMetricsCpuDetecting] = "CPU: Detectando...",
            [LocalizationKeys.ProcessMetricsRamDetecting] = "RAM: Detectando...",
            [LocalizationKeys.ProcessMetricsGpuDetecting] = "GPU: Detectando...",
            [LocalizationKeys.ProcessMetricsGpuUnavailableFormat] = "GPU: {status}",
            [LocalizationKeys.ProcessMetricsGpuGlobalFormat] = "GPU: {value}% global",
            [LocalizationKeys.ProcessMetricsVramGlobalFormat] = "VRAM: {value} global",
            [LocalizationKeys.ProcessMetricsStatusDetecting] = "Detectando...",
            [LocalizationKeys.ProcessMetricsStatusNoProcessGpuEngineCounter] =
                "No se encontro contador GPU Engine para este proceso",
            [LocalizationKeys.ProcessMetricsStatusPermissionUnavailable] = "Permiso no disponible",
            [LocalizationKeys.ProcessMetricsStatusWindowsMetricsUnavailable] =
                "Metricas no disponibles en esta version/controlador de Windows",
            [LocalizationKeys.ProcessMetricsStatusNvidiaMetricsUnavailable] = "Metricas NVIDIA no disponibles",
            [LocalizationKeys.ProcessMetricsStatusAdapterGpuUsage] = "Uso global del adaptador GPU",
            [LocalizationKeys.ProcessMetricsStatusProcessGpuEngineCounter] = "Uso GPU del proceso",
            [LocalizationKeys.ProcessMetricsStatusNvidiaAdapterMetrics] = "Metricas globales del adaptador NVIDIA",
        };
}
