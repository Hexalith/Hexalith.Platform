---
title: Retire KubeSphere without changing workload data
type: story
epic: 4
story: 27
created: 2026-10-01
status: in-progress
baseline_commit: '3b09b03317d6486ec02d84942616ecb2b73595cd'
route: dispatch
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/specs/spec-kubesphere-to-rancher/SPEC.md'
depends_on:
  - 4-26-qualify-rancher-and-the-management-migration
  - 4-0-prove-off-node-backups-and-isolated-restores
---

# Story 4.27: Retire KubeSphere without changing workload data

As Administrator, I want KubeSphere removed through a rehearsed native procedure while applications and recovery inputs are preserved, so that its licensing and compatibility no longer block the supported Kubernetes hop.

**Repo:** Administrator operations. **Covers:** AR-59, AR-63, FR-9, NFR-3. **Dependencies:** 4.26 retirement qualification and current validated 4.0 proofs; precedes 4.1 mutation. No dependency on Rancher availability.

Acceptance criteria:

1. Before mutation, obtain approval bound to the exact removal procedure, resource allowlist, workload interruption scope, incident/recovery owner, window and stop conditions. Independently verify fresh encrypted immutable off-node native workload proofs, external-etcd recovery and node/configuration recovery inputs under the approved attempt policy. Preserve sources and qualifying prior evidence.
2. Capture pre-change native API, authenticated workload health, controllers/replicas, namespace/resource ownership, admissions, storage and PVC UID/binding census. Verify independent private Administrator access again. Drift from the qualified deletion/dependency inventory stops execution.
3. Archive KubeSphere configuration and required metadata under restricted encrypted custody. Rehome any required extension/controller or resource ownership before removing its reconciler. Preserve public application/OIDC service contracts.
4. Apply only the rehearsed, UID-bound uninstall steps to KubeSphere-owned resources. Do not delete application/data namespaces, PVCs/PVs, shared storage/CNI/ingress/identity components or unrelated CRDs. Any finalizer intervention or changed deletion propagation requires the specific approved resource decision; no blanket forced deletion is permitted.
5. Verify the retired releases/controllers, obsolete API services/admission references and public console route are absent; classify any intentionally retained inert archival resources with owner and cleanup date. No unresolved KubeSphere-only runtime/admission dependency may remain on the 1.35 path.
6. Prove native API and private administration, DNS/CNI/storage/admission health, unchanged application/data namespace and PVC identities/bindings, and authenticated protected-workload smoke outcomes. Check old public console access from outside the private path. A failed/unobserved result stops further work and does not mark retirement done.
7. Record signed before/after inventories, deletion identities, preserved-state comparisons, versioned runbook digest and accountable outcome. Prepare a fresh post-retirement etcd point and configuration inventory for 4.1; old-point restoration must explicitly reconcile retired controllers and authority before reopening.
8. Handoff to 4.1 only with accepted retirement evidence. Keep all remaining upgrade blockers, freshness checks and approvals in force; no Kubernetes binary change, drain or upgrade is implied by retirement completion.

## Implementation handoff

Administrator approved the [course correction](../planning-artifacts/sprint-change-proposal-2026-10-01.md). This scoped input is backlog, not an executed qualification. Use the [migration spec](../specs/spec-kubesphere-to-rancher/SPEC.md) and [operations handoff](../../eng/cluster-management/README.md). Retain exact identities, sanitized attempt records and encrypted private inventories; require acceptance evidence and applicable exact procedure approvals before mutation or completion. No live inspection, deletion or installation is recorded by this file.

## Administrator decisions from Story 4.26 — 2026-10-03

- **Console route.** After the Story 4.2 closure, retirement removes the console Ingress, its Certificate and TLS Secret, and the manager Lease.
- **Namespaces.** Remove only the KubeSphere finalizer from the seven finalized namespaces, and keep the namespaces.

## 2026-10-04 owner decision and implementation handoff

- **Retained objects (Administrator decision G108).** The license Secrets, `Cluster/host` and app Category are inert archive. The five `jpiquot` objects in the [retained-object table](../../eng/cluster-management/RETIRE-KUBESPHERE.md#approved-retirement-decisions-2026-10-04) form active authority, including a cluster-admin binding. The Administrator is custodian and approved a 2026-11-03 review date. The G108 decision leaves 4.27's scope unchanged; 4.28 removes the `jpiquot` path and its dangling ServiceAccount subject once the named credential is live.
- **Native administration first (review handoff G110).** Before any deletion, revalidate the recovered existing native client and its unowned cluster-admin authority as the independent path. The named one-year Administrator certificate with its own unowned binding is issued and tested in 4.28; its issuance does not gate 4.27. Sealed native break-glass remains independent of both managers, with custody specifics retained privately.
- **Recovery route (review clarification G111).** Before mutation, capture a fresh production external-etcd recovery point and prove a fenced isolated restore under 4.1's `external-etcd-recovery-point.json` / `external-etcd-isolated-restore.json` contract and 4.0's proof/custody policy, with matching node/configuration recovery. Never restore into the running cluster.
- **Exact execution bytes (review-derived prerequisite G109).** Name the executor issuing production requests. Rerun the full catalog retirement, seven-namespace interventions and fresh-node snapshot/restore fixtures with the exact procedure, driver and imported-module bytes 4.27 will execute, then regenerate the fresh source-bound production plan bound to that procedure digest and those receipts. The kind-bound qualification `Fixture` is not a production executor; a later procedure change requires renewed rehearsal and plan binding before approval.

The exact procedure and allowlist come from the Story 4.26 handoff. Each production change still needs its own approval.

## 2026-10-05 implementation and 2026-10-06 continuation

- [Production executor](../../eng/cluster-management/retire.py) now makes a fresh explicit-native proposal, verifies original SSH signatures and current 4.0/4.1/4.2 prerequisite bindings, and issues requests only after an exact plan/allowlist/procedure/tool/receipt/outage/owner/window approval and matching explicit arm. Separate immutable planning and execution IDs prevent custody reuse. No production attempt was armed or executed by this implementation.
- [Shared phase guards](../../eng/cluster-management/rehearse.py) are transport independent. The production client preserves the exact deletion, named finalizer and content-transition guards, retains every request and stops at the first refusal. Each subsequent mutation checks protected identities/storage, retained full content and authority, current source identity, bound tool/credential/trust/code bytes, unchanged signed evidence and remaining freshness/window. Partial outcomes require a new incident decision; no automatic restoration occurs.
- [Evidence custody](../../eng/cluster-management/evidence.py) supports immutable private 4.27 attempts and signed sanitized receipt publication while preserving existing 4.26 receipts. Native inventories, command bodies/results, signed original inputs and diagnostics are encrypted for two recipients and read back before promotion to a receipt. This local readback does not claim independent off-node recovery custody.
- [Operator handoff](../../eng/cluster-management/RETIRE-KUBESPHERE.md#production-executor-and-exact-attempt-handoff-2026-10-05) defines exact CLI inputs, private signed record contracts, plan/approval transfer, per-phase stops and postflight assessment. Successful phases remain `retired-awaiting-acceptance`; failed/unobserved health, external denial or fresh recovery inputs leave the story incomplete. A separate read-only assessment requires fresh signed authenticated Keycloak/OpenBao/Memories/Forgejo outcomes, private native access, external console denial, new post-retirement etcd/node configuration inputs and exact Administrator acceptance.
- Continuation verification: all **253** cluster-management tests passed with no skips, including all 73 retirement tests and the two new original-exception/custody-refusal checks. Verbose output is retained at `/tmp/story-4-27-final-suite.log`; [current-byte local audit](evidence/epic-4/4-27/20261006t085341z-s427-verification/verification.json) records its checksum and every matrix row's actual passing test. A fixture timeout now retains its original exception, stack, exact command and partial output only in encrypted custody. Diagnostic custody refusal still seals a failed receipt and performs exact owned-resource cleanup; no command retry or phase-guard relaxation was added.
- Current-byte seven-namespace qualification [20261006t074119z-s427-namespaces](evidence/epic-4/4-27/20261006t074119z-s427-namespaces/qualification.json) passed with seven exact UID/resourceVersion-bound PUTs, zero Namespace DELETEs, protected identity/storage/canary preservation, 85 verified encrypted exports and successful cleanup. Sequential full catalog/fresh-node qualification [20261006t074119z-s427-catalog](evidence/epic-4/4-27/20261006t074119z-s427-catalog/qualification.json) passed the 118-object catalog check and 90 controller-cleared cleanup finalizers, all 19 retirement phases with 471 exact baseline removals and 46 retained CRDs, and fresh-node external-etcd restoration. The source node was destroyed before restore; fresh node/member identities, every baseline/CRD UID and canary readback passed. Restored KubeSphere runtime checks passed. All 846 encrypted exports were read back for both recipients, and source, restored-node and aggregate cleanup passed. These are synthetic, isolated fixture outcomes, with no independent off-node custody or production acceptance.
- The real `retire.validate_rehearsals` accepted both complete authoritative private manifests against the current executor, shared module, driver, imported-module, procedure and source-inventory bytes. [current-byte local audit](evidence/epic-4/4-27/20261006t085341z-s427-verification/verification.json) also verifies every private checksum, unchanged frozen intent and unchanged baseline `MAINTENANCE.md`. The pure historical source planner found 2,596 resources, 19 phases, 482 proposed deletions and seven namespace interventions, with all five retained authority objects outside the removal scope. This uses the historical 4.26 census and creates no fresh production plan. The earlier unmocked synthetic native census `20261005t144246z-s427-native-census` and actual Lease observation below retain their original byte identities and prior meaning.
- Superseded attempts stay immutable: [20261005t142929z-s427-catalog](evidence/epic-4/4-27/20261005t142929z-s427-catalog/qualification.json) stopped after 18 phases and originally failed cleanup; separate [conservative cleanup recovery](evidence/epic-4/4-27/20261005t155006z-s427-fixture-cleanup/cleanup-recovery.json) and [read-only cleanup verification](evidence/epic-4/4-27/20261005t155136z-s427-cleanup-verification/cleanup-recovery.json) retained the exact cleanup and proved owned-resource absence without altering that failure. [20261005t155319z-s427-catalog](evidence/epic-4/4-27/20261005t155319z-s427-catalog/qualification.json) passed all 19 retirement phases but stopped on the third fresh-node CRI sandbox stop; cleanup passed. The [retained-image timeout probe](evidence/epic-4/4-27/20261005t165533z-s427-cri-timeout-probe/observation.json) established the two-second CRI default; only exact fresh-node sandbox stops now use an explicit 30-second CRI bound within the unchanged 120-second subprocess bound. [20261005t170204z-s427-catalog](evidence/epic-4/4-27/20261005t170204z-s427-catalog/qualification.json) failed closed on a command/custody error after 15 phase receipts, with 614 verified encrypted exports and successful cleanup. Its original exception was not retained, so the root cause remains unestablished. Its [matching namespace attempt](evidence/epic-4/4-27/20261005t170204z-s427-namespaces/qualification.json) passed. All superseded full-fixture current-match flags are false; no failed attempt is relabelled as successful.

- **Measured operability gate:** the historical census includes 13 Leases. The retained [synthetic native observation](evidence/epic-4/4-27/20261005t105545z-s427-lease-handoff/qualification.json) requested a 12-second pause and measured a 15.205-second response interval: the Node-Lease UID and every other desired-content field stayed unchanged while `spec.renewTime` and its full desired-content digest changed. Both actual command/results remain in four encrypted exports with verified two-recipient readback. This immutable observation records the pre-continuation strict-handoff limit. The approved continuation now permits only explicit signed-plan native Lease renewal with every other desired field fixed, validated monotonic/nonfuture time and the unchanged qualified manager checkpoint. The renewed [actual synthetic Lease pair](evidence/epic-4/4-27/20261005t145105z-s427-lease-handoff/qualification.json) requested 12 seconds and measured a 15.151-second response interval. The real new binding/content validator accepted monotonic renewal on the same UID with every other desired field unchanged, no mutation authority, zero requests and no production plan; five encrypted/read-back exports retain both commands/results and the binding model. Its preceding generic-list-wrapper harness refusal remains a separate immutable failed outcome. This closes the measured local handoff limit under the approved binding contract; all production prerequisites and exact-attempt approval remain required.

- Local prerequisite metadata inspection found the retained 4.0 gate `20261001t061620z` expired at `2026-10-02T06:16:52Z`, and 4.1 records remain historical `20261001t075120z-backup-verified`. No current console-closure record, retirement policy or exact execution approval was found in the inspected retained roots. This read-only local discovery checked metadata/signature-file existence, performed no cryptographic signature validation and made zero remote operations; these historical records supply no current execution gate.

## 2026-10-06 local resume checkpoint

The [resume verification](evidence/epic-4/4-27/20261006t091552z-s427-resume-verification/verification.json) reran all 253 tests with no skips and verified each acceptance-matrix covering test. The real rehearsal validator accepted the existing final-byte catalog and seven-namespace receipts; all 905 and 98 private-manifest files, their 846 and 85 encrypted exports, and retained two-recipient readback receipts passed checksum inspection. Executor, procedure, shared module, driver and test bytes are unchanged, so this audit repeated neither fixtures nor decryption. Frozen intent, baseline maintenance bytes, the historical 19-phase removal scope and all five retained authority objects remain verified.

Local prerequisite metadata still shows the historical 4.0 gate expired and no exact-named current retirement policy or console-closure record in the inspected retained roots. This unsigned local checkpoint performed no cryptographic validation of production prerequisites and made zero production observations or mutation requests. Next obtain accepted independent 4.2 console closure, fresh signed 4.0/4.1 proofs and matching recovery, then a fresh native plan bound to the final-byte receipts and separate exact-attempt approval. Production retirement, signed preservation/health acceptance and the post-retirement 4.1 recovery handoff remain pending.

**Current live gate: closed.** No fresh production plan bound to these final-byte receipts, accepted independent 4.2 console closure, fresh attempt-specific production recovery/readback, separate exact execution approval, production preservation/retirement acceptance or post-retirement 4.1 recovery inputs are supplied by this code change. Keep 4.27 `in-progress` and 4.1's native-hop gate closed. Preserve `jpiquot` authority for its separately approved removal in 4.28 and preserve hashed `MAINTENANCE.md`.

## 2026-10-14 offline Timesheets preparation

The [offline preparer](../../eng/cluster-management/prepare_retirement.py) and
[operator prerequisites](../../eng/cluster-management/STAGING-PREREQUISITES.md)
retain immutable consumed preparation source bytes and complete historical
fixture input lineage in owner-only external packet
`20261014t100941z-s427-reviewed-preparation` under
`/home/administrator/hexalith-retirement-preparation-evidence/`. The separate
[sanitized unsigned local audit](evidence/epic-4/4-27/20261014t100941z-s427-reviewed-preparation/verification.json)
records the actual test, packet, archive and checksum inspection outcomes.
It is outside the consumed source set to avoid self-reference.

The exact external Timesheets handoff input is
`/home/administrator/projects/hexalith/timesheets/references/Hexalith.Platform/eng/timesheets-deployment/staging-dependency-handoff.md`,
SHA-256 `b12758d8a528a876314e7d49b02a864530fc1a7852f40818701ee0f8f789ef38`.
Its October 14 native observations and extended window are input claims, not
observations or approval by this continuation. Concurrent repository changes
supplied the existing local Timesheets handoff; this run preserves it and adds
only the continuation pointer.

The real pure rehearsal validator and all 905/98 private-manifest files plus
846/85 retained two-recipient export/readback records are checked against
unchanged bound execution/procedure/maintenance bytes and the historical
October 4 census. No fixture, decryption, off-node acceptance or current source
qualification is repeated. Recovery metadata originals and signatures remain
in existing private custody; the new packet contains measured path/digest
lineage and explicitly untrusted metadata summaries, never plaintext copies
of those signed originals. Every operational field remains null/not-run,
`signed=false`, `mutationAuthorized=false`, with no production `plan.json`.

Unresolved operator inputs remain: approved trust root/principal, static native
configuration, Administrator/distinct readback recipients and identity,
receipt key/signing scope, approved freshness policy; fresh signed 4.0 proofs,
independent off-node readback and matching production 4.1 point/fenced isolated
restore/node recovery; accepted independent 4.2 closure; private native access
and authenticated health; first fresh census, renewed source-bound fixtures,
second fresh plan and separate exact execution/outage/owner/window/stop/recovery
approval. The existing gate expired October 2; historical receipts/placeholders
and the extended window open no gate. Conditional commands use the unchanged
bound retirement handoff. No remote operation, signing, retirement, upgrade,
deployment, domain command, external publication or Git staging/commit occurs
in this continuation. 4.27 remains `in-progress`, 4.1 closed and Timesheets
Story 3.6 open/FAIL with all later owner gates/default-deny providers preserved.
## 2026-10-14 operational-prerequisite investigation

The [next operator action](../../eng/cluster-management/NEXT-OPERATOR-ACTION.md)
identifies the first unmet prerequisite: current Administrator approval of
fresh recovery bundle `20261014t112353z`'s exact scope, interruption/window and
signing, plus protected signing unlock. The retained `new` interface allocated
only checksum-verified tools/RID/trust inputs offline; operational directories
remain empty, unsigned and unapproved. Standing 4.0 destination/RPO/retention/
maximum-age/single-signer decisions are retained within their scope.

Read-only investigation revalidated the known static `jpiquot@local` transport
(v1.34.9), expected absent console Ingress and source identities. This retained
client does not establish the separately recovered independent access gate.
At the current clock, retained historical validation returned FAIL: 641 of
648 checks pass, seven proof-age/RPO-staleness failures, zero skips, 553 of
553 immutable object versions matching provider readback. Eleven original
historical 4.0/4.1 signatures verified against retained historical trust.
Two bounded read-only Memories execs found Redis three keys, no tenant-registry
or active workflow/reminder counts, and FalkorDB zero keys/graphs; raw results
are encrypted in private investigation custody without claimed decryption.

October 7 accepted Story 4.2 closure is distinguished from the still-missing
fresh signed retirement `console-closure.json`. New sampled console/master-path
GETs were not routed, public OIDC-discovery returned 200 and established public
DNS matched; independent external vantage/authenticated OIDC remain unaccepted.
No capture, quiescence, restore, policy upload or signing was dispatched.

Offline packet `20261014t112004z-s427-prerequisite-investigation` and its
[separate sanitized unsigned verification](evidence/epic-4/4-27/20261014t112004z-s427-prerequisite-investigation/verification.json)
bind final consumed source bytes and separately retained action/tool lineage.
The preparer's zero-remote/closed fields remain distinct from these actual
read-only observations. All original production tasks remain pending; retain
the old packet/receipt, frozen intent, bound executor/procedure/maintenance,
`jpiquot` authority and default-deny providers. 4.27 stays `in-progress`, 4.1
closed and Timesheets 3.6 `in-progress`/FAIL.
## Concrete current policy handoff — 2026-10-14T11:40:42Z

The retained read-only `precheck.py` and local `policy.py` produced an actual
unsigned policy for RID `20261014t112353z`, with no provider writes. The
[next operator action](../../eng/cluster-management/NEXT-OPERATOR-ACTION.md)
binds its exact bytes and precheck before the separately deliberate signing
action; the composite uploading policy step is not proposed for this already
prepared bundle. Existing October 7–31 maintenance dates, this conversation
as incident channel and acknowledged Administrator incident/recovery/outage
roles remain reusable without reapproval. Exact new capture/signing policy
scope and protected signing unlock remain missing; no fresh recovery proof
or retirement authorization is supplied.

Final packet `20261014t114108z-s427-current-policy-preparation` and
[separate unsigned verification](evidence/epic-4/4-27/20261014t114108z-s427-current-policy-preparation/verification.json)
retain corrected final sources and private policy/precheck lineage. The earlier
investigation packet/receipt/supplement retain their dated meaning and exact
bytes. Every original production task stays pending: 4.27 `in-progress`, 4.1
closed and Timesheets 3.6 `in-progress`/FAIL. No upload, signing, capture,
quiescence, restore, retirement, upgrade, deployment or domain mutation ran.

## Approved recovery signing refusal — 2026-10-14

The Administrator's current **“I approve”** binds the exact prepared RID
`20261014t112353z` policy/precheck/handoff/tools and described 4.0 scope to the
existing dates/channel/roles. The
[scope approval](evidence/epic-4/4-27/20261014t120527z-s427-approved-recovery-signing/scope-approval.json)
is retained in new immutable owner-only private custody and sanitized here;
it remains valid and needs no repeated scope permission. Current VCS identity
is recorded separately from the unchanged legacy baseline.

Exact-byte, trust/fingerprint, policy-age/window and safe native cluster-identity
preflight passed. The approved bounded noninteractive
`./sign.sh policy/recovery-policy.json` returned 255 because protected-key
unlock is unavailable; no signature/uploads ledger or publication resulted.
Raw diagnostics are encrypted privately. The
[outcome verification](evidence/epic-4/4-27/20261014t120527z-s427-approved-recovery-signing/verification.json)
retains the true refusal, unchanged inputs and zero dependent capture,
quiescence or restore commands. The
[handoff](../../eng/cluster-management/NEXT-OPERATOR-ACTION.md#current-approval-retained-protected-signing-access-required--2026-10-14)
supplies the exact protected-terminal signing action and preserved approval
reference. The remaining input is signing access, not scope approval.

Fresh signed recovery/restore/cleanup/non-dry validation/readback remains
pending. No original retirement/upgrade/deployment acceptance is claimed;
4.27 stays `in-progress`, 4.1 closed and Timesheets 3.6 open/FAIL. Prior packets,
receipts, approved policy bytes and bound execution/procedure/maintenance
remain unchanged; no fixture rerun, Git staging/commit or domain mutation ran.


## Approved recovery strict stop — 2026-10-14T13:11:55Z

The Administrator completed private-terminal signing of exact policy RID
`20261014t112353z`. The original signature and two exact immutable provider
versions verified, including fresh validator readback, unchanged policy/precheck/
37 tool files/trust, current age/window, safe static native transport, source
identity/readiness and absent isolated targets. The prior signing refusal and
all preparation packets retain their historical meaning and exact bytes.

Twelve approved top-level commands then ran sequentially through the retained
Keycloak OIDC helper. Current Backup phase was `completed`; the isolated
PostgreSQL restore became Ready and all ten database/canary comparisons matched.
The isolated Keycloak app and temporary verifier bootstrap succeeded. The OIDC
helper returned process exit zero but recorded **`allPass=false`**:
`tokenRoleClaimsEqualLive=false`, with a missing live claims reference. Its
isolated client-credentials login returned 200 and seven of eight OIDC/admin/
count checks passed. The unchanged helper requests its live reference token
from `https://auth.tache.ai/realms/master/protocol/openid-connect/token`;
it does not record that reference request's HTTP status. This is a failed
acceptance check, not proof of a particular unrecorded HTTP failure or complete
recovery. The sequence stopped on that semantic result, with no retry.

At the stopped-state read, all four protected namespace UIDs, all 15 source
PVC UIDs/bindings and 15 PV UIDs/claim references were unchanged. Source
Keycloak/OpenBao/Redis/FalkorDB were ready; Memories MCP remained 2/2 and was
never paused. Only isolated `recovery-4-0-kc` remains, with namespace UID
`dafd75fd-65b7-4650-ab2a-7e0993bd68f1`, a new isolated PVC/PV and ready DB/app.
The CNPG watcher remains `keycloak,cnpg-system,recovery-4-0-kc`; other approved
restore targets are absent. Its temporary writer credential copy remains
inside that isolated namespace, pending the incident decision. The local
verifier scratch secret was removed by the retained helper. No later isolation,
OpenBao, Memories, cleanup, records/proofs/gate/validation or signing command
was dispatched. No retirement, upgrade or Timesheets deployment ran.

**Next required input:** the Administrator, as recorded incident owner, must
decide how to handle this preserved isolated target/watcher and the missing
live-reference prerequisite before any operational retry or cleanup. The
existing recovery-scope approval, October 7–31 dates, this conversation as
incident channel and roles persist; do not request them again. Do not rerun
`new`, `precheck.py`, `policy.py`, the uploading policy composite or the failed
helper automatically; do not change the approved/bound tools, reopen the
public master route or weaken the comparison. Fresh recovery remains
unaccepted; 4.27 stays `in-progress`, 4.1 closed and Timesheets 3.6 open/FAIL.

New immutable operator custody is
`/home/administrator/hexalith-retirement-investigation-evidence/20261014t125138z-s427-approved-recovery-run`.
It retains encrypted command/result and stopped-state diagnostics without a
recipient-decryption claim, exact original input/readback bindings, actual
sequential results, failure and protected-state checks. The separate
[sanitized unsigned verification](evidence/epic-4/4-27/20261014t125138z-s427-approved-recovery-run/verification.json)
binds local integrity/preservation; it supplies no signed accepted recovery or
production-retirement gate. Bound retirement execution/procedure/maintenance,
frozen intent, original production tasks and older receipts remain preserved.


## Private OIDC investigation; exact incident action pending — 2026-10-14

The sealed strict-stop attempt retains all twelve completed authorized
Keycloak commands and zero further dependent operational dispatches. Its
failed canonical OIDC artifact remains unchanged, SHA-256
`014463ef76fd99431f6c268edbc41274b5031157f683a1630011956cb5738578`.
This mid-sequence failure followed an actual completed backup/partial restore;
the older zero-capture refusal describes the earlier unavailable-key gate.

Separate authorized investigation through explicit static `jpiquot@local` and
accepted loopback-only private source administration obtained source/restored/
existing-verifier HTTP 200, equal same-client claims digests and all eight
unchanged checks true. It read the existing isolated verifier credential with
bounded READ ONLY SQL and kept credentials/tokens only in memory. Both started
forwards terminated and listeners were absent. Distinct create-only diagnostic
SHA-256 is `38e70fa5cc0f48bcc40884dd0e62d3a04284190cb052aa29f41e503afa2249d7`;
this is investigation, not fresh accepted recovery or incident authorization.
The public master route remains closed and the failed original/37 approved
tools/policy/signature/old receipts remain intact.

The prepared private packet is
`/home/administrator/hexalith-retirement-investigation-evidence/20261014t131927z-s427-private-oidc-correction`.
Its `INCIDENT-ACTION.md` and create-only `incident-decision-final-pending.json`
(`incidentApproved=false`) bind the original approval/failure, corrected
helper/diagnostic, separate guarded proof consumer and actual retained target.
The concrete next owner decision is acceptance of those changed exact bytes
and continuation on the existing target, first replaying missing Keycloak
isolation/target identity. Original `proofs.py` still reads the failed canonical
file; the separate variant supplies only a guarded distinct reference plus
signed-proof provenance, with original acceptance logic byte-identical.
Seventeen meaningful offline tests pass, including changed-input/claims,
unknown/false checks, unavailable reference, create-only output, uncertain
forward cleanup and exact prospective/pending incident-decision boundaries.
No operational proof, isolation, later-system, cleanup or watcher change ran.

This is an incident decision, not repeat original recovery-scope/window/role
approval. Reuse those existing decisions within their narrow scope. A partial
abort needs a separate cleanup packet: unchanged full `cleanup_check.py`
assumes all three isolation records, which are absent, so no executable abort
is advertised. The retained target/watcher remains under the Administrator
incident owner. Keep 4.27 in progress, 4.1 closed and Timesheets 3.6 open/FAIL;
no retirement, upgrade or Timesheets deployment/acceptance is implied. The
[separate unsigned verification](evidence/epic-4/4-27/20261014t131927z-s427-private-oidc-correction/verification.json)
retains exact action/source/checksum/preservation lineage without promoting
the diagnostic to recovery acceptance.


## Accepted OIDC continuation, resumed Memories and unsigned cleanup — 2026-10-14

The Administrator's actual **“Accept the OIDC correction and continue 20261014t112353z.”** decision is retained; original recovery scope, destination, October 7–31 maintenance dates, this conversation as incident channel and acknowledged Administrator incident/recovery/outage roles remain within their existing limits. No repeated scope or incident decision is pending. The supplied **“Freeze announced for this run”** C3 announcement enabled the single final Memories pre/census and approved pause/drain/capture sequence.

The new continuation completed 32 authorized bounded top-level commands with passing actual semantic gates: fresh full incident preflight; retained Keycloak isolation/identity; OpenBao snapshot, immutable transfer/readback, isolated restore and all 10 verification checks; the split paired Memories physical capture/readback/isolated restore and all 17 verification checks; each system's complete isolation/identity; immediate conditional Memories resume; guarded original cleanup; protected-source/watcher and actual cleanup-signing preflight reads. The older 12-command run and its failed canonical OIDC record remain unchanged. The accepted distinct all-eight-check diagnostic is consumed only by the future guarded proof variant; no failed record was overwritten or promoted.

Memories resumed at **2026-10-14T15:18:55Z** with MCP 2/2 ready, both source readiness probes HTTP 200/Healthy, a clean drain recheck, zero pending/in-flight/missing/duplicate work and an unchanged stream. The actual freeze end was announced in this conversation. Both source stores restarted and retained their comparable censuses/AOF health; the approved zero-tenant/zero-graph baseline supplies no logical verifier-pass claim. All three recorded isolated target namespace UIDs, all six target PVs and temporary credential/seal copies are absent. The CNPG controller identity is unchanged and `WATCH_NAMESPACE=keycloak,cnpg-system`. Fresh reads preserve the four protected source namespace UIDs and all 15 PVC/PV bindings. Host-directory removal was inferred from OpenEBS PV deletion; no node-directory inspection was claimed.

The actual cleanup record is **unsigned**, SHA-256 `3ae690daad23e6a0f028ab28ca023ba9a04d4c72f810d9129b117125d08d5b78`, and binds cleanup-check SHA-256 `ac945fb40b695e147f2e6e4dc2a9af5a8fcdb8ecb79fc0c2a814c1d6de997778`. The immediate remaining input is **the Administrator's protected signing access** for that reviewed exact record. [Concrete guarded protected-terminal signing action](/home/administrator/hexalith-retirement-investigation-evidence/20261014t144239z-s427-accepted-oidc-continuation/NEXT-PROTECTED-SIGNING-ACTION.md) binds 99 inputs, exact approved policy/precheck/tool/trust/incident bytes, actual cleanup and fresh native checks. The known noninteractive key refusal was not repeated. Authentic signature verification and fresh exact-version validator readbacks must pass before the separate guarded records prerequisite, original manifest and approved external proof consumer can run. No cleanup signature, manifest/proof writing, backup gate, non-dry final validation or later signatures was dispatched by this continuation at the recorded barrier.

Current read-only M1 preparation matched both live Redis/FalkorDB run IDs to the approved post-copy restart. The Administrator's current GET-only M2 investigation at **2026-10-14T15:32:00Z** enumerated all IAM policies/groups stably, found the sole approved writer policy with exact three permission sets/recovery-project scope and no group membership, and verified the exact current bucket policy's allowed writer actions with no delete/retention/legal-hold rights. The owner-only M2 v2 receipt SHA-256 is `08d587bb85da790b5bbb245eca259ec6ee15cf3d2ea1b27a952d707108dab32b`; encrypted raw readback and the first false local resource-syntax check/correction remain preserved. No IAM write/delete attempt occurred. Neither preparation is the later honest Administrator M1/M2 final-validation attestation; all eight required authentic signatures, immutable readbacks and final non-dry validation remain required.

The [separate unsigned continuation receipt](/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261014t144239z-s427-accepted-oidc-continuation/verification.json) binds owner-only encrypted events, exact source archive, authentic approval, observed resume/freeze-end, provider/IAM read-only receipts and the current signing action. Earlier packets/receipts/archives, original 37 tools, frozen intent, original production Tasks & Acceptance, legacy baseline and bound retirement/procedure/maintenance bytes remain preserved. Concurrent unrelated deferred-work/spec-1-6 edits are retained as workspace activity, with private current snapshots and truthful digest drift; they are not consumed recovery inputs. Unchanged fixture/test suites were not rerun for this operational checkpoint; actual semantic/native gates, syntax, custody, archive and preservation checks supply the current evidence.

This completed recovery work supplies no accepted fresh backup gate, retirement plan/approval, independent native break-glass recovery, attempt/plan-bound signed console closure, 4.1 external-etcd/node recovery, upgrade, Rancher or Timesheets deployment/activation. **4.27 remains in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.**


## Signed cleanup and concurrent unsigned records reconciled — 2026-10-15

At **2026-10-15T08:49:13Z**, this continuation independently authenticated the
unchanged original policy and cleanup signatures and freshly read their four
exact immutable provider versions with the validator credential. All hashes,
byte counts, AES256, COMPLIANCE and retention checks passed. Policy age was
76,105 seconds against 86,400; its earliest deadline remains
**2026-10-15T11:40:42Z (13:40:42 Europe/Paris)**. The maintenance window does not
extend freshness. No new scope/window/role approval is pending.

Separate concurrent custody `20261015t084001z-s427-signed-cleanup-continuation`
created the manifest at 08:45:16Z and three unsigned passing restore proofs at
08:46:19Z. Those stages were not dispatched by this continuation. Their actual
receipt/digest bindings, all 73 manifest local files, three proof results,
RPO/isolation/expiry and accepted OIDC-correction provenance independently
matched. Its manifest records 4,714 provider-version readbacks by that run;
this continuation freshly reread only the four policy/cleanup versions. The
failed original OIDC artifact and original 37 tools remain unchanged. Fresh
native reads preserve four source namespace UIDs, all 15 PVC/PV bindings and
CNPG identity/watch scope; isolated targets, their storage claim references
and temporary credential/seal copies are absent.

This run's new prerequisite command exited 1 without timeout at 08:46:02Z,
while the external records stage was progressing. Its exact stderr remains
sealed encrypted; no recipient decryption or particular original error is
claimed. No dependent stage was dispatched after that stop. A later offline
provider-disabled diagnosis refused the now-existing proof; that later error
does not establish the original failure. The subsequent authorized read-only
reconciliation passed and performed no remote write or record replay.

The immediate prerequisite is the Administrator's own signatures/publication
of the **existing manifest and three proofs**, using the verified
[guarded protected-terminal action](/home/administrator/hexalith-retirement-investigation-evidence/20261015t084001z-s427-signed-cleanup-continuation/NEXT-PROTECTED-SIGNING-ACTION.md)
(SHA-256 `a4cb7d9ce0ff2ccc51332e93bb4ab5525fc42a0417b2a20de0fd3b39d7873bfe`).
Its exact guard passed local-only checks with zero remote requests; its Bash
block passed syntax validation. Full terminal guard/signing was not invoked
and the known unlock refusal was not repeated. All four signatures, the gate
and final validation were absent at reconciliation. Authenticate actual
signatures and fresh immutable versions before any dependent gate; later gate
signing, non-dry validation and honest current M1/M2 remain required. Preserve
an expired attempt rather than changing timestamps or repeating completed
captures/restores/cleanup.

New owner-only custody `20261015t084153z-s427-signed-cleanup-resume` retains the
initial sources, stopped event, encrypted native diagnostics and distinct
read-only reconciliation. The [separate unsigned verification](evidence/epic-4/4-27/20261015t084153z-s427-signed-cleanup-resume/verification.json)
records exact hashes and preservation checks without accepting fresh recovery.
Prior text and concurrent progress are preserved. No unchanged fixture or test
suite was rerun. Original retirement tasks remain pending: **4.27 in-progress,
4.1 closed and Timesheets 3.6 open/FAIL**. No retirement, node/etcd mutation,
upgrade, Rancher, Timesheets deployment/activation or Git staging/commit ran.


## Signed cleanup and prepared unsigned recovery proofs — 2026-10-15

The Administrator's cleanup signature verifies; independent validator readbacks match exact record/signature versions `1792053509510188` and `1792053509947219`. The prepared prerequisite passed, then the retained manifest and approved private-OIDC proof consumer ran sequentially. All 4,714 manifest versions were read back, and three unsigned restore proofs pass with exact policy/manifest/cleanup and accepted incident bindings. The original failed OIDC record is unchanged. No capture, restore, resume or cleanup was repeated.

The [current operator action](../../eng/cluster-management/NEXT-OPERATOR-ACTION.md#authenticated-cleanup-and-prepared-unsigned-proofs--2026-10-15) identifies the concrete guarded Administrator manifest/proof signing action within existing exact approval. The policy age deadline is 2026-10-15T11:40:42Z. Gate creation/signing, non-dry validation, final M1/M2 and the remaining authentic immutable signature/readbacks are pending. The [new unsigned checkpoint](evidence/epic-4/4-27/20261015t084001z-s427-signed-cleanup-continuation/verification.json) preserves source/diagnostic/custody and local refusal/correction lineage. No production task or frozen intent is changed, and no fresh 4.0 or retirement acceptance is claimed. 4.27 stays in-progress, 4.1's operational gate closed and Timesheets 3.6 open/FAIL.


## Fresh signed 4.0 accepted; next recovery prerequisite prepared — 2026-10-15

Recovery `20261014t112353z` passed fresh signed acceptance at
**2026-10-15T11:27:34.340622Z**, valid only until
**2026-10-15T11:40:42Z (13:40:42 Paris)**. All eight original Administrator
signatures authenticate, and a separate validator independently reread all
**16 exact immutable record/signature versions**, matching bytes, hashes,
AES256 and COMPLIANCE retention. The actual non-dry validator completed at
11:12:03Z with **4,809/4,809 checks passing, zero failures/skips**, including
**4,714/4,714 manifest versions**. Actual M1 at 11:14:28Z matched both live
post-copy run IDs; M2 at 11:16:14Z confirmed the complete current writer
IAM/group and bucket-policy restrictions through GET-only evidence. The
Administrator's authentic final validation signature attests those manual
steps. Its record version is `1792063538323774`, signature version
`1792063538589066`; the signed validation SHA-256 is
`b269db6ab85f3a99d27560742921f0192d41925d9a3fe2bebd3adc95c6b234ae`.

The delegated manifest/three-proof signatures and eight uploads completed at
11:01:09Z; the temporary signing agent was stopped. The unchanged gate was
assembled once at 11:03:34Z and then signed by the Administrator in the
protected terminal, followed by the single non-dry validation and final owner
signature. No completed capture, restore or cleanup was repeated. The failed
original OIDC record, accepted distinct correction, all 37 original tools and
all 143 checksum-covered previous-checkpoint files remain unchanged. Earlier
pending receipts retain their dated meaning; this checkpoint resolves those
signing barriers. No repeat scope/window/role approval was requested.

After acceptance, the unchanged offline 4.1 preparer created a new private
packet `20261015t112841z-post-40-recovery-prerequisite`: 16 unsigned original
records, checksum manifest, exact read-back archive of 13 source files and a
separate acceptance/lineage supplement. Its own metadata-only/unvalidated
claims and closed gates were preserved. Preparation made **zero remote
requests** and expanded no operational authorization. The first missing
prerequisite is fresh production external-etcd recovery with a matching
isolated restore and encrypted off-node node/configuration recovery, plus
fresh independent private native/break-glass proof. The retained October 1
scripts hardcode stale paths and were not run. The dated upgrade targets
remain unvalidated. The new handoff binds concrete evidence requirements and
current interface gaps; no node/etcd capture, decryption, new native grant,
retirement, upgrade, Rancher or Timesheets operation was dispatched.

Freshness ends at the deadline above regardless of the maintenance window.
Preserve all completed evidence after expiry; no timestamp change or replay
of completed captures/restores/cleanup can extend this acceptance. **4.27
remains in-progress, 4.1's operational gate closed and Timesheets 3.6
open/FAIL**. Later retirement/console/native/recovery/health/execution,
release/provider/source/deployed-acceptance and default-deny gates remain.

[Unsigned audit of actual signed recovery](evidence/epic-4/4-27/20261015t110948z-s427-signed-gate-final-validation/verification.json); [next offline prerequisite](/home/administrator/hexalith-upgrade-evidence/evidence/epic-4/4-1/20261015t112841z-post-40-recovery-prerequisite/NEXT-PREREQUISITE.md). Original production Tasks & Acceptance remain pending.


## Isolated offline recovery closeout — 2026-10-15

The [attempt-specific guarded procedure](../../eng/kubernetes-upgrade/recovery-20261015t140709z/PROCEDURE.md) and its local-only
`readiness.py` / `test_readiness.py` complete this continuation's reviewable
offline preparation. Logical proposal/target stamp remains `20261015t140709z`,
with lineage `20261015t112841z-post-40-recovery-prerequisite`; the distinct
repository checkpoint is `20261015t143451z-s427-isolated-offline-closeout`. Full current VCS identity
is `55bee680e6901a45946dd8bf1e4a4060952bd7ea`, separate from the preserved legacy baseline.

The root's local audit authenticated the eight original 4.0 signatures and
preserved historical acceptance, but the earliest signed deadline remains
**2026-10-15T11:40:42Z**, basis `policy.preparedAt`. The actual isolated
readiness action at 14:38:15Z returned **exit 1**, `workload recovery expired or
future`, with that exact deadline, zero remote requests and authorization false.
Current provider freshness, new captures/restores and recovered access are not
claimed. No timestamp, historical receipt or failed OIDC record was replaced.

The focused isolated suite passed **19/19 tests**, no skips, including the
coherently rebound freshness case and a mutation that demonstrates omission of
non-approval record freshness is detected. It covers the declared zero-host-
mount isolation profile, retention duration from creation and both API results,
archive/plaintext digests, source/node and native-subject/binding/lineage
identity, event chronology, complete tool roles, custody and side-effect absence.
Metadata consistency remains untrusted and never authenticates or opens a gate.
The exact private bindings pin the public encryption recipient, all five
retained `jpiquot` UIDs, existing encrypted native client, source/tool hashes and
explicit unknowns. No live adapter or operational execution mode is supplied.

Unidentified concurrent edits affected the generic guard/tests/procedure and
shared packet. Those bytes and our earlier drafts/candidate snapshot remain
untouched and unaccepted; this implementation is isolated under
`eng/kubernetes-upgrade/recovery-20261015t140709z/`. New owner-only custody is
`/home/administrator/hexalith-retirement-investigation-evidence/20261015t140709z-s427-expired-recovery-preparation/closeout-20261015t143451z-4fbeb896`. Its actual local failed
rerun and reconstruction corrections, root review-diff paths and 162-file
historical-preservation snapshot are retained. The root independently reran
the final focused suite. Final checksum sealing awaits the root's final checks.

The [sanitized unsigned checkpoint](evidence/epic-4/4-27/20261015t143451z-s427-isolated-offline-closeout/verification.json) binds exact files and outcomes.
Uncovered actions remain fresh 4.0 prerequisite acceptance, exact 4.1 scope and
freshness policy, source snapshot/node capture, provider-prefix permissions,
recipient decryption/independent readback, qualified image-baked tools/stdin
delivery/extraction capacity, recovered authority lineage and 4.1 signing.
The original resolved Docker executable remains group-writable and blocked;
no concurrent staged copy is accepted and no tool repair or daemon call ran.
There was no production command, capture, restore, decryption, signature, new
grant, retirement, upgrade, Rancher, Timesheets deployment or Git staging/commit.
Unchanged broad tests/fixtures were not rerun. **4.27 remains in-progress, 4.1's
operational gate closed and Timesheets 3.6 open/FAIL**; all original production
tasks, later owner gates and default-deny controls remain pending/preserved.


## Timesheets reviewed offline prerequisite packet — 2026-10-15 14:43 UTC

Concurrent preparation and Timesheets checkpoint
`8f9b405c8de33ded25d018cf59194e5a578fa916` are reconciled. Platform HEAD
remains `55bee680e6901a45946dd8bf1e4a4060952bd7ea`. The [reviewed packet](../../../../../hexalith-retirement-investigation-evidence/20261015t140709z-s427-expired-recovery-preparation/TIMESHEETS-REVIEWED-PACKET.md)
freezes exact procedure/guard/test bytes and a new offline binding revision,
without replacing original bindings, helpers or recovery evidence. Its binding
SHA-256 is `80bcbc6592e3c5918bf79b2b689c8d27bf53d9faa49d73e2dd1fda23dd9db08e`;
30 scoped local checksum entries match. This is unsigned offline preparation.

The frozen guard passed **26/26 tests in 1.142s**, zero skips, with matching
source hashes before/after. Its hash-checked `-I -S -B` launcher passes syntax
and refuses a synthetic Python startup hook. Actual readiness at
**14:41:10 UTC** exits **1** with the unchanged earliest expiry
**2026-10-15T11:40:42Z**, `operationalGate=closed` and zero remote requests.
Timesheets' existing launch-readiness lane passes **13/13 in 0.126s**. All 162
historical preservation files and eight signed original pairs still match at
14:43:26 UTC; historical validation/readbacks and the failed OIDC record remain
preserved. Earlier audit/test assertion failures and the concurrent 19-test /
14-failure intermediate fixture stop are retained, followed by the independent
passing frozen-source run. [Exact sanitized results](evidence/epic-4/4-27/20261015t140709z-s427-expired-recovery-preparation/verification.json) bind these facts.

The next unresolved prerequisite is **separate exact authorization to prepare
fresh 4.0 policy/bundle and current source/tool/custody bindings**. No replacement
bundle is allocated. Review and cover those exact fresh bytes, then require
policy signature and immutable independent readback before capture, followed
by actual current recovery/restore/resume/cleanup, signed records/readbacks,
gate, non-dry validation and honest final Administrator M1/M2 acceptance.
The old RID approval and completed four-record delegation cannot cover that
replacement. Fresh accepted 4.0 precedes separately covered 4.1 external-etcd /
node/configuration recovery and independent recovered-native/SSH/break-glass
use. Current unknown identities, image/adapter/capacity qualification,
policy/IAM, recipient access and signing remain blocked; protected unlock is
supplied only in its private terminal at the actual prerequisite.

Preserve existing destination/principal/key, scope and incident decisions,
roles and October 7–31 window within their exact limits. Retirement, upgrades
and deployment retain separate exact approvals. No capture, restore,
decryption, infrastructure mutation, signing, publication, Git staging/commit/
push or dependency update ran. Original production tasks stay pending:
**4.27 in progress, 4.1 operational gate closed, Timesheets 3.6 in progress and
release FAIL**. The offline checklist can complete without accepting retirement.


## Current prerequisite reconciliation — 2026-10-22

The [current operator handoff](../../eng/cluster-management/NEXT-OPERATOR-ACTION.md#current-prerequisite-reconciliation--2026-10-22)
reconciles later unsigned RID `20261017t143202z`, expired at the unchanged
**2026-10-18T14:32:02Z** ceiling, the disclosed three-file source-binding drift
and October 17's owner documentary self-review direction. Earlier absence/
reviewer summaries remain dated history. Current source/authority/custody,
provider/readback and qualified capture/restore/cleanup adapter facts still
precede an exact operational bundle and approval, then fresh accepted signed
4.0 recovery. No live recovery action is ready for final approval. The
[unsigned local reconciliation receipt](evidence/epic-4/4-27/20261022t195505z-s427-prerequisite-reconciliation/verification.json)
binds checked hashes, expiry and preservation under HEAD
`dcb01b1bcbcfe678781674841697de383f25165d`. No operational command, old helper,
unchanged suite, new recovery allocation or timestamp change occurred. Original
production acceptance remains pending: **4.27 in-progress, 4.1 closed and
Timesheets 3.6 open/FAIL**.

## Current source and recovery adapter preparation — 2026-10-23

The [new preparation receipt](evidence/epic-4/4-27/20261023t085529z-s427-recovery-command-preparation/README.md)
and [current handoff](../../eng/cluster-management/NEXT-OPERATOR-ACTION.md#current-source-and-recovery-adapter-preparation--2026-10-23)
bind new read-only source/provider observations separately from offline adapter
tests. The private technical review bundle names exact available tools/inputs,
proposed isolated targets, effects/stops and guarded resume/cleanup procedures.
Added source storage has explicit coverage treatment; historical completeness
and old operational approval are not inherited. Missing system drivers and
actual runtime qualifications remain explicit closed prerequisites. No fresh
recovery RID/policy, signature, upload, capture, quiescence, restore, permission
change, retirement, upgrade or deployment was produced. Original production
acceptance remains pending; **4.27 in-progress, 4.1 closed, Timesheets 3.6
open/FAIL**. The historical frozen intent and checked original evidence/bound
execution bytes remain preserved.


## Recovery command bundle checkpoint — 2026-10-23

Telemetry PostgreSQL is explicitly included in the owner's recovery proposal.
The [verified preparation receipt](evidence/epic-4/4-27/20261023t085529z-s427-recovery-command-preparation/verification.json)
binds the private revision-3 technical bundle, exact available tools/inputs,
94 common and 70 PostgreSQL offline tests, bounded local SQL checks and the
limited preservation audit. Current source volume bindings are resolved in
private custody. Offline consistency remains separate from actual qualification.

This is technical review material with explicit missing executable integration;
no operational approval is requested. Complete native/credential/readback/
signature-scope and system/target/proof-consumer drivers, then rebind a separately
covered fresh attempt before exact operational approval. Actual authenticated
policy/signature immutable readbacks precede capture; actual recovery, source
resume, target cleanup and signed/non-dry/manual acceptance precede later gates.
No prohibited source/provider operation or fresh recovery RID/policy occurred.
Story 4.27 remains in-progress, 4.1 closed and Timesheets 3.6 open/FAIL. Historical
expiry, signed originals, frozen intent and checked bound bytes are preserved.


Final selected bundle: revision 4, as bound by the sanitized verification receipt.
The revision 3 checksum packet remains unchanged. Its first verifier refused the
host's root-owned multicall env hardlink; revision 4 corrects only that runtime
inventory check, with private/tool single-link checks retained. Actual positive,
wrong-anchor and fabricated-qualification checks pass. Adapter/input bytes and
all 164 offline unit results are unchanged. No source/provider action followed.


## Authenticated execution boundary implementation — 2026-10-23

The [execution-boundary interface and fixture handoff](../../eng/recovery-preparation/RECOVERY-EXECUTION-BOUNDARY.md) and
[separate implementation verification](evidence/epic-4/4-27/20261023-recovery-execution-boundary/verification.json) record the new dormant native
execution, independent immutable-readback and original-buffer policy
authentication drivers. The new suites pass 37 offline cases: all four runner
operations compose with inert transports and distinct cluster/principal
bindings; real local processes exercise sealed fd 3, partial/stalled I/O,
bounded cancellation, and a pre-existing public OpenSSH signature verifies
without key generation or signing. The unchanged common/telemetry suites pass
all 164 cases (94 + 70); no skips or external test requests occurred.

These results qualify the documented fixtures only. Concrete independent
provider transport, actual credential/authority and runtime qualification,
store/system and isolated-target drivers, proof-consumer integration and actual
recovery/resume/cleanup acceptance remain prerequisites. Public activation
stays disabled; no credentials or production endpoints were accessed and no
recovery, infrastructure, signing or upload action occurred. The historical
prefixes, revision-4 packet, signed originals, frozen intent and retained scoped
decisions remain preserved; new evidence is bound separately. **4.27 remains
in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.**

Parent verification found and corrected child credential delivery when the parent already owns fd 3. The final new suite has 38 cases; the occupied-descriptor regression also verifies the parent descriptor and its flags remain unchanged. Earlier 37-case results remain the initial implementation checkpoint.


## Reviewed execution-boundary correction — 2026-10-23

The [separate corrected-source verification](evidence/epic-4/4-27/20261023-recovery-execution-boundary/review-1/verification.json) records 61 new driver cases and all 164 unchanged adapter cases passing (225 total, no skips). The initial 38-case packet remains byte-preserved as historical fixture evidence. The corrected boundary atomically owns each exchange, requires completed authentication, rechecks drain/fence freshness at dispatch, poisons cancellation and verifies time after final scans and cleanup. Process fixtures cover closed standard descriptors, credential fd 3, signal-before-reap ownership, independent cleanup and construction failures; wire/envelope tests check exact native mutation preconditions and original synthetic credentials.

These are offline fixtures only. Public live activation remains disabled. No actual credential, source/provider operation, signature generation or new recovery RID/policy was used. Concrete provider/system/target/proof-consumer integration and actual authority/runtime/recovery qualification remain open. Original signed material, revision-4 packet, approved frozen intent, measured historical files and prior handoff prefixes remain preserved. **4.27 remains in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.**


## Final offline execution-boundary verification — 2026-10-23

The [final patched-source receipt](evidence/epic-4/4-27/20261023-recovery-execution-boundary/review-1/final/verification.json) binds 233 passing tests (69 driver cases plus 164 unchanged adapter cases), no skips, and positive detection of in-memory wrong-bearer/empty-CA regressions. Both earlier candidate packets retain their original bytes and meaning. Immutable request dispatch, independently checked input bytes, local request/policy budgets, credential/CA wire assertions, cancellation ownership and cleanup regressions are verified offline.

The approved offline continuation is complete. Public activation stays disabled. Concrete independent provider transport, applicable native credential delivery, full system/target/proof-consumer integration and actual authority/runtime/recovery qualification remain open; non-daemonizing tools require separate qualification. No actual credential, production/provider request, signing, upload, recovery, retirement, upgrade or deployment occurred. Original Story 4.27 and sprint remain **in-progress**; 4.1 stays **closed** and Timesheets 3.6 **open/FAIL**.

## Independent immutable-provider transport implementation — 2026-10-23

The [new scoped spec](spec-4-27-independent-immutable-provider-transport.md),
[provider interface handoff](../../eng/recovery-preparation/RECOVERY-EXECUTION-BOUNDARY.md#independent-s3-transport-implementation--2026-10-23)
and [separate verification](evidence/epic-4/4-27/20261023-immutable-provider-transport/verification.json)
record the next offline prerequisite: implemented `S3ImmutableProvider` behind
`ImmutableProvider.open_exact`. Retained evidence resolves the Scaleway S3
protocol, exact-version object/retention GETs and separate validator credentials.
The implementation pins endpoint, CA, region and credential identity/custody;
checks `AES256`, `COMPLIANCE`, both retention representations, streamed length
and SHA-256; and enforces bounded framing, absolute deadlines and owned cleanup.

All 32 focused inert-transport/synthetic-credential cases pass, including wrong
versions, metadata drift, truncated/oversized responses, timeout/cleanup failures
and composition with `ImmutableReadbackDriver` and all four boundary operations.
The provider remains dormant without an injected connector. Fixture success
continues to carry null operational qualification and recovery acceptance.

Remaining work includes actual credential/IAM/authority and connector/runtime
qualification; applicable native credential delivery; system/store, isolated
target and proof-consumer integrations; fresh exact attempt/policy/tool bindings;
and actual recovery/resume/cleanup and signed/non-dry/manual acceptance under
separate operational scope. No actual credentials/production endpoints,
recovery-attempt allocation, recovery-document signing, upload or recovery
operation occurred. Prior evidence, original handoff prefixes, revision 4 and
unrelated workspace changes remain preserved. **Story 4.27 stays in-progress,
4.1 closed, Timesheets 3.6 open/FAIL; public activation and operational gates
remain closed.**


## Reviewed immutable-provider verification — 2026-10-23

The [corrected-source verification](evidence/epic-4/4-27/20261023-immutable-provider-transport/review-1/verification.json) records **275 passing tests, zero
skips**: 42 provider cases and 233 existing readback/boundary/authentication/
common/telemetry cases. Full command transcripts and source hashes are retained.
The initial 265-test candidate packet and both exact candidate source snapshots
remain separate and unchanged.

Review corrections cover dispatch binding after callbacks and inherited S3
providers, persistent stream ownership through cancellation and handoff,
standalone retention expiry/deadline clamping, exact TLS trust-anchor bytes,
large streamed objects and hostile second responses. An in-memory wrong-CA
substitution is now positively detected. All retained review items were resolved;
no work was deferred by this review. Remaining live credential/authority/runtime,
system/target/proof-consumer and actual recovery integrations stay as listed
above. Public activation remains disabled; **4.27 stays in-progress, 4.1 closed,
and Timesheets 3.6 open/FAIL**.


## Native client-certificate delivery — 2026-10-23

The [separate corrected-source verification](evidence/epic-4/4-27/native-client-certificate-delivery/verification.json) records 297
passing offline tests, zero skips, including 21 focused certificate cases.
The explicitly selected, custody-bound `client-certificate` profile now loads
its exact PEM certificate/key envelope through sealed memory descriptors and
omits Authorization. The default bearer profile preserves its wire behavior.
Real socket-free mutual TLS checks exact source/target client identities and
rejects no-op/wrong-certificate loaders. All four plan fixtures retain distinct
cluster audiences, native requests and semantic guards; deadlines, cancellation,
descriptor preservation and independent cleanup are covered. The parent audit's
interruption-at-first-cleanup-line ownership gap is corrected and regressed.
First-candidate transcripts remain separate from current-source evidence.

Actual credential/custody/IAM/authority and client identity qualification,
live connector/TLS/runtime qualification and activation, complete store/system
and isolated-target orchestration including telemetry PostgreSQL, proof-consumer
integration, fresh exact attempt/policy/tool bindings, and actual recovery,
resume/cleanup plus signed/non-dry/manual acceptance remain pending. Synthetic
fixtures confer no authority. No actual credentials/configuration/endpoints,
recovery-attempt allocation, operational certificate issuance, recovery-document
signing, upload, recovery or infrastructure mutation occurred. Public activation
and operational gates remain closed. Historical evidence, earlier handoff
prefixes, revision 4, signed originals and frozen intent remain preserved;
**4.27 stays in-progress, 4.1 closed and Timesheets 3.6 open/FAIL**.


## Reviewed native client-certificate ownership — 2026-10-23

The [review-1 corrected-source verification](evidence/epic-4/4-27/native-client-certificate-delivery/review-1/verification.json) records **305 passing tests, zero skips**: 29 certificate cases
and the 276 existing boundary/readback/authentication/S3/common/telemetry cases.
The initial 297-test packet, four exact candidate snapshots, first-candidate
transcripts and original spec receipt remain unchanged historical evidence.
The original spec receipt hash is reproducible from its retained
`spec-implementation-checkpoint.md`; the new packet binds the reviewed state
separately and retains the exact original parent probe invocation as an archive.

Certificate inputs now register persistent owners inside allocation, before
returning to installation. Registration itself retains ownership across entry,
append and constructor-return interruptions. C-level descriptor close state
prevents a completed close from retrying a reused descriptor number. Shared
failure state preserves an original loading interrupt through a later cleanup
transition interrupt, while the remaining owners receive cleanup attempts.
Independent parent probes and direct regressions verify these boundaries.

Real socket-free mutual TLS also covers traditional RSA and EC keys, a valid
intermediate certificate chain, and an isolated count-limit refusal below the
byte limits. Valid CRLF and missing-terminal-newline PEM forms retain their
original installed bytes. The isolated fd 3 fixture explicitly checks its
precondition, sentinel bytes and descriptor/status flags. All prior exact peer
DER/CA, no-op/wrong-loader, four-plan, native-wire/precondition, bearer, semantic
stop and null-qualification checks remain passing.

Actual credential/custody/IAM/authority and client identity qualification,
live connector/TLS/runtime qualification and activation, complete store/system
and isolated-target orchestration including telemetry PostgreSQL, proof-consumer
integration, fresh exact attempt/policy/tool bindings, and actual recovery,
resume/cleanup plus signed/non-dry/manual acceptance remain pending. Synthetic
fixtures confer no authority. No actual credentials/configuration/endpoints,
recovery-attempt allocation, operational certificate issuance, recovery-document
signing, upload, recovery or infrastructure mutation occurred. Public activation
and operational gates remain closed; **4.27 stays in-progress, 4.1 closed and
Timesheets 3.6 open/FAIL**. Earlier handoff prefixes and evidence are preserved.


## Review-2 native certificate allocation and cleanup — 2026-10-24

The [review-2 content-bound verification](evidence/epic-4/4-27/native-client-certificate-delivery/review-2/verification.json) records **315 passing
tests, zero skips**: 39 certificate cases and the 276 existing boundary/readback/
authentication/S3/common/telemetry cases. All 305 review-1 tests and every one of
the 24 registration subcases remain covered. Both historical candidate packets,
source snapshots, logs and spec receipt checkpoints retain their original bytes.

Each custody-backed allocator now registers a unique process-local memory-file
identity before the raw memfd syscall. Recovery of an interrupted syscall result
matches that exact memory-file identity, including its pinned inode once known;
closed owners from an earlier allocation cannot suppress a new allocation that
reuses their descriptor number. Stateful close ownership prevents re-closing a
released raw number. Direct opcode regressions cover memfd and duplicate results
before assignment for both certificate/key inputs, while unrelated, similarly
named and reused descriptors remain open and unchanged. Legacy raw allocator
callers retain their compatibility behavior without inheriting this custody
qualification.

The whole native exchange retains stream/error custody through final cleanup.
The original interruption survives a second interruption during initial error
recording, inner cleanup or the guarded outer fallback. Direct real-MemoryBIO
regressions also interrupt both stream-close entries with an open peer and an
owned memory input. Independent release attempts complete before any success;
the tested guarantee covers these two-interrupt patterns, not indefinitely
repeated asynchronous exceptions. New regressions reach custody-backed duplicate,
write, chmod and seal failures, isolate material/count bounds with no loader
dispatch, and reject empty/mismatched fixture collections instead of defaulting
or truncating them.

The executable receipt checker verifies recorded source, snapshot, log and spec
checkpoint hashes by content, without requiring a particular Git HEAD or index.
Its isolated self-test proves that matching changes to working source and its
snapshot still fail the receipt's source hash. The prior review-1 preservation
checker is unchanged: it deliberately requires the preparation HEAD and an
unchanged index, so it is a historical precommit check, not a later content-only
receipt verifier. Python patch/interpreter identity, OpenSSL and OS/release/
architecture are recorded without hostnames. Unrelated concurrent Story 2.3,
deferred-ledger and submodule edits are outside this scoped implementation and
were preserved.

Actual credential/custody/IAM/authority and client identity qualification,
live connector/TLS/runtime qualification and activation, complete store/system
and isolated-target orchestration including telemetry PostgreSQL, proof-consumer
integration, fresh exact attempt/policy/tool bindings, and actual recovery,
resume/cleanup plus signed/non-dry/manual acceptance remain pending. Synthetic
fixtures confer no authority. No actual credentials/configuration/endpoints,
recovery-attempt allocation, operational certificate issuance, recovery-document
signing, upload, recovery or infrastructure mutation occurred. Public activation
and operational gates remain closed; **4.27 stays in-progress, 4.1 closed and
Timesheets 3.6 open/FAIL**. Earlier handoff prefixes and evidence are preserved.


## Review-3 native certificate handoffs and bounded release — 2026-10-24

The [review-3 content-bound verification](evidence/epic-4/4-27/native-client-certificate-delivery/review-3/verification.json) records **325 passing tests,
zero skips**: 49 certificate cases plus the existing 276 boundary/readback/
authentication/S3/common/telemetry cases. All 315 review-2 behaviors, the 39 prior
focused test names and all 24 registration subcases remain. Historical packets,
including the review-2 checker and original adverse parent probes, are preserved.
The original baseline and shared-workspace history remain unchanged.

Native exchange now requires an explicitly supplied private owned connector.
Trusted connectors register a resource-free stream placeholder in shared custody
before constructor acquisition; prepared inert streams already have a shared
owner before transfer. Raw resource-returning callbacks refuse before credential
or context work. Safe partial construction and idempotent release are part of
that private contract; real descriptor release state must be C-owned. Two close
interruptions before release still receive an independent third attempt. Current
exchange cancellation survives secondary recording/cleanup errors, while an
already handled caller interruption cannot replace it. A completed C-level close
never retries a reused unrelated descriptor. Fixture-owned cleanup also closes
preallocated streams on invalid profile or CA refusal.

Memory allocation registers its placeholder before reserving C-owned recovery
scans and before creating a memory file. Recovery rechecks the expected allocator
identity at descriptor adoption and needs no new directory descriptor after raw
allocation. Isolated child tests exercise reservation failure and memfd/duplicate
raw-result transitions at the actual last descriptor slot, restore the child's
RLIMIT_NOFILE and preserve unrelated descriptor bytes/flags. This qualifies the
tested bounded process-local protocol; arbitrary hostile descriptor-table
mutation, unsupported/non-idempotent third-party closers, indefinitely repeated
interruptions, kernel stalls and actual connector/runtime remain unqualified.

The new checker binds sources/snapshots/logs and historical spec checkpoints by
content, then parses actual unittest totals/skips/outcomes. Isolated coherent
source/snapshot and count/skip/outcome metadata controls fail independently.
Historical spec checkpoints remain separate from later workflow status/triage
edits; frozen human intent is still compared exactly. The unsigned packet confers
no external authenticity or operational authority. Runtime/interpreter/OpenSSL
and OS/release/architecture are recorded without hostnames. Unrelated concurrent
Story 2.3/2.4, deferred-ledger, sprint-workflow and submodule edits are excluded.

Actual credential/custody/IAM/authority and client identity qualification,
live connector/TLS/runtime qualification and activation, complete store/system
and isolated-target orchestration including telemetry PostgreSQL, proof-consumer
integration, fresh exact attempt/policy/tool bindings, and actual recovery,
resume/cleanup plus signed/non-dry/manual acceptance remain pending. Synthetic
fixtures confer no authority. No actual credentials/configuration/endpoints,
recovery-attempt allocation, operational certificate issuance, recovery-document
signing, upload, recovery or infrastructure mutation occurred. Public activation
and operational gates remain closed; **4.27 stays in-progress, 4.1 closed and
Timesheets 3.6 open/FAIL**. Earlier handoff prefixes and evidence are preserved.


## Review-4 native certificate lifetime and evidence controls — 2026-10-24

The [review-4 content-bound verification](evidence/epic-4/4-27/native-client-certificate-delivery/review-4/verification.json) records **331 passing tests,
zero skips**: 55 certificate cases plus the existing 276 boundary/readback/
authentication/S3/common/telemetry cases. All 325 review-3 behaviors, all 49 prior
focused names and the 24 registration subcases remain. Every earlier packet,
including the review-3 checker and original adverse trace results, is preserved.
The original baseline and full current preparation HEAD are recorded separately.

The native lifetime guard starts before custody initialization and retains the
prepared stream through constructor entry, partial initialization and return.
Three independent idempotent cleanup passes protect their invocation handoffs
and active stream/input cleanup through the tested finite cancellation patterns.
Direct controls cover two cleanup-invocation interruptions, a lost raw syscall
result followed by scan/registration interruption, and a trusted constructor
failing after acquiring a C-owned descriptor. Caller exception history neither
replaces current cancellation nor turns a successful exchange into refusal.
Acquire must return None and produce a supported owned stream before dispatch;
protocol violations release every known prepared/current stream. The cleanup
identity check uses an explicit bounded loop instead of a generator.

Linear bounded PEM framing accepts TLS-valid compact base64 and leading blank
lines, preserving original installed bytes. Real SSLContext loading and exact
MemoryBIO peer/CA/wire checks remain, along with malformed/encrypted/mismatched
material refusals and separate envelope/material/count limits. Current sweeps
cover 299 line targets, 2,309 opcode targets and 81 secondary-cancellation targets:
no leaks, swallowed cancellations or unraisable exceptions. Five secondary
terminal-propagation sites are classified separately only after descriptor and
peer release is verified; a new external interrupt there has no signal-atomic
primary-return guarantee. Arbitrary hostile descriptor mutation, unsupported
closers, indefinitely repeated interruptions and kernel stalls remain unqualified.

The new checker requires all seven named suites and their exact pinned command
mapping, while keeping counts as recorded data. It rejects non-passing summaries.
Its isolated self-test runs actual unittest failure, skip, expected-failure and
unexpected-success transcripts with the same bound count and unchanged success
claims, updates all referencing hashes coherently, and requires refusal. Coherent
suite omission, command drift, source/snapshot drift and metadata drift also
refuse. Historical checkers/receipts stay unchanged; the unsigned packet supplies
reproducibility only, with no external authenticity or operational authority.

Actual credential/custody/IAM/authority and client identity qualification,
live connector/TLS/runtime qualification and activation, complete store/system
and isolated-target orchestration including telemetry PostgreSQL, proof-consumer
integration, fresh exact attempt/policy/tool bindings, and actual recovery,
resume/cleanup plus signed/non-dry/manual acceptance remain pending. Synthetic
fixtures confer no authority. No actual credentials/configuration/endpoints,
recovery-attempt allocation, operational certificate issuance, recovery-document
signing, upload, recovery or infrastructure mutation occurred. Public activation
and operational gates remain closed; **4.27 stays in-progress, 4.1 closed and
Timesheets 3.6 open/FAIL**. Earlier handoff prefixes and evidence are preserved;
unrelated Story 2.3/2.4, deferred-ledger, sprint-workflow and submodule work is
excluded. This offline pass performed no staging, commit, push or operational helper replay.

Shared-workspace provenance: preparation HEAD remains
`56c783bcd005e3ce0c728dc72e440a696a91d034`; a concurrent operation recorded
`b2ff52312b01db65aef3e8b60fb56681ae97d70a` (`fix: story 4.27`) during this pass.
That history is preserved and the observed current HEAD is separately bound in
`review-4/current-head.txt`; this pass performed no Git staging/commit/push.


## Review-5 native certificate entry and reproducibility — 2026-10-24

The [review-5 content-bound verification](evidence/epic-4/4-27/native-client-certificate-delivery/review-5/verification.json) records **335 passing tests,
zero skips**: 59 certificate cases plus the existing 276 boundary/readback/
authentication/S3/common/telemetry cases. All 331 review-4 behaviors, all 55
focused names and the 24 registration subcases remain. Earlier packets and
handoff prefixes are preserved; the original parent probes and complete
pre-correction independent run remain separate historical adverse evidence.

Exchange cleanup now starts at its first protected body transition, before
placeholder allocation and guard invocation/entry. Independent cleanup retains
prepared C-owned streams even when custody metadata is unavailable. Exact
current-call traceback provenance selects the loading interruption without
following stale saved exception history; re-raising the active caller's same
exception object is also a current failure. Direct no-dispatch regressions
check exact descriptor/flag preservation and C-owned release. Real socket-free
mutual TLS capped at TLS 1.2 verifies both negotiated versions, the original
client identity and exact native PATCH headers/body; default negotiation stays
covered.

Current finite sweeps pass 307 line, 2,369 opcode, 93 secondary-line and 32
prepared-entry targets, with no leaks, swallowed cancellations or unraisable
exceptions. One final cleanup-invocation target is separately classified only
after descriptor and peer release is verified. A new external cancellation
after completed release has no signal-atomic primary-return guarantee. Arbitrary
hostile descriptor mutation, unsupported/non-idempotent closers, indefinitely
repeated interruptions, kernel stalls and actual runtime remain unqualified.

The new receipt checker parses all bound sweep outcomes and reconciles counts,
terminal release/propagation and verified flags with the receipt. Independently
defined complete dependency-source and supporting-packet inventories prevent
empty/omitted inventory acceptance. Verbose executed test identities must equal
each required suite's source inventory, with no repeats. Isolated controls
coherently rebind adverse sweep bytes/hashes, count/terminal mismatches, omitted
dependency drift and same-count plain-OK unrelated/repeated transcripts and
require refusal. Real FAILED/skipped/expected-failure/unexpected-success controls
retain the expected test identities, keeping the hardcoded-parser mutation
control independent. Historical checkers/receipts remain unchanged; this unsigned
packet supplies reproducibility, not external authenticity or authority. Runtime,
source snapshots, spec checkpoints and current full HEAD are separately bound.

Actual credential/custody/IAM/authority and client identity qualification,
live connector/TLS/runtime qualification and activation, complete store/system
and isolated-target orchestration including telemetry PostgreSQL, proof-consumer
integration, fresh exact attempt/policy/tool bindings, and actual recovery,
resume/cleanup plus signed/non-dry/manual acceptance remain pending. Synthetic
fixtures confer no authority. No actual credentials/configuration/endpoints,
recovery-attempt allocation, operational certificate issuance, recovery-document
signing, upload, recovery or infrastructure mutation occurred. Public activation
and operational gates remain closed; **4.27 stays in-progress, 4.1 closed and
Timesheets 3.6 open/FAIL**. Unrelated concurrent work is preserved and excluded.
This pass performed no staging, commit, push or operational helper replay.


## Final native-certificate prerequisite verification — 2026-10-24

[Independent final verification](evidence/epic-4/4-27/native-client-certificate-final-verification-20261024/verification.json).

Independent final verification records **336 passing tests, zero skips** (60
certificate plus 276 retained boundary/readback/authentication/S3/common/telemetry
cases). All 335 prior positive behaviors and the 24 registration subcases remain.
The invalid-method postcondition now uses an explicit bounded loop; its new
regression rejects the old generator in an isolated in-memory mutation control.
Descriptor snapshots include identity, descriptor flags and file status flags.

Current finite sweeps pass **307 line, 2,341 opcode, 93 secondary and 32
prepared-entry targets**, with zero findings/unraisable exceptions and one
external cancellation classified only after verified release. The checker
requires all five exact verification commands and independently pins 215
preserved historical files. The complete 335-test candidate and original adverse
probes remain separate and unchanged. Replay instructions use a fresh recorder
copy so bound evidence is preserved. Three unrelated concurrent Story 2.5
lifecycle findings are recorded separately in the deferred-work ledger.

This completes the offline native-certificate prerequisite only. Actual
credential/authority/runtime, system/target/proof-consumer integration and
recovery qualification remain pending. Public activation stays disabled;
**4.27 remains in-progress, 4.1 closed and Timesheets 3.6 open/FAIL**. No actual
credentials/configuration/endpoints, operational helper replay, recovery
allocation/signing/upload/recovery or infrastructure mutation occurred. The
scoped no-staging/commit/push instruction is retained.

## Authorized operational resume — 2026-10-31

The Administrator requests resuming and completing KubeSphere retirement while
preserving workloads and data. For this run the Administrator explicitly
authorizes remote checks and the gated live operations required by this story,
overriding the build workflow's local-only/no-remote restriction. Earlier
offline-only continuation boundaries do not prohibit this run's authorized work.

Refresh required backup and restore evidence, validate every prerequisite, and
complete retirement and private native-access verification. Use existing
approvals where their exact scope and validity still apply; request only missing
required decisions. This direction supplies no fabricated signature, expired
proof renewal or exact resource/attempt approval. Keep Story 4.1's upgrade gate
closed until all prerequisites pass. Report measured evidence and any remaining
blockers; preserve historical records and unrelated workspace changes. No Git
staging, commit or push is requested. At entry the sole unrelated untracked file
is `spec-2-8-run-ci-integration-on-disposable-runners-with-guaranteed-cleanup.md`
under this implementation-artifacts directory.


## Current guarded recovery packet — 2026-11-01

The explicit operational resume was reconciled with the preserved October 31
coordination pause and accepted amendments. The scheduling end is
**2026-11-02T23:00:00Z**; this exact attempt remains bounded earlier at
**2026-11-01T23:00:00Z**, inside the unchanged measured native/TLS horizon.
No signed proof or certificate freshness was extended. Existing RID
`20261031t175250z` and earlier evidence are retained; no replacement RID was
allocated by this continuation.

The [current unsigned preparation verification](evidence/epic-4/4-27/20261101t000316z-s427-current-recovery-packet/verification.json) binds a concrete
[owner approval procedure](/home/administrator/hexalith-recovery-evidence/4-0/20261031t175250z/CURRENT-RECOVERY-APPROVAL.md) and
[protected-terminal signing action](/home/administrator/hexalith-recovery-evidence/4-0/20261031t175250z/NEXT-PROTECTED-SIGNING-ACTION.md).
Exact unsigned policy SHA-256: `bb5c1e4d86dffe48a4379cac4cc81c49210b38fe897250e6d6cd450c97291a51`.
The strict exact-byte local guard passes with zero remote requests and a closed
operational gate. The policy binds all 52 ordered steps, current source/target
proposals, tools, private SDK, original trust/custody and explicit native profile.

The corrected PostgreSQL v13 synthetic rehearsal passed actual snapshot capture,
decryption, empty-target restore, full canary comparison, creation-fence checks
and exact target namespace/PV/node-path cleanup; aggregate owned fixture cleanup
passed. Its scoped summary is `passed-limited`, without production data or
credentials. The v12 target crash/failure and cleanup remain immutable. The
parent independently passed all 93 changed-repository tests; the final current
caller suite passed 42 cases including 16 pre-dispatch CLI subcases. Actual
workflow-descendant cleanup now retains the leader until group death is observed;
the parent's immediate-state regressions passed. The private single-link SDK's
byte identity and actual three-call validator readback passed. Initial unsigned
candidate/schema/custody/manifest-order refusals and all superseded bytes remain
preserved. These are preparation/qualification outcomes, not whole-source
recovery acceptance.

Current known-source checks used 27 bounded native GETs, 19 destination reads
and three SDK validator reads, preserving protected identities/bindings/readiness
with encrypted raw custody and local readback. No production capture, pause,
restore, provider PUT, signature, retirement or upgrade ran.

**Next actual owner inputs:** review and approve the exact new policy/native/
interruption/custody/stop/resume scope, then sign/publish its original in the
protected terminal. Authenticate its exact immutable policy/signature readbacks
before capture. A separate actual current-RID C3 freeze announcement is required
before the final Memories pre/census/pause; an old reply or policy signature
supplies no such fact. Later authentic original signatures, non-dry validation
and honest current M1/M2 remain required. No repeat destination/key/role/channel
selection or second documentary reviewer is needed. Fresh accepted 4.0, actual
native/etcd/node recovery and separate exact retirement plan/approval/arm still
precede retirement. **4.27 stays in-progress, 4.1's upgrade gate closed and
Timesheets 3.6 open/FAIL**; original production tasks and frozen intent remain
unchanged.


## Current freeze acknowledgement — 2026-11-01

The Administrator replied **“Freeze announced for this run”** in response to the
current request to notify Memories API/operator callers and freeze operator-token
issuance. The owner-only announcement is bound to RID `20261031t175250z` and
policy SHA-256 `bb5c1e4d86dffe48a4379cac4cc81c49210b38fe897250e6d6cd450c97291a51`; the policy bytes are unchanged.

The [new acknowledgement checkpoint](evidence/epic-4/4-27/20261101t063953z-s427-current-freeze-acknowledgement/verification.json) records the actual message
provenance and the local guard's `currentC3AnnouncementSupplied=true`. This is an
unsigned owner statement, not an original policy signature or recovery acceptance.
The original policy signature and publication ledger are still absent. No remote
request, capture, pause, restore, publication or signing was performed.

The next actual owner action remains the [protected policy signing action](../../../../../hexalith-recovery-evidence/4-0/20261031t175250z/NEXT-PROTECTED-SIGNING-ACTION.md).
The current C3 acknowledgement needs no repeat confirmation. Fresh authenticated
policy/signature readbacks still precede capture; all later signatures, manual
acceptance and exact retirement approval remain required. Story 4.27 remains
in-progress and the 4.1 upgrade gate remains closed.


## Authenticated policy continuation after protected signing — 2026-11-01

The Administrator replied **“done”** after the exact protected-terminal action.
The [new original-signature/readback checkpoint](evidence/epic-4/4-27/20261101t120715z-s427-authenticated-policy-continuation/verification.json) records independent
verification of policy `bb5c1e4d86dffe48a4379cac4cc81c49210b38fe897250e6d6cd450c97291a51`, its original Administrator SSH signature, both
actual immutable object versions with the distinct validator credential, current
code/trust/node bindings and the current RID C3 announcement. There are no prior
production step intents. This resolves the initial policy-signing/C3 barriers;
actual whole-source recovery remains unaccepted and no all-record signing
delegation is inferred. The exact attempt ends `2026-11-01T23:00:00Z`.

Resume the [exact reviewed 52-step procedure](../../../../../hexalith-recovery-evidence/4-0/20261031t175250z/CURRENT-RECOVERY-APPROVAL.md) sequentially through
`tools-complete-v3/recovery_step.py`, beginning with the first unexecuted step.
Refresh actual source identity/configuration/health, grants, capacity and time
budget at the named gates; current policy authentication/readbacks precede every
dependent operation. Never replay an existing passed, failed or uncertain intent.
Stop on any nonzero, false or unobserved semantic result and retain encrypted raw
diagnostics. Use only the exact policy's conditional intact-source failure branch
when every stated ownership/content/semantic condition actually passes; an
unknown or changed source remains under the Administrator's incident decision.

After successful Memories recovery and clean drain recheck, resume the intact
source and intake immediately before waiting for later signatures. At each later
authentic original-record signing prerequisite, prepare the actual unsigned
record and exact protected-terminal action; preserve scope approval and request
only the missing real signature. Do not fabricate signatures or sign on the
Administrator's behalf. Fresh accepted 4.0 recovery still precedes the separately
bound native/node/etcd prerequisites. Actual retirement requires its fresh
source-bound plan, exact approval and arm; no upgrade, Rancher or Timesheets
deployment authority is supplied. Story 4.27 remains in-progress and 4.1 closed.


## Corrected recovery policy successor — 2026-11-01

The authenticated RID `20261031t175250z` completed `watch-on` and
`keycloak-pre`, then `keycloak-backup` refused before its intent or backup
request: the signed policy used a pretty raw-file hash for the compact semantic
proposal field required by the actual PostgreSQL consumers. No outage or restore
target began. The exact owned CNPG watch transition was reversed with the
unchanged signed UID/resourceVersion/full-spec guard and rollout passed. Source
intake/worker/clock remain 2/1/0. Original signed policy/signature, immutable
selectors, canonical source records, standing freeze statement and journal are
retained unchanged; no intent is replayed or relabelled successful.

The [new verification](evidence/epic-4/4-27/20261101t123705z-s427-corrected-policy-successor/verification.json) binds unsigned successor RID
`20261101t121847z`, its [reviewable exact correction](../../../../../hexalith-recovery-evidence/4-0/20261101t121847z/CURRENT-RECOVERY-APPROVAL.md)
and [protected-terminal signing action](../../../../../hexalith-recovery-evidence/4-0/20261101t121847z/NEXT-PROTECTED-SIGNING-ACTION.md).
Final policy SHA-256: `fa4053b10c5ac9fc6e10f83de99493fc0ee01a9e29c4d0913c5bed941f7606bf`.
Compact semantic digest and legitimate raw-file/template/proof hashes are checked
separately. The actual producer→Live/record/plan/guard regression, wrong-encoding
controls, original standing-freeze lineage and actual node caller/emitter/RID
agreement pass. The regression also passes with an existing real canonical
policy while altering only disposable fixture copies. Forty-three distinct
current caller cases and nine rebound node guards pass; the parent independently
reran the settled cross-consumer regression and exact guard. The final guard
uses zero remote requests and remains closed pending the new original signature
and immutable readbacks. Unchanged synthetic primitive qualification is retained
without another production recovery claim.

Fresh identity/storage/readiness and measured capacity checks passed; twenty
read-only destination checks passed with encrypted raw custody/readback. The
original **“Freeze announced for this run”** remains the standing owner fact
under explicit predecessor RID/policy/hash lineage; it was neither overwritten
nor fabricated for the successor and needs no repeat announcement. Existing
operational scope approval persists. The concrete missing owner action is the
new exact original policy signature/publication/readback. The successor's fresh
journal starts at `watch-on` only after that barrier, with current source and
time gates remeasured. Later original signatures and honest M1/M2 remain required.

The exact attempt still ends **2026-11-01T23:00:00Z**, within the administrative
end **2026-11-02T23:00:00Z** and unchanged native/TLS horizon. No source measurement
time or old proof freshness was renewed. No backup request, quiescence, source
restore, target creation, successor PUT/signature, retirement, upgrade, Rancher
or Timesheets deployment occurred. Fresh accepted recovery and separate exact
retirement plan/approval/arm remain pending. **4.27 stays in-progress, 4.1 closed,
Timesheets 3.6 open/FAIL**; original production tasks and frozen intent remain
unchanged.


## Authenticated corrected successor continuation — 2026-11-01

The Administrator replied **“done”** after the exact corrected successor signing
block. The [new authenticated checkpoint](evidence/epic-4/4-27/20261101t124411z-s427-authenticated-successor-continuation/verification.json) records independent
verification of corrected policy `fa4053b10c5ac9fc6e10f83de99493fc0ee01a9e29c4d0913c5bed941f7606bf`, its original Administrator SSH
signature, both actual immutable policy/signature versions using the qualified
SDK and distinct validator, and the actual standing freeze through its unchanged
predecessor RID/policy/hash. Successor RID `20261101t121847z` has no step intents.
The signed predecessor remains stopped with watcher restored; never replay or
alter its signed inputs, completed intents or publication ledger.

Resume the [corrected exact 52-step procedure](../../../../../hexalith-recovery-evidence/4-0/20261101t121847z/CURRENT-RECOVERY-APPROVAL.md) from fresh successor
`watch-on`, one exact signed `recovery_step.py` stage at a time. Recheck current
source identity/configuration/health/grants, capacity and remaining time before
mutation. This explicitly continues the already approved recovery scope under
the now-authenticated corrected policy; no repeat scope or freeze confirmation
is needed. The exact attempt ends `2026-11-01T23:00:00Z`.

Retain every result and stop on false/nonzero/unknown observations or uncertain
mutation; no automatic replay or adoption. Use only the policy's exact approved
owned-cleanup/intact-source failure branch when every condition actually passes.
After successful Memories restoration and clean drain recheck, resume the intact
source/intake immediately before later signature waits. No all-record signing
delegation was supplied: at each later original-signature barrier, prepare the
actual record and exact protected-terminal action for the Administrator. Honest
manual final acceptance and every later recovery/retirement gate remain required.
Actual whole-source recovery is still unaccepted; 4.27 stays in-progress and 4.1
closed. No retirement plan/arm, upgrade, Rancher or Timesheets deployment is
authorized by this continuation.


## Retained authenticated Keycloak point continuation — 2026-11-01

Signed RID `20261101t121847z` passed its first five stages: watcher enable,
Keycloak pre, actual fresh backup, WAL/RPO and post. The backup completed,
required WAL archived, pre/post canaries matched and source storage bindings
remained fixed. `keycloak-create-target` then failed at its first source GET
because a legacy call used bare kubectl under the deliberately stripped workflow
environment. No target apply/create occurred. Encrypted diagnostics, all five
passed journals, failed target intent/result, signed originals and four raw source
records remain unchanged. Six explicit read-only GETs proved all four target
namespaces absent, no target PV claims and the completed backup UID preserved.
Exact owned watcher restoration and rollout passed with the controller UID
preserved; intake/worker/clock remain 2/1/0. No source outage began.

The [new verification](evidence/epic-4/4-27/20261101t133559z-s427-retained-keycloak-continuation/verification.json) binds unsigned completion RID
`20261101t131121z`, [exact reviewable scope and lineage](../../../../../hexalith-recovery-evidence/4-0/20261101t131121z/CURRENT-RECOVERY-APPROVAL.md)
and [protected-terminal signing action](../../../../../hexalith-recovery-evidence/4-0/20261101t131121z/NEXT-PROTECTED-SIGNING-ACTION.md).
Policy SHA-256: `b592cd01db298b4b1f556ee7ef9d9d7edb950e326b97653ee6729f2e322a67d4`.
All selected native call sites now use pinned explicit regular kubeconfig/context
and bounded timeouts. The actual stripped workflow environment and helper paths
pass their local tests without ambient defaults. The 49-step continuation omits
all five passed IDs; the prior capture entrypoint explicitly rejects recapture
under this policy. Distinct verification/watch/explicit-target stages precede the
remaining approved sequence; no fabricated successor passes or overwritten failed
intent exists.

The existing actual Keycloak point is imported byte-for-byte. Its source cutoff
**12:47:52Z**, durability/capture **12:48:05Z**, source observation **12:48:10Z**
and measured RPO **18 seconds** remain unchanged. Original encrypted policy/
signature readback at **12:47:16.821065Z** preceded source pre/capture under the
authentic origin policy. Actual restore, proof and final-validator consumers
verify that lineage, original versions, raw source/journal hashes and old age
rules. A later new policy does not renew the point or the original policy age
cap. Unauthorized/mismatched origin, rewritten clocks, expired point and stage
replay controls pass. Fifty-three caller cases and nine rebound node guards
passed; the parent independently passed all 11 selected lineage/transport/
producer-consumer cases and the exact signing guard. Test target outcomes remain
explicitly inert and supply no production restore evidence.

The standing original **“Freeze announced for this run”** record remains at its
actual predecessor RID/policy/hash. Existing operational scope approval persists.
The concrete missing action is the new exact original policy signature/publication
and independent immutable readbacks; no repeat broad scope or freeze confirmation
is requested. Current guard passes with zero remote requests and remains closed.
Original capture clocks, prior signed packets, native/TLS facts and history remain
preserved. The exact attempt still ends **2026-11-01T23:00:00Z** within the broader
administrative window. No new continuation step, signature or PUT ran.

Actual isolated recovery, Memories pause/resume/cleanup, later original signatures,
non-dry validation and honest current M1/M2 remain required. Fresh accepted 4.0
and separate native/node/etcd prerequisites still precede an exact retirement
plan/approval/arm. **4.27 stays in-progress, 4.1 closed, Timesheets 3.6 open/FAIL**.
No retirement, upgrade, Rancher or deployment ran; frozen intent and original
production tasks remain unchanged.


## Authenticated retained-point continuation — 2026-11-01

The Administrator replied **“done”** after the exact retained-point signing
block. The [new authenticated checkpoint](evidence/epic-4/4-27/20261101t140032z-s427-authenticated-retained-continuation/verification.json) records independent original
Administrator signature and exact immutable policy/signature readback verification
for policy `b592cd01db298b4b1f556ee7ef9d9d7edb950e326b97653ee6729f2e322a67d4`, RID `20261101t131121z`. The actual standing freeze and the
original Keycloak point authorization/readbacks also verify. Actual backup UID
`30186df2-2d23-4b3b-a5cc-13badb74db31`, cutoff `12:47:52Z`, durability
`12:48:05Z` and measured RPO 18 seconds remain unchanged; no recapture is claimed.
The continuation journal is empty. Old signed inputs and all stopped/passed/failed
intents remain immutable. The exact attempt ends `2026-11-01T23:00:00Z`.

Resume the [authenticated 49-step retained-point procedure](../../../../../hexalith-recovery-evidence/4-0/20261101t131121z/CURRENT-RECOVERY-APPROVAL.md), one
exact current signed wrapper stage at a time. Begin with actual retained-point
and current-source verification, then the separately named watcher reenable and
explicit native target stages. Never replay the five passed source stages or the
failed old target intent; the current capture entrypoint forbids those replays.
Refresh source identity/configuration/health, grants, capacity and time at the
named gates. No repeat scope or freeze confirmation is needed.

Keep the original capture/durability/RPO clocks and original age caps throughout
restore, proofs and final validation. Stop on false/nonzero/unknown outcomes and
retain encrypted command/results. Apply only the exact approved owned-cleanup or
intact-source failure branch when its conditions actually hold. After successful
Memories restore and clean drain, resume intact source/intake before waiting for
later signatures. Later originals remain Administrator-only; prepare the actual
record and exact protected signing action at the real barrier, without inventing
a signature or all-record delegation. Fresh accepted 4.0 and separately bound
native/etcd/node recovery still precede a fresh exact retirement plan/approval/arm.
Story 4.27 remains in-progress, 4.1 closed; no upgrade, Rancher or Timesheets
deployment is authorized.


## OIDC listener incident: diagnostic passed, exact decision pending — 2026-11-01

The authenticated retained-point run completed its first nine stages and stopped
at the original private OIDC command. Its canonical result remains **allPass=false**,
**portForwardsClosed=false**, SHA-256
`2825e15794ba5f77c0c0971b0a21cdcff12b7133cd082901ea1c7992cb08ae38`.
All passed/failed journals, signed policy, original source point and clocks remain
unchanged. No later isolation, OpenBao or Memories stage followed that failure;
Memories intake/worker/clock remain 2/1/0. The known isolated Keycloak target,
existing verifier and temporary watch extension remain retained.

The [new checkpoint](evidence/epic-4/4-27/20261101t151655z-s427-oidc-listener-incident-preparation/verification.json) records the bounded read-only investigation.
The old bind predicate also refuses a real inert active-close/TIME_WAIT connection
after its owned process exits and no listener remains. The original failure's
exact transient TCP state was not captured; this is a supported diagnosis, not a
retroactive success. Corrected startup/cleanup inspect actual TCP/TCP6 LISTEN state,
require owned-child death and refuse unrelated listeners without signalling them.
All eight checks, claims-shape logic, custody and deadlines remain unchanged.
Eighteen helper tests and ten focused incident/consumer tests pass; the parent
independently passed both selected suites and the exact packet guard.

The distinct current-target read-only diagnostic observed all eight checks
strictly true and both loopback forwards terminated with no listener. Its
SHA-256 is `9a76b6930b63383a740d2991421c28851bca00ceeaa032361919304c3d16c341`.
It permanently remains diagnosticOnly=true and incidentApproved/operationalAcceptance=false.
The failed canonical artifact is neither overwritten nor promoted.

The [concrete narrow incident action](../../../../../hexalith-retirement-investigation-evidence/20261031t160723z-s427-operational-resume/continuation-20261101t140315z/oidc-listener-correction/INCIDENT-ACTION.md) binds the unchanged current policy
`b592cd01db298b4b1f556ee7ef9d9d7edb950e326b97653ee6729f2e322a67d4`,
corrected helper, passing diagnostic, explicit guarded proof consumer, exact
retained target UIDs and separate no-replay 39-stage plan. Approval-bindings
SHA-256: `eab9bc79a217d4adffa61b8dd00e18c1a0033f710670d91e32037a9515d6a602`.
Actual owner acceptance is **pending**. Every selected entrypoint refuses before
remote calls while it remains pending; no continuation was dispatched. The
requested owner reply is **“Accept the OIDC listener correction and continue
20261101t131121z.”** This is the October 14 incident pattern on the existing target,
with no new policy/RID, recapture, bootstrap, broad scope or freeze request.

After actual exact acceptance only, recheck the original authorization, point
age, current source/UID/storage/health and remaining time; begin at the original
unexecuted `keycloak-isolation`. The signed manifest must bind both OIDC artifacts,
the actual decision and every accepted correction input. The explicit proof
variant changes only the guarded OIDC reference/provenance, preserving all original
acceptance and age rules. Target presence requires its exact UID; later absence
requires the authenticated approved cleanup stage and recorded target identity.
Later original signing remains Administrator-only at the real barriers. The
attempt still ends **2026-11-01T23:00:00Z**; no proof or TLS horizon is renewed.

The separately observed unrelated `mcpcli-417-20261101-00c6a713` namespace/PVC/PV
remain unowned and outside cleanup scope. All 85 original protected resource
identities/bindings were present in the encrypted census; no whole-global-census
unchanged claim is made. Existing node probes retain their own bounded before/
after checks and can refuse concurrent unrelated changes.

Fresh accepted recovery, later signatures/readbacks, non-dry validation and honest
current M1/M2 remain unfinished. **4.27 stays in-progress, 4.1 closed, Timesheets
3.6 open/FAIL**. No retirement, upgrade, Rancher or deployment ran. Original
production tasks and frozen intent remain unchanged.


## Actual OIDC listener incident acceptance — 2026-11-01

The Administrator first replied **“accept”**, retained verbatim in create-only
owner custody, then supplied the exact structured reply **“Accept the OIDC listener correction and continue 20261101t131121z.”**. The
[actual incident decision](../../../../../hexalith-retirement-investigation-evidence/20261031t160723z-s427-operational-resume/owner-acceptance-20261101t162030z/incident-decision.json) SHA-256 `15ac76556f28dddfe8eba3dcc671d34f28b9fdb8afe157f88cd54c555d10f559` accepts the
[reviewed incident action](../../../../../hexalith-retirement-investigation-evidence/20261031t160723z-s427-operational-resume/continuation-20261101t140315z/oidc-listener-correction/INCIDENT-ACTION.md), SHA-256
`a46f171ce44f5cd25be30485e7e297f07e9734706776372666aeeed3409a1725`, and unchanged approval bindings
`eab9bc79a217d4adffa61b8dd00e18c1a0033f710670d91e32037a9515d6a602`. The pending draft, original canonical
OIDC failure, distinct passing diagnostic, signed policy and all original capture
clocks remain unchanged. No repeat original policy/scope/freeze approval is pending.

The [authenticated checkpoint](evidence/epic-4/4-27/20261101t162335z-s427-accepted-oidc-listener-continuation/verification.json) records the parent's fresh
read-only exact incident guard: original signed policy and retained-point immutable
readbacks authenticate; current source/retained-target UID/storage/health checks
pass; all 85 original protected resources remain bound. Unrelated observed
namespace/storage remains unowned and outside cleanup scope. No resumed stage had
been dispatched at this checkpoint. The current policy is
`b592cd01db298b4b1f556ee7ef9d9d7edb950e326b97653ee6729f2e322a67d4`, RID `20261101t131121z`; the
exclusive attempt end remains **2026-11-01T23:00:00Z**.

Resume the separate accepted 39-stage dispatcher one exact stage at a time,
beginning at `keycloak-isolation`, using this actual decision path and digest.
Recheck the first-stage live preflight and every original semantic/ownership/time
gate. Do not replay bootstrap, capture or the failed OIDC stage, overwrite original
records, edit accepted bytes, adopt unrelated objects, or dispatch after a failed
or unknown result. Retain encrypted command/results and the distinct continuation
journal. The explicit proof variant must bind both OIDC artifacts, this actual
decision and accepted correction provenance in the signed manifest.

After successful Memories restoration and clean drain checks, resume the intact
source/intake immediately before later signature waits. Later original cleanup,
manifest/proof, gate and final-validation signatures remain Administrator-only;
prepare each exact actual signing action at its real barrier. Honest current
M1/M2 and non-dry validation remain required. Fresh accepted recovery and separate
native/node/etcd prerequisites still precede an exact retirement plan/approval/arm.
Story **4.27 remains in-progress**, 4.1 closed and Timesheets 3.6 open/FAIL.
No retirement, upgrade, Rancher or Timesheets deployment authority is added.


## Memories role-order refusal: source unpaused, exact correction pending — 2026-11-01

After the actual accepted OIDC decision, the separate continuation completed
**15 stages**: Keycloak isolation/identity; OpenBao pre/snapshot/off-node transfer/
post, target/TLS/voters, restore, verification/isolation/identity; and Memories
source-pre. All required Keycloak/OpenBao isolation checks and all 10 OpenBao
restore checks passed. The actual OpenBao off-node snapshot checksum, AES256/
COMPLIANCE readback and temporary source-reader deletion passed; isolated restore
returned HTTP 204. These are real stage observations, not whole-run acceptance.

The next `memories-final-census` returned nonzero before executing its tenant
census or writing `memories/census.json`. Source metadata SQL succeeded; the
original caller compared two role lists positionally. All 18 unique complete
role records match after canonical sorting; only sequence differs. System ID,
timeline, server version and extensions match. The failed original intent/result,
all 15 passed stages, source proposal, signed tools/policy and original clocks
remain unchanged. **No Memories pause, writer/store change or capture followed.**
At the bounded read-only held-state observation, intake/worker/clock remained
2/1/0 and Redis/FalkorDB/telemetry PostgreSQL remained 1/1 ready. Both known
Keycloak/OpenBao target namespaces and the temporary watcher extension remain
retained. No cleanup/adoption was dispatched.

The [new checkpoint](evidence/epic-4/4-27/20261101t165532z-s427-memories-role-order-incident/verification.json) binds the encrypted failure/metadata evidence and
the [concrete narrow correction action](../../../../../hexalith-retirement-investigation-evidence/20261031t160723z-s427-operational-resume/continuation-20261101t140315z/memories-role-order-correction/INCIDENT-ACTION.md). A distinct read-only diagnostic
at **16:41:03Z** completed all remaining source predicates: exact source runtime/
storage identity, control/roles/extensions, complete table census and source
configuration/tools; actual Redis dbsize 3 with zero stream/workflow/reminders/
inflight, FalkorDB zero keys/graphs and the accepted zero-tenant profile.
Diagnostic SHA-256: `ff841d618995df227538256e5f5056694113a5a91e8034e3f2f19a96c0a106ac`.
Its diagnosticOnly=true, incidentApproved/operationalAcceptance=false and no
canonical-census/capture/outage flags remain unchanged.

The correction compares sorted canonical bytes of complete role records, retaining
all fields, duplicate/count/type checks and privileges. Actual producer/consumer
regressions reject changed, missing, extra, duplicate and wrong-type records.
No shell/SQL newline fix was needed: AST and actual runtime values match the
unchanged payload. The final selected Live v2 and supervisor v3 add exact decision
gates, explicit named imports and two pinned-original `mem.py` routes; their core
is equivalent to the actually observed diagnostic. They have **not** run live.
The static proof variant changes only the explicit combined guard/provenance,
preserving original OIDC/retained-point acceptance and clocks. It requires the
new approved cleanup lifecycle without editing the old stopped journal, and the
manifest must bind both OIDC artifacts/decisions and all correction provenance.

All **14 focused tests** pass, including actual old-vs-corrected Live consumer,
real unchanged `mem.py` execution to the inert native boundary under stripped
environment, all three selected CLI gates, canonical source-copy hashes,
new/old proof-guard wiring, exact 24-stage projection and both held target
lifecycle branches. The source remains unpaused. Exact local verification
authenticates original SSH/policy/tools and prior OIDC acceptance, verifies all
settled inputs and reports zero native/remote requests with the gate closed.

Approval-bindings SHA-256:
`f478d1aba1001b2772090aaba536649b183f6d0f719b99e453c31d2abe8ce227`.
Actual new correction acceptance is **pending**. The owner can reply **“accept”**
to the reviewed action; preserve the actual reply verbatim separately from the
canonical accepted statement **“Accept the Memories role-order correction and
continue 20261101t131121z.”** No repeat whole policy, scope, freeze, recapture or
bootstrap action is requested. Only after that exact decision, begin the distinct
`memories-census-role-order-correction` stage, then the original 23 unexecuted
stages. Fresh gates recheck all 85 original protected bindings, both exact held
workload/namespace/storage UIDs, source/intake/watch and remaining time. Unrelated
observed resources remain unowned and outside cleanup scope; no global-census
unchanged claim is made.

The current signed policy remains
`b592cd01db298b4b1f556ee7ef9d9d7edb950e326b97653ee6729f2e322a67d4`;
actual prior OIDC decision remains `15ac76556f28dddfe8eba3dcc671d34f28b9fdb8afe157f88cd54c555d10f559`.
The original Keycloak cutoff **12:47:52Z**, durability **12:48:05Z**, source
observation **12:48:10Z** and RPO **18 seconds** remain unchanged. Exact attempt
end remains **2026-11-01T23:00:00Z**; no point/proof/TLS horizon is renewed.
After successful actual Memories restoration and clean drain, resume intact
source/intake before any later Administrator-only original-signature wait.
Unknown/failed results stop without retry or an invented recovery decision.

Accepted complete recovery, later signatures/readbacks, non-dry validation and
honest M1/M2 remain unfinished. **4.27 stays in-progress, 4.1 closed, Timesheets
3.6 open/FAIL**. No retirement, upgrade, Rancher or deployment ran; frozen intent
and original production tasks remain unchanged.


## Actual Memories role-order incident acceptance — 2026-11-01

The Administrator replied **“accept”** to the exact
[reviewed role-order correction](../../../../../hexalith-retirement-investigation-evidence/20261031t160723z-s427-operational-resume/continuation-20261101t140315z/memories-role-order-correction/INCIDENT-ACTION.md), SHA-256
`535623456c5d303bc9f2f381b5a2722af6c63dc3dff83ee59cc0ec276105cde1`. The actual reply is retained verbatim
in a new create-only [incident decision](../../../../../hexalith-retirement-investigation-evidence/20261031t160723z-s427-operational-resume/role-order-owner-acceptance-20261101t165550z/incident-decision.json), SHA-256
`04d609a0ccd1388932fa80450bfb0f09e0225e851caf792247990f39d2d375ab`; the canonical accepted statement is recorded separately. This
accepts unchanged approval bindings `f478d1aba1001b2772090aaba536649b183f6d0f719b99e453c31d2abe8ce227`
and the separate 24-stage continuation. Prior OIDC acceptance
`15ac76556f28dddfe8eba3dcc671d34f28b9fdb8afe157f88cd54c555d10f559`, signed policy, original proposal,
all 15 prior passed stages, failed census intent/result and original point clocks
remain unchanged. No repeat policy/scope/freeze or incident decision is pending.

The [new authenticated checkpoint](evidence/epic-4/4-27/20261101t165731z-s427-accepted-memories-role-order-continuation/verification.json) records the parent's fresh
full read-only guard: original policy/retained-point immutable readbacks authenticate;
source metadata, complete table census, tools/configuration and zero-tenant profile
pass; all 85 protected bindings and both held namespace/workload/PVC/PV identities
match. Source intake/worker/clock remain 2/1/0. No canonical census or resumed
operation had been written at this checkpoint. Exact attempt end remains
**2026-11-01T23:00:00Z**; no native/TLS or recovery-point horizon is renewed.

Resume the exact accepted `role_order_step.py` dispatcher, one stage at a time,
starting `memories-census-role-order-correction` with this actual decision path/hash.
Recheck the mandatory first-stage live gates, retain encrypted results and the
new no-replay journal, and stop on any false/nonzero/unknown result. Keep signed
originals and accepted correction bytes fixed. Do not replay old stages, adopt
unowned resources or promote diagnostics. The combined proof guard must bind
both incident decisions/corrections, original OIDC failure and diagnostic, and
the legitimate cleanup in this new journal.

After successful Memories recovery and clean drain, resume intact source/intake
immediately before later signature waits. Original cleanup/manifest/proof/gate/
final-validation signatures remain Administrator-only at their real barriers;
prepare each exact actual signing action. Non-dry validation and honest current
M1/M2 remain required. Fresh accepted recovery and separate native/node/etcd
prerequisites still precede an exact retirement plan/approval/arm. **4.27 remains
in-progress, 4.1 closed and Timesheets 3.6 open/FAIL**. No retirement, upgrade,
Rancher or Timesheets deployment authority is added.


## Failed drain; intact intake-only recovery prepared — 2026-11-01

The accepted role-order continuation passed its distinct census and intake pause,
then `memories-drain` exited zero but failed its semantic gate. Both samples
counted one active unowned EventStore publisher in `mcpcli-417-20261101-00c6a713`;
actual EventStore and daprd containers and its Job remain Running/active. All seven
sample-equality checks and empty stream/workflow/reminder/inflight fields passed.
Failed quiescence SHA-256 `540dd7ef385d3ed5f43bd0ef5b227adaa1443e8c3f191ffa413814bb87247fd3` remains unchanged, with
intakePaused/drainPass false. No later stage, writer/store pause or capture ran.
Actual source intake/worker/clock are **0/1/0**; both held targets/watch remain.

The old signed resume branch requires passed drain and paused-writer/supervisor
evidence, which are absent. Do not force that branch, promote quiescence, retry
the failed stage or mutate/adopt the external Job. The [concrete intake-only
recovery action](../../../../../hexalith-retirement-investigation-evidence/20261031t160723z-s427-operational-resume/continuation-20261101t140315z/memories-drain-publisher-incident/intake-recovery/RECOVERY-ACTION.md) restores only owned MCP **0→original2**, with exact
UID/resourceVersion/full-spec tests and one patch. Its binding digest is
`ea6116785cea8598482f21dbd067119d329f9557f906b25c899024a52ad1f55a`.
Actual full intact-source read-only preflight and four focused tests pass; the
parent independently verified all four and 25 unchanged bound inputs.
The [checkpoint](evidence/epic-4/4-27/20261101t171406z-s427-drain-publisher-intake-recovery/verification.json) records pending owner recovery decision, zero
recovery mutation and no backup/continuation or external-Job authority.

The exact attempt still ends **2026-11-01T23:00:00Z**. Original signed policy,
proposal, both accepted corrections, failed records and point clocks remain fixed.
This recovery action grants no backup acceptance or later continuation. **4.27
remains in-progress, 4.1 closed and Timesheets 3.6 open/FAIL**; no retirement,
upgrade, Rancher or deployment ran.


## Actual intake-only recovery acceptance — 2026-11-01

The Administrator replied **“accept”** to the exact [intake-only recovery
action](../../../../../hexalith-retirement-investigation-evidence/20261031t160723z-s427-operational-resume/continuation-20261101t140315z/memories-drain-publisher-incident/intake-recovery/RECOVERY-ACTION.md). The actual create-only [decision](../../../../../hexalith-retirement-investigation-evidence/20261031t160723z-s427-operational-resume/intake-recovery-owner-acceptance-20261101t171845z/incident-decision.json), SHA-256
`d0c380b2a8ad4a493c51e4dfe62bfb5c3a40dfca8b0a879b0c3a4f3906587ff6`,
retains that reply verbatim and binds unchanged recovery inputs
`ea6116785cea8598482f21dbd067119d329f9557f906b25c899024a52ad1f55a`.
No repeat recovery approval is pending.

Execute exactly one accepted `restore_intake.py` attempt with that decision/hash,
using its fresh intact-source/full-spec preflight, UID/resourceVersion/full-spec
JSON tests and encrypted no-replay outcome. Scope is only owned
`hexalith-memories/deployment/memories-mcp` UID
`a9bf6dde-593d-4313-af47-c55670e2a0a7`, **0→original2**, followed by actual
readiness verification. Keep failed quiescence and all accepted/signed records
unchanged. **No backup/capture/continuation, cleanup, source store/writer change,
or external Job/Pod mutation is authorized.** Stop on unknown outcome without
retry. Record actual source availability and the precise remaining drain blocker
afterward. Story4.27 remains in-progress, 4.1 closed and Timesheets3.6 open/FAIL.


## Intact source intake restored; recovery remains stopped — 2026-11-01

The actual intake-only decision `d0c380b2a8ad4a493c51e4dfe62bfb5c3a40dfca8b0a879b0c3a4f3906587ff6`
was executed **once** using the unchanged accepted driver and binding. Fresh
intact-source/full-spec preflight passed. One UID/resourceVersion/complete-spec
JSON patch restored only `hexalith-memories/deployment/memories-mcp`, UID
`a9bf6dde-593d-4313-af47-c55670e2a0a7`, from 0 to its original 2 replicas.
The patch outcome is known and actual two-ready rollout completed at
**2026-11-01T17:20:10Z**. The [actual recovery result](../../../../../hexalith-retirement-investigation-evidence/20261031t160723z-s427-operational-resume/continuation-20261101t140315z/memories-drain-publisher-incident/intake-recovery/restore-result.json), SHA-256
`ea3f8b51c0cf89b9ef4187eb067b6f83f35fe5b98ae577f1bc6b91ea7ed2e595`, records
intake-restored, patchOutcomeKnown/intakeReady true and failedQuiescencePreserved
true, with backupAccepted/operationalContinuationAuthorized/externalJobMutationAuthorized
false. No archived data was restored and no failed stage was retried.

The [new checkpoint](evidence/epic-4/4-27/20261101t172332z-s427-intact-source-intake-restored/verification.json) records a separate read-only observation at
**17:21:29Z**: source intake/worker/clock are **2/1/0 ready**, Memories API is
2-ready, and Redis/FalkorDB/access-telemetry PostgreSQL are each 1-ready. Their
recorded UIDs are preserved. The parent independently verified the exact original
source Deployment specifications/UIDs, readiness and all three source stores.
The recorded intake pause at **16:59:03Z** to confirmed ready at **17:20:10Z**
spans **1267 seconds**; source stores and worker were never changed by this
intake recovery. This interval describes the recorded intake pause, not a claim
that the data services were stopped.

Both isolated targets remain held with exact recorded namespace/workload/PVC/PV
identities: Keycloak app/DB ready 1/1 and OpenBao voters ready 3. The CNPG watch
extension remains `keycloak,cnpg-system,recovery-4-0-kc`. No cleanup, target adoption,
source store/writer change, backup/capture or recovery continuation was dispatched.
All signed originals, accepted corrections, passed/failed journals and clocks
remain unchanged. Failed quiescence SHA-256
`540dd7ef385d3ed5f43bd0ef5b227adaa1443e8c3f191ffa413814bb87247fd3`
still records drainPass/intakePaused false; it has not been promoted.

The precise remaining drain blocker is the still active unowned
`mcpcli-417-20261101-00c6a713/eventstore-nkkwj` Pod, UID
`d888a907-5fb9-4684-9b52-a6f827582884`, controlled by Job `eventstore`, UID
`4b8f43cb-7875-43a6-a4a8-0c5f007879a6`. The final observation still found both
EventStore and daprd containers running/ready. It is not a retained terminal-Pod
false positive. No source-publisher exclusion, unrelated Job mutation, cleanup or
new continuation is authorized by the intake-only decision. Any later work must
respect the real failed drain and retained target state, without replay or
changing accepted bytes. Intake restoration itself supplies no backup acceptance.

The exact attempt still ends **2026-11-01T23:00:00Z**. Original Keycloak point
cutoff/durability/RPO and native/TLS horizons are not renewed. Complete accepted
recovery, later Administrator-only signatures/readbacks, non-dry validation and
honest M1/M2 remain unfinished. **4.27 stays in-progress, 4.1 closed and Timesheets
3.6 open/FAIL**. No retirement, upgrade, Rancher or deployment ran; frozen intent
and original production tasks remain unchanged.
