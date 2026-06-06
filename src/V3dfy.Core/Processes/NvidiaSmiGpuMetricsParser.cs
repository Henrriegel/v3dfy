using System.Globalization;

namespace V3dfy.Core.Processes;

public static class NvidiaSmiGpuMetricsParser
{
    public static ProcessGpuMetricReading ParseAdapterMetrics(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return ProcessGpuMetricReading.Unavailable(
                ProcessGpuMetricReading.NvidiaMetricsUnavailableStatus);
        }

        foreach (var line in output.Split(
            ["\r\n", "\n"],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var values = line
                .Split(',', StringSplitOptions.TrimEntries)
                .Select(ParseNumber)
                .Where(value => value is not null)
                .Select(value => value!.Value)
                .ToArray();

            if (values.Length == 0)
            {
                continue;
            }

            var usedMemoryBytes = values.Length > 1
                ? (long?)(values[1] * 1024 * 1024)
                : null;

            return new(
                UsagePercent: Math.Max(0, values[0]),
                Scope: ProcessGpuMetricScope.Adapter,
                DedicatedMemoryBytes: usedMemoryBytes,
                Status: ProcessGpuMetricReading.NvidiaAdapterMetricsStatus);
        }

        return ProcessGpuMetricReading.Unavailable(
            ProcessGpuMetricReading.NvidiaMetricsUnavailableStatus);
    }

    private static double? ParseNumber(string value)
    {
        var numericText = new string(
            value
                .Where(character =>
                    char.IsDigit(character) ||
                    character == '.' ||
                    character == '-')
                .ToArray());

        return double.TryParse(
            numericText,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var number)
                ? number
                : null;
    }
}
