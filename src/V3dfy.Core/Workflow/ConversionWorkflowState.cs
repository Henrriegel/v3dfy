using V3dfy.Core.Execution;

namespace V3dfy.Core.Workflow;

public sealed class ConversionWorkflowState
{
    public const int SystemStatusToolsTabIndex = 0;

    public const int SystemStatusConversionTabIndex = 1;

    public int SelectedTabIndex { get; private set; }

    public int SelectedSystemStatusTabIndex { get; private set; } = SystemStatusToolsTabIndex;

    public bool HasCompletedAnalysis { get; private set; }

    public bool CanOpenRecommendedSetupTab => HasCompletedAnalysis;

    public bool CanOpenConversionPlanTab => HasCompletedAnalysis;

    public bool CanOpenSystemStatusConversionTab => HasCompletedAnalysis;

    public bool SetSelectedTabIndex(int value)
    {
        if (SelectedTabIndex == value)
        {
            return false;
        }

        SelectedTabIndex = value;
        return true;
    }

    public bool SetSelectedSystemStatusTabIndex(int value)
    {
        var normalizedValue = value == SystemStatusConversionTabIndex &&
            CanOpenSystemStatusConversionTab
                ? SystemStatusConversionTabIndex
                : SystemStatusToolsTabIndex;

        if (SelectedSystemStatusTabIndex == normalizedValue)
        {
            return false;
        }

        SelectedSystemStatusTabIndex = normalizedValue;
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
        SelectedSystemStatusTabIndex = value
            ? SystemStatusConversionTabIndex
            : SystemStatusToolsTabIndex;

        if (!value && SelectedTabIndex != 0)
        {
            SelectedTabIndex = 0;
            selectedTabIndexChanged = true;
        }

        return true;
    }

    public bool ShowConversionReadinessCard(ConversionExecutionStatus status) =>
        HasCompletedAnalysis &&
        status is
            ConversionExecutionStatus.NotStarted or
            ConversionExecutionStatus.Ready or
            ConversionExecutionStatus.Blocked;

    public static bool ShowConversionProgressCard(ConversionExecutionStatus status) =>
        status is
            ConversionExecutionStatus.Running or
            ConversionExecutionStatus.Canceling or
            ConversionExecutionStatus.Canceled or
            ConversionExecutionStatus.Failed or
            ConversionExecutionStatus.Completed;
}
