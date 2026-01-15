using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Maui.Core.Platform;

namespace WheelPicker.Maui.Sample.Views;

public partial class SplashPage : ContentPage
{
    private const string WheelPickerFullText = "WheelPicker";

    public SplashPage()
    {
        InitializeComponent();
        WheelPickerLabel.Text = string.Empty;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        MainThread.BeginInvokeOnMainThread(async () => await RunIntroAsync());
    }

    async Task RunIntroAsync()
    {
        var backgroundColor = GetColor(GetColor("White"), GetColor("OffBlack"));
        var logoStartColor = GetColor("PrimaryDark");
        var logoColor = GetColor(GetColor("Black"), GetColor("White"));
        uint length = 1500;
        var easing = Easing.CubicOut;

        await Task.Delay(500);

        await Task.WhenAll(
            ColorToAsync(this, BackgroundColor, backgroundColor, c =>
            {
                BackgroundColor = c;
                SetStatusBarStyle(c);
            }, length, easing),

            ColorToAsync(LogoImage, logoStartColor, logoColor, SetLogoTintColor, length, easing)
        );

        await TypeTextAsync(WheelPickerLabel, WheelPickerFullText, ms: 50);

        await Shell.Current.GoToAsync($"//{nameof(PropertiesPage)}");
    }

    static void SetStatusBarStyle(Color color)
    {
        try
        {
#pragma warning disable CA1416 // Validate platform compatibility
            StatusBar.SetColor(color);
            StatusBar.SetStyle(color.IsDarkForTheEye() ? StatusBarStyle.LightContent : StatusBarStyle.DarkContent);
#pragma warning restore CA1416 // Validate platform compatibility
        }
        catch { }
    }

    void SetLogoTintColor(Color color)
    {
        try
        {
            if (!LogoImage.Behaviors.Any())
                LogoImage.Behaviors.Add(new IconTintColorBehavior() { TintColor = color });
            else
                ((IconTintColorBehavior)LogoImage.Behaviors[0]).TintColor = color;
        }
        catch { }
    }

    static async Task TypeTextAsync(Label label, string text, int ms)
    {
        for (int i = 1; i <= text.Length; i++)
        {
            label.Text = text.Substring(0, i);
            await Task.Delay(ms);
        }
    }

    static Task ColorToAsync(VisualElement visualElement, Color fromColor, Color toColor, Action<Color> callback, uint length, Easing easing)
    {
        var tcs = new TaskCompletionSource();

        Color transform(double t) =>
            Color.FromRgba(fromColor.Red + t * (toColor.Red - fromColor.Red),
                           fromColor.Green + t * (toColor.Green - fromColor.Green),
                           fromColor.Blue + t * (toColor.Blue - fromColor.Blue),
                           fromColor.Alpha + t * (toColor.Alpha - fromColor.Alpha));

        void finish()
        {
            if (!tcs.Task.IsCompleted)
                tcs.TrySetResult();
        }

        visualElement.Animate("ColorTo", transform, callback, 16, length, easing, (v, c) => finish());

        return tcs.Task;
    }

    static Color GetColor(Color lightColor, Color darkColor)
    {
        return GetCurrentAppTheme() == AppTheme.Light ? lightColor : darkColor;
    }

    static Color GetColor(string colorKey)
    {
        return (Color)((Application.Current?.Resources.TryGetValue(colorKey, out var color) ?? false) ? color : Colors.Transparent);
    }

    static AppTheme? GetCurrentAppTheme()
    {
        if (Application.Current?.UserAppTheme == AppTheme.Unspecified)
        {
            return Application.Current.PlatformAppTheme;
        }

        return Application.Current?.UserAppTheme;
    }
}