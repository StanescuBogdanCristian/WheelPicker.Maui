using Microsoft.Maui.Controls.Shapes;
using System.Runtime.CompilerServices;

namespace SBC.WheelPicker.Helpers;

internal static class ViewHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDifferent(double a, double b, double eps = 1e-3) => Math.Abs(a - b) > eps;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetScale(this View v, double value)
    {
        if (IsDifferent(v.Scale, value))
            v.Scale = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetOpacity(this View v, double value)
    {
        if (IsDifferent(v.Opacity, value))
            v.Opacity = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetRotationX(this View v, double value)
    {
        if (IsDifferent(v.RotationX, value))
            v.RotationX = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetTranslationY(this View v, double value)
    {
        if (IsDifferent(v.TranslationY, value))
            v.TranslationY = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void HideOrShow(this View child, bool hidden)
    {
        if (hidden)
        {
            child.Clip ??= new RectangleGeometry { Rect = Rect.Zero };
        }
        else
        {
            if (child.Clip != null)
                child.Clip = null;
        }
    }
}
