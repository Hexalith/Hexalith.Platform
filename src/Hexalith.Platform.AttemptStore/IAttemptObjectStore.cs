namespace Hexalith.Platform.AttemptStore;

/// <summary>Strong current reads and one conditional write per call.</summary>
public interface IAttemptObjectStore
{
    /// <summary>Reads the current bytes and their opaque ETag. Null means confirmed absence.</summary>
    Task<AttemptObject?> ReadAsync(string key, int maximumBytes, CancellationToken cancellationToken);

    /// <summary>Creates an absent object. False means a confirmed conditional conflict.</summary>
    Task<bool> CreateAsync(string key, byte[] bytes, CancellationToken cancellationToken);

    /// <summary>Replaces exactly the object with the observed ETag. False means a confirmed conditional conflict.</summary>
    Task<bool> ReplaceAsync(string key, string etag, byte[] bytes, CancellationToken cancellationToken);
}

/// <summary>An object and the ETag of the same read.</summary>
public sealed record AttemptObject(byte[] Bytes, string ETag);

/// <summary>The provider could not confirm the result of a write. Reconcile against a fresh read.</summary>
public sealed class AttemptStoreWriteUncertainException : IOException
{
    public AttemptStoreWriteUncertainException(Exception innerException)
        : base("The attempt store could not confirm a conditional write.", innerException)
    {
    }
}
