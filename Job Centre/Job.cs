using System.Text.Json;

namespace JobCentre
{
    /// <summary>
    /// Holds all information connected to a job
    /// </summary>
    internal sealed class Job
    {
        internal JsonElement Payload { get; set; }

        internal int Priority { get; set; }

        internal int Id { get; set; }

        internal int Client { get; set; }

        internal string Queue { get; set; }

        internal Job(JsonElement payload, int priority, int id, int client, string queue)
        {
            Payload = payload;
            Priority = priority;
            Id = id;
            Client = client;
            Queue = queue;
        }
    }
}
