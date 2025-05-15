#if WINDOWS
using Windows.System;
#endif
using System.Collections.ObjectModel;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Microsoft.Maui.Controls.Shapes;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;
using System.Diagnostics;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Input;
using YoutubeDLSharp.Metadata;
using Border = Microsoft.Maui.Controls.Border;
using Color = Microsoft.Maui.Graphics.Color;
using ColumnDefinition = Microsoft.Maui.Controls.ColumnDefinition;
using Grid = Microsoft.Maui.Controls.Grid;
using Path = System.IO.Path;
using Rectangle = Microsoft.Maui.Controls.Shapes.Rectangle;

namespace JabberJay;

public partial class MainPage : ContentPage
{
    private readonly string _soundsFolderName = "Sounds";
    private readonly string _bindingsFile = "Bindings.bson";
    private readonly string _outputDeviceFile = "Output.bson";
    private int _selectedOutputDeviceIndex = -1;
    private readonly IKeyboardListener _keyboardListener; // Used to bind new keys
    private GlobalKeyboardListener? _globalKeyboardListener; // Used to detect input from bound keys
    private string _currentlyBindingFilePath;
    private Dictionary<string, SoundButton> _soundButtons = new(); // Store key bindings
    private string _ytDlpPath = "";
    private string _windowsPath = "";
    private bool _autoStart;

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

    public MainPage(IKeyboardListener keyboardListener)
    {
        InitializeComponent();
        _soundsFolderName = Path.Combine(FileSystem.AppDataDirectory, _soundsFolderName);
        _bindingsFile = Path.Combine(FileSystem.AppDataDirectory, _bindingsFile);
        _outputDeviceFile = Path.Combine(FileSystem.AppDataDirectory, _outputDeviceFile);
        _soundsFolderName = Path.Combine(FileSystem.AppDataDirectory, _soundsFolderName);
        _keyboardListener = keyboardListener;
        _keyboardListener.KeyDown += OnBindKeyDown;
        _autoStart = AutoStartService.GetAutoStart();
        InitializeExternalToolsAsync();
        LoadSoundButtons();
        AnimationExtensions.SetupPointerEffects(AddSound, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
            Color.FromArgb("#786af7"));
        AnimationExtensions.SetupPointerEffects(ImportSound, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
            Color.FromArgb("#786af7"));
        StartGlobalListener();
        BindingContext = this;
        PageContainer.Children.Remove(TrayPopup);
        AppClosingHandler.PageHide += ShowTrayIcon;
    }

    private async void InitializeExternalToolsAsync()
    {
        try
        {
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
                await DisplayAlert("Error", "Could not find import tools. Import feature disabled", "OK");
                ImportSound.IsEnabled = false;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            await DisplayAlert("Error", "Error finding import tools. Import feature disabled", "OK");
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
        if (savedOutput != null && OutputDevices.Select(device => device.ProductName).Contains(savedOutput) && false)
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
    
    

    private async void AddSoundButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            FileResult? result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select an MP3 sound file",
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
                File.Move(sourceFilePath, destinationFilePath, true); // Overwrite if it exists

                CreateSoundButton(destinationFilePath);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error picking or moving file: {ex.Message}", "OK");
        }
    }

    private async void ImportSoundButton_Clicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_ytDlpPath) || !File.Exists(_ytDlpPath))
        {
            await DisplayAlert("Error", "yt-dlp downloader not found or configured. Please check setup.", "OK");
            return;
        }

        string url = await DisplayPromptAsync("Video URL", "Enter a video URL:", "Download");

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                await DisplayAlert("Error", "URL cannot be empty.", "OK");
            }
            else
            {
                await DisplayAlert("Error", "URL is not valid.", "OK");
            }

            return;
        }

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
                await DisplayAlert("Error", "Failed to fetch metadata", "Ok");
                return;
            }

            string newSoundName = metaDataRes.Data.Title;

            MainThread.BeginInvokeOnMainThread(() => DownloadStatusLabel.Text = "Checking existing sounds...");

            if (File.Exists(Path.Combine(_soundsFolderName, $"{newSoundName}.mp3")))
            {
                await DisplayAlert("Already Exists", $"The sound {newSoundName} already exists", "Ok");
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

            IProgress<DownloadProgress> progress = new Progress<DownloadProgress>(HandleDownloadProgress);

            RunResult<string> res = await ytdl.RunAudioDownload(url, format: AudioConversionFormat.Mp3,
                overrideOptions: options, ct: new CancellationTokenSource(TimeSpan.FromMinutes(5)).Token,
                progress: progress);

            if (res.Success)
            {
                MainThread.BeginInvokeOnMainThread(() => DownloadStatusLabel.Text = "Locating File...");
                string[] outputLines = res.Data.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                string? downloadedFilePath = null;
                const string destinationPrefix = "[ExtractAudio] Destination: ";
                string? potentialPathLine =
                    outputLines.LastOrDefault(line => line.StartsWith(destinationPrefix) || line.EndsWith(".mp3"));
                if (potentialPathLine != null)
                {
                    if (potentialPathLine.Contains(destinationPrefix))
                    {
                        downloadedFilePath =
                            potentialPathLine[
                                (potentialPathLine.IndexOf(destinationPrefix, StringComparison.Ordinal) +
                                 destinationPrefix.Length)..].Trim();
                    }
                    else if (File.Exists(Path.Combine(_soundsFolderName, potentialPathLine)))
                    {
                        downloadedFilePath = Path.Combine(_soundsFolderName, potentialPathLine);
                    }
                }

                if (string.IsNullOrEmpty(downloadedFilePath) || !File.Exists(downloadedFilePath))
                {
                    await DisplayAlert("Error", "Could not find downloaded file.", "OK");
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

    }

    private void HandleDownloadProgress(DownloadProgress p)
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


    private async void LoadSoundButtons()
    {
        Dictionary<string, SoundButton>? bindings = new();

        //Loads bindings
        if (File.Exists(_bindingsFile))
        {
            try
            {
                string json = await File.ReadAllTextAsync(_bindingsFile);
                bindings = GetBoundSounds();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        // Loads sounds
        if (Directory.Exists(_soundsFolderName))
        {
            try
            {
                string[] mp3Files = Directory.GetFiles(_soundsFolderName, "*.mp3");
                foreach (string filePath in mp3Files)
                {
                    KeyValuePair<string, SoundButton>? bindingEntry = bindings?.FirstOrDefault(kvp =>
                        string.Equals(kvp.Key, filePath, StringComparison.OrdinalIgnoreCase));

                    if (bindingEntry is { Value: not null })
                    {
                        CreateSoundButton(filePath, bindingEntry.Value.Value.Binding);
                    }
                    else
                    {
                        CreateSoundButton(filePath);
                    }

                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error loading sound buttons from folder: {ex.Message}", "OK");
            }
        }
        else
        {
            // Create the directory if it doesn't exist
            Directory.CreateDirectory(_soundsFolderName);
        }
    }

    private void CreateSoundButton(string filePath, string? binding = null)
    {
        Grid container = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            Margin = new Thickness(3), // Keep a small margin around the entire merged button
            Padding = new Thickness(0)
        };

        SoundButton newSound = new()
        {
            // Border for the sound button
            Play = new Border
            {
                StrokeShape = new RoundRectangle
                {
                    CornerRadius = new CornerRadius(10, 10, 0, 0)
                },
                StrokeThickness = 1, // Optional border
                Content = new Label
                {
                    ClassId = "PlayButton",
                    Text = "▶  " + Path.GetFileNameWithoutExtension(filePath),
                    Padding = new Thickness(10, 8, 10, 10),
                    Margin = new Thickness(0), // Remove internal margin
                    HorizontalTextAlignment = TextAlignment.Center
                }
            },
            Bind = new Border
            {
                StrokeShape = new RoundRectangle
                {
                    CornerRadius = new CornerRadius(0, 0, 10, 0)
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
            Rename = new Border
            {
                StrokeShape = new Rectangle(),
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
                    CornerRadius = new CornerRadius(0, 0, 0, 10)
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
            Binding = binding
        };

        AnimationExtensions.SetupPointerEffects(newSound.Play, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
            Color.FromArgb("#786af7"));
        var playTapGesture = new TapGestureRecognizer();
        playTapGesture.Tapped += (sender, args) => PlaySound(filePath);
        newSound.Play.GestureRecognizers.Add(playTapGesture);
        Grid.SetColumn(newSound.Play, 0);
        Grid.SetColumnSpan(newSound.Play, 3);
        Grid.SetRow(newSound.Play, 0);
        container.Children.Add(newSound.Play);

        AnimationExtensions.SetupPointerEffects(newSound.Bind, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
            Color.FromArgb("#786af7"));
        var bindTapGesture = new TapGestureRecognizer();
        bindTapGesture.Tapped += (sender, args) => StartKeyBinding(sender, filePath);
        newSound.Bind.GestureRecognizers.Add(bindTapGesture);
        Grid.SetColumn(newSound.Bind, 0);
        Grid.SetRow(newSound.Bind, 1);
        container.Children.Add(newSound.Bind);

        AnimationExtensions.SetupPointerEffects(newSound.Rename, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"),
            Color.FromArgb("#786af7"));
        var renameTapGesture = new TapGestureRecognizer();
        renameTapGesture.Tapped += (sender, args) => RenameButton(sender, filePath);
        newSound.Rename.GestureRecognizers.Add(renameTapGesture);
        Grid.SetColumn(newSound.Rename, 1);
        Grid.SetRow(newSound.Rename, 1);
        container.Children.Add(newSound.Rename);

        AnimationExtensions.SetupPointerEffects(newSound.Remove, Colors.LightCoral, Color.FromArgb("#ff2b2b"),
            Color.FromArgb("#786af7"));
        var removeTapGesture = new TapGestureRecognizer();
        removeTapGesture.Tapped += (sender, args) => RemoveSoundButton(container, filePath);
        newSound.Remove.GestureRecognizers.Add(removeTapGesture);
        Grid.SetColumn(newSound.Remove, 2);
        Grid.SetRow(newSound.Remove, 1);
        container.Children.Add(newSound.Remove);
        _soundButtons.Add(filePath, newSound);
        SoundButtonPanel.Add(container);
    }

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
                    Border newBind = new();
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
                                    TapGestureRecognizer oldPlayTapGesture = new();
                                    TapGestureRecognizer newPlayTapGesture = new();
                                    oldPlayTapGesture.Tapped += (sender, args) => PlaySound(newPath);
                                    newPlayTapGesture.Tapped += (sender, args) => PlaySound(filePath);
                                    border.GestureRecognizers.Remove(oldPlayTapGesture);
                                    border.GestureRecognizers.Add(newPlayTapGesture);
                                    newPlay = border;
                                    break;
                                case "BindButton":
                                    TapGestureRecognizer oldBindTapGesture = new();
                                    TapGestureRecognizer newBindTapGesture = new();
                                    oldBindTapGesture.Tapped += (sender, args) => StartKeyBinding(sender, filePath);
                                    newBindTapGesture.Tapped += (sender, args) => StartKeyBinding(sender, newPath);
                                    border.GestureRecognizers.Remove(oldBindTapGesture);
                                    border.GestureRecognizers.Add(newBindTapGesture);
                                    newBind = border;
                                    break;
                                case "RenameButton":
                                    TapGestureRecognizer oldRenameTapGesture = new();
                                    TapGestureRecognizer newRenameTapGesture = new();
                                    oldRenameTapGesture.Tapped += (sender, args) => RenameButton(sender, filePath);
                                    newRenameTapGesture.Tapped += (sender, args) => RenameButton(sender, newPath);
                                    border.GestureRecognizers.Remove(oldRenameTapGesture);
                                    border.GestureRecognizers.Add(newRenameTapGesture);
                                    newRename = border;
                                    break;
                                case "RemoveButton":
                                    TapGestureRecognizer oldRemoveTapGesture = new();
                                    TapGestureRecognizer newRemoveTapGesture = new();
                                    oldRemoveTapGesture.Tapped += (sender, args) =>
                                        RemoveSoundButton(buttonGrid, filePath);
                                    newRemoveTapGesture.Tapped += (sender, args) =>
                                        RemoveSoundButton(buttonGrid, newPath);
                                    border.GestureRecognizers.Remove(oldRemoveTapGesture);
                                    border.GestureRecognizers.Add(newRemoveTapGesture);
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
                            Bind = newBind,
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
                UpdateBindingsFile();
                // Delete the corresponding sound file from the Sounds folder
                if (File.Exists(filePathToRemove))
                {
                    File.Delete(filePathToRemove);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error removing sound: {ex.Message}", "OK");
            }
        }
    }

    private void HandleKeyPress(VirtualKey key)
    {
        foreach (KeyValuePair<string, SoundButton> sound in _soundButtons.Where(sound =>
                     sound.Value.Binding == key.ToString()))
        {
            PlaySound(sound.Key);
            break;
        }
    }

    private async void PlaySound(string filePath)
    {
#if WINDOWS
        try
        {
            if (_selectedOutputDeviceIndex == -1)
            {
                for (int n = 0; n < WaveOut.DeviceCount; n++)
                {
                    WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(n);
                    if (deviceInfo.ProductName.Contains("Voicemeeter Input", StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedOutputDeviceIndex = n;
                        break;
                    }
                }
            }

            if (_selectedOutputDeviceIndex != -1)
            {
                WaveOutEvent outputDevice = new() { DeviceNumber = _selectedOutputDeviceIndex };
                await using AudioFileReader audioFileReader = new(filePath);
                outputDevice.Init(audioFileReader);
                outputDevice.Play();
                while (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    await Task.Delay(100); // Simple way to wait for playback to finish
                }

                outputDevice.Stop();
                outputDevice.Dispose();
            }
            else
            {
                await DisplayAlert("Warning", "Voicemeeter Input not found.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error (Windows/NAudio)", ex.Message, "OK");
        }
#endif

        // Maybe add code to make it play out of speakers on non windows devices
    }

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

    private Dictionary<string, SoundButton>? GetBoundSounds()
    {
        try
        {
            if (File.Exists(_bindingsFile))
            {
                using FileStream fs = new(_bindingsFile, FileMode.Open);
                using BsonDataReader reader = new(fs);
                JsonSerializer serializer = new();
                return serializer.Deserialize<Dictionary<string, SoundButton>>(reader);
            }
            else
            {
                return [];
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return [];
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

    private void ShowTrayIcon(object sender, EventArgs e)
    {
        if (!PageContainer.Contains(TrayPopup))
        {
            PageContainer.Children.Add(TrayPopup);
        }
    }

    [RelayCommand]
    public void ShowWindow()
    {
        AppClosingHandler.ShowApp();
        TrayPopup.IsEnabled = false;
    }

    [RelayCommand]
    public void CloseApp()
    {
        Application.Current.Quit();
    }

    private class SoundButton
    {
        [JsonIgnore] public required Border Play;
        [JsonIgnore] public required Border Bind;
        [JsonIgnore] public required Border Rename;
        [JsonIgnore] public required Border Remove;
        public string? Binding;
    }
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