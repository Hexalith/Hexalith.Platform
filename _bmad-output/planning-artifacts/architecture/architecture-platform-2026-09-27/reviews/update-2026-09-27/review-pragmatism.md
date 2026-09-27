# Pragmatism / Bloat Review — Platform Architecture Spine (update 2026-09-27)

- **Target:** `ARCHITECTURE-SPINE.md` (309 lines, ~6,460 words), status `draft`
- **Lens:** pragmatism for a single Administrator running an internal platform (one Kubernetes node, 7 .NET modules, small team). The questions: does each sentence stop independently built units from diverging, is the machinery proportionate, what is duplicated, and what is hard to parse?
- **Guardrails respected:** these user-adopted decisions are not reopened: AD-1 Option 1 with its fallback, two isolated executors (V-02), per-environment OpenBao (V-23), tenant-key custody class with the bounded-window alternative rejected (V-19), Folders/Projects overrides (V-05, user Folders ruling), AD-9 named exceptions (V-07), interruption and promotion-stop semantics (V-29/V-30), off-site GitHub monitor (V-39), rotation on restore (V-28), and the PRD FR-2/FR-9/NFR-2 envelopes. Where a finding touches one of these, it proposes only wording changes or a move to seed.

## Verdict

**Proportionate overall. Pass with trims; nothing here blocks handoff.** The heaviest obligations are traceable to a user decision or an inherited module invariant, not to author gold-plating. They are:

- the tenant-key custody class (Memories AD-16)
- the synchronous tombstone mirror (Memories AD-21)
- two executors
- per-environment OpenBao
- off-cluster attempt records

The growth from about 245 to 309 lines comes mostly from load-bearing structure: AD-13 to AD-15, the first-shared-versions table, and new R&R rows. The avoidable weight falls into three groups:

1. **PRD restatement.** The Release & Recovery table and the "Local tool" convention re-type FR-4, FR-6 to FR-9 and NFR-2 thresholds verbatim (about 190 words). This creates a second source of truth for numbers the spine says it inherits.
2. **Scattered facts.** Release-record contents appear in 6 places, and the lists already differ. The promotion stop appears in 4 places, the GitHub notification path in 5, and "MCP hosts not deployed" in 4.
3. **Mechanism tails.** Examples: "off the hour", "OS credential store", "listable with owner and age", 10-second sampling, and qualification steps inside AD-1.

A few prose knots also hide real ambiguity: "AD-28", "first recorded outcome fixes precedence", signing keys vs realm keys, and three different homes for the Memories.Aspire digests.

**Estimated saving:** about 570 words (~9%) and 2–3 physical lines. Lines are paragraph-length here, so line count understates the effect. The larger gain is one home per fact, which removes drift that is already visible (see PR-02 and PR-03).

## Counts

| Severity | Count | Disposition | Count |
| --- | --- | --- | --- |
| High | 2 | autofix | 30 |
| Medium | 14 | discuss | 6 |
| Low | 22 | ignore (keep as is) | 2 |
| **Total** | **38** | | **38** |

Summary by category:

- **A.** Mechanism to seed or Deferred: 9
- **B.** Heavy machinery assessments: 8
- **C.** Redundancy: 12
- **D.** Prose: 9

---

## A. Mechanism and PRD restatement that can move without letting units diverge

### PR-01 — R&R table and "Local tool" convention restate PRD thresholds verbatim
- **Severity:** high · **Disposition:** autofix · **Saves:** ~190 words
- **Location:** L158 (intro); rows Staging gate L162, Before production update L165, Rollout L166, Verification L167, Automatic recovery L168, Backup coverage L170, DR evidence L174; convention Local tool and readiness L150.
- **Issue:** The following spine text is PRD FR-4/FR-6/FR-7/FR-8/FR-9 and NFR-2 wording, re-typed into the spine:
  - 10-minute deadlines
  - the five-minute window
  - 60 continuous seconds
  - "twice consecutively … 30 seconds"
  - "single failed probe or restart alone"
  - "never cycle through older releases"
  - "if no workload changed, retain and report"
  - 30-minute runs and 7/30-day retention
  - monthly drills, RPO ≤ 1 h and RTO ≤ 4 h
  - non-empty critical-flow sets and flow-to-test mappings
  - "Process-running is not readiness"
  - "a timeout names unready resources"

  The skill says to inherit settled input silently. Restating it gives two sources of truth that can drift, and it hides the rows' real contribution: the non-PRD precision that V-12, V-15, V-29, V-30, V-60 and V-67 added. The same restatement pattern also repeats AD-3 ("Helm upgrade to the recorded previous identities", "separately planned procedure") and AD-15 (the N→N-1 rehearsal) inside Before production update and Automatic recovery.
- **Proposed change:** State the inheritance once and keep only the deltas.
  - **Intro (L158):** "These policies inherit the [Platform PRD]: FR-6–FR-9, NFR-1/NFR-2 and SM-4–SM-6 thresholds apply as written and are not restated; rows add only what the PRD leaves open. They are requirements, not measurements."
  - **Staging gate:** "PRD FR-6 applies to EventStore evidence-validated candidates. Staging deploy plus E2E is one serialized attempt. Results bind the serving release and the suite, profile and configuration digests and have a maximum age; stale results block like missing ones. A staging failure blocks only that candidate."
  - **Before production update:** "Production requires EventStore release-available, the PRD FR-7 declaration checks, and a healthy baseline: its recorded working release ready and that release's smoke suite passing now. Compatibility evidence is each module's change classification, the AD-15 staging rehearsal in which production's working release reads state and events the candidate wrote, and a prepared AD-15 rollback combination; missing, breaking or wrong-baseline evidence disables automatic promotion (AD-3 planned procedure)."
    - Move "Smoke writes use only synthetic identities and data, without external effects" into the Synthetic identities convention: "Smoke and E2E writes use only these identities and data, without external effects."
  - **Rollout:** "Write the production-promoted record, bound to the active profile digest, before traffic; the PRD FR-7 readiness deadline applies."
  - **Verification:** "PRD FR-7 window and triggers apply. Smoke checks have stable IDs and run at window start, then at a fixed cadence; a timeout is a failure and a result from another release counts as missing. The verdict is total: working only if readiness and the latest passing checks hold at window end, otherwise failed after one bounded grace for an in-flight retry. Re-verify SM-4 access outcomes after access-configuration changes."
    - Move "sample unavailability at ≤10 s" to the Deferred row *Release state, checks and notifications* as a harness parameter.
  - **Automatic recovery:** "PRD FR-8 applies: one attempt, executed as the AD-3 Helm upgrade with the prepared AD-15 combination, including partially changed workloads, and verified with the restored release's own checks. A workload counts as changed when any release-owned rendered object digest differs. A failed first deployment keeps ingress closed and stops; rolling back a first module enrollment removes workloads, never data objects. Report every outcome, including success."
    - The promotion-stop sentences move to Attempt ownership (PR-04).
  - **Backup coverage and cadence:** see PR-14 for the replacement text. It opens with "PRD FR-9 coverage, cadence, retention, encryption and independence apply."
  - **DR evidence:** "Administrator owns the PRD FR-9 drill schedule; the first drill gates G2. Drills assume the primary server and its storage are unavailable, restore into ephemeral egress-denied capacity, measure real detection and apply the declared worst-case response; RTO runs from outage to verified service, including capacity, transfer, replay and validation. Whole-site loss is covered only when prepared capacity sits at an independent location; otherwise it is a recorded residual risk."
  - **Local tool and readiness (L150):** "The pinned Platform .NET tool in `.config/dotnet-tools.json` (version rule in AD-4) provides stable run, teardown and debug commands. PRD FR-4 readiness and startup-deadline rules apply; a declared finite module override wins, and diagnostics record the effective deadline, actual startup duration and any unready resources."

### PR-09 — A rule sits inside the Deferred table
- **Severity:** medium · **Disposition:** autofix · **Saves:** ~5 words
- **Location:** Deferred › Secrets, identity, network and transport (L302): "Local compositions use mTLS, per-receiver ACLs and scoped components; local allow-defaults are never profile inputs."
- **Issue:** The memlog records V-63 (local mTLS/ACL convention) as a **defer**. The spine states it in Deferred as a present-tense rule, so a reader cannot tell whether it binds. The second half, "allow-defaults never feed the profile", is a real invariant: without it, a permissive local configuration could leak into production.
- **Proposed change:**
  - In the Deferred row, write: "Ratify the local Dapr mTLS, per-receiver ACL and scoped-component convention."
  - Add to the Production profile convention: "Local allow-defaults never feed the profile."

### PR-17 — AD-1 carries a qualification step, and its fallback sentence has no actor
- **Severity:** low · **Disposition:** autofix · **Saves:** ~5 words
- **Location:** AD-1 Rule (L54).
- **Issue:** "Qualify export with Parties plus EventStore, Tenants and Memories" is an acceptance step, and the Deferred row *Aspire-to-Helm qualification* (L298) already owns it. The last sentence, "Per-module Dapr hand-modelling, a custom compiler, … switch to the … fallback", reads as though the listed things do the switching.
- **Proposed change:**
  - Delete the qualify sentence from AD-1.
  - In L298, write: "Prove the AD-1 representative composition (Parties with EventStore, Tenants and Memories) with …".
  - Rewrite the last sentence: "If export needs per-module Dapr hand-modelling, a custom compiler, recurring generated-file patches or a duplicate full topology, switch to the small maintained Helm-chart fallback with parity checks."

### PR-13 — AD-11: overlong discovery sentence and McpCli-internal token mechanism
- **Severity:** medium · **Disposition:** autofix · **Saves:** ~15 words
- **Location:** AD-11 Rule (L114).
- **Issue:**
  - The discovery sentence is 45 words long and has five nested conditions.
  - "Never mix URL and token sources" is opaque.
  - "Refresh material in the OS credential store" is McpCli-internal mechanism. It belongs to the McpCli module spine and seed, not to a cross-unit invariant.
- **Proposed change:**
  - Replace the middle of the rule with: "In connected mode an operation is executable only when the selected gateway's EventStore-owned metadata endpoint, derived from the committed catalog, matches its per-operation contract-schema digest, its Contracts-declared surface eligibility allows it and current authorization permits it; unknown or mismatched digests are non-executable. The gateway reauthorizes every call. Credentials are environment-bound: a profile pins one gateway, issuer and audience; hosted access uses short-lived per-environment OIDC tokens sent only to the issuing environment's gateway, which never logs or persists them."
  - Move "refresh material in the OS credential store; URL and token come from the same profile source" to the Deferred row *Connected McpCli* (L299).

### PR-12 — AD-10: an unclear precedence clause and a mechanism tail
- **Severity:** medium · **Disposition:** autofix · **Saves:** ~5 words
- **Location:** AD-10 Rule (L108).
- **Issue:**
  - "The first recorded outcome fixes precedence" has no clear referent. Precedence of what? The memlog (V-66) shows it means that when outcomes conflict, the first recorded one decides cleanup.
  - "Listable with owner and age" is a listing-format detail, and the Deferred row L293 already carries "retained-run listing".
- **Proposed change:**
  - Replace the cleanup sentences: "Local success cleans; local test or startup failures are retained and listable; explicit cancellation cleans only that run; CI always cleans, including after partial startup. When outcomes conflict (for example, a failure followed by cancellation), the first recorded outcome decides cleanup."
  - The "partial startup" clause absorbs AD-5's duplicate (PR-20).

### PR-16 — Interruption row: tighten wording while keeping V-29 behaviour
- **Severity:** medium · **Disposition:** autofix · **Saves:** ~10 words
- **Location:** R&R › Interruption (L164).
- **Issue:** The row is correct, but it is dense and ambiguous. "Detected before verification end plus a bounded grace" parses two ways. The detection-timing mechanics are internal to one unit, the release workflow. The shared contract is only this: never a second recovery, never restart timers, stop on ambiguity. The Deferred row L300 already owns "interruption and concurrency behavior", so the remaining detail can live there.
- **Proposed change:** "Timers anchor to recorded start times and never restart; an observation gap invalidates the window. An interruption detected before the verification deadline plus a bounded grace fails the attempt, which may still use its one recovery. Later detection, record/cluster disagreement, an unknown actual release or lock owner, or an unreadable record stops for intervention without mutation. Restarts never add a recovery."

### PR-29 — Structural Seed closing paragraph mixes status notes with seed
- **Severity:** low · **Disposition:** autofix · **Saves:** ~20 words
- **Location:** L250.
- **Issue:** "No live infrastructure was changed by this architecture work" is a process note, and the memlog already records it. The OpenBao classification duplicates the Deferred Secrets row.
- **Proposed change:** "The first diagram is the deployment and environment view; the second is the AD-13/AD-11 build and release order. Current root composition is an opt-in Works development preview, not the MVP. Observed infrastructure (one node with local storage, one shared OpenBao, existing Memories resources) needs environment classification before reuse; executors, replacement capacity and complete backup coverage are not yet demonstrated."

### PR-34 — Recovery monitor tails: "scheduled off the hour", "warns early"
- **Severity:** low · **Disposition:** autofix · **Saves:** ~5 words
- **Location:** R&R › Recovery point and freshness (L171).
- **Issue:** "Off the hour" is a cron-congestion tip, not an invariant. "Warns early" gives no threshold, so it cannot be tested.
- **Proposed change:** Drop "scheduled off the hour". Replace "warns early" with "warns at a threshold below one hour (seed)". Move the cron tip into Deferred *Recovery capacity and coverage*.

### PR-36 — Module declaration carries a schema-origin note
- **Severity:** low · **Disposition:** autofix · **Saves:** ~6 words
- **Location:** Conventions › Module declaration (L144): "(adopt or supersede Projects' `hexalith.module.v1`)".
- **Issue:** This is an implementation choice for the schema owner. It is not needed to prevent divergence once "one versioned schema" is fixed.
- **Proposed change:** Move it to the first-shared-versions row "Enrollment declaration schema and validator" (L279).

---

## B. Heavy operational machinery: keep, restate as an outcome, or accept the risk

| Item | Where | Load-bearing? | Verdict |
| --- | --- | --- | --- |
| Hostname admission enforcement | Hosted interfaces L151 | Yes. A staging deploy identity could otherwise claim `tache.ai` on the shared ingress. | State the outcome; name a built-in mechanism as seed (PR-05). |
| Attempt watchdog | Attempt ownership L163 | Yes. A dead executor cannot report its own death. | Keep the outcome and fold it into the existing off-site monitor (PR-04). |
| Digest provenance verification | AD-2 L60 | Partly. The registry is written only by Builds. | State the outcome; mechanism is seed (PR-18). |
| Tenant-key custody class ("per-period wrapping") | AD-12 L120 | Yes. Inherited from Memories AD-16; the user rejected the 30-day bounded-window alternative. | Keep. The spine is already outcome-phrased and the mechanism is already seed (PR-37). |
| Off-cluster Keycloak admin-event export and revocation replay | AD-6 L84, DR step 4 L173 | Yes. Revocations after the cut are otherwise lost. | State the outcome in the DR step; the export is its mechanism (PR-15). |
| Integrity and decryptability of every recovery point | Recovery point L171 | Yes, but the cost depends on who verifies. | Clarify the verification point (PR-06). |
| Default-deny egress NetworkPolicy | AD-8 L96 | Yes (NFR-3), but it has no declared input. | Add an egress declaration field (PR-07). |
| Rotate every restored credential on each DR | DR step 4 L173 | Adopted (V-28). | Keep. The drill must fit it inside the 4-hour RTO; the only fix needed is the prose in PR-15 (PR-38). |

**Checked and proportionate, no change:**

- Pod Security `restricted`
- Dapr default-deny ACLs with per-environment trust domains
- PriorityClass and quotas on the shared node
- the private operations repository
- one per-environment lock
- records kept outside the cluster and executor (GitHub deployment records are cheap)
- evidence maximum age
- one realm contract for all four environments
- the prepared-capacity requirement
- the off-site probe

All of these use Kubernetes, Dapr or GitHub built-ins, and each has a clear NFR or decision behind it.

### PR-05 — Hostname "enforced at admission": state the outcome, name a built-in mechanism
- **Severity:** medium · **Disposition:** discuss · **Saves:** ~0 words; removes an implied policy engine
- **Location:** Conventions › Hosted interfaces (L151).
- **Issue:** "Enforced at admission" invites a Kyverno or Gatekeeper deployment, which is new always-on infrastructure for one Administrator. The threat is real: the ingress controller is shared, and AD-7's namespace-scoped staging identity can still create an Ingress for a production host. The fix is to fix the *outcome* and let the mechanism be the cheapest built-in one. The cluster is on 1.34, and the built-in ValidatingAdmissionPolicy has been GA since 1.30.
- **Proposed change:** Replace the hostname sentence and drop the Tenants tail (PR-26): "The staging namespace can neither serve production or issuer hostnames nor obtain their certificates (seed: built-in ValidatingAdmissionPolicy and namespaced or policy-restricted issuers; no separate policy engine)."

### PR-04 — Promotion stop scattered in four places; the watchdog becomes a monitor check
- **Severity:** medium · **Disposition:** autofix · **Saves:** ~15 words; avoids a new component
- **Location:**
  - Automatic recovery (L168) defines the promotion stop.
  - DR step 1 (L173) sets it.
  - Attempt ownership (L163) says where it lives and adds the watchdog.
  - Deferred L300 lists the watchdog.
- **Issue:** The promotion-stop lifecycle (who sets it, where it lives, who clears it, and what counts as a baseline) is split across three rows. The watchdog reads as a separate always-on service. The Administrator already runs an off-site, GitHub-hosted, scheduled monitor (V-39) that could read the same off-cluster records.
- **Proposed change:** Make Attempt ownership the single home: "One per-environment lock covers every workload-affecting change: release, rollback, config-only change, rotation rollout, catalog activation, profile or shared-infrastructure change and DR restore. Shared cluster services have one named change owner and change only while neither environment has an open attempt. The attempt record, working baseline and durable promotion stop live outside the target cluster and the executor host. A production attempt runs from lock to terminal outcome in one job; a record left non-terminal past its maximum lifetime notifies Administrator from off-site (seed: a check in the scheduled freshness monitor). Every non-working terminal outcome and every DR entry sets the promotion stop, which also suspends promotion during recovery; only an authenticated Administrator record naming the reason and a verified current working release clears it. A manual or DR change becomes the working baseline only after passing verification."
  - DR step 1 keeps "set the promotion stop".
  - Delete the promotion-stop sentences from Automatic recovery.

### PR-18 — AD-2 provenance: state the outcome, not the mechanism
- **Severity:** low · **Disposition:** discuss · **Saves:** ~0 words
- **Location:** AD-2 Rule (L60): "Deploy verifies every digest's provenance against the protected Builds workflow and ref."
- **Issue:** The intent (V-56) is sound: deploy only what the protected workflow built. The word "provenance" suggests signing or attestation infrastructure. For a private registry written only by the Builds workflow identity, a registry-permission control gives nearly the same assurance at no extra cost. GitHub's native build attestations, if the plan in use supports them, cost about one step each side. Either mechanism satisfies the outcome.
- **Proposed change:** "Deploy accepts only digests published by the protected Builds workflow and ref (seed: native build attestations or registry write restricted to that workflow identity)." This keeps the decision and makes the mechanism choice explicit.

### PR-06 — Recovery point "verified integrity … decryptable with independent keys": who verifies, and when?
- **Severity:** medium · **Disposition:** discuss · **Saves:** 0 words; prevents an expensive reading
- **Location:** R&R › Recovery point and freshness (L171).
- **Issue:** The monitor measures the age of the newest *usable* point, and usable includes "decryptable with independent keys". One reading of this makes a scheduled monitor download and test-decrypt off-site backups on every run. That is heavy, slow, and would require the monitor to hold decryption keys, which weakens custody. V-32's intent is met more cheaply: the backup job verifies each point when it writes it and records the result, and the monitor reads only metadata.
- **Proposed change:** "…and integrity verified when the point is written (checksums, unbroken chains, test decryption with the independent keys), recorded in off-site metadata. Age is failure time minus cut. An off-site GitHub-hosted scheduled monitor reads that metadata and reports the newest complete set; it warns below one hour and notifies on job failure or age over one hour." Drills then remain the end-to-end proof.

### PR-07 — Default-deny egress has no declared input
- **Severity:** medium · **Disposition:** discuss · **Adds:** ~4 words
- **Location:** AD-8 (L96) default-deny egress; Module declaration (L144).
- **Issue:** Default-deny egress is proportionate: it is a built-in and it serves NFR-3. But the declaration lists "inbound callers" and not outbound external destinations, even though Folders, Memories and others reach external providers (AD-8 already mentions "external-provider tenancy"). Without a declared field, each module's egress lands as a hand patch to the generated chart. That is exactly AD-1's fallback trigger ("recurring generated-file patches"), and two modules would express egress differently.
- **Proposed change:** Add "external egress destinations" after "inbound callers and operations" in the Module declaration convention.

### PR-15 — DR step 4: signing-key referent, and the admin-event export as an outcome
- **Severity:** medium · **Disposition:** autofix · **Saves:** ~5 words
- **Location:** R&R › DR sequence (L173); AD-6 (L84).
- **Issue:**
  - "Rotate every restored credential and signing key" followed by "a compromise-driven restore **also** rotates … realm keys" is contradictory. Realm keys are signing keys. The memlog (V-28) means that application signing keys rotate routinely, while the realm keys and Dapr trust root rotate only after compromise.
  - "From the off-cluster admin-event export" puts mechanism inside the procedure; AD-6 already fixes the export.
- **Proposed change:**
  - Step 4: "Rotate every restored credential and application signing key; re-apply identity revocations made after the cut; compromise-driven restores also rotate the Dapr trust root and realm keys."
  - Step 5: "Verify compatible releases, cross-module integrity, restored-release smokes, SM-4 access outcomes and denial of revoked principals."
  - In AD-6, write "…admin events exported off-cluster for DR replay" so that the referent is explicit.

### PR-37 — Tenant-key custody class (per-period wrapping concern)
- **Severity:** low · **Disposition:** ignore (keep)
- **Location:** AD-12 (L120); Deferred L304 "key-custody mechanism".
- **Issue / assessment:** This is the heaviest item in the spine. It is inherited from Memories AD-16: backups may survive only if they are protected solely by key material that erasure destroys. The user explicitly rejected the pragmatic bounded-window alternative (memlog L189). The spine already states it as an outcome ("erasure makes every earlier key backup unusable for the erased tenant") and leaves the mechanism, such as per-period escrow wrapping, in Deferred.
- **Proposed change:** None. Keep the mechanism out of the spine.

### PR-38 — Full credential rotation on every DR
- **Severity:** low · **Disposition:** ignore (keep)
- **Location:** DR step 4 (L173).
- **Issue / assessment:** This was adopted as V-28. Its real cost is RTO time, and the four-hour drill already measures that. No wording change is needed beyond PR-15.

---

## C. Redundancy across ADs, Conventions, R&R and Source Precedence

### PR-02 — Release-record contents are listed in six places, and the lists already differ
- **Severity:** high · **Disposition:** autofix · **Saves:** ~10 words net; one source of truth
- **Location:**
  - AD-2 (L60): module artifacts, composed-host identity, catalog generation, profile digest, realm-contract version, configuration and check-suite digests.
  - Production profile (L149): catalog generation, **secret-contract digest, app-ID set**, module and Memories.Aspire image digests.
  - AD-6 (L84): realm-contract version.
  - AD-13 (L126): catalog generation.
  - Staging gate (L162) and Rollout (L166): profile digest.
  - Memlog V-09: change classification.
- **Issue:** AD-2 claims to define the record, but it omits the secret-contract digest, the app-ID set and the change classification, which other rows put there. The Builds encoding owner will build from whichever list it reads first.
- **Proposed change:** Make AD-2 the single list, and have other rows say "in the AD-2 release record" without re-listing. AD-2 sentence: "The release record is the single binding of a release: module artifacts and application image digests, composed-host identity, catalog generation, secret-contract digest, app-ID set, profile digest, realm-contract version, change classifications, configuration and check-suite digests, each linked to its evidence; Builds validators own its encoding." Then:
  - Production profile: "Per-release bindings live in the AD-2 release record."
  - AD-13: "…records it in the AD-2 release record."

### PR-03 — The Memories.Aspire digest set has three different homes
- **Severity:** medium · **Disposition:** discuss · **Saves:** ~15 words
- **Location:**
  - Paradigm (L26): "consumed through the profile inventory".
  - AD-3 (L66): "module digest sets as qualification inputs" to a profile facet.
  - Production profile (L149): "Memories.Aspire image digests live in the release record".
- **Issue:** AD-3 requires "exactly one version authority" for environment-layer objects. The Memories.Aspire set pins data services such as Redis and FalkorDB, which are environment-layer objects. Listing it as a per-release binding (V-36) as well suggests a second authority. The two readings can be reconciled, but the spine should say how: the release record binds the profile digest, which already pins the set.
- **Proposed change:**
  - Delete the Memories.Aspire sentence from the Paradigm.
  - In Production profile, write: "The profile inventory pins the environment layer (Keycloak, data services with the Memories.Aspire digest set as qualification input, Dapr control plane) and the deploy toolchain (Helm 4.x, Aspire Kubernetes preview); the release record binds it through the profile digest."
  - Confirm with the user that this matches V-36's intent: the record references the set but does not own it.

### PR-08 — AD-12 duplicates three other homes, and the once-per-environment rule is incomplete
- **Severity:** medium · **Disposition:** autofix · **Saves:** ~30 words
- **Location:** AD-12 (L120) vs Backup coverage (L170), DR evidence (L174), Module declaration (L144) and Deferred (L308).
- **Issue:**
  - "Data backups are immutable" is repeated in Backup coverage.
  - "Drill restores are ephemeral and egress-denied" belongs with the drill row.
  - "Warm standby is deferred…" is repeated in Deferred L308.
  - "Once-per-environment creation tasks never run during recovery" understates V-17, which says such tasks never run on upgrade, rollback, restore *or* recovery. The vocabulary is defined in the declaration convention anyway.
- **Proposed change:**
  - AD-12 rule: "Maintain one active production environment; restore compatible retained applications and configuration plus module-approved recovery sets onto identified prepared replacement capacity, including shared dependencies, independently available operator and decryption access, artifacts and failure reporting. Platform owns the exercised sequence in Release and Recovery Acceptance; modules own state classes, recovery boundaries, reconciliation and integrity checks. Tenant-key custody is a separate backup class never held with ciphertext backups; erasure makes every earlier key backup unusable for the erased tenant while a fresh key backup keeps others recoverable."
  - In Module declaration: "…lifecycle scope (per-start verify; once-per-environment creation, run only when creating an environment and never on upgrade, rollback, restore or recovery; operator-only)…".
  - The drill phrase moves to DR evidence (PR-01).

### PR-20 — AD-5 and AD-10 overlap on CI lifecycle
- **Severity:** low · **Disposition:** autofix · **Saves:** ~20 words
- **Location:** AD-5 (L78) vs AD-10 (L108).
- **Issue:** "Isolate each run's state and resources; preserve results and diagnostics; clean owned resources after success, failure, cancellation or partial startup" restates AD-10, the Diagnostics convention and PRD FR-4. AD-5 should own only CI placement and asset mode.
- **Proposed change:** AD-5 rule: "Reuse Builds workflows on disposable Linux GitHub-hosted runners. Run isolated tests without Platform first, then real-service integration through the AD-10 Platform runner with Release/NuGet artifacts; Builds owns a blocking Platform-runner integration entry. CI never receives staging or production provider credentials. Staging Kubernetes E2E remains a separate mandatory gate." PR-12 moves "partial startup" into AD-10.

### PR-21 — The tool-version rule appears in both AD-4 and the Local tool convention; AD-4 repeats AD-5
- **Severity:** low · **Disposition:** autofix · **Saves:** ~10 words
- **Location:** AD-4 (L72) "pinned CI tool refuses to run when its version differs" and "CI/CD uses Release/NuGet assets under AD-5"; Local tool (L150) "matches the workspace's Platform submodule".
- **Proposed change:** Keep the rule in AD-4 only, and reference it from the convention (PR-01). Delete AD-4's last sentence.

### PR-22 — AD-7 has three clauses owned elsewhere; "hosts" is narrower than the decision
- **Severity:** low · **Disposition:** autofix · **Saves:** ~20 words
- **Location:** AD-7 (L90).
- **Issue:**
  - "Cluster-scoped prerequisites stay outside the application package" is already in AD-3.
  - "Routine build and integration jobs stay on disposable hosted runners" is already in AD-5.
  - The runner-group clause and the private-ops-repository clause state one control twice.
  - The memlog (V-02) says "separate hosts/VMs". The spine's "separate hosts" can be read as two physical machines.
- **Proposed change:** "Staging and production each use a dedicated self-hosted Linux executor on a separate machine or VM, with access to the designated Kubernetes API and verification endpoints; the production executor runs outside the application cluster, and it runs only production jobs and digest-identified smoke suites that passed staging. Staging, test or PR code never runs where production credentials are or were present. Hosted release, recovery and backup workflows, logs and artifacts live in a private operations repository; executors accept only its named workflows on protected refs, never `pull_request` or fork events, with per-job credentials from protected environments. Each deploy identity is namespace-scoped without bind, escalate or impersonate. Recovery execution does not depend on these executors (AD-12)."

### PR-23 — GitHub notification path stated five times; telemetry retention is unspecified
- **Severity:** low · **Disposition:** autofix · **Saves:** ~20 words
- **Location:** Diagnostics (L153); Attempt ownership (L163); Automatic recovery (L168); Recovery point (L171); Detection (L172).
- **Issue:** "Notifies Administrator (through GitHub)" appears in each row. "A hosted telemetry sink keeps a minimum retention" gives no value, so it is not an invariant. Deferred L304 already owns the telemetry sink.
- **Proposed change:**
  - Diagnostics: "GitHub is the single accepted notification path: every Administrator notification in this spine uses it, and site-surviving monitors run off-site."
  - Other rows say only "notifies Administrator".
  - Move the telemetry retention to Deferred L304 as "telemetry sink and its minimum retention".

### PR-24 — "MCP hosts not deployed" stated four times
- **Severity:** low · **Disposition:** autofix · **Saves:** ~25 words
- **Location:** AD-11 (L114); Memories row (L186) "Memories.Mcp is not deployed in the MVP"; Parties row (L189) "Parties.Mcp is not deployed in the MVP"; Deferred L309 "a module MCP host needs an AD admitting it under AD-14".
- **Proposed change:** Keep AD-11 as the only home. Delete the other three clauses.

### PR-25 — "Module gates never waived" and "overrides settled" are each stated three times
- **Severity:** low · **Disposition:** autofix · **Saves:** ~35 words, 1 line
- **Location:**
  - "Gates never waived": Migration and coexistence (L154), Source Precedence paragraph (L181), and the Deferred row *Module service-level and functional qualification* (L306).
  - "Overrides settled": Source Precedence ("Neither blocks enrollment…"), the Deferred intro (L271, "The Folders and Projects overrides are settled"), and Deferred L307.
- **Proposed change:**
  - Keep "gates never waived" in the Source Precedence paragraph, which is where the overrides are stated.
  - Delete the Migration row's last sentence and the Deferred row L306, which assigns no work.
  - Delete the Deferred intro's second sentence. Keep L307 as the owned alignment work.

### PR-26 — Tenants presentation transport and scaling stated three times
- **Severity:** low · **Disposition:** autofix · **Saves:** ~20 words
- **Location:** Paradigm (L46) "Tenants keeps its BFF-to-Tenants REST read contract"; Hosted interfaces (L151) "Tenants InteractiveServer above one replica requires…"; Tenants row (L188).
- **Proposed change:**
  - L46: "Module-owned presentation and read transports, such as the Tenants BFF REST reads, remain valid."
  - Delete the Tenants sentence from Hosted interfaces. The Tenants row keeps the scaling condition.

### PR-27 — Merge the "Lost-window erasures" and "External effects" rows
- **Severity:** low · **Disposition:** autofix · **Saves:** ~15 words, 1 line
- **Location:** L176, L177, plus DR step 1 (L173). Disabling external-effect workers appears three times.
- **Proposed change:** Use one row, **Lost window and external effects**: "Non-Memories erasures, deletions and legal holds acknowledged after the recovery cut are an accepted RPO exception, stated in the DR report. Destructive retention and external-effect workers stay disabled until each owning module has restored and reconciled its external-operation state; never blindly repeat an unknown prior provider outcome."

### PR-28 — Source Precedence input rows restate ADs
- **Severity:** low · **Disposition:** discuss · **Saves:** ~50 words
- **Location:** L185–L191.
- **Issue:** Several rows repeat what the ADs already say:
  - EventStore row: "SDK/runtime ownership".
  - Memories row: repeats the AD-9 exceptions, and its "migrates to Dapr before G2" line is also in Deferred L305.
  - McpCli row: restates AD-11 and AD-14.

  The table is useful as a map for module owners, but each row should carry only what the ADs do not.
- **Proposed change:**
  - Memories: "Per-tenant principals and dynamic secrets; projection replay; tombstone lineage and key custody; direct-provider access only per the AD-9 exceptions."
  - McpCli: "Generic static Contracts enrollment and common CLI/stdio MCP core per AD-11/AD-14; environment-bound short-lived tokens replace static profile bearers for hosted use."
  - EventStore: drop "SDK/runtime ownership".

---

## D. Prose that is hard to parse or has unclear referents

### PR-10 — AD-3 environment-layer sentence
- **Severity:** medium · **Disposition:** autofix · **Saves:** ~10 words
- **Location:** AD-3 (L66).
- **Issue:**
  - "…with keep policies on persistent objects and exactly one version authority each: a profile facet, with module digest sets as qualification inputs" stacks three ideas behind a colon, and "each" has no clear referent.
  - "Keep policies" is a Helm annotation detail; the invariant is that persistent objects survive package removal.
  - "A workload changed when…" is a definition used only by Automatic recovery (PR-01 moves it there) and is missing "counts as".
- **Proposed change:** "Data services, brokers, persistent volumes, CRDs and other cluster-scoped objects form a forward-only environment layer outside the application package. Persistent objects survive package removal. Each environment-layer object has one version authority, a profile-inventory facet; module digest sets such as Memories.Aspire's are qualification inputs to it."

### PR-11 — AD-6 "the distinct AD-28 app-channel contract" is ambiguous
- **Severity:** medium · **Disposition:** autofix · **Saves:** 0 words
- **Location:** AD-6 (L84).
- **Issue:** The spine also cites **Projects AD-28** (L181, availability). A reader cannot tell which module's AD-28 is meant here. It is EventStore's.
- **Proposed change:** "…reusing EventStore's AD-10 JWT and fingerprint conformance and its separate AD-28 app-channel contract."

### PR-14 — Backup-unit class sentence
- **Severity:** medium · **Disposition:** autofix · **Saves:** ~18 words (with PR-01's PRD trim)
- **Location:** Backup coverage (L170): "different classes never share a backup unit unless restore invalidates rebuild-only state as its owner prescribes".
- **Issue:** The double negative plus an exception is hard to parse. It is also unclear whose owner is meant: the unit's or the state's.
- **Proposed change:** "PRD FR-9 coverage, cadence, retention, encryption and independence apply. The inventory adds each environment's OpenBao and gives every entry a recovery owner and recovery class. A backup unit holds one recovery class; it may also contain rebuild-only state only when its restore discards that state as the state's owner prescribes. Copies are immutable and off-site under per-environment-instance prefixes. Rotated key generations are retained at least as long as the data backups."

### PR-19 — AD-2 retention: "at least X or Y" is ambiguous
- **Severity:** low · **Disposition:** autofix · **Saves:** 0 words
- **Location:** AD-2 (L60): "retained at least for the backup retention period or their life as a rollback target".
- **Proposed change:** "…retained for the backup retention period or their life as a rollback target, whichever is longer."

### PR-30 — Production profile mislabels toolchain pins and carries a rationale
- **Severity:** low · **Disposition:** autofix · **Saves:** ~7 words
- **Location:** Production profile (L149).
- **Issue:** "Environment-layer pins (… Helm 4.x toolchain, Aspire Kubernetes preview)": the Helm and Aspire pins are deploy-toolchain pins, not environment-layer objects under AD-3. "Dapr API portability does not migrate data" is rationale and belongs in the memlog.
- **Proposed change:** Use the split wording in PR-03. Delete the portability sentence. Optionally, add AD-8's currency rule here, since the inventory is where versions live (PR-35).

### PR-31 — Secrets: "its token auth makes per-app tokens documented bootstrap material"
- **Severity:** low · **Disposition:** autofix · **Saves:** 0 words
- **Location:** Secrets (L148).
- **Issue:** "Its" has no clear referent. It could mean the component, OpenBao or AD-24.
- **Proposed change:** "Follow EventStore AD-24: Dapr `secretstores.hashicorp.vault` against the environment's OpenBao. Its per-app tokens and any Kubernetes Secrets are documented bootstrap exceptions, each with a named renewal owner." Delete the later duplicate "Kubernetes Secrets are only documented bootstrap exceptions."

### PR-32 — AD-15 "prepare/ready-validate"
- **Severity:** low · **Disposition:** autofix · **Saves:** 0 words
- **Location:** AD-15 (L138).
- **Issue:** This is EventStore catalog-protocol jargon, and it reads as a typo to anyone outside EventStore.
- **Proposed change:** "…record the rollback combination and have it prepared and ready-validated under the EventStore catalog protocol: …".

### PR-33 — G2 "on the observed topology"
- **Severity:** low · **Disposition:** autofix · **Saves:** ~5 words
- **Location:** Production entry gates (L169).
- **Issue:** The qualifier is unclear. Memories is in every domain module's minimum composition, so the condition always applies.
- **Proposed change:** "**G2:** admit users and open ingress only after the AD-12 drill passes, including Memories tombstone continuity (Memories is in every MVP composition)."

### PR-35 — AD-8 currency rule is operational policy, not isolation
- **Severity:** low · **Disposition:** autofix · **Saves:** 0 words
- **Location:** AD-8 (L96): "Reused infrastructure is suitable only while within upstream support and current on security patches."
- **Issue:** This is a sound rule (V-55), but it has nothing to do with environment separation. The profile inventory is the natural owner.
- **Proposed change:** Move it to the Production profile convention: "Inventory pins stay within upstream support and current on security patches while serving production."

---

## Optional (not counted)

- **Accepted-risk ledger.** Accepted residual risks are scattered through the spine:
  - single-node shared kernel (AD-8)
  - GitHub as the single notification path (Diagnostics)
  - prerelease pins (Stack)
  - whole-site loss without an independent location (DR evidence)
  - lost-window erasures (R&R)

  A one-line "Accepted risks" row in Deferred would make them auditable. It saves little, so treat it as optional.
- **Diagram 2** (release order) duplicates AD-11's last sentence and AD-13's build order. It is cheap and orients readers, so keep it.

## Saving estimate

| Area | Words saved (approx.) |
| --- | --- |
| R&R + Local tool PRD restatement (PR-01) | 190 |
| AD trims (PR-08, PR-10, PR-12, PR-13, PR-16, PR-17, PR-20, PR-21, PR-22) | 125 |
| Convention trims (PR-23, PR-26, PR-30, PR-36) | 55 |
| Scattered facts (PR-02, PR-03, PR-04, PR-24, PR-25, PR-27) | 110 |
| Source Precedence table (PR-28) | 50 |
| Structural Seed paragraph and small prose (PR-29, PR-33, PR-34) | 30 |
| **Total** | **~560–600 words (~9%), 2–3 physical lines** |

The main benefit is not length. It is one home for release-record contents, the promotion stop, notification, MCP exclusion and the PRD thresholds, which removes drift that PR-02 and PR-03 show has already started.
