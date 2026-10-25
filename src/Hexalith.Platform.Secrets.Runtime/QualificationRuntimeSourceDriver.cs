using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Closed concrete inventory and HMAC source operations. Raw facts are handed to independent observation; this driver never synthesizes scenario metrics or a qualification pass.</summary>
public sealed class QualificationRuntimeSourceDriver(IPlatformKeyInventoryActor inventory, IPlatformKeyInventoryAuthority inventoryAuthority,
    PlatformHmacService digests, TrustedEnvelopeAuthenticator envelopes, IReadOnlyDictionary<string, JsonElement> enrolledInputs)
{
    /// <summary>Runs only shipped source operations against exact independently enrolled input and installed runtime ports.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject original, CancellationToken cancellationToken)
    {
        string? caseId = SecretsProof.Text(original,"caseId");
        if (caseId is null || !enrolledInputs.TryGetValue(caseId,out var input) || original["enrolledScope"] is not JsonObject scope
            || input.GetProperty("enrolledScope").GetRawText() != scope.ToJsonString()) return null;
        var result = new JsonObject { ["caseId"]=caseId,["requestSha256"]=QualificationProtocolSchema.Digest(original) };
        if (caseId.StartsWith("S1.inventory.",StringComparison.Ordinal))
        {
            var key = input.GetProperty("key").Deserialize<PlatformKeyVersion>(CustodyBindingContract.Json)!;
            if (key.TenantId != SecretsProof.Text(scope,"tenantId") || !await inventoryAuthority.AuthorizeOperationAsync(key,"ReadKeyInventory",cancellationToken).ConfigureAwait(false)) return null;
            var snapshot = await inventory.ReadAsync(key).WaitAsync(cancellationToken).ConfigureAwait(false);
            if (snapshot.TenantId != key.TenantId || snapshot.Purpose != key.Purpose || snapshot.KeyAlias != key.KeyAlias
                || !snapshot.Versions.Any(value=>value.Key==key && value.State==PlatformHmacKeyState.Active)
                || !await inventoryAuthority.ValidateStateAsync(key,snapshot.Revision,CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(snapshot)),cancellationToken).ConfigureAwait(false)) return null;
            result["inventory"] = JsonSerializer.SerializeToNode(snapshot,CustodyBindingContract.Json);
            return result;
        }
        if (caseId == "S2.retained-digest")
        {
            var digestScope = input.GetProperty("digestScope").Deserialize<PlatformHmacScope>(CustodyBindingContract.Json)!;
            if (digestScope.TenantId != SecretsProof.Text(scope,"tenantId")) return null;
            string[] components = input.GetProperty("components").Deserialize<string[]>(CustodyBindingContract.Json)!;
            string version = input.GetProperty("recordedVersion").GetString()!;
            var first = await digests.DigestAsync(digestScope,components,version,cancellationToken).ConfigureAwait(false);
            var exact = await digests.DigestAsync(digestScope,components,version,cancellationToken).ConfigureAwait(false);
            result["first"] = JsonSerializer.SerializeToNode(first,CustodyBindingContract.Json);
            result["lookup"] = JsonSerializer.SerializeToNode(exact,CustodyBindingContract.Json);
            return result;
        }
        if (caseId == "S2.canonical-hmac")
        {
            var identity = input.GetProperty("identity").Deserialize<TrustedEnvelopeIdentity>(CustodyBindingContract.Json)!;
            if (identity.TargetTenantId != SecretsProof.Text(scope,"tenantId")) return null;
            var issued = await envelopes.IssueAsync(identity,TimeSpan.FromSeconds(300),cancellationToken).ConfigureAwait(false);
            result["issueStatus"] = issued.Status.ToString();
            if (issued.Envelope is { } envelope)
            {
                var verified = await envelopes.VerifyAsync(envelope,identity,cancellationToken).ConfigureAwait(false);
                result["verificationStatus"] = verified.Status.ToString();
                result["signedEnvelopeSha256"] = CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(envelope));
            }
            return result;
        }
        return null;
    }
}
