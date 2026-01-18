using Android.Media;
using Android.Views;
using Application = Android.App.Application;
using View = Android.Views.View;

namespace WheelPicker.Maui;

public partial class WheelPicker
{
    private static SoundPool? _soundPool;
    private static int _soundId;
    private static bool _loaded;
    private static bool _initStarted;
    private static readonly object _lock = new();
    private View? _platformView;

    #region Scroll Conflict Handling

    partial void InitializeScrollConflictHandling()
    {
        try
        {
            // detach old handler if we’re being re-attached
            if (_platformView != null)
            {
                _platformView.Touch -= OnPlatformTouch;
                _platformView = null;
            }

            if (Handler?.PlatformView is View v)
            {
                _platformView = v;
                _platformView.Touch += OnPlatformTouch;
            }
        }
        catch
        {
            // best-effort only
        }
    }

    private void OnPlatformTouch(object? sender, View.TouchEventArgs e)
    {
        if (sender is not View v)
            return;

        if (!IsEnabled || !IsSwipeEnabled)
            return;

        var action = e.Event?.ActionMasked ?? MotionEventActions.Cancel;

        switch (action)
        {
            case MotionEventActions.Down:
            case MotionEventActions.Move:
                // While dragging on the wheel, ask the parent (ScrollView, layout, etc.)
                // not to intercept this gesture.
                v.Parent?.RequestDisallowInterceptTouchEvent(true);
                break;

            case MotionEventActions.Up:
            case MotionEventActions.Cancel:
                // Let the parent intercept again for future gestures.
                v.Parent?.RequestDisallowInterceptTouchEvent(false);
                break;
        }

        // IMPORTANT: do NOT set e.Handled = true.
        // We want MAUI’s own gesture system (PanGestureRecognizer) to still receive the touch.
    }

    partial void DisposeScrollConflictHandling()
    {
        try
        {
            if (_platformView != null)
            {
                _platformView.Touch -= OnPlatformTouch;
                _platformView = null;
            }
        }
        catch { }
    }

    #endregion

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

    partial void PlaySoundFeedback()
    {
        if (!_loaded || _soundPool == null)
            return;

        float volume = (float)SoundVolume;

        _soundPool.Play(_soundId, volume, volume, 0, 0, 1f);
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
}