---
title: '2.3 Make local composition resources run-scoped'
type: 'feature'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '6fbe5a024650382e25bac7d47ac919457e8d393c'
story_key: '2-3-make-local-composition-resources-run-scoped'
context:
  - '_bmad-output/implementation-artifacts/epic-2-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Two local reference compositions must reach readiness together without sharing ports, mutable storage, credentials or service discovery. Generalize the existing retention-profile isolation to ordinary signed runner plans and the direct file AppHost, reusing Builds allocation and ownership primitives.

## Boundaries & Constraints

Preserve mTLS, deny-default ACLs, component scopes, declared readiness, catalog pins and source/package boundaries. Keep all mutable runtime files outside checkouts. Honor persisted runner ports; allocate additional ports without overlap. Certificate configuration selects an external parent for private run directories, never a shared writable issuer directory. Remove the globally named scheduler-volume fallback; use owned storage.

Record that reference runs create their own Redis, never consume dapr-init Redis or acquire exported fixture locks, and instantiate no Keycloak. Installed binaries and source templates may be shared read-only. Existing technical own-repository fixtures remain outside Platform integration evidence; descriptor migration belongs to Story 2.9. Adapt Works preview helper callers as necessary without expanding its qualification claim.

Preserve independent workspace edits and running applications. Do not stage gitlinks, publish/repin tools, change the legacy dapr-init action, implement descriptor/attachment/lifecycle policy, or access hosted data. Only stop/delete resources created by these verification runs.

User-approved exception (2026-10-23): align the root file AppHost SDK and Docker, Kubernetes, Redis and Keycloak directives to the catalog's existing Aspire 13.6.3 versions. Preserve all other pins.

## I/O & Edge-Case Matrix

| State | Expected behavior |
| --- | --- |
| Two identical selections | Separate listener ports, certificates, discovery registries, scheduler and application data. |
| Explicit certificate parent | Private child per run; reject paths resolving inside the checkout, including symlinks. |
| Selected runtime unavailable | Fail with the missing prerequisite; never fall back to shared ports or discovery. |
| Startup failure or occupied port | Report failure; preserve diagnostics and avoid foreign-resource cleanup. |

</frozen-after-approval>

## Code Map

- `DaprSelfHostedMtls.cs`: fixed host defaults, shared issuer directory and scheduler volume; preserve trust and credential-read safeguards.
- `src/Hexalith.Platform.Composition/PlatformAppModel.cs`: existing private workspace/Redis/scheduler mounts; ordinary runs ignore allocated control-plane ports. Retention branches demonstrate reuse.
- `src/Hexalith.Platform.Composition/PlatformDaprEndpointSubscriber.cs`: custom Sentry requires native `daprd`; CLI 1.18 hardcodes port 50001. Reuse its guarded argument conversion and selected runtime.
- `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionDaprComponentRenderer.cs`: signed plans already render private SQLite discovery. Expose reusable rendering for direct composition.
- Builds `CompositionEngine`, `CompositionRunPlanFactory`, `CompositionAspireHostPaths`: existing run identity, private workspaces, allocated ports and orchestration paths; retain these contracts.

## Tasks & Acceptance

**Execution:**

- [x] `DaprSelfHostedMtls.cs` — replace shared host/storage/certificate defaults with run-owned allocations; keep container target ports and mTLS unchanged.
- [x] `src/Hexalith.Platform.Composition/PlatformAppModel.cs`, `PlatformDaprEndpointSubscriber.cs`, `PlatformDaprBindings.cs` — wire allocated control-plane endpoints, private credentials and discovery through rendered configuration and actual sidecar commands in both entry paths. Reuse ownership/cleanup boundaries.
- [x] `src/Hexalith.Platform.Composition/WorksPreviewModel.cs` — adapt shared-helper calls to their revised allocation contract.
- [x] `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionDaprComponentRenderer.cs` and `test/Hexalith.Builds.Module.Tests/CompositionDaprComponentRendererTests.cs` — share discovery rendering without changing signed-plan semantics; verify private paths and escaping.
- [x] `tests/Hexalith.Platform.Composition.Tests/PlatformRunIsolationTests.cs`, `PlatformDaprToolchainTests.cs`, `PlatformRetentionProfileTests.cs`, `PlatformDaprBindingsTests.cs` — test the matrix, allocated addresses in actual executable callbacks, independent certificates/storage/discovery and retained security behavior; update obsolete fixed-resource assertions.
- [x] `tests/Hexalith.Platform.Composition.Tests/PlatformConcurrentRunTests.cs` — gated real-run proof using two Builds sessions selecting the six-server reference graph; verify readiness, disjoint resources, independent persisted data, and survivor health after stopping its peer.
- [x] `README.md`, `docs/platform-apphost-form.md` — replace the one-run restriction with scoped-resource inventory, prerequisites and verification instructions; distinguish legacy fixtures and preview scope.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` — update only this story as implementation progresses. Save verification under `_bmad-output/implementation-artifacts/evidence/2-3/`.

- [x] `references/Hexalith.EventStore/Directory.Packages.props` — preserve identical cryptography versions while avoiding duplicate catalog entries; retain the older-catalog fallback and verify both evaluation cases.
- [x] Final required qualification — pass normal builds after the independent Builds lifecycle work stabilizes, diagnose the gateway HXR027/HTTP-503 cutover failure, and rerun the expanded concurrent-native-listener proof plus file-entry smoke.
- [x] `apphost.cs` — apply the user-approved Aspire 13.6.3 directive alignment and pass the current direct file-entry live smoke.

**Acceptance Criteria:**

- Given ordinary signed and file-entry runs, when their graph and executable callbacks are evaluated, then Sentry, placement and scheduler use allocated host ports, run-owned writable storage and matching sidecar addresses; no global scheduler volume is selected.
- Given the resource inventory, when reference composition starts, then every mutable dependency is owned by that run and any shared input is explicitly read-only; legacy Redis, fixture-lock and Keycloak assumptions are excluded by the actual graph.
- Given two reference runs on one machine, when both reach declared readiness and write distinct values under identical test keys, then each store retains its own value, discovery and credentials remain separate, and stopping one leaves the other healthy.

### Review Findings

Code review 2026-10-23 (round 3; Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor) over the story-owned files from baseline `6fbe5a0` to `7c37f3f`, plus Builds renderer `2210aaa..d49834d` and EventStore props `cb41453..85f0bd9`.

- [x] [Review][Patch] Document checkout build outputs as a shared developer input; runner test suites keep private outputs (D1 → B, Administrator 2026-10-23) [src/Hexalith.Platform.Composition/PlatformAppModel.cs:238]
- [x] [Review][Patch] Works harness reads issuer credentials from the composed `dapr-sentry` credential mount, not the configured parent (D2 → A, Administrator 2026-10-23) [references/Hexalith.Works/tests/Hexalith.Works.IntegrationTests/WorksPlatformTrustChecks.cs:45]
- [x] [Review][Patch] Keep the Docker-boundary occupied-port proof and state precisely what it proves; real allocation probes skip occupied ports (D3 → A, Administrator 2026-10-23) [docs/platform-apphost-form.md:178]
- [x] [Review][Patch] Ordinary model test depends on the developer's `~/.dapr/bin/daprd` [tests/Hexalith.Platform.Composition.Tests/PlatformAppModelTests.cs:188]
- [x] [Review][Patch] Assert the composed graph's rendered components target only the run's Redis, with no residual `localhost:6379`/`127.0.0.1:6379` (AC2) [tests/Hexalith.Platform.Composition.Tests/PlatformRunIsolationTests.cs:19]
- [x] [Review][Patch] Assert rendered `spec.mtls.sentryAddress` equals the allocated Sentry listener, and that sidecar listeners are disjoint across identical file-entry selections (AC1, matrix row 1) [tests/Hexalith.Platform.Composition.Tests/PlatformRunIsolationTests.cs:19]
- [x] [Review][Patch] Assert the store persistence wiring: Redis `--dir /data --appendonly yes`, `REDIS_ARGS`, `FALKORDB_DATA_PATH=/data` [src/Hexalith.Platform.Composition/PlatformAppModel.cs:186]
- [x] [Review][Patch] Assert the callback-written native listener receipt in a non-live model test [src/Hexalith.Platform.Composition/PlatformDaprEndpointSubscriber.cs:68]
- [x] [Review][Patch] Test refusal of a symlinked per-run credential child [DaprSelfHostedMtls.cs:39]
- [x] [Review][Patch] Assert the Works preview's selected `RuntimePath` and control-plane addresses before the test substitutes its fixture runtime [src/Hexalith.Platform.Composition/WorksPreviewModel.cs:249]
- [x] [Review][Patch] Document where failed file-entry workspaces (with `composition-failure.txt` and run secrets) are retained and how to remove them [src/Hexalith.Platform.Composition/PlatformCompositionWorkspace.cs:15]
- [x] [Review][Patch] Correct the occupied-port docs: raw Docker output (not redacted), written to fixed `evidence/2-3/occupied-port` (not the concurrent evidence override) [docs/platform-apphost-form.md:184]
- [x] [Review][Patch] Replace the stale "control-plane ports 50001, 51005 and 51006 must be available" sentence [docs/works-host-parity.md:191]
- [x] [Review][Patch] Complete the concurrent-proof command block (Builds Module AppHost Debug build), remove doubled blank lines, align "descriptor migration/adoption" wording with README [docs/platform-apphost-form.md:156]
- [x] [Review][Patch] Correct the evidence index: EventStore relies on catalog pins (both pinned Builds catalogs declare all five packages at 10.0.13); no conditional older-catalog fallback exists [_bmad-output/implementation-artifacts/evidence/2-3/README.md:20]
- [x] [Review][Patch] Include actual project HTTP endpoints from readiness in the concurrent proof's cross-run listener comparison [tests/Hexalith.Platform.Composition.Tests/PlatformConcurrentRunTests.cs:90]
- [x] [Review][Defer] Works preview sidecar endpoints made proxyless without ports [src/Hexalith.Platform.Composition/PlatformDaprEndpointSubscriber.cs:75] — deferred: maybe-false, high if true. With `servers=[]`, `Apply` now sets `TargetPort = Port` (null for Works sidecars, which set no Dapr ports) and `IsProxied = false`. Aspire 13.6.1 contains both proxyless port allocation and the "needs to specify a port… since it isn't using a proxy" refusal. Settle by starting the Works preview once (`Platform:Works:Enabled`) or running the explicit Works Platform-target lane.
- [x] [Review][Defer] `ComposeRun` secondary-port claim acquisition untested [src/Hexalith.Platform.Composition/PlatformAppModel.cs:376] — deferred: Story 2.4 resource-claim work. Only the helper is tested; the signed isolation plan sets no `ResourceClaimStateDirectory`, so reverting `Compose` to `AllocateSecondaryPorts` passes every test.

**Rejected (round 3):**

- false — BH5 adapter rejects Platform `localhost:` launches: the isolated-daprd adapter is selected only by the Works lane; Platform runs never use it, and the G2 bound-loopback spelling is deliberate.
- false — BH6 launch flag/config Sentry mismatch: `--sentry-address` overrides config, and production never rewrites the model port after composition. The fatal receipt check fires only on argument shapes the toolkit is not shown to produce.
- false — BH11 circular Works adapter assertion: it checks that Works-generated endpoints match the adapter's bound spelling, and `originalSentryAddress` still records the input.
- false — BH12 unrelated work: an artifact of filtering shared commits (2.4/4.5 hunks), not of this story's change.
- false — BH13 public API break: `DaprSelfHostedMtls.cs` compiles only into Composition (via `Link`); the removed members are referenced only by an archived Agents evidence snapshot.
- false — BH3/EC12 claim conflict aborts startup: failing with the blocking owner identified is the epic's specified "or identify the blocking run" behaviour (2.4 code).
- false — EC4 Sentry address without a colon: every caller passes a `host:port` literal.
- false — EC19 Works `AppHostDirectory` boundary: Works is reached only from the root `apphost.cs`, whose directory is the Platform checkout.
- false — AA4 fixture-lock part: the composed graph has no fixture-lock code path to exclude; legacy fixtures are outside it.
- low — BH2/EC2/EC3/EC7 thin failure markers: callback-time failures stay in Aspire logs while the host runs (cf. R2-B1); a non-`InvalidDataException` from `Apply` is not shown reachable.
- low — BH7 native runtime version unattested: duplicate of deferred R2-B4.
- low — BH8/EC10 reuse of an existing credential child: the child name derives from a random run path; would need a racing local user.
- low — BH9 hard-coded run label: equals `CompositionEnvironment.ContainerRunLabel`; a rename fails loudly.
- low — BH13 Sentry stored in the `PlacementHealth` slot: no other Platform use of that slot; renaming needs a Builds contract change.
- low — BH14 MergeRunDiscovery stringifies scalars, test naming, Windows cross-drive paths: SQLite discovery uses strings only; Linux/WSL target.
- low — AA5/EC8 external credential child after a compose failure: an empty private directory under an explicitly configured parent; the crash case is already deferred (B5/R2-B3).
- low — AA6 `localhost:50001` fallback in `PlatformDaprBindings.Render`: no production caller passes null.
- low — EC5/EC11 empty `HEXALITH_DAPR_HOME` or certificate setting: loud failures on unlikely input.
- low — EC6/EC15 hosted (Story 4.5) parsing and unknown-appId errors: outside this story and loud.
- low — EC13/EC14 Works preview temp parent mode and failed-composition temp leak: multi-user or rare dev-preview paths; the fix adds guards.
- low — EC16 survivor refresh after the startup deadline: test-only, with the default deadline well beyond the measured 225 s.
- low — EC17 skipped `aspire stop` after a failed start: gated smoke; stopping unconditionally risks the preserved developer AppHost.
- low — EC21 non-BMP surrogate escapes in discovery paths: improbable in temp paths; the fix needs a custom encoder.
- low — EC23 run data inside a checkout: default runner workspaces live under LocalApplicationData, and the default certificate check rejects in-checkout run directories.
- spec edit — AA8 status disagreement across spec/sprint artifacts: resolved by this workflow's status sync.

## Implementation Notes

- Sentry, placement and scheduler require explicit allocated host ports; scheduler storage has no named-volume fallback. Ordinary signed plans retain persisted Redis/gateway-sidecar ports and now use persisted placement/scheduler ports; secondary allocations exclude all persisted ports.
- Default issuer children live under private runtime workspaces. An explicit certificate parent gets a private child derived from the run workspace, so signed-plan preflight and execution reuse only their own child. Existing and dangling symlinks into the checkout are rejected.
- Signed and file compositions use the shared Builds SQLite renderer. Native sidecar command callbacks carry the selected runtime, allocated Sentry/placement/scheduler addresses, original scoped tokens and trust material. Missing native runtime fails before execution.
- Redis, vector, graph and scheduler data mounts are private to the run; mutable store writers use the host UID on Linux. Templates and installed binaries remain read-only inputs. Works callers use the revised helper contract without extending preview qualification.
- Build-output placement was preserved. Concurrent hosted-graph and Builds contract edits were not adopted as this story's work; no gitlinks were staged.


## Spec Change Log

- 2026-10-23 — The user approved the prepared five-directive Aspire 13.6.3 alignment after the current file-entry restore failed NU1605 against the catalog's 13.6.3 dependencies. Add this narrow exception to the frozen pin constraint; preserve all other pins, trust/readiness checks, independent edits, successful concurrent proof and owned cleanup behavior.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 — nested Builds files omitted from review snapshot | low | patch | The files exist as independent untracked Builds work; the root diff does not expand nested untracked files. Restage the review artifact with nested files; do not stage gitlinks or adopt their implementation.  Review artifact restaging now includes expanded submodule diffs and nested untracked source files. |
| B2 — ignored verification logs omitted | medium | patch | Root `.gitignore` excludes `*.log`, including logs directly linked by the 2.3 evidence index. Preserve the referenced receipts in visible evidence files.  Resolved by the story-local .gitignore exception; linked redacted logs are visible to version control. |
| B3 — real startup disposal erases diagnostics | medium | patch | BeforeStart throws on a missing native runtime after hosted services are resolved; PlatformCompositionWorkspace.Dispose unconditionally deletes its path. Preserve a failure marker and its workspace through that demonstrated path.  Resolved with BeforeStart failure markers and marker-aware disposal; registered-service failure-disposal checks passed. |
| B4 — sibling mapped checkout accepts certificate writes | medium | patch | PlatformServerBindings accepts mapped owner paths outside Platform; ResolveCertificateParent compares only PlatformRoot. Extend the internal exclusion check to those mapped roots and test it.  Resolved by validating every mapped checkout root, including symlink targets; focused and ordinary checks passed. |
| B5 — crashed AppHost leaves external issuer child | medium | defer | AppHost disposal owns the external child; forced termination does not run it. Crash/lifecycle retention policy is explicitly excluded from this intent and belongs with runner-owned lifecycle cleanup; record the external-path discovery need. |
| B6 — escaped Helm credential keys | false | reject | The concurrently owned Helm chart/template has been removed from the current workspace during its own re-derivation; the cited rendering path no longer exists. Preserve that independent work. |
| B7 — Helm credential collisions | false | reject | The cited `_helpers.tpl` credential loop is absent from the current tree after independent re-derivation; there is no current chart consumer to patch here. |
| B8 — alternate loopback chart authority | false | reject | The cited Helm endpoint helper no longer exists in the current tree. This independent chart work is outside the local reference-run implementation. |
| B9 — unbounded candidate output | false | reject | Independent runner work now uses selectors and os.read with a remaining 1 MiB+1 budget, aborting overflow while reading; communicate() is gone from run_candidate. |
| B10 — empty Memories positive search passes | false | reject | Independent Memories check now requires any fixture_result in results, including a valid memoryUnitId and matching fixtureCaseId; the reported empty array fails. |
| B11 — native sidecar listener inventory incomplete | low | patch | Container bindings plus persisted plan ports omit secondary native sidecar ports. Record their actual HTTP/gRPC/internal-gRPC/metrics arguments and include them in overlap checks.  Production metadata receipts and proof assertions now include all four native listeners; focused checks passed, final live assertion execution remains blocked before readiness. |
| E1 — cleanup failure skips the peer | medium | patch | The finally loop awaits and asserts each DownAsync directly; an exception exits before the next session. Collect cleanup errors and report them after attempting every owned session.  Resolved by aggregate cleanup after every attempt and receipt; both exception and leftover failure cases passed. |
| E2 — unbounded candidate output | false | reject | Same independently corrected run_candidate path as B9: bounded incremental reads and process termination replace full communicate buffering. |
| G1 — external certificate-parent disposal unverified | medium | patch | The gap review verified no test disposes the registered external-child services for two peers sharing a parent. Add that ownership assertion, including foreign files and peer credentials.  Resolved by actual registered-service disposal for two peers sharing a parent, preserving parent/foreign file/peer credentials; passed. |
| G2 — Works qualification adapter rejects native Sentry address | medium | patch | The Works harness changes the model Sentry listener and selects an adapter that accepts only localhost:50001. The subscriber captured the earlier allocated address. Use the actual current model listener and permit the adapter's exact bound endpoint while retaining its refusal of foreign endpoints.  Resolved by callback-time model-port lookup, bound loopback spelling, and exact adapter acceptance. Actual Works-generated endpoints passed through the real adapter; foreign endpoints remain rejected. |
| V1 — duplicate owner/catalog package declarations | medium | patch | The clean EventStore owner props repeated the current catalog cryptography entries. Conditional additions plus updates preserve 10.0.13 with either catalog shape; both evaluations and the normal Composition build passed. No selected version or independent workspace edit was changed. |




| R2-B1 — late startup disposal | false | reject | The demonstrated file-host startup failure retained its actual cutover exception and health diagnostics in Aspire logs and this attempt's evidence, even after workspace disposal. The claim does not establish loss of the diagnostic output; missing-native-runtime marker retention remains covered by B3. |
| R2-B2 — failed starts excluded from harness cleanup | medium | patch | StartAsync can return RunId without Session after incomplete teardown; the harness records only successful sessions. Resolved by tracking each returned owned ID and session before readiness assertions; session-based shutdown is preserved for successful starts, with by-ID retries for failed starts. Final live cleanup passed. |
| R2-B3 — external issuer crash cleanup | medium | defer | carried B5: external issuer children still rely on AppHost disposal; forced-termination retention policy is explicitly excluded and remains recorded for runner lifecycle cleanup. |
| R2-B4 — direct native version attestation | medium | defer | The new native command selects the same runtime home previously selected by Dapr CLI; direct entry already lacked runtime-version attestation. Current proof uses the verified catalog runtime; record the pre-existing validation gap without changing pins. |
| R2-B5 — expanded concurrent proof absent | false | reject | Current normal-build proof passed 265.071 seconds with both six-server sessions ready, all native listeners inventoried, independent persisted values, survivor readiness and complete cleanup. The cited prior failure is historical. |
| R2-B6 — file-entry qualification absent | medium | patch | The current smoke starts actual resources but fails on gateway health: actor placement disconnected and writer protocol inactive. Resolved by direct placement/scheduler gRPC bindings: actual isolated listeners now match native addresses and scheduler advertisements. File-entry smoke passed all six servers and teardown. |
| R2-B7 — FIFO contract open | medium | defer | TestEnvironmentContractReader opens the path before CanSeek/cancellation. This is independent Builds contract work introduced by its test-environment implementation; preserve it and record the nonblocking-open requirement. |
| R2-B8 — Custody worker socket prerequisite | medium | defer | Program checks socket absence before the worker branch, so an active serving socket rejects a separately launched worker. Independent hosted Custody implementation is excluded from local composition and remains unchanged. |
| R2-B9 — Custody exceptional buffer cleanup | medium | defer | BindingService zeroes key-bearing buffers only after successful provider/serialization calls; exceptions bypass cleanup. This independent hosted Custody path is excluded from local reference-run work. |
| R2-B10 — oversized independent evidence | low | defer | The full baseline snapshot includes about 599 MB across 9,271 sections, dominated by independent EventStore receipts. These immutable own-repository fixtures are excluded from Platform integration evidence and are not rewritten. |
| R2-E1 — external issuer child after crash | medium | defer | carried B5 and R2-B3: same AppHost-disposal-only ownership path and excluded forced-termination lifecycle policy. |
| R2-E2 — claim release throws during teardown | medium | defer | ReleaseOwned obtains a filesystem transaction that can throw IOException after two seconds; it runs outside the teardown catches. Independent Builds resource-claim lifecycle work was introduced by db5abf96d730d82fd6c1ccfa20ffd9c2262b9838, outside the frozen local-composition change. |
| R2-E3 — retention fixture mounts credential parent | low | patch | The fixture discards AddSentry's returned private child and mounts its parent for placement/scheduler. Resolved by passing the returned child and asserting both read-only control-plane credential mounts; ordinary checks passed. |
| R2-G1 — startup claim ownership test gap | medium | defer | The verification reviewer confirmed startup acquisition can be removed without failing existing readiness/teardown assertions. This tests independent Builds resource-claim lifecycle implementation from db5abf96d730d82fd6c1ccfa20ffd9c2262b9838; defer its targeted startup/peer assertions. |
| R2-G2 — parent HXR031 translation gap | medium | defer | The verification reviewer confirmed child conflict serialization is covered but parent startup translation and malformed blocking-owner IDs are untested. This is independent Builds resource-claim lifecycle policy, explicitly excluded from this change. |
| R2-V1 — Timesheets subscriber fixture lacks application | low | patch | The full ordinary suite reproduces a Single lookup failure because its model contains only timesheets-dapr-cli. Resolved by adding the expected application resource and selecting the sidecar explicitly; the affected Timesheets group passed. |
| R4-B1 — claim conflict allocation retry | false | reject | carried BH3/EC12: claim conflicts identify the blocking run, which is the epic's permitted failure behavior; retry policy belongs to the excluded Story 2.4 claim lifecycle. |
| R4-B2 — workspace removed with cleanup survivors | medium | defer | Builds CompositionEngine.TearDownAsync calls TryDeleteAsync after Scanner.RemoveAsync even when Remaining is nonempty or unverified. Surviving owned containers may lose bind-mounted data; runner lifecycle policy is explicitly excluded from this intent. |
| R4-B3 — retained failure workspace includes secrets | false | reject | The retained directory is private run-owned state, deliberately preserved for diagnosis, and the accepted documentation patch names its secrets and manual removal after owned shutdown. No credential disclosure or foreign write path was demonstrated; automatic secret deletion would introduce a different retention policy. |
| R4-B4 — FIFO contract open | medium | defer | carried R2-B7: the independent Builds reader opens synchronously before regular-file/cancellation checks; nonblocking special-file refusal remains recorded with that owner. |
| R4-B5 — future invocation extends local timeout | medium | defer | checks/reference/runner.py bounds deadlineAt minus startedAt but Context.remaining and SIGALRM use deadlineAt against current time. A future startedAt extends this local bound; the trusted executor retains its own timeout. This independent hosted check payload is outside local composition and descriptor policy is excluded. |
| R4-B6 — serving socket prevents worker | medium | defer | carried R2-B8: Program checks socket absence before selecting the worker branch. Independent hosted Custody behavior is excluded from this intent. |
| R4-B7 — duplicate tenant KEK coverage | medium | defer | ComponentProfile.Validate checks two distinct protector ARNs but permits both KEKs for one enrolled tenant. AwsKeyAccess.ReadTenantKekAsync then refuses the uncovered tenant at use time; this independent hosted Custody profile requires owner-side tenant coverage validation. |
| R4-B8 — actual project listeners omitted from proof | medium | patch | Resolved by requiring valid observed HTTP(S) endpoints for every project and adding their ports to the cross-run comparison. The strengthened six-server live proof passed in 258.577 seconds with six project endpoints and 24 native listeners per run, zero overlap and complete cleanup. |
| R4-B9 — no persistence reload after own restart | false | reject | The claimed own-process-restart guarantee is not part of this story's proof. Its required behavior is separate persisted state and survivor health after peer shutdown: the test writes distinct same-key values, saves separate on-disk snapshots, and rereads both stores and the survivor. Persistence wiring is also asserted by the executed model tests. |
| R4-B10 — late startup diagnostics erased | false | reject | carried R2-B1: the demonstrated actual startup failure retains its cutover and health diagnostics in Aspire logs and run evidence; missing-native marker retention is separately covered. Workspace retention for every later callback is not the only diagnostic channel. |
| R4-E1 — obsolete older-catalog fallback claim | false | reject | Both supported pinned catalogs supply Pkcs and Xml at 10.0.13; the accepted round-3 correction explicitly records catalog-only pins and no fallback. The obsolete historical task wording does not establish a failure under a supported catalog; a spec-only correction is rejected by this review workflow. |
| R4-V1 — native KMS identity rejection untested | medium | defer | The verification reviewer confirmed only production Program instantiates AwsKeyAccess; existing key tests use FakeNative, so removing the DecryptResponse.KeyId comparison would remain undetected. This independent hosted Custody provider is explicitly outside local reference composition; add an adapter test with a mismatched response ARN in its owner work. |


## Design Notes

No unresolved intent gaps or irreversible changes. This spans shared hosting, rendering, tests and documentation, so dispatch is appropriate. Allocation probes currently release sockets before startup: do not describe them as cross-process reservations. Bind conflicts must fail safely; two-run qualification must exercise real allocation and binding.

## Verification

Build individual Composition and Builds Module test projects in Debug with `-m:1`; run their xUnit executables using focused `-class`/`-method` selections, then relevant ordinary suites. Run existing file-entry smoke and new concurrent-run proof with catalog-pinned Dapr/Aspire. Preserve commands, source revisions, readiness/resource observations, independent persisted values and cleanup results. Model assertions alone cannot satisfy live acceptance.

Baseline `aspire start --apphost /home/administrator/projects/hexalith/platform/apphost.cs --isolated --format Json --non-interactive` exited 2 before resources: independent Agents/Builds checkout versus staged-gitlink mismatches. Preserve revisions and report unresolved live blockers separately; never weaken identity checks. Planning observed Platform `6d920be8018bfe5a6660cc2f25a98db877ed06aa`, Builds `6635fd2c18f6e9c9c22cba5f534387b47b80ab84`; concurrent work may advance them.


### Implementation verification (2026-10-22)

- Ordinary Composition suite: 138 total, zero failures, three gated skips; final matrix: seven passed. Builds runtime subset: 22 passed (renderer: four).
- Gated concurrent proof: one passed, zero skipped, 403.261 seconds. Both six-server sessions reached readiness; disjoint resources, separate credentials/discovery, three independent persisted-store snapshots, survivor readiness/values and complete owned cleanup were verified.
- Gated occupied-port proof: one passed, zero skipped, 2.409 seconds. Docker refused the occupied model-derived Sentry binding; diagnostics and a separately owned listener/marker survived candidate-only cleanup.
- Required normal builds remain separately blocked by independent Builds analyzer changes; a later source dependency build failed in EventStore.Contracts. Supplemental builds pass with the documented narrowed compiler settings.
- Existing file-entry live smoke was executed and failed before resources on independent Custody/EventStore package-contract compilation. Its graph/native callback tests pass, but that live path is not qualified by the Builds result.
- Full commands, revisions, startup/readiness/resource observations, persisted values, cleanup and limitations: [verification index](evidence/2-3/README.md). No gitlinks were staged and no foreign application was stopped.

### Final verification gate after review corrections (2026-10-22)

The normal Composition Debug build passed with zero warnings/errors after a narrow owner-props compatibility correction: existing cryptography catalog items are updated, while older catalogs get the same 10.0.13 fallback additions. Both catalog evaluation cases passed. The owner file was clean before this correction; existing independent edits and all selected versions were preserved.

141 ordinary Composition cases passed against the normal build (146 total, five gated skips); 22 Builds runtime cases passed. Review checks passed 13 C# cases and eight adapter cases; the additional actual Works-endpoint/real-adapter assertion passed. The occupied-port proof passed again. Builds' normal Debug build also passed before independent lifecycle implementation files changed during verification.

The latest Builds normal build now fails 37 analyzer checks in that independent lifecycle work. Latest file-entry smoke was executed and failed before AppHost resources started on those same errors. Latest concurrent proof was executed and failed HXR027, `cutover-timeout;health-HTTP-503`, before first-session readiness. Its final native-listener overlap assertions remain unexecuted. The earlier 403.261-second concurrency pass is retained as historical evidence, not final-tree qualification.

The required current build/live gate is unresolved, so this spec remains `in-review` and sprint story 2.3 remains `in-progress`. Descriptor/attachment/lifecycle policy is explicitly outside the frozen intent; no changes were made to that independent work and no readiness, source-identity or mTLS guard was weakened. Both failed concurrent runs have no labelled containers or recorded processes remaining. See the [verification index](evidence/2-3/README.md), [startup diagnostics](evidence/2-3/live-concurrent-qualified/startup-failure.json), and [cleanup audit](evidence/2-3/final-cleanup-audit.json).

### Resumed final verification gate — 2026-10-23

The required normal builds now pass with zero warnings/errors. The affected Platform/Timesheets group passes 196 cases (202 total, six gated skips); Builds runtime verification passes 22 cases. The cleanup-focused checks pass three cases with the live case gated separately.

The direct file-entry smoke passes all six servers and owned shutdown in 115.514 seconds. Its former gateway HTTP 503 was traced to isolated mode randomizing the proxied placement/scheduler listeners while native arguments and scheduler advertisements retained allocated ports. Direct gRPC bindings align the actual addresses without changing trust, readiness, container target ports or persisted plan values. The occupied-port proof passes in 1.762 seconds.

The final expanded concurrent proof passes in 225.417 seconds: both six-server sessions ready together; all 24 native listeners per run inventoried and disjoint; separate credentials/discovery/storage; distinct persisted values under identical keys; fresh survivor readiness and values after peer shutdown; both final owned cleanups successful. The first cleanup-harness revision exposed by-ID shutdown's missing live-session stdin channel. Successful sessions now retain their original shutdown path, while failed starts are tracked by ID for retry; the final proof covers that correction.

The original running AppHost is unchanged, no labelled proof containers remain, and source hashes stayed stable during the final proof. No gitlinks were staged, independent edits adopted, checks disabled or tools repinned. The initial broader suite's unrelated release-pin and unchanged test-host content-root failures remain explicitly recorded, separate from the passing story gate. See the [complete resumed verification](evidence/2-3/resume-20261023T065107Z/README.md) and [cleanup audit](evidence/2-3/resume-20261023T065107Z/cleanup-audit.json).

### Round-3 review patch verification — 2026-10-23

All fifteen Patch findings are implemented and verified. The normal Composition and Works integration Debug builds pass with zero warnings/errors. The affected ordinary Composition suite passes 143 cases (147 total, four expected live skips); eight focused matrix cases and thirteen Works hosting/harness cases pass without skips. The bounded occupied-port proof passes in 2.410 seconds with both task containers removed and the separately owned listener/marker preserved through candidate cleanup.

No production composition source changed. Works test-harness trust and replica setup now read the composed Sentry credential mount. Model coverage asserts Redis ownership, rendered Sentry identity, disjoint native listener ports, persistence flags, callback-written receipts, credential-child symlink refusal and original Works runtime/control-plane selection. Documentation now identifies shared developer build outputs, failure-retained file-entry workspaces, fixed raw occupied-port evidence, complete proof builds and the current catalog-only cryptography pins. The existing deferred Works preview live qualification remains deferred. Commands, revisions, hashes and full results are in [round-3 patch evidence](evidence/2-3/review-patches-20261023/README.md).

### Final review and current qualification — 2026-10-23

All sixteen accepted Patch findings are implemented. The final Composition, Builds Module test/AppHost and Works integration builds pass without warnings or errors. The final Platform/Timesheets suite passes 198 cases (204 total, six gated live skips); the Builds runtime subset passes 22 cases. The strengthened concurrent proof executes and passes in 258.577 seconds: six healthy projects per run, six observed project HTTP endpoints plus 24 native listeners per run, independent store values, survivor health and complete owned cleanup. Source hashes remained stable and both pre-existing AppHosts were preserved. Four independent owner findings were appended to deferred-work; carried dispositions were retained.

The current file-entry gate is blocked before resource creation. The Builds catalog now selects Aspire 13.6.3, while the unchanged file AppHost directives still select 13.6.1 integrations; NuGet refuses the Kubernetes, Keycloak and Redis downgrades (NU1605). The earlier passing file-entry receipt remains historical and does not qualify this changed dependency state. No pin or readiness check was weakened. The spec remains `in-review`, and sprint story 2.3 remains `in-progress`.

A private Aspire 13.6.3 installation and a private Dapr 1.18.7 tool home resolved the concurrent runner's prerequisite failures without changing global tools or repository pins. The latter reused binaries from the already cached pinned image; its stopped extraction container was removed. The file-entry pin-alignment proposal is saved but unapplied because the frozen intent preserves tool pins. See [current verification and proposed correction](evidence/2-3/review-final-20261023/README.md).

### Completed after approved Aspire alignment — 2026-10-23

The user approved the prepared upgrade to Aspire 13.6.3. The five root file AppHost SDK/integration directives now match the existing catalog selections; all other pins remain unchanged. The direct file-entry smoke passes without skips in 152.089 seconds, asserting readiness for all six servers and successful owned shutdown. Source hashes remained stable and both pre-existing AppHosts were preserved.

The current qualification gate is complete: the ordinary suites, normal builds, occupied-port proof, strengthened concurrent proof and direct file-entry smoke all pass. The spec is `done`; sprint story 2.3 is `review` per the Build workflow. Independent lifecycle/provider findings and Works preview live qualification remain explicitly deferred. See [approved alignment evidence](evidence/2-3/review-final-20261023/approved-aspire-13-6-3/README.md).
