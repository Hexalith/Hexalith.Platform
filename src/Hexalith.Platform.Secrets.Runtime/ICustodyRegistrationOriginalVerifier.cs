using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Authenticates a complete registration against the actual component's durable wrap original.</summary>
public interface ICustodyRegistrationOriginalVerifier
{
    /// <summary>Verifies the original without performing native key work or trusting index facts.</summary>
    Task<bool> VerifyAsync(CustodyKeyRegistration original, CancellationToken cancellationToken);
}
