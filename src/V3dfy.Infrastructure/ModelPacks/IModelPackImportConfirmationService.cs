using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public sealed record ModelPackImportConfirmationPrompt(
    ModelPackImportPreparationResult Preparation,
    string EnglishTitle,
    string SpanishTitle,
    string EnglishMessage,
    string SpanishMessage);

public interface IModelPackImportConfirmationService
{
    Task<bool> ConfirmAsync(
        ModelPackImportConfirmationPrompt prompt,
        CancellationToken cancellationToken = default);
}

public sealed class AcceptingModelPackImportConfirmationService : IModelPackImportConfirmationService
{
    public static AcceptingModelPackImportConfirmationService Instance { get; } = new();

    private AcceptingModelPackImportConfirmationService()
    {
    }

    public Task<bool> ConfirmAsync(
        ModelPackImportConfirmationPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return Task.FromResult(true);
    }
}
