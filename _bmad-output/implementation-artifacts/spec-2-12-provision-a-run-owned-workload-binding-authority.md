---
title: '2.12 Provision a run-owned workload-binding authority'
type: 'feature'
created: '2026-11-13'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '375231e1552c98a4238482c67b6e69d92ea43d1a'
folders_baseline_commit: '583d3d492f94217809a8efe00424b80ce3fdfa61'
story_key: '2-12-provision-a-run-owned-workload-binding-authority-for-dispos'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Folders acceptance generates callers and a tenant per run; persistent owner enrollment cannot match them.

**Approach:** Provide a disposable HTTPS Keycloak 26.6.4 authority using the existing provider, startup realm import and consumer verification. Prepare trust before launching tests; enroll the final cohort before catalog capture.

## Boundaries & Constraints

**Always:** Require an explicit disposable owner context, fixed finite deadline and private run directory. Bind enrollment to its child process, final cohort and tenant. Bound manifests, grants and signer enrollment by that deadline. Preserve the consumer schema and all TLS, signer, inventory and fresh-probe checks. Keep every resource entry; share credentials only for replicas with identical actual workload identity. Preserve distinct proof Workers and independent RSA proofs. Clean owned authority material on success, failure, cancellation and partial startup; report leftovers.

**Never:** Target persistent/shared/staging/production authorities, accept foreign-run artifacts, create workload-authority admin users/API calls, bypass certificate validation, install global trust, change human authentication, weaken strict acceptance or extend existing deadlines/budgets. Retained evidence contains no credentials, tokens, grants or keys. This path grants no Platform lifecycle qualification or Folders 12.2 completion by itself.

## I/O & Edge-Case Matrix

| State | Behavior | Failure |
| --- | --- | --- |
| Final cohort and tenant | Exact enrollment; existing consumer verifies before catalog | No partial activation |
| Missing launch trust | Named precondition | No TLS fallback |
| Foreign context/manifest, changed cohort/tenant, hosted target | Refuse before activation | No unrelated mutation |
| Exit, cancellation, expiry, partial startup | Remove owned container, manifest, secrets and certificates | Report incomplete cleanup |

</frozen-after-approval>

## Code Map

Aliases: C = `src/Hexalith.Platform.Composition`; K = `infrastructure/keycloak-workload-binding`; T = `tests/Hexalith.Platform.Composition.Tests`; F = `references/Hexalith.Folders`; A = `F/tests/Hexalith.Folders.AppHost.Tests`.

- `C/PlatformWorkloadEnrollment.cs`: `Cohort`, `ValidateInventory`, `ConfigureAsync`; extract a shared inventory rule without relaxing verification.
- `K/verify_component.py`: reuse provider/TLS/JWKS mechanics; its admin mutation path remains component-only.
- `A/FolderDurabilityAcceptanceFixture.cs`: final identities precede line 1145 consumer call and catalog capture; four-minute startup remains fixed. Fixture has private random identity, no Builds ownership descriptor.
- `A/OperationalProviderFixture.cs`: merge workload trust with existing system/Forgejo bundle; do not replace it.
- `F/src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj`: already links canonical consumer source.

## Tasks & Acceptance

**Execution:**

- [x] `C/PlatformWorkloadEnrollmentInventory.cs` (new), `C/PlatformWorkloadEnrollment.cs`: share cohort/inventory construction and validation. Cover source sets, unbound callbacks/bootstrap and bound queries. Folders uses the selected tenant; both Tenants domains use `TenantCanonicalizer.ReservedStorageTenant` (`system`) after SDK normalization. Preserve dynamic query-digest permission; do not copy the component sample tenant.
- [x] `K/run_owned.py` (new): implement a launcher and producer. Prepare private CA/trust before child launch; require finite lifetime; bind a one-use receipt to owner/child process identity, run directory and deadline. After cohort adoption, import all clients/grants into a fresh loopback-only HTTPS authority with the existing JAR and exact server version. Inspect public metadata/JWKS for manifest signer identity. Journal owned resources before creation; finalize exact cleanup after exit, signals or expiry.
- [x] `C/PlatformRunOwnedWorkloadEnrollment.cs` (new): bridge the final model to the producer, validate receipt/path/cohort binding and launch trust, supply owner-only manifest/secret references and `Folders:WorkloadEnrollment:ManifestPath`/`ManifestSha256`. Keep ownership metadata outside the unchanged manifest schema. Propagate trust to every enrolled host before launch.
- [x] `A/FolderDurabilityAcceptanceFixture.cs`, `A/OperationalProviderFixture.cs`, `F/src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj`: adopt the explicit launcher context before the existing consumer call, link shared helpers, merge trust and connect cleanup. Preserve external owner enrollment when no generated context is selected; refuse mixed contexts.
- [x] `T/PlatformRunOwnedWorkloadEnrollmentTests.cs`, `K/test_run_owned.py` (new), `K/verify_component.py`, `A/WorkloadEnrollmentComponentTests.cs`, `A/WorkloadEnrollmentConfigurationTests.cs`: add explicit `--run-owned` component selection; cover the matrix with real provider/consumer tests and focused ownership/lifecycle controls, including replicas, two independent proof callers, expiry, partial provisioning and unrelated-resource preservation.
- [x] `docs/security/workload-binding-enrollment.md`: document launch/build prerequisites, usage and cleanup; allow generated trust only through this disposable path and point the Folders 12.2 paragraph to it. Retain persistent realm-owner requirements.
- [x] `A/WorkloadEnrollmentComponentTests.cs`, `A/WorkloadEnrollmentConfigurationTests.cs`: execute the affected Folders runtime suites against a coherent Debug/source build after the existing FrontComposer build blockers are resolved. Supplementary compile-only evidence does not close this validation task.
- [x] `K/run_owned.py`, `K/run_owned_component.py`, `K/test_run_owned.py`, `A/OperationalProviderFixture.cs`: apply G21–G25 ownership, immutable named-context, cleanup-diagnostic and PEM-separator corrections; preserve child Docker configuration and the replica identity repair.
- [x] `K/run_owned.py`, `K/test_run_owned.py`: apply G26–G30 provider snapshot, replacement-directory preservation, absolute metadata deadline, repeatable named-context cleanup and child-allocation cancellation corrections; verify all five red-to-green controls.
- [x] `C/Hexalith.Platform.Composition.csproj`, `T/Hexalith.Platform.Composition.Tests.csproj`: rerun fresh Debug/source builds after the existing catalog owners resolve inherited EventStore `4.2.0` versus authoritative `5.0.0`. Existing test-assembly execution does not close this build gate.
  - 2026-11-15: this gate passed after the latest-package migration, then regressed at HEAD through the external Builds `v6.0.3` change `58e1a5f4`. It is deferred to the Builds catalog owner by user decision. See "Final verification — 2026-11-15".

**Acceptance Criteria:**

- Given a final disposable cohort, when enrollment starts, then every required live probe succeeds using startup-imported grants and no workload admin identity/API.
- Given prelaunch trust, when consumer and hosts connect, then default certificate validation succeeds before catalog capture; missing trust refuses by name.
- Given changed tenant/cohort or another run's artifact, when adopted, then enrollment refuses before activation.
- Given any terminal path, when cleanup finishes, then owned resources and private material are absent and unrelated resources survive; evidence is labeled `run-owned enrollment` and contains metadata only.

## Implementation Notes

Implemented and audited against the complete Platform/Folders diff. Shared inventory uses actual AppIds and normalized Tenants storage scope; the new authority and launch trust are separate from human authentication. Disposal retries retain incomplete status until directory absence is observed.

All four frozen matrix rows have executed passing coverage in [the acceptance audit](evidence/2-12/acceptance-matrix-audit.json): 23 provider tests, 8 Python controls, 12 focused C# cases and one explicit real bridge/consumer test. The live Python lane also verified 19 RSA probes. Source hashes match [the verification record](evidence/2-12/run-owned-enrollment-verification-2026-11-13.json).

Platform Debug/source validation required temporary net11 props under /tmp. Folders full build remains blocked by unchanged FrontComposer CA2263 at SchemaFingerprintContracts.cs:71 after framework normalization; its two changed enrollment test classes passed compile-only checking, while their runtime suites and strict acceptance remain unverified. No shared build, dependency, CI or pin files changed. These component results grant neither lifecycle qualification nor Folders 12.2 completion.

## Spec Change Log

- 2026-11-13 user follow-up: “use latest hexalith packages.” Package/dependency changes are now explicitly authorized to unblock this story. Use the latest stable published Hexalith packages, checking NuGet rather than assuming the source checkout or retained SDK4.2 consumer view is current. Initial feed observations select EventStore `5.0.1`, Memories `4.0.0`, Builds `5.2.2`, FrontComposer `5.0.0`, Tenants `7.0.7`, Parties `3.1.1`, Folders `1.4.0`, McpCli `2.0.1`, Commons `3.0.0`, Projects `1.2.4`, Conversations `1.6.0` and PolymorphicSerializations `2.0.0`. Confirm all actually consumed package members. This authorizes using published packages for local validation; the old source-only validation requirements remain separately recorded and must not be claimed complete from package validation. Preserve historical qualification inputs and record any still-required latest-package qualification honestly.

  Necessary framework alignment with the current .NET 11 dependencies is included for both package and source builds; retain historical release inputs rather than an incompatible development framework default. Small behavior-preserving compiler compatibility corrections in owning dependencies are included where necessary to complete the original source validation tasks. Do not disable strict build/catalog/acceptance gates or change human authentication behavior.

### Latest-package continuation tasks

- [x] `references/Hexalith.Builds/Props/Directory.Packages.props`, `Directory.Packages.props`, and affected `.csproj` consumers: adopt latest stable published Hexalith dependencies through the owning central catalog and appropriate package-reference configuration. Resolve old SDK4.2/OpenAPI2 selection conflicts for the new current package build without falsifying pinned release metadata, rewriting historical evidence/fixtures or disabling catalog validation. User authorization above permits dependency-owner changes where necessary. Preserve existing in-progress enrollment work and concurrent Epic 4 artifacts.
- [x] `src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj`, `tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj`, and affected consumer sources: make the selected published package set compile, adding compatibility changes only when required by actual APIs. Verify consumed versions in restored assets, keep explicit historical cohort gates honest, and retain exact build/test failures if migration needs additional unpublished producer work.
- [x] `references/Hexalith.Folders/tests/Hexalith.Folders.AppHost.Tests` and Platform enrollment tests: rerun affected enrollment tests against fresh package builds, including explicit real consumer/fresh-host/cancellation verification where buildable. Keep package-mode evidence distinct from the two original source-mode tasks and strict acceptance; retain sanitized commands, version observations and cleanup evidence under `evidence/2-12/`.
- [x] Final verification of the committed current bytes (2026-11-15 resume). The checked tasks above rest on runs that predate the last mechanical net11 output-path edits, the added `SafeCopyIdTests`, and later concurrent commits (Builds now `v6.0.3`, Folders `c01f3833`). The interrupted `folders-current-framework-conformance` run is not a pass. Rerun at current HEAD and bind results to canonical revisions and final assembly hashes in a new `evidence/2-12/final-verification-20261115/`: fresh Debug/source and Debug/package builds of `C`, `T` and `A`; `T` enrollment classes (default plus both explicit consumer/fresh-host and cancellation lanes); affected `A` enrollment classes in both modes; Folders UI regressions (`SafeCopyIdTests`, `CorrelationCopyButtonTests`, `UserContextAccessorRegistrationTests`); the Folders Contracts deployment/OpenApi conformance classes changed for net11 paths; `K` `python3 -B -m unittest test_run_owned` and `verify_component.py --run-owned` (with `--platform-assembly` lanes); and an independent zero-leftover observation. Fix defects in story-owned files. A failure caused by files outside this story gets its exact diagnostics retained and reported, not an edit. Environment: build with `-nodeReuse:false`; `pwsh` is in `~/.dotnet/tools` (add it to `PATH`); run xUnit test DLLs directly with `dotnet exec`, because the slnx `dotnet test` selects zero tests and combining `-class` with `-method` ANDs to zero; never count a zero-test run.
  - 2026-11-15 result: executed and bound in [final verification](evidence/2-12/final-verification-20261115/verification.json). Every listed check passes except the canonical Platform `C`/`T` Debug/source builds, which fail outside story-owned files (see "Final verification — 2026-11-15" below). No story-owned defect was found.
  - 2026-11-15 user decision: defer the default Debug/source Platform build gate to the Builds catalog owner (new deferred-work entry, superseding D5) and continue to review. The task is closed on the passing package-mode results plus the separately labeled explicit-SDK-5.1 source results. It does not claim that the default Platform Debug/source build passes.

## Review Triage Log

### Review pass 1

All three independent reviewers completed before triage. B = blind, E = edge-case, V = verification-gap. Every report is recorded individually before grouping. Sixteen reports yield twelve internal patch groups and one rejected low finding; no frozen intent or public contract changes are required.

| Finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| B1 | medium | patch G1 | stop_child skips killpg once the direct child exits, leaving its surviving process-group descendants; reviewer reproduced this with sleep. |
| B2 | medium | patch G2 | Docker check confirms Keycloak data/quarkus are unwritable as 1001:1001 and writable as 1001:0. Current host GID breaks non-1000 callers. |
| B3 | high | patch G3 | prepare/version/image commands use independent timeouts, SPKI check_output has none, and no final deadline check precedes child launch. Existing finite deadline must bound these calls. |
| B4 | medium | patch G4 | cleanup removes the ownership directory even on container-removal failure; bridge interprets its absence as completion. Outer launcher eventually fails, but fixture disposal can report success prematurely. |
| B5 | medium | patch G5 | AdoptAsync constructs its release-capable lease before exclusive request publication; a second racing adoption enters catch and releases the first owner's request. |
| B6 | low | reject | C# checks permissions/final links but lacks native file-type/UID inspection. A manually substituted FIFO can block that read; the launcher still enforces child lifetime. Actual producer files use owner-created private regular files. This uncommon malformed-owner input would require new native/type handling beyond a direct correction; reject under the low-finding complexity rule. |
| B7 | low | patch G6 | New launcher hardcodes Debian's trust file when SSL_CERT_FILE is absent. Python exposes the resolved platform CA bundle; using it is a direct portability correction. Folders inherits this bundle on the new path. |
| B8 | medium | patch G8 | Existing terminal tests never enroll; partial cleanup manually sets created and mocks Docker. Add actual failure/cancellation/expiry/exit cleanup observations with an unrelated live control. |
| B9 | medium | patch G9 | Existing host assertions evaluate environment callbacks without a running host process. A focused fresh host child must verify actual default TLS metadata/exchange under the propagated configuration. |
| B10 | medium | patch G10 | Refreshed epic planning cache dropped the original campaign identity/per-job cleanup/separate cancellation requirements. Restore those planning facts; this is the generated epic artifact, not an agent-governance rule change. |
| E1 | medium | patch G1 | Same independently reproduced descendant survival as B1; preserve this report and fix the shared process-group termination root. |
| E2 | high | patch G3 | Same unbounded remaining-lease enforcement as B3, specifically version inspection and signer extraction. |
| E3 | low | patch G7 | Cleanup verifies a label by name then removes by reusable name. Capture the verified immutable container ID and remove that ID; this directly corrects the command target. |
| V1 | medium | patch G8 | Pre-verified gap: moving created=True after docker run still passes current tests, yet leaks a created container on command timeout. Exercise real post-creation failure through production provisioning/finalization. |
| V2 | medium | patch G11 | Ordinary verifier uses -explicit on -class, selecting the new run-owned-only fact without its required environment. Select the existing ordinary component method explicitly. |
| V3 | medium | patch G12 | New Folders explicit fact is absent from its exact allowed-not-runnable-tests list; pinned Builds rejects unknown NotRunnable results. Add the exact test name while preserving the gate. |

The smallest fixes remain internal lifecycle/command corrections, exact test registration, restoration of an omitted planning fact, and focused tests for existing acceptance requirements. No intent gap, spec re-derivation or deferral is required.


### Final verification follow-up

| Finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| R1 | medium | patch G13 | Two complete Python runs and a diagnostic run hit Docker's port forwarder refusing a manually selected host port (`ports are not available`, `/forwards/expose` HTTP 500). Isolated runs passed and failed starts cleaned, but free-port probing in the workspace does not reserve a daemon-published port. Use Docker-assigned loopback publication, inspect its actual binding, and verify metadata against that origin. Preserve the failed attempts and original deadlines. |

### Final review correction verification

All G1–G13 patches are complete and independently checked. Final source hashes and exact commands are in [the final verification](evidence/2-12/review-pass-1-final-verification.json); [the final matrix audit](evidence/2-12/review-pass-1-final-matrix-audit.json) confirms every frozen row has executed passing coverage.

Final checks: 23 Maven tests, 17 Python tests, 12 focused C# cases, the separate real consumer/fresh ASP.NET host test, 19 independent RSA probes, five real-container terminal cases and the real UID 1001 caller all pass. Both narrow Debug/source builds report zero warnings/errors; Folders workflow lint and both whitespace gates pass. Independent final inventory has zero authority containers/private directories. The failed pre-allocation attempts remain recorded; diagnosed Docker forwarder refusals were corrected by Docker-assigned loopback publication with strict owned-ID binding checks, without retries or deadline changes.

Three review layers produced 16 reports: 15 corrected, one low finding rejected, none deferred. Final verification added one further corrected allocation finding. The previously recorded FrontComposer CA2263 still blocks the full Folders runtime suites and strict acceptance. Root CI, dependency/framework/release inputs and the original Platform submodule pointer are preserved. Workflow completion marks this implementation ready for owner review, without granting Folders 12.2 completion or Platform lifecycle qualification.

Local delivery preserves the original Platform gitlink; the Folders implementation is committed in its owning repository and recorded in [the local delivery record](evidence/2-12/local-delivery.json). No push or remote operation was performed.

Publication continuation after the user requested push: the feature was rebased over concurrent upstream work without changing tested source bytes; Folders was published first and its verified remote commit is adopted by Platform. [Push preparation](evidence/2-12/push-preparation.json) records canonical revisions and preservation checks. The earlier local-only delivery record remains historical. No nested submodules or tuple/source-release selection files were updated.

### Continuation independent review (three layers)

All three layers returned before triage. Each report is recorded separately before grouping. B = blind, E = edge-case, V = verification-gap. The preserved historical baseline also includes prior catalog/release work; those findings are recorded as pre-existing to this continuation.

| Finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| B1 | high | patch G14 | `owned_members` excludes only initial direct children. A previously existing grandchild becomes an unexcluded direct child after its unrelated parent exits, so cleanup signals it; both ownership reviewers reproduced this. Preserve the complete pre-existing process trees. |
| B2 | medium | patch G15 | Lease construction allocates the directory/journal before handler installation and the outer finally. TERM during constructor publication exits without launcher cleanup; the reviewer reproduced and removed the resulting private directory. Protect allocation from cancellation before creating material. |
| B3 | medium | patch G16 | All Docker operations resolve the mutable default context separately. Switching the default during a lease can make cleanup observe a different daemon and falsely acknowledge absence. Bind the original Docker selection throughout that lease. |
| B4 | medium | patch G17 | Assembly verification accepts a zero exit without requiring the explicit method to execute or adoption to occur. The actual xUnit runner reports success for Total: 0; require execution and successful provision/consumer observations. |
| B5 | medium | patch G18 | `probe_child` uses Python assert for claims and the OpenSSL call. Optimization removes the call while still incrementing probe counts. Reject optimized component execution or use explicit failures. |
| B6 | medium | defer D1 | The already committed named catalog requires an authenticated digest, but `verify-agents-host.sh:61` supplies none. This catalog migration predates the current story continuation; no pin/catalog changes were made here. |
| B7 | medium | defer D2 | The already selected catalog has conditional and default EventStore versions, but `verify_works_host.selected_sdk_version` still requires one element. This pre-existing catalog-reader mismatch is separate from enrollment. |
| B8 | false | reject | The claim asks for a broader SDK host test without showing a failing SDK path. The frozen requirement is default TLS on a running enrolled host plus unchanged consumer verification; the fresh ASP.NET child executes both using propagated environment and pinned signer, and the existing SDK paths are not changed here. |
| B9 | medium | defer D3 | Full Folders runtime verification remains absent because unchanged FrontComposer CA2263 blocks the source build. The limitation is explicitly retained and no strict acceptance or Folders 12.2 claim is made. Fixing the pre-existing FrontComposer build blocker is outside these enrollment edits; editing this spec alone is not an accepted correction. |
| B10 | low | patch G19 | A missing/unreadable expected trust file throws IOException/UnauthorizedAccessException before the named refusal. This routine precondition should retain `missing-prelaunch-trust`; normalize those reads and cover it. |
| E1 | maybe-false (medium, unverified) | defer D4 | Cleanup can see no container after an unresolved Docker client outcome, but the report does not demonstrate the daemon subsequently creating the container despite cancellation. Need a delayed-daemon creation reproduction to distinguish an actual late container from a completed failed request; report is retained without claiming the race disproven. |
| E2 | high | patch G14 | Same independently reproduced unrelated-grandchild reparenting defect as B1; retain both reports and fix their common ownership classification. |
| E3 | medium | defer D1 | Same authenticated named-catalog call-site mismatch as B6, reproduced against Builds 5.2.1. It predates the current continuation and must be repaired by its catalog owner. |
| E4 | medium | patch G15 | Same reproduced constructor-time TERM leak as B2. Constructor allocation precedes cancellation protection and the launcher finally. |
| V1 | medium | patch G20 | Pre-verified regression gap: no C# case cancels adoption after request publication. Deleting catch/dispose leaves all current bridge tests green. Add an explicit scenario that verifies release and prompt cleanup while the child otherwise remains alive. |

The seven patch groups are direct corrections to demonstrated internal behavior or verification, add no public surface, and preserve frozen intent. Four separate deferred entries record two catalog-owner issues, the unchanged Folders build blocker, and the unresolved daemon outcome claim. The SDK-host expansion is rejected on the stated evidence.

### Continuation patch verification and unresolved gate

G14–G20 are implemented and parent-verified. The final run passes 23 provider tests, 28 Python tests, 25 default C# cases, two separately executed explicit C# scenarios (normal consumer/fresh host and cancellation after request publication), and 19 live RSA probes. Both narrow Debug/source builds have zero warnings/errors; all whitespace checks pass. Independent observation finds zero owned containers or private run directories. [Final commands, source hashes and limitations](evidence/2-12/review-pass-2-final/verification.json), [retained outputs](evidence/2-12/review-pass-2-final/outputs.json), and [final matrix audit](evidence/2-12/review-pass-2-final/matrix-audit.json) supersede the implementation handoff for current tested bytes.

The normal Folders build still fails on unchanged FrontComposer CA2263 at SchemaFingerprintContracts.cs:71. A temporary validation-only `WarningsNotAsErrors=CA2263` attempt retained that normal failure and exposed existing FrontComposer net10/net11 CS1705; it also failed. No tracked build, dependency, framework, release or CI input was changed. Affected Folders runtime suites and strict acceptance remain unverified, and the validation task above stays open. The possible late-daemon creation claim remains medium/unverified, requiring the reproduction recorded in deferred work.

The rendered bmad-build review step requires halting when verification fails and cannot be fixed within this implementation. Workflow completion is therefore halted at this gate with this spec `in-review`; no completion, Platform lifecycle qualification or Folders 12.2 claim is made. The changes are staged locally in their owning repositories. No commit, push, remote operation or submodule pointer advance occurred, preserving the concurrent release owner's stable-main window.

### Resumed review after publication — 2026-11-13

All three independent layers completed before triage. B = blind, E = edge-case, V = verification-gap. Each report is recorded separately before grouping. The historical baseline also contains catalog changes preceding this enrollment implementation.

| Finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| B1 | medium | patch G26 | The provider digest is checked on the mutable Maven output before that same path is mounted. An isolated reproduction replaces it between those operations, leaving mounted bytes inconsistent with the receipt. Snapshot the provider into the private owned directory before binding its digest. |
| B2 | low | patch G27 | Cleanup's container snapshot writes journal.json before the inode check. An isolated directory replacement preserves the replacement directory but overwrites its sentinel journal. Delete this redundant cleanup write; provision already journals the immutable ID. |
| B3 | medium | patch G28 | A real loopback response delivering one byte per 0.1 seconds completes after 1.01 seconds despite 0.25 seconds remaining. Socket inactivity timeouts do not enforce the fixed lifetime. Bound the complete metadata operation by the absolute remaining lease. |
| B4 | medium | defer D5 | Fresh builds and the direct catalog command reproduce inherited EventStore 4.2.0 against authoritative 5.0.0. The conflicting catalog/source selection predates enrollment. Existing assembly execution is supplementary; the fresh-build task remains open. |
| B5 | medium | carried defer D3 | Carried: fresh Folders source build again fails unchanged FrontComposer CA2263 at SchemaFingerprintContracts.cs:71. The affected runtime suites remain unexecuted; this is the same previously logged build/runtime gate. |
| B6 | medium | carried defer D1 | Carried: verify-agents-host.sh still omits the authenticated catalog digest required by the earlier catalog migration. Same location and claim as the prior D1 reports. |
| B7 | medium | carried defer D2 | Carried: selected_sdk_version still requires one raw version element despite the conditional/default catalog. Same location and claim as the prior D2 reports. |
| B8 | false | carried reject | Carried: explicit run-owned verification is the selected story contract. Requiring routine CI registration repeats the previously rejected default-CI policy claim; the explicit live controls execute successfully. |
| B9 | medium | defer D6 | Ordinary component verification accepts only exit status, and an actual wrong-assembly method selection exits zero with Total: 0. This exit-code-only proof predates the story's narrower method selection; the new run-owned harness already requires exact executed-test and adoption evidence. Record the ordinary harness gap separately. |
| B10 | medium | carried defer | Carried: the actual provider bundle composition lacks a permanent focused assertion. Same location and claim as the existing trust-composition deferral; supplementary fixture execution does not register a regression test. |
| E1 | medium | patch G29 | After successful cleanup deletes private named-context configuration, created remains true. The launcher's unconditional second cleanup resolves the now-missing context and falsely reports container cleanup failure. Clear creation state once absence is proven, before deleting private configuration. |
| E2 | medium | patch G30 | An isolated Popen wrapper delivers TERM immediately after real child creation, before its handle returns. The current handler interrupts assignment, so stop_child(None) leaves the child alive. Defer cancellation through child-handle assignment and receipt binding, then process it normally. |
| E3 | medium | patch G30 | Same independently reproduced cancellation window as E2 falsifies the terminal-cleanup claim: the launcher reports zero leftovers while its child survives. Retain this claim report and correct the shared allocation boundary. |
| V1 | medium | carried defer | Carried: focused tests build their own bundle and do not execute the provider composition operation. Same previously recorded trust-composition regression gap; no duplicate deferral. |

Five internal patch groups correct demonstrated behavior without changing frozen intent or public contracts. No implementation agent from the previous session is available to re-engage, so the parent applies these corrections. Two new entries retain the pre-existing catalog build gate and ordinary component executed-test gap; existing deferrals are not duplicated.


### Post-publication correction verification and remaining gates

G26–G30 are complete. All five new regression cases failed against the original implementation and pass after the corrections. Two independent reviewers checked the resulting patches and found no concrete regression. The provider is copied into the private owned directory before its receipt digest is bound, and Docker mounts those same bytes. Cleanup performs no journal write into a replacement directory, records confirmed container absence before deleting private Docker configuration, and remains repeatable. A total-operation timer bounds slow metadata headers/body reads by the original lease. Cancellation waits for child-handle ownership and receipt binding, then performs normal cleanup without passing blocked signals to the child.

Final verification passes 23 provider tests, all 37 Python tests, 19 live RSA probes, five real-container terminal controls and the UID 1001 private-file control. The existing source-built C# assembly passes 25 default cases and two separate explicit executions through a real named Docker context, including consumer/fresh-host verification and adoption cancellation. One concurrent full Python invocation timed out at the original 20-second cancellation-control allowance; its complete isolated repeat passed unchanged. Both outputs remain recorded, and no budgets or retry policies changed. Final independent observation finds zero owned containers and zero private run directories.

[Exact commands, canonical revisions, source hashes and limitations](evidence/2-12/post-publication-review-20261113/verification.json), [retained successful and failed output](evidence/2-12/post-publication-review-20261113/outputs.json), and [the final matrix audit](evidence/2-12/post-publication-review-20261113/matrix-audit.json) bind this continuation's evidence. The C# source and existing assembly bytes still match the prior verification; these executions remain supplementary to a fresh source build.

Fresh Composition and Composition.Tests builds still fail in the unchanged Builds catalog gate: inherited EventStore 4.2.0 conflicts with authoritative 5.0.0. The direct catalog command confirms that diagnostic. The Folders Debug/source build still fails unchanged FrontComposer CA2263 at SchemaFingerprintContracts.cs:71. The two validation tasks above remain open; affected Folders runtime suites and strict acceptance remain unexecuted. Previously deferred trust-composition and delayed-daemon claims remain open.

The rendered bmad-build review step requires “if verification fails and the failure cannot be fixed, HALT and escalate to the human.” Those dependency-owner build gates are outside these enrollment corrections, so this continuation halts with status in-review. It makes no completion, Platform lifecycle qualification or Folders 12.2 claim. Changes remain local and unstaged; no commit, push, submodule pointer advance, or dependency/framework/release/CI change occurred.

### Final-verification review — 2026-11-15

Three layers reported before triage: B = blind, E = edge-case, V = verification-gap (VO = its other findings). Scope: Platform enrollment sources `375231e1..0cf01331`, Folders enrollment sources `583d3d49..c01f3833` and the Folders latest-package migration `938c6ad7..c01f3833`. Each report is recorded individually before grouping. Rows marked carried match a logged row: same location and claim, with the code unchanged.

| Finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| B1 | medium | patch G31 | Folders `Directory.Build.props` targets `net11.0`, but `ScaffoldContractTests.cs:324` still asserts `net10.0`, so it fails in `ci.yml`. Eight current gate scripts (`run-adr-runbook-docs`, `run-consumer-docs`, `run-production-observability`, `run-provider-error-docs`, `run-dapr-policy-conformance`, `run-operations-audit-docs`, `run-scheduled-policy-conformance`, `run-nightly-drift`) still resolve `bin/<cfg>/net10.0` and are wired into the contract-spine, nightly-drift and policy-conformance workflows. `docs/contract/contract-spine-ci-gates.md:13` documents the old path. No hash binds these files. The `10.0-alpine` fallback in `Directory.Build.targets` is harmless and is not part of the patch. |
| B2 | low | reject | Hard-coded `net11.0` fixture paths are a maintenance preference. Deriving them from the running assembly adds code for a failure that is loud. |
| B3 | low | carried reject | Carried: pass-2 "dead launcher" and post-publication B4/E10. SIGKILL is uncatchable and container self-expiry would be a new mechanism. The raw exception when the launcher is dead before adoption is the carried B14/E16 case. |
| B4 | low | carried reject | Carried post-publication B5: `kc.sh` enforces the exact version, and retagging needs daemon access. |
| B5 | low | carried reject | Carried post-publication B3/E7: a Keycloak exit during readiness ends at the bridge timeout or lease, with cleanup. |
| B6 | low | carried reject | Carried post-publication B2/E3: the `rm` versus `--rm` race. |
| B7 | low | carried reject | Carried post-publication "dispose masking": needs exception aggregation. |
| B8 | low | patch G33 | The doc lists two cleanup failure codes, but `stop_child` also refuses with `owned-child-tree-cleanup-incomplete` (`run_owned.py:574`), which reaches stderr as a constant refusal. Document it. Losing that code to a double cleanup failure is the carried A5 reject. |
| B9 | low | reject | Cancellation exits 1 with the generic refusal. The user who pressed Ctrl-C knows the cause, and a distinct exit path would add a branch. |
| B10 | low | carried reject | Carried post-publication B12/E12 and the pass-2 empty `x5c` reject: malformed inputs only. |
| B11 | low | carried reject | Carried post-publication E18: the authority is already serving when the untrusted-TLS control runs. |
| B12 | false | carried reject / carried defer D6 | `VerifyFreshHostAsync` selects the method currently executing, so this is the carried resumed-review B3 false. The ordinary `verify_component.py` exit-code-only path is the carried D6 deferral. |
| B13 | medium | defer | The `eventstore` receiver audience (`PlatformWorkloadEnrollment.cs:133`, from `20a7d635`) is asserted only in explicit live lanes, because it is assigned after live TLS metadata and token probes. Same root as V5. |
| B14 | false | reject | `system` and exactly-once only refuse earlier a persistent manifest whose Tenants grant cannot match the SDK's normalized `system` request at runtime. No working manifest is broken. |
| B15 | false | carried reject | Carried continuation B8: it asks for a broader SDK path test without showing a failing SDK path. |
| B16 | low | reject | The inventory is produced by `SerializeToElement` and re-written by the same default encoder, so its bytes are stable. Current Python/C# compared values are ASCII identifiers or hex that need no escaping, and a divergence would refuse loudly. |
| B17 | false | carried reject | Carried post-publication B17/E21: the docs list sudo/setpriv as component prerequisites. |
| B18 | low | carried reject | Carried post-publication B18/E20. |
| B19 | low | reject | The copy buttons have no success indicator either. The catch replaces a circuit crash with a no-op, and a failure status would be new UI state. |
| B20 | low | patch G34 | The doc says to supply the Folders opt-in, but its command omits `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true`. Direct doc correction. |
| E1 | low | carried reject | Carried post-publication B3/E7. |
| E2 | low | reject | `http.client.HTTPException` from a local Quarkus readiness response is not an everyday outcome, and catching it adds a guard. |
| E3 | low | carried reject | Carried post-publication B12/E12. |
| E4 | low | carried reject | Carried post-publication B11/E13: grant IDs are unique by construction. |
| E5 | low | carried reject | Carried post-publication E6 and "dispose masking". |
| E6 | maybe-false (medium, unverified) | carried defer D4 | Carried: the delayed-daemon creation race. |
| E7 | low | carried reject | Carried post-publication B2/E3. |
| E8 | medium | patch G32 | `command()` (`run_owned.py:45`) starts Docker and OpenSSL in the launcher's process group, so a terminal Ctrl-C during provisioning or cleanup kills them while the launcher ignores later signals. A parent-side reproduction with a handler that ignores SIGINT showed the group signal killing the subprocess (`returncode -2`); `start_new_session=True` kept it running (`0`). This contradicts the documented "subsequent signals do not interrupt it". |
| E9 | low | reject | PID recycling of a reaped session leader within one lease is improbable; signalled identities already require a matching start time. |
| E10 | maybe-false (medium, unverified) | carried defer E9 | Carried: the five-second SIGTERM grace. |
| E11 | low | carried reject | Carried post-publication E18. |
| E12 | low | carried reject | Carried post-publication B18/E20. |
| E13 | low | reject | Component-only non-1000 control on the developer host; a timeout fails loudly. |
| E14 | low | carried reject | Carried post-publication E19. |
| E15 | low | reject | Harness-only bridge-cancellation helper; a failure is already loud. |
| E16 | medium | carried defer D6 | Carried: ordinary component exit-code proof. |
| E17 | low | carried reject | Carried post-publication "dispose masking". |
| E18 | low | carried reject | Carried post-publication B15/E17: same-user substitution. |
| E19 | low | carried reject | Carried post-publication E22 and the pass-2 empty inherited trust reject. |
| E20 | low | reject | Story 12.2 code (`f242820d`), not this story's. The lease is 900 s, and an extra validity guard is new complexity. |
| E21 | low | reject | JS interop cancellation was already unhandled before this change, and clipboard writes resolve promptly. |
| E22 | low | reject | Linux-only bridge tests on Linux-only CI and development hosts; skip guards are extra. |
| E23 | low | carried reject | Carried post-publication A4. |
| E24 | low | carried reject | Carried pass-2 "dead launcher" and post-publication B4/E10. |
| E25 | maybe-false (low) | reject | The removed `retryBudget` guard is story 12.2's deliberate change (`f242820d`, with a documented budget comment). Settling it means checking 12.2's strict-lane request flow, and the most it could be is low. |
| V1 | medium | patch G35 | Pre-verified: no default-run test reaches a successful `AdoptAsync`, so dropping host trust (`hostTrustFile ?? trustFile`) or the manifest configuration keeps every default test green. |
| V2 | medium | patch G36 | Pre-verified: cancellation-to-release is exercised only in the explicit lane. Narrowing the catch keeps default tests green. |
| V3 | medium | patch G37 | Pre-verified: the C# simulated launcher echoes `inventorySha256` without recomputing it over the inventory value bytes in `request.json`. |
| V4 | medium | patch G38 | Pre-verified: no case duplicates a Tenants-domain query, so reverting `Count(...) != 1` to `Any` keeps tests green. |
| V5 | medium | defer | Same as B13: the gateway receiver audience is pinned only by explicit live tests. |
| V6 | false | carried reject | Carried pass-2, continuation and resumed V1 rejects: default-CI registration is not the story contract. |
| VO1 | medium | patch G31 | Same `ScaffoldContractTests` `net10.0` assertion as B1. |
| VO2 | medium | patch G31 | Same eight `net10.0` gate scripts as B1. |

Eight internal patch groups (G31–G38) correct demonstrated defects with no public surface or frozen-intent change. One new deferral covers the receiver-audience gap. Every other finding is carried or rejected.

#### Patch application and re-verification, 2026-11-15

The step-03 implementation agent applied all eight groups. Changes:

- **G31:** the ten listed `net10.0` occurrences became `net11.0`: `ScaffoldContractTests.cs:324`, the eight gate scripts and `contract-spine-ci-gates.md:13`. `Hexalith.Folders.Client.Generation` (explicitly `net10.0`), the alpine fallback and historical evidence are unchanged.
- **G32:** `command()` starts its children with `start_new_session=True`. `test_command_children_run_outside_the_launcher_session` fails without the change and passes with it.
- **G33, G34:** the doc lists `owned-child-tree-cleanup-incomplete`, and the launcher example sets the Folders opt-in.
- **G35–G38:** the simulated launcher now covers two successful adoptions (with and without merged host trust), a default-run cancellation that releases the producer, a recomputed inventory digest, and two duplicate Tenants-domain query refusals.

The parent read each new assertion against its finding. Dropping the host-trust fallback or the manifest configuration fails G35, narrowing the catch fails G36's release assertion, and reverting to `Any` fails G38. Only G32 was mutation-run.

Parent re-verification on the patched tree, at Platform `0cf01331`, Builds `v6.0.3` and Folders `c01f3833` plus the uncommitted patches:

- `K`: Maven 23 pass. `test_run_owned` 45/45. `verify_component.py --run-owned`: 19 fresh RSA probes, five real-container terminal controls and the UID 1001 caller.
- `T`, Debug/package: 0 warnings, 0 errors. The enrollment class has 42 cases: 41 pass and one explicit test is not run. Both explicit lanes pass: consumer/fresh-host `provisioned=true` and cancellation `adoptionCancelled=true`.
- `A`, Debug/source: 0 warnings, 0 errors. The enrollment classes pass 8 with six explicit tests not run, and the run-owned live lane passes 1/1.
- Folders `ScaffoldContractTests`: 16/16.
- Nine script-referencing Contracts conformance classes: 115/115.
- `run-consumer-docs-gates.ps1` and `run-scheduled-policy-conformance-gates.ps1` (static-only) resolve their `net11.0` runners and pass 22/22 and 8/8.
- Zero labeled containers and zero private run directories remain.

[Verification](evidence/2-12/review-patches-20261115/verification.json), [summary](evidence/2-12/review-patches-20261115/summary.txt) and the retained logs and xUnit results record the exact commands, source and assembly hashes, and limitations. The default Platform Debug/source build stays deferred to the Builds catalog owner. No commit, push or gitlink change was made.

## Design Notes

Use a two-phase launcher because the verifier runs inside xUnit: changing trust after process startup is insufficient. The dedicated workload authority is separate from Folders' human Keycloak. An explicit launcher receipt establishes authority ownership without pretending the private Folders fixture supplies Platform runner qualification.

## Verification

From K run `mvn -B -ntp package`, `python3 -B -m unittest test_run_owned`, and `python3 -B verify_component.py --run-owned`. Build Platform Composition.Tests and Folders AppHost.Tests individually in Debug/source mode; run their affected xUnit classes directly, including live enrollment controls through the launcher. Record exact commands, revisions, version/provider digests, named negative controls and cleanup observations. Do not substitute component success for the unchanged strict real-provider acceptance suite.

### Review Findings

Review pass 2, 2026-11-13. Four layers reported. Each finding was verified before grouping. Severity is the triage verdict.

- [x] [Review][Patch] Workers replica is given its own AppId, then the test requires a shared secret [references/Hexalith.Folders/tests/Hexalith.Folders.AppHost.Tests/WorkloadEnrollmentComponentTests.cs:95]
- [x] [Review][Patch] A descendant that calls setsid survives after group cleanup reports success [infrastructure/keycloak-workload-binding/run_owned.py:428]
- [x] [Review][Patch] Docker stdout is parsed with stderr merged in [infrastructure/keycloak-workload-binding/run_owned.py:39]
- [x] [Review][Patch] A non-JSON readiness response aborts the lease instead of retrying [infrastructure/keycloak-workload-binding/run_owned.py:316]
- [x] [Review][Patch] Cleanup wait is capped by an already past owner deadline [src/Hexalith.Platform.Composition/PlatformRunOwnedWorkloadEnrollment.cs:162]
- [x] [Review][Patch] Fresh host exchange accepts a token the consumer verifier would refuse [tests/Hexalith.Platform.Composition.Tests/PlatformRunOwnedWorkloadEnrollmentTests.cs:385]
- [x] [Review][Patch] Non-1000 caller evidence prints group and secret mode as constants [infrastructure/keycloak-workload-binding/run_owned_component.py:222]
- [x] [Review][Patch] Cohort digest re-encodes JSON instead of hashing the request bytes [infrastructure/keycloak-workload-binding/run_owned.py:93]
- [x] [Review][Patch] Expiry control shares an eight-second lease with image pull [infrastructure/keycloak-workload-binding/run_owned_component.py:146]
- [x] [Review][Patch] A second TERM, INT, or HUP during cleanup skips removal [infrastructure/keycloak-workload-binding/run_owned.py:475]
- [x] [Review][Patch] SIGQUIT and SIGABRT skip the cleanup handler [infrastructure/keycloak-workload-binding/run_owned.py:478]
- [x] [Review][Patch] Probe revision accepts boolean true and 1.0 [infrastructure/keycloak-workload-binding/run_owned.py:127]
- [x] [Review][Patch] Unrelated lifecycle control sleeps less than the five scenario budgets [infrastructure/keycloak-workload-binding/run_owned_component.py:158]
- [x] [Review][Patch] Normally run tests never assert probe audience equals the Dapr app id [tests/Hexalith.Platform.Composition.Tests/PlatformRunOwnedWorkloadEnrollmentTests.cs:35]
- [x] [Review][Patch] global-administrators storage tenant has no negative test [tests/Hexalith.Platform.Composition.Tests/PlatformRunOwnedWorkloadEnrollmentTests.cs:69]
- [x] [Review][Patch] Producer negative test corrupts global-administrators, not the tenants domain [infrastructure/keycloak-workload-binding/test_run_owned.py:44]

- [x] [Review][Defer] Provider trust composition is not asserted outside the opt-in durability suite [references/Hexalith.Folders/tests/Hexalith.Folders.AppHost.Tests/OperationalProviderFixture.cs:39] — deferred: the only execution of this fixture is the opt-in durability suite, and a hermetic assertion requires splitting certificate composition out of Docker startup in StartAsync.

#### Rejected

- Dead launcher leaves the owned container running — rejected. Cleanup stays with the launcher. Its `finally` already removes the container and the private directory on every exit that runs Python. A child-side `docker rm` leaves that directory, so `DisposeAsync` still fails, and deleting the directory from the child races the launcher.
- `Protected` omits uid and regular-file checks — low, rejected. The launcher writes owner-created regular files in a 0700 directory. A substituted fifo is not an everyday path, and the fix needs native file-type and uid inspection.
- `hostTrustFile` is not size-capped or required to be a regular file — low, rejected. Callers pass a normal PEM. Blocking on a fifo or reading an unbounded file is same-user substitution, and the fix adds type and size guards.
- A root adopter can read another user's mode-0600 receipt — low, rejected. The receipt path comes from the launcher environment for the same user. Checking uid needs native stat inspection.
- A descendant with the capability environment can write `release.json` — low, rejected. That capability is the child tree's release secret, and the directory is mode 0700. Binding the writer pid would be a new authorization rule.
- Host trust is a substring check, the Forgejo PEM is appended without a separator, and the run CA has no path length — low, rejected. The launcher bundle is built with newlines, and the spec requires that bundle on every enrolled host. A path-length limit is extra CA policy.
- Live producer and cleanup controls are outside default CI — false. The story specifies explicit `--run-owned` selection. The Folders allowlist keeps that explicit fact from failing the not-runnable gate.
- Four-minute startup still cancels the run, and an expired lease signals the child — false. The code map keeps the four-minute fixture budget. `lease.check()` and `stop_child` are how a finite lease ends the child.
- The launcher pulls a moving tag and `verify_component.py` still uses Keycloak 26.6 — false. The launcher refuses unless `kc.sh` reports Keycloak 26.6.4. The 26.6 tag is the pre-existing component path.
- `AdoptAsync` returns null and the fixture continues — false. Both launcher variables absent is the specified external-enrollment path. Missing trust is refused by name only when a receipt is present.
- The epic cleanup paraphrase dropped other-environment and hold protection — false. The rewrite still requires owned-only removal, proof that other environments were preserved, and stopping attachments before teardown.
- `killpg` errors other than `ProcessLookupError` abort the signal loop — low, rejected. The launcher owns the session, so a non-ESRCH failure is rare, and `finally` still runs container cleanup. Swallowing every `OSError` adds a guard.
- `require_child` does nothing when no child exists — false. `provision()` runs only after `launch` assigns `lease.child`.
- Boolean `True` is accepted as a one-second lifetime — false. `argparse` `type=int` rejects that string, and every in-repo caller passes an int.
- An empty signer `x5c` raises `IndexError` — low, rejected. The pinned server's JWKS includes `x5c` on this path. A named refusal would be a new guard for a response the live controls do not produce.
- An empty inherited trust file drops system roots — low, rejected. Everyday `SSL_CERT_FILE` is unset or a real bundle. An emptiness guard is extra.
- `_child` with no command raises `IndexError` — false. `launch` refuses an empty command before it spawns `_child`.
- `trust.pem` can be replaced during the provisioning wait — low, rejected. The file is written once before the child starts, in a 0700 directory. A second hash is an extra guard against same-user substitution.
- `SSL_CERT_FILE` pointing at a directory aborts provider startup — false. `File.Exists` returns false for a directory, so `StartAsync` writes the Forgejo certificate and does not throw.

### Review pass 2 implementation audit

All 16 patch items above are implemented. Parent acceptance audit checked the staged product changes against the preserved story baseline and all four frozen matrix rows, and confirmed the current source hashes match the executed verification. [Verification and exact commands](evidence/2-12/review-pass-2-implementation/verification.json), [retained outputs](evidence/2-12/review-pass-2-implementation/outputs.json), and [matrix audit](evidence/2-12/review-pass-2-implementation/acceptance-audit.json) record 23 provider tests, 22 Python tests, 23 default C# cases, one separate real consumer/fresh-host case, and 19 live RSA probes. Both narrow Debug/source builds pass with zero warnings/errors. Independent cleanup observation reports zero owned containers/private directories.

The full Folders build remains blocked by unchanged FrontComposer CA2263 at SchemaFingerprintContracts.cs:71. Its changed test sources pass supplementary compile-only checking; affected Folders runtime suites and strict acceptance remain unverified. The existing trust-composition deferral remains open. No Platform lifecycle qualification, Folders 12.2 completion, commit, push or submodule pointer update is claimed. Independent workflow review follows this implementation audit.


### Resumed review — 2026-11-13

All three independent layers completed before triage (B = blind, E = edge-case, V = verification-gap). The preserved historical diff includes earlier catalog work. Every individual report is recorded below before grouping.

| Finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| B1 | high | patch G21 | A pre-existing helper can create a new descendant after the ancestry snapshot and exit. The subreaper then classifies that unrelated new identity as owned. The reviewer reproduced its termination. Require the documented launcher to have a dedicated process with no pre-existing child tree before allocating material or enabling adoption. This is a direct refusal of the demonstrated unsafe embedding state. |
| B2 | medium | patch G22 | `run_assembly` captures launcher stdout and discards it on nonzero exit, including sanitized `cleanupIncomplete` and remaining owned paths. Preserve only validated cleanup metadata on that failure path. |
| B3 | false | reject | The nested runner selects the very method currently executing in its parent, from the same assembly, with explicit execution enabled. Its exact method exists whenever this branch runs; ordinary zero-selection through a missing method is therefore unreachable here. Its parent harness independently requires execution. An unrelated assembly or changed filter would be a different implementation. |
| B4 | medium | patch G23 | The pinned `DOCKER_CONTEXT` value is a name resolved on each command. Updating that named context changes its endpoint while the lease still uses the same string. Snapshot a selected named context into private run-owned Docker configuration so original-context changes cannot redirect cleanup. Default context and explicit endpoint selection remain unchanged. |
| B5 | medium | patch G24 | If constructor journal publication fails and compensating directory removal also fails, `launch` never receives the lease object. The named failure omits the leftover directory. Emit sanitized owned-path metadata directly from that constructor cleanup failure. |
| B6 | medium | carried defer D1 | Carried: the unchanged Agents verifier omits the authenticated catalog digest required by prior catalog work. Same location and claim as continuation B6/E3; no duplicate deferral. |
| B7 | medium | carried defer D2 | Carried: the unchanged Works reader assumes one raw SDK version element in a catalog with conditional and default values. Same location and claim as continuation B7; no duplicate deferral. |
| B8 | medium | carried defer D3 | Carried: the full Folders source build still fails at unchanged FrontComposer CA2263, and its affected runtime suites remain unexecuted. A fresh build reproduced that gate; no completion is inferred. |
| B9 | medium | carried defer | Carried: the actual provider fixture trust-composition operation lacks a hermetic default test. Same location and claim as the existing Review pass 2 trust-composition deferral. Its extraction is not repeated here. |
| B10 | maybe-false (medium, unverified) | carried defer D4 | Carried: no new delayed-daemon reproduction establishes creation after the first empty snapshot. The prior D4 reproduction requirement remains open; no duplicate deferral. |
| E1 | low | patch G25 | The newly honored inherited `SSL_CERT_FILE` can be a valid PEM without a final newline. The fixture concatenates its footer with the provider header. The reviewer reproduced rejection by OpenSSL/Python and zero imported certificates in .NET. Insert the PEM separator directly. This newly reachable inherited-bundle case differs from the earlier generated-launcher-bundle claim, whose generated CA already ends with a newline. |
| V1 | false | carried reject | Carried: the story deliberately selects real provider controls through explicit `--run-owned` execution. The missing default-CI invocation is the same previously rejected policy claim; the existing explicit consumer/fresh-host test actually executed and passed in this continuation. Adding CI is not a required correction. |

Five independent internal patch groups remain. They correct demonstrated ownership/diagnostic behavior or literal PEM composition, add no public API, and require no frozen-intent change. Carried deferrals retain their existing entries.


### Resumed correction verification and remaining build gates

G21–G25 are implemented and verified. The dedicated CLI refuses unsafe embedding before allocation, and the documented external launcher still preserves unrelated processes while terminating escaped owned descendants. Named Docker contexts are exported/imported into private owned configuration; a real context-retargeting control verifies endpoint stability and confirms the child retains its original Docker configuration. Failed constructors and captured component failures retain only sanitized leftover metadata. The actual linked Folders provider fixture preserves both PEM roots when inherited trust lacks a final newline; Docker is intercepted before creation in that supplementary fixture check.

Final checks pass 23 provider tests, all 32 Python tests, 19 live independent RSA probes, five real-container terminal controls, 25 default C# cases from the existing source-built assembly, and two separate explicit C# consumer/cancellation executions. Both whitespace checks pass. Independent final inventory observes zero owned authority containers and zero private run directories. [Exact commands, revisions, source hashes and limitations](evidence/2-12/review-resume-20261113/verification.json), [retained output](evidence/2-12/review-resume-20261113/outputs.json), and [matrix audit](evidence/2-12/review-resume-20261113/matrix-audit.json) bind the current correction evidence.

The fresh Folders source build still fails on unchanged FrontComposer CA2263 at `SchemaFingerprintContracts.cs:71`. Fresh Platform Composition/Composition.Tests validation also fails in Builds Tooling catalog generation: inherited EventStore `4.2.0` conflicts with authoritative `5.0.0`. The direct catalog command reproduces that diagnostic. The changed Folders enrollment classes pass supplementary compilation, but that and existing Platform assembly execution do not substitute for fresh builds, the affected Folders runtime classes, or strict acceptance. Both validation tasks remain open, and the prior delayed-daemon outcome remains unverified.

Concurrent external work advanced Platform to `79b3ec6a0e377b896fd0ad70347177502659d5ba` during this continuation and removed the initially staged Folders replica repair. Those external CI/source changes were preserved, and the exact story repair was restored unstaged. Folders remains at `c4510025e2b4812c770d7f85de8ec465d9981649`. This workflow performed no staging, commit, push, remote operation or submodule pointer advance and changed no dependency, framework, release or CI inputs.

The rendered review step requires “if verification fails and the failure cannot be fixed, HALT and escalate to the human.” The dependency-owner build gates cannot be closed by these enrollment corrections. This continuation therefore halts with `status: in-review`, without claiming story completion, Platform lifecycle qualification or Folders 12.2 completion.


### User-requested publication continuation

The user explicitly requested pushing all changes after the halted build report. The prerequisite qualifier confirmed that this publication window is clear and retains ownership of Platform source-release qualification/publication. Folders was committed and pushed first as `f1520098d812964622a3a01bf4ec9f3beca46828`; its exact remote `main` was independently verified. Platform adopts that published gitlink with the reviewed source corrections and evidence. [Publication preparation](evidence/2-12/review-resume-20261113/publication-preparation.json) records canonical revisions, successful commitlint results, all root-declared submodule checks and preservation boundaries. The earlier no-publication notes describe the completed build phase; this user-directed publication is a separate continuation and does not close either build/runtime gate or grant qualification.


### User-requested publication of G26–G30

The user requested pushing all changes after the halted build report. All 17 root-declared submodules are clean, and each recorded commit is independently confirmed on an origin remote branch. Platform main is synchronized before the publication commit. [Publication preparation](evidence/2-12/post-publication-review-20261113/publication-preparation.json) records canonical revisions and the repository checks. The local-only notes above describe the completed build phase; this user-directed publication retains status in-review and both fresh-build/runtime gates.


### Review Findings

Post-publication review, 2026-11-13. Scope: Platform `375231e1..43a0495a` plus Folders `583d3d49..f1520098`, limited to the enrollment sources (15 files). Four layers reported: B = blind, E = edge-case, V = verification-gap, A = acceptance. Every report was verified before grouping. The severity shown is the triage verdict. P1 was reproduced: a SIGTERM delivered inside `Lease.get` is retried as `OSError`, and a second SIGTERM is ignored. P2 was confirmed: Folders' Platform gitlink `edf62206` lacks both newly linked source files.

- [x] [Review][Patch] Cancellation raises `InterruptedError`, an `OSError` subclass, so the readiness loop retries it, later signals are ignored, and release-path cleanup reports false leftovers (high; B1, E1, E2) [infrastructure/keycloak-workload-binding/run_owned.py:365]
- [x] [Review][Patch] Folders main compiles `PlatformWorkloadEnrollmentInventory.cs`/`PlatformRunOwnedWorkloadEnrollment.cs`, which are absent from its Platform pin `edf62206`; advance that gitlink to a published Platform commit containing them (high; V other) [references/Hexalith.Folders/src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj:37]
- [x] [Review][Patch] `stop_child` re-sends SIGTERM to every owned process every 20 ms, so second-signal force-exit handlers (Dapr) skip graceful shutdown; signal each identity once per phase (medium; B9) [infrastructure/keycloak-workload-binding/run_owned.py:556]
- [x] [Review][Patch] `AdoptAsync` refusals after `result.json` (trust digest, result/manifest binding, loopback issuer, cohort, secret location, final re-read) have no tests (medium; V1, A2, B14) [src/Hexalith.Platform.Composition/PlatformRunOwnedWorkloadEnrollment.cs:105]
- [x] [Review][Patch] Bridge-cancellation lane replaces `Lease.get`, so `provision`'s own `release.json` check is never exercised (medium; V2) [infrastructure/keycloak-workload-binding/run_owned_component.py:330]
- [x] [Review][Patch] Distinct primary/proof identity and replica-permission refusals are not pinned by code; shared-workload faults trip earlier checks (medium; V3) [infrastructure/keycloak-workload-binding/test_run_owned.py:379]
- [x] [Review][Patch] Launcher trust-bundle PEM separator is not pinned by a no-trailing-newline case (low; V4) [infrastructure/keycloak-workload-binding/test_run_owned.py:483]
- [x] [Review][Patch] Test child inherits the launcher-injected `DOCKER_CONTEXT`; copy the child environment from the original environment (low; A3) [infrastructure/keycloak-workload-binding/run_owned.py:595]
- [x] [Review][Patch] A missing/unreadable `hostTrustFile` escapes the named trust refusal as a raw IO exception (low; A6) [src/Hexalith.Platform.Composition/PlatformRunOwnedWorkloadEnrollment.cs:83]
- [x] [Review][Patch] Summary `docker inspect` runs after `result.json` is published, so its failure kills an already-adopted child; inspect before publication (low; E4) [infrastructure/keycloak-workload-binding/run_owned.py:388]
- [x] [Review][Patch] `run_assembly` discards the launcher's constant refusal line on failure (low; B13) [infrastructure/keycloak-workload-binding/run_owned_component.py:322]
- [x] [Review][Patch] Folders receipt-fault loop asserts only the exception type, not each refusal code (low; B16) [references/Hexalith.Folders/tests/Hexalith.Folders.AppHost.Tests/WorkloadEnrollmentComponentTests.cs:128]

- [x] [Review][Defer] Folders runtime suites and strict acceptance remain unexecuted [references/Hexalith.Folders/tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs:1153] — deferred: carried D3, a pre-existing FrontComposer/net11 build gate; the open validation task already records it (medium; A1)
- [x] [Review][Defer] Five-second SIGTERM grace may be too short for child teardown [infrastructure/keycloak-workload-binding/run_owned.py:548] — deferred: medium, unverified. Settle by observing whether the xUnit child runs fixture disposal on SIGTERM and how long DCP/fixture teardown takes (E9)
- [x] [Review][Defer] Explicit assembly lanes misparse the launcher summary when xUnit colors its output [infrastructure/keycloak-workload-binding/run_owned_component.py:270] — deferred: pre-existing to these patches and found during their verification. With `TERM=xterm-256color`, xUnit ends with an ANSI reset and no newline, so the launcher's summary JSON does not start its line. `verify_component.py --run-owned --platform-assembly` then fails proof validation although the test passed; the HEAD launcher reproduces it. Settle by disabling xUnit color in `run_assembly` or by starting the launcher summary on its own line (medium)

#### Rejected

- A4 low: the published port is loopback-only and secrets stay private. Bridge-network isolation would need host networking and port selection, which brings back the R1 forwarder problem.
- A5 low: losing the child-tree code takes a double cleanup failure, and fixing it needs exception aggregation.
- A7: the fix would edit the spec under review.
- A8, B13 (success path) false: the forwarded output is metadata only, and the launcher forwards the same child stdout by design.
- A9 low: the duplication with `verify_component.py` names no diverging caller, and the fix is a refactor.
- B2/E3 low: the rm-versus-`--rm` race needs Keycloak to exit on its own during cleanup, and the fix adds an exception branch.
- B3/E7 low: a Keycloak exit during readiness still ends at the bridge timeout or lease with cleanup. The fix would add a daemon probe on every iteration.
- B4/E10 low (carried): SIGKILL is uncatchable, and container self-expiry is a new mechanism.
- B5 low (carried): `kc.sh` enforces the exact version, and retagging the image requires daemon access.
- B6 low: the CA key sits in a 0700 directory, and the trust it confers is scoped to `SSL_CERT_FILE`. The same user could replace `trust.pem` anyway.
- B7/E11 low: remote or VM daemons fail loudly (missing mounts, unreachable origin). The fix adds an endpoint guard.
- B8 low: ending the child at the lease deadline is by design (prior pass), and Debian-family hosts keep their `SSL_CERT_DIR` roots after release.
- B10 low (carried): the capability is the child tree's release secret.
- B11/E13 low: bridge-generated grant IDs are unique by construction.
- B12/E12 low: only hand-crafted malformed requests trigger this, and the traceback carries no data.
- B14 (`launcher-exited`)/E16 low: this needs the launcher to die mid-adoption. The reported `owned-cleanup-incomplete` is accurate, and naming the original cause would require restructuring disposal.
- B15/E17 low (carried): this is a same-user substitution, and the check needs native uid inspection.
- B16 (test name) false: `Validate` is the pre-exchange gate in `ConfigureAsync`.
- B17/E21 false: the docs list passwordless `sudo`/`setpriv` as a component-validation prerequisite.
- B18/E20 low: this is a component-only control on the developer host, and it fails loudly.
- E5 low: the exact image is a documented prerequisite, and a cold pull fails loudly.
- E6 low: with the prerequisite image present, `docker run` returns quickly. The fix adds release guards.
- E8 low: orphan zombies are reaped by `stop_child`, and the PID pressure is negligible.
- E14 low: this needs Maven to rewrite the JAR during the snapshot, and the fix adds a double-read guard.
- E15 false: a mismatched name is refused loudly by the launcher's cohort validation.
- E18 low: the authority is already serving when the control runs, and tightening the check adds a guard.
- E19 low: the current output has no such line, and the fix adds a parse guard.
- E22 low: `SSL_CERT_FILE` set to a missing path is a misconfiguration. A similar finding was rejected in a prior pass.
- V other (dispose masking) low: masking needs cleanup to take over 30 s with a live launcher, and the fix needs exception aggregation.

#### Patch application, 2026-11-13

All 12 patches are applied. P2 is local Folders commit `d1ee8d61` (gitlink `edf62206` → `43a0495a`, commitlint clean). Folders is not pushed, and the Platform gitlink is not re-pinned. The other edits are unstaged in Platform and Folders.

- Red-to-green, against HEAD sources:
  - P1: cancellation was retried for 19.3 s, until the lease deadline.
  - P3: 16 SIGTERMs were delivered instead of 1.
  - P8: the child received `DOCKER_CONTEXT`.
  - P10: `result.json` was published before the inspect failure.
  - P11: the refusal code was dropped.
- Guard mutations caught:
  - Python: the release check (P5), the PEM separator (P7), and the distinct-proof, distinct-primary and replica-permission rules (P6).
  - C#: the loopback-issuer and secret-directory checks (P4), and the P9 revert. All 3 new theory cases failed under these mutations.
- Final results:
  - Python: `python3 -B -m unittest test_run_owned`, 43/43 passing.
  - C#: `-class PlatformRunOwnedWorkloadEnrollmentTests`, 35 passing (1 explicit test not run).
  - Live lanes: `verify_component.py --run-owned` gave 19 fresh RSA probes, 5 real-container terminal controls and the UID 1001 caller. `--platform-assembly` with `NO_COLOR=1 TERM=dumb` passed both the consumer/fresh-host lane and the cancellation lane; the cancellation lane recorded `adoptionCancelled` through the production readiness loop.
  - Leftovers: zero owned containers and zero private run directories.
  - Whitespace: `git diff --check` is clean in both repositories.
- Build limits: C# was compiled only for validation. It used `BuildsToolingAssemblyPath`/`BuildsEventStoreHostAssemblyPath` and an empty `GeneratePlatformVersionCatalog` override from the scratchpad, because the D5 catalog conflict (EventStore 4.2.0 vs 5.0.0) still blocks fresh builds. The P12 Folders sources passed the existing analyzer-off typecheck fixture only. Neither open build/runtime validation task is closed, and no Folders 12.2 or lifecycle qualification is claimed.

#### User-requested publication of the post-publication patches

The user asked to push all changes. Folders went first: `d1ee8d61` (Platform pin `edf62206` → `43a0495a`) and `938c6ad7` (receipt refusal codes) were pushed, and `origin/main` was verified at `938c6ad7`. Platform fast-forwarded over two concurrent remote commits (`a777e629`, `b76e0c7c`) that touch none of these files. Its Builds checkout was re-synced to the pinned `v5.2.2` gitlink without recursion. Platform then commits these reviewed corrections and, separately, the Folders gitlink. Status stays `in-progress`; both fresh-build/runtime gates and the medium defers remain open.

### Resumed implementation verification — 2026-11-13

The deferred color-output parsing defect is corrected: the launcher starts its metadata summary on a fresh line after child output. A real child ending with an ANSI reset and no newline reproduces the missing summary before the correction and passes afterward. The real existing C# assembly lane likewise fails before the correction and passes afterward with `TERM=xterm-256color` and `NO_COLOR` unset, including both consumer/fresh-host verification and production adoption cancellation. All 44 Python controls pass from the documented provider directory, including five real-container terminal controls and the UID 1001 caller. Independent final observation finds zero owned containers and zero private run directories.

Fresh Debug/source validation remains blocked. Composition fails the unchanged Builds catalog selection conflict: inherited EventStore `4.2.0` versus authoritative `5.0.0`; a direct catalog invocation confirms it. A fresh Composition.Tests restore also reports `NU1109` for `Microsoft.AspNetCore.OpenApi` `11.0.0` versus central `10.0.15` through source EventStore.Gateway/Tenants. Folders still fails unchanged FrontComposer `CA2263` at `SchemaFingerprintContracts.cs:71`. Neither open validation task is closed. Existing assembly execution remains supplementary; no lifecycle qualification, strict acceptance or Folders 12.2 completion is claimed.

[Exact commands, source hashes, cleanup observations and limitations](evidence/2-12/resume-verification-20261113T1716Z/verification.json) and [retained outputs](evidence/2-12/resume-verification-20261113T1716Z/outputs.json) include the red-to-green evidence and an initial incorrect root-directory Python invocation whose subprocess imports failed. The complete documented provider-directory invocation passed without code or budget changes. Dependency, framework, catalog, release and CI inputs remain unchanged. Concurrent Epic 4 artifacts are preserved; the implementation handoff performed no staging, commit, remote operation or submodule pointer update.

Parent inspection confirms the recorded source hashes and executed component matrix evidence; [the parent audit](evidence/2-12/resume-verification-20261113T1716Z/parent-audit.json) leaves both required build/runtime tasks open. Temporary build scripts and framework-normalization inputs are retained with the verification record. The resolved color-output entry is marked resolved in deferred work. Only this story's changes and evidence are staged locally. The workflow stops at implementation verification: “finish the missing work before proceeding” prevents advancing to review while these dependency-owner gates fail. No commit, remote operation or submodule pointer change occurred.

### Final verification — 2026-11-15

This pass ran at Platform `0cf01331`, Builds `99f7c860` (`v6.0.3`) and Folders `c01f3833`. Every submodule checkout matched its gitlink. Story-owned source hashes and revisions were identical before and after the run. Builds used `-c Debug -m:1 -nodeReuse:false` with `pwsh` on `PATH`. Tests ran from the `net11.0` DLLs through `dotnet exec`, and no zero-test run was counted. [Verification](evidence/2-12/final-verification-20261115/verification.json) holds the exact commands, final assembly hashes, restored package versions, per-class counts and the leftover observation. [Outputs](evidence/2-12/final-verification-20261115/outputs.json) holds the logs, including failures.

Passing, each from a fresh build at current HEAD:

- `K`:
  - `mvn -B -ntp package`: 23 provider tests pass. The JAR digest is `2de2c806…`.
  - `python3 -B -m unittest test_run_owned`: 44/44 pass.
  - `verify_component.py --run-owned`: 19 fresh RSA probes, five real-container terminal controls and the UID 1001 caller all pass.
- `C`, `T` and `A`, Debug/package: all three builds report 0 warnings and 0 errors.
  - The `T` enrollment class has 37 cases: 36 pass and one explicit test is not run by the default lane.
  - The two explicit `T` lanes both execute. The consumer/fresh-host lane reports `provisioned=true`, and the cancellation lane reports `adoptionCancelled=true`.
- `A`, Debug/source and Debug/package: both builds report 0 warnings and 0 errors. In each mode:
  - The enrollment classes have 14 cases: 8 pass and six explicit tests are not run by the default lane. The two live lanes below run those six.
  - The ordinary live consumer lane passes 5/5.
  - The run-owned live consumer lane passes 1/1.
- Folders UI regressions pass 13/13 in both modes: `SafeCopyIdTests` 2, `CorrelationCopyButtonTests` 6, `UserContextAccessorRegistrationTests` 5.
- The Contracts deployment/OpenApi conformance classes changed for net11 paths pass 303/303 in both modes. This count includes `PackedContractsClosureTests`.
- Independent leftover observation: Docker events show 24 authority, component and unrelated-control containers created during the window, and all 24 were destroyed. No labeled container and no private run directory remains.

Retained failures:

- **Platform `C`/`T` Debug/source builds (blocks this task).** Fresh builds fail with the D5 class of error:
  - `C` fails because Builds Tooling `GeneratePlatformVersionCatalog` reports: "Catalog field Hexalith.EventStore.Aspire has inconsistent build selection '4.2.0'; authoritative catalog selects '5.1.0'."
  - `T` fails restore with `NU1109`: `Microsoft.AspNetCore.OpenApi` 11.0.0, required by source Tenants and EventStore.Gateway, versus the central 10.0.15.
  - Cause: Builds `58e1a5f4` ("restore the qualified Platform package view", in `v6.0.3`) put both projects back in the finite `HexalithPlatformSdk42CatalogView`. Platform `Directory.Build.targets` then propagates the resulting `HexalithEventStoreVersion=4.2.0` to every source project reference, including Tooling.
  - Neither file belongs to this story, so the diagnostics are retained and nothing was edited.
  - At current HEAD, this regresses the checked source-build task above.
  - A supplementary run with the explicit caller selection `-p:HexalithEventStoreVersion=5.1.0` passes: both builds report 0 warnings and 0 errors, 36 default cases pass, and both explicit lanes pass. That run does not close this gate.
- **Ordinary Folders live lane, source mode, attempt 1.** It failed at the component `docker run` within 0.54 s. Docker shows its container created and destroyed within 0.1 s. This matches the R1 class of manual host-port refusal in the pre-existing ordinary path, but the cause is unverified because the harness withholds the daemon reason. Attempt 2 on the same assembly passed unchanged.
- **Stale-assembly runs (not counted).** After the failed source builds, the source-mode `T` default and live runs executed the 2026-11-13 assemblies. The current launcher correctly refused the stale inventory with `exact-permission-inventory`.

Package mode for Platform resolves the finite SDK4.2 view chosen by the catalog owner: EventStore 4.2.0, Memories 3.0.27 and Tenants 7.0.7. Folders resolves EventStore 5.1.0, Memories 4.0.0 and Tenants 8.0.0. This pass makes no claim of strict Folders acceptance, Folders 12.2 completion, or Platform lifecycle or SDK qualification. No commit, push or submodule-pointer change was made.

Parent audit: the parent reproduced the Composition Debug/source catalog failure independently at `0cf01331` and corrected the default-lane counts against the xUnit XML (`T` 36 pass and 1 explicit test not run; `A` 8 pass and 6 explicit tests not run, which the live lanes run). By user decision on 2026-11-15, the default Debug/source gate is deferred to the Builds catalog owner and the story proceeds to review.
