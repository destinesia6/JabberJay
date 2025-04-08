#if WINDOWS
    using NAudio.Wave;
    using SoundboardMAUI.WinUI;
    using Windows.System;
#endif
using Plugin.Maui.Audio;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Platform;
using Newtonsoft.Json;
using Border = Microsoft.Maui.Controls.Border;
using Button = Microsoft.Maui.Controls.Button;
using ColumnDefinition = Microsoft.Maui.Controls.ColumnDefinition;
using Grid = Microsoft.Maui.Controls.Grid;
using Path = System.IO.Path;

namespace SoundboardMAUI;

public partial class MainPage : ContentPage
{
    private IAudioPlayer currentPlayer;
    private string SoundsFolderName = "Sounds";
    private string BindingsFile = "Bindings.json";
    private int _voicemeeterInputDeviceIndex = -1;
    private readonly IKeyboardListener _keyboardListener; // Used to bind new keys
    private GlobalKeyboardListener _globalKeyboardListener; // Used to detect input from bound keys
    private string _currentlyBindingFilePath;
    private Dictionary<string, SoundButton> _soundButtons = new(); // Store key bindings
    
    public MainPage(IKeyboardListener keyboardListener)
    {
        InitializeComponent();
        BindingsFile = Path.Combine(FileSystem.AppDataDirectory, BindingsFile);
        SoundsFolderName = Path.Combine(FileSystem.AppDataDirectory, SoundsFolderName);
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
        ColorButton(addSound, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"));
        StartGlobalListener();
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
                string destinationFolderPath = Path.Combine(FileSystem.AppDataDirectory, SoundsFolderName);

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
        Dictionary<string, SoundButton> bindings = new();
        
        //Loads bindings
        if (File.Exists(BindingsFile))
        {
            try
            {
                string json = File.ReadAllText(BindingsFile);
                bindings = JsonConvert.DeserializeObject<Dictionary<string, SoundButton>>(json);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
        
        // Loads sounds
        if (Directory.Exists(SoundsFolderName))
        {
            try
            {
                string[] mp3Files = Directory.GetFiles(SoundsFolderName, "*.mp3");
                foreach (string filePath in mp3Files)
                {
                    if (bindings.TryGetValue(filePath, out SoundButton sound))
                    {
                        CreateSoundButton(filePath, sound.binding);
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
            Directory.CreateDirectory(SoundsFolderName);
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
            play = new Border
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
            bind = new Border
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
            remove = new Border
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
            binding = binding
        };

        ColorButton(newSound.play, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"));
        Grid.SetColumn(newSound.play, 0);
        container.Children.Add(newSound.play);
        ColorButton(newSound.bind, Color.FromArgb("#5e4dff"), Color.FromArgb("#341efa"));
        ((Button)newSound.bind.Content).Clicked += StartKeyBinding;
        Grid.SetColumn(newSound.bind, 1);
        container.Children.Add(newSound.bind);
        ColorButton(newSound.remove, Colors.LightCoral, Color.FromArgb("#ff2b2b"));
        Grid.SetColumn(newSound.remove, 2);
        container.Children.Add(newSound.remove);
        _soundButtons.Add(filePath, newSound);
        SoundButtonPanel.Add(container);
    }

    private void StartKeyBinding(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string filePath } bindButton)
        {
            _currentlyBindingFilePath = filePath;
            bindButton.Text = "Press Key..."; // Indicate waiting for key press
            StopGlobalListener();
            _keyboardListener.StartListening();
        }
    }
    
    private void OnBindKeyDown(object sender, KeyEventArgs e)
    {
        SoundButton selectedSound = _soundButtons[_currentlyBindingFilePath];
        Button bindButton = selectedSound.bind.Content as Button;
        bindButton.Text = e.KeyCode;
        selectedSound.binding = e.KeyCode;
        _keyboardListener.StopListening();
        StartGlobalListener();
        try
        {
            string json = JsonConvert.SerializeObject(_soundButtons.Where(sound => sound.Value.binding != null).ToDictionary(sound => sound.Key, sound => sound.Value));
            File.WriteAllText(BindingsFile, json);
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
        foreach (KeyValuePair<string, SoundButton> sound in _soundButtons.Where(sound => sound.Value.binding == key.ToString()))
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

    private class SoundButton
    {
        [JsonIgnore]
        public Border play;
        [JsonIgnore]
        public Border bind;
        [JsonIgnore]
        public Border remove;
        public string? binding;
    }
}