using V3dfy.Core.Execution;
using V3dfy.Core.Workflow;

namespace V3dfy.Tests.Workflow;

public sealed class ConversionWorkflowStateTests
{
    [Fact]
    public void Defaults_StartOnFirstTabWithAnalysisTabsDisabled()
    {
        var state = new ConversionWorkflowState();

        Assert.Equal(0, state.SelectedTabIndex);
        Assert.False(state.HasCompletedAnalysis);
        Assert.False(state.CanOpenRecommendedSetupTab);
        Assert.False(state.CanOpenConversionPlanTab);
    }

    [Fact]
    public void SetHasCompletedAnalysis_True_EnablesAnalysisTabs()
    {
        var state = new ConversionWorkflowState();

        var changed = state.SetHasCompletedAnalysis(
            true,
            out var selectedTabIndexChanged);

        Assert.True(changed);
        Assert.False(selectedTabIndexChanged);
        Assert.True(state.HasCompletedAnalysis);
        Assert.True(state.CanOpenRecommendedSetupTab);
        Assert.True(state.CanOpenConversionPlanTab);
    }

    [Fact]
    public void SetHasCompletedAnalysis_False_ResetsSelectedTab()
    {
        var state = new ConversionWorkflowState();
        state.SetHasCompletedAnalysis(true, out _);
        state.SetSelectedTabIndex(2);

        var changed = state.SetHasCompletedAnalysis(
            false,
            out var selectedTabIndexChanged);

        Assert.True(changed);
        Assert.True(selectedTabIndexChanged);
        Assert.False(state.HasCompletedAnalysis);
        Assert.Equal(0, state.SelectedTabIndex);
    }

    [Fact]
    public void SetSelectedTabIndex_SameValue_ReturnsFalse()
    {
        var state = new ConversionWorkflowState();

        var changed = state.SetSelectedTabIndex(0);

        Assert.False(changed);
        Assert.Equal(0, state.SelectedTabIndex);
    }

    [Theory]
    [InlineData(ConversionExecutionStatus.NotStarted, true)]
    [InlineData(ConversionExecutionStatus.Ready, true)]
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
