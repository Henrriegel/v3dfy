using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using V3dfy.SetupHelper;

namespace V3dfy.Tests.Packaging;

public sealed class PayloadInstallerTests : IDisposable
{
    private readonly string root = TestPaths.TempRoot("payload-installer-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OfflineInstall_VerifiesPartsAndFinalZipBeforeInstalling()
    {
        var fixture = CreatePayloadFixture();
        var targetDirectory = Path.Combine(root, "install");

        await new PayloadInstaller().InstallAsync(
            new PayloadInstallOptions
            {
                Mode = PayloadInstallMode.Offline,
                ManifestPath = fixture.ManifestPath,
                PartsDirectory = fixture.PartsDirectory,
                TargetDirectory = targetDirectory,
                WorkDirectory = Path.Combine(root, "work"),
            },
            new TestSetupLog(),
            CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(targetDirectory, "V3dfy.App.exe")));
        Assert.True(File.Exists(Path.Combine(targetDirectory, "engine", "iw3", "python", "python.exe")));
        Assert.True(File.Exists(Path.Combine(targetDirectory, "tools", "ffmpeg", "win-x64", "ffprobe.exe")));
        Assert.False(Directory.Exists(Path.Combine(root, "work")));
    }

    [Fact]
    public async Task OfflineInstall_EmitsProgressForVerifyRebuildExtractInstallAndCleanup()
    {
        var fixture = CreatePayloadFixture();
        var progress = new CollectingSetupProgress();

        await new PayloadInstaller().InstallAsync(
            new PayloadInstallOptions
            {
                Mode = PayloadInstallMode.Offline,
                ManifestPath = fixture.ManifestPath,
                PartsDirectory = fixture.PartsDirectory,
                TargetDirectory = Path.Combine(root, "install"),
                WorkDirectory = Path.Combine(root, "work"),
            },
            new TestSetupLog(),
            CancellationToken.None,
            progress);

        Assert.Contains(progress.Events, e => e.Phase == SetupProgressPhase.FindingPart &&
            e.Message.Contains("Finding local payload part", StringComparison.Ordinal));
        Assert.Contains(progress.Events, e => e.Phase == SetupProgressPhase.VerifyingPart &&
            e.Message.Contains("Verifying SHA256", StringComparison.Ordinal));
        Assert.Contains(progress.Events, e => e.Phase == SetupProgressPhase.RebuildingZip &&
            e.Message.Contains("Rebuilding payload ZIP", StringComparison.Ordinal));
        Assert.Contains(progress.Events, e => e.Phase == SetupProgressPhase.VerifyingZip &&
            e.Message.Contains("Verifying SHA256", StringComparison.Ordinal));
        Assert.Contains(progress.Events, e => e.Phase == SetupProgressPhase.ExtractingPayload &&
            e.Message.Contains("Extracting", StringComparison.Ordinal));
        Assert.Contains(progress.Events, e => e.Phase == SetupProgressPhase.InstallingPayload &&
            e.Message.Contains("Installing payload", StringComparison.Ordinal));
        Assert.Contains(progress.Events, e => e.Phase == SetupProgressPhase.CleaningUp &&
            e.Message.Contains("Cleaning temporary files", StringComparison.Ordinal));
        Assert.Contains(progress.Events, e => e.Percent is >= 0 and <= 100);
    }

    [Fact]
    public async Task OfflineInstall_RejectsMissingPart()
    {
        var fixture = CreatePayloadFixture();
        File.Delete(Path.Combine(fixture.PartsDirectory, fixture.PartFileNames[1]));

        var exception = await Assert.ThrowsAsync<PayloadInstallException>(() =>
            new PayloadInstaller().InstallAsync(
                new PayloadInstallOptions
                {
                    Mode = PayloadInstallMode.Offline,
                    ManifestPath = fixture.ManifestPath,
                    PartsDirectory = fixture.PartsDirectory,
                    TargetDirectory = Path.Combine(root, "install"),
                    WorkDirectory = Path.Combine(root, "work"),
                },
                new TestSetupLog(),
                CancellationToken.None));

        Assert.Contains("Missing payload part", exception.Message);
        Assert.False(Directory.Exists(Path.Combine(root, "install")));
    }

    [Fact]
    public async Task OfflineInstall_RejectsWrongPartHashBeforeRebuildingZip()
    {
        var fixture = CreatePayloadFixture();
        var firstPartPath = Path.Combine(fixture.PartsDirectory, fixture.PartFileNames[0]);
        var bytes = await File.ReadAllBytesAsync(firstPartPath);
        bytes[0] = (byte)(bytes[0] == 0 ? 1 : bytes[0] - 1);
        await File.WriteAllBytesAsync(firstPartPath, bytes);

        var exception = await Assert.ThrowsAsync<PayloadInstallException>(() =>
            new PayloadInstaller().InstallAsync(
                new PayloadInstallOptions
                {
                    Mode = PayloadInstallMode.Offline,
                    ManifestPath = fixture.ManifestPath,
                    PartsDirectory = fixture.PartsDirectory,
                    TargetDirectory = Path.Combine(root, "install"),
                    WorkDirectory = Path.Combine(root, "work"),
                },
                new TestSetupLog(),
                CancellationToken.None));

        Assert.Contains("SHA256 mismatch for payload part", exception.Message);
        Assert.False(Directory.Exists(Path.Combine(root, "install")));
        Assert.False(File.Exists(Path.Combine(root, "work", fixture.ZipFileName)));
    }

    [Fact]
    public async Task OfflineInstall_RejectsWrongFinalZipHashBeforeExtraction()
    {
        var fixture = CreatePayloadFixture(zipSha256Override: new string('0', 64));

        var exception = await Assert.ThrowsAsync<PayloadInstallException>(() =>
            new PayloadInstaller().InstallAsync(
                new PayloadInstallOptions
                {
                    Mode = PayloadInstallMode.Offline,
                    ManifestPath = fixture.ManifestPath,
                    PartsDirectory = fixture.PartsDirectory,
                    TargetDirectory = Path.Combine(root, "install"),
                    WorkDirectory = Path.Combine(root, "work"),
                },
                new TestSetupLog(),
                CancellationToken.None));

        Assert.Contains("SHA256 mismatch for rebuilt payload ZIP", exception.Message);
        Assert.False(Directory.Exists(Path.Combine(root, "install")));
    }

    private PayloadFixture CreatePayloadFixture(string? zipSha256Override = null)
    {
        var sourceDirectory = Path.Combine(root, "payload-source");
        Directory.CreateDirectory(sourceDirectory);
        WriteFile(sourceDirectory, "V3dfy.App.exe", "app");
        WriteFile(sourceDirectory, Path.Combine("engine", "iw3", "python", "python.exe"), "python");
        WriteFile(sourceDirectory, Path.Combine("engine", "iw3", "nunif", "iw3", "pretrained_models", "model.bin"), "model");
        WriteFile(sourceDirectory, Path.Combine("tools", "ffmpeg", "win-x64", "ffmpeg.exe"), "ffmpeg");
        WriteFile(sourceDirectory, Path.Combine("tools", "ffmpeg", "win-x64", "ffprobe.exe"), "ffprobe");
        WriteFile(sourceDirectory, Path.Combine("licenses", "README.txt"), "licenses");

        var zipFileName = "v3dfy-v0.1.0-preview.1-win-x64-portable.zip";
        var zipPath = Path.Combine(root, zipFileName);
        ZipFile.CreateFromDirectory(sourceDirectory, zipPath, CompressionLevel.NoCompression, includeBaseDirectory: false);

        var partsDirectory = Path.Combine(root, "parts");
        Directory.CreateDirectory(partsDirectory);
        var partFileNames = SplitFile(zipPath, partsDirectory);
        var manifestPath = Path.Combine(root, "payload-manifest.json");
        var manifest = new
        {
            productName = "v3dfy",
            version = "0.1.0-preview.1",
            releaseBaseUrl = "https://example.test/releases/v0.1.0-preview.1",
            zipFileName,
            zipSha256 = zipSha256Override ?? Sha256(zipPath),
            zipSizeBytes = new FileInfo(zipPath).Length,
            parts = partFileNames.Select(fileName =>
            {
                var partPath = Path.Combine(partsDirectory, fileName);
                return new
                {
                    fileName,
                    sha256 = Sha256(partPath),
                    sizeBytes = new FileInfo(partPath).Length,
                    url = "https://example.test/" + fileName,
                };
            }).ToArray(),
            requiredInstalledPaths = new[]
            {
                "V3dfy.App.exe",
                Path.Combine("engine", "iw3"),
                Path.Combine("engine", "iw3", "python", "python.exe"),
                Path.Combine("engine", "iw3", "nunif", "iw3", "pretrained_models"),
                Path.Combine("tools", "ffmpeg", "win-x64", "ffmpeg.exe"),
                Path.Combine("tools", "ffmpeg", "win-x64", "ffprobe.exe"),
                "licenses",
            },
        };

        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true,
            }));

        return new PayloadFixture(partsDirectory, manifestPath, zipFileName, partFileNames);
    }

    private static void WriteFile(string rootDirectory, string relativePath, string contents)
    {
        var path = Path.Combine(rootDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private static string[] SplitFile(string sourcePath, string partsDirectory)
    {
        var bytes = File.ReadAllBytes(sourcePath);
        var partSize = Math.Max(1, (int)Math.Ceiling(bytes.Length / 3.0));
        var names = new List<string>();

        for (var index = 0; index < 3; index++)
        {
            var offset = index * partSize;
            if (offset >= bytes.Length)
            {
                break;
            }

            var count = Math.Min(partSize, bytes.Length - offset);
            var name = $"v3dfy-v0.1.0-preview.1-win-x64-portable.zip.part{index + 1:D2}";
            File.WriteAllBytes(Path.Combine(partsDirectory, name), bytes.AsSpan(offset, count).ToArray());
            names.Add(name);
        }

        return names.ToArray();
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record PayloadFixture(
        string PartsDirectory,
        string ManifestPath,
        string ZipFileName,
        string[] PartFileNames);

    private sealed class TestSetupLog : ISetupLog
    {
        public void Info(string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Error(string message)
        {
        }
    }

    private sealed class CollectingSetupProgress : ISetupProgress
    {
        public List<SetupProgressEvent> Events { get; } = [];

        public void Report(SetupProgressEvent progress) => Events.Add(progress);
    }
}
