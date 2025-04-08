using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using SoundboardMAUI.WinUI;

namespace SoundboardMAUI;

public static class MauiProgram
{
  public static MauiApp CreateMauiApp()
  {
    var builder = MauiApp.CreateBuilder();
    builder
      .UseMauiApp<App>()
      .ConfigureFonts(fonts =>
      {
        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
      });

    builder.Services.AddSingleton<IKeyboardListener, KeyboardListenerWinUI>();
    builder.Services.AddTransient<MainPage>();
      
#if DEBUG
    builder.Logging.AddDebug();
#endif

    builder.UseMauiApp<App>().UseMauiCommunityToolkitCore();
    
    return builder.Build();
  }
}