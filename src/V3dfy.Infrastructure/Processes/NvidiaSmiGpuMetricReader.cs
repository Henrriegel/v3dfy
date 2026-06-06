using System.ComponentModel;
using System.Diagnostics;
using V3dfy.Core.Processes;

namespace V3dfy.Infrastructure.Processes;

public sealed class NvidiaSmiGpuMetricReader : INvidiaSmiGpuMetricReader
{
    private static readonly TimeSpan MissingToolRetryDelay = TimeSpan.FromSeconds(30);

    private DateTimeOffset _nextProbeAt = DateTimeOffset.MinValue;

    public ProcessGpuMetricReading ReadAdapterMetrics()
    {
        if (DateTimeOffset.UtcNow < _nextProbeAt)
        {
            return ProcessGpuMetricReading.Unavailable(
                ProcessGpuMetricReading.NvidiaMetricsUnavailableStatus);
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "nvidia-smi.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            process.StartInfo.ArgumentList.Add(
                "--query-gpu=utilization.gpu,memory.used");
            process.StartInfo.ArgumentList.Add("--format=csv,noheader,nounits");

            if (!process.Start())
            {
                return MarkUnavailable();
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(milliseconds: 1000) || process.ExitCode != 0)
            {
                TryKill(process);
                return MarkUnavailable();
            }

            var reading = NvidiaSmiGpuMetricsParser.ParseAdapterMetrics(output);
            if (reading.UsagePercent is null)
            {
                MarkProbeDelay();
            }

            return reading;
        }
        catch (Win32Exception)
        {
            return MarkUnavailable();
        }
        catch (InvalidOperationException)
        {
            return MarkUnavailable();
        }
    }

    private ProcessGpuMetricReading MarkUnavailable()
    {
        MarkProbeDelay();
        return ProcessGpuMetricReading.Unavailable(
            ProcessGpuMetricReading.NvidiaMetricsUnavailableStatus);
    }

    private void MarkProbeDelay() =>
        _nextProbeAt = DateTimeOffset.UtcNow.Add(MissingToolRetryDelay);

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }
    }
}
