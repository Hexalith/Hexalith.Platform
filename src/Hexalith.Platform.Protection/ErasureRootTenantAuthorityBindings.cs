using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Five independently enrolled exact grants for one tenant. Credentials must be disjoint across tenants.</summary>
/// <param name="PrivateAuthority">Current private permissions.</param><param name="SourceAuthority">Exact source and writer originals.</param>
/// <param name="GuardAuthority">Exact governance owner.</param><param name="RegistrarAuthority">Private compromise registrar.</param><param name="EvidenceAuthority">Current protocol evidence.</param>
public sealed record ErasureRootTenantAuthorityBindings(IndependentDaprAuthorityBinding PrivateAuthority, IndependentDaprAuthorityBinding SourceAuthority,
    IndependentDaprAuthorityBinding GuardAuthority, IndependentDaprAuthorityBinding RegistrarAuthority, IndependentDaprAuthorityBinding EvidenceAuthority);
