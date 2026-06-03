using V3dfy.Core.Execution;

namespace V3dfy.Core.Workflow;

public sealed class ConversionWorkflowState
{
    public int SelectedTabIndex { get; private set; }

    public bool HasCompletedAnalysis { get; private set; }

    public bool CanOpenRecommendedSetupTab => HasCompletedAnalysis;

    public bool CanOpenConversionPlanTab => HasCompletedAnalysis;

    public bool SetSelectedTabIndex(int value)
    {
        if (SelectedTabIndex == value)
        {
            return false;
        }

        SelectedTabIndex = value;
        return true;
    }

    public bool SetHasCompletedAnalysis(
        bool value,
        out bool selectedTabIndexChanged)
    {
        selectedTabIndexChanged = false;

        if (HasCompletedAnalysis == value)
        {
            return false;
        }

        HasCompletedAnalysis = value;
        if (!value && SelectedTabIndex != 0)
        {
            SelectedTabIndex = 0;
            selectedTabIndexChanged = true;
        }

        return true;
    }

    public bool ShowConversionReadinessCard(ConversionExecutionStatus status) =>
        HasCompletedAnalysis &&
        status is ConversionExecutionStatus.NotStarted or ConversionExecutionStatus.Ready;

    public static bool ShowConversionProgressCard(ConversionExecutionStatus status) =>
        status is
            ConversionExecutionStatus.Running or
            ConversionExecutionStatus.Canceling or
            ConversionExecutionStatus.Canceled or
            ConversionExecutionStatus.Failed or
            ConversionExecutionStatus.Completed;
}
