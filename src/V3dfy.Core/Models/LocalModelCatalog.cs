namespace V3dfy.Core.Models;

public sealed record LocalModelCatalog(
    string CatalogPath,
    LocalModelCatalogStatus Status,
    string? ErrorMessage,
    IReadOnlyList<LocalModelCatalogEntry> Entries,
    IReadOnlyList<LocalModelCatalogEntry> EntriesWithExistingCompatibleFiles,
    IReadOnlyList<LocalModelCatalogEntry> EntriesWithMissingFiles,
    IReadOnlyList<LocalModelFile> UnmanagedCompatibleModelFiles)
{
    public int EntryCount => Entries.Count;

    public static LocalModelCatalog Missing(
        string catalogPath,
        IReadOnlyList<LocalModelFile>? unmanagedCompatibleModelFiles = null) => new(
        CatalogPath: catalogPath,
        Status: LocalModelCatalogStatus.Missing,
        ErrorMessage: null,
        Entries: [],
        EntriesWithExistingCompatibleFiles: [],
        EntriesWithMissingFiles: [],
        UnmanagedCompatibleModelFiles: unmanagedCompatibleModelFiles ?? []);

    public static LocalModelCatalog Invalid(
        string catalogPath,
        string errorMessage,
        IReadOnlyList<LocalModelFile>? unmanagedCompatibleModelFiles = null) => new(
        CatalogPath: catalogPath,
        Status: LocalModelCatalogStatus.Invalid,
        ErrorMessage: errorMessage,
        Entries: [],
        EntriesWithExistingCompatibleFiles: [],
        EntriesWithMissingFiles: [],
        UnmanagedCompatibleModelFiles: unmanagedCompatibleModelFiles ?? []);

    public static LocalModelCatalog Placeholder(
        string catalogPath,
        IReadOnlyList<LocalModelFile>? unmanagedCompatibleModelFiles = null) => new(
        CatalogPath: catalogPath,
        Status: LocalModelCatalogStatus.Placeholder,
        ErrorMessage: null,
        Entries: [],
        EntriesWithExistingCompatibleFiles: [],
        EntriesWithMissingFiles: [],
        UnmanagedCompatibleModelFiles: unmanagedCompatibleModelFiles ?? []);
}
