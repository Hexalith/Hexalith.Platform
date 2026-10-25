using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact current HMAC identity and separately enrolled foreign scope for negative resolution cases.</summary>
public sealed record QualificationHmacResolutionCaseInput(PlatformHmacScope Scope,string Version,string ForeignTenantId,PlatformHmacPurpose ForeignPurpose);
