using System.Security.Cryptography;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Compares actual mounted bearer identities without publishing them or their internal comparison digests.</summary>
internal static class InstalledCredentialIndependence
{
    internal static async Task<bool> CheckAsync(IndependentDaprAuthorityBinding first,IndependentDaprAuthorityBinding second,CancellationToken token)
    {
        if(first.AppId==second.AppId || AgentsOwningProcessComposition.NormalizeCredential(first.CredentialReference)==AgentsOwningProcessComposition.NormalizeCredential(second.CredentialReference)) return false;
        using var a=ECDsa.Create();using var b=ECDsa.Create();a.ImportFromPem(first.PublicKeyPem);b.ImportFromPem(second.PublicKeyPem);
        if(a.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(b.ExportSubjectPublicKeyInfo())) return false;
        byte[]? left=null,right=null;
        try
        {
            left=await IdentityAsync(first.CredentialReference,token).ConfigureAwait(false);right=await IdentityAsync(second.CredentialReference,token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();return !CryptographicOperations.FixedTimeEquals(left,right);
        }
        finally { if(left is not null) CryptographicOperations.ZeroMemory(left);if(right is not null) CryptographicOperations.ZeroMemory(right); }
    }
    private static async Task<byte[]> IdentityAsync(string reference,CancellationToken token)
    {
        string normalized=AgentsOwningProcessComposition.NormalizeCredential(reference);
        if(!normalized.StartsWith("file:/",StringComparison.Ordinal)) throw new IOException("InstalledDistinctIndependentPrivateCredentialMounts");
        return await BoundedRegularFile.RunAcquisitionAsync(async ct=>await DaprSecretsInvoker.ReadMountedBearerIdentityAsync(reference,ct).ConfigureAwait(false)
            ??throw new IOException("InstalledValidIndependentPrivateCredentialMounts"),token).ConfigureAwait(false);
    }
}
