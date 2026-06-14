namespace V3dfy.SetupHelper;

public enum InstallerModelPackSourceKind
{
    WebReleaseAsset,
    OfflineLocalZip,
}

public enum InstallerModelPackTopSelectionState
{
    Unchecked,
    Checked,
    Indeterminate,
}

public sealed class InstallerModelPackSelectionRow
{
    public InstallerModelPackSelectionRow(
        string packId,
        string displayName,
        string bestUse,
        string assetFileName,
        string? sourcePath,
        string url,
        string zipSha256,
        long zipSizeBytes,
        string statusText,
        bool isSelected,
        bool isAvailable,
        InstallerModelPackSourceKind sourceKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipSha256);
        if (zipSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zipSizeBytes), "ZIP size must be positive.");
        }

        PackId = packId;
        DisplayName = displayName;
        BestUse = bestUse;
        AssetFileName = assetFileName;
        SourcePath = sourcePath;
        Url = url;
        ZipSha256 = zipSha256;
        ZipSizeBytes = zipSizeBytes;
        SizeText = InstallerModelPackSizeFormatter.FormatBytes(zipSizeBytes);
        StatusText = statusText;
        IsAvailable = isAvailable;
        SourceKind = sourceKind;
        IsSelected = isAvailable && isSelected;
    }

    public string PackId { get; }

    public string DisplayName { get; }

    public string BestUse { get; }

    public string AssetFileName { get; }

    public string? SourcePath { get; }

    public string Url { get; }

    public string ZipSha256 { get; }

    public long ZipSizeBytes { get; }

    public string SizeText { get; }

    public string StatusText { get; }

    public bool IsSelected { get; set; }

    public bool IsAvailable { get; }

    public InstallerModelPackSourceKind SourceKind { get; }
}

public sealed record InstallerModelPackDiscoveryResult(
    IReadOnlyList<InstallerModelPackSelectionRow> Rows,
    string? NoPacksMessage);

public static class InstallerModelPackDiscovery
{
    private const string WebStatusText = "Available download";
    private const string OfflineStatusText = "Found beside installer";
    private const string OfflineNoPacksMessage =
        "No optional model packs were found beside this installer.";

    public static InstallerModelPackDiscoveryResult DiscoverWeb(
        InstallerModelPackManifest manifest,
        bool useSpanish = false)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var rows = manifest.Packs
            .Where(static pack => pack.InstallerSelectable)
            .Select(pack => CreateRow(
                pack,
                sourcePath: null,
                WebStatusText,
                InstallerModelPackSourceKind.WebReleaseAsset,
                useSpanish))
            .ToArray();

        return new InstallerModelPackDiscoveryResult(rows, rows.Length == 0
            ? "No optional model packs are available for this installer."
            : null);
    }

    public static InstallerModelPackDiscoveryResult DiscoverOffline(
        InstallerModelPackManifest manifest,
        string? sourceDirectory,
        bool useSpanish = false)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
            return new InstallerModelPackDiscoveryResult([], OfflineNoPacksMessage);
        }

        var localZips = Directory.EnumerateFiles(sourceDirectory, "*.zip", SearchOption.TopDirectoryOnly)
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key ?? string.Empty,
                group => group.Order(StringComparer.OrdinalIgnoreCase).First(),
                StringComparer.OrdinalIgnoreCase);

        var rows = manifest.Packs
            .Where(static pack => pack.InstallerSelectable)
            .Where(pack => localZips.ContainsKey(pack.AssetFileName))
            .Select(pack => CreateRow(
                pack,
                localZips[pack.AssetFileName],
                OfflineStatusText,
                InstallerModelPackSourceKind.OfflineLocalZip,
                useSpanish))
            .ToArray();

        return new InstallerModelPackDiscoveryResult(rows, rows.Length == 0
            ? OfflineNoPacksMessage
            : null);
    }

    private static InstallerModelPackSelectionRow CreateRow(
        InstallerModelPackEntry pack,
        string? sourcePath,
        string statusText,
        InstallerModelPackSourceKind sourceKind,
        bool useSpanish)
    {
        var bestUse = useSpanish && !string.IsNullOrWhiteSpace(pack.BestUseSpanish)
            ? pack.BestUseSpanish
            : pack.BestUseEnglish;
        return new InstallerModelPackSelectionRow(
            pack.PackId,
            pack.DisplayName,
            bestUse,
            pack.AssetFileName,
            sourcePath,
            pack.Url,
            pack.ZipSha256,
            pack.ZipSizeBytes,
            statusText,
            pack.DefaultSelected,
            isAvailable: true,
            sourceKind);
    }
}

public sealed class InstallerModelPackSelectionState
{
    private readonly IReadOnlyList<InstallerModelPackSelectionRow> rows;

    public InstallerModelPackSelectionState(IReadOnlyList<InstallerModelPackSelectionRow> rows)
    {
        this.rows = rows ?? throw new ArgumentNullException(nameof(rows));
        foreach (var row in this.rows.Where(static row => !row.IsAvailable))
        {
            row.IsSelected = false;
        }
    }

    public IReadOnlyList<InstallerModelPackSelectionRow> Rows => rows;

    public IReadOnlyList<InstallerModelPackSelectionRow> SelectedRows =>
        rows.Where(static row => row.IsAvailable && row.IsSelected).ToArray();

    public int SelectedCount => rows.Count(static row => row.IsAvailable && row.IsSelected);

    public int VisibleCount => rows.Count;

    public long SelectedTotalSizeBytes =>
        rows.Where(static row => row.IsAvailable && row.IsSelected).Sum(static row => row.ZipSizeBytes);

    public string SelectedTotalSizeText => InstallerModelPackSizeFormatter.FormatBytes(SelectedTotalSizeBytes);

    public bool CanUseTopCheckbox => rows.Any(static row => row.IsAvailable);

    public InstallerModelPackTopSelectionState TopCheckboxState
    {
        get
        {
            var availableRows = rows.Where(static row => row.IsAvailable).ToArray();
            if (availableRows.Length == 0)
            {
                return InstallerModelPackTopSelectionState.Unchecked;
            }

            var selected = availableRows.Count(static row => row.IsSelected);
            if (selected == 0)
            {
                return InstallerModelPackTopSelectionState.Unchecked;
            }

            return selected == availableRows.Length
                ? InstallerModelPackTopSelectionState.Checked
                : InstallerModelPackTopSelectionState.Indeterminate;
        }
    }

    public void SetSelected(string packId, bool isSelected)
    {
        var row = rows.FirstOrDefault(row =>
            string.Equals(row.PackId, packId, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            throw new ArgumentException($"Unknown installer model pack: {packId}", nameof(packId));
        }

        row.IsSelected = row.IsAvailable && isSelected;
    }

    public void ApplyTopCheckboxAction()
    {
        if (!CanUseTopCheckbox)
        {
            return;
        }

        var selectAll = TopCheckboxState != InstallerModelPackTopSelectionState.Checked;
        foreach (var row in rows.Where(static row => row.IsAvailable))
        {
            row.IsSelected = selectAll;
        }
    }
}

internal static class InstallerModelPackSizeFormatter
{
    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

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
}
