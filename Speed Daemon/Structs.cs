namespace SpeedDaemon
{
    /// <summary>
    /// Holds the data related to a reported observation of a certain car
    /// at a certain time on a certain road at a certain position
    /// </summary>
    internal readonly struct Observation
    {
        internal string Plate { get; init; }

        internal uint Timestamp { get; init; }

        internal ushort Road { get; init; }

        internal ushort Mile { get; init; }
    }

    /// <summary>
    /// Holds the data related to a ticket
    /// </summary>
    internal struct Ticket
    {
        internal string Plate { get; init; }

        internal ushort Road { get; init; }

        internal ushort Mile1 { get; init; }

        internal uint Timestamp1 { get; init; }

        internal ushort Mile2 { get; init; }

        internal uint Timestamp2 { get; init; }

        internal ushort Speed { get; init; }

        internal bool Sent { get; set; }
    }
}
