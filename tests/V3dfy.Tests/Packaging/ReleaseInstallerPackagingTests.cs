namespace V3dfy.Tests.Packaging;

public sealed class ReleaseInstallerPackagingTests
{
    private static readonly string Backslash = new([Path.DirectorySeparatorChar]);

    [Fact]
    public void PackagingDocs_ListReleaseInstallerOptionsAndRequirements()
    {
        var docs = ReadRepoFile("docs", "packaging.md");

        Assert.Contains("v3dfy-v0.1.0-preview.1-web-setup.exe", docs);
        Assert.Contains("v3dfy-v0.1.0-preview.1-offline-setup.exe", docs);
        Assert.Contains("README_WEB_INSTALLER.txt", docs);
        Assert.Contains("README_OFFLINE_INSTALLER.txt", docs);
        Assert.Contains("SHA256SUMS.installers.txt", docs);
        Assert.Contains("Internet is required during installation", docs);
        Assert.Contains("same folder", docs);
        Assert.Contains("No internet and no PowerShell are required", docs);
        Assert.Contains("They do not duplicate the 5.4 GB payload into Inno", docs);
        Assert.Contains("classic installer-style v3dfy setup progress window", docs);
        Assert.Contains("large timestamped scrolling setup log", docs);
        Assert.Contains("Uninstall removes the complete `{app}` install tree", docs);
        Assert.Contains("model packs", docs);
        Assert.Contains("engine\\iw3\\nunif\\iw3\\pretrained_models", docs);
        Assert.Contains("User videos, converted outputs, and", docs);
        Assert.Contains("AppData are outside `{app}`", docs);
        Assert.Contains("package-release-payload.ps1", docs);
        Assert.Contains("package-release-installers.ps1", docs);
        Assert.Contains("publish `artifacts\\publish\\v3dfy-win-x64`", docs);
        Assert.Contains("ZIP is older than the publish output", docs);

        var readme = ReadRepoFile("README.md");
        Assert.Contains("package-release-payload.ps1", readme);
        Assert.Contains("package-release-installers.ps1", readme);
        Assert.Contains("publish `artifacts\\publish\\v3dfy-win-x64`", readme);
    }

    [Fact]
    public void PackagingScript_ConfiguresExpectedAssetNames()
    {
        var script = ReadRepoFile("scripts", "package-release-installers.ps1");

        Assert.Contains("v3dfy-v$Version-web-setup.exe", script);
        Assert.Contains("v3dfy-v$Version-offline-setup.exe", script);
        Assert.Contains("$bootstrapScript = Join-Path $repoRoot 'packaging\\inno\\v3dfy-payload-bootstrap.iss'", script);
        Assert.Contains("'/DWebInstaller=1'", script);
        Assert.Contains("'/DOfflineInstaller=1'", script);
        Assert.Contains("-Flavor 'web'", script);
        Assert.Contains("-Flavor 'offline'", script);
        Assert.Contains("README_WEB_INSTALLER.txt", script);
        Assert.Contains("README_OFFLINE_INSTALLER.txt", script);
        Assert.Contains("SHA256SUMS.installers.txt", script);
        Assert.Contains("[string]$ModelPackManifestPath", script);
        Assert.Contains("Get-OptionalInstallerModelPackManifest $ModelPackManifestPath", script);
        Assert.Contains("/DModelPackManifestFile=$ModelPackManifestFile", script);
        Assert.Contains("artifacts\\release\\split", script);
        Assert.Contains("artifacts\\installer", script);
        Assert.Contains("https://github.com/Henrriegel/v3dfy/releases/download/v0.1.0-preview.1", script);
        Assert.Contains("live download", script);
        Assert.Contains("large scrolling setup log", script);
        Assert.Contains("--framework net10.0-windows", script);
    }

    [Fact]
    public void InstallerPackagingScript_ConsumesOptionalModelPackManifestWithoutBuildingPacks()
    {
        var script = ReadRepoFile("scripts", "package-release-installers.ps1");
        var validationFunction = ExtractSourceRange(
            script,
            "function Get-OptionalInstallerModelPackManifest",
            "function Get-PayloadPartFiles");
        var innoFunction = ExtractSourceRange(
            script,
            "function Invoke-InnoCompiler",
            "function Write-InstallerChecksums");

        Assert.Contains("[string]$ModelPackManifestPath", script);
        Assert.Contains("Get-RequiredFile $resolvedPath 'Installer model-pack manifest'", validationFunction);
        Assert.Contains("ConvertFrom-Json -ErrorAction Stop", validationFunction);
        Assert.Contains("$json.schemaVersion -ne 1", validationFunction);
        Assert.Contains("$json.packs", validationFunction);
        Assert.Contains("assetFileName", validationFunction);
        Assert.Contains("zipSha256", validationFunction);
        Assert.Contains("zipSizeBytes", validationFunction);
        Assert.Contains("[string]$ModelPackManifestFile", innoFunction);
        Assert.Contains("/DModelPackManifestFile=$ModelPackManifestFile", innoFunction);
        Assert.Contains("-ModelPackManifestFile $installerModelPackManifestFullName", script);
        Assert.DoesNotContain("build-model-packs.ps1", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invoke-WebRequest", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Start-BitsTransfer", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Compress-Archive", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleasePayloadScript_CreatesPortableZipAndSplitPartsFromPublishOutput()
    {
        var script = ReadRepoFile("scripts", "package-release-payload.ps1");

        Assert.Contains("[string]$PublishDir = 'artifacts\\publish\\v3dfy-win-x64'", script);
        Assert.Contains("[string]$ReleaseDir = 'artifacts\\release'", script);
        Assert.Contains("[string]$SplitDir = 'artifacts\\release\\split'", script);
        Assert.Contains("[int]$PartCount = 3", script);
        Assert.Contains("$portableZipFileName = \"v3dfy-v$Version-win-x64-portable.zip\"", script);
        Assert.Contains("Get-RequiredFile (Join-Path $publishDirectory 'V3dfy.App.exe')", script);
        Assert.Contains("Get-RequiredFile (Join-Path $publishDirectory 'V3dfy.SetupHelper.exe')", script);
        Assert.Contains("[System.IO.Compression.ZipFile]::CreateFromDirectory", script);
        Assert.Contains("Get-ChildItem -LiteralPath $TargetDirectory -Filter \"$ZipFileName.part*\" -File", script);
        Assert.Contains("'{0}.part{1:D2}' -f $ZipFileName, $index", script);
        Assert.Contains("SHA256SUMS.txt", script);
        Assert.Contains("Release payload split count must remain 3", script);
        Assert.Contains("$basePayloadModelValidatorScript = Join-Path $PSScriptRoot 'validate-base-payload-models.ps1'", script);
        Assert.Contains("Invoke-BasePayloadModelValidation $publishDirectory", script);
        Assert.Contains("[Math]::Min([int64]$buffer.Length, $remainingForPart)", script);
    }

    [Fact]
    public void InstallerPackagingScript_RequiresFreshPayloadFromPublishOutput()
    {
        var script = ReadRepoFile("scripts", "package-release-installers.ps1");

        Assert.Contains("$publishRoot = Join-Path $repoRoot 'artifacts\\publish\\v3dfy-win-x64'", script);
        Assert.Contains("$portableZipFileName = \"v3dfy-v$Version-win-x64-portable.zip\"", script);
        Assert.Contains("Get-RequiredFile $portableZipPath 'Release portable ZIP'", script);
        Assert.Contains("Get-RequiredFile (Join-Path $PartsDirectory $partName) \"Payload part $index\"", script);
        Assert.Contains("Unexpected stale payload split part files", script);
        Assert.Contains("Get-NewestPublishFileWriteTimeUtc", script);
        Assert.Contains("$PortableZipFile.LastWriteTimeUtc -lt $newestPublishFileUtc", script);
        Assert.Contains("Release portable ZIP is stale", script);
        Assert.Contains("Get-JoinedPayloadPartsSha256", script);
        Assert.Contains("do not recombine to the portable ZIP", script);
        Assert.Contains("scripts\\package-release-payload.ps1", script);
        Assert.DoesNotContain("Using final ZIP SHA256 from checksum file", script);
    }

    [Fact]
    public void PackagingScript_CapturesSetupHelperPublishOutputBeforeReturningPath()
    {
        var script = ReadRepoFile("scripts", "package-release-installers.ps1");

        Assert.Contains("$publishOutput = & dotnet publish $helperProject", script);
        Assert.Contains("$publishExitCode = $LASTEXITCODE", script);
        Assert.Contains("foreach ($line in $publishOutput)", script);
        Assert.Contains("Write-Host $line", script);
        Assert.Contains("throw \"dotnet publish for setup helper failed with exit code $publishExitCode.\"", script);
        Assert.Contains("return $helperExe", script);
    }

    [Fact]
    public void PublishScript_PublishesSetupHelperIntoAppPublishRoot()
    {
        var script = ReadRepoFile("scripts", "publish-win-x64.ps1");
        var helperPublishFunction = ExtractSourceRange(
            script,
            "function Publish-SetupHelperForApp",
            "& dotnet publish $appProject");

        Assert.Contains("$setupHelperProject = Join-Path $repoRoot 'src\\V3dfy.SetupHelper\\V3dfy.SetupHelper.csproj'", script);
        Assert.Contains("$setupHelperPublishDirectory = Join-Path $repoRoot 'artifacts\\publish\\v3dfy-setup-helper-win-x64'", script);
        Assert.Contains("& dotnet publish $setupHelperProject", helperPublishFunction);
        Assert.Contains("--configuration Release", helperPublishFunction);
        Assert.Contains("--framework net10.0-windows", helperPublishFunction);
        Assert.Contains("--runtime win-x64", helperPublishFunction);
        Assert.Contains("--self-contained true", helperPublishFunction);
        Assert.Contains("-p:PublishSingleFile=true", helperPublishFunction);
        Assert.Contains("Join-Path $setupHelperPublishDirectory 'V3dfy.SetupHelper.exe'", helperPublishFunction);
        Assert.Contains("Copy-Item -Destination $publishDirectory -Recurse -Force", helperPublishFunction);
        Assert.Contains("Publish-SetupHelperForApp", script);
    }

    [Fact]
    public void PublishScript_CleansPublishOutputAndValidatesBasePayloadModels()
    {
        var script = ReadRepoFile("scripts", "publish-win-x64.ps1");
        var copyRepositoryDirectoryFunction = ExtractSourceRange(
            script,
            "function Copy-RepositoryDirectory",
            "function Get-Iw3BundleSource");

        Assert.Contains("Reset-Directory $publishDirectory 'Windows x64 publish output directory'", script);
        Assert.Contains("Remove-Item -LiteralPath $Path -Recurse -Force", script);
        Assert.Contains("Test-SameOrNestedPath $sourceBundleRoot $publishDirectory", script);
        Assert.Contains("Iw3BundleRoot must not be the publish output", script);
        Assert.Contains("$basePayloadModelValidatorScript = Join-Path $PSScriptRoot 'validate-base-payload-models.ps1'", script);
        Assert.Contains("Invoke-BasePayloadModelValidation", script);
        Assert.Contains("Remove-Item -LiteralPath $target -Recurse -Force", copyRepositoryDirectoryFunction);
        Assert.Contains("Copy-Item -LiteralPath $source -Destination $publishDirectory -Recurse -Force", copyRepositoryDirectoryFunction);
        Assert.DoesNotContain("artifacts\\model-packs", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("artifacts/model-packs", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BasePayloadModelGuardScript_AllowsBaseRuntimeAndDeniesOptionalCheckpoints()
    {
        var script = ReadRepoFile("scripts", "validate-base-payload-models.ps1");

        Assert.Contains("depth_anything_metric_depth_indoor.pt", script);
        Assert.Contains("iw3_row_flow_v3_20250627.pth", script);
        Assert.Contains("iw3_depth_aa_20250530.pth", script);
        Assert.Contains("depth_anything_v2_vits.pth", script);
        Assert.Contains("depth_anything_metric_depth_outdoor.pt", script);
        Assert.Contains("distill_any_depth_vits.safetensors", script);
        Assert.Contains("da3mono-large.safetensors", script);
        Assert.Contains("Optional model-pack checkpoint found in base payload", script);
        Assert.Contains("Rebuild publish output from a clean source bundle or import it through model packs", script);
    }

    [Fact]
    public void BasePayloadModelGuard_AllowsSyntheticBaseAndRuntimeFiles()
    {
        var root = TestPaths.TempRoot("base-payload-model-guard", Guid.NewGuid().ToString("N"));
        try
        {
            var checkpointRoot = Path.Combine(
                root,
                "engine",
                "iw3",
                "nunif",
                "iw3",
                "pretrained_models",
                "hub",
                "checkpoints");
            Directory.CreateDirectory(checkpointRoot);
            File.WriteAllText(Path.Combine(checkpointRoot, "depth_anything_metric_depth_indoor.pt"), "base model placeholder");
            File.WriteAllText(Path.Combine(checkpointRoot, "iw3_row_flow_v3_20250627.pth"), "runtime placeholder");
            File.WriteAllText(Path.Combine(checkpointRoot, "iw3_depth_aa_20250530.pth"), "depth aa placeholder");

            var result = RunBasePayloadModelGuard(root);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Base payload model-file guard passed", result.Output);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void BasePayloadModelGuard_FailsForSyntheticOptionalCheckpoint()
    {
        var root = TestPaths.TempRoot("base-payload-model-guard", Guid.NewGuid().ToString("N"));
        try
        {
            var checkpointRoot = Path.Combine(
                root,
                "engine",
                "iw3",
                "nunif",
                "iw3",
                "pretrained_models",
                "hub",
                "checkpoints");
            Directory.CreateDirectory(checkpointRoot);
            File.WriteAllText(Path.Combine(checkpointRoot, "depth_anything_metric_depth_indoor.pt"), "base model placeholder");
            File.WriteAllText(Path.Combine(checkpointRoot, "depth_anything_v2_vits.pth"), "optional model placeholder");

            var result = RunBasePayloadModelGuard(root);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Optional model-pack checkpoint found in base payload", result.Output);
            Assert.Contains("depth_anything_v2_vits.pth", result.Output);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void PublishScript_SetupHelperPublishDoesNotCopyDebugOutput()
    {
        var script = ReadRepoFile("scripts", "publish-win-x64.ps1");
        var helperPublishFunction = ExtractSourceRange(
            script,
            "function Publish-SetupHelperForApp",
            "& dotnet publish $appProject");

        Assert.DoesNotContain("bin\\Debug", helperPublishFunction, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bin/Debug", helperPublishFunction, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Debug\\net", helperPublishFunction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--configuration Release", helperPublishFunction);
        Assert.Contains("--runtime win-x64", helperPublishFunction);
    }

    [Fact]
    public void SetupHelperSource_ContainsClassicProgressLogUi()
    {
        var uiSource = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupHelperUiRunner.Windows.cs");
        var progressSource = ReadRepoFile("src", "V3dfy.SetupHelper", "SetupProgress.cs");
        var installerSource = ReadRepoFile("src", "V3dfy.SetupHelper", "PayloadInstaller.cs");

        Assert.Contains("Text = \"v3dfy Setup\"", uiSource);
        Assert.Contains("Text = \"Installing v3dfy\"", uiSource);
        Assert.Contains("statusLabel", uiSource);
        Assert.Contains("overallProgressTextLabel", uiSource);
        Assert.Contains("overallProgressBar", uiSource);
        Assert.Contains("Text = \"Overall progress\"", uiSource);
        Assert.Contains("progressPanel.Controls.Add(overallProgressTextLabel, 0, 0)", uiSource);
        Assert.Contains("progressPanel.Controls.Add(overallProgressBar, 0, 1)", uiSource);
        Assert.Contains("progressPanel.Controls.Add(statusLabel, 0, 2)", uiSource);
        Assert.Contains("progressPanel.Controls.Add(progressBar, 0, 3)", uiSource);
        Assert.Contains("ProgressBar", uiSource);
        Assert.Contains("progressTextLabel", uiSource);
        Assert.Contains("ListBox", uiSource);
        Assert.Contains("logListBox", uiSource);
        Assert.Contains("Dock = DockStyle.Fill", uiSource);
        Assert.Contains("new RowStyle(SizeType.Percent, 100)", uiSource);
        Assert.Contains("buttonPanel", uiSource);
        Assert.Contains("AutoSize = true", uiSource);
        Assert.Contains("Download:", uiSource);
        Assert.Contains("Verify:", uiSource);
        Assert.Contains("Rebuild package", uiSource);
        Assert.Contains("Extract:", uiSource);
        Assert.Contains("Install files", uiSource);
        Assert.Contains("Clean temporary files", uiSource);
        Assert.Contains("Complete", uiSource);
        Assert.DoesNotContain("of 3", uiSource);
        Assert.DoesNotContain("All package parts verified", uiSource);
        Assert.Contains("SetupProgressPhase.DownloadingPart", installerSource);
        Assert.Contains("SetupProgressPhase.VerifyingPart", installerSource);
        Assert.Contains("SetupProgressPhase.RebuildingZip", installerSource);
        Assert.Contains("SetupProgressPhase.ExtractingPayload", installerSource);
        Assert.Contains("SetupProgressPhase.InstallingPayload", installerSource);
        Assert.Contains("SetupOverallProgressTracker", installerSource);
        Assert.Contains("CreateWebPhaseRanges", installerSource);
        Assert.Contains("CreateOfflinePhaseRanges", installerSource);
        Assert.Contains("Downloading package parts", installerSource);
        Assert.Contains("Verifying package parts", installerSource);
        Assert.Contains("Rebuilding portable package", installerSource);
        Assert.Contains("Verifying portable package", installerSource);
        Assert.Contains("Extracting files", installerSource);
        Assert.Contains("Installing files", installerSource);
        Assert.Contains("Finalizing installation", installerSource);
        Assert.DoesNotContain("PayloadPartSetupProgress", installerSource);
        Assert.DoesNotContain("ReportVerifiedPackagePart", installerSource);
        Assert.DoesNotContain("OverallTotalUnits: manifest.Parts.Count", installerSource);
        Assert.DoesNotContain("OverallTotalUnits = totalParts", installerSource);
        Assert.Contains("manifest.Parts.Count", installerSource);
        Assert.Contains("DownloadingPart", progressSource);
        Assert.Contains("CurrentBytes", progressSource);
        Assert.Contains("TotalBytes", progressSource);
        Assert.Contains("OverallCompletedUnits", progressSource);
        Assert.Contains("OverallTotalUnits", progressSource);
        Assert.Contains("OverallPercent", progressSource);
    }

    [Fact]
    public void InnoBootstrap_CommunicatesWebAndOfflineRequirements()
    {
        var installerScript = ReadRepoFile("packaging", "inno", "v3dfy-payload-bootstrap.iss");

        Assert.Contains("Internet required", installerScript);
        Assert.Contains("downloads the v3dfy payload during installation", installerScript);
        Assert.Contains("Keep all payload .part files beside this setup EXE", installerScript);
        Assert.Contains("same folder as this setup EXE", installerScript);
        Assert.Contains("Create a Desktop shortcut", installerScript);
        Assert.Contains("filesandordirs", installerScript);
        Assert.Contains("--ui", installerScript);
        Assert.Contains("SW_SHOW", installerScript);
    }

    [Fact]
    public void InnoBootstrap_WiresOptionalModelPackManifestToSetupHelper()
    {
        var installerScript = ReadRepoFile("packaging", "inno", "v3dfy-payload-bootstrap.iss");
        var filesSection = ExtractSourceRange(installerScript, "[Files]", "[Tasks]");
        var helperArguments = ExtractSourceRange(
            installerScript,
            "function BuildHelperArguments(LogPath: String): String;",
            "function RunPayloadHelper");
        var prepareToInstall = ExtractSourceRange(
            installerScript,
            "function PrepareToInstall(var NeedsRestart: Boolean): String;",
            "procedure InitializeWizard()");

        Assert.Contains("#ifdef ModelPackManifestFile", installerScript);
        Assert.Contains("#define ModelPackManifestFileName ExtractFileName(ModelPackManifestFile)", installerScript);
        Assert.Contains("Source: \"{#ModelPackManifestFile}\"", filesSection);
        Assert.Contains("DestName: \"{#ModelPackManifestFileName}\"", filesSection);
        Assert.Contains("--model-packs-manifest", helperArguments);
        Assert.Contains("ExpandConstant('{tmp}\\{#ModelPackManifestFileName}')", helperArguments);
        Assert.Contains("if '{#InstallerMode}' = 'offline' then", helperArguments);
        Assert.Contains("--model-packs-source-dir", helperArguments);
        Assert.Contains("ExpandConstant('{src}')", helperArguments);
        Assert.Contains("ExtractTemporaryFile('{#ModelPackManifestFileName}')", prepareToInstall);
        Assert.Contains("--manifest", helperArguments);
        Assert.Contains("--mode {#InstallerMode}", helperArguments);
        Assert.Contains("--parts-dir", helperArguments);

        var sourceDirIndex = helperArguments.IndexOf("--model-packs-source-dir", StringComparison.Ordinal);
        var offlineModeIndex = helperArguments.IndexOf("if '{#InstallerMode}' = 'offline' then", StringComparison.Ordinal);
        Assert.True(sourceDirIndex > offlineModeIndex);
        Assert.DoesNotContain("C:\\", filesSection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstallerReadmes_ExplainOptionalModelPacksWithoutListingFutureModels()
    {
        var script = ReadRepoFile("scripts", "package-release-installers.ps1");

        Assert.Contains("Optional model packs can be selected during setup", script);
        Assert.Contains("Selected optional packs are downloaded from the release", script);
        Assert.Contains("You may select none because", script);
        Assert.Contains("v3dfy includes a base model", script);
        Assert.Contains("model packs can also be imported later from", script);
        Assert.Contains("Optional model-pack ZIPs can be placed beside the offline setup EXE", script);
        Assert.Contains("matching official model-pack ZIPs found", script);
        Assert.Contains("will appear during", script);
        Assert.Contains("If none are found, v3dfy still installs with its base model", script);
        Assert.DoesNotContain("DepthPro", script);
        Assert.DoesNotContain("Video Depth Anything", script);
        Assert.DoesNotContain("non-commercial", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("future model", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InnoScripts_RemoveOnlyAppInstallTreeOnUninstall()
    {
        var bootstrapScript = ReadRepoFile("packaging", "inno", "v3dfy-payload-bootstrap.iss");
        var legacyScript = ReadRepoFile("packaging", "inno", "v3dfy.iss");

        AssertCleanUninstallPolicy(bootstrapScript);
        AssertCleanUninstallPolicy(legacyScript);
    }

    [Fact]
    public void PackagingSource_DoesNotContainForbiddenUserSpecificPaths()
    {
        var forbiddenFragments = new[]
        {
            DrivePath("dev", "v3dfy"),
            DrivePathWithTrailing("Users"),
            string.Concat("lega", "rcia"),
            BackslashPath("Videos", "v3dfy-tests"),
            DrivePath("Temp"),
        };

        var files = new[]
        {
            Path.Combine("README.md"),
            Path.Combine("scripts", "package-release-payload.ps1"),
            Path.Combine("scripts", "package-release-installers.ps1"),
            Path.Combine("packaging", "inno", "v3dfy-payload-bootstrap.iss"),
            Path.Combine("docs", "packaging.md"),
        }.Concat(Directory.EnumerateFiles(
            Path.Combine(RepositoryRoot(), "src", "V3dfy.SetupHelper"),
            "*",
            SearchOption.AllDirectories)
            .Where(static file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)));

        var violations = files
            .SelectMany(file =>
            {
                var text = File.ReadAllText(ToAbsolutePath(file));
                return forbiddenFragments
                    .Where(fragment => text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    .Select(fragment => $"{Path.GetRelativePath(RepositoryRoot(), ToAbsolutePath(file))}: {fragment}");
            })
            .ToArray();

        Assert.Empty(violations);
    }

    private static string ReadRepoFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([RepositoryRoot(), .. segments]));

    private static string ToAbsolutePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(RepositoryRoot(), path);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "v3dfy.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static string DrivePath(params string[] segments) =>
        "C:" + Backslash + BackslashPath(segments);

    private static string DrivePathWithTrailing(params string[] segments) =>
        DrivePath(segments) + Backslash;

    private static string BackslashPath(params string[] segments) =>
        string.Join(Backslash, segments);

    private static void AssertCleanUninstallPolicy(string installerScript)
    {
        var uninstallDeleteSection = ExtractSourceRange(
            installerScript,
            "[UninstallDelete]",
            installerScript.Contains("[Code]", StringComparison.Ordinal)
                ? "[Code]"
                : "[Run]");

        Assert.Contains("Type: filesandordirs; Name: \"{app}\\*\"", uninstallDeleteSection);
        Assert.Contains("Type: dirifempty; Name: \"{app}\"", uninstallDeleteSection);
        Assert.DoesNotContain("Name: \"{autopf}\"", uninstallDeleteSection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Name: \"{pf}\"", uninstallDeleteSection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Name: \"{commonpf}\"", uninstallDeleteSection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Name: \"{userdocs}\"", uninstallDeleteSection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Name: \"{userappdata}\"", uninstallDeleteSection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Name: \"{localappdata}\"", uninstallDeleteSection, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractSourceRange(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);

        return source[start..end];
    }

    private static ScriptResult RunBasePayloadModelGuard(string publishDirectory)
    {
        var scriptPath = Path.Combine(RepositoryRoot(), "scripts", "validate-base-payload-models.ps1");
        var startInfo = new System.Diagnostics.ProcessStartInfo("powershell")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-PublishDir");
        startInfo.ArgumentList.Add(publishDirectory);

        using var process = System.Diagnostics.Process.Start(startInfo);
        Assert.NotNull(process);
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ScriptResult(process.ExitCode, output);
    }

    private sealed record ScriptResult(int ExitCode, string Output);
}
