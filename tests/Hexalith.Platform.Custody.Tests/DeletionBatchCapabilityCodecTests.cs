using Hexalith.EventStore.Contracts.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Closed RFC8785 payload/ES256 vectors, never production key-family, guard or consumption authority.</summary>
public sealed class DeletionBatchCapabilityCodecTests
{
    private static DeletionBatchCapabilityV1 Payload() => new("issuer", "protection-v1", "tenant-a", "request-1", "seal-1", "accepted", 0,
        "batch-1", new string('A', 64), "tenant-a:governance:guard-1", 7, 1, 1, "key-v1");
    private static DeletionCapabilityTrustProfile Profile() => new("issuer", "protection-v1", "tenant-a", "key-v1", "anchor-1", "anchor-v1");
    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Canonical closed JSON is lexically sorted, whitespace-free, exact Unicode and minimally escaped per RFC8785 section3.2.2.2.</summary>
    [Fact]
    public void CanonicalClosedPayloadUsesGoldenSortedFieldsAndMinimalStringEscapes()
    {
        var payload = Payload() with { DestructionSealId = "€<>&/é\u000f\b\t\n\f\r\\\"" };
        string canonical = Encoding.UTF8.GetString(DeletionBatchCapabilityCodec.CanonicalPayload(payload));
        using var json = JsonDocument.Parse(canonical);
        json.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(new[]
        { "AttestationOrdinal", "Audience", "BatchId", "BatchKind", "BatchOrdinal", "CapabilityKeyVersion", "DeletionRequestId", "DestructionSealId",
            "GuardStreamId", "IntendedIssuedGuardRevision", "Issuer", "ManifestDigest", "SigningAttemptOrdinal", "TenantId" });
        json.RootElement.GetProperty("DestructionSealId").GetString().ShouldBe(payload.DestructionSealId);
        canonical.ShouldContain("€<>&/é\\u000f\\b\\t\\n\\f\\r\\\\\\\"");
        canonical.ShouldNotContain("\\u20AC"); canonical.ShouldNotContain("CommittedIssuedGuardRevision"); canonical.ShouldNotContain("ExpiresAt");
        canonical.ShouldNotEndWith("\n");
    }

    /// <summary>One closed signing identity binds the stable seal, batch, expected guard compare, attempt, attestation, key and audience.</summary>
    [Fact]
    public void SigningRequestIdentityBindsEveryRegisterFieldWithoutActualIssueRevision()
    {
        var payload = Payload(); string original = DeletionBatchCapabilityCodec.SigningRequestId(payload);
        original.ShouldBe(Convert.ToHexString(SHA256.HashData(DeletionBatchCapabilityCodec.CanonicalPayload(payload))));
        DeletionBatchCapabilityCodec.SigningRequestId(payload).ShouldBe(original);
        foreach (var changed in new[] { payload with { DestructionSealId = "seal-2" }, payload with { IntendedIssuedGuardRevision = 8 },
            payload with { SigningAttemptOrdinal = 2 }, payload with { AttestationOrdinal = 2 }, payload with { BatchId = "batch-2" },
            payload with { ManifestDigest = new string('B', 64) }, payload with { CapabilityKeyVersion = "key-v2" }, payload with { Audience = "other" } })
        { DeletionBatchCapabilityCodec.SigningRequestId(changed).ShouldNotBe(original); }
        typeof(DeletionBatchCapabilityV1).GetProperties().Length.ShouldBe(14);
    }

    /// <summary>JCS retains valid Unicode as-is and rejects lone surrogate substitution; safe JSON integer representation is mandatory.</summary>
    [Theory]
    [InlineData("surrogate")]
    [InlineData("integer")]
    [InlineData("negative")]
    [InlineData("empty")]
    public void MalformedCapabilityCannotBeSigned(string vector)
    {
        var payload = vector switch { "surrogate" => Payload() with { DestructionSealId = new string((char)0xD800, 1) },
            "integer" => Payload() with { IntendedIssuedGuardRevision = 9007199254740992 },
            "negative" => Payload() with { SigningAttemptOrdinal = 0 }, _ => Payload() with { BatchId = "" } };
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Should.Throw<ArgumentException>(() => DeletionBatchCapabilityCodec.Sign(payload, Profile(), key));
        DeletionBatchCapabilityCodec.Verify(payload, Profile(), "malformed", key).ShouldBeFalse();
    }

    /// <summary>Combining Unicode identities remain distinct rather than normalized into another signed identity.</summary>
    [Fact]
    public void CanonicalStringsDoNotNormalizeDistinctUnicode()
    {
        var first = Payload() with { DestructionSealId = "é" }; var second = first with { DestructionSealId = "e\u0301" };
        DeletionBatchCapabilityCodec.SigningRequestId(first).ShouldNotBe(DeletionBatchCapabilityCodec.SigningRequestId(second));
    }

    /// <summary>Detached ES256 can be verified independently using only the public P256 anchor and exact JWS signing input.</summary>
    [Fact]
    public void PublicAnchorVerifiesActualCanonicalBytesAndDetachedSignature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); using var anchor = ECDsa.Create(key.ExportParameters(false));
        var payload = Payload(); string jws = DeletionBatchCapabilityCodec.Sign(payload, Profile(), key);
        DeletionBatchCapabilityCodec.Verify(payload, Profile(), jws, anchor).ShouldBeTrue();
        string[] parts = jws.Split('.'); parts.Length.ShouldBe(3); parts[1].ShouldBeEmpty();
        byte[] signingInput = Encoding.ASCII.GetBytes(parts[0] + "." + Encode(DeletionBatchCapabilityCodec.CanonicalPayload(payload)));
        anchor.VerifyData(signingInput, Decode(parts[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation).ShouldBeTrue();
        using var header = JsonDocument.Parse(Decode(parts[0])); header.RootElement.GetProperty("typ").GetString().ShouldBe("DeletionBatchCapabilityV1");
        header.RootElement.GetProperty("kid").GetString().ShouldBe("anchor-1");
    }

    /// <summary>Wrong issuer/audience/tenant/key/anchor/version and changed expected issue compare reject offline.</summary>
    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("tenant")]
    [InlineData("key")]
    [InlineData("anchor")]
    [InlineData("anchor-version")]
    [InlineData("guard")]
    public void SubstitutedContextCannotVerify(string vector)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var payload = Payload(); var profile = Profile();
        string signed = DeletionBatchCapabilityCodec.Sign(payload, profile, key);
        payload = vector switch { "issuer" => payload with { Issuer = "other" }, "audience" => payload with { Audience = "other" },
            "tenant" => payload with { TenantId = "other" }, "key" => payload with { CapabilityKeyVersion = "other" },
            "guard" => payload with { IntendedIssuedGuardRevision = 8 }, _ => payload };
        profile = vector switch { "anchor" => profile with { PublicAnchorId = "other" }, "anchor-version" => profile with { PublicAnchorVersion = "other" }, _ => profile };
        DeletionBatchCapabilityCodec.Verify(payload, profile, signed, key).ShouldBeFalse();
    }

    /// <summary>Malformed detached framing and wrong curves cannot be accepted or trigger unbounded signature decoding.</summary>
    [Fact]
    public void BoundedDetachedFramingAndCurveAreRequired()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); using var wrong = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var payload = Payload(); string signed = DeletionBatchCapabilityCodec.Sign(payload, Profile(), key);
        foreach (string malformed in new[] { new string('x', 1000000), signed + "x", signed[..^1], signed.Replace("..", ".payload."), signed + "." })
        { DeletionBatchCapabilityCodec.Verify(payload, Profile(), malformed, key).ShouldBeFalse(); }
        Should.Throw<CryptographicException>(() => DeletionBatchCapabilityCodec.Sign(payload, Profile(), wrong));
        DeletionBatchCapabilityCodec.Verify(payload, Profile(), signed, wrong).ShouldBeFalse();
    }
}
