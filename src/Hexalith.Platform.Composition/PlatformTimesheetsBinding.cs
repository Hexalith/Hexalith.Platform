namespace Hexalith.Platform.Composition;

using System.Globalization;
using System.Security.Cryptography;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

/// <summary>Development host inputs only; the retained candidate grants no qualified providers.</summary>
internal static class PlatformTimesheetsBinding
{
    /// <summary>The executable identity under the selected Timesheets source root.</summary>
    internal const string Project = "src/Hexalith.Timesheets/Hexalith.Timesheets.csproj";
    /// <summary>The hardcoded owner read-model component identity.</summary>
    internal const string Component = "timesheets-readmodels";

    /// <summary>Refuses implicit activation and unsupported release modes before preparation.</summary>
    internal static void ValidateMode(PlatformCompositionOptions options)
    {
        if (options.ActiveModule == "timesheets" && !options.TimesheetsEnabled)
            throw new InvalidDataException("Hexalith.Timesheets: timesheets/host: Platform:Timesheets:Enabled: explicit Development/source opt-in is required.");
        if (options.TimesheetsEnabled && options.Mapping.Mode != WorkspaceMode.Source)
            throw new InvalidDataException("Hexalith.Timesheets: Platform:Timesheets:Enabled requires Development/source mode. A qualified release and production profile are unavailable.");
    }

    /// <summary>Uses the hash-bound candidate selection as the durable Builds source-run opt-in.</summary>
    internal static bool SelectRun(CompositionRunPlan plan, string platformRoot, bool ambientEnabled)
    {
        if (plan.PlatformActiveModule != "timesheets")
        {
            if (ambientEnabled)
                throw new InvalidDataException("Hexalith.Timesheets: an ambient opt-in cannot expand another Builds plan; select the retained Timesheets source candidate.");
            return false;
        }
        if (plan.SourceMapping?.Mode != WorkspaceMode.Source)
            throw new InvalidDataException("Hexalith.Timesheets: the selected Builds plan requires source mode; a qualified release and production profile are unavailable.");
        string candidate = Path.GetFullPath(Path.Combine(platformRoot, "eng", "timesheets-deployment", "hexalith.module-manifest.v2.json"));
        if (plan.PlatformDeclarationPaths.Count != 1 || Path.GetFullPath(plan.PlatformDeclarationPaths[0]) != candidate || !File.Exists(candidate))
            throw new InvalidDataException("Hexalith.Timesheets: the selected Builds plan must select only the retained Timesheets candidate declaration.");
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(candidate)));
        if (!string.Equals(plan.PlatformDeclarationHash, hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Hexalith.Timesheets: PlatformDeclarationHash is missing or differs from the selected candidate bytes.");
        return true;
    }

    /// <summary>Shares resolved Development signing material while preserving default-deny owner providers.</summary>
    internal static void BindLocalAuthentication(List<PlatformServerBinding> servers)
    {
        int hostIndex = servers.FindIndex(server => server.ResourceId == "timesheets");
        int gatewayIndex = servers.FindIndex(server => server.ResourceId == "eventstore");
        if (hostIndex < 0 || gatewayIndex < 0)
            throw new InvalidDataException("Hexalith.Timesheets: timesheets/host: the candidate host and EventStore gateway are required.");
        PlatformServerBinding gateway = servers[gatewayIndex];
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(gateway.Environment
            .Select(pair => new KeyValuePair<string, string?>(pair.Key.Replace("__", ":", StringComparison.Ordinal), pair.Value))).Build();
        JwtBearerAuthenticationOptions contract = configuration.GetSection("Authentication:JwtBearer").Get<JwtBearerAuthenticationOptions>()!;
        ValidateOptionsResult validation = JwtBearerAuthenticationContract.Validate(contract,
            new HostingEnvironment { EnvironmentName = Environments.Development }, "Authentication:JwtBearer");
        if (validation.Failed || !string.IsNullOrWhiteSpace(contract.Authority) || string.IsNullOrWhiteSpace(contract.SigningKey))
            throw new InvalidDataException("Hexalith.Timesheets: timesheets/host: shared Development symmetric authentication is required; a qualified production binding is unavailable. " + validation.FailureMessage);

        Dictionary<string, string> environment = new(servers[hostIndex].Environment, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in gateway.Environment.Where(pair => pair.Key.StartsWith("Authentication__JwtBearer__", StringComparison.Ordinal)))
            environment[pair.Key] = pair.Value;
        foreach (string key in new[] { "Authority", "Issuer", "Audience", "SigningKey", "RequireHttpsMetadata" })
            environment["EventStore__Authentication__" + key] = gateway.Environment["Authentication__JwtBearer__" + key];
        environment["EventStore__DomainService__AppId"] = "timesheets";
        environment["Authentication__WorkloadIssuer__Workload"] = "timesheets";
        environment["Authentication__Workload__Audience"] = "timesheets";
        environment["Authentication__Workload__AllowedCallers__0"] = "eventstore";
        environment["Authentication__Workload__MaximumLifetimeSeconds"] = "300";
        environment["APP_API_TOKEN"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        environment["Timesheets__InternalSurface__AllowOnAnyPort"] = "false";
        environment["Timesheets__LiveDaprFixture__Enabled"] = "false";
        servers[hostIndex] = servers[hostIndex] with { Environment = environment };

        // Preserve the declaration-bound callers instead of overwriting their first slot.
        const string prefix = "Authentication__DaprInternal__AllowedCallers__";
        Dictionary<string, string> gatewayEnvironment = new(gateway.Environment, StringComparer.Ordinal);
        gatewayEnvironment["Authentication__DaprInternal__Audience"] = "eventstore";
        int[] slots = gatewayEnvironment.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(key => int.Parse(key[prefix.Length..], CultureInfo.InvariantCulture)).ToArray();
        if (!gatewayEnvironment.Any(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal) && pair.Value == "timesheets"))
            gatewayEnvironment[prefix + (slots.Length == 0 ? 0 : slots.Max() + 1).ToString(CultureInfo.InvariantCulture)] = "timesheets";
        servers[gatewayIndex] = gateway with { Environment = gatewayEnvironment };
    }
}
