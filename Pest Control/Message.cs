using System.Text;

namespace PestControl
{
    internal sealed class Message
    {
        public Message(MessageType type)
        {
            Type = type;
            _length = GetLengthBytesFromPayload();
        }

        public Message(byte type, byte[] payload, byte[] length)
        {
            Type = (MessageType)type;
            _payload = payload;
            _length = length;
        }

        internal MessageType Type { get; }

        internal byte[] _payload = Array.Empty<byte>();

        private byte[] _length = Array.Empty<byte>();

        private int _cursor;

        private int ConvertedLength => _payload.Length + Constants.Message.Overhead;

        internal byte Checksum => CalculateChecksum();

        internal bool IsReadCompletely => _cursor == _payload.Length;

        internal void AddPayload(string value)
        {
            var valueBytes = Encoding.ASCII.GetBytes(value);
            var lengthBytes = Extensions.GetBytesBigEndian(valueBytes.Length);
            var encodedPayload = lengthBytes.Concat(valueBytes).ToArray();

            AddPayload(ref encodedPayload);
        }

        internal void AddPayload(uint value)
        {
            var valueBytes = Extensions.GetBytesBigEndian(value);

            AddPayload(ref valueBytes);
        }

        internal void AddPayload(byte value)
        {
            var encodedPayload = new byte[1] { value };

            AddPayload(ref encodedPayload);
        }

        internal void AddPayload(ref byte[] value)
        {
            if (_payload == null)
                _payload = new byte[value.Length];
            else
                Array.Resize(ref _payload, _payload.Length + value.Length);

            value.CopyTo(_payload, _payload.Length - value.Length);

            _length = GetLengthBytesFromPayload();
        }

        internal uint GetPayloadUInt()
        {
            if (_cursor + 4 > _payload.Length)
                throw new InvalidDataException("The length succeeded the total length");

            var data = _payload[_cursor..(_cursor + 4)];
            var result = Extensions.ToUInt32BigEndian(data);
            _cursor += 4;

            return result;
        }

        internal string GetPayloadString()
        {
            var length = (int)GetPayloadUInt();

            if (_cursor + length > _payload.Length)
                throw new InvalidDataException("The length succeeded the total length");

            var result = Encoding.ASCII.GetString(_payload, _cursor, length);
            _cursor += length;

            return result;
        }

        internal byte[] Build()
        {
            if (_payload == null || _payload.Length == 0)
                return Array.Empty<byte>();

            var result = new byte[ConvertedLength];
            result[0] = (byte)Type;
            _length.CopyTo(result, 1);
            _payload.CopyTo(result, 5);
            result[^1] = Checksum;

            return result;
        }

        private byte[] GetLengthBytesFromPayload()
        {
            var length = _payload.Length + Constants.Message.Overhead;
            var result = Extensions.GetBytesBigEndian(length);

            return result ?? (new byte[4] { 0, 0, 0, Constants.Message.Overhead });
        }

        private byte CalculateChecksum()
        {
            var total = (byte)Type;

            foreach (var value in _length)
                total += value;

            foreach (var value in _payload)
                total += value;

            return (byte)(256 - (total % 256));
        }

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();
            var type = $"{(byte)Type:x2}";
            stringBuilder.AppendLine(type);

            stringBuilder.AppendJoin(' ', _length.Select(x => $"{x:x2}")).AppendLine();

            foreach (var chunk in _payload.Chunk(4))
                stringBuilder.AppendJoin(' ', chunk.Select(x => $"{x:x2}")).AppendLine();

            var checksum = $"{Checksum:x2}";
            stringBuilder.AppendLine(checksum);

            return stringBuilder.ToString();
        }
    }

    internal enum MessageType : byte
    {
        Hello = 0x50,
        Error = 0x51,
        Ok = 0x52,
        DialAuthority = 0x53,
        TargetPopulations = 0x54,
        CreatePolicy = 0x55,
        DeletePolicy = 0x56,
        PolicyResult = 0x57,
        SiteVisit = 0x58
    }
}
