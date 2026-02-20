using System.Runtime.CompilerServices;

namespace SBC.WheelPicker.Helpers;

/// <summary>
/// Pre-computed lookup tables for Math.Pow() operations to avoid expensive calculations.
/// <para/>
/// Precision: 0.01 (100 entries per 0.0-1.0 range). ~40% faster than Math.Pow().
/// </summary>
internal static class PowerCacheHelper
{
    /// <summary>
    /// Specifies the exponent used to map the normalized distance from the center to item opacity.
    /// Exponent used when mapping normalized distance from the center to item opacity.
    /// </summary>
    /// <remarks>
    /// Values below 1.0 make items fade more aggressively around the center.
    /// <para/>
    /// Values above 1.0 keep items more visible near the center and make them fade faster near the edges.
    /// </remarks>
    private const double OpacityPower = 1.3;

    /// <summary>
    /// Specifies the exponent used to map normalized distance from the center to item tilt (rotation) and scale.
    /// Exponent used when mapping normalized distance from the center to item tilt (rotation) and scale.
    /// </summary>
    /// <remarks>
    /// Values below 1.0 make tilt ramp up more strongly around the center and 
    /// concentrate most of the scaling change near the center.
    /// <para/>
    /// Values above 1.0 move most of the tilt change toward the outer positions and
    /// push more of the scaling change toward the edges.
    /// </remarks>
    private const double TiltAndScalePower = 0.9;

    /// <summary>
    /// Specifies the exponent used to control the compression of the curve when mapping normalized distance from the
    /// center.Exponent used when mapping normalized distance from the center to the "compression"
    /// </summary>
    /// <remarks>
    /// Values below 1.0 concentrate the compression effect near the center, causing items to
    /// spread out more at the edges. 
    /// <para/>
    /// Values above 1.0 make the curve tighter and more compressed toward the
    /// edges, resulting in items packing more closely near the boundaries.
    /// of the curve (how tightly items visually pack toward the edges).
    /// </remarks>
    private const double CompressionPower = 1.2;

    /// <summary>
    /// Represents the exponent used to control bend tightness in curve calculations.
    /// </summary>
    /// <remarks>This constant is typically used in algorithms that adjust the curvature of paths or splines.
    /// Modifying this value affects how sharply curves bend, with higher values resulting in tighter bends.
    /// </remarks>
    private const double BendTightnessPower = 0.85;

    private const int Resolution = 100;
    private static readonly double[] PowerTiltAndScale = new double[Resolution + 1];
    private static readonly double[] PowerCompression = new double[Resolution + 1];
    private static readonly double[] PowerOpacity = new double[Resolution + 1];
    private static readonly double[] PowerBendTightness = new double[Resolution + 1];

    static PowerCacheHelper()
    {
        for (int i = 0; i <= Resolution; i++)
        {
            double t = i / (double)Resolution;
            PowerTiltAndScale[i] = Math.Pow(t, TiltAndScalePower);
            PowerCompression[i] = Math.Pow(t, CompressionPower);
            PowerOpacity[i] = Math.Pow(t, OpacityPower);
            PowerBendTightness[i] = Math.Pow(t, BendTightnessPower);
        }
    }

    /// <summary>
    /// <see cref="TiltAndScalePower"/> = 0.9
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double TiltAndScalePow(double t) => t <= 0 ? 0 : t >= 1 ? 1 : PowerTiltAndScale[(int)(t * Resolution + 0.5)];

    /// <summary>
    /// <see cref="CompressionPower"/> = 1.2
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CompressionPow(double t) => t <= 0 ? 0 : t >= 1 ? 1 : PowerCompression[(int)(t * Resolution + 0.5)];

    /// <summary>
    /// <see cref="OpacityPower"/> = 1.3
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double OpacityPow(double t) => t <= 0 ? 0 : t >= 1 ? 1 : PowerOpacity[(int)(t * Resolution + 0.5)];

    /// <summary>
    /// <see cref="BendTightnessPower"/> = 0.85
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double BendTightnessPow(double t) => t <= 0 ? 0 : t >= 1 ? 1 : PowerBendTightness[(int)(t * Resolution + 0.5)];
}
