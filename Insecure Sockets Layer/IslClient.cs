using System.Net.Sockets;
using System.Text;

namespace InsecureSocketsLayer
{
    /// <summary>
    /// Server for the "Insecure Sockets Layer" protocol
    /// </summary>
    internal static class IslClient
    {
        /// <summary>
        /// Handles the connection of a new client
        /// </summary>
        /// <param name="client">the connection the client connected on</param>
        internal static async Task HandleConnection(TcpClient client)
        {
            using var stream = client.GetStream();

            var clientStreamPosition = 0;
            var serverStreamPosition = 0;

            var hasCipherSpec = false;
            var cipherSpecLength = 0;

            List<Operation> decodingOperations = new();
            List<Operation> encodingOperations = new();

            var overhang = string.Empty;

            // read incoming messages in an infinite loop
            while (client.Connected)
            {
                try
                {
                    var readBuffer = new byte[10000];
                    var messageBuffer = new byte[10000];

                    var messageStart = 0;
                    var messageLength = await stream.ReadAsync(readBuffer);

                    if (messageLength <= 0)
                        continue;

                    // the first message contains the cipher spec, so we have to handle that here
                    if (!hasCipherSpec)
                    {
                        Console.WriteLine("Reading cipherSpec");
                        (cipherSpecLength, encodingOperations) = ReadCipherSpec(readBuffer);
                        decodingOperations = encodingOperations.Reverse<Operation>().ToList();
                        hasCipherSpec = true;

                        // close the connection immediately if the cipherSpec results in a no-op
                        if (!IsCipherSpecValid(encodingOperations))
                        {
                            Console.WriteLine("Invalid cipherSpec. Disconnecting.");
                            stream.Close();
                            client.Close();
                            break;
                        }

                        messageStart = cipherSpecLength;
                        messageLength -= cipherSpecLength;
                    }

                    // copy the actual message into its own buffer for easier handling
                    Array.Resize(ref messageBuffer, messageLength);
                    Array.Copy(readBuffer, messageStart, messageBuffer, 0, messageLength);

                    // decode the request using the cipherSpec in reverse
                    var message = DecodeMessage(messageBuffer, decodingOperations, clientStreamPosition);
                    clientStreamPosition += messageLength;

                    Console.WriteLine($"<-- {message.Replace("\n", "\\n")}");

                    // calculate the necessary responses
                    (var results, overhang) = CalculateResults(message, overhang);
                    foreach (var result in results)
                    {
                        Console.WriteLine($"--> {result.Replace("\n", "\\n")}");

                        // encode the reponse and send it to the client
                        var response = EncodeMessage(result, encodingOperations, serverStreamPosition);
                        await stream.WriteAsync(response);

                        serverStreamPosition += response.Length;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        /// <summary>
        /// Applies the given cipher spec operations on the message and returns the result
        /// </summary>
        /// <param name="message">the message that should be encoded</param>
        /// <param name="cipherSpec">the operations in the order they should be applied</param>
        /// <param name="streamPosition">the current server stream position</param>
        /// <returns>the encoded message</returns>
        private static byte[] EncodeMessage(string message, IEnumerable<Operation> cipherSpec, int streamPosition)
        {
            byte[] result = Encoding.ASCII.GetBytes(message);

            foreach (var operation in cipherSpec)
                result = operation.Apply(result, streamPosition, false);

            return result;
        }

        /// <summary>
        /// Applies the given cipher spec operations on the message and returns the result
        /// </summary>
        /// <param name="message">the message that should be decoded</param>
        /// <param name="cipherSpec">the operations in the order they should be applied</param>
        /// <param name="streamPosition">the current client stream position</param>
        /// <returns>the decoded message</returns>
        private static string DecodeMessage(byte[] message, IEnumerable<Operation> cipherSpec, int streamPosition)
        {
            byte[] result = message;

            foreach (var operation in cipherSpec)
                result = operation.Apply(result, streamPosition, true);

            return Encoding.ASCII.GetString(result);
        }

        /// <summary>
        /// Reads the cipher spec from a given byte array and returns the list of operations
        /// </summary>
        /// <param name="message">the byte array containing the cipher spec</param>
        /// <returns>the list of operations corresponding to the cipher spec</returns>
        private static (int, List<Operation>) ReadCipherSpec(Memory<byte> message)
        {
            var index = 0;
            var result = new List<Operation>();
            var hasMoreValues = true;

            while (hasMoreValues)
            {
                switch (message.Span[index])
                {
                    case 1:
                        result.Add(new ReverseBitsOp());
                        index++;
                        break;
                    case 2:
                        result.Add(new XorOp { Parameter = message.Span[index + 1] });
                        index += 2;
                        break;
                    case 3:
                        result.Add(new XorPosOp());
                        index++;
                        break;
                    case 4:
                        result.Add(new AddOp { Parameter = message.Span[index + 1] });
                        index += 2;
                        break;
                    case 5:
                        result.Add(new AddPosOp());
                        index++;
                        break;
                    default:
                        hasMoreValues = false;
                        index++;
                        break;
                }
            }

            return (index, result);
        }

        /// <summary>
        /// Runs the application layer logic and returns the toy with the highest count
        /// for each line (denoted by a new line character) in the given request
        /// </summary>
        /// <param name="request">the request (potentially containing partial lines)</param>
        /// <param name="overhang">the beginning of a new line from the previous request</param>
        /// <returns>a list of complete lines and the overhang</returns>
        private static (IEnumerable<string>, string) CalculateResults(string request, string overhang)
        {
            var result = new List<string>();
            var lines = request.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // append the overhang of the previous request
            if (request.StartsWith('\n') || lines.Length == 0)
                lines = lines.Prepend(overhang).ToArray();
            else
                lines[0] = overhang + lines[0];

            // calculate the overhang of the current request
            if (lines.Length > 0 && !request.EndsWith('\n'))
            {
                overhang = lines.Last();
                lines = lines.SkipLast(1).ToArray();
            }
            else
            {
                overhang = string.Empty;
            }

            // calculate the correct response for each line in the request
            foreach (var line in lines)
            {
                var toys = line.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var mostNeededToy = toys.OrderByDescending(GetToyCount).First();
                result.Add(mostNeededToy + '\n');
            }

            return (result, overhang);
        }

        /// <summary>
        /// Returns the count for any given toy (e.g. 15 for "15x dog on a string")
        /// </summary>
        /// <param name="value">the given toy</param>
        /// <returns>the count parsed as a number</returns>
        private static int GetToyCount(string value)
        {
            // simply ignore malformatted toys
            _ = int.TryParse(value.Split('x')[0], out var result);
            return result;
        }

        /// <summary>
        /// Checks whether applying the given operations results in a no-op
        /// </summary>
        /// <param name="operations">the list if given decoding operations</param>
        /// <returns>false if operations are resulting in no-op</returns>
        private static bool IsCipherSpecValid(List<Operation> operations)
        {
            // this check does not guarantee the correct result
            byte[] original = Enumerable.Range(0, 256).Select(v => (byte)v).ToArray();
            byte[] testMessage = Enumerable.Range(0, 256).Select(v => (byte)v).ToArray();

            // but the high probability
            foreach (var operation in operations)
                testMessage = operation.Apply(testMessage, 0, true);

            // is good enough for me
            return !original.SequenceEqual(testMessage);
        }
    }
}
