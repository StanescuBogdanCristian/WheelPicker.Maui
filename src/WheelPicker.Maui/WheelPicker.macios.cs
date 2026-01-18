using AVFoundation;
using Foundation;
using UIKit;
#if MACCATALYST
using CoreGraphics;
using System.Diagnostics;
#endif

namespace WheelPicker.Maui;

public partial class WheelPicker
{
    private static AVAudioPlayer? _audioPlayer;
    private static bool _initialized;
    private static readonly object _lock = new();
    private UIView? _platformView;
    private UIScrollView? _parentScrollView;
    private UIPanGestureRecognizer? _scrollShieldRecognizer;
    private bool? _savedParentScrollEnabled;
#if MACCATALYST
        private UIPanGestureRecognizer? _mouseWheelRecognizer;
        private UIView? _mouseWheelAttachedView;
        private long _mouseWheelLastEventTimestamp;
        private bool _mouseWheelTimerRunning;
#endif

    #region Mouse Wheel Handling

#if MACCATALYST
        partial void InitializeMouseWheelHandling()
        {
            if (_mouseWheelAttachedView != null && _mouseWheelRecognizer != null)
            {
                try
                {
                    _mouseWheelAttachedView.RemoveGestureRecognizer(_mouseWheelRecognizer);
                    _mouseWheelRecognizer.Dispose();
                }
                catch { }

                _mouseWheelRecognizer = null;
                _mouseWheelAttachedView = null;
            }

            if (Handler?.PlatformView is not UIView nativeView)
                return;

            var recognizer = new UIPanGestureRecognizer(OnMouseWheelPan)
            {
                // 0 touches = scroll wheel / trackpad only
                MinimumNumberOfTouches = 0,
                MaximumNumberOfTouches = 0,
            };

            if (OperatingSystem.IsMacCatalystVersionAtLeast(13, 4))
            {
                recognizer.AllowedScrollTypesMask = UIScrollTypeMask.All;
            }

            recognizer.ShouldReceiveTouch += (r, touch) => false;
            recognizer.ShouldRecognizeSimultaneously += (r, other) => true;

            nativeView.AddGestureRecognizer(recognizer);

            _mouseWheelRecognizer = recognizer;
            _mouseWheelAttachedView = nativeView;
        }

        private void OnMouseWheelPan(UIPanGestureRecognizer gr)
        {
            if (!IsEnabled || !IsSwipeEnabled)
                return;

            if (ItemsSource == null || ItemsSource.Count == 0)
                return;

            var view = gr.View;
            if (view == null)
                return;

            if (gr.State != UIGestureRecognizerState.Changed)
                return;

            CGPoint delta = gr.TranslationInView(view);
            gr.SetTranslation(CGPoint.Empty, view);

            double dy = delta.Y;

            if (Math.Abs(dy) < 0.1)
                return;

            double deltaItems = -(dy / ItemHeight);

            if (Math.Abs(deltaItems) < 0.01)
                return;

            CancelAnimations();

            double candidate = _virtualCenterIndex + deltaItems;

            if (!Loop)
            {
                candidate = Math.Clamp(candidate, 0, ItemsSource.Count - 1);
            }

            _virtualCenterIndex = candidate;

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
            if (_mouseWheelAttachedView != null && _mouseWheelRecognizer != null)
            {
                try
                {
                    _mouseWheelAttachedView.RemoveGestureRecognizer(_mouseWheelRecognizer);
                    _mouseWheelRecognizer.Dispose();
                }
                catch { }

                _mouseWheelRecognizer = null;
                _mouseWheelAttachedView = null;
            }
        }
#endif

    #endregion

    #region Scroll Conflict Handling

    partial void InitializeScrollConflictHandling()
    {
        try
        {
            // Clear previous state if handler was reattached
            if (_platformView != null && _scrollShieldRecognizer != null)
            {
                _platformView.RemoveGestureRecognizer(_scrollShieldRecognizer);
                _scrollShieldRecognizer.Dispose();
                _scrollShieldRecognizer = null;
            }

            _platformView = null;
            _parentScrollView = null;
            _savedParentScrollEnabled = null;

            if (Handler?.PlatformView is UIView view)
            {
                _platformView = view;
                _parentScrollView = FindParentScrollView(view);

                var recognizer = new UIPanGestureRecognizer(HandlePanShield)
                {
                    CancelsTouchesInView = false
                };

                // Let this recognizer and MAUI's own PanGestureRecognizer
                // run at the same time.
                recognizer.ShouldRecognizeSimultaneously += (r, other) => true;

                _scrollShieldRecognizer = recognizer;
                view.AddGestureRecognizer(recognizer);
            }
        }
        catch
        {
            // best-effort; if anything fails, we just fall back to default behavior
        }
    }

    private void HandlePanShield(UIPanGestureRecognizer gesture)
    {
        if (!IsEnabled || !IsSwipeEnabled)
            return;

        if (_platformView == null)
            return;

        // Lazily re-discover parent scroll if needed
        _parentScrollView ??= FindParentScrollView(_platformView);

        if (_parentScrollView == null)
            return;

        switch (gesture.State)
        {
            case UIGestureRecognizerState.Began:
            case UIGestureRecognizerState.Changed:
                // Disable parent UIScrollView only while dragging over the wheel.
                _savedParentScrollEnabled ??= _parentScrollView.ScrollEnabled;

                _parentScrollView.ScrollEnabled = false;
                break;

            case UIGestureRecognizerState.Cancelled:
            case UIGestureRecognizerState.Ended:
            case UIGestureRecognizerState.Failed:
                // Restore original scroll state.
                if (_savedParentScrollEnabled.HasValue)
                {
                    _parentScrollView.ScrollEnabled = _savedParentScrollEnabled.Value;
                    _savedParentScrollEnabled = null;
                }
                break;
        }
    }

    private static UIScrollView? FindParentScrollView(UIView view)
    {
        var current = view.Superview;
        while (current != null)
        {
            if (current is UIScrollView scroll)
                return scroll;

            current = current.Superview;
        }

        return null;
    }

    partial void DisposeScrollConflictHandling()
    {
        try
        {
            if (_platformView != null && _scrollShieldRecognizer != null)
            {
                _platformView.RemoveGestureRecognizer(_scrollShieldRecognizer);
                _scrollShieldRecognizer.Dispose();
            }

            _scrollShieldRecognizer = null;

            if (_parentScrollView != null && _savedParentScrollEnabled.HasValue)
            {
                _parentScrollView.ScrollEnabled = _savedParentScrollEnabled.Value;
            }

            _platformView = null;
            _parentScrollView = null;
            _savedParentScrollEnabled = null;
        }
        catch
        {
        }
    }

    #endregion

    #region Sound Feedback

    partial void InitializeSoundFeedbackHandling()
    {
        if (_initialized)
            return;

        lock (_lock)
        {
            if (_initialized)
                return;

            _initialized = true;

            Task.Run(async () =>
            {
                try
                {
                    float volume = (float)SoundVolume;

                    using var input = await FileSystem.OpenAppPackageFileAsync(SoundAssetFileName);
                    using var ms = new MemoryStream();
                    await input.CopyToAsync(ms);
                    var data = NSData.FromArray(ms.ToArray());

                    _audioPlayer = AVAudioPlayer.FromData(data);
                    if (_audioPlayer != null)
                    {
                        _audioPlayer.Volume = volume;
                        _audioPlayer.PrepareToPlay();
                    }
                }
                catch
                {
                    // ignore
                }
            });
        }
    }

    partial void PlaySoundFeedback()
    {
        if (_audioPlayer == null)
            return;

        if (_audioPlayer.Playing)
        {
            _audioPlayer.Stop();
            _audioPlayer.CurrentTime = 0;
        }
        else
        {
            _audioPlayer.CurrentTime = 0;
        }

        _audioPlayer.Play();
    }

    partial void DisposeSoundFeedbackHandling()
    {
        try
        {
            lock (_lock)
            {
                if (_audioPlayer != null)
                {
                    try { _audioPlayer.Stop(); } catch { }
                    try { _audioPlayer.Dispose(); } catch { }
                    _audioPlayer = null;
                }

                _initialized = false;
            }
        }
        catch { }
    }

    #endregion
}