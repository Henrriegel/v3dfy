namespace V3dfy.Core.Models;

public sealed record LocalModelCatalogEntry(
    string Id,
    string DisplayName,
    string SpanishDisplayName,
    string File,
    string ModelType,
    string Purpose,
    string Notes,
    bool ReferencedFileExists,
    bool ReferencedFileIsCompatible)
{
    public bool HasExistingCompatibleFile =>
        ReferencedFileExists && ReferencedFileIsCompatible;
}
