namespace InsecureSocketsLayer
{
    /// <summary>
    /// Base class for operations that can be applied to a given message
    /// </summary>
    internal abstract class Operation
    {
        /// <summary>
        /// The N parameter for the "xor" and the "add" operation
        /// </summary>
        internal byte Parameter { get; set; }

        /// <summary>
        /// Applies the operation to each byte of the given message
        /// </summary>
        /// <param name="message">the message as a byte array</param>
        /// <param name="streamPosition">the position of the message in the stream</param>
        /// <param name="reverse">true for decoding, false for encoding</param>
        /// <returns>the message after the operation was applied</returns>
        internal abstract byte[] Apply(byte[] message, int streamPosition, bool reverse);
    }

    /// <summary>
    /// Reverses the order of the bits in each byte (e.g. 10100110 becomes 01100101)
    /// </summary>
    internal sealed class ReverseBitsOp : Operation
    {
        /// <!<inheritdoc/>
        internal override byte[] Apply(byte[] message, int streamPosition, bool reverse)
        {
            return message.Select(b => (byte)(((b * 0x0802u & 0x22110u) | (b * 0x8020u & 0x88440u)) * 0x10101u >> 16)).ToArray();
        }
    }

    /// <summary>
    /// Applies XOR with N to each byte
    /// </summary>
    internal sealed class XorOp : Operation
    {
        /// <!<inheritdoc/>
        internal override byte[] Apply(byte[] message, int streamPosition, bool reverse)
        {
            return message.Select(b => (byte)(b ^ Parameter)).ToArray();
        }
    }

    /// <summary>
    /// Applies XOR with the position in the stream to each byte
    /// </summary>
    internal sealed class XorPosOp : Operation
    {
        /// <!<inheritdoc/>
        internal override byte[] Apply(byte[] message, int streamPosition, bool reverse)
        {
            return message.Select((b, i) => (byte)(b ^ (streamPosition + i))).ToArray();
        }
    }

    /// <summary>
    /// Applies addition with N modulo 256 to each byte
    /// </summary>
    internal sealed class AddOp : Operation
    {
        /// <!<inheritdoc/>
        internal override byte[] Apply(byte[] message, int streamPosition, bool reverse)
        {
            return reverse
                ? message.Select(b => (byte)((b - Parameter) % 256)).ToArray()
                : message.Select(b => (byte)((b + Parameter) % 256)).ToArray();
        }
    }

    /// <summary>
    /// Applies addition with the position in the stream modulo 256 to each byte
    /// </summary>
    internal sealed class AddPosOp : Operation
    {
        /// <!<inheritdoc/>
        internal override byte[] Apply(byte[] message, int streamPosition, bool reverse)
        {
            return reverse
                ? message.Select((b, i) => (byte)((b - streamPosition - i) % 256)).ToArray()
                : message.Select((b, i) => (byte)((b + streamPosition + i) % 256)).ToArray();
        }
    }
}
