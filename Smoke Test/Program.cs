using System.Net.Sockets;
using System.Text;

const int DefaultPort = 19110;

int port = args.Length > 0 && int.TryParse(args[0], out port) ? port : DefaultPort;
var listener = TcpListener.Create(port);
listener.Start();
Console.WriteLine($"Listening on port {port}");

while (true)
{
    try
    {
        var client = await listener.AcceptTcpClientAsync();
        _ = HandleConnectionAsync(client);
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
    }
}

static async Task HandleConnectionAsync(TcpClient client)
{
    Console.WriteLine("Client connected");

    try
    {
        using NetworkStream stream = client.GetStream();
        var buffer = new byte[client.ReceiveBufferSize];
        var memory = buffer.AsMemory(0, buffer.Length);

        while (client.Connected)
        {
            var readBytes = await stream.ReadAsync(memory);
            if (readBytes == 0)
                break;

            var data = buffer.AsMemory(0, readBytes);
            var text = Encoding.ASCII.GetString(data.Span);
            Console.WriteLine(text);
            await stream.WriteAsync(data);
        }
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
    }
    finally
    {
        client.Close();
    }

    Console.WriteLine("Client disconnected");
}
