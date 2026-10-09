using System.Security.Cryptography;
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
            System.Text.Encoding.UTF8.GetBytes("different subject"), keys.Enrollment, Nonce, Now).ShouldBeFalse();
    }

    [Fact]
    public void UnavailableReceiptOrStatus_Refuses()
    {
        using var keys = new Fixture();
        string uri = keys.Enrollment.RetrievalOrigin + "/v1/receipts/" + keys.Claims.ReceiptId;
        string statusUri = keys.Enrollment.StatusOrigin + "/v1/status/" + keys.Claims.ReceiptId + "?nonce=" + Nonce;
        P1ReceiptVerifier.Verify(null, uri, keys.Status, statusUri, keys.Claims, keys.Subject, keys.Enrollment, Nonce, Now).ShouldBeFalse();
        P1ReceiptVerifier.Verify(keys.Receipt, uri, null, statusUri, keys.Claims, keys.Subject, keys.Enrollment, Nonce, Now).ShouldBeFalse();
    }

    [Fact]
    public void ReplayedNonceAndExactExpiry_Refuse()
    {
        using var keys = new Fixture();
        keys.Verify(nonce: new string('f', 64)).ShouldBeFalse();
        keys.Verify(now: keys.StatusClaims.ExpiresAtUtc).ShouldBeFalse();
    }

    private static P1SignedDocument Sign(P1ReceiptClaims claims, ECDsa key) => Sign(P1ReceiptWireV1.Encode(claims), key);
    private static P1SignedDocument Sign(P1StatusClaims claims, ECDsa key) => Sign(P1ReceiptWireV1.Encode(claims), key);
    private static P1SignedDocument Sign(P1SignedDocument document, ECDsa key) => Sign(document.Payload, key);
    private static P1SignedDocument Sign(byte[] payload, ECDsa key)
        => new(payload, key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

    private sealed class Fixture : IDisposable
    {
        public Fixture(string kind = "producer-execution")
        {
            byte[] receiptSpki = ReceiptKey.ExportSubjectPublicKeyInfo();
            byte[] statusSpki = StatusKey.ExportSubjectPublicKeyInfo();
            string receiptFingerprint = Convert.ToHexStringLower(SHA256.HashData(receiptSpki));
            string statusFingerprint = Convert.ToHexStringLower(SHA256.HashData(statusSpki));
            Enrollment = new("https://p1.test.invalid", "https://p1.test.invalid", "https://p1.test.invalid", "hexalith:memories:c1:v1", "fixture-r1",
                receiptSpki, receiptFingerprint, statusSpki, statusFingerprint, Now.AddMinutes(-10), Now.AddDays(1));
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
        public P1ReceiptEnrollment Enrollment { get; }
        public P1ReceiptClaims Claims { get; }
        public P1StatusClaims StatusClaims { get; }
        public P1SignedDocument Receipt { get; }
        public P1SignedDocument Status { get; }
        public byte[] Subject { get; } = System.Text.Encoding.UTF8.GetBytes("offline subject bytes");

        public bool Verify(P1SignedDocument? receipt = default, P1SignedDocument? status = default,
            string? nonce = default, DateTimeOffset? now = default)
            => P1ReceiptVerifier.Verify(receipt ?? Receipt, Enrollment.RetrievalOrigin + "/v1/receipts/" + Claims.ReceiptId,
                status ?? Status, Enrollment.StatusOrigin + "/v1/status/" + Claims.ReceiptId + "?nonce=" + (nonce ?? Nonce),
                Claims, Subject, Enrollment, nonce ?? Nonce, now ?? Now);

        public void Dispose()
        {
            ReceiptKey.Dispose();
            StatusKey.Dispose();
        }
    }
}
