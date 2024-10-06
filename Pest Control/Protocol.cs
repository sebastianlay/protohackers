namespace PestControl
{
    internal static class Protocol
    {
        internal static void SendHelloMessage(BinaryWriter? writer, string _identifier)
        {
            var message = new Message(MessageType.Hello);
            message.AddPayload(Constants.Protocol.Name);
            message.AddPayload(Constants.Protocol.Version);

            Console.WriteLine($"{_identifier,20} --> Hello [{Constants.Protocol.Name} {Constants.Protocol.Version}]");

            SendMessage(message, writer);
        }

        internal static void SendDialAuthorityMessage(uint site, BinaryWriter? writer, string _identifier)
        {
            var message = new Message(MessageType.DialAuthority);
            message.AddPayload(site);

            Console.WriteLine($"{_identifier,20} --> DialAuthority [{site}]");

            SendMessage(message, writer);
        }

        internal static void SendCreatePolicyMessage(string species, byte action, BinaryWriter? writer, string _identifier)
        {
            var message = new Message(MessageType.CreatePolicy);
            message.AddPayload(species);
            message.AddPayload(action);

            Console.WriteLine($"{_identifier,20} --> CreatePolicy [{species} {action}]");

            SendMessage(message, writer);
        }

        internal static void SendDeletePolicyMessage(uint id, BinaryWriter? writer, string _identifier)
        {
            var message = new Message(MessageType.DeletePolicy);
            message.AddPayload(id);

            Console.WriteLine($"{_identifier,20} --> DeletePolicy [{id}]");

            SendMessage(message, writer);
        }

        internal static void SendErrorMessage(string text, BinaryWriter? writer, string _identifier)
        {
            var message = new Message(MessageType.Error);
            message.AddPayload(text);

            Console.WriteLine($"{_identifier,20} --> Error [{text}]");

            SendMessage(message, writer);
        }

        internal static Message ReceiveMessage(BinaryReader? reader, string _identifier)
        {
            var type = reader?.ReadByte() ?? 0;
            var messageLengthBytes = reader?.ReadBytesExactly(4) ?? Array.Empty<byte>();
            if (messageLengthBytes.Length < 4)
                throw new InvalidDataException("Could not read the length of the message");

            var messageLength = Extensions.ToUInt32BigEndian(messageLengthBytes);
            if (messageLength > int.MaxValue)
                throw new InvalidDataException("The given length of the message does not have a valid value");

            var convertedMessageLength = Convert.ToInt32(messageLength);
            var payloadLength = convertedMessageLength - Constants.Message.Overhead;
            if (payloadLength < 0 || payloadLength > int.MaxValue)
                throw new InvalidDataException("The given length of the payload does not have a valid value");

            var payload = reader?.ReadBytesExactly(payloadLength) ?? Array.Empty<byte>();
            if (payload.Length != payloadLength)
                throw new InvalidDataException("The length of the payload is shorter than the given length");

            var checksum = reader?.ReadByte();
            var message = new Message(type, payload, messageLengthBytes);

            Console.WriteLine($"{_identifier,20} <-- {(MessageType)type}");

            if ((MessageType)type == MessageType.Error)
                throw new InvalidDataException("The message is of type error");

            if (checksum != message.Checksum)
                throw new InvalidDataException("The checksum of the message is incorrect");

            return message;
        }

        private static void SendMessage(Message? message, BinaryWriter? writer)
        {
            var builtMessage = message?.Build();
            if (builtMessage != null)
                writer?.Write(builtMessage);
        }
    }
}
