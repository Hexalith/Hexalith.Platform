using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Explicit owning-process qualification composition. Local concrete workflows/readers/projectors are mandatory; startup never enrolls roles or creates originals.</summary>
public static class QualificationOwnerApplication
{
    /// <summary>Runs receiving executor/observer/enrollment operations under the independently installed current authority and role signer.</summary>
    public static async Task<int> RunAsync(string independentReceivingInstallationPath,QualificationOwnerRole role,ECDsa privateRoleSigner,
        IQualificationOriginalStore independentOriginals,IReadOnlyDictionary<string,IQualificationOwnerCaseDriver> concreteCaseDrivers,TimeProvider? clock=null,IQualificationObservedOriginalReader? independentSourceReader=null)
    {
        if(concreteCaseDrivers.Any(pair=>pair.Key!=pair.Value.CaseId)) throw new ArgumentException("Exact closed concrete case registry required.");
        byte[] bytes=await File.ReadAllBytesAsync(independentReceivingInstallationPath).ConfigureAwait(false);
        if(bytes.Length is <=0 or >1024*1024) return 2;CustodyBindingContract.RejectDuplicateProperties(bytes);
        var profile=System.Text.Json.JsonSerializer.Deserialize<IndependentAuthorityReceiverInstallation>(bytes,CustodyBindingContract.Json);
        using var dapr=new Dapr.Client.DaprClientBuilder().Build();
        IQualificationAttemptAuthority? attempts=profile?.CohortAttempt is { } binding?new DaprQualificationAttemptAuthority(new DaprSecretsInvoker(dapr),binding,clock??TimeProvider.System):null;
        return await IndependentAuthorityHttpApplication.RunAsync(independentReceivingInstallationPath,ownerHandlers:
            [new QualificationOwnerOperationHandler(role,privateRoleSigner,independentOriginals,concreteCaseDrivers,clock??TimeProvider.System,independentSourceReader,attempts)]).ConfigureAwait(false);
    }
}
