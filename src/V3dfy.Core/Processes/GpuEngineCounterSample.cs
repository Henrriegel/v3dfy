namespace V3dfy.Core.Processes;

public sealed record GpuEngineCounterSample(
    int? ProcessId,
    string InstanceName,
    string EngineType,
    double UtilizationPercent);
