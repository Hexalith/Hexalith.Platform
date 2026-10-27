using System.Text;

namespace Hexalith.Platform.Custody;

/// <summary>Bounded content-free receiving fields without a computed address getter. Structurally rejected candidates can cross the actual original-target receiver.</summary>
public sealed record ExportKeyDeliveryRawIdentity(string TenantId,string DeliveryId,string ExportId,long CommitAcknowledgementRevision,
    string RequesterActorId,string Purpose,string Audience,DateTimeOffset ExclusiveExpiry,string LifecycleVersion,string StoreTarget,int ContractVersion)
{
    /// <summary>Captures fields without invoking address/contract validation or releasing material.</summary>
    public static ExportKeyDeliveryRawIdentity Capture(ExportKeyDeliveryIdentity value)=>new(value.TenantId,value.DeliveryId,value.ExportId,value.CommitAcknowledgementRevision,value.RequesterActorId,value.Purpose,value.Audience,value.ExclusiveExpiry,value.LifecycleVersion,value.StoreTarget,value.ContractVersion);
    internal ExportKeyDeliveryIdentity Identity()=>new(TenantId,DeliveryId,ExportId,CommitAcknowledgementRevision,RequesterActorId,Purpose,Audience,ExclusiveExpiry,LifecycleVersion,StoreTarget,ContractVersion);
    internal void ValidateBounds()
    {
        foreach(string value in new[]{TenantId,DeliveryId,ExportId,RequesterActorId,Purpose,Audience,LifecycleVersion,StoreTarget})
            if(string.IsNullOrWhiteSpace(value) || value.Length>2048 || new UTF8Encoding(false,true).GetByteCount(value)>2048) throw new ArgumentException("Bounded receiving identity required.");
    }
}
