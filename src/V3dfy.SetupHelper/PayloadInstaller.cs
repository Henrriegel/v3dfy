using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Diagnostics;

namespace V3dfy.SetupHelper;

public sealed class PayloadInstaller
{
    private const int CopyBufferSize = 1024 * 1024;

    private static readonly TimeSpan HttpTimeout = TimeSpan.FromHours(12);

    private static readonly TimeSpan ProgressReportInterval = TimeSpan.FromMilliseconds(250);

    public async Task InstallAsync(
        PayloadInstallOptions options,
        ISetupLog log,
        CancellationToken cancellationToken,
        ISetupProgress? progress = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(log);
        progress ??= NullSetupProgress.Instance;

        progress.Report(new SetupProgressEvent(
            SetupProgressPhase.Preparing,
            "Preparing payload setup."));
        var manifest = await PayloadManifest.LoadAsync(
            NormalizeFullPath(options.ManifestPath),
            cancellationToken);

        var workDirectory = NormalizeFullPath(options.WorkDirectory);
        var targetDirectory = NormalizeFullPath(options.TargetDirectory);
        var targetParent = Directory.GetParent(targetDirectory);
        if (targetParent is null)
        {
            throw new PayloadInstallException($"The install directory is invalid: {targetDirectory}");
        }

        Directory.CreateDirectory(workDirectory);
        Directory.CreateDirectory(targetParent.FullName);

        var stagingDirectory = Path.Combine(
            targetParent.FullName,
            $"{Path.GetFileName(targetDirectory)}.staging-{Guid.NewGuid():N}");

        log.Info($"Installing {manifest.ProductName} {manifest.Version} to {targetDirectory}");
        progress.Report(new SetupProgressEvent(
            SetupProgressPhase.Preparing,
            $"Installing {manifest.ProductName} {manifest.Version}."));

        try
        {
            var partPaths = options.Mode switch
            {
                PayloadInstallMode.Offline => await VerifyOfflinePartsAsync(
                    manifest,
                    options.PartsDirectory,
                    log,
                    progress,
                    cancellationToken),
                PayloadInstallMode.Web => await DownloadAndVerifyPartsAsync(
                    manifest,
                    workDirectory,
                    options.ReleaseBaseUrlOverride,
                    log,
                    progress,
                    cancellationToken),
                _ => throw new PayloadInstallException("Unsupported installer mode."),
            };

            var zipPath = Path.Combine(workDirectory, manifest.ZipFileName);
            EnsureAvailableSpace(
                workDirectory,
                manifest.ZipSizeBytes,
                "temporary rebuilt payload ZIP");

            await RebuildZipAsync(manifest, partPaths, zipPath, log, progress, cancellationToken);
            await VerifyFileAsync(
                zipPath,
                manifest.ZipSha256,
                manifest.ZipSizeBytes,
                "rebuilt payload ZIP",
                SetupProgressPhase.VerifyingZip,
                log,
                progress,
                cancellationToken);

            await ExtractAndInstallAsync(
                manifest,
                zipPath,
                stagingDirectory,
                targetDirectory,
                log,
                progress,
                cancellationToken);

            log.Info("Payload installation completed successfully.");
            progress.Report(new SetupProgressEvent(
                SetupProgressPhase.Completed,
                "Payload installation completed successfully."));
        }
        catch
        {
            TryDeleteDirectory(stagingDirectory, log);
            throw;
        }
        finally
        {
            if (!options.KeepWorkDirectory)
            {
                progress.Report(new SetupProgressEvent(
                    SetupProgressPhase.CleaningUp,
                    "Cleaning temporary files."));
                TryDeleteDirectory(workDirectory, log);
            }
        }
    }

    private static async Task<IReadOnlyList<string>> VerifyOfflinePartsAsync(
        PayloadManifest manifest,
        string? partsDirectory,
        ISetupLog log,
        ISetupProgress progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(partsDirectory))
        {
            throw new PayloadInstallException("The offline installer requires a payload parts directory.");
        }

        var sourceDirectory = NormalizeFullPath(partsDirectory);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new PayloadInstallException(
                $"The payload parts directory does not exist: {sourceDirectory}");
        }

        log.Info($"Reading offline payload parts from {sourceDirectory}");
        var partPaths = new List<string>(manifest.Parts.Count);
        foreach (var part in manifest.Parts)
        {
            var partPath = Path.Combine(sourceDirectory, part.FileName);
            progress.Report(new SetupProgressEvent(
                SetupProgressPhase.FindingPart,
                $"Finding local payload part {part.FileName}.",
                part.FileName));
            await VerifyFileAsync(
                partPath,
                part.Sha256,
                part.SizeBytes,
                $"payload part {part.FileName}",
                SetupProgressPhase.VerifyingPart,
                log,
                progress,
                cancellationToken);
            partPaths.Add(partPath);
        }

        return partPaths;
    }

    private static async Task<IReadOnlyList<string>> DownloadAndVerifyPartsAsync(
        PayloadManifest manifest,
        string workDirectory,
        string? releaseBaseUrlOverride,
        ISetupLog log,
        ISetupProgress progress,
        CancellationToken cancellationToken)
    {
        var downloadDirectory = Path.Combine(workDirectory, "parts");
        Directory.CreateDirectory(downloadDirectory);

        var temporaryBytes = manifest.Parts.Sum(static part => part.SizeBytes) + manifest.ZipSizeBytes;
        EnsureAvailableSpace(
            workDirectory,
            temporaryBytes,
            "temporary downloaded parts and rebuilt payload ZIP");

        using var httpClient = new HttpClient
        {
            Timeout = HttpTimeout,
        };
        httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("v3dfy-setup", manifest.Version));

        var partPaths = new List<string>(manifest.Parts.Count);
        foreach (var part in manifest.Parts)
        {
            var partPath = Path.Combine(downloadDirectory, part.FileName);
            if (File.Exists(partPath) &&
                await FileMatchesAsync(partPath, part.Sha256, part.SizeBytes, cancellationToken))
            {
                log.Info($"Using already verified downloaded part {part.FileName}");
                progress.Report(new SetupProgressEvent(
                    SetupProgressPhase.VerifyingPart,
                    $"Using already verified downloaded part {part.FileName}.",
                    part.FileName,
                    part.SizeBytes,
                    part.SizeBytes));
                partPaths.Add(partPath);
                continue;
            }

            if (File.Exists(partPath))
            {
                File.Delete(partPath);
            }

            var url = BuildPartUrl(manifest, part, releaseBaseUrlOverride);
            log.Info($"Downloading {part.FileName} from {url}");
            await DownloadFileAsync(httpClient, url, partPath, part, progress, cancellationToken);
            await VerifyFileAsync(
                partPath,
                part.Sha256,
                part.SizeBytes,
                $"downloaded payload part {part.FileName}",
                SetupProgressPhase.VerifyingPart,
                log,
                progress,
                cancellationToken);
            partPaths.Add(partPath);
        }

        return partPaths;
    }

    private static async Task DownloadFileAsync(
        HttpClient httpClient,
        string url,
        string destinationPath,
        PayloadPart part,
        ISetupProgress progress,
        CancellationToken cancellationToken)
    {
        var expectedSizeBytes = part.SizeBytes;
        var partialPath = destinationPath + ".download";
        if (File.Exists(partialPath))
        {
            File.Delete(partialPath);
        }

        progress.Report(new SetupProgressEvent(
            SetupProgressPhase.DownloadingPart,
            $"Downloading {part.FileName}.",
            part.FileName,
            0,
            expectedSizeBytes));

        try
        {
            using var response = await httpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new PayloadInstallException(
                    $"Download failed for {url}: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            if (response.Content.Headers.ContentLength is { } contentLength &&
                contentLength != expectedSizeBytes)
            {
                throw new PayloadInstallException(
                    $"Download size mismatch for {url}. Expected {expectedSizeBytes} bytes but the server reported {contentLength} bytes.");
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                FileOptions.SequentialScan);

            await CopyStreamWithProgressAsync(
                source,
                destination,
                expectedSizeBytes,
                SetupProgressPhase.DownloadingPart,
                part.FileName,
                $"Downloading {part.FileName}",
                progress,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not PayloadInstallException)
        {
            throw new PayloadInstallException($"Download failed for {url}: {ex.Message}", ex);
        }

        var actualSize = new FileInfo(partialPath).Length;
        if (actualSize != expectedSizeBytes)
        {
            File.Delete(partialPath);
            throw new PayloadInstallException(
                $"Download size mismatch for {url}. Expected {expectedSizeBytes} bytes but received {actualSize} bytes.");
        }

        File.Move(partialPath, destinationPath, overwrite: true);
        progress.Report(new SetupProgressEvent(
            SetupProgressPhase.DownloadingPart,
            $"Downloaded {part.FileName}.",
            part.FileName,
            expectedSizeBytes,
            expectedSizeBytes));
    }

    private static async Task RebuildZipAsync(
        PayloadManifest manifest,
        IReadOnlyList<string> partPaths,
        string zipPath,
        ISetupLog log,
        ISetupProgress progress,
        CancellationToken cancellationToken)
    {
        if (partPaths.Count != manifest.Parts.Count)
        {
            throw new PayloadInstallException("The payload part count does not match the manifest.");
        }

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        log.Info($"Rebuilding payload ZIP: {zipPath}");
        progress.Report(new SetupProgressEvent(
            SetupProgressPhase.RebuildingZip,
            "Rebuilding payload ZIP.",
            manifest.ZipFileName,
            0,
            manifest.ZipSizeBytes));
        await using var output = new FileStream(
            zipPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            FileOptions.SequentialScan);

        long rebuiltBytes = 0;
        var stopwatch = Stopwatch.StartNew();
        foreach (var partPath in partPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var input = new FileStream(
                partPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.SequentialScan);

            var buffer = new byte[CopyBufferSize];
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                rebuiltBytes += bytesRead;
                ReportByteProgress(
                    progress,
                    stopwatch,
                    SetupProgressPhase.RebuildingZip,
                    $"Rebuilding payload ZIP from {Path.GetFileName(partPath)}",
                    manifest.ZipFileName,
                    rebuiltBytes,
                    manifest.ZipSizeBytes);
            }
        }

        progress.Report(new SetupProgressEvent(
            SetupProgressPhase.RebuildingZip,
            "Rebuilt payload ZIP.",
            manifest.ZipFileName,
            manifest.ZipSizeBytes,
            manifest.ZipSizeBytes));
    }

    private static async Task ExtractAndInstallAsync(
        PayloadManifest manifest,
        string zipPath,
        string stagingDirectory,
        string targetDirectory,
        ISetupLog log,
        ISetupProgress progress,
        CancellationToken cancellationToken)
    {
        if (Directory.Exists(stagingDirectory))
        {
            Directory.Delete(stagingDirectory, recursive: true);
        }

        Directory.CreateDirectory(stagingDirectory);

        using var archive = ZipFile.OpenRead(zipPath);
        var uncompressedBytes = archive.Entries.Sum(static entry => entry.Length);
        EnsureAvailableSpace(stagingDirectory, uncompressedBytes, "temporary extracted payload");

        log.Info($"Extracting payload ZIP to staging directory: {stagingDirectory}");
        progress.Report(new SetupProgressEvent(
            SetupProgressPhase.ExtractingPayload,
            "Extracting payload.",
            manifest.ZipFileName,
            0,
            uncompressedBytes));
        await ExtractArchiveSafelyAsync(archive, stagingDirectory, progress, uncompressedBytes, cancellationToken);
        VerifyInstalledPayload(stagingDirectory, manifest.RequiredInstalledPaths);
        InstallStagedDirectory(stagingDirectory, targetDirectory, log, progress);
    }

    private static async Task ExtractArchiveSafelyAsync(
        ZipArchive archive,
        string destinationDirectory,
        ISetupProgress progress,
        long uncompressedBytes,
        CancellationToken cancellationToken)
    {
        var destinationRoot = NormalizeDirectoryPrefix(destinationDirectory);
        long extractedBytes = 0;
        var stopwatch = Stopwatch.StartNew();

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
            if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new PayloadInstallException(
                    $"The payload ZIP contains an unsafe path: {entry.FullName}");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var destinationParent = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationParent))
            {
                Directory.CreateDirectory(destinationParent);
            }

            using var source = entry.Open();
            await using var destination = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                FileOptions.SequentialScan);

            var buffer = new byte[CopyBufferSize];
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                extractedBytes += bytesRead;
                ReportByteProgress(
                    progress,
                    stopwatch,
                    SetupProgressPhase.ExtractingPayload,
                    $"Extracting {entry.FullName}",
                    entry.FullName,
                    extractedBytes,
                    uncompressedBytes);
            }

            if (entry.LastWriteTime != DateTimeOffset.MinValue)
            {
                File.SetLastWriteTime(destinationPath, entry.LastWriteTime.LocalDateTime);
            }
        }

        progress.Report(new SetupProgressEvent(
            SetupProgressPhase.ExtractingPayload,
            "Extracted payload.",
            null,
            uncompressedBytes,
            uncompressedBytes));
    }

    private static void VerifyInstalledPayload(
        string stagingDirectory,
        IReadOnlyList<string> requiredInstalledPaths)
    {
        foreach (var requiredPath in requiredInstalledPaths)
        {
            var fullPath = Path.GetFullPath(Path.Combine(stagingDirectory, requiredPath));
            if (!fullPath.StartsWith(
                    NormalizeDirectoryPrefix(stagingDirectory),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new PayloadInstallException(
                    $"The payload manifest contains an unsafe required path: {requiredPath}");
            }

            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                throw new PayloadInstallException(
                    $"The extracted payload is missing required file or directory: {requiredPath}");
            }
        }
    }

    private static void InstallStagedDirectory(
        string stagingDirectory,
        string targetDirectory,
        ISetupLog log,
        ISetupProgress progress)
    {
        var targetParent = Directory.GetParent(targetDirectory);
        if (targetParent is null)
        {
            throw new PayloadInstallException($"The install directory is invalid: {targetDirectory}");
        }

        var backupDirectory = Path.Combine(
            targetParent.FullName,
            $"{Path.GetFileName(targetDirectory)}.previous-{Guid.NewGuid():N}");

        var hadExistingTarget = Directory.Exists(targetDirectory);
        try
        {
            progress.Report(new SetupProgressEvent(
                SetupProgressPhase.InstallingPayload,
                "Installing payload into Program Files."));
            if (hadExistingTarget)
            {
                log.Info($"Moving existing install directory to temporary backup: {backupDirectory}");
                progress.Report(new SetupProgressEvent(
                    SetupProgressPhase.InstallingPayload,
                    "Moving existing install directory to a temporary backup."));
                Directory.Move(targetDirectory, backupDirectory);
            }

            log.Info($"Moving staged payload into install directory: {targetDirectory}");
            progress.Report(new SetupProgressEvent(
                SetupProgressPhase.InstallingPayload,
                "Moving staged payload into the install directory."));
            Directory.Move(stagingDirectory, targetDirectory);
        }
        catch (Exception ex)
        {
            TryRestoreBackup(targetDirectory, backupDirectory, log);
            throw new PayloadInstallException(
                $"Failed to install the staged payload into {targetDirectory}: {ex.Message}",
                ex);
        }

        if (hadExistingTarget)
        {
            progress.Report(new SetupProgressEvent(
                SetupProgressPhase.CleaningUp,
                "Cleaning previous install backup."));
            TryDeleteDirectory(backupDirectory, log);
        }
    }

    private static void TryRestoreBackup(
        string targetDirectory,
        string backupDirectory,
        ISetupLog log)
    {
        try
        {
            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(targetDirectory, recursive: true);
            }

            if (Directory.Exists(backupDirectory))
            {
                Directory.Move(backupDirectory, targetDirectory);
                log.Warning("Restored the previous install directory after an install failure.");
            }
        }
        catch (Exception restoreException)
        {
            log.Warning($"Could not restore the previous install directory: {restoreException.Message}");
        }
    }

    private static async Task VerifyFileAsync(
        string filePath,
        string expectedSha256,
        long expectedSizeBytes,
        string description,
        SetupProgressPhase phase,
        ISetupLog log,
        ISetupProgress progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new PayloadInstallException(
                $"Missing {description}: {filePath}");
        }

        var actualSize = new FileInfo(filePath).Length;
        if (actualSize != expectedSizeBytes)
        {
            throw new PayloadInstallException(
                $"Size mismatch for {description}. Expected {expectedSizeBytes} bytes but found {actualSize} bytes: {filePath}");
        }

        var fileName = Path.GetFileName(filePath);
        progress.Report(new SetupProgressEvent(
            phase,
            $"Verifying SHA256 for {description}.",
            fileName,
            0,
            expectedSizeBytes));

        var actualSha256 = await ComputeSha256Async(
            filePath,
            phase,
            $"Verifying SHA256 for {description}",
            fileName,
            progress,
            cancellationToken);
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new PayloadInstallException(
                $"SHA256 mismatch for {description}. Expected {expectedSha256.ToUpperInvariant()} but found {actualSha256}: {filePath}");
        }

        log.Info($"Verified {description}: {Path.GetFileName(filePath)}");
        progress.Report(new SetupProgressEvent(
            phase,
            $"Verified {description}.",
            fileName,
            expectedSizeBytes,
            expectedSizeBytes));
    }

    private static async Task<bool> FileMatchesAsync(
        string filePath,
        string expectedSha256,
        long expectedSizeBytes,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        if (new FileInfo(filePath).Length != expectedSizeBytes)
        {
            return false;
        }

        var actualSha256 = await ComputeSha256Async(filePath, cancellationToken);
        return string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken)
    {
        return await ComputeSha256Async(
            filePath,
            SetupProgressPhase.VerifyingPart,
            "Verifying SHA256",
            Path.GetFileName(filePath),
            NullSetupProgress.Instance,
            cancellationToken);
    }

    private static async Task<string> ComputeSha256Async(
        string filePath,
        SetupProgressPhase phase,
        string message,
        string fileName,
        ISetupProgress progress,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.SequentialScan);

        using var sha256 = SHA256.Create();
        var buffer = new byte[CopyBufferSize];
        var totalBytes = stream.Length;
        long currentBytes = 0;
        var stopwatch = Stopwatch.StartNew();
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            currentBytes += bytesRead;
            ReportByteProgress(
                progress,
                stopwatch,
                phase,
                message,
                fileName,
                currentBytes,
                totalBytes);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>());
    }

    private static async Task CopyStreamWithProgressAsync(
        Stream source,
        Stream destination,
        long totalBytes,
        SetupProgressPhase phase,
        string fileName,
        string message,
        ISetupProgress progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferSize];
        long currentBytes = 0;
        var stopwatch = Stopwatch.StartNew();
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            currentBytes += bytesRead;
            ReportByteProgress(
                progress,
                stopwatch,
                phase,
                message,
                fileName,
                currentBytes,
                totalBytes);
        }

        progress.Report(new SetupProgressEvent(
            phase,
            message,
            fileName,
            currentBytes,
            totalBytes));
    }

    private static void ReportByteProgress(
        ISetupProgress progress,
        Stopwatch stopwatch,
        SetupProgressPhase phase,
        string message,
        string fileName,
        long currentBytes,
        long totalBytes)
    {
        if (currentBytes < totalBytes && stopwatch.Elapsed < ProgressReportInterval)
        {
            return;
        }

        stopwatch.Restart();
        progress.Report(new SetupProgressEvent(
            phase,
            $"{message}: {FormatBytes(currentBytes)} of {FormatBytes(totalBytes)}",
            fileName,
            currentBytes,
            totalBytes));
    }

    private static string BuildPartUrl(
        PayloadManifest manifest,
        PayloadPart part,
        string? releaseBaseUrlOverride)
    {
        if (!string.IsNullOrWhiteSpace(releaseBaseUrlOverride))
        {
            return CombineUrl(releaseBaseUrlOverride, part.FileName);
        }

        if (!string.IsNullOrWhiteSpace(part.Url))
        {
            return part.Url;
        }

        if (string.IsNullOrWhiteSpace(manifest.ReleaseBaseUrl))
        {
            throw new PayloadInstallException(
                $"No download URL is configured for payload part {part.FileName}.");
        }

        return CombineUrl(manifest.ReleaseBaseUrl, part.FileName);
    }

    private static string CombineUrl(string baseUrl, string fileName) =>
        $"{baseUrl.TrimEnd('/')}/{Uri.EscapeDataString(fileName)}";

    private static void EnsureAvailableSpace(
        string path,
        long requiredBytes,
        string purpose)
    {
        var fullPath = NormalizeFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new PayloadInstallException($"Could not determine drive for {fullPath}.");
        }

        var drive = new DriveInfo(root);
        if (!drive.IsReady)
        {
            throw new PayloadInstallException($"Drive {root} is not ready.");
        }

        var bufferBytes = Math.Min(512L * 1024L * 1024L, Math.Max(64L * 1024L * 1024L, requiredBytes / 20L));
        var totalRequired = requiredBytes + bufferBytes;
        if (drive.AvailableFreeSpace < totalRequired)
        {
            throw new PayloadInstallException(
                $"Insufficient disk space for {purpose} on drive {drive.Name}. Required at least {FormatBytes(totalRequired)} free; available {FormatBytes(drive.AvailableFreeSpace)}.");
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private static void TryDeleteDirectory(string directory, ISetupLog log)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex)
        {
            log.Warning($"Could not delete temporary directory {directory}: {ex.Message}");
        }
    }

    private static string NormalizeFullPath(string path) =>
        Path.GetFullPath(path);

    private static string NormalizeDirectoryPrefix(string directory)
    {
        var fullPath = NormalizeFullPath(directory);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }
}
