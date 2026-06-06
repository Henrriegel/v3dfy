namespace V3dfy.Infrastructure.Processes;

public interface IWindowsGpuCounterReader
{
    WindowsGpuCounterSnapshot Read();
}
