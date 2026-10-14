using System.Security.Cryptography;

using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Platform.Custody;

/// <summary>Reference-only opt-in for a Dapr secret store; this supplies no lifecycle, replay or signing authority.</summary>
/// <param name="StoreName">Explicit enrolled Dapr secret-store component.</param>
/// <param name="CredentialReference">Public identity of the application credential enrolled with that component.</param>
/// <param name="AuthorityReference">Independent current inventory authority.</param>
/// <param name="ProfileReference">Independent current trusted-envelope profile.</param>
/// <param name="ExpectedProfile">Exact independently enrolled profile identity and accepted timing.</param>
/// <param name="CallTimeout">Total resolution budget, at most thirty seconds.</param>
public sealed record DaprPlatformHmacOptions(string StoreName, string CredentialReference,
    string AuthorityReference, string ProfileReference, PlatformSigningProfile ExpectedProfile, TimeSpan CallTimeout)
{
    internal bool IsValid(DateTimeOffset now) => Reference(StoreName) && Reference(CredentialReference)
        && Reference(AuthorityReference) && Reference(ProfileReference) && ExpectedProfile is not null
        && ExpectedProfile.IsValid(now) && ExpectedProfile.MaximumLifetime == PlatformAcceptedEnvelopeTiming.MaximumLifetime
        && ExpectedProfile.ClockSkew == PlatformAcceptedEnvelopeTiming.ClockSkew
        && ExpectedProfile.RotationOverlap == PlatformAcceptedEnvelopeTiming.RotationOverlap
        && ExpectedProfile.RecoveryHorizon == PlatformAcceptedEnvelopeTiming.RecoveryHorizon
        && ExpectedProfile.ReplayRetention == PlatformAcceptedEnvelopeTiming.ReplayRetention
        && CallTimeout > TimeSpan.Zero && CallTimeout <= TimeSpan.FromSeconds(30);

    internal static bool Reference(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2048
        && !value.Any(char.IsControl);
}

/// <summary>Public, current exact inventory proof from an independently installed authority.</summary>
/// <param name="AuthorityReference">Enrolled authority identity.</param>
/// <param name="Revision">Current immutable inventory/revocation revision.</param>
/// <param name="Scope">Exact tenant and purpose.</param>
/// <param name="Metadata">Current key lifecycle facts.</param>
/// <param name="StoreName">Enrolled Dapr component.</param>
/// <param name="CredentialReference">Enrolled application credential reference.</param>
/// <param name="SecretReference">Exact versioned backend object, never a current/latest alias.</param>
/// <param name="SecretVersion">Exact independently attested positive numeric OpenBao KV2 version; distinct from opaque key version.</param>
/// <param name="SecretField">Only permitted response field.</param>
/// <param name="ObservedAt">Independent current observation instant.</param>
/// <param name="ValidUntil">Exclusive proof validity.</param>
public sealed record DaprPlatformHmacInventory(string AuthorityReference, string Revision, PlatformHmacScope Scope,
    PlatformHmacKeyMetadata Metadata, string StoreName, string CredentialReference, string SecretReference, string SecretVersion,
    string SecretField, DateTimeOffset ObservedAt, DateTimeOffset ValidUntil);

/// <summary>Must query independent current public authority; configuration or a cached actor ledger is insufficient.</summary>
public interface IDaprPlatformHmacAuthority
{
    /// <summary>Reads independently current exact profile; missing/revoked authority returns null.</summary>
    PlatformSigningProfile? GetCurrentProfile(string profileReference);

    /// <summary>Reads current exact inventory and revocation; null means unavailable. No private bytes are returned.</summary>
    ValueTask<DaprPlatformHmacInventory?> ResolveAsync(string authorityReference, PlatformHmacScope scope,
        string? version, CancellationToken cancellationToken);
}

/// <summary>Current reference-bound profile with a bounded independent authority read and no cache.</summary>
public sealed class DaprPlatformSigningProfileProvider(DaprPlatformHmacOptions options,
    IDaprPlatformHmacAuthority authority, TimeProvider clock) : IPlatformSigningProfileProvider
{
    /// <inheritdoc/>
    public PlatformSigningProfile? GetCurrent()
    {
        if (!options.IsValid(clock.GetUtcNow())) { return null; }
        Task<PlatformSigningProfile?> pending = Task.Run(() => authority.GetCurrentProfile(options.ProfileReference));
        try
        {
            PlatformSigningProfile? profile = pending.WaitAsync(options.CallTimeout).GetAwaiter().GetResult();
            return profile == options.ExpectedProfile && options.IsValid(clock.GetUtcNow()) ? profile : null;
        }
        catch (Exception)
        {
            _ = ObserveAsync(pending);
            return null;
        }
    }

    private static async Task ObserveAsync(Task pending)
    {
        try { await pending.ConfigureAwait(false); } catch (Exception) { /* No authority diagnostics cross custody. */ }
    }
}

/// <summary>Resolves only an independently current exact Dapr HMAC key, rechecking authority before release.</summary>
public sealed class DaprPlatformHmacKeyProvider(DaprClient client, DaprPlatformHmacOptions options,
    IDaprPlatformHmacAuthority authority, TimeProvider clock) : IPlatformHmacKeyProvider
{
    /// <inheritdoc/>
    public async ValueTask<PlatformHmacKeyResolution> ResolveAsync(PlatformHmacScope scope, string? version = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (scope is null || !scope.IsValid || version is not null && !DaprPlatformHmacOptions.Reference(version))
        { return new(CustodyStatus.Invalid); }
        if (!options.IsValid(clock.GetUtcNow())) { return new(CustodyStatus.Unavailable); }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.CallTimeout);
        // The detached worker owns every secret buffer, including after an uncooperative call times out.
        Task<PlatformHmacKeyResolution> pending = Task.Run(() => ResolveCoreAsync(scope, version, deadline.Token), CancellationToken.None);
        try
        {
            PlatformHmacKeyResolution result = await pending.WaitAsync(deadline.Token).ConfigureAwait(false);
            if (deadline.IsCancellationRequested)
            {
                result.Key?.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
                return new(CustodyStatus.Unavailable);
            }
            return result;
        }
        catch (Exception)
        {
            _ = DisposeLateAsync(pending);
            cancellationToken.ThrowIfCancellationRequested();
            return new(CustodyStatus.Unavailable);
        }
    }

    private async Task<PlatformHmacKeyResolution> ResolveCoreAsync(PlatformHmacScope scope, string? version, CancellationToken token)
    {
        byte[]? material = null;
        Dictionary<string, string>? response = null;
        try
        {
            if (await ReadPublicAsync(() => Task.FromResult(authority.GetCurrentProfile(options.ProfileReference)), token).ConfigureAwait(false) != options.ExpectedProfile)
            { return new(CustodyStatus.StaleProfile); }
            DaprPlatformHmacInventory? first = await ReadPublicAsync(() => authority.ResolveAsync(options.AuthorityReference, scope, version, token).AsTask(), token).ConfigureAwait(false);
            CustodyStatus status = Check(first, scope, version);
            if (status != CustodyStatus.Succeeded) { return new(status); }
            token.ThrowIfCancellationRequested();
            response = await client.GetSecretAsync(options.StoreName, first!.SecretReference,
                new Dictionary<string, string> { ["version_id"] = first.SecretVersion }, token).ConfigureAwait(false);
            string? encoded;
            try
            {
                token.ThrowIfCancellationRequested();
                if (response.Count != 1 || !response.TryGetValue(first.SecretField, out encoded)
                    || encoded.Length != 44) { return new(CustodyStatus.Unavailable); }
            }
            finally
            {
                response.Clear();
                response = null;
            }
            // Public reads have their own bounded waits. No decoded key or response dictionary
            // is retained by an uncooperative authority operation.
            DaprPlatformHmacInventory? fresh = await ReadPublicAsync(() => authority.ResolveAsync(options.AuthorityReference, scope,
                version is null ? null : first.Metadata.Version, token).AsTask(), token).ConfigureAwait(false);
            status = Check(fresh, scope, first.Metadata.Version);
            if (status != CustodyStatus.Succeeded) { return new(status); }
            // A changed revision, reference, key state or identity requires a new caller attempt; never revive cached authority.
            if (fresh! with { ObservedAt = first.ObservedAt, ValidUntil = first.ValidUntil } != first
                || await ReadPublicAsync(() => Task.FromResult(authority.GetCurrentProfile(options.ProfileReference)), token).ConfigureAwait(false) != options.ExpectedProfile
                || !options.IsValid(clock.GetUtcNow())) { return new(CustodyStatus.Unavailable); }
            token.ThrowIfCancellationRequested();
            material = Convert.FromBase64String(encoded);
            if (material.Length != 32 || Convert.ToBase64String(material) != encoded) { return new(CustodyStatus.Unavailable); }
            return new(CustodyStatus.Succeeded, new(scope, first.Metadata, material));
        }
        catch (Exception)
        {
            return new(CustodyStatus.Unavailable);
        }
        finally
        {
            if (material is not null) { CryptographicOperations.ZeroMemory(material); }
            // Dapr's API returns immutable strings; discard them immediately and never cache/log/serialize them.
            response?.Clear();
        }
    }

    private static async Task<T> ReadPublicAsync<T>(Func<Task<T>> read, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Task<T> pending = Task.Run(read, CancellationToken.None);
        try { return await pending.WaitAsync(token).ConfigureAwait(false); }
        catch (Exception)
        {
            _ = ObservePublicAsync(pending);
            throw;
        }
    }

    private static async Task ObservePublicAsync(Task pending)
    {
        try { await pending.ConfigureAwait(false); } catch (Exception) { /* Public late failure is content-free. */ }
    }

    private CustodyStatus Check(DaprPlatformHmacInventory? proof, PlatformHmacScope scope, string? version)
    {
        if (proof is null) { return CustodyStatus.Unavailable; }
        if (proof.AuthorityReference != options.AuthorityReference || proof.Scope != scope || proof.StoreName != options.StoreName
            || proof.CredentialReference != options.CredentialReference || !DaprPlatformHmacOptions.Reference(proof.Revision)
            || !DaprPlatformHmacOptions.Reference(proof.SecretReference) || !DaprPlatformHmacOptions.Reference(proof.SecretField)
            || !ulong.TryParse(proof.SecretVersion, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out ulong backendVersion)
            || backendVersion == 0 || backendVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) != proof.SecretVersion
            || version is not null && proof.Metadata.Version != version) { return CustodyStatus.ScopeMismatch; }
        DateTimeOffset now = clock.GetUtcNow();
        if (proof.ObservedAt > now || now >= proof.ValidUntil || !options.IsValid(now)) { return CustodyStatus.Unavailable; }
        if (proof.Metadata.State == PlatformHmacKeyState.Revoked) { return CustodyStatus.RevokedKey; }
        if (!Enum.IsDefined(proof.Metadata.State) || now < proof.Metadata.NotBefore || now >= proof.Metadata.VerifyUntil
            || version is null && proof.Metadata.State != PlatformHmacKeyState.Active
            || proof.Metadata.State == PlatformHmacKeyState.Retained && (proof.Metadata.RetiredAt is null || proof.Metadata.RetiredAt > now))
        { return CustodyStatus.OutsideKeyWindow; }
        return CustodyStatus.Succeeded;
    }

    private static async Task DisposeLateAsync(Task<PlatformHmacKeyResolution> pending)
    {
        try { (await pending.ConfigureAwait(false)).Key?.Dispose(); } catch (Exception) { /* Content-free late cleanup. */ }
    }
}

/// <summary>Explicit runtime opt-in; unavailable defaults remain when references or independent authority are absent.</summary>
public static class DaprPlatformHmacServiceCollectionExtensions
{
    /// <summary>Registers only valid current configuration and an explicitly supplied independently current authority.</summary>
    public static IServiceCollection AddDaprPlatformHmac(this IServiceCollection services, DaprPlatformHmacOptions? options,
        IDaprPlatformHmacAuthority? authority)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddPlatformCustody();
        if (options is null || authority is null || !options.IsValid(TimeProvider.System.GetUtcNow())) { return services; }
        services.AddSingleton(options);
        services.AddSingleton(authority);
        services.Replace(ServiceDescriptor.Singleton<IPlatformHmacKeyProvider, DaprPlatformHmacKeyProvider>());
        services.Replace(ServiceDescriptor.Singleton<IPlatformSigningProfileProvider, DaprPlatformSigningProfileProvider>());
        return services;
    }
}
