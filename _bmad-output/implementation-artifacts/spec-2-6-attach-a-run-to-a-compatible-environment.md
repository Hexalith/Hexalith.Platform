---
title: '2.6 Attach a run to a compatible environment'
type: 'feature'
created: '2026-10-28'
status: 'done'
baseline_commit: '242b4d7297820495b91c5d7719dd6b1c892c978a'
builds_baseline_commit: 'e129a29e6c6e2f8a200581e09b861d75a9f3a7b8'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
  - '{project-root}/references/Hexalith.Builds/docs/contracts/candidate-and-attachment-policy-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A local or CI run-owned environment cannot be reused. Story 2.5 listings hardcode `AttachedRunIds=[]`, `HoldDeadlineUtc=null` and `unavailable-admission-unsupported`, and cleanup never waits for anyone else.

**Approach:** Add explicit attach admission to the Builds module tool under policy `hexalith.platform.candidate-and-attachment-policy.v1`. Admission applies compatibility, hold and owner-state checks. Accepted attachments are persisted in run state with absolute deadlines. Listings and result attachments report them. Owner success-cleanup waits for live holds, and ending one attached run leaves the owner running.

## Boundaries & Constraints

**Always:**
- Hold: omitted means 1800 s. Explicit whole seconds 1–14400 are accepted exactly. Anything else (empty, 0, negative, fractional, >14400) is refused with no clamping or defaulting.
- Record the original request or omission, acceptance UTC time, effective seconds and absolute expiry. Retries presenting the issued `originalAttachmentRequestId` keep the first acceptance and expiry. Unknown ids and post-expiry retries are refused.
- Accept only local or CI run-owned environments with compatible composition, mode, artifact identities, readiness and data isolation. Refuse hosted, developer-started and cross-job environments, each with a stable reason.
- A finished owner refuses new attaches, except a ready local failure-retained environment that is not stopped, cleaning or cleaned.
- Owner success-cleanup waits until every attachment has ended or passed its deadline. Cleanup stays exact-owner, bounded and retryable. First outcomes are never overwritten, and `outcome.lock` semantics are kept.
- Ending an attached run affects only that attachment.
- Keep the 2.5 listing honesty rules. Legacy state shows unknowns, and one invalid entry does not hide healthy ones.
- New refusal diagnostics start at `HXR032`.

**Never:**
- Change the policy id or the reader's hold invariants, edit policy doc v1, or change the v1 contract's meaning.
- Build withdrawal ordering, hold-expiry `withdrawn` outcomes or failure-retention interplay (Story 2.7), CI workflow or job-bound changes (2.8), fixture migration (2.9), candidate acceptance, or publication and pin changes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Accept, default hold | Running compatible local owner, no hold | Accepted, 1800 s, expiry recorded, shown in `list` | N/A |
| Accept, explicit hold | `--hold-seconds 14400` | Accepted exactly | N/A |
| Invalid hold | `0`, `14401`, `1.5`, empty | Refused, nothing recorded | `HXR032` usage refusal |
| Hosted or incompatible | Hosted target, or mode, identity, readiness or isolation mismatch | Refused with the reason | Stable `HXR` reason |
| Owner finished | Owner passed, or stopped or cleaned | Refused | Reason names the owner state |
| Failure-retained | Local failed or cancelled owner, still ready | Accepted | N/A |
| Retry | Issued request id | First acceptance and expiry returned | Unknown id or after expiry: refused |
| Owner finishes | Live attachments | Cleanup deferred, listed as pending cleanup, runs after the last end or deadline | Failed cleanup is reported and retryable without extending holds |
| Attached cancelled | One of N attachments cancelled | Only it ends, owner keeps running | N/A |

</frozen-after-approval>

## Code Map

Paths are under `references/Hexalith.Builds` (B), with `R = B/src/libraries/Hexalith.Builds.Tooling/Runtime`. Builds sits at gitlink `e129a29e`, so the work is a new Builds commit plus a Platform gitlink bump.

- `R/CompositionRunState.cs` and `R/CompositionRunStateStore.cs` -- run-state record (`hexalith.g4-run-state.v1`) and validators. Add attachment records or a sibling store file. Keep the schema string compatible and validate new data.
- `R/CompositionRunListing.cs` and `R/CompositionRunListingEntry.cs` -- replace the hardcoded placeholders with real attachments, deadlines and pending cleanup.
- `R/ModuleCommandExecutionService.cs` -- add attach admission. Make success-cleanup wait for holds. Refusal templates are near lines 270–280 and 699–711.
- `R/CompositionEngine.cs` -- teardown and first-outcome recording under `outcome.lock`. Reuse it for deferred cleanup. Do not alter its outcome semantics.
- `R/CompositionRunResourceScanner.cs`, `R/CompositionResourceReadiness.cs`, `R/ModuleTestRunRecord.cs` -- compatibility and readiness facts, plus the failure-retained flag.
- `B/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs` -- add an `attach` subcommand beside `list` (line 113), with `--hold-seconds`, `--attachment-request-id` and `--output human|json`. Add an end-attachment option that is scoped to that attachment only.
- `B/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestResultWriter.cs:273` and `TestEnvironmentDescriptorWriter.cs:122` -- write the populated `holds`. The reader's existing invariants already validate them. Do not weaken them.
- `B/README.md` (lines ~233–252, ~512) and `docs/contracts/test-environment-v1.md` (~114, ~316) -- replace "attachments are empty / unsupported" text.
- Tests, in `B/test/Hexalith.Builds.Module.Tests/`: extend `CompositionRunListingTests.cs`, `LocalRunLifecycleTests.cs`, `TestResultWriterTests.cs` and `TestEnvironmentContractReaderTests.cs`, and add `AttachAdmissionTests.cs`. Run them directly, not via the slnx. See memory notes: use `-nodeReuse:false` and `-class` filters, and mutate only in a worktree.

## Tasks & Acceptance

**Execution:**
- [x] `R/` attachment model, store and admission service -- hold validation, compatibility and owner-state checks, idempotent retry, persistence -- AC1–3
- [x] `R/CompositionRunListing*.cs` -- populate attached run ids, deadlines, pending cleanup -- AC1
- [x] `R/ModuleCommandExecutionService.cs` and `R/CompositionEngine.cs` -- defer success-cleanup while holds are live and run it when the last one ends. Attached end touches only its own record -- AC4–5
- [x] `Module.Cli/ModuleCommandApplication.cs` -- `attach` command with exit codes and diagnostics -- AC1–2
- [x] `TestEnvironment/*Writer.cs` -- emit holds in result and descriptor -- AC1
- [x] Tests for every matrix row, plus docs and README updates, and `evidence/2-6/` notes

**Acceptance Criteria:**
- Given an explicit attach request to a compatible local or CI run-owned environment, when evaluated, then it is accepted and recorded with its deadline, and `list` shows it.
- Given a hosted or incompatible target, when evaluated, then it is refused with the reason.
- Given a finished owner, when a new attach is requested, then it is refused unless the local environment is failure-retained and attachable.
- Given an accepted attachment and a finished owner, then cleanup waits for every attachment to end or hit its deadline.
- Given a cancelled attached run, then only it ends and the owner keeps running.

## Implementation Notes

## Spec Change Log

## Review Triage Log

- patch (medium): retry skipped context/owner-state checks -- fixed in 4ebd4d8.
- patch (medium): failure-retained exception not local-only; listing pending for CI owners -- fixed.
- patch (medium): corrupt attachments.json escaped catches; expired end time; `--end` accepted admit-only options; owner recheck under lock -- fixed.
- patch (medium): verification gaps (descriptor holds, wait cancel/corrupt, CLI combos, CI attempt/run/repo, parallel admission) -- tests added.
- patch (medium): stale contract paragraph, HXR036 undocumented -- fixed.
- defer (low): artifact-identity matching unverifiable until Epic 3 -- documented, ledger entry.
- false: `--target` self-declared (hosted cannot be detected locally; by design); owner liveness and isolation-evidence strength (not in intent); Story 5.4 files in diff (not part of this change).

## Design Notes

Decision (not user-visible): admission and the end of an attachment are separate `hexalith-module` subcommands, not flags on `test`. The reason is that 2.6 only admits and records, and running tests against an attached environment is driven by the fixtures in 2.9. Deadline expiry is evaluated against UTC at read and cleanup time, and `withdrawn` outcomes stay with 2.7.

## Verification

**Commands:**
- `dotnet build -nodeReuse:false` on the Builds module test project -- expected: 0 warnings, 0 errors
- `dotnet exec` of the Module.Tests Debug DLL with the affected class filters -- expected: all pass

### Review Findings

Code review 2026-10-29 (Builds `e129a29..4ebd4d8`; layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor).

- [x] [Review][Decision] CI owners accept and list holds that are never honored — Admission accepts and persists holds for CI owners and the listing shows their `AttachedRunIds`/`HoldDeadlineUtc`, but `ModuleCommandExecutionService` waits only for local owners (`!ci`), and the docs say "CI owners do not wait for holds". Spec accepts "local or CI" owners and the "Owner finishes" row does not exclude CI. Options: (a) refuse attach to CI owners with a stable HXR035 reason until 2.7/2.8; (b) keep as is and state in the contract that CI holds are advisory; (c) implement the CI wait now. [CompositionAttachAdmission.cs, ModuleCommandExecutionService.cs:1343] (medium; acceptance-auditor+edge-case-hunter) — resolved: refuse attach to CI owners (`HXR035` `owner-ci-holds-unsupported`).
- [x] [Review][Decision] Deferred cleanup lives only in the owner's `test` process — The hold wait runs in-process for up to 14400 s on the command's cancellation token. If the process is killed or the wait is cancelled, no marker records that cleanup is owed: after holds expire the owner lists `not-requested` and only a manual `down` cleans up, and a cancelled wait emits no diagnostic. Spec row says cleanup is "listed as pending cleanup, runs after the last end or deadline". Options: (a) accept and add a cancel-time diagnostic plus doc note; (b) persist a deferred-cleanup marker so listing stays `pending` and `down`/next run finishes it. [ModuleCommandExecutionService.cs:1343, CompositionRunListing.cs] (medium; acceptance-auditor+blind-hunter+edge-case-hunter) — resolved: keep the in-process wait, add an `HXT008` diagnostic when the wait is cancelled, document that `down` retries.
- [x] [Review][Patch] Add tests that CI and failed/cancelled owners clean up without waiting for a live hold [AttachAdmissionTests.cs OwnerRun harness] (medium; verification-gap) — `!ci` or the `ExitCode.Success` condition can be dropped with no test failing.
- [x] [Review][Patch] Add human-output assertions for `attach` and `list` with a live hold (`attached-run=`, `attached=`, `expires=`, non-`unavailable` `hold-deadline`) [AttachAdmissionTests.cs, CompositionRunListingTests.cs] (low; verification-gap)
- [x] [Review][Patch] Narrow the contract claim that results and descriptors "publish the populated holds": the descriptor is written at start (always empty) and the result is a point-in-time read at write [docs/contracts/test-environment-v1.md, README.md] (low; blind-hunter+edge-case-hunter)
- [x] [Review][Defer] Explicit `down`/cleanup while holds are live leaves them `live` and never writes `EndReason: cleanup` [ModuleCommandExecutionService.cs] — deferred: maybe-false (medium, unverified); check whether the listing/`IsLive` treats a cleaned owner's holds as live and whether 2.7/2.8 withdrawal ordering covers it.
- [x] [Review][Defer] Attach racing the start of cleanup: the under-lock owner recheck reads `CleanupDisposition`, but cleanup start never takes `attachments.lock` [CompositionAttachAdmission.cs:~140] — deferred: maybe-false (medium, unverified); settle by checking when `CleanupDisposition` becomes `in-progress` relative to `WaitForNoLiveHoldsAsync` returning.
- [x] [Review][Defer] Corrupt `attachments.json` makes `ReadContractBinding` throw `InvalidDataException` inside result writing [TestResultWriter.cs:~45] — deferred: maybe-false (medium, unverified); check that the caller catches it and still retains the first terminal result.

#### Rejected

- `false`: Listing says `none` for corrupt attachment records — the read failure adds the owner to `unverifiableOwners`, so the whole listing returns the failed/incomplete result.
- `false`: Empty CI identity strings compare equal — `ExecutionContext()` throws on incomplete CI identity, and the recorded context comes from the same writer.
- `false`: `workflowAttempt` `1` vs `1.0` raw-text mismatch — the value is runner-written from an integer; unreachable.
- low: Retry after the owner finished is refused (HXR035) — deliberate fix in 4ebd4d8 (retry runs context and owner-state checks first); recorded in the triage log.
- low: Retry ignores differing mode/composition/hold — idempotent return of the first acceptance is safe; adding comparisons is extra branching.
- low: `attach --end` has no same-context check — ids are runner-issued and unguessable on a local tool; adding it needs an owner read.
- low: `EndAsync` hardcodes `terminal-outcome`; unused `IsConsistent` reasons — owned by 2.7/2.8.
- low: Human listing `state=recorded` for expired holds; leading-zero hold values; HXR033 reused for lock timeouts; `EndedAtUtc <= ExpiresAtUtc` not enforced; cancellation not mapped to HXR033; non-string `kind` throws `InvalidOperationException`; per-owner attachment count unbounded; missing `LocalRunLifecycleTests` edits — negligible in everyday use, and the fixes add guards or parameters.
- low: Artifact-identity matching unverifiable — already deferred to Epic 3 (ledger entry exists); `--target` self-declared — by design (see triage log).
- Reject: Evidence README lacks run counts, and the Platform gitlink bump is outside this diff — process-only.
