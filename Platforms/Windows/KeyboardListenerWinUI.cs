using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using SoundboardMAUI;
using Application = Microsoft.Maui.Controls.Application;
using KeyEventArgs = JabberJay.KeyEventArgs;

namespace JabberJay;

public class KeyboardListenerWinUI : IKeyboardListener
{
  public event EventHandler<KeyEventArgs> KeyDown;
  private InputKeyboardSource keyboardSource;
  private AppWindow appWindow;

  public void StartListening()
  {
    if (Application.Current != null && Application.Current.Windows.Count > 0)
    {
      Window window = Application.Current.Windows[0];
      if (window.Handler?.PlatformView is MauiWinUIWindow mauiWinUIWindow)
      {
        appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(mauiWinUIWindow.WindowHandle));
        if (appWindow != null)
        {
          keyboardSource = InputKeyboardSource.GetForIsland(mauiWinUIWindow.Content.XamlRoot.ContentIsland);
          if (keyboardSource != null)
          {
            keyboardSource.KeyDown += KeyboardSource_KeyDown;
          }
        }
      }
    }
  }

  public void StopListening()
  {
    if (keyboardSource != null)
    {
      keyboardSource.KeyDown -= KeyboardSource_KeyDown;
      keyboardSource = null;
    }
    appWindow = null;
  }
  
  private void KeyboardSource_KeyDown(InputKeyboardSource sender, Microsoft.UI.Input.KeyEventArgs args)
  {
    KeyDown.Invoke(this, new KeyEventArgs { KeyCode = args.VirtualKey.ToString() });
  }
}