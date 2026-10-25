using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Explicit owning-process qualification composition. Local concrete workflows/readers/projectors are mandatory; startup never enrolls roles or creates originals.</summary>
public static class QualificationOwnerApplication
{
    /// <summary>Runs receiving executor/observer/enrollment operations under the independently installed current authority and role signer.</summary>
    public static Task<int> RunAsync(string independentReceivingInstallationPath,QualificationOwnerRole role,ECDsa privateRoleSigner,
        IQualificationOriginalStore independentOriginals,IReadOnlyDictionary<string,IQualificationOwnerCaseDriver> concreteCaseDrivers,TimeProvider? clock=null,IQualificationObservedOriginalReader? independentSourceReader=null)
    {
        if(concreteCaseDrivers.Any(pair=>pair.Key!=pair.Value.CaseId)) throw new ArgumentException("Exact closed concrete case registry required.");
        return IndependentAuthorityHttpApplication.RunAsync(independentReceivingInstallationPath,ownerHandlers:
            [new QualificationOwnerOperationHandler(role,privateRoleSigner,independentOriginals,concreteCaseDrivers,clock??TimeProvider.System,independentSourceReader)]);
    }
}
