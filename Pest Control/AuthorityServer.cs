using System.Collections.Concurrent;

namespace PestControl
{
    internal sealed class AuthorityServer
    {
        private static readonly BlockingCollection<SiteVisit> siteVisits = new();
        private static readonly ConcurrentDictionary<uint, AuthorityServerConnection> connections = new();

        internal static void AddSiteVisit(SiteVisit siteVisit)
        {
            siteVisits.Add(siteVisit);
        }

        internal static void HandleSiteVisits()
        {
            foreach (var siteVisit in siteVisits.GetConsumingEnumerable())
                HandleSiteVisit(siteVisit);
        }

        internal static void CloseConnection(uint site)
        {
            if (connections.TryRemove(site, out var connection))
                connection.Dispose();
        }

        private static void HandleSiteVisit(SiteVisit siteVisit)
        {
            var targetPopulations = GetTargetPopulations(siteVisit.Site);
            if (targetPopulations == null)
                return;

            var reportedPopulations = siteVisit.ReportedPopulations;
            foreach (var targetPopulation in targetPopulations)
            {
                var species = targetPopulation.Key;
                if (reportedPopulations.TryGetValue(species, out var reportedPopulation))
                {
                    var min = targetPopulation.Value.Min;
                    var max = targetPopulation.Value.Max;
                    var count = reportedPopulation.Count;

                    if (count < min)
                        CreatePolicy(siteVisit.Site, species, Constants.Action.Conserve);
                    else if (count > max)
                        CreatePolicy(siteVisit.Site, species, Constants.Action.Cull);
                    else
                        DeletePolicy(siteVisit.Site, species);
                }
                else
                {
                    CreatePolicy(siteVisit.Site, species, Constants.Action.Conserve);
                }
            }
        }

        private static IReadOnlyDictionary<string, TargetPopulation>? GetTargetPopulations(uint site)
        {
            var connection = GetConnection(site);
            return connection.GetTargetPopulations();
        }

        private static void CreatePolicy(uint site, string species, byte action)
        {
            DeletePolicy(site, species);

            var connection = GetConnection(site);
            connection.CreatePolicy(species, action);
        }

        private static void DeletePolicy(uint site, string species)
        {
            var connection = GetConnection(site);
            connection.DeletePolicy(species);
        }

        private static AuthorityServerConnection GetConnection(uint site)
        {
            return connections.GetOrAdd(site, _ => new AuthorityServerConnection(site, $"server {site}"));
        }
    }
}
