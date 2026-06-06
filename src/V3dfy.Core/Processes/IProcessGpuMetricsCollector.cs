namespace V3dfy.Core.Processes;

public interface IProcessGpuMetricsCollector
{
    ProcessGpuMetricReading Capture(int processId);
}
