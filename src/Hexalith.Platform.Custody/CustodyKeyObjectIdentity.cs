namespace Hexalith.Platform.Custody;

/// <summary>Complete purpose-separated wrapped-key object identity; only opaque references cross domain boundaries.</summary>
/// <param name="TenantId">Exact tenant.</param><param name="ObjectId">Exact export or interaction identity.</param><param name="Purpose">Only ExportEnvelopeKey or InteractionRootDek.</param>
/// <param name="KeyAlias">Explicit complete-object alias.</param><param name="KeyVersion">Immutable original root/envelope version.</param><param name="LifecycleVersion">Approved phase-pinned lifecycle.</param>
/// <param name="StoreTarget">Exact physical key/copy owner.</param><param name="ContractVersion">Exact closed custody contract.</param>
public sealed record CustodyKeyObjectIdentity(string TenantId, string ObjectId, PlatformKeyPurpose Purpose, string KeyAlias, string KeyVersion,
    string LifecycleVersion, string StoreTarget, string ContractVersion);
