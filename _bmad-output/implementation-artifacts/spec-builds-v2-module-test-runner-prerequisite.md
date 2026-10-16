---
title: 'Builds v2 module-owned test runner prerequisite'
type: 'feature'
created: '2026-10-16'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'b2890d20575356239555df5b113e3100c1d594c1'
context:
  - '/home/administrator/projects/hexalith/platform/references/Hexalith.Builds/AGENTS.md'
  - '/home/administrator/projects/hexalith/platform/references/Hexalith.Builds/DEVELOPMENT.md'
---

<frozen-after-approval reason="explicit implementation authorization in user request">

## Intent

Implement the existing Builds runner prerequisite for Memories 27.4: validated module-owned v2 test selection and execution through declaration, command, plan, executor, handoff, state and evidence. Current v2 Test always refuses HXR028/HXC002; readiness cannot substitute for test completion. Support an actual Python entry alongside existing native .NET tests, without a replacement runner or Memories descriptor schema.

## Boundaries & Constraints

Selected workspace is standalone Platform. All runner edits belong in its directly declared `references/Hexalith.Builds`, currently HEAD `63dda8fc14882aba383888afd7e6505afab1f345`. Follow that repository's guidance and Platform's permitted AI.Tools baseline, never load any skill from references. Preserve Platform's extensive independent work and Memories' files/gitlinks. No workload/AppHost/container/daemon starts, target contact, dependency updates, staging, commits, publication, Production or Story27.4/A41/sprint transitions. Offline tests may use isolated local process doubles. User explicitly authorizes implementation despite independent work; no additional approval checkpoint is needed.

Read Memories' current Spec14 and canonical report as requirements context, especially their current 2026-10-16 sections. Platform CAP4/AD10 require runner ownership, ready-only execution, finite timing, truthful outcomes, local failure retention and resource cleanup reporting. Existing schema identities should be extended compatibly in Builds; no new descriptor schema in Memories. Actual PostgreSQL composition, tool pin, capability/clock binding, direct candidate alignment and Memories adapter are subsequent integration work, not accepting stubs here.

Selection must come from the selected module's validated v2 declaration through existing `--profile`; undeclared/ambiguous/invalid selections refuse before startup. Add a closed, named test contract to the existing schema/semantic validator. Validate executor, owned existing entry path, format, explicit finite lifetime, duplicate names, symlinks/escape, secret inputs and mode. No shell or caller-supplied executable/argument list. Python uses fixed python3 with an owned .py entry and the existing strict native TRX report contract; zero exit alone cannot pass. Existing .NET test executors and limits remain supported.

Allow explicitly selected execution up to 14,400 seconds from the initial request, sufficient for real 3,600-second retention, 900-second purge grace and setup/observation overhead. Startup/setup consume that budget; readiness still has its independent default600 deadline. Keep separate stopping/diagnostic bound up to 900 seconds and preserve unrelated legacy test900 limit. Bind effective lifetime, absolute execution deadline and matching credential expiry to the selected plan/handoff/evidence. Do not extend or reset lifetime on retry. Credentials remain memory/child environment only and must never enter retained evidence.

Extend existing ownership, observation and outcome records with run/owner identity, selection/source hashes, mapping/candidate provenance, resource/readiness observations, execution expiry and resource disposition. Distinguish declared/expected identities from actual observations; absent loaded backend/candidate evidence must never be invented or earn qualification credit. Reject changed manifest/test bytes before test execution. Fresh owned runs only; unsupported attachment/hosted reuse must refuse, never imply attachment support.

Record first terminal outcome immediately. Local selected-test/startup failure retains surviving resources and diagnostics until explicit down; cancellation must not erase an earlier failure. CI and successful tests clean only owned resources. Explicit stop/cancellation yields withdrawn rather than passing evidence. Make cleanup finish before result/evidence is rendered, propagate failure/remaining resources, preserve first causal failure, and never turn an unavailable scan or exception into complete cleanup. Retained environments must survive parent exit, including AppHostRunner startup failure handling and StopSignal EOF. Durable diagnostics/results and safe failed reports remain after successful cleanup; secret-bearing reports remain rejected and unretained. Extend manual evidence serialization/validation too, not just record properties.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior |
| --- | --- | --- |
| Valid selection | Owned declared test, ready session, zero process exit and strict passing report | Execute once; bind counts/report/hash/selection; apply cleanup policy before evidence |
| Invalid selection | Missing, unknown, duplicate, bad lifetime/executor/path/mode or filtered unsupported request | Refuse before startup/process/target |
| Long observation | Explicit 14,400s execution, 900s stopping | Separate finite deadlines; credential covers declared work; legacy/startup limits unchanged |
| Changed source | Selected manifest or entry differs after readiness | Refuse invocation; retain local failed environment |
| Failure | Nonzero, timeout, missing/failed/skipped/secret report, partial startup | Never pass; local retention, CI cleanup, preserved diagnostics |
| Withdrawal | Owner cancellation/explicit stop | Stop owned execution; withdrawn outcome; preserve earlier failure |
| Cleanup incomplete | Exception, unavailable scan, remaining resource | Nonpassing final result with truthful remaining/unknown disposition |

</frozen-after-approval>

## Code Map

- Builds `schemas/hexalith.module-manifest.v2.json`, `Manifest/PlatformManifestValidator.cs`: closed v2 declaration and semantic validation; `ManifestPathValidator`/`RepositoryPathResolver` already enforce physical containment.
- `Runtime/ModuleCommandExecutionService.cs:ExecutePlatformAsync`: blanket refusals and currently ignored finally cleanup; `--profile` is already required by Test CLI.
- `Runtime/NativeTestExecutor.cs`, `PersistedProfileNativeTests.cs`, `NativeTestHandoff.cs`, `TestReports/NativeTestReportLoader.cs`: reuse fixed execution/report evaluation and secret rejection. SourceMapping.ContainsProject accepts mapped existing files but additionally enforce module ownership.
- `Runtime/CompositionEngine.cs`, options, run plan/state/readiness/session and state store; AppHost StopSignal/AppHostRunner/RunReadiness: bind selected contract, retained process survival, local startup failure retention and actual observation metadata. HXR028 also names stdin stop failure; preserve that meaning.
- `RunEvidence/ModuleRunEvidence*`, existing schemas/writer/validator and native report retention: extend coherently; no fabricated observations.
- `test/Hexalith.Builds.Module.Tests`: existing PlatformManifestCommand/Validation, NativeTestExecutor, CompositionEngine/State, evidence suites supply offline patterns and overrides.

## Tasks & Acceptance

- [x] Extend validated v2 test declaration and pre-start selection on existing command path.
- [x] Execute module-owned Python/native tests with bounded chosen lifetime, matching credentials, source binding and existing strict reports.
- [x] Extend existing plan/handoff/readiness/state/evidence for ownership, observations, terminal outcome and truthful retention/cleanup.
- [x] Verify matrix with focused offline tests and existing affected regressions; no services or dependency restore.
- [x] Document supported declaration/report contract and exact limitations before PostgreSQL integration.

Given invalid input, when Test is requested, then no startup occurs. Given a ready supported test, when executed, then the real process and strict report determine outcome. Given failure or cleanup uncertainty, when results are retained, then they disclose retention/leftovers and never claim completed cleanup. Given legacy tests, when no new lifetime is selected, then their limits remain unchanged.

## Implementation Notes

Implemented in Platform's direct Builds checkout, preserving its original HEAD. The optional closed `module.tests` collection extends the existing v2 declaration. Python uses a fixed `python3 -E -s -B` vector and the existing strict TRX contract. Execution is limited by both the original UTC deadline and a monotonic cancellation timer. Development credentials expire at the execution deadline; separate stopping uses runner ownership without module credentials.

Local failures retain resources, process identity and safe diagnostics; CI and successes perform cleanup before output. First outcomes use a per-run file lock, and explicit down records withdrawal before terminating execution while preserving earlier failures. Existing plan/readiness/mapping documents are exposed in the handoff and copied into safe durable diagnostics. Observed candidate/backend fields remain absent and qualification credit is false. Unimplemented attachment/hosted reuse refuses.

Offline verification: Debug `--no-restore` Module.Tests build succeeded with zero warnings/errors; focused classes passed 293 tests in 9.475s and exact affected legacy methods passed 28 in 1.920s, with zero errors/failures/skips/not-run. Exact argv/results are in `/tmp/builds-selected-final-results.json`, logs in `/tmp/builds-selected-final-{0,1}.log`, and tested source inventory in `/tmp/builds-selected-tested-source.json`. The review diff includes all Builds changes and untracked files, excluding independent Platform work: `/tmp/builds-v2-runner-y6sio70q/runner.diff`, SHA-256 `cd2b894c77a98bc80abad9d3c6c746c8cb26fae418ae5d0cf8df89c285eaaa11`. No staging was used. Platform preservation baseline contains hashes of 45 independent files in `/tmp/builds-v2-runner-y6sio70q/preservation-before.json`.

## Spec Change Log

## Review Triage Log

Three configured independent reviewers examined the complete owned Builds diff. All findings below were checked individually before grouping. Corrections stay within the explicitly authorized runner behavior; no specification renegotiation, staging, full-code reversion, parent-story transition or workload start is authorized or needed.

| Finding | Verdict | Route / verified evidence |
| --- | --- | --- |
| BH1 | high | patch: mapping resolution precedes the selected cancellation source and uses the owner token; setup can exceed the declared lifetime. |
| BH2 | high | patch: process cancellation throws before native report inspection, leaving credential-bearing files in retained workspaces. |
| BH3 | high | patch: the initial outcome write in DownCore and startup failure handling can throw before teardown. Diagnostic failure must not prevent owned cleanup. |
| BH4 | high | patch: stopping outcome writes use None and independent five-second lock waits outside the selected total bound. |
| BH5 | high | patch: exact-ID down still dispatches and hashes through a readable recognizable manifest, so deletion or invalid JSON prevents cleanup. |
| BH6 | medium | patch: flooring the execution deadline can remove nearly one second from a one-second declared lifetime. Separate precise execution timing from JWT second precision. |
| BH7 | high | patch: a late cancellation can return withdrawn after durable completed was written. Resolve cancellation before the first terminal commit and preserve that terminal outcome consistently thereafter. |
| BH8 | high | patch: completed artifacts can contain a selected terminalStatus of withdrawn with a zero outcome; reject that contradiction. |
| BH9 | high | patch: selected manifest/profile/report/count bindings are not cross-checked against the enclosing evidence. |
| BH10 | high | patch: UTF-8-only raw credential detection can miss UTF-16 content before XML redaction removes it. |
| BH11 | medium | patch: references ownership exclusion uses case-sensitive comparison and can admit a case variant on insensitive filesystems. |
| EC1 | high | patch: malformed UTF-16 output can bypass detection and retain raw bytes when redaction returns null. Shared report-safety correction with BH2/BH10. |
| EC2 | high | patch: same demonstrated setup timer gap as BH1. |
| EC3 | high | patch: same stopping-budget gap as BH4. |
| EC4 | high | patch: existing integer counter aggregation can wrap failures to zero; the new accepting selected-test route exposes this strict-report gap. Use checked/widened counter arithmetic and a rejection test. |
| VG1 | medium | patch: Python timeout occurs only after immediate startup; add delayed-startup coverage that observes cancellation before any native child. |
| VG2 | medium | patch with explicit verification limit: the sleeping Python process and EOF tests bypass AppHostRunner's retention wait. Exercise its actual retention/shutdown orchestration through an offline injectable lifecycle seam. The proposed real AppHost subprocess cannot run under the user's explicit no-workload hold; real orchestration integration remains a later check. |
| VG3 | high | patch: selected-record validation has no mismatched run/owner mutation coverage; add targeted artifact negatives. |
| P1 | high | patch: selected report reading/redaction follows report paths and uses unbounded uncancellable reads; reject symlinks/non-regular or oversized reports before reading and never rewrite a foreign target. Bound selected report processing and retain legacy limits. |
| P2 | medium | patch: retained console marker output has no size cap and suppresses validation diagnostics before --validate-platform returns; bound it and preserve safe preflight diagnostics. |

Shared roots: BH1/EC2/VG1; BH2/BH10/EC1/P1; BH3/BH4/EC3; BH8/BH9/VG3. Each individual finding remains recorded above. The remaining findings have distinct causes. No unrelated repository work is included.

## Verification

Build the individual Module.Tests project in Debug with `--no-restore`, then invoke its built xUnit assembly with focused `-class` selectors. Use process doubles only. Run `git diff --check` in Builds and inspect the complete owned diff including new files without staging. Record exact commands/results and any environmental blockers separately.
