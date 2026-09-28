# Pragmatism and Bloat Review, r2: Platform Architecture Spine

- **Target:** `ARCHITECTURE-SPINE.md`. Status `final`, 328 lines, 8,439 words, 15 ADs. Rationale is in `.memlog.md`.
- **Lens:** pragmatism and bloat. This review applies the inclusion test, checks proportionality for one Administrator, a recovery deputy, AI agents, GitHub Free and a single-node cluster, and asks whether the spine works as a build substrate for a small agent working on one story. It also checks readability.
- **Not re-raised:** PR-01 (restated PRD thresholds), PR-37 and PR-38, which the user declined or kept. The adopted decisions listed in the prior guardrails are also left alone: two executors, per-environment OpenBao, tenant-key custody, the interruption and promotion-stop semantics, the off-site monitor, and rotation on restore.
- **Scope:** read-only. This file is the only output.

## Verdict

**PASS WITH TRIMS.** The first-run trims have landed. The spine is now mostly one fact per home, and the heavy controls trace to user decisions. The problems that remain come from **scatter**, not size.

1. Three concepts are described in 3 to 5 places, and those descriptions already disagree:
   - release tiers, meaning the environment layer versus shared infrastructure (PR-101);
   - the legacy MCP/CLI policy from the latest edit (PR-102);
   - production preconditions (PR-103).
2. A small agent cannot find its rules from one entry point. Several coined terms are used before they are defined, and some are never defined at all (PR-104, PR-110, PR-113).

The fix is consolidation, not deletion. The pure trims remove about 460 words (about 5.5%). The structural additions proposed here (terms, records, precondition checklist, tier table, index column, gate column, optional diagram) add about 400 to 480 words, so **net length stays roughly flat**. Taking only the autofix trims gives **about −350 words (about 4%)**.

| Severity | Count | Disposition | Count |
| --- | --- | --- | --- |
| High | 3 | autofix | 19 |
| Medium | 9 | discuss | 6 |
| Low | 14 | defer | 1 |
| **Total** | **26** | ignore | 0 |

PR-102 and PR-108 are counted as discuss and autofix respectively; each has one sub-item with the other disposition.

## Size table

| Section | Lines | Words | Share | Assessment |
| --- | --- | --- | --- | --- |
| Frontmatter | 1–19 | 71 | 1% | Fine. `companions: []`. See PR-124 for the one candidate companion. |
| Design Paradigm and diagram | 23–49 | 279 | 3% | Proportionate. It has no pointer to terms (PR-113), and it is one of four homes for the AppHost rule (PR-107). |
| AD-1 to AD-15 | 53–141 | 3,003 | 36% | Averages 200 words per AD. Seven ADs exceed 200 words. Each Rule is a single paragraph averaging 21 to 30 words per sentence, and the longest sentences reach 45 to 54 words. AD-11 (290 words) and AD-7 (251 words) are overloaded. |
| Consistency Conventions (14 rows) | 147–160 | 1,391 | 16% | Production profile (203 words) mixes definition, procedure and a deploy precondition. Module declaration contains a 110-word sentence. Binding classes defines terms that AD-2, AD-3 and AD-15 use earlier. |
| Release and Recovery Acceptance (16 rows) | 166–183 | 1,526 | 18% | The thresholds stay by user decision. Production preconditions and attempt ordering are spread across 6+ rows (PR-103, PR-112). The DR sequence is a numbered list crammed into a table cell. |
| Source Precedence | 185–197 | 354 | 4% | Good map. The McpCli row is the third home of the legacy-surface rule (PR-102). |
| Stack | 199–215 | 215 | 3% | The Status column carries action items and observations. The template asks for name and version only (PR-126). |
| Structural Seed | 217–263 | 259 | 3% | Good. The diagrams carry shape. |
| Capability → Architecture Map | 265–280 | 208 | 2% | Lists ADs only. The template's "Governed by" column expects conventions too (PR-110). |
| Deferred: First shared versions | 286–301 | 219 | 3% | Exemplary: its "Must precede" column is the model for PR-111. |
| Deferred: Owned work (18 rows) | 303–324 | 838 | 10% | Gates are buried in prose. One row assigns no work, two rows carry binding rules, and one row mixes two gates. |
| Deferred: Accepted risks | 326–328 | 56 | 1% | The right single home, but AD-7, AD-8 and DR evidence restate it (PR-115). |
| **Total** | 328 | 8,439 | | |

### Per-AD fit (Binds/Prevents compared with Rule)

| AD | Words | Match | Note |
| --- | --- | --- | --- |
| AD-1 | 201 | Partial | The environment-layer and Dapr-resource placement belongs to AD-3's boundary (PR-101). |
| AD-2 | 208 | Yes | Provenance wording is duplicated in Workflows (PR-117). |
| AD-3 | 188 | Yes | Tier membership conflicts with AD-1 and Production profile (PR-101). It also hides the module-facing compatibility rule (PR-104). |
| AD-4 | 167 | Yes | Keep. |
| AD-5 | 75 | Yes | Keep. |
| AD-6 | 213 | Yes | Cohesive. Needs sub-bullets only (PR-109). |
| AD-7 | 251 | Partial | Carries the recovery executor, a GitHub Team revisit trigger and an AD-5 duplicate (PR-108). |
| AD-8 | 230 | Mostly | PriorityClass and quotas address availability, not reach. The hostname and single-node sentences are duplicates (PR-115, PR-116). |
| AD-9 | 189 | Yes | Keep. |
| AD-10 | 211 | Yes | Keep. The FR-4/FR-5 lifecycle wording was retained by the PRD reconciliation. |
| AD-11 | 290 | **No** | Legacy retirement and a deferred decision are outside Binds/Prevents (PR-102). |
| AD-12 | 150 | Yes | Keep. It could absorb the recovery-executor sentence (PR-108). |
| AD-13 | 223 | Yes | Uses undefined lifecycle-record vocabulary and contains a one-time ratification step (PR-106, PR-125). |
| AD-14 | 229 | Yes | A "Because …" rationale clause (PR-120). |
| AD-15 | 178 | Yes | The rehearsal content is split with the Staging gate row (PR-105). |

---

## A. Consolidations that remove existing divergence

### PR-101: Release tiers are described in five places, and their membership disagrees
- **Severity:** high. **Disposition:** discuss, because Keycloak and Dapr control-plane placement needs a call.
- **Location:** AD-1 (L57), AD-3 (L69), AD-7 (L93), AD-8 (L99), Production profile (L154), Attempt ownership (L170).
- **Finding:** The "environment layer" means different things in different places:

  | Where | What it says |
  | --- | --- |
  | AD-1 | The environment layer is "a separate **per-environment** definition". |
  | AD-3 | The environment layer includes "**CRDs and other cluster-scoped objects**", which are inherently shared, and lists Keycloak separately under "stay outside application rollback". |
  | Production profile | The environment layer inventory pins "**Keycloak** … **Dapr control plane**", both of which are shared. It also says "an environment-layer change is its own attempt". |
  | AD-7, AD-8 | The environment-layer identity is scoped to the **data namespace**. It cannot apply CRDs or the control plane. Only the production executor's shared-infrastructure workflows get a cluster-scoped identity. |
  | Attempt ownership | Shared-infrastructure changes hold **both** locks. |

  As a result, a Dapr control-plane upgrade could be built either way:
  - as a per-environment environment-layer attempt, which holds one lock and uses a data-namespace identity that cannot perform it; or
  - as a shared-infrastructure change, which holds both locks and uses the cluster-scoped identity.

  The environment-layer generator story and the shared-infrastructure workflow story will diverge on who owns CRDs, the control plane and Keycloak.
- **Proposed change:** Add one small **Release tiers** table directly under AD-3's Rule. It carries the shape now spread over five prose passages:

  | Tier | Contents | Version authority | Changed by | Rollback |
  | --- | --- | --- | --- | --- |
  | Application package (per environment, app namespace) | workloads, services, ingress, Dapr Configurations, WorkflowAccessPolicies, Subscriptions, Resiliency | AD-2 release record | release attempt; application deploy identity | Helm upgrade to the working baseline's package (AD-3) |
  | Environment layer (per environment, data namespace) | data services, brokers, OpenBao, persistent volumes, bootstrap Secrets, data-service-bound Dapr Components | profile-inventory facet; module digest sets are qualification inputs | its own attempt, staging first; environment-layer identity | forward-only; persistent objects survive package removal |
  | Shared infrastructure (cluster-scoped) | CRDs, Dapr control plane, ingress controller, certificate issuers, Keycloak server *(confirm)* | profile inventory | named shared-infrastructure workflow; both locks, staging first; production executor's cluster-scoped identity | forward-only |
  | Outside every release | persistent data, backups, credential and key generations, per-environment realm configuration (AD-6) | owning module or Administrator | Administrator or its declared owner | never rewound |

  Then make these text changes:
  - **AD-1:** replace the two environment-layer and Dapr-placement sentences with "Objects are placed per the AD-3 release tiers; the non-application tiers are not a duplicate topology."
  - **AD-3:** replace the environment-layer list with "Tiers other than the application package are forward-only, versioned in the private operations repository and reproducible onto replacement capacity."
  - **Production profile:** write "pins the environment-layer and shared-infrastructure tiers". Its profile-digest definition should cover both tiers' pins.
- **Displaced content:** Nothing is lost; the table carries it. The Keycloak and control-plane placement needs a one-line memlog decision.

### PR-102: The legacy MCP/CLI policy from the latest edit has three homes that already disagree, and it has no memlog entry
- **Severity:** high. **Disposition:** discuss for the deployed-or-routed question; autofix for everything else.
- **Location:** AD-11 (L117, last four sentences), the McpCli source row (L193), the last Deferred row (L324).
- **Finding:**
  - The three homes already differ:
    - **Scope:** AD-11 covers "module **and technical-module** MCP hosts, plug-ins, and CLIs … no new alternate proprietary MCP/CLI surface". The Deferred row says "no new proprietary **module** MCP/CLI host".
    - **Retirement preconditions:** AD-11 requires "owner-approved operation inventory and replacement or withdrawal evidence". The Deferred row adds "**AD-14 authorization evidence**".
  - AD-11's Binds and Prevents do not mention legacy retirement. Its last sentence ("A generic McpCli contract and transport decision is required before …") is a deferral written into a Rule.
  - The previous text said legacy hosts were "not deployed or routed in Platform compositions". The new text drops that, so it is now unclear whether EventStore.Admin.Mcp and similar hosts may run in compositions during migration.
  - The memlog's last entry is "spine finalized". No entry records this decision, so the decision lives only in the spine. The skill requires that every decision land in the memlog.
  - AD-11's status tag is still `[ADOPTED]`, although other ADs whose scope changed are tagged `[ADOPTED, AMENDED]`.
- **Proposed change:**
  - **AD-11:** end the Rule with "McpCli stdio is the only MVP MCP surface. Platform admits no new proprietary MCP/CLI surface; existing module and technical-module MCP hosts, plug-ins and CLIs, including EventStore Admin, are migration sources [*decide:* neither deployed nor routed in Platform compositions] retired under Migration and coexistence." Tag it `[ADOPTED, AMENDED]`. Add "and new proprietary CLI/MCP surfaces" to Prevents.
  - **Migration and coexistence:** append "Retire a legacy MCP/CLI source only after its owner-approved operation inventory, McpCli replacement or approved withdrawal, and AD-14 authorization evidence."
  - **Deferred row:** change it to "Generic McpCli administration and resource contracts and transport (required before non-gateway or infrastructure-admin cutover); step-up flows; generic mocks; traffic-metric rollback | McpCli, EventStore, owning team | Outside the first increment."
  - **McpCli source row:** drop "Canonical target for all Hexalith-owned CLI/MCP access", because AD-11 carries it.
  - **Memlog:** append one decision line.
- **Displaced content:** the retirement rule goes to the Migration row, the deferral to the Deferred row, and the rationale to the memlog. This saves about 45 words.

### PR-103: Production preconditions are spread over about eight locations, and "Before production update" is one 68-word sentence
- **Severity:** high. **Disposition:** autofix. This is a restructure with no semantic change.
- **Location:** Before production update (L172). The other preconditions it omits are elsewhere:
  - Binding classes (L150): "requires equal staging and production profile digests";
  - Production profile (L154): "deployable … only while current environment-layer versions fall within its qualified sets";
  - AD-2 (L63): provenance acceptance;
  - Release modes (L169): the G3 or Administrator-record trigger;
  - Automatic recovery (L175): the promotion stop must be clear;
  - Attempt ownership (L170): the lock and epoch;
  - Staging gate (L168): exact-release, non-stale evidence;
  - AD-3 (L69) and AD-6 (L87): environment-layer inputs and the realm contract applied before the attempt.
- **Finding:** A story such as "write the production deployment workflow" or "write the release validator" has to assemble the gate from about eight places. Missing one is a production-safety defect, and two independently written validators could check different subsets.
- **Proposed change:** Rewrite the row as an enumerated checklist that points to each home:

  > Production mutates only when all hold; a missing one stops without mutation and notifies. (1) Lock and new epoch held; promotion stop clear. (2) Release-mode trigger satisfied (Release modes). (3) Package, image and record provenance per AD-2. (4) Staging-gate evidence complete for this exact release and within maximum age. (5) EventStore server package release-available. (6) Staging and production profile digests equal; current environment-layer versions within the release's qualified sets; the production realm reporting the required realm-contract version or a compatible later one; required environment-layer inputs applied and verified (AD-3). (7) Valid non-empty readiness and smoke declarations for every module. (8) Production's working baseline ready and its smoke suite passing now. (9) Compatibility evidence: effective change classifications plus the staging rehearsal against this working baseline with the prepared AD-15 rollback set. Missing, breaking or wrong-baseline compatibility evidence routes the release to the Administrator-approved mode instead of stopping. Smoke writes use only synthetic identities and data, without external effects.

  Then delete "Promotion compares release-invariant digests and requires equal staging and production profile digests" from Binding classes. In Production profile, keep only the rollback-target half: "A release is a valid rollback target only while …".
- **Displaced content:** none is lost. This adds about 55 words and removes about 20.

---

## B. Build-substrate usability

### PR-104: "Breaking" is defined in five places, and the module-facing rule is hidden in AD-3
- **Severity:** medium. **Disposition:** discuss, to confirm the trigger list is complete.
- **Location:** Module declaration (L147: none/additive/breaking), Module intake (L149: effective = maximum), AD-3 (L69: "Previous code must read current schemas and events with current security state"), AD-15 (L141: additive only when the baseline tolerates it), Production profile (L154: "otherwise the change is breaking"), Staging gate (L168: a failed rehearsal is breaking evidence).
- **Finding:** An agent working on "classify Parties' release" reads Module declaration and Module intake and never reaches AD-3 or AD-15. The rule that module owners most need for NFR-1 sits inside a Platform rollout AD.
- **Proposed change:** Make Module intake the single definition: "A module release is **breaking** when its predecessor cannot read the state, events and security state it writes (AD-3), when the baseline host cannot tolerate its adapter or descriptor changes (AD-15), or when its staging rehearsal fails; otherwise **additive**, or **none** when no release-owned object changed. A candidate's effective classification is the maximum …". The AD-3 rule stays as written. AD-15, Production profile and Staging gate replace their own "breaking" clauses with "(breaking per Module intake)".
- **Displaced content:** none. Net about −10 words.

### PR-106: Record kinds and lifecycle states are used everywhere but defined nowhere in one place
- **Severity:** medium. **Disposition:** discuss, because it surfaces an ordering question.
- **Location:**
  - AD-2 (L63): the release record;
  - AD-13 (L129): "evidence-validated", "release-available" and "production-promoted" records;
  - Binding classes (L150): the attempt record;
  - Attempt ownership (L170): record location;
  - Rollout (L173): "write the … production-promoted record … before traffic";
  - Production profile (L154): "renewed production-promoted record";
  - Release modes and Automatic recovery (L169, L175): the "Administrator record";
  - Deferred (L301, L314).
- **Finding:**
  - The spine uses six kinds of record:
    - the release record;
    - the attempt record;
    - three EventStore lifecycle records;
    - the Administrator record.

    Lifecycle states come from **EventStore AD-11**, which the spine never cites, and that number collides with Platform AD-11. Nothing says who writes each record, when it is written or where it is held.
  - "Before traffic" is ambiguous. Once ingress is open, new pods receive traffic during the Helm upgrade, so the record probably has to be written before the upgrade. A builder could still write it at readiness.
- **Proposed change:** Rename the row **Binding classes and records** and append:

  > *Release record*: release-invariant values; written only by the publication workflow; held with the package in the Builds registry. *Attempt record*: attempt-bound values, epoch, timers, outcome, promotion stop; written only by the environment's executor identity; held outside the target cluster and executor host. *Lifecycle records* (EventStore AD-11 states): EventStore-issued for the server package, Platform-issued for the composed image; a production-promoted record binds one profile digest and is written before the Helm upgrade [*confirm*]. *Administrator record*: authenticated approval that selects the Administrator-approved mode, clears the promotion stop or takes over an attempt.

  AD-13's last two sentences then shorten to "the composed image's lifecycle records are Platform-issued and bind the release-available server package and extension digests".
- **Displaced content:** none is lost. Adds about 90 words and removes about 45. Record the answer to the ordering question in the memlog.

### PR-110: No entry point per unit; the Capability map lists ADs only
- **Severity:** medium. **Disposition:** autofix.
- **Location:** Capability → Architecture Map (L265–280).
- **Finding:** Here is what three probe stories have to read:
  - **Enroll Parties:** Module declaration, Startup task lifecycle, AD-9, AD-13, AD-14, AD-6, Synthetic identities, Staging gate (critical flows), Before production update (smoke declarations), Migration and coexistence, the Hosting inventory row, the Parties source row, and the First shared versions row. That is 13 locations.
  - **Write the staging deployment workflow:** 11 locations.
  - **Add a Memories recovery hook:** 7 locations.

  The template's map column is "Governed by: AD-id, **convention**, paradigm". Only the ADs are listed.
- **Proposed change:** Add convention and R&R row names to the "Governing decisions" column, for example:
  - FR-6: "… + Staging gate, Module intake, Synthetic identities, Workflows and provenance";
  - FR-7/FR-8: "… + Release modes, Attempt ownership, Interruption, Before production update, Rollout, Verification, Automatic recovery, Catalogs";
  - FR-9: "… + Startup task lifecycle, Backup coverage, Recovery point, DR sequence, DR evidence, Memories erasure continuity, Lost window".
- **Displaced content:** none. Adds about 70 words and makes the map the auditor's checklist that the template intends.

### PR-113: Coined terms are used before they are defined, and some are never defined
- **Severity:** medium. **Disposition:** autofix.
- **Location:**

  | Term | First used | Defined |
  | --- | --- | --- |
  | release-invariant | AD-2 (L63) | Binding classes (L150) |
  | working baseline | AD-2 (L63) | Binding classes (L150) |
  | environment-current | AD-3 (L69) | Binding classes (L150) |
  | G1, G2, G3 | AD-9 (L105) | Production entry gates (L176) |
  | SM-4, SM-5 | L169, L174, L176, L180 | never; they are in the PRD |
  | evidence-validated, release-available, production-promoted | AD-13 (L129) | never; they are EventStore AD-11 states |
  | attempt-bound | L150 | L150 (used only there) |

- **Finding:** A small agent that opens AD-2 cold cannot resolve these terms. Foreign AD numbers also collide with Platform numbers: EventStore AD-10 and AD-11 against Platform AD-10 and AD-11, and Projects AD-28 against EventStore AD-28.
- **Proposed change:** Add a **Terms** paragraph after "Roles" in Design Paradigm, about 70 words:

  > Unprefixed AD-n means this spine; other modules' decisions always carry the module name. PRD glossary terms apply unchanged: working release, working baseline, promotion stop, verification window, recovery point; G1–G3 are the PRD production entry gates and SM-n the PRD success measures. Binding classes and record kinds are defined under Consistency Conventions → Binding classes and records. Lifecycle states are EventStore AD-11's.

  Also prefix "AD-29" in the EventStore source row.
- **Displaced content:** none.

### PR-109: Single-paragraph Rules and long table cells hide the sub-structure
- **Severity:** medium. **Disposition:** autofix. Formatting only, with zero word change; `lint_spine.py` checks only for the presence of Binds, Prevents and Rule.
- **Location:** AD-1, AD-6, AD-7, AD-8, AD-11, AD-13, AD-14 (seven to twelve sentences each); the DR sequence (L180, six numbered steps inside a table cell); Module declaration (L147, a 110-word sentence with 19 semicolons).
- **Finding:** A story agent searching for "which credentials may the staging executor hold" has to parse a 251-word paragraph. The Binds/Prevents framing still matches for these ADs. What fails is findability, not scope.
- **Proposed change:**
  - Render these Rules as three to six labelled sub-bullets. For AD-8, for example: *Namespaces and data*, *Principals*, *Pod and network*, *Dapr*, *Scheduling*, *Proof*.
  - Move the DR sequence out of the table into a numbered list directly under the R&R table, and leave a one-line pointer in the row.
  - Group Module declaration fields into labelled clusters: identity, Dapr and data, network, secrets and identity, lifecycle and recovery, release.
- **Displaced content:** none.

### PR-111: The Deferred "Owned work" table has no gate column, and one row mixes gates
- **Severity:** medium. **Disposition:** autofix.
- **Location:** L305–324, in particular:
  - L316, "Secrets, identity, network and transport", is an 82-word sentence gated "Before hosted readiness", but its last sentence is gated "before the first local enrollment";
  - L310 is gated "before the first module enrolls";
  - L313 is gated "before FR-12 acceptance";
  - L311 is gated "before Folders joins".
- **Finding:** Epic sequencing is the main thing Deferred should make convergent. The gates exist, but they are embedded in prose and not sorted. An agent planning local enrollment will miss the local mTLS ratification inside a "hosted readiness" row. First shared versions already models the fix with its "Must precede" column.
- **Proposed change:** Use the columns Work | Owner | **Gate** | Acceptance boundary, sorted by gate: first local enrollment → Folders joins → hosted readiness → G1 → G2 → G3 → FR-12 acceptance → trigger-based → ongoing. Split L316 into "Identity and secrets" and "Network isolation", and give "Ratify local mTLS, per-receiver ACL and scoped-component convention" its own row gated on the first local enrollment.
- **Displaced content:** none. Adds about 25 words.

### PR-112: The production attempt order is spread across five rows; a diagram would carry it
- **Severity:** medium. **Disposition:** discuss. This is optional and adds about 15 diagram lines.
- **Location:** Catalogs (L151, activation order), Rollout (L173), Verification (L174), Automatic recovery (L175), Attempt ownership (L170).
- **Finding:** The skill's rule is "carry shape in diagrams, not prose". The attempt is a sequence with branches, and drafting it exposes two ordering questions the prose leaves open:
  1. When is the production-promoted record written? See PR-106.
  2. Does a successfully recovered attempt count as a "non-working terminal outcome" that sets the promotion stop?
- **Proposed change:** Add one flowchart under R&R. It is a sketch; confirm the labels against the rows.

  ```mermaid
  flowchart TD
    L["Lock, new epoch"] --> P{"Before production update"}
    P -- missing --> S["Stop without mutation, notify"]
    P -- ok --> G["Prepare candidate and rollback generations;<br/>ready-validate rollback generation on baseline hosts"]
    G --> R["Write production-promoted record"] --> H["Helm upgrade retained package by digest"]
    H --> RD{"Ready ≤10 min?"}
    RD -- yes --> C["Commit candidate generation"] --> V{"5-min verification"}
    V -- working --> W["Record working baseline"]
    RD -- no --> REC["One recovery: baseline package + environment-current values,<br/>commit rollback generation"]
    V -- failed --> REC
    REC --> RV{"Ready and verified?"}
    RV -- yes --> WS["Baseline working; promotion stop set?"]
    RV -- no --> X["Stop for intervention; promotion stop set"]
  ```

  Then shorten the Catalogs activation sentence to "Activation follows the production attempt diagram; recovery commits the rollback generation."
- **Displaced content:** none. Net about +45 words.

### PR-105: The staging rehearsal content is split between AD-15 and the Staging gate row
- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-15 (L141: "exercising at least one idempotent command new in the candidate") and Staging gate (L168: "verifies the baseline reads state and events the candidate wrote").
- **Finding:** Each location names a different rehearsal obligation. The staging-workflow agent reads the row, and only the pointer leads to the second obligation.
- **Proposed change:** Staging gate: "… rehearses the AD-15 rollback set, exercising at least one idempotent command new in the candidate, and verifies the baseline reads state and events the candidate wrote." AD-15: replace its sentence with "Staging rehearses it (Staging gate)."
- **Displaced content:** none. About −15 words.

---

## C. Proportionality

**Heavy but proportionate; no change.** Each item below is user-adopted or guards production safety, erasure or tenant isolation. None of them is duplicated beyond what PR-114 to PR-117 fix, and each is enforceable:
- tenant-key custody class (PR-37);
- full rotation on DR (PR-38);
- per-environment OpenBao and data namespaces;
- two executors plus an off-site recovery executor;
- lock epoch and takeover;
- off-site monitor plus hourly dead-man check;
- off-cluster export of realm admin events;
- Pod Security `restricted`, default-deny NetworkPolicy and Dapr ACLs;
- evidence maximum age;
- the one-realm contract;
- the AD-4 check that the tool's commit equals the submodule HEAD;
- current-plus-previous majors for the declaration schema and the extension API.

### PR-122: "One sidecar patch per release across **local**, CI and staging" is heavy for developer machines and agent sandboxes
- **Severity:** low. **Disposition:** discuss.
- **Location:** Production profile (L154). It comes from memlog V-47, "one Dapr patch per release record".
- **Finding:** CI and staging drift is a real divergence risk. Local sidecars, however, come from each machine's `dapr init`. Failing local runs on a patch mismatch would push every developer and agent environment to track release patches, for little value at the MVP stage.
- **Proposed change:** "One sidecar patch per release across CI, staging and production, within the control plane's supported skew; local runs report their patch in diagnostics and warn on mismatch."
- **Displaced content:** none.

### PR-108: AD-7 carries a revisit trigger, an AD-5 duplicate and the recovery executor
- **Severity:** medium. **Disposition:** autofix for the first three items; discuss for the recovery-executor move.
- **Location:** AD-7 (L93).
- **Finding:**
  - "Adopt GitHub Team controls once a second person gains write access" is a revisit trigger, not a Rule. The memlog (L207) calls it an owned Deferred item, but no Deferred row exists.
  - "Routine build and integration jobs stay on disposable hosted runners" repeats AD-5. This is residue from the first run's PR-22.
  - "Co-scheduling on the shared node is the AD-8 residual risk" repeats Accepted risks.
  - The off-site recovery executor is recovery independence, which is AD-12's concern. AD-12 already calls it "the AD-7 recovery executor".
- **Proposed change:**
  - Delete the AD-5 duplicate and the co-scheduling clause.
  - Move the GitHub Team sentence to a new Deferred row: "GitHub Team controls | Administrator | Trigger: a second writer on the operations repository".
  - Optionally move "An off-site recovery executor runs only recovery workflows with pre-provisioned credentials for the prepared capacity, held in off-site custody; drills use it" into AD-12. In that case, move "recovery execution" from AD-7's Binds to AD-12's Binds.
- **Displaced content:** as listed above. AD-7 goes from 251 to about 215 words.

---

## D. Duplication residue and trims

### PR-107: The AppHost and hosting-ownership rule has four homes, and a binding refusal sits in Deferred
- **Severity:** medium. **Disposition:** autofix.
- **Location:** Paradigm (L27: domain modules "own no AppHost"), AD-10 (L111), Migration and coexistence (L160: "frozen … until retirement"), Deferred L309 ("stay frozen until parity and EventStore AD-22 authority … technical-module hosts stay valid"), Deferred L310 ("new hosting helpers are refused until then").
- **Finding:** The Paradigm states the target and Migration states the transition. An agent reading only the Paradigm could delete a domain AppHost now. L309's "those of later domain modules" also implies that later modules have AppHosts to freeze, which contradicts "own no AppHost".
- **Proposed change:**
  - Paradigm: "… they own no AppHost, Aspire or ServiceDefaults infrastructure; existing ones follow Migration and coexistence."
  - Migration row: owns the freeze, retire and technical-module-tests rule, and absorbs "No new hosting helper is admitted until the canonical ServiceDefaults and Dapr helper set is recorded here."
  - L309: "Inventory each domain module's hosting projects and produce parity and AD-22 evidence."
  - L310: keep only the work.
  - AD-10: keep its sentence, which is about evidence.
- **Displaced content:** the rule goes to the Migration row. About −45 words.

### PR-114: The recovery render and the incompatible-release mode are each stated three or four times
- **Severity:** low. **Disposition:** autofix.
- **Location:**
  - "Working baseline's package with environment-current values" appears in AD-3 (L69), AD-15 (L141), Automatic recovery (L175) and Binding classes.
  - "Incompatible → Administrator-approved" appears in AD-3 (L69), AD-15 (L141), Release modes (L169) and Before production update (L172).
- **Proposed change:** AD-3 defines the render once, and AD-15 and Automatic recovery say "the AD-3 recovery render". Release modes owns the incompatible-release routing; delete AD-3's last sentence. PR-103's checklist keeps its routing clause.
- **Displaced content:** none. About −35 words.

### PR-115: Single-node and whole-site risks are restated outside Accepted risks
- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-7 (L93), AD-8 (L99: "The single-node shared kernel is an accepted residual risk; pods on one node do not establish node or site resilience"), DR evidence (L181: "Whole-site loss is covered only when …"), and Accepted risks (L328).
- **Proposed change:** Delete these sentences from AD-8 and DR evidence. Accepted risks is their only home, and it already states both.
- **Displaced content:** Accepted risks, which already holds them. About −35 words.

### PR-116: The staging-hostname ban has two homes with different mechanisms
- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-8 (L99: "a staging release declaring a production hostname is rejected", which is a validator check) and Hosted interfaces (L157: "the staging namespace can neither serve … nor obtain their certificates", which is admission).
- **Finding:** Both mechanisms may well be intended as defence in depth, but a builder reading only one home implements only one of them.
- **Proposed change:** State both in Hosted interfaces: "The release validator rejects a staging release declaring a production or shared-infrastructure hostname, and the staging namespace can neither serve those hostnames nor obtain their certificates (seed: …)". In AD-8, change the negative-test clause to "… and the Hosted interfaces hostname rules hold".
- **Displaced content:** none. About −12 words.

### PR-117: Provenance fields are listed in AD-2 and in Workflows and provenance, and the lists differ
- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-2 (L63: "provenance names the protected publication workflow and ref") and Workflows and provenance (L155: "caller repository, workflow, protected ref and pinned Builds workflow ref").
- **Proposed change:** AD-2: "Deploy accepts only digests whose provenance matches the Workflows and provenance convention; the release record only from the publication workflow; attempt records and evidence only from the environment's executor identity."
- **Displaced content:** none. About −15 words.

### PR-118: Hosted interfaces restates the gateway, the Deferred exposure decisions and Dapr invocation
- **Severity:** low. **Disposition:** autofix.
- **Location:** Hosted interfaces (L157).
- **Finding:**
  - "Each environment's public exposure uses one declared path, each DNS zone has one named owner, and certificates use one declared ACME challenge" repeats the Deferred Exposure row (L317). With one Administrator, "one named owner" does not bind anything.
  - "The gateway is the composed `eventstore` host's …" repeats AD-13.
  - "Internal service invocation uses Dapr while keeping module API contracts" repeats AD-9 and the Paradigm diagram.
- **Proposed change:** Keep only "certificates use per-environment ACME credentials". Reduce the gateway sentence to "Ingress terminates TLS and routes to the AD-13 gateway but makes no authorization decision; no external caller bypasses it to reach pods." Delete the Dapr sentence.
- **Displaced content:** the Deferred Exposure row (declarations), AD-13 and AD-9. About −40 words.

### PR-119: The "Module service-level and functional qualification" Deferred row assigns no work
- **Severity:** low. **Disposition:** autofix. This is residue from the first run's PR-25.
- **Location:** L321.
- **Proposed change:** Delete the row. The Source Precedence paragraph (L187) already says module gates are never waived.
- **Displaced content:** Source Precedence. About −20 words.

### PR-123: The Memories.Aspire digest sets have four homes
- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-3 (L69), Production profile (L154), Memories source row (L192), Infrastructure currency (L318).
- **Proposed change:** Keep AD-3 (or the PR-101 table) and L318, which is work. Drop the sentence from Production profile and the clause from the Memories row.
- **Displaced content:** none. About −15 words.

### PR-125: AD-13 contains a one-time process step
- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-13 (L129): "EventStore ratifies this rule once, registering Platform as issuer for composed subjects". The EventStore source row (L191) and the Deferred Composed host row (L311) repeat it.
- **Proposed change:** Move it to First shared versions as "AD-13 ratification and composed-subject issuer registration | EventStore | Platform | composed host release-available", and delete it from AD-13 and from the source row.
- **Displaced content:** First shared versions. About −5 words net.

### PR-126: The Stack Status column carries action items and observations
- **Severity:** low. **Disposition:** autofix.
- **Location:** Stack (L207–213): "README floor 13.4.6 and installed 13.5.3 to align"; "Must move to the Builds catalog 3.109.0; local mode uses the root-declared source"; "observed 1.18.1–1.18.4 skew to reconcile"; "accepted risk owned by Platform".
- **Finding:** The template gives the Stack as "Name + version only; the why lives in the memlog". Alignment work is already owned by the Deferred row "Source/package adoption" ("align Stack pins to the Builds catalog"), and the prerelease risk by Accepted risks.
- **Proposed change:** Reduce Status to short tags such as `current`, `prerelease`, `to align` or `toolchain`. Keep "Must equal the AppHost SDK, checked by the Platform tool", which is a rule; alternatively, move it to Local tool and readiness.
- **Displaced content:** the Deferred Source/package row and the memlog (V-46 evidence, already present). About −35 words.

---

## E. Readability

### PR-120: AD-14 contains a rationale clause
- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-14 (L135): "Because `azp` authenticates only confidential clients, surfaces permitted …".
- **Proposed change:** "Surfaces permitted UI-only or confirmation-required operations are confidential server-side clients without loopback or wildcard redirects; public clients such as McpCli are agent-capable least privilege."
- **Displaced content:** memlog (ADV-U8 or the confidential-client entry). About −7 words.

### PR-121: The rollback terminology drifts
- **Severity:** low. **Disposition:** autofix.
- **Location:** "rollback set" (AD-15 title, L273, L278), "rollback combination" (AD-15 Rule, L172), "AD-15 combination" (L172), "rollback generation" (L151, L175).
- **Proposed change:** Define the term once in AD-15: "the **rollback set** is the working baseline's package with environment-current values plus the **rollback generation**, a forward catalog generation holding …". Then use only "rollback set" and "rollback generation".
- **Displaced content:** none.

### PR-124: A companion trigger for the declaration field list
- **Severity:** low. **Disposition:** defer.
- **Location:** Module declaration (L147).
- **Finding:** The field enumeration is correct for now because it is the only source. Once `hexalith.module` v1 exists (First shared versions, L292), however, the spine and the schema become two copies.
- **Proposed change:** Add to that First shared versions row: "on publication, the Module declaration row links to the schema instead of enumerating fields".
- **Displaced content:** the schema, later.

---

## Considered and not raised

- **R&R thresholds, "Local tool and readiness" and AD-10 lifecycle wording.** PR-01 was declined, and the PRD reconciliation required this wording. For the same reason, this review does not propose trimming the envelope numbers restated in the Source Precedence paragraph (L187), even though they are a third copy within the spine. Moving R&R wholesale into a companion was also rejected on this ground.
- **PR-37 and PR-38.** Kept, as agreed.
- **AD-4, AD-5, AD-9, AD-10 and AD-12.** They pass the inclusion test as written, apart from the cross-references already listed.
- **Structural Seed diagrams and First shared versions.** Proportionate. Keep them.

## Suggested order of application

1. **Decisions first (discuss):** PR-101 (Keycloak and control-plane tier), PR-102 (legacy hosts deployed or routed), PR-106 (production-promoted record timing), and PR-104's trigger list. Each needs a memlog line.
2. **Then consolidate (autofix):** PR-103, PR-113, PR-110, PR-111, PR-107, PR-108 and PR-105.
3. **Then trim (autofix):** PR-114 to PR-121, PR-123, PR-125 and PR-126.
4. **Formatting last:** PR-109, so that line references in this report stay valid while the earlier fixes are applied.
