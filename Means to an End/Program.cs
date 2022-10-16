using System.Net.Sockets;

const int DefaultPort = 19112;

int port = args.Length > 0 && int.TryParse(args[0], out port) ? port : DefaultPort;
var listener = TcpListener.Create(port);
listener.Start();
Console.WriteLine($"Listening on port {port}");

while (true)
{
    try
    {
        var client = await listener.AcceptTcpClientAsync();
        _ = Task.Run(() => HandleConnection(client));
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
    }
}

static void HandleConnection(TcpClient client)
{
    Console.WriteLine("Client connected");

    try
    {
        using NetworkStream stream = client.GetStream();
        using BinaryReader reader = new(stream);
        using BinaryWriter writer = new(stream);

        var data = new List<(int timestamp, int price)>();

        while (client.Connected)
        {
            try
            {
                var operation = reader.ReadChar();
                var first = HexToInt(reader.ReadBytes(4));
                var second = HexToInt(reader.ReadBytes(4));
                Console.WriteLine($"{operation} {first} {second}");

                switch (operation)
                {
                    case 'I':
                        data.Add((first, second));
                        break;
                    case 'Q':
                        var result = HandleQuery(data, first, second);
                        var response = IntToHex(result);
                        writer.Write(response);
                        Console.WriteLine(result);
                        break;
                }
            }
            catch (EndOfStreamException)
            {
                break;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
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
        client.Close();
    }

    Console.WriteLine("Client disconnected");
}

static int HexToInt(byte[] number)
{
    Array.Reverse(number); // reverse endianness
    return BitConverter.ToInt32(number);
}

static byte[] IntToHex(int number)
{
    var result = BitConverter.GetBytes(number);
    Array.Reverse(result); // reverse endianness
    return result;
}

static int HandleQuery(IList<(int timestamp, int price)> data, int minTime, int maxTime)
{
    var entries = data.Where(entry => minTime <= entry.timestamp && entry.timestamp <= maxTime);
    var prices = entries.Select(entry => entry.price);

    return prices.Any() ? Convert.ToInt32(Math.Round(prices.Average())) : 0;
}