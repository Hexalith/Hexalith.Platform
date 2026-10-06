using System.Security.Cryptography;

namespace Hexalith.Platform.Custody;

/// <summary>Purpose-separated digests with exact recorded-version recomputation.</summary>
public sealed class PlatformHmacService(IPlatformHmacKeyProvider keys, IPlatformSigningProfileProvider profiles, TimeProvider clock)
{
    /// <summary>Computes a tenant-content or system-observation digest; retained versions ignore envelope overlap.</summary>
    public async Task<PlatformHmacResult> DigestAsync(PlatformHmacScope scope, IReadOnlyList<string?> components,
        string? recordedVersion = null, CancellationToken cancellationToken = default)
    {
        try
        {
            PlatformHmacResult result = await DigestCoreAsync(scope, components, recordedVersion, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(CustodyStatus.Invalid);
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(CustodyStatus.Unavailable);
        }
    }
    private async Task<PlatformHmacResult> DigestCoreAsync(PlatformHmacScope scope, IReadOnlyList<string?> components,
        string? recordedVersion = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(components);
        cancellationToken.ThrowIfCancellationRequested();
        if (!scope.IsValid || scope.Purpose == PlatformHmacPurpose.TrustedEnvelope || recordedVersion is not null && string.IsNullOrWhiteSpace(recordedVersion))
        {
            return new(CustodyStatus.Invalid);
        }
        PlatformSigningProfile? profile = profiles.GetCurrent();
        if (profile is null || !profile.IsValid(clock.GetUtcNow()))
        {
            return new(CustodyStatus.StaleProfile);
        }
        byte[] bytes;
        try
        {
            bytes = PlatformCanonicalBytes.Components(new[]
            {
                scope.Purpose.ToString(), scope.TenantId
            }
            .Concat(components.ToArray()));
        }
        catch (ArgumentException)
        {
            return new(CustodyStatus.Invalid);
        }
        try
        {
            PlatformHmacKeyResolution first = await PlatformKeyResolution.ReadAsync(keys, scope, recordedVersion, cancellationToken).ConfigureAwait(false);
            using PlatformHmacKeySnapshot? key = first.Key;
            CustodyStatus status = PlatformKeyResolution.Check(first, scope, recordedVersion, clock.GetUtcNow(), recordedVersion is null);
            if (status != CustodyStatus.Succeeded)
            {
                return new(status);
            }
            byte[] digest = key!.ComputeTag(bytes);
            try
            {
                PlatformHmacKeyResolution fresh = await PlatformKeyResolution.ReadAsync(keys, scope, recordedVersion is null ? null : key.Metadata.Version, cancellationToken).ConfigureAwait(false);
                using PlatformHmacKeySnapshot? current = fresh.Key;
                status = PlatformKeyResolution.Check(fresh, scope, key.Metadata.Version, clock.GetUtcNow(), recordedVersion is null);
                if (status != CustodyStatus.Succeeded)
                {
                    return new(status);
                }
                byte[] confirmation = current!.ComputeTag(bytes);
                try
                {
                    if (!CryptographicOperations.FixedTimeEquals(digest, confirmation))
                    {
                        return new(CustodyStatus.Unavailable);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    if (profiles.GetCurrent() != profile || !profile.IsValid(clock.GetUtcNow()))
                    {
                        return new(CustodyStatus.StaleProfile);
                    }
                    status = PlatformKeyResolution.Check(fresh, scope, key.Metadata.Version, clock.GetUtcNow(), recordedVersion is null);
                    if (status != CustodyStatus.Succeeded)
                    {
                        return new(status);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    return new(CustodyStatus.Succeeded, key.Metadata.Version, Convert.ToHexStringLower(digest));
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(confirmation);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(digest);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
