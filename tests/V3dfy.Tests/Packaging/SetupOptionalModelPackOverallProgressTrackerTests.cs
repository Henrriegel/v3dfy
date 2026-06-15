using V3dfy.SetupHelper;

namespace V3dfy.Tests.Packaging;

public sealed class SetupOptionalModelPackOverallProgressTrackerTests
{
    [Fact]
    public void Report_WithNoSelectedModelPacksPassesPayloadOverallProgressThrough()
    {
        var inner = new RecordingSetupProgress();
        var tracker = new SetupOptionalModelPackOverallProgressTracker(inner, []);
        var payloadCompleted = PayloadCompletedEvent();

        tracker.Report(payloadCompleted);

        var recorded = Assert.Single(inner.Events);
        Assert.Same(payloadCompleted, recorded);
        Assert.Equal(100, recorded.OverallPercent);
    }

    [Fact]
    public void Report_WithSelectedModelPackDoesNotCompleteGlobalProgressAfterPayload()
    {
        var inner = new RecordingSetupProgress();
        var tracker = CreateTracker(
            inner,
            new SetupOptionalModelPackProgressItem("small.zip", 100));

        tracker.Report(PayloadCompletedEvent());
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.DownloadingModelPack,
            "Downloading optional model pack.",
            "small.zip",
            50,
            100));
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.VerifyingModelPack,
            "Verifying optional model pack.",
            "small.zip",
            100,
            100));
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.ValidatingModelPack,
            "Validating optional model pack.",
            "small.zip"));
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.InstallingModelPack,
            "Installing optional model pack.",
            "small.zip",
            2,
            2));
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.Completed,
            "Installation completed successfully."));

        var payloadComplete = inner.Events[0];
        var downloadProgress = inner.Events[1];
        var verifyProgress = inner.Events[2];
        var validateProgress = inner.Events[3];
        var installProgress = inner.Events[4];
        var finalProgress = inner.Events[5];

        Assert.Equal(85, payloadComplete.OverallPercent);
        Assert.True(payloadComplete.OverallPercent < 100);
        Assert.InRange(downloadProgress.OverallPercent.GetValueOrDefault(), 85, 90);
        Assert.True(verifyProgress.OverallPercent > downloadProgress.OverallPercent);
        Assert.True(validateProgress.OverallPercent > verifyProgress.OverallPercent);
        Assert.True(installProgress.OverallPercent > validateProgress.OverallPercent);
        Assert.Equal(100, finalProgress.OverallPercent);
        AssertMonotonic(inner.Events);
    }

    [Fact]
    public void Report_WithMultipleSelectedModelPacksWeightsOptionalProgressByZipSize()
    {
        var inner = new RecordingSetupProgress();
        var tracker = CreateTracker(
            inner,
            new SetupOptionalModelPackProgressItem("small.zip", 100),
            new SetupOptionalModelPackProgressItem("large.zip", 900));

        tracker.Report(PayloadCompletedEvent());
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.DownloadingModelPack,
            "Downloaded small optional model pack.",
            "small.zip",
            100,
            100));
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.DownloadingModelPack,
            "Downloading large optional model pack.",
            "large.zip",
            450,
            900));
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.VerifyingModelPack,
            "Verified small optional model pack.",
            "small.zip",
            100,
            100));
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.VerifyingModelPack,
            "Verified large optional model pack.",
            "large.zip",
            900,
            900));
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.ValidatingModelPack,
            "Validating large optional model pack.",
            "large.zip"));
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.InstallingModelPack,
            "Installed large optional model pack.",
            "large.zip",
            1,
            1));
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.Completed,
            "Installation completed successfully."));

        var payloadPercent = inner.Events[0].OverallPercent.GetValueOrDefault();
        var smallCompletePercent = inner.Events[1].OverallPercent.GetValueOrDefault();
        var largeHalfPercent = inner.Events[2].OverallPercent.GetValueOrDefault();

        Assert.True(smallCompletePercent > payloadPercent);
        Assert.True(largeHalfPercent > smallCompletePercent);
        Assert.True(
            largeHalfPercent - smallCompletePercent > smallCompletePercent - payloadPercent,
            "The larger selected model pack should contribute more global progress.");
        Assert.Equal(100, inner.Events[^1].OverallPercent);
        AssertMonotonic(inner.Events);
    }

    [Fact]
    public void Report_OptionalFailureCanStillReachFinalCompletionAfterSummary()
    {
        var inner = new RecordingSetupProgress();
        var tracker = CreateTracker(
            inner,
            new SetupOptionalModelPackProgressItem("failed.zip", 100));

        tracker.Report(PayloadCompletedEvent());
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.DownloadingModelPack,
            "Downloading optional model pack.",
            "failed.zip",
            20,
            100));
        tracker.Report(new SetupProgressEvent(
            SetupProgressPhase.Completed,
            "Installation completed with optional model-pack warnings."));

        Assert.Equal(85, inner.Events[0].OverallPercent);
        Assert.True(inner.Events[1].OverallPercent > inner.Events[0].OverallPercent);
        Assert.Equal(100, inner.Events[^1].OverallPercent);
        AssertMonotonic(inner.Events);
    }

    [Fact]
    public void Report_DoesNotChangeCurrentPackageProgressFields()
    {
        var inner = new RecordingSetupProgress();
        var tracker = CreateTracker(
            inner,
            new SetupOptionalModelPackProgressItem("small.zip", 100));
        var currentProgress = new SetupProgressEvent(
            SetupProgressPhase.DownloadingModelPack,
            "Downloading optional model pack.",
            "small.zip",
            25,
            100);

        tracker.Report(currentProgress);

        var recorded = Assert.Single(inner.Events);
        Assert.Equal(currentProgress.Phase, recorded.Phase);
        Assert.Equal(currentProgress.CurrentFile, recorded.CurrentFile);
        Assert.Equal(currentProgress.CurrentBytes, recorded.CurrentBytes);
        Assert.Equal(currentProgress.TotalBytes, recorded.TotalBytes);
        Assert.Equal(25, recorded.Percent);
        Assert.NotNull(recorded.OverallPercent);
    }

    private static SetupOptionalModelPackOverallProgressTracker CreateTracker(
        ISetupProgress inner,
        params SetupOptionalModelPackProgressItem[] items) =>
        new(inner, items);

    private static SetupProgressEvent PayloadCompletedEvent() =>
        new(
            SetupProgressPhase.Completed,
            "Payload installation completed successfully.",
            OverallCompletedUnits: 10000,
            OverallTotalUnits: 10000,
            OverallMessage: "Installation complete");

    private static void AssertMonotonic(IReadOnlyList<SetupProgressEvent> events)
    {
        var overallEvents = events
            .Where(static e => e.OverallPercent is not null)
            .ToArray();
        for (var index = 1; index < overallEvents.Length; index++)
        {
            Assert.True(
                overallEvents[index].OverallPercent >= overallEvents[index - 1].OverallPercent,
                $"Overall progress decreased from {overallEvents[index - 1].OverallPercent} to {overallEvents[index].OverallPercent} at event {index}.");
        }
    }

    private sealed class RecordingSetupProgress : ISetupProgress
    {
        public List<SetupProgressEvent> Events { get; } = [];

        public void Report(SetupProgressEvent progress) => Events.Add(progress);
    }
}
