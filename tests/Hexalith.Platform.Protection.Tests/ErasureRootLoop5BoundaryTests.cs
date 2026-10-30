using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Loop-5 production startup, complete preflight and authoritative-index boundary regressions.</summary>
public sealed class ErasureRootLoop5BoundaryTests
{
    [Fact]
    public void RoleSignerMustActuallySignWithThePinnedPrivateP256Key()
    {
        using var privateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256); using var publicOnly = ECDsa.Create();
        publicOnly.ImportSubjectPublicKeyInfo(privateKey.ExportSubjectPublicKeyInfo(), out _);
        ProtectionQualificationServerExecutable.HasPrivateSigner(publicOnly, privateKey).ShouldBeFalse();
        ProtectionQualificationServerExecutable.HasPrivateSigner(privateKey, publicOnly).ShouldBeTrue();
        using var wrong = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ProtectionQualificationServerExecutable.HasPrivateSigner(wrong, publicOnly).ShouldBeFalse();
        using var curve = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        ProtectionQualificationServerExecutable.HasPrivateSigner(curve, curve).ShouldBeFalse();
    }
    [Fact]
    public async Task ActualPinnedServerStartupRefusesPublicOnlyBeforeGraphAndAcceptsUsablePrivateSignerBoundary()
    {
        string directory = Path.Combine(Path.GetTempPath(), "erc-loop5-startup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            string anchorPath = Path.Combine(directory, "public.pem"), signerPath = Path.Combine(directory, "private.pem");
            await File.WriteAllTextAsync(anchorPath, key.ExportSubjectPublicKeyInfoPem(), TestContext.Current.CancellationToken);
            var args = ProtectionQualificationSourceCatalog.RequiredArguments().ToDictionary(entry => entry.Key, entry => entry.Value.StartsWith("Guard:", StringComparison.Ordinal)
                ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(entry.Value[6..]) } } : new JsonObject());
            var recipes = ProtectionQualificationSourceCatalog.Compile(args);
            var scope = new ProtectionQualificationServiceScope("executor", "isolated-startup", "tenant-a", new string('a', 64), new("unreachable", "proof", new()), new(),
                recipes.Keys.ToDictionary(id => id, id => new ProtectionQualificationObservationRecipe("tenant-a", new(), new()))) { NativeArguments = args };
            (string Path, string Hash) Write(string name, object value)
            { string path = Path.Combine(directory, name); byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value); File.WriteAllBytes(path, bytes); return (path, Convert.ToHexString(SHA256.HashData(bytes))); }
            var scopeFile = Write("scope.json", scope);
            var installation = new ProtectionQualificationServerInstallation(Path.Combine(directory, "host-must-not-be-read.json"), new string('A', 64), scopeFile.Path, scopeFile.Hash,
                signerPath, anchorPath, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(anchorPath, TestContext.Current.CancellationToken))), "env:ABSENT_LOOP5_ROLE", ["source"], [], new("control", "cell", new()), "enrollment", [], new("issuer", "audience", "tenant-a"));
            var installationFile = Write("installation.json", installation);
            foreach (bool privateMaterial in new[] { false, true })
            {
                await File.WriteAllTextAsync(signerPath, privateMaterial ? key.ExportPkcs8PrivateKeyPem() : key.ExportSubjectPublicKeyInfoPem(), TestContext.Current.CancellationToken);
                int boundaryReached = 0;
                (await ProtectionQualificationServerExecutable.RunCoreAsync("executor", installationFile.Path, installationFile.Hash, () => boundaryReached++, TestContext.Current.CancellationToken)).ShouldBe(2);
                boundaryReached.ShouldBe(privateMaterial ? 1 : 0);
                File.Exists(installation.HostInstallationFile).ShouldBeFalse();
            }
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task NativeRotationWithGenuinePendingNullSurvivorsCannotCertifyNoEffect()
    {
        using var fixture = new ErasureRootEngineFixture(); var token = TestContext.Current.CancellationToken; var enrollment = fixture.Enroll();
        (await fixture.Custody.WrapAsync(enrollment.RootIdentity, enrollment.RootOperationId, 0, token)).ShouldNotBeNull();
        var target = enrollment.Binding.Identity.Target;
        var request = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "request", "seal", "AcceptedSet", 0, "batch", DeletionBatchCapabilityIdentity.TargetManifestDigest([target]), "guard", 1, 1, 1, "key"), "component", 1, "dispatch", 2, [target]);
        string call = ErasureRootCustody.Hash("pending-native-boundary"); var before = (await fixture.Custody.CaptureEffectBoundaryAsync("tenant-a", call, "before", false, token))!;
        fixture.Store.Failure = "rotate-after";
        (await fixture.Custody.ConsumeAsync(request, "reservation", token)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        var pending = await fixture.Store.HeadAsync("tenant-a"); pending.Pending.ShouldNotBeNull(); pending.Pending.Survivors.ShouldBeNull();
        var after = (await fixture.Custody.CaptureEffectBoundaryAsync("tenant-a", call, "after", false, token))!;
        after.NativePolicy.LatestVersion.ShouldBeGreaterThan(pending.Epoch); after.Fingerprint.ShouldNotBe(before.Fingerprint); fixture.Store.TrimCount.ShouldBe(0);
    }
    [Theory]
    [InlineData("EnrollOccurrence")]
    [InlineData("Reserve")]
    [InlineData("RetainSealed")]
    [InlineData("CompleteWriter")]
    [InlineData("AcquireEncryptionLease")]
    [InlineData("AcquireCompletionLease")]
    [InlineData("CompleteRootHolder")]
    [InlineData("RetainOccurrenceIndex")]
    [InlineData("MigrateOccurrenceIndex")]
    public async Task MissingRequiredGrantRefusesActualEngineBeforeAnyRootOrRegistryEffect(string operation)
    {
        using var fixture = new ErasureRootEngineFixture(); var original = fixture.Enroll(); var token = TestContext.Current.CancellationToken;
        fixture.Permissions.DeniedOperations.Add(operation);
        string before = ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a")); int writes = fixture.Store.ArchiveWrites;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Engine.ProtectEventPayloadAsync(original.Binding.Identity.Owner,
            new ProtectionQualificationEventPayload(), original.Binding.Identity.PayloadTypeId,
            JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = original.Binding.Identity.Target, Value = "canary" } }),
            "json", ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), token));
        ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a")).ShouldBe(before); fixture.Store.CasCount.ShouldBe(0); fixture.Store.ArchiveWrites.ShouldBe(writes);
        fixture.RegistryState.CommittedState.ShouldBeEmpty();
    }
    [Fact]
    public async Task LegacyMigrationRequiresExplicitPermissionBeforeAnyArchivePrewriteAndFreshReverseLookupIsIndependentOfQualification()
    {
        using var fixture = new ErasureRootEngineFixture(); var token = TestContext.Current.CancellationToken; var original = fixture.Enroll() with { Writer = new("source", "writer", new()) };
        string id = ErasureRootOccurrenceBindings.Id(original.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity));
        var read = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value; read.Head.SourceOriginals.Add(id, original);
        fixture.Store.ReplaceHead("tenant-a", read.Head); fixture.Permissions.DeniedOperations.Add("MigrateOccurrenceIndex");
        int writes = fixture.Store.ArchiveWrites; int cas = fixture.Store.CasCount;
        var fresh = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Custody.SaveAsync("tenant-a", fresh.Version, fresh.Head, token));
        fixture.Store.ArchiveWrites.ShouldBe(writes); fixture.Store.CasCount.ShouldBe(cas);
        fixture.Permissions.DeniedOperations.Clear(); (await fixture.Custody.SaveAsync("tenant-a", fresh.Version, fresh.Head, token)).ShouldBeTrue();
        var indexed = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        indexed.Head.SourceOriginals.ShouldBeEmpty(); indexed.Head.QualificationProducerOriginals.ShouldBeEmpty();
        ErasureRootCustody.Hash(await fixture.Custody.ReadSourceReferenceAsync(indexed.Head, original.Binding.KeyReference, token)).ShouldBe(ErasureRootCustody.Hash(original));
        var orphan = original with { RootOperationId = "uncommitted" };
        _ = await new ErasureRootOccurrenceIndex(fixture.Store, fixture.Custody.Installation, "tenant-a").WriteAsync(indexed.Head.OccurrenceIndexRoot, "source:orphan", orphan, token);
        (await fixture.Custody.ReadSourceAsync(indexed.Head, "orphan", token)).ShouldBeNull();
    }
    [Fact]
    public void PhysicalCopyAliasesAndBackingAliasesCannotCountTwice()
    {
        var source = new ProtectionQualificationNativeCell("source", "snapshot", new() { ["partitionKey"] = "tenant-a" }) { CopyKind = "snapshot" };
        var sources = new Dictionary<string, ProtectionQualificationNativeCell> { ["snapshot"] = source, ["cache"] = source with { CopyKind = "cache" } };
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationPersistedObserver.ValidateCopyLocations(sources));
        sources["cache"] = source with { Component = "alias", CopyKind = "cache" };
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationPersistedObserver.ValidateCopyLocations(sources, new Dictionary<string, string> { ["source"] = "physical", ["alias"] = "physical" }));
        sources["cache"] = source with { Key = "snapshot:cache", CopyKind = "cache" };
        ProtectionQualificationPersistedObserver.ValidateCopyLocations(sources);
    }
    [Fact]
    public void MissingLaterCompromiseEvidenceAndMalformedFutureReferencesRefuseWholeRecipe()
    {
        var request = new JsonObject { ["targets"] = JsonSerializer.SerializeToNode(new[] { new ProtectionTarget("tenant-a", "interaction", "alias") }) };
        var first = new ProtectionQualificationNativeCall("issue", ProtectionQualificationNativeCallKind.Lookup, new() { ["tenantId"] = "tenant-a", ["batchId"] = "batch" });
        var last = new ProtectionQualificationNativeCall("compromise", ProtectionQualificationNativeCallKind.Compromise, new() { ["envelope"] = new JsonObject() });
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationInputSchema.Validate(new([first], last, null, []), request, new HashSet<string>()));
        var capability = new DeletionBatchCapabilityV1("issuer", "audience", "tenant-a", "request", "seal", "AcceptedSet", 0, "batch", new string('A', 64), "guard", 1, 1, 1, "key");
        var sign = new ProtectionQualificationNativeCall("sign", ProtectionQualificationNativeCallKind.Sign, (JsonObject)JsonSerializer.SerializeToNode(capability)!);
        var substitution = last with { Payload = new() { ["envelope"] = JsonSerializer.SerializeToNode(new DeletionCapabilityRevocationEnvelope("issuer", "audience", "tenant-a", "DeletionBatchCapabilitySigningKey", "key", 1, 1, "event", new string('A', 64))),
            ["signedEvidence"] = new JsonObject { ["$original"] = new JsonObject { ["id"] = "sign", ["pointer"] = "" } } } };
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationInputSchema.Validate(new([sign], substitution, null, []), request, new HashSet<string>()));
        var missing = first with { Payload = new() { ["tenantId"] = new JsonObject { ["$original"] = new JsonObject { ["id"] = "later", ["pointer"] = "/TenantId" } }, ["batchId"] = "batch" } };
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationInputSchema.Validate(new([], missing, null, []), request, new HashSet<string>()));
    }
    [Fact]
    public async Task IndependentEffectOriginalsDetectRotationPendingHoldersAndWrappedCiphertext()
    {
        using var fixture = new ErasureRootEngineFixture(); var token = TestContext.Current.CancellationToken;
        var root = (await fixture.Custody.WrapAsync(fixture.Enroll().RootIdentity, "root", 0, token))!;
        async Task<ErasureRootEffectBoundaryEvidence> Capture(string id) => (await fixture.Custody.CaptureEffectBoundaryAsync("tenant-a", ErasureRootCustody.Hash(id), "before", false, token))!;
        var baseline = await Capture("baseline");
        await fixture.Store.RotateAsync("tenant-a", token); var rotated = await Capture("rotated"); rotated.Fingerprint.ShouldNotBe(baseline.Fingerprint);
        using var holder = (await fixture.Custody.UnwrapAsync(root, "live", token))!; var held = await Capture("held"); held.Fingerprint.ShouldNotBe(rotated.Fingerprint);
        var head = await fixture.Store.HeadAsync("tenant-a"); string id = ErasureRootCustody.TargetId(root.Identity); var target = head.Targets[id];
        head.Targets[id] = target with { Wrapped = target.Wrapped with { Ciphertext = target.Wrapped.Ciphertext + "changed" } }; fixture.Store.ReplaceHead("tenant-a", head);
        (await Capture("wrapped")).Fingerprint.ShouldNotBe(held.Fingerprint);
        var read = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        (await fixture.Custody.ReadEffectBoundaryAsync("tenant-a", read.Head, ErasureRootCustody.Hash("baseline"), "before", token)).ShouldBe(baseline);
        (await fixture.Custody.CaptureEffectBoundaryAsync("tenant-a", ErasureRootCustody.Hash("undispatched"), "before", true, token)).ShouldBeNull();
    }
    [Fact]
    public async Task GlobalReverseAmbiguityCannotBeNarrowedByAHydratedCache()
    {
        using var fixture = new ErasureRootEngineFixture(); var token = TestContext.Current.CancellationToken; var first = fixture.Enroll("owner-first") with { Writer = new("source", "writer-first", new()) };
        var second = first with { Binding = first.Binding with { Identity = first.Binding.Identity with { Owner = new("tenant-a", first.Binding.Identity.Owner.Domain, "owner-second") } }, Writer = new("source", "writer-second", new()) };
        var read = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        foreach (var original in new[] { first, second })
        { read.Head.SourceOriginals.Add(ErasureRootOccurrenceBindings.Id(original.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity)), original); }
        (await fixture.Custody.SaveAsync("tenant-a", read.Version, read.Head, token)).ShouldBeTrue();
        var fresh = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        string id = ErasureRootOccurrenceBindings.Id(first.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(first.Binding.Identity));
        (await fixture.Custody.ReadSourceAsync(fresh.Head, id, token)).ShouldNotBeNull();
        (await fixture.Custody.ReadSourceReferenceAsync(fresh.Head, first.Binding.KeyReference, token)).ShouldBeNull();
        (await fixture.Custody.ReadSourceAsync(fresh.Head, ErasureRootOccurrenceBindings.Id(second.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(second.Binding.Identity)), token)).ShouldNotBeNull();
    }
    [Fact]
    public async Task SourceArchivePrewriteWithoutCommittedErcEnrollmentCannotAuthorizeRootIssuance()
    {
        using var fixture = new ErasureRootEngineFixture(); using var independent = new ErasureRootIndependentSourceFixture(); var token = TestContext.Current.CancellationToken;
        var source = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, independent.Native, fixture.Digests, new Dictionary<string, ProtectionQualificationNativeCell> { ["tenant-a"] = new("independent", "root", new()) });
        var original = fixture.Enroll() with { Writer = new("source", "writer", new()) }; fixture.Store.Failure = "cas-before";
        await Should.ThrowAsync<IOException>(() => source.RegisterOriginalAsync(original, token)); independent.Root.OriginalIndexRoot.ShouldNotBeNull();
        var owner = original.Binding.Identity.Owner; var occurrence = ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity);
        (await source.ResolveAsync(owner, occurrence, token)).ShouldNotBeNull(); (await source.VerifyEnrollmentAsync(original, token)).ShouldBeFalse();
        var bindings = new ErasureRootOccurrenceBindings(fixture.Custody, fixture.Permissions, source, fixture.Digests);
        (await bindings.ResolveAsync(owner, occurrence, original.Binding.Identity.Target, true, token)).ShouldBeNull();
        (await fixture.Store.HeadAsync("tenant-a")).Targets.ShouldBeEmpty(); fixture.Store.CasCount.ShouldBe(0);
    }
    [Theory]
    [InlineData("hold")]
    [InlineData("hold-id")]
    [InlineData("batch")]
    [InlineData("codec")]
    [InlineData("prior-pointer")]
    [InlineData("prior-type")]
    [InlineData("lifecycle-id")]
    [InlineData("owner-id")]
    [InlineData("lookup-id")]
    [InlineData("copy-id")]
    [InlineData("fault-id")]
    [InlineData("sign-id")]
    public void MissingUnionInputsAndInvalidAvailableReferencesRefuseWholeRecipe(string mutation)
    {
        var request = ProtectionQualificationInputSchema.InstallationRequest("tenant-a");
        var first = new ProtectionQualificationNativeCall("first", ProtectionQualificationNativeCallKind.Lookup, new() { ["tenantId"] = "tenant-a", ["batchId"] = "batch" });
        var second = first with { Id = "last" }; var prior = new Dictionary<string, JsonNode?> { ["prior"] = new JsonObject { ["count"] = 4 } };
        if (mutation == "lifecycle-id")
        {
            var identity = new CustodyKeyObjectIdentity("tenant-a", "object", PlatformKeyPurpose.InteractionRootDek, "alias", "version", "lifecycle", "store", "custody");
            second = new("last", ProtectionQualificationNativeCallKind.AuthorizeLifecycle, (JsonObject)JsonSerializer.SerializeToNode(new CustodyKeyLifecycleRequest(identity, "", CustodyKeyLifecycleAction.Pin, 1, "hold", "fence", 1, "decision"))!);
        }
        else if (mutation == "owner-id") { second = new("last", ProtectionQualificationNativeCallKind.Block, (JsonObject)JsonSerializer.SerializeToNode(new DeletionBatchBlockRequest("tenant-a", "batch", "", "evidence"))!); }
        else if (mutation == "lookup-id") { second = second with { Payload = new() { ["tenantId"] = "tenant-a", ["batchId"] = "" } }; }
        else if (mutation == "copy-id") { second = new("last", ProtectionQualificationNativeCallKind.Backup, (JsonObject)JsonSerializer.SerializeToNode(new ProtectionQualificationNativeCell("source", "", new()))!); }
        else if (mutation == "fault-id") { second = new("last", ProtectionQualificationNativeCallKind.Fault, new() { ["stepId"] = "", ["boundary"] = "after" }); }
        else if (mutation == "sign-id")
        { second = new("last", ProtectionQualificationNativeCallKind.Sign, (JsonObject)JsonSerializer.SerializeToNode(new DeletionBatchCapabilityV1("issuer", "audience", "tenant-a", "request", "seal", "AcceptedSet", 0, "", new string('A', 64), "guard", 1, 1, 1, "key"))!); }
        else if (mutation is "hold" or "hold-id" or "batch")
        {
            var operation = mutation.StartsWith("hold", StringComparison.Ordinal) ? GovernanceGuardOperation.RegisterHold : GovernanceGuardOperation.RecordBatchIssued;
            var transition = new JsonObject { ["TenantId"] = "tenant-a", ["OperationId"] = "op", ["Operation"] = (int)operation, ["ExpectedGuardRevision"] = 1L, ["EpochId"] = "epoch", ["DeletionRequestId"] = "request", ["AuthorizationId"] = "authorization", ["ReferenceId"] = "reference" };
            if (mutation == "hold-id") { transition["Hold"] = JsonSerializer.SerializeToNode(new GovernanceHoldCommand("", new("tenant-a", "ExactInteraction", "interaction", ""), "disposition", "")); }
            second = new("last", mutation.StartsWith("hold", StringComparison.Ordinal) ? ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch : ProtectionQualificationNativeCallKind.Guard, new() { ["transition"] = transition, ["targets"] = new JsonArray() });
        }
        else if (mutation == "codec")
        { second = second with { Payload = new() { ["tenantId"] = "tenant-a", ["batchId"] = new JsonObject { ["$codec"] = new JsonObject { ["name"] = "TargetManifestDigest", ["input"] = "wrong-shape" } } } }; }
        else
        { second = second with { Payload = new() { ["tenantId"] = "tenant-a", ["batchId"] = new JsonObject { ["$original"] = new JsonObject { ["id"] = "prior", ["pointer"] = mutation == "prior-pointer" ? "/missing" : "/count" } } } }; }
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationInputSchema.Validate(new([first], second, null, []), request, prior));
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("capability")]
    [InlineData("cell")]
    public async Task LaterMissingOwnerArtifactOrCapabilityRefusesCompiledPreparationWithoutAnyStateOrAttempt(string missing)
    {
        using var fixture = new ProtectionQualificationPreparationFixture(); await fixture.InitializeAsync();
        var recipe = fixture.Recipes("P05")["P05"]["DestroyDekManifest"];
        var payload = (JsonObject)recipe.Effect.Payload.DeepClone();
        if (missing == "capability")
        {
            var capability = (JsonObject)recipe.Before.First(call => call.Kind == ProtectionQualificationNativeCallKind.Sign).Payload.DeepClone(); capability["Issuer"] = ""; payload["Capability"] = capability;
        }
        else if (missing == "signature") { payload["DetachedJws"] = ""; }
        else { recipe = recipe with { After = [.. recipe.After, new("missing-existing-source", ProtectionQualificationNativeCallKind.Backup, (JsonObject)JsonSerializer.SerializeToNode(new ProtectionQualificationNativeCell("source", "absent-existing-source", new()))!)] }; }
        recipe = recipe with { Effect = recipe.Effect with { Payload = payload } };
        string nativeBefore = ErasureRootCustody.Hash(await fixture.Consumption.Store.HeadAsync("tenant-a"));
        string guardBefore = ErasureRootCustody.Hash(fixture.Guard.State), sourceBefore = ErasureRootCustody.Hash(fixture.NativeState), ownerBefore = ErasureRootCustody.Hash(fixture.Consumption.State.CommittedState);
        string registryBefore = ErasureRootCustody.Hash(fixture.Engine.RegistryState.CommittedState);
        int SourceOrControlWrites() => NSubstitute.SubstituteExtensions.ReceivedCalls(fixture.Native).Count(call => call.GetMethodInfo().Name is "TrySaveStateAsync" or "SaveStateAsync" or "ExecuteStateTransactionAsync");
        int nativeWrites = SourceOrControlWrites();
        int cas = fixture.Consumption.Store.CasCount, archives = fixture.Consumption.Store.ArchiveWrites, guardWrites = fixture.Guard.Backend.CommittedTransactions;
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.RunAsync(recipe, "P05", "DestroyDekManifest"));
        fixture.Admitted.ShouldBeEmpty(); fixture.Originals.ShouldBeEmpty(); fixture.Signed.ShouldBeEmpty();
        ErasureRootCustody.Hash(await fixture.Consumption.Store.HeadAsync("tenant-a")).ShouldBe(nativeBefore);
        ErasureRootCustody.Hash(fixture.Guard.State).ShouldBe(guardBefore); ErasureRootCustody.Hash(fixture.NativeState).ShouldBe(sourceBefore); ErasureRootCustody.Hash(fixture.Consumption.State.CommittedState).ShouldBe(ownerBefore);
        fixture.Consumption.Store.CasCount.ShouldBe(cas); fixture.Consumption.Store.ArchiveWrites.ShouldBe(archives); fixture.Guard.Backend.CommittedTransactions.ShouldBe(guardWrites);
        ErasureRootCustody.Hash(fixture.Engine.RegistryState.CommittedState).ShouldBe(registryBefore); SourceOrControlWrites().ShouldBe(nativeWrites);
        fixture.Consumption.Store.RotateCount.ShouldBe(0); fixture.Consumption.Store.TrimCount.ShouldBe(0);
    }

    [Fact]
    public void FutureRaceOperandsAndSerializedScalarMembersCannotBecomeLaterInputs()
    {
        var request = ProtectionQualificationInputSchema.InstallationRequest("tenant-a");
        var effect = new ProtectionQualificationNativeCall("effect", ProtectionQualificationNativeCallKind.Lookup, new() { ["tenantId"] = "tenant-a", ["batchId"] = "batch" });
        var race = effect with { Id = "race", Payload = new() { ["tenantId"] = "tenant-a", ["batchId"] = new JsonObject { ["$original"] = new JsonObject { ["id"] = "effect", ["pointer"] = "/BatchId" } } } };
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationInputSchema.Validate(new([], effect, race, []), request, new Dictionary<string, JsonNode?>()));
        var capability = new DeletionBatchCapabilityV1("issuer", "audience", "tenant-a", "request", "seal", "AcceptedSet", 0, "batch", new string('A', 64), "guard", 1, 1, 1, "key");
        var sign = new ProtectionQualificationNativeCall("sign", ProtectionQualificationNativeCallKind.Sign, (JsonObject)JsonSerializer.SerializeToNode(capability)!);
        using var fixture = new ErasureRootEngineFixture();
        var enrollment = fixture.Enroll() with { SnapshotEntry = new(new(1, PayloadProtectionPayloadKind.Snapshot, "snapshot"), "/entry", new string('A', 64), 1) };
        var seal = (JsonObject)JsonSerializer.SerializeToNode(new ProtectionQualificationNativeSeal(enrollment, new("source", "snapshot", new())))!;
        seal["Enrollment"]!["SnapshotEntry"]!["EntryCount"] = new JsonObject { ["$original"] = new JsonObject { ["id"] = "sign", ["pointer"] = "/DetachedJws/Length" } };
        var snapshot = new ProtectionQualificationNativeCall("invalid-scalar", ProtectionQualificationNativeCallKind.SealSnapshot, seal);
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationInputSchema.Validate(new([sign], snapshot, null, []), request, new Dictionary<string, JsonNode?>()));
    }
    [Fact]
    public void InstallationMetadataChecksFullRequestBoundWithoutAuthenticatingPlaceholderTargets()
    {
        var metadata = ProtectionQualificationInputSchema.InstallationRequest("tenant-a"); metadata["targets"]!.AsArray().Count.ShouldBe(1000);
        var lookup = new ProtectionQualificationNativeCall("last-target", ProtectionQualificationNativeCallKind.Lookup, new() { ["tenantId"] = new JsonObject { ["$request"] = "/targets/999/TenantId" }, ["batchId"] = "batch" });
        ProtectionQualificationInputSchema.Validate(new([], lookup, null, []), metadata, new Dictionary<string, JsonNode?>());
        var targets = new[] { new ProtectionTarget("tenant-a", "actual-interaction", "alias") };
        var body = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "request", "seal", "AcceptedSet", 0, "batch", DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key"), "component-artifact", 1, "dispatch", 2, targets);
        var payload = (JsonObject)JsonSerializer.SerializeToNode(body)!; payload["Targets"] = new JsonObject { ["$request"] = "/targets" };
        var call = new ProtectionQualificationNativeCall("owner", ProtectionQualificationNativeCallKind.Register, payload);
        ProtectionQualificationInputSchema.Validate(new([], call, null, []), metadata, new Dictionary<string, JsonNode?>(), validateAvailableSemantics: false);
        var actual = (JsonObject)metadata.DeepClone(); actual["targets"] = JsonSerializer.SerializeToNode(targets);
        ProtectionQualificationInputSchema.Validate(new([], call, null, []), actual, new Dictionary<string, JsonNode?>());
        Should.Throw<ArgumentException>(() => ProtectionQualificationInputSchema.Validate(new([], call, null, []), metadata, new Dictionary<string, JsonNode?>()));
    }

}
