namespace V3dfy.Core.Models;

public sealed record LocalModelSelectionCandidate(
    string Id,
    string DisplayName,
    string RelativePath,
    string FileName,
    string Extension,
    string ModelType,
    string Purpose,
    bool IsCatalogManaged,
    string SpanishDisplayName = "",
    string? Iw3DepthModelName = null,
    string? MappingKey = null,
    string EnglishStatusNote = "",
    string SpanishStatusNote = "")
{
    public static LocalModelSelectionCandidate FromCatalogEntry(
        LocalModelCatalogEntry entry,
        LocalModelFile modelFile)
    {
        var displayName = FirstNonEmpty(entry.DisplayName, entry.Id, modelFile.FileName);
        var spanishDisplayName = FirstNonEmpty(entry.SpanishDisplayName, displayName);

        return new(
            Id: FirstNonEmpty(entry.Id, modelFile.RelativePath),
            DisplayName: displayName,
            RelativePath: modelFile.RelativePath,
            FileName: modelFile.FileName,
            Extension: modelFile.Extension,
            ModelType: entry.ModelType,
            Purpose: entry.Purpose,
            IsCatalogManaged: true,
            SpanishDisplayName: spanishDisplayName);
    }

    public static LocalModelSelectionCandidate FromUnmanagedFile(LocalModelFile modelFile) => new(
        Id: modelFile.RelativePath,
        DisplayName: modelFile.FileName,
        RelativePath: modelFile.RelativePath,
        FileName: modelFile.FileName,
        Extension: modelFile.Extension,
        ModelType: string.Empty,
        Purpose: string.Empty,
        IsCatalogManaged: false);

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
