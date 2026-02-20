using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace SBC.WheelPicker;

public partial class WheelPicker
{
    private static int _soundRefCount;

    private UIElement? _platformView;
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
        if (!HasItems)
            return;

        var point = e.GetCurrentPoint((UIElement)sender);
        var properties = point.Properties;
        int delta = properties.MouseWheelDelta;

        if (properties.IsHorizontalMouseWheel || delta == 0)
            return;

        // Convert notch direction to item delta and delegate to shared logic.
        const double itemsPerNotch = 0.7;
        ApplyMouseWheelDelta((delta > 0 ? -1.0 : 1.0) * itemsPerNotch);

        e.Handled = true;
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
        Interlocked.Increment(ref _soundRefCount);

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

    partial void PlaySoundFeedback(double volume)
    {
        if (_mediaPlayer == null)
            return;

        _mediaPlayer.Volume = volume;
        _mediaPlayer.Pause();
        _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
        _mediaPlayer.Play();
    }

    partial void DisposeSoundFeedbackHandling()
    {
        if (Interlocked.Decrement(ref _soundRefCount) > 0)
            return;
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

    #region Native Haptic Feedback

    // Windows: most devices lack haptic hardware. MAUI's HapticFeedback
    // maps to controller vibration on Xbox or haptic trackpads on Surface
    // devices. Click for ticks, LongPress for snap. No-op on standard PCs.

    private IHapticFeedback? _hapticFallback;

    partial void InitializeNativeHaptic()
    {
        _hapticFallback = Microsoft.Maui.Devices.HapticFeedback.Default;
    }

    partial void PerformTickHaptic(double intensity)
    {
        if (_hapticFallback?.IsSupported != true)
            return;

        _hapticFallback.Perform(HapticFeedbackType.Click);
    }

    partial void PerformSnapHaptic()
    {
        if (_hapticFallback?.IsSupported != true)
            return;

        _hapticFallback.Perform(HapticFeedbackType.LongPress);
    }

    partial void DisposeNativeHaptic()
    {
        _hapticFallback = null;
    }

    #endregion
}
