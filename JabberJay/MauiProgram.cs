using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
#if WINDOWS
using H.NotifyIcon;
#endif
using Microsoft.Extensions.Logging;
using App = SoundboardMAUI.App;

namespace JabberJay;

public static class MauiProgram
{
  public static MauiApp CreateMauiApp()
  {
    var builder = MauiApp.CreateBuilder();
    builder
      .UseMauiApp<App>()
      #if WINDOWS
      .UseNotifyIcon()
      #endif
      .UseMauiCommunityToolkit()
      .ConfigureFonts(fonts =>
      {
        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        fonts.AddFont("fontawesome.ttf", "FontAwesome");
      });
    
    #if WINDOWS
    builder.Services.AddSingleton<IKeyboardListener, KeyboardListenerWinUI>();
    #endif
	  builder.Services.AddSingleton<ApiService>();
    builder.Services.AddTransient<MainPage>();
    
    #if WINDOWS
    AppClosingHandler.HandleAppClosing(builder);
    #endif
      
#if DEBUG
    builder.Logging.AddDebug();
#endif

    builder.UseMauiApp<App>().UseMauiCommunityToolkitCore();
    
    return builder.Build();
  }
}