using Microsoft.Maui.Controls.Shapes;
using SBC.PanContainer;
using SBC.WheelPicker.Converters;
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

    #region Events

    /// <summary>
    /// Occurs when <see cref="SelectedIndex"/> changes as a result of user interaction or programmatic update.
    /// </summary>
    /// <remarks>
    /// The event args carry the previous and current index values as <see cref="IndexChangedEventArgs.PreviousIndex"/>
    /// and <see cref="IndexChangedEventArgs.CurrentIndex"/> (boxed <see langword="int"/>).
    /// </remarks>
    public event EventHandler<IndexChangedEventArgs>? SelectedIndexChanged;

    /// <summary>
    /// Occurs when <see cref="SelectedItem"/> changes as a result of user interaction or programmatic update.
    /// </summary>
    /// <remarks>
    /// The event args carry the previous and current item values as <see cref="ItemChangedEventArgs.PreviousItem"/>
    /// and <see cref="ItemChangedEventArgs.CurrentItem"/>.
    /// </remarks>
    public event EventHandler<ItemChangedEventArgs>? SelectedItemChanged;

    #endregion

    private const string WheelSnapAnimationName = "WheelSnap";
    private const string WheelSpinToAnimationName = "WheelSpinTo";

    private const string SoundAssetFileName = "wheel_tick.wav";

    private const uint FrameRateMs = 16; // ~60 FPS

    // --- Velocity-adaptive feedback ---
    private const double MaxVelocity = 2000.0;
    private const double TickSoundVolumeMin = 0.05;
    private const double TickSoundVolumeMax = 0.20;
    private const double MinFeedbackIntervalMs = 60;

    // feedback debounce
    private long _lastFeedbackTimestamp;

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
    private sealed class GhostContextType { public override string ToString() => string.Empty; }

    private static readonly object GhostContext = new GhostContextType();

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

    // Centered within whatever bounds the developer gives WheelPicker.
    private readonly RectangleGeometry _clipGeometry = new();

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

    private IList? _cachedItems;
    private int _cachedItemsCount;

    #region Helpers

    private int ItemsCount => _cachedItemsCount;
    private bool HasItems => _cachedItemsCount > 0;

    #endregion

    private static readonly IMultiValueConverter ItemStringFormatConverter = new ItemStringFormatMultiConverter();

    private static readonly RelativeBindingSource WheelPickerAncestor =
        new(RelativeBindingSourceMode.FindAncestor, typeof(WheelPicker), 1);

    private static readonly DataTemplate DefaultItemTemplate = new(() =>
    {
        var label = new Label();

        // Bind text using (item, ItemStringFormat) so format updates live without a rebuild.
        var mb = new MultiBinding { Converter = ItemStringFormatConverter };
        mb.Bindings.Add(new Binding("."));
        mb.Bindings.Add(new Binding(nameof(ItemStringFormat), source: WheelPickerAncestor));
        label.SetBinding(Label.TextProperty, mb);

        label.SetBinding(Label.TextColorProperty, new Binding(nameof(ItemTextColor), source: WheelPickerAncestor));
        label.SetBinding(Label.FontSizeProperty, new Binding(nameof(ItemFontSize), source: WheelPickerAncestor));
        label.SetBinding(Label.FontAttributesProperty, new Binding(nameof(ItemFontAttributes), source: WheelPickerAncestor));
        label.SetBinding(Label.FontFamilyProperty, new Binding(nameof(ItemFontFamily), source: WheelPickerAncestor));
        label.SetBinding(Label.PaddingProperty, new Binding(nameof(ItemPadding), source: WheelPickerAncestor));
        label.SetBinding(Label.HorizontalTextAlignmentProperty, new Binding(nameof(ItemHorizontalTextAlignment), source: WheelPickerAncestor));
        label.SetBinding(Label.VerticalTextAlignmentProperty, new Binding(nameof(ItemVerticalTextAlignment), source: WheelPickerAncestor));
        label.SetBinding(Label.LineBreakModeProperty, new Binding(nameof(ItemLineBreakMode), source: WheelPickerAncestor));
        label.SetBinding(Label.MaxLinesProperty, new Binding(nameof(ItemMaxLines), source: WheelPickerAncestor));
        label.SetBinding(Label.FontAutoScalingEnabledProperty, new Binding(nameof(ItemFontAutoScalingEnabled), source: WheelPickerAncestor));

        return label;
    });

    /// <summary>Initializes a new instance of the <see cref="WheelPicker"/> class.</summary>
    public WheelPicker()
    {
        _itemsHost = new() { Spacing = 0 };

        base.AllowedDirections = AllowedPanDirections.Vertical;
        base.DeferToChildGestures = false;
        FlingVelocityThreshold = 220;
        InertiaMinVelocity = 30;
        InertiaDeceleration = 1800;

        Children.Add(_itemsHost);
        Clip = _clipGeometry;
    }

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
    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent != null && !IsSet(ItemTextColorProperty))
        {
            this.SetAppThemeColor(ItemTextColorProperty, Colors.Black, Colors.White);
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

        UpdateClipGeometry();

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
            CancelAllAnimations();

            IsDragging = false;
            IsSpinning = false;
        }

        if (propertyName == nameof(IsClippedToBounds))
            Clip = IsClippedToBounds ? null : _clipGeometry;
    }

    /// <inheritdoc/>
    protected override void OnPanning(PanEventArgs e)
    {
        if (!HasItems || ItemHeight <= 0)
            return;

        switch (e.Status)
        {
            case PanGestureStatus.Started:

                _flingDirection = FlingDirection.None;
                _currentScrollSpeed = 0;

                IsDragging = true;
                IsSpinning = true;

                CancelAllAnimations();
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
                        int last = ItemsCount - 1;
                        _virtualCenterIndex = Math.Clamp(_virtualCenterIndex, 0, last);

                        if (_virtualCenterIndex == 0 || _virtualCenterIndex == last)
                        {
                            CancelAllAnimations();
                            SnapToCurrentSelection(animated: false);
                        }
                    }
                    if (e.Status == PanGestureStatus.Completed)
                    {
                        _flingDirection = e.FlingDirection;
                        StartInertia(e);
                    }
                }

                break;
        }
    }

    /// <inheritdoc/>
    protected override void OnInertia(InertiaEventArgs e)
    {
        if (!HasItems || ItemHeight <= 0)
            return;

        switch (e.Status)
        {
            case InertiaStatus.Started:
                IsSpinning = true;
                CancelAllAnimations();
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

        var items = ItemsSource!;

        index = Math.Clamp(index, 0, ItemsCount - 1);
        int logicalIndex = index;

        double start = _virtualCenterIndex;
        double target = GetNearestVirtualIndexFor(index);
        double delta = target - start;

        bool shouldAnimate = animated && !_isFirstAppearance;

        CancelAllAnimations();

        if (!shouldAnimate || Math.Abs(delta) < 0.001)
        {
            _virtualCenterIndex = target;
            UpdateVisualFromVirtualIndex();
            SetSelectionSilently(logicalIndex, items[logicalIndex]);

            if (!_isFirstAppearance)
                ApplyFeedbacks(isSnap: true);

            return;
        }

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

                var items = ItemsSource!;

                // Re-read count — ItemsSource may have changed during the animation.
                int safeIndex = Math.Clamp(logicalIndex, 0, ItemsCount - 1);
                _virtualCenterIndex = GetNearestVirtualIndexFor(safeIndex);
                UpdateVisualFromVirtualIndex();

                if (!c)
                {
                    SetSelectionSilently(safeIndex, items[safeIndex]);
                    ApplyFeedbacks(isSnap: true);
                }

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

    /// <summary>
    /// Cancels any running animations including inertia animation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this method when you need to stop any running animations, for example:
    /// </para>
    /// <list type="bullet">
    ///   <item>When the user taps the screen during scrolling</item>
    ///   <item>When navigating away from the current view</item>
    ///   <item>When resetting the content position</item>
    /// </list>
    /// </remarks>
    public void CancelAllAnimations()
    {
        try { CancelInertia(); } catch { }
        try { this.AbortAnimation(WheelSnapAnimationName); } catch { }
        try { this.AbortAnimation(WheelSpinToAnimationName); } catch { }
    }

    /// <summary>
    /// Raises the <see cref="SelectedIndexChanged"/> event and executes the <see cref="SelectedIndexChangedCommand"/>, if available.
    /// </summary>
    /// <remarks>
    /// Override this method in a derived class to provide custom handling for index changed events.
    /// </remarks>
    /// <param name="e">The event data for <see cref="IndexChangedEventArgs"/>.</param>
    protected virtual void OnSelectedIndexChanged(IndexChangedEventArgs e)
    {
        SelectedIndexChanged?.Invoke(this, e);

        var param = SelectedIndexChangedCommandParameter ?? e;
        if (SelectedIndexChangedCommand?.CanExecute(param) == true)
            SelectedIndexChangedCommand?.Execute(param);
    }

    /// <summary>
    /// Raises the <see cref="SelectedItemChanged"/> event and executes the <see cref="SelectedItemChangedCommand"/>, if available.
    /// </summary>
    /// <remarks>
    /// Override this method in a derived class to provide custom handling for item changed events.
    /// </remarks>
    /// <param name="e">The event data for <see cref="ItemChangedEventArgs"/>.</param>
    protected virtual void OnSelectedItemChanged(ItemChangedEventArgs e)
    {
        SelectedItemChanged?.Invoke(this, e);

        var param = SelectedItemChangedCommandParameter ?? e;
        if (SelectedItemChangedCommand?.CanExecute(param) == true)
            SelectedItemChangedCommand?.Execute(param);
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
                if (_observableSource != null)
                {
                    _observableSource.CollectionChanged -= OnCollectionChanged;
                    _observableSource = null;
                }
            }
            catch { }

            try { DisposeMouseWheelHandling(); } catch { }
            try { DisposeSoundFeedbackHandling(); } catch { }
            try { DisposeNativeHaptic(); } catch { }

            CancelAllAnimations();
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

    private void UpdateItemsRootHeight(bool needRecenter = true)
    {
        if (ItemHeight <= 0)
            return;

        _cachedVisibleHeight = CalculateVisibleHeight();
        _itemsHost.HeightRequest = _cachedVisibleHeight;

        UpdateClipGeometry();

        if (needRecenter)
            UpdateVisualFromVirtualIndex();
    }

    private void UpdateClipGeometry()
    {
        if (_cachedVisibleHeight <= 0 || Width <= 0 || Height <= 0)
            return;

        double clipTop = (Height - _cachedVisibleHeight) / 2.0;
        _clipGeometry.Rect = new Rect(0, clipTop, Width, _cachedVisibleHeight);
    }

    /// <summary>
    /// Computes the exact visual height needed to show <see cref="VisibleItemsCount"/> items,
    /// accounting for curvature compression and edge scaling.
    /// Uses the same math as the render loop so the clip boundary
    /// sits precisely at the visual edge of the outermost visible item.
    /// <para><c>Windows has a different calculation, thats why we need to use <see cref="ViewHelper.HideOrShow"/></c>
    /// </para>
    /// </summary>
    private double CalculateVisibleHeight()
    {
        double itemHeight = ItemHeight;
        int vic = VisibleItemsCount;

        if (itemHeight <= 0 || vic <= 0)
            return 0;

        double curvature = CurvatureFactor;

        // Flat mode or single item — no compression.
        if (curvature <= 0.0 || vic <= 1)
            return itemHeight * vic;

        // Outermost VIC item distance from center (in slot units).
        double outerOffset = (vic - 1) / 2.0;
        double half = Math.Max(1.0, vic / 2.0);
        double t = outerOffset / half;   // norm [0, 1)

        if (t < 1e-6)
            return itemHeight * vic;

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
            translationY = outerOffset * compression * itemHeight;   // inward shift
        }

        // Visual center of the bottom outermost item, measured from wheel center.
        double visualCenter = outerOffset * itemHeight - translationY;
        // Bottom edge, accounting for scale shrinkage from item center.
        double visualBottom = visualCenter + itemHeight * scale / 2.0;

        // Symmetric top <-> bottom.
        double value = 2.0 * visualBottom;

#if WINDOWS
        value = itemHeight * vic - translationY;
#endif
        return value;
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
        double selectionThreshold = SelectionThreshold;

        if (Loop)
        {
            int roundedRaw = (int)Math.Round(center);
            int normalizedIndex = NormalizeIndex(roundedRaw);
            if (normalizedIndex == -1)
                return false;

            distanceToCenter = Math.Abs(center - roundedRaw);
            if (distanceToCenter > selectionThreshold)
                return false;

            candidateIndex = normalizedIndex;
            return true;
        }
        else
        {
            double clamped = Math.Clamp(center, 0, ItemsCount - 1);
            int rounded = (int)Math.Round(clamped);

            distanceToCenter = Math.Abs(center - rounded);
            if (distanceToCenter > selectionThreshold)
                return false;

            candidateIndex = rounded;
            return true;
        }
    }

    private int FindItemIndex(object? value)
    {
        if (value == null || !HasItems)
            return -1;

        var items = ItemsSource!;
        int count = ItemsCount;

        // Fast path: check SelectedIndex first (most common case)
        int index = SelectedIndex;
        if (index >= 0 && index < count && Equals(items[index], value))
            return index;

        for (int i = 0; i < count; i++)
        {
            if (Equals(items[i], value))
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
        var eventArgs = new IndexChangedEventArgs(oldIndex, newIndex);
        OnSelectedIndexChanged(eventArgs);
    }

    private void RaiseSelectedItemChanged(object? oldItem, object? newItem)
    {
        var eventArgs = new ItemChangedEventArgs(oldItem, newItem);
        OnSelectedItemChanged(eventArgs);

    }

    /// <summary>
    /// Unconditionally sets selection from a virtual target position,
    /// firing feedback only when the selection actually changed.
    /// </summary>
    private void FinalizeSelection(double virtualTarget)
    {
        if (!HasItems)
            return;

        var items = ItemsSource!;

        int logicalIndex = NormalizeIndex((int)Math.Round(virtualTarget));
        if (logicalIndex < 0 || logicalIndex >= ItemsCount)
            return;

        bool changed = logicalIndex != SelectedIndex;

        SetSelectionSilently(logicalIndex, items[logicalIndex]);

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
            view.HideOrShow(false);
        }
        catch { }
    }

    #endregion

    #region Items management

    private void RebuildItems()
    {
        var slots = _itemsHost.Children;
        int slotCount = slots.Count;

        if (_isLoadingItems && slotCount > 0)
        {
            if (_hasValidSize)
                UpdateVisualFromVirtualIndex();
            return;
        }

        int vic = VisibleItemsCount;
        int itemsCount = vic + 2;
        int centerIndex = itemsCount / 2;

        // Recycle current children into the pool
        for (int i = 0; i < slotCount; i++)
        {
            if (slots[i] is View child)
            {
                try { child.SizeChanged -= OnItemSizeChanged; } catch { }
                child.BindingContext = GhostContext;

                if (_viewPool.Count <= itemsCount)
                    _viewPool.Enqueue(child);
            }
        }

        slots.Clear();

        _lastBaseCenterRawIndex = int.MinValue;
        _lastItemsCount = -1;

        var template = ItemTemplate ?? DefaultItemTemplate;

        for (int i = 0; i < itemsCount; i++)
        {
            View? view = null;

            if (_viewPool.Count > 0)
                view = _viewPool.Dequeue();
            else
                view = template.CreateContent() as View;

            if (view == null)
                continue;

            // Prevent BindingContext inheritance from the parent chain.
            // Recycled views already have GhostContext, but freshly created
            // views have null -> MAUI inherits from Page -> ViewModel.ToString()
            // can produce long text that wraps multi-line with Padding, inflating Height.
            view.BindingContext = GhostContext;

            // Single reset per view
            ResetViewTransforms(view);

            if (ItemHeight > 0)
            {
                view.HeightRequest = ItemHeight;
            }
            else
            {
                // if HeightRequest is set, use this, otherwise calculate on SizeChanged
                if (view.HeightRequest > 0)
                {
                    ItemHeight = view.HeightRequest;
                    UpdateItemsRootHeight(!_hasValidSize);
                }
                else
                {
                    if (i == centerIndex)
                    {
                        try { view.SizeChanged -= OnItemSizeChanged; } catch { }
                        view.SizeChanged += OnItemSizeChanged;
                    }
                }
            }

            slots.Add(view);
        }

        if (_hasValidSize)
            UpdateVisualFromVirtualIndex();
    }

    /// <summary>
    /// Clears realized item height requests and re-attaches a single SizeChanged probe
    /// so ItemHeight can be re-derived from the current template.
    /// </summary>
    private void RemeasureItemHeight()
    {
        var slots = _itemsHost.Children;
        int slotCount = slots.Count;
        if (slotCount == 0)
            return;

        int centerIndex = slotCount / 2;

        for (int i = 0; i < slotCount; i++)
        {
            if (slots[i] is not View view)
                continue;

            try { view.SizeChanged -= OnItemSizeChanged; } catch { }

            // Let the template determine height.
            view.ClearValue(HeightRequestProperty);

            if (i == centerIndex)
            {
                // Probe needs real content — GhostContext produces empty text,
                // so font-family/format changes don't affect height and
                // SizeChanged never fires, leaving ItemHeight stuck at 0.
                if (HasItems)
                {
                    int index = SelectedIndex >= 0 && SelectedIndex < ItemsCount ? SelectedIndex : 0;
                    view.BindingContext = ItemsSource![index];
                }

                view.SizeChanged += OnItemSizeChanged;
            }
        }

        // Force rebind on next visual update so the probe view
        // gets its correct BindingContext back after measurement.
        _lastBaseCenterRawIndex = int.MinValue;
        _lastItemsCount = -1;
    }

    private void OnItemSizeChanged(object? sender, EventArgs e)
    {
        if (sender is not View view)
            return;

        if (view.Height <= 0 || !_hasValidSize)
            return;

        if (ItemHeight <= 0)
        {
            var childs = _itemsHost.Children;
            ItemHeight = view.Height;
            UpdateItemsRootHeight(false);

            for (int i = 0; i < childs.Count; i++)
            {
                if (childs[i] is View child)
                    child.HeightRequest = ItemHeight;
            }

            UpdateVisualFromVirtualIndex();
        }

        view.SizeChanged -= OnItemSizeChanged;
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

        var items = ItemsSource!;
        int index = -1;

        if (SelectedItem != null)
            index = FindItemIndex(SelectedItem);

        if (index < 0 && SelectedIndex >= 0 && SelectedIndex < ItemsCount)
            index = SelectedIndex;

        if (index < 0)
            index = 0;

        _virtualCenterIndex = index;
        SetSelectionSilently(index, items[index]);

        _lastBaseCenterRawIndex = int.MinValue;
        _lastItemsCount = -1;
    }

    #endregion

    #region Visual update (hot path — every frame during scroll)

    private void UpdateVisualFromVirtualIndex()
    {
        if (!HasItems)
            return;
        if (_itemsHost.Children.Count == 0)
            return;

        BatchBegin();

        try
        {
            var items = ItemsSource!;
            var children = _itemsHost.Children;
            double itemHeight = ItemHeight;
            int count = ItemsCount;
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

                    // Hide ghosts with opacity
                    // GhostContext prevents BindingContext inheritance so no text leaks.
                    child.SetOpacity(child.BindingContext == GhostContext ? 0 : 1);
                }

                _lastBaseCenterRawIndex = baseCenterRawIndex;
                _lastItemsCount = count;
                _lastLoopFlag = Loop;
            }

            if (itemHeight > 0)
            {
                double visibleH = _cachedVisibleHeight > 0
                    ? _cachedVisibleHeight
                    : itemHeight * VisibleItemsCount;
                double baseTranslation = visibleH / 2.0 - (centerSlot + 0.5) * itemHeight;
                double dynamicOffset = -scrollOffset * itemHeight;
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
            int count = ItemsCount;

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

        bool isScrolling = Math.Abs(fractional) > 0.1;
        int lastSlot = slotCount - 1;

        // --- flat mode ---
        if (curvature <= 0.0)
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (children[i] is not View child)
                    continue;

                bool isGhost = child.BindingContext == GhostContext;

                // Ghosts stay at opacity 0 (set during rebind).
                // Only apply transforms to real items.
                if (!isGhost)
                {
                    child.SetScale(1);
                    child.SetOpacity(1);
                    child.SetRotationX(0);
                    child.SetTranslationY(0);
                    child.HideOrShow((i == 0 || i == lastSlot) && !isScrolling);

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
            child.HideOrShow((i == 0 || i == lastSlot) && !isScrolling);

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

        int count = ItemsCount;

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

        var count = ItemsCount;

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

        CancelAllAnimations();

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
                if (!HasItems)
                {
                    IsSpinning = false;
                    return;
                }

                // Re-read count — ItemsSource may have changed during the animation.
                int logicalIndex = NormalizeIndex((int)Math.Round(target));
                int safeIndex = Math.Clamp(logicalIndex, 0, ItemsCount - 1);

                _virtualCenterIndex = GetNearestVirtualIndexFor(safeIndex);
                UpdateVisualFromVirtualIndex();

                if (!c)
                {
                    FinalizeSelection(_virtualCenterIndex);
                }

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
        int count = ItemsCount;
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
        int count = ItemsCount;

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
        int count = ItemsCount;

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
            candidate = Math.Clamp(candidate, 0, ItemsCount - 1);

        _virtualCenterIndex = candidate;

        CancelAllAnimations();
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
            double max = ItemsCount - 1;

            if (candidate < min)
            {
                double overshoot = min - candidate;
                candidate = min - (overshoot * 0.5);
                hitEdge = true;
            }
            else if (candidate > max)
            {
                double overshoot = candidate - max;
                candidate = max + (overshoot * 0.5);
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
        RefreshItemsCache();

        _isLoadingItems = true;
        SyncStateFromPublicProps();
        RebuildItems();
        _isLoadingItems = false;
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

        RefreshItemsCache();

        // pooled views are no longer valid.
        CancelAllAnimations();

        IsDragging = false;
        IsSpinning = false;

        _viewPool.Clear();
        _itemsHost.Children.Clear();

        _isLoadingItems = true;
        SyncStateFromPublicProps();
        RebuildItems();
        _isLoadingItems = false;
    }

    private void RefreshItemsCache()
    {
        _cachedItems = ItemsSource;
        _cachedItemsCount = _cachedItems?.Count ?? 0;
    }

    #endregion

    #region Property-changed internals

    /// <summary>Common path for SelectedIndex/SelectedItem changes.</summary>
    /// <remarks>Navigates to the given logical index, with or without animation.</remarks>
    private void NavigateToIndex(int index)
    {
        CancelAllAnimations();

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

    private void OnItemTemplateChangedInternal()
    {
        // Structural rebuild: the template changed, so pooled views are no longer valid.
        CancelAllAnimations();

        IsDragging = false;
        IsSpinning = false;

        ItemHeight = 0;
        _viewPool.Clear();
        _itemsHost.Children.Clear();
        _cachedVisibleHeight = 0;

        SyncStateFromPublicProps();
        RebuildItems();
    }

    /// <summary>
    /// Metrics-only update for the realized items (font size, max lines, line-break, etc.).
    /// Does NOT clear the pool or recreate views; it just forces a re-measure of the item height.
    /// </summary>
    private void OnItemMetricsChangedInternal()
    {
        // Metrics properties apply to the default template only, custom templates manage their own sizing.
        if (ItemTemplate != null)
            return;

        ItemHeight = 0;
        _cachedVisibleHeight = 0;

        RemeasureItemHeight();
    }

    private void OnSelectedIndexChangedInternal(int newIndex)
    {
        if (_suppressSelectedIndexCallback || !HasItems || _isLoadingItems)
            return;

        var items = ItemsSource!;
        int index = NormalizeIndex(newIndex);
        if (index < 0 || index >= ItemsCount)
            return;

        _suppressSelectedItemCallback = true;
        SelectedItem = items[index];
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
            ApplyOverlayInternal(oldOverlay, newOverlay);
        else
            _pendingOverlay = new(oldOverlay, newOverlay);
    }

    private void ApplyOverlayInternal(View? oldOverlay, View? newOverlay)
    {
        if (oldOverlay != null && Children.Contains(oldOverlay))
            Children.Remove(oldOverlay);

        if (newOverlay != null)
        {
            newOverlay.InputTransparent = true;
            Children.Add(newOverlay);
        }
    }

    private void OnLoopChangedInternal()
    {
        if (!HasItems)
            return;

        CancelAllAnimations();

        int idx = SelectedIndex;
        var items = ItemsSource!;
        int count = ItemsCount;

        if (idx < 0 || idx >= count)
        {
            idx = NormalizeIndex((int)Math.Round(_virtualCenterIndex));
            if (idx < 0)
                idx = 0;
            if (idx >= count)
                idx = count - 1;
        }

        _virtualCenterIndex = idx;
        SetSelectionSilently(idx, items[idx]);

        _lastBaseCenterRawIndex = int.MinValue;
        _lastItemsCount = -1;

        UpdateVisualFromVirtualIndex();
    }

    private void OnVisibleItemsCountChangedInternal()
    {
        RebuildItems();
        UpdateItemsRootHeight();
    }

    private void OnIsSwipeEnabledInternal(bool enabled)
    {
        base.IsPanEnabled = enabled;

        CancelAllAnimations();

        IsDragging = false;
        IsSpinning = false;
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

        double intensity = isSnap
            ? 1.0
            : 1.0 - Math.Clamp(Math.Abs(_currentScrollSpeed) / MaxVelocity, 0, 1);
        intensity *= intensity; // easing

        var now = Stopwatch.GetTimestamp();

        if (_lastFeedbackTimestamp != 0 && MinFeedbackIntervalMs > 0.0)
        {
            double elapsedMs = (now - _lastFeedbackTimestamp) * 1000.0 / Stopwatch.Frequency;
            if (elapsedMs < MinFeedbackIntervalMs)
                return;
        }

        _lastFeedbackTimestamp = now;

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
                    ? TickSoundVolumeMax
                    : (TickSoundVolumeMax - TickSoundVolumeMin) * intensity;

                PlaySoundFeedback(volume);
            }
            catch { }
        }
    }

    #endregion
}