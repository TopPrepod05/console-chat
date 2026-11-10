

using System.Net.Sockets;
using System.Text;

Console.Write("Введите никнейм: ");
string name = Console.ReadLine()?.Trim() ?? "";

if (string.IsNullOrWhiteSpace(name))
    return;

using var client = new TcpClient();

await client.ConnectAsync("127.0.0.1", 4000);

using var stream = client.GetStream();
using var reader = new StreamReader(stream, Encoding.UTF8);
using var writer = new StreamWriter(stream) { AutoFlush = true };

await writer.WriteLineAsync(name);

var messages = new List<string>();
var input = new StringBuilder();
var consoleLock = new object();

lock (consoleLock)
    Draw();

_ = ReceiveMessagesAsync();

while (true)
{
    var key = Console.ReadKey(true);

    if (key.Key == ConsoleKey.Escape)
        break;

    if (key.Key == ConsoleKey.Enter)
    {
        string message;

        lock (consoleLock)
        {
            message = input.ToString();
            input.Clear();

            if (message == "/clear")
            {
                messages.Clear();
                Draw();
                continue;
            }

            Draw();
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            try
            {
                await writer.WriteLineAsync(message);
            }
            catch (IOException)
            {
                break;
            }
        }
    }
    else if (key.Key == ConsoleKey.Backspace)
    {
        lock (consoleLock)
        {
            if (input.Length > 0)
                input.Length--;

            Draw();
        }
    }
    else if (!char.IsControl(key.KeyChar))
    {
        lock (consoleLock)
        {
            input.Append(key.KeyChar);
            Draw();
        }
    }
}

async Task ReceiveMessagesAsync()
{
    try
    {
        while (await reader.ReadLineAsync() is { } message)
        {
            lock (consoleLock)
            {
                messages.Add(message);
                Draw();
            }
        }
    }
    catch (IOException) { }
    catch (ObjectDisposedException) { }
}

void Draw()
{
    int width = Math.Max(1, Console.WindowWidth - 1);
    int height = Console.WindowHeight;

    var lines = new List<string>();

    foreach (string message in messages)
    {
        for (int i = 0; i < message.Length; i += width)
        {
            lines.Add(message.Substring(
                i, Math.Min(width, message.Length - i)));
        }
    }

    var visible = lines.TakeLast(height - 1).ToArray();

    Console.Clear();

    int start = height - visible.Length - 1;

    for (int i = 0; i < visible.Length; i++)
    {
        Console.SetCursorPosition(0, start + i);
        Console.Write(visible[i]);
    }

    Console.SetCursorPosition(0, height - 1);

    string prompt = "Введите сообщение: ";
    int available = Math.Max(0, width - prompt.Length);

    string typed = input.ToString();

    if (typed.Length > available)
        typed = typed[^available..];

    Console.Write(prompt[..Math.Min(prompt.Length, width)] + typed);
}
