namespace SpeedDaemon
{
    /// <summary>
    /// Handles the reading and writing of data to an underlying network stream
    /// including the conversion from little to big endianess
    /// </summary>
    internal static class Helper
    {
        internal static string ReadString(BinaryReader reader)
        {
            return reader.ReadString();
        }

        internal static byte ReadU8(BinaryReader reader)
        {
            return reader.ReadByte();
        }

        internal static ushort ReadU16(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(2);
            if (bytes == null || bytes.Length < 2)
                return 0; // we have read less than two bytes

            Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes);
        }

        internal static uint ReadU32(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            if (bytes == null || bytes.Length < 4)
                return 0; // we have read less than four bytes

            Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes);
        }

        internal static void Write(BinaryWriter writer, int content)
        {
            writer.Write((byte)content);
        }

        internal static void Write(BinaryWriter writer, string content)
        {
            writer.Write(content);
        }

        internal static void Write(BinaryWriter writer, byte content)
        {
            writer.Write(content);
        }

        internal static void Write(BinaryWriter writer, ushort content)
        {
            var bytes = BitConverter.GetBytes(content);
            Array.Reverse(bytes);
            writer.Write(bytes);
        }

        internal static void Write(BinaryWriter writer, uint content)
        {
            var bytes = BitConverter.GetBytes(content);
            Array.Reverse(bytes);
            writer.Write(bytes);
        }
    }
}
