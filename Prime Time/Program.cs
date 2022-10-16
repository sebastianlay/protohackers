using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

const int DefaultPort = 19111;
const string Method = "isPrime";

int port = args.Length > 0 && int.TryParse(args[0], out port) ? port : DefaultPort;
var listener = TcpListener.Create(port);
listener.Start();
Console.WriteLine($"Listening on port {port}");

while (true)
{
    try
    {
        var client = await listener.AcceptTcpClientAsync();
        HandleConnectionAsync(client);
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
        using StreamReader reader = new(stream);
        using StreamWriter writer = new(stream);

        while (client.Connected)
        {
            var line = await reader.ReadLineAsync();
            if (line == null)
                break;

            try
            {
                Console.WriteLine(line);
                var request = JsonSerializer.Deserialize<Request>(line);
                var result = ValidateRequest(request) && IsPrime(request?.Number);
                var response = new Response() { Method = Method, Prime = result };

                await JsonSerializer.SerializeAsync(stream, response);
                await writer.WriteAsync('\n');
                await writer.FlushAsync();
                Console.WriteLine(response?.Prime.ToString());
            }
            catch (Exception e)
            {
                await writer.WriteAsync("Invalid request");
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

static bool IsPrime(double? number)
{
    if (number == null || (number % 1) != 0 || number <= 1)
        return false;

    if (number == 2)
        return true;

    if (number % 2 == 0)
        return false;

    var boundary = Math.Floor(Math.Sqrt(Convert.ToDouble(number)));
    for (int i = 3; i <= boundary; i += 2)
        if (number % i == 0)
            return false;

    return true;
}

static bool ValidateRequest(Request? request)
{
    if (request?.Method == null || request.Method != Method || request.Number == null)
        throw new FormatException();

    return true;
}

public class Request
{
    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("number")]
    public double? Number { get; set; }
}

public class Response
{
    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("prime")]
    public bool Prime { get; set; }
}