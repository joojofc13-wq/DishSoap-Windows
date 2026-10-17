using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using Dishwasher;

internal static class DiscordPresenceChecks
{
    internal static async Task Run()
    {
        static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL: Discord " + name);
            Console.WriteLine("PASS: Discord " + name);
        }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        string name = "dishsoap-test-" + Guid.NewGuid().ToString("N");
        int attempts = 0;
        async Task<Stream> Connect(CancellationToken token)
        {
            Interlocked.Increment(ref attempts);
            var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            try { await pipe.ConnectAsync(1000, token); return pipe; }
            catch { pipe.Dispose(); throw; }
        }
        using (var disabled = new DesktopDiscordPresence(false, Connect))
        { disabled.SetState(true, false); await Task.Delay(50); Check(attempts == 0, "disabled option never opens IPC"); }
        var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        try
        {
            using var presence = new DesktopDiscordPresence(true, Connect, TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(30));
            await server.WaitForConnectionAsync(deadline.Token);
            var hello = await Read(server, deadline.Token);
            using (var json = JsonDocument.Parse(hello.Data))
                Check(hello.Opcode == 0 && json.RootElement.GetProperty("client_id").GetString() == DesktopDiscordPresence.ApplicationId,
                    "handshake uses the registered public application ID");
            await Write(server, 1, "{\"evt\":\"READY\",\"data\":{}}"u8.ToArray(), deadline.Token, fragment: true);
            var initial = await Read(server, deadline.Token);
            using var activity = JsonDocument.Parse(initial.Data);
            var args = activity.RootElement.GetProperty("args");
            var data = args.GetProperty("activity");
            long started = data.GetProperty("timestamps").GetProperty("start").GetInt64();
            Check(args.GetProperty("pid").GetInt32() == Environment.ProcessId &&
                data.GetProperty("name").GetString() == "The Dishwasher: Dead Samurai" &&
                data.GetProperty("assets").GetProperty("large_image").GetString() == DesktopDiscordPresence.ImageUrl &&
                data.GetProperty("state").GetString() == "In menus", "fragmented READY publishes game name, icon, PID and menu state");
            await Write(server, 1, "{\"cmd\":\"SET_ACTIVITY\",\"evt\":null,\"data\":{}}"u8.ToArray(), deadline.Token);
            while (!presence.ActivityAccepted) await Task.Delay(10, deadline.Token);
            Check(presence.ActivityAccepted, "activity acknowledgement is recognized");
            byte[] ping = { 1, 3, 5, 7 };
            await Write(server, 3, ping, deadline.Token);
            var pong = await Read(server, deadline.Token);
            Check(pong.Opcode == 4 && pong.Data.SequenceEqual(ping), "PING payload is echoed as PONG");
            foreach (var state in new[] { (true, false, "In game"), (true, true, "Paused"), (false, false, "In menus") })
            {
                presence.SetState(state.Item1, state.Item2);
                var update = await Read(server, deadline.Token);
                using var json = JsonDocument.Parse(update.Data);
                var current = json.RootElement.GetProperty("args").GetProperty("activity");
                Check(current.GetProperty("state").GetString() == state.Item3 &&
                    current.GetProperty("timestamps").GetProperty("start").GetInt64() == started,
                    state.Item3 + " updates preserve the session timestamp");
            }
            // Invalid remote data must disconnect/retry without escaping into gameplay.
            var invalid = new byte[8]; BinaryPrimitives.WriteInt32LittleEndian(invalid, 1);
            BinaryPrimitives.WriteInt32LittleEndian(invalid.AsSpan(4), 65537);
            await server.WriteAsync(invalid, deadline.Token); await server.FlushAsync(deadline.Token);
            server.Dispose();
            server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync(deadline.Token);
            await Read(server, deadline.Token);
            await Write(server, 1, "{\"evt\":\"READY\",\"data\":{}}"u8.ToArray(), deadline.Token);
            var reconnected = await Read(server, deadline.Token);
            using (var json = JsonDocument.Parse(reconnected.Data))
                Check(attempts >= 2 && json.RootElement.GetProperty("args").GetProperty("activity")
                    .GetProperty("timestamps").GetProperty("start").GetInt64() == started, "malformed frame/reconnection preserves the session");
            presence.Dispose();
            try
            {
                var cleared = await Read(server, deadline.Token);
                using var json = JsonDocument.Parse(cleared.Data);
                Check(json.RootElement.GetProperty("args").GetProperty("activity").ValueKind == JsonValueKind.Null,
                    "shutdown clears the activity");
            }
            catch (EndOfStreamException) { Check(true, "shutdown disconnects the activity pipe"); }
        }
        finally { server.Dispose(); }
        int offlineAttempts = 0;
        using (var offline = new DesktopDiscordPresence(true, _ => {
            Interlocked.Increment(ref offlineAttempts); return Task.FromException<Stream>(new IOException("test offline"));
        }, retryDelay: TimeSpan.FromMilliseconds(20)))
        {
            await Task.Delay(100);
            Check(offlineAttempts >= 2, "Discord unavailable retries without blocking the caller");
        }
    }

    private static async Task<(int Opcode, byte[] Data)> Read(Stream stream, CancellationToken token)
    {
        var header = new byte[8]; await stream.ReadExactlyAsync(header, token);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        if (length < 0 || length > 65536) throw new InvalidDataException("Invalid test frame.");
        var data = new byte[length]; await stream.ReadExactlyAsync(data, token);
        return (BinaryPrimitives.ReadInt32LittleEndian(header), data);
    }
    private static async Task Write(Stream stream, int opcode, byte[] data, CancellationToken token, bool fragment = false)
    {
        var header = new byte[8]; BinaryPrimitives.WriteInt32LittleEndian(header, opcode);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), data.Length);
        if (fragment) { await stream.WriteAsync(header.AsMemory(0, 3), token); await stream.WriteAsync(header.AsMemory(3), token); }
        else await stream.WriteAsync(header, token);
        await stream.WriteAsync(data, token); await stream.FlushAsync(token);
    }
}
