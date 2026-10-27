using Microsoft.AspNetCore.Http;

namespace Hexalith.Platform.Secrets.Runtime;

// A slot covers body acquisition, JSON/signing buffers, response delivery and all late child work.
internal sealed class IndependentReceivingOwnership(SemaphoreSlim slots,CancellationToken caller):IDisposable
{
    internal static readonly AsyncLocal<IndependentReceivingOwnership?> Current=new();
    private readonly CancellationTokenSource _budget=CancellationTokenSource.CreateLinkedTokenSource(caller);
    private int _owners=1;
    internal CancellationToken Token { get { return _budget.Token; } }
    internal void Start(TimeSpan? timeout=null)=>_budget.CancelAfter(timeout??TimeSpan.FromSeconds(30));
    internal static T Observe<T>(T work) where T:Task { Current.Value?.Track(work);return work; }
    internal void Track(Task work)
    {
        Interlocked.Increment(ref _owners);
        _=work.ContinueWith(task=> { _=task.Exception;Release(); },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
    }
    private void Release() { if(Interlocked.Decrement(ref _owners)==0) { _budget.Dispose();slots.Release(); } }
    private int _disposed;
    public void Dispose() { if(Interlocked.Exchange(ref _disposed,1)==0) Release(); }
    internal IResult Wrap(IResult result)=>new Response(this,result);
    private sealed class Response(IndependentReceivingOwnership owner,IResult result):IResult
    {
        public async Task ExecuteAsync(HttpContext context)
        {
            using var cancelled=owner.Token.Register(()=>context.Abort());
            try { owner.Token.ThrowIfCancellationRequested();await result.ExecuteAsync(context).ConfigureAwait(false); }
            finally { owner.Dispose(); }
        }
    }
}
