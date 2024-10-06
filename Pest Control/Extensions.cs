namespace PestControl
{
    internal static class Extensions
    {
        internal static byte[]? ReadBytesExactly(this BinaryReader? reader, int count)
        {
            if (reader == null)
                return null;

            if (count == 0)
                return Array.Empty<byte>();

            var result = new byte[count];
            var cursor = 0;

            while (cursor < count)
            {
                var readBytes = reader.ReadBytes(count - cursor);
                if (readBytes.Length == 0)
                    return null;

                readBytes.CopyTo(result, cursor);
                cursor += readBytes.Length;
            }

            return result;
        }

        internal static byte[] GetBytesBigEndian(int value)
        {
            var result = BitConverter.GetBytes(value);
            Array.Reverse(result);
            return result;
        }

        internal static byte[] GetBytesBigEndian(uint value)
        {
            var result = BitConverter.GetBytes(value);
            Array.Reverse(result);
            return result;
        }

        internal static uint ToUInt32BigEndian(byte[] value)
        {
            Array.Reverse(value);
            return BitConverter.ToUInt32(value);
        }
    }
}
