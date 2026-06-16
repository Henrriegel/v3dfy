using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;
using V3dfy.App.Services;
using V3dfy.App.ViewModels;
using WpfButton = System.Windows.Controls.Button;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfMediaElement = System.Windows.Controls.MediaElement;
using WpfScrollChangedEventArgs = System.Windows.Controls.ScrollChangedEventArgs;
using WpfScrollViewer = System.Windows.Controls.ScrollViewer;
using WpfSlider = System.Windows.Controls.Slider;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfThumb = System.Windows.Controls.Primitives.Thumb;
using WpfToggleButton = System.Windows.Controls.Primitives.ToggleButton;

namespace V3dfy.App;

public partial class MainWindow : Window
{
    private static readonly TimeSpan PreviewFirstFrameNudge = TimeSpan.FromMilliseconds(33);
    private static readonly Duration SidebarWidthAnimationDuration = new(TimeSpan.FromMilliseconds(150));
    private static readonly Thickness NormalChromeContentMargin = new(0);
    private const double PreviewDefaultVolume = 0.7;
    private const double PreviewMutedVolumeThreshold = 0.001;
    private const string PreviewPlayGlyph = "\uE768";
    private const string PreviewPauseGlyph = "\uE769";
    private const string PreviewVolumeGlyph = "\uE767";
    private const string PreviewMuteGlyph = "\uE74F";
    private const string ImageParallaxVerticalPlayerPrefix = "ImageParallaxVertical";
    private const string ImageParallaxWidePlayerPrefix = "ImageParallaxWide";
    private readonly DispatcherTimer _previewPlaybackTimer;
    private readonly DispatcherTimer _imageParallaxPlaybackTimer;
    private WpfScrollViewer? _previewLogScrollViewer;
    private bool _previewLogShouldAutoScroll = true;
    private bool _isPreviewMediaPlaying;
    private bool _isPreviewMediaEnded;
    private bool _isUpdatingPreviewTimeline;
    private bool _isUserDraggingPreviewTimeline;
    private bool _wasPreviewMediaPlayingBeforeTimelineDrag;
    private bool _isUpdatingPreviewVolume;
    private bool _isUpdatingPreviewMute;
    private double _lastPreviewVolume = PreviewDefaultVolume;
    private string _activeImageParallaxPlayerPrefix = ImageParallaxWidePlayerPrefix;
    private bool _isImageParallaxMediaPlaying;
    private bool _isImageParallaxMediaEnded;
    private bool _isUpdatingImageParallaxTimeline;
    private bool _isUserDraggingImageParallaxTimeline;
    private bool _wasImageParallaxMediaPlayingBeforeTimelineDrag;
    private bool _isUpdatingImageParallaxVolume;
    private bool _isUpdatingImageParallaxMute;
    private double _lastImageParallaxVolume = PreviewDefaultVolume;

    public MainWindow()
    {
        InitializeComponent();
        UpdateChromeContentMargin();
        _previewPlaybackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _previewPlaybackTimer.Tick += OnPreviewPlaybackTimerTick;
        _imageParallaxPlaybackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _imageParallaxPlaybackTimer.Tick += OnImageParallaxPlaybackTimerTick;
        DataContext = new MainWindowViewModel();
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplySidebarWidth(viewModel.SidebarTargetWidth);
            viewModel.ConversionLogs.CollectionChanged += OnConversionLogsChanged;
            viewModel.PreviewGenerationLogs.CollectionChanged += OnPreviewGenerationLogsChanged;
        }
    }

    private void OnMinimizeWindowButtonClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreWindowButtonClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseWindowButtonClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        UpdateChromeContentMargin();
    }

    private void UpdateChromeContentMargin()
    {
        AppChromeRoot.Margin = WindowState == WindowState.Maximized
            ? SystemParameters.WindowResizeBorderThickness
            : NormalChromeContentMargin;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SidebarTargetWidth) &&
            sender is MainWindowViewModel viewModel)
        {
            AnimateSidebarWidth(viewModel.SidebarTargetWidth);
        }
    }

    private void OnSidebarMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        GetViewModel()?.ExpandSidebarForHover();
    }

    private void OnSidebarMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        GetViewModel()?.CollapseSidebarAfterHover();
    }

    private void ApplySidebarWidth(double targetWidth)
    {
        SidebarColumn.Width = new GridLength(targetWidth);
    }

    private void AnimateSidebarWidth(double targetWidth)
    {
        TryRunPreviewViewAction(
            "Sidebar width animation",
            () =>
            {
                var currentWidth = SidebarColumn.ActualWidth > 0
                    ? SidebarColumn.ActualWidth
                    : SidebarColumn.Width.Value;
                var animation = new GridLengthAnimation
                {
                    From = new GridLength(currentWidth),
                    To = new GridLength(targetWidth),
                    Duration = SidebarWidthAnimationDuration,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                };

                SidebarColumn.BeginAnimation(
                    System.Windows.Controls.ColumnDefinition.WidthProperty,
                    animation,
                    HandoffBehavior.SnapshotAndReplace);
            });
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            !viewModel.IsConversionRunning &&
            e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] { Length: 1 } files)
        {
            if (viewModel.IsImageConversionSectionSelected)
            {
                viewModel.SelectDroppedImage(files[0]);
                return;
            }

            viewModel.SelectDroppedVideo(files[0]);
        }
    }

    private void OnConversionLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { IsConversionRunning: true } viewModel ||
            viewModel.ConversionLogs.Count == 0)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() => TryRunPreviewViewAction(
            "Conversion log auto-scroll",
            () =>
            {
                var conversionLogList = FindNamedControl<WpfListBox>("ConversionLiveLogList");
                if (conversionLogList is not null && viewModel.ConversionLogs.Count > 0)
                {
                    conversionLogList.ScrollIntoView(viewModel.ConversionLogs[^1]);
                }
            })));
    }

    private void OnPreviewGenerationLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Reset ||
            viewModel.PreviewGenerationLogs.Count == 0)
        {
            _previewLogShouldAutoScroll = true;
            return;
        }

        if (!viewModel.IsPreviewGenerating)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() => TryRunPreviewViewAction(
            "Preview log auto-scroll",
            () =>
            {
                var previewLogList = FindNamedControl<WpfListBox>("PreviewGenerationLogList");
                if (previewLogList is null || viewModel.PreviewGenerationLogs.Count == 0)
                {
                    return;
                }

                EnsurePreviewLogScrollViewer(previewLogList);
                if (_previewLogShouldAutoScroll)
                {
                    previewLogList.ScrollIntoView(viewModel.PreviewGenerationLogs[^1]);
                    _previewLogScrollViewer?.ScrollToBottom();
                }
            })));
    }

    private void EnsurePreviewLogScrollViewer(WpfListBox previewLogList)
    {
        if (_previewLogScrollViewer is not null)
        {
            return;
        }

        previewLogList.ApplyTemplate();
        _previewLogScrollViewer = FindVisualChild<WpfScrollViewer>(previewLogList);
        if (_previewLogScrollViewer is not null)
        {
            _previewLogScrollViewer.ScrollChanged += OnPreviewLogScrollChanged;
        }
    }

    private void OnPreviewLogScrollChanged(object sender, WpfScrollChangedEventArgs e)
    {
        TryRunPreviewViewAction(
            "Preview log scroll tracking",
            () =>
            {
                if (e.ExtentHeightChange == 0 && Math.Abs(e.VerticalChange) > 0)
                {
                    _previewLogShouldAutoScroll = IsPreviewLogAtBottom();
                }
            });
    }

    private bool IsPreviewLogAtBottom()
    {
        if (_previewLogScrollViewer is null)
        {
            return true;
        }

        return _previewLogScrollViewer.ScrollableHeight - _previewLogScrollViewer.VerticalOffset <= 2;
    }

    private void OnImageParallaxPreviewMediaOpened(object sender, RoutedEventArgs e)
    {
        TryRunPreviewViewAction(
            "Image parallax media opened",
            () =>
            {
                var prefix = ResolveImageParallaxPlayerPrefix(sender);
                _activeImageParallaxPlayerPrefix = prefix;
                _imageParallaxPlaybackTimer.Stop();
                _isImageParallaxMediaPlaying = false;
                _isImageParallaxMediaEnded = false;
                var media = sender as WpfMediaElement ?? FindImageParallaxMedia(prefix);
                var timeline = FindImageParallaxTimeline(prefix);
                TimeSpan? duration = null;
                if (media is not null)
                {
                    media.ScrubbingEnabled = true;
                    media.Pause();
                    media.Position = TimeSpan.Zero;
                    duration = GetPreviewMediaDuration(media);
                }

                if (timeline is not null)
                {
                    _isUpdatingImageParallaxTimeline = true;
                    try
                    {
                        timeline.Maximum = duration?.TotalSeconds ?? 0;
                        timeline.Value = 0;
                    }
                    finally
                    {
                        _isUpdatingImageParallaxTimeline = false;
                    }
                }

                UpdateImageParallaxPlaybackButton(prefix);
                UpdateImageParallaxTimeText(prefix);
                SyncImageParallaxVolumeControls(prefix);
                InitializeImageParallaxFirstFrame(media, duration, prefix);
            });
    }

    private void OnImageParallaxPreviewMediaEnded(object sender, RoutedEventArgs e)
    {
        TryRunPreviewViewAction(
            "Image parallax media ended",
            () =>
            {
                var prefix = ResolveImageParallaxPlayerPrefix(sender);
                _activeImageParallaxPlayerPrefix = prefix;
                _isImageParallaxMediaPlaying = false;
                _isImageParallaxMediaEnded = true;
                _imageParallaxPlaybackTimer.Stop();
                if (GetPreviewMediaDuration(FindImageParallaxMedia(prefix)) is { } duration)
                {
                    SetImageParallaxTimelineValue(prefix, duration.TotalSeconds);
                }

                UpdateImageParallaxPlaybackButton(prefix);
                UpdateImageParallaxTimeText(prefix);
            });
    }

    private void OnImageParallaxPreviewMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        TryRunPreviewViewAction(
            "Image parallax media failed",
            () =>
            {
                _isImageParallaxMediaPlaying = false;
                _isImageParallaxMediaEnded = false;
                _imageParallaxPlaybackTimer.Stop();
                AppErrorLogService.LogRecoverableException("Image parallax media failed", e.ErrorException);
                UpdateImageParallaxPlaybackButton(ResolveImageParallaxPlayerPrefix(sender));
            });
    }

    private void OnImageParallaxVideoPlayerVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        TryRunPreviewViewAction(
            "Image parallax video player visibility",
            () =>
            {
                var prefix = ResolveImageParallaxPlayerPrefix(sender);
                if (e.NewValue is true)
                {
                    _activeImageParallaxPlayerPrefix = prefix;
                    UpdateImageParallaxPlaybackButton(prefix);
                    UpdateImageParallaxTimeText(prefix);
                    SyncImageParallaxVolumeControls(prefix);
                    return;
                }

                var media = FindImageParallaxMedia(prefix);
                media?.Pause();
                if (string.Equals(_activeImageParallaxPlayerPrefix, prefix, StringComparison.Ordinal))
                {
                    _imageParallaxPlaybackTimer.Stop();
                    _isImageParallaxMediaPlaying = false;
                    _isImageParallaxMediaEnded = false;
                }

                UpdateImageParallaxPlaybackButton(prefix);
            });
    }

    private void OnImageParallaxPreviewPlayPauseClicked(object sender, RoutedEventArgs e)
    {
        TryRunPreviewViewAction(
            "Image parallax play/pause",
            () =>
            {
                var prefix = ResolveImageParallaxPlayerPrefix(sender);
                _activeImageParallaxPlayerPrefix = prefix;
                var media = FindImageParallaxMedia(prefix);
                if (media is null)
                {
                    return;
                }

                if (_isImageParallaxMediaPlaying)
                {
                    PauseImageParallaxMedia(media, prefix);
                    return;
                }

                PlayImageParallaxMedia(media, prefix);
            });
    }

    private void OnImageParallaxPreviewTimelinePreviewMouseDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        var prefix = ResolveImageParallaxPlayerPrefix(sender);
        _activeImageParallaxPlayerPrefix = prefix;
        _wasImageParallaxMediaPlayingBeforeTimelineDrag = _isImageParallaxMediaPlaying;
        _imageParallaxPlaybackTimer.Stop();
        if (_wasImageParallaxMediaPlayingBeforeTimelineDrag &&
            FindImageParallaxMedia(prefix) is { } media)
        {
            media.Pause();
        }

        _isUserDraggingImageParallaxTimeline = true;
    }

    private void OnImageParallaxPreviewTimelinePreviewMouseUp(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        TryRunPreviewViewAction(
            "Image parallax timeline seek",
            () =>
            {
                var prefix = ResolveImageParallaxPlayerPrefix(sender);
                _activeImageParallaxPlayerPrefix = prefix;
                _isUserDraggingImageParallaxTimeline = false;
                var media = FindImageParallaxMedia(prefix);
                var timeline = FindImageParallaxTimeline(prefix);
                if (media is not null && timeline is not null)
                {
                    SeekImageParallaxMedia(media, prefix, timeline.Value);
                }

                if (_wasImageParallaxMediaPlayingBeforeTimelineDrag && media is not null)
                {
                    media.Play();
                    _isImageParallaxMediaPlaying = true;
                    _imageParallaxPlaybackTimer.Start();
                }
                else if (media is not null)
                {
                    media.Pause();
                    _isImageParallaxMediaPlaying = false;
                    _imageParallaxPlaybackTimer.Stop();
                }

                _wasImageParallaxMediaPlayingBeforeTimelineDrag = false;
                UpdateImageParallaxPlaybackButton(prefix);
                UpdateImageParallaxTimeText(prefix);
            });
    }

    private void OnImageParallaxPreviewTimelineValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        TryRunPreviewViewAction(
            "Image parallax timeline update",
            () =>
            {
                if (_isUpdatingImageParallaxTimeline)
                {
                    return;
                }

                var prefix = ResolveImageParallaxPlayerPrefix(sender);
                if (_isUserDraggingImageParallaxTimeline &&
                    FindImageParallaxMedia(prefix) is { } media &&
                    FindImageParallaxTimeline(prefix) is { } timeline)
                {
                    SeekImageParallaxMedia(media, prefix, timeline.Value);
                }

                UpdateImageParallaxTimeText(prefix);
            });
    }

    private void OnImageParallaxPreviewVolumeValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        TryRunPreviewViewAction(
            "Image parallax volume update",
            () =>
            {
                if (_isUpdatingImageParallaxVolume)
                {
                    return;
                }

                var prefix = ResolveImageParallaxPlayerPrefix(sender);
                _activeImageParallaxPlayerPrefix = prefix;
                ApplyImageParallaxVolumeValue(prefix, e.NewValue);
            });
    }

    private void OnImageParallaxPreviewVolumeSliderPreviewMouseLeftButtonDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        TryRunPreviewViewAction(
            "Image parallax volume track click",
            () =>
            {
                if (sender is not WpfSlider slider ||
                    slider.ActualWidth <= 0 ||
                    e.OriginalSource is not DependencyObject source ||
                    FindVisualParent<WpfThumb>(source) is not null)
                {
                    return;
                }

                var prefix = ResolveImageParallaxPlayerPrefix(sender);
                _activeImageParallaxPlayerPrefix = prefix;
                var clickedX = Math.Clamp(e.GetPosition(slider).X, 0, slider.ActualWidth);
                var normalizedClick = clickedX / slider.ActualWidth;
                var clickedValue = slider.Minimum + ((slider.Maximum - slider.Minimum) * normalizedClick);
                var clampedValue = Math.Clamp(clickedValue, slider.Minimum, slider.Maximum);

                SetImageParallaxVolumeSliderValue(prefix, clampedValue);
                ApplyImageParallaxVolumeValue(prefix, clampedValue);
                e.Handled = true;
            });
    }

    private void ApplyImageParallaxVolumeValue(string prefix, double value)
    {
        var volume = Math.Clamp(value, 0, 1);
        if (volume <= PreviewMutedVolumeThreshold)
        {
            SetImageParallaxMutedState(
                prefix,
                isMuted: true,
                updateSlider: false,
                preserveCurrentVolume: false);
            return;
        }

        _lastImageParallaxVolume = volume;
        if (FindImageParallaxMedia(prefix) is { } media)
        {
            media.IsMuted = false;
            media.Volume = volume;
        }

        SetImageParallaxMuteToggleChecked(prefix, false);
        UpdateImageParallaxMuteButton(prefix);
    }

    private void OnImageParallaxPreviewMuteChanged(object sender, RoutedEventArgs e)
    {
        TryRunPreviewViewAction(
            "Image parallax mute update",
            () =>
            {
                var prefix = ResolveImageParallaxPlayerPrefix(sender);
                _activeImageParallaxPlayerPrefix = prefix;
                var muteToggle = FindImageParallaxMuteToggle(prefix);
                if (_isUpdatingImageParallaxMute || muteToggle is null)
                {
                    return;
                }

                SetImageParallaxMutedState(
                    prefix,
                    isMuted: muteToggle.IsChecked == true,
                    updateSlider: true,
                    preserveCurrentVolume: true);
            });
    }

    private void OnImageParallaxPlaybackTimerTick(object? sender, EventArgs e)
    {
        TryRunPreviewViewAction(
            "Image parallax playback timer",
            () =>
            {
                var prefix = _activeImageParallaxPlayerPrefix;
                var media = FindImageParallaxMedia(prefix);
                if (media is null)
                {
                    _imageParallaxPlaybackTimer.Stop();
                    return;
                }

                if (!_isUserDraggingImageParallaxTimeline)
                {
                    SetImageParallaxTimelineValue(prefix, media.Position.TotalSeconds);
                }

                UpdateImageParallaxTimeText(prefix);
            });
    }

    private void PlayImageParallaxMedia(WpfMediaElement media, string prefix)
    {
        if (_isImageParallaxMediaEnded)
        {
            media.Position = TimeSpan.Zero;
            SetImageParallaxTimelineValue(prefix, 0);
            _isImageParallaxMediaEnded = false;
        }

        media.Play();
        _imageParallaxPlaybackTimer.Start();
        _isImageParallaxMediaPlaying = true;
        UpdateImageParallaxPlaybackButton(prefix);
    }

    private void PauseImageParallaxMedia(WpfMediaElement media, string prefix)
    {
        media.Pause();
        _imageParallaxPlaybackTimer.Stop();
        _isImageParallaxMediaPlaying = false;
        _isImageParallaxMediaEnded = false;
        UpdateImageParallaxPlaybackButton(prefix);
    }

    private void InitializeImageParallaxFirstFrame(
        WpfMediaElement? media,
        TimeSpan? duration,
        string prefix)
    {
        if (media is null ||
            duration is null ||
            duration <= PreviewFirstFrameNudge)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            new Action(() => TryRunPreviewViewAction(
                "Image parallax first frame initialization",
                () =>
                {
                    if (_isImageParallaxMediaPlaying)
                    {
                        return;
                    }

                    media.ScrubbingEnabled = true;
                    media.Pause();
                    media.Position = PreviewFirstFrameNudge;
                    SetImageParallaxTimelineValue(prefix, 0);
                    UpdateImageParallaxTimeText(prefix);
                })),
            DispatcherPriority.Background);
    }

    private void SeekImageParallaxMedia(WpfMediaElement media, string prefix, double seconds)
    {
        media.Position = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (_isImageParallaxMediaEnded &&
            FindImageParallaxTimeline(prefix) is { } timeline &&
            seconds < timeline.Maximum)
        {
            _isImageParallaxMediaEnded = false;
            UpdateImageParallaxPlaybackButton(prefix);
        }
    }

    private void SetImageParallaxTimelineValue(string prefix, double value)
    {
        var timeline = FindImageParallaxTimeline(prefix);
        if (timeline is null)
        {
            return;
        }

        _isUpdatingImageParallaxTimeline = true;
        try
        {
            timeline.Value = Math.Clamp(value, timeline.Minimum, timeline.Maximum);
        }
        finally
        {
            _isUpdatingImageParallaxTimeline = false;
        }
    }

    private void SyncImageParallaxVolumeControls(string prefix)
    {
        var slider = FindImageParallaxVolumeSlider(prefix);
        var media = FindImageParallaxMedia(prefix);
        var sliderVolume = slider is null
            ? PreviewDefaultVolume
            : Math.Clamp(slider.Value, 0, 1);
        if (sliderVolume > PreviewMutedVolumeThreshold)
        {
            _lastImageParallaxVolume = sliderVolume;
        }

        var isMuted = sliderVolume <= PreviewMutedVolumeThreshold ||
            media?.IsMuted == true;
        if (media is not null)
        {
            media.Volume = isMuted ? 0 : sliderVolume;
            media.IsMuted = isMuted;
        }

        SetImageParallaxMuteToggleChecked(prefix, isMuted);
        UpdateImageParallaxMuteButton(prefix);
    }

    private void SetImageParallaxMutedState(
        string prefix,
        bool isMuted,
        bool updateSlider,
        bool preserveCurrentVolume)
    {
        var slider = FindImageParallaxVolumeSlider(prefix);
        var media = FindImageParallaxMedia(prefix);
        var currentVolume = slider is null
            ? media?.Volume ?? _lastImageParallaxVolume
            : Math.Clamp(slider.Value, 0, 1);

        if (isMuted)
        {
            if (preserveCurrentVolume && currentVolume > PreviewMutedVolumeThreshold)
            {
                _lastImageParallaxVolume = currentVolume;
            }

            if (media is not null)
            {
                media.IsMuted = true;
                media.Volume = 0;
            }

            if (updateSlider)
            {
                SetImageParallaxVolumeSliderValue(prefix, 0);
            }

            SetImageParallaxMuteToggleChecked(prefix, true);
            UpdateImageParallaxMuteButton(prefix);
            return;
        }

        var restoredVolume = _lastImageParallaxVolume > PreviewMutedVolumeThreshold
            ? _lastImageParallaxVolume
            : PreviewDefaultVolume;
        if (media is not null)
        {
            media.IsMuted = false;
            media.Volume = restoredVolume;
        }

        if (updateSlider)
        {
            SetImageParallaxVolumeSliderValue(prefix, restoredVolume);
        }

        SetImageParallaxMuteToggleChecked(prefix, false);
        UpdateImageParallaxMuteButton(prefix);
    }

    private void SetImageParallaxVolumeSliderValue(string prefix, double value)
    {
        var slider = FindImageParallaxVolumeSlider(prefix);
        if (slider is null)
        {
            return;
        }

        _isUpdatingImageParallaxVolume = true;
        try
        {
            slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
        }
        finally
        {
            _isUpdatingImageParallaxVolume = false;
        }
    }

    private void SetImageParallaxMuteToggleChecked(string prefix, bool isMuted)
    {
        var muteToggle = FindImageParallaxMuteToggle(prefix);
        if (muteToggle is null)
        {
            return;
        }

        _isUpdatingImageParallaxMute = true;
        try
        {
            muteToggle.IsChecked = isMuted;
        }
        finally
        {
            _isUpdatingImageParallaxMute = false;
        }
    }

    private void UpdateImageParallaxPlaybackButton(string prefix)
    {
        var playPauseButton = FindImageParallaxPlayPauseButton(prefix);
        if (playPauseButton is null)
        {
            return;
        }

        var viewModel = GetViewModel();
        var label = _isImageParallaxMediaPlaying
            ? viewModel?.PreviewPauseText ?? "Pause"
            : viewModel?.PreviewPlayText ?? "Play";
        playPauseButton.Content = _isImageParallaxMediaPlaying ? PreviewPauseGlyph : PreviewPlayGlyph;
        playPauseButton.ToolTip = label;
        AutomationProperties.SetName(playPauseButton, label);
    }

    private void UpdateImageParallaxMuteButton(string prefix)
    {
        var muteToggle = FindImageParallaxMuteToggle(prefix);
        var volumeIcon = FindImageParallaxVolumeIcon(prefix);
        var media = FindImageParallaxMedia(prefix);
        var slider = FindImageParallaxVolumeSlider(prefix);
        var isMuted = muteToggle?.IsChecked == true ||
            media?.IsMuted == true ||
            slider?.Value <= PreviewMutedVolumeThreshold;
        var viewModel = GetViewModel();
        var label = isMuted
            ? viewModel?.PreviewUnmuteText ?? "Unmute"
            : viewModel?.PreviewMuteText ?? "Mute";
        var glyph = isMuted ? PreviewMuteGlyph : PreviewVolumeGlyph;
        if (muteToggle is not null)
        {
            muteToggle.Content = glyph;
            muteToggle.ToolTip = label;
            AutomationProperties.SetName(muteToggle, label);
        }

        if (volumeIcon is not null)
        {
            volumeIcon.Text = glyph;
        }
    }

    private void UpdateImageParallaxTimeText(string prefix)
    {
        var timeText = FindImageParallaxTimeText(prefix);
        if (timeText is null)
        {
            return;
        }

        var media = FindImageParallaxMedia(prefix);
        var current = media?.Position ?? TimeSpan.Zero;
        var duration = GetPreviewMediaDuration(media) ?? TimeSpan.Zero;
        timeText.Text = $"{FormatPreviewMediaTime(current)} / {FormatPreviewMediaTime(duration)}";
    }

    private WpfMediaElement? FindImageParallaxMedia(string prefix) =>
        FindNamedControl<WpfMediaElement>($"{prefix}PreviewMediaElement");

    private WpfSlider? FindImageParallaxTimeline(string prefix) =>
        FindNamedControl<WpfSlider>($"{prefix}PreviewTimelineSlider");

    private WpfButton? FindImageParallaxPlayPauseButton(string prefix) =>
        FindNamedControl<WpfButton>($"{prefix}PreviewPlayPauseButton");

    private WpfTextBlock? FindImageParallaxTimeText(string prefix) =>
        FindNamedControl<WpfTextBlock>($"{prefix}PreviewTimeText");

    private WpfSlider? FindImageParallaxVolumeSlider(string prefix) =>
        FindNamedControl<WpfSlider>($"{prefix}PreviewVolumeSlider");

    private WpfToggleButton? FindImageParallaxMuteToggle(string prefix) =>
        FindNamedControl<WpfToggleButton>($"{prefix}PreviewMuteToggleButton");

    private WpfTextBlock? FindImageParallaxVolumeIcon(string prefix) =>
        FindNamedControl<WpfTextBlock>($"{prefix}PreviewVolumeIcon");

    private static string ResolveImageParallaxPlayerPrefix(object sender)
    {
        if (sender is FrameworkElement element &&
            element.Name.Contains("Vertical", StringComparison.Ordinal))
        {
            return ImageParallaxVerticalPlayerPrefix;
        }

        return ImageParallaxWidePlayerPrefix;
    }

    private void OnPreviewMediaOpened(object sender, RoutedEventArgs e)
    {
        TryRunPreviewViewAction(
            "Preview media opened",
            () =>
            {
                _previewPlaybackTimer.Stop();
                _isPreviewMediaPlaying = false;
                _isPreviewMediaEnded = false;
                var media = sender as WpfMediaElement ?? FindNamedControl<WpfMediaElement>("PreviewMediaElement");
                var timeline = FindNamedControl<WpfSlider>("PreviewTimelineSlider");
                TimeSpan? duration = null;
                if (media is not null)
                {
                    media.ScrubbingEnabled = true;
                    media.Pause();
                    media.Position = TimeSpan.Zero;
                    duration = GetPreviewMediaDuration(media);
                }

                if (timeline is not null)
                {
                    _isUpdatingPreviewTimeline = true;
                    try
                    {
                        timeline.Maximum = duration?.TotalSeconds ?? 0;
                        timeline.Value = 0;
                    }
                    finally
                    {
                        _isUpdatingPreviewTimeline = false;
                    }
                }

                SetPreviewPlaybackStatus(GetViewModel()?.PreviewPlaybackFallbackText ?? string.Empty);
                SyncPreviewVolumeControls();
                UpdatePreviewPlaybackButton();
                UpdatePreviewTimeText();
                InitializePreviewFirstFrame(media, duration);
            });
    }

    private void OnPreviewMediaEnded(object sender, RoutedEventArgs e)
    {
        TryRunPreviewViewAction(
            "Preview media ended",
            () =>
            {
                _isPreviewMediaPlaying = false;
                _isPreviewMediaEnded = true;
                _previewPlaybackTimer.Stop();
                var media = sender as WpfMediaElement ?? FindNamedControl<WpfMediaElement>("PreviewMediaElement");
                if (GetPreviewMediaDuration(media) is { } duration)
                {
                    SetPreviewTimelineValue(duration.TotalSeconds);
                }

                SetPreviewPlaybackStatus(GetViewModel()?.PreviewEndedText ?? "Preview ended");
                UpdatePreviewPlaybackButton();
                UpdatePreviewTimeText();
            });
    }

    private void OnPreviewMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        TryRunPreviewViewAction(
            "Preview media failed",
            () =>
            {
                _isPreviewMediaPlaying = false;
                _isPreviewMediaEnded = false;
                _previewPlaybackTimer.Stop();
                var message = GetViewModel()?.EmbeddedPlaybackUnavailableText ??
                    "Embedded playback unavailable";
                SetPreviewPlaybackStatus(string.IsNullOrWhiteSpace(e.ErrorException?.Message)
                    ? message
                    : $"{message}: {e.ErrorException.Message}");
                UpdatePreviewPlaybackButton();
            });
    }

    private void OnPreviewReadyModalVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        TryRunPreviewViewAction(
            "Preview ready modal visibility",
            () =>
            {
                if (e.NewValue is true)
                {
                    _isPreviewMediaPlaying = false;
                    _isPreviewMediaEnded = false;
                    if (FindNamedControl<WpfMediaElement>("PreviewMediaElement") is { } media)
                    {
                        media.ScrubbingEnabled = true;
                        media.Pause();
                        media.Position = TimeSpan.Zero;
                    }

                    SetPreviewPlaybackStatus(GetViewModel()?.PreviewPlaybackFallbackText ?? string.Empty);
                    SyncPreviewVolumeControls();
                    UpdatePreviewPlaybackButton();
                    UpdatePreviewTimeText();
                    return;
                }

                _previewPlaybackTimer.Stop();
                _isPreviewMediaPlaying = false;
                _isPreviewMediaEnded = false;
                FindNamedControl<WpfMediaElement>("PreviewMediaElement")?.Pause();
                _wasPreviewMediaPlayingBeforeTimelineDrag = false;
                UpdatePreviewPlaybackButton();
                SyncPreviewVolumeControls();
            });
    }

    private void OnPreviewPlayPauseClicked(object sender, RoutedEventArgs e)
    {
        TryRunPreviewViewAction(
            "Preview play/pause",
            () =>
            {
                var media = FindNamedControl<WpfMediaElement>("PreviewMediaElement");
                if (media is null)
                {
                    SetPreviewPlaybackStatus(GetViewModel()?.EmbeddedPlaybackUnavailableText ??
                        "Embedded playback unavailable");
                    return;
                }

                if (_isPreviewMediaPlaying)
                {
                    PausePreviewMedia(media);
                    return;
                }

                PlayPreviewMedia(media);
            });
    }

    private void OnPreviewTimelinePreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _wasPreviewMediaPlayingBeforeTimelineDrag = _isPreviewMediaPlaying;
        _previewPlaybackTimer.Stop();
        if (_wasPreviewMediaPlayingBeforeTimelineDrag &&
            FindNamedControl<WpfMediaElement>("PreviewMediaElement") is { } media)
        {
            media.Pause();
        }

        _isUserDraggingPreviewTimeline = true;
    }

    private void OnPreviewTimelinePreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        TryRunPreviewViewAction(
            "Preview timeline seek",
            () =>
            {
                _isUserDraggingPreviewTimeline = false;
                var media = FindNamedControl<WpfMediaElement>("PreviewMediaElement");
                var timeline = FindNamedControl<WpfSlider>("PreviewTimelineSlider");
                if (media is not null && timeline is not null)
                {
                    SeekPreviewMedia(media, timeline.Value);
                }

                if (_wasPreviewMediaPlayingBeforeTimelineDrag && media is not null)
                {
                    media.Play();
                    _isPreviewMediaPlaying = true;
                    _previewPlaybackTimer.Start();
                }
                else if (media is not null)
                {
                    media.Pause();
                    _isPreviewMediaPlaying = false;
                    _previewPlaybackTimer.Stop();
                }

                _wasPreviewMediaPlayingBeforeTimelineDrag = false;
                UpdatePreviewPlaybackButton();
                UpdatePreviewTimeText();
            });
    }

    private void OnPreviewTimelineValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        TryRunPreviewViewAction(
            "Preview timeline update",
            () =>
            {
                if (_isUpdatingPreviewTimeline)
                {
                    return;
                }

                if (_isUserDraggingPreviewTimeline &&
                    FindNamedControl<WpfMediaElement>("PreviewMediaElement") is { } media &&
                    FindNamedControl<WpfSlider>("PreviewTimelineSlider") is { } timeline)
                {
                    SeekPreviewMedia(media, timeline.Value);
                }

                UpdatePreviewTimeText();
            });
    }

    private void OnPreviewVolumeValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        TryRunPreviewViewAction(
            "Preview volume update",
            () =>
            {
                if (_isUpdatingPreviewVolume)
                {
                    return;
                }

                ApplyPreviewVolumeValue(e.NewValue);
            });
    }

    private void OnPreviewVolumeSliderPreviewMouseLeftButtonDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        TryRunPreviewViewAction(
            "Preview volume track click",
            () =>
            {
                if (sender is not WpfSlider slider ||
                    slider.ActualWidth <= 0 ||
                    e.OriginalSource is not DependencyObject source ||
                    FindVisualParent<WpfThumb>(source) is not null)
                {
                    return;
                }

                var clickedX = Math.Clamp(e.GetPosition(slider).X, 0, slider.ActualWidth);
                var normalizedClick = clickedX / slider.ActualWidth;
                var clickedValue = slider.Minimum + ((slider.Maximum - slider.Minimum) * normalizedClick);
                var clampedValue = Math.Clamp(clickedValue, slider.Minimum, slider.Maximum);

                SetPreviewVolumeSliderValue(clampedValue);
                ApplyPreviewVolumeValue(clampedValue);
                e.Handled = true;
            });
    }

    private void ApplyPreviewVolumeValue(double value)
    {
        var volume = Math.Clamp(value, 0, 1);
        if (volume <= PreviewMutedVolumeThreshold)
        {
            SetPreviewMutedState(
                isMuted: true,
                updateSlider: false,
                preserveCurrentVolume: false);
            return;
        }

        _lastPreviewVolume = volume;
        if (FindNamedControl<WpfMediaElement>("PreviewMediaElement") is { } media)
        {
            media.IsMuted = false;
            media.Volume = volume;
        }

        SetPreviewMuteToggleChecked(false);
        UpdatePreviewMuteButton();
    }

    private void OnPreviewMuteChanged(object sender, RoutedEventArgs e)
    {
        TryRunPreviewViewAction(
            "Preview mute update",
            () =>
            {
                var muteToggle = FindNamedControl<WpfToggleButton>("PreviewMuteToggleButton");
                if (_isUpdatingPreviewMute || muteToggle is null)
                {
                    return;
                }

                SetPreviewMutedState(
                    isMuted: muteToggle.IsChecked == true,
                    updateSlider: true,
                    preserveCurrentVolume: true);
            });
    }

    private void OnPreviewPlaybackTimerTick(object? sender, EventArgs e)
    {
        TryRunPreviewViewAction(
            "Preview playback timer",
            () =>
            {
                var media = FindNamedControl<WpfMediaElement>("PreviewMediaElement");
                if (media is null)
                {
                    _previewPlaybackTimer.Stop();
                    return;
                }

                if (!_isUserDraggingPreviewTimeline)
                {
                    SetPreviewTimelineValue(media.Position.TotalSeconds);
                }

                UpdatePreviewTimeText();
            });
    }

    private void SetPreviewTimelineValue(double value)
    {
        var timeline = FindNamedControl<WpfSlider>("PreviewTimelineSlider");
        if (timeline is null)
        {
            return;
        }

        _isUpdatingPreviewTimeline = true;
        try
        {
            timeline.Value = Math.Clamp(value, timeline.Minimum, timeline.Maximum);
        }
        finally
        {
            _isUpdatingPreviewTimeline = false;
        }
    }

    private void PlayPreviewMedia(WpfMediaElement media)
    {
        if (_isPreviewMediaEnded)
        {
            media.Position = TimeSpan.Zero;
            SetPreviewTimelineValue(0);
            _isPreviewMediaEnded = false;
        }

        media.Play();
        _previewPlaybackTimer.Start();
        _isPreviewMediaPlaying = true;
        SetPreviewPlaybackStatus(GetViewModel()?.PreviewPlaybackFallbackText ?? string.Empty);
        UpdatePreviewPlaybackButton();
    }

    private void PausePreviewMedia(WpfMediaElement media)
    {
        media.Pause();
        _previewPlaybackTimer.Stop();
        _isPreviewMediaPlaying = false;
        _isPreviewMediaEnded = false;
        SetPreviewPlaybackStatus(GetViewModel()?.PreviewPlaybackFallbackText ?? string.Empty);
        UpdatePreviewPlaybackButton();
    }

    private void InitializePreviewFirstFrame(WpfMediaElement? media, TimeSpan? duration)
    {
        if (media is null ||
            duration is null ||
            duration <= PreviewFirstFrameNudge)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            new Action(() => TryRunPreviewViewAction(
                "Preview first frame initialization",
                () =>
                {
                    if (_isPreviewMediaPlaying)
                    {
                        return;
                    }

                    media.ScrubbingEnabled = true;
                    media.Pause();
                    media.Position = PreviewFirstFrameNudge;
                    SetPreviewTimelineValue(0);
                    UpdatePreviewTimeText();
                })),
            DispatcherPriority.Background);
    }

    private void SyncPreviewVolumeControls()
    {
        var slider = FindNamedControl<WpfSlider>("PreviewVolumeSlider");
        var media = FindNamedControl<WpfMediaElement>("PreviewMediaElement");
        var sliderVolume = slider is null
            ? PreviewDefaultVolume
            : Math.Clamp(slider.Value, 0, 1);
        if (sliderVolume > PreviewMutedVolumeThreshold)
        {
            _lastPreviewVolume = sliderVolume;
        }

        var isMuted = sliderVolume <= PreviewMutedVolumeThreshold ||
            media?.IsMuted == true;
        if (media is not null)
        {
            media.Volume = isMuted ? 0 : sliderVolume;
            media.IsMuted = isMuted;
        }

        SetPreviewMuteToggleChecked(isMuted);
        UpdatePreviewMuteButton();
    }

    private void SetPreviewMutedState(
        bool isMuted,
        bool updateSlider,
        bool preserveCurrentVolume)
    {
        var slider = FindNamedControl<WpfSlider>("PreviewVolumeSlider");
        var media = FindNamedControl<WpfMediaElement>("PreviewMediaElement");
        var currentVolume = slider is null
            ? media?.Volume ?? _lastPreviewVolume
            : Math.Clamp(slider.Value, 0, 1);

        if (isMuted)
        {
            if (preserveCurrentVolume && currentVolume > PreviewMutedVolumeThreshold)
            {
                _lastPreviewVolume = currentVolume;
            }

            if (media is not null)
            {
                media.IsMuted = true;
                media.Volume = 0;
            }

            if (updateSlider)
            {
                SetPreviewVolumeSliderValue(0);
            }

            SetPreviewMuteToggleChecked(true);
            UpdatePreviewMuteButton();
            return;
        }

        var restoredVolume = _lastPreviewVolume > PreviewMutedVolumeThreshold
            ? _lastPreviewVolume
            : PreviewDefaultVolume;
        if (media is not null)
        {
            media.IsMuted = false;
            media.Volume = restoredVolume;
        }

        if (updateSlider)
        {
            SetPreviewVolumeSliderValue(restoredVolume);
        }

        SetPreviewMuteToggleChecked(false);
        UpdatePreviewMuteButton();
    }

    private void SetPreviewVolumeSliderValue(double value)
    {
        var slider = FindNamedControl<WpfSlider>("PreviewVolumeSlider");
        if (slider is null)
        {
            return;
        }

        _isUpdatingPreviewVolume = true;
        try
        {
            slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
        }
        finally
        {
            _isUpdatingPreviewVolume = false;
        }
    }

    private void SetPreviewMuteToggleChecked(bool isMuted)
    {
        var muteToggle = FindNamedControl<WpfToggleButton>("PreviewMuteToggleButton");
        if (muteToggle is null)
        {
            return;
        }

        _isUpdatingPreviewMute = true;
        try
        {
            muteToggle.IsChecked = isMuted;
        }
        finally
        {
            _isUpdatingPreviewMute = false;
        }
    }

    private void UpdatePreviewPlaybackButton()
    {
        var playPauseButton = FindNamedControl<WpfButton>("PreviewPlayPauseButton");
        if (playPauseButton is null)
        {
            return;
        }

        var viewModel = GetViewModel();
        var label = _isPreviewMediaPlaying
            ? viewModel?.PreviewPauseText ?? "Pause"
            : viewModel?.PreviewPlayText ?? "Play";
        playPauseButton.Content = _isPreviewMediaPlaying ? PreviewPauseGlyph : PreviewPlayGlyph;
        playPauseButton.ToolTip = label;
        AutomationProperties.SetName(playPauseButton, label);
    }

    private void UpdatePreviewMuteButton()
    {
        var muteToggle = FindNamedControl<WpfToggleButton>("PreviewMuteToggleButton");
        if (muteToggle is null)
        {
            return;
        }

        var viewModel = GetViewModel();
        var isMuted = muteToggle.IsChecked == true;
        var label = isMuted
            ? viewModel?.PreviewUnmuteText ?? "Unmute"
            : viewModel?.PreviewMuteText ?? "Mute";
        muteToggle.Content = isMuted ? PreviewMuteGlyph : PreviewVolumeGlyph;
        muteToggle.ToolTip = label;
        AutomationProperties.SetName(muteToggle, label);
    }

    private void SeekPreviewMedia(WpfMediaElement media, double seconds)
    {
        media.Position = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (_isPreviewMediaEnded &&
            FindNamedControl<WpfSlider>("PreviewTimelineSlider") is { } timeline &&
            seconds < timeline.Maximum)
        {
            _isPreviewMediaEnded = false;
            UpdatePreviewPlaybackButton();
            SetPreviewPlaybackStatus(GetViewModel()?.PreviewPlaybackFallbackText ?? string.Empty);
        }
    }

    private void UpdatePreviewTimeText()
    {
        var timeText = FindNamedControl<WpfTextBlock>("PreviewTimeText");
        if (timeText is null)
        {
            return;
        }

        var media = FindNamedControl<WpfMediaElement>("PreviewMediaElement");
        var current = media?.Position ?? TimeSpan.Zero;
        var duration = GetPreviewMediaDuration(media) ?? TimeSpan.Zero;
        timeText.Text = $"{FormatPreviewMediaTime(current)} / {FormatPreviewMediaTime(duration)}";
    }

    private void SetPreviewPlaybackStatus(string text)
    {
        if (FindNamedControl<WpfTextBlock>("PreviewPlaybackStatusText") is { } statusText)
        {
            statusText.Text = text;
        }
    }

    private static TimeSpan? GetPreviewMediaDuration(WpfMediaElement? media) =>
        media?.NaturalDuration.HasTimeSpan == true
            ? media.NaturalDuration.TimeSpan
            : null;

    private MainWindowViewModel? GetViewModel() =>
        DataContext as MainWindowViewModel;

    private T? FindNamedControl<T>(string name)
        where T : class =>
        FindName(name) as T;

    private void TryRunPreviewViewAction(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            AppErrorLogService.LogRecoverableException(operation, exception);
        }
    }

    private static string FormatPreviewMediaTime(TimeSpan value) =>
        value.TotalHours >= 1
            ? value.ToString(@"hh\:mm\:ss")
            : value.ToString(@"mm\:ss");

    private static T? FindVisualChild<T>(DependencyObject? parent)
        where T : DependencyObject
    {
        if (parent is null)
        {
            return null;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static T? FindVisualParent<T>(DependencyObject? child)
        where T : DependencyObject
    {
        var current = child;
        while (current is not null)
        {
            if (current is T typedCurrent)
            {
                return typedCurrent;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private sealed class GridLengthAnimation : AnimationTimeline
    {
        public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
            nameof(From),
            typeof(GridLength),
            typeof(GridLengthAnimation),
            new PropertyMetadata(new GridLength(0)));

        public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
            nameof(To),
            typeof(GridLength),
            typeof(GridLengthAnimation),
            new PropertyMetadata(new GridLength(0)));

        public static readonly DependencyProperty EasingFunctionProperty = DependencyProperty.Register(
            nameof(EasingFunction),
            typeof(IEasingFunction),
            typeof(GridLengthAnimation));

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public IEasingFunction? EasingFunction
        {
            get => (IEasingFunction?)GetValue(EasingFunctionProperty);
            set => SetValue(EasingFunctionProperty, value);
        }

        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public override object GetCurrentValue(
            object defaultOriginValue,
            object defaultDestinationValue,
            AnimationClock animationClock)
        {
            var progress = animationClock.CurrentProgress ?? 0d;
            if (EasingFunction is not null)
            {
                progress = EasingFunction.Ease(progress);
            }

            var width = From.Value + ((To.Value - From.Value) * progress);
            return new GridLength(width);
        }
    }
}
