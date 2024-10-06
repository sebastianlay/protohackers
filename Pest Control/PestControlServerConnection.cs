using System.Net.Sockets;

namespace PestControl
{
    internal sealed class PestControlServerConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly BinaryReader? _reader;
        private readonly BinaryWriter? _writer;
        private readonly string _identifier;

        internal PestControlServerConnection(TcpClient client, string identifier)
        {
            var stream = client.GetStream();
            _client = client;
            _reader = new BinaryReader(stream);
            _writer = new BinaryWriter(stream);
            _identifier = identifier;
        }

        internal void HandleConnection()
        {
            Console.WriteLine($"{_identifier,20} <-> connected");

            try
            {
                while (_client.Connected)
                {
                    try
                    {
                        var message = Protocol.ReceiveMessage(_reader, _identifier);
                        switch (message.Type)
                        {
                            case MessageType.Hello:
                                HandleHelloMessage(message);
                                break;
                            case MessageType.SiteVisit:
                                HandleSiteVisitMessage(message);
                                break;
                        }
                    }
                    catch (InvalidDataException e)
                    {
                        Protocol.SendHelloMessage(_writer, _identifier);
                        Protocol.SendErrorMessage($"Invalid Hello message: {e.Message}", _writer, _identifier);
                        return;
                    }
                }
            }
            catch (EndOfStreamException) { }
            catch (IOException) { }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Console.WriteLine($"{_identifier,20} -x- disconnected");
            Dispose();
        }

        private void HandleHelloMessage(Message message)
        {
            try
            {
                var protocol = message.GetPayloadString();
                var version = message.GetPayloadUInt();

                if (protocol != Constants.Protocol.Name || version != Constants.Protocol.Version)
                    throw new InvalidDataException("The protocol or version is invalid");
                else if (!message.IsReadCompletely)
                    throw new InvalidDataException("The message contains unused payload");

                Protocol.SendHelloMessage(_writer, _identifier);
            }
            catch (InvalidDataException e)
            {
                Protocol.SendHelloMessage(_writer, _identifier);
                Protocol.SendErrorMessage($"Invalid Hello message: {e.Message}", _writer, _identifier);
            }
        }

        private void HandleSiteVisitMessage(Message message)
        {
            try
            {
                var reportedPopulations = new Dictionary<string, ReportedPopulation>();

                var site = message.GetPayloadUInt();
                var populationsCount = message.GetPayloadUInt();
                for (int i = 0; i < populationsCount; i++)
                {
                    var species = message.GetPayloadString();
                    var count = (int)message.GetPayloadUInt();

                    var reportedPopulation = new ReportedPopulation { Count = count };

                    if (reportedPopulations.TryGetValue(species, out var existingPopulation) && existingPopulation.Count != reportedPopulation.Count)
                        throw new InvalidDataException("The message contains conflicting observations for same species");

                    reportedPopulations.TryAdd(species, reportedPopulation);
                }

                if (!message.IsReadCompletely)
                    throw new InvalidDataException("The message contains unused payload");

                var siteVisit = new SiteVisit { Site = site, ReportedPopulations = reportedPopulations };
                AuthorityServer.AddSiteVisit(siteVisit);
            }
            catch (InvalidDataException e)
            {
                Protocol.SendErrorMessage($"Invalid SiteVisit message: {e.Message}", _writer, _identifier);
            }
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _client?.Dispose();
        }
    }
}
