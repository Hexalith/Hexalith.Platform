using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual signed receiver transport verifies current explicit private grants, request correlation and revocation.</summary>
public sealed class ErasureRootReceivingTests
{
    [Fact]
    public async Task AuthenticatedRevocationOriginalIsRetainedBeforeRegistrarAndReverifiedForEveryRead()
    {
        using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var authenticator = Substitute.For<IDeletionCapabilityRevocationAuthenticator>();
        var envelope = new DeletionCapabilityRevocationEnvelope("issuer", "audience", "tenant-a", "DeletionBatchCapabilitySigningKey", "compromised-key", 1, 1, "exact-event", new string('A', 64));
        var authorization = new DeletionCapabilityRevocationAuthorization(envelope, "authority-v1", DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddMinutes(5));
        authenticator.VerifyAsync(envelope, "exact-signed-evidence", Arg.Any<CancellationToken>()).Returns(authorization);
        var source = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, native, fixture.Digests);
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, source, Substitute.For<IGovernanceScopeGuard>(), authenticator, new string('B', 64), TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. receiver.State.Callers[0].Methods, .. handler.Methods] }] };
        var evidence = new DaprErasureRootRevocationEvidence(receiver.Authority);
        (await evidence.VerifyRevocationEvidenceAsync(envelope, TestContext.Current.CancellationToken)).ShouldBeFalse();
        var recording = new ErasureRootRecordingRevocationAuthenticator(authenticator, receiver.Authority);
        (await recording.VerifyAsync(envelope, "wrong-evidence", TestContext.Current.CancellationToken)).ShouldBeNull();
        (await fixture.Store.RetainedHeadAsync("tenant-a")).RevocationOriginals.ShouldBeEmpty();
        (await recording.VerifyAsync(envelope, "exact-signed-evidence", TestContext.Current.CancellationToken)).ShouldBe(authorization);
        (await evidence.VerifyRevocationEvidenceAsync(envelope, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await fixture.Store.RetainedHeadAsync("tenant-a")).RevocationOriginals.Values.Single().Envelope.ShouldBe(envelope);
        authenticator.VerifyAsync(envelope, "exact-signed-evidence", Arg.Any<CancellationToken>()).Returns((DeletionCapabilityRevocationAuthorization?)null);
        (await evidence.VerifyRevocationEvidenceAsync(envelope, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
    [Fact]
    public async Task PermanentVolumeLossRequiresSignedRetirementAndExactIndependentPersistedCiphertext()
    {
        using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var original = fixture.Enroll() with { Writer = new("source-proof", "exact-writer", new Dictionary<string, string>()) };
        var independent = new ErasureRootSourceOriginals("tenant-a", "erc-test", new()); int sourceRevision = 0;
        native.GetStateAndETagAsync<ErasureRootSourceOriginals>("source-originals", "source-index", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => (JsonSerializer.Deserialize<ErasureRootSourceOriginals>(JsonSerializer.Serialize(independent))!, sourceRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        native.TrySaveStateAsync("source-originals", "source-index", Arg.Any<ErasureRootSourceOriginals>(), Arg.Any<string>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => { if (call.ArgAt<string>(3) != sourceRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)) { return false; } independent = call.ArgAt<ErasureRootSourceOriginals>(2); sourceRevision++; return true; });
        native.GetStateAsync<ErasureRootSourceOriginals>("source-originals", "source-index", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(_ => independent);
        var nodes = new Dictionary<string, ErasureRootSourceIndexNode>();
        native.SaveStateAsync("source-originals", Arg.Any<string>(), Arg.Any<ErasureRootSourceIndexNode>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => { nodes[call.ArgAt<string>(1)] = call.ArgAt<ErasureRootSourceIndexNode>(2); return Task.CompletedTask; });
        native.GetStateAsync<ErasureRootSourceIndexNode>("source-originals", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => nodes.GetValueOrDefault(call.ArgAt<string>(1))!);
        var source = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, native, fixture.Digests,
            new Dictionary<string, ProtectionQualificationNativeCell> { ["tenant-a"] = new("source-originals", "source-index", new()) });
        string policy = new('A', 64);
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, source, Substitute.For<IGovernanceScopeGuard>(), Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), policy, TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. receiver.State.Callers[0].Methods, .. handler.Methods] }] };
        var sourceClient = new DaprErasureRootOccurrenceSource(receiver.Authority);
        (await sourceClient.RegisterOriginalAsync(original, TestContext.Current.CancellationToken)).ShouldBeTrue();
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = original.Binding.Identity.Target, Value = new { Secret = "loss-canary" } } });
        var occurrence = ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity);
        var write = await fixture.Engine.ProtectEventPayloadAsync(original.Binding.Identity.Owner, Substitute.For<IEventPayload>(), original.Binding.Identity.PayloadTypeId, plain, "json", occurrence, TestContext.Current.CancellationToken);
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, TestContext.Current.CancellationToken);
        fixture.Source.Persisted.Add(original.Binding.OperationId, write.ProtectionResult.PayloadBytes.ToArray());
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, TestContext.Current.CancellationToken);
        var proof = new ErasureRootWriterProof(original.Binding, Convert.ToHexString(SHA256.HashData(write.ProtectionResult.PayloadBytes)), true, true, "native-source-original");
        native.GetStateAsync<ErasureRootWriterProof>("source-proof", "exact-writer", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(proof);
        var installation = new ErasureRootInstallation("erc-test", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant-a"] = "epoch" });
        var permissions = new DaprErasureRootAuthorization(receiver.Authority, fixture.Store, installation);
        var custody = new ErasureRootCustody(fixture.Store, installation, permissions);
        var bindings = new ErasureRootOccurrenceBindings(custody, permissions, sourceClient);
        var engine = new InteractionPayloadProtectionService(fixture.Registry, fixture.Keys, bindings, TimeProvider.System);
        fixture.Store.LoseVolume("tenant-a");
        var unknown = await engine.TryReplayEventPayloadAsync(original.Binding.Identity.Owner, original.Binding.Identity.PayloadTypeId, write.ProtectionResult.PayloadBytes,
            "json+pdenc-v2", write.ProtectionResult.Metadata, occurrence, TestContext.Current.CancellationToken);
        unknown.HasErasedContent.ShouldBeFalse(); unknown.Outcome.IsUnreadable.ShouldBeTrue();
        var loss = new ErasureRootPermanentLossReceipt("erc-test", "enrollment", "tenant-a", "retired-volume-uid", "final-independent-retirement", policy);
        receiver.State = receiver.State with { QualificationCases = new() { ["erc.permanent-loss:erc-test:enrollment"] = JsonSerializer.SerializeToElement(loss, CustodyBindingContract.Json) },
            QualificationBindings = new() { ["erc.copyPolicySha256"] = policy, ["erc.volume.erc-test"] = loss.RetiredVolumeIdentity } };
        (await permissions.ReadPermanentLossAsync("tenant-a", "erc-test", "enrollment", TestContext.Current.CancellationToken)).ShouldBe(loss);
        (await sourceClient.ResolveAsync(original.Binding.Identity.Owner, occurrence, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        (await sourceClient.ResolveErasedStoredAsync(original.Binding.Identity.Owner, occurrence, Convert.ToHexString(SHA256.HashData(write.ProtectionResult.PayloadBytes)),
            original.Binding.KeyReference, loss, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var erased = await engine.TryReplayEventPayloadAsync(original.Binding.Identity.Owner, original.Binding.Identity.PayloadTypeId, write.ProtectionResult.PayloadBytes,
            "json+pdenc-v2", write.ProtectionResult.Metadata, occurrence, TestContext.Current.CancellationToken);
        erased.HasErasedContent.ShouldBeTrue(); erased.Outcome.IsReadable.ShouldBeTrue();
        JsonDocument.Parse(erased.Outcome.PayloadBytes!).RootElement.GetProperty("ProtectedContent").GetProperty("Value").GetProperty("State").GetString().ShouldBe("Erased");
        (await custody.WrapAsync(original.RootIdentity, "forbidden-reissue", 0, TestContext.Current.CancellationToken)).ShouldBeNull();
        var changed = write.ProtectionResult.PayloadBytes.ToArray(); changed[^5] ^= 1;
        (await engine.TryReplayEventPayloadAsync(original.Binding.Identity.Owner, original.Binding.Identity.PayloadTypeId, changed, "json+pdenc-v2", write.ProtectionResult.Metadata,
            occurrence, TestContext.Current.CancellationToken)).HasErasedContent.ShouldBeFalse();
    }
    [Fact]
    public async Task PrivatePermissionRequiresExactRoleMethodAndCurrentGrant()
    {
        using var fixture = new ErasureRootEngineFixture();
        using var native = Substitute.For<DaprClient>();
        var source = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, native, fixture.Digests);
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, source, Substitute.For<IGovernanceScopeGuard>(),
            Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), new string('A', 64), TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        receiver.State = receiver.State with
        {
            Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. receiver.State.Callers[0].Methods, .. handler.Methods] }],
            QualificationBindings = new() { ["erc.allow.WrapDek"] = "erc-owner" },
        };
        var installation = new ErasureRootInstallation("erc-test", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant-a"] = "epoch" });
        var client = new DaprErasureRootAuthorization(receiver.Authority, fixture.Store, installation);
        (await client.AuthorizeAsync("tenant-a", "root-original", "WrapDek", new string('B', 64), TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await client.AuthorizeAsync("tenant-a", "root-original", "UnwrapDek", new string('B', 64), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await client.AuthorizeAsync("tenant-b", "root-original", "WrapDek", new string('B', 64), TestContext.Current.CancellationToken)).ShouldBeFalse();
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { Revoked = true }] };
        (await client.AuthorizeAsync("tenant-a", "root-original", "WrapDek", new string('B', 64), TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>R11-EC-1: absent qualification bindings deny the binding-dependent permission checks as a signed denial instead of throwing.</summary>
    [Theory]
    [InlineData("VerifyReservedManifestAndCopyPolicy")]
    [InlineData("VerifyLifecycle")]
    public async Task AbsentQualificationBindingsDenyWithoutThrowing(string operation)
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, new(fixture.Custody, fixture.Permissions, native, fixture.Digests), Substitute.For<IGovernanceScopeGuard>(),
            Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), new string('A', 64), TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        var grant = new IndependentCallerGrant(new string('0', 64), "erc-owner-identity", "tenant-a|erc", "erc-owner", [.. handler.Methods], DateTimeOffset.UtcNow.AddHours(1), false);
        object original;
        string digest;
        if (operation == "VerifyLifecycle")
        {
            // Every earlier conjunct holds, so only the bindings lookup decides the result.
            var request = new CustodyKeyLifecycleRequest(new CustodyKeyObjectIdentity("tenant-a", "interaction-a", PlatformKeyPurpose.InteractionRootDek, "alias-a", "root-v1",
                "lifecycle", "erc-test", "custody-1"), "lifecycle-operation", CustodyKeyLifecycleAction.Pin, 0, "hold-1", "fence-1", 1, "decision-1");
            original = request;
            digest = ErasureRootCustody.Hash(request);
        }
        else
        {
            original = new { ownerRevision = 1L, ownerStateDigest = new string('C', 64) };
            digest = new string('B', 64);
        }
        var payload = JsonSerializer.SerializeToElement(new { tenantId = "tenant-a", identity = "deferred-row", operation, requestDigest = digest, original }, CustodyBindingContract.Json);
        foreach (var bindings in new Dictionary<string, string>?[] { null, new() })
        {
            var result = await handler.HandleAsync("erc-permission", payload, grant, receiver.State with { QualificationBindings = bindings }, token);
            var evidence = result.ShouldBeOfType<ErasureRootPermissionEvidence>();
            evidence.Permitted.ShouldBeFalse();
            evidence.Operation.ShouldBe(operation);
            evidence.TenantId.ShouldBe("tenant-a");
        }
    }

    /// <summary>R11-BH-7: erased reads keep the resolve identity checks; a source original for another owner, occurrence, key reference or store is never adopted.</summary>
    [Fact]
    public async Task ErasedReadsRefuseEverySubstitutedSourceIdentity()
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture();
        var original = fixture.Enroll() with { Writer = new("source-proof", "exact-writer", new Dictionary<string, string>()) };
        var installation = new ErasureRootInstallation("erc-test", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant-a"] = "epoch" });
        var loss = new ErasureRootPermanentLossReceipt("erc-test", "enrollment", "tenant-a", "retired-volume-uid", "final-independent-retirement", new string('A', 64));
        var authorization = Substitute.For<IErasureRootAuthorization>();
        authorization.ReadPermanentLossAsync("tenant-a", "erc-test", "enrollment", Arg.Any<CancellationToken>()).Returns(loss);
        var custody = new ErasureRootCustody(fixture.Store, installation, authorization);
        var source = Substitute.For<IErasureRootOccurrenceSource>();
        ErasureRootOccurrenceEnrollment? returned = original;
        source.ResolveErasedStoredAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<PayloadProtectionOccurrenceContext>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<ErasureRootPermanentLossReceipt>(), Arg.Any<CancellationToken>()).Returns(_ => returned);
        var bindings = new ErasureRootOccurrenceBindings(custody, authorization, source);
        var identity = original.Binding.Identity;
        var occurrence = ErasureRootOccurrenceBindings.Occurrence(identity);
        byte[] sealedPayload = "{\"ProtectedContent\":{\"Value\":{\"$pdenc\":\"sealed\"}}}"u8.ToArray();
        (await bindings.ResolveErasedStoredAsync(identity.Owner, occurrence, sealedPayload, original.Binding.KeyReference, token)).ShouldBe(original.Binding);
        ErasureRootOccurrenceEnrollment[] substitutions =
        [
            original with { Binding = original.Binding with { Identity = identity with { Owner = new Hexalith.EventStore.Contracts.Identity.AggregateIdentity("tenant-a", "conversations", "other-directory") } } },
            original with { Binding = original.Binding with { Identity = identity with { Sequence = identity.Sequence + 1 } } },
            original with { Binding = original.Binding with { Identity = identity with { Kind = PayloadProtectionPayloadKind.Snapshot } } },
            original with { Binding = original.Binding with { Identity = identity with { PayloadTypeId = "Other.Type" } } },
            original with { Binding = original.Binding with { KeyReference = "01ARZ3NDEKTSV4RRFFQ69G5FAW" } },
            original with { RootIdentity = original.RootIdentity with { StoreTarget = "other-store" } },
        ];
        foreach (var substituted in substitutions)
        {
            returned = substituted;
            (await bindings.ResolveErasedStoredAsync(identity.Owner, occurrence, sealedPayload, original.Binding.KeyReference, token)).ShouldBeNull();
        }

        // The native skeleton read binds the caller's exact binding before reading any persisted-writer proof.
        using var native = Substitute.For<DaprClient>();
        var independent = new ErasureRootSourceOriginals("tenant-a", "erc-test", new() { [ErasureRootOccurrenceBindings.Id(identity.Owner, occurrence)] = original });
        native.GetStateAsync<ErasureRootSourceOriginals>("source-originals", "source-index", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => independent);
        var nativeSource = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, native, fixture.Digests,
            new Dictionary<string, ProtectionQualificationNativeCell> { ["tenant-a"] = new("source-originals", "source-index", new()) });
        string sealedDigest = Convert.ToHexString(SHA256.HashData(sealedPayload));
        (await nativeSource.ReadErasedSnapshotSkeletonAsync(original.Binding with { KeyReference = "01ARZ3NDEKTSV4RRFFQ69G5FAW" }, sealedDigest, loss, token)).ShouldBeNull();
        await native.DidNotReceive().GetStateAsync<ErasureRootWriterProof>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
        (await nativeSource.ReadErasedSnapshotSkeletonAsync(original.Binding, sealedDigest, loss, token)).ShouldBeNull();
        await native.Received(1).GetStateAsync<ErasureRootWriterProof>("source-proof", "exact-writer", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());

        // Without an authenticated permanent loss no erased read is attempted.
        authorization.ReadPermanentLossAsync("tenant-a", "erc-test", "enrollment", Arg.Any<CancellationToken>()).Returns((ErasureRootPermanentLossReceipt?)null);
        returned = original;
        source.ClearReceivedCalls();
        (await bindings.ResolveErasedStoredAsync(identity.Owner, occurrence, sealedPayload, original.Binding.KeyReference, token)).ShouldBeNull();
        await source.DidNotReceive().ResolveErasedStoredAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<PayloadProtectionOccurrenceContext>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ErasureRootPermanentLossReceipt>(), Arg.Any<CancellationToken>());
    }
}
