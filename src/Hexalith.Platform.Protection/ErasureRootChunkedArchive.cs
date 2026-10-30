namespace Hexalith.Platform.Protection;

/// <summary>Bounded immutable same-ERC framing for an authenticated large original; only a tenant-CAS pointer grants authority.</summary>
internal sealed record ErasureRootChunkedArchive(string Format, int Length, string[] Chunks);
