using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Platform.Custody;

/// <summary>Closed custody-owned export workflows over the actual private AES-GCM and export-specific detached ES256 cores. It cannot deliver keys or sign approvals.</summary>
public sealed class ExportQualificationEngine(IExportQualificationMaterialSource materials,IExportQualificationTrustSource publicTrust)
{
    /// <summary>Reports exact missing private export protocols before effects.</summary>
    public Task<IReadOnlyList<string>> PreflightAsync(string caseId,ExportQualificationInput input,CancellationToken token)=>materials.PreflightAsync(input,Signature(caseId)?ExportQualificationMaterialUse.Sign:ExportQualificationMaterialUse.Wrap,token);
    /// <summary>Creates one exact export artifact using independently resolved dedicated material, then verifies it through the same production primitives.</summary>
    public async Task<ExportQualificationOriginal?> ExecuteAsync(string caseId,ExportQualificationInput input,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(input);
        var use=Signature(caseId)?ExportQualificationMaterialUse.Sign:ExportQualificationMaterialUse.Wrap;
        if(!Supported(caseId) || input.Manifest is not { Length:>0 and <=1048576 } || (await materials.PreflightAsync(input,use,token).ConfigureAwait(false)).Count!=0) return null;
        using var owned=await materials.ResolveAsync(input,use,token).ConfigureAwait(false);if(owned is null) return null;
        token.ThrowIfCancellationRequested();
        ExportQualificationOriginal result;
        if(Signature(caseId))
        {
            var trust=await publicTrust.ResolveAsync(input,token).ConfigureAwait(false);if(!Exact(trust,input)) return null;
            token.ThrowIfCancellationRequested();
            string artifact=ExportManifestSignatureCore.Sign(Context(input),input.Manifest,owned.Signer);
            result=new(caseId,input.TenantId,input.ExportId,input.KekVersion,input.SigningKeyVersion,input.ManifestVersion,Hash(input.Manifest),null,null,null,artifact,Hash(trust!.SubjectPublicKeyInfo),false);
        }
        else
        {
            var context=new ExportKeyWrapContext(input.TenantId,input.ExportId,input.KekVersion);
            var wrapped=caseId=="S3.crypto-substitution.purpose"?new WrappedExportKey(context,RandomNumberGenerator.GetBytes(12),new byte[32],new byte[16]):ExportKeyWrappingCore.Wrap(context,owned.ExportKey,owned.Kek);
            if(caseId=="S3.crypto-substitution.purpose")
            {
                byte[] wrongAad=PlatformCanonicalBytes.Components(["NonExportPurposeV1",input.TenantId,input.ExportId,input.KekVersion]);
                try { using var aes=new AesGcm(owned.Kek,16);aes.Encrypt(wrapped.Nonce,owned.ExportKey,wrapped.Ciphertext,wrapped.Tag,wrongAad); }
                finally { CryptographicOperations.ZeroMemory(wrongAad); }
            }
            result=new(caseId,input.TenantId,input.ExportId,input.KekVersion,input.SigningKeyVersion,input.ManifestVersion,Hash(input.Manifest),wrapped.Nonce,wrapped.Ciphertext,wrapped.Tag,null,null,false);
        }
        owned.Dispose();return result with { OwnedBuffersCleared=owned.Cleared };
    }
    /// <summary>Independently reopens and verifies the exact original without fresh wrapping/signing. Every denial is produced by the actual crypto core.</summary>
    public async Task<ExportQualificationObservation?> ObserveAsync(ExportQualificationOriginal original,ExportQualificationInput input,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(input);
        if(!Supported(original.CaseId) || original.TenantId!=input.TenantId || original.ExportId!=input.ExportId || original.KekVersion!=input.KekVersion
            || original.SigningKeyVersion!=input.SigningKeyVersion || original.ManifestVersion!=input.ManifestVersion || original.ManifestSha256!=Hash(input.Manifest)) return null;
        if(Signature(original.CaseId))
        {
            var trust=await publicTrust.ResolveAsync(input,token).ConfigureAwait(false);if(!Exact(trust,input) || original.DetachedJws is null) return null;
            token.ThrowIfCancellationRequested();
            using var anchor=ECDsa.Create();byte[] spki=original.CaseId=="S3.crypto-substitution.public-anchor"?input.ForeignPublicAnchor??[]:trust!.SubjectPublicKeyInfo;
            anchor.ImportSubjectPublicKeyInfo(spki,out int read);if(read!=spki.Length) return null;
            string artifact=original.DetachedJws;
            if(original.CaseId=="S3.crypto-substitution.jws-header") artifact="eyJhbGciOiJub25lIn0"+artifact[artifact.IndexOf('.')..];
            bool validSignature=ExportManifestSignatureCore.Verify(Context(input),input.Manifest,artifact,anchor);
            return new(validSignature?"Verified":"Denied",null,validSignature,Hash(trust!.SubjectPublicKeyInfo)==original.PublicAnchorSha256,null);
        }
        using var owned=await materials.ResolveAsync(input,ExportQualificationMaterialUse.Unwrap,token).ConfigureAwait(false);if(owned is null) return null;
        token.ThrowIfCancellationRequested();
        bool valid;bool? authenticated=null,detached=null,retained=null;byte[] plaintext=new byte[32];
        try
        {
            {
                if(original.Nonce is not { Length:12 } || original.Ciphertext is not { Length:32 } || original.Tag is not { Length:16 }) return null;
                var expected=new ExportKeyWrapContext(input.TenantId,input.ExportId,input.KekVersion);
                var actual=expected;byte[] ciphertext=original.Ciphertext.ToArray(),tag=original.Tag.ToArray();
                switch(original.CaseId)
                {
                    case "S3.crypto-substitution.ciphertext":ciphertext[0]^=1;break;
                    case "S3.crypto-substitution.tag":tag[0]^=1;break;
                    case "S3.crypto-substitution.tenant":expected=expected with { TenantId=expected.TenantId+"-substituted" };break;
                    case "S3.crypto-substitution.export":expected=expected with { ExportId=expected.ExportId+"-substituted" };break;
                    case "S3.crypto-substitution.version":expected=expected with { KekVersion=expected.KekVersion+"-substituted" };break;
                }
                try
                {
                    using var recovered=ExportKeyWrappingCore.Unwrap(expected,new(actual,original.Nonce,ciphertext,tag),owned.Kek);
                    recovered.CopyTo(plaintext);valid=CryptographicOperations.FixedTimeEquals(plaintext,owned.ExportKey);
                }
                catch(CryptographicException) { valid=false; }
                authenticated=valid;
            }
            CryptographicOperations.ZeroMemory(plaintext);owned.Dispose();
            bool cleared=plaintext.AsSpan().IndexOfAnyExcept((byte)0)<0 && owned.Cleared && original.OwnedBuffersCleared;
            return new(valid?"Verified":"Denied",authenticated,detached,retained,cleared);
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }
    private static bool Exact(ExportQualificationTrust? trust,ExportQualificationInput input)=>trust is not null && trust.TenantId==input.TenantId && trust.SigningKeyVersion==input.SigningKeyVersion
        && !string.IsNullOrWhiteSpace(trust.PublicAnchorId) && !string.IsNullOrWhiteSpace(trust.PublicAnchorVersion);
    private static ExportManifestSignatureContext Context(ExportQualificationInput input)=>new(input.TenantId,input.ExportId,input.ManifestVersion,input.SigningKeyVersion);
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static bool Signature(string id)=>id is "S3.es256-offline-anchor" or "S3.crypto-substitution.jws-header" or "S3.crypto-substitution.public-anchor";
    private static bool Supported(string id)=>id is "S3.aes256gcm" or "S3.es256-offline-anchor" or "S3.crypto-substitution.ciphertext" or "S3.crypto-substitution.tag" or "S3.crypto-substitution.tenant"
        or "S3.crypto-substitution.export" or "S3.crypto-substitution.purpose" or "S3.crypto-substitution.version" or "S3.crypto-substitution.jws-header" or "S3.crypto-substitution.public-anchor";
}
