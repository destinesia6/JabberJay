namespace SoundboardMAUI;

public interface IKeyboardListener
{
  event EventHandler<KeyEventArgs> KeyDown;
  void StartListening();
  void StopListening();
}

public class KeyEventArgs : EventArgs
{
  public string? KeyCode { get; set; }
  public string KeyCharacter { get; set; }
}