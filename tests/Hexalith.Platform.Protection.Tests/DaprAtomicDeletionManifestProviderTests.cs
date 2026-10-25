using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Protection;
using Hexalith.Platform.Secrets.Runtime;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class DaprAtomicDeletionManifestProviderTests
{
    [Fact]
    public async Task MissingIndependentAuthorizerKeepsCompleteManifestUnknownAndLookupNeverChangesOriginalIdentity()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var dapr = new DaprClientBuilder().Build();
        var source = new AbsentAuthorizer();
        var enrollment = new AuthorityEnrollmentV1("authority", "incarnation", "store", "profile", "trust", "anchors", new string('A', 64));
        var client = new CustodyLifecycleClient(dapr, new Dictionary<string, string> { [CustodyLifecycleClient.LogicalRole] = "custody" },
            "owner", JsonSerializer.Serialize(enrollment), signer.ExportSubjectPublicKeyInfoPem(), source);
        var provider = new DaprAtomicDeletionManifestProvider(client);
        ProtectionTarget[] targets = [new("tenant", "first", "alias"), new("tenant", "second", "alias")];
        var capability = new DeletionBatchCapabilityV1("issuer", "audience", "tenant", "deletion", "seal", "accepted", 0, "batch",
            DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key-version");
        var original = new DeletionBatchConsumptionRequest(capability, "exact-original-signature", 2, "dispatch", 3, targets);
        var consume = await provider.ConsumeAsync(original, "reservation", TestContext.Current.CancellationToken);
        var lookup = await provider.LookupAsync(original, "reservation", TestContext.Current.CancellationToken);
        consume.State.ShouldBe(DeletionManifestProviderState.Unknown); lookup.State.ShouldBe(DeletionManifestProviderState.Unknown);
        consume.TargetReceipts.ShouldBeEmpty(); lookup.TargetReceipts.ShouldBeEmpty();
        source.Intents.Count.ShouldBe(2);
        source.Intents[0].Operation.ShouldBe("destroy-manifest"); source.Intents[1].Operation.ShouldBe("lookup-original");
        source.Intents[0].OperationId.ShouldBe(source.Intents[1].OperationId);
        source.Intents[0].RequestDigest.ShouldBe(source.Intents[1].RequestDigest);
        source.Intents[0].RequestAttemptId.ShouldNotBe(source.Intents[1].RequestAttemptId);
        using var body = JsonDocument.Parse(source.Intents[0].BodyJson);
        var transmitted = body.RootElement.GetProperty("Manifest").Deserialize<CustodyBindingManifestV1>(CustodyBindingContract.Json)!;
        transmitted.Original.Targets.ShouldBe(targets);
        transmitted.Original.DetachedJws.ShouldBe(original.DetachedJws);
        transmitted.SignedCanonicalPayload.ShouldBe(DeletionBatchCapabilityIdentity.CanonicalPayload(capability));
    }

    private sealed class AbsentAuthorizer : ICustodyLifecycleCallSource
    {
        internal List<CustodyCallIntent> Intents = [];
        public Task<CustodyPreparedCall?> PrepareAsync(CustodyCallIntent intent, CancellationToken cancellationToken)
        { Intents.Add(intent); return Task.FromResult<CustodyPreparedCall?>(null); }
        public Task<CustodyCallAuthorization?> AuthorizeAsync(CustodyCallIntent intent, CustodyPreparedCall prepared, string requestSha256, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Missing source cannot authorize.");
    }
}
