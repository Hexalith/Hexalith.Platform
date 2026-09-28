# Extract: CI candidate artifact binding (A1), attachment lifecycle (A2), architecture drift

Extracted 2026-09-28 for the PRD update. Source material only; no PRD, addendum or architecture file was edited.

Sources (all under `_bmad-output/planning-artifacts/`):

- PRD `prds/prd-platform-2026-09-27/prd.md` (385 lines, frontmatter `updated: 2026-09-27`), addendum `.../addendum.md` (350 lines), PRD memlog `.../.memlog.md`.
- Validation finding detail: `.../validate-2026-09-28/review-adversarial.md` (A1 L9–19, A2 L21–31).
- Architecture: `architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md` (487 lines, abbreviated below as "spine"), `.memlog.md` (261 lines), plus `reviews/update-2026-09-28/` and `reviews/update-2026-09-28-r3/`.

Acceptance convention: "user-accepted" means the memlog line is a `(decision)` entry recording a user selection or acceptance, or an ADOPTED AD entry that follows one. Spine rows are distillations of memlog decisions; the originating memlog line is cited where one exists.

---

## Task 1: A1, CI candidate artifact binding

### 1.1 The finding (review-adversarial.md L9–19, summarized)

CI integration uses Release/NuGet mode. Nothing binds the module artifact that CI actually exercises to the candidate source revision the result is claimed for. As a result, CI could exercise a previously published Parties package and pass. Suggested fix: CI integration acceptance for a candidate must identify and exercise the tested module's Release artifact built from that candidate revision, and must reject results for a different module artifact or revision. Dependency-version selection and package production stay with Platform/Builds. A baseline-only CI run, if supported, must be labelled so that it cannot satisfy candidate acceptance.

### 1.2 Current PRD and addendum text

- **prd.md:90** (FR-2): "The active module and directly declared Hexalith dependencies use source and Debug assets locally; other dependencies use packages at the shared Builds catalog version. CI/CD uses NuGet package references and Release assets. Missing required declared source or duplicate source/package identities fail explicitly, without sibling, ancestor, nested-Platform or package fallback."
- **prd.md:119** (FR-4): "Local integration-test environments run through Aspire using the module configuration and the active source checkout."
- **prd.md:120** (FR-4): "The configured environment supports both local and automated CI integration runs, following the local Debug/project-reference and CI Release/NuGet rules in FR-2."
- **prd.md:121** (FR-4): "Platform owns the integration environment lifecycle; tests consume its identified endpoints and composition. A technical module's independent repository tests may use its own fixture, but do not count as Platform integration evidence. CI environments use disposable runners without staging or production credentials."
- **prd.md:148** (FR-5): "CI runs isolated tests first and real-service integration tests afterward; passing isolated tests alone does not satisfy the module's integration acceptance."
- **prd.md:313** (Success measures preamble): "… Record the release or source revision, environment, result, and supporting diagnostics with each demonstration."
- **prd.md:317** (SM-3): "every MVP module demonstrates its declared minimum composition and required local Aspire/CI integration checks from its FR-2 workspace. …"
- **addendum.md:34**: "AD-4 resolves the active root once and uses the active module plus Hexalith dependencies declared directly by that root from source with Debug assets; other dependencies use packages at the Builds catalog version. CI/CD uses Release/NuGet artifacts. Missing required source, duplicate source/package identities and tool/submodule identity mismatches fail explicitly; sibling, ancestor, nested-Platform and package fallback are forbidden. In a module workspace the Platform submodule commit is the sole Platform identity: local mode runs that source, while the pinned CI tool must embed the same commit. Removing the historical preview's alternate paths remains implementation work."
- **addendum.md:52**: "… Local runs use Aspire and source assets; CI uses disposable Linux GitHub-hosted runners with Release/NuGet artifacts and no hosted-environment credentials. …"

### 1.3 Architecture search result

**No accepted architecture decision binds CI integration evidence to the candidate module's artifact or revision, rejects evidence produced from a different module artifact or revision, or defines a labelled baseline-only CI run.** Searches covered artifact, NuGet, candidate, revision, digest, provenance, package, Release, "module under test", "package parity" and "composition identity". Every exact-release and provenance binding in the architecture applies to the staging and production release path, not to CI integration evidence.

The closest accepted material follows, from nearest to furthest.

**(a) AD-4 package mode: the module under test is built in CI, not pulled from the catalog.** This is user-accepted.

- Spine L100 (AD-4 *Package mode*): "Outside the Platform repository the tool builds the Platform composition from the Platform submodule at a Platform release tag; in the Platform repository it builds from HEAD. The module under test builds in Release against NuGet library packages at the Builds catalog version; dependency services run from their released images at the catalog version, the same images staging runs; the composed host assembles from the server and extension packages."
- Origin: memlog **L235** (decision): "D2 (CI package mode, completes C-02/C-31): no new artifact. In package mode the Platform tool builds the Platform composition from the Platform submodule at a Platform release tag; the module under test builds in Release against NuGet library packages; dependency services run from their released images at the Builds catalog version, pulled with CI's read-only credential (the same images staging runs); … Supersedes the addendum L34 embedded-commit mechanism."
- Acceptance: memlog L234 opens the gate-decision set with "Gate decisions (user accepted recommendations except D4)". D2 is therefore user-accepted.
- What this establishes: the tested module is compiled in Release from the workspace, so CI does not select a published package of the module under test. That implicitly closes the "older Parties package from the catalog" scenario.
- What it does not establish: that the build input is the candidate revision; that the runner or result records the tested module's artifact or revision identity; that a result for a different artifact or revision is rejected as candidate evidence; or any baseline-only labelling.

**(b) AD-4 "Prevents" clause.** This is user-accepted as part of AD-4.

- Spine L95: "**Prevents:** debugging one checkout while executing another, or silently substituting packages, catalogs or Platform versions."
- Original adoption, memlog **L36** (decision): "AD-4 [ADOPTED from PRD FR-2/FR-3] — One active-root source identity. … Prevents: building one checkout while launching another, selecting embedded Platform references, or silently substituting packages for required local Hexalith source. … CI/CD retains the separate accepted Release/NuGet policy. …"
- As adopted, the clause is aimed at local source. The spine's broader wording ("silently substituting packages, catalogs or Platform versions") is a later distillation, but it gives candidate wording for a CI rule.

**(c) Platform-tool identity binding (Platform only, not the tested module).** This is user-accepted.

- Spine L102 (AD-4 *Platform identity*): "Every workspace other than the Platform repository that runs the tool declares `references/Hexalith.Platform` directly, and its submodule commit is the single Platform identity; in the Platform repository the identity is its HEAD. The identity pins the Builds catalog commit and a supported Platform-tool version range. In package mode the tool refuses a composition commit that differs from the submodule HEAD, a Builds submodule that differs from the pinned catalog, or a tool outside the range; source mode warns, and an untagged or dirty submodule is allowed only in source mode."
- Origins: memlog **L180** (V-08, decision, validate-run acceptance) and **L213** (C-31, Batch 1 "user accepted all recommendations"): "… the Platform composition loaded by hexalith-module carries its Platform source commit and CI refuses a mismatch with the submodule HEAD."
- **Drift note for A1 edits:** D2 (memlog L235) explicitly "Supersedes the addendum L34 embedded-commit mechanism". The addendum's clause "the pinned CI tool must embed the same commit" (addendum.md:34) is therefore stale. The current mechanism is: the composition carries its commit; the tool refuses a mismatch with the submodule HEAD, a Builds submodule mismatch, or a tool outside the supported range. The update-run-2 input reconciliation raised the same point as RI-13 (`reviews/update-2026-09-28/review-reconcile-inputs.md` L157–163). Memlog L241 lists "PRD addendum (tool identity, …)" among the pending source-alignment offers.

**(d) AD-5 and AD-10 CI tier and runner descriptor.** These are user-accepted. They say nothing on artifact identity.

- Spine L108 (AD-5 Rule): "Reuse Builds workflows on disposable Linux GitHub-hosted runners. Run isolated tests without Platform first, then real-service integration through the AD-10 runner in package mode as a blocking tier. CI never receives staging or production provider credentials; its only retained-registry access is a read-only pull credential. Staging Kubernetes E2E remains a separate mandatory gate."
- Memlog **L42** (decision: "User selected option 1 for CI hosting: Aspire on disposable Linux runners") and **L43** (decision, AD-5 [ADOPTED]): "… Prevents: routine CI using hosted application data, mixed Debug/source versus Release/package dependencies, or unmanaged cluster test environments. Rule: … compose configured real-service integration environments through Platform/Aspire on disposable Linux GitHub Actions runners using Release/NuGet-based artifacts. …"
- Spine L162 (AD-10 *Default run*): "… Wait for required readiness and successful startup tasks within the finite deadline, then supply endpoints, composition identity and the synthetic tenant identifier." The architecture does not define whether "composition identity" includes the tested module's artifact or revision.
- Spine L457 (Owned work, gate "Before accepting local/CI composition and testing evidence"): "… Prove active-root mapping, finite readiness, exact cleanup, retained-run listing and CI package parity; …" "CI package parity" is a qualification proof item, not a rejection rule.

**(e) Release-path bindings that could serve as mirror wording.** These are user-accepted, but they apply to staging and production, not CI.

- Spine L76 (AD-2 Prevents): "testing one release and deploying, rolling back to or recovering another."
- Spine L225 (Module intake): "Module releases enter through a versioned intake manifest in the Platform repository pinning package and image digests, each module's image-attestation identity and its release-evidence references. …" (origin memlog L180, V-08).
- Spine L232 (Workflows and provenance): "… module images carry attestations naming the module repository, workflow, protected ref and pinned Builds workflow ref, recorded per module in the intake manifest. …"
- Spine L278 (Staging gate): "… All required E2E checks, including the McpCli candidate's flows, pass for the exact release. Results carry the serving release, the production working baseline they were computed against, and the suite, profile and configuration digests; evidence has a declared maximum age, and rerun evidence never hides an earlier failed or incomplete attempt. Missing, skipped, failed, incomplete, stale or wrong-release results block promotion. …"
- Spine L286 (Verification): "… a timeout is a failure, and results from another release count as missing. …"
- Spine L305 (Production precondition 4): "Exact-release staging evidence exists within its maximum age (Staging gate)."

Memlog L241 names "module release-evidence references" in the intake manifest as the route by which module evidence enters the Platform release. No rule defines what those references must bind to. That is the natural hook for A1, but it is not decided.

### 1.4 How the PRD already binds staging E2E evidence to the promoted release (FR-6), for mirroring

- **prd.md:163**: "The required E2E tests exercise the release being considered for promotion in staging, using the deployed real services."
- **prd.md:164**: "Any required E2E test that fails, is skipped, is incomplete, or has no completed passing result prevents promotion; an absent test result is not a pass."
- **prd.md:166**: "Passing results identify the served release, required check suite, runtime profile and configuration. The same retained application artifacts are promoted; stale, mismatched or wrong-release evidence is refused. Platform and Builds define and enforce a finite evidence-age limit before enabling the gate."
- **prd.md:171**: "… Changes to declarations or checked inputs require matching evidence. …"
- **prd.md:189** (FR-7, production smoke): "… Results bind the intended served release. …"
- **prd.md:319** (SM-5): "both release modes enforce exact-release staging E2E evidence for the complete enrolled composition; …"
- **prd.md:324** (SM-C1): "zero releases in either mode promoted with failed, skipped, missing, incomplete, stale or mismatched E2E evidence; …"
- **addendum.md:171**: "… Staging evidence must identify the serving release, suite, profile, configuration and production baseline, and remain within its maximum age. Incomplete, stale or mismatched E2E evidence cannot authorize promotion in either mode. …"

Structural parallel: FR-6 has three parts. It says what is exercised (prd.md:163), what the result must identify (prd.md:166, first sentence), and that mismatched evidence is refused (prd.md:166, second sentence). FR-4 currently states only the mode (prd.md:120). It has no "identify" part and no "refuse mismatched" part for the tested module.

---

## Task 2: A2, attachment lifecycle when the owning run terminates

### 2.1 The finding (review-adversarial.md L21–31, summarized)

Run B explicitly attaches to run A's run-owned environment after passing the compatibility checks. If A succeeds, or is cancelled, while B is still testing, A cleans up its own resources and B loses its services. Ownership protection does not settle this, because the resources belong to A. Suggested fix: define the consequence of an accepted attachment when its owner finishes. Either keep the environment available until attached work terminates, or explicitly terminate and report dependent runs before cleanup. Define how the chosen rule respects failed-local retention, and add the ordering to the SM-3 attachment scenario. No coordination mechanism is needed in the PRD.

### 2.2 Current PRD text

- **prd.md:108** (section intro): "Module developers run isolated tests without starting Platform. Local integration-test environments use Aspire to run Platform's configured real services. Platform also supplies real-service environments for CI integration tests. Failed local test environments remain available for debugging; CI environments are automatically cleaned up after completion or cancellation. …"
- **prd.md:121** (FR-4): "Platform owns the integration environment lifecycle; tests consume its identified endpoints and composition. …"
- **prd.md:128**: "Test data is isolated so tests do not depend on data left by other tests or affect staging or production application data."
- **prd.md:129**: "A new suite or compatible batch gets a fresh run-owned environment by default, or fails with the conflicting run identified. Explicit attachment requires compatible composition, artifact mode, readiness and data isolation in a local or CI run-owned environment; attachment to hosted environments is refused."
- **prd.md:133**: "The environment can serve a test suite or compatible batch. A failed local test or startup attempt leaves surviving environment resources available for debugging; the developer can explicitly stop and clean up the retained environment."
- **prd.md:134**: "A successful local run cleans up automatically. The run's first terminal outcome determines cleanup or retention; later cancellation must not erase an environment retained after failure. Retained environments show their owner and age."
- **prd.md:135**: "Explicit cancellation of a local test run stops that run and cleans up resources it created. An Aspire environment started separately by the developer remains running."
- **prd.md:136**: "CI automatically cleans up its test environment after success, failure, or cancellation, including resources created before provisioning failed."
- **prd.md:137**: "Cleanup affects only the resources belonging to that test environment; it must not remove resources belonging to another active run or a hosted environment."
- **prd.md:138**: "Cleanup can be retried safely and reports remaining owned resources; an incomplete cleanup is not recorded as complete."
- **prd.md:139**: "Test results and useful diagnostic logs remain available after environment cleanup; keeping live resources is not the only means of diagnosing a failure."
- **prd.md:317** (SM-3): "**SM-3 — Test execution and lifecycle:** every MVP module demonstrates its declared minimum composition and required local Aspire/CI integration checks from its FR-2 workspace. Success, failure, startup-timeout, cancellation, collision and attachment scenarios demonstrate the FR-4 ownership/retention/cleanup rules, leftover reporting and accessible diagnostics without affecting another environment. Validates FR-4 and FR-5 at MVP acceptance and after lifecycle changes."
- **prd.md:327** (SM-C4): "zero deletions of another environment's resources, and no cancelled/finished CI run counted as cleaned up while its owned resources remain. Counterbalances SM-3."

### 2.3 Current addendum text

- **addendum.md:54**: "Default to a fresh run-owned environment per suite or compatible batch with isolated ports, resources and catalog. Local success cleans automatically; local failure retains a listable environment with owner and age. The first terminal outcome decides retention or cleanup. Explicit attachment is allowed only to compatible local or CI run-owned environments after composition, artifact-mode, readiness and data-isolation checks; hosted attachment fails closed. Cleanup is idempotent, reports leftovers and never treats a shared AppHost path as ownership."
- **addendum.md:105** (Selected behavior table row): "| Test explicitly attaches to a compatible local or CI environment | Cancelling the test does not stop an environment owned by another run or developer session. Hosted attachment is refused. |"
- Context rows from the same table: addendum.md:101 "| Local test succeeds | Clean up its run-owned environment. |"; :102 "| Local test fails | Retain its environment for debugging; already agreed. |"; :104 "| Developer explicitly cancels a local test run | Stop the run and clean up resources it created. |"; :106 "| CI succeeds, fails, or is cancelled | Clean up run-owned resources, including partial provisioning; already agreed. |"
- **addendum.md:108**: "The Platform runner owns lifecycle and applies the first terminal outcome; normal test-fixture disposal must not defeat retention of failed local environments. …"

### 2.4 Architecture search result

**The architecture has no decision on attached-run lifetime, and no decision on what happens to an attached run when the owning run succeeds, is cancelled or is cleaned up.** Searches covered attach, owner, lease, run-owned, reference count, cancel, retain and first terminal. AD-10 defines attach eligibility, the owner's outcomes and cleanup scope. It does not connect the two.

Accepted material, verbatim:

- Spine L156–164 (AD-10 [ADOPTED, AMENDED]):
  - L159: "**Prevents:** two lifecycle owners, stale implicit reuse, lost debug evidence or cleanup of another environment."
  - L161 (*Owner*): "Only the runner provisions, readiness-waits, records ownership of and cleans multi-module integration environments. …"
  - L162 (*Default run*): "A fresh run-owned environment per suite or compatible batch, with run-scoped ports, resources and catalog instance, or a fast failure naming the blocking run. …"
  - L163 (*Outcome*): "Local success cleans; local test or startup failures are retained and listable with owner and age; explicit cancellation cleans only that run; CI always cleans, including after partial startup. A run's first terminal outcome alone decides retention or cleanup."
  - L164 (*Attach and cleanup*): "Explicit attach is allowed only to local or CI run-owned environments with compatible composition, mode, readiness and data isolation; attaching to hosted environments fails closed. Cleanup is idempotent, removes resources rather than only metadata, and reports leftovers; an incomplete cleanup is never recorded as complete, and a shared AppHost path alone is not ownership."
- Spine L457 (Owned work): "Implement the AD-10 lifecycle in `hexalith-module`: collision failure, deadline and override, first-terminal-outcome retention, listing with owner and age, compatibility-checked attach, resource cleanup reporting leftovers, CI partial-start cleanup and the environment descriptor; … Repeat SM-3 lifecycle scenarios after runner lifecycle changes."

How the decision evolved (memlog). This explains why the gap exists:

- **L109** (direction, proposal): "… Explicit attach to a developer-started session may support debugging only after composition/asset-mode/readiness checks and safe test-data scoping; attached services and developer data remain externally owned. Avoid implicit discovery/reuse. …"
- **L110** (direction, rejected alternative default): "Alternative local default: developer starts/selects a compatible Aspire session and tests attach to it, preserving its services after completion. … Recommend run-owned fresh environment per suite/batch by default with explicit attach for debugging; …"
- **L112** (question): "Pending Coaching choice: 1) fresh run-owned Aspire environment per suite/compatible batch, successful local cleanup by default and explicit attach for debugging (recommended); or 2) default tests against an explicitly selected developer-started compatible environment, whose services remain running. …"
- **L113** (decision, **user-accepted**): "User selected option 1 for local integration lifecycle: a fresh run-owned Aspire environment per suite or compatible batch, automatic cleanup after local success, and explicit developer-session attachment for debugging. This resolves the local default and successful-run cleanup questions."
- **L114** (decision, AD-10 [ADOPTED], follows the L113 user selection): "… Explicit attachment to a developer-started session requires compatible composition/asset mode, readiness and isolated test data; its services and developer data remain externally owned. Cleanup is attributable and idempotent, reports leftovers, and never stops unrelated sessions sharing an AppHost path."
- **L194** (decision, "Trust-boundary and tooling autofixes accepted"; validate-run autofix, accepted): "… V-65 test attach only to local or CI run-owned environments. …" Origin: security finding SEC-17 (`reviews/validate-2026-09-27/review-security.md` L230–236), whose recommendation was "Allow attach only to local or CI run-owned environments, or explicitly designated test environments. Attaching to hosted staging or production fails closed; …"

Interpretation. As originally accepted (L113/L114), attachment targeted a developer-started session whose services "remain externally owned", so the owner-termination problem did not arise. V-65 (L194) narrowed the target to "local or CI run-owned environments" to exclude hosted environments. The attach target became another run's environment, and that environment is subject to the owner's automatic outcome rules (L163). No later entry decides what happens to an attached run when that outcome fires. Memlog L114/L109 "remain externally owned" and addendum.md:105 protect only the *owner's* environment from the attached run's cancellation. The reverse direction is unaddressed.

Related review evidence (none decides the question):

- `reviews/review-adversarial.md` L12 (initial architecture review): "Runner cleanup stops all instances sharing an AppHost path or deletes an explicitly attached developer environment." That is the reverse risk, now closed by L164.
- `reviews/validate-2026-09-27/review-adversarial.md` L182: "Test cleanup cannot reach attached developer sessions or other runs (AD-10)."
- `reviews/validate-2026-09-28/review-rubric.md` L66: "| AD-10 | Ownership, terminal outcome, retained debugging, attach and cleanup behavior are deterministic. |" The rubric judged AD-10 deterministic and did not raise the owner-termination ordering.

### 2.5 Viable options implied by the sources (not a recommendation)

1. **Owner outcome deferred while attached work runs: preserve until attached runs end.** The owner's first terminal outcome is still recorded when it occurs (prd.md:134, spine L163), but its automatic cleanup waits until every accepted attached run has terminated. Failed-local retention is unaffected, because a failed owner retains anyway. A successful or cancelled owner cleans once the last attached run ends. Explicit developer cleanup of a retained environment (prd.md:133) would need the same wait-or-refuse rule. Source basis: the "attached services … remain externally owned / preserving its services" framing (memlog L109, L110, L114); addendum.md:105; prd.md:137 "must not remove resources belonging to another active run". Note that under current wording the attached run owns no resources, so prd.md:137 does not literally cover it. CI implication: a CI owner's "always cleans" (prd.md:136) becomes "cleans after attached work ends", bounded by the job's lifetime.
2. **Owner outcome proceeds; dependent runs are terminated and reported first.** Before cleaning, the owner (runner) ends any attached runs and records their outcome distinctly: environment withdrawn by owner, neither pass nor test failure. Results and diagnostics are preserved (prd.md:139). An attached run's result is invalid as integration evidence if its environment was withdrawn mid-run. Failed-local retention: a failed owner retains, so attached runs continue. Only owner success, cancellation or explicit developer cleanup trigger termination. Source basis: the reviewer's second alternative (review-adversarial.md L31); prd.md:138 "reports remaining owned resources"; prd.md:139.
3. **Narrow what may be attached to, so owner auto-cleanup cannot race attached work.** For example, permit attachment only to environments no longer subject to automatic cleanup: retained failed-local environments, which are cleaned only by explicit developer action (prd.md:133), or a developer-held session. Attachment to an active auto-cleaning run (local success path, or any CI run under prd.md:136) is refused. This reverts toward the originally accepted "developer-session attachment for debugging" purpose (memlog L113) while keeping V-65's hosted exclusion (L194). Explicit cleanup of a retained environment with an attached run would still need option 1 or option 2 behavior.

Under every option, the SM-3 attachment scenario (prd.md:317) would need the owner-finishes-first ordering added, as the reviewer suggested.

---

## Task 3: Other architecture drift since the PRD's last update (2026-09-27T22:22)

### 3.1 Cut-off

The architecture memlog has no per-line timestamps. Entries after the PRD's 22:22 update are memlog **L209–L261**:

- L209: validate run r2, 2026-09-27, directory mtime 23:32.
- L210: drift record for commit f733aa9, 22:52.
- L211–L242: update run 2, 2026-09-28.
- L243–L261: update run 3, 2026-09-28, ending 10:52.

Everything at L208 and earlier (the first "spine finalized") predates the PRD update, and the PRD update reconciled against it (PRD memlog L75–L83).

### 3.2 Excluded as directed (covered elsewhere)

These are listed only so they are not double-counted:

- **Revocation exceptions during restoration:** C-25 (L220); VAL-08 admission records and DR reconciliation (L250); the G-4 containment and lost-window part (L258); D5's lost-window review by Administrator before clearing (L237).
- **Administrator-approved release into degraded or empty production:** C-11 (L222); VAL-04 override bound to the observed stop revision (L249); "working degraded override applies as a clear" (L260).
- **Operations-repository writers and deputy controls:** C-09 deputy model and trigger path (L218); C-21 signed Administrator records and ops-repo credentials (L229); C-22 rulesets (L228); D4 second named writer (L236); D5 deputy in-place recovery and "deputy hours count toward response coverage only after the G2 deputy proof" (L237); the deputy's ability to declare a promotion stop (part of C-34, L222).

### 3.3 User-accepted decisions affecting PRD-level requirements

Ordered by likely PRD impact.

| # | Gist | Memlog line (acceptance) | Spine home | PRD status |
|---|---|---|---|---|
| D-1 | **Approved incompatible release: stricter content and failure behavior.** The Administrator record names, before the attempt, the planned recovery "with its acceptance checks **and maximum duration**". The attempt starts only after a complete recovery point cut after the lock. On a non-working outcome the executor sets the stop, **closes user ingress**, removes candidate workloads while keeping data, and stops. The named recovery then runs as an in-place **data restore** to that point, which discards post-cut live state. There is no in-job automated data restore. | C-38 at L222 (Batch 4, "user accepted all recommendations", L221). VAL-10 at L246, which makes the Administrator record state the named recovery's duration (Batch 1, "user accepted recommendations", L245). G-1 at L255 (user accepted). | L280 Release modes; L282 In-place recovery | **Omits.** prd.md:48 ("approval must name its separately planned recovery procedure before production changes begin") and prd.md:168 ("approval names the recovery procedure and its acceptance checks before the attempt") omit the maximum duration, the pre-attempt recovery point, and ingress closure with manual recovery on failure. Possible wording tension: prd.md:288 says NFR-2 targets "do not permit application rollback to rewind business data", but this path restores data to a cut taken after the lock. prd.md:286 routes incompatible changes to the approved mode, so the path is arguably outside NFR-1. The PRD does not say so. |
| D-2 | **After disaster recovery.** Production runs in a recorded reduced-recovery posture until new prepared capacity is identified, a drill repeats as a material change, and Administrator records the return to G2 conditions. The promotion stop stays set until staging is re-established. There is no emergency release mode. | C-50 at L220 (Batch 3, "user accepted all recommendations", L218). | L296 After DR ("… which is not Empty or degraded production …") | **Omits.** prd.md:207 covers only "becomes the working baseline only after its required verification passes". prd.md:228 repeats drills "after material storage or backup changes". The G2 row (prd.md:53) has no re-qualification after DR. The PRD is silent on "stop stays set until staging re-established" and on the absence of an emergency release path. |
| D-3 | **SM-4 temporary grant bounded by G2, and admission drift sets the stop.** The G1 SM-4 grant record "carries an expiry no later than G2", and "G2 requires its revocation record and denial check". The grant is limited to the synthetic tenant. Separately, the off-site monitor matches every exported admission-group change against a signed admission record, and "any unmatched change notifies and sets the promotion stop". | G-4 at L258 (user accepted). C-10 at L216 (Batch 2, user accepted all). | L289 Production entry gates; L288 Promotion stop | **Partly matches, partly omits.** prd.md:56 ("the test grant is revoked after the check") matches in spirit. The G2 row (prd.md:53) omits the revocation-record and denial-check precondition and the expiry. The FR-8 stop triggers (prd.md:204, :209) omit unmatched admission changes. |
| D-4 | **G1 "admission group is empty" means the *human* group.** Synthetic actors are admitted only through a standing Administrator-granted synthetic-admission group, limited to the synthetic tenant. "G1's empty-group condition applies to the human production-admission group." | C-10 at L216 (Batch 2, user accepted all). | L289 ("the human production-admission group is empty") | **Wording ambiguity; possible contradiction.** prd.md:52 (G1) says "the production admission group is empty", and addendum.md:181 says the same. Production smoke checks with synthetic identities (prd.md:183) need the standing synthetic group, so the PRD should say "human". |
| D-5 | **Expanded promotion-stop triggers.** A failed pre-update health check, a probe failure beyond a declared bound, a recorded incident, or an Administrator or deputy declaration sets the stop. The monitor may set it but never clear it. Setting it never triggers a release search. A clear is applied by compare-and-set on the observed stop revision, and a later set always prevails. | C-34 at L222 (Batch 4 autofix, accepted). VAL-04 at L249 (Batch 2, user accepted). | L288 | **Omits; compatible.** prd.md:204 lists non-working outcomes and DR entry, and prd.md:208 lists incidents establishing production is not working. prd.md:185 says missing preconditions "stop the attempt before mutation", but not that they set the durable stop. The clear rule at prd.md:204 ("authenticated Administrator record naming the reason and verified current working release") matches. |
| D-6 | **Tenant-lifecycle limits on module checks.** Production smoke suites never create or delete tenants. Staging E2E may create or delete only run-scoped tenants carrying the synthetic exclusion marker, within a module-declared tenant-lifecycle critical flow, through a staging-only admission clause. Run-owned local and CI environments are exempt. | D8 at L238 ("Quick confirms accepted"). VAL-05 at L251 (Batch 3, user accepted). | L235 Synthetic identities | **Omits; narrower than PRD.** prd.md:183 says "Read-only checks are preferred; necessary writes use dedicated synthetic identities and data without real-user changes or external effects". FR-6 (prd.md:161–166) places no data constraint on staging E2E. This affects Tenants' module-owned critical-flow and smoke declarations. |
| D-7 | **Shared-infrastructure change control.** Shared changes run under both locks after a complete recovery point and re-run the working release's smokes and the NFR-3 negative tests. A failed verification is non-working. **A failed currency check blocks automatic promotion.** From G1, pending shared changes are rehearsed on a production-profile copy on prepared capacity as part of the monthly drill. An urgent security patch may be applied in place with a recovery point and an Administrator record. Accepted risk: in-place Kubernetes-minor upgrades take both environments down. | C-17 at L224 (Batch 5, "user accepted all recommendations"). | Release tiers L239–248; L487 accepted risks | **Mostly omits; compatible.** prd.md:169 ("Shared-infrastructure changes coordinate both environments") and prd.md:309 ("Repeat the relevant checks after access changes and restoration") cover part of it. The PRD has no currency-check condition on automatic promotion, no requirement to re-run NFR-3 checks after shared-infrastructure changes, and no mention of the accepted two-environment outage. |
| D-8 | **G1 prerequisites made concrete.** Infrastructure currency adds the ingress controller, CNI, registry, backup tooling and Kubernetes patch level. The privileged Forgejo runner moves off the cluster node, earlier if staging holds real data. Keycloak `/admin` and master-realm routes and the public cluster console are removed from public ingress, verified by an external negative probe. | C-23 at L228 (Batch 6, user accepted all). C-27 at L228. C-54 at L229 (Batch 6 autofixes accepted). | L475–477 Owned work (G1 rows) | **Largely matches in substance.** prd.md:52 ("Supported cluster infrastructure and protected deployment/probe access"), prd.md:342 ("Supported infrastructure, ingress/DNS/certificates, executor/probe access … Before G1") and addendum.md:343 ("verify supported, patched infrastructure") cover it. Removing admin-surface exposure *before G1* is a new timing. The PRD's NFR-3 administration-boundary checks (prd.md:307, :309) are gated at G2 via SM-4. Low impact. |
| D-9 | **Staging candidate supersession and reset.** An unadopted candidate whose writes the production baseline cannot read forces a staging data restore before the next candidate's attempt. Each staging attempt first cuts a staging recovery point. The spine adds: "A candidate is not adopted once a later candidate's staging attempt starts without an Administrator record reserving it." | C-37 at L221 (Batch 4, user accepted all). The reservation sentence appears only in the distilled spine (L279, from update-run-2 distillation L231 and L239), with no separate memlog decision. | L279 Staging reset | **Omits; compatible.** prd.md:169 ("concurrent work must not replace the release being tested or invalidate its evidence") is about concurrent work. Sequential supersession, which makes an earlier candidate non-promotable unless Administrator reserves it, is a promotion-eligibility rule with Administrator authority, and the PRD does not state it. |
| D-10 | **G3 suspension on policy change.** "a change to verification, recovery or rollback-set policy suspends automatic promotion until the affected SM-5 rehearsals repeat." | Update-run-2 gate fix from input reconciliation RI-5 (`reviews/update-2026-09-28/review-reconcile-inputs.md` L84–89). Applied as an autofix (memlog L239, event "inputs RI-2..RI-8 …"). This was derived from the PRD, not a new user choice. | L289 (G3) | **Matches in substance.** prd.md:319 (SM-5 "Validates … before G3 and after recovery-policy changes"). The G3 row (prd.md:54) does not state the suspension consequence. Low impact. |
| D-11 | **Legacy MCP/CLI surfaces excluded from every Platform composition.** Legacy MCP hosts and CLIs are "not enrolled, deployed, routed, mapped in an enrolled host or issued a realm client in any Platform composition". Temporary compatibility use outside hosted environments needs a named migration record and removal gate. Any new MCP/CLI surface or transport, including McpCli HTTP, needs an AD. | C-03 at L212 (Batch 1, "user accepted all recommendations"). The sprint change proposal of 2026-09-27 was ratified in the same entry. | AD-11 (spine L166–176) | **Largely matches.** prd.md:278 calls legacy surfaces "obsolete migration sources" and says "Platform does not add or publish another proprietary Hexalith MCP/CLI surface". The PRD does not state that legacy surfaces stay out of every Platform composition until retirement. Low impact. |
| D-12 | **Interim unavailability of confirmation-required cross-module chains.** Until EventStore ships originating-surface attestation, service clients are least-privilege (agent-eligible only), "so confirmed chains such as Projects project-folder.replace stay unavailable rather than unsafe". | C-06 at L215 (Batch 2, user accepted all). VAL-07 at L244 (accepted autofix). | AD-14 (spine L197–207) | **Omits** a capability caveat. FR-12 (prd.md:264: operations requiring confirmation "remain available only through that UI") implies UI availability. The architecture makes some UI-confirmed cross-module chains unavailable in the interim. Module-owned behavior; low to medium impact. |
| D-13 | **New accepted residual risks.** Manual OpenBao unseal leaves both environments sealed after an unplanned restart outside coverage. In-place Kubernetes-minor upgrades take both environments down. Plaintext Redis and FalkorDB connections run inside the data namespace. The Keycloak server, Dapr control plane and Traefik are shared. | C-19 at L219 (Batch 3). C-17 at L224 (Batch 5). D3 at L236 (gate decisions, user accepted). | L487 | **PRD matches in effect.** prd.md:301 ("These targets establish neither an uptime percentage nor continuous response coverage") and prd.md:243 ("shared hardware does not establish node or site resilience") cover them. The **addendum's** accepted-risk list (addendum.md:348) omits the unseal, Kubernetes-minor and plaintext in-transit risks. This is addendum-level only. |

### 3.4 Reviewed and judged implementation-mechanism-only (no PRD-level effect)

- D1: Dapr skew and qualification records (L234).
- D2: CI package-mode mechanism (L235). This is relevant to the A1 addendum wording only; see 1.3(c).
- D6: admission projection (L238).
- D7: run-scoped McpCli build (L238).
- D9: PV/StorageClass rules (L238).
- D10: fence-and-reissue owners (L238).
- C-02: hexalith-module ratified as tool and runner (L213).
- C-04: publication workflow as sole stable McpCli publisher (L214).
- C-07, C-13, C-28, C-58, C-59 (L216).
- C-16: qualification records (L224).
- C-18: hostnames and HTTP-01 (L225).
- C-20: module-code sandbox (L229).
- C-35: production-promoted record before upgrade (L221).
- C-36: t0 at first mutation (L222). This matches prd.md:186's "10 minutes of deployment start".
- C-39: rollback generation (L222).
- C-43: HotReload off, Dapr patch pinning (L225).
- C-47: provenance (L229).
- C-48: registry (L228).
- VAL-01, VAL-03, VAL-06 (L244).
- VAL-02 and G-3: takeover fencing (L248, L257). This matches prd.md:191, where uncertainty stops changes pending intervention.
- VAL-09 and G-1: in-place mechanics beyond D-1 (L245, L255).
- VAL-10: kind-specific timers, apart from the approved-recovery duration in D-1 (L246). DR within coverage is bounded at outage + 4 h, which matches NFR-2. Recovery-release readiness at phase start + 10 min matches prd.md:202.
- G-2: recovery-release catalog generation (L256).
- G-5: hook contract version window (L259).
- L260 autofixes: render modes, Release-tier homes and similar.
- C-05 gate-timing rows (L222) already match prd.md:343 and addendum.md:346, which the PRD memlog L84 update aligned.

### 3.5 Related pending alignment noted by the architecture itself

Memlog L241 ends update run 2 with: "Source-alignment offers pending: PRD addendum (tool identity, notification recipients, deputy follow-up), _bmad-output/specs/spec-platform/SPEC.md, McpCli spine/PRD …". "Tool identity" is the addendum.md:34 supersession in 1.3(c). "Notification recipients" and "deputy follow-up" fall under the excluded deputy and ops-repo topics.
