using System.Net;
using System.Net.Sockets;
using System.Text;

public class SoundboardServer
{
    private TcpListener? _listener;
    private CancellationTokenSource _cancellationTokenSource;
    public Action<string> PlaySoundAction;
    public Action StopSoundAction;
    private readonly Func<List<string>> _getSoundFilesAction;

    public SoundboardServer(Func<List<string>> getSoundFilesAction)
    {
        // This function provides the list of available sound files to send to the client.
        _getSoundFilesAction = getSoundFilesAction;
        _cancellationTokenSource = new CancellationTokenSource();
    }

    // Starts the TCP server, listening for client connections.
    public async Task StartAsync(int port = 5000)
    {
        try
        {
	          _cancellationTokenSource = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Console.WriteLine($"Server started. Listening on port {port}...");

            //StartDiscoveryBroadcasting();

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_cancellationTokenSource.Token);
                Console.WriteLine("Client connected.");
                
                await HandleClientAsync(client);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server stopping due to cancellation request.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server error: {ex.Message}");
        }
        finally
        {
            _listener?.Stop();
        }
    }

    // Handles the communication with a single connected client.
    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            await using var stream = client.GetStream();
            var buffer = new byte[1024];
            
            while (client.Connected && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                int bytesRead = await stream.ReadAsync(buffer, _cancellationTokenSource.Token);
                if (bytesRead == 0)
                {
                    Console.WriteLine("Client disconnected.");
                    break;
                }

                string receivedCommand = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                Console.WriteLine($"Received command: {receivedCommand}");

                if (receivedCommand == "GET_SOUND_LIST")
                {
                    // If the client requests the sound list, send it back.
                    var soundFiles = _getSoundFilesAction.Invoke();
                    var fileListString = string.Join("|", soundFiles);
                    var fileListBytes = Encoding.UTF8.GetBytes(fileListString);
                    await stream.WriteAsync(fileListBytes);
                    Console.WriteLine("Sent sound file list to client.");
                }
                else if (receivedCommand == "STOP")
                {
	                  StopSoundAction?.Invoke();
                }
                else
                {
                    // Otherwise, assume the command is a file path to play.
                    PlaySoundAction?.Invoke(receivedCommand);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client handling error: {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }

    // Stops the TCP server.
    public void Stop()
    {
        _cancellationTokenSource.Cancel();
    }

    // A utility method to get the local IP address for the server.
    public static string GetLocalIpAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }
}
