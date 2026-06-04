using V3dfy.Core.Models;
using V3dfy.Core.Planning;

namespace V3dfy.Core.Execution;

public sealed class ConversionExecutionRequestFactory
{
    public ConversionExecutionRequest Create(
        VideoConversionPlan plan,
        TargetDevicePreset selectedPreset,
        VideoConversionPlanOptions options,
        InternalToolPaths expectedToolPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(selectedPreset);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(expectedToolPaths);

        return new(
            Plan: plan,
            SourcePath: plan.SourcePath,
            OutputPath: plan.SuggestedOutputPath,
            SelectedPreset: selectedPreset,
            Options: options,
            ExpectedToolPaths: expectedToolPaths,
            SelectedLocalModel: plan.SelectedLocalModel,
            CommandPreview: plan.CommandPreview,
            PlanStatus: plan.Status,
            DryRunReason: plan.DryRunReason,
            IsDryRun: plan.IsDryRun,
            CancellationToken: cancellationToken);
    }
}
