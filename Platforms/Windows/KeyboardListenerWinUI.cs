using Windows.UI.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using System;
using Microsoft.UI.Input;
using Microsoft.Maui.Platform;
using Windows.System;
using System.Runtime.InteropServices;
using System.Text;
using Application = Microsoft.Maui.Controls.Application;
using Window = Microsoft.UI.Xaml.Window;

namespace SoundboardMAUI.WinUI;

public class KeyboardListenerWinUI : IKeyboardListener
{
  public event EventHandler<KeyEventArgs> KeyDown;
  private InputKeyboardSource keyboardSource;
  private AppWindow appWindow;

  public void StartListening()
  {
    if (Application.Current.Windows.Count > 0)
    {
      var window = Application.Current.Windows[0];
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
    string character = GetCharacterFromVirtualKey(args.VirtualKey);
    KeyDown?.Invoke(this, new KeyEventArgs { KeyCode = args.VirtualKey.ToString(), KeyCharacter = character });
  }

  private string GetCharacterFromVirtualKey(VirtualKey virtualKey)
  {
    uint vkCode = (uint)virtualKey;
    uint scanCode = MapVirtualKey(vkCode, 0);

    byte[] keyboardState = new byte[256];
    GetKeyboardState(keyboardState);
    
    StringBuilder buffer = new(2);
    int unicodeChar = ToUnicode(vkCode, scanCode, keyboardState, buffer, 1, 0);

    if (unicodeChar > 0)
    {
      return buffer.ToString();
    }

    return string.Empty;
  }

  [DllImport("user32.dll")]
  private static extern uint MapVirtualKey(uint uCode, uint uMapType);

  [DllImport("user32.dll")]
  private static extern bool GetKeyboardState(byte[] lpKeyState);

  [DllImport("user32.dll")]
  private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, StringBuilder pwszBuff, int cchBuff, uint wFlags);
}