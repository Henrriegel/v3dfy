using System.Text.Json;
using V3dfy.SetupHelper;

namespace V3dfy.Tests.Packaging;

public sealed class InstallerModelPackSelectionTests : IDisposable
{
    private readonly string root = TestPaths.TempRoot(
        "installer-model-pack-selection",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ManifestParser_ValidManifestParses()
    {
        var manifest = InstallerModelPackManifest.Parse(CreateManifestJson());

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("0.1.0-preview.1", manifest.V3dfyVersion);
        Assert.Equal("v0.1.0-preview.1", manifest.ReleaseTag);
        var pack = Assert.Single(manifest.Packs);
        Assert.Equal("depth-anything-v2-small", pack.PackId);
        Assert.Equal("v3dfy-modelpack-depth-anything-v2-small-v0.1.0-preview.1.zip", pack.AssetFileName);
        Assert.Equal("General movies and quick tests.", pack.BestUseEnglish);
        Assert.Equal("Peliculas generales y pruebas rapidas.", pack.BestUseSpanish);
        Assert.Equal(1234, pack.ZipSizeBytes);
        Assert.True(pack.InstallerSelectable);
    }

    [Theory]
    [InlineData("schema-version")]
    [InlineData("duplicate-pack-id")]
    [InlineData("duplicate-asset-file-name")]
    [InlineData("absolute-asset-file-name")]
    [InlineData("slash-asset-file-name")]
    [InlineData("absolute-relative-artifact-path")]
    [InlineData("missing-required-field")]
    [InlineData("non-positive-zip-size")]
    [InlineData("invalid-web-url")]
    public void ManifestParser_InvalidManifestFailsWithClearMessage(string scenario)
    {
        var exception = Assert.Throws<InstallerModelPackManifestException>(() =>
            InstallerModelPackManifest.Parse(CreateManifestJson(scenario)));

        Assert.Contains("Invalid installer model-pack manifest", exception.Message);
    }

    [Fact]
    public void WebDiscovery_ReturnsInstallerSelectableRows()
    {
        var manifest = InstallerModelPackManifest.Parse(CreateManifestJson(
            extraPacks:
            [
                CreatePack(
                    "hidden-pack",
                    "Hidden Pack",
                    "v3dfy-modelpack-hidden-pack-v0.1.0-preview.1.zip",
                    installerSelectable: false),
                CreatePack(
                    "selected-pack",
                    "Selected Pack",
                    "v3dfy-modelpack-selected-pack-v0.1.0-preview.1.zip",
                    defaultSelected: true,
                    zipSizeBytes: 2048),
            ]));

        var result = InstallerModelPackDiscovery.DiscoverWeb(manifest);
        var state = new InstallerModelPackSelectionState(result.Rows);

        Assert.Null(result.NoPacksMessage);
        Assert.Equal(2, result.Rows.Count);
        Assert.DoesNotContain(result.Rows, row => row.PackId == "hidden-pack");
        Assert.All(result.Rows, row =>
        {
            Assert.Equal(InstallerModelPackSourceKind.WebReleaseAsset, row.SourceKind);
            Assert.Equal("Available download", row.StatusText);
            Assert.Null(row.SourcePath);
            Assert.StartsWith("https://example.invalid/", row.Url, StringComparison.Ordinal);
            Assert.True(row.IsAvailable);
        });
        Assert.Contains(result.Rows, row => row.PackId == "selected-pack" && row.IsSelected);
        Assert.Equal(1, state.SelectedCount);
        Assert.Equal(2048, state.SelectedTotalSizeBytes);
    }

    [Fact]
    public void OfflineDiscovery_ReturnsOnlyMatchingLocalZipRows()
    {
        var manifest = InstallerModelPackManifest.Parse(CreateManifestJson(
            extraPacks:
            [
                CreatePack(
                    "not-local",
                    "Not Local",
                    "v3dfy-modelpack-not-local-v0.1.0-preview.1.zip"),
            ]));
        var sourceDirectory = Path.Combine(root, "offline-source");
        Directory.CreateDirectory(sourceDirectory);
        var matchingPath = Path.Combine(
            sourceDirectory,
            "v3dfy-modelpack-depth-anything-v2-small-v0.1.0-preview.1.zip");
        File.WriteAllText(matchingPath, "synthetic local zip placeholder");
        File.WriteAllText(
            Path.Combine(sourceDirectory, "v3dfy-modelpack-unknown-v0.1.0-preview.1.zip"),
            "not in manifest");

        var result = InstallerModelPackDiscovery.DiscoverOffline(manifest, sourceDirectory);

        var row = Assert.Single(result.Rows);
        Assert.Null(result.NoPacksMessage);
        Assert.Equal("depth-anything-v2-small", row.PackId);
        Assert.Equal(InstallerModelPackSourceKind.OfflineLocalZip, row.SourceKind);
        Assert.Equal("Found beside installer", row.StatusText);
        Assert.Equal(matchingPath, row.SourcePath);
        Assert.Equal("https://example.invalid/v3dfy-modelpack-depth-anything-v2-small-v0.1.0-preview.1.zip", row.Url);
        Assert.True(row.IsAvailable);
    }

    [Fact]
    public void OfflineDiscovery_NoMatchingZipsReturnsNoPackMessage()
    {
        var manifest = InstallerModelPackManifest.Parse(CreateManifestJson());
        var sourceDirectory = Path.Combine(root, "offline-source");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "unknown.zip"), "not listed");

        var result = InstallerModelPackDiscovery.DiscoverOffline(manifest, sourceDirectory);

        Assert.Empty(result.Rows);
        Assert.Equal("No optional model packs were found beside this installer.", result.NoPacksMessage);
    }

    [Fact]
    public void Discovery_UsesSpanishBestUseAndStatusWhenRequested()
    {
        var manifest = InstallerModelPackManifest.Parse(CreateManifestJson());
        var result = InstallerModelPackDiscovery.DiscoverWeb(manifest, useSpanish: true);

        var row = Assert.Single(result.Rows);
        Assert.Equal("General movies and quick tests.", row.BestUseEnglish);
        Assert.Equal("Peliculas generales y pruebas rapidas.", row.BestUseSpanish);
        Assert.Equal("Peliculas generales y pruebas rapidas.", row.GetBestUse(SetupUiLanguage.Spanish));
        Assert.Equal("Disponible para descargar", row.StatusText);
    }

    [Fact]
    public void Discovery_UsesEnglishBestUseWhenRequested()
    {
        var manifest = InstallerModelPackManifest.Parse(CreateManifestJson());
        var result = InstallerModelPackDiscovery.DiscoverWeb(manifest);

        var row = Assert.Single(result.Rows);
        Assert.Equal("General movies and quick tests.", row.GetBestUse(SetupUiLanguage.English));
    }

    [Fact]
    public void Discovery_FallsBackToEnglishOnlyWhenSpanishBestUseIsMissing()
    {
        var pack = CreatePack(
            "english-only",
            "English Only",
            "v3dfy-modelpack-english-only-v0.1.0-preview.1.zip");
        pack["bestUseSpanish"] = string.Empty;
        var manifest = InstallerModelPackManifest.Parse(CreateManifestJson(extraPacks: [pack]));

        var row = InstallerModelPackDiscovery
            .DiscoverWeb(manifest, useSpanish: true)
            .Rows
            .Single(row => row.PackId == "english-only");

        Assert.Equal("General movies and quick tests.", row.GetBestUse(SetupUiLanguage.Spanish));
    }

    [Fact]
    public void WebAndOfflineDiscoveryShareLocalizedBestUseRows()
    {
        var manifest = InstallerModelPackManifest.Parse(CreateManifestJson());
        var sourceDirectory = Path.Combine(root, "offline-source");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(
            Path.Combine(sourceDirectory, "v3dfy-modelpack-depth-anything-v2-small-v0.1.0-preview.1.zip"),
            "synthetic local zip placeholder");

        var webRow = Assert.Single(InstallerModelPackDiscovery.DiscoverWeb(manifest).Rows);
        var offlineRow = Assert.Single(InstallerModelPackDiscovery.DiscoverOffline(manifest, sourceDirectory).Rows);

        Assert.Equal(
            webRow.GetBestUse(SetupUiLanguage.Spanish),
            offlineRow.GetBestUse(SetupUiLanguage.Spanish));
    }

    [Fact]
    public void TopCheckboxState_TracksSelectionAndTotals()
    {
        var state = new InstallerModelPackSelectionState(
        [
            CreateRow("first", 100),
            CreateRow("second", 200),
        ]);

        Assert.True(state.CanUseTopCheckbox);
        Assert.Equal(InstallerModelPackTopSelectionState.Unchecked, state.TopCheckboxState);
        Assert.Equal(0, state.SelectedCount);

        state.ApplyTopCheckboxAction();
        Assert.Equal(InstallerModelPackTopSelectionState.Checked, state.TopCheckboxState);
        Assert.Equal(2, state.SelectedCount);
        Assert.Equal(300, state.SelectedTotalSizeBytes);

        state.SetSelected("first", false);
        Assert.Equal(InstallerModelPackTopSelectionState.Indeterminate, state.TopCheckboxState);
        Assert.Equal(1, state.SelectedCount);
        Assert.Equal(200, state.SelectedTotalSizeBytes);

        state.ApplyTopCheckboxAction();
        Assert.Equal(InstallerModelPackTopSelectionState.Checked, state.TopCheckboxState);
        Assert.Equal(2, state.SelectedCount);

        state.ApplyTopCheckboxAction();
        Assert.Equal(InstallerModelPackTopSelectionState.Unchecked, state.TopCheckboxState);
        Assert.Equal(0, state.SelectedCount);
    }

    [Fact]
    public void TopCheckboxState_NoRowsIsUncheckedAndDisabled()
    {
        var state = new InstallerModelPackSelectionState([]);

        Assert.False(state.CanUseTopCheckbox);
        Assert.Equal(InstallerModelPackTopSelectionState.Unchecked, state.TopCheckboxState);

        state.ApplyTopCheckboxAction();

        Assert.Equal(InstallerModelPackTopSelectionState.Unchecked, state.TopCheckboxState);
    }

    [Fact]
    public void SelectionPageModel_AllowsContinueWithZeroSelectedAndUpdatesSummary()
    {
        var page = new InstallerModelPackSelectionPageModel(new InstallerModelPackDiscoveryResult(
        [
            CreateRow("first", 1024),
            CreateRow("second", 2048),
        ], NoPacksMessage: null));

        Assert.True(page.HasRows);
        Assert.Equal("Selected: 0 model packs - 0 B", page.SelectedSummaryText);

        page.SelectionState.SetSelected("first", true);

        Assert.Equal("Selected: 1 model pack - 1 KB", page.SelectedSummaryText);

        page.SelectionState.ApplyTopCheckboxAction();

        Assert.Equal("Selected: 2 model packs - 3 KB", page.SelectedSummaryText);
    }

    [Fact]
    public void SelectionPageModel_NoPackStateUsesOfflineInstallerMessage()
    {
        var page = new InstallerModelPackSelectionPageModel(new InstallerModelPackDiscoveryResult(
            [],
            "No optional model packs were found beside this installer."));

        Assert.False(page.HasRows);
        Assert.Equal(InstallerModelPackSelectionPageModel.OfflineNoPacksText, page.NoPacksMessage);
        Assert.Equal("Selected: 0 model packs - 0 B", page.SelectedSummaryText);
    }

    [Fact]
    public void PayloadArgumentParser_PreservesExistingArgumentsWithoutModelPacks()
    {
        var result = PayloadInstallArgumentParser.Parse(
        [
            "--mode",
            "web",
            "--manifest",
            "payload.json",
            "--target-dir",
            "target",
            "--work-dir",
            "work",
            "--log",
            "setup.log",
            "--ui",
        ]);

        Assert.Null(result.Error);
        Assert.True(result.UiRequested);
        Assert.Equal("setup.log", result.LogPath);
        Assert.NotNull(result.Options);
        Assert.Equal(PayloadInstallMode.Web, result.Options.Mode);
        Assert.Equal("payload.json", result.Options.ManifestPath);
        Assert.Null(result.Options.ModelPacksManifestPath);
        Assert.Null(result.Options.ModelPacksSourceDirectory);
    }

    [Fact]
    public void PayloadArgumentParser_ModelPackArgumentsAreOptionalAndOfflineSourceDefaultsToPartsDir()
    {
        var result = PayloadInstallArgumentParser.Parse(
        [
            "--mode",
            "offline",
            "--manifest",
            "payload.json",
            "--target-dir",
            "target",
            "--work-dir",
            "work",
            "--parts-dir",
            "source",
            "--model-packs-manifest",
            "model-packs.json",
        ]);

        Assert.Null(result.Error);
        Assert.NotNull(result.Options);
        Assert.Equal(PayloadInstallMode.Offline, result.Options.Mode);
        Assert.Equal("source", result.Options.PartsDirectory);
        Assert.Equal("model-packs.json", result.Options.ModelPacksManifestPath);
        Assert.Equal("source", result.Options.ModelPacksSourceDirectory);
    }

    [Fact]
    public void PayloadArgumentParser_ExplicitModelPackSourceDirWins()
    {
        var result = PayloadInstallArgumentParser.Parse(
        [
            "--mode",
            "offline",
            "--manifest",
            "payload.json",
            "--target-dir",
            "target",
            "--work-dir",
            "work",
            "--parts-dir",
            "payload-source",
            "--model-packs-manifest",
            "model-packs.json",
            "--model-packs-source-dir",
            "model-pack-source",
        ]);

        Assert.Null(result.Error);
        Assert.NotNull(result.Options);
        Assert.Equal("model-pack-source", result.Options.ModelPacksSourceDirectory);
    }

    [Fact]
    public void PayloadArgumentParser_ReplacementFlagIsExplicit()
    {
        var result = PayloadInstallArgumentParser.Parse(
        [
            "--mode",
            "offline",
            "--manifest",
            "payload.json",
            "--target-dir",
            "target",
            "--work-dir",
            "work",
            "--parts-dir",
            "source",
            "--replace-existing",
        ]);

        Assert.Null(result.Error);
        Assert.NotNull(result.Options);
        Assert.True(result.Options.AllowTargetReplacement);
    }

    private static InstallerModelPackSelectionRow CreateRow(
        string packId,
        long zipSizeBytes,
        bool isAvailable = true) =>
        new(
            packId,
            packId,
            "Best use",
            $"{packId}.zip",
            sourcePath: null,
            "https://example.invalid/" + packId + ".zip",
            new string('a', 64),
            zipSizeBytes,
            "Available download",
            isSelected: false,
            isAvailable,
            InstallerModelPackSourceKind.WebReleaseAsset);

    private static string CreateManifestJson(
        string? scenario = null,
        IReadOnlyList<Dictionary<string, object?>>? extraPacks = null)
    {
        var pack = CreatePack(
            "depth-anything-v2-small",
            "Depth Anything V2 Small",
            "v3dfy-modelpack-depth-anything-v2-small-v0.1.0-preview.1.zip");
        var packs = new List<Dictionary<string, object?>> { pack };
        if (extraPacks is not null)
        {
            packs.AddRange(extraPacks);
        }

        var schemaVersion = 1;
        switch (scenario)
        {
            case "schema-version":
                schemaVersion = 99;
                break;
            case "duplicate-pack-id":
                packs.Add(CreatePack(
                    "depth-anything-v2-small",
                    "Duplicate",
                    "v3dfy-modelpack-duplicate-v0.1.0-preview.1.zip"));
                break;
            case "duplicate-asset-file-name":
                packs.Add(CreatePack(
                    "duplicate",
                    "Duplicate",
                    "v3dfy-modelpack-depth-anything-v2-small-v0.1.0-preview.1.zip"));
                break;
            case "absolute-asset-file-name":
                pack["assetFileName"] = @"C:\temp\pack.zip";
                break;
            case "slash-asset-file-name":
                pack["assetFileName"] = "folder/pack.zip";
                break;
            case "absolute-relative-artifact-path":
                pack["relativeArtifactPath"] = @"C:\temp\pack.zip";
                break;
            case "missing-required-field":
                pack.Remove("displayName");
                break;
            case "non-positive-zip-size":
                pack["zipSizeBytes"] = 0;
                break;
            case "invalid-web-url":
                pack["url"] = "ftp://example.invalid/pack.zip";
                break;
        }

        var manifest = new Dictionary<string, object?>
        {
            ["schemaVersion"] = schemaVersion,
            ["v3dfyVersion"] = "0.1.0-preview.1",
            ["modelPackVersion"] = "0.1.0-preview.1",
            ["releaseTag"] = "v0.1.0-preview.1",
            ["modelPackReleaseBaseUrl"] = "https://example.invalid/releases/v0.1.0-preview.1",
            ["currentIw3Version"] = "nunif-d23721f1",
            ["generatedUtc"] = "2026-06-14T00:00:00Z",
            ["packs"] = packs,
        };

        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static Dictionary<string, object?> CreatePack(
        string packId,
        string displayName,
        string assetFileName,
        bool installerSelectable = true,
        bool defaultSelected = false,
        long zipSizeBytes = 1234)
    {
        var checkpointPath = $"hub/checkpoints/{packId}.pth";
        return new Dictionary<string, object?>
        {
            ["packId"] = packId,
            ["displayName"] = displayName,
            ["bestUseEnglish"] = "General movies and quick tests.",
            ["bestUseSpanish"] = "Peliculas generales y pruebas rapidas.",
            ["assetFileName"] = assetFileName,
            ["relativeArtifactPath"] = $"{packId}/{assetFileName}",
            ["url"] = "https://example.invalid/" + assetFileName,
            ["zipSha256"] = new string('a', 64),
            ["zipSizeBytes"] = zipSizeBytes,
            ["checkpointPath"] = checkpointPath,
            ["checkpointSha256"] = new string('b', 64),
            ["checkpointSizeBytes"] = 456,
            ["iw3DepthModelNames"] = new[] { "Any_V2_S" },
            ["mappingKeys"] = new[] { packId },
            ["installerSelectable"] = installerSelectable,
            ["defaultSelected"] = defaultSelected,
            ["license"] = "SAFE_WITH_NOTICE / Apache-2.0",
            ["sourceUrl"] = "https://example.invalid/source",
            ["modelCardUrl"] = "https://example.invalid/model",
            ["recommendedFor"] = new[] { "general" },
            ["sizeCategory"] = "small",
        };
    }
}
