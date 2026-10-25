using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Separately authenticated original-store captures for the three receiving roles. Sharing an authority installation never broadens a credential's role.</summary>
public sealed record JointPhaseOriginalStores(IQualificationOriginalStore Executor,IQualificationOriginalStore Secrets,IQualificationOriginalStore Command)
{
    /// <summary>Builds only explicit disjoint credential bindings; missing bindings deny production composition.</summary>
    public static JointPhaseOriginalStores Create(ISecretsCredentialBoundDaprInvoker transport,IndependentDaprAuthorityBinding executor,IndependentDaprAuthorityBinding secrets,IndependentDaprAuthorityBinding command,TimeProvider clock)
    {
        if(new[]{executor.CredentialReference,secrets.CredentialReference,command.CredentialReference}.Distinct(StringComparer.Ordinal).Count()!=3)
            throw new ArgumentException("DistinctExecutorJointSecretAndCommandOriginalCredentials");
        return new(new InstalledQualificationOriginalStore(new(transport,executor,clock)),new InstalledQualificationOriginalStore(new(transport,secrets,clock)),new InstalledQualificationOriginalStore(new(transport,command,clock)));
    }
}
