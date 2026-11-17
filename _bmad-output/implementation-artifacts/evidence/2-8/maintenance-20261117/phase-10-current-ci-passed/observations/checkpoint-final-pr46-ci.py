"""Preserve a new immutable checkpoint without altering earlier outcomes."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-current-20261117'
EVIDENCE = ROOT / '_bmad-output/implementation-artifacts/evidence/2-8/maintenance-20261117'
PHASE = EVIDENCE / 'phase-10-current-ci-passed'
SPEC = ROOT / '_bmad-output/implementation-artifacts/spec-2-8-run-ci-integration-on-disposable-runners-with-guaranteed-cleanup.md'
SPRINT = ROOT / '_bmad-output/implementation-artifacts/sprint-status.yaml'
FROZEN = '19044c59bdc8f93eee27bb766b6985fbc2f908f8e9764bebaeb72ecb908d9b0a'

def frozen_digest(raw):
    start = raw.index(b'<frozen-after-approval')
    end = raw.index(b'</frozen-after-approval>', start) + len(b'</frozen-after-approval>')
    return hashlib.sha256(raw[start:end]).hexdigest()

assert frozen_digest(SPEC.read_bytes()) == FROZEN
audit = json.loads((AREA / 'pr46-final-ci-verified-02/primary-receipt-audit.json').read_bytes())
assert audit['status'] == 'passed' and audit['requiredPassingCases'] == 588
PHASE.mkdir(exist_ok=False)
selection = [
    'verify-final-pr46-ci.py', 'verify-final-pr46-ci-02.py', 'audit-final-pr46-ci.py',
    'checkpoint-final-pr46-ci.py', 'pr46-final-ci-verified-02', 'pr46-merge-preparation',
    'current-image-facts-verified-01', 'current-image-facts-dispatch-01',
    'collect_current_image_facts.py', 'test_collect_current_image_facts.py',
    'current-image-facts-workflow.yml', 'prepare-hosted-image-facts.py',
    'prepare-current-eventstore-images.py', 'tenants-7.0.7-package-test-image-facts.json',
    'current-eventstore-proposal-disposition.json', 'released630-attachment-admission-source.cs',
    'pr46-final-ci-verified-01/result.json', 'pr46-final-ci-verified-01/zip-member-inventory.json',
]
for directory in ['current-image-facts-source-01', 'platform-current-eventstore-images-01']:
    selection.extend(str(p.relative_to(AREA)) for p in (AREA / directory).rglob('*')
                     if p.is_file() and p.name != 'recoverable.bundle')
for relative in selection:
    source = AREA / relative
    target = PHASE / 'observations' / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    if source.is_dir():
        shutil.copytree(source, target)
    else:
        shutil.copyfile(source, target)
request = {
    'status': 'awaiting explicit user decision',
    'pr': 'https://github.com/Hexalith/Hexalith.Platform/pull/46',
    'head': audit['headSource'], 'base': audit['baseSource'],
    'action': 'Use existing configured bypass for the required code-owner review and normally merge this one exact PR head.',
    'rule': 'https://github.com/Hexalith/Hexalith.Platform/rules/24657106',
    'ruleName': 'Platform main reviewed changes',
    'requiredCodeOwner': 'jpiquot', 'prAuthor': 'jpiquot',
    'requiredApprovingReviews': 1,
    'ownerReviewExceptionApproved': False,
    'mergePayload': 'observations/pr46-merge-preparation/merge-request.json',
    'mergePayloadSha256': hashlib.sha256((AREA / 'pr46-merge-preparation/merge-request.json').read_bytes()).hexdigest(),
    'prPreflight': '588 actual required cases, zero skips; all six SDK jobs passed',
    'scope': 'This PR and exact head only; no policy changes or future review waiver.',
    'postMerge': 'Fresh normal main tuple and six-SDK checks, then normal supported 2.x source publication and independent release verification.',
}
(PHASE / 'owner-review-exception-request.json').write_text(json.dumps(request, indent=2) + '\n')
checkpoint = {
    'asOf': datetime.now(timezone.utc).isoformat(),
    'status': 'supported Platform2.x / Builds6.3 PR CI passed; specific code-owner-review exception pending',
    'platformCandidate': audit['headSource'], 'platformBase': audit['baseSource'],
    'executedPrVirtualMerge': audit['actualExecutedSource'],
    'executedTreeEqualsCandidateTree': True,
    'buildsVersion': audit['buildsVersion'], 'buildsSource': audit['buildsSource'],
    'tupleRun': audit['tupleRun'], 'sdkRun': audit['sdkRun'],
    'requiredCasesPassed': 588, 'requiredSkipped': 0,
    'suiteCounts': [486, 93, 9], 'sdkVersionsPassed': audit['sdkVersionsPassed'],
    'officialTupleArtifact': audit['artifactId'], 'officialTupleArtifactSha256': audit['artifactSha256'],
    'primaryReceiptsAndAll38XmlGroupsAudited': True,
    'frozenBlockSha256': FROZEN, 'other17DirectPinsPreserved': True,
    'codeOwnerReviewExceptionApproved': False, 'mainMerged': False, 'newPlatformPublication': False,
    'imageFactsRun': 44174015501, 'imageFactsArtifact': 14368338901,
    'imageFactsSource': 'be7149f6b4097b617ec1c5f287e68fe9cd4d9390',
    'imageFactsClassification': 'metadata only; no services or runtime qualification',
    'eventStoreProposalDisposition': 'Disqualified by released HXR033 qualification-owner-expansion; never pushed or used for native execution.',
    'eventStoreProposalControls': {'passed': 39, 'failed': 1, 'skipped': 0},
    'partiesAlternative': 'Investigation only; current full native cohorts remain unadmitted.',
    'lifecycleProofCancellationRequests': 0, 'administrativeCancellationRequests': 1,
    'metadataOnlyDispatchRequestsThisRoute': 1, 'nativeDispatchesThisRoute': 0,
    'originalStoryGoalStillOpen': True, 'candidateAcceptance': False, 'nativeQualificationCredit': False,
    'firstExtractorFailure': 'ZIP authenticated, but the local extractor omitted genuine 6 MB receipts due to its 4 MB cap. Version2 retrieved the same official archive and retained all primary receipts; no CI rerun.',
    'previousPhase': '../phase-09-current-builds-6-3/checkpoint.json',
}
(PHASE / 'checkpoint.json').write_text(json.dumps(checkpoint, indent=2) + '\n')
manifest = [{'path': str(p.relative_to(PHASE)), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest(),
             'bytes': p.stat().st_size} for p in sorted(PHASE.rglob('*')) if p.is_file()]
manifest_path = PHASE / 'manifest.json'
manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')
manifest_sha = hashlib.sha256(manifest_path.read_bytes()).hexdigest()
append = f'''

### Final supported-current PR qualification and corrected native route — 2026-11-17

Final [PR #46](https://github.com/Hexalith/Hexalith.Platform/pull/46) source `408481da06128ae50ad274d5dddcd4234d8b677d` passed hosted tuple [44166185074](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44166185074) and all six SDK jobs in [44166185115](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44166185115), attempt1. Independently authenticated artifact14369843406 has SHA256 `7007cdc09afc7d815eddc37c7d1a84db91694bbf90bf5d4036ca970af1e3c18b`, valid CRCs and unique safe members. Complete primary receipts and all38 required XML groups match the exact committed profiles: current486, base93, extensions9; all588 passed with zero failed, skipped, errors or not-run. The sole current collector used genuine signed Builds6.3.0/source7af and actual NET11, with unchanged output snapshots and clean exact source. The actual executed PR virtual merge is `e6869d11be7e636ae2717ea77e504225eb83f5dd`, parents11719/408481; its Git tree `f8370e59ff5b20b75a681a581a21869f9e8b32c3` equals the PR head tree. Do not label the virtual merge as a literal checkout of408481. These remain PR preflight observations, with no native or candidate acceptance.

The first local artifact extractor authenticated the ZIP but then failed its receipt-count assertion because its4MB retention cap omitted the genuine6MB receipts. That failure is preserved. The corrected extractor retrieved the same official archive, retained all original receipts and passed the independent per-case/source/tool audit. No hosted job was retried. GitHub's latest-release observation still reports Builds6.3.0. Platform remains supported2.x only; the obsolete1.7/4.44 route stays superseded.

The preceding EventStore native-route proposal is **disqualified and must not be delivered**. The actual released Builds6.3.0 `CompositionAttachAdmission` refuses `QualificationOwners` with HXR033/qualification-owner-expansion: “Expanded qualification owners do not support attachment or reuse.” The EventStore package purpose needs that expansion, so it cannot supply this required active-hold observation under unchanged policy. Its local-only proposal `174bebdaf4ec4c7e454fdd7746f7ca1afe1d3c45` was never pushed;39/40 controls passed and the original signed-body failure remains failed. Preserve its sources/logs and the independently useful six hosted image facts. Do not change the published attachment policy or infer native eligibility from those metadata. A focused current Parties alternative remains investigation only; ordinary/public/non-CI full current cohorts remain unadmitted. No native grant or caller has been delivered, and no native dispatch or lifecycle cancellation request was sent on this supported route.

The concrete normal merge payload for PR46/head408481 passed the owning locked commitlint and npm signature checks. GitHub rule [Platform main reviewed changes](https://github.com/Hexalith/Hexalith.Platform/rules/24657106) requires one code-owner approval; sole owner jpiquot is also the PR author and cannot self-review. Its existing configured user bypass is available, but no fresh exception is approved or used. Earlier exact-head exceptions cannot be replayed. Request approval only for this exact completed PR/head; retain all normal fresh main tuple/six-SDK and supported2.x source-publication checks after merge. No protected-main push, manual tag, forged review, rule/credential change, main merge or new Platform publication occurred. The remaining original cancellation-order/cleanup proof stays open; Step03 remains in progress.

Evidence: [phase10 checkpoint](evidence/2-8/maintenance-20261117/phase-10-current-ci-passed/checkpoint.json), [primary receipt audit](evidence/2-8/maintenance-20261117/phase-10-current-ci-passed/observations/pr46-final-ci-verified-02/primary-receipt-audit.json), [specific review-exception request](evidence/2-8/maintenance-20261117/phase-10-current-ci-passed/owner-review-exception-request.json), [manifest](evidence/2-8/maintenance-20261117/phase-10-current-ci-passed/manifest.json). Manifest SHA256 `{manifest_sha}`. Earlier phases and the frozen2526-byte block remain unchanged.
'''
with SPEC.open('ab') as output:
    output.write(append.encode())
assert frozen_digest(SPEC.read_bytes()) == FROZEN
raw = SPRINT.read_text()
key = '  2-8-run-ci-integration-on-disposable-runners-with-guaranteed-cle: in-progress'
assert raw.count(key) == 1
comment = ('  # 2026-11-17 final current PR46: all588 required cases (486+93+9), zero skips, and all six SDK jobs passed.\n'
           '  #   Phase10 authenticates official ZIP, raw receipts/XML and executed PR merge tree; exact-head code-owner exception pending.\n'
           '  #   EventStore native proposal disqualified by released HXR033; no native dispatch or lifecycle cancellation; Step03 remains open.\n')
assert comment not in raw
SPRINT.write_text(raw.replace(key, comment + key))
result = {'phase': str(PHASE.relative_to(ROOT)), 'files': len(manifest),
          'manifestSha256': manifest_sha, 'frozenBlockSha256': FROZEN}
(AREA / 'phase-10-checkpoint-result.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result, indent=2))
