using System.Net.Sockets;

namespace Insecure_Sockets_Layer
{
    public static class Program
    {
        const int DefaultPort = 19118;
        const int SupportedClients = 20;

        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args">The TCP port the client should listen on</param>
        static async Task Main(string[] args)
        {
            ThreadPool.SetMinThreads(SupportedClients, SupportedClients);

            int port = args.Length > 0 && int.TryParse(args[0], out port) ? port : DefaultPort;
            var listener = TcpListener.Create(port);
            listener.Start();

            Console.WriteLine($"Listening on port {port}");

            while (true)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    Console.WriteLine($"New client connected {client.Client.RemoteEndPoint}");
                    _ = IslClient.HandleConnection(client);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}
