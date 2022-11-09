using System.Collections.Concurrent;

namespace SpeedDaemon
{
    /// <summary>
    /// Saves the reported data in a central place and determines
    /// whether tickets have to be issued based on incoming data
    /// </summary>
    public static class RuleEngine
    {
        private static readonly ConcurrentDictionary<(string, ushort), List<Observation>> observations = new();
        private static readonly ConcurrentDictionary<Guid, Ticket> tickets = new();

        public static ConcurrentDictionary<ushort, ushort> SpeedLimits { get; set; } = new();

        private static readonly object observationsLock = new();

        /// <summary>
        /// Saves an observation and compare it with
        /// all previously submitted observations
        /// </summary>
        /// <param name="submittedObservation"></param>
        public static void AddObservation(Observation submittedObservation)
        {
            lock (observationsLock)
            {
                var plate = submittedObservation.Plate;
                var road = submittedObservation.Road;
                observations.TryGetValue((plate, road), out var comparedObservations);
                if (comparedObservations?.Any() == true)
                {
                    foreach (var comparedObservation in comparedObservations)
                    {
                        if (submittedObservation.Timestamp < comparedObservation.Timestamp)
                            CompareObservations(submittedObservation, comparedObservation);
                        else
                            CompareObservations(comparedObservation, submittedObservation);
                    }
                }

                observations.AddOrUpdate(
                    (submittedObservation.Plate, submittedObservation.Road),
                    new List<Observation>() { submittedObservation },
                    (_, observations) => observations.Append(submittedObservation).ToList()
                );
            }
        }

        /// <summary>
        /// Compares two given observations and issue a new ticket
        /// if they indicate a traffic infraction
        /// </summary>
        /// <param name="earlier"></param>
        /// <param name="later"></param>
        private static void CompareObservations(Observation earlier, Observation later)
        {
            double timeInSeconds = later.Timestamp - earlier.Timestamp;
            var timeInHours = timeInSeconds / 3600;
            var distanceInMiles = Math.Abs(later.Mile - earlier.Mile);
            var speed = distanceInMiles / timeInHours;
            var roundedSpeed = Math.Round(speed);
            var limit = SpeedLimits[earlier.Road];

            if (roundedSpeed < limit + 0.5)
                return;

            var convertedSpeed = (ushort)(roundedSpeed * 100);
            var ticket = new Ticket()
            {
                Plate = earlier.Plate,
                Road = earlier.Road,
                Mile1 = earlier.Mile,
                Timestamp1 = earlier.Timestamp,
                Mile2 = later.Mile,
                Timestamp2 = later.Timestamp,
                Speed = convertedSpeed,
                Sent = false
            };

            if (AlreadyHasTicketOnTheseDays(ticket))
                return;

            var ticketGuid = Guid.NewGuid();
            tickets.TryAdd(ticketGuid, ticket);

            var dispatcher = GetDispatcherFor(ticket.Road);
            dispatcher?.SendTicketMessage(ticket);
        }

        /// <summary>
        /// Returns whether a ticket has already been issued for a given
        /// car on the given days (as a ticket can span two days)
        /// </summary>
        /// <param name="possibleTicket"></param>
        /// <returns></returns>
        private static bool AlreadyHasTicketOnTheseDays(Ticket possibleTicket)
        {
            var day1 = GetDayFor(possibleTicket.Timestamp1);
            var day2 = GetDayFor(possibleTicket.Timestamp2);
            var relevantTickets = tickets.Where(ticket => ticket.Value.Plate == possibleTicket.Plate);
            foreach (var relevantTicket in relevantTickets)
            {
                var relevantDay1 = GetDayFor(relevantTicket.Value.Timestamp1);
                var relevantDay2 = GetDayFor(relevantTicket.Value.Timestamp2);
                if (day1 == relevantDay1 || day1 == relevantDay2 || day2 == relevantDay1 || day2 == relevantDay2)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks whether there are any relevant deferred tickets
        /// for a newly registered dispatcher on the given roads
        /// </summary>
        /// <param name="roads"></param>
        /// <returns></returns>
        public static void CheckStoredTickets(ushort[] roads)
        {
            foreach (var road in roads)
            {
                var relevantTickets = tickets.Where(ticket => !ticket.Value.Sent && ticket.Value.Road == road);
                if (!relevantTickets.Any())
                    continue;

                var dispatcher = GetDispatcherFor(road);
                if (dispatcher == null)
                    continue;

                foreach (var relevantTicket in relevantTickets)
                    dispatcher.SendTicketMessage(relevantTicket.Value);
            }
        }

        /// <summary>
        /// Returns a dispatcher that is responsible for the given road
        /// </summary>
        /// <param name="road"></param>
        /// <returns></returns>
        private static Client? GetDispatcherFor(ushort road)
        {
            return Program.Clients.Find(client => client.IsDispatcher && client.Roads?.Contains(road) == true);
        }

        /// <summary>
        /// Converts a timestamp to the number of the day (since 1st of January 1970)
        /// </summary>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        private static uint GetDayFor(uint timestamp)
        {
            return Convert.ToUInt32(Math.Floor((double)timestamp / 86400));
        }
    }
}