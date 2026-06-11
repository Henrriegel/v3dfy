namespace V3dfy.Infrastructure.ModelPacks;

public interface IModelPackFilePicker
{
    Task<string?> PickModelPackZipAsync(CancellationToken cancellationToken = default);
}
