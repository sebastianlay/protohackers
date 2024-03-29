﻿using System.Net.Sockets;

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
                        case 0x58:
                            Protocol.HandleSiteVisitRequest(reader, writer, totalLength);
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
    }
}
