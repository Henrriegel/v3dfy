using System.Net;
using System.Security.Cryptography;
using V3dfy.SetupHelper;

namespace V3dfy.Tests.Packaging;

public sealed class InstallerModelPackAcquisitionServiceTests : IDisposable
{
    private readonly string root = TestPaths.TempRoot(
        "installer-model-pack-acquisition",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WebAcquisition_DownloadsSelectedRowAndVerifiesOuterZip()
    {
        var bytes = "synthetic web model pack bytes"u8.ToArray();
        var handler = new RecordingHttpMessageHandler(_ => CreateResponse(bytes));
        var service = new InstallerModelPackAcquisitionService(new HttpClient(handler));
        var row = CreateWebRow("depth-anything-v2-small", bytes, selected: true);

        var result = await service.AcquireAsync(
            [row],
            Path.Combine(root, "work"),
            new RecordingSetupLog(),
            CancellationToken.None,
            new RecordingSetupProgress());

        var acquired = Assert.Single(result.AcquiredFiles);
        Assert.Empty(result.Failures);
        Assert.Equal(row.PackId, acquired.PackId);
        Assert.Equal(row.AssetFileName, Path.GetFileName(acquired.LocalZipPath));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(acquired.LocalZipPath));
        Assert.StartsWith(
            Path.GetFullPath(Path.Combine(root, "work")),
            acquired.LocalZipPath,
            StringComparison.OrdinalIgnoreCase);
        var requestUri = Assert.Single(handler.RequestedUris);
        Assert.Equal(row.Url, requestUri!.ToString());
    }

    [Fact]
    public async Task WebAcquisition_OnlyDownloadsSelectedRows()
    {
        var bytes = "selected bytes"u8.ToArray();
        var handler = new RecordingHttpMessageHandler(_ => CreateResponse(bytes));
        var service = new InstallerModelPackAcquisitionService(new HttpClient(handler));

        var result = await service.AcquireAsync(
            [
                CreateWebRow("selected", bytes, selected: true),
                CreateWebRow("unselected", "unselected bytes"u8.ToArray(), selected: false),
            ],
            Path.Combine(root, "work"),
            new RecordingSetupLog(),
            CancellationToken.None);

        Assert.Single(result.AcquiredFiles);
        Assert.Empty(result.Failures);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("selected", result.AcquiredFiles[0].PackId);
    }

    [Fact]
    public async Task WebAcquisition_SizeMismatchFailsAndDeletesDownload()
    {
        var expectedBytes = "expected bytes"u8.ToArray();
        var downloadedBytes = "wrong length"u8.ToArray();
        var handler = new RecordingHttpMessageHandler(_ => CreateResponse(downloadedBytes));
        var service = new InstallerModelPackAcquisitionService(new HttpClient(handler));
        var row = CreateWebRow("bad-size", expectedBytes, selected: true);
        var workDirectory = Path.Combine(root, "work");

        var result = await service.AcquireAsync(
            [row],
            workDirectory,
            new RecordingSetupLog(),
            CancellationToken.None);

        Assert.Empty(result.AcquiredFiles);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("size mismatch", failure.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(workDirectory, row.AssetFileName)));
        Assert.False(File.Exists(Path.Combine(workDirectory, row.AssetFileName + ".download")));
    }

    [Fact]
    public async Task WebAcquisition_Sha256MismatchFailsAndDeletesDownloadedFile()
    {
        var downloadedBytes = "downloaded bytes"u8.ToArray();
        var row = CreateWebRow(
            "bad-hash",
            downloadedBytes,
            selected: true,
            overrideSha256: Sha256("different bytes"u8.ToArray()));
        var handler = new RecordingHttpMessageHandler(_ => CreateResponse(downloadedBytes));
        var service = new InstallerModelPackAcquisitionService(new HttpClient(handler));
        var workDirectory = Path.Combine(root, "work");

        var result = await service.AcquireAsync(
            [row],
            workDirectory,
            new RecordingSetupLog(),
            CancellationToken.None);

        Assert.Empty(result.AcquiredFiles);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("SHA256 mismatch", failure.Reason, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(workDirectory, row.AssetFileName)));
    }

    [Fact]
    public async Task WebAcquisition_HttpErrorFailsPack()
    {
        var bytes = "web bytes"u8.ToArray();
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound) { ReasonPhrase = "Not Found" });
        var service = new InstallerModelPackAcquisitionService(new HttpClient(handler));
        var row = CreateWebRow("http-error", bytes, selected: true);

        var result = await service.AcquireAsync(
            [row],
            Path.Combine(root, "work"),
            new RecordingSetupLog(),
            CancellationToken.None);

        Assert.Empty(result.AcquiredFiles);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("Download failed", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WebAcquisition_UnsafeAssetFileNameFailsBeforeDownload()
    {
        var bytes = "web bytes"u8.ToArray();
        var handler = new RecordingHttpMessageHandler(_ => CreateResponse(bytes));
        var service = new InstallerModelPackAcquisitionService(new HttpClient(handler));
        var row = CreateWebRow(
            "unsafe",
            bytes,
            selected: true,
            assetFileName: @"..\unsafe.zip");

        var result = await service.AcquireAsync(
            [row],
            Path.Combine(root, "work"),
            new RecordingSetupLog(),
            CancellationToken.None);

        Assert.Empty(result.AcquiredFiles);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("unsafe", failure.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task OfflineAcquisition_VerifiesLocalZipWithoutHttp()
    {
        var bytes = "offline bytes"u8.ToArray();
        var sourcePath = WriteLocalFile("offline-pack.zip", bytes);
        var handler = new RecordingHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be used."));
        var service = new InstallerModelPackAcquisitionService(new HttpClient(handler));
        var row = CreateOfflineRow("offline", bytes, sourcePath, selected: true);

        var result = await service.AcquireAsync(
            [row],
            Path.Combine(root, "work"),
            new RecordingSetupLog(),
            CancellationToken.None);

        var acquired = Assert.Single(result.AcquiredFiles);
        Assert.Empty(result.Failures);
        Assert.Equal(Path.GetFullPath(sourcePath), acquired.LocalZipPath);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task OfflineAcquisition_MissingLocalFileFailsPack()
    {
        var bytes = "offline bytes"u8.ToArray();
        var sourcePath = Path.Combine(root, "missing.zip");
        var service = new InstallerModelPackAcquisitionService(new HttpClient(new RecordingHttpMessageHandler(_ =>
            throw new InvalidOperationException("HTTP should not be used."))));
        var row = CreateOfflineRow("missing", bytes, sourcePath, selected: true);

        var result = await service.AcquireAsync(
            [row],
            Path.Combine(root, "work"),
            new RecordingSetupLog(),
            CancellationToken.None);

        Assert.Empty(result.AcquiredFiles);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("Missing optional model-pack ZIP", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OfflineAcquisition_SizeMismatchFailsPackWithoutDeletingSource()
    {
        var expectedBytes = "expected offline bytes"u8.ToArray();
        var sourcePath = WriteLocalFile("offline-size.zip", "wrong"u8.ToArray());
        var service = new InstallerModelPackAcquisitionService();
        var row = CreateOfflineRow("bad-size", expectedBytes, sourcePath, selected: true);

        var result = await service.AcquireAsync(
            [row],
            Path.Combine(root, "work"),
            new RecordingSetupLog(),
            CancellationToken.None);

        Assert.Empty(result.AcquiredFiles);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("Size mismatch", failure.Reason, StringComparison.Ordinal);
        Assert.True(File.Exists(sourcePath));
    }

    [Fact]
    public async Task OfflineAcquisition_Sha256MismatchFailsPackWithoutDeletingSource()
    {
        var sourceBytes = "offline source"u8.ToArray();
        var sourcePath = WriteLocalFile("offline-hash.zip", sourceBytes);
        var service = new InstallerModelPackAcquisitionService();
        var row = CreateOfflineRow(
            "bad-hash",
            sourceBytes,
            sourcePath,
            selected: true,
            overrideSha256: Sha256("different"u8.ToArray()));

        var result = await service.AcquireAsync(
            [row],
            Path.Combine(root, "work"),
            new RecordingSetupLog(),
            CancellationToken.None);

        Assert.Empty(result.AcquiredFiles);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("SHA256 mismatch", failure.Reason, StringComparison.Ordinal);
        Assert.True(File.Exists(sourcePath));
    }

    [Fact]
    public async Task Acquisition_ContinuesAfterPartialFailure()
    {
        var goodBytes = "good bytes"u8.ToArray();
        var badBytes = "bad bytes"u8.ToArray();
        var handler = new RecordingHttpMessageHandler(request =>
        {
            var fileName = Path.GetFileName(request.RequestUri!.AbsolutePath);
            return fileName.Contains("good", StringComparison.OrdinalIgnoreCase)
                ? CreateResponse(goodBytes)
                : CreateResponse(badBytes);
        });
        var service = new InstallerModelPackAcquisitionService(new HttpClient(handler));
        var badRow = CreateWebRow(
            "bad",
            badBytes,
            selected: true,
            overrideSha256: Sha256("wrong hash"u8.ToArray()));

        var result = await service.AcquireAsync(
            [
                CreateWebRow("good", goodBytes, selected: true),
                badRow,
            ],
            Path.Combine(root, "work"),
            new RecordingSetupLog(),
            CancellationToken.None);

        var acquired = Assert.Single(result.AcquiredFiles);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("good", acquired.PackId);
        Assert.Equal("bad", failure.PackId);
        Assert.True(result.HasFailures);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Acquisition_UnsupportedSourceKindFailsPack()
    {
        var bytes = "bytes"u8.ToArray();
        var service = new InstallerModelPackAcquisitionService();
        var row = new InstallerModelPackSelectionRow(
            "unsupported",
            "Unsupported",
            "Best use",
            "unsupported.zip",
            sourcePath: null,
            "https://example.invalid/unsupported.zip",
            Sha256(bytes),
            bytes.Length,
            "Unsupported",
            isSelected: true,
            isAvailable: true,
            (InstallerModelPackSourceKind)999);

        var result = await service.AcquireAsync(
            [row],
            Path.Combine(root, "work"),
            new RecordingSetupLog(),
            CancellationToken.None);

        Assert.Empty(result.AcquiredFiles);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("Unsupported optional model-pack source kind", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Acquisition_CancellationIsPropagated()
    {
        var bytes = "bytes"u8.ToArray();
        var service = new InstallerModelPackAcquisitionService();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.AcquireAsync(
                [CreateWebRow("canceled", bytes, selected: true)],
                Path.Combine(root, "work"),
                new RecordingSetupLog(),
                cancellationTokenSource.Token));
    }

    private string WriteLocalFile(string fileName, byte[] bytes)
    {
        var directory = Path.Combine(root, "offline");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static InstallerModelPackSelectionRow CreateWebRow(
        string packId,
        byte[] bytes,
        bool selected,
        string? overrideSha256 = null,
        string? assetFileName = null) =>
        new(
            packId,
            packId,
            "Best use",
            assetFileName ?? $"{packId}.zip",
            sourcePath: null,
            $"https://example.invalid/{assetFileName ?? $"{packId}.zip"}",
            overrideSha256 ?? Sha256(bytes),
            bytes.Length,
            "Available download",
            selected,
            isAvailable: true,
            InstallerModelPackSourceKind.WebReleaseAsset);

    private static InstallerModelPackSelectionRow CreateOfflineRow(
        string packId,
        byte[] bytes,
        string sourcePath,
        bool selected,
        string? overrideSha256 = null) =>
        new(
            packId,
            packId,
            "Best use",
            Path.GetFileName(sourcePath),
            sourcePath,
            "https://example.invalid/" + Path.GetFileName(sourcePath),
            overrideSha256 ?? Sha256(bytes),
            bytes.Length,
            "Found beside installer",
            selected,
            isAvailable: true,
            InstallerModelPackSourceKind.OfflineLocalZip);

    private static HttpResponseMessage CreateResponse(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
        };
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private sealed class RecordingHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public List<Uri?> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            RequestedUris.Add(request.RequestUri);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class RecordingSetupLog : ISetupLog
    {
        public List<string> InfoMessages { get; } = [];

        public List<string> WarningMessages { get; } = [];

        public List<string> ErrorMessages { get; } = [];

        public void Info(string message) => InfoMessages.Add(message);

        public void Warning(string message) => WarningMessages.Add(message);

        public void Error(string message) => ErrorMessages.Add(message);
    }

    private sealed class RecordingSetupProgress : ISetupProgress
    {
        public List<SetupProgressEvent> Events { get; } = [];

        public void Report(SetupProgressEvent progress) => Events.Add(progress);
    }
}
