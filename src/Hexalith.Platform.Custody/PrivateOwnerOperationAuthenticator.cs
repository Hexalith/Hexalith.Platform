using System.Security.Claims;
using System.Security.Cryptography;
using System.Globalization;
using System.Text;

namespace Hexalith.Platform.Custody;

/// <summary>Private candidate exact-operation credential implementation using current machine enrollment plus purpose-separated custody HMAC.
/// No HTTP route, actor registration or issuer/ACL policy is enabled by this library; unavailable grant/profile/key always denies.</summary>
public sealed class PrivateOwnerOperationAuthenticator(IPlatformHmacKeyProvider keys, IPlatformSigningProfileProvider profiles,
    TimeProvider clock, IPrivateOwnerOperationGrantSource? grants = null)
{
    /// <summary>Issues only for an already authenticated dedicated current machine and its exact configured private-owner grant.</summary>
    public Task<PrivateOwnerOperationCredential?> IssueAsync(ClaimsPrincipal authenticatedMachine, PrivateOwnerOperationScope expected, CancellationToken cancellationToken = default)
        => ExecuteAsync(authenticatedMachine, expected, null, cancellationToken);
    /// <summary>Checks the original transport machine plus exact current owner request before lookup/effect; retained terminal evidence is never renewed or mutated.</summary>
    public async Task<bool> AuthorizeAsync(ClaimsPrincipal authenticatedMachine, PrivateOwnerOperationScope expected,
        PrivateOwnerOperationCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        return await ExecuteAsync(authenticatedMachine, expected, credential, cancellationToken).ConfigureAwait(false) is not null;
    }
    private async Task<PrivateOwnerOperationCredential?> ExecuteAsync(ClaimsPrincipal caller, PrivateOwnerOperationScope expected,
        PrivateOwnerOperationCredential? input, CancellationToken token)
    {
        long start = clock.GetTimestamp(); byte[]? canonical = null; byte[]? tag = null; byte[]? supplied = null;
        try
        {
            token.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(caller); ArgumentNullException.ThrowIfNull(expected);
            if (grants is null || !ValidScope(expected) || input is not null && !ValidCredentialIdentities(input)) { return null; }
            var machine = await AwaitAsync(() => Task.FromResult(CaptureMachine(caller)), start, token).ConfigureAwait(false);
            if (machine is null || await AwaitAsync(() => Task.FromResult(profiles.GetCurrent()), start, token).ConfigureAwait(false) is not { } profile
                || !ValidText(profile.Version) || !ValidText(profile.Issuer) || !ValidText(profile.Audience) || !profile.IsValid(clock.GetUtcNow())) { return null; }
            string issuer = machine.Issuer, subject = machine.Subject, client = machine.Client, audience = machine.Audience;
            var grant = await AwaitAsync(() => grants.ResolveCurrentAsync(issuer, subject, client, expected, CancellationToken.None), start, token).ConfigureAwait(false);
            if (!ValidGrant(grant, issuer, subject, client, audience, expected, clock.GetUtcNow())) { return null; }
            byte[] scopeValidation = ScopeBytes(expected); CryptographicOperations.ZeroMemory(scopeValidation);
            DateTimeOffset now = clock.GetUtcNow(); bool issue = input is null;
            if (!issue && (input!.ProfileVersion != profile.Version || input.Issuer != profile.Issuer || input.Audience != profile.Audience
                || input.MachineIssuer != issuer || input.MachineSubject != subject || input.MachineClient != client || input.MachineAudience != audience
                || input.AuthorityReference != grant!.AuthorityReference || input.BindingRevision != grant.BindingRevision || input.Scope != expected
                || !ValidTime(input, profile, now) || input.DeliveryNonce is not { Length: 64 } || input.DeliveryNonce.Any(c => !char.IsAsciiHexDigit(c))
                || input.Tag is not { Length: 64 } || input.Tag.Any(c => !char.IsAsciiHexDigit(c)))) { return null; }
            var scope = new PlatformHmacScope(expected.TenantId, PlatformHmacPurpose.TrustedEnvelope);
            using var first = await ResolveAsync(scope, input?.SigningKeyVersion, start, token).ConfigureAwait(false);
            if (first is null || PlatformKeyResolution.Check(new(CustodyStatus.Succeeded, first), scope, input?.SigningKeyVersion, clock.GetUtcNow(), issue, profile.RotationOverlap) != CustodyStatus.Succeeded) { return null; }
            var credential = input ?? new(profile.Version, profile.Issuer, profile.Audience, issuer, subject, client, audience,
                grant!.AuthorityReference, grant.BindingRevision, expected, now, now.Add(profile.MaximumLifetime), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), first.Metadata.Version, string.Empty);
            canonical = Bytes(credential); tag = first.ComputeTag(canonical);
            if (!issue) { supplied = Convert.FromHexString(input!.Tag); if (!CryptographicOperations.FixedTimeEquals(tag, supplied)) { return null; } }
            using var confirmed = await ResolveAsync(scope, credential.SigningKeyVersion, start, token).ConfigureAwait(false);
            if (confirmed is null || PlatformKeyResolution.Check(new(CustodyStatus.Succeeded, confirmed), scope, credential.SigningKeyVersion, clock.GetUtcNow(), issue, profile.RotationOverlap) != CustodyStatus.Succeeded) { return null; }
            byte[] currentTag = confirmed.ComputeTag(canonical);
            try { if (!CryptographicOperations.FixedTimeEquals(tag, currentTag)) { return null; } } finally { CryptographicOperations.ZeroMemory(currentTag); }
            var currentGrant = await AwaitAsync(() => grants.ResolveCurrentAsync(issuer, subject, client, expected, CancellationToken.None), start, token).ConfigureAwait(false);
            if (currentGrant != grant || !ValidGrant(currentGrant, issuer, subject, client, audience, expected, clock.GetUtcNow())
                || await AwaitAsync(() => Task.FromResult(profiles.GetCurrent()), start, token).ConfigureAwait(false) != profile || !profile.IsValid(clock.GetUtcNow()) || !ValidTime(credential, profile, clock.GetUtcNow())) { return null; }
            token.ThrowIfCancellationRequested();
            if (clock.GetElapsedTime(start) >= TimeSpan.FromSeconds(30)) { return null; }
            return credential with { Tag = Convert.ToHexStringLower(tag) };
        }
        catch (Exception) { token.ThrowIfCancellationRequested(); return null; }
        finally { if (canonical is not null) { CryptographicOperations.ZeroMemory(canonical); } if (tag is not null) { CryptographicOperations.ZeroMemory(tag); } if (supplied is not null) { CryptographicOperations.ZeroMemory(supplied); } }
    }
    private static bool ValidGrant(PrivateOwnerOperationGrant? grant, string issuer, string subject, string client, string audience, PrivateOwnerOperationScope expected, DateTimeOffset now)
        => grant is not null && grant.IsDedicatedServiceAccount && grant.MachineIssuer == issuer && grant.MachineSubject == subject && grant.MachineClient == client
            && grant.MachineAudience == audience && grant.Scope == expected && ValidText(grant.AuthorityReference) && grant.BindingRevision > 0
            && grant.NotBefore.Offset == TimeSpan.Zero && grant.ValidUntil.Offset == TimeSpan.Zero && grant.NotBefore <= now && now < grant.ValidUntil;
    private static string? One(ClaimsIdentity caller, string name)
    {
        string? value = null;
        foreach (Claim claim in caller.FindAll(name))
        {
            if (value is not null || !ValidText(claim.Value)) { return null; }
            value = claim.Value;
        }
        return value;
    }
    private sealed record Machine(string Issuer, string Subject, string Client, string Audience);
    private static Machine? CaptureMachine(ClaimsPrincipal caller)
    {
        ClaimsIdentity? identity = null;
        foreach (ClaimsIdentity candidate in caller.Identities)
        { if (candidate.IsAuthenticated) { if (identity is not null) { return null; } identity = candidate; } }
        if (identity is null) { return null; }
        string? issuer = One(identity, "iss"), subject = One(identity, "sub"), client = One(identity, "azp"), audience = One(identity, "aud");
        return issuer is not null && subject is not null && client is not null && audience is not null ? new(issuer, subject, client, audience) : null;
    }
    private static bool ValidText(string? value)
    {
        try { return !string.IsNullOrWhiteSpace(value) && value.Length <= 2048 && new UTF8Encoding(false, true).GetByteCount(value) <= 2048; }
        catch (EncoderFallbackException) { return false; }
    }
    private static bool ValidScope(PrivateOwnerOperationScope scope)
        => scope.PayloadFingerprint is { Length: 64 } && scope.PayloadFingerprint.All(char.IsAsciiHexDigit)
            && new[] { scope.TenantId, scope.ResourceId, scope.Method, scope.Contract, scope.DigestKeyVersion, scope.AuthenticatedTargetTenantId }.All(ValidText);
    private static bool ValidCredentialIdentities(PrivateOwnerOperationCredential c)
        => ValidScope(c.Scope) && new[] { c.ProfileVersion, c.Issuer, c.Audience, c.MachineIssuer, c.MachineSubject, c.MachineClient,
            c.MachineAudience, c.AuthorityReference, c.SigningKeyVersion }.All(ValidText);
    private static bool ValidTime(PrivateOwnerOperationCredential credential, PlatformSigningProfile profile, DateTimeOffset now)
        => credential.IssuedAt.Offset == TimeSpan.Zero && credential.ExclusiveExpiry.Offset == TimeSpan.Zero && credential.IssuedAt < credential.ExclusiveExpiry
            && credential.ExclusiveExpiry - credential.IssuedAt <= profile.MaximumLifetime && credential.IssuedAt <= now + profile.ClockSkew && now < credential.ExclusiveExpiry;
    private static byte[] ScopeBytes(PrivateOwnerOperationScope scope)
    {
        if (!ValidScope(scope)) { throw new ArgumentException("Invalid bounded private owner scope."); }
        return PlatformCanonicalBytes.Components([scope.TenantId, scope.ResourceId, scope.Method, scope.Contract, scope.PayloadFingerprint, scope.DigestKeyVersion, scope.AuthenticatedTargetTenantId]);
    }
    private static byte[] Bytes(PrivateOwnerOperationCredential c)
    {
        byte[] scope = ScopeBytes(c.Scope);
        try { return PlatformCanonicalBytes.Components(["PrivateOwnerOperation.candidate.v1", c.ProfileVersion, c.Issuer, c.Audience, c.MachineIssuer, c.MachineSubject, c.MachineClient,
            c.MachineAudience, c.AuthorityReference, c.BindingRevision.ToString(CultureInfo.InvariantCulture), Convert.ToHexString(scope),
            c.IssuedAt.ToString("O", CultureInfo.InvariantCulture), c.ExclusiveExpiry.ToString("O", CultureInfo.InvariantCulture), c.DeliveryNonce, c.SigningKeyVersion]); }
        finally { CryptographicOperations.ZeroMemory(scope); }
    }
    private async Task<PlatformHmacKeySnapshot?> ResolveAsync(PlatformHmacScope scope, string? version, long start, CancellationToken token)
    {
        var result = await AwaitAsync(() => keys.ResolveAsync(scope, version, CancellationToken.None).AsTask(), start, token, static value => value.Key?.Dispose()).ConfigureAwait(false);
        if (result.Status == CustodyStatus.Succeeded && result.Key is not null && ValidText(result.Key.Metadata.Version)) { return result.Key; } result.Key?.Dispose(); return null;
    }
    private async Task<T> AwaitAsync<T>(Func<Task<T>> operation, long start, CancellationToken token, Action<T>? abandoned = null)
    {
        token.ThrowIfCancellationRequested(); var pending = Task.Run(operation, CancellationToken.None);
        try
        {
            TimeSpan remaining = TimeSpan.FromSeconds(30) - clock.GetElapsedTime(start); if (remaining <= TimeSpan.Zero) { throw new TimeoutException(); }
            var result = await pending.WaitAsync(remaining, clock, token).ConfigureAwait(false);
            if (token.IsCancellationRequested || clock.GetElapsedTime(start) >= TimeSpan.FromSeconds(30)) { token.ThrowIfCancellationRequested(); throw new TimeoutException(); }
            return result;
        }
        catch (Exception)
        {
            _ = pending.ContinueWith(task => { try { if (task.IsCompletedSuccessfully) { abandoned?.Invoke(task.Result); } else { _ = task.Exception; } } catch (Exception) { /* Cleanup failure is observed; it cannot release evidence. */ } },
                CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default); token.ThrowIfCancellationRequested(); throw;
        }
    }
}
