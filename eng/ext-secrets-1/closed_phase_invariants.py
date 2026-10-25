"""Closed native phase semantics. Installed predicate paths are never executable authority.

The shipped typed native publisher measures guard/protection/stream facts. Those current
snapshots cannot prove complete writer history, ingress-return ordering, principal
transport boundaries, physical copy closure, or all seven protection-owner gates.
These exact missing readers/schemas keep complete phase qualification unavailable.
"""

REQUIRED = {
    'EXT-PROTECTION-1.complete-owner-command': {'G1','G2','G3','G4','G5','G6','G7'},
    'host:H1': {'CurrentAuthenticatedOrigin','ExactPrivateReplayAcl','DurableDenialBeforeReturn','SpoolOutageBlocksIngress'},
    'host:H2': {'CompleteInstalledComposition','BranchBIdentity','NoConsumerExecution'},
    'host:all-consumers-refuse': {'EveryConsumerRefusesUnavailableDependency'},
    'host:H3': {'ActualProductionEngine','PersistedPlaintextAbsence','UnsealEquality','IrreversibleErasedReplay','StartupAndEveryReadiness'},
    'host:H4': {'CurrentMigrationEpoch','RepairFenceAtomicity','ExactBridgeCohort','SuccessorPreservesAllOwnerFacts'},
    'host:v21': {'AcceptedOrdinalAtWrite','HoldSealLinearization','AttestationDispatchOrdering','CompletionBarrierOrdering'},
    'host:v23': {'BlockReserveSameOwner','CompromiseSubscriberOnly','StableBatchSuccessorAttempts','ReplacementKeyBlockSetCompare','UnknownLookupOnly'},
}

# This is the shipped source capability manifest, not an installed profile. The
# closed required-name sets above prevent a manifest from redefining the scope.
import json
from pathlib import Path

_INVENTORY = json.loads(Path(__file__).with_name('native-phase-invariant-inventory.json').read_text())
if {row['caseId']: {item['name'] for item in row['invariants']} for row in _INVENTORY['cases']} != REQUIRED:
    raise ValueError('ClosedNativePhaseInvariantInventoryScope')
MISSING = {item['name']: (item['missingInput'], item['reason'])
           for row in _INVENTORY['cases'] for item in row['invariants'] if item['missingInput'] is not None}


def measure(name, records):
    """Read only the exact typed source schema emitted by the concrete native collector."""
    owner = next((row['payload'] for row in records if isinstance(row.get('payload'),dict) and isinstance(row['payload'].get('guard'),dict)), None)
    if owner is None:
        raise ValueError('ExactTypedNativeOwnerSource')
    if name == 'CurrentMigrationEpoch':
        state=owner['guard']
        return type(state.get('Revision')) is int and state['Revision'] > 0 and isinstance(state.get('EpochId'),str) and bool(state['EpochId'])
    if name in ('ActualProductionEngine','UnsealEquality'):
        originals=owner.get('canaryOriginals')
        if not isinstance(originals,list) or not originals:
            raise ValueError('ActualIndependentCanaryOriginals')
        for original in originals:
            target=original['Reference']['Target']
            if original['SourceRevision'] != 1 or original['EngineName'] != target['EngineId'] or original['EngineRevision'] != target['EngineVersion'] or len(original['EngineRevision']) != 40:
                return False
            if name == 'UnsealEquality' and (original.get('UnsealEqual') is not True or type(original.get('OriginalPlaintextByteCount')) is not int or original['OriginalPlaintextByteCount'] <= 0):
                return False
        return True
    if name == 'StableBatchSuccessorAttempts':
        attempts=owner.get('signingOriginals')
        if not isinstance(attempts,list) or len(attempts)!=3:
            raise ValueError('AllThreeActualSigningOriginals')
        attempts=sorted(attempts,key=lambda value:value['Payload']['SigningAttemptOrdinal'])
        stable=('TenantId','DeletionRequestId','DestructionSealId','BatchId','ManifestDigest','AttestationOrdinal')
        first=attempts[0]['Payload']
        for index,attempt in enumerate(attempts,1):
            payload=attempt['Payload']
            if payload['SigningAttemptOrdinal']!=index or any(payload[field]!=first[field] for field in stable): return False
            if index<3:
                proof=attempt.get('NoIssueProof')
                if attempt['State']!='SignedAttestationObsoleteUnissued' or not isinstance(proof,dict) or proof['Payload']!=payload or proof['SigningRequestId']!=attempt['SigningRequestId']: return False
            elif attempt['State']!='Signed': return False
        batches=[batch for deletion in owner['guard']['Deletions'] for batch in deletion['Batches'] if batch['BatchId']==first['BatchId']]
        return len(batches)==1 and batches[0]['Capability']==attempts[-1]['Payload'] and batches[0]['SigningRequestId']==attempts[-1]['SigningRequestId'] and batches[0]['IssuedGuardRevision']>attempts[-1]['Payload']['IntendedIssuedGuardRevision']
    raise ValueError('ClosedTypedNativeMeasurement.'+name)


def missing_inputs(case_id):
    if case_id not in REQUIRED:
        return ['ClosedNativePhaseCase.' + case_id]
    return sorted({MISSING[name][0] for name in REQUIRED[case_id] if name in MISSING})


def verify(case_id, source):
    from joint_live_phase_runner import Blocked
    missing = missing_inputs(case_id)
    if missing:
        raise Blocked('ClosedNativePhaseSemanticsUnavailable', case_id, *missing)
    # No installed predicate/profile can reach this branch until a case has a
    # shipped closed semantic implementation for every required native fact.
    raise Blocked('ClosedNativePhaseCaseImplementation', case_id)
