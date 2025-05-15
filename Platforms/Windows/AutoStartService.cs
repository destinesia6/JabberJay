namespace JabberJay;
using Microsoft.Win32;

public class AutoStartService
{
  private const string registryKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
  private const string appName = "JabberJay";
  private static readonly string? _appPath = System.Reflection.Assembly.GetEntryAssembly()?.Location;

  public static bool GetAutoStart()
  {
    using RegistryKey? key = Registry.CurrentUser.OpenSubKey(registryKeyPath);
    return key?.GetValue(appName) as string == _appPath;
  }

  public static void SetAutoStart(bool isEnabled)
  {
    using RegistryKey? key = Registry.CurrentUser.OpenSubKey(registryKeyPath, true);
    if (isEnabled)
    {
      if (_appPath != null) key?.SetValue(appName, _appPath);
    }
    else
    {
      key?.DeleteValue(appName, false);
    }
  }
}