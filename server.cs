

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

var server = new TcpListener(IPAddress.Any, 4000);
var clients = new ConcurrentDictionary<int, StreamWriter>();
var sendLock = new SemaphoreSlim(1, 1);

int nextId = 0;

server.Start();
Console.WriteLine("Сервер запущен!");

while (true)
{
    var client = await server.AcceptTcpClientAsync();
    _ = HandleClientAsync(client, Interlocked.Increment(ref nextId));
}

async Task HandleClientAsync(TcpClient client, int id)
{
    using var connection = client;
    using var stream = client.GetStream();
    using var reader = new StreamReader(stream, Encoding.UTF8);
    using var writer = new StreamWriter(stream) { AutoFlush = true };

    try
    {
        string? name = await reader.ReadLineAsync();

        if (string.IsNullOrWhiteSpace(name))
            return;

        clients[id] = writer;
        Console.WriteLine($"{name} подключился!");

        while (await reader.ReadLineAsync() is { } message)
        {
            if (string.IsNullOrWhiteSpace(message))
                continue;

            string text = $"{name}: {message}";

            Console.WriteLine(text);

            await BroadcastAsync(text);
        }
    }
    catch (IOException) { }
    catch (SocketException) { }
    finally
    {
        clients.TryRemove(id, out _);
    }
}

async Task BroadcastAsync(string message)
{
    await sendLock.WaitAsync();

    try
    {
        foreach (var writer in clients.Values)
        {
            try
            {
                await writer.WriteLineAsync(message);
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        }
    }
    finally
    {
        sendLock.Release();
    }
}
