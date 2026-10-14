---
title: '4.27: Retire KubeSphere without changing workload data'
type: 'feature'
epic: 4
story: 27
created: '2026-10-05'
status: 'in-progress'
preparation_review_status: 'passed'
baseline_commit: '54920908f15a99b48e69861baf306365353fc6df'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/4-27-retire-kubesphere-without-changing-workload-data.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 4.26 qualified isolated retirement, but no production executor or approved live attempt exists. Console-closure and recovery gates remain open.

**Approach:** Build an explicit-context production executor using qualified guards, bind a fresh plan and exact-byte rehearsals to an Administrator-approved attempt, then retire and verify KubeSphere after preflight. Retain failed attempts; incomplete outcomes keep the story open.

## Boundaries & Constraints

**Always:** Preserve application/data namespaces, PVC/PV UIDs and bindings, shared CNI/storage/ingress/identity, public application/OIDC contracts and independent native access. Close the public console under 4.2 first. Validate fresh signed 4.0 workload proofs, off-node readback, a fresh production external-etcd point and fenced isolated restore under 4.1's contract, plus matching node/configuration recovery. Obtain a separate approval for the exact executor/procedure, plan, allowlist, outage, owner, window and stop/recovery conditions. The signed plan may explicitly bind existing native `coordination.k8s.io/v1` Lease identities and their complete desired content except `spec.renewTime`; allow only valid monotonic renewal on those exact identities while every other desired field remains unchanged. Preserve the qualified exact manager Lease checkpoint after controller removal. Keep raw inventories, logs, credentials and signed originals in restricted encrypted custody; publish only sanitized outcomes.

**Never:** Treat design approval, historic fixtures, hashes or an old backup gate as live authorization. Do not restore into the running cluster, delete a Namespace/PVC/PV or shared component, use wildcard or blanket finalizer edits, invoke licensed KubeSphere writes, install Rancher, or start the Kubernetes hop. Preserve `jpiquot` authority for 4.28.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Approved attempt | Fresh matching recovery, 4.2 closure, signed exact plan and current native census | Execute 19 reviewed phases with UID/resourceVersion/full-content guards and phase preservation checks | Stop and retain a sealed failure receipt on any unexpected result |
| Stale or incomplete gate | Expired proof, changed procedure/driver/module, drifted UID or desired content outside explicitly bound Lease renewal, missing owner or approval | Zero mutation requests | Record the rejected gate without promoting a proposal to an allowlist |
| Bound Lease renewal | Exact signed-plan native Lease identity, valid nonfuture and non-regressing `spec.renewTime`, all other desired content unchanged | Planning/execution handoff and preservation checks accept only that observed renewal; renewals grant no mutation authority | Missing binding, UID/holder/owner/spec/metadata drift, malformed/future/regressing time, and expired approval stop with zero further mutation requests |
| Partial retirement | An observed deletion, finalizer conflict, controller recreation or failed smoke | Preserve source/recovery evidence, stop remaining phases and require incident decision | Never auto-restore an old point or mark retirement accepted |
| Completion | Manager absent and protected workloads, bindings, access and health verified | Signed before/after outcome and fresh post-retirement 4.1 recovery inputs | Any failed or unobserved check leaves story open |

</frozen-after-approval>

## Code Map

## Approved fresh recovery continuation — 2026-10-14

The human replied **"I approve"** to the final handoff for actual prepared RID `20261014t112353z`. This is current approval of the exact 4.0 policy SHA-256 `021a8c773366fc2cc5b36e5407e03fd0e0a4dca363df1da71704725443bdc85a`, destination-precheck SHA-256 `167fb54aa86d52498df18c5f962b050ec976b4844b9a08747a2c4756b904d4a7` and operator handoff SHA-256 `3e721fd9becad7ec6ef601281edec8e278ef52db424325aa21cdfa50756d3cca`. The retained tools manifest is `29228eca2d14a73048caaa6eddf597ecac52cd9620c151bb4e824e6b2a082669`. Approval binds this current policy/RID's described signing/publication and recovery/capture/isolated-restore/cleanup scope to existing October 7–31 dates, this conversation as incident channel and acknowledged sole-Administrator roles. No repeated scope permission is needed. Earlier offline-only paragraphs below describe historical runs. The approval grants no retirement, 4.1 node/etcd mutation, upgrade, Rancher, Timesheets deployment/activation, Git staging/commit or automatic whole-story acceptance.

Implement only these current continuation tasks:

- [x] Record the human approval in new immutable owner-only private custody and a sanitized repository receipt, binding exact policy/precheck/handoff/tool hashes and reused dates/roles. Preserve all previous packets/receipts and policy bytes. Record actual current VCS identity separately; keep the existing baseline and frozen intent unchanged.
- [x] Recheck current exact policy/precheck bytes and binding, tools/trust/key public fingerprint, safe static native `jpiquot@local` configuration and cluster identity, policy age and current maintenance window before any dependent operation. Inspect signing access without secret disclosure. The root found no usable SSH authentication agent; protected-key unlock remains unsupplied. Attempt the exact approved `sign.sh policy/recovery-policy.json` only with available safe signing access; use a bounded noninteractive attempt that cannot collect a passphrase in chat/logs or hang. Retain the true result and seal a refusal if signing requires unavailable unlock. Do not guess/decrypt/brute-force key material or fabricate a signature. If refusal occurs, no capture/quiescence/restore may run; give the Administrator the exact private-terminal action and preserved approval reference needed to continue, without asking for scope approval again.
- [ ] If signing/publication actually succeeds, independently verify exact original signature and immutable provider upload/readback before capture, then perform only the approved retained 4.0 sequence on the named source: watch-on, Keycloak, OpenBao, the corrected split Memories pre/census/pause/drain/capture/manifest/isolated-target/restore/verify/isolation/identity sequence, conditional approved resume, isolated cleanup, records/proofs, gate and non-dry final validation. Keep the same commands/approved bytes, at-run source/tenant/custody guards, strict stops, encrypted restricted diagnostics, signature ordering and M1/M2 requirements. Never overwrite signed input or run the composite policy stanza on the existing prepared policy. Any failed/unknown step stops; paused intake stays under incident-owner decision. Missing credential/custody/manual acceptance input must be named precisely rather than marked passed.
- [x] Append a truthful new approval/signing outcome checkpoint to the handoff, 4.27 story and this spec. Acceptance remains unproven until fresh signed recovery/restore/cleanup/non-dry validation/readback succeeds. Preserve old preparation source archives even though appended current tracking naturally has new bytes. Keep 4.27 in-progress, 4.1 closed and Timesheets 3.6 open/FAIL; leave original production tasks pending. Verify unchanged bound execution/procedure/maintenance and local receipt integrity; no unchanged fixture rerun or implementation-mirroring tests is needed for this operational checkpoint.

Current dependent-run state: protected-key signing refused with exit 255 and no signature/uploads ledger. The conditional third task remains pending; no capture/quiescence/restore ran. This refusal satisfies the unavailable-unlock acceptance path while preserving current scope approval.

### Policy signed by the Administrator — continuation after "done"

The human subsequently replied **"done"** after the private-terminal signing action. At `2026-10-14T12:45Z`, the root independently found the exact approved policy/precheck unchanged, verified the original policy signature as `jpiquot@itaneo.com` under `hexalith-recovery`, and found two matching policy/signature upload entries. Original signature SHA-256: `7f5dc8431113f6f5a993ed7cefda5a427673a3334fff2a380d8a1a3eab715f0e`. The earlier refusal/receipt remains historical. The conditional third task above is now the active work: independently re-read the exact immutable provider versions with the validator credential, revalidate current source/trust/tool/policy-age/window/isolation gates, and proceed through the authorized retained 4.0 sequence until a real failure or missing input. Do not rerun completed policy preparation or allocate another recovery RID. A signature and locally recorded readback alone are not acceptance.

Root also inspected current-user-owned, owner-only standard `/tmp/ssh-*/agent.*` sockets and the current agent environment; none is usable. Later cleanup/manifest/proofs/gate/validation signatures may therefore need the Administrator's same private-terminal action. This is missing credential access, not another scope-approval requirement. Do all authorized recovery/preparation independent of that future signature and stop only at the actual signed-record prerequisite; never pretend to have signing access or collect passphrases in chat/logs. If a signing credential path or owner-only agent socket is supplied later, verify its approved key identity/custody before using it. Keep the approved tool bytes unchanged. Retain raw capture/restore diagnostics encrypted in restricted custody and publish only sanitized outcomes.

Create a new immutable operator continuation receipt binding original policy/signature plus fresh provider readback and actual sequential command/results. Preserve prior refusal and preparation receipts. After successful Memories restore and clean drain recheck, use the existing approved resume immediately so the intake is not left paused while waiting for later signatures. On failed/uncertain recovery, stop and leave any paused state to the recorded incident owner; provide the exact observed state, failure and approved recovery/resume decision needed. At a normal later signing prerequisite, prepare the actual required unsigned record(s), give their exact guarded signing action and preserve approval; no repeat policy or scope question. Even full successful 4.0 recovery does not authorize 4.1/node/etcd mutation or retirement and cannot close Timesheets 3.6.

Continuation acceptance: Given the current approval, when exact-byte prerequisites pass and protected signing access is available, then only the described 4.0 scope executes with authentic signatures/readback and truthful outcomes. Given unavailable signing unlock or any failed dependent gate, when the operator continuation runs, then it records the exact failure, issues zero capture/quiescence/restore requests, preserves historical evidence, retains scope approval and supplies the concrete credential-unlock/signing action. Scope approval and a successful signer alone never prove fresh recovery or deployed acceptance.

### Failed OIDC reference — authorized investigation and separate incident preparation

The approved sequence stopped at `kc_oidc.py`: exit 0, but `allPass=false` and `tokenRoleClaimsEqualLive=false`, with a null live claims digest. The public master-realm endpoint is closed under accepted Story 4.2. Preserve the failed artifact SHA-256 `014463ef76fd99431f6c268edbc41274b5031157f683a1630011956cb5738578` and all original approved tools/signatures. Source workloads and protected storage identities remain unchanged; only `recovery-4-0-kc` and the temporary CNPG watch extension remain. No OpenBao/Memories operation followed, and Memories intake was never paused. The removed `.kcverifier` scratch file must not be recreated by automatic bootstrap or a credential mutation.

Authorized next work is read-only investigation and offline preparation, not operational continuation past the failed gate:

- Seal the stopped-run receipt and exact current isolated target/controller identities in restricted custody. Retain the historical failed proof and command diagnostics.
- Independently diagnose the same-client live/restored claims via the accepted loopback-only private native Keycloak path, with explicit regular static kubeconfig/context and source/target identity checks. Keep client secrets, tokens and any existing isolated verifier credential only in memory. A read-only SQL lookup of the existing isolated verifier secret is permitted; do not create/reset any credential, alter a realm, reopen a public route, or use executable authentication hooks. Publish only statuses, digest comparisons and sanitized outcomes. Close every temporary port-forward and fail on redirects, unexpected listeners, malformed output or unknown acceptance fields.
- Prepare a separate exact-byte read-only OIDC helper outside the 37-file approved tool bundle. Preserve all eight original acceptance checks and the unchanged claims-shape algorithm; select only the private source endpoint, read the existing isolated verifier credential without mutation, enforce identity/custody/failed-input bindings, and return nonzero for any failed/unknown check. Diagnostic output must be a distinct create-only record, never overwrite or promote the failed original. An investigative all-pass result is not recovery acceptance or incident authorization. Verify meaningful negative cases for mismatch, unavailable live reference, changed input and nonzero failure; keep the implementation small.
- Prepare the exact next incident-owner action, binding original approval, failed evidence, corrected helper/output hashes and the actual retained target. Explain whether accepting this correction and proceeding on that exact target is possible, versus an explicit abort/cleanup decision. Include the minimal offline proof-consumer variant needed to read the distinct corrected OIDC record: the original `proofs.py` currently reads the failed canonical filename. Preserve its acceptance logic and every approved original byte; bind the unchanged failed original, corrected helper/output and explicit incident decision before the variant can consume a replacement reference. Do not silently overwrite or rename the original failure or claim that a diagnostic has fixed the existing consumer. No retry of the operational sequence, original-record replacement, cleanup, watcher change or later-system mutation may run until the Administrator supplies the concrete incident decision for reviewed changed bytes. Reuse the existing policy/window/roles; do not ask for the original scope again. Append a distinct truthful checkpoint after offline preparation, keeping 4.27 in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.

### Exact OIDC incident acceptance — current operational continuation

The human replied **"Accept the OIDC correction and continue 20261014t112353z."** This accepts the reviewed `INCIDENT-ACTION.md` SHA-256 `652894ca35acad46b24fbb4579e57565e5bf35c4df4975ff9c6920126318dd2f`, final pending decision SHA-256 `820ec36a92aa279dc41d14bb7009852cd197e73efbf625aa1b6daaa6634a4936`, and all named helper/diagnostic/target bindings. The root recorded a new create-only actual decision at `/home/administrator/hexalith-retirement-investigation-evidence/20261014t131927z-s427-private-oidc-correction/incident-decision.json`, SHA-256 `28b45cc0a30de866ad6fc7946256c966897cef3a21265e645404111eab5906c5`, at `2026-10-14T14:36:09.940957+00:00`. The pending drafts, sealed preparation, failed original and 37 original tools remain intact. Earlier preparation-only prohibitions above describe historical state; this exact incident acceptance now permits the reviewed correction and continuation on the retained target within the existing 4.0 approval/window/roles. It grants no new retirement, 4.1 mutation, upgrade, Rancher or Timesheets deployment/acceptance authority.

The root independently executed the reviewed **full native read-only incident guard** with the actual decision/digest. It passed exact source/retained-target/protected namespace/PVC/PV bindings, current policy signature/age/window, absent other restore targets and the extended watcher. Recorded policy age was 10,589.784 seconds, within 86,400. This supplied zero operational mutation requests and no recovery acceptance. Recheck the full guard in a new restricted run receipt before the first resumed command; use its approval-only mode for later decision checks after authorized new targets exist or cleanup removes them. Never edit any reviewed correction byte or original tool after this acceptance.

The human subsequently answered the actual C3 operational-input question **"Freeze announced for this run"**, confirming the question's stated freeze of direct Memories operator/automation API calls and no operator-token issuance until the recorded resume. Root retained create-only `/home/administrator/hexalith-retirement-investigation-evidence/20261014t144239z-s427-accepted-oidc-continuation/memories-c3-freeze-announcement.json`, SHA-256 `a18e0b84b0af5642ac1028be38e84f36503ccfe3e1f03cbe5545a61081a73629`, with the actual answer/question provenance. The C3 input is now supplied; recording it performed no intake pause. Before the later Memories phase, verify these exact owner-only bytes and the preceding recovery checks, then use the accepted one-final-pre/census/pause/drain sequence. Earlier pending-C3 paragraphs describe state before this response. Keep the freeze active through any failed/uncertain step; after approved successful resume, record and announce its end truthfully.

Current implementation tasks:

- Record and preserve this exact acceptance and fresh preflight in a unique owner-only continuation directory, with a sanitized repository receipt. Retain every earlier packet/source/archive/input digest. Preserve frozen intent, original production tasks, legacy baseline and bound retirement/maintenance/execution bytes; record actual current VCS identity separately. Keep all raw command/result diagnostics encrypted to the pinned Administrator recipient and credentials/tokens only in memory or established protected custody.
- Follow the accepted `INCIDENT-ACTION.md` argv/order sequentially through the missing Keycloak isolation/target-identity checks, original OpenBao capture/transfer/isolated-target/restore/verify/isolation/identity, then the corrected split Memories sequence and immediate conditional resume, isolated cleanup and later signed-record prerequisites. Create a new local encrypted sequential command wrapper/receipt, preserving the original sealed failed-run wrapper. Reverify exact approved input/custody/policy-age/window/decision bindings before dependent operations; explicitly unset `PYTHONOPTIMIZE`. Require actual semantic results, not process exit zero, including isolation/all-checks, transfer hashes/RPO, restore HTTP/raft checks, quiescence/restart/census, restore/identity/resume/reconciliation and cleanup absence/retention fields. Do not retry a failed/unknown step or dispatch any further dependent operation after it. Collect only read-only state and prepare a concrete incident action if a new failure occurs.
- The accepted action requires an **actual Administrator C3 freeze announcement** before any Memories intake pause. The root has asked for this missing operational input asynchronously while independent Keycloak/OpenBao work proceeds. Do not fabricate that announcement from scope approval or the incident-acceptance reply. If it remains absent, stop before Memories pause, keep intake unchanged, and give the precise announcement text/owner/channel needed; no repeat scope approval is required. Prefer waiting to write the one final Memories pre/census until the freeze announcement is actually available, so approved inputs are not overwritten. Once the owner supplies it, retain its actual message/provenance in new create-only custody and continue within this same approval. Never issue a Memories operator token/API session during the freeze. After successful restore/isolation and a clean drain recheck, use the existing approved resume immediately; do not leave intake paused for signatures. Failed/uncertain operations keep paused state under incident-owner decision.
- Continue until a real failure or missing signed/manual input. Signing unlock is still unavailable unless a verified approved-key agent/socket is explicitly supplied. At the actual cleanup/manifest/proof/gate/validation signature prerequisite, finish all authorized independent preparation, verify the actual unsigned record and give the guarded exact private-terminal signing command. Do not request scope approval again or modify signed records. A bounded safe noninteractive signing attempt is allowed only when warranted by newly available custody; do not repeat the already-known protected-key refusal just to rediscover it. Independently verify exact original signatures and immutable validator readback before each dependent stage. After signed cleanup, use the separate approved `proofs_private_oidc.py` and its exact incident-decision guard instead of the original failed OIDC reference; retain signed correction provenance. Required non-dry validation, honest M1/M2 and all eight required signed records remain acceptance conditions.
- Append a distinct truthful outcome checkpoint to the handoffs/story/spec and seal this new continuation evidence. Report actual completed authorized commands, zero **further** dispatches after any failure, current preserved source/target/intake state and the precise remaining owner input. Keep 4.27 in-progress, 4.1 closed and Timesheets 3.6 open/FAIL. Reuse existing decisions; no original recovery or incident correction approval question remains pending.

## Active operational-prerequisite investigation — 2026-10-14

The latest human instruction resumes this existing spec to advance the first operational prerequisite blocking a **fresh source-bound retirement plan**. The earlier offline continuation below is historical scope/evidence; its run-specific prohibitions do not replace this instruction. Preserve its completed packet and receipt. Current authorization covers read-only investigation of the already named source and local private evidence plus reversible offline preparation. Production mutation, capture/quiescence/restore, signing or publication requires an established approval whose exact scope actually covers the proposed action. Reuse recorded Administrator decisions within their scope; never broaden October 1's run-specific signing delegation, Story 4.2's waiver, the maintenance dates or fixture qualification into current retirement authority. No Git staging/commit, target discovery or change to executor, bound procedure, historical maintenance, frozen intent, default-deny providers or story acceptance is needed.

Implement only these new continuation tasks; the original operational tasks remain pending:

- [x] Investigate the latest checkpoint/receipt and the three existing evidence roots for current 4.0 signatures/validation/readback, 4.1 external-etcd/node recovery, accepted console closure, native access and approved signing/custody inputs. Read Story 4.0's final-run tools, existing private staged tool sources and October 7/8 Story 4.2 decisions. Distinguish actual accepted closure from absence of the executor's exact `console-closure.json`. Inspect credentials/configuration metadata without printing secret content. Read-only native GETs on the known `jpiquot@local` target and read-only historical provider validation may run if their exact transport/configuration can be safely established; refuse executable authentication hooks or unapproved signing. Record precise inspected scope and any transport failure. Do not invent current acceptance from old results.
- [x] Add `eng/cluster-management/NEXT-OPERATOR-ACTION.md` with the ordered gate assessment, the first unmet prerequisite, exact actionable local/conditional operator commands verified against retained tool sources, required non-secret input paths/identities, accountable owner, secure way to supply missing inputs and measurable acceptance evidence. Include existing reusable decisions and their limits. Prefer the concrete first prerequisite action over restating the entire retirement sequence; keep later commands linked to unchanged `STAGING-PREREQUISITES.md`/`RETIRE-KUBESPHERE.md`. Require fresh 4.0 recovery rather than extending historical timestamps. If exact current recovery/capture/signing scope or private unlock input is missing, prepare the command packet but do not dispatch the dependent operational steps.
- [x] Append links and truthful new checkpoint evidence to `eng/cluster-management/STAGING-PREREQUISITES.md`, `eng/timesheets-deployment/staging-dependency-handoff.md`, this spec and the existing 4.27 story. Preserve historical content and the old receipt. Verify documented Bash syntax, unchanged bound files and the relevant existing preparation tests; no new implementation-mirroring tests are needed for documentation. Run the existing offline preparer once with a new unused external packet ID after final documentation bytes settle. Keep a separate sanitized unsigned new verification receipt outside the consumed source set; inspect checksums/modes/archive and closed fields, and clearly separate any read-only observations from the offline preparer's zero-remote behavior. Keep 4.27 and Timesheets 3.6 in-progress/FAIL and 4.1 closed. Do not rerun unchanged fixtures or claim whole-story acceptance.

Acceptance for this continuation: Given the current evidence and reused decisions, when the handoff is read, then it identifies the first unmet prerequisite and supplies commands that match retained tool interfaces with exact input owners and acceptance requirements. Given missing exact operational authorization or unlock/custody input, when preparation completes, then no dependent operational mutation/signing occurs and the blocker is explicit. Given local packet publication, then historical evidence and bound execution bytes remain unchanged, custody/checksums pass and every operational gate stays closed.

Read-only census clarification: current authorization also covers necessary bounded pod execs of inspected read-only Redis INFO/SCAN/count queries to obtain a fresh Memories census. Keep authentication inside the pod, raw observations in private custody, and publish only sanitized counts/digests. An executable transport hook, unresolved credential authority, mutating domain action or inconsistent census stops that dependent investigation; it does not permit quiescence/capture/restore or signing. Record the actual census interface/lineage or the precise reason a fresh census cannot be fetched.

Offline preparation clarification: after verifying the retained staged manifest and inspecting the `new` branch, run `final-run-order.sh new` once to allocate/copy/checksum the new owner-only 4.0 tools bundle. This local allocation is already within the human's reversible offline scope and needs no further permission. Bind its exact RID/tools path and measured checksums in the handoff and receipt, with policy/capture/signing still pending and unapproved. The `policy` branch uploads to the provider and must not run without its established exact current scope. An empty unsigned tools bundle supplies no fresh recovery proof or approval.

Concrete approval preparation clarification: in the allocated fresh 4.0 bundle, run the inspected `precheck.py` with only validator reads/local output and then `policy.py` for an unsigned local policy. Do not invoke the uploading composite `final-run-order.sh policy`, upload helpers, signing or capture/restore/quiescence. Bind the resulting exact private policy/precheck bytes to the existing October 7–31 dates, conversation incident channel and acknowledged Administrator roles; dates/roles need no new selection. Preserve the already sealed first investigation packet/receipt/supplement and use one distinct final packet ID after corrected documentation settles, with each ID invoked once.

## Authorized continuation — 2026-10-14

This run resumes **reversible local preparation only** for Timesheets staging. The current human instruction excludes retirement, upgrades, deployment and domain mutations. Complete the preparation tasks below; the original production execution/acceptance tasks remain pending and are outside this run's authorization. Preserve the frozen intent, current executor/shared-module/driver/procedure bytes, historical maintenance, receipts, retained `jpiquot` authority, Timesheets default-deny providers and Story 3.6 `in-progress`/FAIL. Do not sign, publish externally, stage, commit, run remote commands or discover another target. Existing target: `jpiquot@local`.

Initial investigation: the requested `eng/timesheets-deployment/staging-dependency-handoff.md` was absent here; concurrent workspace activity subsequently supplied its preserved local copy. Read the existing source at `/home/administrator/projects/hexalith/timesheets/references/Hexalith.Platform/eng/timesheets-deployment/staging-dependency-handoff.md`, retain its exact bytes/digest as external handoff input and document this provenance. It reports dated native reads at 2026-10-14T06:43:46Z (server 1.34.9, absent staging namespaces/Gateway API); this run performs no live observation. Its extended window ending 2026-10-31T23:00:00Z grants no approval.

Local `retire.code_binding` still matches the 20261006t091552z audit. The real `retire.validate_rehearsals` accepts private `/home/administrator/hexalith-management-evidence/rehearsal/20261006t074119z-s427-catalog` and `.../20261006t074119z-s427-namespaces` against the exact `_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261004t085143z-census/inventory.json`. Verify every file in both private manifests and retained two-recipient readback records; this is checksum/retained-receipt verification, not fresh decryption, off-node acceptance or current source qualification. Existing receipts bind the historical source inventory; a new census must be compared and, as the existing runbook requires, source-bound fixtures renewed before the final plan. Do not rerun fixtures in this local-only preparation run.

Inspected roots: `/home/administrator/hexalith-{management,recovery,upgrade}-evidence`. Sole current local 4.0 gate is historical `4-0/20261001t061620z/gate/backup-gate.json`, expired 2026-10-02T06:16:52Z. Its original signed validation/proofs and 4.1 `20261001t075120z-backup-verified` point/restore/node records remain historical. New 4.1 `20261014t065220z-preparation` and `20261014t070550z-preparation` are unsigned not-run placeholders. No exact-named retirement-policy/console-closure/retirement-approval record was found in these roots. Signature presence is not signature validation or approval. Private trust root/principal, native static config, Administrator recipient, readback identity, signing scope and current approved freshness policy remain unselected; never infer them from synthetic keys or old recovery trust files.

Preparation tasks for this run:

- [x] `eng/cluster-management/prepare_retirement.py` — provide an offline-only, stdlib local packet preparer. No subprocess/network/signing/retirement dispatch. Read explicit local handoff, historical inventory, exact catalog/namespace manifests and recovery root metadata; reuse only pure existing code/receipt validators, retain complete immutable preparation source bytes and input lineage. Create a unique external owner-only packet (directories 0700, files 0600, checked SHA256SUMS), refusing repository/recovery/input destinations, existing IDs and symlink custody. Keep every operational field unobserved/null/not-run with `signed=false`, `mutationAuthorized=false`, live gate closed. No `plan.json` or pass-labelled operational records may be fabricated. Preserve sources and fail before publication on malformed required input. Metadata audit must distinguish untrusted record claims from measured local integrity and report exact inspected scope.
- [x] `eng/cluster-management/test_prepare_retirement.py` — meaningful tests for no command dispatch, refusal/preservation of unsafe or existing custody, source/archive/checksum lineage and inability of historical/passing/signed-placeholder metadata to open any gate.
- [x] `eng/cluster-management/STAGING-PREREQUISITES.md` — give executable local preparation command and concrete conditional operator command proposals for first native census, sequential namespace/catalog/fresh-node qualification, second fresh receipt-bound plan and original signature verification, using known retained tool paths where verified. Keep unresolved private parameters explicit and fail-closed; commands that sign or contact production require the separately approved scope and are not run here. Name all BACKUP_FILES/GATE_FILES, namespaces/principals, exact evidence fields, two distinct planning IDs and future execution ID, digest bindings, independent access/health/4.2 closure/recovery and exact remaining blockers. Refer to the unchanged bound retirement handoff for future armed execution. Explain that every bound-byte change forces renewed fixtures, exact approval and final plan; historical receipts/window do not establish current approval. Carry all later Timesheets owner/release/infrastructure/provider/source/deployed-acceptance gates unchanged.
- [x] `eng/timesheets-deployment/staging-dependency-handoff.md` — create a local continuation pointer (absent before this run) with external input provenance, packet/command/evidence links, closed gates and Story 3.6 FAIL; do not copy other checkout history as observations by this run.
- [x] Existing 4.27 story/spec — record new local packet evidence, verification and exact unresolved operator inputs without marking original production tasks or 4.27/4.1/Timesheets acceptance complete. Do not edit the pre-existing unrelated dirty files. Execute the preparer once with final bytes and inspect its private modes, all checksums and retained source archive; publish only a deliberately sanitized local unsigned receipt if useful. Verify the relevant test suite and unchanged bound execution/procedure/maintenance bytes.

Preparation acceptance: Given absent current approval/proofs, when the local preparer runs, then it emits only a checksummed unsigned closed-gate packet and concrete conditional commands, with zero remote calls/signing/mutation and truthful historical evidence limits. Given changed or unsafe input/custody, when preparation is attempted, then it refuses without overwriting prior evidence. Given completed local preparation, when the handoff is evaluated, then 4.27 remains in-progress, 4.1 closed and Timesheets 3.6 open/FAIL with exact missing operator evidence.

## Original execution code map

- `eng/cluster-management/rehearse.py` — pure `dependency_plan`, `validate_allowlist`, content/transition/phase guards and `Fixture.retire_phase`; reuse guards, separate fixture transport from production transport. Keep source credentials out of kind.
- `eng/cluster-management/rehearse_catalog.py`, `rehearse_namespaces.py` — exact catalog, seven-namespace and fresh-node fixtures; rerun after executor/shared-code changes.
- `eng/cluster-management/qualify.py` — explicit native census and encrypted raw exports; refresh before planning. `evidence.py` owns immutable two-recipient attempts but currently publishes only to 4.26.
- `eng/kubernetes-upgrade/README.md`, `prepare.py` — 4.1 recovery record contracts; `prepare.py` inspects metadata only, so it cannot validate signatures or approve recovery.
- `eng/cluster-management/RETIRE-KUBESPHERE.md`, `README.md` — retirement procedure, retained objects and operator handoff; preserve hashed `eng/kubernetes-upgrade/MAINTENANCE.md`.
- `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md` — 4.2 is in progress; closure requires independent external denial evidence.

## Tasks & Acceptance

**Execution:**
- [x] `eng/cluster-management/rehearse.py` — extract transport-independent phase execution and preserve all exact deletion, transition, namespace-finalizer and full-content guards for fixture and production use.
- [x] `eng/cluster-management/retire.py` — add explicit native context/tool/credential inputs, fresh census and plan construction, independent preflight evidence validation, exact signed approval binding, default non-mutating mode and an explicitly armed production path; record every request/phase and stop at the first refusal.
- [x] `eng/cluster-management/evidence.py` — support immutable 4.27 private attempts and sanitized publication without changing 4.26 receipts; encrypt/read back raw before/after inventories, commands and diagnostics.
- [x] `eng/cluster-management/test_retire.py` and existing fixture tests — exercise stale gate/approval, drift, conflict, unexpected deletion, retained authority, protected namespace/storage and failed health paths; verify zero requests before a complete gate and fail-closed mid-phase behavior.
- [x] `eng/cluster-management/retire.py`, `test_retire.py` — resolve the approved Lease handoff limit through exact signed-plan identity/content bindings and monotonic-renewal guards; preserve strict comparison for unbound objects and the shared post-controller manager Lease transition, verify renewed execution baselines during assessment, and test both accepted renewal and every refusal boundary.
- [ ] `eng/cluster-management/RETIRE-KUBESPHERE.md`, `README.md` — document exact command inputs, plan/approval handoff, per-phase stop/recovery and post-retirement checks. Rerun catalog, seven-namespace and fresh-node fixtures with final executor/shared-module bytes; bind receipts to the fresh production plan. **Local documentation and exact-byte fixtures are complete; the fresh production plan binding is pending.**
- [ ] `_bmad-output/implementation-artifacts/4-27-retire-kubesphere-without-changing-workload-data.md`, `sprint-status.yaml` — record current gate and attempt evidence; mark done only after signed production preservation and retirement acceptance. Hand fresh post-retirement recovery inputs to 4.1. **The local checkpoint is recorded; live retirement/acceptance and the recovery handoff remain pending.**

**Acceptance Criteria:**
- Given an approved plan bound to current identities and exact rehearsed bytes, when the production executor runs, then every requested deletion is within the approved phase allowlist and each phase proves protected identities/bindings unchanged before the next request.
- Given completed phases, when retirement is assessed, then the old console and manager runtime/admission references are absent, public console access is denied externally, native administration and authenticated Keycloak/OpenBao/Memories/Forgejo outcomes pass, and the signed result names retained archive objects and incidents.
- Given any missing prerequisite or failed/unobserved outcome, when the handoff is evaluated, then 4.27 remains incomplete and 4.1's upgrade gate stays closed.

## Implementation Notes

- 2026-10-14 offline preparation: [local operator prerequisites](../../eng/cluster-management/STAGING-PREREQUISITES.md) provide an executable stdlib preparation command and conditional exact first-census, sequential seven-namespace/catalog/fresh-node, second-plan and original-signature-verification proposals. All preparation task deliverables are implemented; original production tasks below remain pending. The [separate unsigned local audit](evidence/epic-4/4-27/20261014t100941z-s427-reviewed-preparation/verification.json) records final packet inspection/test results without being a consumed source. Packet ID `20261014t100941z-s427-reviewed-preparation` has external owner-only custody under `/home/administrator/hexalith-retirement-preparation-evidence/`, complete immutable preparation source bytes and historical manifest/export lineage. Signed recovery originals/signatures stay in existing private custody; only their measured path/digest/signature-presence lineage and explicitly untrusted metadata summaries are retained in the new packet. The exact external handoff SHA-256 is `b12758d8a528a876314e7d49b02a864530fc1a7852f40818701ee0f8f789ef38`. Concurrent repository activity supplied the existing local Timesheets handoff, so its history was preserved and only a continuation pointer appended. No new live observation, fixture rerun, decryption, off-node acceptance, plan, signing, retirement, upgrade, deployment, domain mutation, external publication, Git staging or commit is supplied. Unselected private trust/principal/static-config/recipient/readback/signing/freshness inputs and fresh accepted 4.0/4.1/4.2/native-access/health/first-census/source-bound-fixture/final-plan/exact-approval evidence remain concrete operator blockers. 4.27 stays `in-progress`, 4.1 closed and Timesheets 3.6 open/FAIL; retain `jpiquot` and all default-deny and later owner gates.

- Resume checkpoint: [20261006t091552z-s427-resume-verification](evidence/epic-4/4-27/20261006t091552z-s427-resume-verification/verification.json) reran all 253 cluster-management tests with no skips and audited every acceptance-matrix covering test. The actual `retire.validate_rehearsals` again accepted the unchanged final-byte catalog and namespace receipts; all 905 catalog and 98 namespace private-manifest files matched their checksums, including 846 and 85 encrypted exports and their retained two-recipient readback receipts. No fixture or decryption was repeated in this audit because execution bytes remain unchanged. Frozen intent, baseline `MAINTENANCE.md`, historical removal scope and retained authority passed comparison. Local metadata still shows an expired 4.0 gate and no exact-named current retirement-policy or console-closure record in the inspected retained roots. The unsigned local receipt supplies no production observation, plan, approval or acceptance; 4.27 remains `in-progress` and 4.1 stays closed.
- `PhaseExecutor` shares the existing reviewed guards between kind and the explicit native production transport. Production dispatch independently checks action kind, approved phase root, UID/resourceVersion and exact named-finalizer body; it stops at the first refusal. Protected and retained state, source/tool/credential/trust/code/evidence identities and gate expiry are checked before subsequent requests.
- Immutable planning custody names a different future execution attempt. Original signatures and exact plan/receipt/procedure/allowlist/outage/owner/window bindings precede arming. A separate read-only assessment requires new signed postflight health/external-denial/recovery inputs and exact Administrator acceptance; successful deletion alone keeps the story open and the 4.1 gate closed.
- Continuation verification: all **253** cluster-management tests passed with no skips, including all 73 retirement tests and the two new original-exception/custody-refusal checks. Verbose output is retained at `/tmp/story-4-27-final-suite.log`; [current-byte local audit](evidence/epic-4/4-27/20261006t085341z-s427-verification/verification.json) records its checksum and every matrix row's actual passing test. A fixture timeout now retains its original exception, stack, exact command and partial output only in encrypted custody. Diagnostic custody refusal still seals a failed receipt and performs exact owned-resource cleanup; no command retry or phase-guard relaxation was added.
- Current-byte seven-namespace qualification [20261006t074119z-s427-namespaces](evidence/epic-4/4-27/20261006t074119z-s427-namespaces/qualification.json) passed with seven exact UID/resourceVersion-bound PUTs, zero Namespace DELETEs, protected identity/storage/canary preservation, 85 verified encrypted exports and successful cleanup. Sequential full catalog/fresh-node qualification [20261006t074119z-s427-catalog](evidence/epic-4/4-27/20261006t074119z-s427-catalog/qualification.json) passed the 118-object catalog check and 90 controller-cleared cleanup finalizers, all 19 retirement phases with 471 exact baseline removals and 46 retained CRDs, and fresh-node external-etcd restoration. The source node was destroyed before restore; fresh node/member identities, every baseline/CRD UID and canary readback passed. Restored KubeSphere runtime checks passed. All 846 encrypted exports were read back for both recipients, and source, restored-node and aggregate cleanup passed. These are synthetic, isolated fixture outcomes, with no independent off-node custody or production acceptance.
- The real `retire.validate_rehearsals` accepted both complete authoritative private manifests against the current executor, shared module, driver, imported-module, procedure and source-inventory bytes. [current-byte local audit](evidence/epic-4/4-27/20261006t085341z-s427-verification/verification.json) also verifies every private checksum, unchanged frozen intent and unchanged baseline `MAINTENANCE.md`. The pure historical source planner found 2,596 resources, 19 phases, 482 proposed deletions and seven namespace interventions, with all five retained authority objects outside the removal scope. This uses the historical 4.26 census and creates no fresh production plan. The earlier unmocked synthetic native census `20261005t144246z-s427-native-census` and actual Lease observation below retain their original byte identities and prior meaning.
- Superseded attempts stay immutable: [20261005t142929z-s427-catalog](evidence/epic-4/4-27/20261005t142929z-s427-catalog/qualification.json) stopped after 18 phases and originally failed cleanup; separate [conservative cleanup recovery](evidence/epic-4/4-27/20261005t155006z-s427-fixture-cleanup/cleanup-recovery.json) and [read-only cleanup verification](evidence/epic-4/4-27/20261005t155136z-s427-cleanup-verification/cleanup-recovery.json) retained the exact cleanup and proved owned-resource absence without altering that failure. [20261005t155319z-s427-catalog](evidence/epic-4/4-27/20261005t155319z-s427-catalog/qualification.json) passed all 19 retirement phases but stopped on the third fresh-node CRI sandbox stop; cleanup passed. The [retained-image timeout probe](evidence/epic-4/4-27/20261005t165533z-s427-cri-timeout-probe/observation.json) established the two-second CRI default; only exact fresh-node sandbox stops now use an explicit 30-second CRI bound within the unchanged 120-second subprocess bound. [20261005t170204z-s427-catalog](evidence/epic-4/4-27/20261005t170204z-s427-catalog/qualification.json) failed closed on a command/custody error after 15 phase receipts, with 614 verified encrypted exports and successful cleanup. Its original exception was not retained, so the root cause remains unestablished. Its [matching namespace attempt](evidence/epic-4/4-27/20261005t170204z-s427-namespaces/qualification.json) passed. All superseded full-fixture current-match flags are false; no failed attempt is relabelled as successful.

- Remaining operational work: fresh native production plan bound to final-byte receipts, accepted 4.2 closure, fresh signed production 4.0/4.1 recovery/readback and matching node/configuration, separate exact execution approval, actual production retirement/preservation and signed postflight acceptance/4.1 recovery handoff. No production attempt was armed or run; story and sprint remain `in-progress`.

- **Measured operability gate:** the historical census includes 13 Leases. The retained [synthetic native observation](evidence/epic-4/4-27/20261005t105545z-s427-lease-handoff/qualification.json) requested a 12-second pause and measured a 15.205-second response interval: the Node-Lease UID and every other desired-content field stayed unchanged while `spec.renewTime` and its full desired-content digest changed. Both actual command/results remain in four encrypted exports with verified two-recipient readback. This immutable observation records the pre-continuation strict-handoff limit. The approved continuation now permits only explicit signed-plan native Lease renewal with every other desired field fixed, validated monotonic/nonfuture time and the unchanged qualified manager checkpoint. The renewed [actual synthetic Lease pair](evidence/epic-4/4-27/20261005t145105z-s427-lease-handoff/qualification.json) requested 12 seconds and measured a 15.151-second response interval. The real new binding/content validator accepted monotonic renewal on the same UID with every other desired field unchanged, no mutation authority, zero requests and no production plan; five encrypted/read-back exports retain both commands/results and the binding model. Its preceding generic-list-wrapper harness refusal remains a separate immutable failed outcome. This closes the measured local handoff limit under the approved binding contract; all production prerequisites and exact-attempt approval remain required.

- Local prerequisite metadata inspection found the retained 4.0 gate `20261001t061620z` expired at `2026-10-02T06:16:52Z`, and 4.1 records remain historical `20261001t075120z-backup-verified`. No current console-closure record, retirement policy or exact execution approval was found in the inspected retained roots. This read-only local discovery checked metadata/signature-file existence, performed no cryptographic signature validation and made zero remote operations; these historical records supply no current execution gate.

## Spec Change Log

- 2026-10-14: Completed only the authorized reversible offline preparation continuation, with immutable source archive/checksum lineage, zero command dispatch and fail-closed unsafe/historical input tests. Kept the frozen intent and original production execution/acceptance tasks unchanged and pending. Packet/test digests and inspection outcomes live in the separate unsigned local audit; concurrent unrelated repository changes are preserved.

- 2026-10-06: Revalidated unchanged software and complete current-byte private fixture manifests in the [resume checkpoint](evidence/epic-4/4-27/20261006t091552z-s427-resume-verification/verification.json). Refreshed local verification and gate metadata without changing frozen intent or executor/procedure bytes. Production prerequisites and acceptance remain pending.

- 2026-10-06: Continued implementation without changing frozen intent. Preserved original encrypted fixture failure diagnostics, renewed exact-byte namespace/catalog/fresh-node qualification, and completed the current local audit. Fresh production planning, prerequisite acceptance and exact execution approval remain pending.

- 2026-10-05: User replied "I approvce" after the documented Lease-renewal operability gate and pending 4.27 work. Continue implementation with the narrow signed-plan Lease-renewal exception above. Fresh production prerequisites and exact-attempt approval remain required. Existing qualification receipts retain their original byte identities; renew final-byte fixtures after this change.

## Review Triage Log

### 2026-10-14 local preparation review

All retained patch findings below are resolved. The parent inspected the corrections and reran the full suite: 284 tests passed with no skips, including 31 preparation tests and all frozen matrix covering tests. The original production tasks and acceptance remain pending. This review covers the current authorized preparation diff; unrelated historical changes since the preserved legacy baseline are outside this continuation.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| Blind 1: manifest ciphertext absent from both receipt lists | medium | The final set guard compares only the two receipt lists; a manifest-covered ciphertext can bypass stanza/readback checks. Patch: include the manifest ciphertext set in that existing equality. |
| Blind 2: existing custody below writable ancestors | medium | The existing-root branch omits ancestor write checks, and its reversed ancestry predicate cannot select an ancestor. Patch: apply the existing custody safety rule to every ancestor, allowing sticky shared parents. |
| Blind 3: ancestor replacement between path check and open | low | Descriptor-relative traversal could further protect against an owner-controlled path substitution. The ancestor permission correction prevents replacement by other users in accepted custody; the remaining scenario requires the owner to concurrently replace its own private paths. Reject: this is not ordinary use of this local operator packet and descriptor plumbing adds substantial complexity. |
| Blind 4: private source modes dropped during final reread | medium | The final source loop calls read_source without its original privacy requirement; same bytes at 0644 can pass. Patch: recheck rehearsal input privacy and custody before publication. |
| Blind 5: selected project can archive a different preparer | medium | Retired code is compared to actual module files, but prepare_retirement.py itself is not. Patch: compare the retained preparer source with the actual executing file. |
| Blind 6: Git pointer-file checkouts are refused | low | The helper explicitly requires a local Git directory and refuses before allocating output. This run uses the existing standalone checkout, verified directly with git rev-parse. Reject: supporting additional worktree/submodule provenance is a new branch of behavior unnecessary for this operator run. |
| Blind 7: os.walk errors are silently omitted | medium | os.walk has no onerror callback; a PermissionError can yield absent-record counts and a completed packet. Patch: refuse on traversal error. This occurrence is introduced by the new preparer even though a related issue exists in the older deferred ledger. |
| Blind 8: added metadata/signatures are missed before publication | medium | The final check rechecks known records only, so a newly added matching record or signature is not compared. Patch: recheck the same exact-name metadata scope before allocating output. |
| Blind 9: validator can read different bytes from archived receipts | medium | validate_rehearsals performs its own reads; a replacement/restoration can leave returned receipt hashes different from captured bytes. Patch: compare returned manifest/record hashes with the captured source bytes. |
| Blind 10: partial output can claim preparationComplete | medium | The destination and attempt record appear before all records and checksums finish. Patch: validate serialization before allocation and publish only a completely checked staging packet into the exclusively reserved final directory. Preserve incomplete staging evidence on failure. |
| Verification gap: nested metadata inventory lacks regression coverage | medium | Existing tests place records at roots only; the review's root-only walk mutation still passes all sixteen tests. Patch: cover realistically nested records/signatures and their published count/path/digest lineage. |
| Verification other: unreadable subtrees disappear | medium | Independently demonstrated the same missing os.walk error handling as Blind 7. Patch with that shared correction; preserve the existing deferred ledger. |
| Edge 1: ciphertext set mismatch | medium | Independently demonstrated the same omitted-manifest ciphertext as Blind 1. Patch with the shared set equality. |
| Edge 2: traversal error | medium | Independently demonstrated the same silent subtree omission as Blind 7. Patch with the shared refusal. |
| Edge 3: writable ancestor | medium | Independently demonstrated existing 0700 custody beneath a nonsticky 0777 parent as Blind 2. Patch with the shared ancestor check. |
| Edge 4: overflowing metadata number fails after partial publication | medium | json.loads accepts 1e999 as infinity and canonical serialization refuses it only after attempt.json is written. Patch: validate serialization before allocating any published output and retain no false complete packet. |

## Design Notes

Approved Lease continuation: build explicit private plan bindings for existing native Lease UIDs, API identity/name/namespace, the original valid renewal time and a complete desired-content digest excluding only `spec.renewTime` (with the same existing status/server-bookkeeping normalization). Execution validates nonfuture, monotonic time and every other desired field against those bindings. Recheck retained Lease content during preservation rather than skipping all Lease desired fields. Seed the execution's private reviewed baseline only from the freshly validated objects so the unchanged shared manager Lease checkpoint still checks its exact post-controller transition. Assessment must verify the encrypted execution-before baseline against the same signed-plan bindings. No wildcard, blanket content exclusion, new Lease mutation, or expiry bypass is permitted. Old plans without renewal bindings retain their strict behavior. Document the explicit bindings in the operator handoff and rerun full catalog/fresh-node and seven-namespace qualification sequentially with settled final bytes; retain superseded attempts truthfully.

Use the established absolute retained-tool input contract consistently: resolve or refuse relative tool paths before hashing and execution so the recorded executable and the invoked executable are identical. Add a focused refusal/binding test for a relative basename that could otherwise resolve through PATH. Settle all changes and meaningful checks before renewing the long fixtures.

Renew the read-only synthetic native Lease observation with the settled executor. Validate its two actual samples through the real signed-plan Lease binding/content validator and retain a sanitized outcome proving monotonic renewal accepted, all other desired fields unchanged, and zero mutation requests or new mutation authority. Keep source credentials out of the fixture and preserve earlier immutable observations.

The renewed isolated restore stopped on the third fresh-sandbox stop with CRI `DeadlineExceeded`; the retained-image probe measured a two-second `crictl` default and no configuration timeout override. Set an explicit `--timeout 30s` only for stopping each exact listed sandbox in the fresh rollback node. Preserve the existing 120-second subprocess bound, validated node/sandbox identities, first nonzero-exit refusal and exact owned-resource cleanup. Verify that a stop failure permits no later sandbox stop or node-input/snapshot replacement. Settle the module and tests, then renew seven-namespace and full catalog/fresh-node qualification sequentially because the shared module bytes changed. Preserve the failed retirement/restore attempt and its successful cleanup; change no production phase guard or frozen intent.

The production executor must call the same reviewed phase guards as the synthetic fixtures through a separately injected native client. Its only mutation mode requires a verified, attempt-specific approval after the final plan and exact-byte fixture receipts exist. A stopped partial deletion is an incident requiring a new decision, never an automatic rollback.

## Verification

**Commands:**
- `python3 -m unittest discover -s eng/cluster-management -p 'test_*.py' -v` — latest resume run: 253 tests pass with no skips in 10.661 seconds; full output retained at `/tmp/story-4-27-resume-suite.log`. Its checksum and passing matrix tests are recorded in the [resume checkpoint](evidence/epic-4/4-27/20261006t091552z-s427-resume-verification/verification.json).
- `git diff --check HEAD` — no whitespace errors.

- [current-byte local audit](evidence/epic-4/4-27/20261006t085341z-s427-verification/verification.json) passed actual current-byte `validate_rehearsals`, complete private-manifest/readback verification, historical source-scope validation, the 253-test matrix audit, frozen-intent comparison and baseline maintenance preservation. The earlier [local audit](evidence/epic-4/4-27/20261005t104023z-s427-verification/verification.json) retains only its original prior-byte meaning. No audit supplies a fresh production gate.

**Matrix audit:** all covering tests ran and passed (`... ok`) in the 253-test verbose output; none was skipped. This verifies software behavior; live operational acceptance remains unobserved.

| Matrix row | Passing covering tests |
| --- | --- |
| Approved attempt | `test_shared_phase_execution_and_real_plan_to_execute_handoff` |
| Stale or incomplete prerequisite | `test_stale_approval_is_zero_mutations`, `test_missing_recovery_or_console_gate_is_zero_mutations`, `test_caller_verified_flag_cannot_replace_actual_signature` |
| Bound Lease renewal | `test_bound_node_lease_renewal_accepts_signed_handoff_without_lease_mutation`, `test_manager_checkpoint_object_get_refuses_nanosecond_regression_below_observed_floor`, `test_bound_lease_holder_drift_after_accepted_delete_stops_further_requests`, `test_assessment_accepts_validated_renewed_execution_baseline_and_later_renewal` |
| Partial retirement | `test_conflict_stops_first_request_without_retry`, `test_unexpected_protected_deletion_stops_before_next_request`, `test_recreated_controller_stops_before_next_request` |
| Completion | `test_signed_postflight_and_exact_administrator_acceptance`, `test_failed_post_retirement_health_leaves_acceptance_and_hop_closed` |

**Manual checks (if no CLI):**
- Inspect exact signed approvals, independent external denial, fresh recovery/readback and authenticated production outcomes before recording completion.


## Operational-prerequisite continuation checkpoint — 2026-10-14

Completed this continuation's local/read-only deliverables only. The
[next operator action](../../eng/cluster-management/NEXT-OPERATOR-ACTION.md)
names concrete unsigned fresh recovery tools bundle `20261014t112353z`, created
with the retained offline `new` interface, and its exact current scope/signing/
protected-unlock blocker. All 37 staged tool checksums match; no operational
record, policy upload or approval is supplied by allocation. Commands preserve
the signed/uploaded-policy guard and split the retained Memories substeps so
its fresh census binds the one actual pre record before pause/capture.

At the current clock, historical provider/source validation returned FAIL
(641/648 pass, seven age/staleness failures, zero skips; all 553 objects freshly
reread with matching bytes). Eleven historical signatures verified against
retained historical recovery trust. Static `jpiquot@local` reads observed
v1.34.9 and expected absent console Ingress, without accepting the recovered
independent native path. Two bounded read-only Memories execs observed Redis
three keys/no tenant-registry or active workflow/reminder counts and FalkorDB
zero keys/graphs; raw outputs remain privately encrypted without readback.
Story 4.2's accepted October 7 closure and October 8 GC follow-up are reused
within scope; narrow current console/master-path GETs were not routed, discovery
returned 200 and public DNS matched, without accepting independent external
vantage/authenticated OIDC or supplying the exact signed retirement record.

The unchanged preparer and 31 existing preparation tests passed locally. New
packet `20261014t112004z-s427-prerequisite-investigation` retains final consumed
source bytes after documentation settles. A separate owner-only supplemental
packet retains the new action document and exact retained-tool/read-only
lineage; its digest and actual packet/source/mode/checksum/Bash/preservation
outcomes are in the
[separate sanitized unsigned verification receipt](evidence/epic-4/4-27/20261014t112004z-s427-prerequisite-investigation/verification.json).
Actual read-only observations are separate from the offline preparer's zero
remote/signing/mutation behavior. No original execution/acceptance task is
completed. Historical packets/receipts, frozen intent, bound execution/procedure/
maintenance and unrelated dirty files remain preserved. Keep 4.27
`in-progress`, 4.1 closed and Timesheets 3.6 `in-progress`/FAIL.


## Final current-policy preparation checkpoint — 2026-10-14T11:40:42Z

Separate retained read-only `precheck.py` and local `policy.py` prepared actual
unsigned policy bytes in fresh RID `20261014t112353z`. No uploading composite,
signature, capture, quiescence or restore was dispatched. The
[next operator action](../../eng/cluster-management/NEXT-OPERATOR-ACTION.md)
binds exact policy/precheck hashes and separately deliberate signing; a needed
refresh allocates a new bundle rather than overwriting this one. Existing
October 7–31 dates, conversation incident channel and Administrator roles
remain reusable within their scope. The missing approval binds this prepared
RID/policy to those decisions and the exact current capture/signing procedure.

Final offline packet `20261014t114108z-s427-current-policy-preparation` and
[separate unsigned verification](evidence/epic-4/4-27/20261014t114108z-s427-current-policy-preparation/verification.json)
retain corrected final consumed sources and separately private policy/action/
tool/read-only lineage. The earlier `20261014t112004z-s427-prerequisite-investigation`
packet/receipt/supplement are unchanged historical checkpoints; both IDs were
used once. Relevant existing tests/Bash checks and unchanged frozen/operational/
execution/procedure/maintenance bytes are verified without rerunning fixtures.
All original operational tasks and live gates remain pending/closed: 4.27
`in-progress`, 4.1 closed and Timesheets 3.6 `in-progress`/FAIL.


## Current approved recovery signing outcome — 2026-10-14

The Administrator's **“I approve”** is retained in immutable owner-only
`20261014t120527z-s427-approved-recovery-signing` custody and the
[sanitized scope receipt](evidence/epic-4/4-27/20261014t120527z-s427-approved-recovery-signing/scope-approval.json).
It binds the exact prepared policy/precheck/handoff/tools to the existing
October 7–31 dates, conversation incident channel and acknowledged roles.
Current VCS identity is `8580eb64ac8a9ac3c025dd6c7f64aa6f39ce5cbb`; the
legacy baseline/frozen intent remain unchanged. Scope permission persists.

All exact-byte/tool/trust/public-fingerprint/policy-age/window guards passed;
safe native static configuration and the same cluster UID/v1.34.9 were reread
with two bounded GETs. No signing agent or protected unlock was available.
The exact approved `./sign.sh policy/recovery-policy.json` attempt, with no
controlling terminal/askpass, stdin `/dev/null` and a 20-second bound, returned
255 without timeout because protected-key unlock is unavailable. No signature
or uploads ledger appeared and publication was not reached. Raw diagnostics
are encrypted privately. Zero capture/quiescence/restore or retirement/upgrade/
deployment commands followed. The
[separate unsigned outcome verification](evidence/epic-4/4-27/20261014t120527z-s427-approved-recovery-signing/verification.json)
binds the actual refusal, archive/mode/checksum and preservation checks.

The [handoff's current approval checkpoint](../../eng/cluster-management/NEXT-OPERATOR-ACTION.md#current-approval-retained-protected-signing-access-required--2026-10-14)
supplies the exact private-terminal signing action and preserved approval
reference. Request only protected signing access, not repeat scope approval.
Fresh authentic signature/provider readback precedes any approved capture;
fresh signed restore/cleanup/non-dry validation, M1/M2 and recovery acceptance
still remain pending. Previous packets/receipts/policy bytes and bound
execution/procedure/maintenance are preserved, with no fixture/test rerun or
Git staging/commit. 4.27 stays `in-progress`, 4.1 closed and Timesheets 3.6
open/FAIL; no original production task or whole-story acceptance is completed.


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
