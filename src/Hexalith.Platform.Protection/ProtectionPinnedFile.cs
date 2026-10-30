using System.Security.Cryptography;

namespace Hexalith.Platform.Protection;

/// <summary>Acquires one bounded file buffer from a single open, including growth and pin checks.</summary>
internal static class ProtectionPinnedFile
{
    internal static byte[] Read(string path, int maximumBytes, string? digest = null, Action? opened = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || new FileInfo(path).LinkTarget is not null
            || digest is not null && (digest.Length != 64 || digest.Any(value => !char.IsAsciiHexDigit(value))))
        { throw new InvalidOperationException("Exact bounded pinned input required."); }
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 8192, FileOptions.SequentialScan);
        if (stream.Length is <= 0 || stream.Length > maximumBytes) { throw new InvalidOperationException("Bounded pinned input required."); }
        byte[] bytes = new byte[checked((int)stream.Length)];
        try
        {
            opened?.Invoke();
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1 || digest is not null && !Convert.ToHexString(SHA256.HashData(bytes)).Equals(digest, StringComparison.OrdinalIgnoreCase))
            { throw new InvalidOperationException("Pinned input changed."); }
            return bytes;
        }
        catch { CryptographicOperations.ZeroMemory(bytes); throw; }
    }
}
