using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Explicit production receiving inputs. Unknown native source/policy and restore protocols are never inferred or installed at startup.</summary>
public sealed record QualificationProtectionInstallation(Uri? EventStoreEndpoint=null,QualificationMountedReadCredentials? GatewayCredentials=null,
    CustodyLifecycleInstallation? ReadAuthority=null,Dictionary<string,QualificationEventStoreAuditScope>? AuditScopes=null,
    GuardedStateTransactionTarget? GuardTarget=null,long GuardTargetRevision=0,CustodyLifecycleInstallation? GuardTargetAuthority=null,
    Dictionary<string,CustodyLifecycleInstallation>? GuardCellAuthorities=null,Dictionary<string,CustodyLifecycleInstallation>? GuardReceiptAuthorities=null,
    string? IndependentEvidenceComponent=null,Dictionary<string,QualificationGuardEvidenceEnrollment>? EvidenceResources=null,
    Dictionary<string,CustodyLifecycleInstallation>? EvidenceAuthorities=null,Dictionary<string,Dictionary<string,System.Text.Json.JsonElement>>? GuardArguments=null,
    Dictionary<string,QualificationGuardCaseDefinition>? GuardDefinitions=null,Hexalith.Platform.Custody.DeletionCapabilityRevocationSubscription? RevocationSubscription=null,Dictionary<string,QualificationReplayCaseInput>? ReplayCases=null);
