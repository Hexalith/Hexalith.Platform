namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact immutable content-addressed native archive, outside the application restore cut.</summary>
public interface IIndependentContentArchive
{
    /// <summary>Durably publishes bytes and returns their SHA-256 address; conflicting bytes can never replace an address.</summary>
    Task<string> PublishAsync(ReadOnlyMemory<byte> bytes,CancellationToken cancellationToken);
    /// <summary>Reads exact digest-verified bytes; missing/corrupt data is unknown, never an absent logical original.</summary>
    Task<byte[]> ReadAsync(string sha256,CancellationToken cancellationToken);
}
