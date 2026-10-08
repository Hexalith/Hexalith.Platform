using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Platform.Custody;

/// <summary>Complete immutable principal-bound export release identity; all fields are safe opaque references.</summary>
/// <param name="TenantId">Exact export tenant.</param>
/// <param name="DeliveryId">Immutable delivery identity.</param>
/// <param name="ExportId">Immutable export identity.</param>
/// <param name="CommitAcknowledgementRevision">Durable export acknowledgement revision.</param>
/// <param name="RequesterActorId">Only this authenticated principal may receive key bytes.</param>
/// <param name="Purpose">Exact permitted key-use purpose.</param>
/// <param name="Audience">Exact direct-delivery audience.</param>
/// <param name="ExclusiveExpiry">Exclusive UTC release boundary.</param>
/// <param name="LifecycleVersion">Exact lifecycle decision version.</param>
/// <param name="StoreTarget">Exact lifecycle-covered export store target.</param>
/// <param name="ContractVersion">Closed delivery contract version.</param>
public sealed record ExportKeyDeliveryIdentity(string TenantId, string DeliveryId, string ExportId, long CommitAcknowledgementRevision,
    string RequesterActorId, string Purpose, string Audience, DateTimeOffset ExclusiveExpiry, string LifecycleVersion, string StoreTarget, int ContractVersion)
{
    /// <summary>Gets the private exact tenant/delivery actor address; changed fields collide into an exact identity conflict.</summary>
    public string ActorId
    {
        get
        {
            Validate();
            return new AggregateIdentity(TenantId, "custody", "export-delivery-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DeliveryId)))).ActorId;
        }
    }
    internal void Validate()
    {
        var strict = new UTF8Encoding(false, true);
        foreach (string field in new[] { TenantId, DeliveryId, ExportId, RequesterActorId, Purpose, Audience, LifecycleVersion, StoreTarget })
        {
            if (string.IsNullOrWhiteSpace(field) || field.Length > 2048) { throw new ArgumentException("Malformed export delivery identity."); }
            _ = strict.GetByteCount(field);
        }
        _ = new AggregateIdentity(TenantId, "custody", "validation");
        if (CommitAcknowledgementRevision <= 0 || ContractVersion != 1 || ExclusiveExpiry == default || ExclusiveExpiry.Offset != TimeSpan.Zero)
        { throw new ArgumentException("Malformed export delivery lifecycle."); }
    }
}
