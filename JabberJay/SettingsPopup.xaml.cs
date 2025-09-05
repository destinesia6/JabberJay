// MyCustomPopup.xaml.cs
using CommunityToolkit.Maui.Views;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

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