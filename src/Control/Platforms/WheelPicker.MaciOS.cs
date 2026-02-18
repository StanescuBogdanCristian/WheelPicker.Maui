using AVFoundation;
using Foundation;
using UIKit;
#if MACCATALYST
using CoreGraphics;
#endif

namespace SBC.WheelPicker;

public partial class WheelPicker
{
    private static AVAudioPlayer? _audioPlayer;
    private static bool _initialized;
    private static readonly object _lock = new();
#if MACCATALYST
    private UIPanGestureRecognizer? _mouseWheelRecognizer;
    private UIView? _mouseWheelAttachedView;
#endif

#if IOS
    // Tick generator: UIImpactFeedbackGenerator with .light style produces a
    // subtle tap. impactOccurred(intensity:) scales 0.0→1.0 in real time,
    // so we ramp intensity based on scroll speed — fast = light, slow = full.
    private UIImpactFeedbackGenerator? _tickFeedback;

    // Snap generator: UIImpactFeedbackGenerator with .medium style produces a
    // more pronounced "thunk" when the wheel locks into its final detent.
    private UIImpactFeedbackGenerator? _snapFeedback;
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
        if (gr.State != UIGestureRecognizerState.Changed)
            return;

        if (!HasItems || ItemHeight <= 0)
            return;

        var view = gr.View;
        if (view == null)
            return;

        CGPoint delta = gr.TranslationInView(view);
        gr.SetTranslation(CGPoint.Empty, view);

        double dy = delta.Y;
        if (Math.Abs(dy) < 0.1)
            return;

        // Convert pixels to items and delegate to shared logic.
        ApplyMouseWheelDelta(-(dy / ItemHeight));
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
                    using var input = await FileSystem.OpenAppPackageFileAsync(SoundAssetFileName);
                    using var ms = new MemoryStream();
                    await input.CopyToAsync(ms);
                    var data = NSData.FromArray(ms.ToArray());

                    _audioPlayer = AVAudioPlayer.FromData(data);
                    _audioPlayer?.PrepareToPlay();
                }
                catch
                {
                    // ignore
                }
            });
        }
    }

    partial void PlaySoundFeedback(double volume)
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

        _audioPlayer.Volume = (float)volume;
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

#if IOS
    #region Native Haptic Feedback

    partial void InitializeNativeHaptic()
    {
        if (OperatingSystem.IsIOSVersionAtLeast(17, 5) && Handler?.PlatformView is UIView view)
        {
            _tickFeedback = UIImpactFeedbackGenerator.GetFeedbackGenerator(
                UIImpactFeedbackStyle.Light, view);
            _snapFeedback = UIImpactFeedbackGenerator.GetFeedbackGenerator(
                UIImpactFeedbackStyle.Medium, view);
        }
        else
        {
#pragma warning disable CA1422 // Validate platform compatibility
            _tickFeedback = new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Light);
            _snapFeedback = new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Medium);
#pragma warning restore CA1422
        }

        _tickFeedback.Prepare();
        _snapFeedback.Prepare();
    }

    partial void PerformTickHaptic(double intensity)
    {
        if (_tickFeedback == null)
            return;

        _tickFeedback.ImpactOccurred((float)intensity);
        _tickFeedback.Prepare();
    }

    partial void PerformSnapHaptic()
    {
        if (_snapFeedback == null)
            return;

        _snapFeedback.ImpactOccurred();
        _snapFeedback.Prepare();
    }

    partial void DisposeNativeHaptic()
    {
        _tickFeedback?.Dispose();
        _tickFeedback = null;

        _snapFeedback?.Dispose();
        _snapFeedback = null;
    }

    #endregion
#endif
}
