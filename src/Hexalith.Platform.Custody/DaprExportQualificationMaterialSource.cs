using System.Security.Cryptography;
using Dapr.Client;

namespace Hexalith.Platform.Custody;

/// <summary>Production restricted export material resolver over the existing version-selected Dapr GetSecret binding. It never accepts interaction roots or creates keys.</summary>
public sealed class DaprExportQualificationMaterialSource(DaprClient client,IPlatformKeyInventoryActor inventory,IPlatformKeyInventoryAuthority authority,
    IExportQualificationTrustSource independentlyPublishedTrust,TimeProvider clock,IReadOnlyDictionary<string,DaprExportQualificationProfile> enrolledProfiles):IExportQualificationMaterialSource
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PreflightAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(!Enum.IsDefined(use) || !enrolledProfiles.TryGetValue(input.ExportId,out var profile) || profile.TenantId!=input.TenantId || profile.ExportId!=input.ExportId
            || profile.NotBefore>clock.GetUtcNow() || profile.ValidUntil<=clock.GetUtcNow() || string.IsNullOrWhiteSpace(profile.PublicAnchorId) || string.IsNullOrWhiteSpace(profile.PublicAnchorVersion)) return Task.FromResult<IReadOnlyList<string>>(["ExactCurrentDedicatedExportProfile"]);
        bool Exact(DaprExportQualificationReference? reference,PlatformKeyPurpose purpose,string? version,string[] codecs)=>reference is not null && reference.InventoryKey.TenantId==input.TenantId
            && reference.InventoryKey.Purpose==purpose && (version is null || reference.InventoryKey.Version==version) && codecs.Contains(reference.Codec,StringComparer.Ordinal);
        bool valid=use==ExportQualificationMaterialUse.Sign?Exact(profile.Signer,PlatformKeyPurpose.ExportManifestSigningKey,input.SigningKeyVersion,["Pkcs8DerBase64","Pkcs8Pem"]):
            Exact(profile.ExportKey,PlatformKeyPurpose.ExportEnvelopeKey,null,["Aes256Base64"]) && Exact(profile.Kek,PlatformKeyPurpose.TenantKek,input.KekVersion,["Aes256Base64"]);
        return Task.FromResult<IReadOnlyList<string>>(valid?[]:["ExactDedicatedExportPurposeVersionReferenceAndCodec"]);
    }
    /// <inheritdoc />
    public async Task<ExportQualificationMaterial?> ResolveAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken cancellationToken)
    {
        if((await PreflightAsync(input,use,cancellationToken).ConfigureAwait(false)).Count!=0) return null;
        var profile=enrolledProfiles[input.ExportId];bool active=use!=ExportQualificationMaterialUse.Unwrap;
        var reader=new DaprExportQualificationReferenceReader(client,inventory,authority);
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);budget.CancelAfter(TimeSpan.FromSeconds(30));
        byte[]? export=null,kek=null,encoded=null;ECDsa? signer=null;bool transferred=false;
        try
        {
            var bindings=use==ExportQualificationMaterialUse.Sign?new[]{profile.Signer!}:new[]{profile.ExportKey!,profile.Kek!};
            var before=new List<PlatformKeyInventorySnapshot>();foreach(var binding in bindings) { var state=await reader.CurrentAsync(binding,active,budget.Token).ConfigureAwait(false);if(state is null) return null;before.Add(state); }
            if(use==ExportQualificationMaterialUse.Sign)
            {
                var reference=profile.Signer!;
                encoded=await reader.ReadAsync(reference,true,value=>reference.Codec=="Pkcs8DerBase64"?DaprExportQualificationReferenceReader.Base64(value):DaprExportQualificationReferenceReader.Pem(value,"PRIVATE KEY"),budget.Token).ConfigureAwait(false);
                if(encoded is null) return null;int encodedLength=encoded.Length;signer=ECDsa.Create();signer.ImportPkcs8PrivateKey(encoded,out int read);CryptographicOperations.ZeroMemory(encoded);encoded=null;
                var trust=await independentlyPublishedTrust.ResolveAsync(input,budget.Token).ConfigureAwait(false);budget.Token.ThrowIfCancellationRequested();
                if(read!=encodedLength || signer.ExportParameters(false).Curve.Oid.Value!="1.2.840.10045.3.1.7" || trust is null || trust.TenantId!=input.TenantId || trust.SigningKeyVersion!=input.SigningKeyVersion
                    || !signer.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(trust.SubjectPublicKeyInfo)) return null;
            }
            else
            {
                export=await reader.ReadAsync(profile.ExportKey!,active,value=>DaprExportQualificationReferenceReader.Base64(value,32),budget.Token).ConfigureAwait(false);
                kek=await reader.ReadAsync(profile.Kek!,active,value=>DaprExportQualificationReferenceReader.Base64(value,32),budget.Token).ConfigureAwait(false);
                if(export is null || kek is null) return null;
            }
            for(int index=0;index<bindings.Length;index++) { var current=await reader.CurrentAsync(bindings[index],active,budget.Token).ConfigureAwait(false);if(current is null || !DaprExportQualificationReferenceReader.Same(before[index],current)) return null; }
            budget.Token.ThrowIfCancellationRequested();if(profile.ValidUntil<=clock.GetUtcNow()) return null;
            var result=new ExportQualificationMaterial(export,kek,signer);transferred=true;return result;
        }
        catch(Exception) { cancellationToken.ThrowIfCancellationRequested();return null; }
        finally { if(encoded is not null) CryptographicOperations.ZeroMemory(encoded);if(!transferred) { if(export is not null) CryptographicOperations.ZeroMemory(export);if(kek is not null) CryptographicOperations.ZeroMemory(kek);signer?.Dispose(); } }
    }
}
