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

Without a production baseline or retained application data, qualify fresh installation without inventing a rollback target. With a baseline and eligible compatibility evidence, start from that baseline, upgrade to the exact candidate and rehearse its prepared rollback set with HotReload off. Exercise a candidate-new idempotent command and verify that baseline code reads candidate state and events. A failed rollback rehearsal is breaking compatibility evidence. Staging catalog, key and secret retirements follow AD-15's expand-only rule relative to production's working baseline.

A retained-data approved attempt lacking safe compatibility needs a named, rehearsed data recovery with retained artifacts, a recovery point, authority, verification and lost-window treatment; it prepares no automatic rollback set. An unadopted candidate's unreadable or unproven-readability writes require a verified in-place staging restore as a separate attempt before a later candidate starts or a same-candidate rerun. Retain recovery points under the policy's retention rule, regenerate the staging realm and remove candidate-only environment objects. Additive candidates need no reset.

## Cross-Story Dependencies

The approved [staging evidence policy v1](../../docs/contracts/staging-evidence-policy-v1.md)
(`hexalith.platform.staging-evidence-policy.v1`, Story 5.1) is the canonical
decision record for Stories 5.3, 5.6–5.11 and the related lifecycle review in
Story 5.4. It fixes:

- **Evidence age.** Evidence has a 24-hour (86,400-second) exclusive deadline
  from attempt start, checked at promotion admission.
- **Triggers.** A retained candidate passing intake opens a pending decision and
  queues an attempt automatically. Checked-input and baseline changes queue
  attempts under that decision. Checked inputs include the preparation branch and
  the branch-determining content of the Administrator approval selecting it.
  Re-intake of a release with a pending decision queues nothing by itself.
  Intake of the release production's latest-working pointer currently names opens
  no decision and queues no attempt. An attempt whose decision is no longer
  pending when it reaches the staging lock is cancelled with its reason; it never
  starts and supersedes nothing. The Maintainer may request a fresh attempt for a
  pending decision after expiry or a failed, cancelled, incomplete, interrupted or
  superseded E2E terminal outcome. There is no periodic retry loop.
- **Reruns.** Only the latest issued serialized E2E attempt can qualify, with a
  full passing rerun. History is preserved, evidence is never reused across
  decisions, and reset or named-recovery evidence never replaces an E2E attempt.
  A decision closes as completed when production's latest-working pointer names
  its release, before that baseline change queues triggers, or as superseded.
  Superseded decisions refuse admission and receive no automatic triggers.
- **Flow review.** The owning module and Platform approve flow removal/remap
  against each applicable latest-accepted and production-baseline predecessor.
  Tenants reviews every lifecycle flow before enrollment and before any changed
  declaration joins the gate, and also approves lifecycle-flow removal/remap.
- **Reset lifetime.** The reset lifetime is 60 minutes (3,600 seconds) from
  recovery entry. The off-site monitor notifies Administrator, the recovery
  deputy and the Platform Maintainer through the architecture's accepted
  private-issue path. It does not abort restoration or unblock later candidates
  before verification. Executor loss interrupts the reset and leaves it
  non-terminal, so the lifetime notification still applies, until re-entry
  records it superseded. An observation gap, a failed or incomplete restore or
  failed verification leaves the reset unverified. Only the initial reset is
  issued automatically. After executor loss or an unverified reset,
  Administrator or the deputy re-enters from the same recovery point in a
  distinct attempt. The re-entry
  inherits the prior deadline before it expires and records entry plus 3,600
  seconds at or after it.
- **Reset triggers.** Unreadable or unproven-readability candidate writes,
  including without a production baseline, require a verified reset before a
  same-candidate rerun or later candidate. The reset uses the earliest pre-write
  point since the last verified reset.
- **Preparation.** Missing applicable preparation proof or an unmatched state
  refuses admission.

Qualification also depends on Epic 6. Story 5.6 completed closure requires
Story 6.16's latest-working pointer move. Story 5.10 reset qualification
requires Story 6.4's off-site monitor for maximum-lifetime notification, Story
6.3's recovery authority for deputy re-entry and Story 6.15's takeover fencing.
Story 5.11 admission qualification requires Story 6.14's precondition 4 check
under the production lock and new epoch.

Platform Maintainer `@jpiquot` is accountable. Builds and Platform must implement
and qualify adoption; modules, Platform and Tenants must record their required
flow approvals and reviews, and Administrator and EventStore must qualify resets.
Publication does not establish enforcement, record unprovided owner approvals or
authorize promotion.

The owned reference check suites precede declaration validation. Staging policy decisions precede evidence acceptance; the vulnerability decision precedes scan enforcement. Tenants and module owners review lifecycle flows before enrollment and before any changed declaration joins the gate. Module recovery hooks are prerequisites for staging resets and applicable named recoveries.

The no-baseline branch enables the first production deployment. Baseline-dependent rollback and retained-data qualification follow establishment of the first working production baseline and must close before accepting a second production candidate. Only the Platform publication workflow may turn an attested McpCli candidate into a stable version after the complete applicable staging gate passes.
