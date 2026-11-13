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

## Design Notes

Use a two-phase launcher because the verifier runs inside xUnit: changing trust after process startup is insufficient. The dedicated workload authority is separate from Folders' human Keycloak. An explicit launcher receipt establishes authority ownership without pretending the private Folders fixture supplies Platform runner qualification.

## Verification

From K run `mvn -B -ntp package`, `python3 -B -m unittest test_run_owned`, and `python3 -B verify_component.py --run-owned`. Build Platform Composition.Tests and Folders AppHost.Tests individually in Debug/source mode; run their affected xUnit classes directly, including live enrollment controls through the launcher. Record exact commands, revisions, version/provider digests, named negative controls and cleanup observations. Do not substitute component success for the unchanged strict real-provider acceptance suite.
