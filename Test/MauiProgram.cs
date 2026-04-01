using Microsoft.Extensions.Logging;
using Test.Pages;
using Test.Services;
using Test.ViewModels;
using CommunityToolkit.Maui;
using Test.Views;


namespace Test
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
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            // ── HttpClient ────────────────────────────────────────
            builder.Services.AddSingleton<HttpClient>(_ => new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)  // ← Offline sẽ fail sau 5 giây thay vì 100 giây
            });

            // Services
            builder.Services.AddSingleton<IAuthService, AuthService>();
            builder.Services.AddSingleton<IApiService, ApiService>();
            builder.Services.AddSingleton<ILocalCacheService, LocalCacheService>();

            builder.Services.AddSingleton<AppShell>();

            // ViewModels
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            builder.Services.AddTransient<MainViewModel>();

            // Pages
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<WeekView>();


#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
