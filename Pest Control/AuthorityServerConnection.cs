using System.Net.Sockets;

namespace PestControl
{
    internal sealed class AuthorityServerConnection : IDisposable
    {
        private readonly TcpClient? _client;
        private readonly BinaryReader? _reader;
        private readonly BinaryWriter? _writer;
        private readonly string _identifier;
        private readonly uint _site;

        private IReadOnlyDictionary<string, TargetPopulation>? _targetPopulations;
        private readonly Dictionary<string, Policy> _policies = new();

        internal AuthorityServerConnection(uint site, string identifier)
        {
            _client = new TcpClient(Constants.AuthorityServer.Hostname, Constants.AuthorityServer.Port);
            var stream = _client.GetStream();
            _reader = new BinaryReader(stream);
            _writer = new BinaryWriter(stream);

            _site = site;
            _identifier = identifier;

            CreateConnection();
        }

        internal IReadOnlyDictionary<string, TargetPopulation>? GetTargetPopulations()
        {
            if (_targetPopulations != null && _targetPopulations.Count != 0)
                return _targetPopulations;

            return _targetPopulations = GetTargetPopulationsFromServer();
        }

        private void CreateConnection()
        {
            try
            {
                Console.WriteLine($"{_identifier,20} <-> connected");

                Protocol.SendHelloMessage(_writer, _identifier);

                var message = Protocol.ReceiveMessage(_reader, _identifier);
                if (message?.Type != MessageType.Hello)
                {
                    Console.WriteLine("Message was of a different type than expected. Expected type: Hello");
                    AuthorityServer.CloseConnection(_site);
                }
            }
            catch (InvalidDataException e)
            {
                Protocol.SendErrorMessage($"Invalid Hello message: {e.Message}", _writer, _identifier);
            }
        }

        private Dictionary<string, TargetPopulation>? GetTargetPopulationsFromServer()
        {
            try
            {
                Protocol.SendDialAuthorityMessage(_site, _writer, _identifier);
                var message = Protocol.ReceiveMessage(_reader, _identifier);
                if (message?.Type != MessageType.TargetPopulations)
                    throw new InvalidDataException("Message was of a different type than expected. Expected type: TargetPopulations");

                var receivedSite = message.GetPayloadUInt();
                if (receivedSite != _site)
                    throw new InvalidDataException("The received site does not match requested site");

                var result = new Dictionary<string, TargetPopulation>();
                var populationsCount = message.GetPayloadUInt();
                for (int i = 0; i < populationsCount; i++)
                {
                    var species = message.GetPayloadString();
                    var min = (int)message.GetPayloadUInt();
                    var max = (int)message.GetPayloadUInt();

                    var targetPopulation = new TargetPopulation { Min = min, Max = max };
                    result.Add(species, targetPopulation);
                }

                if (!message.IsReadCompletely)
                    throw new InvalidDataException("The message contains unused bytes");

                return result;
            }
            catch (InvalidDataException e)
            {
                Protocol.SendErrorMessage($"Invalid TargetPopulations message: {e.Message}", _writer, _identifier);
                AuthorityServer.CloseConnection(_site);

                return null;
            }
        }

        internal void CreatePolicy(string species, byte action)
        {
            try
            {
                Protocol.SendCreatePolicyMessage(species, action, _writer, _identifier);

                var message = Protocol.ReceiveMessage(_reader, _identifier);
                if (message?.Type != MessageType.PolicyResult)
                    throw new InvalidDataException("Message was of a different type than expected. Expected type: PolicyResult");

                var id = message.GetPayloadUInt();
                var policy = new Policy { Id = id };

                _policies.Add(species, policy);
            }
            catch (InvalidDataException e)
            {
                Protocol.SendErrorMessage($"Invalid PolicyResult message: {e.Message}", _writer, _identifier);
                AuthorityServer.CloseConnection(_site);
            }
        }

        internal void DeletePolicy(string species)
        {
            try
            {
                if (!_policies.TryGetValue(species, out var policy))
                    return;

                Protocol.SendDeletePolicyMessage(policy.Id, _writer, _identifier);

                var message = Protocol.ReceiveMessage(_reader, _identifier);
                if (message?.Type != MessageType.Ok)
                    throw new InvalidDataException("Message was of a different type than expected. Expected type: Ok");

                _policies.Remove(species);
            }
            catch (InvalidDataException e)
            {
                Protocol.SendErrorMessage($"Invalid Ok message: {e.Message}", _writer, _identifier);
                AuthorityServer.CloseConnection(_site);
            }
        }

        public void Dispose()
        {
            Console.WriteLine($"{_identifier,20} -x- disconnected");

            _writer?.Dispose();
            _reader?.Dispose();
            _client?.Dispose();
        }
    }
}
