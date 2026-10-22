---
title: '2.3 Make local composition resources run-scoped'
type: 'feature'
created: '2026-10-22'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
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

- [ ] `DaprSelfHostedMtls.cs` — replace shared host/storage/certificate defaults with run-owned allocations; keep container target ports and mTLS unchanged.
- [ ] `src/Hexalith.Platform.Composition/PlatformAppModel.cs`, `PlatformDaprEndpointSubscriber.cs`, `PlatformDaprBindings.cs` — wire allocated control-plane endpoints, private credentials and discovery through rendered configuration and actual sidecar commands in both entry paths. Reuse ownership/cleanup boundaries.
- [ ] `src/Hexalith.Platform.Composition/WorksPreviewModel.cs` — adapt shared-helper calls to their revised allocation contract.
- [ ] `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionDaprComponentRenderer.cs` and `test/Hexalith.Builds.Module.Tests/CompositionDaprComponentRendererTests.cs` — share discovery rendering without changing signed-plan semantics; verify private paths and escaping.
- [ ] `tests/Hexalith.Platform.Composition.Tests/PlatformRunIsolationTests.cs`, `PlatformDaprToolchainTests.cs`, `PlatformRetentionProfileTests.cs`, `PlatformDaprBindingsTests.cs` — test the matrix, allocated addresses in actual executable callbacks, independent certificates/storage/discovery and retained security behavior; update obsolete fixed-resource assertions.
- [ ] `tests/Hexalith.Platform.Composition.Tests/PlatformConcurrentRunTests.cs` — gated real-run proof using two Builds sessions selecting the six-server reference graph; verify readiness, disjoint resources, independent persisted data, and survivor health after stopping its peer.
- [ ] `README.md`, `docs/platform-apphost-form.md` — replace the one-run restriction with scoped-resource inventory, prerequisites and verification instructions; distinguish legacy fixtures and preview scope.
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` — update only this story as implementation progresses. Save verification under `_bmad-output/implementation-artifacts/evidence/2-3/`.

**Acceptance Criteria:**

- Given ordinary signed and file-entry runs, when their graph and executable callbacks are evaluated, then Sentry, placement and scheduler use allocated host ports, run-owned writable storage and matching sidecar addresses; no global scheduler volume is selected.
- Given the resource inventory, when reference composition starts, then every mutable dependency is owned by that run and any shared input is explicitly read-only; legacy Redis, fixture-lock and Keycloak assumptions are excluded by the actual graph.
- Given two reference runs on one machine, when both reach declared readiness and write distinct values under identical test keys, then each store retains its own value, discovery and credentials remain separate, and stopping one leaves the other healthy.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

No unresolved intent gaps or irreversible changes. This spans shared hosting, rendering, tests and documentation, so dispatch is appropriate. Allocation probes currently release sockets before startup: do not describe them as cross-process reservations. Bind conflicts must fail safely; two-run qualification must exercise real allocation and binding.

## Verification

Build individual Composition and Builds Module test projects in Debug with `-m:1`; run their xUnit executables using focused `-class`/`-method` selections, then relevant ordinary suites. Run existing file-entry smoke and new concurrent-run proof with catalog-pinned Dapr/Aspire. Preserve commands, source revisions, readiness/resource observations, independent persisted values and cleanup results. Model assertions alone cannot satisfy live acceptance.

Baseline `aspire start --apphost /home/administrator/projects/hexalith/platform/apphost.cs --isolated --format Json --non-interactive` exited 2 before resources: independent Agents/Builds checkout versus staged-gitlink mismatches. Preserve revisions and report unresolved live blockers separately; never weaken identity checks. Planning observed Platform `6d920be8018bfe5a6660cc2f25a98db877ed06aa`, Builds `6635fd2c18f6e9c9c22cba5f534387b47b80ab84`; concurrent work may advance them.
