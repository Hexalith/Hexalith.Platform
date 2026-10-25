using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Public proof pins returned by the accepted independent enrollment; no private material.</summary>
public sealed class QualificationProofBinding
{
    private readonly byte[] _publicAnchor;

    /// <summary>Installs bounded immutable public PEM bytes after checking the accepted hash and ES256 key type. No path is opened.</summary>
    public QualificationProofBinding(string identity, string version, ReadOnlySpan<byte> publicAnchor, string publicAnchorSha256)
    {
        if (publicAnchor.Length is 0 or > 4096) { throw new ArgumentException("Public anchor exceeds the accepted bound.", nameof(publicAnchor)); }
        byte[] copy = publicAnchor.ToArray();
        string pem = System.Text.Encoding.ASCII.GetString(copy);
        if (!pem.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal)
            || Convert.ToHexString(SHA256.HashData(copy)).ToLowerInvariant() != publicAnchorSha256)
        { throw new ArgumentException("Public anchor does not match the accepted public pin.", nameof(publicAnchor)); }
        using var key = ECDsa.Create();
        key.ImportFromPem(pem);
        if (key.ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
        { throw new ArgumentException("An ES256 public anchor is required.", nameof(publicAnchor)); }
        Identity = identity;
        Version = version;
        _publicAnchor = copy;
    }

    /// <summary>Gets the accepted role identity.</summary>
    public string Identity { get; }

    /// <summary>Gets the accepted provider version.</summary>
    public string Version { get; }

    internal ECDsa OpenPublicKey()
    {
        var key = ECDsa.Create();
        key.ImportFromPem(System.Text.Encoding.ASCII.GetString(_publicAnchor));
        return key;
    }
}
