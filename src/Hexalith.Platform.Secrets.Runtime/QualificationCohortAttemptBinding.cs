namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independently enrolled current cohort authority and exact accepted set. Recipes contain no predicted or reserved attempt identity.</summary>
public sealed record QualificationCohortAttemptBinding(string AppId,string CredentialReference,string PublicKeyPem,string PublicAnchorSha256,string BindingDigest);
