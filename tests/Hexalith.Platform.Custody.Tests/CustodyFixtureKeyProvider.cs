using System.Security.Cryptography;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Fresh local inventory only; no secretstore/backend qualification.</summary>
public sealed class CustodyFixtureKeyProvider(CustodyFixtureClock clock) : IPlatformHmacKeyProvider
{
    /// <summary>Gets or sets the current issuance version.</summary>
    public string CurrentVersion { get; set; } = "key-v1";
    /// <summary>Gets or sets an emergency revoked version.</summary>
    public string? RevokedVersion { get; set; }
    /// <summary>Gets or sets the retirement instant.</summary>
    public DateTimeOffset? RetiredAt { get; set; }
    /// <summary>Gets owned snapshots for disposal verification.</summary>
    public List<PlatformHmacKeySnapshot> Snapshots { get; } = [];
    /// <summary>Gets the number of fresh resolution requests.</summary>
    public int Calls { get; private set; }
    /// <summary>Gets or sets a local resolution hook for outage and cancellation races.</summary>
    public Func<int, PlatformHmacScope, string?, CancellationToken, ValueTask<PlatformHmacKeyResolution>>? Hook { get; set; }
    /// <inheritdoc/>
    public ValueTask<PlatformHmacKeyResolution> ResolveAsync(PlatformHmacScope scope, string? version = null, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Hook is { } hook ? hook(Calls, scope, version, cancellationToken) : ValueTask.FromResult(Create(scope, version));
    }
    /// <summary>Creates an owned fixture key with a fresh lifecycle observation.</summary>
    public PlatformHmacKeyResolution Create(PlatformHmacScope scope, string? version)
    {
        version ??= CurrentVersion;
        if (version is not ("key-v1" or "key-v2"))
        {
            return new(CustodyStatus.UnknownKey);
        }
        PlatformHmacKeyState state = version == RevokedVersion ? PlatformHmacKeyState.Revoked
            : version == CurrentVersion ? PlatformHmacKeyState.Active : PlatformHmacKeyState.Retained;
        byte[] material = Enumerable.Repeat((byte)(version == "key-v1" ? 11 : 12), 32).ToArray();
        try
        {
            var key = new PlatformHmacKeySnapshot(scope, new(version, state, clock.Now.AddDays(-10), clock.Now.AddDays(10),
                state == PlatformHmacKeyState.Retained ? RetiredAt ?? clock.Now : null), material);
            Snapshots.Add(key);
            return new(CustodyStatus.Succeeded, key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
        }
    }
}
