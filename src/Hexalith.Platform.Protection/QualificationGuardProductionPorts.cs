using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Concrete actual guard, protection and signer ports plus the closed recipe workflow.</summary>
public sealed record QualificationGuardProductionPorts(IGovernanceScopeGuard Guard,IDeletionProtectionOwner Protection,IDeletionCapabilitySigningActor Signing,
    QualificationGuardCaseWorkflow Workflow,IReadOnlyDictionary<string,QualificationGuardCaseDefinition> Definitions,QualificationGuardedOriginalReader NativeOriginals);
