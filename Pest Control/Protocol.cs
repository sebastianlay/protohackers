using System.Net;
using System.Text;

namespace PestControl
{
    internal static class Protocol
    {
        internal static void HandleHelloRequest(BinaryReader reader, BinaryWriter writer, uint length)
        {
            Console.WriteLine("<-- Hello");

            var protocolLength = reader.ReadUInt32();
            var protocolChars = reader.ReadChars((int)protocolLength);
            var version = reader.ReadUInt32();
            var checksum = reader.ReadByte();
            var protocol = new string(protocolChars);

            if (protocol != "pestcontrol" || version != 1 || length != 25 || checksum != 0xce)
                SendErrorMessage("Invalid Hello message", writer);
        }

        internal static void HandleErrorRequest(BinaryReader reader, BinaryWriter writer, uint length)
        {
            Console.WriteLine("<-- Error");

            var messageLength = reader.ReadUInt32();
            var messageChars = reader.ReadChars((int)messageLength);
            var checksum = reader.ReadByte();
            var message = new string(messageChars);

            // TODO
        }

        internal static void HandleOkRequest(BinaryReader reader, BinaryWriter writer, uint length)
        {
            Console.WriteLine("<-- Ok");

            var checksum = reader.ReadByte();

            // TODO
        }

        internal static void HandleSiteVisitRequest(BinaryReader reader, BinaryWriter writer, uint length)
        {
            Console.WriteLine("<-- SiteVisit");

            var site = reader.ReadUInt32();
            var populationCount = reader.ReadUInt32();

            var populations = new List<Population>();

            for (int i = 0; i < populationCount; i++)
            {
                var speciesLength = reader.ReadUInt32();
                var species = reader.ReadChars((int)speciesLength);
                var count = reader.ReadUInt32();

                var population = new Population(new string(species), (int)count);
                populations.Add(population);
            }

            var checksum = reader.ReadByte();

            // TODO
        }

        internal static void HandleTargetPopulationsRequest(BinaryReader reader, BinaryWriter writer, uint length)
        {
            Console.WriteLine("<-- TargetPopulations");

            var site = reader.ReadUInt32();
            var populationCount = reader.ReadUInt32();

            var populations = new List<Population>();

            for (int i = 0; i < populationCount; i++)
            {
                var speciesLength = reader.ReadUInt32();
                var species = reader.ReadChars((int)speciesLength);
                var min = reader.ReadUInt32();
                var max = reader.ReadUInt32();

                var population = new Population(new string(species), (int)min, (int)max);
                populations.Add(population);
            }

            var checksum = reader.ReadByte();

            // TODO: react to target population
        }

        internal static void HandlePolicyResultRequest(BinaryReader reader, BinaryWriter writer, uint length)
        {
            Console.WriteLine("<-- PolicyResult");

            var policy = reader.ReadUInt32();
            var checksum = reader.ReadByte();

            // TODO
        }

        internal static void SendHelloMessage(BinaryWriter writer)
        {
            Console.WriteLine("--> Hello");

            var protocol = ToBytes("pestcontrol");
            var version = ToBytes(1);
            var payload = protocol.Concat(version).ToArray();

            SendMessage(payload, 0x50, writer);
        }

        internal static void SendErrorMessage(string message, BinaryWriter writer)
        {
            Console.WriteLine($"--> Error: {message}");

            var payload = ToBytes(message);

            SendMessage(payload, 0x51, writer);
        }

        internal static void SendDialAuthorityMessage(uint authority, BinaryWriter writer)
        {
            Console.WriteLine($"--> DialAuthority: {authority}");

            var payload = ToBytes(authority);

            SendMessage(payload, 0x53, writer);
        }

        internal static void SendCreatePolicyMessage(string species, byte action, BinaryWriter writer)
        {
            Console.WriteLine($"--> CreatePolicy: {species} {action}");

            var speciesBytes = ToBytes(species);
            var payload = speciesBytes.Append(action).ToArray();

            SendMessage(payload, 0x55, writer);
        }

        internal static void SendDeletePolicyMessage(uint policy, BinaryWriter writer)
        {
            Console.WriteLine($"--> DeletePolicy: {policy}");

            var payload = ToBytes(policy);

            SendMessage(payload, 0x56, writer);
        }

        internal static void SendMessage(byte[] payload, byte messageType, BinaryWriter writer)
        {
            uint totalLength = (uint)payload.Length + 6;
            uint checksum = messageType + totalLength;
            foreach (var item in payload)
                checksum += item;

            checksum = 256 - (checksum % 256);

            writer.Write(messageType);
            writer.Write(totalLength);
            writer.Write(payload);
            writer.Write(checksum);
        }

        private static byte[] ToBytes(string value)
        {
            var valueBytes = Encoding.ASCII.GetBytes(value);
            var valueLength = ToBytes(valueBytes.Length);

            return valueLength.Concat(valueBytes).ToArray();
        }

        private static byte[] ToBytes(int value)
        {
            uint bigEndian = (uint)IPAddress.HostToNetworkOrder(value);
            return BitConverter.GetBytes(bigEndian);
        }

        private static byte[] ToBytes(uint value)
        {
            return ToBytes((int)value);
        }
    }

    internal readonly struct Population
    {
        public Population(string species, int min, int max)
        {
            Species = species;
            Min = min;
            Max = max;
        }

        public Population(string species, int count)
        {
            Species = species;
            Count = count;
        }

        internal readonly string Species;

        internal readonly int Min;

        internal readonly int Max;

        internal readonly int Count;
    }
}
