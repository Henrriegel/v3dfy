using V3dfy.Core.Processes;

namespace V3dfy.Infrastructure.Processes;

public interface INvidiaSmiGpuMetricReader
{
    ProcessGpuMetricReading ReadAdapterMetrics();
}
