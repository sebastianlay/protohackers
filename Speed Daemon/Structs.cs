namespace SpeedDaemon
{
    /// <summary>
    /// Holds the data related to a reported observation of a certain car
    /// at a certain time on a certain road at a certain position
    /// </summary>
    public struct Observation
    {
        public string Plate { get; init; }

        public uint Timestamp { get; init; }

        public ushort Road { get; init; }

        public ushort Mile { get; init; }
    }

    /// <summary>
    /// Holds the data related to a ticket
    /// </summary>
    public struct Ticket
    {
        public string Plate { get; init; }

        public ushort Road { get; init; }

        public ushort Mile1 { get; init; }

        public uint Timestamp1 { get; init; }

        public ushort Mile2 { get; init; }

        public uint Timestamp2 { get; init; }

        public ushort Speed { get; init; }

        public bool Sent { get; set; }
    }
}