using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Line_Reversal
{
    /// <summary>
    /// Client that handles the Line Reversal Control Protocol
    /// </summary>
    public class LrcpClient
    {
        const int SupportedClients = 20;
        const int MaxPacketSize = 1000;

        static readonly TimeSpan RetransmissionTimeout = TimeSpan.FromSeconds(5);
        static readonly TimeSpan SessionExpiryTimeout = TimeSpan.FromSeconds(60);

        private readonly int _port;
        private readonly UdpClient _client;
        private readonly ConcurrentDictionary<int, Session> _sessions;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="port">UDP port that the client should listen on</param>
        public LrcpClient(int port)
        {
            _port = port;
            _client = new UdpClient(_port);
            _sessions = new ConcurrentDictionary<int, Session>();
        }

        /// <summary>
        /// Waits for a connection and all subsequent messages
        /// </summary>
        public async Task Listen()
        {
            await Console.Out.WriteLineAsync($"Listening on {_port}");
            ThreadPool.SetMinThreads(SupportedClients, SupportedClients);

            try
            {
                while (true)
                {
                    var result = await _client.ReceiveAsync();
                    await HandleMessageAsync(result.Buffer, result.RemoteEndPoint);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                _client.Close();
            }

            Console.ReadLine();
        }

        /// <summary>
        /// Parses a given package and does some basic validation
        /// </summary>
        /// <param name="buffer">the given message</param>
        /// <param name="endPoint">the endpoint the message was received from</param>
        private async Task HandleMessageAsync(byte[] buffer, IPEndPoint endPoint)
        {
            if (buffer.Length >= MaxPacketSize)
            {
                await Console.Out.WriteLineAsync("Invalid message was 1000 bytes or larger");
                return;
            }

            var message = Encoding.ASCII.GetString(buffer);
            if (message == null)
            {
                await Console.Out.WriteLineAsync("Invalid message could not be parsed as ASCII");
                return;
            }

            var escapedMessage = StringHelper.EscapeForConsole(message);
            await Console.Out.WriteLineAsync($"<-- {escapedMessage}");

            if (!message.StartsWith('/') || !message.EndsWith("/"))
            {
                await Console.Out.WriteLineAsync("Invalid message did not start and end with a slash");
                return;
            }

            // split message only on forward slashes that are not proceeded by a backwards slash
            var splitMessage = Regex.Split(message, @"(?<!\\)/", RegexOptions.Compiled);
            splitMessage = splitMessage.Where(m => !string.IsNullOrEmpty(m)).ToArray();
            if (splitMessage == null || splitMessage.Length == 0)
            {
                await Console.Out.WriteLineAsync("Invalid message did not contain any data");
                return;
            }

            switch (splitMessage[0])
            {
                case "connect":
                    await ParseConnectMessageAsync(splitMessage, endPoint);
                    break;
                case "data":
                    await ParseDataMessageAsync(splitMessage, endPoint);
                    break;
                case "ack":
                    await ParseAckMessageAsync(splitMessage, endPoint);
                    break;
                case "close":
                    await ParseCloseMessageAsync(splitMessage, endPoint);
                    break;
                default:
                    await Console.Out.WriteLineAsync("Invalid message did not have a valid message type");
                    break;
            }
        }

        /// <summary>
        /// Parses the fields of the given "connect" message and does some more validation
        /// </summary>
        /// <param name="splitMessage">the received message, split into its fields</param>
        /// <param name="endPoint">the endpoint the message was received from</param>
        private async Task ParseConnectMessageAsync(string[] splitMessage, IPEndPoint endPoint)
        {
            if (splitMessage.Length != 2)
            {
                await Console.Out.WriteLineAsync("Invalid connect message did not have the correct number of fields");
                return;
            }

            if (!int.TryParse(splitMessage[1], out var session) || session < 0)
            {
                await Console.Out.WriteLineAsync("Invalid connect message did not have a valid session field");
                return;
            }

            await HandleConnectMessageAsync(session, endPoint);
        }

        /// <summary>
        /// Parses the fields of the given "data" message and does some more validation
        /// </summary>
        /// <param name="splitMessage">the received message, split into its fields</param>
        /// <param name="endPoint">the endpoint the message was received from</param>
        private async Task ParseDataMessageAsync(string[] splitMessage, IPEndPoint endPoint)
        {
            if (splitMessage.Length != 4)
            {
                await Console.Out.WriteLineAsync("Invalid connect message did not have the correct number of fields");
                return;
            }

            if (!int.TryParse(splitMessage[1], out var session) || session < 0)
            {
                await Console.Out.WriteLineAsync("Invalid connect message did not have a valid session field");
                return;
            }

            if (!int.TryParse(splitMessage[2], out var pos) || pos < 0)
            {
                await Console.Out.WriteLineAsync("Invalid connect message did not have a valid pos field");
                return;
            }

            var data = splitMessage[3];
            await HandleDataMessageAsync(session, pos, data, endPoint);
        }

        /// <summary>
        /// Parses the fields of the given "ack" message and does some more validation
        /// </summary>
        /// <param name="splitMessage">the received message, split into its fields</param>
        /// <param name="endPoint">the endpoint the message was received from</param>
        private async Task ParseAckMessageAsync(string[] splitMessage, IPEndPoint endPoint)
        {
            if (splitMessage.Length != 3)
            {
                await Console.Out.WriteLineAsync("Invalid ack message did not have the correct number of fields");
                return;
            }

            if (!int.TryParse(splitMessage[1], out var session) || session < 0)
            {
                await Console.Out.WriteLineAsync("Invalid ack message did not have a valid session field");
                return;
            }

            if (!int.TryParse(splitMessage[2], out var length) || length < 0)
            {
                await Console.Out.WriteLineAsync("Invalid ack message did not have a valid length field");
                return;
            }

            await HandleAckMessageAsync(session, length, endPoint);
        }

        /// <summary>
        /// Parses the fields of the given "close" message and does some more validation
        /// </summary>
        /// <param name="splitMessage">the received message, split into its fields</param>
        /// <param name="endPoint">the endpoint the message was received from</param>
        private async Task ParseCloseMessageAsync(string[] splitMessage, IPEndPoint endPoint)
        {
            if (splitMessage.Length != 2)
            {
                await Console.Out.WriteLineAsync("Invalid close message did not have the correct number of fields");
                return;
            }

            if (!int.TryParse(splitMessage[1], out var session) || session < 0)
            {
                await Console.Out.WriteLineAsync("Invalid close message did not have a valid session field");
                return;
            }

            await HandleCloseMessageAsync(session, endPoint);
        }

        /// <summary>
        /// Handles the logic for a given "connect" message
        /// </summary>
        /// <param name="session">the content of the session field</param>
        /// <param name="endPoint">the endpoint the message was received from</param>
        private async Task HandleConnectMessageAsync(int session, IPEndPoint endPoint)
        {
            var newSession = new Session(endPoint);
            if (_sessions.TryAdd(session, newSession))
            {
                newSession.LastActivity = DateTime.Now;
                _ = HandleSessionQueue(newSession);
            }

            await SendMessageAsync($"/ack/{session}/0/", endPoint);
        }

        /// <summary>
        /// Handles the logic for a given "data" message
        /// </summary>
        /// <param name="session">the content of the session field</param>
        /// <param name="pos">the content of the pos field</param>
        /// <param name="data">the content of the data field</param>
        /// <param name="endPoint">the endpoint the message was received from</param>
        private async Task HandleDataMessageAsync(int session, int pos, string data, IPEndPoint endPoint)
        {
            if (!_sessions.TryGetValue(session, out var existingSession))
            {
                await Console.Out.WriteLineAsync("Invalid data message for a non-existing session");
                await SendMessageAsync($"/close/{session}/", endPoint);
                return;
            }

            existingSession.LastActivity = DateTime.Now;

            // save the received data as this is the newest one we got
            if (pos == existingSession.ReceivedPos)
            {
                var unescapedData = StringHelper.Unescape(data);
                var unescapedBytes = Encoding.ASCII.GetBytes(unescapedData);

                existingSession.ReceivedPos = pos + unescapedBytes.Length;
                existingSession.ReceivedData.Seek(pos, SeekOrigin.Begin);
                await existingSession.ReceivedData.WriteAsync(unescapedBytes);

                // check the newly received data for line endings
                await CheckForNewLines(session, existingSession);
            }

            // send an "ack" message (regardless of whether we already received it or not)
            var message = $"/ack/{session}/{existingSession.ReceivedPos}/";
            await SendMessageAsync(message, existingSession.EndPoint);
        }

        /// <summary>
        /// Handles the logic for a given "ack" message
        /// </summary>
        /// <param name="session">the content of the session field</param>
        /// <param name="length">the content of the length field</param>
        /// <param name="endPoint">the endpoint the message was received from</param>
        private async Task HandleAckMessageAsync(int session, int length, IPEndPoint endPoint)
        {
            if (!_sessions.TryGetValue(session, out var existingSession))
            {
                await Console.Out.WriteLineAsync("Invalid ack message for a non-existing session");
                await SendMessageAsync($"/close/{session}/", endPoint);
                return;
            }

            existingSession.LastActivity = DateTime.Now;

            if (length <= existingSession.ReceivedAck)
            {
                await Console.Out.WriteLineAsync("Already received ack message. Ignoring it.");
                return;
            }
            else
            {
                existingSession.ReceivedAck = length;
            }

            if (length > existingSession.SentPos)
            {
                await Console.Out.WriteLineAsync("Invalid ack message for not yet sent data");
                await SendMessageAsync($"/close/{session}/", endPoint);
                return;
            }

            // if we got an "ack" message for a previous state, send all newer data again
            if (length < existingSession.SentPos)
            {
                var buffer = new byte[existingSession.SentPos - length];
                existingSession.SentData.Seek(length, SeekOrigin.Begin);
                await existingSession.SentData.ReadAsync(buffer);
                var data = Encoding.ASCII.GetString(buffer);

                var pos = length;
                var chunks = StringHelper.GetInChunks(data, MaxPacketSize / 2);
                foreach (var chunk in chunks)
                {
                    var escapedData = StringHelper.Escape(chunk);
                    var content = $"/data/{session}/{pos}/{escapedData}/";
                    await SendMessageAsync(content, existingSession.EndPoint);

                    pos += chunk.Length;
                }

                return;
            }
        }

        /// <summary>
        /// Handles the logic for a given "close" message
        /// </summary>
        /// <param name="session">the content of the session field</param>
        /// <param name="endPoint">the endpoint the message was received from</param>
        private async Task HandleCloseMessageAsync(int session, IPEndPoint endPoint)
        {
            if (_sessions.TryRemove(session, out var removedSession) && removedSession != null)
                await removedSession.DisposeAsync();

            await SendMessageAsync($"/close/{session}/", endPoint);
        }

        /// <summary>
        /// Checks whether there are new "lines" available for the "application layer"
        /// and sends the message(s) with the lines reversed
        /// </summary>
        /// <param name="session">the content of the session field</param>
        /// <param name="existingSession">the session that the message should be send on</param>
        private async Task CheckForNewLines(int session, Session existingSession)
        {
            var begin = existingSession.LastNewLine;
            var end = existingSession.ReceivedPos;

            var buffer = new byte[end - begin];
            existingSession.ReceivedData.Seek(begin, SeekOrigin.Begin);
            await existingSession.ReceivedData.ReadAsync(buffer);
            var relevantText = Encoding.ASCII.GetString(buffer);

            var startIndex = 0;
            var lastIndex = 0;
            var foundNewLine = false;

            while (relevantText.IndexOf('\n', startIndex) >= 0)
            {
                foundNewLine = true;

                var nextIndex = relevantText.IndexOf('\n', startIndex);
                var line = relevantText[lastIndex..nextIndex];

                var reversedLine = StringHelper.Reverse(line);
                if (!string.IsNullOrEmpty(reversedLine))
                    await SendDataAsync(reversedLine, session);

                lastIndex = nextIndex + 1;
                startIndex = nextIndex + 1;

                if (relevantText.Length == startIndex)
                    break;
            }

            if (foundNewLine)
                existingSession.LastNewLine = begin + lastIndex;
        }

        /// <summary>
        /// Splits a given line into chunks and sends it as messages (including retries)
        /// </summary>
        /// <param name="data">the content that should be send via messages</param>
        /// <param name="session">the content of the session field</param>
        private async Task SendDataAsync(string data, int session)
        {
            if (!_sessions.TryGetValue(session, out var existingSession))
                return;

            data += '\n';
            var bytes = Encoding.ASCII.GetBytes(data);
            await existingSession.SentData.WriteAsync(bytes);

            var chunks = StringHelper.GetInChunks(data, MaxPacketSize / 2);
            foreach (var chunk in chunks)
            {
                var escapedData = StringHelper.Escape(chunk);
                var content = $"/data/{session}/{existingSession.SentPos}/{escapedData}/";
                await SendMessageAsync(content, existingSession.EndPoint);

                var message = new Message(existingSession.SentPos + chunk.Length, content);
                existingSession.Messages.Enqueue(message);
                existingSession.SentPos += chunk.Length;
            }
        }

        /// <summary>
        /// Sends a preformatted message to the given endpoint
        /// </summary>
        /// <param name="message">the message that should be sent</param>
        /// <param name="endPoint">the endpoint the message should be sent to</param>
        private async Task SendMessageAsync(string message, IPEndPoint endPoint)
        {
            var escapedMessage = StringHelper.EscapeForConsole(message);
            await Console.Out.WriteLineAsync($"--> {escapedMessage}");

            var datagram = Encoding.ASCII.GetBytes(message);
            await _client.SendAsync(datagram, datagram.Length, endPoint);
        }

        /// <summary>
        /// Observes the message queue of the session and resends messages if necessary
        /// </summary>
        /// <param name="session">the session that should be observed</param>
        private async Task HandleSessionQueue(Session session)
        {
            while (true)
            {
                if (session.Closed)
                {
                    await Console.Out.WriteLineAsync("Session was closed");
                    break;
                }

                if (DateTime.Now.Subtract(SessionExpiryTimeout) > session.LastActivity)
                {
                    await Console.Out.WriteLineAsync("Session reached timeout");
                    break;
                }

                if (session.Messages.TryPeek(out var message))
                {
                    if (session.ReceivedAck >= message.RequiredAck)
                    {
                        session.Messages.TryDequeue(out _);
                        continue;
                    }
                    else
                    {
                        await Console.Out.WriteLineAsync("No ack received. Sending message again");
                        await SendMessageAsync(message.Content, session.EndPoint);
                    }
                }

                await Task.Delay((int)RetransmissionTimeout.TotalMilliseconds);
            }
        }
    }
}
