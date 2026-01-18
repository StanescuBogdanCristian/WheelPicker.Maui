using System.Collections;

namespace WheelPicker.Maui;

public partial class WheelPicker
{
    /// <summary>
    /// Identifies the <see cref="ItemsSource"/> bindable property, which
    /// represents the collection of items displayed by the <see cref="WheelPicker"/>.
    /// </summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
           propertyName: nameof(ItemsSource),
           returnType: typeof(IList),
           declaringType: typeof(WheelPicker),
           defaultValue: default(IList),
           propertyChanged: OnItemsSourceChanged);

    /// <summary>
    /// Identifies the <see cref="ItemTemplate"/> bindable property, which specifies
    /// the template used to visualize each item in the <see cref="WheelPicker"/>.
    /// </summary>
    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
           propertyName: nameof(ItemTemplate),
           returnType: typeof(DataTemplate),
           declaringType: typeof(WheelPicker),
           defaultValue: default(DataTemplate),
           propertyChanged: OnItemTemplateChanged);

    /// <summary>
    /// Identifies the <see cref="SelectedIndex"/> bindable property, which represents
    /// the index of the currently selected item in <see cref="ItemsSource"/>.
    /// </summary>
    /// <remarks>
    /// This property supports two-way data binding.
    /// <para/>
    /// Default value is -1. 
    /// </remarks>
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
           propertyName: nameof(SelectedIndex),
           returnType: typeof(int),
           declaringType: typeof(WheelPicker),
           defaultValue: -1,
           defaultBindingMode: BindingMode.TwoWay,
           coerceValue: CoerceSelectedIndex,
           propertyChanged: OnSelectedIndexChanged);

    /// <summary>
    /// Identifies the <see cref="SelectedItem"/> bindable property, which represents
    /// the currently selected item in the wheel.
    /// </summary>
    /// <remarks>
    /// This property supports two-way data binding.
    /// </remarks>
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
           propertyName: nameof(SelectedItem),
           returnType: typeof(object),
           declaringType: typeof(WheelPicker),
           defaultValue: default,
           defaultBindingMode: BindingMode.TwoWay,
           propertyChanged: OnSelectedItemChanged);

    /// <summary>
    /// Identifies the <see cref="IsSelectionAnimated"/> bindable property, which
    /// controls whether selection changes are animated.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="true"/>.
    /// </remarks>
    public static readonly BindableProperty IsSelectionAnimatedProperty = BindableProperty.Create(
           propertyName: nameof(IsSelectionAnimated),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: true);

    /// <summary>
    /// Identifies the <see cref="SelectionThreshold"/> bindable property,
    /// which controls how wide the selection band is around the center of the wheel.
    /// </summary>
    /// <remarks>
    /// Valid range: 0.0 – 1.0. Typical useful range: 0.25–0.50.
    /// <para/>
    /// Default value is 0.40.
    /// </remarks>
    public static readonly BindableProperty SelectionThresholdProperty = BindableProperty.Create(
           propertyName: nameof(SelectionThreshold),
           returnType: typeof(double),
           declaringType: typeof(WheelPicker),
           defaultValue: 0.40d,
           coerceValue: CoerceSelectionThreshold);

    /// <summary>
    /// Identifies the <see cref="Overlay"/> bindable property, which specifies
    /// the view displayed on top of the wheel.
    /// </summary>
    public static readonly BindableProperty OverlayProperty = BindableProperty.Create(
           propertyName: nameof(Overlay),
           returnType: typeof(View),
           declaringType: typeof(WheelPicker),
           defaultValue: default(View),
           propertyChanged: OnOverlayChanged);

    /// <summary>
    /// Identifies the <see cref="Loop"/> bindable property, which controls whether
    /// the wheel loops when scrolling past the first or last item.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="true"/>.
    /// </remarks>
    public static readonly BindableProperty LoopProperty = BindableProperty.Create(
           propertyName: nameof(Loop),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: true,
           propertyChanged: OnLoopChanged);

    /// <summary>
    /// Identifies the <see cref="IsSwipeEnabled"/> bindable property, which controls
    /// whether the user can interact with the wheel using swipe/drag gestures.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="true"/>.
    /// </remarks>
    public static readonly BindableProperty IsSwipeEnabledProperty = BindableProperty.Create(
           propertyName: nameof(IsSwipeEnabled),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: true,
           propertyChanged: OnIsSwipeEnabledChanged);

    /// <summary>
    /// Identifies the <see cref="VisibleItemsCount"/> bindable property, which specifies
    /// how many items are visible at once in the wheel.
    /// </summary>
    /// <remarks>
    /// Valid range: odd integers from 3 to 11.
    /// <para/>
    /// Default value is 5 (an odd value so the center item is the selection).
    /// </remarks>
    public static readonly BindableProperty VisibleItemsCountProperty = BindableProperty.Create(
           propertyName: nameof(VisibleItemsCount),
           returnType: typeof(int),
           declaringType: typeof(WheelPicker),
           defaultValue: 5,
           coerceValue: CoerceVisibleItemsCount,
           propertyChanged: OnVisibleItemsCountChanged);

    /// <summary>
    /// Identifies the <see cref="CurvatureFactor"/> bindable property, which controls
    /// the intensity of the wheel’s curvature / 3D bend effect.
    /// </summary>
    /// <remarks>
    /// Lower values make the wheel flatter
    /// higher values increase the perceived curve.
    /// <para/>
    /// Valid range: 0.0 – 1.0.
    /// <para/>
    /// Default value is 1.0. 
    /// </remarks>
    public static readonly BindableProperty CurvatureFactorProperty = BindableProperty.Create(
           propertyName: nameof(CurvatureFactor),
           returnType: typeof(double),
           declaringType: typeof(WheelPicker),
           defaultValue: 1d,
           coerceValue: CoerceCurvatureFactor,
           propertyChanged: OnCurvatureFactorChanged);

    /// <summary>
    /// Identifies the <see cref="EdgeItemTiltAngle"/> bindable property, which
    /// specifies the maximum rotation angle (in degrees) applied to items near the wheel’s edges.
    /// </summary>
    /// <remarks>
    /// The center item is typically not tilted (0°), while items toward the edges
    /// are rotated up to this angle to create the 3D wheel effect.
    /// <para/>
    /// Valid range: 0.0 – 90.0 degrees.
    /// <para/>
    /// Default value is 70.0 degrees.
    /// </remarks>
    public static readonly BindableProperty EdgeItemTiltAngleProperty = BindableProperty.Create(
           propertyName: nameof(EdgeItemTiltAngle),
           returnType: typeof(double),
           declaringType: typeof(WheelPicker),
           defaultValue: 70.0d,
           coerceValue: CoerceEdgeItemTiltAngle,
           propertyChanged: OnEdgeItemTiltAngleChanged);

    /// <summary>
    /// Identifies the <see cref="EdgeItemScale"/> bindable property, which
    /// specifies the minimum scale applied to items near the wheel’s edges.
    /// </summary>
    /// <remarks>
    /// Valid range: 0.1 – 1.0.
    /// <para/>
    /// Default value is 0.5.
    /// </remarks>
    public static readonly BindableProperty EdgeItemScaleProperty = BindableProperty.Create(
           propertyName: nameof(EdgeItemScale),
           returnType: typeof(double),
           declaringType: typeof(WheelPicker),
           defaultValue: 0.5d,
           coerceValue: CoerceEdgeItemScale,
           propertyChanged: OnEdgeItemScaleChanged);

    /// <summary>
    /// Identifies the <see cref="EdgeItemOpacity"/> bindable property, which
    /// specifies the minimum opacity applied to items near the wheel’s edges.
    /// </summary>
    /// <remarks>
    /// Valid range: 0.1 – 1.0.
    /// <para/>
    /// Default value is 0.1.
    /// </remarks>
    public static readonly BindableProperty EdgeItemOpacityProperty = BindableProperty.Create(
           propertyName: nameof(EdgeItemOpacity),
           returnType: typeof(double),
           declaringType: typeof(WheelPicker),
           defaultValue: 0.1d,
           coerceValue: CoerceEdgeItemOpacity,
           propertyChanged: OnEdgeItemOpacityChanged);

    /// <summary>
    /// Identifies the <see cref="HapticFeedback"/> bindable property, which controls
    /// whether haptic feedback is triggered when the selected item changes.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="true"/>.
    /// </remarks>
    public static readonly BindableProperty HapticFeedbackProperty = BindableProperty.Create(
           propertyName: nameof(HapticFeedback),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: true);

    /// <summary>
    /// Identifies the <see cref="SoundFeedback"/> bindable property, which controls
    /// whether a sound effect is played when the selected item changes.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="true"/>.
    /// </remarks>
    public static readonly BindableProperty SoundFeedbackProperty = BindableProperty.Create(
           propertyName: nameof(SoundFeedback),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: true);

    /// <summary>
    /// Identifies the <see cref="IsDragging"/> bindable property, which indicates
    /// whether the user is currently dragging the wheel.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="false"/>. This property uses
    /// <see cref="BindingMode.OneWayToSource"/> to expose the state to view models.
    /// </remarks>
    public static readonly BindableProperty IsDraggingProperty = BindableProperty.Create(
           propertyName: nameof(IsDragging),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: false,
           defaultBindingMode: BindingMode.OneWayToSource);

    /// <summary>
    /// Identifies the <see cref="IsSpinning"/> bindable property, which indicates
    /// whether the wheel is currently spinning due to inertia or animation.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="false"/>. This property uses
    /// <see cref="BindingMode.OneWayToSource"/> to expose the state to view models.
    /// </remarks>
    public static readonly BindableProperty IsSpinningProperty = BindableProperty.Create(
           propertyName: nameof(IsSpinning),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: false,
           defaultBindingMode: BindingMode.OneWayToSource);

    /// <summary>
    /// Identifies the <see cref="ItemHeight"/> bindable property, which represents
    /// the height (in device-independent units) allocated for each item in the wheel.
    /// </summary>
    /// <remarks>
    /// Default value is 0. This value is typically computed based on <see cref="ItemTemplate"/> content size
    /// and <see cref="VisibleItemsCount"/> and is exposed for read-only binding.
    /// </remarks>
    public static readonly BindableProperty ItemHeightProperty = BindableProperty.Create(
           propertyName: nameof(ItemHeight),
           returnType: typeof(double),
           declaringType: typeof(WheelPicker),
           defaultValue: 0d);

    private static readonly BindableProperty PreviousVisualStateProperty = BindableProperty.CreateAttached(
            propertyName: "PreviousVisualState",
            returnType: typeof(string),
            declaringType: typeof(WheelPicker),
            defaultValue: default(string));

    /// <summary>
    /// Gets or sets the collection used to generate the items displayed by the wheel.
    /// </summary>
    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the template used to visualize each item in the wheel.
    /// </summary>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the index of the currently selected item within <see cref="ItemsSource"/>.
    /// </summary>
    /// <remarks>
    /// This property supports two-way data binding.
    /// <para/>
    /// Default value is -1. 
    /// </remarks>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>
    /// Gets or sets the currently selected item.
    /// </summary>
    /// <remarks>
    /// This property supports two-way data binding.
    /// </remarks>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether selection changes 
    /// are animated when the selected item changes.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="true"/>.
    /// </remarks>
    public bool IsSelectionAnimated
    {
        get => (bool)GetValue(IsSelectionAnimatedProperty);
        set => SetValue(IsSelectionAnimatedProperty, value);
    }

    /// <summary>
    /// Gets or sets the relative size of the selection band around the vertical
    /// center of the wheel, used when determining which item is considered selected.
    /// </summary>
    /// <remarks>
    /// Smaller values create a narrow selection band (only items very close to
    /// the center line are treated as selected).  
    /// Larger values widen the band, allowing items farther from the center
    /// to still count as selected. 
    /// <para/>
    /// Valid range: 0.0 – 1.0. Typical useful range: 0.25–0.50.
    /// <para/>
    /// Default value is 0.40.
    /// </remarks>
    public double SelectionThreshold
    {
        get => (double)GetValue(SelectionThresholdProperty);
        set => SetValue(SelectionThresholdProperty, value);
    }

    /// <summary>
    /// Gets or sets the overlay view displayed on top of the wheel.
    /// </summary>
    public View? Overlay
    {
        get => (View?)GetValue(OverlayProperty);
        set => SetValue(OverlayProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the wheel loops when 
    /// the user scrolls past the first or last item.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="true"/>.
    /// </remarks>
    public bool Loop
    {
        get => (bool)GetValue(LoopProperty);
        set => SetValue(LoopProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether swipe/drag gestures 
    /// are enabled for user interaction with the wheel.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="true"/>.
    /// </remarks>
    public bool IsSwipeEnabled
    {
        get => (bool)GetValue(IsSwipeEnabledProperty);
        set => SetValue(IsSwipeEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of items visible at once in the wheel 
    /// (typically an odd number so the center item is the selection).
    /// </summary>
    /// <remarks>
    /// Valid range: odd integers from 3 to 11.
    /// <para/>
    /// Default value is 5.
    /// </remarks>
    public int VisibleItemsCount
    {
        get => (int)GetValue(VisibleItemsCountProperty);
        set => SetValue(VisibleItemsCountProperty, value);
    }

    /// <summary>
    /// Gets or sets the intensity of the wheel's curvature / 3D bend effect. 
    /// Lower values make the wheel flatter higher values increase the perceived curve.
    /// </summary>
    /// <remarks>
    /// Lower values make the wheel flatter
    /// higher values increase the perceived curve.
    /// <para/>
    /// Valid range: 0.0 – 1.0.
    /// <para/>
    /// Default value is 1.0. 
    /// </remarks>
    public double CurvatureFactor
    {
        get => (double)GetValue(CurvatureFactorProperty);
        set => SetValue(CurvatureFactorProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum tilt angle, in degrees, applied to items near
    /// the edges of the wheel.
    /// </summary>
    /// <remarks>
    /// The center item is typically not tilted (0°), while items toward the edges
    /// are rotated up to this angle to create the 3D wheel effect.
    /// <para/>
    /// Valid range: 0.0 – 90.0 degrees.
    /// <para/>
    /// Default value is 70.0 degrees.
    /// </remarks>
    public double EdgeItemTiltAngle
    {
        get => (double)GetValue(EdgeItemTiltAngleProperty);
        set => SetValue(EdgeItemTiltAngleProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum scale applied to items near the top and bottom
    /// edges of the wheel.
    /// </summary>
    /// <remarks>
    /// A value less than 1.0 shrinks edge items relative to the center item
    /// (usually scaled at 1.0), enhancing the depth/curvature effect.
    /// <para/>
    /// Valid range: 0.1 – 1.0.
    /// <para/>
    /// Default value is 0.5.
    /// </remarks>
    public double EdgeItemScale
    {
        get => (double)GetValue(EdgeItemScaleProperty);
        set => SetValue(EdgeItemScaleProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum opacity applied to items near the top and bottom
    /// edges of the wheel. 
    /// </summary>
    /// <remarks>
    /// A value of 0.0 makes edge items fully transparent, while 1.0 keeps them fully
    /// opaque. The center item typically remains at full opacity, and items
    /// in between are interpolated.
    /// <para/>
    /// Valid range: 0.1 – 1.0.
    /// <para/>
    /// <para/>
    /// Default value is 0.1.
    /// </remarks>
    public double EdgeItemOpacity
    {
        get => (double)GetValue(EdgeItemOpacityProperty);
        set => SetValue(EdgeItemOpacityProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether haptic feedback is triggered 
    /// when the selected item changes.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="true"/>.
    /// </remarks>
    public bool HapticFeedback
    {
        get => (bool)GetValue(HapticFeedbackProperty);
        set => SetValue(HapticFeedbackProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a sound effect is played 
    /// when the selected item changes.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="true"/>.
    /// </remarks>
    public bool SoundFeedback
    {
        get => (bool)GetValue(SoundFeedbackProperty);
        set => SetValue(SoundFeedbackProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether the user is currently dragging the wheel.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="false"/>. This property uses
    /// <see cref="BindingMode.OneWayToSource"/> to expose the state to view models.
    /// </remarks>
    public bool IsDragging
    {
        get => (bool)GetValue(IsDraggingProperty);
        private set => SetValue(IsDraggingProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether the wheel is currently spinning 
    /// due to inertial scrolling or animation.
    /// </summary>
    /// <remarks>
    /// Default value is <see langword="false"/>. This property uses
    /// <see cref="BindingMode.OneWayToSource"/> to expose the state to view models.
    /// </remarks>
    public bool IsSpinning
    {
        get => (bool)GetValue(IsSpinningProperty);
        private set => SetValue(IsSpinningProperty, value);
    }

    /// <summary>
    /// Gets the height allocated for each item in the wheel,
    /// typically computed from the <see cref="ItemTemplate"/> content size and <see cref="VisibleItemsCount"/>.
    /// </summary>
    /// <remarks>
    /// Default value is 0. This value is exposed for read-only binding.
    /// </remarks>
    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        private set => SetValue(ItemHeightProperty, value);
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.OnItemsSourceChangedInternal(newValue as IList);
    }

    private static void OnItemTemplateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.ItemHeight = 0;
        control.RebuildItems();
    }

    private static object CoerceSelectedIndex(BindableObject bindable, object value)
    {
        var control = (WheelPicker)bindable;
        var list = control.ItemsSource;
        var index = (int)value;

        if (list == null || list.Count == 0)
            return -1;

        return control.NormalizeIndex(index);
    }

    private static void OnSelectedIndexChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.OnSelectedIndexChangedInternal((int)newValue);
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.OnSelectedItemChangedInternal(newValue);
    }

    private static object CoerceSelectionThreshold(BindableObject bindable, object value)
    {
        var v = (double)value;

        if (double.IsNaN(v) || double.IsInfinity(v))
            return 0.40d; // fallback to default

        return Math.Clamp(v, 0.0d, 1.0d);
    }

    private static void OnOverlayChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.OnOverlayChangedInternal(oldValue as View, newValue as View);
    }

    private static void OnLoopChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.OnLoopChangedInternal();
    }

    private static void OnIsSwipeEnabledChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;

        if (!(bool)newValue)
        {
            control.CancelAnimations();

            control.IsDragging = false;
            control.IsSpinning = false;
        }
    }

    private static object CoerceVisibleItemsCount(BindableObject bindable, object value)
    {
        var v = (int)value;
        v = Math.Clamp(v, 3, 11);

        // Ensure it's odd (prefer nearest odd)
        if (v % 2 == 0)
            v += v >= 11 ? -1 : 1;

        return v;
    }

    private static void OnVisibleItemsCountChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.OnVisibleItemsCountChangedInternal();
    }

    private static object CoerceCurvatureFactor(BindableObject bindable, object value)
    {
        var v = (double)value;
        return Math.Clamp(v, 0.0d, 1.0d);
    }

    private static void OnCurvatureFactorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.OnAppearanceChangedInternal();
    }

    private static object CoerceEdgeItemTiltAngle(BindableObject bindable, object value)
    {
        var v = (double)value;
        return Math.Clamp(v, 0.0d, 90.0d);
    }

    private static void OnEdgeItemTiltAngleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.OnAppearanceChangedInternal();
    }

    private static object CoerceEdgeItemScale(BindableObject bindable, object value)
    {
        var v = (double)value;
        return Math.Clamp(v, 0.1d, 1.0d);
    }

    private static void OnEdgeItemScaleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.OnAppearanceChangedInternal();
    }

    private static object CoerceEdgeItemOpacity(BindableObject bindable, object value)
    {
        var v = (double)value;
        return Math.Clamp(v, 0.1d, 1.0d);
    }

    private static void OnEdgeItemOpacityChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.OnAppearanceChangedInternal();
    }

    private static string? GetPreviousVisualState(View v) => (string?)v.GetValue(PreviousVisualStateProperty);

    private static void SetPreviousVisualState(View v, string? state) => v.SetValue(PreviousVisualStateProperty, state);
}