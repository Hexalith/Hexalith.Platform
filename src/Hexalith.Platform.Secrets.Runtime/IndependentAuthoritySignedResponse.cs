using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>The existing independent authority signed response codec; contains no transport credential.</summary>
public sealed record IndependentAuthoritySignedResponse(string EnrollmentId,string Scope,string TrustRevision,string Method,string Nonce,
    string RequestSha256,long ObservedAt,long ValidUntil,JsonElement Payload,byte[] Signature);
