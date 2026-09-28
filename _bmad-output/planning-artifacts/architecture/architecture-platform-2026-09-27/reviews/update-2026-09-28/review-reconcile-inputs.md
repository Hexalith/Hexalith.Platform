# Input reconciliation review — update run 2 (2026-09-28)

Reviewer: independent input-reconciliation lens (read-only).
Spine: `ARCHITECTURE-SPINE.md` (467 lines, status draft, updated 2026-09-28).
Inputs: final Platform PRD `prd.md` + `addendum.md`; approved `sprint-change-proposal-2026-09-27.md`; `references/Hexalith.Builds/README.md` "G-4 Local Tools" (checked against the Builds checkout at 0610f78, including `schemas/hexalith.module-manifest.v1.json` and `src/libraries/Hexalith.Builds.Module.Cli`). Decision log: `.memlog.md` (only entries that explicitly override an input are treated as overrides — the Folders scope decision at memlog L153, Projects V-05, McpCli AD-17 via C-04, and the AD-11 scope via C-01).

## Verdict: CONDITIONAL PASS

Every numeric PRD threshold and gate survives unweakened, the deputy role, notification recipients, response coverage and the SM-4 temporary synthetic grant are carried, and the sprint change proposal's edit 9 clause and scope limit have landed. Two High gaps remain: the deputy (and, through a precondition deadlock, even Administrator) has no non-DR manual recovery path after a failed automatic recovery, and an approved release lacking compatibility evidence is not required to name a planned recovery. Both are fixable without reversing an adopted decision.

| Severity | Count |
| --- | --- |
| Critical | 0 |
| High | 2 |
| Medium | 10 |
| Low | 8 |
| **Total** | **20** |

Disposition: 15 autofix, 4 discuss, 1 defer.

## Threshold and gate check (all preserved)

| PRD value | PRD ref | Spine location | Result |
| --- | --- | --- | --- |
| Backup/incremental runs every 30 min | FR-9 L222 | Backup coverage L276; Source Precedence L312 | Match |
| 7-day frequent / 30-day daily retention, complete chains | FR-9 L222 | Backup coverage L276 | Match |
| RPO ≤ 1 h continuously, measured from declared cut | NFR-2 L296, FR-9 L223 | Recovery point L277; DR evidence L279 | Match |
| RTO ≤ 4 h within declared coverage, clock never pauses, stays applicable when coverage ends | NFR-2 L297–301 | Detection and response L278 | Match |
| Freshness check ≥ every 15 min; warn before 1 h; notify on failure / age > 1 h; hourly monitor-silence check | FR-9 L225 | Recovery point L277; Diagnostics L228 | Match |
| Availability probe ≥ every 5 min from G1 | FR-9 L225 | Detection L278; G1 L275 | Match |
| Local startup deadline 10 min from request, finite override, effective value shown | FR-4 L123 | Local tool L225 | Match (see RI-16 for "justified") |
| Rollout ready within 10 min of deployment start | FR-7 L186 | Timing L270 (t0 at first mutation, stricter-or-equal); diagram L253 | Match |
| 5-min verification window; 60 continuous s unavailability; 10-s sampling | FR-7 L187–189 | Verification L272 | Match |
| Smoke fails twice, second 30 s after first; missing pass at deadline fails; timeout fails | FR-7 L188–189 | Verification L272 | Match |
| Recovery: 10 min readiness + same 5-min window, restored release's checks | FR-8 L202 | Automatic recovery L273 | Match |
| One automatic recovery, no cycling, interruptions never replenish | FR-7 L191, FR-8 L205 | Timing L270; Automatic recovery L273 | Match |
| Drill before G2, monthly, after material storage/backup changes | FR-9 L228 | DR evidence L279 | Match |
| G1 / G2 / G3 conditions | PRD L50–54, addendum L179–183 | Production entry gates L275 | Match except RI-11, RI-17 wording |
| SM-4 temporary synthetic grant | PRD L56, addendum L185 | G1 row L275 | Carried; internal contradiction RI-11 |
| Deputy receives all deployment/recovery/backup/monitor notices; independent recovery/key access; restores, verifies, reopens; cannot clear stop or administer admission | PRD L34, L203–204, L377; addendum L214–225 | Roles L34; Diagnostics L228; AD-6 L116; AD-7 L128; DR sequence L300–308 | Carried; execution gap RI-1 |
| Response coverage with time zone, primary/deputy responsibility, max acknowledgement | NFR-2 L299, glossary L381 | Detection and response L278 | Match (RI-15, RI-19 minor) |

## Sprint change proposal reconciliation

Landed: canonical `references/Hexalith.McpCli` link (sources L16, table L318); McpCli as the sole target for Hexalith-owned module-operation CLI/MCP access (AD-11 L161–171); EventStore Admin CLI/MCP named as obsolete migration sources (L170); edit 9's "not enrolled into Platform compositions" plus "named migration record and removal gate" (L170, stricter: outside hosted environments only); retirement only after owner-approved inventory, replacement or withdrawal, authorization, compatibility and acceptance evidence (L170); AD-14 denial of UI-only/confirmation operations preserved (L197); new First-shared-versions row for the migration inventory and generic admin contract (L433). The §1 scope limit is respected: external tooling is untouched and the AD-11 Scope clause (L169) narrows the rule to module-operation surfaces by explicit decision C-01. Gaps: RI-9, RI-10, RI-18.

## Builds tool reality

Builds ships `Hexalith.Builds.Module.Cli` (`hexalith-module`: `run`, `down`, `test` only) and `hexalith-evidence`, unpublished, with a strict `hexalith.module-manifest.v1` whose `platform` block hard-codes `eventStoreVersion 3.109.0`, `daprRuntimeVersion 1.18.2`, `daprSdkVersion 1.18.10` and `frontComposerVersion 4.5.0`; `down` removes only runner-owned invocation metadata and live persisted composition is unavailable at `HXR003`. The spine's ratification (Terms L36, Builds row L319, Owned work L439) is directionally right and admits the unpublished state, but overstates the current schema and under-specifies the lifecycle work: RI-7, RI-8, RI-13, RI-20.

## Findings

### RI-1 — No manual (non-DR) recovery path for the deputy; precondition deadlock blocks Administrator too
- **Severity:** High
- **Input:** PRD FR-8 L204 ("The deputy may perform documented recovery, verify restoration and reopen service"), L207 ("A manual recovery or disaster restore becomes the working baseline only after its required verification passes"), L34 (deputy acts when Administrator is unavailable); addendum L207, L218 (Option 1: deputy "executes the documented recovery procedure through their own identity"), L225.
- **Spine:** attempt diagram L261 ("Stop for intervention"); Empty or degraded production L268 ("A manual change is always an Administrator-approved attempt"); Attempt ownership L269 (only an Administrator record may take over); Production preconditions #1 L288 (promotion stop must be clear); Promotion stop L274 (cleared only with a verified current working release); Binding classes L219 (deputy holds no Administrator-record identity).
- **Gap:** After a failed or unverified automatic recovery, or an executor dying during verification, the deputy's only available action is a full DR entry (fence, rebuild on prepared capacity, restore from backups with up to one hour of data loss), which is disproportionate for a failed rollout on healthy infrastructure and not what FR-8 describes. For Administrator as well, a manual approved attempt must pass precondition 1 (stop clear), yet the stop can only be cleared by naming a verified current working release, which does not exist after a failed recovery. The PRD's manual-recovery path in FR-8 is therefore reachable only through DR.
- **Proposed fix:** Add a Release and Recovery Acceptance row **Manual recovery**: "After a failed, unverified or interrupted recovery, Administrator or the deputy may start a manual recovery to the recorded working baseline, or to an approved release's named recovery, through the recovery or production executor under their own MFA identity. It takes the lock with a new epoch and writes a recovery-initiation record signed by that identity, which is not an Administrator record. Preconditions 1, 2, 4 and 8 do not apply, and the promotion stop stays set. Verification is the same as automatic recovery. The result becomes the working baseline only after verification passes, and only an Administrator record clears the stop." In precondition 1, qualify "the promotion stop is clear" as "for release attempts".
- **Disposition:** discuss (it widens deputy authority to a non-DR executor path, and the GitHub Free single-writer boundary at addendum L326 must be preserved).

### RI-2 — An approved release without compatibility evidence need not name a planned recovery
- **Severity:** High
- **Input:** PRD FR-6 L168 ("If safe automatic rollback cannot be demonstrated, approval names the recovery procedure and its acceptance checks before the attempt"); NFR-1 L286; addendum L171, L203.
- **Spine:** Release modes L267 (only "An incompatible release's record names … its separately planned recovery"); precondition 9 L296 ("Missing, breaking or wrong-baseline evidence routes the release to the Administrator-approved mode"); Module intake L218 ("breaking" excludes *missing* or *stale* evidence); AD-15 L207.
- **Gap:** A release whose compatibility evidence is missing, stale or against the wrong baseline is routed to approved mode, yet only a *breaking* release must name a planned recovery. The release therefore proceeds on an unproven AD-15 automatic recovery, which weakens the PRD's "cannot be demonstrated" gate.
- **Proposed fix:** In Release modes, replace the sentence with: "When compatibility evidence is missing, stale, wrong-baseline, failed or breaking, safe automatic rollback is not demonstrated. The release follows the incompatible-release path, and its record names, before the attempt, its separately planned recovery and acceptance checks." Mirror this in precondition 9.
- **Disposition:** autofix.

### RI-3 — Technical modules' Platform-workspace requirement not carried; C-31 implies technical-module workspaces producing Platform evidence
- **Severity:** Medium
- **Input:** PRD FR-2 L84, L88 (EventStore, Memories and McpCli are directly declared in Platform and run and debugged from source in that workspace); SM-2 L316; SM-3 L317 ("from its FR-2 workspace"); non-goal L62; glossary L358; FR-4 L121.
- **Spine:** AD-4 L97–100 ("Every workspace that runs the tool declares `references/Hexalith.Platform`"); Owned work L442 ("McpCli keeps its reference"). The phrase "Platform workspace" appears nowhere in the spine. See also memlog C-31 ("technical modules producing Platform evidence").
- **Gap:** The spine never states that the three technical/tool modules are root-declared in Platform and debugged, and produce their SM-2/SM-3 evidence, from the Platform workspace. It instead implies that McpCli and other technical-module workspaces may run the Platform tool for Platform evidence. That conflicts with the PRD's workspace model and non-goal.
- **Proposed fix:** Add to AD-4 *Mapping*: "EventStore, Memories and McpCli are declared directly under Platform's `references/` and are built, launched and debugged from source in the Platform workspace. Their Platform source-debugging and integration evidence comes only from that workspace. A technical module's own workspace, including McpCli's Platform reference, supports own-repository tests only (AD-10)."
- **Disposition:** autofix.

### RI-4 — Production preconditions omit module production qualification; unchanged modules not explicit in the gate
- **Severity:** Medium
- **Input:** PRD L46 ("An enrolled module has supplied its module-owned declarations and qualification evidence for the target environment. Local or CI enrollment alone does not qualify a module for production"); FR-7 L185 ("checks required module release qualifications"); FR-6 L161 and L153 ("The release includes unchanged enrolled modules, so changing one module does not omit the others' required checks").
- **Spine:** Production preconditions #7 L294 (only readiness/smoke declarations); Staging gate L266 ("Every included module").
- **Proposed fix:** Precondition 7: "Every included module is enrolled for production, with valid declarations and production qualification evidence present, and has valid, non-empty readiness and smoke declarations." Staging gate: "every enrolled module in the complete release, including unchanged modules, declares a non-empty critical-flow set …"
- **Disposition:** autofix.

### RI-5 — Re-validation triggers dropped (SM-5 after recovery-policy changes, SM-3 after lifecycle changes, Memories continuity)
- **Severity:** Medium
- **Input:** PRD SM-5 L319 ("before G3 and after recovery-policy changes"); SM-3 L317 ("after lifecycle changes"); owned table L345 ("Before G2 and whenever recovery mechanisms change").
- **Spine:** G3 L275 ("only after SM-5 rehearsals"); Owned work L454, L459.
- **Gap:** Once G3 is reached, a change to verification, recovery or rollback-set policy does not suspend automatic promotion until SM-5 repeats. Runner lifecycle changes and Memories recovery-mechanism changes carry no repeat trigger either.
- **Proposed fix:** G3 row: "A change to FR-7/FR-8 verification, recovery or rollback-set policy suspends automatic promotion until the affected SM-5 rehearsals repeat." Owned work L439 and L459: add "repeat SM-3 lifecycle scenarios after runner lifecycle changes" and "repeat whenever recovery mechanisms change".
- **Disposition:** autofix.

### RI-6 — The PRD's "before recovery/deployment stories are finalized" re-sync gate became unconditioned maintenance
- **Severity:** Medium
- **Input:** PRD owned table L346 ("Align architecture operational access and notification recipients … synchronize spec/acceptance wording for both release modes, G1–G3 and RTO coverage | … | Before recovery/deployment stories are finalized"). The addendum now carries stale architecture text: L34 ("the pinned CI tool must embed the same commit"), L214 (issues "assigned to Administrator"), L225 ("Architecture follow-up is required").
- **Spine:** Maintenance row L460 ("not an enrollment block"); memlog C-63 defers the SPEC re-sync.
- **Proposed fix:** Maintenance row: "re-sync `specs/spec-platform/SPEC.md` and the PRD addendum (L34 tool identity, L214 recipients, L225 follow-up) **before recovery or deployment stories are finalized**; not an enrollment block."
- **Disposition:** autofix.

### RI-7 — The ratified manifest schema does not match what Builds ships
- **Severity:** Medium
- **Input:** Builds README L239–243 (strict v1 that rejects unknown fields); `schemas/hexalith.module-manifest.v1.json` L6–9 (field `schema`, `additionalProperties: false`), L63–68 (constants for EventStore, Dapr runtime, Dapr SDK **and FrontComposer 4.5.0**).
- **Spine:** Module declaration L216 ("Builds implements them as `hexalith-module`'s `hexalith.module-manifest.v1` … Each module owns its instance with `schemaVersion`"); Owned work L439 ("Remove the manifest schema's hard-coded EventStore and Dapr version constants").
- **Gap:** v1 uses `schema`, not `schemaVersion`. It lacks readiness, startup tasks, surfaces, critical flows, change classification and recovery inventory, and its closed schema would reject them. The present tense therefore overclaims, and the FrontComposer constant is missing from the removal list.
- **Proposed fix:** L216: "Builds implements them in `hexalith-module`'s manifest schema. Current v1 (field `schema`) lacks these fields, so the Platform declaration ships as its next major or a superseding schema, per the First shared versions row." L439: "remove the EventStore, Dapr and FrontComposer version constants".
- **Disposition:** autofix.

### RI-8 — Runner lifecycle ratified but not enumerated as work, with no gate on local/CI evidence
- **Severity:** Medium
- **Input:** Builds README L210–212, L256–273 (`run`/`down`/`test` only; `down` removes only runner-owned invocation metadata; live persisted composition unavailable at `HXR003`); PRD owned table L339 ("Platform runner ownership and disposable CI lifecycle | … | Before accepting local/CI composition and testing evidence"); FR-4 L122–138.
- **Spine:** AD-10 L156–159; Local tool L225; Owned work "First tool publication" L439 (covers only constants, AppHost, EventStoreHost, identity check, debug entry point and CI tier).
- **Gap:** None of these AD-10 behaviours exist in Builds today:
  - collision failure
  - deadline and override
  - first-terminal-outcome retention
  - listing with owner and age
  - compatibility-checked attach
  - resource cleanup, as opposed to metadata-only removal, that reports leftovers
  - CI partial-start cleanup
  - the environment descriptor

  None of them is an acceptance item, and nothing gates local/CI evidence on them.
- **Proposed fix:** Add an Owned work row, gate "Before accepting local/CI composition and testing evidence", owner "Builds with Platform": "implement the AD-10 lifecycle in `hexalith-module` (the list above), and resolve `HXR003`."
- **Disposition:** autofix.

### RI-9 — Stable McpCli publication monopoly vs the proposal's retained McpCli initial release
- **Severity:** Medium
- **Input:** Sprint change proposal §3 L52 ("Keep the existing McpCli initial release with its demonstrated Tenants/Parties scope"); §5 L135. McpCli AD-17 publishes stable to nuget.org.
- **Spine:** AD-11 *Tool* L166 ("McpCli's own pipeline publishes only prerelease versions"); McpCli row L318 (overrides AD-17); memlog C-04.
- **Gap:** C-04 overrides McpCli AD-17 but does not address the approved proposal's retained first-increment release. As written, McpCli cannot ship a stable first increment until Platform staging validation exists.
- **Proposed fix:** Choose one of the following and record it in the memlog:
  - (a) "McpCli's first increment ships as prerelease until the first Platform staging validation", noted in the McpCli Epic 4/5 handoff; or
  - (b) "McpCli may publish stable versions for standalone local use; Platform compositions and hosted environments accept only Platform-published, staging-validated versions."
- **Disposition:** discuss.

### RI-10 — The "new surfaces" wording leaves a door open that proposal edit 9 closed
- **Severity:** Medium
- **Input:** Proposal edit 9 L80 ("No later module-owned proprietary MCP/CLI host is admitted"); PRD FR-12 L278 ("Platform does not add or publish another proprietary Hexalith MCP/CLI surface").
- **Spine:** AD-11 *New surfaces* L171 ("Any new MCP/CLI surface or transport … needs a Platform AD admitting it"); Trigger row L462 (owner "Owning capability or product team").
- **Proposed fix:** L171: "Any new **McpCli** surface or transport … needs a Platform AD admitting it under AD-14. No module-owned or other proprietary Hexalith MCP/CLI host or CLI is admitted, and Platform publishes no other such surface." L462 owner: "McpCli with Platform".
- **Disposition:** autofix.

### RI-11 — The SM-4 temporary grant contradicts the "only through the synthetic-admission group" rule
- **Severity:** Medium
- **Input:** PRD L56 ("Ingress remains limited to executor/probe sources, no general users are admitted, and the test grant is revoked after the check"); addendum L185.
- **Spine:** G1 row L275 (adds the synthetic identity to the **human** production-admission group) vs Synthetic identities L227 ("Synthetic actors are admitted only through a standing … synthetic-admission group limited to the synthetic tenant") and AD-6 L115. The executor/probe-only ingress during the grant is not restated.
- **Proposed fix:** L227: "… only through the synthetic-admission group, except the G1 SM-4 temporary grant. That identity holds production permissions limited to the synthetic tenant and is exercised only from executor/probe sources while ingress stays closed. Its revocation is recorded and followed by a denial check."
- **Disposition:** autofix.

### RI-12 — A compromise-driven DR may skip realm-key rotation
- **Severity:** Medium
- **Input:** Addendum L273 ("Compromise recovery also rotates the Dapr trust root and realm keys").
- **Spine:** DR step 5 L306 ("Rotate realm signing keys unless the old issuer is proven unavailable … A compromise-driven restore also rotates the Dapr trust root").
- **Gap:** The unavailable-issuer exception still applies under compromise, when the old keys may be held by an attacker.
- **Proposed fix:** "A compromise-driven restore also rotates the Dapr trust root and the realm signing keys, without the unavailable-issuer exception."
- **Disposition:** autofix.

### RI-13 — Tool/submodule identity mechanism changed without a recorded override or a compatibility check
- **Severity:** Low
- **Input:** Addendum L34 ("the pinned CI tool must embed the same commit"; "tool/submodule identity mismatches fail explicitly").
- **Spine:** AD-4 *Platform identity* L100 (the composition carries the commit; the tool compares it with the submodule HEAD); memlog C-02 and C-31 do not state that they override the addendum.
- **Gap:** The Platform-identity guarantee is kept. However, the Builds tool now versions independently of the Platform composition it loads, and nothing checks that the two are compatible.
- **Proposed fix:** Memlog: record that C-31 supersedes the addendum L34 mechanism. AD-4: "the tool also refuses a Platform composition whose declared tool-contract range excludes its version."
- **Disposition:** autofix.

### RI-14 — Notification content omits status
- **Severity:** Low
- **Input:** PRD FR-8 L203 ("identify the release, environment, status and access-controlled diagnostic evidence"); addendum L214 ("and outcome").
- **Spine:** Diagnostics L228.
- **Proposed fix:** "notifications carry environment, release identity, **outcome status** and opaque references …"
- **Disposition:** autofix.

### RI-15 — Incident and drill records drop fields
- **Severity:** Low
- **Input:** Addendum L260 ("original outage time, actual response time and full duration"); PRD FR-9 L228 (drill "records recovered-data age, full elapsed time, coverage and every substituted dependency").
- **Spine:** Detection and response L278; DR evidence L279.
- **Proposed fix:** L278: add "actual acknowledgement/response time". L279: "each drill records recovered-data age, coverage status, full elapsed time and every substitution."
- **Disposition:** autofix.

### RI-16 — FR-4 quiet rules missing
- **Severity:** Low
- **Input:**
  - PRD FR-4 L122: "a required service without usable readiness evidence is a configuration error"
  - PRD FR-4 L123: "justified finite timeout"
  - PRD FR-4 L138: "an incomplete cleanup is not recorded as complete"
  - SM-C4 L327
- **Spine:** Local tool L225; AD-10 L159. Also, the spine's "the complete environment uses the largest declared override" is an extension, not something the PRD states.
- **Proposed fix:**
  - L225: "a required resource without a usable readiness declaration fails configuration; an override carries its justification; an inherited largest override is reported as an override (SM-C5)."
  - L159: "an incomplete cleanup is never recorded as complete."
- **Disposition:** autofix.

### RI-17 — Gate and evidence wording drops
- **Severity:** Low
- **Input:**
  - Addendum L182: G2 requires "Memories tombstone **and key** continuity"
  - Addendum L171: staging evidence must identify the "production baseline"
  - PRD owned table L342: "retain evidence of actual configuration and versions" before G1
- **Spine:** G2 L275; Staging gate L266; Owned work G1 rows L455–457.
- **Proposed fix:**
  - G2: "Memories tombstone and key continuity".
  - Staging gate: "Results carry the serving release, production working baseline and the suite, profile and configuration digests".
  - G1 infrastructure row: "retain evidence of actual configuration and versions".
- **Disposition:** autofix.

### RI-18 — The AD-11 scope carve-out and retirement evidence could be tightened to the proposal's wording
- **Severity:** Low
- **Input:** Proposal §1 L14 (scope is "module presentation and operator tools"; operations keep "authorization, audit, response, and UX guarantees"); §2 L48 (risk of "changed structured output/exit codes"); edit 4 L68 (audit parity).
- **Spine:** AD-11 *Scope* L169 (excludes "recovery-hook tooling"); *Legacy surfaces* L170.
- **Gap:** Recovery hooks administer module state. The carve-out holds only while they are not operator-facing, and retirement evidence omits audit and output parity.
- **Proposed fix:**
  - L169: "recovery hooks are non-interactive contract executables invoked only by the recovery executor, never distributed or invoked as user- or agent-facing CLI/MCP".
  - L170: add "audit and structured-output/exit-code parity" to the retirement evidence.
- **Disposition:** autofix.

### RI-19 — When deputy coverage counts is not stated
- **Severity:** Low
- **Input:** PRD L34 (deputy acts "when Administrator is unavailable"); addendum L225 ("prove independent deputy access before relying on deputy response coverage"), L267 ("Coverage hours, deputy access and acknowledgement bounds must be named before the first qualifying drill").
- **Spine:** Roles L34; Detection and response L278; G2 owned work L458.
- **Proposed fix:** L278: "Deputy hours count toward response coverage only after the G2 deputy proof. Coverage, deputy access and acknowledgement bounds are published before the first qualifying drill." Also decide whether the deputy acts only when Administrator is unavailable. The recommendation is no restriction, with the choice recorded.
- **Disposition:** discuss.

### RI-20 — The addendum's AppHost-form check is not carried into tool ratification
- **Severity:** Low
- **Input:** Addendum L151 ("Architecture must check the actual AppHost form and installed version when implementing the lifecycle"; the testing builder is restricted with file-based AppHosts).
- **Spine:** Owned work "First tool publication" L439 (Builds.Module.AppHost launches the Platform model).
- **Proposed fix:** L439: "confirm the Platform AppHost form (project vs file-based) and Aspire testing-builder compatibility with retained-failure lifecycle."
- **Disposition:** defer (implementation qualification).
