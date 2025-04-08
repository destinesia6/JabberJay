using System.Runtime.InteropServices;
using Windows.System;

namespace SoundboardMAUI.WinUI;

public class GlobalKeyboardListener : IDisposable
{
  private const int WH_KEYBOARD_LL = 13;
  private const int WM_KEYDOWN = 0x0100;
  private IntPtr _hookID = IntPtr.Zero;
  private HookProc _hookProc;
  private Action<VirtualKey> _keyPressHandler;

  public GlobalKeyboardListener(Action<VirtualKey> keyPressAction)
  {
    _keyPressHandler = keyPressAction;
    _hookProc = HookCallback;
    _hookID = SetHook(_hookProc);
  }

  private IntPtr SetHook(HookProc proc)
  {
    using (System.Diagnostics.ProcessModule module = System.Diagnostics.Process.GetCurrentProcess().MainModule)
    {
      return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(module.ModuleName), 0);
    }
  }

  private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
  {
    if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
    {
      int vkCode = Marshal.ReadInt32(lParam);
      VirtualKey key = (VirtualKey)vkCode;
      _keyPressHandler(key);
    }
    return CallNextHookEx(_hookID, nCode, wParam, lParam);
  }

  public void Dispose()
  {
    if (_hookID != IntPtr.Zero)
    {
      UnhookWindowsHookEx(_hookID);
      _hookID = IntPtr.Zero;
    }
  }
 
  private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
  
  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool UnhookWindowsHookEx(IntPtr hhk);

  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

  [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  private static extern IntPtr GetModuleHandle(string lpModuleName);
}