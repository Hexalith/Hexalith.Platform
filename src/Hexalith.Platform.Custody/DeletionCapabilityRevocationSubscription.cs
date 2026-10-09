namespace Hexalith.Platform.Custody;

/// <summary>Exact independently enrolled revocation subscriber scope; a configuration declaration is not authentication.</summary>
/// <param name="Issuer">Accepted independent revocation source.</param>
/// <param name="Audience">Exact committed protection registrar.</param>
/// <param name="TenantId">Exact target tenant.</param>
public sealed record DeletionCapabilityRevocationSubscription(string Issuer, string Audience, string TenantId);
