namespace SBC.WheelPicker;

/// <summary>Provides data for the <see cref="WheelPicker.SelectedItemChanged"/> events.</summary>
/// <param name="previousItem">The value before the change.</param>
/// <param name="currentItem">The value after the change.</param>
public sealed class ItemChangedEventArgs(object? previousItem, object? currentItem) : EventArgs
{
    /// <summary>Gets the value before the selection change.</summary>
    public object? PreviousItem { get; } = previousItem;

    /// <summary>Gets the value after the selection change.</summary>
    public object? CurrentItem { get; } = currentItem;
}