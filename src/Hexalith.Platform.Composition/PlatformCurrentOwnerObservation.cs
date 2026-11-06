namespace Hexalith.Platform.Composition;

using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Independently verifies the process-mapped owner bodies before native readiness is published.</summary>
internal static class PlatformCurrentOwnerObservation
{
    internal static async Task CaptureAsync(IServiceProvider services, PlatformCompositionOptions options, string catalogDirectory, CancellationToken token)
    {
        PlatformCurrentOwnerProof proof = options.CurrentOwnerProof ?? throw new InvalidDataException("Current owner authority is absent.");
        CompositionRunPlan plan = options.RunPlan!;
        if (!proof.Matches(options)) throw new InvalidDataException("Current owner inputs changed before readiness.");
        var notification = services.GetRequiredService<ResourceNotificationService>();
        if (!notification.TryGetCurrentState("parties", out ResourceEvent? resource)
            || !int.TryParse(resource.Snapshot.Properties.FirstOrDefault(property => property.Name == "executable.pid")?.Value?.ToString(), out int launcher))
            throw new InvalidDataException("The current Parties process identity is unavailable.");
        string output = Path.Combine(proof.Artifacts, "bin", "Hexalith.Parties", "release");
        var running = ReadRunningOwners(launcher, output, proof.Bodies);
        int pid = running.Process;
        IReadOnlyDictionary<string, string> runtime = running.Bodies;
        Dictionary<string, string> sdk = new(StringComparer.Ordinal);
        foreach (string name in new[] { "Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService" })
        {
            string path = Path.Combine(output, name + ".dll");
            string[] maps = File.ReadLines("/proc/" + pid + "/maps").Where(line => line.EndsWith(" " + path, StringComparison.Ordinal)).ToArray();
            if (maps.Length == 0) throw new InvalidDataException("The actual current owner SDK mapping is unavailable.");
            string digest = PlatformMappedArtifact.Hash(pid, path, maps[0]);
            Hexalith.Platform.CatalogArtifacts.PublishedCatalogArtifactCohort.VerifyCurrentSdkFile(path, digest);
            sdk[name] = digest;
        }
        var observed = JsonSerializer.Deserialize<RouteCatalogHostObservation>(await File.ReadAllBytesAsync(Path.Combine(catalogDirectory, "parties.observation.json"), token).ConfigureAwait(false))
            ?? throw new InvalidDataException("The current owner's independent catalog observation is absent.");
        if (observed.HostId != "parties" || observed.ExecutableOwners is null
            || observed.ExecutableOwners.SingleOrDefault(pair => new System.Reflection.AssemblyName(pair.Key).Name == "Hexalith.Parties").Value != proof.Bodies["Hexalith.Parties"])
            throw new InvalidDataException("The committed catalog does not bind the actual current owner implementation.");
        using JsonDocument reader = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(catalogDirectory, "current-reader-receipt.json"), token).ConfigureAwait(false));
        proof.VerifyReader(reader.RootElement);
        using JsonDocument scope = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(catalogDirectory, "native-scope.json"), token).ConfigureAwait(false));
        if (!proof.Matches(options)) throw new InvalidDataException("Current owner inputs changed before observation publication.");
        await File.WriteAllBytesAsync(Path.Combine(catalogDirectory, "current-owner-observation.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = PlatformCurrentOwnerProof.Profile, plan.RunId, plan.AttemptId, plan.EnvironmentId,
            revision = proof.SourceRevision,
            mappingHash = options.Mapping.ContentHash, configuration = "Release", qualificationCredit = false,
            platform = options.Mapping.PlatformIdentity, sdkVersion = "4.1.0", tenantsVersion = "7.0.5",
            routingCatalogRoot = scope.RootElement.GetProperty("RoutingCatalogRoot").GetString(),
            packageArchives = proof.PackageArchives,
            runtimeBodies = runtime, catalogBodies = proof.Bodies.Where(pair => pair.Key is "Hexalith.Parties.Contracts" or "Hexalith.Parties.EventStoreExtensions").ToDictionary(),
            sdkBodies = sdk,
            observedExecutableOwners = observed.ExecutableOwners, committedCatalogValidated = true,
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), token).ConfigureAwait(false);
    }

    /// <summary>Reads actual owned process mappings; this observation seam grants no runner or qualification authority.</summary>
    internal static (int Process, IReadOnlyDictionary<string, string> Bodies) ReadRunningOwners(int launcher, string output, IReadOnlyDictionary<string, string> expectedBodies)
    {
        int[] applications = ProcessTree(launcher).Where(process => File.ReadLines("/proc/" + process + "/maps")
            .Any(line => line.EndsWith(output + "/Hexalith.Parties.dll", StringComparison.Ordinal))).ToArray();
        if (applications.Length != 1) throw new InvalidDataException("The current owner executable is not uniquely mapped by the owned process.");
        int pid = applications[0];
        Dictionary<string, string> runtime = new(StringComparer.Ordinal);
        foreach (var expected in expectedBodies.Where(pair => pair.Key != "Hexalith.Parties.EventStoreExtensions"))
        {
            string path = Path.Combine(output, expected.Key + ".dll");
            string[] maps = File.ReadLines("/proc/" + pid + "/maps").Where(line => line.EndsWith(" " + path, StringComparison.Ordinal)).ToArray();
            if (maps.Length == 0) throw new InvalidDataException("The running owner differs from its measured Release body.");
            string actual = PlatformMappedArtifact.Hash(pid, path, maps[0]);
            if (actual != expected.Value || maps.Skip(1).Any(line => PlatformMappedArtifact.Hash(pid, path, line) != actual))
                throw new InvalidDataException("The running owner differs from its measured Release body.");
            runtime[expected.Key] = actual;
        }
        return (pid, runtime);
    }

    private static IEnumerable<int> ProcessTree(int owner)
    {
        Queue<int> pending = new();
        HashSet<int> seen = [];
        pending.Enqueue(owner);
        while (pending.Count > 0 && seen.Count < 64)
        {
            int process = pending.Dequeue();
            if (!seen.Add(process)) continue;
            yield return process;
            string children = "/proc/" + process + "/task/" + process + "/children";
            if (!File.Exists(children)) continue;
            foreach (string child in File.ReadAllText(children).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(child, out int pid) && pid > 0) pending.Enqueue(pid);
        }
    }
}
