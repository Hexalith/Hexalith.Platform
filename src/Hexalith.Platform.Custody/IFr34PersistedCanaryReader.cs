namespace Hexalith.Platform.Custody;

/// <summary>Independent actual storage inspection, with no encryption, identity self-report or supplied seal buffer fallback.</summary>
public interface IFr34PersistedCanaryReader
{
    /// <summary>Reads bytes and metadata of the original exact committed record; unknown/unpersisted sources return no evidence.</summary>
    Task<Fr34PersistedCanary?> ReadAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default);
}
