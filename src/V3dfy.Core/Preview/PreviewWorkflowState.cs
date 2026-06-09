namespace V3dfy.Core.Preview;

public sealed record PreviewWorkflowState(
    PreviewGenerationStatus Status,
    string? OutputPath,
    string? ConfigurationFingerprint,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    TimeSpan PreviewStartTime,
    TimeSpan PreviewDuration,
    string EnglishDetail,
    string SpanishDetail,
    bool WasAccepted = false)
{
    public bool IsGenerating => Status == PreviewGenerationStatus.Generating;

    public bool IsReady => Status is PreviewGenerationStatus.Ready or PreviewGenerationStatus.Accepted;

    public bool IsAccepted => Status == PreviewGenerationStatus.Accepted;

    public bool IsOutdated => Status == PreviewGenerationStatus.Outdated;

    public static PreviewWorkflowState NotGenerated(
        TimeSpan previewStartTime,
        TimeSpan previewDuration) => new(
        Status: PreviewGenerationStatus.NotGenerated,
        OutputPath: null,
        ConfigurationFingerprint: null,
        StartedAt: null,
        FinishedAt: null,
        PreviewStartTime: previewStartTime,
        PreviewDuration: previewDuration,
        EnglishDetail: "No preview generated.",
        SpanishDetail: "No se ha generado una vista previa.");

    public PreviewWorkflowState Generating(
        PreviewConfigurationSnapshot configuration,
        DateTimeOffset startedAt) => this with
    {
        Status = PreviewGenerationStatus.Generating,
        OutputPath = null,
        ConfigurationFingerprint = configuration.Fingerprint,
        StartedAt = startedAt,
        FinishedAt = null,
        PreviewStartTime = configuration.PreviewStartTime,
        PreviewDuration = configuration.PreviewDuration,
        EnglishDetail = "Preparing preview...",
        SpanishDetail = "Preparando vista previa...",
        WasAccepted = false,
    };

    public PreviewWorkflowState Complete(
        PreviewGenerationResult result,
        PreviewConfigurationSnapshot configuration) => this with
    {
        Status = result.Status,
        OutputPath = result.Success ? result.PreviewOutputPath : OutputPath,
        ConfigurationFingerprint = configuration.Fingerprint,
        StartedAt = result.StartedAt,
        FinishedAt = result.FinishedAt,
        PreviewStartTime = configuration.PreviewStartTime,
        PreviewDuration = configuration.PreviewDuration,
        EnglishDetail = result.EnglishSummary,
        SpanishDetail = result.SpanishSummary,
        WasAccepted = false,
    };

    public PreviewWorkflowState Accept(
        PreviewConfigurationSnapshot currentConfiguration)
    {
        ArgumentNullException.ThrowIfNull(currentConfiguration);

        if (Status != PreviewGenerationStatus.Ready ||
            string.IsNullOrWhiteSpace(OutputPath) ||
            !string.Equals(
                ConfigurationFingerprint,
                currentConfiguration.Fingerprint,
                StringComparison.Ordinal))
        {
            return MarkOutdated(
                "Preview is outdated. Regenerate it for the current settings.",
                "La vista previa esta desactualizada. Regenerala para la configuracion actual.");
        }

        return this with
        {
            Status = PreviewGenerationStatus.Accepted,
            EnglishDetail = "Preview accepted. Final conversion is unlocked.",
            SpanishDetail = "Vista previa aceptada. La conversion final esta desbloqueada.",
            WasAccepted = true,
        };
    }

    public bool IsAcceptedCurrent(PreviewConfigurationSnapshot currentConfiguration)
    {
        ArgumentNullException.ThrowIfNull(currentConfiguration);

        return Status == PreviewGenerationStatus.Accepted &&
            !string.IsNullOrWhiteSpace(OutputPath) &&
            string.Equals(
                ConfigurationFingerprint,
                currentConfiguration.Fingerprint,
                StringComparison.Ordinal);
    }

    public PreviewWorkflowState Deleted(
        TimeSpan previewStartTime,
        TimeSpan previewDuration) => NotGenerated(previewStartTime, previewDuration);

    public PreviewWorkflowState MarkOutdated(
        string englishDetail,
        string spanishDetail) =>
        Status is PreviewGenerationStatus.Ready or PreviewGenerationStatus.Accepted
            ? this with
            {
                Status = PreviewGenerationStatus.Outdated,
                EnglishDetail = englishDetail,
                SpanishDetail = spanishDetail,
            }
            : this;

    public PreviewWorkflowState RestoreAcceptedIfCurrent(
        PreviewConfigurationSnapshot currentConfiguration,
        bool previewFileExists)
    {
        ArgumentNullException.ThrowIfNull(currentConfiguration);

        if (Status != PreviewGenerationStatus.Outdated ||
            !WasAccepted ||
            !previewFileExists ||
            string.IsNullOrWhiteSpace(OutputPath) ||
            !string.Equals(
                ConfigurationFingerprint,
                currentConfiguration.Fingerprint,
                StringComparison.Ordinal))
        {
            return this;
        }

        return this with
        {
            Status = PreviewGenerationStatus.Accepted,
            EnglishDetail = "Preview accepted. Final conversion is unlocked.",
            SpanishDetail = "Vista previa aceptada. La conversion final esta desbloqueada.",
        };
    }

    public PreviewWorkflowState UpdateForCurrentConfiguration(
        PreviewConfigurationSnapshot currentConfiguration,
        bool previewFileExists)
    {
        ArgumentNullException.ThrowIfNull(currentConfiguration);

        var restored = RestoreAcceptedIfCurrent(currentConfiguration, previewFileExists);
        if (restored != this)
        {
            return restored;
        }

        return MarkOutdatedIfConfigurationChanged(currentConfiguration);
    }

    public PreviewWorkflowState MarkOutdatedIfConfigurationChanged(
        PreviewConfigurationSnapshot currentConfiguration)
    {
        ArgumentNullException.ThrowIfNull(currentConfiguration);

        if (Status is not (PreviewGenerationStatus.Ready or PreviewGenerationStatus.Accepted) ||
            string.Equals(
                ConfigurationFingerprint,
                currentConfiguration.Fingerprint,
                StringComparison.Ordinal))
        {
            return this;
        }

        return MarkOutdated(
            "Preview is outdated. Regenerate it for the current settings.",
            "La vista previa esta desactualizada. Regenerala para la configuracion actual.");
    }
}
