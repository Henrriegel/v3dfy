using V3dfy.Core.Readiness;

namespace V3dfy.Core.Execution;

public sealed class ConversionExecutionFeatureGate(bool isRealConversionExecutionEnabled = true)
{
    public bool IsRealConversionExecutionEnabled { get; } = isRealConversionExecutionEnabled;

    public bool CanStartConversion(
        bool hasCompletedAnalysis,
        bool hasConversionPlan,
        ConversionReadiness? readiness) =>
        EvaluateStart(hasCompletedAnalysis, hasConversionPlan, readiness).CanStart;

    public ConversionExecutionStartGateResult EvaluateStart(
        bool hasCompletedAnalysis,
        bool hasConversionPlan,
        ConversionReadiness? readiness)
    {
        if (!hasCompletedAnalysis)
        {
            return ConversionExecutionStartGateResult.Blocked(
                ConversionExecutionBlocker.NoCompletedAnalysis,
                "Conversion blocked until a source video is analyzed.",
                "La conversi\u00f3n est\u00e1 bloqueada hasta analizar un video de origen.",
                "Analyze a video before conversion can start. No local process was started.",
                "Analiza un video antes de iniciar la conversi\u00f3n. No se inici\u00f3 ning\u00fan proceso local.");
        }

        if (!hasConversionPlan)
        {
            return ConversionExecutionStartGateResult.Blocked(
                ConversionExecutionBlocker.MissingConversionPlan,
                "Conversion blocked until a conversion plan is prepared.",
                "La conversi\u00f3n est\u00e1 bloqueada hasta preparar un plan de conversi\u00f3n.",
                "Prepare the conversion plan before conversion can start. No local process was started.",
                "Prepara el plan de conversi\u00f3n antes de iniciar. No se inici\u00f3 ning\u00fan proceso local.");
        }

        if (readiness is null)
        {
            return ConversionExecutionStartGateResult.Blocked(
                ConversionExecutionBlocker.ReadinessUnknown,
                "Conversion blocked until local dependency readiness is checked.",
                "La conversi\u00f3n est\u00e1 bloqueada hasta revisar las dependencias locales.",
                "Refresh system status before conversion can start. No local process was started.",
                "Actualiza el estado del sistema antes de iniciar. No se inici\u00f3 ning\u00fan proceso local.");
        }

        if (!readiness.CanConvert)
        {
            return ConversionExecutionStartGateResult.Blocked(
                ConversionExecutionBlocker.MissingLocalDependencies,
                "Conversion blocked by missing local engine dependencies.",
                "La conversi\u00f3n est\u00e1 bloqueada por dependencias locales faltantes del motor.",
                "Bundle FFmpeg, FFprobe, embedded Python, the local iw3 engine, and local 3D models before conversion. No Python, iw3, or FFmpeg conversion process was started.",
                "Incluye FFmpeg, FFprobe, Python embebido, el motor local iw3 y los modelos 3D locales antes de convertir. No se inici\u00f3 ning\u00fan proceso de Python, iw3 ni conversi\u00f3n con FFmpeg.");
        }

        if (!IsRealConversionExecutionEnabled)
        {
            return ConversionExecutionStartGateResult.Blocked(
                ConversionExecutionBlocker.FeatureDisabled,
                "Conversion execution is not enabled yet.",
                "La ejecuci\u00f3n de conversi\u00f3n a\u00fan no est\u00e1 habilitada.",
                "Conversion execution was explicitly disabled by configuration. No Python, iw3, or FFmpeg conversion process was started.",
                "La ejecucion de conversion fue deshabilitada explicitamente por configuracion. No se inicio ningun proceso de Python, iw3 ni conversion con FFmpeg.");
        }

        return ConversionExecutionStartGateResult.Ready();
    }
}
