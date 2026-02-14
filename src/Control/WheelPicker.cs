using Microsoft.Maui.Controls.Shapes;
using SBC.PanContainer;
using SBC.WheelPicker.Helpers;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Container = SBC.PanContainer.PanContainer;

namespace SBC.WheelPicker;

/// <summary>
/// Provides a vertically scrollable wheel-style picker control that displays a list of items and allows users to select
/// one by spinning or dragging the wheel. Supports animated transitions, visual effects, and customizable item
/// templates.
/// </summary>
/// <remarks>
/// WheelPicker is designed for scenarios where a compact, touch-friendly selection interface is needed,
/// such as date or value pickers. It supports looping (circular scrolling), haptic and sound feedback, and visual state
/// transitions for selected and non-selected items. The control can be customized via properties such as item template,
/// visible item count, and edge effects.
/// </remarks>
public partial class WheelPicker : Container, IDisposable
{
    /// <summary>Visual state name applied to non-selected items.</summary>
    public const string DefaultItemVisualState = "DefaultItem";

    /// <summary>Visual state name applied to the currently selected (center) item.</summary>
    public const string CurrentItemVisualState = "CurrentItem";

    private const string WheelSnapAnimationName = "WheelSnap";
    private const string WheelSpinToAnimationName = "WheelSpinTo";

    private const string SoundAssetFileName = "wheel_tick.wav";

    private const uint FrameRateMs = 16; // ~60 FPS

    // ── Velocity-adaptive feedback ──────────────────────────────────
    //
    // Thresholds derived from FlingVelocityThreshold (DIPs/sec):
    //   <= 1× threshold -> full intensity — deliberate scroll, each notch distinct
    //   1×–4× threshold -> linear ramp 1.0 -> 0.0 — notches blur
    //   >= 4× threshold -> skip — wheel spinning freely
    //
    // With default 220 DIPs/sec:
    //   full at <= 220 DIPs/sec (≈ 5 items/sec)
    //   skip at >= 880 DIPs/sec (≈ 20 items/sec)
    private const double SpinFreeMultiplier = 4.0;
    private const double SnapSoundVolume = 0.25;
    private const double TickSoundVolumeMin = 0.05;
    private const double TickSoundVolumeMax = 0.20;

    private const double MaxTiltAngle = 90.0;

    // Edge bend tightness calculation.
    private const double MinBendTightness = 0.15;
    private const double MaxBendTightness = 0.40;

    /// How much velocity influences the snap target after a pan release.
    private const double SnapProjectionFactor = 0.12;

    // ----- VisualStateManager optimizations -----

    // Interned strings to allow reference equality for state names
    private static readonly string CurrentStateInterned = string.Intern(CurrentItemVisualState);
    private static readonly string DefaultStateInterned = string.Intern(DefaultItemVisualState);

    /// <summary>
    /// Sentinel BindingContext for ghost slots. In MAUI, setting BindingContext
    /// to null enables inheritance from the parent, so ghost slots would bind
    /// to the page's ViewModel and display its ToString(). A non-null sentinel
    /// with empty ToString() prevents both inheritance and visible content.
    /// </summary>
    private sealed class GhostContextType
    {
        public override string ToString() => string.Empty;
    }

    private static readonly object GhostContext = new GhostContextType();

    private readonly Grid _itemsRoot;
    private readonly VerticalStackLayout _itemsHost;

    private INotifyCollectionChanged? _observableSource;

    // Virtual center index — fractional, can exceed [0, Count-1] during fling/bounce.
    private double _virtualCenterIndex;

    private readonly Queue<View> _viewPool = new();

    // Suppression flags to prevent re-entrant property callbacks.
    private bool _suppressSelectedIndexCallback;
    private bool _suppressSelectedItemCallback;

    // Cache to avoid rebinding items every frame.
    private int _lastBaseCenterRawIndex = int.MinValue;
    private int _lastItemsCount = -1;
    private bool _lastLoopFlag = true; // matches Loop default

    // ----- Initialization state -----

    // True until the control has received a valid size and completed first visual update.
    // While true, animations and feedback are suppressed.
    private bool _isFirstAppearance = true;

    // True while processing ItemsSource change. Prevents redundant work during property sync.
    private bool _isLoadingItems;

    // Tracks whether we've had at least one non-zero layout pass.
    private bool _hasValidSize;

    // Pending overlay to apply once control has a valid size.
    private KeyValuePair<View?, View?>? _pendingOverlay;

    private FlingDirection _flingDirection;

    // Current scroll speed in DIPs/sec — updated from both pan and inertia
    // handlers. Used by ApplyFeedbacks and ComputeVelocityMatchedDuration.
    private double _currentScrollSpeed;

    private bool _disposed;

    // ----- Mouse-wheel snap scheduling (shared across Mac/Windows platforms) -----
    private long _mouseWheelLastEventTimestamp;
    private bool _mouseWheelTimerRunning;

    // Cached visible height — accounts for curvature compression.
    // Updated whenever VIC, ItemHeight, or curvature params change.
    private double _cachedVisibleHeight;

    // Dedup guard — ensures each actual change fires exactly once,
    // regardless of which path triggered it (scroll, snap, binding, code-behind).
    private int _lastNotifiedIndex = -1;
    private object? _lastNotifiedItem;

    private bool HasItems => ItemsSource?.Count > 0;

    /// <summary>Initializes a new instance of the <see cref="WheelPicker"/> class.</summary>
    public WheelPicker()
    {
        _itemsHost = new() { Spacing = 0 };

        _itemsRoot = new() { IsClippedToBounds = true };
        _itemsRoot.Children.Add(_itemsHost);

        base.AllowedDirections = AllowedPanDirections.Vertical;
        base.DeferToChildGestures = false;
        FlingVelocityThreshold = 220;
        InertiaMinVelocity = 30;
        InertiaDeceleration = 1800;
        IsClippedToBounds = true;

        Children.Add(_itemsRoot);
    }

    #region Events

    /// <summary>
    /// Occurs when <see cref="SelectedIndex"/> changes as a result of user interaction or programmatic update.
    /// </summary>
    /// <remarks>
    /// The event args carry the previous and current index values as <see cref="SelectionChangedEventArgs.PreviousSelection"/>
    /// and <see cref="SelectionChangedEventArgs.CurrentSelection"/> (boxed <see langword="int"/>).
    /// </remarks>
    public event EventHandler<SelectionChangedEventArgs>? SelectedIndexChanged;

    /// <summary>
    /// Occurs when <see cref="SelectedItem"/> changes as a result of user interaction or programmatic update.
    /// </summary>
    /// <remarks>
    /// The event args carry the previous and current item values as <see cref="SelectionChangedEventArgs.PreviousSelection"/>
    /// and <see cref="SelectionChangedEventArgs.CurrentSelection"/>.
    /// </remarks>
    public event EventHandler<SelectionChangedEventArgs>? SelectedItemChanged;

    #endregion

    /// <inheritdoc/>
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler != null)
        {
            InitializeMouseWheelHandling();
            InitializeSoundFeedbackHandling();
            InitializeNativeHaptic();
        }
        else
        {
            Dispose();
        }
    }

    /// <inheritdoc/>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0 || height <= 0)
            return;

        bool wasFirstLayout = !_hasValidSize;
        _hasValidSize = true;

        if (_pendingOverlay != null)
        {
            ApplyOverlayInternal(_pendingOverlay.Value.Key, _pendingOverlay.Value.Value);
            _pendingOverlay = null;
        }

        UpdateVisualFromVirtualIndex();

        if (wasFirstLayout && _isFirstAppearance)
        {
            _isFirstAppearance = false;

            // Sync the event dedup cache with the initial selection so the
            // first real event reports accurate old values.
            _lastNotifiedIndex = SelectedIndex;
            _lastNotifiedItem = SelectedItem;
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled))
        {
            if (!IsEnabled)
            {
                CancelAnimations();

                IsDragging = false;
                IsSpinning = false;
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnPanning(PanEventArgs e)
    {
        if (!HasItems || ItemHeight <= 0)
            return;

        base.OnPanning(e);

        switch (e.Status)
        {
            case PanGestureStatus.Started:

                _flingDirection = FlingDirection.None;
                _currentScrollSpeed = 0;

                IsDragging = true;
                IsSpinning = true;

                CancelAnimations();
                break;

            case PanGestureStatus.Running:
                _currentScrollSpeed = e.Speed;
                ApplyScrollDelta(e.DeltaY, fromInertia: false);
                break;

            case PanGestureStatus.Completed:
            case PanGestureStatus.Canceled:

                IsDragging = false;

                if (!HasItems)
                {
                    IsSpinning = false;
                    return;
                }

                if (!e.IsFling)
                {
                    SnapAfterPan(e.ReleaseVelocityY);
                }
                else
                {
                    if (!Loop)
                    {
                        int last = ItemsSource!.Count - 1;
                        _virtualCenterIndex = Math.Clamp(_virtualCenterIndex, 0, last);

                        if (_virtualCenterIndex == 0 || _virtualCenterIndex == last)
                        {
                            CancelAnimations();
                            SnapToCurrentSelection(animated: false);
                        }
                    }

                    _flingDirection = e.FlingDirection;
                }

                break;
        }
    }

    /// <inheritdoc/>
    protected override void OnInertia(InertiaEventArgs e)
    {
        base.OnInertia(e);

        if (!HasItems || ItemHeight <= 0)
            return;

        switch (e.Status)
        {
            case InertiaStatus.Started:
                IsSpinning = true;
                CancelAnimations();
                break;

            case InertiaStatus.Running:
                {
                    _currentScrollSpeed = e.Speed;

                    bool hitEdge = ApplyScrollDelta(e.DeltaY, fromInertia: true);
                    if (hitEdge)
                    {
                        e.Cancel = true;
                    }

                    break;
                }

            case InertiaStatus.Canceled:
                SnapToCurrentSelection(animated: false);
                break;

            case InertiaStatus.Completed:
                SnapAfterInertia();
                break;
        }
    }

    /// <summary>
    /// Scrolls the wheel so that the item at the specified <paramref name="index"/> becomes centered and selected.
    /// </summary>
    /// <param name="index">
    /// The zero-based index of the target item within <see cref="ItemsSource"/>.
    /// </param>
    /// <remarks>
    /// This is a convenience overload that behaves the same as
    /// <see cref="SpinTo(int, bool)"/> with <c>animated</c> controlled by <see cref="IsSelectionAnimated"/>.
    /// </remarks>
    public void SpinTo(int index) => SpinTo(index, IsSelectionAnimated);

    /// <summary>
    /// Scrolls the wheel so that the item at the specified <paramref name="index"/> becomes centered and selected,
    /// optionally animating the transition.
    /// </summary>
    /// <param name="index">The zero-based index of the target item within <see cref="ItemsSource"/>.</param>
    /// <param name="animated"><c>true</c> to animate the scroll using a smooth easing curve; <c>false</c> to jump immediately to the target.</param>
    public void SpinTo(int index, bool animated = true)
    {
        if (!HasItems)
            return;

        int count = ItemsSource!.Count;

        index = Math.Clamp(index, 0, count - 1);
        int logicalIndex = index;

        double start = _virtualCenterIndex;
        double target = GetNearestVirtualIndexFor(index);
        double delta = target - start;

        bool shouldAnimate = animated && !_isFirstAppearance;

        if (!shouldAnimate || Math.Abs(delta) < 0.001)
        {
            _virtualCenterIndex = target;
            UpdateVisualFromVirtualIndex();
            SetSelectionSilently(logicalIndex, ItemsSource[logicalIndex]);

            if (!_isFirstAppearance)
                ApplyFeedbacks(isSnap: true);

            return;
        }

        CancelAnimations();

        IsSpinning = true;
        IsDragging = false;

        double distance = Math.Abs(delta);
        uint lengthMs = (uint)Math.Clamp(120 + distance * 60, 120, 250);

        var animation = new Animation(t =>
        {
            _virtualCenterIndex = start + delta * t;
            UpdateVisualFromVirtualIndex();
        });

        animation.Commit(
            this,
            WheelSpinToAnimationName,
            rate: FrameRateMs,
            length: lengthMs,
            easing: Easing.SinOut,
            finished: (v, c) =>
            {
                if (!HasItems)
                {
                    IsSpinning = false;
                    return;
                }

                // Re-read count — ItemsSource may have changed during the animation.
                int safeIndex = Math.Clamp(logicalIndex, 0, ItemsSource!.Count - 1);

                _virtualCenterIndex = GetNearestVirtualIndexFor(safeIndex);

                UpdateVisualFromVirtualIndex();
                SetSelectionSilently(safeIndex, ItemsSource[safeIndex]);

                ApplyFeedbacks(isSnap: true);

                if (!IsDragging)
                    IsSpinning = false;
            });
    }

    /// <summary>
    /// Scrolls the wheel so that the specified <paramref name="item"/> becomes centered and selected.
    /// </summary>
    /// <param name="item">
    /// The item to select from <see cref="ItemsSource"/>.
    /// </param>
    /// <remarks>
    /// This is a convenience overload that behaves the same as
    /// <see cref="SpinTo(object?, bool)"/> with <c>animated</c> controlled by <see cref="IsSelectionAnimated"/>.
    /// </remarks>
    public void SpinTo(object? item) => SpinTo(item, IsSelectionAnimated);

    /// <summary>
    /// Scrolls the wheel so that the specified <paramref name="item"/> becomes centered and selected,
    /// optionally animating the transition.
    /// </summary>
    /// <param name="item">The item to select from <see cref="ItemsSource"/>.</param>
    /// <param name="animated"><c>true</c> to animate the scroll using a smooth easing curve; <c>false</c> to jump immediately to the target.</param>
    public void SpinTo(object? item, bool animated = true)
    {
        if (!HasItems || item == null)
            return;

        int index = FindItemIndex(item);
        if (index < 0)
            return;

        SpinTo(index, animated);
    }

    #region IDisposable

    /// <summary>Releases resources used by the <see cref="WheelPicker"/>.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            try
            {
                _observableSource?.CollectionChanged -= OnCollectionChanged;
                _observableSource = null;
            }
            catch { }

            try { DisposeMouseWheelHandling(); } catch { }
            try { DisposeSoundFeedbackHandling(); } catch { }
            try { DisposeNativeHaptic(); } catch { }

            CancelAnimations();
        }

        _disposed = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Platform-specific partial methods

    partial void InitializeMouseWheelHandling();

    partial void DisposeMouseWheelHandling();

    partial void InitializeSoundFeedbackHandling();

    partial void PlaySoundFeedback(double volume);

    partial void DisposeSoundFeedbackHandling();

    /// <summary>Prepare platform-specific haptic generators.</summary>
    partial void InitializeNativeHaptic();

    /// <summary>
    /// Light tick haptic for item crossings during scroll.
    /// <paramref name="intensity"/> is normalized 0.0→1.0: slow scroll = 1.0, fast = lower.
    /// </summary>
    partial void PerformTickHaptic(double intensity);

    /// <summary>
    /// Distinct, heavier haptic when the wheel settles on its final item.
    /// Feels like the wheel clicking into a detent after spinning.
    /// </summary>
    partial void PerformSnapHaptic();

    /// <summary>Release platform-specific haptic resources.</summary>
    partial void DisposeNativeHaptic();

    #endregion

    #region Layout & clipping

    private void UpdateItemsRootHeight()
    {
        if (ItemHeight <= 0)
            return;

#if WINDOWS
        // Windows clips based on layout bounds rather than visual/scaled bounds,
        // so compressed heights cause VIC items to be clipped. Use flat height.
        _cachedVisibleHeight = ItemHeight * VisibleItemsCount;
#else
        _cachedVisibleHeight = CalculateVisibleHeight();
#endif
        _itemsRoot.HeightRequest = _cachedVisibleHeight;

        // recenter wheel
        UpdateVisualFromVirtualIndex();
    }

    /// <summary>
    /// Computes the exact visual height needed to show <see cref="VisibleItemsCount"/> items,
    /// accounting for curvature compression and edge scaling.
    /// Uses the same math as the render loop so the clip boundary
    /// sits precisely at the visual edge of the outermost visible item.
    /// </summary>
    private double CalculateVisibleHeight()
    {
        double ih = ItemHeight;
        int vic = VisibleItemsCount;

        if (ih <= 0 || vic <= 0)
            return 0;

        double curvature = CurvatureFactor;

        // Flat mode or single item — no compression.
        if (curvature <= 0.0 || vic <= 1)
            return ih * vic;

        // Outermost VIC item distance from center (in slot units).
        double outerOffset = (vic - 1) / 2.0;
        double half = Math.Max(1.0, vic / 2.0);
        double t = outerOffset / half;   // norm ∈ [0, 1)

        if (t < 1e-6)
            return ih * vic;

        // ── Replicate render-loop math for the outermost VIC item ──

        double edgeItemScale = EdgeItemScale;
        double edgeItemTiltAngle = EdgeItemTiltAngle;
        double scaleRange = 1.0 - edgeItemScale;

        double tiltAndScalePow = PowerCacheHelper.TiltAndScalePow(t);
        double baseScale = 1.0 - scaleRange * tiltAndScalePow;
        double scale = 1.0 + (baseScale - 1.0) * curvature;

        double translationY = 0.0;
        double edgeTightness = ComputeEdgeBendTightness(edgeItemScale, edgeItemTiltAngle);
        double effectiveCompression = edgeTightness * curvature;

        if (effectiveCompression > 0.0)
        {
            double invScaleRange = scaleRange > 1e-6 ? 1.0 / scaleRange : 0.0;
            double invEdgeTilt = edgeItemTiltAngle > 0.0 ? 1.0 / edgeItemTiltAngle : 0.0;

            double baseTilt = edgeItemTiltAngle * tiltAndScalePow;
            double tiltDeg = baseTilt * curvature;

            double scaleNorm = Math.Clamp((scale - edgeItemScale) * invScaleRange, 0.0, 1.0);
            double tiltNorm = Math.Min(Math.Abs(tiltDeg), edgeItemTiltAngle) * invEdgeTilt;
            double edgeFactor = (1.0 - scaleNorm + tiltNorm) * 0.5;

            double compression = effectiveCompression * PowerCacheHelper.CompressionPow(edgeFactor);
            translationY = outerOffset * compression * ih;   // inward shift
        }

        // Visual center of the bottom outermost item, measured from wheel center.
        double visualCenter = outerOffset * ih - translationY;
        // Bottom edge, accounting for scale shrinkage from item center.
        double visualBottom = visualCenter + ih * scale / 2.0;

        // Symmetric top ↔ bottom.
        return 2.0 * visualBottom;
    }

    #endregion

    #region Edge bend tightness

    /// <summary>
    /// Combines scale + tilt contributions into a normalized factor [0, ~0.4]
    /// that controls how tightly items compress toward the edges.
    /// Returns 0 when all edge effects are disabled.
    /// </summary>
    private static double ComputeEdgeBendTightness(double edgeItemScale, double edgeItemTiltAngle)
    {
        double scaleBend = 1.0 - edgeItemScale;
        double tiltBend = edgeItemTiltAngle / MaxTiltAngle;
        double raw = (scaleBend + tiltBend) * 0.5;

        raw = Math.Clamp(raw, 0.0, 1.0);

        if (raw < 1e-6)
            return 0.0;

        raw = PowerCacheHelper.BendTightnessPow(raw);

        return MinBendTightness + (MaxBendTightness - MinBendTightness) * raw;
    }

    #endregion

    #region Snap duration helpers

    private static uint ComputeSnapDuration(double absDeltaItems)
    {
        const double minMs = 80.0;
        const double maxMs = 220.0;
        const double perItemMs = 140.0;

        double clampedDelta = Math.Clamp(absDeltaItems, 0.0, 1.5);
        double ms = minMs + perItemMs * clampedDelta;
        if (ms > maxMs)
            ms = maxMs;

        return (uint)ms;
    }

    /// <summary>
    /// Computes snap duration so the <see cref="Easing.SinOut"/> curve
    /// begins at the same speed the wheel was moving when inertia ended.
    /// For SinOut (= sin(t·π/2)), derivative at t=0 is π/2, so:
    /// duration = distance × (π/2) / V_terminal.
    /// </summary>
    private uint ComputeVelocityMatchedDuration(double absDeltaItems, double velocity)
    {
        if (velocity > 0 && ItemHeight > 0 && absDeltaItems > 0.001)
        {
            double distancePixels = absDeltaItems * ItemHeight;
            double durationSec = distancePixels * (Math.PI / 2.0) / velocity;

            return (uint)(durationSec * 1000.0);
        }

        return ComputeSnapDuration(absDeltaItems);
    }

    #endregion

    #region Selection helpers

    /// <summary>
    /// Computes the index that would be selected for the current _virtualCenterIndex,
    /// using SelectionThreshold, without modifying SelectedIndex/SelectedItem.
    /// </summary>
    private bool TryGetSelectionCandidateIndex(out int candidateIndex, out double distanceToCenter)
    {
        candidateIndex = -1;
        distanceToCenter = double.MaxValue;

        if (!HasItems)
            return false;

        double center = _virtualCenterIndex;

        if (Loop)
        {
            int roundedRaw = (int)Math.Round(center);
            int normalizedIndex = NormalizeIndex(roundedRaw);
            if (normalizedIndex == -1)
                return false;

            distanceToCenter = Math.Abs(center - roundedRaw);
            if (distanceToCenter > SelectionThreshold)
                return false;

            candidateIndex = normalizedIndex;
            return true;
        }
        else
        {
            double clamped = Math.Clamp(center, 0, ItemsSource!.Count - 1);
            int rounded = (int)Math.Round(clamped);

            distanceToCenter = Math.Abs(center - rounded);
            if (distanceToCenter > SelectionThreshold)
                return false;

            candidateIndex = rounded;
            return true;
        }
    }

    private int FindItemIndex(object? value)
    {
        if (value == null || !HasItems)
            return -1;

        int count = ItemsSource!.Count;
        for (int i = 0; i < count; i++)
        {
            if (Equals(ItemsSource[i], value))
                return i;
        }

        return -1;
    }

    /// <summary>Sets SelectedIndex + SelectedItem without triggering their property-changed callbacks.</summary>
    private void SetSelectionSilently(int index, object? item)
    {
        _suppressSelectedIndexCallback = true;
        _suppressSelectedItemCallback = true;

        SelectedIndex = index;
        SelectedItem = item;

        _suppressSelectedIndexCallback = false;
        _suppressSelectedItemCallback = false;

        NotifySelectionChangedIfNeeded();
    }

    private void NotifySelectionChangedIfNeeded()
    {
        if (_isFirstAppearance || _isLoadingItems)
            return;

        int currentIndex = SelectedIndex;
        object? currentItem = SelectedItem;

        if (currentIndex != _lastNotifiedIndex)
        {
            int oldIndex = _lastNotifiedIndex;
            _lastNotifiedIndex = currentIndex;
            RaiseSelectedIndexChanged(oldIndex, currentIndex);
        }

        if (!Equals(currentItem, _lastNotifiedItem))
        {
            object? oldItem = _lastNotifiedItem;
            _lastNotifiedItem = currentItem;
            RaiseSelectedItemChanged(oldItem, currentItem);
        }
    }

    private void RaiseSelectedIndexChanged(int oldIndex, int newIndex)
    {
        var eventArgs = new SelectionChangedEventArgs(oldIndex, newIndex);

        SelectedIndexChanged?.Invoke(this, eventArgs);

        var param = SelectedIndexChangedCommandParameter ?? eventArgs;
        if (SelectedIndexChangedCommand?.CanExecute(param) == true)
            SelectedIndexChangedCommand?.Execute(param);
    }

    private void RaiseSelectedItemChanged(object? oldItem, object? newItem)
    {
        var eventArgs = new SelectionChangedEventArgs(oldItem, newItem);
        SelectedItemChanged?.Invoke(this, eventArgs);

        var param = SelectedItemChangedCommandParameter ?? eventArgs;
        if (SelectedItemChangedCommand?.CanExecute(param) == true)
            SelectedItemChangedCommand?.Execute(param);
    }

    /// <summary>
    /// Unconditionally sets selection from a virtual target position,
    /// firing feedback only when the selection actually changed.
    /// </summary>
    private void FinalizeSelection(double virtualTarget)
    {
        if (!HasItems)
            return;

        int logicalIndex = NormalizeIndex((int)Math.Round(virtualTarget));
        if (logicalIndex < 0 || logicalIndex >= ItemsSource!.Count)
            return;

        bool changed = logicalIndex != SelectedIndex;

        SetSelectionSilently(logicalIndex, ItemsSource[logicalIndex]);

        if (changed)
            ApplyFeedbacks(isSnap: true);
    }

    /// <summary>Threshold-based selection update used while the wheel is still moving.</summary>
    private void UpdateSelectionWhileScrolling()
    {
        if (!HasItems)
            return;

        if (!TryGetSelectionCandidateIndex(out int candidateIndex, out _))
            return;

        if (candidateIndex == SelectedIndex)
            return;

        SetSelectionSilently(candidateIndex, ItemsSource![candidateIndex]);

        ApplyFeedbacks();
    }

    #endregion

    #region View transforms

    private static void ResetViewTransforms(View view)
    {
        try
        {
            view.AnchorX = 0.5;
            view.AnchorY = 0.5;
            view.SetScale(1);
            view.SetOpacity(1);
            view.SetRotationX(0);
            view.SetTranslationY(0);
            view.Clip = null;
        }
        catch { }
    }

    /// <summary>
    /// Toggles slot rendering via Clip. Used for sentinel hiding on Windows
    /// where sentinels sit at the container boundary and leak through.
    /// NOT used for ghosts — Clip = Rect.Zero can break StackLayout
    /// measurement on some platforms; ghosts use Opacity = 0 instead.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HideOrShowSlot(View child, bool hidden)
    {
        if (hidden)
        {
            child.Clip ??= new RectangleGeometry { Rect = Rect.Zero };
        }
        else
        {
            if (child.Clip != null)
                child.Clip = null;
        }
    }

    #endregion

    #region Items management

    private void RebuildItems()
    {
        // Recycle current children into the pool
        for (int i = 0; i < _itemsHost.Children.Count; i++)
        {
            if (_itemsHost.Children[i] is View child)
            {
                try { child.SizeChanged -= OnItemSizeChanged; } catch { }
                child.BindingContext = GhostContext;

                if (_viewPool.Count <= VisibleItemsCount)
                    _viewPool.Enqueue(child);
            }
        }

        _itemsHost.Children.Clear();

        _lastBaseCenterRawIndex = int.MinValue;
        _lastItemsCount = -1;

        if (VisibleItemsCount <= 0)
            return;

        int itemsCount = VisibleItemsCount + 2;

        for (int i = 0; i < itemsCount; i++)
        {
            View? view = null;

            if (_viewPool.Count > 0)
                view = _viewPool.Dequeue();
            else if (ItemTemplate != null)
                view = ItemTemplate.CreateContent() as View;

            if (view == null)
                continue;

            // Single reset per view — not at enqueue AND dequeue.
            ResetViewTransforms(view);

            if (ItemHeight > 0)
            {
                view.HeightRequest = ItemHeight;
            }
            else
            {
                view.SizeChanged += OnItemSizeChanged;
            }

            _itemsHost.Children.Add(view);
        }

        if (_hasValidSize)
        {
            UpdateVisualFromVirtualIndex();
        }
    }

    private void OnItemSizeChanged(object? sender, EventArgs e)
    {
        if (sender is not View v)
            return;

        if (v.Height <= 0)
            return;

        if (ItemHeight <= 0)
        {
            ItemHeight = v.Height;
            UpdateItemsRootHeight();

            for (int i = 0; i < _itemsHost.Children.Count; i++)
            {
                if (_itemsHost.Children[i] is View child)
                    child.HeightRequest = ItemHeight;
            }

            if (_hasValidSize)
            {
                UpdateVisualFromVirtualIndex();
            }
        }

        v.SizeChanged -= OnItemSizeChanged;
    }

    private void SyncStateFromPublicProps()
    {
        if (!HasItems)
        {
            _virtualCenterIndex = 0;
            SetSelectionSilently(-1, null);

            _lastBaseCenterRawIndex = int.MinValue;
            _lastItemsCount = -1;

            return;
        }

        int count = ItemsSource!.Count;
        int index = -1;

        if (SelectedItem != null)
            index = FindItemIndex(SelectedItem);

        if (index < 0 && SelectedIndex >= 0 && SelectedIndex < count)
            index = SelectedIndex;

        if (index < 0)
            index = 0;

        _virtualCenterIndex = index;
        SetSelectionSilently(index, ItemsSource[index]);

        _lastBaseCenterRawIndex = int.MinValue;
        _lastItemsCount = -1;
    }

    #endregion

    #region Visual update (hot path — every frame during scroll)

    private void UpdateVisualFromVirtualIndex()
    {
        if (!HasItems)
            return;
        if (_itemsHost.Children.Count == 0 || VisibleItemsCount <= 0)
            return;

        BatchBegin();

        try
        {
            var items = ItemsSource!;
            var children = _itemsHost.Children;
            int count = items.Count;
            int slotCount = children.Count;
            int centerSlot = slotCount / 2;

            double centerIndex = _virtualCenterIndex;
            int baseCenterRawIndex = (int)Math.Round(centerIndex);
            double scrollOffset = centerIndex - baseCenterRawIndex;

            bool needRebind =
                baseCenterRawIndex != _lastBaseCenterRawIndex ||
                count != _lastItemsCount ||
                Loop != _lastLoopFlag;

            if (needRebind)
            {
                for (int i = 0; i < slotCount; i++)
                {
                    if (children[i] is not View child)
                        continue;

                    int slotOffset = i - centerSlot;
                    int rawIndex = baseCenterRawIndex + slotOffset;

                    if (Loop)
                    {
                        int itemIndex = NormalizeIndex(rawIndex);
                        child.BindingContext = (itemIndex == -1) ? GhostContext : items[itemIndex];
                    }
                    else
                    {
                        if (rawIndex < 0 || rawIndex >= count)
                            child.BindingContext = GhostContext;
                        else
                            child.BindingContext = items[rawIndex];
                    }

                    // Hide ghosts with opacity — Clip = Rect.Zero can break
                    // StackLayout measurement on some platforms. GhostContext
                    // prevents BindingContext inheritance so no text leaks.
                    child.SetOpacity(child.BindingContext == GhostContext ? 0 : 1);
                }

                _lastBaseCenterRawIndex = baseCenterRawIndex;
                _lastItemsCount = count;
                _lastLoopFlag = Loop;
            }

            if (ItemHeight > 0)
            {
                double visibleH = _cachedVisibleHeight > 0
                    ? _cachedVisibleHeight
                    : ItemHeight * VisibleItemsCount;
                double baseTranslation = visibleH / 2.0 - (centerSlot + 0.5) * ItemHeight;
                double dynamicOffset = -scrollOffset * ItemHeight;
                _itemsHost.TranslationY = baseTranslation + dynamicOffset;
            }

            UpdateItemVisualStates();
        }
        finally
        {
            BatchCommit();
        }
    }

    private void UpdateItemVisualStates()
    {
        var children = _itemsHost.Children;
        int slotCount = children.Count;
        if (slotCount == 0 || !HasItems)
            return;

        int slotCenter = slotCount / 2;
        double half = Math.Max(1.0, VisibleItemsCount / 2.0);
        double invHalf = 1.0 / half;

        double roundedCenter = Math.Round(_virtualCenterIndex);
        double fractional = _virtualCenterIndex - roundedCenter;
        double visualCenterSlot = slotCenter + fractional;

        // Determine which slot is the selected center item by index math.
        // Avoids per-slot Equals() boxing on the hot path.
        int centerSlotIdx = -1;
        if (TryGetSelectionCandidateIndex(out int candidateIndex, out _))
        {
            int baseCenterRaw = (int)roundedCenter;
            int count = ItemsSource!.Count;

            for (int i = 0; i < slotCount; i++)
            {
                int rawIndex = baseCenterRaw + (i - slotCenter);
                int mapped = Loop
                    ? NormalizeIndex(rawIndex)
                    : (rawIndex >= 0 && rawIndex < count ? rawIndex : -1);

                if (mapped == candidateIndex)
                {
                    centerSlotIdx = i;
                    break;
                }
            }
        }

        // Cache BindableProperty reads — each GetValue() has overhead.
        double curvature = CurvatureFactor;
        double edgeItemScale = EdgeItemScale;
        double edgeItemOpacity = EdgeItemOpacity;
        double edgeItemTiltAngle = EdgeItemTiltAngle;
        double itemHeight = ItemHeight;

#if WINDOWS
        // On Windows, HeightRequest = ih × VIC, so sentinels sit exactly
        // at the clip boundary and leak through. Hide them via Clip at rest
        // (Clip is safe for sentinels — they're real items, no layout impact),
        // show during scroll for continuity.
        bool isScrolling = Math.Abs(fractional) > 0.01;
        int lastSlot = slotCount - 1;
#endif

        // --- flat mode ---
        if (curvature <= 0.0)
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (children[i] is not View child)
                    continue;

                bool isGhost = child.BindingContext == GhostContext;

#if WINDOWS
                if (!isGhost)
                {
                    bool isSentinel = i == 0 || i == lastSlot;
                    HideOrShowSlot(child, isSentinel && !isScrolling);
                }
#endif

                // Ghosts stay at opacity 0 (set during rebind).
                // Only apply transforms to real items.
                if (!isGhost)
                {
                    child.SetScale(1);
                    child.SetOpacity(1);
                    child.SetRotationX(0);
                    child.SetTranslationY(0);
                    ApplyVisualState(child, i == centerSlotIdx);
                }
            }
            return;
        }

        // --- curved mode ---

        // Pre-compute loop-invariant values.
        double edgeTightness = ComputeEdgeBendTightness(edgeItemScale, edgeItemTiltAngle);
        double effectiveCompression = edgeTightness * curvature;
        double scaleRange = 1.0 - edgeItemScale;
        double invScaleRange = scaleRange > 1e-6 ? 1.0 / scaleRange : 0.0;
        double invEdgeTilt = edgeItemTiltAngle > 0.0 ? 1.0 / edgeItemTiltAngle : 0.0;
        bool hasCompression = itemHeight > 0 && effectiveCompression > 0.0;

        for (int i = 0; i < slotCount; i++)
        {
            if (children[i] is not View child)
                continue;

            // Ghost slots — opacity 0 from rebind, skip math.
            if (child.BindingContext == GhostContext)
                continue;

#if WINDOWS
            {
                bool isSentinel = i == 0 || i == lastSlot;
                HideOrShowSlot(child, isSentinel && !isScrolling);
            }
#endif

            double visualOffset = i - visualCenterSlot;
            double norm = Math.Clamp(visualOffset * invHalf, -1.0, 1.0);
            double t = Math.Abs(norm);

            double scale, opacity, tiltDeg;
            double translationY = 0.0;

            if (t < 1e-6)
            {
                scale = 1.0;
                opacity = 1.0;
                tiltDeg = 0.0;
            }
            else
            {
                double tiltAndScalePow = PowerCacheHelper.TiltAndScalePow(t);
                double opacityPow = PowerCacheHelper.OpacityPow(t);

                double baseScale = 1.0 - scaleRange * tiltAndScalePow;
                double baseOpacity = 1.0 - (1.0 - edgeItemOpacity) * opacityPow;
                double baseTilt = edgeItemTiltAngle * tiltAndScalePow;

                scale = 1.0 + (baseScale - 1.0) * curvature;
                opacity = 1.0 + (baseOpacity - 1.0) * curvature;
                tiltDeg = baseTilt * curvature;

                if (hasCompression)
                {
                    double scaleNorm = Math.Clamp((scale - edgeItemScale) * invScaleRange, 0.0, 1.0);
                    double tiltNorm = Math.Min(Math.Abs(tiltDeg), edgeItemTiltAngle) * invEdgeTilt;
                    double edgeFactor = (1.0 - scaleNorm + tiltNorm) * 0.5;

                    double compression = effectiveCompression * PowerCacheHelper.CompressionPow(edgeFactor);
                    translationY = -visualOffset * compression * itemHeight;
                }
            }

            child.SetScale(scale);
            child.SetOpacity(opacity);
            child.SetRotationX((norm < 0 ? 1.0 : -1.0) * tiltDeg);
            child.SetTranslationY(translationY);

            ApplyVisualState(child, i == centerSlotIdx);
        }
    }

    private static void ApplyVisualState(View child, bool isCenter)
    {
        var targetState = isCenter ? CurrentStateInterned : DefaultStateInterned;
        var prevState = GetPreviousVisualState(child);

        // Use reference equality for interned state strings — much faster than ordinal comparison
        if (!ReferenceEquals(prevState, targetState))
        {
            VisualStateManager.GoToState(child, targetState);
            SetPreviousVisualState(child, targetState);
        }
    }

    #endregion

    #region Virtual index helpers

    private double GetNearestVirtualIndexFor(int targetIndex)
    {
        if (!HasItems)
            return _virtualCenterIndex;

        int count = ItemsSource!.Count;

        targetIndex = Math.Clamp(targetIndex, 0, count - 1);

        if (!Loop)
            return targetIndex;

        double start = _virtualCenterIndex;
        double k = Math.Round((start - targetIndex) / count);
        return k * count + targetIndex;
    }

    private int NormalizeIndex(int index)
    {
        if (!HasItems)
            return -1;

        var count = ItemsSource!.Count;

        if (Loop)
        {
            var m = index % count;
            if (m < 0) m += count;
            return m;
        }

        if (index < 0) return 0;
        if (index >= count) return count - 1;
        return index;
    }

    #endregion

    #region Unified snap infrastructure

    /// <summary>Core snap method — all snap paths funnel through here.</summary>
    /// <remarks>Animates _virtualCenterIndex from current position to target, then calls FinalizeSelection on completion.</remarks>
    private void SnapTo(double target, uint durationMs, Easing easing)
    {
        if (!HasItems)
            return;

        double start = _virtualCenterIndex;
        double delta = target - start;

        this.AbortAnimation(WheelSnapAnimationName);

        if (Math.Abs(delta) < 0.001)
        {
            _virtualCenterIndex = target;
            UpdateVisualFromVirtualIndex();
            FinalizeSelection(target);

            if (!IsDragging)
                IsSpinning = false;
            return;
        }

        IsSpinning = true;

        var animation = new Animation(t =>
        {
            _virtualCenterIndex = start + delta * t;
            UpdateVisualFromVirtualIndex();
        });

        animation.Commit(
            this,
            WheelSnapAnimationName,
            rate: FrameRateMs,
            length: durationMs,
            easing: easing,
            finished: (v, c) =>
            {
                _virtualCenterIndex = target;
                UpdateVisualFromVirtualIndex();
                FinalizeSelection(target);

                if (!IsDragging)
                    IsSpinning = false;
            });
    }

    /// <summary>Snap to SelectedIndex (or nearest index as fallback).</summary>
    /// <remarks>Used after edge-bounce cancel and programmatic scroll.</remarks>
    private void SnapToCurrentSelection(bool animated = true)
    {
        if (!HasItems)
            return;

        int selectedIndex = SelectedIndex;
        int count = ItemsSource!.Count;
        double start = _virtualCenterIndex;
        double target;

        if (selectedIndex >= 0 && selectedIndex < count)
        {
            target = Loop
                ? GetNearestVirtualIndexFor(selectedIndex)
                : selectedIndex;
        }
        else
        {
            target = Math.Round(start);
            if (!Loop)
                target = Math.Clamp(target, 0, count - 1);
        }

        bool shouldAnimate = animated && !_isFirstAppearance;

        if (!shouldAnimate)
        {
            _virtualCenterIndex = target;
            UpdateVisualFromVirtualIndex();
            FinalizeSelection(target);

            if (!IsDragging)
                IsSpinning = false;
            return;
        }

        double absDelta = Math.Abs(target - start);
        SnapTo(target, ComputeSnapDuration(absDelta), Easing.CubicOut);
    }

    /// <summary> Micro-snap after inertia completes — continues in the fling direction.</summary>
    private void SnapAfterInertia()
    {
        if (!HasItems)
            return;

        double start = _virtualCenterIndex;
        int count = ItemsSource!.Count;

        double target = _flingDirection switch
        {
            FlingDirection.Up => Math.Ceiling(start),
            FlingDirection.Down => Math.Floor(start),
            _ => Math.Round(start)
        };

        if (!Loop)
            target = Math.Clamp(target, 0, count - 1);

        double absDelta = Math.Abs(target - start);
        uint duration = ComputeVelocityMatchedDuration(absDelta, _currentScrollSpeed);

        SnapTo(target, duration, Easing.SinOut);
    }

    /// <summary>Snap after the user lifts their finger without flinging.</summary>
    /// <remarks>Projects the release velocity forward, then rounds to the nearest item.</remarks>
    private void SnapAfterPan(double releaseVelocityY)
    {
        if (!HasItems || ItemHeight <= 0)
            return;

        double start = _virtualCenterIndex;
        int count = ItemsSource!.Count;

        double nearest = Math.Round(start);
        double distanceToNearest = Math.Abs(start - nearest);

        double target;

        if (distanceToNearest <= SelectionThreshold)
        {
            target = nearest;
        }
        else
        {
            double velocityInItems = -releaseVelocityY / ItemHeight;
            double projected = start + velocityInItems * SnapProjectionFactor;
            target = Math.Round(projected);
        }

        if (!Loop)
            target = Math.Clamp(target, 0, count - 1);

        double absDelta = Math.Abs(target - start);

        // Duration: velocity-matched for fast releases, distance-based for slow ones.
        double absVelocity = Math.Abs(releaseVelocityY);
        uint duration;

        if (absVelocity > 1.0)
        {
            duration = Math.Clamp(
                ComputeVelocityMatchedDuration(absDelta, absVelocity),
                80, 250);
        }
        else
        {
            duration = (uint)Math.Clamp(100 + absDelta * 120, 100, 220);
        }

        SnapTo(target, duration, Easing.CubicOut);
    }

    #endregion

    #region Mouse-wheel shared logic for Mac/Windows

    /// <summary>Called from platform-specific mouse/trackpad handlers.</summary>
    /// <remarks>deltaItems is already in "number of items" units.</remarks> 
    private void ApplyMouseWheelDelta(double deltaItems)
    {
        if (!IsEnabled || !IsSwipeEnabled || !HasItems)
            return;

        if (Math.Abs(deltaItems) < 0.01)
            return;

        double candidate = _virtualCenterIndex + deltaItems;

        if (!Loop)
            candidate = Math.Clamp(candidate, 0, ItemsSource!.Count - 1);

        _virtualCenterIndex = candidate;

        CancelAnimations();
        UpdateVisualFromVirtualIndex();
        UpdateSelectionWhileScrolling();

        IsDragging = false;
        IsSpinning = true;

        ScheduleMouseWheelSnap();
    }

    /// <summary> After the last mouse-wheel event, waits ~120 ms of inactivity then snaps.</summary>
    private void ScheduleMouseWheelSnap()
    {
        _mouseWheelLastEventTimestamp = Stopwatch.GetTimestamp();

        if (_mouseWheelTimerRunning)
            return;

        _mouseWheelTimerRunning = true;

        Dispatcher.StartTimer(TimeSpan.FromMilliseconds(80), () =>
        {
            double elapsedMs = (Stopwatch.GetTimestamp() - _mouseWheelLastEventTimestamp)
                               * 1000.0 / Stopwatch.Frequency;

            if (elapsedMs >= 120.0)
            {
                _mouseWheelTimerRunning = false;
                SnapToCurrentSelection();

                return false;
            }

            return true;
        });
    }

    #endregion

    #region Scroll delta

    private bool ApplyScrollDelta(double deltaPixels, bool fromInertia)
    {
        if (deltaPixels == 0 ||
            ItemHeight <= 0 ||
            !HasItems)
        {
            return false;
        }

        double deltaItems = -(deltaPixels / ItemHeight);
        if (Math.Abs(deltaItems) < double.Epsilon)
            return false;

        double candidate = _virtualCenterIndex + deltaItems;
        bool hitEdge = false;

        if (!Loop)
        {
            double min = 0;
            double max = ItemsSource!.Count - 1;

            if (candidate < min)
            {
                double overshoot = min - candidate;
                candidate = min - overshoot * 0.35;
                hitEdge = true;
            }
            else if (candidate > max)
            {
                double overshoot = candidate - max;
                candidate = max + overshoot * 0.35;
                hitEdge = true;
            }
        }

        _virtualCenterIndex = candidate;
        UpdateVisualFromVirtualIndex();
        UpdateSelectionWhileScrolling();

        return fromInertia && hitEdge;
    }

    #endregion

    #region Collection change handlers

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncStateFromPublicProps();
        RebuildItems();
    }

    private void OnItemsSourceChangedInternal(IList? newSource)
    {
        if (_observableSource != null)
        {
            _observableSource.CollectionChanged -= OnCollectionChanged;
            _observableSource = null;
        }

        if (newSource is INotifyCollectionChanged incc)
        {
            _observableSource = incc;
            _observableSource.CollectionChanged += OnCollectionChanged;
        }

        IsDragging = false;
        IsSpinning = false;

        _isLoadingItems = true;

        _lastBaseCenterRawIndex = int.MinValue;
        _lastItemsCount = -1;

        SyncStateFromPublicProps();
        RebuildItems();

        _isLoadingItems = false;
    }

    #endregion

    #region Property-changed internals

    /// <summary>Common path for SelectedIndex/SelectedItem changes.</summary>
    /// <remarks>Navigates to the given logical index, with or without animation.</remarks>
    private void NavigateToIndex(int index)
    {
        CancelAnimations();

        if (_isFirstAppearance)
        {
            _virtualCenterIndex = GetNearestVirtualIndexFor(index);
            if (_hasValidSize)
                UpdateVisualFromVirtualIndex();
            return;
        }

        if (IsSelectionAnimated)
        {
            SpinTo(index, animated: true);
        }
        else
        {
            _virtualCenterIndex = GetNearestVirtualIndexFor(index);
            UpdateVisualFromVirtualIndex();
            ApplyFeedbacks(isSnap: true);
        }
    }

    private void OnSelectedIndexChangedInternal(int newIndex)
    {
        if (_suppressSelectedIndexCallback || !HasItems || _isLoadingItems)
            return;

        int count = ItemsSource!.Count;
        int index = NormalizeIndex(newIndex);
        if (index < 0 || index >= count)
            return;

        _suppressSelectedItemCallback = true;
        SelectedItem = ItemsSource[index];
        _suppressSelectedItemCallback = false;

        NavigateToIndex(index);
    }

    private void OnSelectedItemChangedInternal(object newValue)
    {
        if (_suppressSelectedItemCallback || !HasItems || _isLoadingItems)
            return;

        int index = FindItemIndex(newValue);
        if (index < 0)
            return;

        _suppressSelectedIndexCallback = true;
        SelectedIndex = index;
        _suppressSelectedIndexCallback = false;

        NavigateToIndex(index);
    }

    private void OnOverlayChangedInternal(View? oldOverlay, View? newOverlay)
    {
        if (_hasValidSize)
        {
            ApplyOverlayInternal(oldOverlay, newOverlay);
        }
        else
        {
            _pendingOverlay = new(oldOverlay, newOverlay);
        }
    }

    private void ApplyOverlayInternal(View? oldOverlay, View? newOverlay)
    {
        if (_itemsRoot == null) return;

        if (oldOverlay != null && _itemsRoot.Children.Contains(oldOverlay))
            _itemsRoot.Children.Remove(oldOverlay);

        if (newOverlay != null)
        {
            newOverlay.InputTransparent = true;
            _itemsRoot.Children.Add(newOverlay);
        }
    }

    private void OnLoopChangedInternal()
    {
        if (!HasItems)
            return;

        CancelAnimations();

        int idx = SelectedIndex;
        int count = ItemsSource!.Count;

        if (idx < 0 || idx >= count)
        {
            idx = NormalizeIndex((int)Math.Round(_virtualCenterIndex));
            if (idx < 0)
                idx = 0;
            if (idx >= count)
                idx = count - 1;
        }

        _virtualCenterIndex = idx;
        SetSelectionSilently(idx, ItemsSource[idx]);

        _lastBaseCenterRawIndex = int.MinValue;
        _lastItemsCount = -1;

        UpdateVisualFromVirtualIndex();
    }

    private void OnVisibleItemsCountChangedInternal()
    {
        RebuildItems();
        UpdateItemsRootHeight();
    }

    #endregion

    #region Feedback

    /// <summary>
    /// Fires haptic and/or sound feedback. Two modes:
    /// <list type="bullet">
    /// <item><b>Tick</b> (default): light feedback for each item crossing. Intensity
    ///   scales with scroll velocity — fast = light/skip, slow = full. Models a
    ///   physical detent wheel where individual notches blur at speed.</item>
    /// <item><b>Snap</b> (<paramref name="isSnap"/> = true): distinct, heavier feedback
    ///   when the wheel settles on its final item. Always fires (bypasses throttle).</item>
    /// </list>
    /// </summary>
    private void ApplyFeedbacks(bool isSnap = false)
    {
        if (_isFirstAppearance)
            return;

        // Derive thresholds from FlingVelocityThreshold (DIPs/sec).
        double speed = _currentScrollSpeed;
        double fullAt = FlingVelocityThreshold;
        double skipAt = FlingVelocityThreshold * SpinFreeMultiplier;

        // Normalized intensity: 1.0 at <= fullAt, ramps to 0.0 at skipAt.
        double intensity = isSnap
            ? 1.0
            : 1.0 - Math.Clamp((speed - fullAt) / (skipAt - fullAt), 0.0, 1.0);

        // Spinning freely — skip everything.
        if (!isSnap && intensity <= 0.0)
            return;

        if (HapticFeedback)
        {
            try
            {
                if (isSnap)
                    PerformSnapHaptic();
                else
                    PerformTickHaptic(intensity);
            }
            catch { }
        }

        if (SoundFeedback)
        {
            try
            {
                double volume = isSnap
                    ? SnapSoundVolume
                    : TickSoundVolumeMin + (TickSoundVolumeMax - TickSoundVolumeMin) * intensity;

                PlaySoundFeedback(volume);
            }
            catch { }
        }
    }

    #endregion

    #region Internal plumbing

    private void SetPanEnabled(bool enabled)
    {
        base.IsPanEnabled = enabled;
    }

    private void CancelAnimations()
    {
        try { CancelInertia(); } catch { }
        try { this.AbortAnimation(WheelSnapAnimationName); } catch { }
        try { this.AbortAnimation(WheelSpinToAnimationName); } catch { }
    }

    #endregion
}
