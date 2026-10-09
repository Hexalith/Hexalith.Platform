using System.Security.Cryptography;

namespace Hexalith.Platform.Custody;

/// <summary>Authenticates delivery, registers original first-seen evidence, then reconfirms authentication before any consumer constructs a target command.</summary>
/// <param name="authenticator">Cryptographic/profile/time verifier.</param><param name="registrar">Private exact first-seen capability.</param>
/// <param name="profiles">Independent current profile authority.</param><param name="clock">Whole-operation budget and original evaluation clock.</param>
/// <remarks>A success still requires consuming domain authorization and idempotency. No target handler, dispatcher, Workflow or public service receives the registrar.</remarks>
public sealed class TrustedEnvelopeReplayVerifier(TrustedEnvelopeAuthenticator authenticator, ITrustedEnvelopeReplayRegistrar registrar,
    IPlatformSigningProfileProvider profiles, TimeProvider clock)
{
    /// <summary>Returns an authenticated envelope only after a complete exact original replay receipt and final current crypto/time/profile checks.</summary>
    public async Task<TrustedEnvelopeResult> VerifyAsync(TrustedEnvelope envelope, TrustedEnvelopeIdentity expected, CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        byte[]? canonical = null; byte[]? tag = null; byte[]? complete = null;
        try
        {
            budget.Check();
            var profile = profiles.GetCurrent();
            if (profile is null || !profile.IsValid(clock.GetUtcNow())) { return new(CustodyStatus.StaleProfile); }
            var authenticated = await budget.ReadAsync(() => authenticator.VerifyAsync(envelope, expected, CancellationToken.None)).ConfigureAwait(false);
            if (authenticated.Status != CustodyStatus.Succeeded || authenticated.Envelope is not { } captured) { return authenticated; }
            DateTimeOffset evaluatedAt = clock.GetUtcNow();
            canonical = TrustedEnvelopeCodec.Delivery(captured); tag = Convert.FromHexString(captured.Tag);
            complete = new byte[checked(canonical.Length + tag.Length)]; canonical.CopyTo(complete, 0); tag.CopyTo(complete, canonical.Length);
            var intent = new TrustedEnvelopeReplayIntent(captured.Identity.Issuer, captured.DeliveryNonce, captured.Identity.TargetTenantId,
                captured.Identity.LogicalCommandId, captured.SigningKeyVersion, Convert.ToHexString(SHA256.HashData(complete)));
            TrustedEnvelopeReplayReceipt? receipt;
            try { receipt = await budget.ReadAsync(() => registrar.RegisterAsync(intent, evaluatedAt, profile.ReplayRetention, CancellationToken.None)).ConfigureAwait(false); }
            catch (Exception) { budget.Check(); receipt = null; }
            if (receipt is null)
            { receipt = await budget.ReadAsync(() => registrar.LookupAsync(intent, CancellationToken.None)).ConfigureAwait(false); }
            if (!Exact(receipt, intent, evaluatedAt, profile.ReplayRetention)) { return new(CustodyStatus.Unavailable); }
            var confirmed = await budget.ReadAsync(() => registrar.LookupAsync(intent, CancellationToken.None)).ConfigureAwait(false);
            if (confirmed != receipt) { return new(CustodyStatus.Unavailable); }
            var final = await budget.ReadAsync(() => authenticator.VerifyAsync(captured, expected, CancellationToken.None)).ConfigureAwait(false);
            budget.Check();
            return profiles.GetCurrent() == profile && profile.IsValid(clock.GetUtcNow()) && Exact(receipt, intent, clock.GetUtcNow(), profile.ReplayRetention)
                ? final : new(CustodyStatus.StaleProfile);
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return new(CustodyStatus.Unavailable); }
        finally
        {
            if (canonical is not null) { CryptographicOperations.ZeroMemory(canonical); }
            if (tag is not null) { CryptographicOperations.ZeroMemory(tag); }
            if (complete is not null) { CryptographicOperations.ZeroMemory(complete); }
        }
    }
    private static bool Exact(TrustedEnvelopeReplayReceipt? receipt, TrustedEnvelopeReplayIntent intent, DateTimeOffset now, TimeSpan retention)
        => receipt is not null && receipt.Intent == intent && receipt.FirstSeenAt != default && receipt.FirstSeenAt <= now
            && receipt.FirstSeenAt.Offset == TimeSpan.Zero && receipt.RetainUntil.Offset == TimeSpan.Zero && RetentionExact(receipt, retention) && receipt.RetainUntil > now && receipt.SourceRevision == 1
            && !string.IsNullOrWhiteSpace(receipt.ReceiptId) && receipt.ReceiptId.Length <= 2048;
    private static bool RetentionExact(TrustedEnvelopeReplayReceipt receipt, TimeSpan retention)
    { try { return receipt.RetainUntil == receipt.FirstSeenAt.Add(retention); } catch (ArgumentOutOfRangeException) { return false; } }
}
