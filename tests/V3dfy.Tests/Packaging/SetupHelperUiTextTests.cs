using V3dfy.SetupHelper;

namespace V3dfy.Tests.Packaging;

public sealed class SetupHelperUiTextTests
{
    [Fact]
    public void TextCatalog_ContainsEnglishAndSpanishInstallerStrings()
    {
        var english = SetupUiText.For(SetupUiLanguage.English);
        var spanish = SetupUiText.For(SetupUiLanguage.Spanish);

        Assert.Equal("Optional model packs", english.OptionalModelPacksTitle);
        Assert.Equal("Modelos opcionales", spanish.OptionalModelPacksTitle);
        Assert.Contains("v3dfy already includes a base model", english.OptionalModelPacksBody);
        Assert.Contains("v3dfy ya incluye un modelo base", spanish.OptionalModelPacksBody);
        Assert.Equal("Continue", english.ContinueButton);
        Assert.Equal("Continuar", spanish.ContinueButton);
        Assert.Equal("Cancel", english.CancelButton);
        Assert.Equal("Cancelar", spanish.CancelButton);
        Assert.Equal("Close", english.CloseButton);
        Assert.Equal("Cerrar", spanish.CloseButton);
        Assert.Equal("Downloading optional model pack", english.DownloadingOptionalModelPack);
        Assert.Equal("Descargando modelo opcional", spanish.DownloadingOptionalModelPack);
        Assert.Equal("Verifying optional model pack", english.VerifyingOptionalModelPack);
        Assert.Equal("Verificando modelo opcional", spanish.VerifyingOptionalModelPack);
        Assert.Equal("Installing optional model pack", english.InstallingOptionalModelPack);
        Assert.Equal("Instalando modelo opcional", spanish.InstallingOptionalModelPack);
    }

    [Fact]
    public void TextCatalog_ContainsSpanishSetupLogLabels()
    {
        var spanish = SetupUiText.For(SetupUiLanguage.Spanish);

        Assert.Equal("Buscar", spanish.LogFindLabel);
        Assert.Equal("Verificar", spanish.LogVerifyLabel);
        Assert.Equal("Progreso de verificacion", spanish.LogVerifyProgressLabel);
        Assert.Equal("Reconstruir paquete", spanish.LogRebuildPackageLabel);
        Assert.Equal("Progreso de reconstruccion", spanish.LogRebuildProgressLabel);
        Assert.Equal("Verificar paquete", spanish.LogVerifyPackageLabel);
        Assert.Equal("Progreso de verificacion del paquete", spanish.LogVerifyPackageProgressLabel);
        Assert.Equal("Limpiar archivos temporales", spanish.LogCleanTemporaryFilesLabel);
        Assert.Contains("solicitada", spanish.CancelRequested);
        Assert.Equal("ERROR", spanish.ErrorPrefix);
        Assert.Contains("cancelada", spanish.InstallationCanceled);
    }

    [Fact]
    public void SelectionSummary_IsLocalized()
    {
        var english = SetupUiText.For(SetupUiLanguage.English);
        var spanish = SetupUiText.For(SetupUiLanguage.Spanish);

        Assert.Equal("Selected: 1 model pack - 1 KB", english.FormatSelectedSummary(1, "1 KB"));
        Assert.Equal("Selected: 2 model packs - 3 KB", english.FormatSelectedSummary(2, "3 KB"));
        Assert.Equal("Seleccionados: 1 modelo - 1 KB", spanish.FormatSelectedSummary(1, "1 KB"));
        Assert.Equal("Seleccionados: 2 modelos - 3 KB", spanish.FormatSelectedSummary(2, "3 KB"));
    }

    [Fact]
    public void SelectionPageModel_UsesProvidedTextCatalog()
    {
        var page = new InstallerModelPackSelectionPageModel(
            new InstallerModelPackDiscoveryResult(
                [CreateRow("first", 1024), CreateRow("second", 2048)],
                NoPacksMessage: null),
            SetupUiText.For(SetupUiLanguage.Spanish));

        Assert.Equal("Seleccionados: 0 modelos - 0 B", page.SelectedSummaryText);

        page.SelectionState.SetSelected("first", true);

        Assert.Equal("Seleccionados: 1 modelo - 1 KB", page.SelectedSummaryText);

        page.SelectionState.ApplyTopCheckboxAction();

        Assert.Equal("Seleccionados: 2 modelos - 3 KB", page.SelectedSummaryText);
    }

    [Fact]
    public void OverallProgressMessages_CanBeTranslated()
    {
        var spanish = SetupUiText.For(SetupUiLanguage.Spanish);

        Assert.Equal("Descargando modelos opcionales", spanish.TranslateOverallMessage("Downloading optional model packs"));
        Assert.Equal("Verificando modelos opcionales", spanish.TranslateOverallMessage("Verifying optional model packs"));
        Assert.Equal("Validando modelos opcionales", spanish.TranslateOverallMessage("Validating optional model packs"));
        Assert.Equal("Instalando modelos opcionales", spanish.TranslateOverallMessage("Installing optional model packs"));
        Assert.Equal("Instalación completa", spanish.TranslateOverallMessage("Installation complete"));
    }

    private static InstallerModelPackSelectionRow CreateRow(string packId, long zipSizeBytes) =>
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
            isAvailable: true,
            InstallerModelPackSourceKind.WebReleaseAsset);
}
