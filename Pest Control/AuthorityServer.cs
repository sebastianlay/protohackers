using System.Net.Sockets;

namespace PestControl
{
    internal static class AuthorityServer
    {
        private static int CurrentAuthority = -1;

        internal static void HandleConnection(TcpClient client)
        {
            using var stream = client.GetStream();
            using var reader = new BinaryReader(stream);
            using var writer = new BinaryWriter(stream);

            Console.WriteLine("Authority server connected");

            Protocol.SendHelloMessage(writer);

            try
            {
                while (client.Connected)
                {
                    var messageType = reader.ReadByte();
                    var totalLength = reader.ReadUInt32();
                    switch (messageType)
                    {
                        case 0x50:
                            Protocol.HandleHelloRequest(reader, writer, totalLength);
                            break;
                        case 0x51:
                            Protocol.HandleErrorRequest(reader, writer, totalLength);
                            break;
                        case 0x52:
                            Protocol.HandleOkRequest(reader, writer, totalLength);
                            break;
                        case 0x54:
                            Protocol.HandleTargetPopulationsRequest(reader, writer, totalLength);
                            break;
                        case 0x57:
                            Protocol.HandlePolicyResultRequest(reader, writer, totalLength);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Console.WriteLine("Authority server disconnected");
        }
    }
}
