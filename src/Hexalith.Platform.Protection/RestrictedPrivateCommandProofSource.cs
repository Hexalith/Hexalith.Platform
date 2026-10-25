using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Platform.Protection;

/// <summary>Distinct private recovery-proof capability; it cannot grant a new observation or a replay retry.</summary>
public interface IPrivateRecoveryCommandProofSource
{
    /// <summary>Requires independent current exact-original recovery authorization before signing the unchanged security command.</summary>
    ValueTask<string?> IssueRecoveryAsync(BoundWorkloadCommandRequest request,SecurityObservationRecoveryGrant grant,CancellationToken cancellationToken);
}

/// <summary>Private workload proof signing restricted to the two exact owner command shapes. No public/general dispatcher authority.</summary>
public sealed class RestrictedPrivateCommandProofSource(string caller, string principal, string mountedRsaKeyFile,
    IndependentDaprAuthority currentScopes, TimeProvider clock) : IEventStoreGatewayBoundCommandProofSource,IPrivateRecoveryCommandProofSource
{
    /// <inheritdoc />
    public ValueTask<string?> IssueAsync(BoundWorkloadCommandRequest request, CancellationToken cancellationToken)=>IssueCoreAsync(request,null,cancellationToken);
    /// <inheritdoc />
    public ValueTask<string?> IssueRecoveryAsync(BoundWorkloadCommandRequest request,SecurityObservationRecoveryGrant grant,CancellationToken cancellationToken)
        =>IssueCoreAsync(request,grant,cancellationToken);
    private async ValueTask<string?> IssueCoreAsync(BoundWorkloadCommandRequest request,SecurityObservationRecoveryGrant? grant,CancellationToken cancellationToken)
    {
        if (request.PrincipalId != principal || request.Command.Extensions is { Count: > 0 }
            || request.Command.IdempotencyKey != request.Command.MessageId) return null;
        var command = request.Command;
        string aggregate;
        string[] fields;
        if (command.Domain == "trusted-envelope-replay" && command.Tenant == "system"
            && command.CommandType == "Hexalith.Agents.Contracts.TrustedEnvelopeReplay.RegisterTrustedEnvelopeFirstSeen")
        {
            fields = ["issuer", "deliveryNonce", "targetTenantId", "logicalCommandId", "signingKeyVersion", "canonicalAndTagDigest", "firstSeenAt", "retainUntil"];
            if (!ExactFields(command.Payload, fields)) return null;
            aggregate = Convert.ToHexStringLower(SHA256.HashData(PlatformCanonicalBytes.Identity(["trusted-envelope-replay", command.Payload.GetProperty("issuer").GetString()!, command.Payload.GetProperty("deliveryNonce").GetString()!])));
        }
        else if (command.Domain == "security-event-log" && command.CommandType == "Hexalith.Agents.Contracts.SecurityEventLog.RecordSecurityEvent")
        {
            fields = ["securityObservationId", "routingTenantId", "utcDay", "observedAt", "reasonCode", "untrustedFieldsHmac", "digestKeyVersion"];
            if (!ExactFields(command.Payload, fields) || command.Payload.GetProperty("routingTenantId").GetString() != command.Tenant) return null;
            aggregate = Convert.ToHexStringLower(SHA256.HashData(PlatformCanonicalBytes.Identity(["security-event-log", command.Tenant, command.Payload.GetProperty("utcDay").GetString()!])));
        }
        else return null;
        if(command.AggregateId!=aggregate || grant is not null && (command.Domain!="security-event-log" || command.MessageId!=PrivateRecordingIdentity.SecurityRecoveryMessageId(grant))) return null;
        bool admitted=grant is null
            ? await currentScopes.InvokeAsync<Permission>("recording-command-authorize",new { caller,principal,command },cancellationToken) is { Allowed:true }
            : await currentScopes.InvokeAsync<Permission>("recording-recovery-command-authorize",new { caller,principal,command,grant },cancellationToken) is { Allowed:true };
        if(!admitted) return null;
        // Independent workload RSA key; this is not a decision approval issuer or capability signing family.
        var info = new FileInfo(mountedRsaKeyFile);
        if (!info.Exists || info.Length is <= 0 or > 16384) return null;
        string key = (await File.ReadAllTextAsync(mountedRsaKeyFile, cancellationToken)).Trim();
        return BoundWorkloadCommandProof.Issue(request, caller, key, clock.GetUtcNow());
    }
    private static bool ExactFields(JsonElement payload, string[] expected)
    {
        if (payload.ValueKind != JsonValueKind.Object) return false;
        var names = payload.EnumerateObject().Select(p => p.Name).ToArray();
        return names.Length == expected.Length && names.Distinct(StringComparer.Ordinal).Count() == expected.Length
            && names.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal));
    }
    private sealed record Permission(bool Allowed);
}
