using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Authenticates the actual source, then independently retains its exact evidence before the subscriber can register a compromise. A lost publication returns unknown and never permits registration.</summary>
public sealed class ErasureRootRecordingRevocationAuthenticator(IDeletionCapabilityRevocationAuthenticator actual, IndependentDaprAuthority registrar)
    : IDeletionCapabilityRevocationAuthenticator
{
    /// <inheritdoc />
    public async Task<DeletionCapabilityRevocationAuthorization?> VerifyAsync(DeletionCapabilityRevocationEnvelope envelope, string signedEvidence, CancellationToken cancellationToken)
    {
        var verified = await actual.VerifyAsync(envelope, signedEvidence, cancellationToken).ConfigureAwait(false);
        if (verified?.Envelope != envelope) { return null; }
        bool retained = await registrar.InvokeAsync<bool>("erc-register-revocation-original", new { tenantId = envelope.TenantId,
            original = new ErasureRootRevocationOriginal(envelope, signedEvidence) }, cancellationToken).ConfigureAwait(false);
        return retained ? verified : null;
    }
}
