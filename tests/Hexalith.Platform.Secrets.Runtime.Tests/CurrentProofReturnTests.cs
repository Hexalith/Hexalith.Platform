using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class CurrentProofReturnTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task PublicNullEvidenceStillObservesOriginalCallerCancellation(bool alreadyCancelled)
    {
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var caller=new CancellationTokenSource();
        var transport=new NullCancellationTransport(caller.Cancel);var authority=new IndependentDaprAuthority(transport,new("fixture-app","file:/fixture-credential",key.ExportSubjectPublicKeyInfoPem(),"enrollment","scope","trust"),TimeProvider.System);
        if(alreadyCancelled) caller.Cancel();
        var error=await Should.ThrowAsync<OperationCanceledException>(()=>authority.InvokeAsync<JsonElement>("fixture-current",new { value="fixture" },caller.Token));
        error.CancellationToken.ShouldBe(caller.Token);transport.Calls.ShouldBe(alreadyCancelled?0:1);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task PublicOwnerBudgetKeepsInternalTimeoutDistinctFromOriginalCallerCancellation(bool callerCancels)
    {
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);var transport=new StalledTransport();using var caller=new CancellationTokenSource();
        var authority=new IndependentDaprAuthority(transport,new("fixture-app","file:/fixture-credential",key.ExportSubjectPublicKeyInfoPem(),"enrollment","scope","trust"),TimeProvider.System);
        var reading=authority.InvokeWithinBudgetAsync<JsonElement>("fixture-current",new { value="fixture" },caller.Token,TimeSpan.FromMilliseconds(100));
        await transport.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);
        if(callerCancels)
        {
            caller.Cancel();var error=await Should.ThrowAsync<OperationCanceledException>(()=>reading.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken));error.CancellationToken.ShouldBe(caller.Token);
        }
        else (await reading.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken)).ValueKind.ShouldBe(JsonValueKind.Undefined);
        transport.Calls.ShouldBe(1);
    }
    [Theory]
    [InlineData("valid")][InlineData("expiry")][InlineData("cancel")]
    public async Task PublicAuthorityReturnRechecksAfterItsActualPayloadDecoder(string outcome)
    {
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);var at=DateTimeOffset.UnixEpoch.AddYears(50);var clock=new MutableClock(at);
        using var cancel=new CancellationTokenSource();bool decoded=false;
        Decoder.Action.Value=()=> { decoded=true;if(outcome=="expiry") clock.Now=at.AddSeconds(1);if(outcome=="cancel")cancel.Cancel(); };
        try
        {
            var authority=new IndependentDaprAuthority(new SignedTransport(key,at),new("fixture-app","file:/fixture-credential",key.ExportSubjectPublicKeyInfoPem(),"enrollment","scope","trust"),clock);
            if(outcome=="cancel") await Should.ThrowAsync<OperationCanceledException>(()=>authority.InvokeAsync<Decoded>("fixture-current",new { value="fixture" },cancel.Token));
            else { var result=await authority.InvokeAsync<Decoded>("fixture-current",new { value="fixture" },cancel.Token);if(outcome=="valid")result.ShouldNotBeNull();else result.ShouldBeNull(); }
            decoded.ShouldBeTrue();
        }
        finally { Decoder.Action.Value=null; }
    }
    [Theory]
    [InlineData(-1,false,false)][InlineData(0,false,false)][InlineData(1,false,false)][InlineData(0,true,false)]
    [InlineData(-1,false,true)][InlineData(0,false,true)][InlineData(1,false,true)][InlineData(0,true,true)]
    public async Task RealSignedAuthorityEvidenceIsCurrentAfterEncodingAndCrypto(int expiryOffset,bool cancelled,bool evidenceOnly)
    {
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);var at=DateTimeOffset.UnixEpoch.AddYears(50);
        using var cancel=new CancellationTokenSource();var clock=new DecisionClock(at,at.AddSeconds(1-expiryOffset),cancelled?cancel.Cancel:null);
        var authority=new IndependentDaprAuthority(new SignedTransport(key,at),new("fixture-app","file:/fixture-credential",key.ExportSubjectPublicKeyInfoPem(),"enrollment","scope","trust"),clock);
        async Task<bool> Read()=>evidenceOnly?await authority.ReadOriginalEvidenceAsync("executor","request",cancel.Token) is not null:(await authority.InvokeAsync<JsonElement>("fixture-current",new { value="fixture" },cancel.Token)).ValueKind==JsonValueKind.Object;
        if(cancelled) await Should.ThrowAsync<OperationCanceledException>(()=>Read());
        else (await Read()).ShouldBe(expiryOffset==1);
        clock.Reads.ShouldBe(expiryOffset==1 && !evidenceOnly?3:2);
    }
    [Theory]
    [InlineData("before")][InlineData("exclusive")][InlineData("expired")][InlineData("historical")][InlineData("cancel")]
    public void ActualP256QualificationVerifierRechecksFinalCurrentTimeAndPreservesAuthenticatedOriginalTime(string boundary)
    {
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);var payload=QualificationHostTests.RolePayload("executor");var request=QualificationHostTests.Request();
        long until=payload["validUntil"]!.GetValue<long>(),observed=payload["observedAt"]!.GetValue<long>();
        var proof=new JsonObject { ["payload"]=payload,["signature"]=Convert.ToBase64String(key.SignData(QualificationProofVerifier.CanonicalBytes(payload),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) };
        var initial=DateTimeOffset.FromUnixTimeSeconds(observed);var final=DateTimeOffset.FromUnixTimeSeconds(boundary=="before"?until-1:boundary=="exclusive"?until:until+1);
        using var cancel=new CancellationTokenSource();var clock=new DecisionClock(initial,final,boundary=="cancel"?cancel.Cancel:null);
        byte[] anchor=Encoding.ASCII.GetBytes(key.ExportSubjectPublicKeyInfoPem());
        var verifier=new QualificationProofVerifier(new Dictionary<string,QualificationProofBinding>{{"executor",new("executor","v1",anchor,Convert.ToHexStringLower(SHA256.HashData(anchor)))}},clock);
        if(boundary=="cancel") Should.Throw<OperationCanceledException>(()=>verifier.Verify("executor",request,proof,cancel.Token));
        else (boundary=="historical"?verifier.VerifyOriginal("executor",request,proof,observed,cancel.Token):verifier.Verify("executor",request,proof,cancel.Token)).ShouldBe(boundary is "before" or "historical");
        clock.Reads.ShouldBe(2);
    }
    private sealed class DecisionClock(DateTimeOffset initial,DateTimeOffset final,Action? atFinal):TimeProvider
    {
        internal int Reads;
        public override DateTimeOffset GetUtcNow() { if(++Reads==1)return initial;atFinal?.Invoke();return final; }
    }
    private sealed class MutableClock(DateTimeOffset at):TimeProvider { internal DateTimeOffset Now=at;public override DateTimeOffset GetUtcNow()=>Now; }
    [JsonConverter(typeof(Decoder))]
    public sealed class Decoded;
    public sealed class Decoder:JsonConverter<Decoded>
    {
        internal static readonly AsyncLocal<Action?> Action=new();
        public override Decoded? Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options) { using var value=JsonDocument.ParseValue(ref reader);Action.Value?.Invoke();return new(); }
        public override void Write(Utf8JsonWriter writer,Decoded value,JsonSerializerOptions options)=>throw new NotSupportedException();
    }
    private sealed class SignedTransport(ECDsa key,DateTimeOffset at):ISecretsCredentialBoundDaprInvoker
    {
        public Task<string?> InvokeAsync(string appId,string method,string body,CancellationToken token)=>throw new InvalidOperationException();
        public Task<string?> InvokeCredentialBoundAsync(string appId,string credential,string method,string body,CancellationToken token)
        {
            using var parsed=JsonDocument.Parse(body);var request=parsed.RootElement;
            var response=new IndependentAuthoritySignedResponse("enrollment","scope","trust",request.GetProperty("Method").GetString()!,request.GetProperty("Nonce").GetString()!,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))),at.ToUnixTimeSeconds(),at.AddSeconds(1).ToUnixTimeSeconds(),JsonSerializer.SerializeToElement(new { allowed=true }),[]);
            return Task.FromResult<string?>(JsonSerializer.Serialize(response with { Signature=key.SignData(JsonSerializer.SerializeToUtf8Bytes(response,CustodyBindingContract.Json),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence) },CustodyBindingContract.Json));
        }
    }
    private sealed class StalledTransport:ISecretsCredentialBoundDaprInvoker
    {
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);internal int Calls;
        public Task<string?> InvokeAsync(string appId,string method,string body,CancellationToken token)=>throw new InvalidOperationException();
        public async Task<string?> InvokeCredentialBoundAsync(string appId,string credential,string method,string body,CancellationToken token) { Calls++;Entered.TrySetResult();await Task.Delay(Timeout.Infinite,token);return null; }
    }
    private sealed class NullCancellationTransport(Action cancel):ISecretsCredentialBoundDaprInvoker
    {
        internal int Calls;
        public Task<string?> InvokeAsync(string appId,string method,string body,CancellationToken token)=>throw new InvalidOperationException();
        public Task<string?> InvokeCredentialBoundAsync(string appId,string credential,string method,string body,CancellationToken token) { Calls++;cancel();return Task.FromResult<string?>(null); }
    }
}
