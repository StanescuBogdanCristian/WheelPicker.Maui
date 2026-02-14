using SBC.PanContainer;

namespace SBC.WheelPicker;

/// <summary>
/// Extension methods for registering <see cref="WheelPicker"/>  with the MAUI app builder.
/// </summary>
public static class AppBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="WheelPicker"/> handler with the MAUI application.
    /// </summary>
    /// <param name="builder">The <see cref="MauiAppBuilder"/> to configure.</param>
    /// <returns>The <see cref="MauiAppBuilder"/> for method chaining.</returns>
    /// <remarks>
    /// Call this method in your <c>MauiProgram.cs</c> to enable WheelPicker:
    /// </remarks>
    public static MauiAppBuilder UseWheelPicker(this MauiAppBuilder builder)
    {
        builder.UsePanContainer();

        return builder;
    }
}