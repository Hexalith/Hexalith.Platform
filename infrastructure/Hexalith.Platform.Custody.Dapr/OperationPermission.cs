using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record OperationPermission(string Operation, string OperationId, string RequestAttemptId, string Challenge,
    string OwnerPurposeScope, string ProfileDigest);
