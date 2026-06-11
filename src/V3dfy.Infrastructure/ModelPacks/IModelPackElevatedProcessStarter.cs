using System.Diagnostics;
using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public interface IModelPackElevatedProcessStarter
{
    Task<ModelPackElevatedProcessResult> StartAndWaitAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken = default);
}
