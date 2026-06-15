namespace V3dfy.SetupHelper;

public enum SetupUiThemeKind
{
    Light,
    Dark,
}

public sealed record SetupUiThemeDefinition(
    SetupUiThemeKind Kind,
    string WindowBackground,
    string PanelBackground,
    string ElevatedBackground,
    string Text,
    string MutedText,
    string Accent,
    string ButtonBackground,
    string ButtonText,
    string Border,
    string GridBackground,
    string GridAlternateBackground,
    string LogBackground)
{
    public static SetupUiThemeDefinition For(SetupUiThemeKind kind) =>
        kind == SetupUiThemeKind.Light ? Light : Dark;

    public static SetupUiThemeDefinition Light { get; } = new(
        SetupUiThemeKind.Light,
        WindowBackground: "#F4F7FA",
        PanelBackground: "#FFFFFF",
        ElevatedBackground: "#EAF1F7",
        Text: "#16202A",
        MutedText: "#526171",
        Accent: "#0677B8",
        ButtonBackground: "#087DBF",
        ButtonText: "#FFFFFF",
        Border: "#C8D3DF",
        GridBackground: "#FFFFFF",
        GridAlternateBackground: "#F0F5FA",
        LogBackground: "#111820");

    public static SetupUiThemeDefinition Dark { get; } = new(
        SetupUiThemeKind.Dark,
        WindowBackground: "#0F141A",
        PanelBackground: "#171F28",
        ElevatedBackground: "#202A35",
        Text: "#F3F7FB",
        MutedText: "#A9B6C4",
        Accent: "#36C5F0",
        ButtonBackground: "#168CC4",
        ButtonText: "#FFFFFF",
        Border: "#324050",
        GridBackground: "#151D26",
        GridAlternateBackground: "#1C2631",
        LogBackground: "#0A0F14");
}
