namespace Hexalith.Platform.Protection;

/// <summary>Native KV v2 CAS version and exact head bytes; null means unavailable or missing enrollment, never authorized genesis.</summary>
/// <param name="Version">Native KV version used for the conditional write.</param>
/// <param name="Bytes">Exact serialized tenant head.</param>
public sealed record ErasureRootHeadSnapshot(long Version, byte[] Bytes);
