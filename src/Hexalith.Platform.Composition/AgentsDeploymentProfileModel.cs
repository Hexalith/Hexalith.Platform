namespace Hexalith.Platform.Composition;

/// <summary>Accepted reference-only Agents deployment profile. Strings are references, not secret material.</summary>
/// <param name="Services">Sibling source roots for EventStore, Tenants, Parties, Conversations, and Agents.</param>
/// <param name="CurrentTenant">Current tenant reference.</param>
/// <param name="GlobalAdministrator">Global administrator reference bound to Tenants bootstrap.</param>
/// <param name="HumanIdentity">Human identity reference.</param>
/// <param name="ApproverEvidence">Approver evidence reference. This is not an approved exact-Conversation or class/time-range scope.</param>
/// <param name="ReplayReference">Private replay registrar reference.</param>
/// <param name="RecorderReference">Private denial recorder reference.</param>
/// <param name="WorkerReference">Private spool worker reference.</param>
/// <param name="SpoolBackend">Replicated spool backend reference.</param>
/// <param name="ReplicaCount">Configured spool replica count.</param>
/// <param name="FailureModel">Spool failure-model reference.</param>
/// <param name="ProtectionEngine">Protection engine reference. No engine is installed by validation.</param>
/// <param name="ProtectionObserver">Independent protection observer reference.</param>
/// <param name="PartiesBinding">Installed Parties runtime binding reference.</param>
/// <param name="ConversationsBinding">Installed Conversations runtime binding reference.</param>
public sealed record AgentsDeploymentProfileModel(
    IReadOnlyDictionary<string, string> Services,
    string CurrentTenant,
    string GlobalAdministrator,
    string HumanIdentity,
    string ApproverEvidence,
    string ReplayReference,
    string RecorderReference,
    string WorkerReference,
    string SpoolBackend,
    int ReplicaCount,
    string FailureModel,
    string ProtectionEngine,
    string ProtectionObserver,
    string PartiesBinding,
    string ConversationsBinding,
    string IntegrationDate = "")
{
    internal bool AcceptedContextValidated { get; init; }
}
