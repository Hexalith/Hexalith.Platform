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
