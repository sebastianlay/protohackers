using System.Net;
using System.Net.Sockets;
using System.Text;

const int DefaultPort = 19114;

int port = args.Length > 0 && int.TryParse(args[0], out port) ? port : DefaultPort;
var client = new UdpClient(port);

var database = new Dictionary<string, string> {
    { "version", "My Unusual Database Program 1.0" }
};

Console.WriteLine($"Listening on port {port}");

try
{
    while (true)
    {
        var result = await client.ReceiveAsync();
        _ = HandleMessageAsync(result.Buffer, result.RemoteEndPoint);
    }
}
catch (Exception e)
{
    Console.WriteLine(e);
}
finally
{
    client.Close();
}

async Task HandleMessageAsync(byte[] buffer, IPEndPoint endpoint)
{
    try
    {
        var message = Encoding.ASCII.GetString(buffer);
        Console.WriteLine($"< {message}");
        if (message.Contains('='))
        {
            // insert request
            var index = message.IndexOf('=');
            var key = message[..index];
            if (key == "version")
                return;
            var value = message[(index + 1)..];
            database[key] = value;
        }
        else
        {
            // retrieve request
            var value = database.GetValueOrDefault(message) ?? string.Empty;
            var response = $"{message}={value}";
            Console.WriteLine($"> {response}");
            var result = Encoding.ASCII.GetBytes(response);
            await client.SendAsync(result, endpoint);
        }
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }
}
