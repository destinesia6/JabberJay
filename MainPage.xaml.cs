#if WINDOWS
using Windows.System;
#endif
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Platform;
using NAudio.Wave;
using Newtonsoft.Json;
using SoundboardMAUI;
using CommunityToolkit.Mvvm.Input;
using Border = Microsoft.Maui.Controls.Border;
using Button = Microsoft.Maui.Controls.Button;
using Color = Microsoft.Maui.Graphics.Color;
using ColumnDefinition = Microsoft.Maui.Controls.ColumnDefinition;
using Grid = Microsoft.Maui.Controls.Grid;
using Path = System.IO.Path;
using Rectangle = Microsoft.Maui.Controls.Shapes.Rectangle;

namespace JabberJay;

public partial class MainPage : ContentPage
{
    private readonly string _soundsFolderName = "Sounds";
    private readonly string _bindingsFile = "Bindings.json";
    private int _voicemeeterInputDeviceIndex = -1;
    private readonly IKeyboardListener _keyboardListener; // Used to bind new keys
    private GlobalKeyboardListener? _globalKeyboardListener; // Used to detect input from bound keys
    private string _currentlyBindingFilePath;
    private Dictionary<string, SoundButton> _soundButtons = new(); // Store key bindings
    
    public MainPage(IKeyboardListener keyboardListener)
    {
        InitializeComponent();
        _bindingsFile = Path.Combine(FileSystem.AppDataDirectory, _bindingsFile);
        _soundsFolderName = Path.Combine(FileSystem.AppDataDirectory, _soundsFolderName);
        _keyboardListener = keyboardListener;
        _keyboardListener.KeyDown += OnBindKeyDown;
        for (int n = 0; n < WaveOut.DeviceCount; n++)
        {
            WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(n);
            if (deviceInfo.ProductName.Contains("Voicemeeter Input", StringComparison.OrdinalIgnoreCase))
            {
                _voicemeeterInputDeviceIndex = n;
                break;
            }
        }
        LoadSoundButtons();
        ColorButton(AddSound, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"));
        StartGlobalListener();
        BindingContext = this;
        PageContainer.Children.Remove(TrayPopup);
        AppClosingHandler.PageHide += ShowTrayIcon;
    }
    
    private async void AddSoundButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            FileResult? result = await FilePicker.PickAsync(new PickOptions
            {
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
                string destinationFolderPath = Path.Combine(FileSystem.AppDataDirectory, _soundsFolderName);

                // Ensure the directory exists
                if (!Directory.Exists(destinationFolderPath))
                {
                    Directory.CreateDirectory(destinationFolderPath);
                }

                string fileName = Path.GetFileName(sourceFilePath);
                string destinationFilePath = Path.Combine(destinationFolderPath, fileName);

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
    

    private async void LoadSoundButtons()
    {
        Dictionary<string, SoundButton>? bindings = new();
        
        //Loads bindings
        if (File.Exists(_bindingsFile))
        {
            try
            {
                string json = await File.ReadAllTextAsync(_bindingsFile);
                bindings = JsonConvert.DeserializeObject<Dictionary<string, SoundButton>>(json);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
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
                    if (bindings != null && bindings.TryGetValue(filePath, out SoundButton? sound))
                    {
                        CreateSoundButton(filePath, sound.Binding);
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
            // Create the directory if it doesn't exist (optional, AddSoundButton will also create it)
            Directory.CreateDirectory(_soundsFolderName);
        }
    }

    private void CreateSoundButton(string filePath, string? binding = null)
    {
        Grid container = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto), // For the bind button
                new ColumnDefinition(GridLength.Auto) // For remove button
            },
            Margin = new Thickness(5), // Keep a small margin around the entire merged button
            Padding = new Thickness(0)
        };

        SoundButton newSound = new()
        {
            // Border for the sound button
            Play = new Border
            {
                StrokeShape = new RoundRectangle
                {
                    CornerRadius = new CornerRadius(10, 0, 10, 0)
                },
                StrokeThickness = 1, // Optional border
                Content = new Button
                {
                    Text = Path.GetFileNameWithoutExtension(filePath),
                    Padding = new Thickness(10),
                    Command = new Command(() => PlaySound(filePath)),
                    CommandParameter = filePath,
                    Margin = new Thickness(0) // Remove internal margin
                }
            },
            Bind = new Border
            {
                StrokeShape = new Rectangle(),
                StrokeThickness = 1,
                Content = new Button
                {
                    Text = binding ?? "Bind Key",
                    HeightRequest = 30,
                    FontSize = 12,
                    CommandParameter = filePath,
                    Margin = new Thickness(0) // Remove margin
                }
            },
            Remove = new Border
            {
                StrokeShape = new RoundRectangle
                {
                    CornerRadius =  new CornerRadius(0, 10, 0, 10)
                },
                StrokeThickness = 1, // Optional border
                Stroke = Colors.Gray, // Optional border color
                Content = new Button
                {
                    Text = "X",
                    WidthRequest = 30,
                    HeightRequest = 30,
                    FontSize = 12,
                    TextColor = Colors.White,
                    Margin = new Thickness(0), // Remove internal margin
                    Command = new Command(() => RemoveSoundButton(container, filePath))
                }
            },
            Binding = binding
        };

        ColorButton(newSound.Play, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"));
        Grid.SetColumn(newSound.Play, 0);
        container.Children.Add(newSound.Play);
        ColorButton(newSound.Bind, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"));
        ((Button)newSound.Bind.Content).Clicked += StartKeyBinding;
        Grid.SetColumn(newSound.Bind, 1);
        container.Children.Add(newSound.Bind);
        ColorButton(newSound.Remove, Colors.LightCoral, Color.FromArgb("#ff2b2b"));
        Grid.SetColumn(newSound.Remove, 2);
        container.Children.Add(newSound.Remove);
        _soundButtons.Add(filePath, newSound);
        SoundButtonPanel.Add(container);
    }

    private void StartKeyBinding(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string filePath } bindButton)
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
        if (selectedSound.Bind.Content is Button bindButton && e.KeyCode != null) bindButton.Text = e.KeyCode;
        selectedSound.Binding = e.KeyCode;
        _keyboardListener.StopListening();
        StartGlobalListener();
        try
        {
            string json = JsonConvert.SerializeObject(_soundButtons.Where(sound => sound.Value.Binding != null).ToDictionary(sound => sound.Key, sound => sound.Value));
            File.WriteAllText(_bindingsFile, json);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            throw;
        }
    }
    
    private static void ColorButton(VisualElement element, Color color, Color hoverColor)
    {
        element.BackgroundColor = color;
        element.HandlerChanged += (sender, args) =>
        {
            switch (sender)
            {
                case Button { Handler.PlatformView: Microsoft.UI.Xaml.Controls.Button buttonHandler } button:
                    buttonHandler.PointerExited += (s, a) =>
                    {
                        button.BackgroundColor = color;
                    };
                    buttonHandler.PointerEntered += (s, a) =>
                    {
                        button.BackgroundColor = hoverColor;
                    };
                    break;
                case Border { Handler.PlatformView: ContentPanel borderHandler } border:
                {
                    if (border.Content != null)
                    {
                        border.Content.BackgroundColor = color;
                        borderHandler.PointerExited += (s, a) =>
                        {
                            border.BackgroundColor = color;
                            border.Content.BackgroundColor = color;
                        };
                        borderHandler.PointerEntered += (s, a) =>
                        {
                            border.BackgroundColor = hoverColor;
                            border.Content.BackgroundColor = hoverColor;
                        };
                    }
                    break;
                }
            }
        };
    }
    
    private async void RemoveSoundButton(Grid containerToRemove, string filePathToRemove)
    {
        if (await DisplayAlert("Confirm", $"Are you sure you want to remove '{Path.GetFileNameWithoutExtension(filePathToRemove)}'?", "Yes", "No"))
        {
            try
            {
                // Remove the container (which holds both buttons) from the SoundButtonPanel
                SoundButtonPanel.Remove(containerToRemove);

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
        foreach (KeyValuePair<string, SoundButton> sound in _soundButtons.Where(sound => sound.Value.Binding == key.ToString()))
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
            if (_voicemeeterInputDeviceIndex == -1)
            {
                for (int n = 0; n < WaveOut.DeviceCount; n++)
                {
                    WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(n);
                    if (deviceInfo.ProductName.Contains("Voicemeeter Input", StringComparison.OrdinalIgnoreCase))
                    {
                        _voicemeeterInputDeviceIndex = n;
                        break;
                    }
                }
            }

            if (_voicemeeterInputDeviceIndex != -1)
            {
                WaveOutEvent outputDevice = new() { DeviceNumber = _voicemeeterInputDeviceIndex };
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

    private void ShowTrayIcon(object sender, EventArgs e)
    {
        PageContainer.Children.Add(TrayPopup);
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
        [JsonIgnore]
        public required Border Play;
        [JsonIgnore]
        public required Border Bind;
        [JsonIgnore]
        public required Border Remove;
        public string? Binding;
    }
}