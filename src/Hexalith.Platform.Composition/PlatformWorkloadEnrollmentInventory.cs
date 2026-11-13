using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Platform.Composition;

/// <summary>The shared complete workload inventory used by producers and the protected consumer.</summary>
public static class PlatformWorkloadEnrollmentInventory
{
    private const string BindingPrefix = "eventstore:bound-";

    /// <summary>Builds probes for the final disposable cohort and its selected canonical tenant.</summary>
    /// <param name="builder">The final model before catalog capture.</param>
    /// <param name="selectedTenant">The actual selected fixture tenant.</param>
    /// <returns>A credential-free exact resource and probe inventory.</returns>
    public static JsonElement Create(IDistributedApplicationBuilder builder, string selectedTenant)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!TenantCanonicalizer.TryCanonicalize(selectedTenant, out string tenant) || tenant != selectedTenant) throw Refused();
        ProjectResource[] cohort = Cohort(builder);
        string AppId(string resource) => Workload(cohort.Single(item => item.Name == resource));
        var clients = new List<object>();
        foreach (ProjectResource resource in cohort.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            string workload = Workload(resource);
            var probes = new List<object>();
            void Probe(string audience, string operation, Dictionary<string, string> bindings)
                => probes.Add(new { grantId = "run-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(workload)))[..24] + "-" + probes.Count,
                    revision = 1, audience, operation, bindings });
            if (resource.Name == "eventstore")
            {
                foreach (string receiver in new[] { AppId("folders"), AppId("tenants") })
                    foreach (string operation in new[] { "process", "project", "metadata", "replay-state" })
                        Probe(receiver, "domain-service:" + operation, []);
                foreach ((string receiver, string domain, string storageTenant) in new[]
                {
                    (AppId("folders"), "folders", tenant),
                    (AppId("tenants"), "tenants", TenantCanonicalizer.ReservedStorageTenant),
                    (AppId("tenants"), "global-administrators", TenantCanonicalizer.ReservedStorageTenant),
                })
                    Probe(receiver, "domain-service:query", new() { [BindingPrefix + "tenant"] = storageTenant, [BindingPrefix + "domain"] = domain,
                        [BindingPrefix + "query-authority-v1"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(workload + "|" + domain + "|" + storageTenant))) });
            }
            else if (resource.Name == "tenants")
            {
                Probe(AppId("eventstore"), "eventstore:command-submit", []);
                Probe(AppId("eventstore"), "eventstore:stream-read", []);
            }
            else
                Probe(AppId("eventstore"), "eventstore:stream-read", new() { [BindingPrefix + "tenant"] = tenant, [BindingPrefix + "domain"] = "folders" });
            clients.Add(new { resource = resource.Name, clientId = workload, workload, probes });
        }
        JsonElement inventory = JsonSerializer.SerializeToElement(new { tenant, receivers = cohort.Select(item => item.Name).Order(StringComparer.Ordinal), clients });
        Validate(cohort, [.. inventory.GetProperty("clients").EnumerateArray()], tenant);
        return inventory;
    }

    /// <summary>Reads the actual Dapr caller identity of one physical resource.</summary>
    /// <param name="resource">The enrolled host.</param>
    /// <returns>The actual caller identifier.</returns>
    public static string Workload(ProjectResource resource) => resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar
        .Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.AppId ?? throw Refused();

    /// <summary>Gets every physical enrolled resource from the final application model.</summary>
    /// <param name="builder">The final application model.</param>
    /// <returns>The complete physical cohort, including replicas and proof callers.</returns>
    public static ProjectResource[] Cohort(IDistributedApplicationBuilder builder) => [.. builder.Resources.OfType<ProjectResource>().Where(project =>
        project.Name is "eventstore" or "tenants" or "folders" or "folders-replica"
        || project.Name.StartsWith("folders-workers", StringComparison.Ordinal))];

    /// <summary>Checks the exact source, callback, bootstrap and bound query inventory.</summary>
    /// <param name="cohort">Every physical enrolled resource.</param>
    /// <param name="clients">The protected client probe inventory.</param>
    /// <param name="selectedTenant">The fixture tenant, when selected.</param>
    public static void Validate(ProjectResource[] cohort, JsonElement[] clients, string? selectedTenant)
    {
        JsonElement Client(string resource) => clients.Single(client => Text(client, "resource") == resource);
        static string AppId(ProjectResource resource) => resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar
            .Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.AppId ?? throw Refused();
        static bool Pair(JsonElement probe, string audience, string operation) => Text(probe, "audience") == audience && Text(probe, "operation") == operation;
        static bool Unbound(JsonElement probe) => probe.GetProperty("bindings").ValueKind == JsonValueKind.Object && !probe.GetProperty("bindings").EnumerateObject().Any();
        static bool Query(JsonElement probe, string domain, string? tenant = null)
        {
            JsonElement binding = probe.GetProperty("bindings");
            if (binding.ValueKind != JsonValueKind.Object || binding.EnumerateObject().Count() != 3
                || Text(binding, BindingPrefix + "domain") != domain || !binding.TryGetProperty(BindingPrefix + "tenant", out JsonElement boundTenant)
                || !binding.TryGetProperty(BindingPrefix + "query-authority-v1", out JsonElement digest) || !Hex(digest.GetString())) return false;
            return !string.IsNullOrWhiteSpace(boundTenant.GetString()) && !boundTenant.GetString()!.Contains('*') && (tenant is null || boundTenant.GetString() == tenant);
        }
        string gatewayAudience = AppId(cohort.Single(resource => resource.Name == "eventstore"));
        string SourceTenant(JsonElement probe)
        {
            JsonElement bindings = probe.GetProperty("bindings");
            if (!Pair(probe, gatewayAudience, "eventstore:stream-read") || bindings.ValueKind != JsonValueKind.Object
                || bindings.EnumerateObject().Count() != 2 || Text(bindings, BindingPrefix + "domain") != "folders") throw Refused();
            string tenant = Text(bindings, BindingPrefix + "tenant");
            if (string.IsNullOrWhiteSpace(tenant) || tenant.Contains('*') || tenant != tenant.Trim()) throw Refused();
            return tenant;
        }
        string[] tenants = [.. Client("folders").GetProperty("probes").EnumerateArray().Select(SourceTenant).Order(StringComparer.Ordinal)];
        if (tenants.Length == 0 || tenants.Distinct(StringComparer.Ordinal).Count() != tenants.Length
            || selectedTenant is not null && !tenants.Contains(selectedTenant, StringComparer.Ordinal)) throw Refused();
        foreach (ProjectResource source in cohort.Where(resource => resource.Name is "folders" or "folders-replica"
            || resource.Name.StartsWith("folders-workers", StringComparison.Ordinal)))
            if (!Client(source.Name).GetProperty("probes").EnumerateArray().Select(SourceTenant).Order(StringComparer.Ordinal).SequenceEqual(tenants, StringComparer.Ordinal)) throw Refused();
        JsonElement[] bootstrap = [.. Client("tenants").GetProperty("probes").EnumerateArray()];
        if (bootstrap.Length != 2 || bootstrap.Count(probe => Pair(probe, gatewayAudience, "eventstore:command-submit") && Unbound(probe)) != 1
            || bootstrap.Count(probe => Pair(probe, gatewayAudience, "eventstore:stream-read") && Unbound(probe)) != 1) throw Refused();
        JsonElement[] gateway = [.. Client("eventstore").GetProperty("probes").EnumerateArray()];
        string folders = AppId(cohort.Single(resource => resource.Name == "folders")), owner = AppId(cohort.Single(resource => resource.Name == "tenants"));
        string[] callbacks = ["domain-service:process", "domain-service:project", "domain-service:metadata", "domain-service:replay-state"];
        foreach (string audience in new[] { folders, owner })
            foreach (string operation in callbacks)
                if (gateway.Count(probe => Pair(probe, audience, operation) && Unbound(probe)) != 1) throw Refused();
        foreach (string tenant in tenants)
            if (gateway.Count(probe => Pair(probe, folders, "domain-service:query") && Query(probe, "folders", tenant)) != 1) throw Refused();
        foreach (string domain in new[] { "tenants", "global-administrators" })
            if (gateway.Count(probe => Pair(probe, owner, "domain-service:query") && Query(probe, domain, TenantCanonicalizer.ReservedStorageTenant)) != 1) throw Refused();
        if (gateway.Any(probe => !((Text(probe, "audience") == folders || Text(probe, "audience") == owner)
            && callbacks.Contains(Text(probe, "operation"), StringComparer.Ordinal) && Unbound(probe))
            && !(Pair(probe, folders, "domain-service:query") && tenants.Any(tenant => Query(probe, "folders", tenant)))
            && !(Pair(probe, owner, "domain-service:query") && (Query(probe, "tenants", TenantCanonicalizer.ReservedStorageTenant) || Query(probe, "global-administrators", TenantCanonicalizer.ReservedStorageTenant))))) throw Refused();
    }

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? throw Refused();
    private static bool Hex(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static InvalidOperationException Refused() => new("Protected workload enrollment verification refused. Exact complete cohort inventory is required.");
}
