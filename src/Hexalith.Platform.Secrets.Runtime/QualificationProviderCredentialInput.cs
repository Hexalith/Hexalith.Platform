using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact independently enrolled private credential reference. This record contains no credential value.</summary>
public sealed record QualificationProviderCredentialInput(PlatformKeyVersion Key,string SecretStore,string SecretReference,string SecretField,string SecretVersion,string EnrolledScopeSha256);
