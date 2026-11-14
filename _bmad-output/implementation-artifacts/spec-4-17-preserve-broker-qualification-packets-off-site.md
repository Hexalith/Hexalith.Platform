---
title: 'Story 4.17: Preserve broker qualification packets off-site'
type: 'chore'
created: '2026-11-14'
status: 'done'
route: 'dispatch'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
review_loop_iteration: 0
baseline_commit: '2a1a343c35d58e6368cd4c34288dc3f9c567f8b9'
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Four broker packets have only local hard links and expiring GitHub copies. Complete packets are needed while guarantees remain open; the earliest recorded expiry is 2026-12-08T23:05:01Z.

**Approach:** Preserve four untouched ZIPs and their inventory manifest in an accepted existing archive, independently retrieve exact versions, and record per-archive custody outcomes. Resolve destination/protection/roles before execution.

## Boundaries & Constraints

**Always:** Preserve bytes, identities, authority labels and originals. Retain packets while guarantees remain open, then review deletion against policy. Keep Platform 4.17/EventStore 3.21 in-progress, five guarantees REJECTED/open, technical FAIL (20/24), selection null and production FAIL.

**Never:** Repack/overwrite; expose credentials/private contents; change bucket/IAM/lifecycle controls, qualification inputs or assessments; provision storage; dispatch qualification; commit/push. Preservation grants no broker acceptance.

## I/O & Edge-Case Matrix

| State | Behavior | Failure handling |
| --- | --- | --- |
| Five admitted exact files | Conditional writes; independent exact-version retrieval | Per-file outcome |
| Source/private-material/scope failure | No affected upload | Stop, preserve originals |
| Collision/uncertain PUT | No overwrite or automatic retry | Reconcile read-only |
| Incomplete/wrong readback or protection | Custody incomplete | No credit from other files |

**Owner decision (2026-11-14, “apply recommended”):** Admit these four sanitized ZIPs and unchanged manifest to Scaleway `hexalith-recovery-points`, `fr-par`, under fresh `evidence/4-17/<transfer-id>/`. Keep existing 30-day COMPLIANCE/no-expiry evidence settings and Administrator-owned no-deletion hold until guarantees close. Codex acts for the Administrator through the writer reference; a separate read-only agent retrieves through the validator reference. This is separately executed automated verification on a shared host, not independent human accountability or a security boundary. No object legal hold or policy change.

</frozen-after-approval>

## Code Map

- `docs/implementation/story-4-17-packet-custody-preparation-2026-11-14.md`: four identities/hashes/deadlines, acceptance and reproducible inventory procedure. Preserve its historical preparation record.
- `artifacts/story-4-17-custody-staging-2026-11-14/manifest.json`: SHA-256 `ea4b7666ae5b77b3f00b24b6ee32c35c98da6bbe5004ebc24fb5f9c1bfdd183f`; four ZIPs total 7,777,857 bytes, 2,215 entries, 2,059 packet artifacts. Keep local-only/null fields unchanged; create separate receipts.
- `_bmad-output/implementation-artifacts/evidence/epic-4/recovery-destination-setup.md`: historical endpoint, versioning/AES256/retention, lifecycle and access evidence; current recheck required.
- `eng/cluster-management/NEXT-OPERATOR-ACTION.md`: `~/.config/hexalith-recovery/{writer,validator}.env` references. Parse privately as data, never shell-source/print.
- `eng/kubernetes-upgrade/native-node-recovery/publication.py`: conditional PUT/version-bound retrieval pattern. Its encrypted Story 4.0 guards exclude broker ZIPs: never invoke or relax them. Use the existing S3 SDK for this one-off operation.

## Tasks & Acceptance

**Execution:**
- [x] `docs/implementation/story-4-17-custody/plan.md` — bind accepted destination/protection/actual roles, fresh prefix, five source commitments, operations and stop rules. Independently inspect the exact one-off operation before mutation; retain its digest.
- [x] `docs/implementation/story-4-17-custody/preflight.json` — record sanitized current archive/access/protection checks and original inventory/schema/private-material admission. Missing scope, drift or unsafe material blocks PUT; control changes require a separate proposal.
- [x] `artifacts/story-4-17-custody-staging-2026-11-14/` — read five unchanged files through descriptors, verify hashes/stability, upload once each with conditional creation, explicit endpoint/region, verified TLS and no SDK retry. Retain intents and returned versions; uncertainty stops execution.
- [x] `docs/implementation/story-4-17-custody/receipt.json` — accepted independent verifier uses validator-only credentials to download exact versions, checks every digest/entry/packet artifact, safety/private-material exclusions, encryption/retention and manifest. Per archive record identity, original authority, object/version, actual operator/verifier, checks/outcome and completion time. All five must pass before overall acceptance.
- [x] `docs/implementation/story-4-17-packet-custody-preparation-2026-11-14.md`, `docs/implementation/story-4-17-next-candidate-intake.md`, `_bmad-output/implementation-artifacts/deferred-work.md` — append actual custody results/links; close only demonstrated preservation work. Keep engineering dependencies and parent statuses unchanged.

**Acceptance Criteria:**
- Given accepted scope, when independently retrieved, then all four packets and manifest match full commitments before their respective expiries.
- Given a failed/uncertain operation, when reporting custody, then its archive remains unaccepted with original evidence authority.
- Given preservation, when evaluating Story 4.17, then broker dispositions and staging/Subscription prerequisites remain unchanged.

## Implementation Notes

- The owner approved the recommended plan and execution. This explicitly authorizes the bounded archive writes/readback despite the generic build step's no-remote-operations rule. No other remote mutation is authorized. Parent status is already in-progress; preserve its file unchanged.
- Before any PUT, prepare the one-off script, plan and meaningful offline matrix checks. Send their exact paths/digests to the parent agent and await its independent technical inspection. This is a code-review gate, not another user approval. Retain the inspected script with the sanitized evidence. Do not relax existing recovery helper guards.
- Dispatch a separate read-only verifier only after upload; it must retrieve exact returned versions with validator credentials, preserve download evidence and report checks independently. Never expose credential values, signed URLs, raw SDK errors or private archive contents in tool output. Record sanitized errors; uncertain PUTs receive read-only reconciliation only.

## Spec Change Log

## Review Triage Log

| Finding | Verdict / route | Evidence and disposition |
| --- | --- | --- |
| B1 missing directory fsync | medium / patch | File contents are flushed, but new marker directory entries are not. Preserve executed bytes, then flush the containing directory in the retained tooling. Conditional creation and verified stored versions remain intact. |
| B2 verifier controls cannot rerun safely | medium / patch | The standalone matrix exclusively writes its historical report and exits 1 on rerun. Add a local-only rerun route with a fresh report, preserving the original script/report hashes. |
| B3 failed-receipt retry framework | low / reject | This is an explicitly one-off verifier; refusing an existing receipt preserves evidence. The successful accepted transfer needs no retry framework or accepted-attempt selector. Document fresh read-only reassessment without replacing historical receipts. |
| B4 pre-PUT failure lacks terminal record | medium / patch | A credential/source/protection exception after the start marker occurs outside the PUT incident handler. Retain a sanitized pre-PUT failure record; no new transfer attempt is authorized. |
| B5 manifest exclusion scan omitted | low / patch | The fifth object's metadata/digest are verified, but its raw bytes are not passed through the bounded private-material scan. Inspect those exact bytes separately and retain the result without modifying the uploaded manifest. |
| B6 exact object ACL not observed | medium / patch | Bucket ACL/policy were checked, but object-version ACLs were not requested. Perform validator-only exact-version ACL GETs and retain a supplemental admission result; change no grant. |
| B7 upload-result uncertainty could be accepted | false / reject | The sole producer writes upload-results.json only after all five returned-version records succeed; any uncertain PUT raises first and writes no aggregate. Fixed keys/hashes and independent bytes/protection checks also bind actual custody. The claimed failed aggregate is not emitted by the caller. |
| B8 artifact-hash negative stops at count | medium / patch | The synthetic packet-artifact case has an empty map with expected count one, so it reaches count refusal rather than digest refusal. Add a correct-sized wrong-digest case in supplemental checks. |
| B9 exhaustive nested-archive negative matrix | low / reject | Current inputs are five immutable commitments; real nested public charts were fully inspected, and byte drift is refused. A broad generic archive-validator matrix adds disproportionate work to this one packet set; retain the bounded-scan limitation. |
| B10 hold follow-up guidance | low / patch | Owner and indefinite operational hold are recorded, but a concise read-only follow-up before initial lock expiry is useful. Document the owner/checkpoint and fresh-record procedure; schedule no automation and change no policy. |
| E1 S3 redirect bypasses normal retry limit | medium / patch | Independent offline transport returned 301 then 200 and observed two sends despite total_max_attempts=1. Add a guard before a second PUT send and a regression control. Retain exact executed source and qualify historical request-count claims; never replay the completed transfer. |
| E2 missing directory fsync | medium / patch | Same demonstrated journal durability gap as B1; retain this finding separately and apply the same directory-flush correction. |
| V1 failed protection not tested through receipt | medium / patch | Pre-verified mutation probe removed the readback-success gate while all 25 controls still passed; a bad-protection download then received an accepted receipt. Original code is correct. Add an offline retrieval-to-receipt failure test that requires exit 1 and keeps that archive and overall custody unaccepted. |

## Verification

- Run the preparation document's inventory procedure on local and independently downloaded files: four ZIPs, 2,215 entries, 2,059 artifact hashes; verify manifest digest separately.
- Compare version-bound HEAD/GET/retention and receipts with accepted policy. ETags/local readback cannot substitute.
- Check local links, `git diff --check`, unchanged parent statuses/qualification inputs. No behavioral broker tests apply.


## Implementation Record

- Completed bounded preservation at **2026-11-14T01:14:47.503827Z**, before all four original expiries. All four untouched ZIPs plus unchanged manifest are accepted in the [per-file receipt](../../docs/implementation/story-4-17-custody/receipt.json).
- Retained the exact [plan](../../docs/implementation/story-4-17-custody/plan.md), [one-off operation](../../docs/implementation/story-4-17-custody/transfer_once.py), [independent pre-write inspection](../../docs/implementation/story-4-17-custody/inspection.json), [current preflight](../../docs/implementation/story-4-17-custody/preflight.json), per-file intents/returned versions and [upload results](../../docs/implementation/story-4-17-custody/upload-results.json). Exactly five conditional PUTs returned versions; no collision, uncertainty, retry or additional remote mutation occurred.
- Writer executor `/root/preserve_packets_impl` acted for the Administrator through the accepted writer reference. Separate `/root/custody_verifier` used only the validator reference for exact-version HEAD/GET/retention and full independent downloads. Shared-host execution provides no independent human accountability or security boundary; effective IAM/API-key identity bindings remain historical/reference bindings, with current bucket-policy controls observed.
- Verification: [17 offline transfer checks](../../docs/implementation/story-4-17-custody/offline-checks.json) and [25 verifier controls](../../docs/implementation/story-4-17-custody/verifier-negative-checks.json) PASS; literal preparation inventory procedure PASS both locally and on independent downloads; four ZIP commitments, 2,215 entries, 2,059 packet artifact hashes and separate unchanged manifest digest PASS. Bounded safety/private-material checks and version-bound AES256/30-day COMPLIANCE/no-expiry evidence protection PASS.
- Administrator retains the no-deletion hold while guarantees are open, then reviews deletion against policy; no object legal hold or controls changed. Original bytes, hard links, identities, authority labels and original manifest local-only/null fields remain intact. Historical preparation/intake/deferred records were updated append-only, closing only demonstrated preservation.
- [Protected-input verification](../../docs/implementation/story-4-17-custody/unchanged-inputs.json) confirms parent status/qualification inputs and guarded Story 4.0 helper unchanged. Platform 4.17/EventStore 3.21 remain in-progress; five guarantees REJECTED/open, technical FAIL (20/24), selection null, production FAIL. Engineering, staging and Subscription prerequisites remain open. No qualification, commit or push occurred.

- Post-review preservation: exact executed upload source is retained as [transfer_once.executed.py](../../docs/implementation/story-4-17-custody/transfer_once.executed.py), SHA-256 `19cb51fdf00db95a1d25080c7d88cc0997a1640e6ca693e2b9b6697f22be0c11`. Local hardening does not authorize or replay any upload; original inspection, preflight, test reports and custody receipt remain historical evidence.

## Review completion

- All three workflow review layers returned; all 13 findings were triaged individually. Ten finding rows were corrected (B1/E2 share the journal root cause), two low-value expansions rejected and one caller-unreachable claim refuted. No work was newly deferred.
- Parent independently confirmed the pinned SDK redirect behavior offline, inspected the local hardening diff and reran 17 transfer controls, 25 original verifier controls through fresh reports and seven supplemental controls: **49/49 PASS**. The literal inventory procedure also passed against independent downloads after corrections. [Final checks](../../docs/implementation/story-4-17-custody/post-review-checks.json) preserve evidence hashes and actual results.
- B5's manifest scan passed. B6's version-specific ACL API was denied; unchanged five-version inventories bracketed five owner-only current-object ACL observations. [Supplement](../../docs/implementation/story-4-17-custody/acl-current-version-checks.json) records current privacy by inference, not a successful version-bound ACL response or continuous guarantee. No grant changed.
- Exact executed upload source, original inspection/plan/reports and accepted custody receipt remain unchanged. New transfer hardening is unexecuted and has no new execution approval. [Review notes](../../docs/implementation/story-4-17-custody/post-review-verification.md) correct historical HTTP-send/durability claims and document the Administrator checkpoint.
- Only this preservation spec is done. The approved frozen constraints override generic finish instructions to move the parent story to review or create a local commit: both parents/status files remain unchanged and no Git mutation is performed.
