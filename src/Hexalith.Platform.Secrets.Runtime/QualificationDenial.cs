namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Qualification stays unavailable for all-copy and nonrollback. Inventory and profile name themselves on their own adapters.</summary>
public sealed record QualificationDenial(int StatusCode, IReadOnlyList<string> MissingInputs, int QualificationEffects)
{
    /// <summary>Gets whether production mutation is allowed. This delivery never allows it.</summary>
    public bool ProductionMutationAllowed { get; }

    /// <summary>Names the qualification blockers. A missing inventory or profile reference is not attributed to this check.</summary>
    public static QualificationDenial Evaluate(SecretsRuntimeConfiguration? configuration)
    {
        configuration ??= new SecretsRuntimeConfiguration();
        var missing = new List<string>();
        if (!SecretsProvisioning.IsReference(configuration.AllCopyLifecycleAuthorityReference)) { missing.Add("runtime.allCopyLifecycleAuthorityReference"); }
        if (!SecretsProvisioning.IsReference(configuration.NonrollbackJournalReference)) { missing.Add("runtime.nonrollbackJournalReference"); }
        if (!SecretsProvisioning.IsReference(configuration.AcceptedReplacementCommit)) { missing.Add("acceptedReplacementCommit"); }
        return new QualificationDenial(missing.Count == 0 ? 200 : 503, missing, 0);
    }
}
