using System.Globalization;
using V3dfy.Core.Localization;

namespace V3dfy.Core.Processes;

public static class ProcessMetricDisplayFormatter
{
    public static ProcessMetricDisplayText Detecting(LocalizedTextProvider localize)
    {
        ArgumentNullException.ThrowIfNull(localize);

        return new(
            Cpu: localize(LocalizationKeys.ProcessMetricsCpuDetecting),
            Ram: localize(LocalizationKeys.ProcessMetricsRamDetecting),
            Gpu: localize(LocalizationKeys.ProcessMetricsGpuDetecting));
    }

    public static ProcessMetricDisplayText Format(
        ProcessMetricSample metrics,
        LocalizedTextProvider localize)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(localize);

        return new(
            Cpu: FormatCpu(metrics.CpuUsagePercent, localize),
            Ram: FormatMemory(metrics.PrivateMemoryBytes ?? metrics.WorkingSetBytes, localize),
            Gpu: FormatGpu(metrics, localize),
            Vram: FormatVram(metrics, localize));
    }

    private static string FormatCpu(
        double? cpuUsagePercent,
        LocalizedTextProvider localize) =>
        cpuUsagePercent is null
            ? localize(LocalizationKeys.ProcessMetricsCpuDetecting)
            : $"CPU: {FormatPercent(cpuUsagePercent.Value)}%";

    private static string FormatMemory(
        long? bytes,
        LocalizedTextProvider localize) =>
        bytes is null
            ? localize(LocalizationKeys.ProcessMetricsRamDetecting)
            : $"RAM: {FormatBytes(bytes.Value)}";

    private static string FormatGpu(
        ProcessMetricSample metrics,
        LocalizedTextProvider localize)
    {
        if (metrics.GpuUsagePercent is { } gpuUsagePercent)
        {
            var value = FormatPercent(gpuUsagePercent);
            return metrics.GpuScope == ProcessGpuMetricScope.Adapter
                ? localize(LocalizationKeys.ProcessMetricsGpuGlobalFormat, ("value", value))
                : $"GPU: {value}%";
        }

        if (string.IsNullOrWhiteSpace(metrics.GpuStatus))
        {
            return localize(LocalizationKeys.ProcessMetricsGpuDetecting);
        }

        return localize(
            LocalizationKeys.ProcessMetricsGpuUnavailableFormat,
            ("status", LocalizeGpuStatus(metrics.GpuStatus, localize)));
    }

    private static string FormatVram(
        ProcessMetricSample metrics,
        LocalizedTextProvider localize)
    {
        if (metrics.GpuDedicatedMemoryBytes is null)
        {
            return string.Empty;
        }

        var value = FormatBytes(metrics.GpuDedicatedMemoryBytes.Value);

        return metrics.GpuScope == ProcessGpuMetricScope.Adapter
            ? localize(LocalizationKeys.ProcessMetricsVramGlobalFormat, ("value", value))
            : $"VRAM: {value}";
    }

    private static string LocalizeGpuStatus(
        string status,
        LocalizedTextProvider localize) =>
        status switch
        {
            ProcessGpuMetricReading.DetectingStatus => localize(LocalizationKeys.ProcessMetricsStatusDetecting),
            ProcessGpuMetricReading.NoProcessGpuEngineCounterStatus =>
                localize(LocalizationKeys.ProcessMetricsStatusNoProcessGpuEngineCounter),
            ProcessGpuMetricReading.PermissionUnavailableStatus =>
                localize(LocalizationKeys.ProcessMetricsStatusPermissionUnavailable),
            ProcessGpuMetricReading.WindowsMetricsUnavailableStatus =>
                localize(LocalizationKeys.ProcessMetricsStatusWindowsMetricsUnavailable),
            ProcessGpuMetricReading.NvidiaMetricsUnavailableStatus =>
                localize(LocalizationKeys.ProcessMetricsStatusNvidiaMetricsUnavailable),
            ProcessGpuMetricReading.AdapterGpuUsageStatus =>
                localize(LocalizationKeys.ProcessMetricsStatusAdapterGpuUsage),
            ProcessGpuMetricReading.ProcessGpuEngineCounterStatus =>
                localize(LocalizationKeys.ProcessMetricsStatusProcessGpuEngineCounter),
            ProcessGpuMetricReading.NvidiaAdapterMetricsStatus =>
                localize(LocalizationKeys.ProcessMetricsStatusNvidiaAdapterMetrics),
            _ => status,
        };

    private static string FormatBytes(long bytes)
    {
        const double gibibyte = 1024 * 1024 * 1024;
        const double mebibyte = 1024 * 1024;

        return bytes >= gibibyte
            ? $"{bytes / gibibyte:0.00} GB"
            : $"{bytes / mebibyte:0.0} MB";
    }

    private static string FormatPercent(double value) =>
        value.ToString("0.0", CultureInfo.InvariantCulture);
}
