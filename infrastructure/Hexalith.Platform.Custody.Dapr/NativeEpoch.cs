using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record NativeEpoch(string OpaqueId, string KeyArn, string KeyId, string TenantId);
