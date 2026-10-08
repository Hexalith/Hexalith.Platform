using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Private stateless S3 cryptographic prerequisites using ephemeral keys; no production custody claims.</summary>
public sealed class ExportCustodyCryptographyTests
{
    private static readonly ExportKeyWrapContext WrapContext = new("tenant-a", "export-1", "kek-1");
    private static readonly ExportManifestSignatureContext SignatureContext = new("tenant-a", "export-1", 1, "manifest-key-1");

    /// <summary>Wrapping authenticates bytes with fresh nonces and clears the owned plaintext capability on disposal.</summary>
    [Fact]
    public void WrappedKeyRoundTripAndFreshNonceKeepPlaintextPrivate()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        byte[] kek = RandomNumberGenerator.GetBytes(32);
        var first = ExportKeyWrappingCore.Wrap(WrapContext, key, kek);
        var second = ExportKeyWrappingCore.Wrap(WrapContext, key, kek);
        first.Ciphertext.ShouldNotBe(key);
        first.Nonce.ShouldNotBe(second.Nonce);
        using var owned = ExportKeyWrappingCore.Unwrap(WrapContext, first, kek);
        byte[] restored = new byte[32];
        owned.CopyTo(restored);
        restored.ShouldBe(key);
        JsonSerializer.Serialize(owned).ShouldBe("{}");
        owned.ToString().ShouldBe("OwnedExportKey");
        owned.Dispose();
        Should.Throw<ObjectDisposedException>(() => owned.CopyTo(restored));
    }

    /// <summary>Disposal actually overwrites the retained owned plaintext buffer, not only its disposed flag.</summary>
    [Fact]
    public void DisposalZeroesEveryOwnedKeyByte()
    {
        byte[] retained = Enumerable.Range(1, 32).Select(x => (byte)x).ToArray();
        using var owned = new OwnedExportKey(retained);
        owned.Dispose();
        retained.ShouldAllBe(value => value == 0);
        owned.Dispose();
        retained.ShouldAllBe(value => value == 0);
    }

    /// <summary>Malformed large text/framing is denied before any unbounded split or signature decode.</summary>
    [Theory]
    [InlineData("large-signature")]
    [InlineData("large-header")]
    [InlineData("many-segments")]
    [InlineData("whitespace")]
    [InlineData("short")]
    [InlineData("long")]
    [InlineData("dot")]
    [InlineData("invalid-base64")]
    public void BoundedMalformedJwsCannotVerify(string variant)
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] manifest = "{}"u8.ToArray();
        string valid = ExportManifestSignatureCore.Sign(SignatureContext, manifest, signer);
        string header = valid[..valid.IndexOf('.', StringComparison.Ordinal)];
        string malformed = variant switch
        {
            "large-signature" => header + ".." + new string('A', 1_000_000),
            "large-header" => new string('A', 1_000_000) + ".." + new string('A', 86),
            "many-segments" => new string('.', 1_000_000),
            "whitespace" => new string(' ', 1_000_000),
            "short" => valid[..^1],
            "long" => valid + "A",
            "dot" => header + ".A." + new string('A', 85),
            _ => header + ".." + new string('!', 86),
        };
        ExportManifestSignatureCore.Verify(SignatureContext, manifest, malformed, signer).ShouldBeFalse();
    }

    /// <summary>Changing any exact context denies unwrap even when the original key remains available.</summary>
    [Theory]
    [InlineData("tenant")]
    [InlineData("export")]
    [InlineData("version")]
    public void ChangedWrapIdentityCannotReleaseKey(string field)
    {
        byte[] kek = RandomNumberGenerator.GetBytes(32);
        var wrapped = ExportKeyWrappingCore.Wrap(WrapContext, RandomNumberGenerator.GetBytes(32), kek);
        var changed = field switch
        {
            "tenant" => WrapContext with { TenantId = "tenant-b" },
            "export" => WrapContext with { ExportId = "export-2" },
            _ => WrapContext with { KekVersion = "kek-2" },
        };
        Should.Throw<CryptographicException>(() => ExportKeyWrappingCore.Unwrap(changed, wrapped, kek));
        // Also reject an attacker relabelling the ciphertext's context to match the requested substitution.
        Should.Throw<CryptographicException>(() => ExportKeyWrappingCore.Unwrap(changed, wrapped with { Context = changed }, kek));
    }

    /// <summary>Independent nonce/ciphertext/tag corruption and a wrong KEK cannot yield plaintext.</summary>
    [Theory]
    [InlineData("nonce")]
    [InlineData("ciphertext")]
    [InlineData("tag")]
    [InlineData("key")]
    public void TamperedWrappedBytesCannotReleaseKey(string field)
    {
        byte[] kek = RandomNumberGenerator.GetBytes(32);
        var wrapped = ExportKeyWrappingCore.Wrap(WrapContext, RandomNumberGenerator.GetBytes(32), kek);
        if (field == "nonce") { wrapped.Nonce[0] ^= 1; }
        if (field == "ciphertext") { wrapped.Ciphertext[0] ^= 1; }
        if (field == "tag") { wrapped.Tag[0] ^= 1; }
        if (field == "key") { kek[0] ^= 1; }
        Should.Throw<CryptographicException>(() => ExportKeyWrappingCore.Unwrap(WrapContext, wrapped, kek));
    }

    /// <summary>Malformed envelopes and key sizes fail before a plaintext capability exists.</summary>
    [Theory]
    [InlineData("nonce")]
    [InlineData("ciphertext")]
    [InlineData("tag")]
    [InlineData("null-nonce")]
    [InlineData("null-ciphertext")]
    [InlineData("null-tag")]
    public void InvalidWrappedFormatFailsClosed(string field)
    {
        byte[] kek = RandomNumberGenerator.GetBytes(32);
        var wrapped = ExportKeyWrappingCore.Wrap(WrapContext, RandomNumberGenerator.GetBytes(32), kek);
        var invalid = field switch
        {
            "nonce" => wrapped with { Nonce = new byte[11] },
            "ciphertext" => wrapped with { Ciphertext = new byte[31] },
            "tag" => wrapped with { Tag = new byte[15] },
            "null-nonce" => wrapped with { Nonce = null! },
            "null-ciphertext" => wrapped with { Ciphertext = null! },
            _ => wrapped with { Tag = null! },
        };
        Should.Throw<CryptographicException>(() => ExportKeyWrappingCore.Unwrap(WrapContext, invalid, kek));
    }

    /// <summary>AES-256 is mandatory, rather than accepting weaker AES material or arbitrary key lengths.</summary>
    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(31)]
    [InlineData(33)]
    public void UnsupportedKeySizesCannotWrapOrUnwrap(int length)
    {
        byte[] key = new byte[32];
        byte[] kek = new byte[32];
        Should.Throw<ArgumentException>(() => ExportKeyWrappingCore.Wrap(WrapContext, new byte[length], kek));
        Should.Throw<ArgumentException>(() => ExportKeyWrappingCore.Wrap(WrapContext, key, new byte[length]));
        var wrapped = ExportKeyWrappingCore.Wrap(WrapContext, key, kek);
        Should.Throw<CryptographicException>(() => ExportKeyWrappingCore.Unwrap(WrapContext, wrapped, new byte[length]));
    }

    /// <summary>Separate public-key-only offline verification uses RFC JWS signing input and the 64-byte ES256 wire shape.</summary>
    [Fact]
    public void DetachedEs256SignatureVerifiesWithIndependentPublicOnlyAnchor()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var anchor = ECDsa.Create();
        anchor.ImportSubjectPublicKeyInfo(signer.ExportSubjectPublicKeyInfo(), out _);
        byte[] manifest = "{\"objectCount\":2,\"encrypted\":true}"u8.ToArray();
        string jws = ExportManifestSignatureCore.Sign(SignatureContext, manifest, signer);
        ExportManifestSignatureCore.Verify(SignatureContext, manifest, jws, anchor).ShouldBeTrue();
        string[] parts = jws.Split('.');
        parts.Length.ShouldBe(3);
        parts[1].ShouldBeEmpty();
        byte[] signature = Decode(parts[2]);
        signature.Length.ShouldBe(64);
        using var header = JsonDocument.Parse(Decode(parts[0]));
        header.RootElement.GetProperty("alg").GetString().ShouldBe("ES256");
        header.RootElement.GetProperty("typ").GetString().ShouldBe("hexalith-export-manifest-v1");
        // Independently assemble RFC 7515's encoded protected-header + '.' + encoded payload.
        byte[] input = Encoding.ASCII.GetBytes(parts[0] + "." + Encode(manifest));
        anchor.VerifyData(input, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation).ShouldBeTrue();
        Should.Throw<CryptographicException>(() => anchor.ExportParameters(true));
    }

    /// <summary>Signature identity substitutions are rejected before a future lifecycle consumer can accept them.</summary>
    [Theory]
    [InlineData("tenant")]
    [InlineData("export")]
    [InlineData("version")]
    [InlineData("key")]
    public void ChangedSignatureIdentityCannotVerify(string field)
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] manifest = "{}"u8.ToArray();
        string jws = ExportManifestSignatureCore.Sign(SignatureContext, manifest, signer);
        var changed = field switch
        {
            "tenant" => SignatureContext with { TenantId = "tenant-b" },
            "export" => SignatureContext with { ExportId = "export-2" },
            "version" => SignatureContext with { ManifestVersion = 2 },
            _ => SignatureContext with { SigningKeyVersion = "manifest-key-2" },
        };
        ExportManifestSignatureCore.Verify(changed, manifest, jws, signer).ShouldBeFalse();
    }

    /// <summary>Altered payload, tag, header purpose, algorithm, and trust anchor do not verify.</summary>
    [Theory]
    [InlineData("payload")]
    [InlineData("signature")]
    [InlineData("purpose")]
    [InlineData("algorithm")]
    [InlineData("anchor")]
    [InlineData("embedded")]
    [InlineData("padded")]
    public void ChangedJwsOrPublicAnchorCannotVerify(string field)
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var foreignAnchor = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] manifest = "{}"u8.ToArray();
        string[] parts = ExportManifestSignatureCore.Sign(SignatureContext, manifest, signer).Split('.');
        if (field == "payload") { manifest[0] = (byte)'!'; }
        if (field == "signature") { byte[] signature = Decode(parts[2]); signature[0] ^= 1; parts[2] = Encode(signature); }
        if (field is "purpose" or "algorithm")
        {
            string header = Encoding.UTF8.GetString(Decode(parts[0]));
            header = field == "purpose" ? header.Replace("hexalith-export-manifest-v1", "decision-approval-v1", StringComparison.Ordinal)
                : header.Replace("ES256", "HS256", StringComparison.Ordinal);
            parts[0] = Encode(Encoding.UTF8.GetBytes(header));
        }
        if (field == "embedded") { parts[1] = Encode(manifest); }
        if (field == "padded") { parts[2] += "=="; }
        string changed = string.Join('.', parts);
        ExportManifestSignatureCore.Verify(SignatureContext, manifest, changed, field == "anchor" ? foreignAnchor : signer).ShouldBeFalse();
    }

    /// <summary>Invalid UTF-16 identities cannot collide with a signed replacement character or another malformed identity.</summary>
    [Theory]
    [InlineData("tenant", "high")]
    [InlineData("export", "high")]
    [InlineData("key", "high")]
    [InlineData("tenant", "low")]
    [InlineData("export", "low")]
    [InlineData("key", "low")]
    public void MalformedSignatureIdentityCannotSignOrVerify(string field, string kind)
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] manifest = "{}"u8.ToArray();
        var replacement = WithSignatureIdentity(field, "identity-\uFFFD");
        string replacementSignature = ExportManifestSignatureCore.Sign(replacement, manifest, signer);
        foreach (string invalid in kind == "high" ? new[] { "identity-\uD800", "identity-\uD801" } : new[] { "identity-\uDC00", "identity-\uDC01" })
        {
            var malformed = WithSignatureIdentity(field, invalid);
            Should.Throw<EncoderFallbackException>(() => ExportManifestSignatureCore.Sign(malformed, manifest, signer));
            ExportManifestSignatureCore.Verify(malformed, manifest, replacementSignature, signer).ShouldBeFalse();
        }
    }

    /// <summary>Valid Unicode remains exactly bound without silently normalizing distinct identities.</summary>
    [Theory]
    [InlineData("tenant")]
    [InlineData("export")]
    [InlineData("key")]
    public void ValidUnicodeSignatureIdentityIsExact(string field)
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] manifest = "{}"u8.ToArray();
        var composed = WithSignatureIdentity(field, "identity-\u00E9-\U0001F512");
        string signature = ExportManifestSignatureCore.Sign(composed, manifest, signer);
        ExportManifestSignatureCore.Verify(composed, manifest, signature, signer).ShouldBeTrue();
        ExportManifestSignatureCore.Verify(WithSignatureIdentity(field, "identity-e\u0301-\U0001F512"), manifest, signature, signer).ShouldBeFalse();
    }

    private static ExportManifestSignatureContext WithSignatureIdentity(string field, string value)
        => field switch
        {
            "tenant" => SignatureContext with { TenantId = value },
            "export" => SignatureContext with { ExportId = value },
            _ => SignatureContext with { SigningKeyVersion = value },
        };

    /// <summary>ES256 never admits a different curve or missing exact identity.</summary>
    [Fact]
    public void WrongCurveOrInvalidContextCannotSign()
    {
        using var wrongCurve = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Should.Throw<CryptographicException>(() => ExportManifestSignatureCore.Sign(SignatureContext, "{}"u8.ToArray(), wrongCurve));
        ExportManifestSignatureCore.Verify(SignatureContext, "{}"u8.ToArray(), "invalid", wrongCurve).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => ExportManifestSignatureCore.Sign(SignatureContext with { ManifestVersion = 0 }, "{}"u8.ToArray(), signer));
        Should.Throw<ArgumentException>(() => ExportKeyWrappingCore.Wrap(WrapContext with { TenantId = " " }, new byte[32], new byte[32]));
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
}
