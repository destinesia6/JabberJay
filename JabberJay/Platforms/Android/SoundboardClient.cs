using System.Net.Sockets;
using System.Text;

// This service is intended to be run in your Android MAUI app.
// It connects to the Windows server and sends commands over the network.
public class SoundboardClient
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    
    // Connects to the server.
    // The serverIpAddress must be the IP address of the PC running the Windows app.
    public async Task<bool> ConnectAsync(string serverIpAddress, int port = 5000)
    {
        try
        {
            if (_client?.Connected == true)
            {
                // Already connected.
                await UpdateSoundListAsync();
                return true;
            }
            
            _client = new TcpClient();
            await _client.ConnectAsync(serverIpAddress, port);
            
            if (_client.Connected)
            {
                _stream = _client.GetStream();
                Console.WriteLine("Connected to server.");
                
                // Call the new function to get the initial list of sounds.
                await UpdateSoundListAsync();

                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection error: {ex.Message}");
            _client?.Close();
            _client = null;
            return false;
        }
    }
    
    // NEW: Function to explicitly request and update the list of sound files from the server.
    public async Task<List<string>?> UpdateSoundListAsync()
    {
        if (_stream == null)
        {
            Console.WriteLine("Not connected to server to update list.");
            return null;
        }

        try
        {
            // Send a specific command to the server requesting the list of files.
            await _stream.WriteAsync("GET_SOUND_LIST"u8.ToArray());

            // Read the response from the server.
            byte[] buffer = new byte[8192];
            int bytesRead = await _stream.ReadAsync(buffer);
            if (bytesRead > 0)
            {
                return Encoding.UTF8.GetString(buffer, 0, bytesRead).Split('|').ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating sound list: {ex.Message}");
            Disconnect();
        }
        return null;
    }
    
    // Sends a command (which is now the full file path) to the server.
    public async Task SendCommandAsync(string command)
    {
        if (_stream == null)
        {
            Console.WriteLine("Not connected to server.");
            return;
        }

        try
        {
            // Encode the command into a byte array.
            byte[] data = Encoding.UTF8.GetBytes(command);
            
            // Send the data over the network stream.
            await _stream.WriteAsync(data);
            Console.WriteLine($"Sent command: {command}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending command: {ex.Message}");
            Disconnect();
        }
    }
    
    // Disconnects from the server.
    public void Disconnect()
    {
        _stream?.Dispose();
        _stream = null;
        _client?.Close();
        _client = null;
        Console.WriteLine("Disconnected from server.");
    }
}
