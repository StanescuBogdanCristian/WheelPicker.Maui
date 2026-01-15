using Microsoft.Maui.Controls.Shapes;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace WheelPicker.Maui
{
    public partial class WheelPicker : ContentView
    {
        public const string DefaultItemVisualState = "DefaultItem";
        public const string CurrentItemVisualState = "CurrentItem";

        private const string WheelInertiaAnimationName = "WheelInertia";
        private const string WheelSnapAnimationName = "WheelSnap";
        private const string WheelSpinToAnimationName = "WheelSpinTo";

        private const string SoundAssetFileName = "wheel_tick.wav";
        private const double SoundVolume = 0.20;

        private const uint FrameRateMs = 16; // ~60 FPS

        // Exponent used when mapping normalized distance from the center to the item scale.
        // values below 1 concentrate most of the scaling change near the center.
        // values above 1 push more of the scaling change toward the edges.
        private const double ScalePower = 0.9;

        // Exponent used when mapping normalized distance from the center to item opacity.
        // values below 1 make items fade more aggressively around the center.
        // values above 1 keep items more visible near the center and make them fade faster near the edges.
        private const double OpacityPower = 1.3;

        // Exponent used when mapping normalized distance from the center to item tilt (rotation).
        // values below 1 make tilt ramp up more strongly around the center.
        // values above 1 move most of the tilt change toward the outer positions.
        private const double TiltPower = 0.9;

        // Exponent used when mapping normalized distance from the center to the "compression"
        // of the curve (how tightly items visually pack toward the edges).
        // values below 1 concentrate compression effects near the center.
        // values above 1 make the curve feel tighter and more compressed toward the edges.
        private const double CompressionPower = 1.2;

        private readonly IHapticFeedback _hapticFeedback;
        private readonly Grid _rootGrid;

        private readonly VerticalStackLayout _itemsHost;

        private readonly PanGestureRecognizer _panGesture;

        private INotifyCollectionChanged? _observableSource;

        // Virtual center index (can be fractional; can go outside [0, Count-1] during fling/bounce)
        private double _virtualCenterIndex;
        private double _panStartVirtualCenterIndex;

        // For inertia
        private double _lastPanTotalY;
        private long _lastPanTimestamp; // ticks from Stopwatch.GetTimestamp()
        private double _currentVelocityItemsPerSecond;

        // Simple view pool to reuse template instances
        private readonly Queue<View> _viewPool = new();

        // Avoid feedback loops when we set properties internally
        private bool _suppressSelectedIndexCallback;
        private bool _suppressSelectedItemCallback;

        // Cache to avoid rebinding items every frame
        private int _lastBaseCenterRawIndex = int.MinValue;
        private int _lastItemsCount = -1;
        private bool _lastLoopFlag;

        // Initialization + feedback control
        private bool _isInitializing = true;   // start in "initializing" mode
        private bool _initialSelectionHandled; // true once we've applied initial selection
        private bool _hasAppeared;             // true one size alocated

        // feedback debounce
        private long _lastFeedbackTimestamp;

        public WheelPicker()
        {
            _hapticFeedback = Microsoft.Maui.Devices.HapticFeedback.Default;

            _itemsHost = new() { Spacing = 0 };

            _rootGrid = new();
            _rootGrid.Children.Add(_itemsHost);
            _rootGrid.SizeChanged += OnRootGridSizeChanged;

            Content = _rootGrid;

            _panGesture = new PanGestureRecognizer();
            _panGesture.PanUpdated += OnPanUpdated;
            GestureRecognizers.Add(_panGesture);
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();

            if (Handler != null)
            {
                InitializeMouseWheelHandling();
                InitializeScrollConflictHandling();
                InitializeSoundFeedbackHandling();
            }
            else
            {
                Cleanup();
            }
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (width <= 0 || height <= 0)
                return;

            if (!_hasAppeared)
                _hasAppeared = true;

            UpdateVisualFromVirtualIndex();
        }

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
            if (ItemsSource == null || ItemsSource.Count == 0)
                return;

            index = Math.Clamp(index, 0, ItemsSource.Count - 1);
            int logicalIndex = index; // the “real” index inside ItemsSource

            double start = _virtualCenterIndex;
            double target = GetNearestVirtualIndexFor(index);
            double delta = target - start;

            // No animation (or tiny move) -> just jump + one feedback
            if (!animated || Math.Abs(delta) < 0.001)
            {
                _virtualCenterIndex = target;
                UpdateVisualFromVirtualIndex();

                // set selection once
                _suppressSelectedIndexCallback = true;
                SelectedIndex = logicalIndex;
                _suppressSelectedIndexCallback = false;

                _suppressSelectedItemCallback = true;
                SelectedItem = ItemsSource[logicalIndex];
                _suppressSelectedItemCallback = false;

                ApplyFeedbacks();

                return;
            }

            // Animated scroll
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
                easing: Easing.CubicOut,
                finished: (v, c) =>
                {
                    if (ItemsSource == null || ItemsSource.Count == 0)
                    {
                        IsSpinning = false;
                        return;
                    }

                    int safeIndex = Math.Clamp(logicalIndex, 0, ItemsSource.Count - 1);

                    _virtualCenterIndex = GetNearestVirtualIndexFor(safeIndex);

                    UpdateVisualFromVirtualIndex();

                    _suppressSelectedIndexCallback = true;
                    SelectedIndex = logicalIndex;
                    _suppressSelectedIndexCallback = false;

                    _suppressSelectedItemCallback = true;
                    SelectedItem = ItemsSource[logicalIndex];
                    _suppressSelectedItemCallback = false;

                    ApplyFeedbacks();

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
            if (ItemsSource == null || ItemsSource.Count == 0 || item == null)
                return;

            int index = -1;
            for (int i = 0; i < ItemsSource.Count; i++)
            {
                if (Equals(ItemsSource[i], item))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
                return;

            SpinTo(index, animated);
        }

        partial void InitializeScrollConflictHandling();

        partial void DisposeScrollConflictHandling();

        partial void InitializeMouseWheelHandling();

        partial void DisposeMouseWheelHandling();

        partial void InitializeSoundFeedbackHandling();

        partial void PlaySoundFeedback();

        partial void DisposeSoundFeedbackHandling();

        private void OnRootGridSizeChanged(object? sender, EventArgs e)
        {
            UpdateClipGeometry();
        }

        /// <summary>
        /// Derived factor controlling how tightly items visually compress near the edges.
        /// It is computed from the edge configuration (scale / tilt), so more
        /// aggressive edge settings automatically increase the bend, and mild settings
        /// keep the wheel almost flat.
        /// Returns a value in roughly [0, ~0.4].
        /// </summary>
        private double ComputeEdgeBendTightness()
        {
            double scaleBend = 1.0 - EdgeItemScale;
            double tiltBend = EdgeItemTiltAngle / 90.0;

            double raw = scaleBend * 0.5 + tiltBend * 0.5;
            raw = Math.Clamp(raw, 0.0, 1.0);

            if (raw < 1e-6)
                return 0.0;

            const double minTight = 0.15;
            const double maxTight = 0.40;

            return minTight + (maxTight - minTight) * raw;
        }

        /// <summary>
        /// Computes the minimum time (in ms) between haptic/sound feedback events,
        /// derived from <see cref="SelectionThreshold" />.
        /// Smaller thresholds (stricter center requirement) give slightly shorter
        /// intervals; larger thresholds (more permissive) give slightly longer ones.
        /// </summary>
        private double GetFeedbackMinIntervalMs()
        {
            double norm = SelectionThreshold / 0.5;

            const double minMs = 30.0;  // fastest allowed ticking
            const double maxMs = 80.0;  // slowest allowed ticking

            return minMs + (maxMs - minMs) * norm;
        }

        /// <summary>
        /// Computes the index that **would** be selected for the current
        /// _virtualCenterIndex, using SelectionThreshold, without actually
        /// modifying SelectedIndex / SelectedItem.
        /// Returns true if we are within the threshold; otherwise false.
        /// </summary>
        private bool TryGetSelectionCandidateIndex(out int candidateIndex, out double distanceToCenter)
        {
            candidateIndex = -1;
            distanceToCenter = double.MaxValue;

            if (ItemsSource == null || ItemsSource.Count == 0)
                return false;

            double center = _virtualCenterIndex;

            if (Loop)
            {
                // For looped mode, use the *raw* rounded index, then normalize.
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
                // Non-loop: clamp within the valid range before rounding.
                double clamped = Math.Clamp(center, 0, ItemsSource.Count - 1);
                int rounded = (int)Math.Round(clamped);

                distanceToCenter = Math.Abs(center - rounded);
                if (distanceToCenter > SelectionThreshold)
                    return false;

                candidateIndex = rounded;
                return true;
            }
        }

        private void UpdateClipGeometry()
        {
            if (_rootGrid == null || _rootGrid.Width <= 0 || _rootGrid.Height <= 0)
                return;

            double width = _rootGrid.Width;
            double height = _rootGrid.Height;

            double bendTightness = ComputeEdgeBendTightness();

            // If either intensity or derived bend is zero, show full height (no extra clipping).
            if (CurvatureFactor <= 0.0 || bendTightness <= 0.0)
            {
                var fullRect = new Rect(0, 0, width, height);

                if (_rootGrid.Clip is RectangleGeometry fullGeom)
                    fullGeom.Rect = fullRect;
                else
                    _rootGrid.Clip = new RectangleGeometry { Rect = fullRect };

                return;
            }

            double maxClipShrink = bendTightness * 0.65;
            double shrink = maxClipShrink * CurvatureFactor;
            shrink = Math.Clamp(shrink, 0.0, 0.9);

            double clipHeight = height * (1.0 - shrink);
            double offsetY = (height - clipHeight) / 2.0;

            var rect = new Rect(0, offsetY, width, clipHeight);

            if (_rootGrid.Clip is RectangleGeometry rectGeom)
                rectGeom.Rect = rect;
            else
                _rootGrid.Clip = new RectangleGeometry { Rect = rect };
        }

        private void UpdateControlHeight()
        {
            if (VisibleItemsCount <= 0 || ItemHeight <= 0)
                return;

            double total = ItemHeight * VisibleItemsCount;
            _rootGrid.HeightRequest = total;
        }

        private static void ResetViewTransforms(View view)
        {
            try
            {
                // reset transforms
                view.AnchorX = 0.5;
                view.AnchorY = 0.5;
                view.SetScale(1);
                view.SetOpacity(1);
                view.SetRotationX(0);
                view.SetTranslationY(0);
            }
            catch { }
        }

        private void RebuildItems()
        {
            // Recycle current children into the pool
            foreach (var child in _itemsHost.Children.OfType<View>())
            {
                try { child.SizeChanged -= OnItemSizeChanged; } catch { }
                child.BindingContext = null;
                if (_viewPool.Count < 32)
                {
                    ResetViewTransforms(child);
                    _viewPool.Enqueue(child);
                }
            }

            _itemsHost.Children.Clear();

            // force re-bind of items on next UpdateVisualFromVirtualIndex
            _lastBaseCenterRawIndex = int.MinValue;
            _lastItemsCount = -1;

            if (VisibleItemsCount <= 0)
                return;

            int itemsCount = VisibleItemsCount + 2;

            for (int i = 0; i < itemsCount; i++)
            {
                View? view = null;

                if (_viewPool.Count > 0)
                {
                    view = _viewPool.Dequeue();
                    ResetViewTransforms(view);
                }
                else if (ItemTemplate != null)
                {
                    var content = ItemTemplate.CreateContent();
                    if (content is View v)
                        view = v;
                }

                // if a view fails to create, skip it
                if (view == null)
                    continue;

                // Ensure consistent rotation pivot and initial transform values
                ResetViewTransforms(view);

                if (ItemHeight > 0)
                {
                    // We already know the height => no more SizeChanged needed
                    view.HeightRequest = ItemHeight;
                }
                else
                {
                    // Only when we still don't know row height
                    view.SizeChanged += OnItemSizeChanged;
                }

                _itemsHost.Children.Add(view);
            }

            UpdateVisualFromVirtualIndex();
        }

        private void SyncStateFromPublicProps()
        {
            if (ItemsSource == null || ItemsSource.Count == 0)
            {
                _virtualCenterIndex = 0;

                _suppressSelectedIndexCallback = true;
                SelectedIndex = -1;
                _suppressSelectedIndexCallback = false;

                _suppressSelectedItemCallback = true;
                SelectedItem = null;
                _suppressSelectedItemCallback = false;

                _lastBaseCenterRawIndex = int.MinValue;
                _lastItemsCount = -1;

                _initialSelectionHandled = false;
                return;
            }

            int count = ItemsSource.Count;
            int index = -1;

            // 1) Try SelectedItem
            if (SelectedItem != null)
            {
                for (int i = 0; i < count; i++)
                {
                    if (Equals(ItemsSource[i], SelectedItem))
                    {
                        index = i;
                        break;
                    }
                }
            }

            // 2) Try SelectedIndex
            if (index < 0 && SelectedIndex >= 0 && SelectedIndex < count)
                index = SelectedIndex;

            // 3) Fallback to first
            if (index < 0)
                index = 0;

            _virtualCenterIndex = index;

            _suppressSelectedIndexCallback = true;
            SelectedIndex = index;
            _suppressSelectedIndexCallback = false;

            _suppressSelectedItemCallback = true;
            SelectedItem = ItemsSource[index];
            _suppressSelectedItemCallback = false;

            _lastBaseCenterRawIndex = int.MinValue;
            _lastItemsCount = -1;

            _initialSelectionHandled = true;
        }

        private void OnItemSizeChanged(object? sender, EventArgs e)
        {
            if (sender is not View v)
                return;

            if (v.Height <= 0)
                return;

            // First time we discover a non-zero height, lock it in
            if (ItemHeight <= 0)
            {
                ItemHeight = v.Height;
                UpdateControlHeight();

                // Apply to all current children
                foreach (var child in _itemsHost.Children.OfType<View>())
                {
                    child.HeightRequest = ItemHeight;
                }

                UpdateVisualFromVirtualIndex();
            }

            // We don't need further notifications from this child
            v.SizeChanged -= OnItemSizeChanged;
        }

        private void UpdateVisualFromVirtualIndex()
        {
            if (ItemsSource == null || ItemsSource.Count == 0)
                return;
            if (_itemsHost.Children.Count == 0 || VisibleItemsCount <= 0)
                return;

            BatchBegin();

            try
            {
                var items = ItemsSource;
                var children = _itemsHost.Children;
                int count = items.Count;
                int slotCount = children.Count;
                int centerSlot = slotCount / 2;

                double centerIndex = _virtualCenterIndex;
                int baseCenterRawIndex = (int)Math.Round(centerIndex);
                double scrollOffset = centerIndex - baseCenterRawIndex; // [-0.5, 0.5]

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
                            child.BindingContext = items[itemIndex];
                        }
                        else
                        {
                            if (rawIndex < 0 || rawIndex >= count)
                                child.BindingContext = null;
                            else
                                child.BindingContext = items[rawIndex];
                        }
                    }

                    _lastBaseCenterRawIndex = baseCenterRawIndex;
                    _lastItemsCount = count;
                    _lastLoopFlag = Loop;
                }

                if (ItemHeight > 0)
                {
                    double baseTranslation = ItemHeight * (VisibleItemsCount / 2.0 - centerSlot - 0.5);
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
            if (slotCount == 0)
                return;

            var items = ItemsSource;
            if (items == null || items.Count == 0)
                return;

            int slotCenter = slotCount / 2;
            int visible = VisibleItemsCount + 2;
            double half = Math.Max(1.0, (visible - 1) / 2.0);
            double invHalf = 1.0 / half;

            double roundedCenter = Math.Round(_virtualCenterIndex);
            double fractional = _virtualCenterIndex - roundedCenter;
            double visualCenterSlot = slotCenter + fractional;

            int nearestSlot = (int)Math.Round(visualCenterSlot);
            if (nearestSlot < 0) nearestSlot = 0;
            else if (nearestSlot >= slotCount) nearestSlot = slotCount - 1;

            object? centerItem = null;
            if (TryGetSelectionCandidateIndex(out int candidateIndex, out _))
            {
                if (candidateIndex >= 0 && candidateIndex < items.Count)
                    centerItem = items[candidateIndex];
            }

            // ---- flat mode ----
            if (CurvatureFactor <= 0.0)
            {
                for (int i = 0; i < slotCount; i++)
                {
                    if (children[i] is not View child)
                        continue;

                    bool isGhost = child.BindingContext == null;

                    child.SetScale(1);
                    child.SetOpacity(isGhost ? 0 : 1);
                    child.SetRotationX(0);
                    child.SetTranslationY(0);

                    if (isGhost)
                        continue;

                    bool isCenter = centerItem != null && Equals(child.BindingContext, centerItem);
                    var state = isCenter ? CurrentItemVisualState : DefaultItemVisualState;
                    var prev = GetPreviousVisualState(child);
                    if (!string.Equals(prev, state, StringComparison.Ordinal))
                    {
                        VisualStateManager.GoToState(child, state);
                        SetPreviousVisualState(child, state);
                    }
                }
                return;
            }

            // effective "how hard we bend at the edge"
            double edgeTightness = ComputeEdgeBendTightness();
            double effectiveEdgeCompression = edgeTightness * CurvatureFactor;

            for (int i = 0; i < slotCount; i++)
            {
                if (children[i] is not View child)
                    continue;

                double visualOffset = i - visualCenterSlot;
                double norm = visualOffset * invHalf;
                if (norm < -1.0) norm = -1.0;
                else if (norm > 1.0) norm = 1.0;

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
                    double scalePow = Math.Pow(t, ScalePower);
                    double opacityPow = Math.Pow(t, OpacityPower);
                    double tiltPow = Math.Pow(t, TiltPower);

                    double baseScale = 1.0 - (1.0 - EdgeItemScale) * scalePow;
                    double baseOpacity = 1.0 - (1.0 - EdgeItemOpacity) * opacityPow;
                    double baseTiltDeg = EdgeItemTiltAngle * tiltPow;

                    scale = 1.0 + (baseScale - 1.0) * CurvatureFactor;
                    opacity = 1.0 + (baseOpacity - 1.0) * CurvatureFactor;
                    tiltDeg = baseTiltDeg * CurvatureFactor;

                    if (ItemHeight > 0 && effectiveEdgeCompression > 0.0)
                    {
                        double scaleRange = 1.0 - EdgeItemScale;
                        double scaleNorm = scaleRange > 1e-6
                            ? (scale - EdgeItemScale) / scaleRange
                            : 0.0;
                        scaleNorm = Math.Clamp(scaleNorm, 0.0, 1.0);

                        double tiltNormEdge = 0.0;
                        if (EdgeItemTiltAngle > 0.0)
                        {
                            tiltNormEdge = Math.Min(Math.Abs(tiltDeg), EdgeItemTiltAngle) / EdgeItemTiltAngle;
                        }

                        double edgeFactor = (1.0 - scaleNorm + tiltNormEdge) * 0.5;

                        double shapedEdge = Math.Pow(edgeFactor, CompressionPower);

                        double compression = effectiveEdgeCompression * shapedEdge;

                        double originalOffsetRows = i - visualCenterSlot;
                        double compressedOffsetRows = originalOffsetRows * (1.0 - compression);

                        translationY = (compressedOffsetRows - originalOffsetRows) * ItemHeight;
                    }
                }

                bool isGhost = child.BindingContext == null;

                if (isGhost)
                {
                    child.SetScale(0);
                    child.SetOpacity(0);
                    child.SetRotationX(0);
                    child.SetTranslationY(0);
                    continue;
                }

                double signX = norm < 0 ? 1 : -1;
                double tiltX = signX * tiltDeg;

                child.SetScale(scale);
                child.SetOpacity(opacity);
                child.SetRotationX(tiltX);
                child.SetTranslationY(translationY);

                bool isCenter = centerItem != null && Equals(child.BindingContext, centerItem);
                var targetState = isCenter ? CurrentItemVisualState : DefaultItemVisualState;
                var prevState = GetPreviousVisualState(child);
                if (!string.Equals(prevState, targetState, StringComparison.Ordinal))
                {
                    VisualStateManager.GoToState(child, targetState);
                    SetPreviousVisualState(child, targetState);
                }
            }
        }

        private double GetNearestVirtualIndexFor(int targetIndex)
        {
            if (ItemsSource == null || ItemsSource.Count == 0)
                return _virtualCenterIndex;

            targetIndex = Math.Clamp(targetIndex, 0, ItemsSource.Count - 1);

            if (!Loop)
                return targetIndex;

            int n = ItemsSource.Count;
            double start = _virtualCenterIndex;

            // choose k so k*n + targetIndex is closest to current virtual index
            double k = Math.Round((start - targetIndex) / n);
            return k * n + targetIndex;
        }

        private void SnapToNearestIndex(bool animated = true)
        {
            if (ItemsSource == null || ItemsSource.Count == 0)
                return;

            double start = _virtualCenterIndex;
            double target = Math.Round(start);

            if (!Loop)
            {
                // For non-loop mode, never allow landing outside the list
                target = Math.Clamp(target, 0, ItemsSource.Count - 1);
            }

            double delta = target - start;

            this.AbortAnimation(WheelInertiaAnimationName);
            this.AbortAnimation(WheelSnapAnimationName);

            if (!animated || Math.Abs(delta) < 0.001)
            {
                _virtualCenterIndex = target;
                UpdateVisualFromVirtualIndex();
                UpdateSelectionWhileScrolling();

                if (!IsDragging)
                    IsSpinning = false;

                return;
            }

            // Slightly longer snap if farther away, but still short
            uint length = (uint)Math.Clamp(80 + Math.Abs(delta) * 40, 80, 180); // ms

            IsSpinning = true;

            var animation = new Animation(t =>
            {
                _virtualCenterIndex = start + delta * t;
                UpdateVisualFromVirtualIndex();
                UpdateSelectionWhileScrolling();
            });

            animation.Commit(
                this,
                WheelSnapAnimationName,
                rate: FrameRateMs,
                length: length,
                easing: Easing.CubicOut,
                finished: (v, c) =>
                {
                    _virtualCenterIndex = target;
                    UpdateVisualFromVirtualIndex();
                    UpdateSelectionWhileScrolling();

                    if (!IsDragging)
                        IsSpinning = false;
                });
        }

        private void SnapToSelectedIndex(bool animated = true)
        {
            if (ItemsSource == null || ItemsSource.Count == 0)
                return;

            int selectedIndex = SelectedIndex;

            if (selectedIndex < 0 || selectedIndex >= ItemsSource.Count)
            {
                SnapToNearestIndex(animated);
                return;
            }

            double start = _virtualCenterIndex;
            double target;

            if (Loop)
            {
                int n = ItemsSource.Count;
                int idx = selectedIndex;

                double k = Math.Round((start - idx) / n);
                target = k * n + idx;
            }
            else
            {
                target = selectedIndex;
            }

            double delta = target - start;

            this.AbortAnimation(WheelInertiaAnimationName);
            this.AbortAnimation(WheelSnapAnimationName);

            if (!animated || Math.Abs(delta) < 0.001)
            {
                _virtualCenterIndex = target;
                UpdateVisualFromVirtualIndex();
                UpdateSelectionWhileScrolling();

                if (!IsDragging)
                    IsSpinning = false;

                return;
            }

            uint length = (uint)Math.Clamp(80 + Math.Abs(delta) * 40, 80, 180);

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
                length: length,
                easing: Easing.CubicOut,
                finished: (v, c) =>
                {
                    _virtualCenterIndex = target;
                    UpdateVisualFromVirtualIndex();
                    UpdateSelectionWhileScrolling();

                    if (!IsDragging)
                        IsSpinning = false;
                });
        }

        private int NormalizeIndex(int index)
        {
            if (ItemsSource == null || ItemsSource.Count == 0)
                return -1;

            var count = ItemsSource.Count;

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

        private void UpdateSelectionWhileScrolling()
        {
            if (ItemsSource == null || ItemsSource.Count == 0)
                return;

            if (!TryGetSelectionCandidateIndex(out int candidateIndex, out _))
                return;

            if (candidateIndex == SelectedIndex)
                return;

            _suppressSelectedIndexCallback = true;
            SelectedIndex = candidateIndex;
            _suppressSelectedIndexCallback = false;

            _suppressSelectedItemCallback = true;
            SelectedItem = ItemsSource[candidateIndex];
            _suppressSelectedItemCallback = false;

            ApplyFeedbacks();
        }

        private void StartInertiaAnimation(double velocityItemsPerSecond)
        {
            if (ItemsSource == null || ItemsSource.Count == 0)
            {
                SnapToSelectedIndex(animated: true);
                return;
            }

            double speed = Math.Abs(velocityItemsPerSecond);
            if (speed < 0.05)
            {
                SnapToSelectedIndex(animated: true);
                return;
            }

            const double decel = 10.0;       // items/s²
            const double maxSpeed = 12.0;    // clamp crazy flings
            const double maxDistance = 12.0; // max items to travel

            speed = Math.Min(speed, maxSpeed);

            double direction = Math.Sign(velocityItemsPerSecond); // -1, 0, +1
            double distance = (speed * speed) / (2.0 * decel);   // d = v² / (2a)
            distance *= direction;
            distance = Math.Clamp(distance, -maxDistance, maxDistance);

            // Start from a safe index
            double start = _virtualCenterIndex;
            if (!Loop)
            {
                start = Math.Clamp(start, 0, ItemsSource.Count - 1);
                _virtualCenterIndex = start;
            }

            double target = start + distance;

            if (!Loop)
            {
                double minIndex = 0;
                double maxIndex = ItemsSource.Count - 1;
                target = Math.Clamp(target, minIndex, maxIndex);
            }

            double delta = target - start;
            if (Math.Abs(delta) < 0.01 || direction == 0.0)
            {
                SnapToSelectedIndex(animated: true);
                return;
            }

            double durationSeconds = speed / decel;
            durationSeconds = Math.Clamp(durationSeconds, 0.18, 0.7);
            uint lengthMs = (uint)(durationSeconds * 1000.0);

            this.AbortAnimation(WheelSnapAnimationName);
            this.AbortAnimation(WheelInertiaAnimationName);

            IsSpinning = true;

            var animation = new Animation(t =>
            {
                _virtualCenterIndex = start + delta * t;
                UpdateVisualFromVirtualIndex();
                UpdateSelectionWhileScrolling();
            });

            animation.Commit(
                this,
                WheelInertiaAnimationName,
                rate: FrameRateMs,
                length: lengthMs,
                easing: Easing.CubicOut,
                finished: (v, c) =>
                {
                    if (ItemsSource == null || ItemsSource.Count == 0)
                    {
                        IsSpinning = false;
                        return;
                    }

                    double finalCenter = _virtualCenterIndex;

                    double nearestSlot = Math.Round(finalCenter);
                    double distanceToNearest = Math.Abs(finalCenter - nearestSlot);

                    if (direction == 0.0 || distanceToNearest <= SelectionThreshold || SelectionThreshold <= 0.0)
                    {
                        SnapToSelectedIndex(animated: true);
                        return;
                    }

                    int count = ItemsSource.Count;

                    int baseIndex = SelectedIndex;
                    if (baseIndex < 0 || baseIndex >= count)
                    {
                        if (Loop)
                        {
                            baseIndex = NormalizeIndex((int)Math.Round(finalCenter));
                        }
                        else
                        {
                            double clampedCenter = Math.Clamp(finalCenter, 0, count - 1);
                            baseIndex = (int)Math.Round(clampedCenter);
                        }
                    }

                    int step = direction > 0 ? 1 : -1;

                    int targetIndex;
                    if (Loop)
                        targetIndex = NormalizeIndex(baseIndex + step);
                    else
                        targetIndex = Math.Clamp(baseIndex + step, 0, count - 1);

                    _suppressSelectedIndexCallback = true;
                    SelectedIndex = targetIndex;
                    _suppressSelectedIndexCallback = false;

                    _suppressSelectedItemCallback = true;
                    SelectedItem = ItemsSource[targetIndex];
                    _suppressSelectedItemCallback = false;

                    SnapToSelectedIndex(animated: true);

                    ApplyFeedbacks();
                });
        }

        private void ApplyFeedbacks()
        {
            // Never tick/haptic during initialization or before first layout
            if (_isInitializing || !_hasAppeared)
                return;

            var now = Stopwatch.GetTimestamp();

            double minIntervalMs = GetFeedbackMinIntervalMs();

            if (_lastFeedbackTimestamp != 0 && minIntervalMs > 0.0)
            {
                double elapsedMs = (now - _lastFeedbackTimestamp) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMs < minIntervalMs)
                    return;
            }

            _lastFeedbackTimestamp = now;

            if (HapticFeedback && _hapticFeedback.IsSupported)
            {
                try
                {
                    _hapticFeedback.Perform(HapticFeedbackType.LongPress);
                }
                catch { }
            }

            if (SoundFeedback)
            {
                try
                {
                    PlaySoundFeedback();
                }
                catch { }
            }
        }

        private void CancelAnimations()
        {
            try
            {
                this.AbortAnimation(WheelInertiaAnimationName);
            }
            catch { }
            try
            {
                this.AbortAnimation(WheelSnapAnimationName);
            }
            catch { }
            try
            {
                this.AbortAnimation(WheelSpinToAnimationName);
            }
            catch { }
        }

        private void Cleanup()
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

            try
            {
                if (_panGesture != null)
                {
                    _panGesture.PanUpdated -= OnPanUpdated;
                    if (GestureRecognizers.Contains(_panGesture))
                        GestureRecognizers.Remove(_panGesture);
                }
            }
            catch { }

            try
            {
                _rootGrid.SizeChanged -= OnRootGridSizeChanged;
            }
            catch { }

            // Dispose platform MouseWheel handling
            try
            {
                DisposeMouseWheelHandling();
            }
            catch { }

            // Dispose platform scroll conflict handling
            try
            {
                DisposeScrollConflictHandling();
            }
            catch { }

            // Dispose platform audio resources if any
            try
            {
                DisposeSoundFeedbackHandling();
            }
            catch { }

            // Abort running animations
            CancelAnimations();
        }

        private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (!IsSwipeEnabled || !IsEnabled)
                return;

            if (ItemsSource == null || ItemsSource.Count == 0 || ItemHeight <= 0)
                return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:

                    IsDragging = true;
                    IsSpinning = true;

                    _panStartVirtualCenterIndex = _virtualCenterIndex;
                    _lastPanTotalY = 0;
                    _lastPanTimestamp = Stopwatch.GetTimestamp();
                    _currentVelocityItemsPerSecond = 0;

                    CancelAnimations();
                    break;

                case GestureStatus.Running:
                    {
                        var now = Stopwatch.GetTimestamp();
                        double dt = (now - _lastPanTimestamp) / (double)Stopwatch.Frequency;
                        double deltaTotalY = e.TotalY - _lastPanTotalY;

                        if (dt > 0)
                        {
                            double deltaItems = -(deltaTotalY / ItemHeight);
                            _currentVelocityItemsPerSecond = deltaItems / dt;
                        }

                        _lastPanTimestamp = now;
                        _lastPanTotalY = e.TotalY;

                        double totalDeltaItems = -(e.TotalY / ItemHeight);
                        double candidate = _panStartVirtualCenterIndex + totalDeltaItems;

                        if (!Loop)
                        {
                            double min = 0;
                            double max = ItemsSource.Count - 1;

                            if (candidate < min)
                            {
                                double overshoot = min - candidate;
                                candidate = min - overshoot * 0.35;
                            }
                            else if (candidate > max)
                            {
                                double overshoot = candidate - max;
                                candidate = max + overshoot * 0.35;
                            }
                        }

                        _virtualCenterIndex = candidate;
                        UpdateVisualFromVirtualIndex();
                        UpdateSelectionWhileScrolling();
                    }
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    {
                        IsDragging = false;

                        if (ItemsSource == null || ItemsSource.Count == 0)
                        {
                            IsSpinning = false;
                            return;
                        }

                        int last = ItemsSource.Count - 1;

                        if (!Loop)
                        {
                            _virtualCenterIndex = Math.Clamp(_virtualCenterIndex, 0, last);
                            UpdateVisualFromVirtualIndex();
                            UpdateSelectionWhileScrolling();
                        }

                        double speedItems = Math.Abs(_currentVelocityItemsPerSecond);

                        double itemHeight = ItemHeight <= 0 ? 1 : ItemHeight;
                        const double baseUnitsPerSecond = 300.0;
                        double flingSpeedThresholdItems = baseUnitsPerSecond / itemHeight;

                        if (speedItems < flingSpeedThresholdItems)
                        {
                            SnapToSelectedIndex(animated: true);
                        }
                        else
                        {
                            StartInertiaAnimation(_currentVelocityItemsPerSecond);
                        }
                    }
                    break;
            }
        }

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

            _isInitializing = true;
            _initialSelectionHandled = false;

            _lastBaseCenterRawIndex = int.MinValue;
            _lastItemsCount = -1;

            SyncStateFromPublicProps();
            RebuildItems();

            _isInitializing = false;
        }

        private void OnSelectedIndexChangedInternal(int newIndex)
        {
            if (_suppressSelectedIndexCallback || ItemsSource == null || ItemsSource.Count == 0)
                return;

            int count = ItemsSource.Count;
            int index = NormalizeIndex(newIndex);
            if (index < 0 || index >= count)
                return;

            _suppressSelectedItemCallback = true;
            SelectedItem = ItemsSource[index];
            _suppressSelectedItemCallback = false;

            // Treat as "initializing" if:
            // - we are still initializing, OR
            // - initial selection hasn't been applied yet, OR
            // - control hasn't had its first non-zero layout
            bool isInitializing = _isInitializing || !_initialSelectionHandled || !_hasAppeared;

            if (isInitializing)
            {
                _initialSelectionHandled = true;

                _virtualCenterIndex = GetNearestVirtualIndexFor(index);
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
                ApplyFeedbacks();
            }
        }

        private void OnSelectedItemChangedInternal(object newValue)
        {
            if (_suppressSelectedItemCallback || ItemsSource == null || ItemsSource.Count == 0)
                return;

            int count = ItemsSource.Count;
            int index = -1;

            if (newValue != null)
            {
                for (int i = 0; i < count; i++)
                {
                    if (Equals(ItemsSource[i], newValue))
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index < 0)
                return;

            _suppressSelectedIndexCallback = true;
            SelectedIndex = index;
            _suppressSelectedIndexCallback = false;

            // Treat as "initializing" if:
            // - we are still initializing, OR
            // - initial selection hasn't been applied yet, OR
            // - control hasn't had its first non-zero layout
            bool isInitializing = _isInitializing || !_initialSelectionHandled || !_hasAppeared;

            if (isInitializing)
            {
                _initialSelectionHandled = true;

                _virtualCenterIndex = GetNearestVirtualIndexFor(index);
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
                ApplyFeedbacks();
            }
        }

        private void OnOverlayChangedInternal(View? oldOverlay, View? newOverlay)
        {
            if (_rootGrid == null)
                return;

            if (oldOverlay != null && _rootGrid.Children.Contains(oldOverlay))
                _rootGrid.Children.Remove(oldOverlay);

            if (newOverlay != null)
            {
                newOverlay.InputTransparent = true;
                _rootGrid.Children.Add(newOverlay);
            }
        }

        private void OnLoopChangedInternal()
        {
            if (ItemsSource == null || ItemsSource.Count == 0)
                return;

            int idx = SelectedIndex;

            if (idx < 0 || idx >= ItemsSource.Count)
            {
                idx = NormalizeIndex((int)Math.Round(_virtualCenterIndex));
                if (idx < 0) idx = 0;
                if (idx >= ItemsSource.Count) idx = ItemsSource.Count - 1;
            }

            _virtualCenterIndex = idx;

            _suppressSelectedIndexCallback = true;
            SelectedIndex = idx;
            _suppressSelectedIndexCallback = false;

            _suppressSelectedItemCallback = true;
            SelectedItem = ItemsSource[idx];
            _suppressSelectedItemCallback = false;

            _lastBaseCenterRawIndex = int.MinValue;
            _lastItemsCount = -1;

            UpdateVisualFromVirtualIndex();
        }

        private void OnVisibleItemsCountChangedInternal()
        {
            UpdateControlHeight();
            RebuildItems();
            UpdateClipGeometry();
        }

        private void OnAppearanceChangedInternal()
        {
            UpdateItemVisualStates();
            UpdateClipGeometry();
        }
    }
}
