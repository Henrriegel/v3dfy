using V3dfy.Core.Execution;

namespace V3dfy.Core.Workflow;

public sealed class ConversionWorkflowState
{
    public const int SourceAndAnalysisStepIndex = 0;

    public const int ThreeDSetupStepIndex = 1;

    public const int ConversionPlanStepIndex = 2;

    public const int SystemStatusToolsTabIndex = 0;

    public const int SystemStatusConversionTabIndex = 1;

    public int SelectedStepIndex { get; private set; } = SourceAndAnalysisStepIndex;

    public int SelectedTabIndex => SelectedStepIndex;

    public int SelectedSystemStatusTabIndex { get; private set; } = SystemStatusToolsTabIndex;

    public bool HasCompletedAnalysis { get; private set; }

    public bool CanOpenSourceAndAnalysisStep => true;

    public bool CanOpenThreeDSetupStep => HasCompletedAnalysis;

    public bool CanOpenConversionPlanStep { get; private set; }

    public bool CanOpenRecommendedSetupTab => CanOpenThreeDSetupStep;

    public bool CanOpenConversionPlanTab => CanOpenConversionPlanStep;

    public bool CanOpenSystemStatusConversionTab => HasCompletedAnalysis;

    public bool CanGoBack => SelectedStepIndex > SourceAndAnalysisStepIndex;

    public bool CanGoNext => SelectedStepIndex < ConversionPlanStepIndex &&
        CanOpenStep(SelectedStepIndex + 1);

    public bool IsSourceAndAnalysisStepSelected => SelectedStepIndex == SourceAndAnalysisStepIndex;

    public bool IsThreeDSetupStepSelected => SelectedStepIndex == ThreeDSetupStepIndex;

    public bool IsConversionPlanStepSelected => SelectedStepIndex == ConversionPlanStepIndex;

    public bool SetSelectedTabIndex(int value)
    {
        return SetSelectedStepIndex(value);
    }

    public bool SetSelectedStepIndex(int value)
    {
        if (value < SourceAndAnalysisStepIndex ||
            value > ConversionPlanStepIndex ||
            !CanOpenStep(value) ||
            SelectedStepIndex == value)
        {
            return false;
        }

        SelectedStepIndex = value;
        return true;
    }

    public bool MoveBack()
    {
        if (!CanGoBack)
        {
            return false;
        }

        SelectedStepIndex--;
        return true;
    }

    public bool MoveNext()
    {
        if (!CanGoNext)
        {
            return false;
        }

        SelectedStepIndex++;
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
        if (!value)
        {
            CanOpenConversionPlanStep = false;
        }

        SelectedSystemStatusTabIndex = value
            ? SystemStatusConversionTabIndex
            : SystemStatusToolsTabIndex;

        if (!CanOpenStep(SelectedStepIndex))
        {
            SelectedStepIndex = SourceAndAnalysisStepIndex;
            selectedTabIndexChanged = true;
        }

        return true;
    }

    public bool SetCanOpenConversionPlanStep(
        bool value,
        out bool selectedStepIndexChanged)
    {
        selectedStepIndexChanged = false;
        var normalizedValue = value && HasCompletedAnalysis;
        if (CanOpenConversionPlanStep == normalizedValue)
        {
            return false;
        }

        CanOpenConversionPlanStep = normalizedValue;
        if (!normalizedValue && SelectedStepIndex == ConversionPlanStepIndex)
        {
            SelectedStepIndex = HasCompletedAnalysis
                ? ThreeDSetupStepIndex
                : SourceAndAnalysisStepIndex;
            selectedStepIndexChanged = true;
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

    private bool CanOpenStep(int stepIndex) => stepIndex switch
    {
        SourceAndAnalysisStepIndex => true,
        ThreeDSetupStepIndex => CanOpenThreeDSetupStep,
        ConversionPlanStepIndex => CanOpenConversionPlanStep,
        _ => false,
    };
}
