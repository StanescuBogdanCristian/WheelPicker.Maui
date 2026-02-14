using SBC.PanContainer;
using System.ComponentModel;
using System.Windows.Input;

namespace SBC.WheelPicker;

public partial class WheelPicker
{
    /// <summary>Velocity threshold (DIPs/sec) required to trigger a wheel fling.</summary>
    /// <remarks>Optimized for wheel-style interaction. Lower values allow easier multi-item scrolling.</remarks>
    /// <value>Default is <c>220</c> DIPs/sec.</value>
    public override double FlingVelocityThreshold
    {
        get => base.FlingVelocityThreshold;
        set => base.FlingVelocityThreshold = value;
    }

    /// <summary>Minimum velocity (DIPs/sec) at which wheel inertia continues.</summary>
    /// <remarks>Lower values allow smooth micro-coasting before snapping.</remarks>
    /// <value>Default is <c>30</c> DIPs/sec.</value>
    public override double InertiaMinVelocity
    {
        get => base.InertiaMinVelocity;
        set => base.InertiaMinVelocity = value;
    }

    /// <summary>Deceleration rate (DIPs/sec²) applied during wheel inertia.</summary>
    /// <remarks>Higher values cause a faster, more decisive stop.</remarks>
    /// <value>Default is <c>1800</c> DIPs/sec².</value>
    public override double InertiaDeceleration
    {
        get => base.InertiaDeceleration;
        set => base.InertiaDeceleration = value;
    }

    #region Hidden. For internal use or unnecessary

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker control don't need this event.", true)]
    public new event EventHandler<PressEventArgs>? Pressed;

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker control don't need this event.", true)]
    public new event EventHandler<PressEventArgs>? Released;

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker control don't need this event.", true)]
    public new event EventHandler<PressEventArgs>? Panning;

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker control don't need this event.", true)]
    public new event EventHandler<PressEventArgs>? Inertia;

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker controls this internally.", true)]
    public override bool IsPanEnabled
    {
        get => base.IsPanEnabled;
        set => base.IsPanEnabled = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker controls this internally.", true)]
    public override AllowedPanDirections AllowedDirections
    {
        get => base.AllowedDirections;
        set => throw new NotSupportedException();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker controls this internally.", true)]
    public override double GestureThreshold
    {
        get => base.GestureThreshold;
        set => base.GestureThreshold = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker controls don't need this.", true)]
    public override bool DeferToChildGestures
    {
        get => base.DeferToChildGestures;
        set => base.DeferToChildGestures = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker controls don't need this.", true)]
    public override ICommand? PanCommand
    {
        get => base.PanCommand;
        set => base.PanCommand = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker controls don't need this.", true)]
    public override object? PanCommandParameter
    {
        get => base.PanCommandParameter;
        set => base.PanCommandParameter = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker controls don't need this.", true)]
    public override ICommand? InertiaCommand
    {
        get => base.InertiaCommand;
        set => base.InertiaCommand = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker controls don't need this.", true)]
    public override object? InertiaCommandParameter
    {
        get => base.InertiaCommandParameter;
        set => base.InertiaCommandParameter = value;
    }

    #endregion
}
