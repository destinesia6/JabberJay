using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class SoundboardServer
{
    private TcpListener? _listener;
    private CancellationTokenSource _cancellationTokenSource;
    public Action<string>? PlaySoundAction;
    public Action? StopSoundAction;
    private readonly Func<List<string>> _getSoundFilesAction;

    public SoundboardServer(Func<List<string>> getSoundFilesAction)
    {
        _getSoundFilesAction = getSoundFilesAction;
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task StartAsync(int port = 5000)
    {
        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Console.WriteLine($"Server started. Listening on port {port}...");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_cancellationTokenSource.Token);
                Console.WriteLine("Client connected.");
                
                // Process client connection without blocking new connections
                _ = HandleClientAsync(client);
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

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            while (client.Connected && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                string? receivedCommand = await reader.ReadLineAsync(_cancellationTokenSource.Token);
                if (receivedCommand == null)
                {
                    Console.WriteLine("Client disconnected.");
                    break;
                }

                receivedCommand = receivedCommand.Trim();
                Console.WriteLine($"Received command: {receivedCommand}");

                if (receivedCommand == "GET_SOUND_LIST")
                {
                    var soundFiles = _getSoundFilesAction.Invoke();
                    var fileListString = string.Join("|", soundFiles);
                    
                    // Send entire list as a single line terminated by a newline
                    await writer.WriteLineAsync(fileListString);
                    Console.WriteLine($"Sent {soundFiles.Count} sound files to client.");
                }
                else if (receivedCommand == "STOP")
                {
                    StopSoundAction?.Invoke();
                }
                else
                {
                    if (File.Exists(receivedCommand)) 
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

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
    }

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