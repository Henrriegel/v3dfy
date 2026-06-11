using V3dfy.Core.Models;

namespace V3dfy.Infrastructure.ModelPacks;

public static class ModelPackImportConfirmationFormatter
{
    public static ModelPackImportConfirmationPrompt CreatePrompt(
        ModelPackImportPreparationResult preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);

        return new ModelPackImportConfirmationPrompt(
            preparation,
            EnglishTitle: "Confirm model pack import",
            SpanishTitle: "Confirmar importacion de paquete de modelos",
            EnglishMessage: CreateMessage(preparation, useSpanish: false),
            SpanishMessage: CreateMessage(preparation, useSpanish: true));
    }

    private static string CreateMessage(
        ModelPackImportPreparationResult preparation,
        bool useSpanish)
    {
        var adminLine = preparation.ElevationRequired
            ? useSpanish
                ? "Permiso de administrador/UAC: requerido. Windows pedira permiso antes de instalar."
                : "Administrator/UAC permission: required. Windows will ask for permission before installing."
            : useSpanish
                ? "Permiso de administrador/UAC: no se espera para este destino, pero Windows podria pedirlo segun los permisos de la carpeta."
                : "Administrator/UAC permission: not expected for this target, but Windows may still ask depending on folder permissions.";

        IReadOnlyList<string> lines = useSpanish
            ?
            [
                $"Paquete: {DisplayName(preparation)}",
                $"Archivos por instalar: {preparation.FilesToInstall.Count}",
                $"Archivos ya instalados: {preparation.AlreadyInstalledFiles.Count}",
                $"Conflictos: {preparation.Conflicts.Count}",
                $"Carpeta destino: {preparation.TargetPretrainedModelsRoot}",
                adminLine,
                "Disponibilidad de modelos: los modelos importados solo seran seleccionables cuando v3dfy ya los soporte o los tenga mapeados.",
                string.Empty,
                "Continua solo si confias en este paquete de modelos.",
            ]
            :
            [
                $"Pack: {DisplayName(preparation)}",
                $"Files to install: {preparation.FilesToInstall.Count}",
                $"Already installed files: {preparation.AlreadyInstalledFiles.Count}",
                $"Conflicts: {preparation.Conflicts.Count}",
                $"Target folder: {preparation.TargetPretrainedModelsRoot}",
                adminLine,
                "Model availability: imported models become selectable only when v3dfy already supports or maps them.",
                string.Empty,
                "Continue only if you trust this model pack.",
            ];

        return string.Join(Environment.NewLine, lines);
    }

    private static string DisplayName(ModelPackImportPreparationResult preparation) =>
        preparation.Manifest?.DisplayName ??
        Path.GetFileName(preparation.ModelPackZipPath);
}
