using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact replay state projection shared by positive envelope and restricted replay readers.</summary>
public static class QualificationReplayStateProjection
{
    /// <summary>Projects only the actual exact original times/identity. The nonce digest uses the enrolled public canonical nonce frame.</summary>
    public static bool Apply(JsonObject state,JsonNode receipt,JsonObject scope)
    {
        var original=receipt.Deserialize<TrustedEnvelopeReplayReceipt>(CustodyBindingContract.Json);if(original is null) return false;
        string nonce=QualificationProtocolSchema.Digest(new JsonObject { ["nonce"]=original.Intent.DeliveryNonce });
        if(original.SourceRevision!=1 || original.Intent.TargetTenantId!=scope["tenantId"]?.GetValue<string>() || original.Intent.Issuer!=scope["issuer"]?.GetValue<string>()
            || nonce!=scope["nonceDigest"]?.GetValue<string>() || original.RetainUntil!=original.FirstSeenAt.AddSeconds(604800)) return false;
        state["replayNamespace"]="system/trusted-envelope-replay";state["replayTenant"]=original.Intent.TargetTenantId;state["replayIssuer"]=original.Intent.Issuer;state["nonceDigest"]=nonce;
        state["firstSeenAt"]=original.FirstSeenAt.ToUnixTimeMilliseconds();state["retainUntil"]=original.RetainUntil.ToUnixTimeMilliseconds();state["keyVersion"]=original.Intent.SigningKeyVersion;return true;
    }
}
