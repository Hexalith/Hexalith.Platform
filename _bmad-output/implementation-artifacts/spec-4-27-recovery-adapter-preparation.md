---
title: '4.27: Prepare bounded recovery adapters and an exact command bundle'
type: 'feature'
epic: 4
story: 27
created: '2026-10-23'
status: 'in-progress'
route: 'dispatch'
baseline_commit: '7c37f3f2c5e7e55693db70baa8885bdb10bd9ff6'
review_loop_iteration: 1
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The October 17 unsigned recovery candidate is expired. Current source, authority, custody, provider protection and recovery adapter facts must be resolved before the owner can review a concrete next attempt.

**Approach:** Perform bounded read-only investigation, implement reversible local adapters, verify them offline, and assemble one exact command bundle. The user's October 23 instruction authorizes this preparation without intermediate permission checkpoints; actual operation remains prohibited in this run.

## Boundaries & Constraints

**Always:** Preserve the original Story 4.27 frozen intent, expired attempts, signed originals, bound retirement/procedure/maintenance bytes and original production acceptance. Retain scoped owner decisions, including sole-owner documentary self-review, existing destination/principal/key, roles, incident channel and October 7–31 window. Treat current authentication, grant lineage, provider protections and runtime success as separate facts. Use distinct validator credentials for immutable exact-version byte/hash/retention readback. Authenticate and independently read back a new policy and signature before any future capture. Bind source/target identity, exact tool bytes, stop behavior and source-resume/target-cleanup procedures. Keep raw source/provider/custody details encrypted privately; public output is a neutral sanitized projection.

**Never:** Capture, quiesce, restore, sign, upload, change permissions, retire, upgrade or deploy in this preparation. Never select a historical executor through a new RID, allocate a fresh recovery RID merely to refresh dates, reopen an old attempt or accept self-reported booleans as operational qualification. Keep 4.27 in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Offline plan | Exact current bindings and typed source/target objects | Deterministic capture/restore/resume/cleanup requests and required evidence | No credentials, sockets or live dispatcher invoked |
| Unsafe input | Expiry, hash/UID drift, alias, source-target collision, malformed data, ambiguous identity | Refuse before injected operation | Fixed sanitized failure; preserve earlier records |
| Capture/restore boundary | Approved fresh policy and independently checked immutable input | Explicit request bounds, source protection and verified isolated target | Unknown/failed semantic outcome stops; no automatic retry |
| Source resume | Recorded prior replicas, valid restore/isolation, healthy restarted stores and fresh clean drain | Guarded exact-identity replica patch and actual reconciliation requirement | Failed/uncertain path retains pause for incident owner |
| Target cleanup | Recorded owned namespace/PVC/PV UIDs and protected source set | UID/resourceVersion-preconditioned deletions, exact watcher restoration and independent absence checks | 403/timeouts/remaining storage/path cannot mean absence |

</frozen-after-approval>

## Code Map

- `eng/recovery-preparation/recovery_adapters.py` (new): typed guarded adapter request builders and injected-I/O state machine; CLI only local validation/rendering, no live activation. Current runtime qualification belongs to a separately approved driver, never these offline results.
- `eng/recovery-preparation/test_recovery_adapters.py` (new): adversarial synthetic request/state tests including every matrix row.
- `eng/recovery-preparation/RECOVERY-ADAPTERS.md` (new): interface, bounds, evidence requirements and qualification limits.
- `/home/administrator/hexalith-retirement-investigation-evidence/20261023t085529z-s427-recovery-command-preparation/review-1-source/` -- exact archived implementation/test/docs and manifest before review-loop reversion; restore as the reference implementation and re-derive with the non-frozen change-log requirements below. No operational artifacts or credentials are in this source snapshot.
- `eng/recovery-preparation/static_transport_preflight.py`, `native_https_transport.py`: reuse safe custody/schema patterns as reference; preserve their historical bindings and bytes.
- Historical private October 14 tools: reference algorithms for CNPG/WAL, OpenBao raft, paired Memories copy/drain, private OIDC, isolation and cleanup; never import/run/modify originals.
- `eng/cluster-management/NEXT-OPERATOR-ACTION.md:875`: append a new truthful handoff, preserve old bytes as prefix.

## Tasks & Acceptance

**Execution:**
- [ ] `eng/recovery-preparation/recovery_adapters.py` -- implement typed capture/restore/resume/cleanup request builders and guarded transitions with injected backend; exact immutable reads, explicit resource identities, bounded argv/stdin, no ambient credentials, no retries, create-only receipts and safe failure states. Real-system acceptance fields remain unestablished without actual execution. Public CLI must refuse live activation before touching credentials. Do not claim these primitives implement complete CNPG/OpenBao/Memories orchestration.
- [ ] `eng/recovery-preparation/test_recovery_adapters.py` -- test meaningful adversarial inputs, semantic failures, source protection, resume/cleanup handling, partial failure and offline/live separation.
- [ ] `eng/recovery-preparation/RECOVERY-ADAPTERS.md` -- document how the adapters compose with exact future staged operations, explicit missing orchestration/qualification and failure recovery.
- [ ] Private new preparation custody -- collect current bounded read-only native/provider/custody facts; retain exact request/tool/input identities and encrypted raw data; prepare one concrete reviewable bundle with commands, proposed targets, expected effects, dependencies, stops, resume and cleanup. Missing current authority/runtime facts stay explicit gates.
- [ ] New sanitized evidence receipt and append-only handoff/story/spec checkpoint -- bind tools/tests/private packet, preserve historical bytes and gates; do not stage, commit or push.

**Acceptance Criteria:**
- Given the current read-only authorization, when observations complete, then measured facts and unknown PKI/recovery-access/provider/runtime qualifications are explicit and no prohibited operation occurred.
- Given synthetic adapter inputs, when tests run, then unsafe transitions issue no further requests and process success cannot override semantic failure.
- Given the completed preparation, when the owner reviews the bundle, then exact hashes, inputs, proposed targets, effects, stops and recovery procedures are available without a fresh RID or operational approval being inferred.

## Implementation Notes

Investigation and offline implementation are explicitly authorized by the current user. No intermediate skill approval is required. Existing unrelated dirty submodules are preserved. This spec covers preparation only; the original Story 4.27 production tasks remain pending.

Implementation dispatch owns the three new adapter/code-test/documentation files. The parent session owns current observations, private bundle assembly, preservation audit and append-only tracking; two investigation agents are collecting native/provider facts. Do not duplicate their remote reads or edit their private directories. Report parent-owned tasks as pending integration, not a reason to run operational work.

## Spec Change Log

2026-10-23 review 1 — non-frozen implementation contract needs explicit source-volume ownership and namespace-creation fencing. All seventeen findings below are verified against the retained diff. Re-derive the three implementation files from the exact private `review-1-source` snapshot, preserving its positive behavior and fixing the complete set below. Parent owns final packet rebinding/index and public tracking. No frozen text changes.

KEEP: offline-only CLI, unqualified/null acceptance, all 53 existing passing adversarial tests, exact readback bytes passed to signature verification, no historical executor or ambient credential loader, create-only records, semantic stops and source/target proposal separation. Existing snapshots and initial tool/input measurements remain unchanged; final revised hashes require a distinct explicit relation.

Re-derivation requirements:
- Replace fully bound `protectedPaths` string entries with typed source volume records `{pvUid,nodeUid,path}` (reuse VolumePath). Require exact one-to-one coverage of protected source PV UIDs. Reject missing/duplicate mappings and equal/ancestor/descendant overlap between any target path and any source path. Pass these bound source-volume records to dependent stage protocol requests. Proposal schema has no source paths and must remain compatible with actual current proposal inputs.
- Require sourceStores to be distinct UID/path objects of supported store kinds (StatefulSet, Deployment or CNPG Cluster) belonging to sourceNamespace; reject Namespace or unrelated resource entries. Protect watcher UID and path regardless of resourceVersion differences in ownedTargets.
- Reject noncanonical numeric port spellings before endpoint comparison, including leading zeros.
- Cleanup must verify a separately qualified creation fence immediately before complete owned inventory and again immediately before namespace deletion. Bind the actual fence Resource identity through a new required `creationFence` field for fully bound cleanup (null for other operations). Reuse supported Resource type NetworkPolicy as a separately qualified external fence reference only if appropriate; a NetworkPolicy is not itself a creation fence. Prefer an explicit typed fence identifier/reference (uid/version/namespaceUid) with protocol observation; never claim that this module creates/enforces the fence. Failed/unknown/changed fence prevents destructive requests. Proposal keeps the fence unestablished/null. No new live fence or permission mutation may execute.
- Validate native Kubernetes DELETE accepted status codes 200/202 and standard Status/resource response shapes, matching supplied UID/name/group where present; keep identity preconditions and require independent absence before completion. NotFound must validate exact requested name/plural/API group and allow standard optional Status fields. Wrong group, malformed response, forbidden and timeout still refuse. Use primary Kubernetes v0.34 API response fixtures, not a synthetic exact-dict-only schema.
- Recheck policy window and fresh drain after create-only intent persistence, immediately before backend dispatch, so slow persistence cannot permit expired/stale mutations. Add slow-receipt tests.
- The explicit external creation-fence reference may bind a ValidatingAdmissionPolicyBinding identity, namespace UID and qualification digest. It is never an owned cleanup target. Restrict cluster-scoped owned cleanup kinds to Namespace and PersistentVolume, so adding a fence reference type cannot broaden deletion authority to admission/cluster-control objects. This module neither creates nor qualifies the actual fence.
- Final cleanup source preservation must treat the intentionally restored watcher with matching UID and restored exact watch scope, allowing its legitimate resourceVersion change; all other protected resources retain strict identity checks. Include watcher in protectedSources in a synthetic fixture and verify successful cleanup plus wrong restored scope refusal.
- Add post-restore and post-cleanup source UID/resourceVersion drift tests that require failure/no completion. They must fail if all preserve-source requests are removed. Preserve every existing test, updating only changed source-volume/fence schema fixtures and real API-shape expectations.


## Review Triage Log

| Finding | Verdict | Verified evidence / route |
| --- | --- | --- |
| Blind 1 | high | Target/source ancestor paths pass equality-only check; source-volume contract loopback fixes containment. |
| Blind 2 | high | Empty source path list accepted despite source PV; bad_spec requires typed complete source volume mapping. |
| Blind 3 | medium | Namespace/duplicate sourceStores accepted; re-derivation restricts store kind and unique identities. |
| Blind 4 | high | Watcher owned-target comparison used dataclass RV equality; same UID/path with altered RV accepted. |
| Blind 5 | medium | Leading-zero port strings alias the same endpoint under different cluster UIDs. |
| Blind 6 | medium | Valid native DELETE Status fields/202 rejected after deletion; validate native shapes with mandatory later absence. |
| Blind 7 | medium | Real grouped-resource NotFound has details.group; exact synthetic dict rejects it. |
| Blind 8 | high | Namespace RV does not fence descendant creation; bad_spec requires bound creation-fence observation around inventory/deletion. |
| Blind 9 | medium | Initial private tool/proposal measurements predate auth-byte correction; parent will preserve v1 and explicitly rebind final revision. |
| Blind 10 | medium | Draft references not-yet-assembled bundle.json and local argv absent; parent will add final index and exact offline commands. |
| Edge 1 | high | Persistence can consume policy/drain freshness after the pre-write check; dispatch must recheck. |
| Edge 2 | high | Same containment root cause as Blind 1, independently verified. |
| Edge 3 | medium | Current proposal protects watcher; its authorized patch changes RV and strict final preservation wrongly refuses. |
| Edge 4 | medium | Same native DELETE schema root cause as Blind 6. |
| Edge 5 | medium | Same grouped NotFound schema root cause as Blind 7. |
| Edge 6 | medium | Same stale tool/proposal measurement root cause as Blind 9. |
| Verification 1 | medium | Removing every final preserve-source request still passes all 53 tests; add actual post-operation drift regressions. |

All findings are this preparation's issue; none are deferred. Source-volume/fence contract gaps route to bad_spec. Other code/test corrections are retained re-derivation requirements; parent packet index/hash corrections remain pending integration. Original Story 4.27 intent and production acceptance remain unchanged.

## Verification

- `/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_recovery_adapters.py` -- all meaningful offline cases pass, no live I/O.
- Compare preservation inventory and frozen block before/after; verify private checksums and exact public projections; `git diff --check`.
