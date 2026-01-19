#if WINDOWS
using Windows.System;
using NetSparkleUpdater.Enums;
using NAudio.Wave;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;
using YoutubeDLSharp.Metadata;
#endif
using NetSparkleUpdater.Events;
using System.Collections.ObjectModel;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Microsoft.Maui.Controls.Shapes;
using Plugin.Maui.Audio;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using SoundboardMAUI;
using Border = Microsoft.Maui.Controls.Border;
using Color = Microsoft.Maui.Graphics.Color;
using ColumnDefinition = Microsoft.Maui.Controls.ColumnDefinition;
using Grid = Microsoft.Maui.Controls.Grid;
using Path = System.IO.Path;
using Rectangle = Microsoft.Maui.Controls.Shapes.Rectangle;

namespace JabberJay;

public partial class MainPage : ContentPage
{
		private Window? _mainWindow;
    private readonly string _soundsFolderName = "Sounds";
    private readonly string _bindingsFile = "Bindings.bson";
    private readonly string _outputDeviceFile = "Output.bson";
    private readonly string _netSettingsFile = "NetSettings.bson";
    private ApiService _apiService;
    private int _selectedOutputDeviceIndex = -1;
    private string _currentlyBindingFilePath;
    private Dictionary<string, SoundButton> _soundButtons = new(); // Store key bindings
    private string _ytDlpPath = "";
    private string _windowsPath = "";
    private string _ipConfig;
    private int _port;
    #if WINDOWS
		private List<WaveOutEvent> _playingSounds = [];
		private bool _autoStart;
		private SoundboardServer _server;
		private bool _serverStart;
    private TaskbarIcon _trayPopup = new();
    private readonly IKeyboardListener _keyboardListener; // Used to bind new keys
    private GlobalKeyboardListener? _globalKeyboardListener; // Used to detect input from bound keys
    
    public bool AutoStart
    {
        get => _autoStart;
        set
        {
            if (_autoStart != value)
            {
                _autoStart = value;
                OnPropertyChanged();
                AutoStartService.SetAutoStart(value);
            }
        }
    }
    public bool ServerStart
    {
	    get => _serverStart;
	    set
	    {
		    if (_serverStart == value) return;
		    _serverStart = value;
		    OnPropertyChanged();
		    StartServer();
	    }
    }

    public int SelectedOutputDeviceIndex
    {
        get => _selectedOutputDeviceIndex;
        set
        {
            if (_selectedOutputDeviceIndex != value)
            {
                _selectedOutputDeviceIndex = value;
                OnPropertyChanged();
            }
        }
    }
    private WaveOutCapabilities _selectedOutputDevice;
    public WaveOutCapabilities SelectedOutputDevice
    {
        get => _selectedOutputDevice;
        set
        {
            if (value.ProductName != _selectedOutputDevice.ProductName)
            {
                _selectedOutputDevice = value;
                OnPropertyChanged();
                SelectedOutputDeviceIndex = FindDeviceIndex(value);
                UpdateOutputDeviceBson(value.ProductName);
            }
        }
    }

    public ObservableCollection<WaveOutCapabilities> OutputDevices { get; set; } = [];
    


    #else
    AudioManager _audioManager = new();
    private SoundboardClient _soundboardClient;
    public ObservableCollection<object> OutputDevices { get; set; } = [];
    public string SelectedOutputDevice { get; set; } = "";
    public string ProductName { get; set; } = "";
		private List<IAudioPlayer> _playingSounds = new();

    private bool _clientToggle;

    public bool ClientToggle
    {
	    get  => _clientToggle;
	    set
	    {
		    if (value == _clientToggle) return;
		    _clientToggle = value;
		    OnPropertyChanged();
		    if (_clientToggle)
		    {
			    Task.Run(ConnectClient);
		    }
		    else
		    {
			    Task.Run(DisconnectClient);
		    }
	    }
    }
    
    #endif
		private Updater _appUpdater = new();
    #if WINDOWS
    public MainPage(IKeyboardListener keyboardListener, ApiService apiService)
    {
        InitializeComponent();
        _soundsFolderName = Path.Combine(FileSystem.AppDataDirectory, _soundsFolderName);
        _bindingsFile = Path.Combine(FileSystem.AppDataDirectory, _bindingsFile);
        _outputDeviceFile = Path.Combine(FileSystem.AppDataDirectory, _outputDeviceFile);
        _netSettingsFile = Path.Combine(FileSystem.AppDataDirectory, _netSettingsFile);
        _appUpdater.UpdateDetected += AppUpdateDetected;
        _appUpdater.DownloadFinished += AppDownloadFinished;
        _appUpdater.UpdateFailed += AppUpdateFailed;
        _appUpdater.DownloadStarted += AppDownloadStarted;
        _appUpdater.DownloadMadeProgress += AppDownloadMadeProgress;
        _keyboardListener = keyboardListener;
        _keyboardListener.KeyDown += OnBindKeyDown;
        _server = new SoundboardServer(GetFilesList);
        _server.PlaySoundAction += PlaySound;
        _server.StopSoundAction += StopPlayback;
        AddTrayIcon();
        _autoStart = AutoStartService.GetAutoStart();
        StartGlobalListener();
        InitializeExternalToolsAsync();
        VersionLabel.Text = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";
        Task.Run(LoadSoundButtons);
        AnimationExtensions.SetupPointerEffects(AddSound, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
            Color.FromArgb("#786af7"));
        AnimationExtensions.SetupPointerEffects(ImportSound, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
            Color.FromArgb("#786af7"));
        ToggleClient.IsVisible = false;
        BindingContext = this;
        PageContainer.Children.Remove(_trayPopup);
        AppClosingHandler.PageHide += ShowTrayIcon;
        Task.Delay(2000);
        _appUpdater.CheckForUpdates();
        _apiService = apiService;
    }

    protected override void OnAppearing()
    {
	    MainThread.BeginInvokeOnMainThread(() => ShowTrayIcon(null, null));
    }
    
    private void AddTrayIcon() // Creates the object for the tray icon, which can be added when the app is closed
    {
        _trayPopup = new TaskbarIcon
        {
            IconSource = "jabberjay.ico",
            LeftClickCommand = ShowWindowCommand,
            NoLeftClickDelay = true
        };
        MenuFlyout menu = [];
        MenuFlyoutItem exitMenuItem = new()
        {
            Command = CloseAppCommand,
            Text = "Exit"
        };
        menu.Add(exitMenuItem);
        FlyoutBase.SetContextFlyout(_trayPopup, menu);
	  }
    
    private void StartServer()
    {
	    UpdatePort();
	    UpdateServerToggle();
	    if (_serverStart)
	    {
		    _ = Task.Run(async () =>
		    {
			    try
			    {
				    if (_port > 0)
				    {
					    await _server.StartAsync(_port);
				    }
				    else
				    {
					    await _server.StartAsync();
				    }
			    }
			    catch (Exception ex)
			    {
				    Console.WriteLine(ex);
			    }
		    });

		    if (_port > 0)
		    {
			    MainThread.BeginInvokeOnMainThread(() => ServerIP.Text = $"IP: {GetLocalIPAddress()} Port: {_port}");
		    }
		    else
		    {
			    MainThread.BeginInvokeOnMainThread(() => ServerIP.Text = $"IP: {GetLocalIPAddress()} Port: 5000");
		    }
	    }
	    else
	    {
		    _server.Stop();
		    MainThread.BeginInvokeOnMainThread(() => ServerIP.Text = "");
	    }
    }
    
    #else
    public MainPage(ApiService apiService)
    {
        InitializeComponent();
        _appUpdater.UpdateDetected += AppUpdateDetected;
        OutputPickerContainer.IsVisible = false;
        AutoStartContainer.IsVisible = false;
        ServerStartContainer.IsVisible = false;
        _soundsFolderName = Path.Combine(FileSystem.AppDataDirectory, _soundsFolderName);
        _bindingsFile = Path.Combine(FileSystem.AppDataDirectory, _bindingsFile);
        _outputDeviceFile = Path.Combine(FileSystem.AppDataDirectory, _outputDeviceFile);
				_netSettingsFile = Path.Combine(FileSystem.AppDataDirectory, _netSettingsFile);
        VersionLabel.Text = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";
        LoadSoundButtons();
        AnimationExtensions.SetupPointerEffects(AddSound, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
            Color.FromArgb("#786af7"));
        AnimationExtensions.SetupPointerEffects(ImportSound, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
            Color.FromArgb("#786af7"));
        BindingContext = this;
        ConfigureNetSettings();
        _apiService = apiService;
        _soundboardClient = new SoundboardClient();
    }
    #endif

    #if WINDOWS
    private async void AppUpdateDetected(object? sender, UpdateDetectedEventArgs args)
    {
        try
        {
            if (await DisplayAlert("Update Available", $"An update to version {args.LatestVersion.Version} is available. Do you want to download it?", "Yes", "No"))
            {
                _appUpdater.DownloadLatest(args.LatestVersion);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private void AppDownloadStarted(object? s, EventArgs e)
    {
        DownloadStatusLabel.Text = "Downloading Update...";
        ProgressLayout.IsVisible = true;
    }

    private void AppDownloadMadeProgress(object sender, ItemDownloadProgressEventArgs args)
    {
        DownloadProgressBar.Progress = args.ProgressPercentage;
    }

    private void AppDownloadFinished(object? sender, DownloadFinishedEventArgs downloadedItem)
    {
        string updaterPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Installer", "Installer.exe"));
        string originalDownloadPath = downloadedItem.FilePath;
        string targetDownloadPath = Path.Combine(Path.GetDirectoryName(originalDownloadPath) ?? "", "JabberJay.7z");
        if (targetDownloadPath != originalDownloadPath) File.Move(originalDownloadPath, targetDownloadPath, true);
        
        // string updaterPath = "C:/Work/SoundboardMAUI/Installer/bin/Debug/net9.0/Installer.exe";
        if (!File.Exists(updaterPath)) return;
        Process.Start(updaterPath, $"\"{targetDownloadPath}\"");
        Environment.Exit(0);
    }

    private void AppUpdateFailed(object? sender, InstallUpdateFailureReason args)
    {
        Console.WriteLine(args);
        DisplayAlert("Error", "Failed to download update", "OK");
    }


    private async void InitializeExternalToolsAsync()
    {
        try
        {
	          ConfigureNetSettings();
            string windowsPath = Path.Combine(AppContext.BaseDirectory, "Platforms", "Windows");
            string ytDlpPath = Path.Combine(windowsPath, "yt-dlp.exe");
            string ffmpegPath = Path.Combine(windowsPath, "ffmpeg.exe");
            string ffmprobePath = Path.Combine(windowsPath, "ffprobe.exe");
            if (File.Exists(ytDlpPath) && File.Exists(ffmpegPath) && File.Exists(ffmprobePath))
            {
                _ytDlpPath = ytDlpPath;
                _windowsPath = windowsPath;
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error", "Could not find import tools. Import feature disabled", "OK"));
                ImportSound.IsEnabled = false;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error", "Error finding import tools. Import feature disabled", "OK"));
            ImportSound.IsEnabled = false;
        }

        SetUpOutputDevices();
    }

    private void SetUpOutputDevices()
    {
        OutputDevices = [];
        for (int n = 0; n < WaveOut.DeviceCount; n++)
        {
            OutputDevices.Add(WaveOut.GetCapabilities(n));
        }

        string? savedOutput = GetOutputDeviceFromBson();
        if (savedOutput != null && OutputDevices.Select(device => device.ProductName).Contains(savedOutput))
        {
            SelectedOutputDevice = OutputDevices.First(device => device.ProductName == savedOutput);
        }
        else
        {
            SetDefaultOutputDevice();
        }
    }

    private void SetDefaultOutputDevice()
    {
        for (int n = 0; n < WaveOut.DeviceCount; n++) // Checks if voicemeeter is active, if so set as default output
        {
            WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(n);
            if (deviceInfo.ProductName.Contains("Voicemeeter Input", StringComparison.OrdinalIgnoreCase))
            {
                SelectedOutputDevice = deviceInfo;
                return;
            }
        }

        SelectedOutputDevice = OutputDevices.First();
    }
    #else
    private void AppUpdateDetected(object? sender, UpdateDetectedEventArgs args)
    {
        try
        {
	        MainThread.BeginInvokeOnMainThread(async void () =>
	        {
		        if (await DisplayAlert("Update Available", $"An update to version {args.LatestVersion.Version} is available on the JabberJay github", "Update", "Later"))
		        {
			        await Launcher.Default.OpenAsync($"https://github.com/destinesia6/JabberJay/releases/tag/{args.LatestVersion.Version}");
		        }
	        });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    #endif

	private void UpdatePort()
	{
		if (!File.Exists(_netSettingsFile))
		{
			_port = 5000;
		}
		else
		{
			try
			{
				using FileStream fs = new(_netSettingsFile, FileMode.Open);
				using BsonDataReader reader = new(fs);
				JsonSerializer serializer = new();
				AppConfig? config = serializer.Deserialize<AppConfig>(reader);
				reader.Close();
				if (config == null) return;
				_port = config.Port;
#if ANDROID
				_ipConfig = config.IpAddress;
#endif
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}
	}
	
	private void ConfigureNetSettings()
	{
		if (!File.Exists(_netSettingsFile))
		{
			AppConfig defaultConfig = new() { Port = 5000, IpAddress = "" };
			byte[] bsonBytes = defaultConfig.ToBson();
			File.WriteAllBytes(_netSettingsFile, bsonBytes);
		}
		else
		{
			try
			{
				using FileStream fs = new(_netSettingsFile, FileMode.Open);
				using BsonDataReader reader = new(fs);
				JsonSerializer serializer = new();
				AppConfig? config = serializer.Deserialize<AppConfig>(reader);
				reader.Close();
				if (config != null)
				{
					_port = config.Port;
#if WINDOWS
					_ipConfig = GetLocalIPAddress();
					ServerStart = config.ServerStarted;
#else
					_ipConfig = config.IpAddress;
#endif
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}
	}

	public void ShowSettings(object sender, TappedEventArgs e)
	{
		this.ShowPopup(new SettingsPopup());
	}

    private async void AddSoundButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            FileResult? result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select a sound file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".mp3"] },
                    { DevicePlatform.Android, ["audio/mpeg"] },
                    { DevicePlatform.iOS, ["public.mp3"] }
                })
            });

            if (result != null)
            {
                string sourceFilePath = result.FullPath;

                // Ensure the directory exists
                if (!Directory.Exists(_soundsFolderName))
                {
                    Directory.CreateDirectory(_soundsFolderName);
                }

                string fileName = Path.GetFileName(sourceFilePath);
                string sanitizedFileName =
                    string.Join("_",
                        fileName.Split(Path.GetInvalidFileNameChars())); // Protects against invalid file names
                string destinationFilePath = Path.Combine(_soundsFolderName, sanitizedFileName);

                // Move the file
                File.Copy(sourceFilePath, destinationFilePath, true); // Overwrite if it exists

                CreateSoundButton(destinationFilePath);
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error", $"Error picking or moving file: {ex.Message}", "OK"));
        }
    }

    
    private async void ImportSoundButton_Clicked(object sender, EventArgs e)
    {
	    string url = await DisplayPromptAsync("Video URL", "Enter a video URL:", "Download", "Cancel");

	    if (String.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
	    {
		    if (String.IsNullOrWhiteSpace(url))
		    {
			    MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error", "URL cannot be empty.", "OK"));
		    }
		    else
		    {
			    MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error", "URL is not valid.", "OK"));
		    }

		    return;
	    }
	    
	    
	    if (String.IsNullOrEmpty(_ytDlpPath) || !File.Exists(_ytDlpPath))
	    {
#if WINDOWS
            MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error", "yt-dlp downloader not found or configured. Using remote API, this will not work for youtube links.", "OK"));
#endif

		    ProgressLayout.IsVisible = true;
		    DownloadProgressBar.Progress = 0;
		    DownloadStatusLabel.Text = "Initializing...";

		    try
		    {
			    MainThread.BeginInvokeOnMainThread(() => DownloadStatusLabel.Text = "Requesting download URL from Cloud...");

			    AudioResponse? apiResponse = await _apiService.RequestAudioDownloadUrl(url);
			    
			    if (apiResponse == null || String.IsNullOrEmpty(apiResponse.DownloadUrl))
			    {
				    MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error", apiResponse?.Message ?? "Failed to get download URL from API.", "OK"));
				    return;
			    }
			    
			    MainThread.BeginInvokeOnMainThread(() => DownloadStatusLabel.Text = "Starting download...");

			    IProgress<DownloadProgress> progress = new Progress<DownloadProgress>(HandleDownloadProgressApi);
			    
			    string? downloadedFilePath = await Task.Run(() => _apiService.DownloadFile(
				    apiResponse.DownloadUrl,
				    _soundsFolderName,
				    progress
			    ));
			    
			    if (String.IsNullOrEmpty(downloadedFilePath) || !File.Exists(downloadedFilePath))
			    {
				    MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error", "Could not find downloaded file after API request.", "OK"));
				    return;
			    }

			    MainThread.BeginInvokeOnMainThread(() => DownloadStatusLabel.Text = "Adding button...");
			    CreateSoundButton(downloadedFilePath); // Your existing method
			    MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Download Complete", $"'{Path.GetFileNameWithoutExtension(downloadedFilePath)}' has been downloaded!", "OK"));

		    }
				catch (Exception exception)
		    {
			    Console.WriteLine(exception);
			    MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error", $"An unexpected error occurred: {exception.Message}", "OK"));
		    }
		    finally 
		    {
			    MainThread.BeginInvokeOnMainThread(() => { ProgressLayout.IsVisible = false; });
		    }
	    }
	    else
	    {
		    

#if WINDOWS
        

        ProgressLayout.IsVisible = true;
        DownloadProgressBar.Progress = 0;
        DownloadStatusLabel.Text = "Initializing...";

        try
        {
            YoutubeDL ytdl = new()
            {
                YoutubeDLPath = _ytDlpPath,
                FFmpegPath = _windowsPath,
                OutputFolder = _soundsFolderName,
                OutputFileTemplate = "%(title)s.%(ext)s"
            };

            MainThread.BeginInvokeOnMainThread(() => DownloadStatusLabel.Text = "Fetching video info...");
            RunResult<VideoData> metaDataRes =
                await ytdl.RunVideoDataFetch(url, ct: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
            if (!metaDataRes.Success || metaDataRes.Data == null || string.IsNullOrEmpty(metaDataRes.Data.Title))
            {
                MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error", "Failed to fetch metadata", "OK"));
                return;
            }

            string newSoundName = metaDataRes.Data.Title;

            MainThread.BeginInvokeOnMainThread(() => DownloadStatusLabel.Text = "Checking existing sounds...");

            if (File.Exists(Path.Combine(_soundsFolderName, $"{newSoundName}.mp3")))
            {
                MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Already Exists", $"The sound {newSoundName} already exists", "OK"));
                return;
            }

            MainThread.BeginInvokeOnMainThread(() => DownloadStatusLabel.Text = "Starting download...");

            OptionSet options = new()
            {
                ExtractAudio = true,
                AudioFormat = AudioConversionFormat.Mp3,
                AudioQuality = 0,
                NoPlaylist = true,
                Output = Path.Combine(_soundsFolderName, "%(title)s.%(ext)s")
            };

            IProgress<YoutubeDLSharp.DownloadProgress> progress = new Progress<YoutubeDLSharp.DownloadProgress>(HandleDownloadProgress);

            RunResult<string> res = await ytdl.RunAudioDownload(url, format: AudioConversionFormat.Mp3,
                overrideOptions: options, ct: new CancellationTokenSource(TimeSpan.FromMinutes(5)).Token,
                progress: progress);

            if (res.Success)
            {
                MainThread.BeginInvokeOnMainThread(() => DownloadStatusLabel.Text = "Locating File...");
                string expectedFilename = $"{newSoundName}.mp3";
                string downloadedFilePath = Path.Combine(_soundsFolderName, expectedFilename);

                // Check if the file exists using the expected path
                if (string.IsNullOrEmpty(downloadedFilePath) || !File.Exists(downloadedFilePath))
                {
	                MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error", "Could not find downloaded file at expected path.", "OK"));
	                return;
                }

                if (File.Exists(downloadedFilePath))
                {
                    MainThread.BeginInvokeOnMainThread(() => DownloadStatusLabel.Text = "Adding button...");
                    CreateSoundButton(downloadedFilePath);
                }
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            throw;
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => { ProgressLayout.IsVisible = false; });
        }
#endif
		  }
    }

    private void ToggleClient_Clicked(object sender, EventArgs e)
    {
#if ANDROID
	    if (ClientToggle)
	    {
		    ToggleClientLabel.Text = "Disconnecting...";
		    ClientToggle = false;
	    }
	    else
	    {
		    ToggleClientLabel.Text = "Finding server...";
		    ClientToggle = true;
	    }
#endif
    }

    #if ANDROID
    private async Task ConnectClient()
    {
	    UpdatePort();
	    
	    string serverIp = _ipConfig;
	    if (String.IsNullOrWhiteSpace(serverIp))
	    {
		    await Application.Current?.Dispatcher.DispatchAsync(async () =>
		    {
			    serverIp = await DisplayPromptAsync("Enter PC IP address", "Enter the computers IP address:", "Connect");
		    })!;
	    }

	    if (!String.IsNullOrEmpty(serverIp))
	    {
		    Application.Current?.Dispatcher.Dispatch(() =>
		    {
			    ToggleClientLabel.Text = "Connecting...";
		    });
		    bool isConnected = await _soundboardClient.ConnectAsync(serverIp, _port);
		    if (isConnected)
		    {
			    Application.Current?.Dispatcher.Dispatch(() =>
			    {
				    ToggleClientLabel.Text = "Stop Remote";
			    });
			    if (Application.Current != null) await Application.Current.Dispatcher.DispatchAsync(async () => { await LoadServerButtons(); });
			    return;
		    }

		    Application.Current?.Dispatcher.Dispatch(() =>
		    {
			    ToggleClientLabel.Text = "Connection failed";
		    });
	    }
	    else
	    {
		    Application.Current?.Dispatcher.Dispatch(() =>
		    {
			    ToggleClientLabel.Text = "Server not found";
		    });
	    }

	    _clientToggle = false;

	    await Task.Delay(5000);

	    Application.Current?.Dispatcher.Dispatch(() =>
	    {
		    ToggleClientLabel.Text = "Start Remote";
	    });
    }

    private async Task DisconnectClient()
    {
	    _soundboardClient.Disconnect();
	    await Application.Current?.Dispatcher.DispatchAsync(async () =>
	    {
		    await LoadSoundButtons();
		    ToggleClientLabel.Text = "Start Remote";
	    })!;
    }
#else
    private void HandleDownloadProgress(YoutubeDLSharp.DownloadProgress p)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            switch (p.State)
            {
                case DownloadState.Downloading:
                    DownloadStatusLabel.Text = $"Downloading... {p.Progress * 100:N0}%";
                    DownloadProgressBar.Progress = p.Progress;
                    break;
                case DownloadState.PostProcessing:
                    DownloadStatusLabel.Text = "Processing audio...";
                    DownloadProgressBar.Progress = 1;
                    break;
                case DownloadState.Error:
                    DownloadStatusLabel.Text = "Error, download canceled";
                    DownloadProgressBar.Progress = 0;
                    break;
                default:
                    DownloadStatusLabel.Text = p.State.ToString();
                    DownloadProgressBar.Progress = p.Progress;
                    break;
            }
            
        });
    }
#endif

    private void HandleDownloadProgressApi(DownloadProgress p)
    {
	    MainThread.BeginInvokeOnMainThread(() =>
	    {
		    DownloadProgressBar.Progress = p.Percentage;
		    DownloadStatusLabel.Text = $"Downloading: {p.Percentage:P1} ({p.BytesReceived / 1024 / 1024}MB / {p.TotalBytes / 1024 / 1024}MB)";
	    });
    }


    private async Task LoadSoundButtons()
    {
	    Dictionary<string, SoundButton> bindings = new();
	    _soundButtons = new Dictionary<string, SoundButton>();
	    SoundButtonPanel.Clear();
        //Loads bindings
        #if WINDOWS
        if (File.Exists(_bindingsFile))
        {
            try
            {
                bindings = GetBoundSounds();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
        KeyValuePair<string, SoundButton>? stopBind = bindings?.FirstOrDefault(kvp =>
	        String.Equals(kvp.Key, "stop", StringComparison.OrdinalIgnoreCase));
        if (stopBind is { Value: not null })
        {
	        NewStopButton(stopBind.Value.Value.Binding);
        }
        else
        {
	        NewStopButton();
        }
#else
				NewStopButton();
#endif
        // Loads sounds
        if (Directory.Exists(_soundsFolderName))
        {
            try
            {
	            List<string> mp3Files = GetFilesList();
                mp3Files.AddRange(Directory.GetFiles(_soundsFolderName, "*.mpeg").ToList());
                foreach (string filePath in mp3Files)
                {
                    #if WINDOWS
                    KeyValuePair<string, SoundButton>? bindingEntry = bindings?.FirstOrDefault(kvp =>
                        String.Equals(kvp.Key, filePath, StringComparison.OrdinalIgnoreCase));

                    if (bindingEntry is { Value: not null })
                    {
                        CreateSoundButton(filePath, bindingEntry.Value.Value.Binding);
                    }
                    else
                    {
                        CreateSoundButton(filePath);
                    }
                    #else
                    CreateSoundButton(filePath);
                    #endif

                }
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error", $"Error loading sound buttons from folder: {ex.Message}", "OK"));
            }
        }
        else
        {
            // Create the directory if it doesn't exist
            Directory.CreateDirectory(_soundsFolderName);
        }
    }

    private List<string> GetFilesList()
    {
	    return Directory.GetFiles(_soundsFolderName, "*.mp3").ToList();
    }

#if ANDROID
    private async Task LoadServerButtons()
    {
	    Dictionary<string, SoundButton> bindings = new();
	    try
	    {
		    SoundButtonPanel.Clear();
		    _soundButtons = new Dictionary<string, SoundButton>();
		    NewStopButton();
		    List<string>? mp3Files = await _soundboardClient.UpdateSoundListAsync();
		    if (mp3Files is { Count: > 0 })
		    {
			    foreach (string filePath in mp3Files)
			    {
				    CreateSoundButton(filePath);
			    }
		    }
	    }
	    catch (Exception ex)
	    {
		    Console.WriteLine(ex.Message);
	    }
    }
#endif

	private void NewStopButton(string? binding = null)
	{
		StopContainer.Clear();
		Grid container = new()
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(GridLength.Auto)
			},
			RowDefinitions =
			{
				new RowDefinition(GridLength.Auto)
			},
			
			#if WINDOWS
			Margin = new Thickness(0),
			#else
			Margin = new Thickness(0, 5, 0, 0),
			#endif
			Padding = new Thickness(0)
		};
		SoundButton stopButton = CreateStopButton(binding);
		AnimationExtensions.SetupPointerEffects(stopButton.Play, Color.FromArgb("#f70202"), Color.FromArgb("#b00404"),
			Color.FromArgb("#6b0101"));
		
		TapGestureRecognizer stopGesture = new();
#if WINDOWS
		stopGesture.Tapped += (sender, args) => StopPlayback();
#else
      if (ClientToggle)
      {
        stopGesture.Tapped += async (sender, args) => await _soundboardClient.SendCommandAsync("STOP");
      }
      else
      {
	      stopGesture.Tapped += (sender, args) => StopPlayback();
      }
#endif
		stopButton.Play.GestureRecognizers.Add(stopGesture);
		Grid.SetColumn(stopButton.Play, 0);
		Grid.SetRow(stopButton.Play, 0);
		container.Children.Add(stopButton.Play);
		
#if WINDOWS
		AnimationExtensions.SetupPointerEffects(stopButton.Bind, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"), Color.FromArgb("#786af7"));
		TapGestureRecognizer bindTapGesture = new();
		bindTapGesture.Tapped += (sender, args) => StartKeyBinding(sender, "stop");
		stopButton.Bind.GestureRecognizers.Add(bindTapGesture);
		Grid.SetColumn(stopButton.Bind, 1);
		Grid.SetRow(stopButton.Bind, 0);
		container.Children.Add(stopButton.Bind);
#endif
		StopContainer.Add(container);
		_soundButtons.Add("stop", stopButton);
	}
	
    private void CreateSoundButton(string filePath, string? binding = null)
    {
        Grid container = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                #if WINDOWS
                new ColumnDefinition(GridLength.Auto)
                #endif
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            Margin = new Thickness(2), // Keep a small margin around the entire merged button
            Padding = new Thickness(0)
        };
        
#if WINDOWS
	    SoundButton newSound = NewButton(filePath, binding);
#else
	    string androidFriendlyPath = filePath.Replace('\\', '/');
	    SoundButton newSound = new() { Play = new Border
	    {
		    StrokeShape = new RoundRectangle
		    {
			    CornerRadius = new CornerRadius(5, 5, 5, 5)
		    },
		    StrokeThickness = 1, // Optional border
		    Content = new Label
		    {
			    ClassId = "PlayButton",
			    Text = "▶  " + Path.GetFileNameWithoutExtension(androidFriendlyPath),
			    Padding = new Thickness(10, 28, 10, 30),
			    Margin = new Thickness(0), // Remove internal margin
			    HorizontalTextAlignment = TextAlignment.Center
		    }
	    }};
		    if (!ClientToggle)
				{
					newSound = NewButton(filePath);
				}
#endif
	    

        AnimationExtensions.SetupPointerEffects(newSound.Play, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
            Color.FromArgb("#786af7"));
        TapGestureRecognizer playTapGesture = new();
#if WINDOWS
				playTapGesture.Tapped += (sender, args) => PlaySound(filePath);
#else
        if (ClientToggle)
        {
	        playTapGesture.Tapped += async (sender, args) => await _soundboardClient.SendCommandAsync(filePath);
        }
        else
        {
	        playTapGesture.Tapped += (sender, args) => PlaySound(filePath);
        }
#endif

        newSound.Play.GestureRecognizers.Add(playTapGesture);
        Grid.SetColumn(newSound.Play, 0);
        #if WINDOWS
        Grid.SetColumnSpan(newSound.Play, 3);
        #else
        Grid.SetColumnSpan(newSound.Play, 2);
        #endif
        Grid.SetRow(newSound.Play, 0);
        container.Children.Add(newSound.Play);

        #if WINDOWS
        AnimationExtensions.SetupPointerEffects(newSound.Bind, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
            Color.FromArgb("#786af7"));
        var bindTapGesture = new TapGestureRecognizer();
        bindTapGesture.Tapped += (sender, args) => StartKeyBinding(sender, filePath);
        newSound.Bind.GestureRecognizers.Add(bindTapGesture);
        Grid.SetColumn(newSound.Bind, 0);
        Grid.SetRow(newSound.Bind, 1);
        container.Children.Add(newSound.Bind);
        #endif

	    if (newSound.Rename != null)
	    {
		    AnimationExtensions.SetupPointerEffects(newSound.Rename, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
			    Color.FromArgb("#786af7"));
		    TapGestureRecognizer renameTapGesture = new();
		    renameTapGesture.Tapped += (sender, args) => RenameButton(sender, filePath);
		    newSound.Rename.GestureRecognizers.Add(renameTapGesture);
#if WINDOWS
        Grid.SetColumn(newSound.Rename, 1);
#else
		    Grid.SetColumn(newSound.Rename, 0);
#endif
		    Grid.SetRow(newSound.Rename, 1);
		    container.Children.Add(newSound.Rename);
	    }

	    if (newSound.Remove != null)
	    {
		    AnimationExtensions.SetupPointerEffects(newSound.Remove, Colors.LightCoral, Color.FromArgb("#ff2b2b"),
			    Color.FromArgb("#786af7"));
		    TapGestureRecognizer removeTapGesture = new();
		    removeTapGesture.Tapped += (sender, args) => RemoveSoundButton(container, filePath);
		    newSound.Remove.GestureRecognizers.Add(removeTapGesture);
#if WINDOWS
        Grid.SetColumn(newSound.Remove, 2);
#else
		    Grid.SetColumn(newSound.Remove, 1);
#endif
		    Grid.SetRow(newSound.Remove, 1);
		    container.Children.Add(newSound.Remove);
	    }

	    _soundButtons.Add(filePath, newSound);
        SoundButtonPanel.Add(container);
    }

    private SoundButton CreateStopButton(string? binding = null)
    {
	    return new SoundButton
	    {
		    Play = new Border
		    {
			    StrokeShape = new RoundRectangle()
			    {
#if WINDOWS
				    CornerRadius = new CornerRadius(5, 0, 5, 0)
#else
						CornerRadius = new CornerRadius(5, 5, 5, 5)
#endif
			    },
			    StrokeThickness = 1,
			    Content = new Label
			    {
				    ClassId = "PlayButton",
				    Text = "Stop",
#if WINDOWS
				    Padding = new Thickness(10, 8, 10, 10),
#else
		        Padding = new Thickness(22, 22, 22, 25),
#endif 
				    Margin = new Thickness(0),
				    HorizontalTextAlignment = TextAlignment.Center
			    }
		    },
		    #if WINDOWS
		    Bind = new Border
		    {
			    StrokeShape = new RoundRectangle
			    {
				    CornerRadius = new CornerRadius(0, 5, 0, 5)
			    },
			    StrokeThickness = 1,
			    Content = new Label
			    {
				    ClassId = "BindButton",
				    Text = binding ?? "Bind Key",
				    Padding = new Thickness(10, 8, 10, 10),
				    Margin = new Thickness(0),
				    HorizontalTextAlignment = TextAlignment.Center
			    }
		    },
		    #endif
		    Binding = binding
	    };
    }

    private SoundButton NewButton(string filePath, string? binding = null)
    {
	    return new SoundButton
	        {
		        // Border for the sound button
		        Play = new Border
		        {
			        StrokeShape = new RoundRectangle
			        {
				        CornerRadius = new CornerRadius(5, 5, 0, 0)
			        },
			        StrokeThickness = 1, // Optional border
			        Content = new Label
			        {
				        ClassId = "PlayButton",
				        Text = "▶  " + Path.GetFileNameWithoutExtension(filePath),

#if WINDOWS
				        Padding = new Thickness(10, 8, 10, 10),
				        WidthRequest = 250,
				        LineBreakMode = LineBreakMode.HeadTruncation,
#else
								Padding = new Thickness(5, 28, 5, 30),
								WidthRequest = 150,
								VerticalTextAlignment = TextAlignment.Center,
								MaxLines = 2,
								HeightRequest = 100,
#endif
				        Margin = new Thickness(0), // Remove internal margin
				        HorizontalTextAlignment = TextAlignment.Center
			        }
		        },
#if WINDOWS
            Bind = new Border
            {
                StrokeShape = new RoundRectangle
                {
                    CornerRadius = new CornerRadius(0, 0, 5, 0)
                },
                StrokeThickness = 1,
                Content = new Label
                {
                    ClassId = "BindButton",
                    Text = binding ?? "Bind Key",
                    Padding = new Thickness(10, 8, 10, 10),
                    Margin = new Thickness(0), // Remove margin
                    HorizontalTextAlignment = TextAlignment.Center
                }
            },
#endif
		        Rename = new Border
		        {
#if WINDOWS // As there is no bind button this will be the bottom left on mobile, hence the rounded corner needed
                StrokeShape = new Rectangle(),
#else
			        StrokeShape = new RoundRectangle()
			        {
				        CornerRadius = new CornerRadius(0, 0, 5, 0)
			        },
#endif
			        StrokeThickness = 1,
			        Content = new Label
			        {
				        ClassId = "RenameButton",
				        Text = "\u270F",
				        Padding = new Thickness(10, 8, 10, 10),
				        Margin = new Thickness(0), // Remove margin
				        HorizontalTextAlignment = TextAlignment.Center
			        }
		        },
		        Remove = new Border
		        {
			        StrokeShape = new RoundRectangle
			        {
				        CornerRadius = new CornerRadius(0, 0, 0, 5)
			        },
			        StrokeThickness = 1, // Optional border
			        Stroke = Colors.Gray, // Optional border color
			        Content = new Label
			        {
				        ClassId = "RemoveButton",
				        Text = "X",
				        Padding = new Thickness(10, 8, 10, 10),
				        TextColor = Colors.White,
				        Margin = new Thickness(0), // Remove internal margin
				        HorizontalTextAlignment = TextAlignment.Center
			        }
		        },
#if WINDOWS
            Binding = binding
#endif
	        };
    }

    #if WINDOWS
    private void StartKeyBinding(object? sender, string filePath)
    {
        if (sender is Border { Content: Label bindButton })
        {
            _currentlyBindingFilePath = filePath;
            bindButton.Text = "Press Key..."; // Indicate waiting for key press
            StopGlobalListener();
            _keyboardListener.StartListening();
        }
    }

    private void OnBindKeyDown(object? sender, KeyEventArgs e)
    {
        SoundButton selectedSound = _soundButtons[_currentlyBindingFilePath];
        if (selectedSound.Bind.Content is Label bindButton && e.KeyCode != null) bindButton.Text = e.KeyCode;
        selectedSound.Binding = e.KeyCode;
        _keyboardListener.StopListening();
        StartGlobalListener();
        UpdateBindingsFile();
    }
    #endif

    private async void RenameButton(object sender, string filePath)
    {
        if (sender is Border renameButton)
        {
            if (renameButton.Parent is Grid buttonGrid)
            {
                RenamePopup popup = new()
                {
                    Anchor = renameButton
                };

                string? newName = await this.ShowPopupAsync(popup) as string;
                if (!string.IsNullOrEmpty(newName))
                {
                    string newPath = Path.Combine(_soundsFolderName, newName + ".mp3");
                    Border newPlay = new();
                    #if WINDOWS
                    Border newBind = new();
                    #endif
                    Border newRename = new();
                    Border newRemove = new();

                    foreach (IView? child in buttonGrid.Children)
                    {
                        if (child is Border { Content: Label label } border)
                        {
                            switch (label.ClassId)
                            {
                                case "PlayButton":
                                    label.Text = "▶  " + newName;
                                    TapGestureRecognizer newPlayTapGesture = new();
                                    newPlayTapGesture.Tapped += (sender, args) => PlaySound(newPath);
                                    border.GestureRecognizers.Clear();
                                    border.GestureRecognizers.Add(newPlayTapGesture);
                                    AnimationExtensions.SetupPointerEffects(border, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
                                        Color.FromArgb("#786af7"));
                                    newPlay = border;
                                    break;
                                #if WINDOWS
                                case "BindButton":
                                    TapGestureRecognizer newBindTapGesture = new();
                                    newBindTapGesture.Tapped += (sender, args) => StartKeyBinding(sender, newPath);
                                    border.GestureRecognizers.Clear();
                                    border.GestureRecognizers.Add(newBindTapGesture);
                                    AnimationExtensions.SetupPointerEffects(border, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
                                        Color.FromArgb("#786af7"));
                                    newBind = border;
                                    break;
                                #endif
                                case "RenameButton":
                                    TapGestureRecognizer newRenameTapGesture = new();
                                    newRenameTapGesture.Tapped += (sender, args) => RenameButton(sender, newPath);
                                    border.GestureRecognizers.Clear();
                                    border.GestureRecognizers.Add(newRenameTapGesture);
                                    AnimationExtensions.SetupPointerEffects(border, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
                                        Color.FromArgb("#786af7"));
                                    newRename = border;
                                    break;
                                case "RemoveButton":
                                    TapGestureRecognizer newRemoveTapGesture = new();
                                    newRemoveTapGesture.Tapped += (sender, args) =>
                                        RemoveSoundButton(buttonGrid, newPath);
                                    border.GestureRecognizers.Clear();
                                    border.GestureRecognizers.Add(newRemoveTapGesture);
                                    AnimationExtensions.SetupPointerEffects(border, Colors.LightCoral, Color.FromArgb("#ff2b2b"),
                                        Color.FromArgb("#786af7"));
                                    newRemove = border;
                                    break;
                            }
                        }
                    }

                    if (_soundButtons.TryGetValue(filePath, out SoundButton? value))
                    {
                        string? binding = value.Binding;
                        _soundButtons.Remove(filePath);
                        _soundButtons.Add(newPath, new SoundButton()
                        {
                            Play = newPlay,
                            #if WINDOWS
                            Bind = newBind,
                            #endif
                            Rename = newRename,
                            Remove = newRemove,
                            Binding = binding
                        });
                    }

                    File.Move(filePath, newPath);
                }
            }
        }
    }
    
    public static string GetLocalIPAddress()
    {
        // Use a preprocessor directive to compile this code only on Windows.
        // This ensures the code is not included in other platform builds,
        // which might use a different method or might not require this feature.
        #if WINDOWS
        try
        {
            // Get all network interfaces on the machine.
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
	            // We are only interested in active, operational interfaces that are
                // either Ethernet or Wireless (Wi-Fi).
                if (networkInterface is not { OperationalStatus: OperationalStatus.Up, NetworkInterfaceType: NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211 }) continue;
                // Get the IP properties for the current interface.
                IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();

                // Iterate through all the unicast IP addresses assigned to this interface.
                foreach (UnicastIPAddressInformation ip in ipProperties.UnicastAddresses)
                {
	                // Check if the address is an IPv4 address.
	                if (ip.Address.AddressFamily != AddressFamily.InterNetwork) continue;
	                // Ensure the address is not a loopback address (e.g., 127.0.0.1)
	                // and is not a link-local address. We want a public or private IP.
	                if (!IPAddress.IsLoopback(ip.Address)) return ip.Address.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            // In a real application, you might want to log this exception
            // for debugging purposes. For now, we'll just return "Error".
            Console.WriteLine($"Error getting IP address: {ex.Message}");
            return "Error";
        }
        #endif

        // If no suitable IP address was found on Windows, or if the code is running on
        // a non-Windows platform, this line will be executed.
        return "Not Found";
    }

    private async void RemoveSoundButton(Grid containerToRemove, string filePathToRemove)
    {
        if (await DisplayAlert("Confirm",
                $"Are you sure you want to remove '{Path.GetFileNameWithoutExtension(filePathToRemove)}'?", "Yes",
                "No"))
        {
            try
            {
                // Remove the container (which holds both buttons) from the SoundButtonPanel
                SoundButtonPanel.Remove(containerToRemove);
                _soundButtons.Remove(filePathToRemove);
                #if WINDOWS
                UpdateBindingsFile();       
                #endif
                // Delete the corresponding sound file from the Sounds folder
                if (File.Exists(filePathToRemove))
                {
                    File.Delete(filePathToRemove);
                }
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error", $"Error removing sound: {ex.Message}", "OK"));
            }
        }
    }

    #if WINDOWS
    private void HandleKeyPress(VirtualKey key)
    {
        foreach (KeyValuePair<string, SoundButton> sound in _soundButtons.Where(sound => sound.Value.Binding == key.ToString()))
        {
	        if (sound.Key == "stop")
	        {
		        StopPlayback();
	        }
	        else
	        {
		        PlaySound(sound.Key);
	        }
	        break;
        }
    }
    #endif

	private void StopPlayback()
	{
		foreach (var sound in _playingSounds.ToList())
		{
			sound.Stop();
		}
	}

    private async void PlaySound(string filePath)
    {
        #if WINDOWS
	    try
	    {
		    if (_selectedOutputDeviceIndex == -1)
		    {
			    SetDefaultOutputDevice();
		    }

		    if (_selectedOutputDeviceIndex != -1)
		    {
			    WaveOutEvent outputDevice = new() { DeviceNumber = _selectedOutputDeviceIndex };
			    await using AudioFileReader audioFileReader = new(filePath);
			    outputDevice.Init(audioFileReader);
			    _playingSounds.Add(outputDevice);
			    outputDevice.Play();

			    while (outputDevice.PlaybackState == PlaybackState.Playing)
			    {
				    await Task.Delay(100);
			    }

			    _playingSounds.Remove(outputDevice);

			    outputDevice.Stop();
			    outputDevice.Dispose();
		    }
		    else
		    {
			    MainThread.BeginInvokeOnMainThread(async void () =>
				    await DisplayAlert("Warning", "Voicemeeter Input not found.", "OK"));
		    }
	    }
	    catch (Exception ex)
	    {
		    MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error (Windows/NAudio)", ex.Message, "OK"));
	    }
#else
        try
        {
            var player = _audioManager.CreatePlayer(filePath);
            _playingSounds.Add(player);

            player.PlaybackEnded += (s, e) =>
            {
	            _playingSounds.Remove(player);
	            player.Dispose();
            };
            
            player.Play();
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async void () => await DisplayAlert("Error (Audio Manager)", ex.Message, "OK"));
        }
#endif

	    // Maybe add code to make it play out of speakers on non windows devices
    }

    #if WINDOWS
    private void UpdateBindingsFile()
    {
        try
        {
            using FileStream fs = new(_bindingsFile, FileMode.Create);
            using BsonDataWriter writer = new(fs);
            JsonSerializer serializer = new();
            serializer.Serialize(writer, _soundButtons.Where(sound => sound.Value.Binding != null).ToDictionary());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    
    private void UpdateServerToggle()
    {
	    AppConfig appConfig = new()
	    {
		    ServerStarted = ServerStart,
		    IpAddress = _ipConfig,
		    Port = _port
	    };
	    
	    try
	    {
		    using FileStream fs = new(_netSettingsFile, FileMode.Create);
		    using BsonDataWriter writer = new(fs);
		    JsonSerializer serializer = new();
		    serializer.Serialize(writer, appConfig);
	    }
	    catch (Exception ex)
	    {
		    Console.WriteLine(ex.Message);
	    }
    }

    private Dictionary<string, SoundButton>? GetBoundSounds()
    {
        try
        {
	        if (!File.Exists(_bindingsFile)) return [];
	        using FileStream fs = new(_bindingsFile, FileMode.Open);
	        using BsonDataReader reader = new(fs);
	        JsonSerializer serializer = new();
	        return serializer.Deserialize<Dictionary<string, SoundButton>>(reader);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return [];
        }
    }
    
    private void UpdateOutputDeviceBson(string newOutputDevice)
    {
        try
        {
            var data = new { OutputDevice = newOutputDevice };
            using FileStream fs = new(_outputDeviceFile, FileMode.Create);
            using BsonDataWriter writer = new(fs);
            JsonSerializer serializer = new();
            serializer.Serialize(writer, data);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private string? GetOutputDeviceFromBson()
    {
        try
        {
            if (File.Exists(_outputDeviceFile))
            {
                byte[] bsonData = File.ReadAllBytes(_outputDeviceFile);
                BsonDocument bsonDocument = BsonSerializer.Deserialize<BsonDocument>(bsonData);
                dynamic savedOutput = BsonSerializer.Deserialize<dynamic>(bsonDocument);
                return savedOutput?.OutputDevice;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return null;
    }

    private void StartGlobalListener()
    {
        _globalKeyboardListener = new GlobalKeyboardListener(HandleKeyPress);
    }

    private void StopGlobalListener()
    {
        if (_globalKeyboardListener != null)
        {
            _globalKeyboardListener.Dispose();
            _globalKeyboardListener = null;
        }
    }
    

    private int FindDeviceIndex(WaveOutCapabilities capabilities)
    {
        for (int n = 0; n < WaveOut.DeviceCount; n++)
        {
            if (WaveOut.GetCapabilities(n).ProductName == capabilities.ProductName &&
                WaveOut.GetCapabilities(n).ManufacturerGuid == capabilities.ManufacturerGuid &&
                WaveOut.GetCapabilities(n).ProductGuid == capabilities.ProductGuid &&
                WaveOut.GetCapabilities(n).Channels == capabilities.Channels &&
                WaveOut.GetCapabilities(n).SupportsPlaybackRateControl == capabilities.SupportsPlaybackRateControl)
            {
                return n;
            }
        }
        return -1; // Device not found
    }

    private void ShowTrayIcon(object? sender, EventArgs? e)
    {
        if (!PageContainer.Contains(_trayPopup))
        {
            PageContainer.Children.Add(_trayPopup);
        }
    }
    #endif
    
    [RelayCommand]
    public void ShowWindow()
    {
        #if WINDOWS
        AppClosingHandler.ShowApp();
        _trayPopup.IsEnabled = false;
        #endif
    }

    [RelayCommand]
    public void CloseApp()
    {
        Application.Current.Quit();
    }

    public class SoundButton
    {
        [JsonIgnore] public required Border Play;
        #if WINDOWS
        [JsonIgnore] public required Border Bind;
        #endif
        [JsonIgnore] public Border? Rename;
        [JsonIgnore] public Border? Remove;
        public string? Binding;
    }
}

public class AppConfig
{
	public int Port { get; set; }
	public string IpAddress { get; set; }
	
	public bool ServerStarted { get; set; }
}

public static class AnimationExtensions
{
    public static void SetupPointerEffects(View element, Color originalColor, Color? hoverColor = null, Color? pressColor = null, uint animationSpeed = 125)
    {
        element.BackgroundColor = originalColor;
        
        PointerGestureRecognizer? gestureRecognizer = element.GestureRecognizers.OfType<PointerGestureRecognizer>().FirstOrDefault();
        if (gestureRecognizer == null && (hoverColor != null || pressColor != null))
        {
            gestureRecognizer = new PointerGestureRecognizer();
            if (hoverColor != null)
            {
                gestureRecognizer.PointerEntered += async (s, e) => { await element.ColorTo(hoverColor, animationSpeed); };
                gestureRecognizer.PointerExited += async (s, e) => { await element.ColorTo(originalColor, animationSpeed); };
            }

            if (pressColor != null)
            {
                gestureRecognizer.PointerPressed += (s, e) =>
                {
                    element.BackgroundColor = pressColor;
                };
                
                gestureRecognizer.PointerReleased += async (s, e) =>
                {
                    await element.ColorTo(hoverColor ?? originalColor, animationSpeed);
                };
            }
            element.GestureRecognizers.Add(gestureRecognizer);
        }
    }
    
    private static Task<bool> ColorTo(this VisualElement element, Color color, uint length = 250, Easing? easing = null)
    {
        Color fromColor = element.BackgroundColor;
        TaskCompletionSource<bool> tcs = new();

        new Animation(t =>
        {
            element.BackgroundColor = Color.FromRgba(
                fromColor.Red + (color.Red - fromColor.Red) * t,
                fromColor.Green + (color.Green - fromColor.Green) * t,
                fromColor.Blue + (color.Blue - fromColor.Blue) * t,
                fromColor.Alpha + (color.Alpha - fromColor.Alpha) * t);
        }).Commit(element, "ColorTo", length: length, easing: easing, finished: (v, cancelled) =>
        {
            tcs.SetResult(!cancelled);
        });

        return tcs.Task;
    }
}