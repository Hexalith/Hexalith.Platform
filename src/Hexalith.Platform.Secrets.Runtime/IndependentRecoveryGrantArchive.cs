using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

// Immutable authorizations stay outside the bounded head. A signed committed-head barrier, rather than a pre-CAS file, proves publication.
internal sealed class IndependentRecoveryGrantArchive(string directory,IndependentAuthorityReceiverInstallation installation,ECDsa signer)
{
    internal async Task<bool> ContainsAsync(SecurityObservationRecoveryGrant grant,long currentRevision,CancellationToken token)
    {
        string path=PathFor(grant);var file=new FileInfo(path);if(!file.Exists) return false;
        if(file.LinkTarget is not null || file.Length is 0 or >1024*1024) throw new IOException("Exact immutable recovery grant required.");
        byte[] bytes=await File.ReadAllBytesAsync(path,token).ConfigureAwait(false);CustodyBindingContract.RejectDuplicateProperties(bytes);
        var record=JsonSerializer.Deserialize<ArchivedGrant>(bytes,CustodyBindingContract.Json);
        if(record is null || record.EnrollmentId!=installation.EnrollmentId || record.Scope!=installation.Scope || record.TrustRevision!=installation.TrustRevision
            || record.CommittedHeadRevision<1 || record.CommittedHeadRevision>currentRevision || !Same(record.Grant,grant)) return false;
        using var key=ECDsa.Create();key.ImportFromPem(installation.PublicKeyPem);
        return key.VerifyData(JsonSerializer.SerializeToUtf8Bytes(record with { Signature=[] },CustodyBindingContract.Json),record.Signature,HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence);
    }
    internal async Task PublishAsync(SecurityObservationRecoveryGrant grant,long committedRevision,CancellationToken token)
    {
        if(await ContainsAsync(grant,committedRevision,token).ConfigureAwait(false)) return;
        if(committedRevision<1) throw new IOException("Actual committed grant authority revision required.");
        var record=new ArchivedGrant(installation.EnrollmentId,installation.Scope,installation.TrustRevision,committedRevision,grant,[]);
        lock(signer) record=record with { Signature=signer.SignData(JsonSerializer.SerializeToUtf8Bytes(record,CustodyBindingContract.Json),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence) };
        await ImmutableFileArchive.PublishAsync(PathFor(grant),JsonSerializer.SerializeToUtf8Bytes(record,CustodyBindingContract.Json),token).ConfigureAwait(false);
    }
    private string PathFor(SecurityObservationRecoveryGrant grant)=>Path.Combine(directory,"recovery-grants",Hash(installation.TrustRevision),Hash(new { grant.Target,grant.Original }),Hash(grant)+".json");
    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,CustodyBindingContract.Json)));
    private static bool Same<T>(T left,T right)=>JsonSerializer.SerializeToUtf8Bytes(left,CustodyBindingContract.Json).AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right,CustodyBindingContract.Json));
    private sealed record ArchivedGrant(string EnrollmentId,string Scope,string TrustRevision,long CommittedHeadRevision,SecurityObservationRecoveryGrant Grant,byte[] Signature);
}
