using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Private logical-original and recovery transport addresses. A new transport never changes the observation identity, body, first-seen or UTC day.</summary>
public static class PrivateRecordingIdentity
{
    /// <summary>Gets the original security-recording transport identity.</summary>
    public static string SecurityOriginalMessageId(string observationId)=>StableUlid("security",observationId);
    /// <summary>Gets one deterministic current-grant-bound recovery transport; terminal idempotency cannot replay the rejected ordinary attempt.</summary>
    public static string SecurityRecoveryMessageId(SecurityObservationRecoveryGrant grant)=>StableUlid("security-recovery",grant.Original.Intent.ObservationId,
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(grant,CustodyBindingContract.Json))));
    /// <summary>Derives the unchanged private message identifier from exact framed components.</summary>
    public static string StableUlid(params string[] components)
    {
        byte[] hash=SHA256.HashData(PlatformCanonicalBytes.Identity(components));var value=new BigInteger(hash.AsSpan(0,16),isUnsigned:true,isBigEndian:true);
        const string alphabet="0123456789ABCDEFGHJKMNPQRSTVWXYZ";var output=new char[26];
        for(int index=25;index>=0;index--) { output[index]=alphabet[(int)(value&31)];value>>=5; }return new(output);
    }
}
