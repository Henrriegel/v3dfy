using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public sealed class ModelPackInstallPlanner
{
    private const int CopyBufferSize = 1024 * 1024;
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

        var zipPath = NormalizeFullPath(request.ModelPackZipPath);
        var targetRoot = NormalizeFullPath(request.TargetPretrainedModelsRoot);
        var elevationWouldBeRequired = IsUnderProgramFiles(targetRoot);

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
            if (!TryNormalizeRelativePath(entry.FullName, out var relativePath, out var error))
            {
                errors.Add($"ZIP entry '{entry.FullName}' is unsafe: {error}");
                continue;
            }

            if (IsDirectoryEntry(entry))
            {
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
            if (!TryNormalizeModelPackFilePath(file.Path, out var relativePath, out var error))
            {
                errors.Add($"files[{index}].path is invalid: {error}");
                continue;
            }

            if (IsProtectedRuntimeDependency(relativePath))
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

            if (!TryNormalizeModelPackFilePath(model.File, out var relativePath, out var error))
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
            if (!TryNormalizeModelPackFilePath(licenses[index], out var relativePath, out var error))
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
            var actualSha256 = await ComputeSha256Async(stream, cancellationToken);
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
            var destinationPath = ResolveDestinationPath(targetRoot, file.RelativePath);
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
                existingSha256 = await ComputeSha256Async(existingStream, cancellationToken);
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

    private static string ResolveDestinationPath(string targetRoot, string relativePath)
    {
        var destinationPath = Path.GetFullPath(Path.Combine(
            [targetRoot, .. relativePath.Split('/')]));
        if (!IsSameOrUnderDirectory(targetRoot, destinationPath))
        {
            throw new InvalidOperationException($"Resolved destination escapes target root: {relativePath}");
        }

        return destinationPath;
    }

    private static bool TryNormalizeModelPackFilePath(
        string? rawPath,
        out string relativePath,
        out string error)
    {
        if (!TryNormalizeRelativePath(rawPath, out relativePath, out error))
        {
            return false;
        }

        if (string.Equals(relativePath, ModelPackManifest.FileName, StringComparison.OrdinalIgnoreCase))
        {
            error = $"{ModelPackManifest.FileName} is reserved.";
            return false;
        }

        if (relativePath.StartsWith("engine/iw3/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(relativePath, "engine/iw3", StringComparison.OrdinalIgnoreCase))
        {
            error = "Paths are relative to pretrained_models and must not include engine/iw3.";
            return false;
        }

        if (relativePath.StartsWith("pretrained_models/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(relativePath, "pretrained_models", StringComparison.OrdinalIgnoreCase))
        {
            error = "Paths are relative to pretrained_models and must not include pretrained_models.";
            return false;
        }

        return true;
    }

    private static bool TryNormalizeRelativePath(
        string? rawPath,
        out string relativePath,
        out string error)
    {
        relativePath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            error = "Path is required.";
            return false;
        }

        var candidate = rawPath.Trim().Replace('\\', '/');
        if (candidate.StartsWith("//", StringComparison.Ordinal) ||
            candidate.StartsWith("/", StringComparison.Ordinal) ||
            Path.IsPathRooted(candidate))
        {
            error = "Absolute, rooted, and UNC paths are not allowed.";
            return false;
        }

        if (candidate.EndsWith("/", StringComparison.Ordinal))
        {
            error = "File path must not end with a directory separator.";
            return false;
        }

        var segments = candidate.Split('/');
        if (segments.Length == 0 ||
            segments.Any(string.IsNullOrWhiteSpace))
        {
            error = "Path segments must not be empty.";
            return false;
        }

        if (segments.Any(segment => segment is "." or ".."))
        {
            error = "Path traversal segments are not allowed.";
            return false;
        }

        if (segments.Any(segment => segment.Contains(':')))
        {
            error = "Rooted drive paths are not allowed.";
            return false;
        }

        relativePath = string.Join('/', segments);
        return true;
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

    private static bool IsProtectedRuntimeDependency(string relativePath) =>
        string.Equals(
            relativePath,
            "hub/checkpoints/" + Iw3EngineBundleContract.Iw3DefaultStereoRuntimeDependencyFileName,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsDirectoryEntry(ZipArchiveEntry entry) =>
        string.IsNullOrEmpty(entry.Name);

    private static async Task<string> ComputeSha256Async(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        var buffer = new byte[CopyBufferSize];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash ?? []);
    }

    private static bool IsUnderProgramFiles(string path)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };

        return roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(NormalizeFullPath)
            .Any(root => IsSameOrUnderDirectory(root, path));
    }

    private static bool IsSameOrUnderDirectory(string rootDirectory, string candidatePath)
    {
        var root = NormalizeFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = NormalizeFullPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFullPath(string path) =>
        Path.GetFullPath(path);

    private sealed record DeclaredModelPackFile(
        string RelativePath,
        string Sha256,
        long SizeBytes,
        string Role);
}
