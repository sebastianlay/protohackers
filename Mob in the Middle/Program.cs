using System.Net.Sockets;

const int DefaultPort = 19115;

const string UpstreamHostname = "chat.protohackers.com";
const int UpstreamPort = 16963;
const string TonysAddress = "7YWHMfk9JZe0LM0g1ZauHuiSxhI";

int port = args.Length > 0 && int.TryParse(args[0], out port) ? port : DefaultPort;
var listener = TcpListener.Create(port);
listener.Start();
Console.WriteLine($"Listening on port {port}");

while (true)
{
    try
    {
        var downstreamClient = await listener.AcceptTcpClientAsync();
        var upstreamClient = new TcpClient(UpstreamHostname, UpstreamPort);
        _ = Task.Run(() => HandleConnection(downstreamClient, upstreamClient));
        _ = Task.Run(() => HandleConnection(upstreamClient, downstreamClient));
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
    }
}

async Task HandleConnection(TcpClient incomingClient, TcpClient outgoingClient)
{
    Console.WriteLine("Client connected");

    try
    {
        using NetworkStream incomingStream = incomingClient.GetStream();
        using BinaryReader incomingReader = new(incomingStream);

        using NetworkStream outgoingStream = outgoingClient.GetStream();
        using StreamWriter outgoingWriter = new(outgoingStream);

        while (incomingClient.Connected && outgoingClient.Connected)
        {
            try
            {
                // unfortunately we can't just use a StreamReader and ReadLine() as
                // there is no way to check whether the line was actually terminated
                // with a line feed character as is necessary for the last test
                var line = string.Empty;
                while (incomingStream.CanRead)
                {
                    var readChar = incomingReader.ReadChar();
                    if (readChar == '\n')
                        break;

                    line += readChar;
                }

                var rewrittenLine = RewriteLine(line);
                await outgoingWriter.WriteLineAsync(rewrittenLine);
                await outgoingWriter.FlushAsync();

                Console.WriteLine($"> {line}");
            }
            catch (Exception)
            {
                break;
            }
        }
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
    }
    finally
    {
        incomingClient.Close();
    }

    Console.WriteLine("Client disconnected");
}

string RewriteLine(string line)
{
    // https://xkcd.com/1171/
    var tokens = line.Split(' ');
    var rewrittenTokens = tokens.Select(token => IsBoguscoinAddress(token) ? TonysAddress : token);
    return string.Join(' ', rewrittenTokens);
}

bool IsBoguscoinAddress(string token)
{
    return token.StartsWith('7') && token.Length >= 26 && token.Length <= 35 && token.All(char.IsLetterOrDigit);
}
