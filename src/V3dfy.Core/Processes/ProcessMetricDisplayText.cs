namespace V3dfy.Core.Processes;

public sealed record ProcessMetricDisplayText(
    string Cpu,
    string Ram,
    string Gpu,
    string Vram = "");
