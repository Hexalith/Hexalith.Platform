namespace Hexalith.Platform.Custody;

/// <summary>Owns the complete restricted export resolution, including late collaborators and cleanup.</summary>
internal static class DaprExportQualificationOperation
{
    private static readonly SemaphoreSlim Slots=new(4,4);
    private static readonly AsyncLocal<CancellationToken?> Current=new();
    private static readonly AsyncLocal<TimeSpan?> Budgets=new();
    internal static TimeSpan? BudgetOverride { get=>Budgets.Value;set=>Budgets.Value=value; }
    internal static int ActiveOwners=>4-Slots.CurrentCount;

    internal static async Task<T?> RunAsync<T>(Func<CancellationToken,Task<T?>> work,Action<T> discard,CancellationToken caller) where T:class
    {
        caller.ThrowIfCancellationRequested();
        if(Current.Value is { } owning) return await work(owning).ConfigureAwait(false);
        var budget=CancellationTokenSource.CreateLinkedTokenSource(caller);
        budget.CancelAfter(BudgetOverride??TimeSpan.FromSeconds(30));
        var token=budget.Token;
        if(!Slots.Wait(0)) { budget.Dispose();return null; }
        object gate=new();bool abandoned=false,workFinished=false,callerFinished=false,released=false;T? unclaimed=null;
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Both flags are protected by gate. Completed work still owns its private result until
        // the caller claims/discards it; an abandoned caller still owns any actual late cleanup.
        void ReleaseFinishedOwner()
        {
            if(!released && workFinished && callerFinished)
            { released=true;try { budget.Dispose(); }finally { Slots.Release(); } }
        }
        try
        {
            _=Task.Run(async ()=>
            {
                Current.Value=token;
                try
                {
                    var value=await work(token).ConfigureAwait(false);
                    lock(gate) { if(abandoned) { if(value is not null) discard(value); }else unclaimed=value; }
                }
                catch(Exception) { /* Private collaborator diagnostics never escape. */ }
                finally
                {
                    Current.Value=null;
                    lock(gate) { workFinished=true;ReleaseFinishedOwner(); }
                    completion.TrySetResult();
                }
            },CancellationToken.None);
        }
        catch { Slots.Release();budget.Dispose();throw; }
        try
        {
            await completion.Task.WaitAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            caller.ThrowIfCancellationRequested();
            lock(gate) { var value=unclaimed;unclaimed=null;return value; }
        }
        catch
        {
            lock(gate) { abandoned=true;if(unclaimed is not null) { discard(unclaimed);unclaimed=null; } }
            caller.ThrowIfCancellationRequested();return null;
        }
        finally { lock(gate) { callerFinished=true;ReleaseFinishedOwner(); } }
    }
}
