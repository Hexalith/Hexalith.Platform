using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

// Immutable authorizations stay outside the bounded head. A signed committed-head barrier, rather than a pre-CAS file, proves publication.
internal sealed class IndependentRecoveryGrantArchive(string directory,IndependentAuthorityReceiverInstallation installation,ECDsa signer,IndependentArchiveCapacity? capacity=null)
{
    internal async Task<bool> ContainsAsync(SecurityObservationRecoveryGrant grant,long currentRevision,CancellationToken token)
    {
        string path=PathFor(grant);byte[] bytes;
        try { bytes=await BoundedRegularFile.ReadAsync(path,1024*1024,token).ConfigureAwait(false); }
        catch(IOException) { return false; }
CustodyBindingContract.RejectDuplicateProperties(bytes);
        var record=JsonSerializer.Deserialize<ArchivedGrant>(bytes,CustodyBindingContract.Json);
        if(record is null || record.EnrollmentId!=installation.EnrollmentId || record.Scope!=installation.Scope || record.TrustRevision!=installation.TrustRevision
            || record.CommittedHeadRevision<1 || record.CommittedHeadRevision>currentRevision || !Same(record.Grant,grant)) return false;
        using var key=ECDsa.Create();key.ImportFromPem(installation.PublicKeyPem);
        return key.VerifyData(JsonSerializer.SerializeToUtf8Bytes(record with { Signature=[] },CustodyBindingContract.Json),record.Signature,HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence);
    }
    internal async Task PrepareAsync(SecurityObservationRecoveryGrant grant,long currentRevision,CancellationToken token)
    {
        if(capacity is not null && !await ContainsAsync(grant,currentRevision,token).ConfigureAwait(false))
            await capacity.ReserveAsync(capacity.Ticket("recovery-grant",Hash(grant)),1024*1024+2L*65536,0,token,Hash(grant),"recovery-grant",Hash(grant),1).ConfigureAwait(false);
    }
    internal async Task PublishAsync(SecurityObservationRecoveryGrant grant,long committedRevision,CancellationToken token)
    {
        if(await ContainsAsync(grant,committedRevision,token).ConfigureAwait(false))
        {
            if(capacity is not null) await capacity.SettleAsync(capacity.Ticket("recovery-grant",Hash(grant)),token).ConfigureAwait(false);
            return;
        }
        if(committedRevision<1) throw new IOException("Actual committed grant authority revision required.");
        var record=new ArchivedGrant(installation.EnrollmentId,installation.Scope,installation.TrustRevision,committedRevision,grant,[]);
        lock(signer) record=record with { Signature=signer.SignData(JsonSerializer.SerializeToUtf8Bytes(record,CustodyBindingContract.Json),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence) };
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(record,CustodyBindingContract.Json);
        if(capacity is not null)
        {
            if(bytes.Length>1024*1024) throw new IOException("Bounded recovery grant archive record required.");
            string ticket=capacity.Ticket("recovery-grant",Hash(grant));await PrepareAsync(grant,committedRevision,token).ConfigureAwait(false);
            await capacity.PublishAsync(ticket,PathFor(grant),bytes,token,stableRecoveryCommitment:Hash(grant)).ConfigureAwait(false);await capacity.SettleAsync(ticket,token).ConfigureAwait(false);
        }
        else await ImmutableFileArchive.PublishAsync(PathFor(grant),bytes,token).ConfigureAwait(false);
    }
    private string PathFor(SecurityObservationRecoveryGrant grant)=>Path.Combine(directory,"recovery-grants",Hash(installation.TrustRevision),Hash(new { grant.Target,grant.Original }),Hash(grant)+".json");
    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,CustodyBindingContract.Json)));
    private static bool Same<T>(T left,T right)=>JsonSerializer.SerializeToUtf8Bytes(left,CustodyBindingContract.Json).AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right,CustodyBindingContract.Json));
    private sealed record ArchivedGrant(string EnrollmentId,string Scope,string TrustRevision,long CommittedHeadRevision,SecurityObservationRecoveryGrant Grant,byte[] Signature);
}
