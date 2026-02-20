using SBC.WheelPicker.Helpers;
using System.Collections;
using System.Windows.Input;

namespace SBC.WheelPicker;

public partial class WheelPicker
{
    /// <summary>Identifies the <see cref="ItemsSource"/> bindable property.</summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
           propertyName: nameof(ItemsSource),
           returnType: typeof(IList),
           declaringType: typeof(WheelPicker),
           defaultValue: default(IList),
           propertyChanged: OnItemsSourceChanged);

    /// <summary>Identifies the <see cref="ItemTemplate"/> bindable property.</summary>
    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
           propertyName: nameof(ItemTemplate),
           returnType: typeof(DataTemplate),
           declaringType: typeof(WheelPicker),
           defaultValue: default(DataTemplate),
           propertyChanged: OnItemTemplateChanged);

    public static readonly BindableProperty ItemStringFormatProperty = BindableProperty.Create(
           propertyName: nameof(ItemStringFormat),
           returnType: typeof(string),
           declaringType: typeof(WheelPicker),
           defaultValue: default(string),
           coerceValue: CoerceItemStringFormat);

    /// <summary>Identifies the <see cref="ItemTextColor"/> bindable property.</summary>
    public static readonly BindableProperty ItemTextColorProperty = BindableProperty.Create(
           propertyName: nameof(ItemTextColor),
           returnType: typeof(Color),
           declaringType: typeof(WheelPicker));

    /// <summary>Identifies the <see cref="ItemFontSize"/> bindable property.</summary>
    public static readonly BindableProperty ItemFontSizeProperty = BindableProperty.Create(
           propertyName: nameof(ItemFontSize),
           returnType: typeof(double),
           declaringType: typeof(WheelPicker),
           defaultValue: 14d,
           propertyChanged: OnItemMetricsChanged);

    /// <summary>Identifies the <see cref="ItemFontAttributes"/> bindable property.</summary>
    public static readonly BindableProperty ItemFontAttributesProperty = BindableProperty.Create(
           propertyName: nameof(ItemFontAttributes),
           returnType: typeof(FontAttributes),
           declaringType: typeof(WheelPicker),
           defaultValue: FontAttributes.None,
           propertyChanged: OnItemMetricsChanged);

    /// <summary>Identifies the <see cref="ItemFontFamily"/> bindable property.</summary>
    public static readonly BindableProperty ItemFontFamilyProperty = BindableProperty.Create(
           propertyName: nameof(ItemFontFamily),
           returnType: typeof(string),
           declaringType: typeof(WheelPicker),
           defaultValue: default(string));

    /// <summary>Identifies the <see cref="ItemPadding"/> bindable property.</summary>
    public static readonly BindableProperty ItemPaddingProperty = BindableProperty.Create(
           propertyName: nameof(ItemPadding),
           returnType: typeof(Thickness),
           declaringType: typeof(WheelPicker),
           defaultValue: new Thickness(0),
           propertyChanged: OnItemMetricsChanged);

    /// <summary>Identifies the <see cref="ItemHorizontalTextAlignment"/> bindable property.</summary>
    public static readonly BindableProperty ItemHorizontalTextAlignmentProperty = BindableProperty.Create(
           propertyName: nameof(ItemHorizontalTextAlignment),
           returnType: typeof(TextAlignment),
           declaringType: typeof(WheelPicker),
           defaultValue: TextAlignment.Center);

    /// <summary>Identifies the <see cref="ItemVerticalTextAlignment"/> bindable property.</summary>
    public static readonly BindableProperty ItemVerticalTextAlignmentProperty = BindableProperty.Create(
           propertyName: nameof(ItemVerticalTextAlignment),
           returnType: typeof(TextAlignment),
           declaringType: typeof(WheelPicker),
           defaultValue: TextAlignment.Center);

    /// <summary>Identifies the <see cref="ItemLineBreakMode"/> bindable property.</summary>
    public static readonly BindableProperty ItemLineBreakModeProperty = BindableProperty.Create(
           propertyName: nameof(ItemLineBreakMode),
           returnType: typeof(LineBreakMode),
           declaringType: typeof(WheelPicker),
           defaultValue: LineBreakMode.NoWrap,
           propertyChanged: OnItemMetricsChanged);

    /// <summary>Identifies the <see cref="ItemMaxLines"/> bindable property.</summary>
    public static readonly BindableProperty ItemMaxLinesProperty = BindableProperty.Create(
           propertyName: nameof(ItemMaxLines),
           returnType: typeof(int),
           declaringType: typeof(WheelPicker),
           defaultValue: 1,
           propertyChanged: OnItemMetricsChanged);

    /// <summary>Identifies the <see cref="ItemFontAutoScalingEnabled"/> bindable property.</summary>
    public static readonly BindableProperty ItemFontAutoScalingEnabledProperty = BindableProperty.Create(
           propertyName: nameof(ItemFontAutoScalingEnabled),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: true,
           propertyChanged: OnItemMetricsChanged);

    /// <summary>Identifies the <see cref="SelectedIndex"/> bindable property.</summary>
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
           propertyName: nameof(SelectedIndex),
           returnType: typeof(int),
           declaringType: typeof(WheelPicker),
           defaultValue: -1,
           defaultBindingMode: BindingMode.TwoWay,
           coerceValue: CoerceSelectedIndex,
           propertyChanged: OnSelectedIndexChanged);

    /// <summary>Identifies the <see cref="SelectedItem"/> bindable property.</summary>
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
           propertyName: nameof(SelectedItem),
           returnType: typeof(object),
           declaringType: typeof(WheelPicker),
           defaultValue: default,
           defaultBindingMode: BindingMode.TwoWay,
           propertyChanged: OnSelectedItemChanged);

    /// <summary>Identifies the <see cref="IsSelectionAnimated"/> bindable property.</summary>
    public static readonly BindableProperty IsSelectionAnimatedProperty = BindableProperty.Create(
           propertyName: nameof(IsSelectionAnimated),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: true);

    /// <summary>Identifies the <see cref="SelectionThreshold"/> bindable property.</summary>
    public static readonly BindableProperty SelectionThresholdProperty = BindableProperty.Create(
           propertyName: nameof(SelectionThreshold),
           returnType: typeof(double),
           declaringType: typeof(WheelPicker),
           defaultValue: 0.40d,
           coerceValue: CoerceSelectionThreshold);

    /// <summary>Identifies the <see cref="Overlay"/> bindable property.</summary>
    public static readonly BindableProperty OverlayProperty = BindableProperty.Create(
           propertyName: nameof(Overlay),
           returnType: typeof(View),
           declaringType: typeof(WheelPicker),
           defaultValue: default(View),
           propertyChanged: OnOverlayChanged);

    /// <summary>Identifies the <see cref="Loop"/> bindable property.</summary>
    public static readonly BindableProperty LoopProperty = BindableProperty.Create(
           propertyName: nameof(Loop),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: true,
           propertyChanged: OnLoopChanged);

    /// <summary>Identifies the <see cref="IsSwipeEnabled"/> bindable property.</summary>
    public static readonly BindableProperty IsSwipeEnabledProperty = BindableProperty.Create(
           propertyName: nameof(IsSwipeEnabled),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: true,
           propertyChanged: OnIsSwipeEnabledChanged);

    /// <summary>Identifies the <see cref="VisibleItemsCount"/> bindable property.</summary>
    public static readonly BindableProperty VisibleItemsCountProperty = BindableProperty.Create(
           propertyName: nameof(VisibleItemsCount),
           returnType: typeof(int),
           declaringType: typeof(WheelPicker),
           defaultValue: 5,
           coerceValue: CoerceVisibleItemsCount,
           propertyChanged: OnVisibleItemsCountChanged);

    /// <summary>Identifies the <see cref="CurvatureFactor"/> bindable property.</summary>
    public static readonly BindableProperty CurvatureFactorProperty = BindableProperty.Create(
           propertyName: nameof(CurvatureFactor),
           returnType: typeof(double),
           declaringType: typeof(WheelPicker),
           defaultValue: 1d,
           coerceValue: CoerceCurvatureFactor,
           propertyChanged: OnCurvatureFactorChanged);

    /// <summary>Identifies the <see cref="EdgeItemTiltAngle"/> bindable property.</summary>
    public static readonly BindableProperty EdgeItemTiltAngleProperty = BindableProperty.Create(
           propertyName: nameof(EdgeItemTiltAngle),
           returnType: typeof(double),
           declaringType: typeof(WheelPicker),
           defaultValue: 70.0d,
           coerceValue: CoerceEdgeItemTiltAngle,
           propertyChanged: OnEdgeItemTiltAngleChanged);

    /// <summary>Identifies the <see cref="EdgeItemScale"/> bindable property.</summary>
    public static readonly BindableProperty EdgeItemScaleProperty = BindableProperty.Create(
           propertyName: nameof(EdgeItemScale),
           returnType: typeof(double),
           declaringType: typeof(WheelPicker),
           defaultValue: 0.5d,
           coerceValue: CoerceEdgeItemScale,
           propertyChanged: OnEdgeItemScaleChanged);

    /// <summary>Identifies the <see cref="EdgeItemOpacity"/> bindable property.</summary>
    public static readonly BindableProperty EdgeItemOpacityProperty = BindableProperty.Create(
           propertyName: nameof(EdgeItemOpacity),
           returnType: typeof(double),
           declaringType: typeof(WheelPicker),
           defaultValue: 0.1d,
           coerceValue: CoerceEdgeItemOpacity,
           propertyChanged: OnEdgeItemOpacityChanged);

    /// <summary>Identifies the <see cref="HapticFeedback"/> bindable property.</summary>
    public static readonly BindableProperty HapticFeedbackProperty = BindableProperty.Create(
           propertyName: nameof(HapticFeedback),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: true);

    /// <summary>Identifies the <see cref="SoundFeedback"/> bindable property.</summary>
    public static readonly BindableProperty SoundFeedbackProperty = BindableProperty.Create(
           propertyName: nameof(SoundFeedback),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: true);

    /// <summary>Identifies the <see cref="SelectedIndexChangedCommand"/> bindable property.</summary>
    public static readonly BindableProperty SelectedIndexChangedCommandProperty = BindableProperty.Create(
           propertyName: nameof(SelectedIndexChangedCommand),
           returnType: typeof(ICommand),
           declaringType: typeof(WheelPicker),
           defaultValue: default(ICommand));

    /// <summary>Identifies the <see cref="SelectedIndexChangedCommandParameter"/> bindable property.</summary>
    public static readonly BindableProperty SelectedIndexChangedCommandParameterProperty = BindableProperty.Create(
           propertyName: nameof(SelectedIndexChangedCommandParameter),
           returnType: typeof(object),
           declaringType: typeof(WheelPicker),
           defaultValue: default);

    /// <summary>Identifies the <see cref="SelectedItemChangedCommand"/> bindable property.</summary>
    public static readonly BindableProperty SelectedItemChangedCommandProperty = BindableProperty.Create(
           propertyName: nameof(SelectedItemChangedCommand),
           returnType: typeof(ICommand),
           declaringType: typeof(WheelPicker),
           defaultValue: default(ICommand));

    /// <summary>Identifies the <see cref="SelectedItemChangedCommandParameter"/> bindable property.</summary>
    public static readonly BindableProperty SelectedItemChangedCommandParameterProperty = BindableProperty.Create(
           propertyName: nameof(SelectedItemChangedCommandParameter),
           returnType: typeof(object),
           declaringType: typeof(WheelPicker),
           defaultValue: default);

    /// <summary>Identifies the <see cref="IsDragging"/> bindable property.</summary>
    public static readonly BindableProperty IsDraggingProperty = BindableProperty.Create(
           propertyName: nameof(IsDragging),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: false,
           defaultBindingMode: BindingMode.OneWayToSource);

    /// <summary>Identifies the <see cref="IsSpinning"/> bindable property.</summary>
    public static readonly BindableProperty IsSpinningProperty = BindableProperty.Create(
           propertyName: nameof(IsSpinning),
           returnType: typeof(bool),
           declaringType: typeof(WheelPicker),
           defaultValue: false,
           defaultBindingMode: BindingMode.OneWayToSource);

    private static readonly BindablePropertyKey ItemHeightPropertyKey = BindableProperty.CreateReadOnly(
           propertyName: nameof(ItemHeight),
           returnType: typeof(double),
           declaringType: typeof(WheelPicker),
           defaultValue: 0d);

    /// <summary>Identifies the <see cref="ItemHeight"/> bindable property (read-only).</summary>
    /// <remarks>
    /// ItemHeight is derived from the realized item template. It cannot be set from XAML or code outside of WheelPicker.
    /// </remarks>
    public static readonly BindableProperty ItemHeightProperty = ItemHeightPropertyKey.BindableProperty;

    private static readonly BindableProperty PreviousVisualStateProperty = BindableProperty.CreateAttached(
            propertyName: "PreviousVisualState",
            returnType: typeof(string),
            declaringType: typeof(WheelPicker),
            defaultValue: default(string));

    /// <summary>Gets or sets the collection used to generate the items displayed by the wheel.</summary>
    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Gets or sets the template used to visualize each item in the wheel.</summary>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>Gets or sets the string format used by the default item template.</summary>
    /// <remarks>This property is ignored when a custom <see cref="ItemTemplate"/> is provided.</remarks>
    public string? ItemStringFormat
    {
        get => (string?)GetValue(ItemStringFormatProperty);
        set => SetValue(ItemStringFormatProperty, value);
    }

    /// <summary>Gets or sets the text color used by the default item template.</summary>
    /// <remarks>This property is ignored when a custom <see cref="ItemTemplate"/> is provided.</remarks>
    public Color ItemTextColor
    {
        get => (Color)GetValue(ItemTextColorProperty);
        set => SetValue(ItemTextColorProperty, value);
    }

    /// <summary>Gets or sets the font size used by the default item template.</summary>
    /// <remarks>This property is ignored when a custom <see cref="ItemTemplate"/> is provided.</remarks>
    /// <value>Default is <c>14</c>.</value>
    public double ItemFontSize
    {
        get => (double)GetValue(ItemFontSizeProperty);
        set => SetValue(ItemFontSizeProperty, value);
    }

    /// <summary>Gets or sets the font attributes used by the default item template.</summary>
    /// <remarks>This property is ignored when a custom <see cref="ItemTemplate"/> is provided.</remarks>
    /// <value>Default is <see cref="FontAttributes.None"/>.</value>
    public FontAttributes ItemFontAttributes
    {
        get => (FontAttributes)GetValue(ItemFontAttributesProperty);
        set => SetValue(ItemFontAttributesProperty, value);
    }

    /// <summary>Gets or sets the font family used by the default item template.</summary>
    /// <remarks>This property is ignored when a custom <see cref="ItemTemplate"/> is provided.</remarks>
    public string ItemFontFamily
    {
        get => (string)GetValue(ItemFontFamilyProperty);
        set => SetValue(ItemFontFamilyProperty, value);
    }

    /// <summary>Gets or sets the padding used by the default item template.</summary>
    /// <remarks>This property is ignored when a custom <see cref="ItemTemplate"/> is provided.</remarks>
    public Thickness ItemPadding
    {
        get => (Thickness)GetValue(ItemPaddingProperty);
        set => SetValue(ItemPaddingProperty, value);
    }

    /// <summary>Gets or sets the horizontal text alignment of the default item template.</summary>
    /// <remarks>This property is ignored when a custom <see cref="ItemTemplate"/> is provided.</remarks>
    /// <value>Default is <see cref="TextAlignment.Center"/>.</value>
    public TextAlignment ItemHorizontalTextAlignment
    {
        get => (TextAlignment)GetValue(ItemHorizontalTextAlignmentProperty);
        set => SetValue(ItemHorizontalTextAlignmentProperty, value);
    }

    /// <summary>Gets or sets the vertical text alignment of the default item template.</summary>
    /// <remarks>This property is ignored when a custom <see cref="ItemTemplate"/> is provided.</remarks>
    /// <value>Default is <see cref="TextAlignment.Center"/>.</value>
    public TextAlignment ItemVerticalTextAlignment
    {
        get => (TextAlignment)GetValue(ItemVerticalTextAlignmentProperty);
        set => SetValue(ItemVerticalTextAlignmentProperty, value);
    }

    /// <summary>Gets or sets the line break mode of the default item template.</summary>
    /// <remarks>This property is ignored when a custom <see cref="ItemTemplate"/> is provided.</remarks>
    /// <value>Default is <see cref="LineBreakMode.NoWrap"/>.</value>
    public LineBreakMode ItemLineBreakMode
    {
        get => (LineBreakMode)GetValue(ItemLineBreakModeProperty);
        set => SetValue(ItemLineBreakModeProperty, value);
    }

    /// <summary>Gets or sets the max lines of the default item template.</summary>
    /// <remarks>This property is ignored when a custom <see cref="ItemTemplate"/> is provided.</remarks>
    /// <value>Default is <c>1</c>.</value>
    public int ItemMaxLines
    {
        get => (int)GetValue(ItemMaxLinesProperty);
        set => SetValue(ItemMaxLinesProperty, value);
    }

    /// <summary>Gets or sets whether font auto-scaling is enabled for the default item template.</summary>
    /// <remarks>This property is ignored when a custom <see cref="ItemTemplate"/> is provided.</remarks>
    /// <value>Default is <see langword="true"/>.</value>
    public bool ItemFontAutoScalingEnabled
    {
        get => (bool)GetValue(ItemFontAutoScalingEnabledProperty);
        set => SetValue(ItemFontAutoScalingEnabledProperty, value);
    }

    /// <summary>Gets or sets the index of the currently selected item within <see cref="ItemsSource"/>.</summary>
    /// <remarks>This property supports two-way data binding.</remarks>
    /// <value>Default is <c>-1</c>.</value>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>Gets or sets the currently selected item.</summary>
    /// <remarks>This property supports two-way data binding.</remarks>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether selection changes
    /// are animated when the selected item changes.
    /// </summary>
    /// <value>Default is <see langword="true"/>.</value>
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
    /// <para/>Valid range: 0.1 – 0.9. Typical useful range: 0.25–0.50.<para/>
    /// </remarks>
    /// <value>Default is <c>0.40</c>.</value>
    public double SelectionThreshold
    {
        get => (double)GetValue(SelectionThresholdProperty);
        set => SetValue(SelectionThresholdProperty, value);
    }

    /// <summary>Gets or sets the overlay view displayed on top of the wheel.</summary>
    public View? Overlay
    {
        get => (View?)GetValue(OverlayProperty);
        set => SetValue(OverlayProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the wheel loops when
    /// the user scrolls past the first or last item.
    /// </summary>
    /// <value>Default value is <see langword="true"/>.</value>
    public bool Loop
    {
        get => (bool)GetValue(LoopProperty);
        set => SetValue(LoopProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether swipe/drag gestures
    /// are enabled for user interaction with the wheel.
    /// </summary>
    /// <value>Default value is <see langword="true"/>.</value>
    public bool IsSwipeEnabled
    {
        get => (bool)GetValue(IsSwipeEnabledProperty);
        set => SetValue(IsSwipeEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of items visible at once in the wheel
    /// (typically an odd number so the center item is the selection).
    /// </summary>
    /// <remarks>Valid range: odd integers from 1 to 11.</remarks>
    /// <value>Default value is <c>5</c>.</value>
    public int VisibleItemsCount
    {
        get => (int)GetValue(VisibleItemsCountProperty);
        set => SetValue(VisibleItemsCountProperty, value);
    }

    /// <summary>
    /// Gets or sets the intensity of the wheel's curvature / 3D bend effect.
    /// </summary>
    /// <remarks>
    /// Lower values make the wheel flatter,
    /// higher values increase the perceived curve.
    /// <para/>Valid range: 0.0 – 1.0.
    /// </remarks>
    /// <value>Default value is <c>1.0</c>.</value>
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
    /// <para/>Valid range: 0.0 – 90.0 degrees.
    /// </remarks>
    /// <value>Default value is <c>70.0</c> degrees.</value>
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
    /// <para/><c>Valid range: 0.1 – 1.0</c>.
    /// </remarks>
    /// <value>Default value is <c>0.5</c>.</value>
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
    /// <para/>Valid range: 0.1 – 1.0.
    /// </remarks>
    /// <value>Default value is <c>0.1</c>.</value>
    public double EdgeItemOpacity
    {
        get => (double)GetValue(EdgeItemOpacityProperty);
        set => SetValue(EdgeItemOpacityProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether haptic feedback is triggered
    /// when the selected item changes.
    /// </summary>
    /// <value>Default value is <see langword="true"/>.</value>
    public bool HapticFeedback
    {
        get => (bool)GetValue(HapticFeedbackProperty);
        set => SetValue(HapticFeedbackProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a sound effect (tick) is played
    /// when the selected item changes.
    /// </summary>
    /// <value>Default value is <see langword="true"/>.</value>
    public bool SoundFeedback
    {
        get => (bool)GetValue(SoundFeedbackProperty);
        set => SetValue(SoundFeedbackProperty, value);
    }

    /// <summary>Gets or sets the command invoked when <see cref="SelectedIndex"/> changes.</summary>
    /// <remarks>Receives <see cref="SelectionChangedEventArgs"/> unless <see cref="SelectedIndexChangedCommandParameter"/> is set.</remarks>
    public ICommand? SelectedIndexChangedCommand
    {
        get => (ICommand?)GetValue(SelectedIndexChangedCommandProperty);
        set => SetValue(SelectedIndexChangedCommandProperty, value);
    }

    /// <summary>Gets or sets an explicit parameter passed to <see cref="SelectedIndexChangedCommand"/>.</summary>
    /// <remarks>If null, <see cref="SelectionChangedEventArgs"/> is passed instead.</remarks>
    public object? SelectedIndexChangedCommandParameter
    {
        get => GetValue(SelectedIndexChangedCommandParameterProperty);
        set => SetValue(SelectedIndexChangedCommandParameterProperty, value);
    }

    /// <summary>
    /// Gets or sets the command invoked when <see cref="SelectedItem"/> changes.
    /// </summary>
    /// <remarks>Receives <see cref="SelectionChangedEventArgs"/> unless <see cref="SelectedItemChangedCommandParameter"/> is set.</remarks>
    public ICommand? SelectedItemChangedCommand
    {
        get => (ICommand?)GetValue(SelectedItemChangedCommandProperty);
        set => SetValue(SelectedItemChangedCommandProperty, value);
    }

    /// <summary>Gets or sets an explicit parameter passed to <see cref="SelectedItemChangedCommand"/>.</summary>
    /// <remarks>If null, <see cref="SelectionChangedEventArgs"/> is passed instead.</remarks>
    public object? SelectedItemChangedCommandParameter
    {
        get => GetValue(SelectedItemChangedCommandParameterProperty);
        set => SetValue(SelectedItemChangedCommandParameterProperty, value);
    }

    /// <summary>Gets a value indicating whether the user is currently dragging the wheel.</summary>
    /// <remarks>
    /// This property uses <see cref="BindingMode.OneWayToSource"/> to expose the state to view models.
    /// </remarks>
    /// <value>Default value is <see langword="false"/>.</value>
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
    /// This property uses <see cref="BindingMode.OneWayToSource"/> to expose the state to view models.
    /// </remarks>
    /// <value>Default value is <see langword="false"/>.</value>
    public bool IsSpinning
    {
        get => (bool)GetValue(IsSpinningProperty);
        private set => SetValue(IsSpinningProperty, value);
    }

    /// <summary>
    /// Gets the height allocated for each item in the wheel,
    /// typically computed from the <see cref="ItemTemplate"/> content size and <see cref="VisibleItemsCount"/>.
    /// </summary>
    /// <remarks>This value is exposed for read-only binding.</remarks>
    /// <value>Default value is <c>0</c>.</value>
    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        private set => SetValue(ItemHeightPropertyKey, value);
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.OnItemsSourceChangedInternal(newValue as IList);
    }

    private static void OnItemTemplateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.OnItemTemplateChangedInternal();
    }

    private static object CoerceItemStringFormat(BindableObject bindable, object value)
    {
        return StringHelper.NormalizeStringFormat(value as string) ?? string.Empty;
    }

    private static void OnItemMetricsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.OnItemMetricsChangedInternal();
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
            return SelectionThresholdProperty.DefaultValue;

        return Math.Clamp(v, 0.1d, 0.9d);
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
        control.OnIsSwipeEnabledInternal((bool)newValue);
    }

    private static object CoerceVisibleItemsCount(BindableObject bindable, object value)
    {
        var v = (int)value;
        if (int.IsNegative(v))
            return VisibleItemsCountProperty.DefaultValue;

        v = Math.Clamp(v, 1, 11);

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
        if (double.IsNaN(v) || double.IsInfinity(v))
            return CurvatureFactorProperty.DefaultValue;

        return Math.Clamp(v, 0.0d, 1.0d);
    }

    private static void OnCurvatureFactorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.UpdateItemsRootHeight();
    }

    private static object CoerceEdgeItemTiltAngle(BindableObject bindable, object value)
    {
        var v = (double)value;
        if (double.IsNaN(v) || double.IsInfinity(v))
            return EdgeItemTiltAngleProperty.DefaultValue;

        return Math.Clamp(v, 0.1d, 90.0d);
    }

    private static void OnEdgeItemTiltAngleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.UpdateItemsRootHeight();
    }

    private static object CoerceEdgeItemScale(BindableObject bindable, object value)
    {
        var v = (double)value;
        if (double.IsNaN(v) || double.IsInfinity(v))
            return EdgeItemScaleProperty.DefaultValue;

        return Math.Clamp(v, 0.1d, 1.0d);
    }

    private static void OnEdgeItemScaleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.UpdateItemsRootHeight();
    }

    private static object CoerceEdgeItemOpacity(BindableObject bindable, object value)
    {
        var v = (double)value;
        if (double.IsNaN(v) || double.IsInfinity(v))
            return EdgeItemOpacityProperty.DefaultValue;

        return Math.Clamp(v, 0.1d, 1.0d);
    }

    private static void OnEdgeItemOpacityChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WheelPicker)bindable;
        control.UpdateItemVisualStates();
    }

    private static string? GetPreviousVisualState(View v) => (string?)v.GetValue(PreviousVisualStateProperty);

    private static void SetPreviousVisualState(View v, string? state) => v.SetValue(PreviousVisualStateProperty, state);
}
