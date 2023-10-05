using System.Net.Sockets;

namespace SpeedDaemon
{
    /// <summary>
    /// Main wrapper for our program
    /// </summary>
    internal static class Program
    {
        private const int DefaultPort = 19116;
        private const int SupportedClients = 150;

        internal static List<Client> Clients { get; set; } = new();

        /// <summary>
        /// Listens for new clients and spins up a new thread for each connected client
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static async Task Main(string[] args)
        {
            int port = args.Length > 0 && int.TryParse(args[0], out port) ? port : DefaultPort;
            var listener = TcpListener.Create(port);
            listener.Start();
            Console.WriteLine($"Listening on port {port}");

            // set the number of minimum threads to something absurdly high
            // to be able to keep the frequency of the heartbeat messages high enough
            ThreadPool.SetMinThreads(SupportedClients, SupportedClients);

            while (true)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleConnection(client));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        /// <summary>
        /// Runs the main loop of the client to handle incoming messages
        /// </summary>
        /// <param name="tcpClient"></param>
        private static void HandleConnection(TcpClient tcpClient)
        {
            Console.WriteLine("Client connected");

            var client = new Client(tcpClient);
            Clients.Add(client);

            try
            {
                while (tcpClient.Connected)
                {
                    try
                    {
                        client.HandleNextMessage();
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                client.Close();
                client.Dispose();
                Clients.Remove(client);
            }

            Console.WriteLine("Client disconnected");
        }
    }
}
