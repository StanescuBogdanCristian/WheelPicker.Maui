using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.Diagnostics;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace WheelPicker.Maui;

public partial class WheelPicker
{
    private UIElement? _platformView;
    private long _mouseWheelLastEventTimestamp;
    private bool _mouseWheelTimerRunning;
    private static MediaPlayer? _mediaPlayer;
    private static Stream? _audioStream;
    private static bool _initialized;
    private static bool _initStarted;
    private static readonly object _lock = new();

    #region Mouse Wheel Handling
    partial void InitializeMouseWheelHandling()
    {
        if (_platformView != null)
        {
            _platformView.PointerWheelChanged -= OnPointerWheelChanged;
            _platformView = null;
        }

        if (Handler?.PlatformView is UIElement element)
        {
            _platformView = element;
            _platformView.PointerWheelChanged += OnPointerWheelChanged;
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!IsSwipeEnabled || !IsEnabled || ItemsSource == null || ItemsSource.Count == 0)
            return;

        var point = e.GetCurrentPoint((UIElement)sender);
        int delta = point.Properties.MouseWheelDelta;

        if (delta == 0)
            return;

        // 1 = full item, <1 = slower, >1 = faster
        const double itemsPerNotch = 0.7;

        double itemDelta = (delta > 0 ? -1.0 : 1.0) * itemsPerNotch;

        ApplyWheelScroll(itemDelta);

        e.Handled = true;
    }

    private void ApplyWheelScroll(double itemDelta)
    {
        if (ItemsSource == null || ItemsSource.Count == 0)
            return;

        double newVirtual = _virtualCenterIndex + itemDelta;

        if (!Loop)
        {
            newVirtual = Math.Clamp(newVirtual, 0, ItemsSource.Count - 1);
        }

        _virtualCenterIndex = newVirtual;

        UpdateVisualFromVirtualIndex();
        UpdateSelectionWhileScrolling();

        IsDragging = false;
        IsSpinning = true;

        // Record time of last wheel event
        _mouseWheelLastEventTimestamp = Stopwatch.GetTimestamp();

        if (!_mouseWheelTimerRunning)
        {
            _mouseWheelTimerRunning = true;

            Dispatcher.StartTimer(TimeSpan.FromMilliseconds(80), () =>
            {
                double elapsedMs = (Stopwatch.GetTimestamp() - _mouseWheelLastEventTimestamp) * 1000.0 / Stopwatch.Frequency;

                if (elapsedMs >= 120.0)
                {
                    _mouseWheelTimerRunning = false;
                    SnapToSelectedIndex();

                    return false;
                }

                return true;
            });
        }
    }

    partial void DisposeMouseWheelHandling()
    {
        if (_platformView != null)
        {
            _platformView.PointerWheelChanged -= OnPointerWheelChanged;
            _platformView = null;
        }
    }

    #endregion

    #region Sound Feedback

    partial void InitializeSoundFeedbackHandling()
    {
        if (_initialized || _initStarted)
            return;

        lock (_lock)
        {
            if (_initialized || _initStarted)
                return;

            _initStarted = true;
            _ = InitSoundAsync();
        }
    }

    private static async Task InitSoundAsync()
    {
        try
        {
            _audioStream = await FileSystem.OpenAppPackageFileAsync(SoundAssetFileName);
            var ras = _audioStream.AsRandomAccessStream();

            var player = new MediaPlayer
            {
                AudioCategory = MediaPlayerAudioCategory.SoundEffects,
                Volume = SoundVolume,
                Source = MediaSource.CreateFromStream(ras, $"audio/wav")
            };

            _mediaPlayer = player;
            _initialized = true;
        }
        catch
        {
            // leave _mediaPlayer null; PlaySoundFeedback will just no-op
        }
    }

    partial void PlaySoundFeedback()
    {
        if (_mediaPlayer == null)
            return;

        _mediaPlayer.Pause();
        _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
        _mediaPlayer.Play();
    }

    partial void DisposeSoundFeedbackHandling()
    {
        try
        {
            lock (_lock)
            {
                if (_mediaPlayer != null)
                {
                    try { _mediaPlayer.Dispose(); } catch { }
                    _mediaPlayer = null;
                }

                if (_audioStream != null)
                {
                    try { _audioStream.Dispose(); } catch { }
                    _audioStream = null;
                }

                _initialized = false;
                _initStarted = false;
            }
        }
        catch { }
    }

    #endregion
}