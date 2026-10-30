using System.Text.Json;
using Hexalith.EventStore.Server.Security;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

using Shouldly;

// This test intentionally executes the embedded, hash-pinned owner fixture in this assembly.
// The production Server also exports this name. Keep the fixture and every unrelated diagnostic enforced.
#pragma warning disable CS0436 // Intentional local pinned owner fixture at the callers below.
namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class CustodyBindingFeasibilityTests
{
    [Fact]
    public async Task EmitDeterministicF1Evidence()
    {
        await CustodyBindingLocalTransport.RunIfConfiguredAsync(TestContext.Current.CancellationToken);
        var requests = new List<object>();
        foreach (var construction in new (string Name, int Count, string? Field, bool Maximal)[] { ("single", 1, null, false),
            ("ordinary-1000", 1000, null, false), ("ascii-2048x1000", 1000, new string('x', 2048), true),
            ("escaped-control-2048x1000", 1000, new string('\u0001', 2048), true),
            ("unicode-bmp-2048x1000", 1000, new string('\u0800', 2048), true),
            ("unicode-astral-2048x1000", 1000, string.Concat(Enumerable.Repeat("😀", 1024)), true) })
        {
            bool structural = construction.Name.StartsWith("unicode-", StringComparison.Ordinal);
            var request = CustodyBindingFixture.Manifest(construction.Count, construction.Field, construction.Maximal, structural);
            if (structural)
            {
                Should.Throw<ArgumentException>(() => DeletionConsumptionIdentity.Capture(request.Manifest!.Original));
                CustodyBindingContract.Validate(request).ShouldBeFalse();
            }
            else
            {
                DeletionConsumptionIdentity.Capture(request.Manifest!.Original).Targets.ShouldBe(request.Manifest.Original.Targets);
                CustodyBindingContract.Validate(request).ShouldBeTrue();
            }
            byte[] bytes = structural ? JsonSerializer.SerializeToUtf8Bytes(request, CustodyBindingContract.Json) : CustodyBindingContract.Encode(request);
            int sdk = CustodyBindingDaprInvoker.ApiRequestSize("synthetic-configured-role", "destroy-manifest", bytes.Length);
            int component = CustodyBindingDaprInvoker.ComponentRequestSize("destroy-manifest", bytes.Length);
            if (construction.Maximal) { sdk.ShouldBeGreaterThan(CustodyBindingContract.DefaultMessageBytes); }
            else { sdk.ShouldBeLessThan(CustodyBindingContract.DefaultMessageBytes); }
            requests.Add(new { construction.Name, LegalOwnerRequest = !structural, ActualOwnerCapturePassed = !structural, TargetCount = construction.Count, FieldUtf16Units = construction.Field?.Length,
                Construction = construction.Maximal ? "legal ASCII/control: tenant=64 ASCII t; structural Unicode: tenant=field (owner-invalid); interaction=field[..2044]+D4 ordinal; alias=field; batch=batch; ordinals=0..999" : "CustodyBindingFixture.Manifest(count)",
                JsonBytes = bytes.Length, JsonSha256 = CustodyBindingContract.Hash(bytes), SdkApiProtobufBytes = sdk,
                SdkGrpcFrameBytes = sdk + 5, SidecarApiReceiveLimitBytes = CustodyBindingContract.DefaultMessageBytes,
                SidecarComponentRequestProtobufBytes = component, ComponentServerDefaultReceiveLimitBytes = CustodyBindingContract.DefaultMessageBytes,
                FitsPinnedDefault = sdk <= CustodyBindingContract.DefaultMessageBytes && component <= CustodyBindingContract.DefaultMessageBytes });
        }

        var retainedMaximum = CustodyBindingFixture.Manifest(1000, new string('\u0001', 2048), maximal: true);
        DeletionConsumptionIdentity.Capture(retainedMaximum.Manifest!.Original);
        var retainedSdk = await CustodyBindingDaprInvokerTests.CaptureCandidateAsync(retainedMaximum, TestContext.Current.CancellationToken);
        retainedSdk.DataBytes.ShouldBe(24_692_881); retainedSdk.ApiProtobufBytes.ShouldBe(24_692_931);
        var transitionBytes = new byte[RecoverableAnchoredState.MaximumPendingBytes];
        var transition = new AnchoredStateTransition("owner|purpose", 1, 2, CustodyBindingFixture.Digest,
            CustodyBindingContract.Hash(transitionBytes), transitionBytes);
        var transitionRequest = CustodyBindingFixture.Bind(new(CustodyBindingFixture.Envelope("admit-transition", target: transition.TargetDigest), Transition: transition));
        var transitionSdk = await CustodyBindingDaprInvokerTests.CaptureCandidateAsync(transitionRequest, TestContext.Current.CancellationToken);
        transitionSdk.DataBytes.ShouldBeLessThan(CustodyBindingContract.MaximumTransitionRequestBound);
        CustodyBindingContract.Encode(retainedMaximum).Length.ShouldBeLessThan(CustodyBindingContract.MaximumManifestRequestBound);

        // Legal owner input with large escaped targets/receipts; conservative structural bound remains 64 MiB.
        var maximum = CustodyBindingFixture.Manifest(1000, new string('\u0001', 2048), maximal: true);
        var m = maximum.Manifest!;
        var capability = m.Original.Capability with { BatchId = new string('b', 256) };
        maximum = CustodyBindingFixture.Bind(maximum with { Manifest = m with { Original = m.Original with { Capability = capability },
            SignedCanonicalPayload = DeletionBatchCapabilityIdentity.CanonicalPayload(capability) } });
        DeletionConsumptionIdentity.Capture(maximum.Manifest!.Original);
        var original = CustodyBindingFixture.Original(maximum);
        original = original with { Result = original.Result with { TargetReceipts = original.Result.TargetReceipts.Select((r, n) =>
            r with { ReceiptId = new string('\u0001', 2044) + n.ToString("D4") }).ToArray() }, Signature = new byte[8192] };
        byte[] resultBytes = JsonSerializer.SerializeToUtf8Bytes(original, CustodyBindingContract.Json);
        resultBytes.Length.ShouldBeGreaterThan(RecoverableAnchoredState.MaximumPendingBytes);
        resultBytes.Length.ShouldBeLessThan(CustodyBindingContract.MaximumOriginalBytes);
        int fullPageFrameBytes = JsonSerializer.SerializeToUtf8Bytes(new CustodyBindingOriginalPageV1(
            CustodyBindingFixture.Header(maximum, original).Header, 0, CustodyBindingContract.MaximumPageBytes,
            CustodyBindingContract.Hash(resultBytes.AsSpan(0, CustodyBindingContract.MaximumPageBytes)),
            resultBytes.AsSpan(0, CustodyBindingContract.MaximumPageBytes).ToArray()), CustodyBindingContract.Json).Length;
        fullPageFrameBytes.ShouldBeLessThan(CustodyBindingContract.MaximumPageFrameBytes);

        var representative = LedgerMeasure(maximal: false);
        var worst = LedgerMeasure(maximal: true);
        representative.Bytes.ShouldBeLessThan(RecoverableAnchoredState.MaximumPendingBytes);
        worst.Bytes.ShouldBeGreaterThan(RecoverableAnchoredState.MaximumPendingBytes);
        long resultUpperBound = 1000L * (5 * 2048 * 6 + 256) + 16 * 2048 * 6 + 16384;
        resultUpperBound.ShouldBeLessThan(CustodyBindingContract.MaximumOriginalBytes);
        var evidence = new
        {
            Kind = "SyntheticF1SourceContractOnly",
            OwnerCaptureSource = new { Path = "EventStore:src/Hexalith.EventStore.Server/Security/DeletionConsumptionIdentity.cs",
                Sha256 = OwnerCaptureSourceHash(), LocalFixture = "tests/Hexalith.Platform.Secrets.Runtime.Tests/OwnerConformance/DeletionConsumptionIdentity.cs",
                Provenance = "Byte-identical pinned local test-only fixture; source-mode verifier compares its SHA with the clean pinned EventStore owner source" },
            DaprRuntime = "1.18.1", DaprClient = "1.18.10",
            DependencyAvailable = false, LiveReady = false, QualifiedComponentInstalled = false,
            PhysicalEvidence = false, NativeEffects = 0, RealSidecarPathExecuted = false,
            SdkSerialization = "Real pinned SDK exercised against a local HttpMessageHandler in CustodyBindingDaprInvokerTests; not daprd/component/provider evidence",
            RequestVectors = requests,
            CandidateSdkCaptures = new { RetainedMaximumManifest = retainedSdk, MaximumPendingTransition = transitionSdk,
                ComponentForwardedMetadataWireBudgetBytes = CustodyBindingDaprInvoker.MaximumForwardedMetadataWireBytes,
                FullManifestRequestBoundBytes = CustodyBindingContract.MaximumManifestRequestBound,
                FullTransitionRequestBoundBytes = CustodyBindingContract.MaximumTransitionRequestBound,
                DecodeCapBytes = CustodyBindingContract.MaximumRequestBytes,
                SidecarOrComponentExecuted = false, ProductionInvokerProfileChanged = false },
            OriginalResult = new { InputActualOwnerCapturePassed = true, Measurement = "Large legal-owner-input synthetic result; not physical evidence or a tight legal maximum", JsonBytes = resultBytes.Length, JsonSha256 = CustodyBindingContract.Hash(resultBytes),
                MaximumAssemblyBytes = CustodyBindingContract.MaximumOriginalBytes, DerivedClosedResultUpperBoundBytes = resultUpperBound,
                PageDataBytes = CustodyBindingContract.MaximumPageBytes, FullPageJsonBytes = fullPageFrameBytes,
                MaximumPageWireBytesExclusive = CustodyBindingContract.MaximumPageFrameBytes,
                PagesRequired = (resultBytes.Length + CustodyBindingContract.MaximumPageBytes - 1) / CustodyBindingContract.MaximumPageBytes,
                Construction = "tenant=64 ASCII t; interaction/alias maximal U+0001 fields; batch=256 ASCII b; receipt=2044 U+0001+D4 ordinal; signature=8192 zero bytes; other fields fixed" },
            ActorHeadroom = new { Registrations = 1000, RetainedRequests = 10000, PendingLimitBytes = RecoverableAnchoredState.MaximumPendingBytes,
                RepresentativeLedgerBytes = representative.Bytes, RepresentativeHeadroomBytes = RecoverableAnchoredState.MaximumPendingBytes - representative.Bytes,
                StructuralFieldCountLedgerBytes = worst.Bytes, StructuralHeadroomBytes = RecoverableAnchoredState.MaximumPendingBytes - worst.Bytes,
                WorstEntryBytes = worst.EntryBytes,
                Construction = "Structural field/count-bound witness, not a reachable original schedule: 1000 fixed-width distinct objects; ten distinct Pinned requests/outcomes and ten retained pins per object; text=2044 U+0001+D4, <=2048 UTF8 bytes; exact JSON array accounting from equal-width entries. No current-authority or admitted revision history is proved.",
                CompletionCapacityProven = false,
                Required = "Component must reserve byte/native/buffer completion headroom at admission; count limits alone cannot fit every legal retained actor ledger. Existing actor limits/policy remain unchanged." },
            QualificationStop = "Pinned defaults remain closed; bounded 128 MiB candidate SDK serialization is synthetic only. Component/backend, installed header/metadata limits, independent completion verifier and separate authorization remain absent. No tighter public fields or splitting.",
            BufferClosure = "Unproved: SDK ByteString/read-only response, grpc/sidecar/component and late I/O copies require actual holder inventory and accepted zeroing or termination proof.",
        };
        string? path = Environment.GetEnvironmentVariable("EXT_SECRETS_1_F1_EVIDENCE");
        if (path is not null)
        {
            Path.IsPathFullyQualified(path).ShouldBeTrue();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Deliberately no timestamp/random values: same source/fixtures produce identical bytes.
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(evidence, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static string OwnerCaptureSourceHash()
    {
        using var source = typeof(CustodyBindingFeasibilityTests).Assembly.GetManifestResourceStream("PinnedDeletionConsumptionIdentitySource")!;
        using var bytes = new MemoryStream(); source.CopyTo(bytes);
        return CustodyBindingContract.Hash(bytes.ToArray());
    }

    private static (long Bytes, int EntryBytes) LedgerMeasure(bool maximal)
    {
        string Text(string small, int n = 0) => maximal ? new string('\u0001', 2044) + n.ToString("D4") : small + n.ToString("D4");
        CustodyKeyLifecycleEntry Entry(int key)
        {
            var identity = new CustodyKeyObjectIdentity(Text("tenant"), Text("object", key), PlatformKeyPurpose.InteractionRootDek,
                Text("alias"), Text("version"), Text("lifecycle"), Text("store"), Text("contract"));
            var registration = new CustodyKeyRegistration(identity, Text("wrap"), 1, Text("kek"), Text("opaque"), Text("wrap-receipt"));
            var requests = Enumerable.Range(0, 10).Select(n => new CustodyKeyLifecycleRequest(identity, Text("operation", n), CustodyKeyLifecycleAction.Pin,
                n + 1, Text("hold", n), Text("fence"), 1, Text("decision"))).ToArray();
            var outcomes = requests.Select(r => new CustodyKeyLifecycleOutcome(identity, r.OperationId,
                CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(r)), CustodyKeyLifecycleStatus.Pinned, Text("receipt"))).ToArray();
            return new(registration, requests, requests, outcomes);
        }
        // Every varying suffix has four unescaped digits, hence every entry has exactly the same encoded length.
        int entryBytes = JsonSerializer.SerializeToUtf8Bytes(Entry(0)).Length;
        JsonSerializer.SerializeToUtf8Bytes(Entry(999)).Length.ShouldBe(entryBytes);
        string tenant = Text("tenant");
        int emptyBytes = JsonSerializer.SerializeToUtf8Bytes(new CustodyKeyLifecycleLedger(tenant, 11000, [])).Length;
        long bytes = emptyBytes + 1000L * entryBytes + 999;
        if (!maximal)
        {
            var actual = new CustodyKeyLifecycleLedger(tenant, 11000, Enumerable.Range(0, 1000).Select(Entry).ToArray());
            JsonSerializer.SerializeToUtf8Bytes(actual).LongLength.ShouldBe(bytes);
        }
        return (bytes, entryBytes);
    }
}

#pragma warning restore CS0436
