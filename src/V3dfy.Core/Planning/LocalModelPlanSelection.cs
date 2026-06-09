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
    LocalModelPlanSource Source,
    string Id = "",
    string FileName = "",
    string SpanishDisplayName = "",
    string? Iw3DepthModelName = null,
    string? MappingKey = null,
    string EnglishStatusNote = "",
    string SpanishStatusNote = "")
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
                : LocalModelPlanSource.UnmanagedLocalFile,
            Id: candidate.Id,
            FileName: candidate.FileName,
            SpanishDisplayName: candidate.SpanishDisplayName,
            Iw3DepthModelName: candidate.Iw3DepthModelName,
            MappingKey: candidate.MappingKey,
            EnglishStatusNote: candidate.EnglishStatusNote,
            SpanishStatusNote: candidate.SpanishStatusNote);
    }
}
