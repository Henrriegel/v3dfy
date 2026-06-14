namespace V3dfy.SetupHelper;

public sealed class InstallerModelPackSelectionPageModel
{
    public const string TitleText = "Optional model packs";

    public const string BodyText =
        "v3dfy already includes a base model. Select optional model packs to install now, or leave everything unchecked and import models later from the app.";

    public const string OfflineNoPacksText =
        "No optional model packs found beside this installer. v3dfy will still install with its base model. You can import model packs later from the app.";

    public InstallerModelPackSelectionPageModel(InstallerModelPackDiscoveryResult discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        SelectionState = new InstallerModelPackSelectionState(discovery.Rows);
        NoPacksMessage = discovery.Rows.Count == 0
            ? OfflineNoPacksText
            : discovery.NoPacksMessage;
    }

    public InstallerModelPackSelectionState SelectionState { get; }

    public IReadOnlyList<InstallerModelPackSelectionRow> Rows => SelectionState.Rows;

    public string? NoPacksMessage { get; }

    public bool HasRows => Rows.Count > 0;

    public string SelectedSummaryText
    {
        get
        {
            var count = SelectionState.SelectedCount;
            var noun = count == 1 ? "model pack" : "model packs";
            return $"Selected: {count} {noun} - {SelectionState.SelectedTotalSizeText}";
        }
    }
}
