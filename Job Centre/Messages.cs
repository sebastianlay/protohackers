using System.Text.Json;

namespace Job_Centre
{
    internal class ErrorMessage
    {
        public string Status => "error";

        public string? Error { get; set; }
    }

    internal class StatusMessage
    {
        public string? Status { get; set; }

        public int? Id { get; set; }

        public JsonElement? Job { get; set; }

        public int? Pri { get; set; }

        public string? Queue { get; set; }
    }
}
