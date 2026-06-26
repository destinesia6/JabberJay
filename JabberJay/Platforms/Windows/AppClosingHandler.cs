using Microsoft.Maui.LifecycleEvents;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using WinRT.Interop;

#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace JabberJay;

public static class AppClosingHandler
{
  private static AppWindow? _appWindow;
  private static bool _isClosingPrevented;
  public static event EventHandler PageHide;
#if WINDOWS
  private static IntPtr _hwnd;
  
  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool SetForegroundWindow(IntPtr hWnd);
  
  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

  private const int SW_RESTORE = 9;
#endif

  public static void HandleAppClosing(MauiAppBuilder builder)
  {
    builder.ConfigureLifecycleEvents(events =>
    {
#if WINDOWS
      events.AddWindows(windowLifetimeBuilder =>
      {
        windowLifetimeBuilder.OnWindowCreated(window =>
        {
          _hwnd = WindowNative.GetWindowHandle(window);
          
          _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd));
          if (_appWindow != null)
          {
            _appWindow.Closing += OnAppWindowClosing;
          }
        });
      });
#endif
    });
  }

  public static void ShowApp()
  {
    _appWindow?.Show();
    _isClosingPrevented = false;
    
#if WINDOWS
    if (_hwnd != IntPtr.Zero)
    {
      ShowWindow(_hwnd, SW_RESTORE);
      SetForegroundWindow(_hwnd);
    }
#endif
  }

  public static void OnAppWindowClosing(object? sender, AppWindowClosingEventArgs args)
  {
    if (!_isClosingPrevented)
    {
      args.Cancel = true;
      _isClosingPrevented = true;
      if (_appWindow != null)
      {
        _appWindow.Hide();
      }

      PageHide.Invoke(null, EventArgs.Empty);
    }
  }
}