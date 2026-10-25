using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

// The payload is a closed typed claim per purpose, never a caller 'verified=true' field.
internal sealed record SignedStatement(string Purpose, string SignerId, string AuthorityId, string AuthorityIncarnation,
    string TenantId, long Revision, string CheckpointDigest, string SubjectSha256, DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt, JsonElement Payload, byte[] Signature);
