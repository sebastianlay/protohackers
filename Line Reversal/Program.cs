namespace Line_Reversal
{
    public static class Program
    {
        const int DefaultPort = 19117;

        static async Task Main(string[] args)
        {
            int port = args.Length > 0 && int.TryParse(args[0], out port) ? port : DefaultPort;
            var client = new LrcpClient(port);

            await client.Listen();
        }
    }
}
