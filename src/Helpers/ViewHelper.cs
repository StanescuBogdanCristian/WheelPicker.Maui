using Microsoft.Maui.Controls.Shapes;
using System.Runtime.CompilerServices;

namespace SBC.WheelPicker.Helpers;

internal static class ViewHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDifferent(double a, double b, double eps = 1e-3) => Math.Abs(a - b) > eps;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetScale(this View view, double value)
    {
        if (IsDifferent(view.Scale, value))
            view.Scale = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetOpacity(this View view, double value)
    {
        if (IsDifferent(view.Opacity, value))
            view.Opacity = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetRotationX(this View view, double value)
    {
        if (IsDifferent(view.RotationX, value))
            view.RotationX = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetTranslationY(this View view, double value)
    {
        if (IsDifferent(view.TranslationY, value))
            view.TranslationY = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void HideOrShow(this View view, bool hidden)
    {
#if !WINDOWS
        return;
#endif
        if (hidden)
        {
            view.Clip ??= new RectangleGeometry { Rect = Rect.Zero };
        }
        else
        {
            if (view.Clip != null)
                view.Clip = null;
        }
    }
}
