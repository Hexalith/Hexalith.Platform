namespace Hexalith.Platform.Custody;

/// <summary>Authenticated identity and role evidence; authorization remains the consuming owner's responsibility.</summary>
/// <param name="Kind">Exactly one principal kind.</param>
/// <param name="ActorTenantId">Human authority tenant; absent for automation.</param>
/// <param name="HumanActorId">Stable human actor, absent for automation.</param>
/// <param name="PartyId">Party identity for User only.</param>
/// <param name="BindingVersion">Historical human/Party binding version for User only.</param>
/// <param name="RoleBasis">Current role authority version for humans.</param>
/// <param name="WorkflowKind">Closed automation kind.</param>
/// <param name="WorkflowInstanceId">Exact workflow identity.</param>
/// <param name="Activity">Exact activity.</param>
/// <param name="OnBehalfOfPartyId">Current step initiator for Interaction only.</param>
public sealed record TrustedPrincipal(TrustedPrincipalKind Kind, string? ActorTenantId, string? HumanActorId,
    string? PartyId, long? BindingVersion, string? RoleBasis, string? WorkflowKind, string? WorkflowInstanceId,
    string? Activity, string? OnBehalfOfPartyId)
{
    /// <summary>Checks closed shape only; it never infers an operation allowlist or fresh authority.</summary>
    public bool IsValid(string targetTenant)
    {
        bool human = !string.IsNullOrWhiteSpace(HumanActorId) && !string.IsNullOrWhiteSpace(RoleBasis)
            && !string.IsNullOrWhiteSpace(ActorTenantId) && WorkflowKind is null && WorkflowInstanceId is null
            && Activity is null && OnBehalfOfPartyId is null;
        return Kind switch
        {
            TrustedPrincipalKind.User => human && ActorTenantId == targetTenant && !string.IsNullOrWhiteSpace(PartyId) && BindingVersion > 0,
            TrustedPrincipalKind.Administrator => human && ActorTenantId == targetTenant && PartyId is null && BindingVersion is null,
            TrustedPrincipalKind.Platform => human && ActorTenantId == "system" && PartyId is null && BindingVersion is null,
            TrustedPrincipalKind.Workflow => HumanActorId is null && ActorTenantId is null && PartyId is null && BindingVersion is null
                && RoleBasis is null && !string.IsNullOrWhiteSpace(WorkflowInstanceId) && !string.IsNullOrWhiteSpace(Activity)
                && (WorkflowKind == "Interaction" ? !string.IsNullOrWhiteSpace(OnBehalfOfPartyId)
                    : WorkflowKind is "SystemTimer" or "GovernanceProtection" or "InteractionDirectoryMigration" or "ConversationDeletionPropagation"
                        && OnBehalfOfPartyId is null),
            _ => false,
        };
    }
}
