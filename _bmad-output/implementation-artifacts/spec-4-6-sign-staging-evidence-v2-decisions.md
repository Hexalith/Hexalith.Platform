---
title: '4.6 Sign staging-evidence v2 decisions and attempts'
type: 'feature'
created: '2026-11-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '66a79eea615dbf6e19ba62cc80cb2caa36c1f6d6'
builds_baseline_commit: '2d9ef8b454ff897daca0149f3549844f584b634a'
builds_implementation_commit: '4adecfe2af4758aa66039a1221de20774ca56e87'
story_key: '4-6-encode-release-attempt-qualification-and-administrator-recor'
context:
  - '{project-root}/docs/contracts/staging-evidence-policy-v2.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Staging-evidence policy v2 adds pending-decision actions that need authenticated records.

Extend Builds' signed record contracts so Story 4.7 can verify the authority and exact inputs of each conditional decision action.

## Boundaries & Constraints

Sign the action, exact policy ID, decision ID, retained release ID and digest, and accountable writer and key. Bind production actions to the exact production attempt revision and epoch. Bind staging supersession to the initiating staging attempt and its own epoch. V2 attempts sign their policy and decision binding. Require explicit v2 verification and preserve the current reviewed v1 schemas and canonical bytes. Use independent writer enrollment and metadata-only failures.

Limit this change to encoding, signature verification and checks against supplied verified context. Story 4.7 owns CAS, durable revisions, replay, ordering and reconciliation. Later consumers own live eligibility, fencing, policy activation, evidence qualification and Administrator reservation lifecycle.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Error handling |
| --- | --- | --- | --- |
| Admission reservation | In-progress production release attempt before mutation | Only a production-scoped Platform promotion evaluator can sign it | Refuse wrong scope, attempt, release, decision or epoch |
| Explicit reservation | Administrator reserves a pending decision | Require exclusive Administrator custody and a separate signed action with decision and release | Refuse executor, evaluator, deputy and any attempt or epoch fields |
| Supersession | Staging start or prerequisite reset targets a pending decision | The authorized staging writer binds that decision and the triggering staging attempt, which may concern another release | Refuse production scope and stale staging epoch |
| Completion or release | Exact reservation and authoritative terminal production attempt | Its writer signs completion with a matching working pointer, or release with a non-working outcome | Refuse mismatched reservation or pointer, old epoch and superseded or uncertain outcome alone |
| Version or payload confusion | Missing policy, v1/v2 mismatch, tampered binding or extra field | Only the selected closed schema supplies verified content | Fail closed without fallback |

</frozen-after-approval>

## Code Map

Paths below are relative to `references/Hexalith.Builds`.

- `src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/ReleaseRecordCodec.cs` handles bounded strict JSON, canonical ES256 signing and verified wrappers. `RecordReference` binds IDs and content digests.
- `src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTrustPolicy.cs` resolves kind, environment and role against independent enrollment. `RecordTransitionValidator.cs` resolves verified references and attempt revisions.
- `test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordTestContext.cs` supplies ephemeral keys. `RecordSignatureTests.cs` pins v1 canonical bytes.

## Tasks & Acceptance

**Execution**

- [x] `schemas/hexalith.release-records.v2.json` and `src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj` — add and embed closed schemas for v2 attempts and decision actions.
- [x] `src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/{AttemptRecord,StagingEvidenceBinding,PromotionDecisionRecord,ReleaseRecordCodec}.cs` — add typed signed fields and explicit v2 selection. Omit the v2-only attempt binding from v1 serialization.
- [x] `src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/{RecordTrustPolicy,RecordWriterKey,RecordTransitionValidator}.cs` — enforce the action writer matrix, immutable attempt bindings and exact verified references and epochs. Add an explicit promotion-evaluator role. Keep verified inputs immutable.
- [x] `test/Hexalith.Builds.Module.Tests/ReleaseRecords/PromotionDecisionTests.cs` — exercise the matrix with real keys. Isolate signature changes from schema and trust failures. Verify version refusal in both directions.
- [x] `ReleaseRecords.md` — document versions, action writers, policy source pin and the CAS handoff.

**Acceptance criteria**

- Given separately enrolled writers, when a v2 action is verified and checked against authoritative records supplied by the caller, then it establishes only the action and references its writer is entitled to sign.
- Given existing v1 callers and reviewed v1 records, when they sign, encode, verify or validate a transition, then their existing APIs and behavior are preserved.

## Implementation Notes

Initial Debug compilation passed with zero warnings or errors. All 240 ReleaseRecords cases ran and passed, including 32 v2 cases and all 208 existing cases. The v2 cases cover enrolled writer identity, exact signed references and epochs, Administrator separation, both staging supersession paths, completion and release, and schema version refusal. [Build log](evidence/story-4-6-staging-evidence-v2/module-build.log) · [Per-case results](evidence/story-4-6-staging-evidence-v2/release-records-tests.xml)

After review, Debug compilation passed with zero warnings or errors. All 254 ReleaseRecords cases passed, including 46 v2 cases. The final verification used local source copies with Git metadata, the prebuilt Platform composition model, and the local MSBuild version override `6.6.2` within the fixture's supported range. All 31 release-record input hashes in the test checkout matched the workspace. [Build log](evidence/story-4-6-staging-evidence-v2/reviewed-build.log) · [Per-case results](evidence/story-4-6-staging-evidence-v2/reviewed-release-records.xml) · [Source hashes](evidence/story-4-6-staging-evidence-v2/reviewed-source-sha256.txt)

The complete module suite reported 3,285 passed, zero failed and one skipped. The skipped qualification case requires the controlled catalog SDK interception harness. The routing-catalog integration fixture passed. The disposable checkout was removed after the suite exited, and both workspaces passed their whitespace checks. [Module results](evidence/story-4-6-staging-evidence-v2/reviewed-module-tests.xml) · [Module log](evidence/story-4-6-staging-evidence-v2/reviewed-module-tests.log)

The local Builds commit includes the reviewed v1 changes used by the v2 attempt schema. Builds' pinned commitlint accepted the exact message with zero warnings or errors. [Commit validation](evidence/story-4-6-staging-evidence-v2/commitlint-builds.log)

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence and resolution |
| --- | --- | --- |
| Blind 1 | medium | A signed terminal revision with a different check timeout passed closure while ordinary attempt validation refused it. Reuse the immutable payload comparison. |
| Blind 2 | medium | A signed staging attempt with a different subject and the target release record ID passed after changing the digest. Require distinct release record IDs and document the authoritative trigger check. |
| Blind 3 | medium | A verified staging attempt for a second release could reuse the target decision ID. Require distinct decision IDs for different releases. |
| Blind 4 | medium | A recovery-only key under the same writer identity as the ordinary release executor passed closure. Require `executor` on the closing key. |
| Blind 5 | medium | The available signed fields cannot distinguish a prerequisite reset from another in-place restore. Make the prerequisite relationship an explicit obligation at the 4.7 CAS handoff. |
| Blind 6 | low | Closure verifies the pointer's bindings without a preceding pointer. Document which pointer evidence 4.7 must compare at commit and reconciliation. |
| Blind 7 | medium | The action tests cover valid shapes and Administrator field refusal. Add isolated negative wire cases for missing and forbidden fields on the other actions. |
| Blind 8 | medium | Successful closure tests use the admission epoch. Verify closure after a same-attempt resumption at a current higher epoch and refuse the earlier epoch. |
| Blind 9 | medium | Revision tests did not exercise the linked takeover path. Add linked v2 and cross-version v1 predecessor coverage and refuse a changed predecessor decision or a v1 successor to a v2 predecessor. |
| Blind 10 | low | Round trips could miss a change in v2 canonical bytes and omitted fields. Exercise the public signing-bytes API with a pinned vector and an independent signature. |
| Edge 1 | medium | The same-schema successor guard prevented a v1 attempt's terminal revision from remaining v1 with an authorized v2 successor. Preserve the predecessor wire version and require v2 successor evidence for v2 predecessors. |
| Edge 2 | medium | Writer ID equality admits a distinct recovery-only key enrolled for the same identity. Require the ordinary executor role on the closing key while allowing enrolled executor key rotation. |
| Verification 1 | medium | The stale-epoch tests invalidated both the action and attempt. Sign a current action that references a stale attempt and expect refusal. |
| Verification 2 | medium | Existing changed attempts also failed eligibility. Substitute an otherwise eligible same-ID revision with a different content digest in admission and closure. |
| Verification 3 | medium | The current tests cannot distinguish `>=` from equality on the admitted epoch. Exercise successful higher-epoch completion and release. |
| Verification 4 | medium | The v2 overload of `GetSigningBytes` was bypassed by the existing round trips. Verify a signature generated externally over its output. |
| Verification 5 | medium | Missing working pointers were tested at transition time only. Exercise schema rejection on the verification entry point. |

## Design Notes

The source is Platform revision `e899cf23dda711fbd85db9ec2b87365d9453ab60`, with policy SHA-256 `cfff4df7a1548a38274841aa983f8b851e4709c264b7aa24292702ad4fa4f0c6` and the inherited v1 pin recorded in the policy. A terminal attempt has different content from its admitted revision. Validate the exact reservation and terminal references and compare attempt lineage and epoch separately.

## Verification

- Build the Debug `test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj` with `-m:1` in a `run-with-temp` workspace. Run the built test assembly's ReleaseRecords cases, then the module suite. Retain results outside `TMPDIR`.
- Run `git diff --check` in Builds and Platform.
