using Microsoft.AspNetCore.Http;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Owning HTTP boundary for one qualification service, before authentication or frame acquisition.</summary>
public sealed class QualificationHttpReceiver
{
    private readonly SemaphoreSlim _slots=new(4,4);
    /// <summary>Retains finite ownership through response delivery and every actually running child.</summary>
    public async Task<IResult> HandleAsync(string role,string operation,SecretsRuntimeConfiguration configuration,HttpContext context,
        string? channel,IQualificationCallerAuthenticator? authenticator,IQualificationRoleProvider? provider,IQualificationOriginalStore? store,
        IQualificationPublicProofVerifier? verifier,CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        if(!_slots.Wait(0)) { IndependentReceivingDiagnostics.Record(IndependentReceivingFailure.CapacitySaturated);return Results.StatusCode(503); }
        var owner=new IndependentReceivingOwnership(_slots,caller);
        owner.Start(configuration.CallTimeout is { } timeout && timeout>TimeSpan.Zero && timeout<=TimeSpan.FromSeconds(30)?timeout:TimeSpan.FromSeconds(30));
        var previous=IndependentReceivingOwnership.Current.Value;IndependentReceivingOwnership.Current.Value=owner;
        context.Response.RegisterForDispose(owner);
        try
        {
            var work=QualificationHttpHost.HandleAsync(role,operation,configuration,context,channel,authenticator,provider,store,verifier,owner.Token);
            owner.Track(work);
            QualificationHostResult result;
            try { result=await work.WaitAsync(owner.Token).ConfigureAwait(false); }
            catch(OperationCanceledException) when(!caller.IsCancellationRequested)
            {
                IndependentReceivingDiagnostics.Record(IndependentReceivingFailure.AcquisitionTimeout);
                result=QualificationHost.Deny(["ExactOriginalLookupRequired"],role=="executor");
            }
            IndependentReceivingDiagnostics.ObserveStatus(result.StatusCode);
            // JSON serialization is deferred to response execution while the same slot is owned.
            return owner.Wrap(Results.Json(result.Body,statusCode:result.StatusCode));
        }
        catch(OperationCanceledException) when(caller.IsCancellationRequested)
        { IndependentReceivingDiagnostics.Record(IndependentReceivingFailure.CallerCancelled);owner.Dispose();throw; }
        catch { owner.Dispose();throw; }
        finally { IndependentReceivingOwnership.Current.Value=previous; }
    }
}
