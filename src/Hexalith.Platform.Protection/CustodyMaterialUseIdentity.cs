using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Preserves the original native holder-completion identity across producer/provider restart and distinct per-read deliveries.</summary>
internal static class CustodyMaterialUseIdentity
{
    internal static string Digest(CustodyKeyUse use)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(use)));
}
