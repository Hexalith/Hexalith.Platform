namespace Hexalith.Platform.Custody;

/// <summary>Independent actual storage inspection, with no encryption, identity self-report or supplied seal buffer fallback.</summary>
public interface IFr34PersistedCanaryReader
{
    /// <summary>Reads the original exact committed record; unknown/unpersisted sources return no evidence.</summary>
    /// <remarks>The returned byte array is newly detached and transfers to the caller. The reader must not retain, share, or mutate it after completion.</remarks>
    Task<Fr34PersistedCanary?> ReadAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default);
}
