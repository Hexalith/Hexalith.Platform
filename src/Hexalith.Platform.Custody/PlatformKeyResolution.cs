namespace Hexalith.Platform.Custody;

/// <summary>Bounds caller waiting and disposes an owned key that arrives after cancellation.</summary>
internal static class PlatformKeyResolution
{
    /// <summary>Resolves a fresh exact scope without releasing late keys or provider diagnostics.</summary>
    internal static async Task<PlatformHmacKeyResolution> ReadAsync(IPlatformHmacKeyProvider provider,
        PlatformHmacScope scope, string? version, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Task<PlatformHmacKeyResolution> pending;
        try
        {
            pending = provider.ResolveAsync(scope, version, token).AsTask();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            token.ThrowIfCancellationRequested();
            return new(CustodyStatus.Unavailable);
        }
        try
        {
            PlatformHmacKeyResolution result = await pending.WaitAsync(token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
            {
                result.Key?.Dispose();
                token.ThrowIfCancellationRequested();
            }
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _ = DisposeLateAsync(pending);
            throw;
        }
        catch (Exception)
        {
            token.ThrowIfCancellationRequested();
            return new(CustodyStatus.Unavailable);
        }
    }
    private static async Task DisposeLateAsync(Task<PlatformHmacKeyResolution> pending)
    {
        try
        {
            (await pending.ConfigureAwait(false)).Key?.Dispose();
        }
        catch (Exception)
        {
            /* Content-free observation of late failure. */
        }
    }
    /// <summary>Checks provider-authenticated scope, state and exclusive lifecycle bounds.</summary>
    internal static CustodyStatus Check(PlatformHmacKeyResolution result, PlatformHmacScope scope,
        string? version, DateTimeOffset now, bool issue, TimeSpan? overlap = null)
    {
        if (result.Status != CustodyStatus.Succeeded)
        {
            return result.Status;
        }
        if (result.Key is not
        { }
        key || key.Scope != scope || version is not null && key.Metadata.Version != version)
        {
            return CustodyStatus.ScopeMismatch;
        }
        PlatformHmacKeyMetadata meta = key.Metadata;
        if (meta.State == PlatformHmacKeyState.Revoked)
        {
            return CustodyStatus.RevokedKey;
        }
        if (now < meta.NotBefore || now >= meta.VerifyUntil || issue && meta.State != PlatformHmacKeyState.Active)
        {
            return CustodyStatus.OutsideKeyWindow;
        }
        if (meta.State == PlatformHmacKeyState.Retained && meta.RetiredAt > now)
        {
            return CustodyStatus.OutsideKeyWindow;
        }
        if (meta.State == PlatformHmacKeyState.Retained && overlap is
        { }
        window
            && now - meta.RetiredAt!.Value >= window)
            {
                return CustodyStatus.OutsideKeyWindow;
            }
        return CustodyStatus.Succeeded;
    }
}
