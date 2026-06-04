using V3dfy.Core.Models;

namespace V3dfy.Core.Readiness;

public sealed class ConversionReadinessService
{
    private const string EnglishReadyStatus =
        "Conversion ready. All required local components are available.";
    private const string SpanishReadyStatus =
        "Conversión lista. Todos los componentes locales requeridos están disponibles.";
    private const string EnglishBlockedStatus =
        "Conversion unavailable. Required local components are missing.";
    private const string SpanishBlockedStatus =
        "Conversión no disponible. Faltan componentes locales requeridos.";
    private const string EnglishRequiredComponents =
        "Required components: FFmpeg, FFprobe, embedded Python runtime, local iw3 engine, local 3D models.";
    private const string SpanishRequiredComponents =
        "Componentes requeridos: FFmpeg, FFprobe, runtime de Python embebido, motor local iw3, modelos 3D locales.";

    public ConversionReadiness Evaluate(EngineHealthStatus healthStatus)
    {
        ArgumentNullException.ThrowIfNull(healthStatus);

        var issues = new List<ConversionReadinessIssue>();

        AddMissingIssue(
            healthStatus.Ffmpeg,
            issues,
            "FFmpeg is missing.",
            "Falta FFmpeg.");
        AddMissingIssue(
            healthStatus.Ffprobe,
            issues,
            "FFprobe is missing.",
            "Falta FFprobe.");
        AddMissingIssue(
            healthStatus.Python,
            issues,
            "Embedded Python runtime is missing.",
            "Falta el runtime de Python embebido.");
        AddMissingIssue(
            healthStatus.Iw3EngineDirectory,
            issues,
            "Local iw3 engine is missing.",
            "Falta el motor local iw3.");
        AddMissingIssue(
            healthStatus.ModelsDirectory,
            issues,
            "Local 3D models are missing.",
            "Faltan los modelos 3D locales.");

        var canConvert = issues.Count == 0;

        return new(
            CanConvert: canConvert,
            EnglishStatus: canConvert ? EnglishReadyStatus : EnglishBlockedStatus,
            SpanishStatus: canConvert ? SpanishReadyStatus : SpanishBlockedStatus,
            Issues: issues,
            EnglishRequiredComponentsSummary: EnglishRequiredComponents,
            SpanishRequiredComponentsSummary: SpanishRequiredComponents);
    }

    public ConversionReadiness Evaluate(EngineDependencyHealth dependencyHealth)
    {
        ArgumentNullException.ThrowIfNull(dependencyHealth);

        var issues = new List<ConversionReadinessIssue>();

        AddDetailedMissingIssue(
            dependencyHealth.Ffmpeg,
            issues,
            "FFmpeg is missing.",
            "Falta FFmpeg.");
        AddDetailedMissingIssue(
            dependencyHealth.Ffprobe,
            issues,
            "FFprobe is missing.",
            "Falta FFprobe.");
        AddDetailedMissingIssue(
            dependencyHealth.Python,
            issues,
            "Embedded Python runtime is missing.",
            "Falta el runtime de Python embebido.");
        AddDetailedMissingIssue(
            dependencyHealth.Iw3EngineDirectory,
            issues,
            "Local iw3 engine is missing.",
            "Falta el motor local iw3.");
        AddDetailedMissingIssue(
            dependencyHealth.ModelsDirectory,
            issues,
            "Local 3D models are missing.",
            "Faltan los modelos 3D locales.");

        var canConvert = issues.Count == 0;

        return new(
            CanConvert: canConvert,
            EnglishStatus: canConvert ? EnglishReadyStatus : EnglishBlockedStatus,
            SpanishStatus: canConvert ? SpanishReadyStatus : SpanishBlockedStatus,
            Issues: issues,
            EnglishRequiredComponentsSummary: EnglishRequiredComponents,
            SpanishRequiredComponentsSummary: SpanishRequiredComponents);
    }

    private static void AddMissingIssue(
        ToolHealthStatus status,
        ICollection<ConversionReadinessIssue> issues,
        string englishMessage,
        string spanishMessage)
    {
        if (status == ToolHealthStatus.Missing)
        {
            issues.Add(new(englishMessage, spanishMessage));
        }
    }

    private static void AddDetailedMissingIssue(
        ToolDependencyHealth dependencyHealth,
        ICollection<ConversionReadinessIssue> issues,
        string englishMessage,
        string spanishMessage)
    {
        if (dependencyHealth.Status == ToolHealthStatus.Found)
        {
            return;
        }

        issues.Add(new(
            $"{englishMessage} {EnglishDetail(dependencyHealth)}",
            $"{spanishMessage} {SpanishDetail(dependencyHealth)}"));
    }

    private static string EnglishDetail(ToolDependencyHealth dependencyHealth) =>
        dependencyHealth.DetailKind switch
        {
            ToolHealthDetailKind.BundledFileMissing =>
                $"Expected bundled executable: {dependencyHealth.ExpectedPath}",
            ToolHealthDetailKind.EngineDirectoryMissing =>
                $"Expected engine directory: {dependencyHealth.ExpectedPath}",
            ToolHealthDetailKind.EnginePlaceholderOnly =>
                $"Engine directory exists but only placeholder or contract files were detected: {dependencyHealth.ExpectedPath}",
            ToolHealthDetailKind.EngineManifestMissing =>
                $"Expected a non-placeholder engine manifest: {Path.Combine(dependencyHealth.ExpectedPath, "ENGINE_MANIFEST.json")}",
            ToolHealthDetailKind.EngineEntryFilesMissing =>
                $"Expected the iw3 package entry file: {ExpectedEngineEntryPath(dependencyHealth.ExpectedPath)}",
            ToolHealthDetailKind.ModelsDirectoryMissing =>
                $"Expected pretrained models directory: {dependencyHealth.ExpectedPath}",
            ToolHealthDetailKind.ModelFilesMissing =>
                $"No supported model files were found under: {dependencyHealth.ExpectedPath}",
            _ => $"Expected local path: {dependencyHealth.ExpectedPath}",
        };

    private static string SpanishDetail(ToolDependencyHealth dependencyHealth) =>
        dependencyHealth.DetailKind switch
        {
            ToolHealthDetailKind.BundledFileMissing =>
                $"Ejecutable incluido esperado: {dependencyHealth.ExpectedPath}",
            ToolHealthDetailKind.EngineDirectoryMissing =>
                $"Carpeta esperada del motor: {dependencyHealth.ExpectedPath}",
            ToolHealthDetailKind.EnginePlaceholderOnly =>
                $"La carpeta del motor existe, pero solo contiene marcadores o archivos de contrato: {dependencyHealth.ExpectedPath}",
            ToolHealthDetailKind.EngineManifestMissing =>
                $"Se esperaba un manifiesto del motor que no fuera marcador: {Path.Combine(dependencyHealth.ExpectedPath, "ENGINE_MANIFEST.json")}",
            ToolHealthDetailKind.EngineEntryFilesMissing =>
                $"Se esperaba el archivo de entrada del paquete iw3: {ExpectedEngineEntryPath(dependencyHealth.ExpectedPath)}",
            ToolHealthDetailKind.ModelsDirectoryMissing =>
                $"Carpeta esperada de modelos preentrenados: {dependencyHealth.ExpectedPath}",
            ToolHealthDetailKind.ModelFilesMissing =>
                $"No se encontraron modelos compatibles en: {dependencyHealth.ExpectedPath}",
            _ => $"Ruta local esperada: {dependencyHealth.ExpectedPath}",
        };

    private static string ExpectedEngineEntryPath(string engineDirectory) =>
        Path.Combine(
            [engineDirectory, .. SplitRelativePath(Iw3EngineBundleContract.EngineEntryRelativePaths[0])]);

    private static string[] SplitRelativePath(string relativePath) =>
        relativePath.Split(
            ['/', '\\'],
            StringSplitOptions.RemoveEmptyEntries);
}
