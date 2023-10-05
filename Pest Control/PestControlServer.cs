using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PestControl
{
    internal static class PestControlServer
    {
        internal static void HandleConnection(TcpClient client)
        {
            using var stream = client.GetStream();
            using var reader = new BinaryReader(stream);
            using var writer = new BinaryWriter(stream);

            Console.WriteLine("Client connected");

            SendHelloMessage(writer);

            try
            {
                while (client.Connected)
                {
                    var messageType = reader.ReadByte();
                    var totalLength = reader.ReadUInt32();
                    switch (messageType)
                    {
                        case 0x50:
                            HandleHelloRequest(reader, writer, totalLength);
                            break;
                        case 0x51:
                            HandleErrorRequest(reader, writer, totalLength);
                            break;
                        case 0x52:
                            HandleOkRequest(reader, writer, totalLength);
                            break;
                        case 0x58:
                            HandleSiteVisitRequest(reader, writer, totalLength);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Console.WriteLine("Client disconnected");
        }

        private static void HandleHelloRequest(BinaryReader reader, BinaryWriter writer, uint length)
        {
            Console.WriteLine("<-- Hello");

            var protocolLength = reader.ReadUInt32();
            var protocol = reader.ReadChars((int)protocolLength);
            var version = reader.ReadUInt32();
            var checksum = reader.ReadByte();
            var protocolString = new string(protocol);

            if (protocolString != "pestcontrol" || version != 1 || length != 25 || checksum != 0xce)
                SendErrorMessage("Invalid Hello message", writer);
        }

        private static void HandleErrorRequest(BinaryReader reader, BinaryWriter writer, uint length)
        {
            Console.WriteLine("<-- Error");
        }

        private static void HandleOkRequest(BinaryReader reader, BinaryWriter writer, uint length)
        {
            Console.WriteLine("<-- Ok");
        }

        private static void HandleSiteVisitRequest(BinaryReader reader, BinaryWriter writer, uint length)
        {
            Console.WriteLine("<-- SiteVisit");
        }

        private static void SendHelloMessage(BinaryWriter writer)
        {
            Console.WriteLine("--> Hello");

            var protocol = Encoding.ASCII.GetBytes("pestcontrol");
            var protocolLength = ToBytes(protocol.Length);
            var version = ToBytes(1);
            var payload = protocolLength.Concat(protocol).Concat(version).ToArray();

            SendMessage(payload, 0x50, writer);
        }

        private static void SendErrorMessage(string message, BinaryWriter writer)
        {
            Console.WriteLine($"--> Error: {message}");

            var messageBytes = Encoding.ASCII.GetBytes(message);
            var messageLength = ToBytes(messageBytes.Length);
            var payload = messageLength.Concat(messageBytes).ToArray();

            SendMessage(payload, 0x51, writer);
        }

        private static void SendMessage(byte[] payload, byte messageType, BinaryWriter writer)
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
}
