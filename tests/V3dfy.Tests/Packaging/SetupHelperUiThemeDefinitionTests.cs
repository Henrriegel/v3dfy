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
}
