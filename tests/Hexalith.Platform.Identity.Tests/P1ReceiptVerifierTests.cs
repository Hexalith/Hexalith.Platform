using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Hexalith.Platform.Identity;
using Shouldly;

namespace Hexalith.Platform.Identity.Tests;

/// <summary>Ephemeral fixture keys exercise the offline protocol; they are not operationally enrolled.</summary>
public sealed class P1ReceiptVerifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly string Nonce = new('e', 64);

    [Theory]
    [InlineData("producer-execution")]
    [InlineData("grant")]
    [InlineData("target")]
    [InlineData("custody")]
    public void TestOnlyKeys_VerifyAllFourReceiptKinds(string kind)
    {
        using var keys = new Fixture(kind);
        keys.Verify().ShouldBeTrue();
    }

    [Fact]
    public void ForgedSignature_Refuses()
    {
        using var keys = new Fixture();
        using ECDsa foreign = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        P1SignedDocument forged = Sign(keys.Receipt, foreign);
        keys.Verify(receipt: forged).ShouldBeFalse();
    }

    [Fact]
    public void ChangedSignedBytes_Refuse()
    {
        using var keys = new Fixture();
        P1SignedDocument changed = keys.Receipt with { Payload = keys.Receipt.Payload.ToArray() };
        changed.Payload[^1] ^= 1;
        keys.Verify(receipt: changed).ShouldBeFalse();
    }

    [Fact]
    public void ValidSignatureOnWrongScope_Refuses()
    {
        using var keys = new Fixture();
        P1ReceiptClaims wrong = keys.Claims with { TargetSha256 = new string('b', 64) };
        keys.Verify(receipt: Sign(wrong, keys.ReceiptKey)).ShouldBeFalse();
        keys.Verify(receipt: Sign(keys.Claims with { Issuer = "https://other.test.invalid" }, keys.ReceiptKey)).ShouldBeFalse();
        keys.Verify(receipt: Sign(keys.Claims with { Reasons = "changed reason" }, keys.ReceiptKey)).ShouldBeFalse();
    }

    [Fact]
    public void SignedRevocation_RefusesWithoutGrace()
    {
        using var keys = new Fixture();
        P1StatusClaims revoked = keys.StatusClaims with { ReceiptState = "revoked" };
        keys.Verify(status: Sign(revoked, keys.StatusKey)).ShouldBeFalse();
        keys.Verify(status: Sign(keys.StatusClaims with { KeyState = "revoked" }, keys.StatusKey)).ShouldBeFalse();
    }

    [Fact]
    public void ForgedStatusAndChangedSubject_Refuse()
    {
        using var keys = new Fixture();
        using ECDsa foreign = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        keys.Verify(status: Sign(keys.Status, foreign)).ShouldBeFalse();
        string uri = keys.Enrollment.RetrievalOrigin + "/v1/receipts/" + keys.Claims.ReceiptId;
        string statusUri = keys.Enrollment.StatusOrigin + "/v1/status/" + keys.Claims.ReceiptId + "?nonce=" + Nonce;
        P1ReceiptVerifier.Verify(keys.Receipt, uri, keys.Status, statusUri, keys.Claims,
            System.Text.Encoding.UTF8.GetBytes("different subject"), keys.AuthenticatedEnrollment, Nonce, Now).ShouldBeFalse();
    }

    [Fact]
    public void UnavailableReceiptOrStatus_Refuses()
    {
        using var keys = new Fixture();
        string uri = keys.Enrollment.RetrievalOrigin + "/v1/receipts/" + keys.Claims.ReceiptId;
        string statusUri = keys.Enrollment.StatusOrigin + "/v1/status/" + keys.Claims.ReceiptId + "?nonce=" + Nonce;
        P1ReceiptVerifier.Verify(null, uri, keys.Status, statusUri, keys.Claims, keys.Subject, keys.AuthenticatedEnrollment, Nonce, Now).ShouldBeFalse();
        P1ReceiptVerifier.Verify(keys.Receipt, uri, null, statusUri, keys.Claims, keys.Subject, keys.AuthenticatedEnrollment, Nonce, Now).ShouldBeFalse();
        P1ReceiptVerifier.Verify(keys.Receipt, uri, keys.Status, statusUri, keys.Claims, keys.Subject, null, Nonce, Now).ShouldBeFalse();
    }

    [Fact]
    public void ReplayedNonceAndExactExpiry_Refuse()
    {
        using var keys = new Fixture();
        keys.Verify(nonce: new string('f', 64)).ShouldBeFalse();
        keys.Verify(now: keys.StatusClaims.ExpiresAtUtc).ShouldBeFalse();
    }

    [Fact]
    public void Bootstrap_MissingWrongRootAndChangedBytes_Refuse()
    {
        using var keys = new Fixture();
        using ECDsa foreign = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        P1BootstrapVerifier.TryVerify(null, keys.Pin, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap, null, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap, keys.Pin with
        {
            RootPublicKeySpki = foreign.ExportSubjectPublicKeyInfo(),
            RootKeyFingerprint = Fingerprint(foreign.ExportSubjectPublicKeyInfo()),
        }, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(Sign(keys.Bootstrap, foreign), keys.Pin, Now, out _).ShouldBeFalse();
        byte[] changed = keys.Bootstrap.Payload.ToArray();
        changed[^1] ^= 1;
        P1BootstrapVerifier.TryVerify(keys.Bootstrap with { Payload = changed }, keys.Pin, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap, keys.Pin with { RootPublicKeySpki = null! }, Now, out _).ShouldBeFalse();
    }

    [Fact]
    public void Bootstrap_SignedSubstitutionAndExactScope_Refuse()
    {
        using var keys = new Fixture();
        P1BootstrapClaims substituted = keys.BootstrapClaims with { Issuer = "https://other.test.invalid" };
        P1SignedDocument changed = Sign(substituted, keys.RootKey);
        P1BootstrapPin pin = keys.Pin with { BootstrapSha256 = Fingerprint(changed.Payload) };
        P1BootstrapVerifier.TryVerify(changed, pin, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap, keys.Pin with { Revision = "other-r1" }, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap, keys.Pin with { Audience = "other-audience" }, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap, keys.Pin with { PrincipalMappingRevision = "other-map" }, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap, keys.Pin with { RetrievalOrigin = "https://other.test.invalid" }, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap, keys.Pin with { StatusOrigin = "https://other.test.invalid" }, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap, keys.Pin with { ReceiptKeyFingerprint = new string('f', 64) }, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap, keys.Pin with { StatusKeyFingerprint = new string('f', 64) }, Now, out _).ShouldBeFalse();
        byte[] noncanonical = [.. keys.Bootstrap.Payload, 0];
        P1SignedDocument appended = Sign(noncanonical, keys.RootKey);
        P1BootstrapVerifier.TryVerify(appended, keys.Pin with { BootstrapSha256 = Fingerprint(noncanonical) }, Now, out _).ShouldBeFalse();
        P1BootstrapClaims denied = keys.BootstrapClaims with { Issuer = "NO_SUPPORTED_RECEIPT_ISSUER" };
        P1SignedDocument signedDenial = Sign(denied, keys.RootKey);
        P1BootstrapVerifier.TryVerify(signedDenial, keys.Pin with
        {
            Issuer = denied.Issuer,
            BootstrapSha256 = Fingerprint(signedDenial.Payload),
        }, Now, out _).ShouldBeFalse();
    }

    [Fact]
    public void Bootstrap_ExpiredOrInvalidKeys_Refuse()
    {
        using var keys = new Fixture();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap, keys.Pin, keys.BootstrapClaims.ExpiresAtUtc, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap, keys.Pin, keys.BootstrapClaims.EffectiveAtUtc.AddTicks(-1), out _).ShouldBeFalse();
        P1BootstrapClaims sameKey = keys.BootstrapClaims with
        {
            StatusPublicKeySpki = keys.BootstrapClaims.ReceiptPublicKeySpki,
            StatusKeyFingerprint = keys.BootstrapClaims.ReceiptKeyFingerprint,
        };
        P1SignedDocument changed = Sign(sameKey, keys.RootKey);
        P1BootstrapPin pin = keys.Pin with
        {
            BootstrapSha256 = Fingerprint(changed.Payload),
            StatusKeyFingerprint = sameKey.StatusKeyFingerprint,
        };
        P1BootstrapVerifier.TryVerify(changed, pin, Now, out _).ShouldBeFalse();
        byte[] rootSpki = keys.RootKey.ExportSubjectPublicKeyInfo();
        P1BootstrapClaims rootReceipt = keys.BootstrapClaims with
        {
            ReceiptPublicKeySpki = rootSpki,
            ReceiptKeyFingerprint = Fingerprint(rootSpki),
        };
        P1SignedDocument signedRootReceipt = Sign(rootReceipt, keys.RootKey);
        P1BootstrapVerifier.TryVerify(signedRootReceipt, keys.Pin with
        {
            ReceiptKeyFingerprint = rootReceipt.ReceiptKeyFingerprint,
            BootstrapSha256 = Fingerprint(signedRootReceipt.Payload),
        }, Now, out _).ShouldBeFalse();
        P1BootstrapClaims rootStatus = keys.BootstrapClaims with
        {
            StatusPublicKeySpki = rootSpki,
            StatusKeyFingerprint = Fingerprint(rootSpki),
        };
        P1SignedDocument signedRootStatus = Sign(rootStatus, keys.RootKey);
        P1BootstrapVerifier.TryVerify(signedRootStatus, keys.Pin with
        {
            StatusKeyFingerprint = rootStatus.StatusKeyFingerprint,
            BootstrapSha256 = Fingerprint(signedRootStatus.Payload),
        }, Now, out _).ShouldBeFalse();
    }

    [Fact]
    public void Bootstrap_IndependentCanonicalWireVector_Verifies()
    {
        using var keys = new Fixture();
        string[] fields = [
            "fixture-r1", "https://p1.test.invalid", "hexalith:memories:c1:v1",
            "https://p1.test.invalid", "https://p1.test.invalid", "fixture-map-r1",
            Convert.ToHexStringLower(keys.ReceiptKey.ExportSubjectPublicKeyInfo()),
            keys.BootstrapClaims.ReceiptKeyFingerprint,
            Convert.ToHexStringLower(keys.StatusKey.ExportSubjectPublicKeyInfo()),
            keys.BootstrapClaims.StatusKeyFingerprint,
            "2026-10-09T11:50:00.000Z", "2026-10-10T12:00:00.000Z",
        ];
        byte[] wire = AssembleBootstrapWire(fields);
        P1BootstrapWireV1.Encode(keys.BootstrapClaims).AsSpan().SequenceEqual(wire).ShouldBeTrue();
        P1BootstrapWireV1.TryDecode(wire, out P1BootstrapClaims? decoded).ShouldBeTrue();
        decoded.ShouldNotBeNull();
        decoded.Revision.ShouldBe("fixture-r1");
        P1BootstrapVerifier.TryVerify(Sign(wire, keys.RootKey),
            keys.Pin with { BootstrapSha256 = Fingerprint(wire) }, Now, out P1AuthenticatedEnrollment? verified).ShouldBeTrue();
        verified.ShouldNotBeNull();
    }

    [Fact]
    public void Bootstrap_MalformedWireKeyAndSignature_Refuse()
    {
        using var keys = new Fixture();
        byte[] malformedLength = keys.Bootstrap.Payload.ToArray();
        BinaryPrimitives.WriteInt32BigEndian(malformedLength.AsSpan(9, 4), int.MaxValue);
        P1BootstrapWireV1.TryDecode(malformedLength, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(Sign(malformedLength, keys.RootKey),
            keys.Pin with { BootstrapSha256 = Fingerprint(malformedLength) }, Now, out _).ShouldBeFalse();
        byte[] malformedUtf8 = keys.Bootstrap.Payload.ToArray();
        malformedUtf8[13] = 0xff;
        P1BootstrapWireV1.TryDecode(malformedUtf8, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(Sign(malformedUtf8, keys.RootKey),
            keys.Pin with { BootstrapSha256 = Fingerprint(malformedUtf8) }, Now, out _).ShouldBeFalse();
        byte[] invalidSpki = [0x30, 0x00];
        P1BootstrapClaims invalidKey = keys.BootstrapClaims with
        {
            ReceiptPublicKeySpki = invalidSpki,
            ReceiptKeyFingerprint = Fingerprint(invalidSpki),
        };
        P1SignedDocument signedInvalidKey = Sign(invalidKey, keys.RootKey);
        P1BootstrapVerifier.TryVerify(signedInvalidKey, keys.Pin with
        {
            ReceiptKeyFingerprint = invalidKey.ReceiptKeyFingerprint,
            BootstrapSha256 = Fingerprint(signedInvalidKey.Payload),
        }, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap, keys.Pin with
        {
            RootPublicKeySpki = invalidSpki,
            RootKeyFingerprint = Fingerprint(invalidSpki),
        }, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap with { Signature = new byte[63] }, keys.Pin, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap with { Signature = new byte[65] }, keys.Pin, Now, out _).ShouldBeFalse();
        P1BootstrapVerifier.TryVerify(keys.Bootstrap with { Payload = new byte[1024 * 1024 + 1] },
            keys.Pin, Now, out _).ShouldBeFalse();
    }

    [Fact]
    public void BootstrapWire_UnrepresentableTimeAndOversizeField_Refuse()
    {
        using var keys = new Fixture();
        Should.Throw<ArgumentException>(() => P1BootstrapWireV1.Encode(keys.BootstrapClaims with
        {
            EffectiveAtUtc = keys.BootstrapClaims.EffectiveAtUtc.AddTicks(1),
        }));
        Should.Throw<ArgumentException>(() => P1BootstrapWireV1.Encode(keys.BootstrapClaims with
        {
            Revision = new string('x', 1024 * 1024),
        }));
    }

    [Fact]
    public void AuthenticatedEnrollment_CopiesVerifiedKeyBytes()
    {
        using var keys = new Fixture();
        keys.BootstrapClaims.ReceiptPublicKeySpki[0] ^= 1;
        keys.BootstrapClaims.StatusPublicKeySpki[0] ^= 1;
        keys.Pin.RootPublicKeySpki[0] ^= 1;
        keys.Verify().ShouldBeTrue();
    }

    [Fact]
    public void MalformedReceiptStatusAndRevocation_Refuse()
    {
        using var keys = new Fixture();
        keys.Verify(receipt: new P1SignedDocument(null!, keys.Receipt.Signature)).ShouldBeFalse();
        keys.Verify(status: new P1SignedDocument(keys.Status.Payload, null!)).ShouldBeFalse();
        keys.Verify(status: Sign(keys.StatusClaims with { GrantState = "unknown" }, keys.StatusKey)).ShouldBeFalse();
        keys.Verify(status: Sign(keys.StatusClaims with { PolicyState = "revoked" }, keys.StatusKey)).ShouldBeFalse();
        keys.Verify(status: Sign(keys.StatusClaims with { SessionState = "unavailable" }, keys.StatusKey)).ShouldBeFalse();
        keys.Verify(status: Sign(keys.StatusClaims with { ObservedAtUtc = Now.AddMinutes(-6) }, keys.StatusKey)).ShouldBeFalse();
        keys.Verify(receipt: Sign(keys.Claims with { Decision = "approved" }, keys.ReceiptKey)).ShouldBeFalse();
        keys.Verify(receipt: Sign(keys.Claims with { SubjectLength = keys.Subject.LongLength + 1 }, keys.ReceiptKey)).ShouldBeFalse();
        P1StatusClaims atReceiptExpiry = keys.StatusClaims with
        {
            ObservedAtUtc = keys.Claims.ExpiresAtUtc.AddSeconds(-1),
            ExpiresAtUtc = keys.Claims.ExpiresAtUtc.AddSeconds(30),
        };
        keys.Verify(status: Sign(atReceiptExpiry, keys.StatusKey), now: keys.Claims.ExpiresAtUtc).ShouldBeFalse();
    }

    [Fact]
    public void Receipt_ExpectedTimeMustMatchExactRepresentableUtcInstant()
    {
        using var keys = new Fixture();
        keys.Verify(expected: keys.Claims with { IssuedAtUtc = keys.Claims.IssuedAtUtc.AddTicks(1) }).ShouldBeFalse();
        keys.Verify(expected: keys.Claims with { ExpiresAtUtc = keys.Claims.ExpiresAtUtc.AddTicks(1) }).ShouldBeFalse();
        keys.Verify(expected: keys.Claims with { IssuedAtUtc = keys.Claims.IssuedAtUtc.ToOffset(TimeSpan.FromHours(1)) }).ShouldBeFalse();
    }

    [Fact]
    public void ReceiptWire_UnrepresentableTimeAndOversizeField_Refuse()
    {
        using var keys = new Fixture();
        Should.Throw<ArgumentException>(() => P1ReceiptWireV1.Encode(keys.Claims with
        {
            IssuedAtUtc = keys.Claims.IssuedAtUtc.AddTicks(1),
        }));
        Should.Throw<ArgumentException>(() => P1ReceiptWireV1.Encode(keys.StatusClaims with
        {
            ObservedAtUtc = keys.StatusClaims.ObservedAtUtc.ToOffset(TimeSpan.FromHours(1)),
        }));
        P1ReceiptClaims oversize = keys.Claims with { Reasons = new string('é', 600_000) };
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Should.Throw<ArgumentException>(() => P1ReceiptWireV1.Encode(oversize));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        allocated.ShouldBeLessThan(256L * 1024);
    }

    [Fact]
    public void SignedEnvelope_ExactFramingAndTransportLimit()
    {
        byte[] signature = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        byte[] independent = [0, 0, 0, 3, 0xa1, 0xb2, 0xc3, .. signature];
        P1SignedEnvelopeV1.TryDecode(independent, out P1SignedDocument? decoded).ShouldBeTrue();
        decoded.ShouldNotBeNull();
        decoded.Payload.ShouldBe(new byte[] { 0xa1, 0xb2, 0xc3 });
        decoded.Signature.ShouldBe(signature);
        P1SignedEnvelopeV1.Encode(decoded).ShouldBe(independent);

        byte[] maxPayload = new byte[1_048_508];
        byte[] maxBody = P1SignedEnvelopeV1.Encode(new(maxPayload, signature));
        maxBody.Length.ShouldBe(1_048_576);
        P1SignedEnvelopeV1.TryDecode(maxBody, out P1SignedDocument? maxDecoded).ShouldBeTrue();
        maxDecoded.ShouldNotBeNull();
        maxDecoded.Payload.Length.ShouldBe(maxPayload.Length);
        Should.Throw<ArgumentException>(() => P1SignedEnvelopeV1.Encode(new(new byte[maxPayload.Length + 1], signature)));
        P1SignedEnvelopeV1.TryDecode(new byte[1_048_577], out P1SignedDocument? oversized).ShouldBeFalse();
        oversized.ShouldBeNull();
    }

    [Fact]
    public void SignedEnvelope_TruncationTrailingAndMalformedLengths_Refuse()
    {
        byte[] valid = P1SignedEnvelopeV1.Encode(new([1, 2, 3], new byte[64]));
        P1SignedEnvelopeV1.TryDecode(valid[..^1], out _).ShouldBeFalse();
        P1SignedEnvelopeV1.TryDecode([.. valid, 0], out _).ShouldBeFalse();
        P1SignedEnvelopeV1.TryDecode(valid[..3], out _).ShouldBeFalse();
        byte[] zeroLength = valid.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(zeroLength, 0);
        P1SignedEnvelopeV1.TryDecode(zeroLength, out _).ShouldBeFalse();
        byte[] tooLong = valid.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(tooLong, (uint)P1SignedEnvelopeV1.MaxTransportPayloadLength + 1);
        P1SignedEnvelopeV1.TryDecode(tooLong, out _).ShouldBeFalse();
        byte[] highBitLength = valid.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(highBitLength, uint.MaxValue);
        P1SignedEnvelopeV1.TryDecode(highBitLength, out _).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => P1SignedEnvelopeV1.Encode(new([1], new byte[63])));
    }

    [Fact]
    public void Transport_ExactTypesAndSignedFixtureComposeWithEvidence()
    {
        using var keys = new Fixture();
        byte[] receiptBody = P1SignedEnvelopeV1.Encode(keys.Receipt);
        byte[] statusBody = P1SignedEnvelopeV1.Encode(keys.Status);
        P1ReceiptTransportV1.TryDecodeReceipt("application/vnd.hexalith.p1-receipt.v1", receiptBody, out P1SignedDocument? receipt).ShouldBeTrue();
        P1ReceiptTransportV1.TryDecodeStatus("application/vnd.hexalith.p1-status.v1", statusBody, out P1SignedDocument? status).ShouldBeTrue();
        receipt.ShouldNotBeNull();
        status.ShouldNotBeNull();
        string retrievalUri = keys.Enrollment.RetrievalOrigin + "/v1/receipts/" + keys.Claims.ReceiptId;
        string statusUri = keys.Enrollment.StatusOrigin + "/v1/status/" + keys.Claims.ReceiptId + "?nonce=" + Nonce;
        P1ReceiptVerifier.TryVerify(receipt, retrievalUri, status, statusUri, keys.Claims, keys.Subject,
            keys.AuthenticatedEnrollment, Nonce, Now, out P1VerifiedReceiptEvidence? evidence).ShouldBeTrue();
        evidence.ShouldNotBeNull();
        keys.Verify().ShouldBeTrue();
        evidence.ReceiptPayloadSha256.ShouldBe(Fingerprint(keys.Receipt.Payload));
        evidence.ReceiptSignatureSha256.ShouldBe(Fingerprint(keys.Receipt.Signature));
        evidence.StatusPayloadSha256.ShouldBe(Fingerprint(keys.Status.Payload));
        evidence.StatusSignatureSha256.ShouldBe(Fingerprint(keys.Status.Signature));
        evidence.SubjectSha256.ShouldBe(Fingerprint(keys.Subject));
        evidence.SubjectLength.ShouldBe(keys.Subject.LongLength);
        evidence.BootstrapSha256.ShouldBe(Fingerprint(keys.Bootstrap.Payload));
        evidence.RetrievalUri.ShouldBe(retrievalUri);
        evidence.StatusUri.ShouldBe(statusUri);
        evidence.RequestNonce.ShouldBe(Nonce);

        receiptBody[4] ^= 1;
        statusBody[4] ^= 1;
        receipt.Payload.ShouldBe(keys.Receipt.Payload);
        status.Payload.ShouldBe(keys.Status.Payload);
        receipt.Payload[0] ^= 1;
        status.Signature[0] ^= 1;
        keys.Subject[0] ^= 1;
        evidence.ReceiptPayloadSha256.ShouldBe(Fingerprint(keys.Receipt.Payload));
        evidence.StatusSignatureSha256.ShouldBe(Fingerprint(keys.Status.Signature));
        evidence.SubjectSha256.ShouldNotBe(Fingerprint(keys.Subject));
        P1ReceiptVerifier.TryVerify(receipt, retrievalUri, status, statusUri, keys.Claims, keys.Subject,
            keys.AuthenticatedEnrollment, Nonce, Now, out P1VerifiedReceiptEvidence? changedEvidence).ShouldBeFalse();
        changedEvidence.ShouldBeNull();
    }

    [Fact]
    public void Transport_WrongTypeMalformedPayloadAndChangedSignature_Refuse()
    {
        using var keys = new Fixture();
        byte[] receiptBody = P1SignedEnvelopeV1.Encode(keys.Receipt);
        byte[] statusBody = P1SignedEnvelopeV1.Encode(keys.Status);
        P1ReceiptTransportV1.TryDecodeReceipt(null, receiptBody, out _).ShouldBeFalse();
        P1ReceiptTransportV1.TryDecodeReceipt("application/vnd.hexalith.p1-receipt.v2", receiptBody, out P1SignedDocument? wrongVersion).ShouldBeFalse();
        wrongVersion.ShouldBeNull();
        P1ReceiptTransportV1.TryDecodeReceipt(P1ReceiptTransportV1.StatusMediaType, receiptBody, out P1SignedDocument? wrongType).ShouldBeFalse();
        wrongType.ShouldBeNull();
        P1ReceiptTransportV1.TryDecodeReceipt(P1ReceiptTransportV1.ReceiptMediaType + "; charset=utf-8", receiptBody, out _).ShouldBeFalse();
        P1ReceiptTransportV1.TryDecodeReceipt(P1ReceiptTransportV1.ReceiptMediaType.ToUpperInvariant(), receiptBody, out _).ShouldBeFalse();
        P1ReceiptTransportV1.TryDecodeStatus(P1ReceiptTransportV1.ReceiptMediaType, statusBody, out _).ShouldBeFalse();
        P1ReceiptTransportV1.TryDecodeStatus(P1ReceiptTransportV1.StatusMediaType, receiptBody, out _).ShouldBeFalse();
        byte[] malformedReceipt = receiptBody.ToArray();
        malformedReceipt[4] ^= 1;
        P1ReceiptTransportV1.TryDecodeReceipt(P1ReceiptTransportV1.ReceiptMediaType, malformedReceipt, out P1SignedDocument? malformed).ShouldBeFalse();
        malformed.ShouldBeNull();
        P1ReceiptTransportV1.TryDecodeReceipt(P1ReceiptTransportV1.ReceiptMediaType, receiptBody[..^1], out P1SignedDocument? truncated).ShouldBeFalse();
        truncated.ShouldBeNull();
        P1ReceiptTransportV1.TryDecodeStatus(P1ReceiptTransportV1.StatusMediaType, [.. statusBody, 0], out _).ShouldBeFalse();

        byte[] changedSignature = receiptBody.ToArray();
        changedSignature[^1] ^= 1;
        P1ReceiptTransportV1.TryDecodeReceipt(P1ReceiptTransportV1.ReceiptMediaType, changedSignature, out P1SignedDocument? decoded).ShouldBeTrue();
        decoded.ShouldNotBeNull();
        string retrievalUri = keys.Enrollment.RetrievalOrigin + "/v1/receipts/" + keys.Claims.ReceiptId;
        string statusUri = keys.Enrollment.StatusOrigin + "/v1/status/" + keys.Claims.ReceiptId + "?nonce=" + Nonce;
        P1ReceiptVerifier.TryVerify(decoded, retrievalUri, keys.Status, statusUri, keys.Claims, keys.Subject,
            keys.AuthenticatedEnrollment, Nonce, Now, out P1VerifiedReceiptEvidence? evidence).ShouldBeFalse();
        evidence.ShouldBeNull();
    }

    private static P1SignedDocument Sign(P1ReceiptClaims claims, ECDsa key) => Sign(P1ReceiptWireV1.Encode(claims), key);
    private static P1SignedDocument Sign(P1StatusClaims claims, ECDsa key) => Sign(P1ReceiptWireV1.Encode(claims), key);
    private static P1SignedDocument Sign(P1BootstrapClaims claims, ECDsa key) => Sign(P1BootstrapWireV1.Encode(claims), key);
    private static P1SignedDocument Sign(P1SignedDocument document, ECDsa key) => Sign(document.Payload, key);
    private static P1SignedDocument Sign(byte[] payload, ECDsa key)
        => new(payload, key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    private static string Fingerprint(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static byte[] AssembleBootstrapWire(string[] fields)
    {
        using var stream = new MemoryStream();
        stream.Write("HX-P1B/1\n"u8);
        Span<byte> length = stackalloc byte[4];
        foreach (string field in fields)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(field);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            stream.Write(length);
            stream.Write(bytes);
        }

        return stream.ToArray();
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(string kind = "producer-execution")
        {
            byte[] receiptSpki = ReceiptKey.ExportSubjectPublicKeyInfo();
            byte[] statusSpki = StatusKey.ExportSubjectPublicKeyInfo();
            string receiptFingerprint = Fingerprint(receiptSpki);
            string statusFingerprint = Fingerprint(statusSpki);
            BootstrapClaims = new("fixture-r1", "https://p1.test.invalid", "hexalith:memories:c1:v1",
                "https://p1.test.invalid", "https://p1.test.invalid", "fixture-map-r1", receiptSpki,
                receiptFingerprint, statusSpki, statusFingerprint, Now.AddMinutes(-10), Now.AddDays(1));
            Bootstrap = Sign(BootstrapClaims, RootKey);
            byte[] rootSpki = RootKey.ExportSubjectPublicKeyInfo();
            Pin = new(rootSpki, Fingerprint(rootSpki), Fingerprint(Bootstrap.Payload), BootstrapClaims.Revision,
                BootstrapClaims.Issuer, BootstrapClaims.Audience, BootstrapClaims.RetrievalOrigin,
                BootstrapClaims.StatusOrigin, receiptFingerprint, statusFingerprint, BootstrapClaims.PrincipalMappingRevision);
            if (!P1BootstrapVerifier.TryVerify(Bootstrap, Pin, Now, out P1AuthenticatedEnrollment? authenticated))
            {
                throw new InvalidOperationException("Fixture bootstrap was not verified.");
            }
            AuthenticatedEnrollment = authenticated!;
            Enrollment = new(BootstrapClaims.Issuer, BootstrapClaims.RetrievalOrigin, BootstrapClaims.StatusOrigin,
                BootstrapClaims.Audience, BootstrapClaims.Revision, receiptSpki, receiptFingerprint, statusSpki,
                statusFingerprint, BootstrapClaims.EffectiveAtUtc, BootstrapClaims.ExpiresAtUtc);
            string subjectDigest = Convert.ToHexStringLower(SHA256.HashData(Subject));
            string action = kind switch { "grant" => "grant-session", "target" => "observe-target", "custody" => "retain", _ => "capture" };
            Claims = new(new string('a', 64), Enrollment.Issuer, receiptFingerprint, Enrollment.Audience, kind, action,
                "platform:actor:01HX0000000000000000000001", "sha256:" + subjectDigest, subjectDigest, Subject.LongLength,
                new string('d', 64), new string('d', 64), "tenant-a", "cluster-a", "namespace-uid-a",
                new string('a', 64), "session-a", "source-r1", "registry-r1", "policy-r1", "grant-a",
                "observed", "", Now.AddMinutes(-1), Now.AddHours(1));
            StatusClaims = new(Enrollment.Issuer, Enrollment.TrustRevision, Claims.ReceiptId, Claims.KeyFingerprint,
                Claims.GrantId, Claims.SessionId, Claims.PolicyRevision, "active", "active", "active", "active", "active",
                Nonce, Now.AddSeconds(-1), Now.AddSeconds(30));
            Receipt = Sign(Claims, ReceiptKey);
            Status = Sign(StatusClaims, StatusKey);
        }

        public ECDsa ReceiptKey { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public ECDsa StatusKey { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public ECDsa RootKey { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public P1BootstrapClaims BootstrapClaims { get; }
        public P1SignedDocument Bootstrap { get; }
        public P1BootstrapPin Pin { get; }
        public P1AuthenticatedEnrollment AuthenticatedEnrollment { get; }
        public P1ReceiptEnrollment Enrollment { get; }
        public P1ReceiptClaims Claims { get; }
        public P1StatusClaims StatusClaims { get; }
        public P1SignedDocument Receipt { get; }
        public P1SignedDocument Status { get; }
        public byte[] Subject { get; } = System.Text.Encoding.UTF8.GetBytes("offline subject bytes");

        public bool Verify(P1SignedDocument? receipt = default, P1SignedDocument? status = default,
            string? nonce = default, DateTimeOffset? now = default, P1ReceiptClaims? expected = default)
            => P1ReceiptVerifier.Verify(receipt ?? Receipt, Enrollment.RetrievalOrigin + "/v1/receipts/" + Claims.ReceiptId,
                status ?? Status, Enrollment.StatusOrigin + "/v1/status/" + Claims.ReceiptId + "?nonce=" + (nonce ?? Nonce),
                expected ?? Claims, Subject, AuthenticatedEnrollment, nonce ?? Nonce, now ?? Now);

        public void Dispose()
        {
            ReceiptKey.Dispose();
            StatusKey.Dispose();
            RootKey.Dispose();
        }
    }
}
