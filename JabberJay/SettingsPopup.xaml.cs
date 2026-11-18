// MyCustomPopup.xaml.cs

using System.IO.Compression;
using CommunityToolkit.Maui.Views;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using SoundboardMAUI;
using System.IO.Compression;
#if WINDOWS
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
#endif

namespace JabberJay; // Replace YourAppName with your actual namespace

public partial class SettingsPopup : Popup
{
		private AppConfig? _config;
    private readonly string _configFilePath = Path.Combine(FileSystem.AppDataDirectory, "NetSettings.bson");

    public SettingsPopup()
    {
        InitializeComponent();
        LoadConfig();
    }

    private void LoadConfig()
    {
        try
        {
	        using FileStream fs = new(_configFilePath, FileMode.Open);
	        using BsonDataReader reader = new(fs);
	        JsonSerializer serializer = new();
	        _config = serializer.Deserialize<AppConfig>(reader);
          if (_config == null) return;
          PortInput.Text = _config.Port.ToString();
          IPInput.Text = _config.IpAddress;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading config in popup: {ex.Message}");
            _config = new AppConfig { Port = 5000, IpAddress = "" };
            PortInput.Text = _config.Port.ToString();
            IPInput.Text = _config.IpAddress;
        }
    }

    private async void SaveConfig()
    {
        try
        {
	        byte[] bsonBytes = _config.ToBson();
	        
	        await File.WriteAllBytesAsync(_configFilePath, bsonBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving config: {ex.Message}");
        }
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            string appDataDir = FileSystem.AppDataDirectory;

            if (!Directory.Exists(appDataDir) || !Directory.EnumerateFileSystemEntries(appDataDir).Any())
            {
                await Shell.Current.DisplayAlert("Warning", "The AppData directory is empty or does not exist.", "OK");
                return;
            }

            string zipName = $"JabberJay_Config_{DateTime.Now:yyyyMMdd_HHmmss}.zip";

    #if WINDOWS
            FileSavePicker savePicker = new() { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            savePicker.FileTypeChoices.Add("ZIP Archive", new List<string> { ".zip" });
            savePicker.SuggestedFileName = zipName;
            
            MauiWinUIWindow? window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as MauiWinUIWindow;
            if (window != null) InitializeWithWindow.Initialize(savePicker, window.WindowHandle);

            StorageFile? file = await savePicker.PickSaveFileAsync();
            if (file == null) return;

            ExportButton.Text = "Loading...";
            await Task.Yield();
            
            if (File.Exists(file.Path)) File.Delete(file.Path);
            
            ZipFile.CreateFromDirectory(appDataDir, file.Path, CompressionLevel.Optimal, includeBaseDirectory: false);
            
            ExportButton.Text = "Export";
            await Shell.Current.DisplayAlert("Success", $"AppData backup successfully saved to:\n{file.Path}", "OK");
    #elif ANDROID
            ExportButton.Text = "Loading...";
            await Task.Yield();

            string tempZipPath = Path.Combine(FileSystem.CacheDirectory, zipName);
            if (File.Exists(tempZipPath)) File.Delete(tempZipPath);

            ZipFile.CreateFromDirectory(appDataDir, tempZipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            ExportButton.Text = "Export";

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Save AppData Backup",
                File = new ShareFile(tempZipPath)
            });
    #endif
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to create backup: {ex.Message}", "OK");
        }
    }

    private async void OnImportClicked(object sender, EventArgs e)
    {
      try
      {
        string zipFilePath = string.Empty;

    #if WINDOWS
        FileOpenPicker openPicker = new()
        {
          ViewMode = PickerViewMode.Thumbnail,
          SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        openPicker.FileTypeFilter.Add(".zip");

        MauiWinUIWindow? window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as MauiWinUIWindow;
        if (window != null) InitializeWithWindow.Initialize(openPicker, window.WindowHandle);

        StorageFile? file = await openPicker.PickSingleFileAsync();
        if (file == null) return;
        zipFilePath = file.Path;

    #elif ANDROID
            // Use MAUI's cross-platform FilePicker for Android
            var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "application/zip", "application/x-zip-compressed" } }
            });

            var pickResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select AppData Backup Zip",
                FileTypes = customFileType
            });

            if (pickResult == null) return;
            zipFilePath = pickResult.FullPath;

            // Handle Android content URIs by copying to a local cache file stream if needed
            if (zipFilePath.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            {
                string cachePath = Path.Combine(FileSystem.CacheDirectory, "import_temp.zip");
                using (var inputStream = await pickResult.OpenReadAsync())
                using (var outputStream = File.Create(cachePath))
                {
                    await inputStream.CopyToAsync(outputStream);
                }
                zipFilePath = cachePath;
            }
    #endif
        
        ImportButton.Text = "Validating...";
        await Task.Yield();
        // Validate Zip contents safely across platforms
        using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
        {
          List<string> entryNames = archive.Entries.Select(e => e.FullName).ToList();
          
          bool hasSoundsFolder = entryNames.Any(n =>
            String.Equals(n, "Sounds/", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(n, "Sounds", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Sounds/", StringComparison.OrdinalIgnoreCase));

          if (!hasSoundsFolder)
          {
            await Shell.Current.DisplayAlert("Invalid Backup", "The selected backup is invalid.", "OK");
            ImportButton.Text = "Import";
            await Task.Yield();
            return;
          }
        }

        ImportButton.Text = "Import";
        await Task.Yield();

        bool confirm = await Shell.Current.DisplayAlert("Confirm Restore",
          "Restoring a backup will replace your current app data files. Do you want to continue?", "Yes", "No");

        if (!confirm) return;
        
        ImportButton.Text = "Restoring...";
        await Task.Yield();
        
        string appDataDir = FileSystem.AppDataDirectory;
            
        foreach (string filePath in Directory.GetFiles(appDataDir))
        {
          File.Delete(filePath);
        }
        foreach (string dirPath in Directory.GetDirectories(appDataDir))
        {
          Directory.Delete(dirPath, recursive: true);
        }
            
        ZipFile.ExtractToDirectory(zipFilePath, appDataDir, overwriteFiles: true);

        await Shell.Current.DisplayAlert("Success", "AppData successfully restored from backup!", "OK");

        ImportButton.Text = "Import";
        await Task.Yield();
        
        Close();
            
        if (Application.Current != null) Application.Current.Windows[0].Page = new AppShell();
      }
      catch (Exception ex)
      {
        await Shell.Current.DisplayAlert("Error", $"Failed to restore backup: {ex.Message}", "OK");
      }
    }

  private void OnSubmitClicked(object sender, EventArgs e)
    {
        // Update config object from textbox values
        if (int.TryParse(PortInput.Text, out int port))
        {
	        _config.Port = port;
        }
        else
        {
	        // Handle invalid port input, e.g., show an error
	        Application.Current?.MainPage?.DisplayAlert("Input Error", "Please enter a valid port number.", "OK");
	        return; // Stop processing if input is invalid
        }
        _config.IpAddress = IPInput.Text;

        SaveConfig(); // Save the updated config

        Application.Current?.MainPage?.DisplayAlert("Input Submitted", $"Port: {_config.Port}\nIP Address: {_config.IpAddress}", "OK");

        Close();
    }
}