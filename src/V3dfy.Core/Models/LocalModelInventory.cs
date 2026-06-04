namespace V3dfy.Core.Models;

public sealed record LocalModelInventory(
    string ModelsDirectory,
    bool DirectoryExists,
    IReadOnlyList<string> SupportedExtensions,
    IReadOnlyList<LocalModelFile> CompatibleModelFiles,
    LocalModelCatalog Catalog)
{
    public int CompatibleModelCount => CompatibleModelFiles.Count;

    public bool HasCompatibleModels => CompatibleModelCount > 0;

    public IReadOnlyList<LocalModelSelectionCandidate> SelectionCandidates =>
        CreateSelectionCandidates();

    public static LocalModelInventory Empty(
        string modelsDirectory,
        bool directoryExists = false) => new(
        ModelsDirectory: modelsDirectory,
        DirectoryExists: directoryExists,
        SupportedExtensions: Iw3EngineBundleContract.SupportedModelExtensions,
        CompatibleModelFiles: [],
        Catalog: LocalModelCatalog.Missing(
            Path.Combine(modelsDirectory, Iw3EngineBundleContract.ModelCatalogFileName)));

    private IReadOnlyList<LocalModelSelectionCandidate> CreateSelectionCandidates()
    {
        if (!HasCompatibleModels)
        {
            return [];
        }

        var compatibleFilesByPath = CompatibleModelFiles.ToDictionary(
            file => file.RelativePath,
            StringComparer.OrdinalIgnoreCase);
        var selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<LocalModelSelectionCandidate>();

        foreach (var entry in Catalog.EntriesWithExistingCompatibleFiles)
        {
            if (compatibleFilesByPath.TryGetValue(entry.File, out var modelFile) &&
                selectedPaths.Add(modelFile.RelativePath))
            {
                candidates.Add(LocalModelSelectionCandidate.FromCatalogEntry(entry, modelFile));
            }
        }

        foreach (var modelFile in Catalog.UnmanagedCompatibleModelFiles)
        {
            if (selectedPaths.Add(modelFile.RelativePath))
            {
                candidates.Add(LocalModelSelectionCandidate.FromUnmanagedFile(modelFile));
            }
        }

        return candidates;
    }
}
