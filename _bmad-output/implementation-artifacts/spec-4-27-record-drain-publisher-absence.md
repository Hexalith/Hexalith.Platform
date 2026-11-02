---
title: '4.27: Record the drain publisher absence'
type: 'chore'
created: '2026-11-02'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'c869f1c7b6b86bd693911ad10e328235ea697da8'
story_key: '4-27-retire-kubesphere-without-changing-workload-data'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The stopped 4.27 recovery attempt `20261101t131121z` failed its drain because Pod `mcpcli-417-20261101-00c6a713/eventstore-nkkwj` was publishing. `MEMORIES-DRAIN-OWNER-HANDOFF.md` asks for that workload's disposition, its time and its exact identities to be recorded. On 2026-11-02 the namespace was found already deleted. It belonged to McpCli Story 4.18's temporary environment, approved on 2026-11-01. No removal receipt exists in McpCli or Platform, and the 4.18 private `/tmp` working folder was emptied. Asked on 2026-11-02, the user answered "Not me / not sure", so the actor and time are unknown.

**Approach:** Record the release honestly. Using the checksum-verified kubectl v1.34.9 and context `jpiquot@local`, take one fresh read-only observation:
- the cluster identity;
- the namespace's absence;
- the count of Pods matching `dapr.io/app-id=eventstore` across all namespaces, which is the same selector `mem.py` uses;
- the absence of any PV whose claim belongs to the deleted namespace.

Check local session records for who removed it, and when, without assuming either. Save a sanitized evidence receipt under `evidence/epic-4/4-27/`. Add a dated checkpoint to the handoff, the 4.27 story and spec, and the operator docs.

**Decisions (2026-11-02):**
- The user chose "record the release only". Preparing the successor recovery is a separate later run. It needs a new RID bundle, a new signed policy, new captures and a held-target cleanup decision, plus the user's window, freeze and held-target choices.
- The removal is recorded as "observed absent, actor/time unrecorded" and flagged as an unexplained deletion, unless local records identify the actor.

**Limits:**
- This observation does not pass drain. A successor attempt must still prove a zero global publisher count in both drain samples, 65 seconds apart.
- Make no cluster mutation.
- Do not replay or overwrite any stopped-attempt record or tool.
- Do not run the recovery tools that are bound to the expired RID.
- Leave sprint story 4.27 `in-progress`. The workflow's review/done sprint sync is not applied to this broader story.
- Commit only this spec's files. Other sessions' working-tree changes stay untouched.

</frozen-after-approval>

## Implementation Notes

**Actor found.** Local Codex logs settled the "who and when":
- Session `rollout-2026-11-01T13-22-28-01a1970f…` was working McpCli 4.18. It deleted the namespace with a server-enforced UID precondition at 2026-11-01T19:50:54Z and finished at 19:51:13Z.
- This happened inside the approved packet's review window, roughly 2.5 hours after the 17:21Z drain failure.
- The receipt exists, but in McpCli's git-ignored `.artifacts/`. That is why it looked unrecorded. It is bound here by SHA-256.
- The disposition is recorded as "released by its owning workflow", and the user's "Not me / not sure" answer is noted.

**Surprise: a second publisher.** McpCli environment `mcpcli-417-20261102-6383083d` (Pod `eventstore-q7qqz`) ran from 12:07Z to 14:15Z on 2026-11-02 and was cleaned up. It is recorded as a lesson for the successor outage window.

**Discrepancy, recorded but not acted on.** The 4.18 private root claimed to keep its backups and journals, yet it held only `clients/` on 2026-11-02.

**Observation.** The read-only observation ran at 2026-11-02T20:02:46Z:
- kubectl v1.34.9, SHA-256 `73bb6f50…`, the same binary as McpCli's provenance record;
- `jpiquot@local` against `https://192.168.1.30:6443`, kube-system UID `b5860674…`, which matches earlier evidence;
- two reads of the `mem.py` selector, both zero;
- both namespaces absent, no `mcpcli-*` namespaces, no PVs claimed from them.

The script made only `get`, `version` and `config view` calls.

**Files changed:**
- New folder `evidence/epic-4/4-27/20261102t200246z-s427-drain-publisher-absence/` with `observation.json`, `observe.py` and `disposition.json`.
- The same dated checkpoint appended to the handoff, `NEXT-OPERATOR-ACTION.md`, `STAGING-PREREQUISITES.md`, the Timesheets staging-dependency handoff, and the 4.27 story and spec.
- Sprint status unchanged: 4.27 is already in-progress.

**After review.** The patch round copied the McpCli cleanup and second-environment records into the evidence folder, byte-identical with their bound hashes. It also recorded:
- the root cause: environment approval at 14:04:49Z and deployment at 14:05:19Z, inside the recovery attempt;
- the second environment's user approval, at 11:26:33Z on 2026-11-02;
- the `mem.py` SHA-256 and the kubectl provenance;
- that the user has not confirmed the attribution.

The six-document checkpoint was regenerated. It now keeps "4.1 closed / Timesheets 3.6 open/FAIL" and requires a freeze of every EventStore-publishing workflow.

This frozen Intent said no removal receipt existed and the actor was unknown. That was what we knew before the McpCli `.artifacts/` receipt and the Codex logs were found. The receipt records the corrected facts.

**Verification.**
- All seven hashes of the copied records were recomputed and match.
- Every new relative link resolves from all six documents.
- `disposition.json` parses.
- `git diff --check` is clean.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| 4.1/Timesheets status wording changed | medium | patch | Earlier checkpoints said "4.1 closed, Timesheets 3.6 open/FAIL"; the new text said "blocked" and dropped Timesheets. Restored. |
| Root-cause timeline missing | medium | patch | McpCli `approval.json` shows 14:04:49Z, and infrastructure started at 14:05:19Z, inside RID 13:11:21Z–23:00Z. Added. |
| Lesson too narrow, never a requirement | medium | patch | The `mem.py:109` selector counts every Pod with the app-id. Generalized, and added to the successor requirements. A cross-repo guard was deferred. |
| Hashes bound to ephemeral files | medium | patch | The receipts were in git-ignored `.artifacts/` and in `/tmp`. Byte-identical copies are now retained, and the Codex call timestamp is cited. |
| Custody requirement vs PV deletion and missing backups | medium | patch + defer | The PV deletion follows the approved packet's reviewed cleanup lifecycle, now recorded. The private-root loss is unexplained, so it is deferred. |
| Second environment's authorization undocumented | medium | patch | The user approval at 11:26:33Z (review `26a2b4f2…`) and the reviewed-runner cleanup were found in the Codex log and added. PV absence is covered by the claimRef check. |
| Receipt thinner than its predecessor | low | patch (partial) | Added the read-only request count and Timesheets field. Pre-append tracking hashes were rejected as ceremony. |
| "Refuse every entrypoint" not verified this run | low | patch | Now marked as carried over and not re-executed. |
| observe.py self-proof gaps | low | patch (partial) | Added the `mem.py` SHA-256 and kubectl provenance. The `assert` under `-O` and per-read timestamps were rejected: a one-off script run with `-I`, and start/end times are recorded. |
| "Unowned" label and 417/418 naming | low | patch | Supersession and prefix origin added. |
| User never confirmed the attribution | medium | patch | Marked as not confirmed by the user, in both the receipt and the documents. |
| Spec gaps (frozen facts, verification, sanitization) | low | patch (partial) | Frozen Intent can't be edited, so a correction note and a verification list were added. Sanitization was rejected: session IDs and local paths are not secrets and are already common in this repo. |

