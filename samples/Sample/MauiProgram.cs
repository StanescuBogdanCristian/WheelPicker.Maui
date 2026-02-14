using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SBC.WheelPicker;

namespace Sample
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseWheelPicker()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("Poppins-Regular", "PoppinsRegular");
                    fonts.AddFont("Poppins-Bold", "PoppinsBold");
                    fonts.AddFont("SF-Pro", "SanFrancisco");
                    fonts.AddFont("Roboto-Regular.ttf", "RobotoRegular");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                });
#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
