using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Supplied P-256 current-authority credential. A missing, changed, or stale credential signs nothing and authorizes nothing.</summary>
internal sealed class PrivateCustodyBindingAuthority
{
    private readonly ECDsa _key;
    private readonly byte[] _spki;
    private readonly Dictionary<string, string> _originals = new(StringComparer.Ordinal);
    private readonly HashSet<string> _boundRequests = new(StringComparer.Ordinal);
    private readonly bool _current;

    private PrivateCustodyBindingAuthority(ECDsa key, byte[] spki, string issuer, string audience, string tenantId, string authorityId, long revision, bool current)
    {
        _key = key;
        _spki = spki;
        Issuer = issuer;
        Audience = audience;
        TenantId = tenantId;
        AuthorityId = authorityId;
        Revision = revision;
        _current = current;
    }

    internal string Issuer { get; }
    internal string Audience { get; }
    internal string TenantId { get; }
    internal string AuthorityId { get; }
    internal long Revision { get; }
    internal string KeyVersion { get; } = "capability-v1";
    internal string PublicAnchorId => Convert.ToHexString(SHA256.HashData(_spki))[..32];
    internal string PublicAnchorVersion => "v1";
    internal string ArtifactDigest => Convert.ToHexString(SHA256.HashData(_spki));
    internal DeletionCapabilityTrustProfile Profile => new(Issuer, Audience, TenantId, KeyVersion, PublicAnchorId, PublicAnchorVersion);

    /// <summary>Captures the supplied key. A null key, a non-P-256 key, or a non-positive revision captures nothing.</summary>
    internal static PrivateCustodyBindingAuthority? Create(ECDsa? key, string? issuer, string? audience, string? tenantId, string? authorityId, long revision)
    {
        if (key is null || revision <= 0 || !Text(issuer) || !Text(audience) || !Text(tenantId) || !Text(authorityId) || !IsP256(key)) { return null; }
        var owned = ECDsa.Create();
        try
        {
            owned.ImportPkcs8PrivateKey(key.ExportPkcs8PrivateKey(), out int read);
            byte[] spki = owned.ExportSubjectPublicKeyInfo();
            if (read <= 0 || spki.Length is 0 or > 512 || !IsP256(owned)) { owned.Dispose(); return null; }
            return new(owned, spki, issuer!, audience!, tenantId!, authorityId!, revision, true);
        }
        catch (CryptographicException) { owned.Dispose(); return null; }
    }

    /// <summary>Same captured key with current permission withdrawn. It retains no new signature.</summary>
    internal PrivateCustodyBindingAuthority AsStale() => new(_key, _spki, Issuer, Audience, TenantId, AuthorityId, Revision, false);

    /// <summary>Signs with the captured key. Exact retry returns the original detached signature.</summary>
    internal string? Sign(DeletionBatchCapabilityV1 payload) => Sign(payload, _key);

    /// <summary>Signs only when the presented key is the captured credential. A changed key writes nothing.</summary>
    internal string? Sign(DeletionBatchCapabilityV1 payload, ECDsa? presentedKey)
    {
        if (!_current || !SameKey(presentedKey) || !Matches(payload)) { return null; }
        string id;
        try { id = DeletionBatchCapabilityCodec.SigningRequestId(payload); }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException) { return null; }
        if (_originals.TryGetValue(id, out string? original)) { return original; }
        string signature;
        try { signature = DeletionBatchCapabilityCodec.Sign(payload, Profile, _key); }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException) { return null; }
        _originals.Add(id, signature);
        return signature;
    }

    /// <summary>Verifies with the captured public key.</summary>
    internal bool Verify(DeletionBatchCapabilityV1 payload, string? signature) => Verify(payload, signature, _key);

    /// <summary>Verifies the original detached signature only when the presented key is the captured credential.</summary>
    internal bool Verify(DeletionBatchCapabilityV1 payload, string? signature, ECDsa? presentedKey)
    {
        if (!_current || signature is null || !SameKey(presentedKey) || !Matches(payload)) { return false; }
        try { return DeletionBatchCapabilityCodec.Verify(payload, Profile, signature, _key); }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException) { return false; }
    }

    /// <summary>Checks the captured credential fingerprint over the exact request. Proof bytes, app ids, and binding names are not inputs.</summary>
    internal bool AuthorizesExact(string? issuer, string? audience, string? tenantId, string? authorityId, long revision, string operation, string operationId, string requestDigest, ReadOnlySpan<byte> presentedFingerprint)
    {
        if (!_current || revision != Revision || issuer != Issuer || audience != Audience || tenantId != TenantId || authorityId != AuthorityId
            || !Text(operation) || !Text(operationId) || requestDigest is not { Length: 64 }) { return false; }
        byte[] expected = RequestFingerprint(operation, operationId, requestDigest);
        return presentedFingerprint.Length == expected.Length && CryptographicOperations.FixedTimeEquals(expected, presentedFingerprint)
            && _boundRequests.Contains(Convert.ToHexString(expected));
    }

    /// <summary>Records the credential fingerprint for one exact request. A stale credential binds nothing.</summary>
    internal bool BindRequest(string operation, string operationId, string requestDigest)
    {
        if (!_current || !Text(operation) || !Text(operationId) || requestDigest is not { Length: 64 }) { return false; }
        _boundRequests.Add(Convert.ToHexString(RequestFingerprint(operation, operationId, requestDigest)));
        return true;
    }

    /// <summary>Fingerprint of this credential over one exact operation, operation id, and request digest.</summary>
    internal byte[] RequestFingerprint(string operation, string operationId, string requestDigest)
    {
        byte[] prefix = Encoding.UTF8.GetBytes(string.Join('\0', "custody-binding-authority.v1", Issuer, Audience, TenantId, AuthorityId,
            Revision.ToString(CultureInfo.InvariantCulture), operation, operationId, requestDigest));
        byte[] material = new byte[_spki.Length + prefix.Length];
        _spki.CopyTo(material, 0);
        prefix.CopyTo(material, _spki.Length);
        try { return SHA256.HashData(material); }
        finally { CryptographicOperations.ZeroMemory(material); CryptographicOperations.ZeroMemory(prefix); }
    }

    private bool Matches(DeletionBatchCapabilityV1 payload)
        => payload is not null && payload.Issuer == Issuer && payload.Audience == Audience && payload.TenantId == TenantId && payload.CapabilityKeyVersion == KeyVersion;

    private bool SameKey(ECDsa? presented)
    {
        if (presented is null || !IsP256(presented)) { return false; }
        try
        {
            byte[] spki = presented.ExportSubjectPublicKeyInfo();
            return spki.Length == _spki.Length && CryptographicOperations.FixedTimeEquals(spki, _spki);
        }
        catch (CryptographicException) { return false; }
    }

    private static bool IsP256(ECDsa key) => key.KeySize == 256 && key.ExportParameters(false).Curve.Oid.Value == "1.2.840.10045.3.1.7";

    private static bool Text(string? value)
    {
        try { return !string.IsNullOrWhiteSpace(value) && value.Length <= 2048 && new UTF8Encoding(false, true).GetByteCount(value) <= 2048; }
        catch (EncoderFallbackException) { return false; }
    }
}
