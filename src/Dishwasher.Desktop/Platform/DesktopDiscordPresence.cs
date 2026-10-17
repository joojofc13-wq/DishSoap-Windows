using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Dishwasher
{
    // Local Discord IPC only. The application ID is public; no user tokens or OAuth are used.
    internal sealed class DesktopDiscordPresence : IDisposable
    {
        internal const string ApplicationId = "1560847384727134278";
        internal const string GameName = "The Dishwasher: Dead Samurai";
        internal const string ImageUrl = "https://raw.githubusercontent.com/joojofc13-wq/DishSoap-Windows/v0.2.0/src/Dishwasher.Desktop/Assets/DiscordPresence.png";
        private readonly CancellationTokenSource _stop = new();
        private readonly SemaphoreSlim _writer = new(1, 1);
        private readonly Func<CancellationToken, Task<Stream>> _connect;
        private readonly TimeSpan _interval;
        private readonly TimeSpan _retryDelay;
        private readonly long _started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        private readonly Task _worker;
        private string _state = "In menus";
        private int _disposed;
        private int _accepted;
        internal bool ActivityAccepted => Volatile.Read(ref _accepted) != 0;
        private string _acceptedName, _acceptedImage;
        internal string AcceptedName => Volatile.Read(ref _acceptedName);
        internal string AcceptedImage => Volatile.Read(ref _acceptedImage);

        internal DesktopDiscordPresence(bool enabled,
            Func<CancellationToken, Task<Stream>> connect = null, TimeSpan? interval = null, TimeSpan? retryDelay = null)
        {
            _connect = connect ?? ConnectAsync;
            _interval = interval ?? TimeSpan.FromSeconds(5);
            _retryDelay = retryDelay ?? TimeSpan.FromSeconds(10);
            _worker = enabled ? Task.Run(RunAsync) : Task.CompletedTask;
        }

        internal void SetState(bool playing, bool paused) =>
            Volatile.Write(ref _state, playing ? (paused ? "Paused" : "In game") : "In menus");

        private static async Task<Stream> ConnectAsync(CancellationToken token)
        {
            for (int index = 0; index < 10; index++)
            {
                var pipe = new NamedPipeClientStream(".", "discord-ipc-" + index,
                    PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    await pipe.ConnectAsync(100, token).ConfigureAwait(false);
                    return pipe;
                }
                catch (Exception ex) when (ex is IOException || ex is TimeoutException || ex is UnauthorizedAccessException)
                { pipe.Dispose(); }
                catch { pipe.Dispose(); throw; }
            }
            throw new IOException("Discord desktop IPC is unavailable.");
        }

        private async Task RunAsync()
        {
            bool reportedUnavailable = false;
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    using Stream pipe = await _connect(_stop.Token).ConfigureAwait(false);
                    using var connection = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    Task reader = null;
                    bool ready = false;
                    try
                    {
                        await WriteAsync(pipe, 0, JsonSerializer.SerializeToUtf8Bytes(new { v = 1, client_id = ApplicationId }), connection.Token).ConfigureAwait(false);
                        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(connection.Token))
                        {
                            deadline.CancelAfter(TimeSpan.FromSeconds(5));
                            await ReadAsync(pipe, true, deadline.Token).ConfigureAwait(false);
                        }
                        ready = true; reportedUnavailable = false; Volatile.Write(ref _accepted, 0);
                        Log.Info("[discord] Rich Presence connected.");
                        string sentState = Volatile.Read(ref _state);
                        await SendActivityAsync(pipe, sentState, false, connection.Token).ConfigureAwait(false);
                        reader = ReadAsync(pipe, false, connection.Token);
                        while (!connection.IsCancellationRequested)
                        {
                            Task tick = Task.Delay(_interval, connection.Token);
                            if (await Task.WhenAny(reader, tick).ConfigureAwait(false) == reader)
                            { await reader.ConfigureAwait(false); break; }
                            await tick.ConfigureAwait(false);
                            string state = Volatile.Read(ref _state);
                            if (state != sentState)
                            {
                                await SendActivityAsync(pipe, state, false, connection.Token).ConfigureAwait(false);
                                sentState = state;
                            }
                        }
                    }
                    finally
                    {
                        connection.Cancel();
                        if (reader != null) { try { await reader.ConfigureAwait(false); } catch { } }
                        if (ready && _stop.IsCancellationRequested)
                        {
                            using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
                            try { await SendActivityAsync(pipe, null, true, deadline.Token).ConfigureAwait(false); }
                            catch { /* Discord also clears presence when this pipe/process closes. */ }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_stop.IsCancellationRequested) break;
                    if (!reportedUnavailable)
                    { Log.Info("[discord] Presence unavailable; retrying in background (" + ex.GetType().Name + ")."); reportedUnavailable = true; }
                }
                try { await Task.Delay(_retryDelay, _stop.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        private Task SendActivityAsync(Stream pipe, string state, bool clear, CancellationToken token)
        {
            object activity = clear ? null : new
            {
                name = GameName, type = 0, details = "Playing " + GameName, state,
                timestamps = new { start = _started },
                assets = new { large_image = ImageUrl, large_text = GameName }, instance = false
            };
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(new
            {
                cmd = "SET_ACTIVITY", args = new { pid = Environment.ProcessId, activity },
                nonce = Guid.NewGuid().ToString("N")
            });
            return WriteAsync(pipe, 1, data, token);
        }

        private async Task ReadAsync(Stream pipe, bool waitForReady, CancellationToken token)
        {
            var header = new byte[8];
            while (true)
            {
                await pipe.ReadExactlyAsync(header, token).ConfigureAwait(false);
                int opcode = BinaryPrimitives.ReadInt32LittleEndian(header);
                int length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
                if (length < 0 || length > 65536) throw new InvalidDataException("Invalid Discord IPC frame length.");
                var data = new byte[length];
                await pipe.ReadExactlyAsync(data, token).ConfigureAwait(false);
                if (opcode == 2) throw new IOException("Discord IPC closed.");
                if (opcode == 3) { await WriteAsync(pipe, 4, data, token).ConfigureAwait(false); continue; }
                if (opcode == 4) continue;
                if (opcode != 1) throw new InvalidDataException("Unexpected Discord IPC opcode.");
                using var message = JsonDocument.Parse(data);
                if (message.RootElement.TryGetProperty("cmd", out var command) && command.GetString() == "SET_ACTIVITY" &&
                    (!message.RootElement.TryGetProperty("evt", out var status) || status.ValueKind == JsonValueKind.Null))
                {
                    if (message.RootElement.TryGetProperty("data", out var activity) && activity.ValueKind == JsonValueKind.Object)
                    {
                        if (activity.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                            Volatile.Write(ref _acceptedName, name.GetString());
                        if (activity.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Object &&
                            assets.TryGetProperty("large_image", out var image) && image.ValueKind == JsonValueKind.String)
                            Volatile.Write(ref _acceptedImage, image.GetString());
                    }
                    if (Interlocked.Exchange(ref _accepted, 1) == 0) Log.Info("[discord] Rich Presence activity accepted.");
                }
                if (!message.RootElement.TryGetProperty("evt", out var evt)) continue;
                string kind = evt.GetString();
                if (kind == "ERROR") throw new IOException("Discord rejected the Rich Presence request.");
                if (waitForReady && kind == "READY") return;
            }
        }

        private async Task WriteAsync(Stream pipe, int opcode, byte[] data, CancellationToken token)
        {
            var header = new byte[8];
            BinaryPrimitives.WriteInt32LittleEndian(header, opcode);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), data.Length);
            await _writer.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await pipe.WriteAsync(header, token).ConfigureAwait(false);
                await pipe.WriteAsync(data, token).ConfigureAwait(false);
                await pipe.FlushAsync(token).ConfigureAwait(false);
            }
            finally { _writer.Release(); }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _stop.Cancel();
            // Bounded exit only; all connection/read/retry work stays off the render thread.
            try { _worker.Wait(TimeSpan.FromMilliseconds(300)); } catch { }
            _ = _worker.ContinueWith(_ => { _stop.Dispose(); _writer.Dispose(); }, TaskScheduler.Default);
        }
    }
}
