using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Read-only independent grant owner. Exact installed bindings come from current authoritative state, never the requester.</summary>
public sealed class DeletionSigningAvailabilityOperationHandler(TimeProvider clock) : IIndependentOwnerOperationHandler
{
    /// <inheritdoc />
    public IReadOnlySet<string> Methods { get; } = new HashSet<string>(["signing-readiness-permission"], StringComparer.Ordinal);
    /// <inheritdoc />
    public bool IsReadOnly(string method) => true;
    /// <inheritdoc />
    public bool RequiresCurrentResultAdmission(string method) => false;
    /// <inheritdoc />
    public Task<object?> RecoverAsync(string method, JsonElement payload, IndependentCallerGrant caller, IndependentAuthorityState current, CancellationToken token)
        => HandleAsync(method, payload, caller, current, token);
    /// <inheritdoc />
    public Task<object?> HandleAsync(string method, JsonElement payload, IndependentCallerGrant caller, IndependentAuthorityState current, CancellationToken token)
    {
        var request = payload.Deserialize<DeletionCapabilitySigningReadinessRequest>(CustodyBindingContract.Json);
        if (method != "signing-readiness-permission" || request is null || caller.Revoked || caller.Role != "deletion-signing-owner"
            || caller.ScopeReference != request.TenantId + "|deletion-signing-availability" || caller.ValidUntil <= clock.GetUtcNow() || current.ValidUntil <= clock.GetUtcNow()
            || request.KeyFamily != "DeletionBatchCapabilitySigningKey" || request.TrustProfileRevision <= 0 || request.InstallationBinding is not { Length: 64 }
            || string.IsNullOrWhiteSpace(request.ProviderIdentity) || current.QualificationBindings is null) { return Task.FromResult<object?>(null); }
        string identity = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
        if (!current.QualificationBindings.TryGetValue("deletion-signing-availability:" + identity, out string? allowedRole) || allowedRole != caller.Role)
        { return Task.FromResult<object?>(null); }
        var now = clock.GetUtcNow(); var until = new[] { now.AddSeconds(30), caller.ValidUntil, current.ValidUntil }.Min();
        return Task.FromResult<object?>(new DeletionCapabilitySigningAvailability(request, request.ProviderIdentity, current.TrustRevision,
            ["SignDeletionCapability", "LookupDeletionCapability", "ObsoleteUnissued", "RecoverSigningSuccessor"], now, until));
    }
}
