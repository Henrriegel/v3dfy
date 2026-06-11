using System.IO.Compression;
using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public sealed class ModelPackInstallExecutor
{
    private readonly ModelPackInstallPlanner planner;
    private readonly IModelPackInstallFileOperations fileOperations;

    public ModelPackInstallExecutor(
        ModelPackInstallPlanner? planner = null,
        IModelPackInstallFileOperations? fileOperations = null)
    {
        this.planner = planner ?? new ModelPackInstallPlanner();
        this.fileOperations = fileOperations ?? new FileSystemModelPackInstallFileOperations();
    }

    public async Task<ModelPackInstallResult> InstallAsync(
        ModelPackInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var plan = await planner.CreateDryRunPlanAsync(
            new ModelPackDryRunInstallRequest(
                request.ModelPackZipPath,
                request.TargetPretrainedModelsRoot,
                request.CurrentIw3Version,
                request.CurrentV3dfyVersion),
            cancellationToken);

        return await InstallAsync(plan, request.StagingRoot, cancellationToken);
    }

    public async Task<ModelPackInstallResult> InstallAsync(
        ModelPackDryRunInstallPlan plan,
        string stagingRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);

        var installedFiles = new List<ModelPackPlannedFile>();
        var alreadyInstalledFiles = new List<ModelPackPlannedFile>(plan.AlreadyInstalledFiles);
        var skippedFiles = new List<ModelPackPlannedFile>(plan.AlreadyInstalledFiles);
        var copiedFiles = new List<ModelPackPlannedFile>();
        var rollbackFilesRemoved = new List<ModelPackPlannedFile>();
        var errors = new List<string>(plan.ValidationErrors);
        var warnings = new List<string>(plan.Warnings);
        string? stagingPath = null;

        foreach (var conflict in plan.Conflicts)
        {
            errors.Add(conflict.Message);
        }

        if (!plan.IsValid)
        {
            return CreateResult(success: false);
        }

        if (plan.FilesToInstall.Count == 0)
        {
            return CreateResult(success: errors.Count == 0);
        }

        stagingPath = ModelPackPathRules.NormalizeFullPath(Path.Combine(
            stagingRoot,
            $"v3dfy-model-pack-{Guid.NewGuid():N}"));
        if (!ModelPackPathRules.IsSameOrUnderDirectory(stagingRoot, stagingPath))
        {
            errors.Add("Resolved staging path escapes the staging root.");
            return CreateResult(success: false);
        }

        try
        {
            fileOperations.CreateDirectory(stagingPath);

            await ExtractPlannedFilesToStagingAsync(
                plan,
                stagingPath,
                errors,
                cancellationToken);
            if (errors.Count > 0)
            {
                return CreateResult(success: false);
            }

            await VerifyStagedFilesAsync(
                plan.FilesToInstall,
                stagingPath,
                errors,
                cancellationToken);
            if (errors.Count > 0)
            {
                return CreateResult(success: false);
            }

            var copiedSuccessfully = await CopyStagedFilesToTargetAsync(
                plan.FilesToInstall,
                stagingPath,
                installedFiles,
                alreadyInstalledFiles,
                skippedFiles,
                copiedFiles,
                errors,
                cancellationToken);
            if (!copiedSuccessfully)
            {
                RollbackCopiedFiles(copiedFiles, installedFiles, rollbackFilesRemoved, errors);
                return CreateResult(success: false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            errors.Add("Model pack install was canceled.");
            RollbackCopiedFiles(copiedFiles, installedFiles, rollbackFilesRemoved, errors);
            return CreateResult(success: false);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            errors.Add($"Model pack install failed: {exception.Message}");
            RollbackCopiedFiles(copiedFiles, installedFiles, rollbackFilesRemoved, errors);
            return CreateResult(success: false);
        }
        finally
        {
            if (stagingPath is not null)
            {
                TryDeleteStagingDirectory(stagingPath, warnings);
            }
        }

        return CreateResult(success: errors.Count == 0);

        ModelPackInstallResult CreateResult(bool success) => new(
            Success: success,
            Manifest: plan.Manifest,
            ModelPackZipPath: plan.ModelPackZipPath,
            TargetPretrainedModelsRoot: plan.TargetPretrainedModelsRoot,
            StagingPath: stagingPath,
            InstalledFiles: installedFiles,
            AlreadyInstalledFiles: alreadyInstalledFiles,
            SkippedFiles: skippedFiles,
            RollbackFilesRemoved: rollbackFilesRemoved,
            Errors: errors,
            Warnings: warnings);
    }

    private async Task ExtractPlannedFilesToStagingAsync(
        ModelPackDryRunInstallPlan plan,
        string stagingPath,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(plan.ModelPackZipPath);
        var archiveFiles = BuildArchiveFileMap(archive, errors);
        if (errors.Count > 0)
        {
            return;
        }

        foreach (var plannedFile in plan.FilesToInstall)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryValidatePlanPath(plannedFile.RelativePath, out var relativePath, out var error))
            {
                errors.Add($"Install plan path is invalid: {error}");
                continue;
            }

            if (!archiveFiles.TryGetValue(relativePath, out var entry))
            {
                errors.Add($"Planned file is missing from ZIP: {relativePath}");
                continue;
            }

            var stagedPath = ModelPackPathRules.ResolveDestinationPath(stagingPath, relativePath);
            fileOperations.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
            await using var input = entry.Open();
            await using var output = new FileStream(
                stagedPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                useAsync: true);
            await input.CopyToAsync(output, cancellationToken);
        }
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

    private async Task VerifyStagedFilesAsync(
        IReadOnlyList<ModelPackPlannedFile> plannedFiles,
        string stagingPath,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        foreach (var plannedFile in plannedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stagedPath = ModelPackPathRules.ResolveDestinationPath(stagingPath, plannedFile.RelativePath);
            if (!await FileMatchesPlanAsync(stagedPath, plannedFile, errors, "Staged file", cancellationToken))
            {
                continue;
            }
        }
    }

    private async Task<bool> CopyStagedFilesToTargetAsync(
        IReadOnlyList<ModelPackPlannedFile> plannedFiles,
        string stagingPath,
        ICollection<ModelPackPlannedFile> installedFiles,
        ICollection<ModelPackPlannedFile> alreadyInstalledFiles,
        ICollection<ModelPackPlannedFile> skippedFiles,
        ICollection<ModelPackPlannedFile> copiedFiles,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        foreach (var plannedFile in plannedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await TrySkipExistingSameHashTargetAsync(
                    plannedFile,
                    alreadyInstalledFiles,
                    skippedFiles,
                    errors,
                    cancellationToken))
            {
                continue;
            }

            if (errors.Count > 0)
            {
                return false;
            }

            var sourcePath = ModelPackPathRules.ResolveDestinationPath(stagingPath, plannedFile.RelativePath);
            var tempDestinationPath = plannedFile.DestinationPath + $".v3dfy-install-{Guid.NewGuid():N}.tmp";
            try
            {
                fileOperations.CreateDirectory(Path.GetDirectoryName(plannedFile.DestinationPath)!);
                await fileOperations.CopyFileAsync(sourcePath, tempDestinationPath, cancellationToken);
                if (!await FileMatchesPlanAsync(
                        tempDestinationPath,
                        plannedFile,
                        errors,
                        "Temporary copy",
                        cancellationToken))
                {
                    fileOperations.DeleteFile(tempDestinationPath);
                    return false;
                }

                if (await TrySkipExistingSameHashTargetAsync(
                        plannedFile,
                        alreadyInstalledFiles,
                        skippedFiles,
                        errors,
                        cancellationToken))
                {
                    fileOperations.DeleteFile(tempDestinationPath);
                    continue;
                }

                if (errors.Count > 0)
                {
                    fileOperations.DeleteFile(tempDestinationPath);
                    return false;
                }

                fileOperations.MoveFile(tempDestinationPath, plannedFile.DestinationPath);
                copiedFiles.Add(plannedFile);

                if (!await FileMatchesPlanAsync(
                        plannedFile.DestinationPath,
                        plannedFile,
                        errors,
                        "Installed file",
                        cancellationToken))
                {
                    return false;
                }

                AddUnique(installedFiles, plannedFile);
            }
            catch (OperationCanceledException)
            {
                fileOperations.DeleteFile(tempDestinationPath);
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                fileOperations.DeleteFile(tempDestinationPath);
                errors.Add($"Failed to install {plannedFile.RelativePath}: {exception.Message}");
                return false;
            }
        }

        return true;
    }

    private async Task<bool> TrySkipExistingSameHashTargetAsync(
        ModelPackPlannedFile plannedFile,
        ICollection<ModelPackPlannedFile> alreadyInstalledFiles,
        ICollection<ModelPackPlannedFile> skippedFiles,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        if (!fileOperations.FileExists(plannedFile.DestinationPath))
        {
            return false;
        }

        var existingLength = fileOperations.GetFileLength(plannedFile.DestinationPath);
        string? existingSha256 = null;
        if (existingLength == plannedFile.SizeBytes)
        {
            await using var stream = fileOperations.OpenRead(plannedFile.DestinationPath);
            existingSha256 = await ModelPackFileHash.ComputeSha256Async(stream, cancellationToken);
        }

        if (existingLength == plannedFile.SizeBytes &&
            string.Equals(existingSha256, plannedFile.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            AddUnique(alreadyInstalledFiles, plannedFile);
            AddUnique(skippedFiles, plannedFile);
            return true;
        }

        errors.Add($"Target file already exists with different content: {plannedFile.RelativePath}");
        return false;
    }

    private async Task<bool> FileMatchesPlanAsync(
        string path,
        ModelPackPlannedFile plannedFile,
        ICollection<string> errors,
        string label,
        CancellationToken cancellationToken)
    {
        if (!fileOperations.FileExists(path))
        {
            errors.Add($"{label} is missing: {plannedFile.RelativePath}");
            return false;
        }

        var actualSize = fileOperations.GetFileLength(path);
        if (actualSize != plannedFile.SizeBytes)
        {
            errors.Add(
                $"{label} size mismatch for {plannedFile.RelativePath}. Expected {plannedFile.SizeBytes} bytes but found {actualSize} bytes.");
            return false;
        }

        await using var stream = fileOperations.OpenRead(path);
        var actualSha256 = await ModelPackFileHash.ComputeSha256Async(stream, cancellationToken);
        if (!string.Equals(actualSha256, plannedFile.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{label} SHA256 mismatch for {plannedFile.RelativePath}. Expected {plannedFile.Sha256.ToUpperInvariant()} but found {actualSha256}.");
            return false;
        }

        return true;
    }

    private static bool TryValidatePlanPath(
        string relativePath,
        out string normalizedRelativePath,
        out string error)
    {
        if (!ModelPackPathRules.TryNormalizeModelPackFilePath(relativePath, out normalizedRelativePath, out error))
        {
            return false;
        }

        if (!string.Equals(relativePath, normalizedRelativePath, StringComparison.OrdinalIgnoreCase))
        {
            error = "Plan path is not normalized.";
            return false;
        }

        if (ModelPackPathRules.IsProtectedRuntimeDependency(normalizedRelativePath))
        {
            error = $"Model packs must not include protected iw3 runtime dependency: {normalizedRelativePath}";
            return false;
        }

        return true;
    }

    private void RollbackCopiedFiles(
        IReadOnlyList<ModelPackPlannedFile> copiedFiles,
        IList<ModelPackPlannedFile> installedFiles,
        ICollection<ModelPackPlannedFile> rollbackFilesRemoved,
        ICollection<string> errors)
    {
        foreach (var copiedFile in copiedFiles.Reverse())
        {
            try
            {
                if (!fileOperations.FileExists(copiedFile.DestinationPath))
                {
                    continue;
                }

                fileOperations.DeleteFile(copiedFile.DestinationPath);
                AddUnique(rollbackFilesRemoved, copiedFile);
                RemoveByRelativePath(installedFiles, copiedFile.RelativePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Add($"Rollback failed for {copiedFile.RelativePath}: {exception.Message}");
            }
        }
    }

    private void TryDeleteStagingDirectory(string stagingPath, ICollection<string> warnings)
    {
        try
        {
            fileOperations.DeleteDirectory(stagingPath, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Could not delete model pack staging directory '{stagingPath}': {exception.Message}");
        }
    }

    private static void AddUnique(
        ICollection<ModelPackPlannedFile> files,
        ModelPackPlannedFile file)
    {
        if (!files.Any(existing =>
                string.Equals(existing.RelativePath, file.RelativePath, StringComparison.OrdinalIgnoreCase)))
        {
            files.Add(file);
        }
    }

    private static void RemoveByRelativePath(
        IList<ModelPackPlannedFile> files,
        string relativePath)
    {
        for (var index = files.Count - 1; index >= 0; index--)
        {
            if (string.Equals(files[index].RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
            {
                files.RemoveAt(index);
            }
        }
    }
}
