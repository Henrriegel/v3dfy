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
    }

    [Fact]
    public void PackagingScript_ConfiguresExpectedAssetNames()
    {
        var script = ReadRepoFile("scripts", "package-release-installers.ps1");

        Assert.Contains("v3dfy-v$Version-web-setup.exe", script);
        Assert.Contains("v3dfy-v$Version-offline-setup.exe", script);
        Assert.Contains("README_WEB_INSTALLER.txt", script);
        Assert.Contains("README_OFFLINE_INSTALLER.txt", script);
        Assert.Contains("SHA256SUMS.installers.txt", script);
        Assert.Contains("artifacts\\release\\split", script);
        Assert.Contains("artifacts\\installer", script);
        Assert.Contains("https://github.com/Henrriegel/v3dfy/releases/download/v0.1.0-preview.1", script);
        Assert.Contains("live download", script);
        Assert.Contains("large scrolling setup log", script);
        Assert.Contains("--framework net10.0-windows", script);
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
        Assert.Contains("SetupProgressPhase.DownloadingPart", installerSource);
        Assert.Contains("SetupProgressPhase.VerifyingPart", installerSource);
        Assert.Contains("SetupProgressPhase.RebuildingZip", installerSource);
        Assert.Contains("SetupProgressPhase.ExtractingPayload", installerSource);
        Assert.Contains("SetupProgressPhase.InstallingPayload", installerSource);
        Assert.Contains("DownloadingPart", progressSource);
        Assert.Contains("CurrentBytes", progressSource);
        Assert.Contains("TotalBytes", progressSource);
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

    private static string ExtractSourceRange(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);

        return source[start..end];
    }
}
