using JabberJay;
using NetSparkleUpdater;
using NetSparkleUpdater.Events;
using Application = Microsoft.Maui.Controls.Application;
using Window = Microsoft.Maui.Controls.Window;

namespace SoundboardMAUI;

public partial class App : Application
{
  private MainPage _mainPageInstance;
  
  public App()
  {
    InitializeComponent();
    //AppRegistration.CheckAndRegisterAppCurrentUser();
  }
  
  protected override Window CreateWindow(IActivationState? activationState)
  {
    return new Window(new AppShell());
  }
}