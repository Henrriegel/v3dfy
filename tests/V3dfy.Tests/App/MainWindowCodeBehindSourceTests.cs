namespace V3dfy.Tests.App;

public sealed class MainWindowCodeBehindSourceTests
{
    [Fact]
    public void PreviewGenerationLog_AutoScrollsUnlessUserScrolledAway()
    {
        var source = ReadMainWindowCodeBehindSource();

        Assert.Contains("_previewLogShouldAutoScroll", source);
        Assert.Contains("NotifyCollectionChangedAction.Reset", source);
        Assert.Contains("OnPreviewLogScrollChanged", source);
        Assert.Contains("IsPreviewLogAtBottom()", source);
        Assert.Contains("FindNamedControl<WpfListBox>(\"PreviewGenerationLogList\")", source);
        Assert.Contains("previewLogList.ScrollIntoView", source);
        Assert.Contains("ScrollToBottom()", source);
        Assert.Contains("TryRunPreviewViewAction", source);
    }

    [Fact]
    public void PreviewReadyModal_UsesLightweightMediaElementPlaybackHandlers()
    {
        var source = ReadMainWindowCodeBehindSource();

        Assert.Contains("OnPreviewMediaOpened", source);
        Assert.Contains("OnPreviewMediaEnded", source);
        Assert.Contains("OnPreviewMediaFailed", source);
        Assert.Contains("OnPreviewPlayPauseClicked", source);
        Assert.DoesNotContain("OnPreviewReplayClicked", source);
        Assert.Contains("OnPreviewTimelineValueChanged", source);
        Assert.Contains("OnPreviewVolumeValueChanged", source);
        Assert.Contains("OnPreviewMuteChanged", source);
        Assert.Contains("DispatcherTimer", source);
        Assert.Contains("PreviewFirstFrameNudge", source);
        Assert.Contains("media.ScrubbingEnabled = true;", source);
        Assert.Contains("media.Pause();", source);
        Assert.Contains("AutomationProperties.SetName(playPauseButton, label);", source);
        Assert.Contains("AutomationProperties.SetName(muteToggle, label);", source);
        Assert.DoesNotContain("VLC", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LibVLC", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImageParallaxGeneratedVideoPreview_UsesLightweightMediaElementPlaybackHandlers()
    {
        var source = ReadMainWindowCodeBehindSource();

        Assert.Contains("OnImageParallaxPreviewMediaOpened", source);
        Assert.Contains("OnImageParallaxPreviewMediaEnded", source);
        Assert.Contains("OnImageParallaxPreviewMediaFailed", source);
        Assert.Contains("OnImageParallaxPreviewPlayPauseClicked", source);
        Assert.Contains("OnImageParallaxPreviewTimelineValueChanged", source);
        Assert.Contains("OnImageParallaxPreviewVolumeValueChanged", source);
        Assert.Contains("OnImageParallaxPreviewVolumeSliderPreviewMouseLeftButtonDown", source);
        Assert.Contains("OnImageParallaxPreviewMuteChanged", source);
        Assert.Contains("OnImageParallaxVideoPlayerVisibilityChanged", source);
        Assert.Contains("_imageParallaxPlaybackTimer", source);
        Assert.Contains("_isUpdatingImageParallaxVolume", source);
        Assert.Contains("_isUpdatingImageParallaxMute", source);
        Assert.Contains("_lastImageParallaxVolume", source);
        Assert.Contains("ImageParallaxVerticalPlayerPrefix", source);
        Assert.Contains("ImageParallaxWidePlayerPrefix", source);
        var xaml = ReadMainWindowXamlSource();
        Assert.Contains("ImageParallaxVerticalPreviewMediaElement", xaml);
        Assert.Contains("ImageParallaxWidePreviewMediaElement", xaml);
        Assert.Contains("MinHeight=\"220\"", xaml);
        Assert.Contains("ImageParallaxVerticalPreviewVolumeSlider", xaml);
        Assert.Contains("ImageParallaxVerticalPreviewMuteToggleButton", xaml);
        Assert.Contains("ImageParallaxWidePreviewVolumeSlider", xaml);
        Assert.Contains("ImageParallaxWidePreviewMuteToggleButton", xaml);
        Assert.DoesNotContain("ImageParallaxVideoPlayerMinHeight", xaml);
        Assert.DoesNotContain("ToggleImageParallaxVideoMaximizeCommand", xaml);
        Assert.DoesNotContain("ImageParallaxVideoMaximizeGlyphText", xaml);
        Assert.DoesNotContain("ImageParallaxVideoMaximizeButton", xaml);
        Assert.DoesNotContain("Content=\"{Binding ImageParallaxVideoMaximizeButtonText}\"", xaml);
        Assert.DoesNotContain("VLC", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LibVLC", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImageParallaxVolumeMuteHandlers_SynchronizeBothPreviewLayouts()
    {
        var source = ReadMainWindowCodeBehindSource();
        var volumeMethod = ExtractSourceRange(
            source,
            "private void OnImageParallaxPreviewVolumeValueChanged",
            "private void OnImageParallaxPreviewVolumeSliderPreviewMouseLeftButtonDown");
        var trackClickMethod = ExtractSourceRange(
            source,
            "private void OnImageParallaxPreviewVolumeSliderPreviewMouseLeftButtonDown",
            "private void ApplyImageParallaxVolumeValue");
        var mutedStateMethod = ExtractSourceRange(
            source,
            "private void SetImageParallaxMutedState",
            "private void SetImageParallaxVolumeSliderValue");
        var muteButtonMethod = ExtractSourceRange(
            source,
            "private void UpdateImageParallaxMuteButton",
            "private void UpdateImageParallaxTimeText");

        Assert.Contains("ResolveImageParallaxPlayerPrefix(sender)", volumeMethod);
        Assert.Contains("ApplyImageParallaxVolumeValue(prefix, e.NewValue);", volumeMethod);
        Assert.Contains("sender is not WpfSlider slider", trackClickMethod);
        Assert.Contains("FindVisualParent<WpfThumb>(source) is not null", trackClickMethod);
        Assert.Contains("SetImageParallaxVolumeSliderValue(prefix, clampedValue);", trackClickMethod);
        Assert.Contains("ApplyImageParallaxVolumeValue(prefix, clampedValue);", trackClickMethod);
        Assert.Contains("media.IsMuted = true;", mutedStateMethod);
        Assert.Contains("media.Volume = 0;", mutedStateMethod);
        Assert.Contains("SetImageParallaxVolumeSliderValue(prefix, 0);", mutedStateMethod);
        Assert.Contains("media.IsMuted = false;", mutedStateMethod);
        Assert.Contains("media.Volume = restoredVolume;", mutedStateMethod);
        Assert.Contains("SetImageParallaxVolumeSliderValue(prefix, restoredVolume);", mutedStateMethod);
        Assert.Contains("PreviewUnmuteText", muteButtonMethod);
        Assert.Contains("PreviewMuteText", muteButtonMethod);
        Assert.Contains("PreviewMuteGlyph", muteButtonMethod);
        Assert.Contains("PreviewVolumeGlyph", muteButtonMethod);
    }

    [Fact]
    public void PreviewHandlers_GuardAgainstUnloadedNamedControls()
    {
        var source = ReadMainWindowCodeBehindSource();

        Assert.Contains("FindNamedControl<WpfMediaElement>(\"PreviewMediaElement\")", source);
        Assert.Contains("FindNamedControl<WpfSlider>(\"PreviewTimelineSlider\")", source);
        Assert.Contains("FindNamedControl<WpfButton>(\"PreviewPlayPauseButton\")", source);
        Assert.Contains("FindNamedControl<WpfTextBlock>(\"PreviewPlaybackStatusText\")", source);
        Assert.Contains("FindNamedControl<WpfToggleButton>(\"PreviewMuteToggleButton\")", source);
        Assert.Contains("AppErrorLogService.LogRecoverableException(operation, exception)", source);
        Assert.Contains("FindVisualChild<T>(DependencyObject? parent)", source);
        Assert.Contains("FindVisualParent<T>(DependencyObject? child)", source);
    }

    [Fact]
    public void PreviewPlayback_DoesNotAutoplayAndHandlesSeekAndEndedState()
    {
        var source = ReadMainWindowCodeBehindSource();
        var mediaOpenedMethod = ExtractSourceRange(
            source,
            "private void OnPreviewMediaOpened",
            "private void OnPreviewMediaEnded");
        var playPauseMethod = ExtractSourceRange(
            source,
            "private void OnPreviewPlayPauseClicked",
            "private void OnPreviewTimelinePreviewMouseDown");
        var playHelperMethod = ExtractSourceRange(
            source,
            "private void PlayPreviewMedia",
            "private void PausePreviewMedia");
        var pauseHelperMethod = ExtractSourceRange(
            source,
            "private void PausePreviewMedia",
            "private void InitializePreviewFirstFrame");
        var firstFrameMethod = ExtractSourceRange(
            source,
            "private void InitializePreviewFirstFrame",
            "private void SyncPreviewVolumeControls");
        var seekMethod = ExtractSourceRange(
            source,
            "private void SeekPreviewMedia",
            "private void UpdatePreviewTimeText");
        var timelineValueMethod = ExtractSourceRange(
            source,
            "private void OnPreviewTimelineValueChanged",
            "private void OnPreviewVolumeValueChanged");

        Assert.Contains("media.Pause();", mediaOpenedMethod);
        Assert.DoesNotContain("media.Play();", mediaOpenedMethod);
        Assert.Contains("Dispatcher.BeginInvoke", firstFrameMethod);
        Assert.Contains("media.Position = PreviewFirstFrameNudge;", firstFrameMethod);
        Assert.DoesNotContain("media.Play();", firstFrameMethod);
        Assert.Contains("if (_isPreviewMediaPlaying)", playPauseMethod);
        Assert.Contains("PausePreviewMedia(media);", playPauseMethod);
        Assert.Contains("PlayPreviewMedia(media);", playPauseMethod);
        Assert.Contains("if (_isPreviewMediaEnded)", playHelperMethod);
        Assert.Contains("media.Position = TimeSpan.Zero;", playHelperMethod);
        Assert.Contains("media.Play();", playHelperMethod);
        Assert.Contains("media.Pause();", pauseHelperMethod);
        Assert.Contains("_previewPlaybackTimer.Stop();", pauseHelperMethod);
        Assert.Contains("SeekPreviewMedia(media, timeline.Value);", timelineValueMethod);
        Assert.DoesNotContain("media.Play();", timelineValueMethod);
        Assert.DoesNotContain("_previewPlaybackTimer.Start();", timelineValueMethod);
        Assert.Contains("_isPreviewMediaEnded = false;", seekMethod);
        Assert.DoesNotContain("media.Play();", seekMethod);
    }

    [Fact]
    public void PreviewVolumeMuteHandlers_SynchronizeSliderToggleAndMediaState()
    {
        var source = ReadMainWindowCodeBehindSource();
        var volumeMethod = ExtractSourceRange(
            source,
            "private void OnPreviewVolumeValueChanged",
            "private void OnPreviewMuteChanged");
        var muteMethod = ExtractSourceRange(
            source,
            "private void OnPreviewMuteChanged",
            "private void OnPreviewPlaybackTimerTick");
        var mutedStateMethod = ExtractSourceRange(
            source,
            "private void SetPreviewMutedState",
            "private void SetPreviewVolumeSliderValue");
        var muteButtonMethod = ExtractSourceRange(
            source,
            "private void UpdatePreviewMuteButton",
            "private void SeekPreviewMedia");

        Assert.Contains("_isUpdatingPreviewVolume", source);
        Assert.Contains("_isUpdatingPreviewMute", source);
        Assert.Contains("_lastPreviewVolume", source);
        Assert.Contains("PreviewDefaultVolume", source);
        Assert.Contains("SetPreviewMutedState(", volumeMethod);
        Assert.Contains("media.IsMuted = false;", volumeMethod);
        Assert.Contains("SetPreviewMuteToggleChecked(false);", volumeMethod);
        Assert.Contains("SetPreviewMutedState(", muteMethod);
        Assert.Contains("media.IsMuted = true;", mutedStateMethod);
        Assert.Contains("media.Volume = 0;", mutedStateMethod);
        Assert.Contains("SetPreviewVolumeSliderValue(0);", mutedStateMethod);
        Assert.Contains("media.IsMuted = false;", mutedStateMethod);
        Assert.Contains("media.Volume = restoredVolume;", mutedStateMethod);
        Assert.Contains("SetPreviewVolumeSliderValue(restoredVolume);", mutedStateMethod);
        Assert.Contains("PreviewUnmuteText", muteButtonMethod);
        Assert.Contains("PreviewMuteText", muteButtonMethod);
        Assert.Contains("muteToggle.Content = isMuted ? PreviewMuteGlyph : PreviewVolumeGlyph;", muteButtonMethod);
    }

    [Fact]
    public void PreviewVolumeTrackClick_MapsMouseXToClampedVolumeWithoutTargetingTimeline()
    {
        var source = ReadMainWindowCodeBehindSource();
        var handler = ExtractSourceRange(
            source,
            "private void OnPreviewVolumeSliderPreviewMouseLeftButtonDown",
            "private void ApplyPreviewVolumeValue");
        var applyVolumeMethod = ExtractSourceRange(
            source,
            "private void ApplyPreviewVolumeValue",
            "private void OnPreviewMuteChanged");

        Assert.Contains("sender is not WpfSlider slider", handler);
        Assert.Contains("slider.ActualWidth <= 0", handler);
        Assert.Contains("FindVisualParent<WpfThumb>(source) is not null", handler);
        Assert.Contains("e.GetPosition(slider).X", handler);
        Assert.Contains("clickedX / slider.ActualWidth", handler);
        Assert.Contains("slider.Minimum + ((slider.Maximum - slider.Minimum) * normalizedClick)", handler);
        Assert.Contains("Math.Clamp(clickedValue, slider.Minimum, slider.Maximum)", handler);
        Assert.Contains("SetPreviewVolumeSliderValue(clampedValue);", handler);
        Assert.Contains("ApplyPreviewVolumeValue(clampedValue);", handler);
        Assert.Contains("e.Handled = true;", handler);
        Assert.DoesNotContain("PreviewTimelineSlider", handler);
        Assert.Contains("SetPreviewMutedState(", applyVolumeMethod);
        Assert.Contains("media.IsMuted = false;", applyVolumeMethod);
        Assert.Contains("SetPreviewMuteToggleChecked(false);", applyVolumeMethod);
    }

    private static string ReadMainWindowCodeBehindSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "V3dfy.App",
                "MainWindow.xaml.cs");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate src/V3dfy.App/MainWindow.xaml.cs.");
    }

    private static string ReadMainWindowXamlSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "V3dfy.App",
                "MainWindow.xaml");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate src/V3dfy.App/MainWindow.xaml.");
    }

    private static string ExtractSourceRange(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);

        return source[start..end];
    }
}
