namespace V3dfy.Tests.App;

public sealed class MainWindowXamlStructureTests
{
    [Fact]
    public void ConversionPlanTab_ContainsOutputProfileSelectorBeforeOutputContainer()
    {
        var xaml = ReadMainWindowXaml();

        var profileSelectorIndex = xaml.IndexOf(
            "SelectedOutputPreset",
            StringComparison.Ordinal);
        var outputContainerIndex = xaml.IndexOf(
            "SelectedOutputContainer",
            StringComparison.Ordinal);

        Assert.True(profileSelectorIndex >= 0);
        Assert.True(outputContainerIndex >= 0);
        Assert.True(profileSelectorIndex < outputContainerIndex);
        Assert.Contains("ShowProfileDetailsCommand", xaml);
        Assert.Contains("ProfileDetailsBodyText", xaml);
    }

    [Fact]
    public void MainWindow_DoesNotKeepPermanentSelectedOutputPresetCard()
    {
        var xaml = ReadMainWindowXaml();

        Assert.DoesNotContain("RecommendedPresetTitle", xaml);
        Assert.DoesNotContain("Grid.Row=\"4\" Style=\"{StaticResource CardStyle}\"", xaml);
    }

    [Fact]
    public void ConversionPlanTab_HidesLgOptionsBehindProfileVisibilityBinding()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("Visibility=\"{Binding LgCompatibilityOptionsVisibility}\"", xaml);
        Assert.Contains("CreateLgCompatibilityCopyText", xaml);
        Assert.Contains("PreferLgCompatibilityCopyWhenOpeningText", xaml);
        Assert.Contains("LgCompatibilityCopyExplanationText", xaml);
        Assert.Contains("InputBackgroundBrush", xaml);
        Assert.Contains("CardBorderBrush", xaml);
        Assert.Contains("BorderThickness=\"1\"", xaml);
        Assert.Contains("TargetType=\"CheckBox\"", xaml);
        Assert.Contains("PrimaryTextBrush", xaml);
    }

    private static string ReadMainWindowXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "V3dfy.App",
                "MainWindow.xaml");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate src/V3dfy.App/MainWindow.xaml.");
    }
}
