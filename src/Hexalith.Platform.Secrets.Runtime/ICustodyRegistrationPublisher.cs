using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Publishes a verified retained wrap original without allocating or wrapping a key.</summary>
public interface ICustodyRegistrationPublisher
{
    /// <summary>Returns the unchanged independently retained registration, or unknown after refusal or lost acknowledgement.</summary>
    Task<CustodyKeyRegistration?> PublishAsync(CustodyKeyRegistration original, CancellationToken cancellationToken);
}
