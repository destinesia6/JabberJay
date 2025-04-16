using JabberJay;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Microsoft.Extensions.DependencyInjection;
using Application = Microsoft.Maui.Controls.Application;
using Window = Microsoft.Maui.Controls.Window;

namespace SoundboardMAUI;

public partial class App : Application
{
  private MainPage _mainPageInstance;
  
  public App()
  {
    InitializeComponent();
  }
  
  protected override Window CreateWindow(IActivationState? activationState)
  {
    return new Window(new AppShell());
  }
}