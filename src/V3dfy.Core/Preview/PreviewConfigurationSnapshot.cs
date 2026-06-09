using V3dfy.Core.Models;
using V3dfy.Core.Planning;

namespace V3dfy.Core.Preview;

public sealed record PreviewConfigurationSnapshot(
    string SourcePath,
    string OutputProfileName,
    OutputContainer OutputContainer,
    AiQualityPreset QualityPreset,
    ThreeDIntensity Intensity,
    ThreeDOutputFormat ThreeDOutputFormat,
    string ModelKey,
    string ModelDisplayName,
    string ModelRelativePath,
    string Iw3DepthModelName,
    TimeSpan PreviewStartTime,
    TimeSpan PreviewDuration)
{
    public string Fingerprint => string.Join(
        "|",
        [
            Normalize(SourcePath),
            Normalize(OutputProfileName),
            OutputContainer.ToString(),
            QualityPreset.ToString(),
            Intensity.ToString(),
            ThreeDOutputFormat.ToString(),
            Normalize(ModelKey),
            Normalize(ModelRelativePath),
            Normalize(Iw3DepthModelName),
            PreviewStartTime.ToString("c"),
            PreviewDuration.ToString("c"),
        ]);

    public static PreviewConfigurationSnapshot Create(
        VideoConversionPlan plan,
        TargetDevicePreset selectedPreset,
        PreviewTimeRange previewRange)
    {
        ArgumentNullException.ThrowIfNull(previewRange);

        return Create(
            plan,
            selectedPreset,
            previewRange.From,
            previewRange.Duration);
    }

    public static PreviewConfigurationSnapshot Create(
        VideoConversionPlan plan,
        TargetDevicePreset selectedPreset,
        TimeSpan previewStartTime,
        TimeSpan previewDuration)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(selectedPreset);

        var selectedModel = plan.SelectedLocalModel;
        return new(
            SourcePath: plan.SourcePath,
            OutputProfileName: selectedPreset.Name,
            OutputContainer: plan.OutputContainer,
            QualityPreset: plan.QualityPreset,
            Intensity: plan.Intensity,
            ThreeDOutputFormat: plan.ThreeDOutputFormat,
            ModelKey: FirstNonEmpty(selectedModel?.MappingKey, selectedModel?.Id, selectedModel?.RelativePath),
            ModelDisplayName: selectedModel?.DisplayName ?? string.Empty,
            ModelRelativePath: selectedModel?.RelativePath ?? string.Empty,
            Iw3DepthModelName: selectedModel?.Iw3DepthModelName ?? string.Empty,
            PreviewStartTime: previewStartTime,
            PreviewDuration: previewDuration);
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace('\\', '/').ToUpperInvariant();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
