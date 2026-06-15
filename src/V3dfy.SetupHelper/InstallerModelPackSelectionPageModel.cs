namespace V3dfy.SetupHelper;

public sealed class InstallerModelPackSelectionPageModel
{
    public const string TitleText = "Optional model packs";

    public const string BodyText =
        "v3dfy already includes a base model. Select optional model packs to install now, or leave everything unchecked and import models later from the app.";

    public const string OfflineNoPacksText =
        "No optional model packs were found beside this installer. v3dfy will still install with its base model. You can import model packs later from the app.";

    public InstallerModelPackSelectionPageModel(
        InstallerModelPackDiscoveryResult discovery,
        SetupUiText? text = null)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        Text = text ?? SetupUiText.For(SetupUiLanguage.English);
        SelectionState = new InstallerModelPackSelectionState(discovery.Rows);
        NoPacksMessage = discovery.Rows.Count == 0
            ? Text.OfflineNoPacksText
            : discovery.NoPacksMessage;
    }

    public SetupUiText Text { get; }

    public InstallerModelPackSelectionState SelectionState { get; }

    public IReadOnlyList<InstallerModelPackSelectionRow> Rows => SelectionState.Rows;

    public string? NoPacksMessage { get; }

    public bool HasRows => Rows.Count > 0;

    public string SelectedSummaryText
    {
        get
        {
            var count = SelectionState.SelectedCount;
            return Text.FormatSelectedSummary(count, SelectionState.SelectedTotalSizeText);
        }
    }
}
