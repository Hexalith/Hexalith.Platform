using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independently enrolled public service identity. Credentials are mounted references, never DI registrations.</summary>
public sealed record IndependentDaprAuthorityBinding(string AppId, string CredentialReference, string PublicKeyPem,
    string EnrollmentId, string Scope, string CurrentTrustRevision);
