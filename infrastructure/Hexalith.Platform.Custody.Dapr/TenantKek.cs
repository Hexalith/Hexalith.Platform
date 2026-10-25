using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record TenantKek(string TenantId, string Version, string ProtectorArn, string ProtectorKeyId, byte[] EncryptedKey);
