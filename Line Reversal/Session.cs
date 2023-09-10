using System.Collections.Concurrent;
using System.Net;

namespace Line_Reversal
{
    /// <summary>
    /// A session of the LRCP protocol
    /// </summary>
    public class Session : IAsyncDisposable
    {
        public IPEndPoint EndPoint { get; init; }

        public MemoryStream ReceivedData { get; set; }

        public MemoryStream SentData { get; set; }

        public int ReceivedPos { get; set; }

        public int ReceivedAck { get; set; }

        public int SentPos { get; set; }

        public int LastNewLine { get; set; }

        public DateTime LastActivity { get; set; }

        public ConcurrentQueue<Message> Messages { get; set; }

        public bool Closed { get; set; }

        public Session(IPEndPoint endPoint)
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
    public class Message
    {
        public int RequiredAck { get; set; }

        public string Content { get; set; }

        public Message(int requiredAck, string content)
        {
            RequiredAck = requiredAck;
            Content = content;
        }
    }
}
