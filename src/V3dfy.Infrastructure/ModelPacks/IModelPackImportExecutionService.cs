using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public interface IModelPackImportExecutionService
{
    Task<ModelPackImportExecutionResult> ExecuteAsync(
        ModelPackImportLaunchPreparationResult preparation,
        CancellationToken cancellationToken = default);
}
