using System.Security.Cryptography;

namespace Hexalith.Platform.Custody;

/// <summary>Cryptographic envelope authentication only; the consuming verifier must register replay before dispatch.</summary>
public sealed class TrustedEnvelopeAuthenticator(IPlatformHmacKeyProvider keys, IPlatformSigningProfileProvider profiles, TimeProvider clock)
{
    /// <summary>Signs a freshly nonced delivery under the current exact tenant key.</summary>
    public Task<TrustedEnvelopeResult> IssueAsync(TrustedEnvelopeIdentity identity, TimeSpan lifetime,
        CancellationToken cancellationToken = default)
        => AuthenticateAsync(identity, lifetime, null, cancellationToken);
    // Read-only readiness for the independently enrolled qualification issuance recipe; it creates no nonce or signature.
    internal async Task<bool> CanIssueChallengeAsync(TrustedEnvelopeIdentity identity,CancellationToken token)
    {
        var profile=profiles.GetCurrent();if(profile is null || !TrustedEnvelopeCodec.IsValid(identity,profile) || !profile.IsValid(clock.GetUtcNow())) return false;
        var scope=new PlatformHmacScope(identity.TargetTenantId,PlatformHmacPurpose.TrustedEnvelope);
        var resolution=await PlatformKeyResolution.ReadAsync(keys,scope,null,token).ConfigureAwait(false);using var key=resolution.Key;
        var current=profiles.GetCurrent();DateTimeOffset at=clock.GetUtcNow();token.ThrowIfCancellationRequested();
        return current==profile && profile.IsValid(at) && PlatformKeyResolution.Check(resolution,scope,null,at,true,profile.RotationOverlap)==CustodyStatus.Succeeded;
    }
    // Only the source-owned qualification assembly can issue a nonce-bound challenge under its separately scoped current key authority.
    internal Task<TrustedEnvelopeResult> IssueNonceBoundChallengeAsync(TrustedEnvelopeIdentity identity,TimeSpan lifetime,string originalNonce,CancellationToken token)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(originalNonce,"^[0-9a-f]{64}$")) return Task.FromResult(new TrustedEnvelopeResult(CustodyStatus.Invalid,EvaluatedAt:clock.GetUtcNow()));
        return AuthenticateAsync(identity,lifetime,null,token,originalNonce);
    }
    /// <summary>Verifies against independently supplied exact identity; success confers no domain permission.</summary>
    public Task<TrustedEnvelopeResult> VerifyAsync(TrustedEnvelope envelope, TrustedEnvelopeIdentity expected,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return AuthenticateAsync(expected, default, envelope, cancellationToken);
    }
    // The replay owner's separate current-profile provider is sampled at the
    // final owned decision, inside that owner's already bounded worker.
    internal Task<TrustedEnvelopeResult> VerifyReplayAdmissionAsync(TrustedEnvelope envelope,TrustedEnvelopeIdentity expected,
        PlatformSigningProfile requiredProfile,IPlatformSigningProfileProvider replayProfiles,CancellationToken token)
        =>AuthenticateAsync(expected,default,envelope,token,finalReplayPolicy:()=>replayProfiles.GetCurrent()==requiredProfile);
    private async Task<TrustedEnvelopeResult> AuthenticateAsync(TrustedEnvelopeIdentity identity, TimeSpan lifetime,
        TrustedEnvelope? input, CancellationToken token,string? originalNonce=null,Func<bool>? finalReplayPolicy=null)
    {
        try
        {
            TrustedEnvelopeResult result = await AuthenticateCoreAsync(identity, lifetime, input, token,originalNonce,finalReplayPolicy).ConfigureAwait(false);
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
            return new(CustodyStatus.Invalid, EvaluatedAt:clock.GetUtcNow());
        }
        catch (Exception)
        {
            token.ThrowIfCancellationRequested();
            return new(CustodyStatus.Unavailable, EvaluatedAt:clock.GetUtcNow());
        }
    }
    private async Task<TrustedEnvelopeResult> AuthenticateCoreAsync(TrustedEnvelopeIdentity expected, TimeSpan lifetime,
        TrustedEnvelope? input, CancellationToken token,string? originalNonce=null,Func<bool>? finalReplayPolicy=null)
    {
        ArgumentNullException.ThrowIfNull(expected);
        token.ThrowIfCancellationRequested();
        PlatformSigningProfile? profile = profiles.GetCurrent();
        DateTimeOffset profileDecisionAt=clock.GetUtcNow();
        if (profile is null || !profile.IsValid(profileDecisionAt))
        {
            return new(CustodyStatus.StaleProfile, EvaluatedAt:profileDecisionAt);
        }
        TrustedEnvelopeIdentity identity;
        try
        {
            expected = TrustedEnvelopeCodec.Snapshot(expected);
            identity = TrustedEnvelopeCodec.Snapshot(input?.Identity ?? expected);
            if (!TrustedEnvelopeCodec.IsValid(expected, profile) || !TrustedEnvelopeCodec.IsValid(identity, profile))
            {
                return new(CustodyStatus.Invalid, EvaluatedAt:clock.GetUtcNow());
            }
        }
        catch (ArgumentException)
        {
            return new(CustodyStatus.Invalid, EvaluatedAt:clock.GetUtcNow());
        }
        byte[] expectedBytes = TrustedEnvelopeCodec.Identity(expected);
        byte[]? identityBytes = null;
        try
        {
            identityBytes = TrustedEnvelopeCodec.Identity(identity);
            if (!expectedBytes.AsSpan().SequenceEqual(identityBytes))
            {
                return new(CustodyStatus.ScopeMismatch, EvaluatedAt:clock.GetUtcNow());
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
            return new(CustodyStatus.Invalid, EvaluatedAt:clock.GetUtcNow());
        }
        if (!issue && CheckTime(input!, profile, now) is
        { }
        failure && failure != CustodyStatus.Succeeded)
        {
            return new(failure, EvaluatedAt:now);
        }
        var scope = new PlatformHmacScope(identity.TargetTenantId, PlatformHmacPurpose.TrustedEnvelope);
        PlatformHmacKeyResolution first = await PlatformKeyResolution.ReadAsync(keys, scope, input?.SigningKeyVersion, token).ConfigureAwait(false);
        using PlatformHmacKeySnapshot? key = first.Key;
        DateTimeOffset keyDecisionAt=clock.GetUtcNow();
        CustodyStatus status = PlatformKeyResolution.Check(first, scope, input?.SigningKeyVersion, keyDecisionAt, issue, profile.RotationOverlap);
        if (status != CustodyStatus.Succeeded)
        {
            return new(status, EvaluatedAt:keyDecisionAt);
        }
        TrustedEnvelope envelope;
        try
        {
            now = clock.GetUtcNow();
            envelope = input is null ? new(identity, now, now.Add(lifetime), originalNonce??Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), key!.Metadata.Version, string.Empty)
                : input with { Identity = identity };
        }
        catch (ArgumentOutOfRangeException)
        {
            return new(CustodyStatus.Invalid, EvaluatedAt:clock.GetUtcNow());
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
                    return new(CustodyStatus.InvalidTag, EvaluatedAt:clock.GetUtcNow());
                }
                try
                {
                    if (!CryptographicOperations.FixedTimeEquals(tag, supplied))
                    {
                        return new(CustodyStatus.InvalidTag, EvaluatedAt:clock.GetUtcNow());
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(supplied);
                }
            }
            PlatformHmacKeyResolution fresh = await PlatformKeyResolution.ReadAsync(keys, scope, issue ? null : envelope.SigningKeyVersion, token).ConfigureAwait(false);
            using PlatformHmacKeySnapshot? current = fresh.Key;
            keyDecisionAt=clock.GetUtcNow();
            status = PlatformKeyResolution.Check(fresh, scope, envelope.SigningKeyVersion, keyDecisionAt, issue, profile.RotationOverlap);
            if (status != CustodyStatus.Succeeded)
            {
                return new(status, EvaluatedAt:keyDecisionAt);
            }
            byte[] confirmation = current!.ComputeTag(canonical);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(tag, confirmation))
                {
                    return new(CustodyStatus.Unavailable, EvaluatedAt:clock.GetUtcNow());
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(confirmation);
            }
            token.ThrowIfCancellationRequested();
            bool replayCurrent=finalReplayPolicy?.Invoke()??true;
            var selectedProfile=profiles.GetCurrent();
            DateTimeOffset decidedAt=clock.GetUtcNow();
            if (!replayCurrent || selectedProfile != profile || !profile.IsValid(decidedAt))
            {
                return new(CustodyStatus.StaleProfile, EvaluatedAt:decidedAt);
            }
            status = PlatformKeyResolution.Check(fresh, scope, envelope.SigningKeyVersion, decidedAt, issue, profile.RotationOverlap);
            if (status != CustodyStatus.Succeeded)
            {
                return new(status, EvaluatedAt:decidedAt);
            }
            status = CheckTime(envelope, profile, decidedAt);
            token.ThrowIfCancellationRequested();
            return status == CustodyStatus.Succeeded ? new(status, envelope with { Tag = Convert.ToHexStringLower(tag) },decidedAt) : new(status, EvaluatedAt:decidedAt);
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
