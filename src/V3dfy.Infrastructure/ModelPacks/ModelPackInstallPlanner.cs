using System.IO.Compression;
using System.Text.Json;
using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public sealed class ModelPackInstallPlanner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ModelPackDryRunInstallPlan> CreateDryRunPlanAsync(
        ModelPackDryRunInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        var warnings = new List<string>();
        var filesToInstall = new List<ModelPackPlannedFile>();
        var alreadyInstalled = new List<ModelPackPlannedFile>();
        var conflicts = new List<ModelPackFileConflict>();
        ModelPackManifest? manifest = null;
        ModelPackManifestSummary? summary = null;

        var zipPath = ModelPackPathRules.NormalizeFullPath(request.ModelPackZipPath);
        var targetRoot = ModelPackPathRules.NormalizeFullPath(request.TargetPretrainedModelsRoot);
        var elevationWouldBeRequired = ModelPackPathRules.IsUnderProgramFiles(targetRoot);

        if (!File.Exists(zipPath))
        {
            errors.Add($"Model pack ZIP was not found: {zipPath}");
            return CreatePlan(false);
        }

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var archiveFiles = BuildArchiveFileMap(archive, errors);
            manifest = await ReadManifestAsync(archiveFiles, errors, cancellationToken);
            if (manifest is not null)
            {
                summary = CreateSummary(manifest);
                var declaredFiles = ValidateManifest(
                    manifest,
                    request.CurrentIw3Version,
                    warnings,
                    errors);

                ValidateArchiveContents(archiveFiles, declaredFiles, errors);

                if (errors.Count == 0)
                {
                    await ValidateArchiveFileHashesAsync(
                        archiveFiles,
                        declaredFiles,
                        errors,
                        cancellationToken);
                }

                if (errors.Count == 0)
                {
                    await CreateFilePlanAsync(
                        declaredFiles,
                        targetRoot,
                        filesToInstall,
                        alreadyInstalled,
                        conflicts,
                        cancellationToken);
                }
            }
        }
        catch (InvalidDataException exception)
        {
            errors.Add($"Model pack ZIP is invalid: {exception.Message}");
        }
        catch (JsonException exception)
        {
            errors.Add($"MODEL_PACK.json is invalid JSON: {exception.Message}");
        }
        catch (IOException exception)
        {
            errors.Add($"Could not inspect model pack ZIP: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            errors.Add($"Could not inspect model pack ZIP: {exception.Message}");
        }

        return CreatePlan(errors.Count == 0 && conflicts.Count == 0);

        ModelPackDryRunInstallPlan CreatePlan(bool isValid) => new(
            IsValid: isValid,
            Manifest: summary,
            ModelPackZipPath: zipPath,
            TargetPretrainedModelsRoot: targetRoot,
            FilesToInstall: filesToInstall,
            AlreadyInstalledFiles: alreadyInstalled,
            Conflicts: conflicts,
            ValidationErrors: errors,
            Warnings: warnings,
            ElevationWouldBeRequired: elevationWouldBeRequired);
    }

    private static Dictionary<string, ZipArchiveEntry> BuildArchiveFileMap(
        ZipArchive archive,
        ICollection<string> errors)
    {
        var files = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (ModelPackPathRules.IsZipDirectoryEntry(entry))
            {
                if (!ModelPackPathRules.TryNormalizeArchiveDirectoryPath(
                        entry.FullName,
                        out _,
                        out var directoryError))
                {
                    errors.Add($"ZIP entry '{entry.FullName}' is unsafe: {directoryError}");
                }

                continue;
            }

            if (!ModelPackPathRules.TryNormalizeArchivePath(entry.FullName, out var relativePath, out var error))
            {
                errors.Add($"ZIP entry '{entry.FullName}' is unsafe: {error}");
                continue;
            }

            if (!files.TryAdd(relativePath, entry))
            {
                errors.Add($"ZIP contains duplicate path variants for '{relativePath}'.");
            }
        }

        return files;
    }

    private static async Task<ModelPackManifest?> ReadManifestAsync(
        IReadOnlyDictionary<string, ZipArchiveEntry> archiveFiles,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        if (!archiveFiles.TryGetValue(ModelPackManifest.FileName, out var manifestEntry))
        {
            errors.Add($"Model pack ZIP is missing {ModelPackManifest.FileName}.");
            return null;
        }

        await using var stream = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<ModelPackManifest>(
            stream,
            JsonOptions,
            cancellationToken);
        if (manifest is null)
        {
            errors.Add($"{ModelPackManifest.FileName} is empty.");
        }

        return manifest;
    }

    private static ModelPackManifestSummary CreateSummary(ModelPackManifest manifest) => new(
        PackId: manifest.PackId,
        PackVersion: manifest.PackVersion,
        DisplayName: manifest.DisplayName,
        TargetRoot: manifest.TargetRoot,
        CompatibleIw3Versions: manifest.CompatibleIw3Versions ?? [],
        MinV3dfyVersion: manifest.MinV3dfyVersion,
        ModelCount: manifest.Models?.Count ?? 0,
        FileCount: manifest.Files?.Count ?? 0);

    private static Dictionary<string, DeclaredModelPackFile> ValidateManifest(
        ModelPackManifest manifest,
        string? currentIw3Version,
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        var declaredFiles = new Dictionary<string, DeclaredModelPackFile>(StringComparer.OrdinalIgnoreCase);

        if (manifest.SchemaVersion != ModelPackManifest.SupportedSchemaVersion)
        {
            errors.Add($"Unsupported model pack schemaVersion: {manifest.SchemaVersion}.");
        }

        RequireString(manifest.PackId, "packId", errors);
        RequireString(manifest.PackVersion, "packVersion", errors);
        RequireString(manifest.DisplayName, "displayName", errors);
        RequireString(manifest.TargetRoot, "targetRoot", errors);
        RequireString(manifest.MinV3dfyVersion, "minV3dfyVersion", errors);

        if (!string.Equals(
                manifest.TargetRoot,
                ModelPackManifest.ExpectedTargetRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"Model pack targetRoot must be '{ModelPackManifest.ExpectedTargetRoot}', not '{manifest.TargetRoot}'.");
        }

        var compatibleIw3Versions = manifest.CompatibleIw3Versions ?? [];
        if (compatibleIw3Versions.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(currentIw3Version))
            {
                warnings.Add("Model pack declares compatible iw3 versions, but no current iw3 version was provided.");
            }
            else if (!compatibleIw3Versions.Contains(currentIw3Version, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Model pack is not compatible with bundled iw3 version '{currentIw3Version}'.");
            }
        }

        var files = manifest.Files ?? [];
        if (files.Count == 0)
        {
            errors.Add("Model pack must declare at least one file.");
        }

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            if (!ModelPackPathRules.TryNormalizeModelPackFilePath(file.Path, out var relativePath, out var error))
            {
                errors.Add($"files[{index}].path is invalid: {error}");
                continue;
            }

            if (ModelPackPathRules.IsProtectedRuntimeDependency(relativePath))
            {
                errors.Add($"Model packs must not include protected iw3 runtime dependency: {relativePath}");
                continue;
            }

            ValidateHash(file.Sha256, $"files[{index}].sha256", errors);
            ValidateSize(file.SizeBytes, $"files[{index}].sizeBytes", errors);
            RequireString(file.Role, $"files[{index}].role", errors);

            if (!declaredFiles.TryAdd(
                    relativePath,
                    new DeclaredModelPackFile(
                        relativePath,
                        file.Sha256,
                        file.SizeBytes,
                        file.Role)))
            {
                errors.Add($"Model pack declares duplicate file path variants for '{relativePath}'.");
            }
        }

        var models = manifest.Models ?? [];
        if (models.Count == 0)
        {
            errors.Add("Model pack must declare at least one model.");
        }

        for (var index = 0; index < models.Count; index++)
        {
            var model = models[index];
            RequireString(model.MappingKey, $"models[{index}].mappingKey", errors);
            RequireString(model.Iw3DepthModelName, $"models[{index}].iw3DepthModelName", errors);
            ValidateHash(model.Sha256, $"models[{index}].sha256", errors);
            ValidateSize(model.SizeBytes, $"models[{index}].sizeBytes", errors);

            if (!ModelPackPathRules.TryNormalizeModelPackFilePath(model.File, out var relativePath, out var error))
            {
                errors.Add($"models[{index}].file is invalid: {error}");
                continue;
            }

            if (!declaredFiles.TryGetValue(relativePath, out var declaredFile))
            {
                errors.Add($"models[{index}].file is not declared in files: {relativePath}");
                continue;
            }

            if (!string.Equals(model.Sha256, declaredFile.Sha256, StringComparison.OrdinalIgnoreCase) ||
                model.SizeBytes != declaredFile.SizeBytes)
            {
                errors.Add($"models[{index}] hash and size must match files entry for {relativePath}.");
            }
        }

        var licenses = manifest.Licenses ?? [];
        for (var index = 0; index < licenses.Count; index++)
        {
            if (!ModelPackPathRules.TryNormalizeModelPackFilePath(licenses[index], out var relativePath, out var error))
            {
                errors.Add($"licenses[{index}] is invalid: {error}");
                continue;
            }

            if (!declaredFiles.ContainsKey(relativePath))
            {
                errors.Add($"licenses[{index}] is not declared in files: {relativePath}");
            }
        }

        return declaredFiles;
    }

    private static void ValidateArchiveContents(
        IReadOnlyDictionary<string, ZipArchiveEntry> archiveFiles,
        IReadOnlyDictionary<string, DeclaredModelPackFile> declaredFiles,
        ICollection<string> errors)
    {
        foreach (var archiveFile in archiveFiles.Keys)
        {
            if (string.Equals(archiveFile, ModelPackManifest.FileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!declaredFiles.ContainsKey(archiveFile))
            {
                errors.Add($"ZIP entry is not declared in MODEL_PACK.json: {archiveFile}");
            }
        }

        foreach (var declaredFile in declaredFiles.Keys)
        {
            if (!archiveFiles.ContainsKey(declaredFile))
            {
                errors.Add($"Manifest file is missing from ZIP: {declaredFile}");
            }
        }
    }

    private static async Task ValidateArchiveFileHashesAsync(
        IReadOnlyDictionary<string, ZipArchiveEntry> archiveFiles,
        IReadOnlyDictionary<string, DeclaredModelPackFile> declaredFiles,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        foreach (var declaredFile in declaredFiles.Values)
        {
            var entry = archiveFiles[declaredFile.RelativePath];
            if (entry.Length != declaredFile.SizeBytes)
            {
                errors.Add(
                    $"Size mismatch for {declaredFile.RelativePath}. Expected {declaredFile.SizeBytes} bytes but found {entry.Length} bytes.");
                continue;
            }

            await using var stream = entry.Open();
            var actualSha256 = await ModelPackFileHash.ComputeSha256Async(stream, cancellationToken);
            if (!string.Equals(actualSha256, declaredFile.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"SHA256 mismatch for {declaredFile.RelativePath}. Expected {declaredFile.Sha256.ToUpperInvariant()} but found {actualSha256}.");
            }
        }
    }

    private static async Task CreateFilePlanAsync(
        IReadOnlyDictionary<string, DeclaredModelPackFile> declaredFiles,
        string targetRoot,
        ICollection<ModelPackPlannedFile> filesToInstall,
        ICollection<ModelPackPlannedFile> alreadyInstalled,
        ICollection<ModelPackFileConflict> conflicts,
        CancellationToken cancellationToken)
    {
        foreach (var file in declaredFiles.Values.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var destinationPath = ModelPackPathRules.ResolveDestinationPath(targetRoot, file.RelativePath);
            var plannedFile = new ModelPackPlannedFile(
                file.RelativePath,
                destinationPath,
                file.Sha256,
                file.SizeBytes,
                file.Role);

            if (!File.Exists(destinationPath))
            {
                filesToInstall.Add(plannedFile);
                continue;
            }

            var existingSize = new FileInfo(destinationPath).Length;
            string? existingSha256 = null;
            if (existingSize == file.SizeBytes)
            {
                await using var existingStream = File.OpenRead(destinationPath);
                existingSha256 = await ModelPackFileHash.ComputeSha256Async(existingStream, cancellationToken);
            }

            if (existingSize == file.SizeBytes &&
                string.Equals(existingSha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                alreadyInstalled.Add(plannedFile);
                continue;
            }

            conflicts.Add(new ModelPackFileConflict(
                file.RelativePath,
                destinationPath,
                file.Sha256,
                existingSha256,
                file.SizeBytes,
                existingSize,
                $"Target file already exists with different content: {file.RelativePath}"));
        }
    }

    private static void RequireString(string value, string propertyName, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"MODEL_PACK.json is missing required field '{propertyName}'.");
        }
    }

    private static void ValidateHash(string value, string propertyName, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length != 64 ||
            value.Any(static character => !Uri.IsHexDigit(character)))
        {
            errors.Add($"MODEL_PACK.json contains an invalid {propertyName} value.");
        }
    }

    private static void ValidateSize(long value, string propertyName, ICollection<string> errors)
    {
        if (value <= 0)
        {
            errors.Add($"MODEL_PACK.json contains an invalid {propertyName} value.");
        }
    }

    private sealed record DeclaredModelPackFile(
        string RelativePath,
        string Sha256,
        long SizeBytes,
        string Role);
}
