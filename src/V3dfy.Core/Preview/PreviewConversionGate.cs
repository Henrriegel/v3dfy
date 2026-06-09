namespace V3dfy.Core.Preview;

public static class PreviewConversionGate
{
    public static PreviewConversionGateResult Evaluate(
        PreviewWorkflowState state,
        PreviewConfigurationSnapshot? currentConfiguration)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (currentConfiguration is null)
        {
            return PreviewRequired(
                "Generate and accept a preview after the video analysis is complete.",
                "Genera y acepta una vista previa despues de completar el analisis del video.");
        }

        if (state.IsAcceptedCurrent(currentConfiguration))
        {
            return new(
                CanStart: true,
                EnglishStatus: "Preview accepted",
                SpanishStatus: "Vista previa aceptada",
                EnglishDetail: "The accepted preview matches the current selected configuration.",
                SpanishDetail: "La vista previa aceptada coincide con la configuracion seleccionada actual.");
        }

        if (state.Status == PreviewGenerationStatus.Outdated ||
            (state.Status == PreviewGenerationStatus.Accepted &&
             !string.Equals(
                 state.ConfigurationFingerprint,
                 currentConfiguration.Fingerprint,
                 StringComparison.Ordinal)))
        {
            return new(
                CanStart: false,
                EnglishStatus: "Preview outdated",
                SpanishStatus: "Vista previa desactualizada",
                EnglishDetail: "Regenerate and accept the preview before final conversion.",
                SpanishDetail: "Regenera y acepta la vista previa antes de la conversion final.");
        }

        return PreviewRequired(state.Status switch
        {
            PreviewGenerationStatus.Generating =>
                "Wait for the preview to finish, then review and continue.",
            PreviewGenerationStatus.Ready =>
                "Review the preview and click Continue before final conversion.",
            PreviewGenerationStatus.Failed =>
                "Generate a successful preview before final conversion.",
            PreviewGenerationStatus.Canceled =>
                "Generate and accept a preview before final conversion.",
            _ =>
                "Generate and accept a preview before final conversion.",
        }, state.Status switch
        {
            PreviewGenerationStatus.Generating =>
                "Espera a que termine la vista previa, revisala y continua.",
            PreviewGenerationStatus.Ready =>
                "Revisa la vista previa y haz clic en Continuar antes de la conversion final.",
            PreviewGenerationStatus.Failed =>
                "Genera una vista previa correcta antes de la conversion final.",
            PreviewGenerationStatus.Canceled =>
                "Genera y acepta una vista previa antes de la conversion final.",
            _ =>
                "Genera y acepta una vista previa antes de la conversion final.",
        });
    }

    private static PreviewConversionGateResult PreviewRequired(
        string englishDetail,
        string spanishDetail) => new(
        CanStart: false,
        EnglishStatus: "Preview required",
        SpanishStatus: "Vista previa requerida",
        EnglishDetail: englishDetail,
        SpanishDetail: spanishDetail);
}
