using JabberJay;
using NetSparkleUpdater;
using NetSparkleUpdater.Configurations;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
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
  private SparkleUpdater _sparkleUpdater;
  
  public App()
  {
    InitializeComponent();
  }
  
  protected override Window CreateWindow(IActivationState? activationState)
  {
    return new Window(new AppShell());
  }

  private void InitializeUpdater()
  {
    string appCastUrl = "ADD_THE_GITHUB_URL"; //TODO: Add GitHub URL
    
    Ed25519Checker signatureVerifier = new(SecurityMode.Strict, publicKeyFile: "NetSparkle_Ed25519.pub");
    
    _sparkleUpdater = new SparkleUpdater(appCastUrl, signatureVerifier);

    _sparkleUpdater.StartLoop(true);
  }
}