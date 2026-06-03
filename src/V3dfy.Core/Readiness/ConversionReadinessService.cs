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
}
