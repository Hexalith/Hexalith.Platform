using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.Platform.Composition;

// Six current entrypoints are WebApplications. Agents.UI is a Razor library; its actual host is a separate accepted installation input.
internal static class AcceptedListenerBindings
{
    internal const string Requirement="External.IndependentlyAcceptedCompleteResourceListenersV1";
    internal const string UiHostRequirement="External.IndependentlyAcceptedAgentsUiHostListenerContractV1";
    private static readonly IReadOnlyDictionary<string,string> Roles=new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["eventstore"]="eventstore-owner",["tenants"]="tenants-owner",["tenants-api"]="tenants-api",
        ["parties"]="parties-owner",["conversations"]="conversations-owner",["agents"]="agents-owner",["agents-ui"]="agents-ui-host"
    };
    internal static IReadOnlyDictionary<string,AcceptedResourceListener> Validate(JsonElement inventory,AgentsDeploymentProfileModel profile)
    {
        if(profile.ListenerBindingsJson is null) throw new InvalidDataException(Requirement);
        using var document=AcceptedDependencyRecords.Json(profile.ListenerBindingsJson);var bindings=document.RootElement;
        if(bindings.ValueKind!=JsonValueKind.Object || !bindings.EnumerateObject().Select(row=>row.Name).Order(StringComparer.Ordinal).SequenceEqual(Roles.Keys.Order(StringComparer.Ordinal)))
            throw new InvalidDataException(Requirement+": exact seven resource keys required.");
        var result=new Dictionary<string,AcceptedResourceListener>(StringComparer.Ordinal);
        foreach(var pair in Roles)
        {
            var value=bindings.GetProperty(pair.Key);
            if(pair.Key=="agents-ui" && (value.ValueKind!=JsonValueKind.Object || !value.TryGetProperty("uiHost",out _))) throw new InvalidDataException(UiHostRequirement);
            string[] keys=pair.Key=="agents-ui"?["protocol","role","address","port","endpointName","uiHost"]:["protocol","role","address","port","endpointName"];
            if(value.ValueKind!=JsonValueKind.Object || !value.EnumerateObject().Select(row=>row.Name).Order(StringComparer.Ordinal).SequenceEqual(keys.Order(StringComparer.Ordinal))
                || value.GetProperty("protocol").GetString()!="AspNetCoreHttpListenerV1" || value.GetProperty("role").GetString()!=pair.Value
                || value.GetProperty("address").GetString() is not ("0.0.0.0" or "127.0.0.1" or AgentsDeploymentProfile.Host)
                || !value.GetProperty("port").TryGetInt32(out int port) || port is <1 or >65535
                || !Regex.IsMatch(value.GetProperty("endpointName").GetString()??"","^[a-z][a-z0-9-]{0,62}$"))
                throw new InvalidDataException(Requirement+": invalid listener for "+pair.Key);
            var graphs=inventory.GetProperty("resources").EnumerateArray().Where(row=>row.GetProperty("resource").GetString()==pair.Key).ToArray();
            if(graphs.Length!=1 || !graphs[0].TryGetProperty("listener",out var accepted) || CoordinatedProtectionAcceptance.CanonicalHash(accepted)!=CoordinatedProtectionAcceptance.CanonicalHash(value))
                throw new InvalidDataException(Requirement+": accepted listener substitution for "+pair.Key);
            if(pair.Key=="agents-ui") ValidateUiHost(value.GetProperty("uiHost"),graphs[0]);
            var next=new AcceptedResourceListener(pair.Value,value.GetProperty("address").GetString()!,port,value.GetProperty("endpointName").GetString()!);
            if(result.Values.Any(old=>old.EndpointName==next.EndpointName || old.Port==next.Port && (old.Address==next.Address || old.Address=="0.0.0.0" || next.Address=="0.0.0.0")))
                throw new InvalidDataException(Requirement+": conflicting listener ownership.");
            result.Add(pair.Key,next);
        }
        return result;
    }
    private static void ValidateUiHost(JsonElement host,JsonElement graph)
    {
        try
        {
            if(!Keys(host,["artifact","sha256","contract"])) throw new InvalidDataException(UiHostRequirement);
            var contract=host.GetProperty("contract");
            if(!Keys(contract,["protocol","entryPoint","uiComponentIdentity","frontComposerRegistrationIdentity","uiComponentArtifact","uiComponentSha256","frontComposerRegistrationArtifact","frontComposerRegistrationSha256","evidenceSha256","listenerArgument","listenerEnvironment"])
                || contract.GetProperty("protocol").GetString()!="IndependentlyAcceptedFrontComposerUiHostHttpV1"
                || contract.GetProperty("entryPoint").GetString()!=graph.GetProperty("entryPoint").GetString()
                || contract.GetProperty("uiComponentIdentity").GetString()!="Hexalith.Agents.UI"
                || contract.GetProperty("frontComposerRegistrationIdentity").GetString()!="Hexalith.Agents.UI.Composition.AgentsFrontComposerRegistration"
                || contract.GetProperty("listenerArgument").GetString()!="--urls" || contract.GetProperty("listenerEnvironment").GetString()!="ASPNETCORE_URLS"
                || !Digest(contract.GetProperty("evidenceSha256").GetString())) throw new InvalidDataException(UiHostRequirement);
            var pins=new[]{(host.GetProperty("artifact").GetString(),host.GetProperty("sha256").GetString()),
                (contract.GetProperty("uiComponentArtifact").GetString(),contract.GetProperty("uiComponentSha256").GetString()),
                (contract.GetProperty("frontComposerRegistrationArtifact").GetString(),contract.GetProperty("frontComposerRegistrationSha256").GetString())};
            if(pins.Select(pin=>pin.Item1).Distinct(StringComparer.Ordinal).Count()!=3) throw new InvalidDataException(UiHostRequirement);
            foreach(var (name,digest) in pins)
            {
                if(string.IsNullOrEmpty(name) || name.Length>256 || name.Contains('\0') || Path.IsPathFullyQualified(name) || name.Split('/').Any(part=>part is "" or "." or "..") || !Digest(digest)) throw new InvalidDataException(UiHostRequirement);
                var files=graph.GetProperty("files").EnumerateArray().Where(file=>file.GetProperty("path").GetString()==name).ToArray();
                if(files.Length!=1 || files[0].GetProperty("sha256").GetString()!=digest || files[0].GetProperty("bytes").GetInt64()<1
                    || name==host.GetProperty("artifact").GetString() && files[0].GetProperty("bytes").GetInt64()>65536) throw new InvalidDataException(UiHostRequirement);
            }
        }
        catch(Exception error) when(error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new InvalidDataException(UiHostRequirement,error); }
    }
    internal static void ValidateUiHostArtifact(JsonElement host,ReadOnlySpan<byte> bytes)
    {
        using var document=AcceptedDependencyRecords.Json(System.Text.Encoding.UTF8.GetString(bytes));
        if(CoordinatedProtectionAcceptance.CanonicalHash(document.RootElement)!=CoordinatedProtectionAcceptance.CanonicalHash(host.GetProperty("contract")))
            throw new InvalidDataException(UiHostRequirement+": actual host contract bytes differ.");
    }
    private static bool Digest(string? value)=>Regex.IsMatch(value??"","^[0-9a-f]{64}$");
    private static bool Keys(JsonElement value,string[] keys)=>value.ValueKind==JsonValueKind.Object
        && value.EnumerateObject().Select(row=>row.Name).Order(StringComparer.Ordinal).SequenceEqual(keys.Order(StringComparer.Ordinal));
}
