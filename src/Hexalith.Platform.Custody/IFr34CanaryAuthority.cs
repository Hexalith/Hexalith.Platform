namespace Hexalith.Platform.Custody;

/// <summary>Independent exact production target qualification and irreversible canary destruction evidence.</summary>
public interface IFr34CanaryAuthority
{
    /// <summary>Authenticates currently installed exact engine/version/custody bindings, independently of engine self-report.</summary>
    Task<Fr34CanaryAuthorization?> ObserveAsync(Fr34ProtectionTarget expected, CancellationToken cancellationToken = default);
    /// <summary>Authenticates complete irreversible original canary DEK destruction at the enrolled custody owner.</summary>
    Task<bool> ConfirmDestroyedAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default);
}
