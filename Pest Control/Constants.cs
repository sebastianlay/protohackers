namespace PestControl
{
    internal static class Constants
    {
        internal const int DefaultPort = 19121;
        internal const int MinimumThreads = 100;

        internal static class AuthorityServer
        {
            internal const string Hostname = "pestcontrol.protohackers.com";
            internal const int Port = 20547;
        }

        internal static class Action
        {
            internal const byte Cull = 0x90;
            internal const byte Conserve = 0xa0;
        }

        internal static class Protocol
        {
            internal const string Name = "pestcontrol";
            internal const uint Version = 1;
        }

        internal static class Message
        {
            internal const int Overhead = 6; // 1 byte for type, 4 bytes for length and 1 byte for checksum
        }
    }
}
