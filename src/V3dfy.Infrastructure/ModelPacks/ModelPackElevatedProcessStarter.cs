using System.ComponentModel;
using System.Diagnostics;
using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public sealed class ModelPackElevatedProcessStarter : IModelPackElevatedProcessStarter
{
    public async Task<ModelPackElevatedProcessResult> StartAndWaitAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ModelPackElevatedProcessResult(
                    Started: false,
                    ExitCode: null,
                    ErrorMessage: "Could not start the model pack helper process.");
            }

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new ModelPackElevatedProcessResult(
                    Started: true,
                    ExitCode: null,
                    ErrorMessage: "Model pack helper wait was canceled.");
            }

            return new ModelPackElevatedProcessResult(
                Started: true,
                ExitCode: process.ExitCode);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            return new ModelPackElevatedProcessResult(
                Started: false,
                ExitCode: null,
                ErrorMessage: $"Could not start the model pack helper process: {exception.Message}");
        }
    }
}
