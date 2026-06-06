namespace V3dfy.Tests.App;

public sealed class MainWindowViewModelLocalizationSourceTests
{
    [Fact]
    public void LiveConversionSummary_UsesLocalizedProfileAndCompatibilityLabels()
    {
        var source = ReadMainWindowViewModelSource();

        Assert.Contains("\"Live conversion\"", source);
        Assert.Contains("\"Conversion en vivo\"", source);
        Assert.Contains("\"Conversion summary\"", source);
        Assert.Contains("\"Resumen de conversion\"", source);
        Assert.Contains("\"Output profile\"", source);
        Assert.Contains("\"Perfil de salida\"", source);
        Assert.Contains("\"Custom based on", source);
        Assert.Contains("\"Personalizado basado en", source);
        Assert.Contains("\"Primary output\"", source);
        Assert.Contains("\"Salida principal\"", source);
        Assert.Contains("\"LG-compatible copy\"", source);
        Assert.Contains("\"Copia compatible LG\"", source);
        Assert.Contains("\"Create LG 3D TV 2012 compatible MP4 copy\"", source);
        Assert.Contains("\"Crear copia MP4 compatible con LG 3D TV 2012\"", source);
        Assert.Contains("\"Open LG-compatible copy when available\"", source);
        Assert.Contains("\"Abrir copia compatible LG cuando exista\"", source);
        Assert.Contains("\"Primary output was generated successfully", source);
        Assert.Contains("\"La salida principal se genero correctamente", source);
        Assert.Contains("\"LG-compatible copy was generated successfully", source);
        Assert.Contains("\"La copia compatible LG se genero correctamente", source);
        Assert.DoesNotContain("\"Selected preset\"", source);
    }

    private static string ReadMainWindowViewModelSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "V3dfy.App",
                "ViewModels",
                "MainWindowViewModel.cs");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate src/V3dfy.App/ViewModels/MainWindowViewModel.cs.");
    }
}
