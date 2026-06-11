namespace V3dfy.Infrastructure.ModelPacks;

public sealed class NullModelPackInventoryRefreshHook : IModelPackInventoryRefreshHook
{
    public static NullModelPackInventoryRefreshHook Instance { get; } = new();

    private NullModelPackInventoryRefreshHook()
    {
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
