using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Current independent bounded recipe producer; implementations must retain claims and release admissions before native delivery.</summary>
public interface IFr34OccurrenceEnrollment
{
    /// <summary>Finds or atomically derives the exact claim from an independently enrolled finite parent. Missing native inputs deny.</summary>
    Task<Fr34OccurrenceClaim?> ResolveAsync(AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence,CancellationToken token);
    /// <summary>Claims a once-only encryption/completion lease under current independent authorization.</summary>
    Task<bool> AcquireLeaseAsync(InteractionProtectionBinding binding,bool completion,CancellationToken token);
    /// <summary>Returns the exact pending read use, or the next pre-admitted use only after actual prior completion.</summary>
    Task<CustodyInteractionMaterialProfile?> MaterialAsync(InteractionOccurrenceIdentity identity,bool writing,CancellationToken token);
    /// <summary>Durably admits one release only. An uncertain previous release cannot be repeated.</summary>
    Task<bool> AdmitReleaseAsync(InteractionOccurrenceIdentity identity,CustodyInteractionMaterialProfile profile,CancellationToken token);
    /// <summary>Enumerates the finite retained pending native holders for exact completion recovery.</summary>
    Task<IReadOnlyList<CustodyInteractionMaterialProfile>> PendingAsync(CancellationToken token);
    /// <summary>Reads exact independently authenticated original destruction/barrier evidence, without repeating destruction or releasing material.</summary>
    Task<InteractionProtectionErasureWitness?> ReadErasureAsync(InteractionProtectionBinding binding,CancellationToken token)=>Task.FromResult<InteractionProtectionErasureWitness?>(null);
}
