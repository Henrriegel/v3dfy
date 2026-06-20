using System.Text.RegularExpressions;
using V3dfy.SetupHelper;

namespace V3dfy.Tests.Packaging;

public sealed partial class SetupHelperUiThemeDefinitionTests
{
    [Theory]
    [InlineData(SetupUiThemeKind.Light)]
    [InlineData(SetupUiThemeKind.Dark)]
    public void ThemeDefinitions_ContainRequiredColors(SetupUiThemeKind kind)
    {
        var theme = SetupUiThemeDefinition.For(kind);
        var colors = new[]
        {
            theme.WindowBackground,
            theme.PanelBackground,
            theme.ElevatedBackground,
            theme.Text,
            theme.MutedText,
            theme.Accent,
            theme.ButtonBackground,
            theme.ButtonText,
            theme.Border,
            theme.GridBackground,
            theme.GridAlternateBackground,
            theme.LogBackground,
        };

        Assert.All(colors, color => Assert.Matches(HexColorPattern(), color));
        Assert.NotEqual(theme.WindowBackground, theme.Text);
        Assert.NotEqual(theme.ButtonBackground, theme.ButtonText);
        Assert.NotEqual(theme.GridBackground, theme.Text);
    }

    [Theory]
    [InlineData(SetupUiThemeKind.Light)]
    [InlineData(SetupUiThemeKind.Dark)]
    public void ThemeDefinitions_LogTextHasReadableContrast(SetupUiThemeKind kind)
    {
        var theme = SetupUiThemeDefinition.For(kind);

        Assert.True(
            ContrastRatio(theme.Text, theme.LogBackground) >= 4.5,
            $"{kind} log contrast is too low.");
    }

    [Fact]
    public void DarkTheme_UsesV3dfyLikeCyanAccentAndLightText()
    {
        var dark = SetupUiThemeDefinition.For(SetupUiThemeKind.Dark);

        Assert.Equal("#36C5F0", dark.Accent);
        Assert.Equal("#F3F7FB", dark.Text);
        Assert.Equal("#0F141A", dark.WindowBackground);
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorPattern();

    private static double ContrastRatio(string foreground, string background)
    {
        var lighter = Math.Max(RelativeLuminance(foreground), RelativeLuminance(background));
        var darker = Math.Min(RelativeLuminance(foreground), RelativeLuminance(background));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string color)
    {
        var r = Channel(color[1..3]);
        var g = Channel(color[3..5]);
        var b = Channel(color[5..7]);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double Channel(string hex)
    {
        var value = Convert.ToInt32(hex, 16) / 255.0;
        return value <= 0.03928
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
