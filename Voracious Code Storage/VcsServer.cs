using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Voracious_Code_Storage
{
    internal static partial class VcsServer
    {
        private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromMilliseconds(1);

        private static readonly Dictionary<string, List<string>> Files = new();

        private const string AllowedCharacters = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!\"#$%&'()*+, -./:;<=>?@[\\]^_`{|}~\n\t";

        [GeneratedRegex("^\\/([a-zA-Z0-9.\\-_]+\\/?)+$")]
        private static partial Regex ValidFileName();

        [GeneratedRegex("^\\/([a-zA-Z0-9.\\-_]+\\/?)*$")]
        private static partial Regex ValidDirectoryName();

        /// <summary>
        /// Main entry point for new TCP connections
        /// </summary>
        /// <param name="client"></param>
        public static async Task HandleConnectionAsync(TcpClient client)
        {
            using var stream = client.GetStream();
            using var streamReader = new StreamReader(stream);

            Console.WriteLine("Client connected");

            try
            {
                while (client.Connected)
                {
                    // check explicitely if the connection is still open
                    // (since for some reason there is no exception when the connection is closed from the client-side)
                    if (client.Client.Poll(ConnectionTimeout, SelectMode.SelectRead) && !stream.DataAvailable)
                        break;

                    await SendMessageAsync("READY", stream);

                    // read the incoming line
                    var line = await streamReader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    // output incoming line
                    Console.WriteLine($"<-- {line.Replace("\n", "\\n")}");

                    var splitLine = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var verb = splitLine[0];
                    var verbLower = verb.ToLower();

                    var args = Array.Empty<string>();
                    if (splitLine.Length > 1)
                        args = splitLine[1..];

                    switch (verbLower)
                    {
                        case "help":
                            await HandleHelpRequestAsync(stream);
                            break;
                        case "get":
                            await HandleGetRequestAsync(args, stream);
                            break;
                        case "put":
                            await HandlePutRequestAsync(args, stream, streamReader);
                            break;
                        case "list":
                            await HandleListRequestAsync(args, stream);
                            break;
                        default:
                            await HandleOtherRequestAsync(verb, stream);
                            break;
                    }
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

            Console.WriteLine("Client disconnected");
        }

        /// <summary>
        /// Handles the "help" request
        /// </summary>
        /// <param name="stream"></param>
        private static async Task HandleHelpRequestAsync(NetworkStream stream)
        {
            await SendMessageAsync("OK usage: HELP|GET|PUT|LIST", stream);
        }

        /// <summary>
        /// Handles the "get" request
        /// </summary>
        /// <param name="args"></param>
        /// <param name="stream"></param>
        private static async Task HandleGetRequestAsync(string[] args, NetworkStream stream)
        {
            if (args.Length == 0 || args.Length > 2)
            {
                await SendMessageAsync("ERR usage: GET file [revision]", stream);
                return;
            }

            var file = args[0];

            if (!ValidFileName().IsMatch(file))
            {
                await SendMessageAsync("ERR illegal file name", stream);
                return;
            }

            if (!Files.ContainsKey(file))
            {
                await SendMessageAsync("ERR no such file", stream);
                return;
            }

            string? result = null;

            // try to get the revision from the given argument
            if (args.Length == 2)
            {
                if (!int.TryParse(args[1][1..], out var revision) || revision <= 0)
                {
                    await SendMessageAsync("ERR no such revision", stream);
                    return;
                }

                if (Files[file].Count >= revision)
                    result = Files[file][revision - 1];
            }
            else // return the latest revision if none was given
            {
                if (Files[file].Count > 0)
                    result = Files[file][^1];
            }

            if (result == null)
            {
                await SendMessageAsync("ERR no such revision", stream);
                return;
            }

            await SendMessageAsync($"OK {result.Length}", stream);
            await SendMessageAsync(result, stream);
        }

        /// <summary>
        /// Handles the "put" request
        /// </summary>
        /// <param name="args"></param>
        /// <param name="stream"></param>
        /// <param name="streamReader"></param>
        private static async Task HandlePutRequestAsync(string[] args, NetworkStream stream, StreamReader streamReader)
        {
            if (args.Length != 2)
            {
                await SendMessageAsync("ERR usage: PUT file length newline data", stream);
                return;
            }

            var file = args[0];

            if (!ValidFileName().IsMatch(file))
            {
                await SendMessageAsync("ERR illegal file name", stream);
                return;
            }

            _ = int.TryParse(args[1], out var length);
            var buffer = new char[length];
            var read = 0;

            // read from the stream until the buffer is filled
            if (length > 0)
                while ((read += streamReader.Read(buffer, read, length - read)) < length) { }

            var content = new string(buffer);

            Console.WriteLine($"<-- {content.Replace("\n", "\\n")}");

            // check for not allowed characters
            if (!content.All(IsAllowedCharacter))
            {
                await SendMessageAsync("ERR text files only", stream);
                return;
            }

            // check if file already exists
            if (Files.ContainsKey(file))
            {
                // only add new version if previous version does not match new version
                var previousVersion = Files[file][^1];
                if (previousVersion != content)
                    Files[file].Add(content);
            }
            else
            {
                // create a new first version if file does not exist yet
                Files.Add(file, new List<string> { content });
            }

            await SendMessageAsync($"OK r{Files[file].Count}", stream);
        }

        /// <summary>
        /// Handles the "list" request
        /// </summary>
        /// <param name="args"></param>
        /// <param name="stream"></param>
        private static async Task HandleListRequestAsync(string[] args, NetworkStream stream)
        {
            if (args.Length != 1)
            {
                await SendMessageAsync("ERR usage: LIST dir", stream);
                return;
            }

            var dir = args[0];

            if (!ValidDirectoryName().IsMatch(dir))
            {
                await SendMessageAsync("ERR illegal dir name", stream);
                return;
            }

            var results = new List<string>();

            var splitDirectory = dir.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var file in Files)
            {
                var splitFile = file.Key.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (!IsMatch(splitFile, splitDirectory))
                    continue;

                if (splitFile.Length == splitDirectory.Length + 1)
                    results.Add($"{splitFile[^1]} r{file.Value.Count}");
                else
                    results.Add($"{splitFile[splitDirectory.Length]}/ DIR");
            }

            var orderedResults = results.Distinct().OrderBy(result => result, StringComparer.Ordinal);
            await SendMessageAsync($"OK {orderedResults.Count()}", stream);
            foreach (var orderedResult in orderedResults)
                await SendMessageAsync(orderedResult, stream);
        }

        /// <summary>
        /// Handles requests without a proper verb
        /// </summary>
        /// <param name="verb"></param>
        /// <param name="stream"></param>
        private static async Task HandleOtherRequestAsync(string verb, NetworkStream stream)
        {
            await SendMessageAsync($"ERR illegal method: {verb}", stream);
        }

        /// <summary>
        /// Checks whether the given file should be listed in the given directory
        /// </summary>
        /// <param name="file"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
        private static bool IsMatch(string[] file, string[] directory)
        {
            if (file.Length < directory.Length)
                return false;

            if (directory.Length == 0)
                return true;

            for (int i = 0; i < directory.Length; i++)
            {
                if (file[i] != directory[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns whether the given character is in the set of allowed characters
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        private static bool IsAllowedCharacter(char c) => AllowedCharacters.Contains(c);

        /// <summary>
        /// Writes a message to the given stream
        /// </summary>
        /// <param name="message"></param>
        /// <param name="stream"></param>
        private static async Task SendMessageAsync(string message, NetworkStream stream)
        {
            // output outgoing line
            Console.WriteLine($"--> {message.Replace("\n", "\\n")}");

            // terminate the JSON with a newline character and write it to the stream
            if (!message.EndsWith('\n'))
                message += '\n';
            var bytes = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(bytes);
        }
    }
}
