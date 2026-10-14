---
title: Retire KubeSphere without changing workload data
type: story
epic: 4
story: 27
created: 2026-10-01
status: in-progress
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
