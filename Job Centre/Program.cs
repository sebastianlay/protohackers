using System.Net.Sockets;

namespace JobCentre
{
    internal static class Program
    {
        private const int DefaultPort = 19119;
        private const int MinimumThreads = 100;

        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args">The TCP port the client should listen on</param>
        private static async Task Main(string[] args)
        {
            // set a minimum thread count to allow the ThreadPool to quickly spawn new threads
            ThreadPool.SetMinThreads(MinimumThreads, MinimumThreads);

            int port = args.Length > 0 && int.TryParse(args[0], out port) ? port : DefaultPort;
            var listener = TcpListener.Create(port);
            listener.Start();

            Console.WriteLine($"Listening on port {port}");

            while (true)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => JobServer.HandleConnectionAsync(client));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}
