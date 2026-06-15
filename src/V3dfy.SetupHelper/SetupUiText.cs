namespace V3dfy.SetupHelper;

public enum SetupUiLanguage
{
    English,
    Spanish,
}

public sealed class SetupUiText
{
    private SetupUiText(SetupUiLanguage language)
    {
        Language = language;
    }

    public SetupUiLanguage Language { get; }

    public static SetupUiText For(SetupUiLanguage language) => new(language);

    public string WindowTitle => "v3dfy Setup";

    public string LanguageLabel => Language == SetupUiLanguage.Spanish ? "Idioma" : "Language";

    public string ThemeLabel => Language == SetupUiLanguage.Spanish ? "Tema" : "Theme";

    public string EnglishLanguageName => "English";

    public string SpanishLanguageName => "Español";

    public string LightThemeName => Language == SetupUiLanguage.Spanish ? "Claro" : "Light";

    public string DarkThemeName => Language == SetupUiLanguage.Spanish ? "Oscuro" : "Dark";

    public string Subtitle =>
        Language == SetupUiLanguage.Spanish
            ? "Instalador local/offline con modelos opcionales."
            : "Local/offline installer with optional model packs.";

    public string InstallingTitle =>
        Language == SetupUiLanguage.Spanish ? "Instalando v3dfy" : "Installing v3dfy";

    public string OptionalModelPacksTitle =>
        Language == SetupUiLanguage.Spanish ? "Modelos opcionales" : "Optional model packs";

    public string OptionalModelPacksBody =>
        Language == SetupUiLanguage.Spanish
            ? "v3dfy ya incluye un modelo base. Selecciona modelos opcionales para instalarlos ahora, o deja todo sin seleccionar e impórtalos más tarde desde la aplicación."
            : "v3dfy already includes a base model. Select optional model packs to install now, or leave everything unchecked and import models later from the app.";

    public string OfflineNoPacksText =>
        Language == SetupUiLanguage.Spanish
            ? "No se encontraron modelos opcionales junto a este instalador. v3dfy se instalará con su modelo base. Puedes importar modelos más tarde desde la aplicación."
            : "No optional model packs were found beside this installer. v3dfy will still install with its base model. You can import model packs later from the app.";

    public string SelectAllVisibleOptionalModelPacks =>
        Language == SetupUiLanguage.Spanish
            ? "Seleccionar todos los modelos opcionales visibles"
            : "Select all visible optional model packs";

    public string ModelColumn => Language == SetupUiLanguage.Spanish ? "Modelo" : "Model";

    public string BestUseColumn => Language == SetupUiLanguage.Spanish ? "Uso recomendado" : "Best use";

    public string SizeColumn => Language == SetupUiLanguage.Spanish ? "Tamaño" : "Size";

    public string SourceStatusColumn =>
        Language == SetupUiLanguage.Spanish ? "Origen / Estado" : "Source / Status";

    public string WebReleaseAssetStatus =>
        Language == SetupUiLanguage.Spanish ? "Disponible para descargar" : "Available download";

    public string OfflineLocalZipStatus =>
        Language == SetupUiLanguage.Spanish ? "Encontrado junto al instalador" : "Found beside installer";

    public string ContinueButton => Language == SetupUiLanguage.Spanish ? "Continuar" : "Continue";

    public string CancelButton => Language == SetupUiLanguage.Spanish ? "Cancelar" : "Cancel";

    public string CloseButton => Language == SetupUiLanguage.Spanish ? "Cerrar" : "Close";

    public string PreparingSetup =>
        Language == SetupUiLanguage.Spanish ? "Preparando instalación" : "Preparing setup";

    public string OverallProgress =>
        Language == SetupUiLanguage.Spanish ? "Progreso general" : "Overall progress";

    public string CurrentProgress =>
        Language == SetupUiLanguage.Spanish ? "Progreso actual" : "Current progress";

    public string Working => Language == SetupUiLanguage.Spanish ? "Trabajando..." : "Working...";

    public string Details => Language == SetupUiLanguage.Spanish ? "Detalles" : "Details";

    public string InstallationComplete =>
        Language == SetupUiLanguage.Spanish ? "Instalación completa" : "Installation complete";

    public string Complete => Language == SetupUiLanguage.Spanish ? "Completo" : "Complete";

    public string Failed => Language == SetupUiLanguage.Spanish ? "Falló" : "Failed";

    public string InstallationFailed =>
        Language == SetupUiLanguage.Spanish ? "La instalación falló" : "Installation failed";

    public string CancelRequested =>
        Language == SetupUiLanguage.Spanish ? "Cancelación solicitada" : "Cancel requested";

    public string CancelingSetup =>
        Language == SetupUiLanguage.Spanish ? "Cancelando instalación" : "Canceling setup";

    public string DownloadingAction => Language == SetupUiLanguage.Spanish ? "Descargando" : "Downloading";

    public string VerifyingAction => Language == SetupUiLanguage.Spanish ? "Verificando" : "Verifying";

    public string FindingAction => Language == SetupUiLanguage.Spanish ? "Buscando" : "Finding";

    public string RebuildingPortablePackage =>
        Language == SetupUiLanguage.Spanish ? "Reconstruyendo paquete portable" : "Rebuilding portable package";

    public string VerifyingPortablePackage =>
        Language == SetupUiLanguage.Spanish ? "Verificando paquete portable" : "Verifying portable package";

    public string ExtractingFiles =>
        Language == SetupUiLanguage.Spanish ? "Extrayendo archivos" : "Extracting files";

    public string InstallingV3dfy =>
        Language == SetupUiLanguage.Spanish ? "Instalando v3dfy" : "Installing v3dfy";

    public string DownloadingOptionalModelPack =>
        Language == SetupUiLanguage.Spanish ? "Descargando modelo opcional" : "Downloading optional model pack";

    public string VerifyingOptionalModelPack =>
        Language == SetupUiLanguage.Spanish ? "Verificando modelo opcional" : "Verifying optional model pack";

    public string ValidatingOptionalModelPack =>
        Language == SetupUiLanguage.Spanish ? "Validando modelo opcional" : "Validating optional model pack";

    public string InstallingOptionalModelPack =>
        Language == SetupUiLanguage.Spanish ? "Instalando modelo opcional" : "Installing optional model pack";

    public string CleaningTemporaryFiles =>
        Language == SetupUiLanguage.Spanish ? "Limpiando archivos temporales" : "Cleaning temporary files";

    public string OptionalModelPackCatalogLoadFailed =>
        Language == SetupUiLanguage.Spanish
            ? "No se pudo cargar el catálogo de modelos opcionales. v3dfy continuará la instalación con su modelo base."
            : "Optional model-pack catalog could not be loaded. v3dfy will continue installing with its base model.";

    public string OptionalModelPackWarnings =>
        Language == SetupUiLanguage.Spanish
            ? "v3dfy se instaló, pero algunos modelos opcionales no se pudieron instalar. Puedes importarlos más tarde desde la aplicación."
            : "v3dfy was installed, but some optional model packs could not be installed. You can import them later from the app.";

    public string SuccessWithBaseModel =>
        Language == SetupUiLanguage.Spanish
            ? "v3dfy se instaló con su modelo base."
            : "v3dfy was installed with its base model.";

    public string SuccessWithOptionalModelPacks =>
        Language == SetupUiLanguage.Spanish
            ? "v3dfy se instaló. Los modelos opcionales seleccionados fueron instalados."
            : "v3dfy was installed. Selected optional model packs were installed.";

    public string InstallationCanceled =>
        Language == SetupUiLanguage.Spanish ? "La instalación fue cancelada." : "Installation was cancelled.";

    public string WarningPrefix => Language == SetupUiLanguage.Spanish ? "ADVERTENCIA" : "WARNING";

    public string ErrorPrefix => Language == SetupUiLanguage.Spanish ? "ERROR" : "ERROR";

    public string FormatSelectedSummary(int count, string sizeText)
    {
        if (Language == SetupUiLanguage.Spanish)
        {
            var noun = count == 1 ? "modelo" : "modelos";
            return $"Seleccionados: {count} {noun} - {sizeText}";
        }

        var englishNoun = count == 1 ? "model pack" : "model packs";
        return $"Selected: {count} {englishNoun} - {sizeText}";
    }

    public string FormatPayloadPartStatus(string action, int partNumber) =>
        Language == SetupUiLanguage.Spanish
            ? $"{action} parte {partNumber} del paquete"
            : $"{action} payload part {partNumber}";

    public string FormatNoOptionalModelPacksSelected() =>
        Language == SetupUiLanguage.Spanish
            ? "No se seleccionaron modelos opcionales. v3dfy se instalará con su modelo base."
            : "No optional model packs selected. v3dfy will install with its base model.";

    public string FormatOptionalModelPacksSelected(int count) =>
        Language == SetupUiLanguage.Spanish
            ? $"Modelos opcionales seleccionados para descargar, verificar e instalar después del paquete de la aplicación: {count}."
            : $"Optional model packs selected for download, verification, and install after app payload: {count}.";

    public string FormatDownloadingVerifyingOptionalModelPacks(int count) =>
        Language == SetupUiLanguage.Spanish
            ? $"Descargando/verificando modelos opcionales: {count}."
            : $"Downloading/verifying optional model packs: {count}.";

    public string FormatOptionalModelPackAcquisitionFailed(string reason) =>
        Language == SetupUiLanguage.Spanish
            ? $"No se pudieron obtener los modelos opcionales: {reason}. Puedes importarlos más tarde desde la aplicación."
            : $"Optional model-pack acquisition failed: {reason}. You can import model packs later from the app.";

    public string FormatOptionalModelPackImportFailed(string reason) =>
        Language == SetupUiLanguage.Spanish
            ? $"No se pudieron importar los modelos opcionales: {reason}. Puedes importarlos más tarde desde la aplicación."
            : $"Optional model-pack import failed: {reason}. You can import model packs later from the app.";

    public string FormatOptionalModelPacksVerified(int successCount, int failureCount) =>
        failureCount > 0
            ? Language == SetupUiLanguage.Spanish
                ? $"Modelos opcionales verificados: {successCount}; errores de descarga/verificación: {failureCount}."
                : $"Optional model packs verified: {successCount}; acquisition failed: {failureCount}."
            : Language == SetupUiLanguage.Spanish
                ? $"Modelos opcionales verificados: {successCount}."
                : $"Optional model packs verified: {successCount}.";

    public string FormatOptionalModelPacksInstalled(int successCount, int failureCount) =>
        failureCount > 0
            ? Language == SetupUiLanguage.Spanish
                ? $"Modelos opcionales instalados: {successCount}; errores de importación: {failureCount}."
                : $"Optional model packs installed: {successCount}; import failed: {failureCount}."
            : Language == SetupUiLanguage.Spanish
                ? $"Modelos opcionales instalados: {successCount}."
                : $"Optional model packs installed: {successCount}.";

    public string TranslateOverallMessage(string? message) =>
        message switch
        {
            "Preparing payload" => PreparingSetup,
            "Downloading package parts" => Language == SetupUiLanguage.Spanish
                ? "Descargando partes del paquete"
                : "Downloading package parts",
            "Verifying package parts" => Language == SetupUiLanguage.Spanish
                ? "Verificando partes del paquete"
                : "Verifying package parts",
            "Rebuilding portable package" => RebuildingPortablePackage,
            "Verifying portable package" => VerifyingPortablePackage,
            "Extracting files" => ExtractingFiles,
            "Installing files" => Language == SetupUiLanguage.Spanish ? "Instalando archivos" : "Installing files",
            "Finalizing installation" => Language == SetupUiLanguage.Spanish
                ? "Finalizando instalación"
                : "Finalizing installation",
            "Finalizing app payload" => Language == SetupUiLanguage.Spanish
                ? "Finalizando paquete de la aplicación"
                : "Finalizing app payload",
            "App payload installed" => Language == SetupUiLanguage.Spanish
                ? "Paquete de la aplicación instalado"
                : "App payload installed",
            "Downloading optional model packs" => Language == SetupUiLanguage.Spanish
                ? "Descargando modelos opcionales"
                : "Downloading optional model packs",
            "Verifying optional model packs" => Language == SetupUiLanguage.Spanish
                ? "Verificando modelos opcionales"
                : "Verifying optional model packs",
            "Validating optional model packs" => Language == SetupUiLanguage.Spanish
                ? "Validando modelos opcionales"
                : "Validating optional model packs",
            "Installing optional model packs" => Language == SetupUiLanguage.Spanish
                ? "Instalando modelos opcionales"
                : "Installing optional model packs",
            "Installation complete" => InstallationComplete,
            null or "" => OverallProgress,
            _ => message,
        };
}
