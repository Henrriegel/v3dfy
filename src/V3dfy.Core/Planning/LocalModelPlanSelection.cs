using V3dfy.Core.Models;

namespace V3dfy.Core.Planning;

public enum LocalModelPlanSource
{
    CatalogMetadata,
    UnmanagedLocalFile,
}

public sealed record LocalModelPlanSelection(
    string DisplayName,
    string RelativePath,
    LocalModelPlanSource Source)
{
    public string EnglishSourceText => Source switch
    {
        LocalModelPlanSource.CatalogMetadata => "Catalog metadata",
        LocalModelPlanSource.UnmanagedLocalFile => "Unmanaged local file",
        _ => Source.ToString(),
    };

    public string SpanishSourceText => Source switch
    {
        LocalModelPlanSource.CatalogMetadata => "Cat\u00e1logo",
        LocalModelPlanSource.UnmanagedLocalFile => "Archivo local no administrado",
        _ => Source.ToString(),
    };

    public static LocalModelPlanSelection FromCandidate(
        LocalModelSelectionCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new(
            DisplayName: candidate.DisplayName,
            RelativePath: candidate.RelativePath,
            Source: candidate.IsCatalogManaged
                ? LocalModelPlanSource.CatalogMetadata
                : LocalModelPlanSource.UnmanagedLocalFile);
    }
}
