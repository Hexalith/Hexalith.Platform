# Epic 5 Context: Gate every release on exact-release staging evidence

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Require complete, current staging evidence for the exact retained release before either automatic or Administrator-approved production promotion. Prove fresh installation when production has no baseline; otherwise qualify compatibility and rollback, or the approved path's named retained-data recovery. Keep failed attempts visible and return staging to a representative state before testing later incompatible candidates. Publish a stable McpCli version only after applicable staging validation passes.

## Stories

- Story 5.1: Decide the staging evidence policies
- Story 5.2: Decide the image and dependency vulnerability policy
- Story 5.3: Validate critical-flow and smoke declarations for every included module
- Story 5.4: Review tenant-lifecycle flows and handle leftover synthetic tenants
- Story 5.5: Scan module and composed images against the vulnerability policy
- Story 5.6: Run exact-release staging E2E in a serialized staging attempt
- Story 5.7: Rehearse a fresh install when production has no baseline
- Story 5.8: Rehearse the rollback set and record compatibility evidence
- Story 5.9: Qualify the named recovery for a retained-data approved attempt
- Story 5.10: Reset staging for unadopted incompatible candidates
- Story 5.11: Evaluate staging evidence for promotion

## Requirements & Constraints

Every included module, including unchanged modules, infrastructure modules and tools, supplies a non-empty critical-flow declaration with complete required E2E mappings. Tests exercise deployed real services. Missing or invalid declarations and failed, skipped, incomplete or absent required results block promotion. Isolated tests, integration tests and health checks do not replace critical-flow staging E2E. Removing or remapping a critical flow requires module-owner review; matching digests alone cannot justify reduced coverage.

Evidence identifies the served release and the required suite, runtime profile and configuration digests. It has a finite maximum age. Wrong-release, stale or mismatched evidence fails the gate. Reruns preserve prior failures and incomplete attempts; a staging failure blocks its candidate. Evidence policies fix triggers, rerun acceptance, flow-change review and the staging-reset duration, with owners and revisit conditions.

Builds and Platform decide the image and dependency vulnerability policy before the first staging promotion: scanner, blocking severity and exception approval, recording and expiry. Scan module and composed images at intake. Findings at or above the threshold block promotion unless covered by a recorded, unexpired exception. Retain every scanned image digest and scanner-database identity with release evidence. Module-image attestation is a distinct prerequisite that precedes publication.

Tenant creation and deletion during E2E are limited to owner-reviewed lifecycle flows using run-scoped synthetic tenants under staging-only admission. Exclusion from real-tenant views follows the immutable, attested synthetic-creation marker. Leftover tenants are removed or reported with their creating run. Production smoke has its own non-empty declaration; necessary writes use synthetic identities and data, with no real-user change, external effect, or tenant creation/deletion.

## Technical Decisions

Promote the same immutable Helm package and images qualified in staging. The release record binds artifacts, image digests, composed-host identity, routing content, profile template, configuration and check-suite evidence. Staging accepts evidence-validated EventStore server packages. Staging and production profile digests must agree, and effective infrastructure inputs must remain within qualified sets.

One serialized environment attempt holds the lock and monotonic epoch across preparation, deployment and evidence collection. Cut a staging recovery point before candidate writes. Executors keep module-supplied E2E and smoke code sandboxed away from deployment credentials. Provenance, isolation and the staging gate apply equally to both release modes; Administrator approval does not waive them.

Without a production baseline or retained application data, qualify fresh installation without inventing a rollback target. With a baseline and eligible compatibility evidence, start from that baseline, upgrade to the exact candidate and rehearse its prepared rollback set with HotReload off. Exercise a candidate-new idempotent command and verify that baseline code reads candidate state and events. A failed rollback rehearsal is breaking compatibility evidence. Catalog, key and secret retirements remain expand-only relative to the working baseline.

A retained-data approved attempt lacking safe compatibility needs a named, rehearsed data recovery with retained artifacts, a recovery point, authority, verification and lost-window treatment; it prepares no automatic rollback set. An unadopted candidate's incompatible writes require an in-place staging restore as a separate attempt before a later candidate starts. Keep referenced recovery points, regenerate the staging realm and remove candidate-only environment objects. Additive candidates need no reset.

## Cross-Story Dependencies

The approved [staging evidence policy v1](../../docs/contracts/staging-evidence-policy-v1.md)
(`hexalith.platform.staging-evidence-policy.v1`, Story 5.1) is the canonical
decision record for Stories 5.3, 5.6–5.11 and the related lifecycle review in
Story 5.4. It fixes a 24-hour (86,400-second) exclusive evidence deadline from
attempt start at promotion admission; automatic attempts for retained intake
candidates or checked-input/baseline changes, with explicit Maintainer requests
after expiry/failure and no periodic retry loop; full passing reruns from only
the latest issued serialized attempt, preserving history without cross-decision
reuse; owning-module and Platform approval of flow removal/remap, with Tenants
review for lifecycle changes; and a 60-minute (3,600-second) reset lifetime from
recovery entry that notifies Administrator and the Platform Maintainer without
aborting restoration or unblocking later candidates before verification.
Platform Maintainer `@jpiquot` is accountable. Builds and Platform must implement
and qualify adoption; modules must record flow reviews, and Administrator and
EventStore must qualify resets. Publication does not establish enforcement,
record unprovided owner approvals or authorize promotion.

The owned reference check suites precede declaration validation. Staging policy decisions precede evidence acceptance; the vulnerability decision precedes scan enforcement. Tenants and module owners review lifecycle flows before gate enrollment. Module recovery hooks are prerequisites for staging resets and applicable named recoveries.

The no-baseline branch enables the first production deployment. Baseline-dependent rollback and retained-data qualification follow establishment of the first working production baseline and must close before accepting a second production candidate. Only the Platform publication workflow may turn an attested McpCli candidate into a stable version after the complete applicable staging gate passes.
