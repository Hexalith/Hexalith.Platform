using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Closed source recipes for the supported technical guard/signing/protection cases. Argument values come only from independently enrolled exact case originals.</summary>
public static class QualificationGuardRecipeCatalog
{
    /// <summary>Compiles a case into typed operations. Missing resource, writer, clock or fault-controller protocol arguments refuse before execution.</summary>
    public static QualificationGuardCaseDefinition Compile(string caseId,string tenant,string batch,string scopeSha256,
        IReadOnlyDictionary<string,JsonElement> exactArguments)
    {
        var recipe=Recipe(caseId);
        QualificationGuardStep Resolve(string token,int ordinal)
        {
            string key=ordinal+":"+token;
            if(!exactArguments.TryGetValue(key,out var json)) throw new InvalidOperationException("IndependentEnrolledCaseArgument."+caseId+"."+key);
            var step=json.Deserialize<QualificationGuardStep>(Hexalith.Platform.Secrets.Runtime.CustodyBindingContract.Json)
                ?? throw new InvalidOperationException("ClosedTypedCaseArgument."+caseId);
            string actual=step.Kind==QualificationGuardStepKind.Guard?"Guard:"+step.Transition?.Operation:step.Kind.ToString();
            if(actual!=token) throw new InvalidOperationException("ClosedCaseOperationOrdering."+caseId);
            return step;
        }
        int index=0;
        var before=recipe.Before.Select(token=>Resolve(token,++index)).ToArray();
        var effect=Resolve(recipe.Effect,++index);
        var trigger=recipe.Trigger is null?null:Resolve(recipe.Trigger,++index);
        var after=recipe.After.Select(token=>Resolve(token,++index)).ToArray();
        if(exactArguments.Count!=index) throw new InvalidOperationException("UnknownCaseOperationArgument."+caseId);
        return new(caseId,tenant,batch,scopeSha256,before,effect,trigger,recipe.Boundary,after);
    }
    private sealed record SourceRecipe(string[] Before,string Effect,string? Trigger,string Boundary,string[] After);
    private static SourceRecipe Recipe(string id)
    {
        static SourceRecipe Plain(string effect,params string[] before)=>new(before,effect,null,"none",[]);
        static SourceRecipe Race(string effect,string trigger,string boundary)=>new(effect=="Reserve"?["Register"]:
            effect=="Guard:CommitDispatch"?["Guard:AuthorizeDispatch"]:["Guard:AuthorizeCompletion"],effect,trigger,boundary,[]);
        string point=id[(id.LastIndexOf('.')+1)..];
        if(id.StartsWith("Recovery.",StringComparison.Ordinal))
        {
            string operation=id.Split('.')[1];
            return operation switch {
                "signing"=>Plain("Sign"),"consume"=>Plain("Reserve","Register"),"compromise-block"=>Plain("Compromise"),
                "activation"=>Plain("Activate","Register","Compromise","Sign","Guard:ReplaceAttestation","Guard:AuthorizeDispatch","Guard:CommitDispatch"),
                "guard-issue"=>Plain("Guard:RecordBatchIssued","Sign"),"dispatch"=>Plain("Guard:CommitDispatch","Guard:AuthorizeDispatch"),
                "completion"=>Plain("Guard:CommitCompletion","Guard:AuthorizeCompletion"),
                _=>throw new InvalidOperationException("DifferentClosedRecoveryOwnerProtocol."+id) };
        }
        if(id.StartsWith("S4.migration.",StringComparison.Ordinal))
            throw new InvalidOperationException("SelectedActualWriterAndRecoveryControlProtocol."+id);
        if(id=="S4.migration-successor-preservation")
            return new(["Guard:InstallEpoch","Guard:InstallRepairFence","Guard:RecordBridgeDrain"],"Guard:ActivateSuccessor",null,"none",[]);
        if(id is "v23.two-consecutive-stale-signing-attempts" or "v23.stale-attempt-with-rotation")
            return new(["Sign","Guard:RecordBatchIssuanceStale","ObsoleteUnissued","Sign","Guard:RecordBatchIssuanceStale","ObsoleteUnissued"],"Sign",null,"none",["Guard:RecordBatchIssued"]);
        if(id=="v23.unknown-sign-or-issue") return new([],"Sign",null,"none",["Guard:RecordBatchIssued"]);
        if(id=="v23.exact-no-issue-proof") return Plain("ObsoleteUnissued","Sign","Guard:RecordBatchIssuanceStale");
        if(id.StartsWith("v23.compromise-envelope-substitution.",StringComparison.Ordinal) || id is "v23.compromise-complete-enumeration" or "v23.incomplete-enumeration") return Plain("Compromise");
        if(id=="v23.compromise-private-isolation") throw new InvalidOperationException("IndependentForbiddenWorkloadTransportAndNativeAclReadProtocol."+id);
        if(id.StartsWith("v23.admission-integrity-block-reserve.",StringComparison.Ordinal)) return Race("Reserve","Block",point);
        if(id.StartsWith("v23.key-compromise-reserve.",StringComparison.Ordinal)) return Race("Reserve","Compromise",point);
        if(id=="v23.second-compromise.before-replacement") return Plain("Activate","Register","Compromise","Compromise","Sign","Guard:ReplaceAttestation","Guard:AuthorizeDispatch","Guard:CommitDispatch");
        if(id=="v23.second-compromise.before-dispatch") return Plain("Activate","Register","Compromise","Sign","Guard:ReplaceAttestation","Compromise","Guard:AuthorizeDispatch","Guard:CommitDispatch");
        if(id=="v23.second-compromise.before-activation") return Plain("Activate","Register","Compromise","Sign","Guard:ReplaceAttestation","Guard:AuthorizeDispatch","Guard:CommitDispatch","Compromise");
        if(id=="v23.second-compromise.at-activation") return new(["Register","Compromise","Sign","Guard:ReplaceAttestation","Guard:AuthorizeDispatch","Guard:CommitDispatch"],"Activate","Compromise","at",[]);
        if(id=="v23.second-compromise.after-activation-before-reserve") return Plain("Reserve","Register","Compromise","Sign","Guard:ReplaceAttestation","Guard:AuthorizeDispatch","Guard:CommitDispatch","Activate","Compromise");
        if(id=="v23.same-batch-reattest") return Plain("Reserve","Register","Compromise","Sign","Guard:ReplaceAttestation","Guard:AuthorizeDispatch","Guard:CommitDispatch","Activate");
        if(id is "v23.admission-block-never-reopens" or "v23.no-false-replacement") return Plain("Activate","Register","Block");
        if(id.StartsWith("v23.activation-substitution.",StringComparison.Ordinal)) return Plain("Activate","Register","Compromise","Sign","Guard:ReplaceAttestation","Guard:AuthorizeDispatch","Guard:CommitDispatch");
        if(id=="v23.same-alias-coverage") return Plain("Reserve","Register");
        if(id=="v23.delayed-nonexpiring-restore") return Plain("Reserve","Register");
        if(id.StartsWith("v23.hold-dispatch.",StringComparison.Ordinal)) return Race("Guard:CommitDispatch","Guard:RegisterHold",point);
        if(id.StartsWith("v23.compromise-dispatch.",StringComparison.Ordinal)) return Race("Guard:CommitDispatch","Compromise",point);
        if(id.StartsWith("v23.guard-mutation-completion.",StringComparison.Ordinal)) return Race("Guard:CommitCompletion","Guard:RegisterHold",point);
        throw new InvalidOperationException("ClosedCaseUsesDifferentConcreteDriver."+id);
    }
}
