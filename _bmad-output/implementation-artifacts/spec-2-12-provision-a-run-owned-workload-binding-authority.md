---
title: '2.12 Provision a run-owned workload-binding authority'
type: 'feature'
created: '2026-11-13'
status: 'in-review'
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
- [ ] `A/WorkloadEnrollmentComponentTests.cs`, `A/WorkloadEnrollmentConfigurationTests.cs`: execute the affected Folders runtime suites against a coherent Debug/source build after the existing FrontComposer build blockers are resolved. Supplementary compile-only evidence does not close this validation task.

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
