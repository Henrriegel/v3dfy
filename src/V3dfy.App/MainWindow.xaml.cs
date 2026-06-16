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
    private const double PreviewDefaultVolume = 0.7;
    private const double PreviewMutedVolumeThreshold = 0.001;
    private const string PreviewPlayGlyph = "\uE768";
    private const string PreviewPauseGlyph = "\uE769";
    private const string PreviewVolumeGlyph = "\uE767";
    private const string PreviewMuteGlyph = "\uE74F";
    private readonly DispatcherTimer _previewPlaybackTimer;
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

    public MainWindow()
    {
        InitializeComponent();
        _previewPlaybackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _previewPlaybackTimer.Tick += OnPreviewPlaybackTimerTick;
        DataContext = new MainWindowViewModel();
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplySidebarWidth(viewModel.SidebarTargetWidth);
            viewModel.ConversionLogs.CollectionChanged += OnConversionLogsChanged;
            viewModel.PreviewGenerationLogs.CollectionChanged += OnPreviewGenerationLogsChanged;
        }
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
