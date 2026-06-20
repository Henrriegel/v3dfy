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
    public void SetupProgressEvent_OverallFieldsAreOptional()
    {
        var currentOnly = new SetupProgressEvent(
            SetupProgressPhase.DownloadingPart,
            "Downloading payload part.",
            "payload.part01",
            5,
            10);

        Assert.Equal(50.0, currentOnly.Percent.GetValueOrDefault());
        Assert.Null(currentOnly.OverallCompletedUnits);
        Assert.Null(currentOnly.OverallTotalUnits);
        Assert.Null(currentOnly.OverallMessage);
        Assert.Null(currentOnly.OverallPercent);

        var withOverall = currentOnly with
        {
            OverallCompletedUnits = 3333,
            OverallTotalUnits = 10000,
            OverallMessage = "Verifying package parts",
        };

        Assert.Equal(50.0, withOverall.Percent.GetValueOrDefault());
        Assert.InRange(withOverall.OverallPercent.GetValueOrDefault(), 33.3, 33.4);
        Assert.Equal("Verifying package parts", withOverall.OverallMessage);
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
    public async Task OfflineInstall_ReinstallReplacesExistingTargetAndRemovesStaleFiles()
    {
        var fixture = CreatePayloadFixture();
        var targetDirectory = Path.Combine(root, "install");
        var siblingDirectory = Path.Combine(root, "sibling");
        Directory.CreateDirectory(siblingDirectory);
        var siblingSentinel = Path.Combine(siblingDirectory, "outside-target.txt");
        await File.WriteAllTextAsync(siblingSentinel, "do not delete");

        await new PayloadInstaller().InstallAsync(
            new PayloadInstallOptions
            {
                Mode = PayloadInstallMode.Offline,
                ManifestPath = fixture.ManifestPath,
                PartsDirectory = fixture.PartsDirectory,
                TargetDirectory = targetDirectory,
                WorkDirectory = Path.Combine(root, "work-first"),
            },
            new TestSetupLog(),
            CancellationToken.None);

        var staleFile = Path.Combine(targetDirectory, "leftover-test.txt");
        var importedModel = Path.Combine(
            targetDirectory,
            "engine",
            "iw3",
            "nunif",
            "iw3",
            "pretrained_models",
            "hub",
            "checkpoints",
            "imported-model-pack-test.pt");
        Directory.CreateDirectory(Path.GetDirectoryName(importedModel)!);
        await File.WriteAllTextAsync(staleFile, "stale");
        await File.WriteAllTextAsync(importedModel, "imported model");

        await new PayloadInstaller().InstallAsync(
            new PayloadInstallOptions
            {
                Mode = PayloadInstallMode.Offline,
                ManifestPath = fixture.ManifestPath,
                PartsDirectory = fixture.PartsDirectory,
                TargetDirectory = targetDirectory,
                WorkDirectory = Path.Combine(root, "work-second"),
                AllowTargetReplacement = true,
            },
            new TestSetupLog(),
            CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(targetDirectory, "V3dfy.App.exe")));
        Assert.True(File.Exists(Path.Combine(targetDirectory, "engine", "iw3", "python", "python.exe")));
        Assert.True(File.Exists(Path.Combine(targetDirectory, "tools", "ffmpeg", "win-x64", "ffprobe.exe")));
        Assert.False(File.Exists(staleFile));
        Assert.False(File.Exists(importedModel));
        Assert.True(File.Exists(siblingSentinel));
        Assert.Empty(Directory.EnumerateDirectories(root, "install.previous-*", SearchOption.TopDirectoryOnly));
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
    public async Task OfflineInstall_BlocksExistingTargetUnlessReplacementIsAllowed()
    {
        var fixture = CreatePayloadFixture();
        var targetDirectory = Path.Combine(root, "install");
        var workDirectory = Path.Combine(root, "work");
        Directory.CreateDirectory(targetDirectory);
        var sentinel = Path.Combine(targetDirectory, "existing.txt");
        await File.WriteAllTextAsync(sentinel, "keep");

        var exception = await Assert.ThrowsAsync<PayloadInstallException>(() =>
            new PayloadInstaller().InstallAsync(
                new PayloadInstallOptions
                {
                    Mode = PayloadInstallMode.Offline,
                    ManifestPath = fixture.ManifestPath,
                    PartsDirectory = fixture.PartsDirectory,
                    TargetDirectory = targetDirectory,
                    WorkDirectory = workDirectory,
                },
                new TestSetupLog(),
                CancellationToken.None));

        Assert.Contains("Confirm replacement before continuing", exception.Message);
        Assert.Equal("keep", await File.ReadAllTextAsync(sentinel));
        Assert.False(Directory.Exists(workDirectory));
    }

    [Fact]
    public async Task OfflineInstall_CancelDuringExtractionCleansWorkAndStagingWithoutFinalTarget()
    {
        var fixture = CreatePayloadFixture();
        var targetDirectory = Path.Combine(root, "install");
        var workDirectory = Path.Combine(root, "work");
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelOnPhaseProgress(SetupProgressPhase.ExtractingPayload, cancellation);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new PayloadInstaller().InstallAsync(
                new PayloadInstallOptions
                {
                    Mode = PayloadInstallMode.Offline,
                    ManifestPath = fixture.ManifestPath,
                    PartsDirectory = fixture.PartsDirectory,
                    TargetDirectory = targetDirectory,
                    WorkDirectory = workDirectory,
                },
                new TestSetupLog(),
                cancellation.Token,
                progress));

        Assert.False(Directory.Exists(targetDirectory));
        Assert.False(Directory.Exists(workDirectory));
        Assert.Empty(Directory.EnumerateDirectories(root, "install.staging-*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task OfflineInstall_ReportsFullInstallOverallProgress()
    {
        var fixture = CreatePayloadFixture();
        var progress = new CollectingSetupProgress();

        Assert.Equal(3, fixture.PartFileNames.Length);

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

        var overallEvents = progress.Events
            .Where(static e => e.OverallPercent is not null)
            .ToArray();

        Assert.NotEmpty(overallEvents);
        Assert.All(overallEvents[..^1], e => Assert.True(e.OverallPercent < 100));
        Assert.Equal(100, overallEvents[^1].OverallPercent);
        Assert.Equal(SetupProgressPhase.Completed, overallEvents[^1].Phase);

        for (var index = 1; index < overallEvents.Length; index++)
        {
            Assert.True(
                overallEvents[index].OverallPercent >= overallEvents[index - 1].OverallPercent,
                $"Overall progress decreased from {overallEvents[index - 1].OverallPercent} to {overallEvents[index].OverallPercent} at event {index}.");
        }

        var firstPartVerified = FindCompletedFileEvent(
            progress.Events,
            SetupProgressPhase.VerifyingPart,
            fixture.PartFileNames[0]);
        var secondPartVerified = FindCompletedFileEvent(
            progress.Events,
            SetupProgressPhase.VerifyingPart,
            fixture.PartFileNames[1]);
        var thirdPartVerified = FindCompletedFileEvent(
            progress.Events,
            SetupProgressPhase.VerifyingPart,
            fixture.PartFileNames[2]);
        var rebuildComplete = FindCompletedFileEvent(
            progress.Events,
            SetupProgressPhase.RebuildingZip,
            fixture.ZipFileName);
        var finalZipVerified = FindCompletedFileEvent(
            progress.Events,
            SetupProgressPhase.VerifyingZip,
            fixture.ZipFileName);
        var extractionComplete = progress.Events.Last(e =>
            e.Phase == SetupProgressPhase.ExtractingPayload &&
            e.CurrentFile is null &&
            e.CurrentBytes == e.TotalBytes &&
            e.OverallPercent is not null);
        var installProgress = progress.Events.First(e =>
            e.Phase == SetupProgressPhase.InstallingPayload &&
            e.OverallPercent is not null);
        var cleanupProgress = progress.Events.First(e =>
            e.Phase == SetupProgressPhase.CleaningUp &&
            e.OverallPercent is not null);

        Assert.InRange(firstPartVerified.OverallPercent.GetValueOrDefault(), 1, 100);
        Assert.True(firstPartVerified.OverallPercent < 100);
        Assert.True(secondPartVerified.OverallPercent < 100);
        Assert.True(thirdPartVerified.OverallPercent < 100);
        Assert.True(rebuildComplete.OverallPercent > thirdPartVerified.OverallPercent);
        Assert.True(rebuildComplete.OverallPercent < 100);
        Assert.True(finalZipVerified.OverallPercent > rebuildComplete.OverallPercent);
        Assert.True(finalZipVerified.OverallPercent < 100);
        Assert.True(extractionComplete.OverallPercent > finalZipVerified.OverallPercent);
        Assert.True(extractionComplete.OverallPercent < 100);
        Assert.True(installProgress.OverallPercent > extractionComplete.OverallPercent);
        Assert.True(installProgress.OverallPercent < 100);
        Assert.True(cleanupProgress.OverallPercent > installProgress.OverallPercent);
        Assert.True(cleanupProgress.OverallPercent < 100);

        Assert.Contains(overallEvents, e => e.OverallMessage == "Verifying package parts");
        Assert.Contains(overallEvents, e => e.OverallMessage == "Rebuilding portable package");
        Assert.Contains(overallEvents, e => e.OverallMessage == "Verifying portable package");
        Assert.Contains(overallEvents, e => e.OverallMessage == "Extracting files");
        Assert.Contains(overallEvents, e => e.OverallMessage == "Installing files");
        Assert.Contains(overallEvents, e => e.OverallMessage == "Finalizing installation");
        Assert.Contains(overallEvents, e => e.OverallMessage == "Installation complete");
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

    private static SetupProgressEvent FindCompletedFileEvent(
        IEnumerable<SetupProgressEvent> events,
        SetupProgressPhase phase,
        string fileName) =>
        events.Last(e =>
            e.Phase == phase &&
            string.Equals(e.CurrentFile, fileName, StringComparison.OrdinalIgnoreCase) &&
            e.CurrentBytes == e.TotalBytes &&
            e.OverallPercent is not null);

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

    private sealed class CancelOnPhaseProgress(
        SetupProgressPhase phase,
        CancellationTokenSource cancellation) : ISetupProgress
    {
        public void Report(SetupProgressEvent progress)
        {
            if (progress.Phase == phase)
            {
                cancellation.Cancel();
            }
        }
    }
}
