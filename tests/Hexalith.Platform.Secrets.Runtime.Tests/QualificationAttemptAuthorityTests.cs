using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationAttemptAuthorityTests
{
    [Fact]
    public async Task ActualSignedRetainedAttemptReaderBindsRequestCaseAndCurrentReplyWithoutReserveOrEnrollment()
    {
        using var transport=new CurrentAttemptTransport();byte[] anchor=Encoding.ASCII.GetBytes(transport.Key.ExportSubjectPublicKeyInfoPem());
        var binding=new QualificationCohortAttemptBinding("cohort","file:/fixture-cohort",Encoding.ASCII.GetString(anchor),Convert.ToHexStringLower(SHA256.HashData(anchor)),new string('a',64));
        var authority=new DaprQualificationAttemptAuthority(transport,binding,TimeProvider.System);
        var request=new JsonObject { ["runId"]=Guid.NewGuid().ToString(),["targetCommit"]=new string('b',40),["planSha256"]=QualificationProtocolSchema.PlanSha256,["caseId"]="S1.reject.missing-config" };
        (await authority.ValidateAsync(request,TestContext.Current.CancellationToken)).ShouldBeTrue();
        transport.Requests.Single()["payload"]!["caseId"]!.GetValue<string>().ShouldBe(request["caseId"]!.GetValue<string>());
        foreach(string substitution in new[]{"request","signature","expired","absent","transport"})
        {
            transport.Mode=substitution;(await authority.ValidateAsync(request,TestContext.Current.CancellationToken)).ShouldBeFalse();
        }
        transport.Requests.ShouldAllBe(value=>value["method"]!.GetValue<string>()=="qualification-attempt");
    }
    private sealed class CurrentAttemptTransport:ISecretsCredentialBoundDaprInvoker,IDisposable
    {
        internal ECDsa Key=ECDsa.Create(ECCurve.NamedCurves.nistP256);internal string Mode="current";internal List<JsonObject> Requests=[];
        public Task<string?> InvokeAsync(string app,string method,string body,CancellationToken token)=>throw new InvalidOperationException("Uncredentialed call.");
        public Task<string?> InvokeCredentialBoundAsync(string app,string credential,string method,string body,CancellationToken token)
        {
            app.ShouldBe("cohort");credential.ShouldBe("file:/fixture-cohort");method.ShouldBe("joint/v1/qualification-attempt");
            var request=JsonNode.Parse(body)!.AsObject();Requests.Add(request);
            if(Mode=="transport") return Task.FromResult<string?>(null);
            long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();long at=Mode=="expired"?now-60:now;
            var payload=new JsonObject { ["requestId"]=request["requestId"]!.DeepClone(),["method"]="qualification-attempt",["requestSha256"]=Mode=="request"?new string('f',64):Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body))),
                ["observedAt"]=at,["validUntil"]=at+30,["result"]=new JsonObject { ["exact"]=Mode!="absent",["binding"]=request["payload"]!.DeepClone() } };
            byte[] signature=Key.SignData(QualificationProofVerifier.CanonicalBytes(payload),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence);if(Mode=="signature") signature[0]^=1;
            return Task.FromResult<string?>(new JsonObject { ["payload"]=payload,["signature"]=Convert.ToBase64String(signature) }.ToJsonString());
        }
        public void Dispose()=>Key.Dispose();
    }
}
