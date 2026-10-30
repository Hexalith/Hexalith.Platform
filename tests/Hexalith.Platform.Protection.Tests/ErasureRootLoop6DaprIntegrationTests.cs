using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Dapr.Client;
using Grpc.Net.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Owned real-sidecar/Redis source framing and independent replay. These cases never execute the owner verifier or Agents.</summary>
public sealed class ErasureRootLoop6DaprIntegrationTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("ERC_LOOP6_DAPR_GRPC") is { Length: > 0 };
    [Fact(Skip = "Requires the owned disposable Dapr/Redis integration harness.", SkipUnless = nameof(Enabled))]
    public async Task RealDaprSourceFramingRetainsLargeExactOriginalsAndReplaysWithDeadErc()
    {
        var token = TestContext.Current.CancellationToken; string endpoint = Environment.GetEnvironmentVariable("ERC_LOOP6_DAPR_GRPC")!;
        string phase = Environment.GetEnvironmentVariable("ERC_LOOP6_DAPR_PHASE") ?? "large";
        using var native = ErasureRootDaprTransport.Configure(new DaprClientBuilder().UseGrpcEndpoint(endpoint)).Build();
        var location = new ProtectionQualificationNativeCell("erc-source-test", "source-root", new());
        if (phase is "legacy-seed" or "legacy-default-refusal" or "legacy-read")
        {
            string payload = new('~', 5 * 1024 * 1024); byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
            string[] chunks = Enumerable.Range(0, (bytes.Length + 4194303) / 4194304).Select(index => Convert.ToHexString(SHA256.HashData(bytes.AsSpan(index * 4194304, Math.Min(4194304, bytes.Length - index * 4194304))))).ToArray();
            var legacy = new ErasureRootSourceIndexNode("tenant-a", "erc-test", "enrollment", ErasureRootCustody.Hash("legacy"), new(), "legacy", null, chunks, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
            string pointer = ErasureRootCustody.Hash(legacy);
            if (phase == "legacy-seed")
            {
                using var seed = new DaprClientBuilder().UseGrpcEndpoint(endpoint).UseGrpcChannelOptions(new GrpcChannelOptions { MaxSendMessageSize = 8 * 1024 * 1024, MaxReceiveMessageSize = 8 * 1024 * 1024 }).Build();
                for (int index = 0; index < chunks.Length; index++)
                { await seed.SaveStateAsync(location.Component, location.Key + ":source-blob-v2:" + chunks[index], bytes.AsSpan(index * 4194304, Math.Min(4194304, bytes.Length - index * 4194304)).ToArray(), cancellationToken: token); }
                await seed.SaveStateAsync(location.Component, location.Key + ":source-original-v2:" + pointer, legacy, cancellationToken: token);
            }
            else
            {
                // Former-schema nodes keep their exact keys/hashes. Their base64 response
                // needs verified 8 MiB allowances on both the sidecar and SDK.
                var reader = new ErasureRootSourceIndex(native, location, "tenant-a", "erc-test", "enrollment");
                if (phase == "legacy-default-refusal")
                {
                    var error = await Should.ThrowAsync<Dapr.DaprException>(() => reader.ReadAsync<string>(pointer, "legacy", token));
                    ((Grpc.Core.RpcException)error.InnerException!).StatusCode.ShouldBe(Grpc.Core.StatusCode.ResourceExhausted);
                }
                else { (await reader.ReadAsync<string>(pointer, "legacy", token)).ShouldBe(payload); }
            }
            Console.WriteLine(JsonSerializer.Serialize(new { realDapr = true, phase, legacyChunkBytes = 4194304, originalBytes = bytes.Length, sdkReceiveBytes = 8388608 }));
            return;
        }
        var transportIndex = new ErasureRootSourceIndex(native, location, "tenant-a", "erc-test", "enrollment");
        var transportState = JsonSerializer.SerializeToElement(new { Records = new string('x', 9 * 1024 * 1024) });
        string transportRoot;
        try { transportRoot = await transportIndex.RetainAsync(null, "actual-9m-source-state", transportState, token); }
        catch (Dapr.DaprException error) when (error.InnerException is Grpc.Core.RpcException rpc)
        { Console.WriteLine(JsonSerializer.Serialize(new { daprDiagnostic = true, code = rpc.StatusCode.ToString(), refusedSize = rpc.Status.Detail.Contains("message", StringComparison.OrdinalIgnoreCase), refusedConsistency = rpc.Status.Detail.Contains("consistency", StringComparison.OrdinalIgnoreCase), refusedState = rpc.Status.Detail.Contains("state", StringComparison.OrdinalIgnoreCase), detailLength = rpc.Status.Detail.Length })); throw; }
        using (var transportReader = ErasureRootDaprTransport.Configure(new DaprClientBuilder().UseGrpcEndpoint(endpoint)).Build())
        {
            var freshIndex = new ErasureRootSourceIndex(transportReader, location, "tenant-a", "erc-test", "enrollment");
            // JsonObject is the same supported exact JSON source state behind the generic index.
            var actual = await freshIndex.ReadAsync<System.Text.Json.Nodes.JsonObject>(transportRoot, "actual-9m-source-state", token);
            actual!["Records"]!.GetValue<string>().Length.ShouldBe(9 * 1024 * 1024);
            ErasureRootCustody.Hash(actual).ShouldBe(ErasureRootCustody.Hash(transportState));
        }
        var upper = JsonSerializer.SerializeToElement(new { Records = new string('y', 32 * 1024 * 1024 - 64) });
        string upperRoot = await transportIndex.RetainAsync(transportRoot, "actual-32m-source-state", upper, token);
        var upperReader = new ErasureRootSourceIndex(native, location, "tenant-a", "erc-test", "enrollment");
        var exactUpper = (await upperReader.ReadAsync<System.Text.Json.Nodes.JsonObject>(upperRoot, "actual-32m-source-state", token))!;
        exactUpper["Records"]!.GetValue<string>().Length.ShouldBe(32 * 1024 * 1024 - 64);
        ErasureRootCustody.Hash(exactUpper).ShouldBe(ErasureRootCustody.Hash(upper));
        using var fixture = new ErasureRootEngineFixture();
        await native.SaveStateAsync(location.Component, location.Key, new ErasureRootSourceOriginals("tenant-a", "erc-test", new()) { EnrollmentId = "enrollment" }, cancellationToken: token);
        var source = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, native, fixture.Digests,
            new Dictionary<string, ProtectionQualificationNativeCell> { ["tenant-a"] = location });
        var seedOriginal = fixture.Enroll(); var parent = new PayloadProtectionOccurrenceContext(9, PayloadProtectionPayloadKind.Snapshot, "hx-snapshot-v1:folded-state");
        var original = seedOriginal with { Binding = seedOriginal.Binding with { Identity = seedOriginal.Binding.Identity with { Kind = PayloadProtectionPayloadKind.Snapshot, PayloadTypeId = parent.PayloadTypeId, Sequence = 9 } }, Writer = new(location.Component, "writer-proof", new()) };
        fixture.Source.Enrollments.Add(ErasureRootOccurrenceBindings.Id(original.Binding.Identity.Owner, parent), original);
        (await source.RegisterOriginalAsync(original, token)).ShouldBeTrue();
        var state = JsonSerializer.SerializeToElement(new { Confidential = new string('x', 768 * 1024) });
        var type = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }.GetTypeInfo(typeof(JsonElement));
        var write = await fixture.Engine.ProtectSnapshotAsync(original.Binding.Identity.Owner, state, type, parent, token);
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token);
        var record = (await fixture.Registry.LookupAsync(original.Binding.Identity, token)).Record!;
        (await source.VerifySealedAsync(record, record.Sealed!, token)).ShouldBeTrue();
        fixture.Source.Persisted.Add(original.Binding.OperationId, record.Sealed!.PayloadBytes);
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token);
        await native.SaveStateAsync(location.Component, original.Writer!.Key, new ErasureRootWriterProof(original.Binding, Convert.ToHexString(SHA256.HashData(record.Sealed.PayloadBytes)), true, true, "native-independent-writer"), cancellationToken: token);
        var root = await native.GetStateAsync<ErasureRootSourceOriginals>(location.Component, location.Key, cancellationToken: token);
        using var fresh = ErasureRootDaprTransport.Configure(new DaprClientBuilder().UseGrpcEndpoint(endpoint)).Build();
        var snapshotIndex = new ErasureRootSourceIndex(fresh, location, "tenant-a", "erc-test", "enrollment");
        var exact = (await snapshotIndex.ReadAsync<InteractionOccurrenceSealedResult>(root.SnapshotIndexRoot, original.Binding.KeyReference, token))!;
        exact.PayloadBytes.ShouldBe(record.Sealed.PayloadBytes); exact.SnapshotReplaySkeleton.ShouldBe(record.Sealed.SnapshotReplaySkeleton);
        var loss = new ErasureRootPermanentLossReceipt("erc-test", "enrollment", "tenant-a", "retired-owned-volume", "independent-retirement", new string('A', 64));
        fixture.Permissions.PermanentLoss = loss;
        var independent = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, fresh, fixture.Digests, new Dictionary<string, ProtectionQualificationNativeCell> { ["tenant-a"] = location });
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, independent, Substitute.For<IGovernanceScopeGuard>(), Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), loss.CopyPolicySha256, TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. handler.Methods] }],
            QualificationCases = new() { ["erc.permanent-loss:erc-test:enrollment"] = JsonSerializer.SerializeToElement(loss, CustodyBindingContract.Json) },
            QualificationBindings = new() { ["erc.copyPolicySha256"] = loss.CopyPolicySha256, ["erc.volume.erc-test"] = loss.RetiredVolumeIdentity } };
        var sourceClient = new DaprErasureRootOccurrenceSource(receiver.Authority);
        var bindings = new ErasureRootOccurrenceBindings(fixture.Custody, fixture.Permissions, sourceClient);
        fixture.Store.ReadObserved = () => throw new IOException("Dead former ERC");
        var engine = new InteractionPayloadProtectionService(fixture.Registry, fixture.Keys, bindings, TimeProvider.System);
        var erased = await engine.TryReplaySnapshotAsync(original.Binding.Identity.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata, type, parent, token);
        erased.IsReadable.ShouldBeTrue(); ((JsonElement)erased.State!).GetProperty("State").GetString().ShouldBe("Erased");
        Console.WriteLine(JsonSerializer.Serialize(new { realDapr = true, phase, exactSourceStateBytes = JsonSerializer.SerializeToUtf8Bytes(transportState).Length, upperSourceStateBytes = JsonSerializer.SerializeToUtf8Bytes(upper).Length, plaintextBytes = JsonSerializer.SerializeToUtf8Bytes(state).Length, sealedBytes = exact.PayloadBytes.Length,
            originalBytes = JsonSerializer.SerializeToUtf8Bytes(exact).Length, sourceChunkBytes = ErasureRootSourceIndex.TransportChunkBytes, sidecarRequestBytes = 4194304, sdkSendLimit = "unchanged SDK default", sdkReceiveBytes = 8388608, deadErcReplay = "Erased" }));
    }
}
