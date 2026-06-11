namespace V3dfy.Infrastructure.ModelPacks;

public interface IModelPackInventoryRefreshHook
{
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
