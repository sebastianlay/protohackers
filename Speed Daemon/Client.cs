using System.Net.Sockets;

namespace SpeedDaemon
{
    /// <summary>
    /// Handles incoming and outgoing messages for a client
    /// </summary>
    internal sealed class Client : IDisposable
    {
        internal Client(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
            NetworkStream stream = tcpClient.GetStream();
            reader = new(stream);
            writer = new(stream);
        }

        private readonly TcpClient tcpClient;

        private readonly BinaryReader reader;

        private readonly BinaryWriter writer;

        internal bool IsCamera { get; private set; }

        internal bool IsDispatcher { get; private set; }

        internal ushort? Road { get; private set; }

        internal ushort? Mile { get; private set; }

        internal ushort[]? Roads { get; private set; }

        internal Timer? Heartbeat { get; private set; }

        /// <summary>
        /// Reads the first byte of the next message and determines
        /// how to handle the rest of the incoming message
        /// </summary>
        internal void HandleNextMessage()
        {
            var operation = Helper.ReadU8(reader);
            switch (operation)
            {
                case 0x20: // Plate
                    HandlePlateMessage();
                    break;

                case 0x40: // Heartbeat
                    HandleHeartbeatMessage();
                    break;

                case 0x80: // IAmCamera
                    HandleCameraMessage();
                    break;

                case 0x81: // IAmDispatcher
                    HandleDispatcherMessage();
                    break;

                default: // unknown
                    SendErrorMessage("You sent an invalid message format");
                    break;
            }
        }

        /// <summary>
        /// Handles the message where a camera reports the
        /// observation of a car plate at a given timestamp
        /// </summary>
        private void HandlePlateMessage()
        {
            var plate = Helper.ReadString(reader);
            var timestamp = Helper.ReadU32(reader);

            if (!IsCamera || Road == null || Mile == null)
            {
                SendErrorMessage("You have not identified as a camera.");
                return;
            }

            var observation = new Observation()
            {
                Plate = plate,
                Timestamp = timestamp,
                Road = Road.Value,
                Mile = Mile.Value
            };

            RuleEngine.AddObservation(observation);
        }

        /// <summary>
        /// Handles the message where a client requests a given interval
        /// for heartbeat messages to keep the connection alive
        /// </summary>
        private void HandleHeartbeatMessage()
        {
            if (Heartbeat != null)
            {
                SendErrorMessage("You have already requested hearbeats.");
                return;
            }

            var interval = Helper.ReadU32(reader);
            if (interval == 0)
                return;

            Heartbeat = new Timer(SendHeartbeatMessage, null, 0, interval * 100);
        }

        /// <summary>
        /// Handles the message where a client tries to identify itself as a camera
        /// </summary>
        private void HandleCameraMessage()
        {
            if (IsCamera || IsDispatcher)
            {
                SendErrorMessage("You have already identified as a camera or a dispatcher.");
                return;
            }

            IsCamera = true;
            var road = Helper.ReadU16(reader);
            Road = road;
            Mile = Helper.ReadU16(reader);
            var limit = Helper.ReadU16(reader);

            RuleEngine.SpeedLimits.AddOrUpdate(road, _ => limit, (_, _) => limit);
        }

        /// <summary>
        /// Handles the message where a client tries to identify itself as a dispatcher
        /// </summary>
        private void HandleDispatcherMessage()
        {
            if (IsCamera || IsDispatcher)
            {
                SendErrorMessage("You have already identified as a camera or a dispatcher.");
                return;
            }

            IsDispatcher = true;
            var numroads = Helper.ReadU8(reader);
            Roads = new ushort[numroads];
            for (int i = 0; i < numroads; i++)
                Roads[i] = Helper.ReadU16(reader);

            RuleEngine.CheckStoredTickets(Roads);
        }

        /// <summary>
        /// Sends the given error message to the client and closes the connection
        /// </summary>
        /// <param name="message"></param>
        private void SendErrorMessage(string message)
        {
            Helper.Write(writer, 0x10);
            Helper.Write(writer, message);
            Close();
        }

        /// <summary>
        /// Sends the given ticket to the client
        /// </summary>
        /// <param name="ticket"></param>
        internal void SendTicketMessage(Ticket ticket)
        {
            Helper.Write(writer, 0x21);
            Helper.Write(writer, ticket.Plate);
            Helper.Write(writer, ticket.Road);
            Helper.Write(writer, ticket.Mile1);
            Helper.Write(writer, ticket.Timestamp1);
            Helper.Write(writer, ticket.Mile2);
            Helper.Write(writer, ticket.Timestamp2);
            Helper.Write(writer, ticket.Speed);
            ticket.Sent = true;
        }

        /// <summary>
        /// Sends a heartbeat message to the client
        /// </summary>
        /// <param name="state"></param>
        private void SendHeartbeatMessage(object? state)
        {
            Helper.Write(writer, 0x41);
        }

        /// <summary>
        /// Closes the connection to the client and releases all resources
        /// </summary>
        internal void Close()
        {
            Heartbeat?.Dispose();
            reader.Close();
            writer.Close();
            tcpClient.Close();
        }

        /// <summary>
        /// Disposes the client
        /// </summary>
        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }
    }
}
