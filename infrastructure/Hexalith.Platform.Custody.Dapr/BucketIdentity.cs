using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record BucketIdentity(string Name, string OwnerAccountId, string EnrollmentKey, string EnrollmentVersion,
    string EnrollmentSha256);
