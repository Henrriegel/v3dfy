using System.Text.Json;
using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.Health;

public sealed class InternalToolsHealthChecker
{
    private const long MaxModelCatalogBytes = 256 * 1024;
    private const long MaxCliCapabilitiesBytes = 128 * 1024;

    public EngineHealthStatus Check(InternalToolPaths paths) => CheckDetailed(paths).Summary;

    public EngineDependencyHealth CheckDetailed(InternalToolPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var modelInventory = GetModelInventory(paths.ModelsDirectory, paths.ModelCatalogFile);
        var cliCapabilities = GetIw3CliCapabilities(paths.Iw3CliCapabilitiesFile);

        return new EngineDependencyHealth(
            Ffmpeg: GetBundledFileHealth(paths.FfmpegExecutable),
            Ffprobe: GetBundledFileHealth(paths.FfprobeExecutable),
            Python: GetBundledFileHealth(paths.PythonExecutable),
            Iw3EngineDirectory: GetIw3EngineHealth(paths.Iw3EngineDirectory),
            ModelsDirectory: GetModelsHealth(modelInventory),
            ModelInventory: modelInventory,
            Iw3CliCapabilities: cliCapabilities);
    }

    private static ToolDependencyHealth GetBundledFileHealth(string path) =>
        File.Exists(path)
            ? new(ToolHealthStatus.Found, ToolHealthDetailKind.BundledFileFound, path)
            : new(ToolHealthStatus.Missing, ToolHealthDetailKind.BundledFileMissing, path);

    private static ToolDependencyHealth GetIw3EngineHealth(string path)
    {
        if (!Directory.Exists(path))
        {
            return new(
                ToolHealthStatus.Missing,
                ToolHealthDetailKind.EngineDirectoryMissing,
                path);
        }

        var manifestPath = Path.Combine(path, "ENGINE_MANIFEST.json");
        var hasNonPlaceholderManifest = HasNonPlaceholderManifest(manifestPath);
        var hasRequiredEntryFile = Iw3EngineBundleContract.EngineEntryRelativePaths
            .Any(relativePath => File.Exists(Path.Combine(
                [path, .. SplitRelativePath(relativePath)])));

        if (hasNonPlaceholderManifest && hasRequiredEntryFile)
        {
            return new(ToolHealthStatus.Found, ToolHealthDetailKind.EngineBundleFound, path);
        }

        if (hasNonPlaceholderManifest)
        {
            return new(ToolHealthStatus.Missing, ToolHealthDetailKind.EngineEntryFilesMissing, path);
        }

        if (hasRequiredEntryFile)
        {
            return new(ToolHealthStatus.Missing, ToolHealthDetailKind.EngineManifestMissing, path);
        }

        var hasNonPlaceholderContent = Directory
            .EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Any(file => IsNonPlaceholderEngineContent(path, file));

        if (!hasNonPlaceholderContent)
        {
            return new(ToolHealthStatus.Missing, ToolHealthDetailKind.EnginePlaceholderOnly, path);
        }

        return new(ToolHealthStatus.Missing, ToolHealthDetailKind.EngineManifestMissing, path);
    }

    private static ToolDependencyHealth GetModelsHealth(LocalModelInventory inventory)
    {
        if (!inventory.DirectoryExists)
        {
            return new(
                ToolHealthStatus.Missing,
                ToolHealthDetailKind.ModelsDirectoryMissing,
                inventory.ModelsDirectory);
        }

        return inventory.HasCompatibleModels
            ? new(ToolHealthStatus.Found, ToolHealthDetailKind.ModelFilesFound, inventory.ModelsDirectory)
            : new(ToolHealthStatus.Missing, ToolHealthDetailKind.ModelFilesMissing, inventory.ModelsDirectory);
    }

    private static Iw3CliCapabilitiesManifest GetIw3CliCapabilities(
        string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return Iw3CliCapabilitiesManifest.Missing(manifestPath);
        }

        try
        {
            var manifestFile = new FileInfo(manifestPath);
            if (manifestFile.Length > MaxCliCapabilitiesBytes)
            {
                return Iw3CliCapabilitiesManifest.Invalid(
                    manifestPath,
                    $"CLI capabilities manifest is larger than {MaxCliCapabilitiesBytes} bytes.");
            }

            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            if (IsPlaceholderCapabilitiesManifest(root))
            {
                return Iw3CliCapabilitiesManifest.Placeholder(manifestPath);
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Iw3CliCapabilitiesManifest.Invalid(
                    manifestPath,
                    "CLI capabilities manifest root must be a JSON object.");
            }

            if (!TryGetStringArray(root, "verifiedOptions", out var verifiedOptions, out var errorMessage) ||
                !TryGetStringArray(root, "unverifiedOptions", out var unverifiedOptions, out errorMessage))
            {
                return Iw3CliCapabilitiesManifest.Invalid(manifestPath, errorMessage);
            }

            return new(
                ManifestPath: manifestPath,
                Status: Iw3CliCapabilitiesStatus.Found,
                ErrorMessage: null,
                BundledIw3Version: GetOptionalString(root, "bundledIw3Version") is { Length: > 0 } bundledVersion
                    ? bundledVersion
                    : GetOptionalString(root, "iw3Version"),
                VerifiedBaseCommand: GetOptionalBoolean(root, "verifiedBaseCommand"),
                VerifiedOptions: verifiedOptions,
                UnverifiedOptions: unverifiedOptions,
                VerificationSource: GetOptionalString(root, "verificationSource"),
                VerifiedAtUtc: GetOptionalString(root, "verifiedAtUtc"),
                Notes: GetOptionalString(root, "notes"));
        }
        catch (JsonException exception)
        {
            return Iw3CliCapabilitiesManifest.Invalid(manifestPath, exception.Message);
        }
        catch (IOException exception)
        {
            return Iw3CliCapabilitiesManifest.Invalid(manifestPath, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Iw3CliCapabilitiesManifest.Invalid(manifestPath, exception.Message);
        }
    }

    private static LocalModelInventory GetModelInventory(
        string path,
        string modelCatalogFile)
    {
        if (!Directory.Exists(path))
        {
            return LocalModelInventory.Empty(path, modelCatalogFile);
        }

        var compatibleModelFiles = Directory
            .EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Where(IsCompatibleModelFile)
            .Select(file => CreateLocalModelFile(path, file))
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var catalog = GetModelCatalog(path, modelCatalogFile, compatibleModelFiles);

        return new(
            ModelsDirectory: path,
            DirectoryExists: true,
            SupportedExtensions: Iw3EngineBundleContract.SupportedModelExtensions,
            CompatibleModelFiles: compatibleModelFiles,
            Catalog: catalog);
    }

    private static LocalModelCatalog GetModelCatalog(
        string modelsDirectory,
        string catalogPath,
        IReadOnlyList<LocalModelFile> compatibleModelFiles)
    {
        if (!File.Exists(catalogPath))
        {
            return LocalModelCatalog.Missing(catalogPath, compatibleModelFiles);
        }

        try
        {
            var catalogFile = new FileInfo(catalogPath);
            if (catalogFile.Length > MaxModelCatalogBytes)
            {
                return LocalModelCatalog.Invalid(
                    catalogPath,
                    $"Catalog is larger than {MaxModelCatalogBytes} bytes.",
                    compatibleModelFiles);
            }

            using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
            var root = document.RootElement;
            if (IsPlaceholderCatalog(root))
            {
                return LocalModelCatalog.Placeholder(catalogPath, compatibleModelFiles);
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return LocalModelCatalog.Invalid(
                    catalogPath,
                    "Catalog root must be a JSON object.",
                    compatibleModelFiles);
            }

            if (!root.TryGetProperty("models", out var modelsElement))
            {
                return CreateModelCatalog(catalogPath, [], compatibleModelFiles);
            }

            if (modelsElement.ValueKind != JsonValueKind.Array)
            {
                return LocalModelCatalog.Invalid(
                    catalogPath,
                    "Catalog models property must be an array.",
                    compatibleModelFiles);
            }

            var entries = modelsElement
                .EnumerateArray()
                .Where(element => element.ValueKind == JsonValueKind.Object)
                .Select(element => CreateCatalogEntry(element, modelsDirectory, compatibleModelFiles))
                .ToArray();

            return CreateModelCatalog(catalogPath, entries, compatibleModelFiles);
        }
        catch (JsonException exception)
        {
            return LocalModelCatalog.Invalid(catalogPath, exception.Message, compatibleModelFiles);
        }
        catch (IOException exception)
        {
            return LocalModelCatalog.Invalid(catalogPath, exception.Message, compatibleModelFiles);
        }
        catch (UnauthorizedAccessException exception)
        {
            return LocalModelCatalog.Invalid(catalogPath, exception.Message, compatibleModelFiles);
        }
    }

    private static LocalModelCatalog CreateModelCatalog(
        string catalogPath,
        IReadOnlyList<LocalModelCatalogEntry> entries,
        IReadOnlyList<LocalModelFile> compatibleModelFiles)
    {
        var entriesWithExistingCompatibleFiles = entries
            .Where(entry => entry.HasExistingCompatibleFile)
            .ToArray();
        var entriesWithMissingFiles = entries
            .Where(entry => !entry.HasExistingCompatibleFile)
            .ToArray();
        var catalogManagedFiles = entriesWithExistingCompatibleFiles
            .Select(entry => NormalizeCatalogFile(entry.File))
            .Where(file => !string.IsNullOrWhiteSpace(file))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unmanagedCompatibleModelFiles = compatibleModelFiles
            .Where(file => !catalogManagedFiles.Contains(file.RelativePath))
            .ToArray();

        return new(
            CatalogPath: catalogPath,
            Status: LocalModelCatalogStatus.Found,
            ErrorMessage: null,
            Entries: entries,
            EntriesWithExistingCompatibleFiles: entriesWithExistingCompatibleFiles,
            EntriesWithMissingFiles: entriesWithMissingFiles,
            UnmanagedCompatibleModelFiles: unmanagedCompatibleModelFiles);
    }

    private static LocalModelCatalogEntry CreateCatalogEntry(
        JsonElement element,
        string modelsDirectory,
        IReadOnlyList<LocalModelFile> compatibleModelFiles)
    {
        var file = GetOptionalString(element, "file");
        var normalizedFile = NormalizeCatalogFile(file);
        var referencedFileExists = !string.IsNullOrWhiteSpace(normalizedFile) &&
            CatalogReferenceExists(modelsDirectory, normalizedFile);
        var referencedFileIsCompatible = !string.IsNullOrWhiteSpace(normalizedFile) &&
            compatibleModelFiles.Any(modelFile => string.Equals(
                modelFile.RelativePath,
                normalizedFile,
                StringComparison.OrdinalIgnoreCase));

        return new(
            Id: GetOptionalString(element, "id"),
            DisplayName: GetOptionalString(element, "displayName"),
            File: normalizedFile,
            ModelType: GetOptionalString(element, "modelType"),
            Purpose: GetOptionalString(element, "purpose"),
            Notes: GetOptionalString(element, "notes"),
            ReferencedFileExists: referencedFileExists,
            ReferencedFileIsCompatible: referencedFileIsCompatible);
    }

    private static bool CatalogReferenceExists(string modelsDirectory, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(
            [modelsDirectory, .. SplitRelativePath(relativePath)]));
        var root = Path.GetFullPath(modelsDirectory);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return File.Exists(fullPath);
    }

    private static string NormalizeCatalogFile(string file)
    {
        if (string.IsNullOrWhiteSpace(file) ||
            Path.IsPathRooted(file) ||
            file.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Contains(".."))
        {
            return string.Empty;
        }

        return string.Join(
            '/',
            file.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries));
    }

    private static string GetOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool GetOptionalBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.True;

    private static bool TryGetStringArray(
        JsonElement element,
        string propertyName,
        out IReadOnlyList<string> values,
        out string errorMessage)
    {
        values = [];
        errorMessage = string.Empty;

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            errorMessage = $"CLI capabilities {propertyName} property must be an array.";
            return false;
        }

        var parsedValues = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                errorMessage = $"CLI capabilities {propertyName} entries must be strings.";
                return false;
            }

            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                parsedValues.Add(value);
            }
        }

        values = parsedValues;
        return true;
    }

    private static bool IsPlaceholderCapabilitiesManifest(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("placeholder", out var placeholder) &&
            placeholder.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        return IsPlaceholderStringProperty(root, "version") ||
            IsPlaceholderStringProperty(root, "iw3Version") ||
            IsPlaceholderStringProperty(root, "bundledIw3Version");
    }

    private static bool IsPlaceholderStringProperty(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), "placeholder", StringComparison.OrdinalIgnoreCase);

    private static bool IsPlaceholderCatalog(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("placeholder", out var placeholder) &&
            placeholder.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        return root.TryGetProperty("version", out var version) &&
            version.ValueKind == JsonValueKind.String &&
            string.Equals(version.GetString(), "placeholder", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCompatibleModelFile(string file)
    {
        var fileName = Path.GetFileName(file);
        if (IsPlaceholderOrContractModelFile(fileName))
        {
            return false;
        }

        return Iw3EngineBundleContract.SupportedModelExtensions.Contains(
            Path.GetExtension(file),
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsPlaceholderOrContractModelFile(string fileName)
    {
        if (Iw3EngineBundleContract.PlaceholderOrContractFileNames.Contains(
            fileName,
            StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return string.Equals(fileName, ".gitkeep", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nameWithoutExtension, "placeholder", StringComparison.OrdinalIgnoreCase);
    }

    private static LocalModelFile CreateLocalModelFile(string modelsDirectory, string file)
    {
        var relativePath = Path
            .GetRelativePath(modelsDirectory, file)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

        return new(
            FileName: Path.GetFileName(file),
            RelativePath: relativePath,
            Extension: Path.GetExtension(file));
    }

    private static bool IsNonPlaceholderEngineContent(string engineDirectory, string file)
    {
        var relativePath = Path.GetRelativePath(engineDirectory, file);
        var firstSegment = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries)[0];

        if (string.Equals(firstSegment, "models", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(firstSegment, "python", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(file);
        if (Iw3EngineBundleContract.PlaceholderOrContractFileNames.Contains(
            fileName,
            StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string[] SplitRelativePath(string relativePath) =>
        relativePath.Split(
            ['/', '\\'],
            StringSplitOptions.RemoveEmptyEntries);

    private static bool HasNonPlaceholderManifest(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(path));
            return manifest.RootElement.TryGetProperty("version", out var version) &&
                !string.IsNullOrWhiteSpace(version.GetString()) &&
                !string.Equals(
                    version.GetString(),
                    "placeholder",
                    StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

}
