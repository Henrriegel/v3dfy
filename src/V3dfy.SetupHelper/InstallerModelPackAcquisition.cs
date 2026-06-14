using System.Diagnostics;
using System.Security.Cryptography;

namespace V3dfy.SetupHelper;

public sealed record InstallerModelPackAcquiredFile(
    string PackId,
    string DisplayName,
    string AssetFileName,
    InstallerModelPackSourceKind SourceKind,
    string LocalZipPath,
    string ZipSha256,
    long ZipSizeBytes);

public sealed record InstallerModelPackAcquisitionFailure(
    string PackId,
    string DisplayName,
    string AssetFileName,
    InstallerModelPackSourceKind SourceKind,
    string Reason);

public sealed record InstallerModelPackAcquisitionResult(
    IReadOnlyList<InstallerModelPackAcquiredFile> AcquiredFiles,
    IReadOnlyList<InstallerModelPackAcquisitionFailure> Failures)
{
    public int SuccessCount => AcquiredFiles.Count;

    public int FailureCount => Failures.Count;

    public bool HasFailures => FailureCount > 0;
}

public sealed class InstallerModelPackAcquisitionService
{
    private const int CopyBufferSize = 1024 * 1024;

    private static readonly TimeSpan ProgressReportInterval = TimeSpan.FromMilliseconds(250);

    private readonly HttpClient httpClient;

    public InstallerModelPackAcquisitionService(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
    }

    public async Task<InstallerModelPackAcquisitionResult> AcquireAsync(
        IReadOnlyList<InstallerModelPackSelectionRow> rows,
        string workDirectory,
        ISetupLog log,
        CancellationToken cancellationToken,
        ISetupProgress? progress = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(workDirectory);
        ArgumentNullException.ThrowIfNull(log);
        progress ??= NullSetupProgress.Instance;

        var acquired = new List<InstallerModelPackAcquiredFile>();
        var failures = new List<InstallerModelPackAcquisitionFailure>();
        var selectedRows = rows
            .Where(static row => row.IsAvailable && row.IsSelected)
            .ToArray();

        if (selectedRows.Length == 0)
        {
            return new InstallerModelPackAcquisitionResult(acquired, failures);
        }

        var downloadRoot = Path.GetFullPath(workDirectory);
        Directory.CreateDirectory(downloadRoot);

        foreach (var row in selectedRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var acquiredFile = row.SourceKind switch
                {
                    InstallerModelPackSourceKind.WebReleaseAsset => await AcquireWebAsync(
                        row,
                        downloadRoot,
                        log,
                        progress,
                        cancellationToken),
                    InstallerModelPackSourceKind.OfflineLocalZip => await AcquireOfflineAsync(
                        row,
                        log,
                        progress,
                        cancellationToken),
                    _ => throw new PayloadInstallException(
                        $"Unsupported optional model-pack source kind: {row.SourceKind}."),
                };

                acquired.Add(acquiredFile);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var reason = ex is PayloadInstallException ? ex.Message : ex.Message;
                failures.Add(new InstallerModelPackAcquisitionFailure(
                    row.PackId,
                    row.DisplayName,
                    row.AssetFileName,
                    row.SourceKind,
                    reason));
                log.Warning($"Optional model pack failed: {row.DisplayName}. {reason}");
            }
        }

        return new InstallerModelPackAcquisitionResult(acquired, failures);
    }

    private async Task<InstallerModelPackAcquiredFile> AcquireWebAsync(
        InstallerModelPackSelectionRow row,
        string downloadRoot,
        ISetupLog log,
        ISetupProgress progress,
        CancellationToken cancellationToken)
    {
        var destinationPath = GetSafeDownloadPath(downloadRoot, row.AssetFileName);
        log.Info($"Downloading optional model pack {row.DisplayName} from {row.Url}");

        if (File.Exists(destinationPath) &&
            await FileMatchesAsync(destinationPath, row.ZipSha256, row.ZipSizeBytes, cancellationToken))
        {
            log.Info($"Using already verified optional model pack: {row.AssetFileName}");
            progress.Report(new SetupProgressEvent(
                SetupProgressPhase.VerifyingModelPack,
                $"Using already verified optional model pack {row.AssetFileName}.",
                row.AssetFileName,
                row.ZipSizeBytes,
                row.ZipSizeBytes));
            return CreateAcquiredFile(row, destinationPath);
        }

        DeleteIfExists(destinationPath);
        await DownloadFileAsync(row, destinationPath, progress, cancellationToken);
        await VerifyFileAsync(row, destinationPath, log, progress, cancellationToken, deleteOnFailure: true);
        return CreateAcquiredFile(row, destinationPath);
    }

    private static async Task<InstallerModelPackAcquiredFile> AcquireOfflineAsync(
        InstallerModelPackSelectionRow row,
        ISetupLog log,
        ISetupProgress progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.SourcePath))
        {
            throw new PayloadInstallException(
                $"Offline optional model pack has no source path: {row.DisplayName}.");
        }

        var sourcePath = Path.GetFullPath(row.SourcePath);
        log.Info($"Verifying local optional model pack {row.DisplayName}: {sourcePath}");
        await VerifyFileAsync(row, sourcePath, log, progress, cancellationToken, deleteOnFailure: false);
        return CreateAcquiredFile(row, sourcePath);
    }

    private async Task DownloadFileAsync(
        InstallerModelPackSelectionRow row,
        string destinationPath,
        ISetupProgress progress,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(row.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new PayloadInstallException(
                $"Optional model-pack URL is invalid for {row.DisplayName}: {row.Url}");
        }

        var partialPath = destinationPath + ".download";
        DeleteIfExists(partialPath);

        progress.Report(new SetupProgressEvent(
            SetupProgressPhase.DownloadingModelPack,
            $"Downloading optional model pack {row.DisplayName}.",
            row.AssetFileName,
            0,
            row.ZipSizeBytes));

        try
        {
            using var response = await httpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new PayloadInstallException(
                    $"Download failed for optional model pack {row.DisplayName}: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            if (response.Content.Headers.ContentLength is { } contentLength &&
                contentLength != row.ZipSizeBytes)
            {
                throw new PayloadInstallException(
                    $"Download size mismatch for optional model pack {row.DisplayName}. Expected {row.ZipSizeBytes} bytes but the server reported {contentLength} bytes.");
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
                row.ZipSizeBytes,
                row.AssetFileName,
                $"Downloading optional model pack {row.DisplayName}",
                progress,
                cancellationToken);
        }
        catch
        {
            DeleteIfExists(partialPath);
            throw;
        }

        var actualSize = new FileInfo(partialPath).Length;
        if (actualSize != row.ZipSizeBytes)
        {
            DeleteIfExists(partialPath);
            throw new PayloadInstallException(
                $"Download size mismatch for optional model pack {row.DisplayName}. Expected {row.ZipSizeBytes} bytes but received {actualSize} bytes.");
        }

        File.Move(partialPath, destinationPath, overwrite: true);
        progress.Report(new SetupProgressEvent(
            SetupProgressPhase.DownloadingModelPack,
            $"Downloaded optional model pack {row.DisplayName}.",
            row.AssetFileName,
            row.ZipSizeBytes,
            row.ZipSizeBytes));
    }

    private static async Task VerifyFileAsync(
        InstallerModelPackSelectionRow row,
        string filePath,
        ISetupLog log,
        ISetupProgress progress,
        CancellationToken cancellationToken,
        bool deleteOnFailure)
    {
        if (!File.Exists(filePath))
        {
            throw new PayloadInstallException(
                $"Missing optional model-pack ZIP: {filePath}");
        }

        var actualSize = new FileInfo(filePath).Length;
        if (actualSize != row.ZipSizeBytes)
        {
            DeleteIfRequested(filePath, deleteOnFailure);
            throw new PayloadInstallException(
                $"Size mismatch for optional model pack {row.DisplayName}. Expected {row.ZipSizeBytes} bytes but found {actualSize} bytes: {filePath}");
        }

        progress.Report(new SetupProgressEvent(
            SetupProgressPhase.VerifyingModelPack,
            $"Verifying optional model pack {row.DisplayName}.",
            row.AssetFileName,
            0,
            row.ZipSizeBytes));
        var actualSha256 = await ComputeSha256Async(
            filePath,
            row.AssetFileName,
            $"Verifying optional model pack {row.DisplayName}",
            progress,
            cancellationToken);

        if (!string.Equals(actualSha256, row.ZipSha256, StringComparison.OrdinalIgnoreCase))
        {
            DeleteIfRequested(filePath, deleteOnFailure);
            throw new PayloadInstallException(
                $"SHA256 mismatch for optional model pack {row.DisplayName}. Expected {row.ZipSha256.ToUpperInvariant()} but found {actualSha256}: {filePath}");
        }

        log.Info($"Verified optional model pack: {row.DisplayName}");
        progress.Report(new SetupProgressEvent(
            SetupProgressPhase.VerifyingModelPack,
            $"Verified optional model pack {row.DisplayName}.",
            row.AssetFileName,
            row.ZipSizeBytes,
            row.ZipSizeBytes));
    }

    private static InstallerModelPackAcquiredFile CreateAcquiredFile(
        InstallerModelPackSelectionRow row,
        string localZipPath) =>
        new(
            row.PackId,
            row.DisplayName,
            row.AssetFileName,
            row.SourceKind,
            Path.GetFullPath(localZipPath),
            row.ZipSha256,
            row.ZipSizeBytes);

    private static string GetSafeDownloadPath(string downloadRoot, string assetFileName)
    {
        if (!IsSimpleFileName(assetFileName))
        {
            throw new PayloadInstallException(
                $"Optional model-pack asset file name is unsafe: {assetFileName}");
        }

        var rootPrefix = NormalizeDirectoryPrefix(downloadRoot);
        var path = Path.GetFullPath(Path.Combine(downloadRoot, assetFileName));
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new PayloadInstallException(
                $"Optional model-pack download path escapes the work directory: {assetFileName}");
        }

        return path;
    }

    private static bool IsSimpleFileName(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !Path.IsPathRooted(value) &&
        !value.Contains('/', StringComparison.Ordinal) &&
        !value.Contains('\\', StringComparison.Ordinal) &&
        !HasDriveRoot(value) &&
        string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal);

    private static bool HasDriveRoot(string value) =>
        value.Length >= 2 &&
        char.IsAsciiLetter(value[0]) &&
        value[1] == ':';

    private static async Task<bool> FileMatchesAsync(
        string filePath,
        string expectedSha256,
        long expectedSizeBytes,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath) || new FileInfo(filePath).Length != expectedSizeBytes)
        {
            return false;
        }

        var actualSha256 = await ComputeSha256Async(
            filePath,
            Path.GetFileName(filePath),
            "Verifying optional model pack",
            NullSetupProgress.Instance,
            cancellationToken);
        return string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CopyStreamWithProgressAsync(
        Stream source,
        Stream destination,
        long totalBytes,
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
                SetupProgressPhase.DownloadingModelPack,
                message,
                fileName,
                currentBytes,
                totalBytes);
        }

        progress.Report(new SetupProgressEvent(
            SetupProgressPhase.DownloadingModelPack,
            message,
            fileName,
            currentBytes,
            totalBytes));
    }

    private static async Task<string> ComputeSha256Async(
        string filePath,
        string fileName,
        string message,
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
                SetupProgressPhase.VerifyingModelPack,
                message,
                fileName,
                currentBytes,
                totalBytes);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>());
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
            $"{message}: {InstallerModelPackSizeFormatter.FormatBytes(currentBytes)} of {InstallerModelPackSizeFormatter.FormatBytes(totalBytes)}",
            fileName,
            currentBytes,
            totalBytes));
    }

    private static string NormalizeDirectoryPrefix(string directory)
    {
        var fullPath = Path.GetFullPath(directory);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteIfRequested(string path, bool deleteOnFailure)
    {
        if (deleteOnFailure)
        {
            DeleteIfExists(path);
        }
    }
}
