using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
#if WINDOWS
using H.NotifyIcon;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using WinRT.Interop;
#endif
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

using App = SoundboardMAUI.App;

namespace JabberJay;

public static class MauiProgram
{
	private static bool _hasBeenHidden = false;
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
      }).ConfigureLifecycleEvents(events =>
      {
#if WINDOWS
	      events.AddWindows(windowLifecycleBuilder =>
	      {
		      windowLifecycleBuilder.OnWindowCreated(window =>
		      {
			      window.Activated += (sender, args) =>
			      {
				      if (_hasBeenHidden) return;
				      AppWindow? appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(window)));
				      appWindow?.Hide();
				      _hasBeenHidden = true;
			      };
		      });
	      });
#endif
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