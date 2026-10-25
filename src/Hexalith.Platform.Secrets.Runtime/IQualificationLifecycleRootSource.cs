using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Private original root delivery for the exact independently enrolled canary. A source owns every returned array; the workflow clears it.</summary>
public interface IQualificationLifecycleRootSource
{
    /// <summary>Resolves the exact current root under its selected native holder-admission protocol, or returns null.</summary>
    Task<byte[]?> ReadAsync(CustodyKeyRegistration registration,CustodyKeyUse use,CancellationToken cancellationToken);
}
