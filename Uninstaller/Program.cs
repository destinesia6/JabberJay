using System;
using System.IO;
using Microsoft.Win32;

namespace Installer;

class Program
{
    public static int Main(string[] args)
    {
        // Section to remove registry keys
        
        string appName = "JabberJay";
        string appGuid = "C787B774-CB98-4826-9BBB-A3298B85DA95";
        string startMenuProgramsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        string appShortcutFolderPath = Path.Combine(startMenuProgramsPath, appName);
        string shortcutFilePath = Path.Combine(appShortcutFolderPath, $"{appName}.lnk");
        
        string uninstallRegistryKeyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        string uninstallSubKeyPath = appGuid;

        if (File.Exists(shortcutFilePath))
        {
            try
            {
                File.Delete(shortcutFilePath);
                if (Directory.Exists(appShortcutFolderPath) && Directory.GetFiles(appShortcutFolderPath).Length == 0)
                {
                    try
                    {
                        Directory.Delete(appShortcutFolderPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during cleanup: {ex.Message}");
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error deleting Start Menu shortcut {shortcutFilePath}: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Permission denied to delete Start Menu shortcut {shortcutFilePath}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred deleting shortcut {shortcutFilePath}: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Start Menu shortcut not found, nothing to delete.");
        }

        using (RegistryKey uninstallKey = Registry.CurrentUser.OpenSubKey(uninstallRegistryKeyPath, true))
        {
            if (uninstallKey != null)
            {
                try
                {
                    if (uninstallKey.OpenSubKey(uninstallSubKeyPath, false) != null)
                    {
                        uninstallKey.DeleteSubKeyTree(uninstallSubKeyPath);
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.WriteLine($"Permission denied to delete Registry entry: {ex.Message}");
                    Console.WriteLine("You might need to run the uninstaller as administrator if this entry was created with elevated privileges.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An unexpected error occurred deleting Registry entry: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Uninstall key not found, nothing to delete.");
            }
        }
        
        // Section to remove files
        
        Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory; // Moves current directory to uninstall folder
        string installationDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "JabberJay"));

        string installerDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Installer"));
        
        if (Directory.Exists(installationDirectory)) Directory.Delete(installationDirectory, true);
        if (Directory.Exists(installerDirectory)) Directory.Delete(installerDirectory, true);
        
        return 0;
    }
}