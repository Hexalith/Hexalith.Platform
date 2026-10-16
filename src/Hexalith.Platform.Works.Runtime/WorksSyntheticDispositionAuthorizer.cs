using System.Security.Claims;

using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.Platform.Works.Runtime;

/// <summary>Exact, explicitly installed grants for disposable Development qualification only.</summary>
public sealed class WorksSyntheticDispositionAuthorizer : IProcessDispositionAuthorizer
{
    private readonly HashSet<ProcessSource> _sources;
    private readonly string _operatorWorkload;

    /// <summary>Copies a bounded source grant set; nothing is discovered or inferred.</summary>
    /// <param name="explicitGrants">Exact canonical tenant/domain/aggregate grants.</param>
    /// <param name="operatorWorkload">The authenticated synthetic operator workload.</param>
    public WorksSyntheticDispositionAuthorizer(IEnumerable<ProcessSource> explicitGrants, string operatorWorkload)
    {
        ArgumentNullException.ThrowIfNull(explicitGrants);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorWorkload);
        _sources = [];
        foreach (ProcessSource source in explicitGrants.Take(65))
        {
            ArgumentNullException.ThrowIfNull(source);
            source.Validate();
            if (!_sources.Add(source)) throw new ArgumentException("Duplicate synthetic source grant.", nameof(explicitGrants));
        }
        if (_sources.Count is < 1 or > 64) throw new ArgumentException("Synthetic qualification requires 1..64 exact source grants.", nameof(explicitGrants));
        _operatorWorkload = operatorWorkload;
    }

    /// <inheritdoc/>
    public Task<bool> AuthorizeAsync(ClaimsPrincipal principal, ProcessSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        source.Validate();
        ClaimsIdentity[] identities = [.. principal.Identities.Where(identity => identity.IsAuthenticated)];
        if (identities.Length != 1 || !_sources.Contains(source)) return Task.FromResult(false);
        ClaimsIdentity identity = identities[0];
        string? actor = Single(identity, EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType);
        return Task.FromResult(
            Single(identity, ClaimTypes.NameIdentifier) == "workload:" + _operatorWorkload
            && Single(identity, EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType) == _operatorWorkload
            && identity.FindAll(EventStoreWorkloadAuthenticationDefaults.OperationClaimType).Any(claim => claim.Value == "process:dispose")
            && Single(identity, EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType) == source.Tenant
            && Single(identity, EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType) == source.Domain
            && Single(identity, EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "aggregate") == source.Aggregate
            && Single(identity, EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "purpose") == "works.process-disposition"
            && !string.IsNullOrWhiteSpace(actor) && actor.Length <= 128);
    }

    private static string? Single(ClaimsIdentity identity, string type)
    {
        Claim[] claims = [.. identity.FindAll(type).Take(2)];
        return claims.Length == 1 ? claims[0].Value : null;
    }
}
