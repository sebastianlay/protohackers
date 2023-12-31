using System.Net.Sockets;

namespace PestControl
{
    internal static class Program
    {
        private const int DefaultPort = 19121;
        private const int MinimumThreads = 100;

        private const string AuthorityHostname = "pestcontrol.protohackers.com";
        private const int AuthorityPort = 20547;

        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args">The TCP port the client should listen on</param>
        private static async Task Main(string[] args)
        {
            // set a minimum thread count to allow the ThreadPool to quickly spawn new threads
            ThreadPool.SetMinThreads(MinimumThreads, MinimumThreads);

            var authorityServerClient = new TcpClient(AuthorityHostname, AuthorityPort);
            _ = Task.Run(() => AuthorityServer.HandleConnection(authorityServerClient));

            int port = args.Length > 0 && int.TryParse(args[0], out port) ? port : DefaultPort;
            var listener = TcpListener.Create(port);
            listener.Start();

            Console.WriteLine($"Listening on port {port}");

            while (true)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => PestControlServer.HandleConnection(client));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}
