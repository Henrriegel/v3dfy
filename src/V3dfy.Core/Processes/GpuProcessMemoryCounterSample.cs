namespace V3dfy.Core.Processes;

public sealed record GpuProcessMemoryCounterSample(
    int ProcessId,
    string InstanceName,
    long DedicatedUsageBytes);
