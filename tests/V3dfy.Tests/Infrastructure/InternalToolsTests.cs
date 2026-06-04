using V3dfy.Core.Models;
using V3dfy.Infrastructure.Health;
using V3dfy.Infrastructure.Paths;

namespace V3dfy.Tests.Infrastructure;

public sealed class InternalToolsTests
{
    [Fact]
    public void Resolver_UsesApplicationBaseDirectoryInsteadOfGlobalPath()
    {
        var baseDirectory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "app-root"));

        var paths = new InternalToolPathResolver(baseDirectory).Resolve();

        Assert.Equal(
            Path.Combine(baseDirectory, "tools", "ffmpeg", "win-x64", "ffmpeg.exe"),
            paths.FfmpegExecutable);
        Assert.Equal(
            Path.Combine(baseDirectory, "tools", "ffmpeg", "win-x64", "ffprobe.exe"),
            paths.FfprobeExecutable);
        Assert.Equal(
            Path.Combine(baseDirectory, "engine", "iw3", "python", "python.exe"),
            paths.PythonExecutable);
        Assert.Equal(
            Path.Combine(baseDirectory, "engine", "iw3"),
            paths.Iw3EngineDirectory);
        Assert.Equal(
            Path.Combine(baseDirectory, "engine", "iw3", "models"),
            paths.ModelsDirectory);
        Assert.Equal(
            Path.Combine(baseDirectory, "engine", "iw3", "models", "MODEL_CATALOG.json"),
            paths.ModelCatalogFile);
        Assert.Equal(
            Path.Combine(baseDirectory, "engine", "iw3", "IW3_CLI_CAPABILITIES.json"),
            paths.Iw3CliCapabilitiesFile);
    }

    [Fact]
    public void Resolver_HandlesTrailingSlashesCleanly()
    {
        var baseDirectory = Path.GetFullPath(Path.Combine(
            Environment.CurrentDirectory,
            "app-root-with-trailing-slash")) + Path.DirectorySeparatorChar;
        var normalizedBaseDirectory = Path.GetFullPath(baseDirectory);

        var paths = new InternalToolPathResolver(baseDirectory).Resolve();

        Assert.Equal(
            Path.Combine(normalizedBaseDirectory, "tools", "ffmpeg", "win-x64", "ffmpeg.exe"),
            paths.FfmpegExecutable);
        Assert.Equal(
            Path.Combine(normalizedBaseDirectory, "engine", "iw3", "models", "MODEL_CATALOG.json"),
            paths.ModelCatalogFile);
        Assert.Equal(
            Path.Combine(normalizedBaseDirectory, "engine", "iw3", "IW3_CLI_CAPABILITIES.json"),
            paths.Iw3CliCapabilitiesFile);
    }

    [Fact]
    public void Resolver_DoesNotUseCurrentDirectoryAsRuntimeRoot_WhenRootIsProvided()
    {
        var currentDirectory = Path.GetFullPath(Environment.CurrentDirectory);
        var baseDirectory = Path.GetFullPath(Path.Combine(
            currentDirectory,
            "runtime-root",
            Guid.NewGuid().ToString("N")));

        var paths = new InternalToolPathResolver(baseDirectory).Resolve();

        Assert.StartsWith(
            baseDirectory,
            paths.FfmpegExecutable,
            StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(
            Path.Combine(currentDirectory, "tools", "ffmpeg", "win-x64", "ffmpeg.exe"),
            paths.FfmpegExecutable);
    }

    [Fact]
    public void Resolver_KeepsOptionalMetadataUnderIw3Bundle()
    {
        var baseDirectory = Path.GetFullPath(Path.Combine(
            Environment.CurrentDirectory,
            "metadata-root",
            Guid.NewGuid().ToString("N")));

        var paths = new InternalToolPathResolver(baseDirectory).Resolve();

        Assert.StartsWith(
            paths.Iw3EngineDirectory,
            paths.Iw3CliCapabilitiesFile,
            StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(
            paths.ModelsDirectory,
            paths.ModelCatalogFile,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HealthCheck_MarksAllComponentsMissing_WhenInternalToolsDoNotExist()
    {
        var baseDirectory = Path.Combine(
            Environment.CurrentDirectory,
            "missing-tools",
            Guid.NewGuid().ToString("N"));
        var paths = new InternalToolPathResolver(baseDirectory).Resolve();

        var status = new InternalToolsHealthChecker().Check(paths);

        Assert.Equal(ToolHealthStatus.Missing, status.Ffmpeg);
        Assert.Equal(ToolHealthStatus.Missing, status.Ffprobe);
        Assert.Equal(ToolHealthStatus.Missing, status.Python);
        Assert.Equal(ToolHealthStatus.Missing, status.Iw3EngineDirectory);
        Assert.Equal(ToolHealthStatus.Missing, status.ModelsDirectory);
        Assert.False(status.IsComplete);
    }

    [Fact]
    public void Resolver_ProducesPathsCompatibleWithInternalToolsHealthChecker()
    {
        var paths = CreateToolLayout();

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(paths.ModelCatalogFile, health.ModelInventory.Catalog.CatalogPath);
        Assert.Equal(paths.Iw3CliCapabilitiesFile, health.Iw3CliCapabilities.ManifestPath);
    }

    [Fact]
    public void DetailedHealthCheck_ReturnsExpectedPathsAndMissingFileReasons()
    {
        var paths = CreateToolLayout();

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(paths.FfmpegExecutable, health.Ffmpeg.ExpectedPath);
        Assert.Equal(ToolHealthStatus.Missing, health.Ffmpeg.Status);
        Assert.Equal(ToolHealthDetailKind.BundledFileMissing, health.Ffmpeg.DetailKind);
        Assert.Equal(paths.FfprobeExecutable, health.Ffprobe.ExpectedPath);
        Assert.Equal(ToolHealthDetailKind.BundledFileMissing, health.Ffprobe.DetailKind);
        Assert.Equal(paths.PythonExecutable, health.Python.ExpectedPath);
        Assert.Equal(ToolHealthDetailKind.BundledFileMissing, health.Python.DetailKind);
    }

    [Fact]
    public void HealthCheck_MarksPlaceholderOnlyEngineAndModelsMissing()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.Iw3EngineDirectory);
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.Iw3EngineDirectory, "README.md"), "placeholder");
        File.WriteAllText(Path.Combine(paths.Iw3EngineDirectory, "ENGINE_BUNDLE_CONTRACT.md"), "contract");
        File.WriteAllText(
            Path.Combine(paths.Iw3EngineDirectory, "ENGINE_MANIFEST.json"),
            """{"version":"placeholder"}""");
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "README.md"), "placeholder");

        var status = new InternalToolsHealthChecker().Check(paths);

        Assert.Equal(ToolHealthStatus.Missing, status.Iw3EngineDirectory);
        Assert.Equal(ToolHealthStatus.Missing, status.ModelsDirectory);
    }

    [Fact]
    public void DetailedHealthCheck_ReportsPlaceholderOnlyEngineAndEmptyModels()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.Iw3EngineDirectory);
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.Iw3EngineDirectory, "README.md"), "placeholder");
        File.WriteAllText(Path.Combine(paths.Iw3EngineDirectory, "ENGINE_BUNDLE_CONTRACT.md"), "contract");
        File.WriteAllText(
            Path.Combine(paths.Iw3EngineDirectory, "ENGINE_MANIFEST.json"),
            """{"version":"placeholder"}""");
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "README.md"), "placeholder");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(paths.Iw3EngineDirectory, health.Iw3EngineDirectory.ExpectedPath);
        Assert.Equal(ToolHealthStatus.Missing, health.Iw3EngineDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.EnginePlaceholderOnly, health.Iw3EngineDirectory.DetailKind);
        Assert.Equal(paths.ModelsDirectory, health.ModelsDirectory.ExpectedPath);
        Assert.Equal(ToolHealthStatus.Missing, health.ModelsDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.ModelFilesMissing, health.ModelsDirectory.DetailKind);
    }

    [Fact]
    public void HealthCheck_MarksEngineMissing_WhenEntryExistsWithoutManifest()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.Iw3EngineDirectory);
        File.WriteAllText(Path.Combine(paths.Iw3EngineDirectory, "iw3.py"), "# entrypoint");

        var status = new InternalToolsHealthChecker().Check(paths);

        Assert.Equal(ToolHealthStatus.Missing, status.Iw3EngineDirectory);
    }

    [Fact]
    public void DetailedHealthCheck_ReportsEntryFilesMissing_WhenManifestExistsWithoutEntry()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.Iw3EngineDirectory);
        File.WriteAllText(
            Path.Combine(paths.Iw3EngineDirectory, "ENGINE_MANIFEST.json"),
            """{"version":"1.0.0"}""");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Missing, health.Iw3EngineDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.EngineEntryFilesMissing, health.Iw3EngineDirectory.DetailKind);
    }

    [Fact]
    public void DetailedHealthCheck_ReportsManifestMissing_WhenEntryExistsWithoutManifest()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.Iw3EngineDirectory);
        File.WriteAllText(Path.Combine(paths.Iw3EngineDirectory, "iw3.py"), "# entrypoint");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Missing, health.Iw3EngineDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.EngineManifestMissing, health.Iw3EngineDirectory.DetailKind);
    }

    [Fact]
    public void DetailedHealthCheck_MarksEngineFound_WhenManifestAndEntryExist()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.Iw3EngineDirectory);
        File.WriteAllText(
            Path.Combine(paths.Iw3EngineDirectory, "ENGINE_MANIFEST.json"),
            """{"version":"1.0.0"}""");
        File.WriteAllText(Path.Combine(paths.Iw3EngineDirectory, "iw3.py"), "# entrypoint");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Found, health.Iw3EngineDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.EngineBundleFound, health.Iw3EngineDirectory.DetailKind);
    }

    [Fact]
    public void DetailedHealthCheck_MarksEngineFound_WhenPackageMainAndManifestExist()
    {
        var paths = CreateToolLayout();
        var packageDirectory = Path.Combine(paths.Iw3EngineDirectory, "iw3");
        Directory.CreateDirectory(packageDirectory);
        File.WriteAllText(
            Path.Combine(paths.Iw3EngineDirectory, "ENGINE_MANIFEST.json"),
            """{"version":"1.0.0"}""");
        File.WriteAllText(Path.Combine(packageDirectory, "__main__.py"), "# entrypoint");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Found, health.Iw3EngineDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.EngineBundleFound, health.Iw3EngineDirectory.DetailKind);
    }

    [Fact]
    public void DetailedHealthCheck_MissingCliCapabilitiesManifest_IsSafeAndDoesNotAffectEngineHealth()
    {
        var paths = CreateToolLayout();
        CreateReadyIw3Engine(paths);

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Found, health.Iw3EngineDirectory.Status);
        Assert.Equal(Iw3CliCapabilitiesStatus.Missing, health.Iw3CliCapabilities.Status);
        Assert.Equal(
            paths.Iw3CliCapabilitiesFile,
            health.Iw3CliCapabilities.ManifestPath);
        Assert.False(health.Iw3CliCapabilities.HasVerifiedCapabilities);
    }

    [Fact]
    public void DetailedHealthCheck_InvalidCliCapabilitiesManifest_DoesNotCrashOrAffectEngineHealth()
    {
        var paths = CreateToolLayout();
        CreateReadyIw3Engine(paths);
        File.WriteAllText(
            paths.Iw3CliCapabilitiesFile,
            "{ invalid json");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Found, health.Iw3EngineDirectory.Status);
        Assert.Equal(Iw3CliCapabilitiesStatus.Invalid, health.Iw3CliCapabilities.Status);
        Assert.False(string.IsNullOrWhiteSpace(health.Iw3CliCapabilities.ErrorMessage));
        Assert.False(health.Iw3CliCapabilities.HasVerifiedCapabilities);
    }

    [Fact]
    public void DetailedHealthCheck_PlaceholderCliCapabilitiesManifest_IsNotVerified()
    {
        var paths = CreateToolLayout();
        CreateReadyIw3Engine(paths);
        File.WriteAllText(
            paths.Iw3CliCapabilitiesFile,
            """
            {
              "placeholder": true,
              "verifiedBaseCommand": true,
              "verifiedOptions": ["selected model"]
            }
            """);

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(Iw3CliCapabilitiesStatus.Placeholder, health.Iw3CliCapabilities.Status);
        Assert.False(health.Iw3CliCapabilities.HasVerifiedCapabilities);
        Assert.Empty(health.Iw3CliCapabilities.VerifiedOptions);
    }

    [Fact]
    public void DetailedHealthCheck_CliCapabilitiesManifestAlone_DoesNotMarkEngineFound()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.Iw3EngineDirectory);
        File.WriteAllText(
            paths.Iw3CliCapabilitiesFile,
            """
            {
              "bundledIw3Version": "1.2.3",
              "verifiedBaseCommand": true
            }
            """);

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Missing, health.Iw3EngineDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.EnginePlaceholderOnly, health.Iw3EngineDirectory.DetailKind);
        Assert.Equal(Iw3CliCapabilitiesStatus.Found, health.Iw3CliCapabilities.Status);
        Assert.True(health.Iw3CliCapabilities.HasVerifiedCapabilities);
    }

    [Fact]
    public void DetailedHealthCheck_VerifiedCliCapabilitiesAreMetadataOnly()
    {
        var paths = CreateToolLayout();
        CreateReadyIw3Engine(paths);
        File.WriteAllText(
            paths.Iw3CliCapabilitiesFile,
            """
            {
              "bundledIw3Version": "1.2.3",
              "verifiedBaseCommand": true,
              "verifiedOptions": ["-i", "-o"],
              "unverifiedOptions": ["selected model", "quality preset"],
              "verificationSource": "python -m iw3 -h",
              "verifiedAtUtc": "2026-06-04T00:00:00Z",
              "notes": "Verified during bundle preparation."
            }
            """);

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(Iw3CliCapabilitiesStatus.Found, health.Iw3CliCapabilities.Status);
        Assert.True(health.Iw3CliCapabilities.HasVerifiedCapabilities);
        Assert.Equal("1.2.3", health.Iw3CliCapabilities.BundledIw3Version);
        Assert.True(health.Iw3CliCapabilities.VerifiedBaseCommand);
        Assert.Equal(["-i", "-o"], health.Iw3CliCapabilities.VerifiedOptions);
        Assert.Equal(["selected model", "quality preset"], health.Iw3CliCapabilities.UnverifiedOptions);
        Assert.Equal("python -m iw3 -h", health.Iw3CliCapabilities.VerificationSource);
        Assert.Equal("2026-06-04T00:00:00Z", health.Iw3CliCapabilities.VerifiedAtUtc);
        Assert.Equal("Verified during bundle preparation.", health.Iw3CliCapabilities.Notes);
        Assert.Equal(ToolHealthStatus.Found, health.Iw3EngineDirectory.Status);
    }

    [Theory]
    [InlineData("depth-model.pth")]
    [InlineData("depth-model.onnx")]
    public void HealthCheck_MarksModelsFound_WhenModelFileExists(string fileName)
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, fileName), "model");

        var status = new InternalToolsHealthChecker().Check(paths);

        Assert.Equal(ToolHealthStatus.Found, status.ModelsDirectory);
    }

    [Fact]
    public void DetailedHealthCheck_MarksModelsFound_WhenModelFileExists()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "depth-model.safetensors"), "model");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Found, health.ModelsDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.ModelFilesFound, health.ModelsDirectory.DetailKind);
    }

    [Fact]
    public void DetailedHealthCheck_EmptyModelsDirectory_ReportsNoCompatibleModels()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Missing, health.ModelsDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.ModelFilesMissing, health.ModelsDirectory.DetailKind);
        Assert.True(health.ModelInventory.DirectoryExists);
        Assert.Empty(health.ModelInventory.CompatibleModelFiles);
        Assert.Equal(0, health.ModelInventory.CompatibleModelCount);
    }

    [Fact]
    public void DetailedHealthCheck_IgnoresReadmePlaceholderAndContractModelFiles()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "README.md"), "placeholder");
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "ENGINE_BUNDLE_CONTRACT.md"), "contract");
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "placeholder.pth"), "placeholder");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Missing, health.ModelsDirectory.Status);
        Assert.Equal(ToolHealthDetailKind.ModelFilesMissing, health.ModelsDirectory.DetailKind);
        Assert.Empty(health.ModelInventory.CompatibleModelFiles);
    }

    [Fact]
    public void DetailedHealthCheck_DetectsSupportedModelExtensions()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        foreach (var extension in Iw3EngineBundleContract.SupportedModelExtensions)
        {
            File.WriteAllText(
                Path.Combine(paths.ModelsDirectory, $"depth-model{extension}"),
                "model");
        }

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Found, health.ModelsDirectory.Status);
        Assert.Equal(
            Iw3EngineBundleContract.SupportedModelExtensions.Count,
            health.ModelInventory.CompatibleModelCount);
        Assert.All(
            health.ModelInventory.CompatibleModelFiles,
            modelFile => Assert.Contains(
                modelFile.Extension,
                Iw3EngineBundleContract.SupportedModelExtensions,
                StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void DetailedHealthCheck_IgnoresUnsupportedModelExtensions()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "notes.txt"), "not a model");
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "model.json"), "{}");
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "model.weights"), "not supported");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Missing, health.ModelsDirectory.Status);
        Assert.Empty(health.ModelInventory.CompatibleModelFiles);
    }

    [Fact]
    public void DetailedHealthCheck_DetectsNestedModelFiles()
    {
        var paths = CreateToolLayout();
        var nestedDirectory = Path.Combine(paths.ModelsDirectory, "stereo", "depth");
        Directory.CreateDirectory(nestedDirectory);
        File.WriteAllText(Path.Combine(nestedDirectory, "depth-model.onnx"), "model");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(ToolHealthStatus.Found, health.ModelsDirectory.Status);
        var modelFile = Assert.Single(health.ModelInventory.CompatibleModelFiles);
        Assert.Equal("depth-model.onnx", modelFile.FileName);
        Assert.Equal("stereo/depth/depth-model.onnx", modelFile.RelativePath);
        Assert.Equal(".onnx", modelFile.Extension);
    }

    [Fact]
    public void DetailedHealthCheck_MissingModelCatalog_TreatsCompatibleFilesAsUnmanaged()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "depth-model.onnx"), "model");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(LocalModelCatalogStatus.Missing, health.ModelInventory.Catalog.Status);
        Assert.Equal(
            paths.ModelCatalogFile,
            health.ModelInventory.Catalog.CatalogPath);
        var unmanagedFile = Assert.Single(health.ModelInventory.Catalog.UnmanagedCompatibleModelFiles);
        Assert.Equal("depth-model.onnx", unmanagedFile.RelativePath);
    }

    [Fact]
    public void DetailedHealthCheck_InvalidModelCatalog_DoesNotCrash()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "depth-model.onnx"), "model");
        File.WriteAllText(
            paths.ModelCatalogFile,
            "{ invalid json");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(LocalModelCatalogStatus.Invalid, health.ModelInventory.Catalog.Status);
        Assert.False(string.IsNullOrWhiteSpace(health.ModelInventory.Catalog.ErrorMessage));
        Assert.Single(health.ModelInventory.Catalog.UnmanagedCompatibleModelFiles);
        Assert.Equal(ToolHealthStatus.Found, health.ModelsDirectory.Status);
    }

    [Fact]
    public void DetailedHealthCheck_ModelCatalogWithExistingCompatibleFile_ReportsManagedEntry()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "depth-model.onnx"), "model");
        File.WriteAllText(
            paths.ModelCatalogFile,
            """
            {
              "models": [
                {
                  "id": "depth",
                  "displayName": "Depth model",
                  "file": "depth-model.onnx",
                  "modelType": "depth-estimation",
                  "purpose": "2D to 3D depth"
                }
              ]
            }
            """);

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(LocalModelCatalogStatus.Found, health.ModelInventory.Catalog.Status);
        Assert.Equal(1, health.ModelInventory.Catalog.EntryCount);
        var entry = Assert.Single(health.ModelInventory.Catalog.EntriesWithExistingCompatibleFiles);
        Assert.Equal("depth", entry.Id);
        Assert.True(entry.ReferencedFileExists);
        Assert.True(entry.ReferencedFileIsCompatible);
        Assert.Empty(health.ModelInventory.Catalog.EntriesWithMissingFiles);
        Assert.Empty(health.ModelInventory.Catalog.UnmanagedCompatibleModelFiles);
    }

    [Fact]
    public void DetailedHealthCheck_ModelCatalogEntryWithMissingFile_IsReported()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(
            paths.ModelCatalogFile,
            """
            {
              "models": [
                {
                  "id": "missing-depth",
                  "displayName": "Missing depth model",
                  "file": "missing-depth.onnx",
                  "purpose": "2D to 3D depth"
                }
              ]
            }
            """);

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(LocalModelCatalogStatus.Found, health.ModelInventory.Catalog.Status);
        var entry = Assert.Single(health.ModelInventory.Catalog.EntriesWithMissingFiles);
        Assert.Equal("missing-depth", entry.Id);
        Assert.False(entry.ReferencedFileExists);
        Assert.False(entry.ReferencedFileIsCompatible);
        Assert.Equal(ToolHealthStatus.Missing, health.ModelsDirectory.Status);
    }

    [Fact]
    public void DetailedHealthCheck_CompatibleModelNotListedInCatalog_RemainsUnmanaged()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "unlisted-model.ckpt"), "model");
        File.WriteAllText(
            paths.ModelCatalogFile,
            """{"models":[]}""");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(LocalModelCatalogStatus.Found, health.ModelInventory.Catalog.Status);
        var unmanagedFile = Assert.Single(health.ModelInventory.Catalog.UnmanagedCompatibleModelFiles);
        Assert.Equal("unlisted-model.ckpt", unmanagedFile.RelativePath);
        Assert.Empty(health.ModelInventory.Catalog.Entries);
        Assert.Equal(ToolHealthStatus.Found, health.ModelsDirectory.Status);
    }

    [Fact]
    public void DetailedHealthCheck_ModelCatalogAndPlaceholderFiles_DoNotCountAsModels()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "placeholder.pt"), "placeholder");
        File.WriteAllText(
            paths.ModelCatalogFile,
            """{"version":"placeholder","models":[]}""");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Equal(LocalModelCatalogStatus.Placeholder, health.ModelInventory.Catalog.Status);
        Assert.Empty(health.ModelInventory.CompatibleModelFiles);
        Assert.Equal(ToolHealthStatus.Missing, health.ModelsDirectory.Status);
    }

    [Fact]
    public void DetailedHealthCheck_ModelSelectionCandidatesAreEmpty_WhenNoCompatibleModelsExist()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Empty(health.ModelInventory.SelectionCandidates);
    }

    [Fact]
    public void DetailedHealthCheck_CatalogManagedCompatibleModel_BecomesFriendlySelectionCandidate()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "depth-model.onnx"), "model");
        File.WriteAllText(
            paths.ModelCatalogFile,
            """
            {
              "models": [
                {
                  "id": "depth-default",
                  "displayName": "Default depth model",
                  "file": "depth-model.onnx",
                  "modelType": "depth-estimation",
                  "purpose": "2D to 3D depth"
                }
              ]
            }
            """);

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        var candidate = Assert.Single(health.ModelInventory.SelectionCandidates);
        Assert.True(candidate.IsCatalogManaged);
        Assert.Equal("depth-default", candidate.Id);
        Assert.Equal("Default depth model", candidate.DisplayName);
        Assert.Equal("depth-model.onnx", candidate.RelativePath);
        Assert.Equal("depth-estimation", candidate.ModelType);
        Assert.Equal("2D to 3D depth", candidate.Purpose);
    }

    [Fact]
    public void DetailedHealthCheck_UnmanagedCompatibleModel_BecomesSelectionCandidate()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "unmanaged-depth.safetensors"), "model");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        var candidate = Assert.Single(health.ModelInventory.SelectionCandidates);
        Assert.False(candidate.IsCatalogManaged);
        Assert.Equal("unmanaged-depth.safetensors", candidate.Id);
        Assert.Equal("unmanaged-depth.safetensors", candidate.DisplayName);
        Assert.Equal("unmanaged-depth.safetensors", candidate.RelativePath);
        Assert.Equal(".safetensors", candidate.Extension);
    }

    [Fact]
    public void DetailedHealthCheck_MissingUnsupportedAndUnsafeCatalogReferences_DoNotBecomeSelectionCandidates()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "notes.txt"), "not a supported model");
        File.WriteAllText(
            paths.ModelCatalogFile,
            """
            {
              "models": [
                {
                  "id": "missing",
                  "displayName": "Missing model",
                  "file": "missing.onnx"
                },
                {
                  "id": "unsupported",
                  "displayName": "Unsupported file",
                  "file": "notes.txt"
                },
                {
                  "id": "unsafe",
                  "displayName": "Unsafe file",
                  "file": "../escape.onnx"
                }
              ]
            }
            """);

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Empty(health.ModelInventory.SelectionCandidates);
        Assert.Equal(3, health.ModelInventory.Catalog.EntriesWithMissingFiles.Count);
    }

    [Fact]
    public void DetailedHealthCheck_PlaceholderFiles_DoNotBecomeSelectionCandidates()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(paths.ModelsDirectory);
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "README.md"), "placeholder");
        File.WriteAllText(Path.Combine(paths.ModelsDirectory, "placeholder.onnx"), "placeholder");
        File.WriteAllText(
            paths.ModelCatalogFile,
            """{"version":"placeholder","models":[]}""");

        var health = new InternalToolsHealthChecker().CheckDetailed(paths);

        Assert.Empty(health.ModelInventory.CompatibleModelFiles);
        Assert.Empty(health.ModelInventory.SelectionCandidates);
    }

    [Fact]
    public void HealthCheck_MarksExactExecutableFilesFound()
    {
        var paths = CreateToolLayout();
        Directory.CreateDirectory(Path.GetDirectoryName(paths.FfmpegExecutable)!);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.PythonExecutable)!);
        File.WriteAllText(paths.FfmpegExecutable, "ffmpeg");
        File.WriteAllText(paths.FfprobeExecutable, "ffprobe");
        File.WriteAllText(paths.PythonExecutable, "python");

        var status = new InternalToolsHealthChecker().Check(paths);

        Assert.Equal(ToolHealthStatus.Found, status.Ffmpeg);
        Assert.Equal(ToolHealthStatus.Found, status.Ffprobe);
        Assert.Equal(ToolHealthStatus.Found, status.Python);
    }

    private static InternalToolPaths CreateToolLayout()
    {
        var baseDirectory = Path.Combine(
            Environment.CurrentDirectory,
            "tool-layouts",
            Guid.NewGuid().ToString("N"));

        return new InternalToolPathResolver(baseDirectory).Resolve();
    }

    private static void CreateReadyIw3Engine(InternalToolPaths paths)
    {
        Directory.CreateDirectory(paths.Iw3EngineDirectory);
        File.WriteAllText(
            Path.Combine(paths.Iw3EngineDirectory, "ENGINE_MANIFEST.json"),
            """{"version":"1.0.0"}""");
        File.WriteAllText(Path.Combine(paths.Iw3EngineDirectory, "iw3.py"), "# entrypoint");
    }
}
