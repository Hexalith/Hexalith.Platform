using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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
            var prepared = CustodyJson.Read<CustodyPreparedCall>(preparedResult.Bytes);
            var authorization = await Call(receiver, "authorize", CustodyJson.Bytes(new { Intent = intent, Prepared = prepared, RequestSha256 = Fixture.Digest }));
            if (authorization.Status != 200) throw new Exception("Actual private-operation receiver did not bind the exact retained prepare.");
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
            if (fixture.Native.Encrypts != 0 || fixture.Native.Decrypts != 0) throw new Exception("Authority test ran a native key operation.");
        }
        finally { File.Delete(channelFile); }
    }
    private static async Task<(int Status, byte[] Bytes)> Call(CustodyIndependentRoleReceiver receiver, string method, byte[] bytes)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
        context.Request.Headers["dapr-api-token"] = "isolated-channel"; context.Request.Headers.Authorization = "Bearer isolated-caller";
        context.Request.Body = new MemoryStream(bytes); context.Response.Body = new MemoryStream();
        await (await receiver.HandleAsync(context, method, default)).ExecuteAsync(context);
        return (context.Response.StatusCode, ((MemoryStream)context.Response.Body).ToArray());
    }
    private sealed class AuthorityTransport : ISecretsCredentialBoundDaprInvoker, IDisposable
    {
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly Dictionary<string, IndependentOriginal> _originals = [];
        internal int Begins;
        internal RetainedControlOriginal? Control;
        internal SignedStatement? Observation;
        internal IndependentDaprAuthorityBinding Binding(string scope) => new("independent-owner", "file:/isolated", _key.ExportSubjectPublicKeyInfoPem(), "enrollment", scope, "trust-1");
        public Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken) => throw new Exception("Uncredentialed source fallback.");
        public Task<string?> InvokeCredentialBoundAsync(string appId, string credentialReference, string method, string jsonBody, CancellationToken cancellationToken)
        {
            using var parsed = JsonDocument.Parse(jsonBody); var request = parsed.RootElement; var payload = request.GetProperty("Payload");
            object result;
            if (method.EndsWith("custody-role-authorize", StringComparison.Ordinal)) result = new { Allowed = true };
            else if (method.EndsWith("custody-control-original", StringComparison.Ordinal)) result = Control!;
            else if (method.EndsWith("custody-native-observation-original",StringComparison.Ordinal)) result = Observation!;
            else if (method.EndsWith("qualification-begin-original", StringComparison.Ordinal))
            {
                Begins++; string id = payload.GetProperty("requestId").GetString()!; var original = payload.GetProperty("original").Clone();
                bool first = !_originals.ContainsKey(id); if (first) _originals.Add(id, new(original, null));
                result = new { Exact = _originals[id].Request.GetRawText() == original.GetRawText(), First = first };
            }
            else result = _originals[payload.GetProperty("requestId").GetString()!];
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var response = new Response("enrollment", request.GetProperty("Scope").GetString()!, "trust-1", request.GetProperty("Method").GetString()!, request.GetProperty("Nonce").GetString()!,
                CustodyJson.Hash(Encoding.UTF8.GetBytes(jsonBody)), now, now + 30, JsonSerializer.SerializeToElement(result, CustodyBindingContract.Json), []);
            response = response with { Signature = _key.SignData(CustodyJson.Bytes(response), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence) };
            return Task.FromResult<string?>(JsonSerializer.Serialize(response, CustodyBindingContract.Json));
        }
        public void Dispose() => _key.Dispose();
        private sealed record Response(string EnrollmentId, string Scope, string TrustRevision, string Method, string Nonce, string RequestSha256,
            long ObservedAt, long ValidUntil, JsonElement Payload, byte[] Signature);
    }
}
