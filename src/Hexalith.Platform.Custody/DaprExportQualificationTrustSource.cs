using System.Security.Cryptography;
using Dapr.Client;

namespace Hexalith.Platform.Custody;

/// <summary>Production exact Dapr public-anchor reader with current independently authorized inventory before and after each native read.</summary>
public sealed class DaprExportQualificationTrustSource(DaprClient client,IPlatformKeyInventoryActor inventory,IPlatformKeyInventoryAuthority authority,
    TimeProvider clock,IReadOnlyDictionary<string,DaprExportQualificationProfile> enrolledProfiles):IExportQualificationTrustSource
{
    /// <inheritdoc />
    public async Task<ExportQualificationTrust?> ResolveAsync(ExportQualificationInput input,CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if(!enrolledProfiles.TryGetValue(input.ExportId,out var profile) || profile.ExportId!=input.ExportId || profile.TenantId!=input.TenantId || profile.NotBefore>clock.GetUtcNow() || profile.ValidUntil<=clock.GetUtcNow()
            || string.IsNullOrWhiteSpace(profile.PublicAnchorId) || string.IsNullOrWhiteSpace(profile.PublicAnchorVersion)) return null;
        var reference=profile.PublicAnchor;var key=reference.InventoryKey;
        if(key.TenantId!=input.TenantId || key.Purpose!=PlatformKeyPurpose.ExportManifestSigningKey || key.Version!=input.SigningKeyVersion
            || key.PublicAnchorId!=profile.PublicAnchorId || key.PublicAnchorVersion!=profile.PublicAnchorVersion || reference.Codec is not ("SpkiDerBase64" or "SpkiPem")) return null;
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);budget.CancelAfter(TimeSpan.FromSeconds(30));
        byte[]? bytes=null;bool transferred=false;
        try
        {
            bytes=await new DaprExportQualificationReferenceReader(client,inventory,authority).ReadAsync(reference,false,
                value=>reference.Codec=="SpkiDerBase64"?DaprExportQualificationReferenceReader.Base64(value):DaprExportQualificationReferenceReader.Pem(value,"PUBLIC KEY"),budget.Token).ConfigureAwait(false);
            if(bytes is null) return null;
            using var anchor=ECDsa.Create();anchor.ImportSubjectPublicKeyInfo(bytes,out int read);
            if(read!=bytes.Length || anchor.ExportParameters(false).Curve.Oid.Value!="1.2.840.10045.3.1.7" || profile.ValidUntil<=clock.GetUtcNow()) return null;
            transferred=true;return new(input.TenantId,input.SigningKeyVersion,profile.PublicAnchorId,profile.PublicAnchorVersion,bytes);
        }
        catch(Exception) { cancellationToken.ThrowIfCancellationRequested();return null; }
        finally { if(!transferred && bytes is not null) CryptographicOperations.ZeroMemory(bytes); }
    }
}
