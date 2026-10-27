using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Private exact replay admission through an independently enrolled conditional owner.</summary>
public sealed class DaprIndependentReplayRegistrar(IndependentDaprAuthority authority) : ITrustedEnvelopeReplayRegistrar
{
    /// <inheritdoc />
    public async Task<TrustedEnvelopeReplayReceipt?> RegisterAsync(TrustedEnvelopeReplayIntent intent, DateTimeOffset evaluatedAt,
        TimeSpan retention, CancellationToken cancellationToken = default)
    {
        if (retention < TimeSpan.FromSeconds(604800) || evaluatedAt.Offset != TimeSpan.Zero) return null;
        var result = await authority.InvokeAsync<TrustedEnvelopeReplayReceipt>("replay-register-exact", new { intent, evaluatedAt, retentionTicks = retention.Ticks }, cancellationToken).ConfigureAwait(false);
        return result?.Intent == intent && result.SourceRevision == 1 && result.RetainUntil == result.FirstSeenAt.Add(retention) ? result : null;
    }
    /// <inheritdoc />
    public async Task<TrustedEnvelopeReplayReceipt?> LookupFirstSeenAsync(TrustedEnvelopeReplayIntent intent,CancellationToken cancellationToken=default)
    {
        var result=await authority.InvokeAsync<TrustedEnvelopeReplayReceipt>("replay-lookup-first-seen",new { intent },cancellationToken).ConfigureAwait(false);
        return result?.Intent.Issuer==intent.Issuer && result.Intent.DeliveryNonce==intent.DeliveryNonce && result.SourceRevision==1?result:null;
    }
    /// <inheritdoc />
    public async Task<TrustedEnvelopeReplayReceipt?> LookupAsync(TrustedEnvelopeReplayIntent intent, CancellationToken cancellationToken = default)
    {
        var result = await authority.InvokeAsync<TrustedEnvelopeReplayReceipt>("replay-lookup-exact", new { intent }, cancellationToken).ConfigureAwait(false);
        return result?.Intent == intent && result.SourceRevision == 1 ? result : null;
    }
}
