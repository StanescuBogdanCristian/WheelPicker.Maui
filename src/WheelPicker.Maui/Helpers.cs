namespace WheelPicker.Maui
{
    internal static class Helpers
    {
        private static bool IsDifferent(double a, double b, double eps = 1e-3) => Math.Abs(a - b) > eps;

        public static void SetScale(this View v, double value)
        {
            if (IsDifferent(v.Scale, value))
                v.Scale = value;
        }

        public static void SetOpacity(this View v, double value)
        {
            if (IsDifferent(v.Opacity, value))
                v.Opacity = value;
        }

        public static void SetRotationX(this View v, double value)
        {
            if (IsDifferent(v.RotationX, value))
                v.RotationX = value;
        }

        public static void SetTranslationY(this View v, double value)
        {
            if (IsDifferent(v.TranslationY, value))
                v.TranslationY = value;
        }
    }
}
