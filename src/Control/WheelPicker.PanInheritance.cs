using SBC.PanContainer;
using System.ComponentModel;

namespace SBC.WheelPicker;

public partial class WheelPicker
{
    #region Hidden. For internal use or unnecessary

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker controls this internally.", true)]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override bool IsPanEnabled
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    {
        get => base.IsPanEnabled;
        set => base.IsPanEnabled = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("WheelPicker controls this internally.", true)]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override AllowedPanDirections AllowedDirections
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    {
        get => base.AllowedDirections;
        set => throw new NotSupportedException();
    }

    #endregion
}
