namespace V3dfy.Core.Processes;

public static class ProcessMetricDisplayFormatter
{
    public static ProcessMetricDisplayText Detecting(bool useSpanish) => new(
        Cpu: useSpanish ? "CPU: Detectando..." : "CPU: Detecting...",
        Ram: useSpanish ? "RAM: Detectando..." : "RAM: Detecting...",
        Gpu: useSpanish ? "GPU: Detectando..." : "GPU: Detecting...");

    public static ProcessMetricDisplayText Format(
        ProcessMetricSample metrics,
        bool useSpanish)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        return new(
            Cpu: FormatCpu(metrics.CpuUsagePercent, useSpanish),
            Ram: FormatMemory(metrics.PrivateMemoryBytes ?? metrics.WorkingSetBytes, useSpanish),
            Gpu: FormatGpu(metrics, useSpanish),
            Vram: FormatVram(metrics, useSpanish));
    }

    private static string FormatCpu(double? cpuUsagePercent, bool useSpanish) =>
        cpuUsagePercent is null
            ? useSpanish ? "CPU: Detectando..." : "CPU: Detecting..."
            : $"CPU: {cpuUsagePercent.Value:0.0}%";

    private static string FormatMemory(long? bytes, bool useSpanish) =>
        bytes is null
            ? useSpanish ? "RAM: Detectando..." : "RAM: Detecting..."
            : $"RAM: {FormatBytes(bytes.Value)}";

    private static string FormatGpu(ProcessMetricSample metrics, bool useSpanish)
    {
        if (metrics.GpuUsagePercent is { } gpuUsagePercent)
        {
            var scopeSuffix = metrics.GpuScope == ProcessGpuMetricScope.Adapter
                ? useSpanish ? " global" : " global"
                : string.Empty;
            return $"GPU: {gpuUsagePercent:0.0}%{scopeSuffix}";
        }

        if (string.IsNullOrWhiteSpace(metrics.GpuStatus))
        {
            return useSpanish ? "GPU: Detectando..." : "GPU: Detecting...";
        }

        return $"GPU: {LocalizeGpuStatus(metrics.GpuStatus, useSpanish)}";
    }

    private static string FormatVram(ProcessMetricSample metrics, bool useSpanish)
    {
        if (metrics.GpuDedicatedMemoryBytes is null)
        {
            return string.Empty;
        }

        var scopeSuffix = metrics.GpuScope == ProcessGpuMetricScope.Adapter
            ? useSpanish ? " global" : " global"
            : string.Empty;

        return $"VRAM: {FormatBytes(metrics.GpuDedicatedMemoryBytes.Value)}{scopeSuffix}";
    }

    private static string LocalizeGpuStatus(string status, bool useSpanish)
    {
        if (!useSpanish)
        {
            return status;
        }

        return status switch
        {
            ProcessGpuMetricReading.DetectingStatus => "Detectando...",
            ProcessGpuMetricReading.NoProcessGpuEngineCounterStatus =>
                "No se encontro contador GPU Engine para este proceso",
            ProcessGpuMetricReading.PermissionUnavailableStatus =>
                "Permiso no disponible",
            ProcessGpuMetricReading.WindowsMetricsUnavailableStatus =>
                "Metricas no disponibles en esta version/controlador de Windows",
            ProcessGpuMetricReading.NvidiaMetricsUnavailableStatus =>
                "Metricas NVIDIA no disponibles",
            ProcessGpuMetricReading.AdapterGpuUsageStatus =>
                "Uso global del adaptador GPU",
            ProcessGpuMetricReading.ProcessGpuEngineCounterStatus =>
                "Uso GPU del proceso",
            ProcessGpuMetricReading.NvidiaAdapterMetricsStatus =>
                "Metricas globales del adaptador NVIDIA",
            _ => status,
        };
    }

    private static string FormatBytes(long bytes)
    {
        const double gibibyte = 1024 * 1024 * 1024;
        const double mebibyte = 1024 * 1024;

        return bytes >= gibibyte
            ? $"{bytes / gibibyte:0.00} GB"
            : $"{bytes / mebibyte:0.0} MB";
    }
}
