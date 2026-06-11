using System.Text.Json;
using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public sealed class ModelPackHelperResultReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ModelPackInstallResult> ReadAsync(
        string resultPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultPath);

        await using var stream = File.OpenRead(resultPath);
        var result = await JsonSerializer.DeserializeAsync<ModelPackInstallResult>(
            stream,
            JsonOptions,
            cancellationToken);

        return result ?? throw new InvalidDataException(
            $"Model pack helper result file is empty or invalid: {resultPath}");
    }
}
