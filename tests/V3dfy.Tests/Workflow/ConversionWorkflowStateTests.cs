using V3dfy.Core.Execution;
using V3dfy.Core.Workflow;

namespace V3dfy.Tests.Workflow;

public sealed class ConversionWorkflowStateTests
{
    [Fact]
    public void Defaults_StartOnSourceStepWithLaterStepsDisabled()
    {
        var state = new ConversionWorkflowState();

        Assert.Equal(ConversionWorkflowState.SourceAndAnalysisStepIndex, state.SelectedStepIndex);
        Assert.True(state.CanOpenSourceAndAnalysisStep);
        Assert.False(state.CanOpenThreeDSetupStep);
        Assert.False(state.CanOpenConversionPlanStep);
        Assert.False(state.CanGoBack);
        Assert.False(state.CanGoNext);
    }

    [Fact]
    public void SetHasCompletedAnalysis_True_Enables3dSetupButNotPlan()
    {
        var state = new ConversionWorkflowState();

        var changed = state.SetHasCompletedAnalysis(true, out var selectedStepChanged);

        Assert.True(changed);
        Assert.False(selectedStepChanged);
        Assert.True(state.CanOpenThreeDSetupStep);
        Assert.False(state.CanOpenConversionPlanStep);
        Assert.True(state.CanGoNext);
    }

    [Fact]
    public void SetCanOpenConversionPlanStep_RequiresCompletedAnalysis()
    {
        var state = new ConversionWorkflowState();

        state.SetCanOpenConversionPlanStep(true, out _);

        Assert.False(state.CanOpenConversionPlanStep);

        state.SetHasCompletedAnalysis(true, out _);
        state.SetCanOpenConversionPlanStep(true, out var selectedStepChanged);

        Assert.False(selectedStepChanged);
        Assert.True(state.CanOpenConversionPlanStep);
    }

    [Fact]
    public void MoveNextAndBack_RespectAvailableSteps()
    {
        var state = new ConversionWorkflowState();

        Assert.False(state.MoveNext());

        state.SetHasCompletedAnalysis(true, out _);
        Assert.True(state.MoveNext());
        Assert.Equal(ConversionWorkflowState.ThreeDSetupStepIndex, state.SelectedStepIndex);
        Assert.True(state.MoveBack());
        Assert.Equal(ConversionWorkflowState.SourceAndAnalysisStepIndex, state.SelectedStepIndex);
    }

    [Fact]
    public void SetSelectedStepIndex_DoesNotJumpForwardToUnavailableStep()
    {
        var state = new ConversionWorkflowState();

        var changed = state.SetSelectedStepIndex(ConversionWorkflowState.ConversionPlanStepIndex);

        Assert.False(changed);
        Assert.Equal(ConversionWorkflowState.SourceAndAnalysisStepIndex, state.SelectedStepIndex);
    }

    [Fact]
    public void ClearingAnalysis_ResetsSelectedStepToSource()
    {
        var state = new ConversionWorkflowState();
        state.SetHasCompletedAnalysis(true, out _);
        state.SetCanOpenConversionPlanStep(true, out _);
        state.SetSelectedStepIndex(ConversionWorkflowState.ConversionPlanStepIndex);

        var changed = state.SetHasCompletedAnalysis(false, out var selectedStepChanged);

        Assert.True(changed);
        Assert.True(selectedStepChanged);
        Assert.Equal(ConversionWorkflowState.SourceAndAnalysisStepIndex, state.SelectedStepIndex);
        Assert.False(state.CanOpenThreeDSetupStep);
        Assert.False(state.CanOpenConversionPlanStep);
    }

    [Theory]
    [InlineData(ConversionExecutionStatus.NotStarted, true)]
    [InlineData(ConversionExecutionStatus.Ready, true)]
    [InlineData(ConversionExecutionStatus.Blocked, true)]
    [InlineData(ConversionExecutionStatus.Running, false)]
    [InlineData(ConversionExecutionStatus.Completed, false)]
    public void ShowConversionReadinessCard_RequiresCompletedAnalysisAndIdleStatus(
        ConversionExecutionStatus status,
        bool expected)
    {
        var state = new ConversionWorkflowState();
        state.SetHasCompletedAnalysis(true, out _);

        var visible = state.ShowConversionReadinessCard(status);

        Assert.Equal(expected, visible);
    }

    [Fact]
    public void ShowConversionReadinessCard_WithoutAnalysis_ReturnsFalse()
    {
        var state = new ConversionWorkflowState();

        var visible = state.ShowConversionReadinessCard(ConversionExecutionStatus.NotStarted);

        Assert.False(visible);
    }

    [Theory]
    [InlineData(ConversionExecutionStatus.NotStarted, false)]
    [InlineData(ConversionExecutionStatus.Ready, false)]
    [InlineData(ConversionExecutionStatus.Blocked, false)]
    [InlineData(ConversionExecutionStatus.Running, true)]
    [InlineData(ConversionExecutionStatus.Canceling, true)]
    [InlineData(ConversionExecutionStatus.Canceled, true)]
    [InlineData(ConversionExecutionStatus.Failed, true)]
    [InlineData(ConversionExecutionStatus.Completed, true)]
    public void ShowConversionProgressCard_MatchesExecutionStatuses(
        ConversionExecutionStatus status,
        bool expected)
    {
        var visible = ConversionWorkflowState.ShowConversionProgressCard(status);

        Assert.Equal(expected, visible);
    }
}
