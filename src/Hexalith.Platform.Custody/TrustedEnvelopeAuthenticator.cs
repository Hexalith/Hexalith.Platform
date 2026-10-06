using System.Security.Cryptography;

namespace Hexalith.Platform.Custody;

/// <summary>Cryptographic envelope authentication only; the consuming verifier must register replay before dispatch.</summary>
public sealed class TrustedEnvelopeAuthenticator(IPlatformHmacKeyProvider keys, IPlatformSigningProfileProvider profiles, TimeProvider clock)
{
    /// <summary>Signs a freshly nonced delivery under the current exact tenant key.</summary>
    public Task<TrustedEnvelopeResult> IssueAsync(TrustedEnvelopeIdentity identity, TimeSpan lifetime,
        CancellationToken cancellationToken = default)
        => AuthenticateAsync(identity, lifetime, null, cancellationToken);
    /// <summary>Verifies against independently supplied exact identity; success confers no domain permission.</summary>
    public Task<TrustedEnvelopeResult> VerifyAsync(TrustedEnvelope envelope, TrustedEnvelopeIdentity expected,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return AuthenticateAsync(expected, default, envelope, cancellationToken);
    }
    private async Task<TrustedEnvelopeResult> AuthenticateAsync(TrustedEnvelopeIdentity identity, TimeSpan lifetime,
        TrustedEnvelope? input, CancellationToken token)
    {
        try
        {
            TrustedEnvelopeResult result = await AuthenticateCoreAsync(identity, lifetime, input, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException)
        {
            token.ThrowIfCancellationRequested();
            return new(CustodyStatus.Invalid);
        }
        catch (Exception)
        {
            token.ThrowIfCancellationRequested();
            return new(CustodyStatus.Unavailable);
        }
    }
    private async Task<TrustedEnvelopeResult> AuthenticateCoreAsync(TrustedEnvelopeIdentity expected, TimeSpan lifetime,
        TrustedEnvelope? input, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(expected);
        token.ThrowIfCancellationRequested();
        PlatformSigningProfile? profile = profiles.GetCurrent();
        if (profile is null || !profile.IsValid(clock.GetUtcNow()))
        {
            return new(CustodyStatus.StaleProfile);
        }
        TrustedEnvelopeIdentity identity;
        try
        {
            expected = TrustedEnvelopeCodec.Snapshot(expected);
            identity = TrustedEnvelopeCodec.Snapshot(input?.Identity ?? expected);
            if (!TrustedEnvelopeCodec.IsValid(expected, profile) || !TrustedEnvelopeCodec.IsValid(identity, profile))
            {
                return new(CustodyStatus.Invalid);
            }
        }
        catch (ArgumentException)
        {
            return new(CustodyStatus.Invalid);
        }
        byte[] expectedBytes = TrustedEnvelopeCodec.Identity(expected);
        byte[]? identityBytes = null;
        try
        {
            identityBytes = TrustedEnvelopeCodec.Identity(identity);
            if (!expectedBytes.AsSpan().SequenceEqual(identityBytes))
            {
                return new(CustodyStatus.ScopeMismatch);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedBytes);
            if (identityBytes is not null)
            {
                CryptographicOperations.ZeroMemory(identityBytes);
            }
        }
        bool issue = input is null;
        DateTimeOffset now = clock.GetUtcNow();
        if (issue && (lifetime <= TimeSpan.Zero || lifetime > profile.MaximumLifetime))
        {
            return new(CustodyStatus.Invalid);
        }
        if (!issue && CheckTime(input!, profile, now) is
        { }
        failure && failure != CustodyStatus.Succeeded)
        {
            return new(failure);
        }
        var scope = new PlatformHmacScope(identity.TargetTenantId, PlatformHmacPurpose.TrustedEnvelope);
        PlatformHmacKeyResolution first = await PlatformKeyResolution.ReadAsync(keys, scope, input?.SigningKeyVersion, token).ConfigureAwait(false);
        using PlatformHmacKeySnapshot? key = first.Key;
        CustodyStatus status = PlatformKeyResolution.Check(first, scope, input?.SigningKeyVersion, clock.GetUtcNow(), issue, profile.RotationOverlap);
        if (status != CustodyStatus.Succeeded)
        {
            return new(status);
        }
        TrustedEnvelope envelope;
        try
        {
            now = clock.GetUtcNow();
            envelope = input is null ? new(identity, now, now.Add(lifetime), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), key!.Metadata.Version, string.Empty)
                : input with { Identity = identity };
        }
        catch (ArgumentOutOfRangeException)
        {
            return new(CustodyStatus.Invalid);
        }
        byte[] canonical = TrustedEnvelopeCodec.Delivery(envelope);
        byte[] tag = [];
        try
        {
            tag = key!.ComputeTag(canonical);
            if (!issue)
            {
                byte[] supplied;
                try
                {
                    supplied = Convert.FromHexString(envelope.Tag);
                }
                catch (FormatException)
                {
                    return new(CustodyStatus.InvalidTag);
                }
                try
                {
                    if (!CryptographicOperations.FixedTimeEquals(tag, supplied))
                    {
                        return new(CustodyStatus.InvalidTag);
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(supplied);
                }
            }
            PlatformHmacKeyResolution fresh = await PlatformKeyResolution.ReadAsync(keys, scope, issue ? null : envelope.SigningKeyVersion, token).ConfigureAwait(false);
            using PlatformHmacKeySnapshot? current = fresh.Key;
            status = PlatformKeyResolution.Check(fresh, scope, envelope.SigningKeyVersion, clock.GetUtcNow(), issue, profile.RotationOverlap);
            if (status != CustodyStatus.Succeeded)
            {
                return new(status);
            }
            byte[] confirmation = current!.ComputeTag(canonical);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(tag, confirmation))
                {
                    return new(CustodyStatus.Unavailable);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(confirmation);
            }
            token.ThrowIfCancellationRequested();
            if (profiles.GetCurrent() != profile || !profile.IsValid(clock.GetUtcNow()))
            {
                return new(CustodyStatus.StaleProfile);
            }
            status = PlatformKeyResolution.Check(fresh, scope, envelope.SigningKeyVersion, clock.GetUtcNow(), issue, profile.RotationOverlap);
            if (status != CustodyStatus.Succeeded)
            {
                return new(status);
            }
            status = CheckTime(envelope, profile, clock.GetUtcNow());
            token.ThrowIfCancellationRequested();
            return status == CustodyStatus.Succeeded ? new(status, envelope with { Tag = Convert.ToHexStringLower(tag) }) : new(status);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
            CryptographicOperations.ZeroMemory(tag);
        }
    }
    private static CustodyStatus CheckTime(TrustedEnvelope envelope, PlatformSigningProfile profile, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(envelope.DeliveryNonce) || string.IsNullOrWhiteSpace(envelope.SigningKeyVersion)
            || envelope.ExpiresAt <= envelope.IssuedAt || envelope.ExpiresAt - envelope.IssuedAt > profile.MaximumLifetime)
        {
            return CustodyStatus.Invalid;
        }
        if (envelope.IssuedAt > now && envelope.IssuedAt - now > profile.ClockSkew)
        {
            return CustodyStatus.FutureIssued;
        }
        if (now >= envelope.ExpiresAt)
        {
            return CustodyStatus.Expired;
        }
        return CustodyStatus.Succeeded;
    }
}
