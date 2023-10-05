using System.Collections.Concurrent;
using System.Net;

namespace LineReversal
{
    /// <summary>
    /// A session of the LRCP protocol
    /// </summary>
    internal sealed class Session : IAsyncDisposable
    {
        internal IPEndPoint EndPoint { get; init; }

        internal MemoryStream ReceivedData { get; set; }

        internal MemoryStream SentData { get; set; }

        internal int ReceivedPos { get; set; }

        internal int ReceivedAck { get; set; }

        internal int SentPos { get; set; }

        internal int LastNewLine { get; set; }

        internal DateTime LastActivity { get; set; }

        internal ConcurrentQueue<Message> Messages { get; set; }

        internal bool Closed { get; set; }

        internal Session(IPEndPoint endPoint)
        {
            EndPoint = endPoint;
            ReceivedData = new MemoryStream();
            SentData = new MemoryStream();
            Messages = new ConcurrentQueue<Message>();
        }

        public async ValueTask DisposeAsync()
        {
            Closed = true;

            await ReceivedData.DisposeAsync();
            await SentData.DisposeAsync();

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// A message of the LRCP protocol (that can be send again later)
    /// </summary>
    internal sealed class Message
    {
        internal int RequiredAck { get; set; }

        internal string Content { get; set; }

        internal Message(int requiredAck, string content)
        {
            RequiredAck = requiredAck;
            Content = content;
        }
    }
}
