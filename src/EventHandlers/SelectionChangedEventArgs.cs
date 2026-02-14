namespace SBC.WheelPicker;

/// <summary>
/// Provides data for the <see cref="WheelPicker.SelectedIndexChanged"/>
/// and <see cref="WheelPicker.SelectedItemChanged"/> events.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="SelectionChangedEventArgs"/>.
/// </remarks>
/// <param name="previousSelection">The value before the change.</param>
/// <param name="currentSelection">The value after the change.</param>
public sealed class SelectionChangedEventArgs(object? previousSelection, object? currentSelection) : EventArgs
{
    /// <summary>Gets the value before the selection change.</summary>
    public object? PreviousSelection { get; } = previousSelection;

    /// <summary>Gets the value after the selection change.</summary>
    public object? CurrentSelection { get; } = currentSelection;
}