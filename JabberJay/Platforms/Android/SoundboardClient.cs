using System.IO;
using System.Net.Sockets;
using System.Text;

public class SoundboardClient
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public async Task<bool> ConnectAsync(string serverIpAddress, int port = 5000)
    {
        try
        {
            if (_client?.Connected == true)
            {
                await UpdateSoundListAsync();
                return true;
            }

            Disconnect(); // Reset any existing dead connection state

            _client = new TcpClient();
            await _client.ConnectAsync(serverIpAddress, port);

            if (_client.Connected)
            {
                _stream = _client.GetStream();
                _reader = new StreamReader(_stream, Encoding.UTF8);
                _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
                
                Console.WriteLine("Connected to server.");

                await UpdateSoundListAsync();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection error: {ex.Message}");
            Disconnect();
            return false;
        }
    }

    public async Task<List<string>?> UpdateSoundListAsync()
    {
        if (_writer == null || _reader == null)
        {
            Console.WriteLine("Not connected to server to update list.");
            return null;
        }

        try
        {
            // Send request command as a single line
            await _writer.WriteLineAsync("GET_SOUND_LIST");

            // ReadLineAsync automatically waits until the entire line (\n) arrives across all TCP packets
            string? response = await _reader.ReadLineAsync();
            if (!string.IsNullOrEmpty(response))
            {
                var sounds = response.Split('|').ToList();
                Console.WriteLine($"Received {sounds.Count} sounds from server.");
                return sounds;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating sound list: {ex.Message}");
            Disconnect();
        }
        return null;
    }

    public async Task SendCommandAsync(string command)
    {
        if (_writer == null)
        {
            Console.WriteLine("Not connected to server.");
            return;
        }

        try
        {
            await _writer.WriteLineAsync(command);
            Console.WriteLine($"Sent command: {command}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending command: {ex.Message}");
            Disconnect();
        }
    }

    public void Disconnect()
    {
        _reader?.Dispose();
        _reader = null;
        _writer?.Dispose();
        _writer = null;
        _stream?.Dispose();
        _stream = null;
        _client?.Close();
        _client = null;
        Console.WriteLine("Disconnected from server.");
    }
}