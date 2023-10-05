using System.Text.Json;

namespace JobCentre
{
    internal sealed class ErrorMessage
    {
        internal static string Status => "error";

        internal string? Error { get; set; }
    }

    internal sealed class StatusMessage
    {
        internal string? Status { get; set; }

        internal int? Id { get; set; }

        internal JsonElement? Job { get; set; }

        internal int? Pri { get; set; }

        internal string? Queue { get; set; }
    }
}
