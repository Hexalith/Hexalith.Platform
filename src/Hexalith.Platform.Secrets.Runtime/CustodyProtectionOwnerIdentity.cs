using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact EventStore v23 owner installation identity; these bytes describe genesis and never authorize it.</summary>
public static class CustodyProtectionOwnerIdentity
{
    /// <summary>Returns the actor's existing pending-transition owner scope, independent of the governance guard.</summary>
    public static string Scope(string tenant) => new AggregateIdentity(tenant, "protection", "deletion-consumption-v23").ActorId + "|deletion-consumption-v23";
    /// <summary>Returns the existing empty DeletionConsumptionLedger default-serializer bytes in declared property order.</summary>
    public static byte[] GenesisBytes(string tenant)
    {
        var identity = new AggregateIdentity(tenant, "protection", "deletion-consumption-v23");
        if (identity.TenantId != tenant) throw new ArgumentException("Exact canonical tenant required.", nameof(tenant));
        return JsonSerializer.SerializeToUtf8Bytes(new { TenantId = tenant, Revision = 0L, KeyBlockSetRevision = 0L,
            Batches = Array.Empty<object>(), Revocations = Array.Empty<object>(), Operations = Array.Empty<object>() });
    }
    /// <summary>Returns the existing owner's uppercase SHA-256. Only independent installation may enroll this digest.</summary>
    public static string GenesisDigest(string tenant) => Convert.ToHexString(SHA256.HashData(GenesisBytes(tenant)));
}
