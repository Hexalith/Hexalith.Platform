using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Secrets.Runtime;

using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class CustodyBindingContractTests
{
    [Fact]
    public void RejectedDecodedWrapClearsOnlyItsOwnedRootBuffer()
    {
        var envelope = CustodyBindingFixture.Envelope("wrap");
        var input = new CustodyBindingWrapInputV1(CustodyBindingFixture.Identity, envelope.OperationId, envelope.ExpectedCustodyRevision,
            "kek", "wrapped", Enumerable.Repeat((byte)7,32).ToArray(), new("lease", "attempt", "recipient", "transport", "wrapped"));
        var request = CustodyBindingFixture.Bind(new(envelope, WrapInput: input));
        var invalid = request with { Envelope = request.Envelope with { Challenge = "" } };
        byte[] bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(invalid,CustodyBindingContract.Json);
        var decoded = System.Text.Json.JsonSerializer.Deserialize<CustodyBindingRequestV1>(bytes,CustodyBindingContract.Json)!;
        byte[] owned = decoded.WrapInput!.RootKey;
        CustodyBindingContract.ValidateDecoded(decoded).ShouldBeNull();
        owned.ShouldAllBe(value => value == 0);
        input.RootKey.ShouldAllBe(value => value == 7);
        CustodyBindingContract.Decode(bytes).ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PinUnpinAndExactReadOnlyLookupRetainOriginalIdentityAndDigest(bool unpin)
    {
        var effect = CustodyBindingFixture.Lifecycle(unpin);
        var lookup = CustodyBindingFixture.Lifecycle(unpin, lookup: true);
        CustodyBindingContract.Validate(effect).ShouldBeTrue();
        CustodyBindingContract.Validate(lookup).ShouldBeTrue();
        lookup.Lifecycle.ShouldBe(effect.Lifecycle);
        lookup.Envelope.RequestDigest.ShouldBe(effect.Envelope.RequestDigest);
        CustodyBindingContract.Validate(lookup with { Lifecycle = null }).ShouldBeFalse();
        var changed = lookup with { Lifecycle = lookup.Lifecycle! with { HoldId = "other" } };
        changed = changed with { Envelope = changed.Envelope with { Proof = changed.Envelope.Proof with { HoldId = "other" } } };
        CustodyBindingContract.Validate(changed).ShouldBeFalse(); // original digest cannot follow a substitution
        var badAction = CustodyBindingFixture.Bind(effect with { Lifecycle = effect.Lifecycle! with { Action = unpin
            ? Hexalith.Platform.Custody.CustodyKeyLifecycleAction.Pin : Hexalith.Platform.Custody.CustodyKeyLifecycleAction.Unpin } });
        badAction = badAction with { Envelope = badAction.Envelope with { Proof = badAction.Envelope.Proof with { ReleaseGuardRevision = 3, ReleaseGuardReceiptId = "release" } } };
        CustodyBindingContract.Validate(badAction).ShouldBeFalse();
        if (unpin)
        {
            var missingRelease = effect with { Envelope = effect.Envelope with { Proof = effect.Envelope.Proof with { ReleaseGuardRevision = null, ReleaseGuardReceiptId = null } } };
            CustodyBindingContract.Validate(missingRelease).ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData("kek")]
    [InlineData("reference")]
    [InlineData("receipt")]
    [InlineData("registration-operation")]
    [InlineData("hold")]
    [InlineData("fence")]
    [InlineData("decision")]
    [InlineData("operation")]
    public void CustodyMetadataRetainsExistingUtf8ByteBoundsIndependently(string field)
    {
        var request = CustodyBindingFixture.Lifecycle();
        string large = new('\u0800', 2048);
        var registration = request.Registration!; var original = request.Lifecycle!; var e = request.Envelope;
        request = field switch
        {
            "kek" => request with { Registration = registration with { KekVersion = large } },
            "reference" => request with { Registration = registration with { OpaqueWrappedKeyReference = large } },
            "receipt" => request with { Registration = registration with { WrapReceiptId = large } },
            "registration-operation" => request with { Registration = registration with { OperationId = large } },
            "hold" => request with { Lifecycle = original with { HoldId = large }, Envelope = e with { Proof = e.Proof with { HoldId = large } } },
            "fence" => request with { Lifecycle = original with { FenceTokenId = large }, Envelope = e with { Proof = e.Proof with { GuardReceiptId = large } } },
            "decision" => request with { Lifecycle = original with { DecisionVersion = large }, Envelope = e with { Proof = e.Proof with { DecisionVersion = large } } },
            _ => request with { Lifecycle = original with { OperationId = large }, Envelope = e with { OperationId = large } },
        };
        CustodyBindingContract.Validate(CustodyBindingFixture.Bind(request)).ShouldBeFalse();
    }

    [Fact]
    public void ManifestRejectsReversedDispatchAndInvalidJwsUnicodeIndependently()
    {
        var request = CustodyBindingFixture.Manifest(); var m = request.Manifest!;
        var early = request with { Manifest = m with { Original = m.Original with { DispatchGuardRevision = 1 } },
            Envelope = request.Envelope with { Proof = request.Envelope.Proof with { DispatchRevision = 1 } } };
        CustodyBindingContract.Validate(CustodyBindingFixture.Bind(early)).ShouldBeFalse();
        var surrogate = request with { Manifest = m with { Original = m.Original with { DetachedJws = "\uD800" } } };
        CustodyBindingContract.Validate(CustodyBindingFixture.Bind(surrogate)).ShouldBeFalse();
        // The general signed canonical primitive remains UTF-16 bounded rather than custody-byte bounded.
        DeletionBatchCapabilityIdentity.CanonicalPayload(m.Original.Capability with { DestructionSealId = new string('\u0800', 2048) }).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("HoldId")]
    [InlineData("DispatchReceiptId")]
    [InlineData("ProtectionReservationReceiptId")]
    public void PrivateProofFieldsAreBoundedWithoutChangingOwnerIdentity(string field)
    {
        var request = CustodyBindingFixture.Manifest();
        var e = request.Envelope;
        var p = e.Proof;
        string oversized = new('\u0001', 2049);
        p = field switch
        {
            "HoldId" => p with { HoldId = oversized },
            "DispatchReceiptId" => p with { DispatchReceiptId = oversized },
            _ => p with { ProtectionReservationReceiptId = oversized },
        };
        var changed = request with { Envelope = e with { Proof = p } };
        changed.Envelope.RequestDigest.ShouldBe(request.Envelope.RequestDigest);
        CustodyBindingContract.Validate(changed).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => CustodyBindingContract.Encode(changed));
        var bounded = request with { Envelope = e with { Proof = e.Proof with { HoldId = new string('\u0001', 2048) } } };
        CustodyBindingContract.Validate(bounded).ShouldBeTrue();
        CustodyBindingContract.Decode(CustodyBindingContract.Encode(bounded)).ShouldNotBeNull();
        CustodyBindingContract.MaximumManifestRequestBound.ShouldBeLessThan(CustodyBindingContract.MaximumRequestBytes);
        CustodyBindingContract.MaximumTransitionRequestBound.ShouldBeLessThan(CustodyBindingContract.MaximumRequestBytes);
    }

    [Fact]
    public void ContradictoryWrapRegistrationEnvelopeIsRejected()
    {
        var e = CustodyBindingFixture.Envelope("wrap") with { OperationId = CustodyBindingFixture.Registration.OperationId };
        var request = CustodyBindingFixture.Bind(new(e, CustodyBindingFixture.Registration));
        CustodyBindingContract.Validate(request).ShouldBeTrue();
        CustodyBindingContract.Validate(request with { Envelope = e with { OperationId = "other", RequestDigest = request.Envelope.RequestDigest } }).ShouldBeFalse();
        var wrongRevision = request with { Envelope = request.Envelope with { ExpectedCustodyRevision = 2,
            Proof = request.Envelope.Proof with { ExpectedCustodyRevision = 2 } } };
        CustodyBindingContract.Validate(wrongRevision).ShouldBeFalse();
    }

    [Fact]
    public void ExistingCanonicalFramingAndC0OuterVectorsRemainExact()
    {
        var identity = CustodyBindingFixture.Identity;
        byte[] inner = CustodyBindingEnvelopeBytes.Inner(identity, "kek-v1", new byte[12], new byte[32], new byte[16]);
        string identityJson = "{\"ContractVersion\":\"contract\",\"KeyAlias\":\"alias\",\"KeyVersion\":\"v1\",\"LifecycleVersion\":\"lifecycle\",\"ObjectId\":\"object\",\"Purpose\":\"InteractionRootDek\",\"StoreTarget\":\"opaque-store\",\"TenantId\":\"tenant\"}";
        Encoding.UTF8.GetString(inner).ShouldBe("{\"Ciphertext\":\"" + new string('A', 43) + "\",\"Identity\":" + identityJson
            + ",\"KekVersion\":\"kek-v1\",\"Nonce\":\"AAAAAAAAAAAAAAAA\",\"Tag\":\"" + new string('A', 22) + "\"}");
        var outer = new OuterEnvelopeV1(identity, "kek-v1", "opaque-wrap", "outer1", new byte[12], [0], new byte[16]);
        string[] components = ["22:custody-epoch-outer-v1", "6:tenant", "6:object", "18:InteractionRootDek", "5:alias", "2:v1",
            "9:lifecycle", "12:opaque-store", "8:contract", "6:kek-v1", "11:opaque-wrap", "6:outer1"];
        byte[] golden = Encoding.UTF8.GetBytes(string.Concat(components.Select(c => "\u0001" + c + "\u001F")));
        CustodyBindingEnvelopeBytes.OuterAad(outer).ShouldBe(golden);
        Encoding.UTF8.GetString(CustodyBindingEnvelopeBytes.Outer(outer)).ShouldBe("{\"Ciphertext\":\"AA\",\"Identity\":" + identityJson
            + ",\"KekVersion\":\"kek-v1\",\"Nonce\":\"AAAAAAAAAAAAAAAA\",\"OpaqueWrappedKeyReference\":\"opaque-wrap\",\"OuterKeyGeneration\":\"outer1\",\"Tag\":\"" + new string('A', 22) + "\"}");
        Hexalith.Platform.Custody.PlatformCanonicalBytes.Components(["😀", null, ""]).ShouldBe(
            new byte[] { 1, 50, 58, 240, 159, 152, 128, 31, 0, 48, 58, 31, 1, 48, 58, 31 });
        var unicode = identity with { ObjectId = "€<>&/e\u0301\u000f\b\t\n\f\r\\\"😀\u2028" };
        string value = Encoding.UTF8.GetString(CustodyBindingEnvelopeBytes.Inner(unicode, "kek-v1", new byte[12], new byte[32], new byte[16]));
        value.ShouldContain("€<>&/e\u0301\\u000f\\b\\t\\n\\f\\r\\\\\\\"😀\u2028");
        value.ShouldNotContain("\\u20AC");
        Should.Throw<ArgumentException>(() => CustodyBindingEnvelopeBytes.Inner(unicode with { ObjectId = "\uD800" }, "kek-v1", new byte[12], new byte[32], new byte[16]));
    }

    [Fact]
    public void ExistingManifestDigestSignedJcsOrderAndOptionalBytesSurviveClosedTransport()
    {
        var request = CustodyBindingFixture.Manifest(2);
        var m = request.Manifest!;
        var unicode = m.Original.Capability with { DestructionSealId = "€<>&/é😀\u000f\n\\\"" };
        var changed = m with { Original = m.Original with { Capability = unicode, DetachedJws = new string('s', 16384) },
            SignedCanonicalPayload = DeletionBatchCapabilityIdentity.CanonicalPayload(unicode) };
        request = CustodyBindingFixture.Bind(request with { Manifest = changed });
        byte[] bytes = CustodyBindingContract.Encode(request);
        var restored = CustodyBindingContract.Decode(bytes).ShouldNotBeNull();
        restored.Manifest!.SignedCanonicalPayload.ShouldBe(DeletionBatchCapabilityIdentity.CanonicalPayload(unicode));
        restored.Manifest.Original.DetachedJws.ShouldBe(changed.Original.DetachedJws);
        restored.Manifest.Original.Targets.ShouldBe(changed.Original.Targets);
        restored.Manifest.Original.Capability.ManifestDigest.ShouldBe(DeletionBatchCapabilityIdentity.TargetManifestDigest(changed.Original.Targets));
        DeletionBatchCapabilityIdentity.TargetManifestDigest(changed.Original.Targets.Reverse().ToArray())
            .ShouldNotBe(changed.Original.Capability.ManifestDigest);
        request.Envelope.RequestDigest.ShouldBe(CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(changed.Original)));
        var retry = request with { Envelope = request.Envelope with { RequestAttemptId = "new-attempt", Challenge = "new-challenge" } };
        CustodyBindingContract.OriginalRequestDigest(retry).ShouldBe(request.Envelope.RequestDigest);
        CustodyBindingContract.Validate(retry).ShouldBeTrue();
        var substitution = request with { Manifest = changed with { Original = changed.Original with { Targets = changed.Original.Targets.Reverse().ToArray() } } };
        CustodyBindingContract.Validate(CustodyBindingFixture.Bind(substitution)).ShouldBeFalse();
        changed.SignedCanonicalPayload[0] ^= 1;
        CustodyBindingContract.Validate(request).ShouldBeFalse();
    }

    [Theory]
    [InlineData("unknown-root")]
    [InlineData("unknown-nested")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("wrong-operation")]
    [InlineData("wrong-target")]
    [InlineData("wrong-digest")]
    [InlineData("null")]
    public void ClosedRequestRejectsSubstitutionAndAmbiguity(string vector)
    {
        byte[] bytes = CustodyBindingContract.Encode(CustodyBindingFixture.Manifest());
        var json = JsonNode.Parse(bytes)!.AsObject();
        switch (vector)
        {
            case "unknown-root": json["ProviderEndpoint"] = "forbidden"; break;
            case "unknown-nested": json["Manifest"]!["Original"]!["Capability"]!["ExpiresAt"] = 1; break;
            case "missing": json["Envelope"]!.AsObject().Remove("OperationId"); break;
            case "wrong-operation": json["Envelope"]!["Operation"] = "key-delete"; break;
            case "wrong-target": json["Manifest"]!["Original"]!["Targets"]![0]!["AgentInteractionId"] = "other"; break;
            case "wrong-digest": json["Envelope"]!["RequestDigest"] = new string('B', 64); break;
            case "null": json["Manifest"] = null; break;
        }
        byte[] malformed = vector == "duplicate" ? Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\"ProtocolVersion\":",
            "\"ProtocolVersion\":\"ignored\",\"ProtocolVersion\":", StringComparison.Ordinal)) : Encoding.UTF8.GetBytes(json.ToJsonString());
        CustodyBindingContract.Decode(malformed).ShouldBeNull();
    }

    [Fact]
    public void NativeIntentsAndEphemeralDeliveryRetainOriginalAcrossUncertaintyAndReinit()
    {
        var i = new NativeIntentV1("tenant", "operation", "intent", CustodyBindingFixture.Digest, "producer-1", "source", "destination",
            CustodyBindingFixture.Digest, 1, CustodyBindingIntentPhase.Prepared);
        var sent = i with { Phase = CustodyBindingIntentPhase.SendCommitted };
        CustodyBindingIntentRules.CanAdvance(i, sent, false, false, false, false).ShouldBeTrue();
        CustodyBindingIntentRules.DuplicateIsLookupOnly(sent).ShouldBeTrue();
        CustodyBindingIntentRules.CanAdvance(sent, i, false, false, false, false).ShouldBeFalse();
        var durable = sent with { Phase = CustodyBindingIntentPhase.ResultDurable };
        CustodyBindingIntentRules.CanAdvance(sent, durable, false, true, true, true).ShouldBeFalse();
        CustodyBindingIntentRules.CanAdvance(sent, durable, true, false, false, false).ShouldBeTrue();
        var closed = durable with { Phase = CustodyBindingIntentPhase.Closed };
        foreach (var completion in new[] { (false, true, true), (true, false, true), (true, true, false) })
        { CustodyBindingIntentRules.CanAdvance(durable, closed, true, completion.Item1, completion.Item2, completion.Item3).ShouldBeFalse(); }
        CustodyBindingIntentRules.CanAdvance(durable, closed, true, true, true, true).ShouldBeTrue();
        CustodyBindingIntentRules.CanAdvance(sent, durable with { ProducerIncarnation = "new-producer" }, true, true, true, true).ShouldBeFalse();
        CustodyBindingIntentRules.CanAdvance(sent, durable with { IntentId = "replacement" }, true, true, true, true).ShouldBeFalse();
        CustodyBindingIntentRules.SameEnrollment(CustodyBindingFixture.Enrollment, CustodyBindingFixture.Enrollment).ShouldBeTrue();
        foreach (var changed in new[] { CustodyBindingFixture.Enrollment with { AuthorityIncarnation = "new" },
            CustodyBindingFixture.Enrollment with { StoreTarget = "new" }, CustodyBindingFixture.Enrollment with { TrustProfileRevision = "new" },
            CustodyBindingFixture.Enrollment with { ArtifactProfileDigest = new string('B', 64) } })
        { CustodyBindingIntentRules.SameEnrollment(CustodyBindingFixture.Enrollment, changed).ShouldBeFalse(); }
        // Same enrollment leaves the retained SendCommitted intent unchanged; no reset/replacement primitive exists.
        sent.Phase.ShouldBe(CustodyBindingIntentPhase.SendCommitted);
        var use = new CustodyBindingKeyUseV1("lease", "delivery", "recipient", "transport", "original-root");
        var delivery = new CustodyBindingDeliveryV1(use, CustodyBindingIntentPhase.Prepared, false, false, false, false);
        CustodyBindingIntentRules.CanRelease(delivery).ShouldBeTrue();
        CustodyBindingIntentRules.CanRelease(delivery with { FrozenOrErased = true }).ShouldBeFalse();
        foreach (var phase in new[] { CustodyBindingIntentPhase.SendCommitted, CustodyBindingIntentPhase.ResultDurable, CustodyBindingIntentPhase.Closed })
        { CustodyBindingIntentRules.CanRelease(delivery with { Phase = phase }).ShouldBeFalse(); }
        CustodyBindingIntentRules.CanClose(delivery with { Phase = CustodyBindingIntentPhase.ResultDurable, IoComplete = true, BuffersComplete = true }).ShouldBeFalse();
        CustodyBindingIntentRules.CanClose(delivery with { Phase = CustodyBindingIntentPhase.ResultDurable, IoComplete = true, BuffersComplete = true,
            HolderInventoryComplete = true }).ShouldBeTrue();
    }
}
