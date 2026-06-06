using System.Diagnostics;
using V3dfy.Core.Execution;

namespace V3dfy.Infrastructure.Files;

public sealed class ShellOutputFileOpenService : IOutputFileOpenService
{
    public void Open(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = outputPath,
            UseShellExecute = true,
        });
    }
}
