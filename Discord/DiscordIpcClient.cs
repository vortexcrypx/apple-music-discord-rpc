using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AppleMusicDiscordRPC.Discord;

public class DiscordIpcClient : IDisposable
{
    private const string DefaultClientId = "773825528921849856"; // Verified Apple Music Client ID
    private readonly string _clientId;
    private NamedPipeClientStream? _pipe;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private bool _isConnected;
    private readonly int _currentPid;

    public bool IsConnected => _isConnected && _pipe != null && _pipe.IsConnected;

    public DiscordIpcClient(string? clientId = null)
    {
        _clientId = string.IsNullOrWhiteSpace(clientId) ? DefaultClientId : clientId;
        _currentPid = Environment.ProcessId;
    }

    public async Task EnsureConnectedAsync()
    {
        if (IsConnected) return;

        await _writeLock.WaitAsync();
        try
        {
            if (IsConnected) return;

            ClosePipe();

            for (int i = 0; i < 10; i++)
            {
                var pipeName = $"discord-ipc-{i}";
                try
                {
                    var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(100);
                    _pipe = pipe;
                    break;
                }
                catch
                {
                    // Pipe not found or cannot connect, try next index
                }
            }

            if (_pipe == null || !_pipe.IsConnected)
            {
                _isConnected = false;
                return;
            }

            // Perform handshake (Opcode 0)
            var handshake = new DiscordHandshake { Version = 1, ClientId = _clientId };
            var handshakeJson = JsonSerializer.Serialize(handshake);
            await WritePacketAsync(0, handshakeJson);

            // Read handshake response
            var (op, response) = await ReadPacketAsync();
            if (op == 1 && response.Contains("READY"))
            {
                _isConnected = true;
                Console.WriteLine("[DiscordIPC] Successfully connected and logged in.");
            }
            else
            {
                ClosePipe();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DiscordIPC] Connection error: {ex.Message}");
            ClosePipe();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SetActivityAsync(DiscordActivity? activity)
    {
        try
        {
            await EnsureConnectedAsync();
            if (!IsConnected) return;

            var payload = new DiscordPayload<SetActivityArgs>
            {
                Command = "SET_ACTIVITY",
                Args = new SetActivityArgs
                {
                    Pid = _currentPid,
                    Activity = activity
                }
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            await _writeLock.WaitAsync();
            try
            {
                await WritePacketAsync(1, json);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DiscordIPC] SetActivity failed: {ex.Message}");
            ClosePipe();
        }
    }

    public async Task ClearActivityAsync()
    {
        await SetActivityAsync(null);
    }

    private async Task WritePacketAsync(int opcode, string json)
    {
        if (_pipe == null || !_pipe.IsConnected) return;

        var payloadBytes = Encoding.UTF8.GetBytes(json);
        var header = new byte[8];
        BitConverter.TryWriteBytes(header.AsSpan(0, 4), opcode);
        BitConverter.TryWriteBytes(header.AsSpan(4, 4), payloadBytes.Length);

        await _pipe.WriteAsync(header, 0, 8);
        await _pipe.WriteAsync(payloadBytes, 0, payloadBytes.Length);
        await _pipe.FlushAsync();
    }

    private async Task<(int Opcode, string Payload)> ReadPacketAsync()
    {
        if (_pipe == null || !_pipe.IsConnected) return (-1, string.Empty);

        var header = new byte[8];
        int read = await _pipe.ReadAsync(header, 0, 8);
        if (read < 8) return (-1, string.Empty);

        int opcode = BitConverter.ToInt32(header, 0);
        int length = BitConverter.ToInt32(header, 4);

        var buffer = new byte[length];
        int totalRead = 0;
        while (totalRead < length)
        {
            int r = await _pipe.ReadAsync(buffer, totalRead, length - totalRead);
            if (r <= 0) break;
            totalRead += r;
        }

        var json = Encoding.UTF8.GetString(buffer, 0, totalRead);
        return (opcode, json);
    }

    private void ClosePipe()
    {
        _isConnected = false;
        if (_pipe != null)
        {
            try
            {
                _pipe.Dispose();
            }
            catch { }
            _pipe = null;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        ClosePipe();
        _writeLock.Dispose();
    }
}
