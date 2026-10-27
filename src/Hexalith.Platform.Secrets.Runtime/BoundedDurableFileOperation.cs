namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Bounds callers while retaining worker, fencing and buffer ownership until blocking kernel work and cleanup actually finish.</summary>
public static class BoundedDurableFileOperation
{
    private static readonly SemaphoreSlim Workers=new(4,4);
    private static readonly AsyncLocal<bool> Owned=new();
    private static readonly AsyncLocal<CancellationToken> Budget=new();
    /// <summary>The owning operation's total cancellation budget, also used before independent authority advancement.</summary>
    public static CancellationToken CurrentToken=>Budget.Value;
    /// <summary>Acquires a worker before copying input, and completes that copy before cancellation can return ownership to its caller.</summary>
    internal static async Task<T> RunWithInputAsync<T>(ReadOnlyMemory<byte> input,Func<byte[],CancellationToken,Task<T>> action,CancellationToken token)
    {
        if(Owned.Value)
        {
            using var nestedBudget=CancellationTokenSource.CreateLinkedTokenSource(CurrentToken,token);
            token=nestedBudget.Token;token.ThrowIfCancellationRequested();byte[] nested=input.ToArray();
            var previous=Budget.Value;Budget.Value=token;
            try { return await action(nested,token).ConfigureAwait(false); }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(nested);Budget.Value=previous; }
        }
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(token);budget.CancelAfter(TimeSpan.FromSeconds(30));token=budget.Token;
        await Workers.WaitAsync(token).ConfigureAwait(false);
        byte[] owned;
        try { token.ThrowIfCancellationRequested();owned=input.ToArray(); }
        catch { Workers.Release();throw; }
        var work=Task.Run(async()=>
        {
            Owned.Value=true;Budget.Value=token;
            try { token.ThrowIfCancellationRequested();return await action(owned,token).ConfigureAwait(false); }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(owned);Owned.Value=false;Budget.Value=default;Workers.Release(); }
        },CancellationToken.None);
        IndependentReceivingOwnership.Current.Value?.Track(work);
        try { return await work.WaitAsync(token).ConfigureAwait(false); }
        catch { _=work.ContinueWith(task=>_ =task.Exception,CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);throw; }
    }
    /// <summary>Nested durable operations remain inside their outer fence and total deadline.</summary>
    public static async Task<T> RunAsync<T>(Func<CancellationToken,Task<T>> action,CancellationToken token=default,TimeSpan? timeout=null)
    {
        if(Owned.Value)
        {
            using var nestedBudget=CancellationTokenSource.CreateLinkedTokenSource(CurrentToken,token);
            if(timeout is not null) nestedBudget.CancelAfter(timeout.Value);
            token=nestedBudget.Token;token.ThrowIfCancellationRequested();var previous=Budget.Value;Budget.Value=token;
            try { return await action(token).ConfigureAwait(false); }
            finally { Budget.Value=previous; }
        }
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(token);budget.CancelAfter(timeout??TimeSpan.FromSeconds(30));token=budget.Token;
        await Workers.WaitAsync(token).ConfigureAwait(false);
        var work=Task.Run(async()=>
        {
            Owned.Value=true;Budget.Value=token;
            try { token.ThrowIfCancellationRequested();return await action(token).ConfigureAwait(false); }
            finally { Owned.Value=false;Budget.Value=default;Workers.Release(); }
        },CancellationToken.None);
        IndependentReceivingOwnership.Current.Value?.Track(work);
        try { return await work.WaitAsync(token).ConfigureAwait(false); }
        catch { _=work.ContinueWith(task=> { if(task.Status==TaskStatus.RanToCompletion && task.Result is IDisposable resource) resource.Dispose();else _=task.Exception; },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);throw; }
    }
}
