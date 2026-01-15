using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace WheelPicker.Maui.Sample
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("Poppins-Regular", "PoppinsRegular");
                    fonts.AddFont("Poppins-Bold", "PoppinsBold");
                    fonts.AddFont("SF-Pro", "SanFrancisco");
                    fonts.AddFont("Roboto-Regular.ttf", "RobotoRegular");
                });
#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
