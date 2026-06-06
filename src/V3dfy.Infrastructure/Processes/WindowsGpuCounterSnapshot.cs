using V3dfy.Core.Processes;

namespace V3dfy.Infrastructure.Processes;

public sealed record WindowsGpuCounterSnapshot(
    IReadOnlyList<GpuEngineCounterSample> EngineCounters,
    IReadOnlyList<GpuProcessMemoryCounterSample> MemoryCounters,
    string? FailureReason = null)
{
    public static WindowsGpuCounterSnapshot Failed(string reason) => new(
        EngineCounters: [],
        MemoryCounters: [],
        FailureReason: reason);
}
