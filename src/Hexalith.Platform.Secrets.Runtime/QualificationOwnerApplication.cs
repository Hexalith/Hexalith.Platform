using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Explicit owning-process qualification composition. Local concrete workflows/readers/projectors are mandatory; startup never enrolls roles or creates originals.</summary>
public static class QualificationOwnerApplication
{
    /// <summary>Runs receiving executor/observer/enrollment operations under the independently installed current authority and role signer.</summary>
    public static Task<int> RunAsync(string independentReceivingInstallationPath,QualificationOwnerRole role,ECDsa privateRoleSigner,
        IQualificationOriginalStore independentOriginals,IReadOnlyDictionary<string,IQualificationOwnerCaseDriver> concreteCaseDrivers,TimeProvider? clock=null,IQualificationObservedOriginalReader? independentSourceReader=null)
        =>RunWithStartupBudgetAsync(independentReceivingInstallationPath,role,privateRoleSigner,independentOriginals,concreteCaseDrivers,clock,independentSourceReader,CancellationToken.None);
    internal static async Task<int> RunWithStartupBudgetAsync(string independentReceivingInstallationPath,QualificationOwnerRole role,ECDsa privateRoleSigner,
        IQualificationOriginalStore independentOriginals,IReadOnlyDictionary<string,IQualificationOwnerCaseDriver> concreteCaseDrivers,TimeProvider? clock,IQualificationObservedOriginalReader? independentSourceReader,CancellationToken token,TimeSpan? startupTimeout=null,
        Func<IndependentAuthorityReceiverInstallation,IReadOnlyList<IIndependentOwnerOperationHandler>,CancellationToken,CancellationToken,Task<int>>? runCaptured=null,CancellationToken? startupToken=null)
    {
        CancellationToken lifetimeToken=token;using var budget=CancellationTokenSource.CreateLinkedTokenSource(token,startupToken??CancellationToken.None);if(startupToken is null) budget.CancelAfter(startupTimeout??TimeSpan.FromSeconds(30));token=budget.Token;
        if(concreteCaseDrivers.Any(pair=>pair.Key!=pair.Value.CaseId)) throw new ArgumentException("Exact closed concrete case registry required.");
        byte[] bytes=await BoundedRegularFile.ReadAsync(independentReceivingInstallationPath,1024*1024,token).ConfigureAwait(false);
        if(bytes.Length is <=0 or >1024*1024) return 2;CustodyBindingContract.RejectDuplicateProperties(bytes);
        var profile=System.Text.Json.JsonSerializer.Deserialize<IndependentAuthorityReceiverInstallation>(bytes,CustodyBindingContract.Json);
        if(profile is null) return 2;token.ThrowIfCancellationRequested();
        using var dapr=new Dapr.Client.DaprClientBuilder().Build();
        IQualificationAttemptAuthority? attempts=profile.CohortAttempt is { } binding?new DaprQualificationAttemptAuthority(new DaprSecretsInvoker(dapr),binding,clock??TimeProvider.System):null;
        IReadOnlyList<IIndependentOwnerOperationHandler> handlers=[new QualificationOwnerOperationHandler(role,privateRoleSigner,independentOriginals,concreteCaseDrivers,clock??TimeProvider.System,independentSourceReader,attempts)];
        // The already acquired profile is the accepted startup snapshot; delegation neither reopens it nor renews the deadline.
        return runCaptured is null
            ?await IndependentAuthorityHttpApplication.RunCapturedAsync(profile,null,handlers,null,token,lifetimeToken).ConfigureAwait(false)
            :await runCaptured(profile,handlers,token,lifetimeToken).ConfigureAwait(false);
    }
}
