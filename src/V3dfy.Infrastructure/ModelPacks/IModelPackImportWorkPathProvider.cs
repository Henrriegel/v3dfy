using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public interface IModelPackImportWorkPathProvider
{
    ModelPackImportWorkPaths CreateWorkPaths();
}
