using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Views;

namespace JabberJay;

public partial class RenamePopup : Popup
{
  private TaskCompletionSource<string> _tcs;
  
  public RenamePopup()
  {
    InitializeComponent();
    AnimationExtensions.SetupPointerEffects(OkButton, Color.FromArgb("#00ab0b"), Color.FromArgb("#017509"));
    AnimationExtensions.SetupPointerEffects(CancelButton, Color.FromArgb("#c40000"), Color.FromArgb("#850000"));
    _tcs = new TaskCompletionSource<string>();
  }

  private void OkButton_Clicked(object sender, EventArgs e)
  {
    Close(NewTextEntry.Text);
  }

  private void CancelButton_Clicked(object sender, EventArgs e)
  {
    Close();
  }

  public Task<string> GetResultAsync() => _tcs.Task;

  protected override void OnHandlerChanging(HandlerChangingEventArgs args)
  {
    base.OnHandlerChanging(args);
    if (Handler == null) // Handler is being detached (popup is closing)
    {
      // If the TaskCompletionSource hasn't been completed by a button click,
      // we might need a default completion (e.g., with null or an empty string)
      if (!_tcs.Task.IsCompleted)
      {
        _tcs.SetResult(null); // Or string.Empty, depending on your preference
      }
    }
  }
}