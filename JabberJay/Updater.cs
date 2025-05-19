using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater;
using NetSparkleUpdater.Events;

namespace JabberJay;

public class Updater
{
  private SparkleUpdater _sparkleUpdater;

  public event EventHandler<UpdateDetectedEventArgs> UpdateDetected;
  public event EventHandler<DownloadFinishedEventArgs> DownloadFinished;
  public event EventHandler<InstallUpdateFailureReason> UpdateFailed; 
  
  public Updater()
  {
    const string appCastUrl = "https://destinesia6.github.io/JabberJay/appcast.xml";
    
    Ed25519Checker signatureVerifier = new(SecurityMode.Strict, publicKeyFile: "NetSparkle_Ed25519.pub");

    _sparkleUpdater = new SparkleUpdater(appCastUrl, signatureVerifier);
    _sparkleUpdater.LogWriter = new LogWriter(LogWriterOutputMode.Console);
    _sparkleUpdater.UpdateDetected += (sender, eventArgs) => UpdateDetected?.Invoke(sender, eventArgs);
    _sparkleUpdater.DownloadFinished += (appCastItem, filePath) => DownloadFinished?.Invoke(null, new DownloadFinishedEventArgs(appCastItem, filePath));
    _sparkleUpdater.InstallUpdateFailed += TriggerUpdateFailed;
  }

  public void CheckForUpdates()
  {
    _sparkleUpdater.CheckForUpdatesQuietly();
  }

  private bool TriggerUpdateFailed(InstallUpdateFailureReason e, string? s)
  {
    UpdateFailed.Invoke(null, e);
    return true; // Investigate functionality
  }

  public void StopUpdater()
  {
    _sparkleUpdater.StopLoop();
    _sparkleUpdater.Dispose();
  }

  public void DownloadLatest(AppCastItem updateDetails)
  {
    _sparkleUpdater.InitAndBeginDownload(updateDetails);
  }
}

public class DownloadFinishedEventArgs(AppCastItem appCastItem, string filePath)
{
  public AppCastItem AppCastItem { get; set; } = appCastItem;
  public string FilePath { get; set; } = filePath;
}