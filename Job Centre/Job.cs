using System.Text.Json;

namespace Job_Centre
{
    /// <summary>
    /// Holds all information connected to a job
    /// </summary>
    internal class Job
    {
        public JsonElement Payload { get; set; }

        public int Priority { get; set; }

        public int Id { get; set; }

        public int Client { get; set; }

        public string Queue { get; set; }

        public Job(JsonElement payload, int priority, int id, int client, string queue)
        {
            Payload = payload;
            Priority = priority;
            Id = id;
            Client = client;
            Queue = queue;
        }
    }
}
