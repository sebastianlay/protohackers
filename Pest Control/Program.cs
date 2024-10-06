
using System.Net.Sockets;

namespace PestControl
{
    internal static class Program
    {
        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args">The TCP port the client should listen on</param>
        private static async Task Main(string[] args)
        {
            // set a minimum thread count to allow the ThreadPool to quickly spawn new threads
            ThreadPool.SetMinThreads(Constants.MinimumThreads, Constants.MinimumThreads);

            _ = Task.Run(AuthorityServer.HandleSiteVisits);

            int port = args.Length > 0 && int.TryParse(args[0], out port) ? port : Constants.DefaultPort;
            var listener = TcpListener.Create(port);
            listener.Start();

            Console.WriteLine($"Listening on port {port}");

            while (true)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    var postControlServerConnection = new PestControlServerConnection(client, $"client {client.Client.Handle}");
                    _ = Task.Run(postControlServerConnection.HandleConnection);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}
