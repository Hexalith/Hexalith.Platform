using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Independent source service receiving path. Only actual typed native readers may publish; callers supply no records, signer, predicates or expected outcomes.</summary>
public sealed class JointPhaseSourceOperationHandler(JointPhaseNativeSourcePublisher publisher,TimeProvider clock):IIndependentOwnerOperationHandler
{
    /// <inheritdoc />
    public bool IsReadOnly(string method)=>method is "joint-phase-native-source-preflight";
    /// <inheritdoc />
    public async Task<object?> RecoverAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken token)
    {
        if(method!="joint-phase-publish-native-source" || caller.Role!="joint-phase-source-reader" || current.ValidUntil<=clock.GetUtcNow()) return null;
        var input=payload.Deserialize<Request>(CustodyBindingContract.Json);
        if(input is null || JointPhaseRequestRecipe.Phase(input.phaseRequest) is not { } phase || caller.ScopeReference!="joint-phase:"+phase) return null;
        return await publisher.LookupAsync(input.caseId,input.phaseRequest,token).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public IReadOnlySet<string> Methods { get; }=new HashSet<string>(["joint-phase-publish-native-source","joint-phase-native-source-preflight"],StringComparer.Ordinal);
    /// <inheritdoc />
    public async Task<object?> HandleAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState state,CancellationToken token)
    {
        if(!Methods.Contains(method) || caller.Role!="joint-phase-source-reader" || state.ValidUntil<=clock.GetUtcNow()) throw new InvalidOperationException("CurrentIndependentJointPhaseSourceReadScope");
        var input=payload.Deserialize<Request>(CustodyBindingContract.Json)??throw new InvalidOperationException("ClosedJointPhaseSourceRequest");
        if(JointPhaseRequestRecipe.Phase(input.phaseRequest) is not { } phase || caller.ScopeReference!="joint-phase:"+phase) return null;
        if(method=="joint-phase-native-source-preflight") return new { MissingInputs=await publisher.PreflightAsync(input.caseId,input.phaseRequest,token).ConfigureAwait(false) };
        if((await publisher.PreflightAsync(input.caseId,input.phaseRequest,token).ConfigureAwait(false)).Count!=0) return null;
        return await publisher.PublishAsync(input.caseId,input.phaseRequest,token).ConfigureAwait(false);
    }
    private sealed record Request(string caseId,System.Text.Json.Nodes.JsonObject phaseRequest);
}
