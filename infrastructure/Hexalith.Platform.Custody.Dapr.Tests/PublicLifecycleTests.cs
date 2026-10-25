using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Grpc.Net.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Protection;
using Hexalith.Platform.Secrets.Runtime;

internal static class PublicLifecycleTests
{
    internal static async Task Run()
    {
        using var f = new Fixture(); var handler = new SdkBridge(f);
        using var sdk = Sdk(handler); var source = new LifecycleAuthorizer(f); var client = Client(f, sdk, source);
        var identity = new CustodyKeyObjectIdentity("tenant", "public-root", PlatformKeyPurpose.InteractionRootDek, "alias", "v1", "lifecycle", "store", "contract");
        var wrapUse = new CustodyKeyUse("wrap-lease", "wrap-delivery", "recipient", "transport", "public-root");
        byte[] input = Fixture.Root.ToArray();
        var registration = await client.WrapAsync(identity, "public-wrap", f.Store.State.Head.Revision, "kek", "public-root", wrapUse, input)
            ?? throw new Exception("Public wrap unavailable.");
        Check(input.AsSpan().SequenceEqual(Fixture.Root) && f.Native.Encrypts == 1, "Public wrap failed or stole caller input.");
        Check(source.Requests.All(s => !s.Contains(Convert.ToBase64String(input), StringComparison.Ordinal) && !s.Contains("RootKey", StringComparison.Ordinal)), "Authorizer received key material.");
        var lookup = await client.LookupOriginalAsync(registration!);
        Check(lookup.Status == CustodyKeyLifecycleStatus.Wrapped && lookup.ReceiptId == registration!.WrapReceiptId, "Public original registration lookup failed.");
        var use = new CustodyKeyUse("unwrap-lease", "unwrap-delivery", "recipient", "transport", "public-root");
        var released = await client.UnwrapAsync(registration, "public-unwrap", use);
        Check(released.State == "Released" && released.Key!.Bytes.Span.SequenceEqual(Fixture.Root), "Actual original root did not cross SDK/service boundary.");
        Check(!JsonSerializer.Serialize(released).Contains(Convert.ToBase64String(Fixture.Root), StringComparison.Ordinal)
            && !JsonSerializer.Serialize(released.Key).Contains("Bytes", StringComparison.Ordinal), "Public result serialization leaked key bytes.");
        var borrowed = released.Key!.Bytes; released.Key.Dispose();
        Check(borrowed.Span.IndexOfAnyExcept((byte)0) == -1, "Owned root was not zeroed.");
        handler.ReplayCapturedUnwrap = true;
        var replayed = await client.UnwrapAsync(registration, "public-unwrap", use);
        Check(replayed.Key is null && replayed.State == "Unknown" && f.Native.Decrypts == 1,
            "A valid earlier signed unwrap response replayed against a fresh challenge exposed a key or repeated native release.");
        handler.ReplayCapturedUnwrap = false;
        var duplicate = await client.UnwrapAsync(registration, "public-unwrap", use);
        Check(duplicate.Key is null && duplicate.State == "ResultDurable" && f.Native.Decrypts == 1, "Duplicate delivery released a key twice.");
        source.Mode = "missing-completion";
        Check(!(await client.CompleteKeyUseAsync(registration, "complete-public", use)).HoldersCompleted, "Local cleanup replaced independent completion.");
        source.Mode = "normal";
        Check((await client.CompleteKeyUseAsync(registration, "complete-public", use)).HoldersCompleted, "Exact independent completion did not close holders.");
        var pin = new CustodyKeyLifecycleRequest(identity, "public-pin", CustodyKeyLifecycleAction.Pin, f.Store.State.Head.Revision, "hold", "registered", 2, "decision");
        var pinned = await client.ExecuteLifecycleAsync(registration, pin);
        Check(pinned.Status == CustodyKeyLifecycleStatus.Pinned && (await client.LookupOriginalAsync(registration, pin)) == pinned, "Signed pin original failed.");
        var port = new DaprCustodyKeyLifecycleProvider(client, new ManifestLocator(null));
        var owner = new CustodyOwnerAuthority(sdk, "custody", "tenant", f.OwnerScope, Encoding.UTF8.GetString(CustodyJson.Bytes(f.Profile.Enrollment)),
            f.Profile.ComponentPublicKeyPem, new OwnerCalls(f));
        var authority = new DaprCustodyKeyLifecycleAuthority("tenant", owner, client, port, new RegistrationLocator(registration));
        Check(await authority.VerifyRegistrationAsync(registration) && await authority.VerifyOutcomeAsync(pin, pinned), "Independent Dapr lifecycle authority refused original.");
        Check(!await authority.VerifyOutcomeAsync(pin, pinned with { ReceiptId = "forged" }) && !await authority.ValidateStateAsync("foreign", 0, Fixture.Digest), "Substituted authority evidence accepted.");
        var unpin = pin with { OperationId = "public-unpin", Action = CustodyKeyLifecycleAction.Unpin, ExpectedRevision = f.Store.State.Head.Revision };
        Check((await client.ExecuteLifecycleAsync(registration, unpin)).Status == CustodyKeyLifecycleStatus.Unpinned, "Public exact unpin failed.");
        int sends = handler.Calls;
        foreach (string mode in new[] { "refuse", "wrong-scope", "wrong-hash", "stale", "bad-signature" })
        {
            source.Mode = mode;
            Check((await client.LookupOriginalAsync(registration)).Status == CustodyKeyLifecycleStatus.Unknown, mode + " granted an original.");
        }
        Check(f.Native.Encrypts == 1 && f.Native.Decrypts == 1 && f.Native.Schedules == 0, "Refused calls caused a physical effect.");
        Check(handler.Calls > sends, "Signed stale/refusal controls were not exercised at service boundary.");
        source.Mode = "normal"; handler.CorruptResultProof = true;
        Check((await client.LookupOriginalAsync(registration)).Status == CustodyKeyLifecycleStatus.Unknown, "Forged response signature accepted.");
        handler.CorruptResultProof = false; handler.LoseNextKeyAcknowledgement = true;
        var lost = use with { KeyUseLeaseId = "lost-lease", DeliveryAttemptId = "lost-delivery" };
        Check((await client.UnwrapAsync(registration, "lost-unwrapped", lost)).Key is null, "Lost response exposed a key.");
        Check((await client.UnwrapAsync(registration, "lost-unwrapped", lost)).Key is null && f.Native.Decrypts == 2, "Lost response retry replayed release.");
        Check(f.Store.Objects.Values.All(b => !Encoding.UTF8.GetString(b).Contains(Convert.ToBase64String(Fixture.Root), StringComparison.Ordinal)), "Public flow persisted plaintext key.");
        CryptographicOperations.ZeroMemory(input);
    }

    internal static async Task AdmitManifest(Fixture f, CustodyBindingRequestV1 original)
    {
        var handler = new SdkBridge(f); using var sdk = Sdk(handler); var client = Client(f, sdk, new LifecycleAuthorizer(f));
        var provider = new DaprAtomicDeletionManifestProvider(client);
        var result = await provider.ConsumeAsync(original.Manifest!.Original, original.Manifest.ProtectionReservationReceiptId);
        Check(result.State == DeletionManifestProviderState.Unknown && result.TargetReceipts.Count == 0
            && f.Store.State.Checkpoint.Cycle?.Manifest.Manifest?.Original.Targets.Count == 2, "Public manifest admission did not reserve the complete vector and return signed Unknown.");
    }

    internal static async Task ManifestRecovery(Fixture f, CustodyBindingRequestV1 original)
    {
        var handler = new SdkBridge(f); using var sdk = Sdk(handler); var source = new LifecycleAuthorizer(f);
        // The fixture admitted its deterministic operation using the same public identity before B1.

        var client = Client(f, sdk, source); var provider = new DaprAtomicDeletionManifestProvider(client);
        var result = await provider.LookupAsync(original.Manifest!.Original, original.Manifest.ProtectionReservationReceiptId);
        Check(result.State == DeletionManifestProviderState.Consumed && result.TargetReceipts.Count == 2, "Public provider failed complete signed original recovery.");
        var target = original.Manifest.Original.Targets[0];
        var registration = f.Store.State.Checkpoint.Registrations.Values.Single(r => r.Registration.Identity.ObjectId == target.AgentInteractionId).Registration;
        var lifecycle = new DaprCustodyKeyLifecycleProvider(client, new ManifestLocator(original.Manifest.Original));
        var destroy = new CustodyKeyLifecycleRequest(registration.Identity, "per-key-original", CustodyKeyLifecycleAction.Destroy,
            f.Store.State.Head.Revision, null, "barrier", 3, "decision", original.Manifest.ProtectionReservationReceiptId);
        string digest = CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(destroy));
        var perKey = await lifecycle.LookupAsync(registration, destroy, digest);
        Check(perKey.Status == CustodyKeyLifecycleStatus.Destroyed && perKey.ReceiptId == result.TargetReceipts[0].ReceiptId
            && perKey.RestoreBarrierReceiptId == "restore", "Per-key destroy did not recover its complete original manifest/vector.");
        Check(await lifecycle.ExecuteAsync(registration, destroy, digest) == perKey, "Per-key retry replaced the original complete manifest receipt.");
        foreach (var substituted in new[] {
            registration.Identity with { KeyVersion = "other-key" }, registration.Identity with { LifecycleVersion = "other-lifecycle" },
            registration.Identity with { StoreTarget = "other-store" }, registration.Identity with { ContractVersion = "other-contract" } })
        {
            var changedRegistration = registration with { Identity = substituted };
            var changedDestroy = destroy with { Identity = substituted };
            var changed = await lifecycle.LookupAsync(changedRegistration, changedDestroy,
                CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(changedDestroy)));
            Check(changed.Status == CustodyKeyLifecycleStatus.Unknown, "Changed complete root identity inherited target-triple destruction evidence.");
        }
        var subset = original.Manifest.Original with { Targets = [target] };
        var incomplete = new DaprCustodyKeyLifecycleProvider(client, new ManifestLocator(subset));
        Check((await incomplete.ExecuteAsync(registration, destroy, digest)).Status == CustodyKeyLifecycleStatus.Unknown, "Subset manifest became per-key authority.");
        // Exercise the actual qualification wrapper through the SDK/component boundary after losing the local case-result acknowledgement.
        var localOriginals=new QualificationOriginalFixture();
        var qualificationScope=new System.Text.Json.Nodes.JsonObject { ["tenantId"]=registration.Identity.TenantId };
        var qualificationRequest=new System.Text.Json.Nodes.JsonObject { ["caseId"]="S3.lifecycle.destroy",["requestId"]="fixture-wrapper-lost-result",["enrolledScope"]=qualificationScope };
        var qualificationInput=new QualificationLifecycleCaseInput(registration,Convert.ToHexStringLower(SHA256.HashData(QualificationProofVerifier.CanonicalBytes(qualificationScope))),
            Manifest:original.Manifest.Original,ReservationReceiptId:original.Manifest.ProtectionReservationReceiptId);
        var wrapper=new QualificationLifecycleCaseWorkflow(client,localOriginals,new Dictionary<string,QualificationLifecycleCaseInput>{{"S3.lifecycle.destroy",qualificationInput}});
        await localOriginals.BeginAsync("executor","fixture-wrapper-lost-result:case-result",qualificationRequest,default);
        var originalHead=f.Store.State.Head;int originalSends=f.Native.Schedules,requests=source.Requests.Count;
        var recoveredWrapper=await wrapper.LookupAsync(qualificationRequest,default)??throw new Exception("Qualification wrapper stranded an independently recoverable manifest original.");
        Check(recoveredWrapper["actual"]!["Result"]!["TargetReceipts"]!.AsArray().Count==2,"Qualification wrapper dropped the original receipt vector.");
        Check(source.Requests.Skip(requests).Select(value=> { using var parsed=JsonDocument.Parse(value);var raw=parsed.RootElement;return (raw.TryGetProperty("Intent",out var intent)?intent:raw).Deserialize<CustodyCallIntent>(CustodyBindingContract.Json)!; }).All(value=>value.Operation is "lookup-original" or "read-original-page"),
            "Qualification recovery invoked an effect entrypoint instead of the exact original reader.");
        Check(f.Store.State.Head==originalHead && f.Native.Schedules==originalSends,"Qualification original recovery repeated native work.");
        var before = f.Store.State.Head; int sends = f.Native.Schedules;
        handler.CorruptPage = true;
        Check((await provider.LookupAsync(original.Manifest.Original, original.Manifest.ProtectionReservationReceiptId)).State == DeletionManifestProviderState.Unknown, "Corrupt original page yielded Consumed.");
        handler.CorruptPage = false; handler.LoseNextPage = true;
        Check((await provider.LookupAsync(original.Manifest.Original, original.Manifest.ProtectionReservationReceiptId)).State == DeletionManifestProviderState.Unknown, "Missing original page yielded Consumed.");
        Check((await provider.LookupAsync(original.Manifest.Original, original.Manifest.ProtectionReservationReceiptId)).State == DeletionManifestProviderState.Consumed, "Lost page recovery changed original.");
        Check(f.Store.State.Head == before && f.Native.Schedules == sends, "Original recovery started a physical effect.");
    }
    private sealed class QualificationOriginalFixture:IQualificationOriginalStore
    {
        private readonly Dictionary<string,(System.Text.Json.Nodes.JsonObject Original,System.Text.Json.Nodes.JsonObject? Result)> _items=[];
        public Task<bool> BeginAsync(string role,string id,System.Text.Json.Nodes.JsonObject original,CancellationToken token)
        {
            if(_items.TryGetValue(id,out var old)) { if(!System.Text.Json.Nodes.JsonNode.DeepEquals(old.Original,original)) throw new Exception("Changed qualification fixture original.");return Task.FromResult(false); }
            _items[id]=((System.Text.Json.Nodes.JsonObject)original.DeepClone(),null);return Task.FromResult(true);
        }
        public Task RetainAsync(string role,string id,System.Text.Json.Nodes.JsonObject result,CancellationToken token)
        { var old=_items[id];if(old.Result is not null && !System.Text.Json.Nodes.JsonNode.DeepEquals(old.Result,result)) throw new Exception("Changed fixture result.");_items[id]=(old.Original,(System.Text.Json.Nodes.JsonObject)result.DeepClone());return Task.CompletedTask; }
        public Task<System.Text.Json.Nodes.JsonObject?> LookupResultAsync(string role,string id,CancellationToken token)=>Task.FromResult(_items.GetValueOrDefault(id).Result);
    }
    internal static CustodyLifecycleClient Client(Fixture f, DaprClient sdk, LifecycleAuthorizer source, TimeProvider? clock = null) => new(sdk,
        new Dictionary<string, string> { [CustodyLifecycleClient.LogicalRole] = "custody" }, f.OwnerScope,
        Encoding.UTF8.GetString(CustodyJson.Bytes(f.Profile.Enrollment)), f.Profile.ComponentPublicKeyPem,
        new DaprCustodyAuthorizerClient(source, new Dictionary<string, string> { [DaprCustodyAuthorizerClient.LogicalRole] = "authorizer" }, "file:/tmp/fixture-private-reference")) { OperationClock = clock ?? TimeProvider.System };
    private static DaprClient Sdk(SdkBridge handler) => new DaprClientBuilder().UseGrpcEndpoint("http://127.0.0.1:1")
        .UseGrpcChannelOptions(new GrpcChannelOptions { HttpHandler = handler }).Build();
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}

// Disposable independent issuer. The production adapter contains no implementation of these governing authorities.
internal sealed class LifecycleAuthorizer(Fixture fixture) : ISecretsCredentialBoundDaprInvoker
{
    internal string Mode = "normal";
    internal Action<string>? Stage { get; set; }
    internal List<string> Requests = [];
    public Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken token)
        => throw new Exception("Uncredentialed authorizer fallback.");
    public async Task<string?> InvokeCredentialBoundAsync(string appId, string credentialReference, string method, string jsonBody, CancellationToken token)
    {
        Requests.Add(jsonBody); Stage?.Invoke(method); token.ThrowIfCancellationRequested();
        if (appId != "authorizer" || credentialReference != "file:/tmp/fixture-private-reference" || Mode == "refuse") return null;
        using var document = JsonDocument.Parse(jsonBody); var root = document.RootElement;
        if (method.EndsWith("/prepare", StringComparison.Ordinal))
        {
            var intent = root.Deserialize<CustodyCallIntent>(CustodyBindingContract.Json)!;
            using var body = JsonDocument.Parse(intent.BodyJson); var b = body.RootElement;
            T? Read<T>(string name) => b.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.Deserialize<T>(CustodyBindingContract.Json) : default;
            var registration = Read<CustodyKeyRegistration>("Registration"); var lifecycle = Read<CustodyKeyLifecycleRequest>("Lifecycle");
            var manifest = Read<CustodyBindingManifestV1>("Manifest"); var page = Read<CustodyBindingPageReadV1>("Page");
            var request = fixture.Request(intent.Operation, registration, lifecycle, manifest, page: page);
            var e = request.Envelope with { OperationId = intent.OperationId, RequestDigest = intent.RequestDigest,
                RequestAttemptId = intent.RequestAttemptId, Challenge = intent.Challenge };
            if (Mode == "wrong-scope") e = e with { OwnerPurposeScope = "foreign" };
            if (Mode == "stale") e = e with { CheckpointDigest = Fixture.Digest, Proof = e.Proof with { CheckpointDigest = Fixture.Digest } };
            return JsonSerializer.Serialize(new CustodyPreparedCall(JsonSerializer.Serialize(e, CustodyBindingContract.Json), CustodyBindingContract.DefaultMessageBytes,
                Fixture.Digest, Fixture.Digest), CustodyBindingContract.Json);
        }
        var call = root.GetProperty("Intent").Deserialize<CustodyCallIntent>(CustodyBindingContract.Json)!;
        var prepared = root.GetProperty("Prepared").Deserialize<CustodyPreparedCall>(CustodyBindingContract.Json)!;
        var envelope = JsonSerializer.Deserialize<CustodyBindingEnvelopeV1>(prepared.EnvelopeJson, CustodyBindingContract.Json)!;
        string digest = root.GetProperty("RequestSha256").GetString()!; fixture.Qualify();
        var permission = new OperationPermission(envelope.Operation, envelope.OperationId, envelope.RequestAttemptId, envelope.Challenge, envelope.OwnerPurposeScope, fixture.Profile.ProfileDigest);
        var facts = new ControlClaim(CustodyJson.Hash(CustodyJson.Bytes(envelope.Proof)), Fixture.Digest, Fixture.Digest, Fixture.Digest, 100,
            envelope.Operation == "pin" ? null : envelope.Proof.DispatchRevision, envelope.Operation == "pin" ? null : envelope.Proof.DispatchReceiptId,
            envelope.Operation == "pin" ? new("reservation", envelope.Proof.HoldId!, envelope.Proof.TargetVectorDigest,
                envelope.Proof.GuardRevision!.Value, envelope.Proof.GuardReceiptId!, "RegisteredDispatchBlocked") : null);
        var operation = fixture.Sign("private-operation", digest, permission);
        if (Mode == "stale")
        {
            var current = fixture.Store.State;
            fixture.Store.State = current with { Head = current.Head with { Revision = current.Head.Revision - 1 } };
            try { operation = fixture.Sign("private-operation", digest, permission); }
            finally { fixture.Store.State = current; }
        }
        if (Mode == "bad-signature") operation = operation with { Signature = [1, 2, 3] };
        var metadata = new Dictionary<string, string> { ["custody-operation-proof"] = Convert.ToBase64String(CustodyJson.Bytes(operation)),
            ["custody-control-proof"] = Convert.ToBase64String(CustodyJson.Bytes(fixture.Sign("control", CustodyJson.Hash(CustodyJson.Bytes(envelope.Proof)), facts))) };
        using var publicBody = JsonDocument.Parse(call.BodyJson);
        if (publicBody.RootElement.TryGetProperty("KeyUse", out var keyUseJson))
        {
            var use = keyUseJson.Deserialize<CustodyBindingKeyUseV1>(CustodyBindingContract.Json)!;
            if (call.Operation == "complete-key-use" && Mode != "missing-completion")
            {
                string id = "key-" + CustodyJson.Hash(Encoding.UTF8.GetBytes(CustodyJson.DeliveryId(use)));
                var reference = fixture.Store.State.Checkpoint.NativeResults[id + ":admission"];
                var admitted = CustodyJson.Read<SignedStatement>(await fixture.Store.ReadImmutableAsync(reference, token));
                var holders = CustodyJson.Read<KeyAdmission>(CustodyJson.Bytes(admitted.Payload)).Holders;
                var complete = new KeyCompletion(use, reference.Sha256, holders.Select(h => new HolderCompletion(h.CopyId, h.ProcessIncarnation,
                    h.TransportIncarnation, Fixture.Digest, Fixture.Digest, "VerifiedZeroed")).ToArray());
                metadata["custody-key-admission"] = Convert.ToBase64String(CustodyJson.Bytes(fixture.Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(complete)), complete)));
            }
            else if (call.Operation is "wrap" or "unwrap")
            {
                var admission = new KeyAdmission(use, call.RequestDigest, fixture.Profile.ProfileDigest, new[] { "caller", "sdk", "sidecar", "uds", "grpc", "tls", "component", "native" }
                    .Select(role => new KeyHolder("copy-" + use.KeyUseLeaseId + "-" + role, role, use.RecipientProcessIncarnation, use.TransportIncarnation, "owner", Fixture.Digest)).ToArray());
                metadata["custody-key-admission"] = Convert.ToBase64String(CustodyJson.Bytes(fixture.Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(admission)), admission)));
            }
        }
        return JsonSerializer.Serialize(new CustodyCallAuthorization(Mode == "wrong-hash" ? Fixture.Digest : digest, metadata), CustodyBindingContract.Json);
    }
}

internal sealed class ManifestLocator(DeletionBatchConsumptionRequest? original) : ICustodyReservedManifestSource
{
    public Task<DeletionBatchConsumptionRequest?> FindAsync(string reservationReceiptId, CancellationToken token)
        => Task.FromResult(reservationReceiptId == "reservation" ? original : null);
}
internal sealed class RegistrationLocator(CustodyKeyRegistration registration) : ICustodyKeyRegistrationSource
{
    public Task<CustodyKeyRegistration?> FindAsync(CustodyKeyObjectIdentity identity, CancellationToken token)
        => Task.FromResult<CustodyKeyRegistration?>(identity == registration.Identity ? registration : null);
}
