using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobCentre
{
    internal static class JobServer
    {
        // use a global lock for all operations on jobs and job collections
        // (since .NET does not have a built-in thread-safe sorted collection and I am not going to write one)
        private static readonly object globalLock = new();

        // divide jobs into "unassigned" and "assigned" for fast retrieval during a "get" request
        // (since the "get" request only cares about unassigned jobs)
        private static readonly SortedSet<Job> UnassignedJobs = new(new JobPriorityComparer());
        private static readonly Dictionary<int, Job> AssignedJobs = new();

        private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromMilliseconds(1);
        private static readonly TimeSpan WaitForJobTimeout = TimeSpan.FromMilliseconds(500);

        // keep track of the assigned client IDs and job IDs
        private static int MaxJobId;
        private static int MaxClientId;

        /// <summary>
        /// Main entry point for a new incoming TCP connection
        /// </summary>
        /// <param name="client"></param>
        internal static async Task HandleConnectionAsync(TcpClient client)
        {
            using var stream = client.GetStream();
            using var streamReader = new StreamReader(stream);

            var clientId = Interlocked.Increment(ref MaxClientId);

            Console.WriteLine($"Client {clientId} connected");

            try
            {
                while (client.Connected)
                {
                    // check explicitely if the connection is still open
                    // (since for some reason there is no exception when the connection is closed from the client-side)
                    if (client.Client.Poll(ConnectionTimeout, SelectMode.SelectRead) && !stream.DataAvailable)
                        break;

                    // read the incoming line
                    var line = await streamReader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    // output incoming line
                    Console.WriteLine($"<-- {line.Replace("\n", "\\n")}");

                    // parse the line as JSON
                    var memory = line.AsMemory();
                    JsonDocument document;
                    try
                    {
                        document = JsonDocument.Parse(memory);
                    }
                    catch (Exception)
                    {
                        await SendErrorMessageAsync("Request did contain malformed JSON", stream);
                        continue;
                    }

                    if (!document.RootElement.TryGetProperty("request", out var request))
                    {
                        await SendErrorMessageAsync("Request did not contain a request field", stream);
                        continue;
                    }

                    // handle the request according to the "request" field
                    switch (request.GetString())
                    {
                        case "put":
                            await HandlePutRequestAsync(document.RootElement, stream);
                            break;
                        case "get":
                            await HandleGetRequestAsync(document.RootElement, clientId, stream);
                            break;
                        case "delete":
                            await HandleDeleteRequestAsync(document.RootElement, stream);
                            break;
                        case "abort":
                            await HandleAbortRequestAsync(document.RootElement, clientId, stream);
                            break;
                        default:
                            await SendErrorMessageAsync("Request did not contain a valid request field", stream);
                            continue;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                client.Close();
            }

            UnassignJobsForClient(clientId);

            Console.WriteLine($"Client {clientId} disconnected");
        }

        /// <summary>
        /// Removes the disconnected client from all jobs that he was working on
        /// </summary>
        /// <param name="clientId"></param>
        private static void UnassignJobsForClient(int clientId)
        {
            lock (globalLock)
            {
                var jobsToUpdate = AssignedJobs.Where(job => job.Value.Client == clientId);
                foreach (var job in jobsToUpdate)
                {
                    job.Value.Client = -1;
                    AssignedJobs.Remove(job.Key);
                    UnassignedJobs.Add(job.Value);
                }
            }
        }

        /// <summary>
        /// Handles the request when the "request" field contains "put"
        /// </summary>
        /// <param name="rootElement"></param>
        /// <param name="stream"></param>
        private static async Task HandlePutRequestAsync(JsonElement rootElement, NetworkStream stream)
        {
            if (!rootElement.TryGetProperty("queue", out var queueIdField))
            {
                await SendErrorMessageAsync("Request did not contain a queue field", stream);
                return;
            }

            var queueId = queueIdField.GetString();
            if (string.IsNullOrEmpty(queueId))
            {
                await SendErrorMessageAsync("Request did not contain a valid queue field", stream);
                return;
            }

            if (!rootElement.TryGetProperty("job", out var element))
            {
                await SendErrorMessageAsync("Request did not contain a valid job field", stream);
                return;
            }

            if (!rootElement.TryGetProperty("pri", out var priorityField))
            {
                await SendErrorMessageAsync("Request did not contain a priority field", stream);
                return;
            }

            if (!priorityField.TryGetInt32(out var priority))
            {
                await SendErrorMessageAsync("Request did not contain a valid priority field", stream);
                return;
            }

            // create a new job from the given information
            var jobId = Interlocked.Increment(ref MaxJobId);
            var job = new Job(element, priority, jobId, -1, queueId);

            // and add the job to the unassigned jobs
            lock (globalLock)
                UnassignedJobs.Add(job);

            await SendStatusMessageAsync("ok", stream, jobId);
        }

        /// <summary>
        /// Handles the request when the "request" field contains "get"
        /// </summary>
        /// <param name="rootElement"></param>
        /// <param name="clientId"></param>
        /// <param name="stream"></param>
        private static async Task HandleGetRequestAsync(JsonElement rootElement, int clientId, NetworkStream stream)
        {
            // retrieve value of the optional "wait" field
            var wait = false;
            var waitWasFound = rootElement.TryGetProperty("wait", out var waitField);
            if (waitWasFound)
                wait = waitField.GetBoolean();

            // retrieve IDs of the requested queues
            if (!rootElement.TryGetProperty("queues", out var queuesField))
            {
                await SendErrorMessageAsync("Request did not contain a queues field", stream);
                return;
            }

            var queues = queuesField.EnumerateArray();
            var queueIds = queues.Select(q => q.GetString()).WhereNotNull().ToHashSet();

            // try to find the next job for the given queues
            var hasNextJob = await AssignNextJobAsync(queueIds, clientId, stream);
            if (hasNextJob)
                return;

            // return early if the client does not want to wait for a next job
            if (!wait)
            {
                await SendStatusMessageAsync("no-job", stream);
                return;
            }

            // otherwise wait for a little bit and try again
            while (!hasNextJob)
            {
                await Task.Delay(WaitForJobTimeout);
                hasNextJob = await AssignNextJobAsync(queueIds, clientId, stream);
            }
        }

        /// <summary>
        /// Tries to find a suitable job for the client to work on and assign the client if a job was found
        /// </summary>
        /// <param name="queueIds"></param>
        /// <param name="clientId"></param>
        /// <param name="stream"></param>
        /// <returns>Whether a job was found or not</returns>
        private static async Task<bool> AssignNextJobAsync(HashSet<string> queueIds, int clientId, NetworkStream stream)
        {
            Job? nextJob = null;
            lock (globalLock)
            {
                nextJob = UnassignedJobs.FirstOrDefault(job => queueIds.Contains(job.Queue));
                if (nextJob != null)
                {
                    nextJob.Client = clientId;
                    UnassignedJobs.Remove(nextJob);
                    AssignedJobs.Add(nextJob.Id, nextJob);
                }
            }

            if (nextJob != null)
            {
                await SendStatusMessageAsync("ok", stream, nextJob.Id, nextJob.Payload, nextJob.Priority, nextJob.Queue);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles the request when the "request" field contains "delete"
        /// </summary>
        /// <param name="rootElement"></param>
        /// <param name="stream"></param>
        private static async Task HandleDeleteRequestAsync(JsonElement rootElement, NetworkStream stream)
        {
            if (!rootElement.TryGetProperty("id", out var idField))
            {
                await SendErrorMessageAsync("Request did not contain an id field", stream);
                return;
            }

            if (!idField.TryGetInt32(out var id))
            {
                await SendErrorMessageAsync("Request did not contain a valid id field", stream);
                return;
            }

            // delete the job either from the assigned jobs or the unassigned jobs
            Job? jobToDelete = null;
            var successfullyDeleted = false;
            lock (globalLock)
            {
                if (AssignedJobs.Remove(id, out jobToDelete))
                {
                    successfullyDeleted = true;
                }
                else
                {
                    jobToDelete = UnassignedJobs.FirstOrDefault(job => job.Id == id);
                    if (jobToDelete != null)
                        successfullyDeleted = UnassignedJobs.Remove(jobToDelete);
                }
            }

            if (jobToDelete == null || !successfullyDeleted)
            {
                await SendStatusMessageAsync("no-job", stream);
                return;
            }

            await SendStatusMessageAsync("ok", stream);
        }

        /// <summary>
        /// Handles the request when the "request" field contains "abort"
        /// </summary>
        /// <param name="rootElement"></param>
        /// <param name="clientId"></param>
        /// <param name="stream"></param>
        private static async Task HandleAbortRequestAsync(JsonElement rootElement, int clientId, NetworkStream stream)
        {
            if (!rootElement.TryGetProperty("id", out var idField))
            {
                await SendErrorMessageAsync("Request did not contain an id field", stream);
                return;
            }

            if (!idField.TryGetInt32(out var id))
            {
                await SendErrorMessageAsync("Request did not contain a valid id field", stream);
                return;
            }

            // try to find the job that should be aborted by ID
            Job? jobToAbort = null;
            lock (globalLock)
            {
                if (!AssignedJobs.TryGetValue(id, out jobToAbort))
                    jobToAbort = UnassignedJobs.FirstOrDefault(job => job.Id == id);
            }

            if (jobToAbort == null)
            {
                await SendStatusMessageAsync("no-job", stream);
                return;
            }

            if (jobToAbort.Client == -1 || jobToAbort.Client != clientId)
            {
                await SendErrorMessageAsync("Client that requested abort was not working on it", stream);
                return;
            }

            // abort the job by moving it from the assigned jobs to the unassigned jobs
            lock (globalLock)
            {
                jobToAbort.Client = -1;
                AssignedJobs.Remove(jobToAbort.Id);
                UnassignedJobs.Add(jobToAbort);
            }

            await SendStatusMessageAsync("ok", stream);
        }

        /// <summary>
        /// Formats the given "status" message as a JSON error message and sends it to the given stream
        /// </summary>
        /// <param name="message"></param>
        /// <param name="stream"></param>
        /// <param name="id"></param>
        /// <param name="job"></param>
        /// <param name="pri"></param>
        /// <param name="queue"></param>
        private static async Task SendStatusMessageAsync(string message, NetworkStream stream,
            int? id = null, JsonElement? job = null, int? pri = null, string? queue = null)
        {
            var statusMessage = new StatusMessage
            {
                Status = message,
                Id = id,
                Job = job,
                Pri = pri,
                Queue = queue
            };
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var serializedMessage = JsonSerializer.Serialize(statusMessage, jsonSerializerOptions);
            await SendSerializedMessageAsync(serializedMessage, stream);
        }

        /// <summary>
        /// Formats the given "error" message as a JSON error message and sends it to the given stream
        /// </summary>
        /// <param name="message"></param>
        /// <param name="stream"></param>
        private static async Task SendErrorMessageAsync(string message, NetworkStream stream)
        {
            var errorMessage = new ErrorMessage
            {
                Error = message
            };
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var serializedMessage = JsonSerializer.Serialize(errorMessage, jsonSerializerOptions);
            await SendSerializedMessageAsync(serializedMessage, stream);
        }

        /// <summary>
        /// Writes a serialized JSON message to the given stream
        /// </summary>
        /// <param name="message"></param>
        /// <param name="stream"></param>
        private static async Task SendSerializedMessageAsync(string message, NetworkStream stream)
        {
            // output outgoing line
            Console.WriteLine($"--> {message.Replace("\n", "\\n")}");

            // terminate the JSON with a newline character and write it to the stream
            message += '\n';
            var bytes = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(bytes);
        }

        /// <summary>
        /// Filters out the null values from an enumerable
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <returns>The enumerable without the null values</returns>
        private static IEnumerable<TSource> WhereNotNull<TSource>(this IEnumerable<TSource?> source)
        {
            foreach (var element in source)
            {
                if (element != null)
                    yield return element;
            }
        }
    }

    /// <summary>
    /// Comparer that sorts Jobs based on their priority (in descending order)
    /// </summary>
    internal sealed class JobPriorityComparer : IComparer<Job>
    {
        int IComparer<Job>.Compare(Job? x, Job? y)
        {
            if (x == null && y == null)
                return 0;

            if (x == null)
                return -1;

            if (y == null)
                return 1;

            var result = -x.Priority.CompareTo(y.Priority);

            // if the priority is equal, sort by ID
            // (since the SortedSet does not allow two items with the same sort "value")
            if (result == 0)
                return x.Id - y.Id;

            return result;
        }
    }
}
