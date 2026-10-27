using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Grpc.Net.Client;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Joins the actual publisher, receiving authority, original verifier and authorizer with disposable transport/state.</summary>
internal sealed class RegistrationAuthorityFixture : ISecretsCredentialBoundDaprInvoker, IDisposable
{
    private readonly Fixture _component;
    private readonly ECDsa _signer=ECDsa.Create(ECCurve.NamedCurves.nistP256),_guard=ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly string _channel=Path.GetTempFileName();
    private readonly DaprClient _stateClient,_componentClient;
    private readonly RegistrationAuthorityStateTransport _state;
    private readonly IndependentAuthorityReceiverInstallation _installation;
    private readonly ICustodyRegistrationOriginalVerifier _verifier;
    private IndependentAuthorityReceiver _receiver;
    private readonly CustodyIndependentRoleReceiver _authorizer;
    internal Action? AfterPublicationCommit;
    internal bool RevokeAfterPrepare;
    internal int PrepareWrites;
    internal readonly List<string> Calls=[];
    internal IndependentAuthorityState State=>_state.State;
    internal DaprCustodyRegistrationPublisher Publisher=>new(Authority("publisher"));
    internal DaprCustodyOriginalLocators Locator=>new(Authority("publisher"));

    internal RegistrationAuthorityFixture(Fixture component)
    {
        _component=component;File.WriteAllText(_channel,"joined-channel");
        component.Profile=component.Profile with { Trust=component.Profile.Trust.Append(new TrustedSigner("joined-guard","guard-control-original",_guard.ExportSubjectPublicKeyInfoPem())).ToArray() };
        component.Verifier=new(component.Profile,TimeProvider.System);component.RestartPublication(component.Publisher);
        IndependentCallerGrant Grant(string token,string role,string scope,string[] methods)=>new(Hash(Encoding.UTF8.GetBytes(token)),token,scope,role,methods,DateTimeOffset.UtcNow.AddHours(1),false);
        _state=new(new("joined-enrollment","joined-scope","joined-trust",0,DateTimeOffset.UtcNow.AddHours(1),[
            Grant("publisher","custody-registration-publisher","tenant",["custody-publish-registration-original","custody-registration-original"]),
            Grant("lookup","authorizer","tenant|"+component.OwnerScope,["custody-intent:lookup-original"]),
            Grant("role","authorizer","tenant|"+component.OwnerScope,["custody-role-authorize","custody-control-original","qualification-begin-original","qualification-lookup-original"])
        ],null,0,Fixture.Digest,[],[],[],[],[],[]));
        _stateClient=new DaprClientBuilder().UseGrpcEndpoint("http://127.0.0.1:1").UseJsonSerializationOptions(CustodyBindingContract.Json)
            .UseGrpcChannelOptions(new GrpcChannelOptions { HttpHandler=_state }).Build();
        _componentClient=new DaprClientBuilder().UseGrpcEndpoint("http://127.0.0.1:1").UseGrpcChannelOptions(new GrpcChannelOptions { HttpHandler=new SdkBridge(component) }).Build();
        _authorizer=new(component.Profile,new("authorizer","private-operation","unused",_channel,Binding("role"),Binding("role")),component.Store,_stateClient,component.OperationSigner,TimeProvider.System,this);
        _verifier=new DaprCustodyRegistrationOriginalVerifier(new CustodyLifecycleClient(_componentClient,new Dictionary<string,string> { [CustodyLifecycleClient.LogicalRole]="custody" },component.OwnerScope,
            Encoding.UTF8.GetString(CustodyJson.Bytes(component.Profile.Enrollment)),component.Profile.ComponentPublicKeyPem,
            new DaprCustodyAuthorizerClient(this,new Dictionary<string,string> { [DaprCustodyAuthorizerClient.LogicalRole]="joined-authorizer" },"file:/lookup")));
        _installation=new("joined-enrollment","joined-scope","joined-trust","joined-store","head","unused",_signer.ExportSubjectPublicKeyInfoPem(),"file:"+_channel,CompletionReserveBytes:1024*1024);
        _receiver=Receiver();
    }
    private IndependentAuthorityReceiver Receiver()=>new(_stateClient,_installation,_signer,TimeProvider.System,registrationVerifier:_verifier);
    internal void Restart()=>_receiver=Receiver();
    private IndependentDaprAuthorityBinding Binding(string token)=>new("joined-authority","file:/"+token,_signer.ExportSubjectPublicKeyInfoPem(),"joined-enrollment","joined-scope","joined-trust");
    private IndependentDaprAuthority Authority(string token)=>new(this,Binding(token),TimeProvider.System);
    public Task<string?> InvokeAsync(string app,string method,string body,CancellationToken token)=>throw new InvalidOperationException("Uncredentialed fixture call.");
    public async Task<string?> InvokeCredentialBoundAsync(string app,string credential,string method,string body,CancellationToken token)
    {
        string operation=method[(method.LastIndexOf('/')+1)..];
        bool role=app=="joined-authorizer";
        if(!role && operation=="custody-publish-registration-original")
        {
            using var parsed=JsonDocument.Parse(body);
            SeedControl(parsed.RootElement.GetProperty("Payload").GetProperty("original").Deserialize<CustodyKeyRegistration>(CustodyBindingContract.Json)!);
        }
        int before=State.Originals.Count;
        var context=new DefaultHttpContext { RequestServices=new ServiceCollection().AddLogging().BuildServiceProvider() };
        context.Request.Headers["dapr-api-token"]="joined-channel";context.Request.Headers.Authorization="Bearer "+credential["file:/".Length..];
        context.Request.Body=new MemoryStream(Encoding.UTF8.GetBytes(body));context.Response.Body=new MemoryStream();
        await (await (role?_authorizer.HandleAsync(context,operation,token):_receiver.HandleAsync(context,operation,token))).ExecuteAsync(context);
        Calls.Add(app+":"+operation+":"+context.Response.StatusCode);
        if(role && operation=="authorize" && context.Response.StatusCode==200)
        {
            var issued=CustodyJson.Read<CustodyCallAuthorization>(((MemoryStream)context.Response.Body).ToArray());
            var permission=CustodyJson.Read<SignedStatement>(Convert.FromBase64String(issued.Metadata["custody-operation-proof"]));
            if(permission.ExpiresAt-permission.IssuedAt!=TimeSpan.FromSeconds(30)) throw new Exception("Actual authorizer exceeded its unchanged exact 30-second bound.");
        }
        if(role && operation=="prepare" && context.Response.StatusCode==200)
        {
            if(State.Originals.Count<=before) throw new Exception("Actual authorizer did not persist its prepare on the publication head.");
            PrepareWrites++;
            if(RevokeAfterPrepare) _state.Replace(State with { Revision=State.Revision+1,Callers=State.Callers.Select(value=>value.Identity=="publisher"?value with { Revoked=true }:value).ToArray() });
        }
        if(!role && operation=="custody-publish-registration-original" && context.Response.StatusCode==200) AfterPublicationCommit?.Invoke();
        return context.Response.StatusCode==200?Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()):null;
    }
    private void SeedControl(CustodyKeyRegistration registration)
    {
        var request=_component.Lookup(registration);var e=request.Envelope;
        var facts=new ControlClaim(Hash(CustodyJson.Bytes(e.Proof)),Fixture.Digest,Fixture.Digest,Fixture.Digest,100,null,null);
        var statement=new SignedStatement("guard-control-original","joined-guard",_component.Profile.Enrollment.AuthorityId,_component.Profile.Enrollment.AuthorityIncarnation,"tenant",
            _component.Store.State.Head.Revision,_component.Store.State.Head.Digest,facts.ControlProofSha256,DateTimeOffset.UtcNow.AddSeconds(-1),DateTimeOffset.UtcNow.AddSeconds(25),JsonSerializer.SerializeToElement(facts,CustodyBindingContract.Json),[]);
        statement=statement with { Signature=_guard.SignData(CustodyJson.Bytes(statement),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence) };
        var control=new RetainedControlOriginal(e.Proof,statement,_component.Sign("control",facts.ControlProofSha256,facts));
        string id=Hash(CustodyJson.Bytes(new { tenant="tenant",scope=_component.OwnerScope,operationId=registration.OperationId,revision=e.ExpectedCustodyRevision,digest=e.CheckpointDigest }));
        var originals=new Dictionary<string,IndependentOriginal>(State.Originals) { ["control-original:"+id]=new(JsonSerializer.SerializeToElement(control,CustodyBindingContract.Json),null) };
        _state.Replace(State with { Revision=State.Revision+1,Originals=originals });
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    public void Dispose() { _stateClient.Dispose();_componentClient.Dispose();_signer.Dispose();_guard.Dispose();File.Delete(_channel); }
}
