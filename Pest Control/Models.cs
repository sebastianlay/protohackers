namespace PestControl
{
    internal sealed class SiteVisit
    {
        internal required uint Site { get; init; }
        internal required IReadOnlyDictionary<string, ReportedPopulation> ReportedPopulations { get; init; }

        public override string ToString()
        {
            return $"Site: {Site} / ReportedPopulations: {ReportedPopulations.Count}";
        }
    }

    internal readonly struct ReportedPopulation
    {
        internal required int Count { get; init; }

        public override string ToString()
        {
            return $"Count: {Count}";
        }
    }

    internal readonly struct TargetPopulation
    {
        internal readonly int Min { get; init; }
        internal readonly int Max { get; init; }

        public override string ToString()
        {
            return $"Min: {Min} / Max: {Max}";
        }
    }

    internal readonly struct Policy
    {
        internal readonly uint Id { get; init; }

        public override string ToString()
        {
            return $"Id: {Id}";
        }
    }
}
