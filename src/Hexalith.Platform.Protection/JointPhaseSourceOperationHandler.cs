using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Independent source service receiving path. Only actual typed native readers may publish; callers supply no records, signer, predicates or expected outcomes.</summary>
public sealed class JointPhaseSourceOperationHandler(JointPhaseNativeSourcePublisher publisher,TimeProvider clock):IIndependentOwnerOperationHandler
{
    /// <inheritdoc />
    public IReadOnlySet<string> Methods { get; }=new HashSet<string>(["joint-phase-publish-native-source","joint-phase-native-source-preflight"],StringComparer.Ordinal);
    /// <inheritdoc />
    public async Task<object?> HandleAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState state,CancellationToken token)
    {
        if(!Methods.Contains(method) || caller.Role!="joint-phase-source-reader" || state.ValidUntil<=clock.GetUtcNow()) throw new InvalidOperationException("CurrentIndependentJointPhaseSourceReadScope");
        var input=payload.Deserialize<Request>(CustodyBindingContract.Json)??throw new InvalidOperationException("ClosedJointPhaseSourceRequest");
        if(input.phaseRequest["phase"]?.GetValue<int>() is not { } phase || caller.ScopeReference!="joint-phase:"+phase) return null;
        if(method=="joint-phase-native-source-preflight") return new { MissingInputs=await publisher.PreflightAsync(input.caseId,input.phaseRequest,token).ConfigureAwait(false) };
        if((await publisher.PreflightAsync(input.caseId,input.phaseRequest,token).ConfigureAwait(false)).Count!=0) return null;
        return await publisher.PublishAsync(input.caseId,input.phaseRequest,token).ConfigureAwait(false);
    }
    private sealed record Request(string caseId,System.Text.Json.Nodes.JsonObject phaseRequest);
}
