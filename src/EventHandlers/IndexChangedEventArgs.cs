namespace SBC.WheelPicker;

/// <summary>Provides data for the <see cref="WheelPicker.SelectedIndexChanged"/> events.</summary>
/// <param name="previousIndex">The index before the change.</param>
/// <param name="currentIndex">The index after the change.</param>
public sealed class IndexChangedEventArgs(int previousIndex, int currentIndex) : EventArgs
{
    /// <summary>Gets the index before the selection change.</summary>
    public int PreviousIndex { get; } = previousIndex;

    /// <summary>Gets the index after the selection change.</summary>
    public int CurrentIndex { get; } = currentIndex;
}
