using System;
using System.Linq;
using Application = Microsoft.UI.Xaml.Application;
using Window = Microsoft.Maui.Controls.Window;

#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
#endif

namespace JabberJay;

public class WindowHelper
{
#if WINDOWS
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;
#endif

    public static void BrindWindowToFront()
    {
#if WINDOWS
        Window? mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (mauiWindow is null) return;
        
        Microsoft.UI.Xaml.Window? nativeHandler = mauiWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (nativeHandler is null) return;
        
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeHandler);
        
        ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
#endif
    }
}