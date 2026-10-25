using System.Security.Cryptography;
using System.Text.Json;
using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using Dapr.Client;
using Grpc.Net.Client;
using Dapr.Proto.Components.V1;
using Google.Protobuf;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Hexalith.Platform.Custody.Dapr;

// All keys, stores and observer claims below are disposable component fixtures. No network/native
// resource is opened. A fixture PASS is source evidence only and cannot satisfy physical qualification.
var tests = new (string, Func<Task>)[] {
    ("missing-qualification-and-enrollment-close-effects", Missing),
    ("ephemeral-key-delivery-duplicate-and-holder-completion", Keys),
    ("lost-wrap-result-recovers-without-native-resend", LostWrap),
    ("registration-only-original-lookup-and-substitution-denial", RegistrationLookup),
    ("independent-owner-genesis-and-consecutive-transitions", Anchors),
    ("hold-pin-release-and-complete-manifest-reservation", Holds),
    ("single-head-cas-and-app-restore-exclusions", Restore),
    ("complete-protobuf-unknown-field-and-metadata-bounds", Frames),
    ("shared-cut-independent-closure-and-exact-original-pages", Lifecycle),
    ("public-owner-adapter-real-sdk-signed-genesis-and-transition-wire", OwnerAdapter),
    ("eventstore-published-owner-bridge-exact-framing-and-recovery", EventStoreOwnerBridgeTests.Run),
    ("real-native-store-identity-immutable-cas-and-loss", NativeStoreTests.Run),
    ("serving-startup-and-disjoint-profile-lanes", ReviewRegressionTests.StartupAndProfiles),
    ("canonical-delivery-tuples-and-retained-use-denial", ReviewRegressionTests.DeliveryTuples),
    ("signed-pre-cycle-hold-reservation-denial", ReviewRegressionTests.SignedHoldReservations),
    ("binding-service-init-and-invoke-boundary", ServiceBoundaryTests.Run),
    ("real-aws-key-adapters-and-contexts", AwsKeyAdapterTests.Run),
    ("uncertain-successor-retirement-abandonment-and-replacement", SuccessorRetirement),
    ("maximum-manifest-transition-immutable-index-roundtrip", CheckpointCapacityTests.Run),
};
if (args.Length > 0) tests = tests.Where(t => args.Contains(t.Item1, StringComparer.Ordinal)).ToArray();
int failed = 0;
foreach (var (name, run) in tests) try { await run(); Console.WriteLine("PASS " + name); }
catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + " " + e); }
Console.WriteLine($"sourceComponentOnly=true physicalQualification=false passed={tests.Length - failed} failed={failed}");
return failed == 0 ? 0 : 1;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static async Task Closed(Func<Task> action) { try { await action(); } catch (AuthorityUnavailableException) { return; } catch (AuthorityConflictException) { return; } throw new Exception("Expected closed result."); }
static async Task Missing() {
    using var f = new Fixture(); var request = f.Wrap("one");
    await Closed(() => f.Call(request, qualified: false)); Check(f.Native.Encrypts == 0, "Missing qualification sent native key.");
    f.Store.State = f.Store.State with { Checkpoint = f.Store.State.Checkpoint with { Anchors = [] } };
    byte[] bytes = [1]; var t = new AnchoredStateTransition("owner", 0, 1, Fixture.Digest, CustodyJson.Hash(bytes), bytes);
    await Closed(() => f.Call(f.Request("admit-transition", transition: t))); Check(f.Store.State.Checkpoint.Anchors.Count == 0, "Self-enrolled missing owner.");
}
static async Task Keys() {
    using var f = new Fixture(); var wrapped = await f.Call(f.Wrap("one")); var registration = wrapped.WrappedRegistration!;
    Check(registration is not null && f.Native.Encrypts == 1, "Wrap failed.");
    var use = new CustodyBindingKeyUseV1("lease-u", "delivery-u", "recipient", "transport", registration!.OpaqueWrappedKeyReference);
    var request = f.Request("unwrap", registration, keyUse: use); var delivered = await f.Call(request);
    Check(delivered.KeyDelivery!.RootKey.AsSpan().SequenceEqual(Fixture.Root), "Original root changed.");
    CryptographicOperations.ZeroMemory(delivered.KeyDelivery.RootKey);
    var duplicate = await f.Call(f.Request("unwrap", registration, keyUse: use));
    Check(duplicate.KeyDelivery is null && duplicate.Delivery is not null && f.Native.Decrypts == 1, "Duplicate key release.");
    string id = "key-" + CustodyJson.Hash(System.Text.Encoding.UTF8.GetBytes(CustodyJson.DeliveryId(use)));
    var admissionObject = f.Store.State.Checkpoint.NativeResults[id + ":admission"];
    var signedAdmission = CustodyJson.Read<SignedStatement>(await f.Store.ReadImmutableAsync(admissionObject, default));
    var admission = CustodyJson.Read<KeyAdmission>(CustodyJson.Bytes(signedAdmission.Payload));
    var completion = new KeyCompletion(use, admissionObject.Sha256, admission.Holders.Select(h => new HolderCompletion(h.CopyId,
        h.ProcessIncarnation, h.TransportIncarnation, Fixture.Digest, Fixture.Digest, "VerifiedZeroed")).ToArray());
    var completeRequest = f.Request("complete-key-use", registration, keyUse: use);
    var completed = await f.Call(completeRequest, evidence: f.Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(completion)), completion));
    Check(completed.Delivery!.Phase == CustodyBindingIntentPhase.Closed, "Completion not retained.");
    Check(f.Store.Objects.Values.All(b => !System.Text.Encoding.UTF8.GetString(b).Contains(Convert.ToBase64String(Fixture.Root), StringComparison.Ordinal)), "Raw root persisted.");
}
static async Task LostWrap() {
    using var f = new Fixture(); f.Store.FailRegistrationCommit = true; var request = f.Wrap("lost");
    await Closed(() => f.Call(request)); Check(f.Native.Encrypts == 1 && f.Store.State.Checkpoint.Registrations.Count == 0, "Loss injection missed.");
    var intent = f.Store.State.Checkpoint.NativeIntents.Values.Single();
    await f.Call(request); Check(f.Native.Encrypts == 1, "Duplicate wrap resent.");
    var operation = new WorkerOperation("recover-wrap-result", request.Envelope.OperationId, intent.IntentId, intent.SourceGeneration, intent.DestinationGeneration, "", "executor-process-1");
    f.Qualify(); await f.Exchange.RecoverWrapAsync("tenant", operation, f.Sign("private-operation", CustodyJson.Hash(CustodyJson.Bytes(operation)), operation), default);
    var recovered = await f.Call(request); Check(recovered.WrappedRegistration?.OpaqueWrappedKeyReference == "lost" && f.Native.Encrypts == 1, "Original wrap recovery changed identity or resent.");
    var lookup = await f.Call(f.Lookup(recovered.WrappedRegistration!), qualified: false);
    Check(lookup.Lifecycle?.ReceiptId == recovered.WrappedRegistration!.WrapReceiptId && f.Native.Encrypts == 1, "Lost-result registration lookup did not recover exact receipt.");
}
static async Task RegistrationLookup() {
    using var f = new Fixture(); var wrap = f.Wrap("lookup"); var registration = (await f.Call(wrap)).WrappedRegistration!;
    var lookup = f.Lookup(registration); var before = f.Store.State.Head; int objects = f.Store.Objects.Count;
    var handler = new SdkBridge(f); using var client = new DaprClientBuilder().UseGrpcEndpoint("http://127.0.0.1:1").UseGrpcChannelOptions(new GrpcChannelOptions { HttpHandler = handler }).Build();
    var invoker = new CustodyBindingDaprInvoker(client, new Dictionary<string, string> { [CustodyBindingContract.LogicalRole] = "custody" }, new LookupAuthority(f, lookup));
    var wire = await invoker.InvokeAsync(lookup, TimeSpan.FromSeconds(5));
    Check(wire.State == CustodyBindingTransportState.ReceivedUnverified && wire.Response?.Lifecycle?.ReceiptId == registration.WrapReceiptId && handler.Calls == 1,
        "Real SDK/invoker did not accept the correlated wrapped lifecycle original.");
    Check(lookup.WrapInput is null && lookup.Envelope.RequestDigest != wrap.Envelope.RequestDigest, "Lookup accidentally depends on raw wrap input.");
    _ = await f.Call(lookup, qualified: false); // Deliberately discard the first lookup acknowledgement.
    var recovered = await f.Call(lookup, qualified: false);
    var retry = await f.Call(f.Lookup(registration), qualified: false);
    Check(recovered.Lifecycle == retry.Lifecycle && recovered.WrappedRegistration is null && recovered.KeyDelivery is null
        && recovered.Lifecycle!.Identity == registration.Identity && recovered.Lifecycle.OperationId == registration.OperationId
        && recovered.Lifecycle.RequestDigest == lookup.Envelope.RequestDigest && recovered.Lifecycle.Status == CustodyKeyLifecycleStatus.Wrapped
        && recovered.Lifecycle.ReceiptId == registration.WrapReceiptId, "Registration lookup changed the original receipt or response correlation.");
    foreach (var changed in new[] {
        registration with { WrapReceiptId = "other-receipt" }, registration with { OpaqueWrappedKeyReference = "other-reference" },
        registration with { OperationId = "other-operation" }, registration with { ExpectedRevision = registration.ExpectedRevision + 1 },
        registration with { KekVersion = "other-kek" }, registration with { Identity = registration.Identity with { ObjectId = "other-object" } },
        registration with { Identity = registration.Identity with { KeyAlias = "other-alias" } },
        registration with { Identity = registration.Identity with { KeyVersion = "other-version" } },
        registration with { Identity = registration.Identity with { TenantId = "other" } },
    }) await Closed(() => f.Call(f.Lookup(changed), qualified: false));
    Check(f.Store.State.Head == before && f.Store.Objects.Count == objects && f.Native.Encrypts == 1 && f.Native.Decrypts == 0 && f.Native.Schedules == 0,
        "Read-only lookup mutated authority, persisted a replacement or sent native work.");
}

static async Task Anchors() {
    using var f = new Fixture();
    AnchoredStateTransition? first = null;
    foreach (int revision in new[] { 0, 1, 2 }) {
        var anchor = f.Store.State.Checkpoint.Anchors["owner"]; byte[] bytes = [(byte)(revision + 1)];
        var transition = new AnchoredStateTransition("owner", revision, revision + 1, anchor.Digest, CustodyJson.Hash(bytes), bytes);
        first ??= transition;
        var admit = await f.Call(f.Request("admit-transition", transition: transition));
        if (revision > 0) {
            var before = f.Store.State.Head;
            if (!(await f.Call(f.Request("verify-transition", transition: first))).TransitionVerified!.Value
                || !(await f.Call(f.Request("record-transition", transition: first))).TransitionVerified!.Value
                || f.Store.State.Head != before) throw new Exception("Historical recorded transition disappeared or changed current owner during retry.");
        } Check(admit.TransitionProof?.State == "Admitted", "Admission advanced owner.");
        Check(f.Store.State.Checkpoint.Anchors["owner"].Revision == revision, "Admission mutated anchor.");
        var record = await f.Call(f.Request(revision == 1 ? "recover-transition" : "record-transition", transition: transition));
        Check(record.TransitionProof?.OwnerRevision == revision + 1, "Record/recovery did not advance exact owner.");
        Check((await f.Call(f.Request("verify-transition", transition: transition))).TransitionVerified == true, "Exact retained transition not verifiable.");
    }
    var staleBytes = new byte[] { 99 }; var stale = new AnchoredStateTransition("owner", 0, 1, Fixture.Digest, CustodyJson.Hash(staleBytes), staleBytes);
    await Closed(() => f.Call(f.Request("record-transition", transition: stale)));
    var read = await f.Call(f.Request("read-current")); Check(read.Current?.OwnerRevision == 3 && read.Current.ObservedRevision != 3, "Owner revision equated with custody head.");
}
static async Task Holds() {
    using var f = new Fixture(); var registration = (await f.Call(f.Wrap("held"))).WrappedRegistration!;
    var free = (await f.Call(f.Wrap("free", "a-free-object"))).WrappedRegistration!;
    var pin = f.Pin(registration, false); await f.Call(pin);
    var before = f.Store.State.Head;
    await Closed(() => f.Call(f.Manifest([free, registration])));
    Check(f.Store.State.Checkpoint.Cycle is null && f.Store.State.Head == before && f.Native.Schedules == 0,
        "Pinned second target allowed a partial manifest effect.");
    await f.Call(f.Pin(registration, true)); await f.Call(f.Manifest([free, registration]));
    await Closed(() => f.Call(f.Pin(registration, false))); Check(f.Store.State.Checkpoint.Pins.Count == 0, "Late hold pinned dispatched target.");
    var frozen = f.Wrap("after-freeze"); await Closed(() => f.Call(frozen)); Check(f.Native.Encrypts == 2, "Frozen allocation sent native key.");
}
static async Task Restore() {
    using var f = new Fixture(); var old = f.Store.State; await f.Call(f.Wrap("one"));
    await Closed(() => f.Store.CommitAsync(old, SemanticProvider.Clone(old), default));
    await Closed(() => f.Call(f.Wrap("replacement"))); Check(f.Native.Encrypts == 1, "Restored app reallocated interaction.");
    var read = f.Request("read-current"); var a = await f.Call(read); var b = await f.Call(read);
    Check(f.Store.Reads >= 4 && a.Current!.ObservedRevision == b.Current!.ObservedRevision, "Read current skipped native current read.");
}
static Task Frames() {
    var request = new InvokeRequest { Operation = "read-current", Data = ByteString.CopyFromUtf8("{}") };
    Check(BindingService.FrameAllowed(request), "Small frame rejected.");
    byte[] unknown; using (var stream = new MemoryStream()) { using (var coded = new CodedOutputStream(stream, true)) {
        request.WriteTo(coded); coded.WriteTag(99, WireFormat.WireType.LengthDelimited); coded.WriteBytes(ByteString.CopyFrom(new byte[CustodyBindingContract.CandidateRequestMessageBytes])); }
        unknown = stream.ToArray(); }
    Check(!BindingService.FrameAllowed(InvokeRequest.Parser.ParseFrom(unknown)), "Unknown protobuf bytes bypassed complete frame bound.");
    request.Metadata.Add("too-large", new string('x', 65536)); Check(!BindingService.FrameAllowed(request), "Metadata cap bypassed."); return Task.CompletedTask;
}
static async Task Lifecycle() {
    using var f = new Fixture(); var targetRequest = f.Wrap("target"); var target = (await f.Call(targetRequest)).WrappedRegistration!;
    var secondRequest = f.Wrap("target-2", "second-object"); var second = (await f.Call(secondRequest)).WrappedRegistration!;
    var survivorRequest = f.Wrap("survivor", "survivor-object"); var survivor = (await f.Call(survivorRequest)).WrappedRegistration!;
    await f.CloseKey(target, targetRequest.WrapInput!.KeyUse); await f.CloseKey(survivor, survivorRequest.WrapInput!.KeyUse);
    await f.CloseKey(second, secondRequest.WrapInput!.KeyUse);
    var manifest = f.Manifest([target, second]); await f.Call(manifest);
    var op = new WorkerOperation("select-successor", manifest.Envelope.OperationId, "select", "e1", "e2", "", "executor-process-1"); f.Qualify();
    await f.Worker.SelectSuccessorAsync("tenant", op, f.Sign("private-operation", CustodyJson.Hash(CustodyJson.Bytes(op)), op), default);
    var cycle = f.Store.State.Checkpoint.Cycle!;
    ClosureInventory Inventory(bool final = false) {
        var copies = f.Store.State.Checkpoint.Registrations.Values.Select(r => new CopyEntry("object-" + r.Registration.OpaqueWrappedKeyReference,
            CopyClass.ObjectVersion, "owner", "archive", "producer", null, r.Epoch, r.Registration.OpaqueWrappedKeyReference,
            f.Store.State.Checkpoint.Originals[r.Registration.OperationId].Response!.Sha256, Fixture.Digest,
            final && r.Epoch == "e1" ? ClosureDisposition.CryptographicallyErased : ClosureDisposition.GatedCiphertext)).Concat(new[] { "e1", "e2" }.Select(epoch =>
            new CopyEntry("epoch-" + epoch, CopyClass.KmsGeneration, "owner", "kms", "producer", null, epoch, epoch, Fixture.Digest,
                Fixture.Digest, final && epoch == "e1" ? ClosureDisposition.PermanentlyDeleted : ClosureDisposition.GatedCiphertext))).Concat(
            f.Store.State.Checkpoint.NativeResults.Where(pair => pair.Key.EndsWith(":admission", StringComparison.Ordinal)).SelectMany(pair => {
                var signed = CustodyJson.Read<SignedStatement>(f.Store.Objects[pair.Value.Key]); var admission = CustodyJson.Read<KeyAdmission>(CustodyJson.Bytes(signed.Payload));
                return admission.Holders.Select(h => new CopyEntry(h.CopyId, h.Role switch { "caller" => CopyClass.HostMemory, "sdk" => CopyClass.SdkBuffer,
                    "sidecar" => CopyClass.SidecarBuffer, "uds" => CopyClass.UdsBuffer, "grpc" => CopyClass.GrpcBuffer, "tls" => CopyClass.TlsBuffer, _ => CopyClass.ComponentBuffer },
                    h.OwnerId, "memory", h.ProcessIncarnation, null, "e1", admission.KeyUse.OriginalRootReference, h.AdmissionOriginalDigest, Fixture.Digest, ClosureDisposition.VerifiedZeroed));
            })).ToArray();
        return new(cycle.Manifest.Manifest!.Original.Capability.ManifestDigest, "e1", "e2", copies,
            copies.Select(c => c.ProducerIncarnation).Concat(f.Store.State.Checkpoint.NativeIntents.Values.Select(i => i.ProducerIncarnation)).Append("producer").Distinct().Select(p => new SealedQueue("q-" + p, p, 0, 0, Fixture.Digest, Fixture.Digest)).ToArray(), Enum.GetValues<CopyClass>().Select(c => new ClassCoverage(c, Fixture.Digest,
                copies.Where(copy => copy.Class == c).Select(copy => copy.CopyId).ToArray())).ToArray(), copies.Select(c => c.ProducerIncarnation).Concat(f.Store.State.Checkpoint.NativeIntents.Values.Select(i => i.ProducerIncarnation)).Append("producer").Distinct().ToArray(), f.Store.State.Checkpoint.NativeIntents.Keys.ToArray());
    }
    PreparationEvidence Preparation() { var inventory = Inventory(); var survivors = f.Store.State.Checkpoint.Registrations.Values.Where(r => r.Registration.Identity.ObjectId == "survivor-object").ToArray();
        return new(new(inventory.ManifestDigest, "e1", "e2", CustodyJson.Hash(CustodyJson.Bytes(inventory)), CustodyJson.Hash(CustodyJson.Bytes(survivors)),
            Fixture.Digest, Fixture.Digest, [], [survivor.OpaqueWrappedKeyReference], [target.OpaqueWrappedKeyReference, second.OpaqueWrappedKeyReference], Fixture.Digest, Fixture.Digest), inventory); }
    var prep = Preparation(); f.Qualify(); await Closed(() => f.Worker.ArmAsync("tenant", f.Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(prep)), prep), default));
    var copy = new WorkerOperation("copy-survivor", manifest.Envelope.OperationId, "copy", "e1", "e2", survivor.OpaqueWrappedKeyReference, "executor-process-1"); f.Qualify();
    var missingProducer = copy with { ProducerIncarnation = "" };
    await Closed(() => f.Worker.CopySurvivorAsync("tenant", missingProducer, f.Sign("private-operation", CustodyJson.Hash(CustodyJson.Bytes(missingProducer)), missingProducer), default));
    await f.Worker.CopySurvivorAsync("tenant", copy, f.Sign("private-operation", CustodyJson.Hash(CustodyJson.Bytes(copy)), copy), default);
    Check(f.Store.State.Checkpoint.NativeIntents["copy"].ProducerIncarnation == "executor-process-1", "Signing identity substituted for the executor process.");
    prep = Preparation(); f.Qualify(); await Closed(() => f.Worker.ArmAsync("tenant", f.Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(prep)), prep), default));
    await f.CloseNative("copy");
    var empty = Inventory() with { Copies = [], Coverage = Enum.GetValues<CopyClass>().Select(c => new ClassCoverage(c, Fixture.Digest, [])).ToArray() };
    Check(!ClosureRules.Complete(empty, f.Store.State.Checkpoint, false), "Empty inventory contradicted known material.");
    var omittedProducer = Inventory() with { ProducerIncarnations = Inventory().ProducerIncarnations.Where(p => p != "executor-process-1").ToArray(),
        Queues = Inventory().Queues.Where(q => q.ProducerIncarnation != "executor-process-1").ToArray() };
    Check(!ClosureRules.Complete(omittedProducer, f.Store.State.Checkpoint, false), "Native executor process was omitted from closure.");
    prep = Preparation(); f.Qualify();
    await f.Worker.ArmAsync("tenant", f.Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(prep)), prep), default);
    Check(f.Store.State.Checkpoint.CurrentEpoch == "e2" && !f.Store.State.Checkpoint.Frozen, "B3 did not adopt successor atomically.");
    var schedule = new WorkerOperation("schedule-cut", manifest.Envelope.OperationId, "cut", "e1", "e2", "", "executor-process-1"); f.Qualify();
    f.Native.FailScheduleAck = true;
    await Closed(() => f.Worker.ScheduleCutAsync("tenant", schedule, f.Sign("private-operation", CustodyJson.Hash(CustodyJson.Bytes(schedule)), schedule), default));
    f.Qualify(); await f.Worker.ScheduleCutAsync("tenant", schedule, f.Sign("private-operation", CustodyJson.Hash(CustodyJson.Bytes(schedule)), schedule), default);
    Check(f.Native.Schedules == 1, "Destructive duplicate resent native cut.");
    var recoveredSchedule = new RecoveredNativeResult("cut", CustodyJson.Hash(CustodyJson.Bytes(schedule)), "schedule-cut",
        CustodyJson.Bytes(new ScheduleResult("cut", "ScheduleObservedNotErased")), Fixture.Digest, schedule);
    f.Qualify(); await f.Worker.RecoverNativeResultAsync("tenant", f.Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(recoveredSchedule)), recoveredSchedule), default);
    Check(f.Native.Schedules == 1, "Native recovery resent destructive effect.");
    FinalClaim FinalFacts(string originalSha) { var inventory = Inventory(final: true); var c = f.Store.State.Checkpoint.Cycle!;
        return new(inventory.ManifestDigest, "e1", "e2", c.PreparationDigest!, ["e1"], CustodyJson.Hash(CustodyJson.Bytes(inventory)), Fixture.Digest,
            Fixture.Digest, "restore", Fixture.Digest, originalSha); }
    var originalPreparation = new OriginalPreparationEvidence(FinalFacts(new string('0', 64)), Inventory(final: true)); f.Qualify();
    await Closed(() => f.Worker.PrepareOriginalAsync("tenant", f.Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(originalPreparation)), originalPreparation), default));
    await f.CloseNative("cut"); originalPreparation = new(FinalFacts(new string('0', 64)), Inventory(final: true)); f.Qualify();
    var preparationStatement = f.Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(originalPreparation)), originalPreparation);
    f.Store.FailIntentResultAck = true;
    await Closed(() => f.Worker.PrepareOriginalAsync("tenant", preparationStatement, default));
    var evidenceDirectory = Directory.CreateTempSubdirectory("hexalith-semantic-original-").FullName;
    var output = Path.Combine(evidenceDirectory, "prepared-original.json");
    await WorkerEntry.ExecuteAsync(f.Worker, new("prepare-original", "tenant", null, preparationStatement, null, output), default);
    byte[] bytes = await File.ReadAllBytesAsync(output);
    File.WriteAllText(Path.Combine(evidenceDirectory, "component-public.pem"), f.Profile.ComponentPublicKeyPem);
    File.WriteAllBytes(Path.Combine(evidenceDirectory, "preparation-statement.json"), CustodyJson.Bytes(preparationStatement));
    Console.WriteLine("retainedSourceOriginal=" + output);
    Check((await f.Worker.PrepareOriginalAsync("tenant", preparationStatement, default)).AsSpan().SequenceEqual(bytes), "Lost preparation acknowledgement replaced original signature.");
    var publication = new PublicationEvidence(FinalFacts(CustodyJson.Hash(bytes)), Inventory(final: true)); f.Qualify();
    await f.Worker.PublishAsync("tenant", f.Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(publication)), publication), bytes, default);
    var header = (await f.Call(manifest)).OriginalHeader!; Check(header.TargetCount == 2 && header.OriginalSha256 == CustodyJson.Hash(bytes), "Incomplete/changed terminal original.");
    Check(CustodyJson.Read<CustodyBindingOriginalV1>(bytes).Result.TargetReceipts.Select(r => r.Target)
        .SequenceEqual(manifest.Manifest!.Original.Targets), "Multi-target terminal receipt vector changed order or coverage.");
    var page = f.Request("read-original-page", page: new(header, 0, bytes.Length)); var actual = await f.Call(page);
    Check(actual.Page!.Data.AsSpan().SequenceEqual(bytes), "Original bytes/signature changed.");
    var changed = header with { OriginalSha256 = Fixture.Digest }; await Closed(() => f.Call(f.Request("read-original-page", page: new(changed, 0, bytes.Length))));
    var survivorPlain = await f.Native.ReadOuterKeyAsync(f.Store.State.Checkpoint.Registrations[survivor.OpaqueWrappedKeyReference], default);
    CryptographicOperations.ZeroMemory(survivorPlain); Check(f.Native.Encrypts == 4, "Wrong survivor-copy effect count.");
}

static async Task OwnerAdapter() {
    var realGenesis = new Hexalith.EventStore.Server.Security.DeletionConsumptionLedger("tenant", 0, 0, [], [], []);
    Check(JsonSerializer.SerializeToUtf8Bytes(realGenesis).AsSpan().SequenceEqual(CustodyProtectionOwnerIdentity.GenesisBytes("tenant")), "EventStore genesis byte projection drift.");
    Check(CustodyProtectionOwnerIdentity.Scope("tenant") == "tenant:protection:deletion-consumption-v23|deletion-consumption-v23", "EventStore owner scope drift.");
    string scope = CustodyProtectionOwnerIdentity.Scope("tenant"); string genesis = CustodyProtectionOwnerIdentity.GenesisDigest("tenant");
    using var f = new Fixture(genesis, scope); var handler = new SdkBridge(f);
    using var client = new DaprClientBuilder().UseGrpcEndpoint("http://127.0.0.1:1").UseGrpcChannelOptions(new GrpcChannelOptions { HttpHandler = handler }).Build();
    var owner = new CustodyOwnerAuthority(client, "custody", "tenant", scope, CustodyJson.Bytes(f.Profile.Enrollment) is var e ? System.Text.Encoding.UTF8.GetString(e) : "",
        f.Profile.ComponentPublicKeyPem, new OwnerCalls(f));
    Check(await owner.ValidateStateAsync(0, genesis), "Signed installed genesis refused.");
    var target = new byte[] { 2 }; var transition = new AnchoredStateTransition(scope, 0, 1, genesis, CustodyJson.Hash(target), target);
    Check(await owner.AdmitTransitionAsync(transition), "Public adapter admission failed.");
    Check(await owner.RecordTransitionAsync(transition), "Public adapter record failed.");
    Check(await owner.VerifyTransitionAsync(transition), "Public adapter verify failed.");
    Check(!await owner.ValidateStateAsync(0, genesis), "Restored owner genesis promoted after advance.");
    Check(await owner.ValidateStateAsync(1, transition.TargetDigest), "Independent current owner invalid.");
    Check(await owner.AuthorizeOperationAsync("original-operation", "reserve"), "Exact private operation permission unavailable.");
    handler.CorruptSignature = true; Check(!await owner.ValidateStateAsync(1, transition.TargetDigest), "Unverified current owner accepted.");
    Check(handler.Calls == 8, "SDK repeated or skipped calls.");
}

static async Task SuccessorRetirement() {
    using var f = new Fixture(thirdEpoch: true); var targetRequest = f.Wrap("target"); var target = (await f.Call(targetRequest)).WrappedRegistration!;
    var survivorRequest = f.Wrap("survivor", "survivor-object"); var survivor = (await f.Call(survivorRequest)).WrappedRegistration!;
    await f.CloseKey(target, targetRequest.WrapInput!.KeyUse); await f.CloseKey(survivor, survivorRequest.WrapInput!.KeyUse);
    var manifest = f.Manifest([target]); await f.Call(manifest);
    var select = new WorkerOperation("select-successor", manifest.Envelope.OperationId, "select", "e1", "e2", "", "executor-process-1"); f.Qualify();
    await f.Worker.SelectSuccessorAsync("tenant", select, f.Sign("private-operation", CustodyJson.Hash(CustodyJson.Bytes(select)), select), default);
    var copy = new WorkerOperation("copy-survivor", manifest.Envelope.OperationId, "copy", "e1", "e2", survivor.OpaqueWrappedKeyReference, "executor-process-1"); f.Qualify();
    await f.Worker.CopySurvivorAsync("tenant", copy, f.Sign("private-operation", CustodyJson.Hash(CustodyJson.Bytes(copy)), copy), default);
    var retire = new WorkerOperation("retire-successor", manifest.Envelope.OperationId, "retire", "e2", "retired", "", "executor-process-1"); f.Qualify(); f.Native.FailScheduleAck = true;
    await Closed(() => f.Worker.RetireAlternateAsync("tenant", retire, f.Sign("private-operation", CustodyJson.Hash(CustodyJson.Bytes(retire)), retire), default));
    Check(f.Store.State.Checkpoint.Cycle!.Phase == CyclePhase.RetiringSuccessor && f.Store.State.Checkpoint.Frozen, "Uncertain successor was not durably fenced.");
    f.Qualify(); await f.Worker.RetireAlternateAsync("tenant", retire, f.Sign("private-operation", CustodyJson.Hash(CustodyJson.Bytes(retire)), retire), default);
    Check(f.Native.Schedules == 1, "Uncertain retirement resent native schedule.");
    var replacement = select with { DestinationEpoch = "e3", IntentId = "select-replacement" }; f.Qualify();
    await Closed(() => f.Worker.SelectSuccessorAsync("tenant", replacement, f.Sign("private-operation", CustodyJson.Hash(CustodyJson.Bytes(replacement)), replacement), default));
    var invalid = new AbandonedSuccessor("e2", ["copy", "retire"], "not-final-evidence", Fixture.Digest, Fixture.Digest); f.Qualify();
    await Closed(() => f.Worker.AbandonSuccessorAsync("tenant", f.Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(invalid)), invalid), default));
    var final = invalid with { FinalDeletionEvidenceDigest = Fixture.Digest }; f.Qualify();
    await f.Worker.AbandonSuccessorAsync("tenant", f.Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(final)), final), default);
    Check(f.Store.State.Checkpoint.Registrations[survivor.OpaqueWrappedKeyReference].Epoch == "e1" && f.Store.State.Checkpoint.Cycle!.TaintedEpochs.SequenceEqual(["e2"]), "Original survivor source or permanent taint lost.");
    f.Qualify(); await f.Worker.SelectSuccessorAsync("tenant", replacement, f.Sign("private-operation", CustodyJson.Hash(CustodyJson.Bytes(replacement)), replacement), default);
    Check(f.Store.State.Checkpoint.Cycle!.SuccessorEpoch == "e3" && f.Native.Schedules == 1 && f.Profile.Epochs.Count(e => e.TenantId == "tenant") == 3, "Replacement exceeded admitted epochs or replayed retirement.");
}

sealed class Fixture : IDisposable {
    internal static readonly string Digest = new('A', 64);
    internal static readonly byte[] Root = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    readonly Dictionary<string, ECDsa> signers = new(); readonly ECDsa response = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    internal ComponentProfile Profile; internal MemoryStore Store; internal FakeNative Native; internal StatementVerifier Verifier;
    internal KeyExchange Exchange; internal SemanticProvider Provider; internal RecoverableLifecycle Worker;
    internal string OwnerScope; int sequence; readonly string qualification = Path.GetTempFileName();
    internal Fixture(string? ownerDigest = null, string ownerScope = "owner", bool thirdEpoch = false) {
        OwnerScope = ownerScope;
        foreach (string purpose in new[] { "private-operation", "control", "independent-observer" }) signers.Add(purpose, ECDsa.Create(ECCurve.NamedCurves.nistP256));
        var enrollment = new AuthorityEnrollmentV1("authority", "incarnation", "store", "profile", "trust", "anchors", Digest);
        Profile = new(enrollment, "eu-west-1", "arn:aws:dynamodb:eu-west-1:000000000000:table/fixture", "fixture-table-id",
            new("checkpoints", "000000000000", "enrollment", "v1", Digest), new("originals", "000000000000", "enrollment", "v1", Digest),
            DateTimeOffset.UtcNow.AddYears(10), 1024 * 1024, "/tmp/custody-fixture/socket", "/tmp/uninstalled", response.ExportSubjectPublicKeyInfoPem(),
            signers.Select(s => new TrustedSigner(s.Key, s.Key, s.Value.ExportSubjectPublicKeyInfoPem())).ToArray(),
            new[] { new NativeEpoch("e1", "arn:aws:kms:eu-west-1:000000000000:key/e1", "e1", "tenant"), new NativeEpoch("e2", "arn:aws:kms:eu-west-1:000000000000:key/e2", "e2", "tenant"), new NativeEpoch("other-e1", "arn:aws:kms:eu-west-1:000000000000:key/other-e1", "other-e1", "other"), new NativeEpoch("other-e2", "arn:aws:kms:eu-west-1:000000000000:key/other-e2", "other-e2", "other") }.Concat(thirdEpoch ? [new NativeEpoch("e3", "arn:aws:kms:eu-west-1:000000000000:key/e3", "e3", "tenant")] : []).ToArray(),
            ["tenant", "other"], qualification, [new("tenant", "kek", "arn:aws:kms:eu-west-1:000000000000:key/r1", "r1", [1]), new("other", "kek", "arn:aws:kms:eu-west-1:000000000000:key/r2", "r2", [2])]);
        Profile.Validate();
        var checkpoint = new Checkpoint("tenant", Profile.EnrollmentDigest, 7, Digest, "e1", [], [], [], [], [], new() { [OwnerScope] = new(0, ownerDigest ?? Digest, null, null) }, null, [], [], []);
        Store = new(Profile, checkpoint); Native = new(); Verifier = new(Profile, TimeProvider.System); Exchange = new(Profile, Store, Native, Native, Verifier);
        Provider = new(Profile, Store, Verifier, response, Exchange); Worker = new(Profile, Store, Native, Verifier, response);
    }
    internal void Qualify() { var q = new QualificationClaim(Profile.ProfileDigest, Profile.Enrollment.ArtifactProfileDigest, Digest, Digest, Digest, Profile.CompletionReserveBytes, Digest, Digest);
        File.WriteAllBytes(qualification, CustodyJson.Bytes(Sign("independent-observer", Profile.ProfileDigest, q))); }
    internal SignedStatement Sign<T>(string purpose, string subject, T payload) {
        var s = new SignedStatement(purpose, purpose, Profile.Enrollment.AuthorityId, Profile.Enrollment.AuthorityIncarnation, "tenant", Store.State.Head.Revision,
            Store.State.Head.Digest, subject, DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(20), JsonSerializer.SerializeToElement(payload, CustodyBindingContract.Json), []);
        return s with { Signature = signers[purpose].SignData(CustodyJson.Bytes(s), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence) };
    }
    internal IReadOnlyDictionary<string, string> OwnerMetadata(CustodyBindingRequestV1 request) {
        Qualify(); var e = request.Envelope; byte[] bytes = CustodyBindingContract.Encode(request);
        var permission = new OperationPermission(e.Operation, e.OperationId, e.RequestAttemptId, e.Challenge, e.OwnerPurposeScope, Profile.ProfileDigest);
        var control = new ControlClaim(CustodyJson.Hash(CustodyJson.Bytes(e.Proof)), Digest, Digest, Digest, 100, null, null);
        return new Dictionary<string, string> { ["custody-operation-proof"] = Convert.ToBase64String(CustodyJson.Bytes(Sign("private-operation", CustodyJson.Hash(bytes), permission))),
            ["custody-control-proof"] = Convert.ToBase64String(CustodyJson.Bytes(Sign("control", CustodyJson.Hash(CustodyJson.Bytes(e.Proof)), control))) };
    }
    internal async Task<CustodyBindingResponseV1> Call(CustodyBindingRequestV1 request, bool qualified = true, SignedStatement? evidence = null, Func<ControlClaim, ControlClaim>? changeControl = null) {
        if (qualified) Qualify();
        byte[] bytes = CustodyBindingContract.Encode(request); var e = request.Envelope;
        var permission = new OperationPermission(e.Operation, e.OperationId, e.RequestAttemptId, e.Challenge, e.OwnerPurposeScope, Profile.ProfileDigest);
        var control = new ControlClaim(CustodyJson.Hash(CustodyJson.Bytes(e.Proof)), Digest, Digest, Digest, 100,
            e.Operation == "pin" ? null : e.Proof.DispatchRevision, e.Operation == "pin" ? null : e.Proof.DispatchReceiptId,
            e.Operation == "pin" ? new("hold-reservation", e.Proof.HoldId!, e.Proof.TargetVectorDigest, e.Proof.GuardRevision!.Value, e.Proof.GuardReceiptId!, "RegisteredDispatchBlocked") : null);
        if (changeControl is not null) control = changeControl(control);
        var use = request.WrapInput?.KeyUse ?? request.KeyUse;
        if (evidence is null && use is not null) { var admission = new KeyAdmission(use, e.RequestDigest, Profile.ProfileDigest,
            new[] { "caller", "sdk", "sidecar", "uds", "grpc", "tls", "component", "native" }.Select(r => new KeyHolder("copy-" + use.KeyUseLeaseId + "-" + r, r, use.RecipientProcessIncarnation, use.TransportIncarnation, "owner", Digest)).ToArray());
            evidence = Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(admission)), admission); }
        return await Provider.InvokeAsync(bytes, Sign("private-operation", CustodyJson.Hash(bytes), permission), Sign("control", CustodyJson.Hash(CustodyJson.Bytes(e.Proof)), control), default, evidence);
    }
    internal CustodyBindingRequestV1 Request(string operation, CustodyKeyRegistration? registration = null, CustodyKeyLifecycleRequest? lifecycle = null,
        CustodyBindingManifestV1? manifest = null, AnchoredStateTransition? transition = null, CustodyBindingPageReadV1? page = null, CustodyBindingKeyUseV1? keyUse = null,
        CustodyBindingWrapInputV1? wrap = null) {
        string target = manifest?.Original.Capability.ManifestDigest ?? transition?.TargetDigest ?? page?.Header.ManifestDigest
            ?? (lifecycle is null ? Digest : DeletionBatchCapabilityIdentity.TargetManifestDigest([new("tenant", lifecycle.Identity.ObjectId, lifecycle.Identity.KeyAlias)]));
        var proof = new ControlAdmissionProofV1("control", "custody", "proof", "incarnation", "tenant", target, operation, lifecycle?.HoldId,
            manifest is null ? lifecycle?.FenceRevision : 2, manifest is null ? lifecycle?.FenceTokenId : "issued", lifecycle?.Action == CustodyKeyLifecycleAction.Unpin ? 3 : null,
            lifecycle?.Action == CustodyKeyLifecycleAction.Unpin ? "release" : null, manifest?.Original.DispatchGuardRevision ?? 0,
            manifest?.Original.DispatchReceiptId, manifest?.ProtectionReservationReceiptId ?? page?.Header.ReservationReceiptId, "decision", Store.State.Head.Revision, Store.State.Head.Digest, [1]);
        var envelope = new CustodyBindingEnvelopeV1(CustodyBindingContract.Protocol, operation, wrap?.OperationId ?? lifecycle?.OperationId ?? (manifest is not null ? "destroy" : "op-" + ++sequence), Digest,
            "tenant", OwnerScope, Profile.Enrollment, Store.State.Head.Revision, Store.State.Head.Digest, "attempt-" + ++sequence, "challenge-" + sequence, proof);
        var request = new CustodyBindingRequestV1(envelope, registration, lifecycle, manifest, transition, page, keyUse, wrap);
        return request with { Envelope = envelope with { RequestDigest = CustodyBindingContract.OriginalRequestDigest(request) } };
    }
    internal CustodyBindingRequestV1 Wrap(string opaque, string objectId = "object") { var identity = new CustodyKeyObjectIdentity("tenant", objectId, PlatformKeyPurpose.InteractionRootDek, "alias", "v1", "lifecycle", "store", "contract");
        return Request("wrap", wrap: new(identity, "wrap-" + opaque, Store.State.Head.Revision, "kek", opaque, Root.ToArray(), new("lease-" + opaque, "delivery-" + opaque, "recipient", "transport", opaque))); }
    internal CustodyBindingRequestV1 Lookup(CustodyKeyRegistration registration) {
        var request = Request("lookup-original", registration); var e = request.Envelope;
        return request with { Envelope = e with { TenantId = registration.Identity.TenantId, OperationId = registration.OperationId,
            ExpectedCustodyRevision = registration.ExpectedRevision,
            Proof = e.Proof with { TenantId = registration.Identity.TenantId, ExpectedCustodyRevision = registration.ExpectedRevision } } };
    }
    internal CustodyBindingRequestV1 Pin(CustodyKeyRegistration registration, bool release) { var l = new CustodyKeyLifecycleRequest(registration.Identity, "hold-" + ++sequence,
        release ? CustodyKeyLifecycleAction.Unpin : CustodyKeyLifecycleAction.Pin, Store.State.Head.Revision, "hold", "registered", 2, "decision");
        return Request(release ? "unpin" : "pin", registration, lifecycle: l); }
    internal CustodyBindingRequestV1 Manifest(CustodyKeyRegistration[] registrations) { var targets = registrations.Select(r => new ProtectionTarget("tenant", r.Identity.ObjectId, r.Identity.KeyAlias)).OrderBy(t => t.AgentInteractionId, StringComparer.Ordinal).ToArray();
        var capability = new DeletionBatchCapabilityV1("issuer", "audience", "tenant", "deletion", "seal", "kind", 0, "batch", DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key");
        return Request("destroy-manifest", manifest: new(new(capability, "original-jws", 2, "dispatch", 3, targets), DeletionBatchCapabilityIdentity.CanonicalPayload(capability), "reservation")); }

    internal async Task CloseKey(CustodyKeyRegistration registration, CustodyBindingKeyUseV1 use) {
        string id = "key-" + CustodyJson.Hash(System.Text.Encoding.UTF8.GetBytes(CustodyJson.DeliveryId(use)));
        var reference = Store.State.Checkpoint.NativeResults[id + ":admission"];
        var signed = CustodyJson.Read<SignedStatement>(await Store.ReadImmutableAsync(reference, default));
        var admitted = CustodyJson.Read<KeyAdmission>(CustodyJson.Bytes(signed.Payload));
        var completion = new KeyCompletion(use, reference.Sha256, admitted.Holders.Select(h => new HolderCompletion(h.CopyId,
            h.ProcessIncarnation, h.TransportIncarnation, Digest, Digest, "VerifiedZeroed")).ToArray());
        await Call(Request("complete-key-use", registration, keyUse: use), evidence: Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(completion)), completion));
    }
    internal async Task CloseNative(string id) {
        var intent = Store.State.Checkpoint.NativeIntents[id]; var result = Store.State.Checkpoint.NativeResults[id];
        var completion = new NativeCompletion(id, intent.ProducerIncarnation, result.Sha256, Digest, Digest, Digest); Qualify();
        await Worker.CloseNativeIntentAsync("tenant", Sign("independent-observer", CustodyJson.Hash(CustodyJson.Bytes(completion)), completion), default);
    }
    public void Dispose() { foreach (var s in signers.Values) s.Dispose(); response.Dispose(); File.Delete(qualification); Native.Dispose(); }
}
sealed class MemoryStore(ComponentProfile profile, Checkpoint initial) : IAuthorityStore {
    internal CurrentState State = Make(initial); private CurrentState OtherState = Make(initial with { TenantId = "other", CurrentEpoch = "other-e1" }); internal Dictionary<string, byte[]> Objects = new(); readonly Dictionary<string, ObjectVersion> results = new(); internal bool FailRegistrationCommit; internal bool FailIntentResultAck; internal int Reads;
    static CurrentState Make(Checkpoint checkpoint) { byte[] bytes = CustodyJson.Bytes(checkpoint); var version = new ObjectVersion("checkpoint/fixture", "v1", CustodyJson.Hash(bytes), bytes.Length, DateTimeOffset.MaxValue); return new(new(checkpoint.Revision, version.Sha256, version, checkpoint.EnrollmentDigest), checkpoint); }
    public Task<CurrentState> ReadAsync(string tenant, CancellationToken token) { Reads++; if (tenant != State.Checkpoint.TenantId && tenant != "other") throw new AuthorityUnavailableException("Tenant mismatch."); return Task.FromResult(CustodyJson.Read<CurrentState>(CustodyJson.Bytes(tenant == "other" ? OtherState : State))); }
    public Task<ObjectVersion> PutImmutableAsync(ReadOnlyMemory<byte> bytes, string category, CancellationToken token) { string key = category + "/" + Guid.NewGuid().ToString("N"); Objects.Add(key, bytes.ToArray()); return Task.FromResult(new ObjectVersion(key, "v1", CustodyJson.Hash(bytes.Span), bytes.Length, DateTimeOffset.MaxValue)); }
    public Task<byte[]> ReadImmutableAsync(ObjectVersion version, CancellationToken token) { byte[] bytes = Objects[version.Key]; if (CustodyJson.Hash(bytes) != version.Sha256) throw new AuthorityUnavailableException("Substituted immutable."); return Task.FromResult(bytes.ToArray()); }
    public async Task<ObjectVersion> PutIntentResultAsync(ReadOnlyMemory<byte> bytes, string locator, CancellationToken token) { if (results.ContainsKey(locator)) throw new AuthorityUnavailableException("Immutable native result exists."); var version = await PutImmutableAsync(bytes, "journal", token); results.Add(locator, version); if (FailIntentResultAck) { FailIntentResultAck = false; throw new AuthorityUnavailableException("Synthetic immutable result acknowledgement loss."); } return version; }
    public Task<ObjectVersion?> FindIntentResultAsync(string locator, CancellationToken token) => Task.FromResult(results.TryGetValue(locator, out var version) ? version : null);
    public Task<CurrentState> CommitAsync(CurrentState expected, Checkpoint next, CancellationToken token) {
        if (State.Head != expected.Head) throw new AuthorityConflictException();
        if (FailRegistrationCommit && next.Registrations.Count > State.Checkpoint.Registrations.Count) { FailRegistrationCommit = false; throw new AuthorityUnavailableException("Synthetic result/CAS loss."); }
        if (CustodyJson.Bytes(next).Length > 32 * 1024 * 1024 - profile.CompletionReserveBytes || next.Registrations.Count > 1000 || next.Originals.Count > 10000) throw new AuthorityUnavailableException("Capacity.");
        State = Make(CustodyJson.Read<Checkpoint>(CustodyJson.Bytes(next))); return Task.FromResult(State);
    }
}
sealed class FakeNative : IEpochOperations, IKeyAccess, IDisposable {
    readonly byte[] kek = RandomNumberGenerator.GetBytes(32); readonly Dictionary<string, byte[]> epochs = new() { ["e1"] = RandomNumberGenerator.GetBytes(32), ["e2"] = RandomNumberGenerator.GetBytes(32) }; internal int Encrypts, Decrypts, Schedules; internal bool FailScheduleAck;
    public Task<byte[]> EncryptOuterKeyAsync(string epoch, string tenant, byte[] key, CancellationToken token) { Encrypts++; var nonce = RandomNumberGenerator.GetBytes(12); var cipher = new byte[32]; var tag = new byte[16]; using var aes = new AesGcm(epochs[epoch], 16); aes.Encrypt(nonce, key, cipher, tag); return Task.FromResult(nonce.Concat(cipher).Concat(tag).ToArray()); }
    public async Task<byte[]> ReencryptOuterKeyAsync(string source, string destination, string tenant, byte[] encryptedKey, CancellationToken token) { var plain = Decrypt(source, encryptedKey); try { return await EncryptOuterKeyAsync(destination, tenant, plain, token); } finally { CryptographicOperations.ZeroMemory(plain); } }
    public Task ScheduleSourceCutAsync(string epoch, string tenant, CancellationToken token) { Schedules++; if (FailScheduleAck) { FailScheduleAck = false; throw new AuthorityUnavailableException("Synthetic native acknowledgement loss."); } return Task.CompletedTask; }
    public Task<byte[]> ReadTenantKekAsync(string tenant, string version, CancellationToken token) => Task.FromResult(kek.ToArray());
    public Task<byte[]> ReadOuterKeyAsync(StoredRegistration registration, CancellationToken token) { Decrypts++; return Task.FromResult(Decrypt(registration.Epoch, registration.EncryptedOuterKey)); }
    byte[] Decrypt(string epoch, byte[] ciphertext) { byte[] plain = new byte[32]; using var aes = new AesGcm(epochs[epoch], 16); aes.Decrypt(ciphertext.AsSpan(0, 12), ciphertext.AsSpan(12, 32), ciphertext.AsSpan(44, 16), plain); return plain; }
    public void Dispose() { CryptographicOperations.ZeroMemory(kek); foreach (var key in epochs.Values) CryptographicOperations.ZeroMemory(key); }
}

sealed class OwnerCalls(Fixture fixture) : ICustodyOwnerCallSource {
    public Task<CustodyOwnerAuthorizedCall?> AuthorizeAsync(string phase, string tenant, string ownerScope, string? operationIdentity, string? transitionJson, CancellationToken token) {
        var transition = transitionJson is null ? null : CustodyJson.Read<AnchoredStateTransition>(System.Text.Encoding.UTF8.GetBytes(transitionJson));
        var request = fixture.Request(phase, transition: transition);
        if (operationIdentity is not null) request = request with { Envelope = request.Envelope with { OperationId = operationIdentity } };
        return Task.FromResult<CustodyOwnerAuthorizedCall?>(new(System.Text.Encoding.UTF8.GetString(CustodyBindingContract.Encode(request)), fixture.OwnerMetadata(request),
            CustodyBindingContract.DefaultMessageBytes, Fixture.Digest));
    }
}
sealed class SdkBridge(Fixture fixture) : HttpMessageHandler {
    readonly BindingService service = new(fixture.Profile, fixture.Provider, fixture.Store);
    bool initialized;
    internal int Calls; internal bool CorruptSignature;
    internal bool LoseNextRecordAcknowledgement;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
        Calls++; byte[] frame = await request.Content!.ReadAsByteArrayAsync(token); byte[] data = []; var metadata = new Dictionary<string, string>();
        using (var input = new CodedInputStream(frame, 5, frame.Length - 5)) { uint tag; while ((tag = input.ReadTag()) != 0) {
            int field = WireFormat.GetTagFieldNumber(tag); if (field == 2) data = input.ReadBytes().ToByteArray();
            else if (field == 3) { using var map = new CodedInputStream(input.ReadBytes().ToByteArray()); string key = "", value = ""; uint mapTag;
                while ((mapTag = map.ReadTag()) != 0) { if (WireFormat.GetTagFieldNumber(mapTag) == 1) key = map.ReadString(); else if (WireFormat.GetTagFieldNumber(mapTag) == 2) value = map.ReadString(); else map.SkipLastField(); } metadata.Add(key, value); }
            else input.SkipLastField(); } }
        if (!initialized) { await service.Init(ServiceBoundaryTests.Init(fixture.Profile.ProfileDigest), new FixtureCallContext()); initialized = true; }
        var invocation = new InvokeRequest { Operation = CustodyBindingContract.Decode(data)!.Envelope.Operation, Data = ByteString.CopyFrom(data) };
        foreach (var pair in metadata) invocation.Metadata.Add(pair.Key, pair.Value);
        var componentResponse = await service.Invoke(invocation, new FixtureCallContext());
        var response = CustodyJson.Read<CustodyBindingResponseV1>(componentResponse.Data.Span);
        if (LoseNextRecordAcknowledgement && invocation.Operation == "record-transition") { LoseNextRecordAcknowledgement = false; throw new HttpRequestException("Controlled lost owner-record acknowledgement after service commit."); }
        if (CorruptSignature && response.Current is { } current) response = response with { Current = current with { Signature = [1, 2, 3] } };
        byte[] json = CustodyJson.Bytes(response); using var protobuf = new MemoryStream(); using (var coded = new CodedOutputStream(protobuf, true)) {
            coded.WriteTag(1, WireFormat.WireType.LengthDelimited); coded.WriteBytes(ByteString.CopyFrom(json)); }
        byte[] resultFrame = new byte[protobuf.Length + 5]; BinaryPrimitives.WriteInt32BigEndian(resultFrame.AsSpan(1, 4), (int)protobuf.Length); protobuf.ToArray().CopyTo(resultFrame, 5);
        var result = new HttpResponseMessage(HttpStatusCode.OK) { Version = new(2, 0), Content = new ByteArrayContent(resultFrame) };
        result.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc"); result.TrailingHeaders.Add("grpc-status", "0"); return result;
    }
}

sealed class LookupAuthority(Fixture fixture, CustodyBindingRequestV1 request) : ICustodyBindingTransportAuthority {
    public Task<bool> AuthorizeAsync(CustodyBindingRequestV1 exact, string method, CancellationToken token) => Task.FromResult(method == "custody-lifecycle/lookup-original"
        && CustodyBindingContract.Encode(exact).AsSpan().SequenceEqual(CustodyBindingContract.Encode(request)));
    public Task<CustodyBindingTransportAuthorization?> PrepareTransportAsync(CustodyBindingRequestV1 exact, CancellationToken token)
        => Task.FromResult<CustodyBindingTransportAuthorization?>(new(fixture.OwnerMetadata(exact), CustodyBindingContract.DefaultMessageBytes, Fixture.Digest, null));
}
