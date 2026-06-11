using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public interface IModelPackImportPreparationService
{
    Task<ModelPackImportLaunchPreparationResult> PrepareImportAsync(
        ModelPackImportPrepareRequest request,
        CancellationToken cancellationToken = default);
}
