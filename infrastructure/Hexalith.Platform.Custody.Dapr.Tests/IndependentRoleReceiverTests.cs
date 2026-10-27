using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Security.Cryptography;
using NSubstitute;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class IndependentRoleReceiverTests
{
    internal static async Task Run()
    {
        using var fixture = new Fixture();
        using var guard = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var roleKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var transport = new AuthorityTransport();
        fixture.Profile = fixture.Profile with { Trust = fixture.Profile.Trust.Where(t => t.Purpose != "private-operation")
            .Append(new("authorizer", "private-operation", roleKey.ExportSubjectPublicKeyInfoPem()))
            .Append(new("guard", "guard-control-original", guard.ExportSubjectPublicKeyInfoPem())).ToArray() };
        fixture.Profile.Validate();
        string channelFile = Path.GetTempFileName(); await File.WriteAllTextAsync(channelFile, "isolated-channel");
        try
        {
            var request = fixture.Request("read-current"); var envelope = request.Envelope;
            var facts = new ControlClaim(CustodyJson.Hash(CustodyJson.Bytes(envelope.Proof)), Fixture.Digest, Fixture.Digest, Fixture.Digest, 100, null, null);
            var controlStatement = fixture.Sign("control", facts.ControlProofSha256, facts);
            var original = new SignedStatement("guard-control-original", "guard", fixture.Profile.Enrollment.AuthorityId, fixture.Profile.Enrollment.AuthorityIncarnation,
                "tenant", fixture.Store.State.Head.Revision, fixture.Store.State.Head.Digest, facts.ControlProofSha256,
                DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(20), JsonSerializer.SerializeToElement(facts, CustodyBindingContract.Json), []);
            SignedStatement Signed(SignedStatement value) => value with { Signature = guard.SignData(CustodyJson.Bytes(value with { Signature = [] }), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence) };
            async Task Install(RetainedControlOriginal value)
            {
                transport.Control = value;
                var reference = await fixture.Store.PutImmutableAsync(CustodyJson.Bytes(value), "original", default);
                fixture.Store.State.Checkpoint.Originals["control:" + envelope.OperationId] = new(envelope.RequestDigest, "guard-control-original", reference, null);
            }
            await Install(new(envelope.Proof, Signed(original), controlStatement));
            using var dapr = new DaprClientBuilder().Build();
            var installation = new CustodyRoleInstallation("authorizer", "authorizer", "unused", channelFile, transport.Binding("scopes"), transport.Binding("originals"));
            var receiver = new CustodyIndependentRoleReceiver(fixture.Profile, installation, fixture.Store, dapr, roleKey, TimeProvider.System, transport);
            var intent = new CustodyCallIntent("read-current", "tenant", envelope.OwnerPurposeScope, envelope.OperationId, envelope.RequestDigest, "{}", envelope.RequestAttemptId, envelope.Challenge);
            var preparedResult = await Call(receiver, "prepare", CustodyJson.Bytes(intent));
            if (preparedResult.Status != 200) throw new Exception("Actual current authorizer receiver refused valid retained controls.");
            // Real loopback HTTP route and body-limit middleware, using only disposable enrolled fixture inputs.
            var webBuilder=WebApplication.CreateSlimBuilder();webBuilder.Logging.ClearProviders();webBuilder.WebHost.UseUrls("http://127.0.0.1:0");
            webBuilder.WebHost.ConfigureKestrel(options=>options.Limits.MaxRequestBodySize=CustodyBindingContract.CandidateRequestMessageBytes);
            await using(var web=webBuilder.Build())
            {
                web.MapPost("/custody-lifecycle/{method}",(HttpContext context,string method,CancellationToken cancellation)=>receiver.HandleAsync(context,method,cancellation));
                await web.StartAsync();
                try
                {
                    string address=web.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                    using var http=new HttpClient { BaseAddress=new Uri(address),Timeout=TimeSpan.FromSeconds(5) };
                    http.DefaultRequestHeaders.Add("dapr-api-token","isolated-channel");http.DefaultRequestHeaders.Authorization=new("Bearer","isolated-caller");
                    using var reply=await http.PostAsync("/custody-lifecycle/prepare",new ByteArrayContent(CustodyJson.Bytes(intent)));
                    if(reply.StatusCode!=System.Net.HttpStatusCode.OK) throw new Exception("Actual HTTP custody-role route did not prepare its scoped current request.");
                    using(var socket=new System.Net.Sockets.TcpClient())
                    using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        var uri=new Uri(address);await socket.ConnectAsync("127.0.0.1",uri.Port,deadline.Token);using var network=socket.GetStream();
                        string headers="POST /custody-lifecycle/prepare HTTP/1.1\r\nHost: localhost\r\ndapr-api-token: isolated-channel\r\nAuthorization: Bearer isolated-caller\r\nContent-Length: "+(CustodyBindingContract.CandidateRequestMessageBytes+1L)+"\r\nConnection: close\r\n\r\n";
                        await network.WriteAsync(Encoding.ASCII.GetBytes(headers),deadline.Token);using var reader=new StreamReader(network);
                        string line=await reader.ReadLineAsync(deadline.Token)??"";
                        if(!line.Contains(" 413 ",StringComparison.Ordinal) && !line.Contains(" 503 ",StringComparison.Ordinal)) throw new Exception("Actual oversized HTTP role body was not refused before body delivery: "+line);
                    }
                    using var malformed=new StringContent("{malformed");using var refusal=await http.PostAsync("/custody-lifecycle/prepare",malformed);
                    if(refusal.IsSuccessStatusCode) throw new Exception("Actual role HTTP endpoint accepted malformed framing.");
                }
                finally { await web.StopAsync(); }
            }
            var prepared = CustodyJson.Read<CustodyPreparedCall>(preparedResult.Bytes);
            if(prepared.WorkloadGrant?.Identity!="fixture-workload") throw new Exception("Prepared response lost its exact current workload binding.");
            var heldResponses=new List<(HttpContext Context,IResult Result)>();
            for(int index=0;index<4;index++)
            {
                var context=Context(CustodyJson.Bytes(intent));heldResponses.Add((context,await receiver.HandleAsync(context,"prepare",default)));
            }
            var saturated=Context(CustodyJson.Bytes(intent));var unread=new UnreadLargeBody();saturated.Request.Body=unread;
            await (await receiver.HandleAsync(saturated,"prepare",default)).ExecuteAsync(saturated);
            if(saturated.Response.StatusCode!=503 || unread.Reads!=0) throw new Exception("Saturated actual role endpoint allocated/read a candidate frame.");
            foreach(var response in heldResponses) await response.Result.ExecuteAsync(response.Context);
            bool failStartup=true;
            var startupReceiver=new CustodyIndependentRoleReceiver(fixture.Profile,installation,fixture.Store,dapr,roleKey,TimeProvider.System,transport)
                { ReceivingStartup=()=> { if(failStartup) { failStartup=false;throw new IOException("fixture startup failure"); } } };
            if((await Call(startupReceiver,"prepare",CustodyJson.Bytes(intent))).Status!=503 || (await Call(startupReceiver,"prepare",CustodyJson.Bytes(intent))).Status!=200)
                throw new Exception("Role startup failure leaked ownership.");
            transport.DuringBegin=()=>transport.WorkloadRevoked=true;
            if((await Call(receiver,"prepare",CustodyJson.Bytes(intent))).Status==200) throw new Exception("Revocation winning during prepare admission leaked a response.");
            transport.DuringBegin=null;transport.WorkloadRevoked=false;
            transport.DuringControlRead=()=>transport.WorkloadRevoked=true;
            if((await Call(receiver,"prepare",CustodyJson.Bytes(intent))).Status==200) throw new Exception("Revocation winning during control read leaked a response.");
            transport.DuringControlRead=null;transport.WorkloadRevoked=false;
            var late=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int enteredCount=0;
            transport.BeforeInvoke=()=> { if(Interlocked.Increment(ref enteredCount)==4) allEntered.TrySetResult();return late.Task; };var lateCalls=new List<(HttpContext Context,Task<IResult> Result)>();
            try
            {
            using(var cancelled=new CancellationTokenSource())
            {
                for(int index=0;index<4;index++) { var context=Context(CustodyJson.Bytes(intent));lateCalls.Add((context,receiver.HandleAsync(context,"prepare",cancelled.Token))); }
                await allEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
                cancelled.Cancel();
                foreach(var call in lateCalls) { var result=await call.Result;try { await result.ExecuteAsync(call.Context); }catch(OperationCanceledException) { } }
                var blocked=Context(CustodyJson.Bytes(intent));var neverRead=new UnreadLargeBody();blocked.Request.Body=neverRead;
                await (await receiver.HandleAsync(blocked,"prepare",default)).ExecuteAsync(blocked);
                if(blocked.Response.StatusCode!=503 || neverRead.Reads!=0) throw new Exception("Canceled actual role work released its receiving slot while private transport was still live.");
            }
            }
            finally { transport.BeforeInvoke=null;late.TrySetResult(); }
            var recoveryDeadline=DateTimeOffset.UtcNow.AddSeconds(2);int recoveredStatus;
            do { recoveredStatus=(await Call(receiver,"prepare",CustodyJson.Bytes(intent))).Status;if(recoveredStatus!=200) await Task.Delay(10); }
            while(recoveredStatus!=200 && DateTimeOffset.UtcNow<recoveryDeadline);
            if(recoveredStatus!=200) throw new Exception("Late role transport cleanup did not recover receiving capacity.");

            var authorization = await Call(receiver, "authorize", CustodyJson.Bytes(new { Intent = intent, Prepared = prepared, RequestSha256 = Fixture.Digest }));
            if (authorization.Status != 200) throw new Exception("Actual private-operation receiver did not bind the exact retained prepare.");
            transport.DuringControlRead=()=>transport.WorkloadRevoked=true;
            if((await Call(receiver,"authorize",CustodyJson.Bytes(new { Intent=intent,Prepared=prepared,RequestSha256=Fixture.Digest }))).Status==200)
                throw new Exception("Authorize response survived workload revocation during actual retained control read.");
            transport.DuringControlRead=null;transport.WorkloadRevoked=false;
            var issued = CustodyJson.Read<CustodyCallAuthorization>(authorization.Bytes);
            var permission = CustodyJson.Read<SignedStatement>(Convert.FromBase64String(issued.Metadata["custody-operation-proof"]));
            if (!roleKey.VerifyData(CustodyJson.Bytes(permission with { Signature = [] }), permission.Signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
                throw new Exception("Actual permission signature failed.");
            var ownerHandler = new CustodyOwnerOperationHandler(fixture.Profile, fixture.Store,
                new IndependentDaprAuthority(transport,transport.Binding("originals"),TimeProvider.System),"authorizer",roleKey,TimeProvider.System);
            var ownerGrant = new IndependentCallerGrant("unused","owner", "tenant|"+envelope.OwnerPurposeScope,"custody-owner",["custody-owner-phase:read-current"],DateTimeOffset.UtcNow.AddMinutes(1),false);
            var ownerState = new IndependentAuthorityState("enrollment","scope","trust-1",0,DateTimeOffset.UtcNow.AddMinutes(1),[],null,0,Fixture.Digest,[],[],[],[],[],[]);
            var ownerPayload = JsonSerializer.SerializeToElement(new { phase="read-current",tenant="tenant",ownerScope=envelope.OwnerPurposeScope,operationIdentity=(string?)null,transitionJson=(string?)null });
            var ownerCall = (CustodyOwnerAuthorizedCall?)await ownerHandler.HandleAsync("custody-owner-call",ownerPayload,ownerGrant,ownerState,default);
            if(ownerCall is null || CustodyBindingContract.Decode(Encoding.UTF8.GetBytes(ownerCall.RequestJson)) is not { } ownerWire
                || ownerWire.Envelope.Operation!="read-current" || !ownerCall.Metadata.ContainsKey("custody-operation-proof")) throw new Exception("Actual native owner-call framing unavailable.");
            try { await ownerHandler.HandleAsync("custody-owner-call",ownerPayload,ownerGrant with { ScopeReference="foreign" },ownerState,default); throw new Exception("Substituted owner scope admitted."); }
            catch(AuthorityUnavailableException) { }

            // Consecutive stable framing traverses the actual outer authority receiver, never an uncontrolled native transition.
            using var outerDapr=NSubstitute.Substitute.For<DaprClient>();int outerSaves=0,framingRevision=0;
            var framingGrant=ownerGrant with { BearerSha256=CustodyJson.Hash("framing-workload"u8),Methods=["custody-owner-call","custody-owner-phase:read-current","custody-owner-phase:verify-transition","custody-owner-phase:admit-transition","custody-owner-phase:record-transition","custody-owner-phase:recover-transition"] };
            var framingState=ownerState with { EnrollmentId="framing",Scope="framing",TrustRevision="framing",Callers=[framingGrant] };
            outerDapr.GetStateAndETagAsync<IndependentAuthorityState>("fixture-framing","head",NSubstitute.Arg.Any<ConsistencyMode?>(),NSubstitute.Arg.Any<IReadOnlyDictionary<string,string>>(),NSubstitute.Arg.Any<CancellationToken>()).Returns(_=>(framingState,framingRevision.ToString()));
            outerDapr.TrySaveStateAsync("fixture-framing","head",NSubstitute.Arg.Any<IndependentAuthorityState>(),NSubstitute.Arg.Any<string>(),NSubstitute.Arg.Any<StateOptions>(),NSubstitute.Arg.Any<IReadOnlyDictionary<string,string>>(),NSubstitute.Arg.Any<CancellationToken>()).Returns(call=> { if(call.ArgAt<string>(3)!=framingRevision.ToString()) return false;framingState=call.ArgAt<IndependentAuthorityState>(2);framingRevision++;outerSaves++;return true; });
            var outer=new IndependentAuthorityReceiver(outerDapr,new("framing","framing","framing","fixture-framing","head","unused",roleKey.ExportSubjectPublicKeyInfoPem(),"file:"+channelFile),roleKey,TimeProvider.System,ownerHandlers:[ownerHandler]);
            async Task<CustodyOwnerAuthorizedCall?> Frame(object value)
            {
                var context=new DefaultHttpContext { RequestServices=new ServiceCollection().AddLogging().BuildServiceProvider() };
                context.Request.Headers["dapr-api-token"]="isolated-channel";context.Request.Headers.Authorization="Bearer framing-workload";context.Response.Body=new MemoryStream();
                context.Request.Body=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new { Protocol="platform-independent-authority-v1",EnrollmentId="framing",Scope="framing",TrustRevision="framing",Method="custody-owner-call",Nonce=Guid.NewGuid().ToString("N"),Payload=value },CustodyBindingContract.Json));
                await (await outer.HandleAsync(context,"custody-owner-call",default)).ExecuteAsync(context);
                if(context.Response.StatusCode!=200) return null;
                using var result=JsonDocument.Parse(((MemoryStream)context.Response.Body).ToArray());return result.RootElement.GetProperty("Payload").Deserialize<CustodyOwnerAuthorizedCall>(CustodyBindingContract.Json);
            }
            foreach(string phase in new[]{"read-current","verify-transition","admit-transition","record-transition","recover-transition"})
            {
                var transition=new AnchoredStateTransition(envelope.OwnerPurposeScope,0,1,Fixture.Digest,CustodyJson.Hash(new byte[]{1}),new byte[]{1});
                var proof=envelope.Proof with { Action=phase,TargetVectorDigest=phase=="read-current"?envelope.Proof.TargetVectorDigest:transition.TargetDigest };string digest=CustodyJson.Hash(CustodyJson.Bytes(proof));var phaseFacts=facts with { ControlProofSha256=digest };
                transport.Control=new(proof,Signed(original with { SubjectSha256=digest,Payload=JsonSerializer.SerializeToElement(phaseFacts,CustodyBindingContract.Json) }),fixture.Sign("control",digest,phaseFacts));
                object input=new { phase,tenant="tenant",ownerScope=envelope.OwnerPurposeScope,operationIdentity=(string?)null,transitionJson=phase=="read-current"?null:JsonSerializer.Serialize(transition,CustodyBindingContract.Json) };
                var firstFrame=await Frame(input);var nextFrame=await Frame(input);
                if(firstFrame is null || nextFrame is null || firstFrame.RequestJson==nextFrame.RequestJson) throw new Exception("Repeated current owner framing failed or reused its nonce/challenge: "+phase);
            }
            transport.Control=new(envelope.Proof,Signed(original),controlStatement);
            transport.DuringControlRead=()=> { transport.DuringControlRead=null;framingState=framingState with { Revision=framingState.Revision+1,Callers=[framingGrant with { Revoked=true }] };framingRevision++; };
            int beforeRevocation=outerSaves;
            if(await Frame(new { phase="read-current",tenant="tenant",ownerScope=envelope.OwnerPurposeScope,operationIdentity=(string?)null,transitionJson=(string?)null }) is not null || outerSaves!=beforeRevocation)
                throw new Exception("Outer revocation winning during independent control read leaked a newly framed proof.");
            framingState=framingState with { Callers=[framingGrant with { Revoked=true }] };
            if(await Frame(new { phase="read-current",tenant="tenant",ownerScope=envelope.OwnerPurposeScope,operationIdentity=(string?)null,transitionJson=(string?)null }) is not null || outerSaves<10)
                throw new Exception("Current owner revocation did not deny or fresh framing lacked its exact publication ordering: saves="+outerSaves);
            fixture.Qualify();
            var wrap=fixture.Wrap("receiver-key"); var wrapEnvelope=wrap.Envelope;
            var wrapFacts=new ControlClaim(CustodyJson.Hash(CustodyJson.Bytes(wrapEnvelope.Proof)),Fixture.Digest,Fixture.Digest,Fixture.Digest,100,null,null);
            var wrapOriginal=original with { SubjectSha256=wrapFacts.ControlProofSha256,Payload=JsonSerializer.SerializeToElement(wrapFacts,CustodyBindingContract.Json) };
            transport.Control=new(wrapEnvelope.Proof,Signed(wrapOriginal),fixture.Sign("control",wrapFacts.ControlProofSha256,wrapFacts));
            var admission=new KeyAdmission(wrap.WrapInput!.KeyUse,wrapEnvelope.RequestDigest,fixture.Profile.ProfileDigest,
                new[]{"caller","sdk","sidecar","uds","grpc","tls","component","native"}.Select(role=>new KeyHolder("copy-"+role,role,"recipient","transport","owner",Fixture.Digest)).ToArray());
            transport.Observation=fixture.Sign("independent-observer",CustodyJson.Hash(CustodyJson.Bytes(admission)),admission);
            var wrapIntent=new CustodyCallIntent("wrap","tenant",wrapEnvelope.OwnerPurposeScope,wrapEnvelope.OperationId,wrapEnvelope.RequestDigest,
                JsonSerializer.Serialize(new { wrap.WrapInput.Identity,wrap.WrapInput.OperationId,wrap.WrapInput.ExpectedRevision,wrap.WrapInput.KekVersion,wrap.WrapInput.OpaqueWrappedKeyReference,wrap.WrapInput.KeyUse },CustodyBindingContract.Json),wrapEnvelope.RequestAttemptId,wrapEnvelope.Challenge);
            var keyPreparedResponse=await Call(receiver,"prepare",CustodyJson.Bytes(wrapIntent));
            if(keyPreparedResponse.Status!=200) throw new Exception("Valid current key closure prepare refused.");
            var keyPrepared=CustodyJson.Read<CustodyPreparedCall>(keyPreparedResponse.Bytes);
            if(keyPrepared.KeyClosureProfileDigest!=Fixture.Digest) throw new Exception("Current verified key closure digest omitted.");
            var keyAuthorization=await Call(receiver,"authorize",CustodyJson.Bytes(new { Intent=wrapIntent,Prepared=keyPrepared,RequestSha256=Fixture.Digest }));
            if(keyAuthorization.Status!=200 || !CustodyJson.Read<CustodyCallAuthorization>(keyAuthorization.Bytes).Metadata.ContainsKey("custody-key-admission")) throw new Exception("Exact independently signed key admission omitted.");
            transport.Observation=null;
            if((await Call(receiver,"authorize",CustodyJson.Bytes(new { Intent=wrapIntent,Prepared=keyPrepared,RequestSha256=Fixture.Digest }))).Status!=503) throw new Exception("Missing key admission did not close authorization.");
            transport.Control=new(envelope.Proof,Signed(original),controlStatement);
            int writes = transport.Begins;
            await Install(new(envelope.Proof, Signed(original with { IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-1), ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-31) }), controlStatement));
            if ((await Call(receiver, "prepare", CustodyJson.Bytes(intent))).Status != 503 || transport.Begins != writes)
                throw new Exception("Expired independent control reached original admission.");
            await Install(new(envelope.Proof with { TargetVectorDigest = new string('B', 64) }, Signed(original), controlStatement));
            if ((await Call(receiver, "prepare", CustodyJson.Bytes(intent))).Status != 503 || transport.Begins != writes)
                throw new Exception("Substituted control original reached admission.");
            var observerProfile=fixture.Profile with { Trust=fixture.Profile.Trust.Append(new("role-observer","independent-observer",roleKey.ExportSubjectPublicKeyInfoPem())).Append(new("native-evidence","native-evidence-original",guard.ExportSubjectPublicKeyInfoPem())).ToArray() };
            var observerInstallation=installation with { Role="observer",SignerId="role-observer" };
            var observer=new CustodyIndependentRoleReceiver(observerProfile,observerInstallation,fixture.Store,dapr,roleKey,TimeProvider.System,transport);
            var nativeOriginal=new SignedStatement("native-evidence-original","native-evidence",fixture.Profile.Enrollment.AuthorityId,fixture.Profile.Enrollment.AuthorityIncarnation,"tenant",fixture.Store.State.Head.Revision,fixture.Store.State.Head.Digest,intent.RequestDigest,DateTimeOffset.UtcNow.AddSeconds(-1),DateTimeOffset.UtcNow.AddSeconds(20),JsonSerializer.SerializeToElement(new { nativeOriginal="exact" }),[]);
            transport.Observation=Signed(nativeOriginal);
            if((await Call(observer,"observe",CustodyJson.Bytes(intent))).Status!=200) throw new Exception("Actual observer direct path refused its independently signed native original.");
            transport.DuringObservation=()=>transport.WorkloadRevoked=true;
            if((await Call(observer,"observe",CustodyJson.Bytes(intent))).Status==200) throw new Exception("Observer issued after workload revocation during native read.");
            transport.DuringObservation=null;transport.WorkloadRevoked=false;
            DateTimeOffset originalExpiry=transport.WorkloadUntil;
            foreach(string boundary in new[]{"control","begin","native"})
            {
                var clock=new RoleClock();transport.WorkloadUntil=clock.Now.AddSeconds(2);
                Action expire=()=>clock.Now=transport.WorkloadUntil;
                transport.DuringControlRead=boundary=="control"?expire:null;transport.DuringBegin=boundary=="begin"?expire:null;transport.DuringObservation=boundary=="native"?expire:null;
                var currentReceiver=boundary=="native"?new CustodyIndependentRoleReceiver(observerProfile,observerInstallation,fixture.Store,dapr,roleKey,clock,transport):new CustodyIndependentRoleReceiver(fixture.Profile,installation,fixture.Store,dapr,roleKey,clock,transport);
                // Retain a fresh exact preparation identity; existing historical originals are left byte-identical.
                var expiring=intent with { RequestAttemptId="expiry-"+boundary,Challenge="expiry-"+boundary };
                if(boundary!="native") transport.Control=new(envelope.Proof,Signed(original),controlStatement);
                if((await Call(currentReceiver,boundary=="native"?"observe":"prepare",CustodyJson.Bytes(expiring))).Status==200)
                    throw new Exception("Role proof escaped exclusive workload expiry during "+boundary+" read/admission.");
                transport.DuringControlRead=null;transport.DuringBegin=null;transport.DuringObservation=null;
            }
            transport.WorkloadUntil=originalExpiry;
            if (fixture.Native.Encrypts != 0 || fixture.Native.Decrypts != 0) throw new Exception("Authority test ran a native key operation.");
        }
        finally { File.Delete(channelFile); }
    }
    private static async Task<(int Status, byte[] Bytes)> Call(CustodyIndependentRoleReceiver receiver, string method, byte[] bytes)
    {
        var context=Context(bytes);
        await (await receiver.HandleAsync(context, method, default)).ExecuteAsync(context);
        return (context.Response.StatusCode, ((MemoryStream)context.Response.Body).ToArray());
    }
    private static HttpContext Context(byte[] bytes)
    {
        var context=new DefaultHttpContext { RequestServices=new ServiceCollection().AddLogging().BuildServiceProvider() };
        context.Request.Headers["dapr-api-token"]="isolated-channel";context.Request.Headers.Authorization="Bearer isolated-caller";
        context.Request.Body=new MemoryStream(bytes);context.Response.Body=new MemoryStream();return context;
    }
    private sealed class RoleClock:TimeProvider
    { internal DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now; }
    private sealed class UnreadLargeBody:MemoryStream
    {
        internal int Reads;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default) { Reads++;throw new Exception("Saturated body was read."); }
        public override long Length=>CustodyBindingContract.CandidateRequestMessageBytes+1L;
    }
    private sealed class AuthorityTransport : ISecretsCredentialBoundDaprInvoker, IDisposable
    {
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly Dictionary<string, IndependentOriginal> _originals = [];
        internal int Begins;
        internal bool WorkloadRevoked;
        internal DateTimeOffset WorkloadUntil=DateTimeOffset.UtcNow.AddMinutes(5);
        internal Action? DuringBegin;
        internal Action? DuringObservation { get; set; }
        internal Func<Task>? BeforeInvoke { get; set; }

        internal RetainedControlOriginal? Control;
        internal Action? DuringControlRead;
        internal SignedStatement? Observation;
        internal IndependentDaprAuthorityBinding Binding(string scope) => new("independent-owner", "file:/isolated", _key.ExportSubjectPublicKeyInfoPem(), "enrollment", scope, "trust-1");
        public Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken) => throw new Exception("Uncredentialed source fallback.");
        public async Task<string?> InvokeCredentialBoundAsync(string appId, string credentialReference, string method, string jsonBody, CancellationToken cancellationToken)
        {
            if(BeforeInvoke is { } wait) await wait();
            using var parsed = JsonDocument.Parse(jsonBody); var request = parsed.RootElement; var payload = request.GetProperty("Payload");
            object result;
            if(method.EndsWith("custody-role-authorize",StringComparison.Ordinal))
            {
                var intent=payload.GetProperty("intent").Deserialize<CustodyCallIntent>(CustodyBindingContract.Json)!;
                var grant=new CustodyRoleWorkloadGrant("fixture-workload",payload.GetProperty("bearerSha256").GetString()!,payload.GetProperty("role").GetString()!,intent.TenantId+"|"+intent.OwnerPurposeScope,CustodyJson.Hash(CustodyJson.Bytes(intent)),Fixture.Digest,WorkloadUntil);
                result=WorkloadRevoked?null!:payload.TryGetProperty("grant",out _)?new CustodyRoleIssuance(grant,payload.GetProperty("method").GetString()!,payload.GetProperty("responseSha256").GetString()!):grant;
            }
            else if (method.EndsWith("custody-control-original", StringComparison.Ordinal)) { DuringControlRead?.Invoke();result = Control!; }
            else if (method.EndsWith("custody-native-observation-original",StringComparison.Ordinal)) { DuringObservation?.Invoke();result=Observation!; }
            else if (method.EndsWith("qualification-begin-original", StringComparison.Ordinal))
            {
                DuringBegin?.Invoke();Begins++; string id = payload.GetProperty("requestId").GetString()!; var original = payload.GetProperty("original").Clone();
                bool first = !_originals.ContainsKey(id); if (first) _originals.Add(id, new(original, null));
                result = new { Exact = _originals[id].Request.GetRawText() == original.GetRawText(), First = first };
            }
            else result = _originals[payload.GetProperty("requestId").GetString()!];
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var response = new Response("enrollment", request.GetProperty("Scope").GetString()!, "trust-1", request.GetProperty("Method").GetString()!, request.GetProperty("Nonce").GetString()!,
                CustodyJson.Hash(Encoding.UTF8.GetBytes(jsonBody)), now, now + 30, JsonSerializer.SerializeToElement(result, CustodyBindingContract.Json), []);
            response = response with { Signature = _key.SignData(CustodyJson.Bytes(response), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence) };
            return JsonSerializer.Serialize(response, CustodyBindingContract.Json);
        }
        public void Dispose() => _key.Dispose();
        private sealed record Response(string EnrollmentId, string Scope, string TrustRevision, string Method, string Nonce, string RequestSha256,
            long ObservedAt, long ValidUntil, JsonElement Payload, byte[] Signature);
    }
}
