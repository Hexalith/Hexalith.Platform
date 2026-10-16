---
title: 'Builds v2 module-owned test runner prerequisite'
type: 'feature'
created: '2026-10-16'
status: 'done'
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

Initial pre-review offline verification: Debug `--no-restore` Module.Tests build succeeded with zero warnings/errors; focused classes passed 293 tests in 9.475s and exact affected legacy methods passed 28 in 1.920s, with zero errors/failures/skips/not-run. Exact argv/results are in `/tmp/builds-selected-final-results.json`, logs in `/tmp/builds-selected-final-{0,1}.log`, and tested source inventory in `/tmp/builds-selected-tested-source.json`. The review diff includes all Builds changes and untracked files, excluding independent Platform work: `/tmp/builds-v2-runner-y6sio70q/runner.diff`, SHA-256 `cd2b894c77a98bc80abad9d3c6c746c8cb26fae418ae5d0cf8df89c285eaaa11`. No staging was used. Platform preservation baseline contains hashes of 45 independent files in `/tmp/builds-v2-runner-y6sio70q/preservation-before.json`.

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


## Final Correction Disposition

All individually triaged findings above are corrected in the authorized runner scope. Setup uses the original monotonic budget before mapping/startup; precise execution expiry and JWT expiry differ only by necessary sub-second JWT rounding. Selected process shutdown, encoded report inspection, startup-timeout observations, terminal bookkeeping and teardown share the first stopping timestamp and allowance. Diagnostic/lock failures cannot prevent an owned inert process from being stopped. Exact-ID down survives missing/corrupt declarations. First outcomes remain consistent across returned, durable and embedded results; evidence cross-binds ownership, source, profile, report hashes and counts. Overflow, mixed-case dependency ownership, report links/special/oversized files, XML encodings/comments/processing instructions, output caps and safe preflight diagnostics have targeted regressions.

The final startup-timeout correction remains within BH4/EC3: the actual readiness-deadline tail now bounds observation and reuses that bound in teardown, preserving HXR021 on observation I/O/cancellation failures and disclosing HXT008. Its four observed/I/O/timeout/legacy cases use an inert tagged process through the existing lifecycle method. The actual AppHost retention/shutdown orchestration is exercised with offline delegates. Real Aspire/PostgreSQL orchestration was deliberately not executed and remains an integration verification limit, rather than live evidence or deferred acceptance credit.

## Final Source-bound Verification

Parent verification ran from Platform's directly declared Builds repository after the final source freeze:

- `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj --configuration Debug --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`: exit 0; zero warnings/errors, MSBuild elapsed 52.78 seconds.
- Built xUnit assembly with 11 explicit affected class selectors: **340 passed**, 18.953 seconds; zero errors/failures/skips/not-run.
- Same assembly with 16 explicit safe legacy/startup method selectors: **30 passed**, 2.882 seconds; zero errors/failures/skips/not-run. This includes the independent 600-second default and finite startup-override checks. No real-start or restoring fixture helper was selected.
- Exact argv, exits, elapsed times and raw log hashes: `/tmp/builds-v2-runner-y6sio70q/final-offline-results.json`, SHA-256 `71b1b612ec06f5d08027847050b5f99a3a62e2c23b1052b5647962d3fea72a91`.
- Before/after hashes of all 34 changed/new Builds files and tested assemblies match: `/tmp/builds-v2-runner-y6sio70q/final-tested-source.json`, SHA-256 `fb5a5db9af3e58d9a51fff80e10065060332ac36c12f793401919523d3e99d5c`.
- Complete final tracked/untracked runner diff, without staging: `/tmp/builds-v2-runner-y6sio70q/runner-final.diff`, SHA-256 `07265f9c4f5fe1b3cb4edb524589b393f0d5b5b5c99f54dbd53f05d32ec1d41b`.

The earlier 366-test snapshot before the startup-tail correction is archived separately under `before-startup-tail-correction/`; only the final **370-test** result is attributed to the final source. Three independent reviewers completed; parent inspected the corrections. No unresolved review defect is carried into this prerequisite completion.

All owning checkout HEADs remain at their original values. Nothing is staged in Platform or Builds. Of 45 independently dirty/untracked Platform baseline files, 42 remain byte-identical; three changed concurrently outside this task (`eng/verify_works_host.py`, `src/Hexalith.Platform.Composition/SdkReadinessHealthCheck.cs`, `tests/Hexalith.Platform.Works.Tests/SdkReadinessHealthCheckTests.cs`) and were left untouched. Exact readback is `/tmp/builds-v2-runner-y6sio70q/preservation-final.json`. No workload/server/target contact, dependency update, commit, publication, Production change or Story 27.4/A41/sprint transition occurred. The done status here applies only to this unnumbered Platform runner prerequisite artifact.

## Remaining PostgreSQL Integration Work

| Owner / next work | Current boundary and required result |
| --- | --- |
| Platform PostgreSQL composition | `PlatformDaprBindings.Type` and the telemetry-state reference component still select Redis. Implement the existing runner-selected, run-owned PostgreSQL 18.6 profile with immutable Dapr 1.18.1/backend identities, verify-full TLS, actual resources/readiness, observation access, volume/network ownership and cleanup. Preserve other consumers' Redis defaults. Populate actually observed loaded candidate/backend identities; current runner fields deliberately remain null with `qualificationCredit=false`. |
| Platform / Memories capability and workload | `src/Hexalith.Platform.Composition/Declarations/local-profile.json` still disables lifecycle with placeholder profile/capability evidence and empty clock sources. Direct Memories' qualification gate still requires `Qualification` and historical KVM profile `95aa…`. Implement the supported local profile/capability and signed clock/nonce/expiry binding through the normal logger/acknowledgement path; copying the KVM hash or enabling placeholders is insufficient. |
| Tool / direct candidate / consumer alignment | Platform still lacks `.config/dotnet-tools.json`; the fixed `python3` executor is not an interpreter/dependency pin. Direct Memories remains `c285b84fddac7dda1efaf4435b8de18694282583`, without `tools/access_telemetry_functional_retention.py`. Under a later authorized dependency change, align that direct candidate, pin supported tools, declare the real module-owned test, and implement its TRX adapter plus validation of the existing plan/readiness/handoff/result encoding. No replacement runner or Memories descriptor schema is needed. |
| Later operational verification | After these implementation gaps are resolved and a concrete run is authorized, verify real orchestration, current withdrawal, failure retention and actual cleanup, then the one-writer/two-125-record cohorts with at least 1,800 real seconds separation, 3,600-second retention, 900-second purge grace, younger-cohort preservation and positive per-record attribution. Offline runner tests confer no live/full qualification or A41 credit. |
