using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Exact retained full-root identity lookup from a source independent of caller-supplied lifecycle/store fields.</summary>
public interface IInteractionRootRegistrationSource
{
    /// <summary>Reads only the unchanged complete original registration for this target/version.</summary>
    Task<CustodyKeyRegistration?> FindRootAsync(ProtectionTarget target,string rootVersion,CancellationToken cancellationToken);
}
