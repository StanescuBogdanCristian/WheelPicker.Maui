using Android.Media;
using Android.OS;
using Android.Views;
using Application = Android.App.Application;
using AView = Android.Views.View;

namespace SBC.WheelPicker;

public partial class WheelPicker
{
    private static SoundPool? _soundPool;
    private static int _soundId;
    private static bool _loaded;
    private static bool _initStarted;
    private static readonly object _lock = new();

    private Vibrator? _vibrator;
    private bool _useComposition;

    #region Sound Feedback

    partial void InitializeSoundFeedbackHandling()
    {
        if (_loaded || _initStarted)
            return;

        lock (_lock)
        {
            if (_loaded || _initStarted)
                return;

            _initStarted = true;

            Task.Run(async () =>
            {
                try
                {
                    var context = Application.Context;

                    var attrs = new AudioAttributes.Builder()
                        ?.SetUsage(AudioUsageKind.AssistanceSonification)
                        ?.SetContentType(AudioContentType.Sonification)
                        ?.Build();

                    var sp = new SoundPool.Builder()
                        ?.SetMaxStreams(4)
                        ?.SetAudioAttributes(attrs)
                        ?.Build();

                    using var input = await FileSystem.OpenAppPackageFileAsync(SoundAssetFileName);

                    var cachePath = Path.Combine(context.CacheDir!.AbsolutePath, SoundAssetFileName);
                    using (var fs = File.Open(cachePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await input.CopyToAsync(fs);
                    }

                    _soundPool = sp;
                    if (_soundPool == null)
                        return;

                    _soundPool.LoadComplete += (_, e) =>
                    {
                        if (e.SampleId == _soundId)
                            _loaded = true;
                    };

                    _soundId = _soundPool.Load(cachePath, 1);
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
        if (!_loaded || _soundPool == null)
            return;

        float vol = (float)volume;

        _soundPool.Play(_soundId, vol, vol, 0, 0, 1f);
    }

    partial void DisposeSoundFeedbackHandling()
    {
        try
        {
            lock (_lock)
            {
                if (_soundPool != null)
                {
                    try { _soundPool.Release(); } catch { }
                    _soundPool.Dispose();
                    _soundPool = null;
                }

                _loaded = false;
                _initStarted = false;
                _soundId = 0;
            }
        }
        catch { }
    }

    #endregion

    #region Native Haptic Feedback

    partial void InitializeNativeHaptic()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            return;

        var vibrator = Application.Context.GetSystemService("vibrator") as Vibrator;
        if (vibrator?.HasVibrator != true)
            return;

        if (!vibrator.AreAllPrimitivesSupported(
                (int)VibrationEffectCompositionPrimitive.Tick,
                (int)VibrationEffectCompositionPrimitive.Click))
            return;

        _vibrator = vibrator;
        _useComposition = true;
    }

    partial void PerformTickHaptic(double intensity)
    {
        if (_useComposition && _vibrator != null)
        {
            float scale = (float)(0.3 + 0.7 * intensity);

#pragma warning disable CA1416 // Validate platform compatibility
            var effect = VibrationEffect.StartComposition()
                .AddPrimitive((int)VibrationEffectCompositionPrimitive.Click, scale)
                .Compose();

            _vibrator.Vibrate(effect);
#pragma warning restore CA1416 // Validate platform compatibility
            effect.Dispose();
            return;
        }

        // Pre-31 fallback: discrete constants.
        if (Handler?.PlatformView is not AView view)
            return;

        var constant = intensity > 0.5
            ? FeedbackConstants.LongPress
            : FeedbackConstants.ClockTick;

        view.PerformHapticFeedback(constant);
    }

    partial void PerformSnapHaptic()
    {
        if (_useComposition && _vibrator != null)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            var effect = VibrationEffect.StartComposition()
                .AddPrimitive((int)VibrationEffectCompositionPrimitive.Click, 1.0f)
                .Compose();

            _vibrator.Vibrate(effect);
#pragma warning restore CA1416 // Validate platform compatibility
            effect.Dispose();
            return;
        }

        // Pre-31 fallback.
        if (Handler?.PlatformView is not AView view)
            return;

        var fallback = OperatingSystem.IsAndroidVersionAtLeast(30)
            ? FeedbackConstants.Confirm
            : FeedbackConstants.LongPress;

        view.PerformHapticFeedback(fallback);
    }

    partial void DisposeNativeHaptic()
    {
        _vibrator = null;
        _useComposition = false;
    }

    #endregion
}