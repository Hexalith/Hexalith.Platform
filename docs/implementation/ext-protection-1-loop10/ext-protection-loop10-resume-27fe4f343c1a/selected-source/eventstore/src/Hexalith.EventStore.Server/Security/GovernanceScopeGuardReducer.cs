using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Pure closed technical guard protocol. It enforces exact current facts at the joint source/guard commit; independent evidence never replaces these transitions.</summary>
public static class GovernanceScopeGuardReducer
{
    /// <summary>Returns a detached next guard or an exact no-effect result. Product branches with missing/Open disposition remain blocked.</summary>
    public static GovernanceGuardReduction Reduce(TenantGovernanceGuardState state, GovernanceGuardTransition command,
        GovernanceGuardEvidence evidence, string intentDigest)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(evidence);
        if (!Shape(command) || state.TenantId != command.TenantId || state.Revision <= 0 || state.InstallationId.Length == 0
            || state.Deletions is null || state.Deletions.Any(value => value is null || value.AdmissionFenceGuardRevision <= 0)
            || evidence.TenantId != state.TenantId || evidence.IntentDigest != intentDigest || !Text(evidence.AuthorityReceiptId))
        { return Result(state, command, intentDigest, "Unavailable", false); }
        var deletion = state.Deletions.SingleOrDefault(value => value.RequestId == command.DeletionRequestId);
        var authorization = state.Authorizations.SingleOrDefault(value => value.AuthorizationId == command.AuthorizationId);
        if (command.Operation == GovernanceGuardOperation.CommitContentBinding && authorization is not null && deletion is not null
            && (authorization.Ordinal != deletion.Ordinal || authorization.PredecessorBindingId != (Latest(deletion)?.BindingId ?? "")))
        { return Result(state, command, intentDigest, "Obsolete", false); }
        if (command.ExpectedGuardRevision != state.Revision) { return Result(state, command, intentDigest, "Stale", false); }
        if (state.Receipts.Any(value => value.OperationId == command.OperationId)) { return Result(state, command, intentDigest, "Conflict", false); }
        long next = checked(state.Revision + 1);
        string reference = command.ReferenceId;
        string status = "Committed";
        bool writes = false;
        switch (command.Operation)
        {
            case GovernanceGuardOperation.InstallEpoch:
                if (Text(state.EpochId) || !Text(command.EpochId) || !Text(evidence.LegacyRevocationReceipt) || !Text(evidence.WriterEnforcementReceipt)) { return Denied(); }
                state = state with { EpochId = command.EpochId, LegacyRevocationReceipt = evidence.LegacyRevocationReceipt, WriterEnforcementReceipt = evidence.WriterEnforcementReceipt };
                break;
            case GovernanceGuardOperation.InstallRepairFence:
                var repair = command.Repair!;
                if (state.Repair is not null || repair.TenantId != state.TenantId || repair.EpochId != state.EpochId || repair.OperationId != command.OperationId
                    || repair.ExpectedRevision != state.Revision || !Text(repair.NamespaceCheckpoint) || !Text(repair.WriterRevocationReceipt)
                    || !Same(repair.Cohort, evidence.RepairCohort) || !ValidCohort(repair.Cohort, state.TenantId)) { return Denied(); }
                state = state with { Repair = new(command.OperationId, state.EpochId, repair.NamespaceCheckpoint, repair.Cohort.ToArray(), []) };
                break;
            case GovernanceGuardOperation.RecordBridgeDrain:
                if (state.Repair is null || command.EpochId != state.EpochId || command.BridgeOriginal is not { } drain
                    || reference != drain.OperationId || !state.Repair.Cohort.Any(value => Original(value) == drain)
                    || state.Repair.DrainedOriginals.Contains(drain)) { return Denied(); }
                state = state with { Repair = state.Repair with { DrainedOriginals = state.Repair.DrainedOriginals.Append(drain).ToArray() } };
                break;
            case GovernanceGuardOperation.ActivateSuccessor:
                if (state.Repair is null || !Text(command.EpochId) || command.EpochId == state.EpochId
                    || state.RepairHistory.Any(retired => retired.EpochId == command.EpochId)
                    || !state.Repair.Cohort.All(value => state.Repair.DrainedOriginals.Contains(Original(value)))
                    || !Text(evidence.LegacyRevocationReceipt) || !Text(evidence.WriterEnforcementReceipt)) { return Denied(); }
                state = state with { EpochId = command.EpochId, LegacyRevocationReceipt = evidence.LegacyRevocationReceipt,
                    WriterEnforcementReceipt = evidence.WriterEnforcementReceipt, RepairHistory = state.RepairHistory.Append(state.Repair).ToArray(), Repair = null };
                break;
            case GovernanceGuardOperation.AppendWrite:
                var facts = command.Write!;
                if (!ValidFacts(facts, state.TenantId) || facts.EpochId != state.EpochId || command.EpochId != state.EpochId || !Text(evidence.AppendResourceId) || !Hex(evidence.TargetMutationDigest)) { return Denied(); }
                if (state.Repair is null && command.BridgeOriginal is not null) { return Denied(); }
                if (state.Repair is not null && (command.BridgeOriginal is not { } bridge || reference != bridge.OperationId
                    || state.Repair.DrainedOriginals.Contains(bridge) || !state.Repair.Cohort.Any(value => Original(value) == bridge && value.Kind == facts.Kind
                    && value.Owner.ToString() == facts.PermitOwnerId && value.PermitId == facts.PermitId && value.EffectCapabilityId == facts.EffectCapabilityId
                    && value.OwnerRevision == facts.CapabilityOwnerRevision && value.SourceConversationId == facts.PermitSourceConversationId))) { return Denied(); }
                var matching = state.Deletions.Where(value => Matches(value.Scope, facts)).ToArray();
                if (matching.Any(value => Admission(facts.Kind) || Content(facts.Kind) && value.ContentBindings.Count > 0)) { return Denied(); }
                var attributions = matching.OrderBy(value => value.RequestId, StringComparer.Ordinal)
                    .Select(value => new GovernanceAdmissionAttribution(value.RequestId, value.Ordinal)).ToArray();
                if (!ValidAttributions(attributions)) { return Denied(); }
                long acceptedOrdinal = matching.Select(value => value.Ordinal).DefaultIfEmpty(0).Max();
                return Result(state, command, intentDigest, "Accepted", true, true, acceptedOrdinal, reference, evidence, attributions);
            case GovernanceGuardOperation.AuthorizeAdmissionFence:
                if (!ValidScope(command.Scope, state.TenantId) || deletion is not null || !Unique(evidence.RequiredOwnerIds) || evidence.RequiredOwnerIds.Count == 0
                    || !Unique(evidence.ObligationIds) || state.Repair is not null || !Text(state.EpochId)) { return Denied(); }
                AddAuthorization(GovernanceGuardOperation.CommitAdmissionFence, 1, "", "");
                break;
            case GovernanceGuardOperation.CommitAdmissionFence:
                if (!Authorized(GovernanceGuardOperation.CommitAdmissionFence) || deletion is not null || !ValidScope(command.Scope, state.TenantId)
                    || evidence.RequiredOwnerIds.Count == 0 || !Unique(evidence.RequiredOwnerIds) || !Unique(evidence.ObligationIds)) { return Denied(); }
                var admitted = new GovernanceDeletionState(command.DeletionRequestId, command.Scope!, Hash(command.Scope), 1,
                    evidence.RequiredOwnerIds.Order(StringComparer.Ordinal).ToArray(), evidence.ObligationIds.Order(StringComparer.Ordinal).ToArray(), [], [], [], "", [], false, false)
                    { AdmissionFenceGuardRevision = next };
                state = state with { Deletions = state.Deletions.Append(admitted).ToArray() }; reference = admitted.PredicateDigest;
                break;
            case GovernanceGuardOperation.RecordViolation:
                var ordinal = command.Ordinal!;
                if (deletion is null || ordinal.Ordinal != deletion.Ordinal || !Text(ordinal.ViolationId) || deletion.Violations.Any(value => value.ViolationId == ordinal.ViolationId)
                    || ordinal.ViolationKind is not ("Admission" or "Content") || ordinal.AcceptedAtOrdinal < 0 || ordinal.AcceptedAtGuardHighWater <= 0
                    || ordinal.AcceptedAtGuardHighWater > state.Revision || !Text(ordinal.ResourceId) || !Unique(ordinal.InvalidatedArtifactIds)) { return Denied(); }
                if (evidence.OriginalAcceptance is not { Status: "Accepted" } accepted || accepted.GuardHighWater != ordinal.AcceptedAtGuardHighWater
                    || !Text(accepted.ReceiptId)
                    || evidence.ViolationWriteFacts is null || !ValidFacts(evidence.ViolationWriteFacts, state.TenantId) || !Matches(deletion.Scope, evidence.ViolationWriteFacts)
                    || evidence.ViolationResourceId != ordinal.ResourceId
                    || (ordinal.ViolationKind == "Admission" ? !Admission(evidence.ViolationWriteFacts.Kind) : !Content(evidence.ViolationWriteFacts.Kind))
                    || accepted.OperationId != evidence.ViolationAcceptanceOperationId || !Text(evidence.ViolationAcceptanceOperationId)
                    || accepted.AcceptedWriteResourceId != ordinal.ResourceId || accepted.AcceptedWriteFacts is null
                    || !OriginalAttribution(accepted, deletion, ordinal.AcceptedAtOrdinal)
                    || deletion.Violations.Any(value => !Text(value.AcceptanceReceiptId) || value.AcceptanceReceiptId == accepted.ReceiptId)
                    || !Same(accepted.AcceptedWriteFacts, evidence.ViolationWriteFacts)
                    || !Hex(evidence.ViolationTargetMutationDigest) || accepted.AcceptedTargetMutationDigest != evidence.ViolationTargetMutationDigest
                    || !state.Receipts.Any(original => Same(original, accepted))) { return Denied(); }
                var latest = Latest(deletion);
                bool sealedDeletion = Text(deletion.SealId);
                if (sealedDeletion && ordinal.ViolationKind == "Admission" && !Text(evidence.ProtectionBlockReceiptId)) { return Denied(); }
                if (sealedDeletion && ordinal.ViolationKind == "Content" && (evidence.ViolationProtectionTarget is null
                    || evidence.ViolationProtectionTarget.TenantId != state.TenantId || evidence.ViolationProtectionTarget.AgentInteractionId != evidence.ViolationWriteFacts.AgentInteractionId
                    || !Text(evidence.ViolationProtectionTarget.TargetProtectionKeyAlias))) { return Denied(); }
                if (!sealedDeletion && (latest is null ? !Text(ordinal.NoCutReceiptId) || ordinal.NoCutReceiptId != evidence.NoCutReceiptId || ordinal.InvalidatedArtifactIds.Count != 0
                    : !Same(ordinal.InvalidatedArtifactIds.Order(StringComparer.Ordinal).ToArray(), new[] { latest.GlobalCutId, latest.TokenId }.Order(StringComparer.Ordinal).ToArray()) || Text(ordinal.NoCutReceiptId))) { return Denied(); }
                if (sealedDeletion && (ordinal.InvalidatedArtifactIds.Count != 0 || Text(ordinal.NoCutReceiptId))) { return Denied(); }
                long successor = sealedDeletion ? deletion.Ordinal : checked(deletion.Ordinal + 1);
                var violation = new GovernanceViolation(ordinal.ViolationId, deletion.Ordinal, successor, ordinal.ViolationKind, ordinal.AcceptedAtOrdinal,
                    ordinal.AcceptedAtGuardHighWater, ordinal.ResourceId, ordinal.InvalidatedArtifactIds.ToArray(), ordinal.NoCutReceiptId, evidence.AuthorityReceiptId)
                    { AcceptanceReceiptId = accepted.ReceiptId, ProtectionTarget = sealedDeletion && ordinal.ViolationKind == "Content" ? evidence.ViolationProtectionTarget : null };
                ReplaceDeletion(deletion with { Ordinal = successor, ObligationIds = sealedDeletion ? deletion.ObligationIds : deletion.ObligationIds.Append(ordinal.ViolationId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                    Violations = deletion.Violations.Append(violation).ToArray(), IntegrityCompromised = deletion.IntegrityCompromised || sealedDeletion && ordinal.ViolationKind == "Admission", Completed = false });
                reference = ordinal.ViolationId;
                break;
            case GovernanceGuardOperation.RecordOwnerCycleEffective:
                ordinal = command.Ordinal!;
                if (deletion is null || Text(deletion.SealId) || ordinal.Ordinal != deletion.Ordinal || !deletion.RequiredOwnerIds.Contains(ordinal.OwnerId, StringComparer.Ordinal)
                    || ordinal.ObligationDigest != Hash(deletion.ObligationIds) || deletion.OwnerCycles.Any(value => value.Ordinal == ordinal.Ordinal && value.OwnerId == ordinal.OwnerId)) { return Denied(); }
                ReplaceDeletion(deletion with { OwnerCycles = deletion.OwnerCycles.Append(new(ordinal.Ordinal, ordinal.OwnerId, ordinal.ObligationDigest, evidence.AuthorityReceiptId)).ToArray() });
                break;
            case GovernanceGuardOperation.AuthorizeContentBinding:
                if (deletion is null || Text(deletion.SealId) || !Ready(deletion, evidence) || !Cut(command.Ordinal!, deletion)) { return Denied(); }
                AddAuthorization(GovernanceGuardOperation.CommitContentBinding, deletion.Ordinal, Latest(deletion)?.BindingId ?? "", "");
                break;
            case GovernanceGuardOperation.CommitContentBinding:
                if (deletion is null || !Authorized(GovernanceGuardOperation.CommitContentBinding) || Text(deletion.SealId) || !Ready(deletion, evidence)
                    || !Cut(command.Ordinal!, deletion) || !GapFree(deletion, authorization!)) { return Denied(); }
                var binding = new GovernanceContentBinding(deletion.Ordinal, command.Ordinal!.GlobalCutId, command.Ordinal.TokenId,
                    Hash(new[] { state.TenantId, deletion.RequestId, deletion.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), command.Ordinal.GlobalCutId, command.Ordinal.TokenId }), Latest(deletion)?.BindingId ?? "");
                ReplaceDeletion(deletion with { ContentBindings = deletion.ContentBindings.Append(binding).ToArray() }); reference = binding.BindingId;
                break;
            case GovernanceGuardOperation.RegisterHold:
                var hold = command.Hold!;
                if (!ValidScope(hold.Scope, state.TenantId) || !Text(hold.HoldId) || !Text(hold.DispositionVersion) || state.Holds.Any(value => value.HoldId == hold.HoldId)) { return Denied(); }
                state = state with { Holds = state.Holds.Append(new(hold.HoldId, hold.Scope, hold.DispositionVersion, true,
                    state.Deletions.Any(value => Text(value.SealId) && Overlaps(value.Scope, hold.Scope)), "")).ToArray() };
                reference = hold.HoldId;
                break;
            case GovernanceGuardOperation.ReleaseHold:
                hold = command.Hold!;
                var registered = state.Holds.SingleOrDefault(value => value.HoldId == hold.HoldId);
                if (registered is null || !registered.Active || !Same(registered.Scope, hold.Scope) || registered.DispositionVersion != hold.DispositionVersion || !Text(hold.ReleaseReceiptId)
                    || hold.ReleaseReceiptId != evidence.AuthorityReceiptId) { return Denied(); }
                state = state with { Holds = state.Holds.Select(value => value.HoldId == hold.HoldId ? value with { Active = false, ReleaseReceiptId = hold.ReleaseReceiptId } : value).ToArray() };
                break;
            case GovernanceGuardOperation.AuthorizeDestructionStart:
                if (deletion is null || Text(deletion.SealId) || !CanSeal(deletion) || !ValidBatch(command.Batch!, state.TenantId) || command.Batch!.BatchKind != "AcceptedSet"
                    || command.Batch.BatchOrdinal != 0 || !HoldAllows(deletion, evidence)) { return Denied(); }
                AddAuthorization(GovernanceGuardOperation.CommitDestructionStart, deletion.Ordinal, Latest(deletion)!.BindingId, command.Batch.ManifestDigest);
                break;
            case GovernanceGuardOperation.CommitDestructionStart:
                if (deletion is null || !Authorized(GovernanceGuardOperation.CommitDestructionStart) || Text(deletion.SealId) || !CanSeal(deletion)
                    || !HoldAllows(deletion, evidence) || !ValidBatch(command.Batch!, state.TenantId) || authorization!.ManifestDigest != command.Batch!.ManifestDigest
                    || command.Batch.BatchKind != "AcceptedSet" || command.Batch.BatchOrdinal != 0) { return Denied(); }
                string sealId = DestructionSealId(state.TenantId, deletion.RequestId, deletion.Scope);
                if (command.Batch.BatchId != BatchId(deletion.RequestId, sealId, command.Batch)) { return Denied(); }
                ReplaceDeletion(deletion with { SealId = sealId, Batches = [Batch(command.Batch, "AwaitingAttestation")] }); reference = sealId;
                break;
            case GovernanceGuardOperation.AuthorizeContainment:
                if (deletion is null || !Text(deletion.SealId) || deletion.IntegrityCompromised || !HoldAllows(deletion, evidence) || !ValidBatch(command.Batch!, state.TenantId)
                    || command.Batch!.BatchKind != "Containment" || command.Batch.Targets.Count != 1 || !deletion.Violations.Any(value => value.Kind == "Content" && value.ResourceId == reference
                        && value.ProtectionTarget == command.Batch.Targets[0])) { return Denied(); }
                var coverage = deletion.Batches.SingleOrDefault(value => value.Targets.Contains(command.Batch.Targets[0]));
                if (coverage is not null)
                {
                    ReplaceBatch(coverage with { CoveredResourceIds = coverage.CoveredResourceIds.Append(reference).Distinct(StringComparer.Ordinal).ToArray() });
                    return Result(state, command, intentDigest, "CoverageLinked", true, false, deletion.Ordinal, coverage.BatchId);
                }
                long nextOrdinal = checked(deletion.Batches.Where(value => value.Kind == "Containment").Select(value => value.Ordinal).DefaultIfEmpty(0).Max() + 1);
                if (command.Batch.BatchOrdinal != nextOrdinal || command.Batch.BatchId != BatchId(deletion.RequestId, deletion.SealId, command.Batch)) { return Denied(); }
                ReplaceDeletion(deletion with { Batches = deletion.Batches.Append(Batch(command.Batch, "AuthorizedContainment") with { CoveredResourceIds = [reference] }).ToArray() });
                AddAuthorization(GovernanceGuardOperation.CommitContainment, deletion.Ordinal, Latest(deletion)!.BindingId, command.Batch.ManifestDigest);
                break;
            case GovernanceGuardOperation.CommitContainment:
                var batch = FindBatch();
                if (deletion is null || batch is null || !Authorized(GovernanceGuardOperation.CommitContainment) || deletion.IntegrityCompromised || batch.ProtectionOutcome != "AuthorizedContainment"
                    || !ExactBatch(batch, command.Batch!) || !HoldAllows(deletion, evidence)) { return Denied(); }
                ReplaceBatch(batch with { ProtectionOutcome = "AwaitingAttestation" }); reference = batch.BatchId;
                break;
            case GovernanceGuardOperation.RecordBatchIssuanceStale:
                if (deletion is null || FindBatch() is null || !Text(command.AuthorizationId)) { return Denied(); }
                return Result(state, command, intentDigest, "IssuanceStale", false, false, deletion.Ordinal, command.Batch!.BatchId);
            case GovernanceGuardOperation.RecordBatchIssued:
                batch = FindBatch();
                if (deletion is null || batch is null || deletion.IntegrityCompromised || batch.ProtectionOutcome != "AwaitingAttestation" || !ExactBatch(batch, command.Batch!)
                    || command.Batch!.AttestationOrdinal != 1 || !HealthyAttestation(command.Batch) || !CapabilityMatches(command.Batch, deletion)) { return Denied(); }
                ReplaceBatch(batch with { AttestationOrdinal = command.Batch.AttestationOrdinal, CapabilityKeyVersion = command.Batch.CapabilityKeyVersion,
                    AttestationDigest = command.Batch.AttestationDigest, IssueReceiptId = ReceiptId(state, command, intentDigest, "Committed", next), ProtectionOutcome = "Unconsumed", Capability = command.Batch.Capability,
                    DetachedJws = command.Batch.DetachedJws, SigningRequestId = command.Batch.SigningRequestId, IssuedGuardRevision = next }); reference = batch.BatchId;
                break;
            case GovernanceGuardOperation.AuthorizeDispatch:
                batch = FindBatch();
                if (deletion is null || batch is null || deletion.IntegrityCompromised || !HoldAllows(deletion, evidence) || !Dispatchable(batch) || !ExactAttestation(batch, command.Batch!)) { return Denied(); }
                AddAuthorization(GovernanceGuardOperation.CommitDispatch, deletion.Ordinal, Latest(deletion)!.BindingId, batch.ManifestDigest);
                break;
            case GovernanceGuardOperation.CommitDispatch:
                batch = FindBatch();
                if (deletion is null || batch is null || !Authorized(GovernanceGuardOperation.CommitDispatch) || deletion.IntegrityCompromised || !HoldAllows(deletion, evidence)
                    || !Dispatchable(batch) || !ExactAttestation(batch, command.Batch!)) { return Denied(); }
                ReplaceBatch(batch with { DispatchReceiptId = ReceiptId(state, command, intentDigest, "Committed", next), DispatchGuardRevision = next }); reference = batch.BatchId;
                break;
            case GovernanceGuardOperation.RecordKeyCompromise:
                var revocation = command.RevocationReceipt;
                if (!Text(command.Batch!.CapabilityKeyVersion) || !Text(evidence.ProtectionBlockReceiptId) || command.Batch.BlockSetRevision <= 0
                    || revocation is null || evidence.RevocationReceipt is null || Hash(revocation) != Hash(evidence.RevocationReceipt)
                    || revocation.Envelope.TenantId != state.TenantId || revocation.Envelope.KeyFamily != "DeletionBatchCapabilitySigningKey"
                    || revocation.Envelope.KeyVersion != command.Batch.CapabilityKeyVersion || revocation.Envelope.RevocationRevision <= 0 || revocation.Envelope.TrustProfileRevision <= 0
                    || revocation.OwnerRevision <= 0 || revocation.KeyBlockSetRevision != command.Batch.BlockSetRevision || revocation.ReceiptId != evidence.ProtectionBlockReceiptId
                    || !Unique(revocation.AffectedBatchIds) || !Same(revocation.AffectedBatchIds, revocation.AffectedBatchIds.Order(StringComparer.Ordinal).ToArray())
                    || state.Revocations.Any(prior => prior.Envelope.EventIdentity == revocation.Envelope.EventIdentity
                        || prior.Envelope.KeyVersion == revocation.Envelope.KeyVersion && prior.Envelope.RevocationRevision == revocation.Envelope.RevocationRevision)) { return Denied(); }
                state = state with { CompromisedKeyVersions = state.CompromisedKeyVersions.Append(command.Batch.CapabilityKeyVersion).Distinct(StringComparer.Ordinal).ToArray(), Revocations = state.Revocations.Append(revocation).ToArray(), Deletions = state.Deletions.Select(value => value with
                { Batches = value.Batches.Select(item => item.CapabilityKeyVersion == command.Batch.CapabilityKeyVersion && !Terminal(item) && item.ProtectionOutcome is not ("ConsumptionBlocked:CapabilityKeyCompromise" or "ReplacementAwaitingActivation")
                    ? item with { ProtectionOutcome = "ConsumptionBlocked:CapabilityKeyCompromise", ProtectionReceiptId = revocation.AffectedBatchIds.Contains(item.BatchId, StringComparer.Ordinal) ? Hash(new[] { revocation.ReceiptId, item.BatchId }) : "",
                        BlockSetRevision = command.Batch.BlockSetRevision } : item).ToArray() }).ToArray() };
                break;
            case GovernanceGuardOperation.ReplaceAttestation:
                batch = FindBatch();
                if (deletion is null || batch is null || deletion.IntegrityCompromised || batch.ProtectionOutcome != "ConsumptionBlocked:CapabilityKeyCompromise" || !ExactBatch(batch, command.Batch!)
                    || command.Batch!.AttestationOrdinal != batch.AttestationOrdinal + 1 || command.Batch.BlockSetRevision != batch.BlockSetRevision || !HealthyAttestation(command.Batch)
                    || !CapabilityMatches(command.Batch, deletion)) { return Denied(); }
                ReplaceBatch(batch with { AttestationOrdinal = command.Batch.AttestationOrdinal, CapabilityKeyVersion = command.Batch.CapabilityKeyVersion, AttestationDigest = command.Batch.AttestationDigest,
                    IssueReceiptId = ReceiptId(state, command, intentDigest, "Committed", next), DispatchReceiptId = "", ProtectionOutcome = "ReplacementAwaitingActivation", Capability = command.Batch.Capability,
                    DetachedJws = command.Batch.DetachedJws, SigningRequestId = command.Batch.SigningRequestId, IssuedGuardRevision = next, DispatchGuardRevision = 0, RejectedUnissuedReplacement = null }); reference = batch.BatchId;
                break;
            case GovernanceGuardOperation.RecordProtectionOutcome:
                batch = FindBatch();
                if (deletion is null || batch is null || !ExactAttestation(batch, command.Batch!) || !Text(command.Batch!.ProtectionReceiptId) || command.Batch.ProtectionReceiptId != evidence.AuthorityReceiptId
                    || Terminal(batch)) { return Denied(); }
                if (command.Batch.ProtectionOutcome == "UnissuedReplacementBlocked")
                {
                    if (batch.ProtectionOutcome != "ConsumptionBlocked:CapabilityKeyCompromise" || !UnissuedReplacementProof(state, batch, command.Batch, evidence)) { return Denied(); }
                    ReplaceBatch(batch with { ProtectionReceiptId = command.Batch.ProtectionReceiptId, BlockSetRevision = command.Batch.BlockSetRevision,
                        RejectedUnissuedReplacement = evidence.ProtectionBlockedReplacement });
                }
                else if (command.Batch.ProtectionOutcome == "Activated")
                {
                    if (batch.ProtectionOutcome != "ReplacementAwaitingActivation" || !Text(batch.DispatchReceiptId) || command.Batch.BlockSetRevision < batch.BlockSetRevision
                        || command.Batch.BlockSetRevision != batch.BlockSetRevision && !ActivationProof(batch, command.Batch, evidence)
                        || state.CompromisedKeyVersions.Contains(batch.CapabilityKeyVersion, StringComparer.Ordinal)) { return Denied(); }
                    ReplaceBatch(batch with { ProtectionOutcome = "Unconsumed", ProtectionReceiptId = command.Batch.ProtectionReceiptId, BlockSetRevision = command.Batch.BlockSetRevision });
                }
                else if (command.Batch.ProtectionOutcome == "ActivationBlockedByReplacementKeyCompromise")
                {
                    if (batch.ProtectionOutcome != "ReplacementAwaitingActivation" || command.Batch.BlockSetRevision <= batch.BlockSetRevision
                        || !BlockedReplacementProof(state, batch, command.Batch, evidence)) { return Denied(); }
                    state = state with { CompromisedKeyVersions = state.CompromisedKeyVersions.Append(batch.CapabilityKeyVersion).Distinct(StringComparer.Ordinal).ToArray() };
                    ReplaceBatch(batch with { ProtectionOutcome = "ConsumptionBlocked:CapabilityKeyCompromise", ProtectionReceiptId = command.Batch.ProtectionReceiptId,
                        BlockSetRevision = command.Batch.BlockSetRevision, DispatchReceiptId = "" });
                }
                else
                {
                    bool originalTerminal = OriginalTerminalProof(batch, command.Batch, evidence);
                    if (deletion.IntegrityCompromised || batch.ProtectionOutcome != "Unconsumed" && !(batch.ProtectionOutcome == "ConsumptionBlocked:CapabilityKeyCompromise" && originalTerminal)
                        || !Text(batch.DispatchReceiptId) || command.Batch.ProtectionOutcome is not ("Consumed" or "AlreadyDestroyed") || command.Batch.TargetReceiptIds.Count != batch.Targets.Count
                        || !Unique(command.Batch.TargetReceiptIds) || state.CompromisedKeyVersions.Contains(batch.CapabilityKeyVersion, StringComparer.Ordinal) && !originalTerminal) { return Denied(); }
                    ReplaceBatch(batch with { ProtectionOutcome = command.Batch.ProtectionOutcome, ProtectionReceiptId = command.Batch.ProtectionReceiptId, TargetReceiptIds = command.Batch.TargetReceiptIds.ToArray() });
                }
                break;
            case GovernanceGuardOperation.AuthorizeCompletion:
                if (deletion is null || !CompleteProof(deletion)) { return Denied(); }
                AddAuthorization(GovernanceGuardOperation.CommitCompletion, deletion.Ordinal, Latest(deletion)!.BindingId, "");
                break;
            case GovernanceGuardOperation.CommitCompletion:
                if (deletion is null || !Authorized(GovernanceGuardOperation.CommitCompletion) || !CompleteProof(deletion)) { return Denied(); }
                ReplaceDeletion(deletion with { Completed = true }); status = "CompletionSealed"; reference = deletion.RequestId;
                break;
            default: return Denied();
        }
        return Result(state, command, intentDigest, status, true, writes, deletion?.Ordinal ?? 0, reference);

        GovernanceGuardReduction Denied() => Result(state, command, intentDigest, "Blocked", false);
        void ReplaceDeletion(GovernanceDeletionState value) { deletion = value; state = state with { Deletions = state.Deletions.Select(item => item.RequestId == value.RequestId ? value : item).ToArray() }; }
        GovernanceBatchState? FindBatch() => deletion?.Batches.SingleOrDefault(value => value.BatchId == command.Batch?.BatchId);
        void ReplaceBatch(GovernanceBatchState value) => ReplaceDeletion(deletion! with { Batches = deletion!.Batches.Select(item => item.BatchId == value.BatchId ? value : item).ToArray() });
        bool Authorized(GovernanceGuardOperation effect) => authorization is not null && authorization.Effect == effect && authorization.RequestId == command.DeletionRequestId
            && authorization.BatchId == (command.Batch?.BatchId ?? "") && authorization.GuardRevision == state.Revision && authorization.Ordinal == (deletion?.Ordinal ?? 1)
            && authorization.DispositionVersion == evidence.DispositionVersion && authorization.EffectBasisDigest == EffectBasis();
        void AddAuthorization(GovernanceGuardOperation effect, long ordinalValue, string predecessor, string manifest)
        {
            if (!Text(command.AuthorizationId) || state.Authorizations.Any(value => value.AuthorizationId == command.AuthorizationId)) { throw new ArgumentException("Authorization identity conflict."); }
            state = state with { Authorizations = state.Authorizations.Append(new(command.AuthorizationId, effect, command.DeletionRequestId, command.Batch?.BatchId ?? "", next,
                ordinalValue, predecessor, manifest, evidence.DispositionVersion, evidence.AuthorityReceiptId, EffectBasis())).ToArray() };
        }
        string EffectBasis() => Hash(new { command.Scope, command.Ordinal, command.Batch, evidence.RequiredOwnerIds, evidence.ObligationIds, evidence.DispositionVersion });
        bool HoldAllows(GovernanceDeletionState value, GovernanceGuardEvidence proof)
        {
            var holds = state.Holds.Where(item => item.Active && Overlaps(item.Scope, value.Scope)).ToArray();
            return holds.Length == 0 || proof.Disposition == "DeletionAllowed" && Text(proof.DispositionVersion) && holds.All(item => item.DispositionVersion == proof.DispositionVersion);
        }
        bool CanSeal(GovernanceDeletionState value) => state.Repair is null && Text(state.EpochId) && !value.IntegrityCompromised && !value.Completed && Ready(value, evidence)
            && Latest(value)?.Ordinal == value.Ordinal;
        bool HealthyAttestation(GovernanceBatchCommand value) => Text(value.CapabilityKeyVersion) && Hex(value.AttestationDigest) && !state.CompromisedKeyVersions.Contains(value.CapabilityKeyVersion, StringComparer.Ordinal);
        bool CapabilityMatches(GovernanceBatchCommand value, GovernanceDeletionState request)
        {
            var payload = value.Capability;
            return payload is not null && Text(evidence.CapabilityIssuer) && Text(evidence.CapabilityAudience) && Text(evidence.GuardStreamId)
                && payload.Issuer == evidence.CapabilityIssuer && payload.Audience == evidence.CapabilityAudience && payload.GuardStreamId == evidence.GuardStreamId
                && payload.TenantId == state.TenantId && payload.DeletionRequestId == request.RequestId && payload.DestructionSealId == request.SealId
                && payload.BatchId == value.BatchId && payload.BatchKind == value.BatchKind && payload.BatchOrdinal == value.BatchOrdinal && payload.ManifestDigest == value.ManifestDigest
                && payload.AttestationOrdinal == value.AttestationOrdinal && payload.CapabilityKeyVersion == value.CapabilityKeyVersion && payload.IntendedIssuedGuardRevision == state.Revision
                && payload.SigningAttemptOrdinal > 0 && value.SigningRequestId == DeletionBatchCapabilityIdentity.SigningRequestId(payload)
                && value.DetachedJws is { Length: > 0 and <= 16384 } && value.AttestationDigest == Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value.DetachedJws)));
        }
        bool Dispatchable(GovernanceBatchState value) => Text(value.IssueReceiptId) && Hex(value.AttestationDigest) && value.ProtectionOutcome is "Unconsumed" or "ReplacementAwaitingActivation"
            && !state.CompromisedKeyVersions.Contains(value.CapabilityKeyVersion, StringComparer.Ordinal);
        bool CompleteProof(GovernanceDeletionState value) => Text(value.SealId) && !value.Completed && !value.IntegrityCompromised && state.Repair is null && HoldAllows(value, evidence)
            && Ready(value, evidence) && Latest(value)?.Ordinal == value.Ordinal && value.Batches.Count > 0 && value.Batches.All(Terminal)
            && Unique(evidence.RequiredOutcomeReceiptIds) && evidence.RequiredOutcomeReceiptIds.Count > 0
            && value.Batches.All(item => evidence.RequiredOutcomeReceiptIds.Contains(item.ProtectionReceiptId, StringComparer.Ordinal))
            && value.Violations.Where(item => item.Kind == "Content" && item.SuccessorOrdinal == item.Ordinal)
                .All(item => value.Batches.Any(batch => Terminal(batch) && batch.CoveredResourceIds.Contains(item.ResourceId, StringComparer.Ordinal)));
    }

    /// <summary>Canonical technical scope digest used by the owning transition and installed request.</summary>
    public static string PredicateDigest(GovernanceScopeV1 scope)
    { ArgumentNullException.ThrowIfNull(scope); return ValidScope(scope, scope.TenantId) ? Hash(scope) : throw new ArgumentException("Unsupported canonical scope."); }
    /// <summary>Deterministic original seal identity shared with preparation; actual authority still requires the conditional destruction-start commit.</summary>
    public static string DestructionSealId(string tenantId, string requestId, GovernanceScopeV1 scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId); ArgumentException.ThrowIfNullOrWhiteSpace(requestId); ArgumentNullException.ThrowIfNull(scope);
        if (scope.TenantId != tenantId) { throw new ArgumentException("Foreign seal scope.", nameof(scope)); }
        return Hash(new[] { tenantId, requestId, PredicateDigest(scope), "DestructionSealed" });
    }
    /// <summary>Canonical sorted target manifest digest; provider-neutral complete identities are retained.</summary>
    public static string ManifestDigest(IReadOnlyList<GovernanceProtectionTarget> targets)
    { ArgumentNullException.ThrowIfNull(targets); return DeletionBatchCapabilityIdentity.TargetManifestDigest(targets.Select(value => new ProtectionTarget(value.TenantId, value.AgentInteractionId, value.TargetProtectionKeyAlias)).ToArray()); }
    /// <summary>Deterministic non-expiring immutable accepted/containment batch identity.</summary>
    public static string BatchId(string requestId, string sealId, GovernanceBatchCommand batch)
    { ArgumentNullException.ThrowIfNull(batch); return Hash(new[] { requestId, sealId, batch.BatchKind, batch.BatchOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture), batch.ManifestDigest }); }
    /// <summary>Exact obligation set digest for separately keyed Effective owner cycles.</summary>
    public static string ObligationDigest(IReadOnlyList<string> obligations) => Hash(obligations);
    /// <summary>Rejects malformed or unsupported caller shapes before a restricted owner lookup.</summary>
    public static bool IsClosedCommand(GovernanceGuardTransition command) => command is not null && Shape(command)
        && (command.Scope is null || ValidScope(command.Scope, command.TenantId))
        && (command.Write is null || ValidFacts(command.Write, command.TenantId))
        && (command.Hold is null || ValidScope(command.Hold.Scope, command.TenantId));
    private static GovernanceGuardReduction Result(TenantGovernanceGuardState state, GovernanceGuardTransition command, string digest, string status,
        bool mutate, bool writes = false, long ordinal = 0, string reference = "", GovernanceGuardEvidence? acceptance = null,
        IReadOnlyList<GovernanceAdmissionAttribution>? attributions = null)
    {
        long revision = checked(state.Revision + (mutate ? 1 : 0));
        var receipt = new GovernanceProtocolReceipt(command.OperationId, digest, status, revision, ordinal, reference, ReceiptId(state, command, digest, status, revision))
            { AdmissionAttributionsJson = JsonSerializer.Serialize(attributions ?? []), AcceptedWriteResourceId = acceptance?.AppendResourceId ?? "", AcceptedWriteFacts = acceptance is null ? null : command.Write,
                AcceptedTargetMutationDigest = acceptance?.TargetMutationDigest ?? "", Capability = command.Batch?.Capability, SigningRequestId = command.Batch?.SigningRequestId ?? "",
                DetachedJwsDigest = command.Batch?.DetachedJws is { Length: > 0 } jws ? Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(jws))) : "" };
        return new(mutate ? state with { Revision = revision, Receipts = state.Receipts.Append(receipt).ToArray() } : state, receipt, mutate, writes);
    }
    private static bool OriginalAttribution(GovernanceProtocolReceipt accepted, GovernanceDeletionState deletion, long ordinal)
    {
        if (deletion.AdmissionFenceGuardRevision <= 0 || accepted.AdmissionAttributionsJson is null
            || accepted.AdmissionAttributionsJson.Length > 65536) { return false; }
        GovernanceAdmissionAttribution[]? attributions;
        try { attributions = JsonSerializer.Deserialize<GovernanceAdmissionAttribution[]>(accepted.AdmissionAttributionsJson); }
        catch (JsonException) { return false; }
        if (attributions is null || !ValidAttributions(attributions) || JsonSerializer.Serialize(attributions) != accepted.AdmissionAttributionsJson) { return false; }
        var installed = attributions.SingleOrDefault(value => value.DeletionRequestId == deletion.RequestId);
        return installed is null
            ? accepted.GuardHighWater < deletion.AdmissionFenceGuardRevision && ordinal == 0
            : accepted.GuardHighWater >= deletion.AdmissionFenceGuardRevision && installed.Ordinal == ordinal;
    }
    private static bool ValidAttributions(IReadOnlyList<GovernanceAdmissionAttribution> attributions)
        => attributions.Count <= 1000 && attributions.All(value => value is not null && Text(value.DeletionRequestId) && value.Ordinal > 0)
            && attributions.Select(value => value.DeletionRequestId).Distinct(StringComparer.Ordinal).Count() == attributions.Count
            && attributions.SequenceEqual(attributions.OrderBy(value => value.DeletionRequestId, StringComparer.Ordinal))
            && JsonSerializer.Serialize(attributions).Length <= 65536;
    private static bool Ready(GovernanceDeletionState deletion, GovernanceGuardEvidence evidence) => evidence.ZeroOrdinal == deletion.Ordinal && Text(evidence.CurrentZeroReceiptId)
        && !deletion.Violations.Any(value => value.Ordinal == deletion.Ordinal && value.SuccessorOrdinal > value.Ordinal)
        && deletion.RequiredOwnerIds.All(owner => deletion.OwnerCycles.Any(value => value.Ordinal == deletion.Ordinal && value.OwnerId == owner && value.ObligationDigest == Hash(deletion.ObligationIds)));
    private static bool GapFree(GovernanceDeletionState deletion, GovernanceAuthorization authorization)
    {
        long start = Latest(deletion)?.Ordinal ?? 1;
        if (authorization.PredecessorBindingId != (Latest(deletion)?.BindingId ?? "")) { return false; }
        for (long ordinal = start; ordinal < deletion.Ordinal; ordinal++)
        {
            var gap = deletion.Violations.Where(value => value.Ordinal == ordinal && value.SuccessorOrdinal == ordinal + 1).ToArray();
            if (gap.Length != 1 || !Text(gap[0].ReceiptId) || gap[0].InvalidatedArtifactIds.Count == 0 && !Text(gap[0].NoCutReceiptId)) { return false; }
        }
        return true;
    }
    private static bool Cut(GovernanceOrdinalCommand value, GovernanceDeletionState deletion) => value.Ordinal == deletion.Ordinal && Text(value.GlobalCutId) && Text(value.TokenId)
        && Latest(deletion)?.Ordinal != deletion.Ordinal;
    private static GovernanceContentBinding? Latest(GovernanceDeletionState value) => value.ContentBindings.LastOrDefault();
    private static bool ValidFacts(GovernanceWriteFacts value, string tenant) => value.TenantId == tenant && Text(value.AgentInteractionId) && Text(value.PermitSourceConversationId)
        && (value.Kind == DirectoryWriteKind.Create ? value.FirstEventSourceConversationId == "" || value.FirstEventSourceConversationId == value.PermitSourceConversationId
            : value.FirstEventSourceConversationId == value.PermitSourceConversationId)
        && new[] { value.PermitOwnerId, value.PermitId, value.EffectCapabilityId, value.EpochId, value.SourceReceiptId }.All(Text)
        && value.CapabilityOwnerRevision > 0 && Enum.IsDefined(value.Kind);
    private static bool ValidScope(GovernanceScopeV1? scope, string tenant) => scope is not null && scope.TenantId == tenant
        && (scope.Kind == "ExactInteraction" && Text(scope.AgentInteractionId) && scope.SourceConversationId == ""
            || scope.Kind == "SourceDeletionExactConversation" && Text(scope.SourceConversationId) && scope.AgentInteractionId == "");
    private static bool Matches(GovernanceScopeV1 scope, GovernanceWriteFacts facts) => scope.TenantId == facts.TenantId
        && (scope.Kind == "ExactInteraction" ? scope.AgentInteractionId == facts.AgentInteractionId : scope.SourceConversationId == facts.PermitSourceConversationId);
    private static bool Overlaps(GovernanceScopeV1 a, GovernanceScopeV1 b) => a.TenantId == b.TenantId && (a.Kind != b.Kind || Same(a, b)); // Mixed kinds conservatively contend until exact owner evidence narrows them.
    private static bool Admission(DirectoryWriteKind kind) => kind is DirectoryWriteKind.Permit or DirectoryWriteKind.UserActionIntent or DirectoryWriteKind.LeaseAcquire
        or DirectoryWriteKind.LeaseCommit or DirectoryWriteKind.RateAuthorization or DirectoryWriteKind.OpenAuthorization or DirectoryWriteKind.BudgetAuthorization or DirectoryWriteKind.CapacityAuthorization;
    private static bool Content(DirectoryWriteKind kind) => kind is DirectoryWriteKind.Create or DirectoryWriteKind.ContentAppend or DirectoryWriteKind.CreationOutbox;
    private static bool ValidCohort(IReadOnlyList<DirectoryRepairCohortItem> cohort, string tenant) => cohort is not null && cohort.Count <= 1000
        && cohort.All(value => value is not null && value.Owner is not null && value.Owner.TenantId == tenant && Enum.IsDefined(value.Kind) && value.OwnerRevision > 0 && value.OriginalState is "Pending" or "Committed" or "Authorized"
            && new[] { value.OperationId, value.SourceConversationId, value.PermitId, value.EffectCapabilityId }.All(Text))
        && cohort.Select(Original).Distinct().Count() == cohort.Count;
    private static DirectoryRepairOriginalIdentity Original(DirectoryRepairCohortItem item) => new(item.Owner, item.OperationId);
    private static bool ValidBatch(GovernanceBatchCommand batch, string tenant) => Text(batch.BatchId) && batch.Targets is { Count: > 0 and <= 1000 }
        && batch.Targets.All(value => value.TenantId == tenant && Text(value.AgentInteractionId) && Text(value.TargetProtectionKeyAlias))
        && batch.Targets.Distinct().Count() == batch.Targets.Count && Same(batch.Targets, batch.Targets.OrderBy(value => value.AgentInteractionId, StringComparer.Ordinal)
            .ThenBy(value => value.TargetProtectionKeyAlias, StringComparer.Ordinal).ToArray()) && batch.ManifestDigest == ManifestDigest(batch.Targets);
    private static GovernanceBatchState Batch(GovernanceBatchCommand value, string status) => new(value.BatchId, value.BatchKind, value.BatchOrdinal, value.Targets.ToArray(), value.ManifestDigest,
        0, "", "", "", "", status, "", [], 0, []);
    private static bool ExactBatch(GovernanceBatchState state, GovernanceBatchCommand command) => state.BatchId == command.BatchId && state.Kind == command.BatchKind
        && state.Ordinal == command.BatchOrdinal && state.ManifestDigest == command.ManifestDigest && Same(state.Targets, command.Targets);
    private static bool ExactAttestation(GovernanceBatchState state, GovernanceBatchCommand command) => ExactBatch(state, command) && state.AttestationOrdinal == command.AttestationOrdinal
        && state.CapabilityKeyVersion == command.CapabilityKeyVersion && state.AttestationDigest == command.AttestationDigest && state.Capability == command.Capability
        && state.DetachedJws == command.DetachedJws && state.SigningRequestId == command.SigningRequestId && state.IssuedGuardRevision > 0;
    private static bool ActivationProof(GovernanceBatchState batch, GovernanceBatchCommand command, GovernanceGuardEvidence evidence)
    {
        var activation = evidence.ProtectionActivationRequest; var outcome = evidence.ProtectionActivationOutcome; var request = activation?.Replacement;
        return activation is not null && outcome is not null && request is not null && activation.CompromiseBlockReceiptId == batch.ProtectionReceiptId
            && activation.GuardReplacementReceiptId == batch.IssueReceiptId && activation.ExpectedKeyBlockSetRevision == command.BlockSetRevision
            && request.Capability == batch.Capability && request.DetachedJws == batch.DetachedJws && request.CommittedIssuedGuardRevision == batch.IssuedGuardRevision
            && request.DispatchReceiptId == batch.DispatchReceiptId && request.DispatchGuardRevision == batch.DispatchGuardRevision
            && outcome.TenantId == batch.Capability?.TenantId && outcome.BatchId == batch.BatchId && outcome.Status == DeletionConsumptionStatus.Unconsumed
            && outcome.ReceiptId == command.ProtectionReceiptId && outcome.KeyBlockSetRevision == command.BlockSetRevision && outcome.OwnerRevision > 0
            && request.Targets.SequenceEqual(batch.Targets.Select(t => new ProtectionTarget(t.TenantId, t.AgentInteractionId, t.TargetProtectionKeyAlias)));
    }
    private static bool BlockedReplacementProof(TenantGovernanceGuardState state, GovernanceBatchState batch, GovernanceBatchCommand command, GovernanceGuardEvidence evidence)
    {
        var retained = evidence.ProtectionBlockedReplacement; var phase = retained?.Original; var outcome = retained?.Outcome;
        return phase is not null && outcome is not null && phase.CompromiseBlockReceiptId == batch.ProtectionReceiptId
            && phase.GuardReplacementReceiptId == batch.IssueReceiptId && phase.Capability == batch.Capability && phase.DetachedJws == batch.DetachedJws
            && phase.SigningRequestId == batch.SigningRequestId && phase.CommittedIssuedGuardRevision == batch.IssuedGuardRevision
            && phase.ExpectedKeyBlockSetRevision == command.BlockSetRevision && phase.Targets.SequenceEqual(batch.Targets.Select(t => new ProtectionTarget(t.TenantId, t.AgentInteractionId, t.TargetProtectionKeyAlias)))
            && phase.RevocationReceipt.Envelope.TenantId == state.TenantId && phase.RevocationReceipt.Envelope.KeyVersion == batch.CapabilityKeyVersion
            && phase.RevocationReceipt.KeyBlockSetRevision <= phase.ExpectedKeyBlockSetRevision && state.Revocations.Any(r => Hash(r) == Hash(phase.RevocationReceipt))
            && outcome.TenantId == state.TenantId && outcome.BatchId == batch.BatchId && outcome.Status == DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise
            && outcome.OwnerRevision > 0 && outcome.KeyBlockSetRevision == phase.ExpectedKeyBlockSetRevision && outcome.ReceiptId == command.ProtectionReceiptId
            && outcome.BlockReason == DeletionConsumptionBlockReason.CapabilityKeyCompromise && outcome.BlockedKeyVersion == batch.CapabilityKeyVersion
            && outcome.RevocationRevision == phase.RevocationReceipt.Envelope.RevocationRevision && outcome.TargetReceipts.Count == 0;
    }
    private static bool UnissuedReplacementProof(TenantGovernanceGuardState state, GovernanceBatchState batch, GovernanceBatchCommand command, GovernanceGuardEvidence evidence)
    {
        var original = evidence.ProtectionBlockedReplacement; var phase = original?.Original; var outcome = original?.Outcome;
        if (phase?.UnissuedProof is not { } refused || outcome is null || batch.Capability is not { } issued
            || phase.CompromiseBlockReceiptId != batch.ProtectionReceiptId || phase.CommittedIssuedGuardRevision != 0
            || batch.AttestationOrdinal == long.MaxValue
            || phase.Capability.AttestationOrdinal != batch.AttestationOrdinal + 1 || phase.Capability.BatchId != batch.BatchId
            || phase.Capability.ManifestDigest != batch.ManifestDigest || phase.Capability.DestructionSealId != issued.DestructionSealId
            || phase.Capability.DeletionRequestId != issued.DeletionRequestId || phase.Capability.TenantId != state.TenantId
            || phase.GuardReplacementReceiptId != refused.Refusal.ReceiptId || refused.Refusal.Status is not ("Blocked" or "Stale")
            || refused.Transition.Batch?.Capability != phase.Capability || refused.Refusal.SigningRequestId != phase.SigningRequestId
            || refused.CurrentGuardRevision != state.Revision || refused.CurrentGuardStateDigest != Hash(state)
            || !state.CompromisedKeyVersions.Contains(phase.Capability.CapabilityKeyVersion, StringComparer.Ordinal)
            || !state.Revocations.Any(value => Hash(value) == Hash(phase.RevocationReceipt))
            || state.Receipts.Any(value => value.Status == "Committed" && value.SigningRequestId == phase.SigningRequestId)
            || !phase.Targets.SequenceEqual(batch.Targets.Select(target => new ProtectionTarget(target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias)))
            || outcome.Status != DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise || outcome.TenantId != state.TenantId
            || outcome.BatchId != batch.BatchId || outcome.BlockedKeyVersion != phase.Capability.CapabilityKeyVersion || outcome.RevocationRevision != phase.RevocationReceipt.Envelope.RevocationRevision
            || outcome.BlockReason != DeletionConsumptionBlockReason.CapabilityKeyCompromise || outcome.TargetReceipts.Count != 0
            || outcome.KeyBlockSetRevision != phase.ExpectedKeyBlockSetRevision || outcome.KeyBlockSetRevision != command.BlockSetRevision
            || outcome.ReceiptId != command.ProtectionReceiptId || outcome.OwnerRevision <= 0) { return false; }
        return outcome.ReceiptId == DeletionConsumptionIdentity.Digest(new[] { phase.OperationId, Hash(phase), "blocked-unissued-replacement" });
    }
    private static bool OriginalTerminalProof(GovernanceBatchState batch, GovernanceBatchCommand command, GovernanceGuardEvidence evidence)
    {
        var request = evidence.ProtectionOriginalRequest; var outcome = evidence.ProtectionTerminalOutcome;
        return request is not null && outcome is not null && request.Capability == batch.Capability && request.DetachedJws == batch.DetachedJws
            && request.CommittedIssuedGuardRevision == batch.IssuedGuardRevision && request.DispatchReceiptId == batch.DispatchReceiptId
            && request.DispatchGuardRevision == batch.DispatchGuardRevision && outcome.TenantId == request.Capability.TenantId
            && outcome.BatchId == batch.BatchId && outcome.OwnerRevision > 0 && outcome.ReceiptId == command.ProtectionReceiptId
            && outcome.Status is DeletionConsumptionStatus.Consumed or DeletionConsumptionStatus.AlreadyDestroyedByBatch
            && outcome.TargetReceipts.Select(r => r.Target).SequenceEqual(request.Targets)
            && request.Targets.SequenceEqual(batch.Targets.Select(t => new ProtectionTarget(t.TenantId, t.AgentInteractionId, t.TargetProtectionKeyAlias)))
            && outcome.TargetReceipts.Select(r => r.ReceiptId).SequenceEqual(command.TargetReceiptIds);
    }
    private static bool Terminal(GovernanceBatchState value) => value.ProtectionOutcome is "Consumed" or "AlreadyDestroyed" && Text(value.ProtectionReceiptId)
        && value.TargetReceiptIds.Count == value.Targets.Count && Unique(value.TargetReceiptIds);
    private static bool Shape(GovernanceGuardTransition value)
    {
        if (!Text(value.TenantId) || !Text(value.OperationId) || value.ExpectedGuardRevision <= 0 || !Enum.IsDefined(value.Operation)) { return false; }
        if (value.BridgeOriginal is { } original && (value.Operation is not (GovernanceGuardOperation.AppendWrite or GovernanceGuardOperation.RecordBridgeDrain)
            || original.Owner is null || original.Owner.TenantId != value.TenantId || !Text(original.OperationId))) { return false; }
        bool write = value.Operation == GovernanceGuardOperation.AppendWrite;
        bool repair = value.Operation == GovernanceGuardOperation.InstallRepairFence;
        bool ordinal = value.Operation is GovernanceGuardOperation.RecordViolation or GovernanceGuardOperation.RecordOwnerCycleEffective or GovernanceGuardOperation.AuthorizeContentBinding or GovernanceGuardOperation.CommitContentBinding;
        bool hold = value.Operation is GovernanceGuardOperation.RegisterHold or GovernanceGuardOperation.ReleaseHold;
        bool batch = value.Operation is GovernanceGuardOperation.AuthorizeDestructionStart or GovernanceGuardOperation.CommitDestructionStart or GovernanceGuardOperation.AuthorizeContainment
            or GovernanceGuardOperation.CommitContainment or GovernanceGuardOperation.RecordBatchIssued or GovernanceGuardOperation.RecordBatchIssuanceStale or GovernanceGuardOperation.AuthorizeDispatch
            or GovernanceGuardOperation.CommitDispatch or GovernanceGuardOperation.RecordKeyCompromise or GovernanceGuardOperation.ReplaceAttestation or GovernanceGuardOperation.RecordProtectionOutcome;
        bool scope = value.Operation is GovernanceGuardOperation.AuthorizeAdmissionFence or GovernanceGuardOperation.CommitAdmissionFence;
        return (value.Write is not null) == write && (value.Repair is not null) == repair && (value.Ordinal is not null) == ordinal && (value.Hold is not null) == hold
            && (value.Batch is not null) == batch && (value.Scope is not null) == scope
            && (value.RevocationReceipt is not null) == (value.Operation == GovernanceGuardOperation.RecordKeyCompromise)
            && (value.Operation is GovernanceGuardOperation.InstallEpoch or GovernanceGuardOperation.InstallRepairFence or GovernanceGuardOperation.RecordBridgeDrain
                or GovernanceGuardOperation.ActivateSuccessor or GovernanceGuardOperation.AppendWrite or GovernanceGuardOperation.RegisterHold or GovernanceGuardOperation.ReleaseHold
                or GovernanceGuardOperation.RecordKeyCompromise || Text(value.DeletionRequestId));
    }
    private static bool Unique(IReadOnlyList<string> values) => values is not null && values.Count <= 1000 && values.All(Text) && values.Distinct(StringComparer.Ordinal).Count() == values.Count;
    private static string ReceiptId(TenantGovernanceGuardState state, GovernanceGuardTransition command, string digest, string status, long revision)
        => Hash(new[] { state.TenantId, state.InstallationId, command.OperationId, digest, status, revision.ToString(System.Globalization.CultureInfo.InvariantCulture) });
    private static bool Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048) { return false; }
        try { _ = new System.Text.UTF8Encoding(false, true).GetByteCount(value); return true; } catch (System.Text.EncoderFallbackException) { return false; }
    }
    private static bool Hex(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    private static bool Same<T>(T a, T b) => Hash(a) == Hash(b);
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
