using CommunityToolkit.Maui.Core;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;
using H.NotifyIcon.Core;
using App = SoundboardMAUI.App;

namespace JabberJay;

public static class MauiProgram
{
  public static MauiApp CreateMauiApp()
  {
    var builder = MauiApp.CreateBuilder();
    builder
      .UseMauiApp<App>()
      .UseNotifyIcon()
      .ConfigureFonts(fonts =>
      {
        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
      });

    builder.Services.AddSingleton<IKeyboardListener, KeyboardListenerWinUI>();
    builder.Services.AddTransient<MainPage>();
    
    AppClosingHandler.HandleAppClosing(builder);
      
#if DEBUG
    builder.Logging.AddDebug();
#endif

    builder.UseMauiApp<App>().UseMauiCommunityToolkitCore();
    
    return builder.Build();
  }
}