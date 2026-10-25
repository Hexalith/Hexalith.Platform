using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Reads the existing cohort attempt through its authenticated signed receiving protocol. It has no reserve, execute or enrollment operation.</summary>
public sealed class DaprQualificationAttemptAuthority(ISecretsCredentialBoundDaprInvoker transport,QualificationCohortAttemptBinding accepted,TimeProvider clock):IQualificationAttemptAuthority
{
    /// <inheritdoc />
    public async Task<bool> ValidateAsync(JsonObject request,CancellationToken cancellationToken)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            byte[] anchor=Encoding.ASCII.GetBytes(accepted.PublicKeyPem);
            if(!SecretsProvisioning.IsDaprApplicationId(accepted.AppId) || !SecretsProvisioning.IsReference(accepted.CredentialReference)
                || Convert.ToHexStringLower(SHA256.HashData(anchor))!=accepted.PublicAnchorSha256 || accepted.BindingDigest is not { Length:64 } || !accepted.BindingDigest.All(char.IsAsciiHexDigitLower)) return false;
            string nonce=Guid.NewGuid().ToString();
            var payload=new JsonObject { ["bindingDigest"]=accepted.BindingDigest,["attemptId"]=request["runId"]?.DeepClone(),
                ["targetCommit"]=request["targetCommit"]?.DeepClone(),["planSha256"]=request["planSha256"]?.DeepClone(),["caseId"]=request["caseId"]?.DeepClone() };
            var wire=new JsonObject { ["protocol"]="OQ-5.4-1.joint.v1",["requestId"]=nonce,["method"]="qualification-attempt",["payload"]=payload };
            byte[] body=QualificationProofVerifier.CanonicalBytes(wire);
            string? raw=await transport.InvokeCredentialBoundAsync(accepted.AppId,accepted.CredentialReference,"joint/v1/qualification-attempt",Encoding.UTF8.GetString(body),deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
            var envelope=raw is null?null:SecretsProof.Parse(raw);if(envelope?["payload"] is not JsonObject proof || envelope.Count!=2) return false;
            using var key=ECDsa.Create();key.ImportFromPem(accepted.PublicKeyPem);
            long now=clock.GetUtcNow().ToUnixTimeSeconds();
            return key.ExportParameters(false).Curve.Oid.Value=="1.2.840.10045.3.1.7" && proof["requestId"]?.GetValue<string>()==nonce
                && proof["method"]?.GetValue<string>()=="qualification-attempt" && proof["requestSha256"]?.GetValue<string>()==Convert.ToHexStringLower(SHA256.HashData(body))
                && proof["observedAt"]?.GetValue<long>() is { } at && proof["validUntil"]?.GetValue<long>() is { } until && at<=now && at>=now-30 && until>now && until<=at+30
                && proof["result"]?["exact"]?.GetValue<bool>()==true && JsonNode.DeepEquals(proof["result"]?["binding"],payload)
                && key.VerifyData(QualificationProofVerifier.CanonicalBytes(proof),Convert.FromBase64String(envelope["signature"]!.GetValue<string>()),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) { throw; }
        catch(Exception) { return false; }
    }
}
