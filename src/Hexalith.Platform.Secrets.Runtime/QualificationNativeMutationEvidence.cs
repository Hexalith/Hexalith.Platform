using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Versioned exact native event, terminal command status and independently signed dispatch-original read.</summary>
public sealed record QualificationNativeMutationEvidence(string Version,string CaseRequestSha256,string TargetId,NativeSourceProof Source,
    long SourceRevision,string EventId,string NativeEventDigest,CommandStatusQueryResponse CommandStatus,IndependentAuthorityReadEvidence Authorization,string NativePrincipalId,long NativeOccurredAt)
{
    /// <summary>Stable identity includes the exact stream and committed native event.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string MutationId=>QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(new { Source.TenantId,Source.Domain,Source.AggregateId,SourceRevision,EventId,NativeEventDigest },CustodyBindingContract.Json)!);
    /// <summary>Immutable authorization identity, independent of renewable lookup response framing.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string AuthorizationId
    {
        get
        {
            var original=Authorization.Response.Payload.Deserialize<IndependentOriginal>(CustodyBindingContract.Json)??throw new InvalidOperationException("Exact dispatch original required.");
            return QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(new { original.Request,original.RetainedRevision,original.DispatchBinding },CustodyBindingContract.Json)!);
        }
    }
    /// <summary>Verifies actual retained signed authority evidence at its original time and all native mutation correlations.</summary>
    public bool Verify(JsonObject request,IndependentDaprAuthorityBinding authority,long observedAt)
    {
        try
        {
            if(Authorization?.Response is not { Signature.Length:>0 } || Authorization.Request.ValueKind!=JsonValueKind.Object || Authorization.Response.Payload.ValueKind!=JsonValueKind.Object || CommandStatus is null) return false;
            var response=Authorization.Response;var wire=Authorization.Request;
            var retained=response.Payload.Deserialize<IndependentOriginal>(CustodyBindingContract.Json);
            var binding=retained?.DispatchBinding;var facts=binding?.Facts;
            if(Version!="EXT-SECRETS-1.native-mutation.v2" || CaseRequestSha256!=QualificationProtocolSchema.Digest(request) || string.IsNullOrWhiteSpace(TargetId)
                || Source is not { CodecVersion:1,Erased:false,RetainedFloor:1 } || string.IsNullOrWhiteSpace(Source.SourceIncarnation) || SourceRevision<1 || SourceRevision>Source.HeadSequence
                || !Hex(NativeEventDigest) || string.IsNullOrWhiteSpace(EventId) || response.EnrollmentId!=authority.EnrollmentId || response.Scope!=authority.Scope
                || response.TrustRevision!=authority.CurrentTrustRevision || response.Method!="qualification-lookup-original"
                || response.ObservedAt>observedAt || response.ValidUntil<=response.ObservedAt || response.ValidUntil>response.ObservedAt+30
                || wire.GetProperty("Protocol").GetString()!="platform-independent-authority-v1" || wire.GetProperty("Method").GetString()!=response.Method
                || wire.GetProperty("EnrollmentId").GetString()!=response.EnrollmentId || wire.GetProperty("Scope").GetString()!=response.Scope
                || wire.GetProperty("TrustRevision").GetString()!=response.TrustRevision || wire.GetProperty("Nonce").GetString()!=response.Nonce
                || Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(wire,CustodyBindingContract.Json)))!=response.RequestSha256
                || retained is not { SubmissionState:"Dispatched",RetainedRevision:>0 } || binding is null || facts is not { Version:"IndependentCommandDispatchV2",AuthorityRevision:>0 }
                || facts.EnrollmentId!=authority.EnrollmentId || facts.TrustRevision!=authority.CurrentTrustRevision || facts.AuthorizedAt<0 || facts.AuthorizedAt/1000>response.ObservedAt
                || facts.MessageId!=wire.GetProperty("Payload").GetProperty("requestId").GetString()
                || wire.GetProperty("Payload").GetProperty("role").GetString() is not ("private-replay" or "private-security-observation" or "fr34-engine")
                || facts.TenantId!=Source.TenantId || facts.Domain!=Source.Domain || facts.AggregateId!=Source.AggregateId || binding.Domain!=Source.Domain
                || string.IsNullOrWhiteSpace(binding.Caller) || string.IsNullOrWhiteSpace(binding.Principal) || !Hex(binding.CommandSha256) || !Hex(binding.GrantSha256)
                || NativePrincipalId!=binding.Principal || NativeOccurredAt<=0 || NativeOccurredAt/1000>observedAt
                || string.IsNullOrWhiteSpace(facts.CommandType) || facts.SourcePermission!="recording-source:"+(facts.Domain=="agents"?"legacy-security-event-log":facts.Domain)
                || CommandStatus.Status!="Completed" || CommandStatus.StatusCode!=(int)Hexalith.EventStore.Contracts.Commands.CommandStatus.Completed || CommandStatus.TenantId!=Source.TenantId || CommandStatus.Domain!=Source.Domain || CommandStatus.AggregateId!=Source.AggregateId
                || CommandStatus.MessageId!=facts.MessageId || CommandStatus.EventCount is not >0 || CommandStatus.CommittedEventSequence is not { } end
                || end>Source.HeadSequence || SourceRevision>end || SourceRevision<=end-CommandStatus.EventCount || CommandStatus.FailureReason is not null || CommandStatus.Retryable is true) return false;
            using var key=ECDsa.Create();key.ImportFromPem(authority.PublicKeyPem);
            return key.ExportParameters(false).Curve.Oid.Value=="1.2.840.10045.3.1.7" && key.VerifyData(JsonSerializer.SerializeToUtf8Bytes(response with { Signature=[] },CustodyBindingContract.Json),response.Signature,HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence);
        }
        catch(Exception error) when(error is JsonException or InvalidOperationException or ArgumentException or CryptographicException or KeyNotFoundException or OverflowException) { return false; }
    }
    /// <summary>Correlates the exact event/incarnation proof with the independently read native source census.</summary>
    public bool MatchesNative(JsonArray sources)=>sources.OfType<JsonObject>().Count(stream=>
        JsonNode.DeepEquals(stream["Proof"],JsonSerializer.SerializeToNode(Source,CustodyBindingContract.Json))
        && stream["Identity"]?["TenantId"]?.GetValue<string>()==Source.TenantId && stream["Identity"]?["Domain"]?.GetValue<string>()==Source.Domain && stream["Identity"]?["AggregateId"]?.GetValue<string>()==Source.AggregateId
        && stream["Events"] is JsonArray events && events.OfType<JsonObject>().Count(item=>item["SourceRevision"]?.GetValue<long>()==SourceRevision
            && item["EventId"]?.GetValue<string>()==EventId && item["NativeEventDigest"]?.GetValue<string>()==NativeEventDigest && item["PrincipalId"]?.GetValue<string>()==NativePrincipalId
            && item["OccurredAt"]?.GetValue<DateTimeOffset>().ToUnixTimeMilliseconds()==NativeOccurredAt)==1)==1;
    private static bool Hex(string? value)=>value is { Length:64 } && value.All(char.IsAsciiHexDigit);
}
