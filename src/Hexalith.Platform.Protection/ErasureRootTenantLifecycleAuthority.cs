using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Exact private lifecycle actor authority routing. Only enrolled tenant/owner scopes select an authority.</summary>
public sealed class ErasureRootTenantLifecycleAuthority : ICustodyKeyLifecycleAuthority
{
    private readonly IReadOnlyDictionary<string, ErasureRootLifecycleAuthority> _authorities;
    /// <summary>Builds exact authorities from the accepted installation and same-ERC anchors.</summary>
    public ErasureRootTenantLifecycleAuthority(ErasureRootCustody custody, IErasureRootAuthorization authorization)
        => _authorities = custody.Installation.NativeKeys.Keys.ToDictionary(tenant => tenant,
            tenant => new ErasureRootLifecycleAuthority(custody, authorization, new(custody, authorization, tenant, Scope(tenant))), StringComparer.Ordinal);
    /// <summary>Exact nonrollback lifecycle actor scope, including the actor's actual state key.</summary>
    public static string Scope(string tenant) => CustodyKeyLifecycleActor.GetActorId(tenant) + "|custody-key-lifecycle-candidate-v1";
    private ErasureRootLifecycleAuthority? Tenant(string tenant)
        => new AggregateIdentity(tenant, "custody", "key-lifecycle-v1").TenantId == tenant && _authorities.TryGetValue(tenant, out var owner) ? owner : null;
    private ErasureRootLifecycleAuthority? Owner(string scope)
    { var selected = _authorities.Keys.SingleOrDefault(tenant => Scope(tenant) == scope); return selected is null ? null : _authorities[selected]; }
    /// <inheritdoc />
    public Task<bool> AuthorizeOperationAsync(CustodyKeyObjectIdentity identity, string id, string method, string digest, CancellationToken token = default)
        => Tenant(identity.TenantId)?.AuthorizeOperationAsync(identity, id, method, digest, token) ?? Task.FromResult(false);
    /// <inheritdoc />
    public Task<bool> VerifyRegistrationAsync(CustodyKeyRegistration registration, CancellationToken token = default)
        => Tenant(registration.Identity.TenantId)?.VerifyRegistrationAsync(registration, token) ?? Task.FromResult(false);
    /// <inheritdoc />
    public Task<bool> AuthorizeEffectAsync(CustodyKeyLifecycleRequest request, CancellationToken token = default)
        => Tenant(request.Identity.TenantId)?.AuthorizeEffectAsync(request, token) ?? Task.FromResult(false);
    /// <inheritdoc />
    public Task<bool> VerifyOutcomeAsync(CustodyKeyLifecycleRequest request, CustodyKeyLifecycleOutcome outcome, CancellationToken token = default)
        => Tenant(request.Identity.TenantId)?.VerifyOutcomeAsync(request, outcome, token) ?? Task.FromResult(false);
    /// <inheritdoc />
    public Task<bool> ValidateStateAsync(string tenant, long revision, string digest, CancellationToken token = default)
        => Tenant(tenant)?.ValidateStateAsync(tenant, revision, digest, token) ?? Task.FromResult(false);
    /// <inheritdoc />
    public Task<bool> RecordRevisionAsync(string tenant, long expected, long next, string digest, CancellationToken token = default) => Task.FromResult(false);
    /// <inheritdoc />
    public Task<AnchoredStateTransition?> ReadCurrentOriginalAsync(string scope, CancellationToken token = default)
        => Owner(scope)?.ReadCurrentOriginalAsync(scope, token) ?? Task.FromResult<AnchoredStateTransition?>(null);
    /// <inheritdoc />
    public Task<AnchoredStateTransition?> ReadAdmittedOriginalAsync(string scope, CancellationToken token = default)
        => Owner(scope)?.ReadAdmittedOriginalAsync(scope, token) ?? Task.FromResult<AnchoredStateTransition?>(null);
    /// <inheritdoc />
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken token = default)
        => Owner(transition.ScopeId)?.AdmitTransitionAsync(transition, token) ?? Task.FromResult(false);
    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken token = default)
        => Owner(transition.ScopeId)?.RecoverTransitionAsync(transition, token) ?? Task.FromResult(false);
    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken token = default)
        => Owner(transition.ScopeId)?.RecordTransitionAsync(transition, token) ?? Task.FromResult(false);
    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken token = default)
        => Owner(transition.ScopeId)?.VerifyTransitionAsync(transition, token) ?? Task.FromResult(false);
}
