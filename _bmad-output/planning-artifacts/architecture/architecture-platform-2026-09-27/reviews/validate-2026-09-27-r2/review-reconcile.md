# Review — input reconciliation and decision-log drift (validate r2)

Date: 2026-09-27. Reviewer lens: input reconciliation and memlog drift. Read-only; this file is the only file written.

**Verdict: FAIL.** The spine can't stay `final` as written. Since finalization it has had an unrecorded AD-11 amendment. It contradicts the final PRD on named recipients, deputy authority, pre-G2 admission and qualification timing, and it silently drops an operative memlog rule. Everything can be fixed in one update pass. Most items are wording autofixes. Two need discussion: RC-2, the deputy access mechanism, and RC-6, the scope of the Platform .NET tool.

Counts: 0 critical, 6 high, 8 medium, 5 low (19 findings).

Inputs read:

- Spine (working tree) and `git diff` of the spine.
- Memlog, grepped and not read whole: last entry L208 `(event) spine finalized`, frontmatter `updated: 2026-09-27T19:34`.
- PRD and addendum (working tree, both `status: final`), their diffs, and the PRD `.memlog.md` diff.
- The PRD update folder: `update-summary.md`, `memlog-audit.md` and `reconcile-architecture.md`.
- `sprint-change-proposal-2026-09-27.md`.
- McpCli spine and PRD under `references/Hexalith.McpCli`. The submodule working tree carries uncommitted edits.
- `specs/spec-platform/SPEC.md`, for the downstream check only.

## A. Memlog ↔ spine drift

The memlog has no entry after L208. Every spine change listed below postdates the finalization, and none of them has a memlog decision. The spine file's mtime is 22:35; the memlog was last updated at 19:34. No memlog decision recorded after the last re-distill (L197) was dropped: L200–L203 all landed. Two older V-27 clauses were dropped (A-14).

| # | Spine location | Spine rule (current) | Memlog position | Drift | Which side should change |
| --- | --- | --- | --- | --- | --- |
| A-1 | AD-11 L117 | `Hexalith.McpCli` naming | L126 "one generic versioned McpCli tool" | Cosmetic | Neither; mention it in the memlog amendment event |
| A-2 | AD-11 L117 | All proprietary module and technical-module MCP hosts, plug-ins and CLIs, including EventStore Admin, are obsolete migration sources | None. Its authority is the approved proposal (L80, L86, approval L125/L133) | Unsupported rule | **Memlog**: append a `(decision)` citing the Administrator-approved proposal |
| A-3 | AD-11 L117 | "Platform admits no new alternate proprietary MCP/CLI surface" | **Contradicts L182** (V-48): "…until a later AD admits each as a declared surface with its own client under AD-14" | Contradiction | **Memlog**: supersede L182's "until a later AD admits" clause. **Spine**: scope the rule (RC-6); as written it also catches the memlog-backed Platform .NET tool (L148, L158, L180) |
| A-4 | AD-11 L117 | Retire only after owner-approved inventory and replacement/withdrawal evidence | Partial: L158 requires "exact-subject consumer-removal authorization … before legacy retirement" | Unsupported, and narrower than both L158 and the PRD | **Both**: record the gate in the memlog; the spine adds authorization, compatibility, acceptance and AD-22 (RC-7) |
| A-5 | AD-11 L117 | A generic McpCli contract and transport decision is required before non-gateway/infra-admin cutover | None. L127 is consistent ("no remote module plugin loading or module-specific code") | Unsupported | **Memlog** append; **spine** names the form of the decision (RC-9) |
| A-6 | AD-11 (removed text) | The old sentence "no module, FrontComposer or technical-module MCP host, including EventStore.Admin.Mcp, is deployed or routed in Platform compositions" was deleted | **L182 still binding**: "neither deployed nor routed in Platform local, CI, staging or production compositions" | **Dropped rule** | **Spine**: restore the deploy/route prohibition (RC-5). The memlog supersedes only the "later AD" clause |
| A-7 | Source Precedence L193 | McpCli link moved to `references/Hexalith.McpCli/...` | L117/L120 record sibling `../mcpcli` as canonical; L127 lists "canonical McpCli root enrollment" as pending | Memlog stale; the spine matches repo reality (staged submodule) | **Memlog**: append an `(event)` recording the root-declared submodule and superseding the sibling-canonical note |
| A-8 | Source Precedence L193 | "Canonical target for all Hexalith-owned CLI/MCP access … first increment" | None | Unsupported; over-broad (RC-6) | **Memlog** append; **spine** narrows to module-operation access |
| A-9 | Deferred L324 | Row rewritten: "Legacy MCP/CLI retirement … Outside the first increment; no new proprietary module MCP/CLI host is admitted…" | L182 (additional surfaces need an AD under AD-14); L181 (step-up "not executable through McpCli in the MVP") | Dropped the general AD-14 admission gate for additional MCP surfaces and transports; "MVP" replaced by the undefined "first increment" for unrelated items | **Spine** (RC-8) |
| A-10 | Diagnostics L159, Automatic recovery L175, Detection L179 | Notifications go to Administrator only | L159, L191, L201: Administrator only | The memlog and spine agree with each other but are stale against the final PRD | **Memlog** first: record a decision adopting the PRD deputy recipients. Then the **spine** (RC-1) |
| A-11 | AD-6 L87 | "production realm changes are Administrator-only" | L184 (V-26) | Stale against the PRD's deputy recovery authority | **Memlog** decision (carve-out), then **spine** (RC-2) |
| A-12 | AD-7 L93 | Operations repository writable only by Administrator; executor allowlists | L200 (RV-1) | Leaves no deputy recovery path | **Memlog** decision (discuss), then **spine** (RC-2) |
| A-13 | G1 L176 | "production admission group is empty" | L191 (V-21/V-34) | Memlog lacks the PRD's pre-G2 synthetic admission step | **Both** (RC-3) |
| A-14 | AD-11 L117 | "tokens sent only to their issuing environment's gateway" | **L184 (V-27)** also says "URL and token never mix sources" and "refresh material in the OS credential store" | Two memlog clauses dropped at the earlier distill. They now matter because the McpCli spine contradicts them | **Spine**: restore them (RC-11) |
| A-15 | Frontmatter L8/L10 | `status: final`, `updated: 2026-09-27` | L208 `spine finalized`, recorded before the edit | Process drift: a finalized spine was amended without an event or gate | **Memlog**: append an amendment event. **Spine**: status per RC-14 |

## B. PRD ↔ spine coverage (working-tree PRD and addendum, both final)

| Requirement / consequence | Spine location | Status |
| --- | --- | --- |
| FR-1 complete local environment; McpCli as a local client, no hosted McpCli service | AD-1, AD-11 L117, AD-13 | Landed |
| FR-2 run "only the components specified by the module configuration" (prd.md L84) | Design Paradigm L27 ("its declared dependencies"), AD-4 L75, Module declaration L147 ("enabled server list") | Partial: wording ambiguous (RC-18) |
| FR-2 direct-root source/Debug, CI Release/NuGet, no fallback | AD-4 L75 | Landed |
| FR-3 developer-owned server list; technical-module minimum compositions | Module declaration L147, L27 | Landed |
| FR-4 readiness, 10-minute deadline, override, diagnostics | AD-10 L111, Local tool L156 | Landed |
| FR-4 fresh run-owned environment, attach rules, first-terminal-outcome retention, idempotent cleanup | AD-10 L111 | Landed |
| FR-5 isolated tests first, without Platform | AD-5 L81 | Landed |
| FR-6 E2E gate in both modes; exact-release evidence; evidence age | Staging gate L168, Release modes L169 | Landed |
| FR-6 approval names the recovery procedure **and its acceptance checks before the attempt** (prd.md L168; L48) | Release modes L169 | Partial (RC-15) |
| Downstream: staging trigger/rerun evidence policy, Platform with Builds (prd.md L338; addendum L173) | — | Missing (RC-13) |
| Downstream: review policy for removing or remapping critical-flow checks (prd.md L337) | — | Missing (RC-13) |
| FR-7 thresholds, healthy-baseline precondition, interruption | L172–L174, L171 | Landed |
| FR-8 one recovery, budgets, no cycling, first deployment | Automatic recovery L175 | Landed |
| FR-8 report "through GitHub to Administrator **and the named deputy**" (prd.md L203) | L159, L175 | **Contradicted** (RC-1) |
| FR-8 deputy may recover, verify and reopen; cannot clear the stop or administer users (prd.md L204, L34) | L175 (stop: Administrator-only, landed); L180 step 6 (reopen actor unstated); AD-6 L87; AD-7 L93 | Partial / contradicted (RC-2) |
| FR-8 "Delivery and the deputy's independent recovery access are demonstrated before G2" (prd.md L203) | Deferred L314 ("actual GitHub issue delivery", before G3), L316 | **Contradicted** on timing (RC-4) |
| FR-8 a post-window incident establishing non-working production sets or retains the stop (prd.md L208) | — | Missing (RC-13) |
| FR-9 cadence, retention, recovery-point definition, off-site immutability | L177, L178 | Landed |
| FR-9 artifacts, access and decryption independently available to "Administrator and the deputy" (prd.md L224) | AD-12 L123, Secrets L153 (unseal custodians only) | Partial (RC-2) |
| FR-9 monitoring "notifies Administrator and the deputy through GitHub" (prd.md L225; addendum L251) | L178, L179, L159 | **Contradicted** (RC-1) |
| FR-9 fence, quarantine, rotate, re-apply revocations, verify, re-enable protection | DR sequence L180 | Landed |
| FR-9 Memories tombstone continuity | L182 | Landed |
| FR-9 drill cadence, isolation, substitutions | L181 | Landed |
| FR-10 isolated environments, hostnames, surfaces | AD-8 L99, Hosted interfaces L157 | Landed |
| FR-11 Administrator alone grants admission; recovery authority grants no user administration | AD-6 L87 | Landed. AD-6's blanket realm rule over-reaches (RC-2) |
| FR-12 eligibility, refusal, digest matching, environment binding, server-side surface and actor | AD-11 L117, AD-14 L135 | Landed |
| FR-12 "its CLI head is the Hexalith-owned command-line surface" (prd.md L278) | AD-11 L117 (implied) | Partial; clashes with the Platform .NET tool (RC-6) |
| FR-12 legacy retirement after "owner-approved operation inventory, replacement or withdrawal decision, authorization, compatibility, and acceptance evidence" (prd.md L278) | AD-11 L117 (inventory, replacement/withdrawal only); Deferred L324 (+ AD-14 authorization) | Partial (RC-7) |
| FR-12 "Platform does not add or publish another proprietary Hexalith MCP/CLI surface" | AD-11 L117 | Landed, but unscoped (RC-6) |
| NFR-1 rollback data safety | AD-3 L69, AD-15 L141 | Landed |
| NFR-2 RPO ≤1 h, RTO ≤4 h measured outage-to-verified | DR evidence L181 | Landed |
| NFR-2 coverage semantics: in-coverage start stays covered after coverage ends; clock never pauses; every incident records full duration and coverage status; time zone, primary/deputy responsibility, acknowledgement delay (prd.md L297–L301; addendum L260) | Detection and response L179 | Partial (RC-10) |
| NFR-2 whole-site loss only with independent capacity | L181 | Landed |
| NFR-3 isolation incl. automation, restored copies | AD-6/AD-8/AD-14 | Landed |
| G1: closed ingress, empty group, monitoring **and GitHub notifications operate** (prd.md L52) | L176; Deferred L317 (monitor before G1), L314 (delivery before G3) | Partial (RC-4) |
| Pre-G2 temporary synthetic admission for SM-4, revoked afterwards (prd.md L56; addendum L185 "Architecture must carry this…") | L176 ("admission group is empty"), Synthetic identities L158 | **Missing, and the G1 wording contradicts it** (RC-3) |
| G2: isolation/access checks pass; recovery access, capacity, notifications and response verified (prd.md L53) | L176 (drill only), Deferred L316/L319 | Partial (RC-4) |
| G3 after SM-5 rehearsals | L176 | Landed |
| Addendum L346: release/attempt controls "before the first applicable production attempt, including an approved pre-G3 attempt" | Deferred L314 "Before G3" | **Contradicted** (RC-4) |
| Addendum L218/L323: deputy restores already-authorized access as recovery; no admission delegation | AD-6 L87 ("production realm changes are Administrator-only") | **Contradicted** (RC-2) |
| Addendum L225/L326: deputy access must keep the single-writer boundary or trigger the GitHub Team review | AD-7 L93 (review trigger only) | Partial (RC-2) |
| Addendum L22: legacy hosts/CLIs not deployed or routed as supported surfaces; EventStore Admin and non-gateway capabilities need a generic extension decision | AD-11 L117 | Partial: the deploy/route clause was dropped (RC-5) |
| Addendum L173: retention for backup period or rollback-target life | AD-2 L63 | Landed |
| SM-5 / SM-6 deputy access and delivery to Administrator and the deputy (prd.md L319–L320) | Deferred L314 | Partial (RC-1/RC-4) |

Quiet requirements that the AD structure dropped:

- the deputy as a named notification recipient;
- the deputy as the one who reopens service;
- "acceptance checks before the attempt";
- the post-window incident stop;
- the RTO coverage clock semantics;
- the temporary synthetic admission;
- the "first applicable attempt" timing.

## C. Sprint change proposal ↔ spine

- **Platform-owned correction, proposal L30 and item 9 (L80).** Landed:
  - the replacement policy;
  - the canonical link, which resolves to the staged submodule;
  - the no-new-surface rule.

  Not landed:
  - "Legacy hosts and CLIs are not enrolled into new Platform compositions";
  - "temporary compatibility use requires a named migration record and removal gate" (RC-5).
- **Platform role, proposal §5 L104 and item 14 L92.** The proposal makes Platform own "release composition, candidate evidence, and no-new-surface policy" and removal of "each old package and Platform route". The rewritten Deferred row (L324) names no Platform owner (RC-8).
- **Other owners' seams the spine should name as owned work** (proposal L31, L34, L38, L46, items 10 and 12). The spine names only "generic administration and resource contracts". It doesn't name:
  - the FrontComposer resource and skill corpus, and the `inspect`/`migrate` commands;
  - REST/Dapr-backed legacy operations;
  - EventStore destructive/admin operations, which retain their confirmation gates;
  - the ChatBot and Projects machine-confirmation conflicts with AD-14;
  - the migration inventory as a shared versioned artifact.

  There is also no first-shared-versions row gating retirement (RC-9).
- **AD-11 against the rest of the spine.**
  - AD-14: consistent in intent ("AD-14's agent-surface denials remain binding"). However, AD-14 is written for gateways and `azp`, and a non-gateway transport has no rule for how it derives the surface (RC-9).
  - Source Precedence McpCli row: consistent, but over-broad (RC-6). It also relies on uncommitted McpCli edits (RC-14).
  - Capability map FR-12 (L277): doesn't reflect the retirement policy (RC-17).
  - Deferred (L324): lost the MVP scope and the AD-14 gate for additional transports (RC-8).
  - Migration and coexistence (L160): not updated for MCP/CLI retirement (RC-7).

## D. McpCli spine/PRD ↔ Platform spine

| McpCli source (submodule working tree) | Conflict with Platform | Explicitly overridden? |
| --- | --- | --- |
| Spine AD-13 L136: each setting (URL, token, actor) resolves independently from flag, environment or profile; `EVENTSTORE_ACTOR` | Memlog L184: "URL and token never mix sources"; the actor is the token subject | Partially. AD-11 refuses `--actor` only, and the source-mixing clause was dropped (RC-11) |
| Spine AD-14 L142: plaintext `token` in `~/.eventstore/mcpcli.json`; AD-10 L118 `StaticBearerTokenHandler` | Memlog L184: refresh material in the OS credential store | Partially. The Source Precedence row covers "static profile bearers"; the storage clause is absent (RC-11) |
| Spine AD-10 L118 and PRD L85, L366, L504: the HTTP release takes the actor from a forwarded user header, with a forwarding handler "in the Parties.Mcp pattern" | AD-14 L135: surface and actor come from the authenticated token, never caller headers; raw token forwarding is forbidden | **No.** The Deferred rewrite removed the AD-14 admission gate for additional transports (RC-8/RC-11) |
| Spine AD-16 L150, L190, L251: own `tests/Hexalith.McpCli.AppHost` | AD-10 L111: McpCli tests never start their own AppHost | Explicit in AD-10, but no owned amendment row (RC-12) |
| Spine AD-17 L156, L272: semantic-release pushes `Hexalith.McpCli` to nuget.org from green `main` | AD-11: McpCli is "published only after staging validation" from Platform's intake Contracts | **No.** Two publishers of one package identity (RC-12) |
| McpCli `.gitmodules` declares `references/Hexalith.Platform` | Deferred L308 removes the "unused nested Platform reference"; AD-4 ties the CI tool to the submodule HEAD; SPEC L116 asks architecture how McpCli CI gets a runner version | No (RC-12) |
| Committed McpCli PRD §2.2/§8.3: EventStore Admin excluded (the working tree removes this, uncommitted) | AD-11: EventStore Admin is a migration source | Only by an uncommitted upstream edit (RC-14) |
| Spine L33 (working tree): gateway-only first increment; a generic extension decision is needed before non-gateway retirement | AD-11: consistent | Consistent. The form of the decision (McpCli-only or a Platform AD) is unclear (RC-9) |
| PRD L42: first-increment business administration "even when it requires a platform-administrator token" | AD-14: public McpCli client is agent-capable least privilege | Not contradictory, since the gateway authorizes per call. Noted for RC-9's authorization class |

## Findings

### RC-1: Notifications exclude the named recovery deputy

- **Severity:** high. **Disposition:** autofix, after a memlog decision entry.
- **Location:** spine Diagnostics L159, Automatic recovery L175, Detection and response L179. Against prd.md L203, L225, L319–L320, L346; addendum L214, L251.
- **Finding:** The spine says "GitHub issues assigned to Administrator are the single accepted notification path for every notification in this spine". The final PRD requires GitHub delivery to Administrator **and the named deputy** for deployment failures, recovery results, backup failure and freshness. Memlog L159, L191 and L201 still record Administrator only.
- **Consequence:** Implementers build Administrator-only issue routing. Deputy response coverage, and therefore the NFR-2 coverage and G2 evidence, can't be met, and two binding sources conflict.
- **Fix:**
  - L159: "GitHub issues assigned to Administrator and the named recovery deputy are the single accepted notification path for every notification in this spine."
  - L175: "…including success, to Administrator and the recovery deputy."
  - L179: "…and notifies Administrator and the recovery deputy."
  - Memlog: append a `(decision)` adopting the PRD deputy recipients and superseding the recipient clauses of L159, L191 and L201.

### RC-2: Deputy operational access and authority not implemented, and conflicting controls remain

- **Severity:** high. **Disposition:** discuss (mechanism), then autofix the wording.
- **Location:**
  - Roles L29;
  - AD-6 L87 ("production realm changes are Administrator-only");
  - AD-7 L93 (ops repo writable only by Administrator; executors refuse jobs not on their allowlist);
  - Secrets L153 ("Administrator holds the Memories operator role");
  - Synthetic identities L158;
  - DR sequence L180 step 6 (who reopens is unstated);
  - Deferred L316 and L319.

  Against prd.md L34, L204, L224, L346; addendum L218, L225, L323, L326, L342.
- **Finding:** The PRD selected Option 1. The deputy receives alerts and holds independent recovery and key access. The deputy may execute recovery, verify and reopen, but only Administrator clears the promotion stop or administers admission. The PRD also says this "does not automatically grant operations-repository write access". Four spine controls block that model:
  - Under AD-7 the deputy has no path to trigger recovery workflows: on GitHub, dispatching a workflow needs write access.
  - AD-6 forbids the realm changes that DR steps 2 and 4 need: Keycloak restore, re-applying revocations, and realm-key rotation on compromise.
  - The Memories operator role, needed for recovery hooks, is Administrator-only.
  - DR step 6 doesn't say who may reopen.
- **Consequence:** A deputy-run recovery either violates the spine or can't happen. The PRD handoff row (prd.md L346) is unmet, and G2 deputy evidence can't be produced.
- **Fix:**
  - Decide the trigger path. Either (a) the deputy gets write access, which triggers AD-7's GitHub Team adoption, or (b) **recommended:** the off-site recovery executor accepts an allowlisted recovery run started under the deputy's own identity, with credentials held in off-site custody, and no ops-repo write access.
  - AD-6 wording: "…production realm changes are Administrator-only, except that a recovery run by Administrator or the recovery deputy restores the realm and re-applies recorded revocations and key rotations; neither recovery role changes production admission."
  - Roles L29: "…a named recovery deputy receives recovery alerts, holds independent recovery and key access, and may execute the documented recovery, verify restoration and reopen service; only Administrator clears the promotion stop or administers production admission."
  - DR step 6: "…reopen (Administrator or the recovery deputy, after step 5 passes; the promotion stop stays set)."
  - Extend the Memories operator role to recovery runs.
  - Deferred L319: "prove the deputy's own identity, minimum permissions, key custody, alert delivery and rehearsed restoration and reopening before G2".

### RC-3: Pre-G2 synthetic admission step missing, and the G1 wording forbids it

- **Severity:** high. **Disposition:** autofix, plus a memlog entry.
- **Location:** Production entry gates L176, Synthetic identities L158. Against prd.md L56, L318; addendum L185; PRD `update-summary.md` "Downstream alignment"; `reconcile-architecture.md` L81.
- **Finding:** The spine says only that "the production admission group is empty" at G1. The PRD lets Administrator temporarily admit one designated synthetic identity between G1 and G2 for SM-4 positive evidence, and explicitly asks architecture to carry the step.
- **Consequence:** Either SM-4 positive production evidence can't be produced before G2, or implementers breach the G1 rule.
- **Fix:** Add to G1: "After G1 deployment and before G2, Administrator may temporarily add one designated synthetic test identity to the production admission group solely for SM-4 positive evidence; ingress stays executor/probe-only, no general user is admitted, the grant is revoked after the check, and this does not open G2."

### RC-4: Qualification timing is later than the PRD

- **Severity:** high. **Disposition:** autofix.
- **Location:** Deferred "Release state, checks and notifications" L314 ("Before G3"), Exposure L317, Recovery capacity L319, G1/G2 L176. Against addendum L346; prd.md L52–L53, L203, L343.
- **Finding:** The PRD requires three things earlier than the spine does:
  - release/attempt controls (lock and epoch, provenance, one recovery, interruption and concurrency, delivery) before the first applicable production attempt, **including an approved pre-G3 attempt**;
  - GitHub notifications operating at G1;
  - deputy delivery and access before G2.

  The spine defers all of these to G3. This also contradicts the spine's own Release modes rule (L169) that lock, provenance and records apply to both modes. G2 also omits "isolation and access checks pass" and "recovery access, capacity, notifications and response arrangements verified".
- **Consequence:** Approved pre-G3 production attempts run on unqualified lock and recovery controls, and G1/G2 open without proven alerting.
- **Fix:**
  - L314: replace "Before G3:" with "Before the first applicable production attempt, including an Administrator-approved pre-G3 attempt:". Append "SM-5 rehearsals additionally gate G3."
  - Move "actual GitHub issue delivery" to L317 (before G1).
  - Add "delivery to and independent access by the recovery deputy" to L319 (before G2).
  - G2: "…only after isolation and access checks pass, recovery access, capacity, notifications and response arrangements are verified, and the AD-12 drill passes…"

### RC-5: AD-11 amendment unrecorded; the memlog's deploy/route prohibition dropped; proposal item 9 only partly landed

- **Severity:** high. **Disposition:** autofix, with a memlog decision entry citing the approved proposal.
- **Location:** AD-11 L117 (and `git diff` of the spine). Against memlog L182; proposal L80 (item 9), L125/L133 (approval); addendum L22.
- **Finding:** The amendment replaced "no module, FrontComposer or technical-module MCP host, including EventStore.Admin.Mcp, is deployed or routed in Platform compositions until an AD admits it under AD-14". Nothing restates that obsolete hosts are not deployed or routed. The proposal's "Legacy hosts and CLIs are not enrolled into new Platform compositions; temporary compatibility use requires a named migration record and removal gate" is absent. Memlog L182, still the authority, forbids deployment and routing. The only thing that changed is that a later AD may no longer admit them. No memlog entry records the directive.
- **Consequence:** "Obsolete migration source" without a composition prohibition reads as permission to keep legacy hosted MCP (Memories.Mcp, Parties.Mcp) and EventStore Admin routes in Platform compositions during migration. The spine's decision authority is also broken.
- **Fix:**
  - Append to AD-11: "Legacy MCP hosts and CLIs are not enrolled, deployed or routed in Platform local, CI, staging or production compositions; any temporary compatibility use requires a named migration record and removal gate."
  - Memlog: `(decision)` "Administrator-approved sprint change proposal 2026-09-27: McpCli is the sole target for Hexalith-owned module-operation CLI/MCP access; supersedes L182's 'until a later AD admits each' clause; deploy/route prohibition retained."

### RC-6: The no-new-proprietary-CLI rule collides with the Platform .NET tool

- **Severity:** high. **Disposition:** discuss.
- **Location:** AD-11 L117 ("Platform admits no new alternate proprietary MCP/CLI surface"), Source Precedence L193 ("Canonical target for all Hexalith-owned CLI/MCP access"). Against AD-4 L75, Local tool convention L156, memlog L148, L158, L180 (Projects AD-25 pinned Platform tool); prd.md L278 ("Platform does not add or publish another proprietary Hexalith MCP/CLI surface"); proposal L14 (scope: "module presentation and operator tools"); McpCli PRD L434 ("per-module").
- **Finding:** Platform itself publishes a Hexalith-owned CLI: the pinned Platform .NET tool with run, teardown and debug commands, which Projects consumes. As worded, the new rule and the McpCli row cover it.
- **Consequence:** Implementers may either fold Platform orchestration into McpCli, which is a large unplanned redesign, or treat the Platform tool as non-compliant.
- **Fix:** Add to AD-11: "This rule governs module-operation access surfaces; the Platform .NET tool (AD-4) and build, deployment and recovery tooling that expose no module commands or queries are outside it." Change L193 to "Canonical target for all Hexalith-owned module-operation CLI/MCP access." Record the scope in the memlog. Alternative: Administrator decides that Platform commands move into McpCli, and records it.

### RC-7: Retirement gate is narrower than FR-12 and not aligned with the migration convention

- **Severity:** medium. **Disposition:** autofix.
- **Location:** AD-11 L117, Deferred L324, Migration and coexistence L160. Against prd.md L278; memlog L158; proposal L92, L104.
- **Finding:** FR-12 retires a legacy surface only after an "owner-approved operation inventory, replacement or withdrawal decision, authorization, compatibility, and acceptance evidence". AD-11 names only inventory and replacement/withdrawal evidence. L324 adds only AD-14 authorization. L160 still governs only AppHost retirement, with AD-22 consumer-removal authority. Nobody owns removing legacy package and route declarations from Platform compositions.
- **Consequence:** Legacy CLIs or MCP hosts can be retired without compatibility or acceptance evidence, and EventStore AD-22 authority may be skipped for Admin packages.
- **Fix:**
  - AD-11: "Retire each source only after its owner-approved operation inventory, a McpCli replacement or approved withdrawal decision, and AD-14 authorization, compatibility and acceptance evidence, plus any applicable EventStore AD-22 consumer-removal authority."
  - L160: append "Legacy MCP/CLI packages and routes retire under AD-11; Platform removes their composition and route declarations."

### RC-8: The Deferred row rewrite loses the MVP scope and the AD-14 gate for additional MCP transports

- **Severity:** medium. **Disposition:** autofix.
- **Location:** Deferred L324. Against memlog L181, L182; prd.md L60–L61 (non-goals); McpCli spine AD-10 L118, PRD L504 (HTTP release with forwarded user header); proposal L104.
- **Finding:**
  - "Outside the first increment", a McpCli term the spine never defines, now scopes generic mocks, traffic-metric rollback and step-up flows. Those are Platform **MVP** non-goals.
  - The old "Additional MCP surfaces … need an AD admitting it under AD-14" gate is gone. Nothing now gates McpCli's own planned HTTP transport, whose forwarded-header actor model contradicts AD-14.
  - Platform isn't named as an owner.
- **Consequence:** Scope ambiguity for unrelated deferrals, and an open door for a hosted HTTP MCP surface outside AD-14.
- **Fix:** Split the row:
  1. "Legacy MCP/CLI migration and retirement; generic administration and resource contracts | McpCli, EventStore, FrontComposer and module owners, with Platform for composition and route removal | Per AD-11; no new proprietary module MCP/CLI host is admitted."
  2. "Additional MCP transports including McpCli HTTP, step-up flows, generic mocks, traffic-metric rollback | Owning capability or product team | Outside the MVP; an additional MCP surface or transport needs an AD admitting it as a declared surface with its own client under AD-14; forwarded headers never establish the actor."

### RC-9: The form and owner of the non-gateway/admin cutover decision are unclear; other owners' seams are unnamed

- **Severity:** medium. **Disposition:** discuss.
- **Location:** AD-11 L117 ("A generic McpCli contract and transport decision is required…"), AD-14 L135, First shared versions L290–L301. Against proposal L31, L34, L38, L46, L82, L86; McpCli spine L33.
- **Finding:** AD-14 derives the surface at gateways from `azp`. A non-gateway transport, such as an EventStore Admin API, REST/Dapr-only operations or FrontComposer resources, needs:
  - a surface class and client in the realm contract;
  - an ingress exposure rule (the Hosted interfaces convention says external callers reach pods only via the gateway).

  None of that is a McpCli-only decision. Separately, destructive admin operations requiring confirmation remain denied through McpCli under AD-14, so they must move to a confidential UI or be withdrawn. Neither point is stated. No shared artifact (the migration inventory schema, or the generic admin/resource contract) gates retirement.
- **Consequence:** McpCli Story 5.2 could settle a transport without a Platform AD. Admin operations could be exposed through ingress outside AD-14, or lost with no disposition.
- **Fix:**
  - AD-11: "…cut over; the decision is recorded as a Platform AD amending AD-11/AD-14 (surface class, Keycloak client, gateway or ingress exposure, realm-contract entries). Confirmation-required administrative operations move to a confidential UI surface or are withdrawn."
  - Add a first-shared-versions row: "McpCli migration inventory schema and generic administration/resource contract | McpCli with EventStore | FrontComposer, module owners | Any legacy MCP/CLI retirement".

### RC-10: NFR-2 response-coverage semantics only partly landed

- **Severity:** medium. **Disposition:** autofix.
- **Location:** Detection and response L179, DR evidence L181. Against prd.md L297–L301; addendum L258–L267; `update-summary.md` product choice 2.
- **Finding:** The spine says only "the RTO is assessed within them". Four things are missing:
  - an outage beginning within coverage stays covered when coverage ends;
  - the clock never pauses or restarts;
  - every incident records its full duration and coverage status;
  - coverage publishes a time zone, primary/deputy responsibility and a maximum acknowledgement delay.
- **Consequence:** Divergent RTO measurement between the drill, the reports and the PRD. This was an explicitly handed-off definition.
- **Fix:** L179: "The recovery procedure publishes response coverage with its time zone, primary and deputy responsibility and maximum acknowledgement time. The four-hour RTO applies to outages beginning within coverage and stays applicable when coverage ends mid-incident; outside coverage no four-hour commitment is made. Every incident records original outage time, coverage status and full outage-to-verified-restoration duration; the clock never pauses or restarts."

### RC-11: McpCli settings and profile contradictions not explicitly overridden; V-27 clauses dropped

- **Severity:** medium. **Disposition:** autofix.
- **Location:** AD-11 L117, Source Precedence L193. Against memlog L184 (V-27); McpCli spine AD-13 L136, AD-14 L142, AD-10 L118; McpCli PRD L85, L314, L366.
- **Finding:** McpCli resolves URL, token and actor independently from flag, environment or profile, stores a plaintext token, and plans a forwarded-header actor. The spine only refuses `--actor` and forbids "static profile bearers". It dropped "URL and token never mix sources" and "refresh material in the OS credential store".
- **Consequence:** A flag URL combined with a profile token can send a token to the wrong gateway. Refresh tokens can be persisted in plaintext.
- **Fix:**
  - AD-11: "A profile's gateway, issuer, audience and token come from one source and never mix; refresh material lives in the OS credential store; hosted McpCli derives the actor from the token and refuses actor values from flags, environment or profiles."
  - L193: add "overrides McpCli AD-10's forwarded-header actor, AD-13's per-setting source mixing and AD-14's plaintext profile token for hosted use."

### RC-12: McpCli release, test-harness and Platform-identity seams unresolved

- **Severity:** medium. **Disposition:** discuss.
- **Location:** AD-11 L117, AD-10 L111, AD-4 L75, Deferred L308, L313. Against McpCli spine AD-16 L150, L251, AD-17 L156, L272; McpCli `.gitmodules` (`references/Hexalith.Platform`); `SPEC.md` L116.
- **Finding:**
  - McpCli publishes `Hexalith.McpCli` to nuget.org on green `main`. Platform says the McpCli candidate is "published only after staging validation". Nothing says which publisher is of record.
  - McpCli's harness starts its own AppHost, which AD-10 forbids, and no amendment is owned.
  - Removing McpCli's nested Platform reference leaves the AD-4 CI-tool identity check without an anchor. The SPEC asks architecture about this directly.
- **Consequence:** Two release channels for one tool, a harness that violates AD-10, and an unanswered architecture question in the SPEC.
- **Fix:** L313: "…reconcile McpCli AD-17 publication with Platform's staging-gated McpCli publication (one publisher of record per version), AD-16's own AppHost with AD-10, and McpCli's Platform-runner identity once its nested Platform reference is removed (AD-4)." Answer SPEC L116 in the spine.

### RC-13: Three PRD obligations have no spine home

- **Severity:** medium. **Disposition:** autofix.
- **Location:** Automatic recovery L175, Deferred L314. Against prd.md L208, L337, L338; addendum L173.
- **Finding:** The spine carries none of these:
  - a post-window incident that establishes non-working production sets or retains the promotion stop;
  - the staging trigger and rerun-evidence policy (Platform with Builds);
  - the review policy for removing or remapping critical-flow checks (module owners with Platform).
- **Consequence:** Promotion can resume after a post-window outage. Reruns can hide failed attempts. Coverage can shrink behind matching digests.
- **Fix:**
  - L175: "An incident after the verification window that establishes production is not working sets or retains the promotion stop until Administrator records a verified working baseline; it never triggers an automatic release search."
  - L314: add "staging triggers and rerun-evidence rules (Platform with Builds) before the first accepted staging evidence; review policy for removal or remapping of critical-flow checks (module owners with Platform) before release-gate qualification".

### RC-14: Frontmatter, sources and upstream traceability are stale

- **Severity:** medium. **Disposition:** autofix.
- **Location:** spine L8–L18, Source Precedence L193. Against memlog L208 and frontmatter `updated: 2026-09-27T19:34`; proposal; McpCli submodule working tree.
- **Finding:**
  - `status: final` is inaccurate. The spine was changed after `(event) spine finalized`, with no memlog decision or gate, and it contradicts the final PRD (RC-1 to RC-4).
  - `updated: '2026-09-27'` is still the right date.
  - `sources` omits `../../sprint-change-proposal-2026-09-27.md`, the only authority for the AD-11 amendment, and the McpCli spine and PRD.
  - The McpCli row cites a position ("canonical target… first increment", EventStore Admin in scope) that exists only in **uncommitted** submodule edits. The pinned McpCli commit's PRD §2.2/§8.3 still excludes EventStore Admin.
- **Consequence:** Readers take an ungated amendment as final, and clean checkouts resolve the link to contradicting upstream text.
- **Fix:**
  - Set `status: draft` until the memlog entries and this gate's fixes land, then re-finalize with an `(event)`.
  - Add these sources: `../../sprint-change-proposal-2026-09-27.md`, `../../../../references/Hexalith.McpCli/_bmad-output/planning-artifacts/architecture/architecture-mcpcli-2026-09-22/ARCHITECTURE-SPINE.md`, and `../../../../references/Hexalith.McpCli/_bmad-output/planning-artifacts/prds/prd-mcpcli-2026-09-21/prd.md`.
  - State in L193 that Platform AD-11 overrides the committed McpCli EventStore Admin exclusion until the upstream amendment merges.

### RC-15: Approved-mode record omits the recovery acceptance checks and "before the attempt"

- **Severity:** low. **Disposition:** autofix.
- **Location:** Release modes L169. Against prd.md L48, L168, L319.
- **Fix:** "…incompatible releases, whose record names, before the attempt, the separately planned recovery and its acceptance checks, which replace automatic rollback."

### RC-16: "including EventStore Admin" is ambiguous

- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-11 L117. Against proposal L38, L86 (only Admin CLI/MCP are obsolete; EventStore keeps administration semantics); addendum L14 (the Works preview composes "EventStore/admin/Operations").
- **Fix:** "…including the EventStore Admin CLI and MCP (`Hexalith.EventStore.Admin.Cli`, `.Admin.Mcp`)…"

### RC-17: AD-11 labels and cross-references not updated

- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-11 heading L113 (`[ADOPTED]`), Prevents line L116, Capability map L277.
- **Fix:**
  - Heading: `[ADOPTED, AMENDED]`.
  - Prevents: add "proprietary per-module MCP/CLI surfaces".
  - FR-12 map row: "…; legacy MCP/CLI retirement per AD-11".

### RC-18: FR-2 "module configuration" wording not mirrored

- **Severity:** low. **Disposition:** autofix.
- **Location:** Design Paradigm L27, AD-4 L75. Against prd.md L84.
- **Finding:** "its declared dependencies" can be read as `references/` declarations rather than the module configuration's server list.
- **Fix:** L27: "…EventStore, Tenants, Memories and the servers its module configuration (the declaration's enabled server list) specifies; source or package mode follows AD-4."

### RC-19: Downstream companions still carry the pre-change wording

- **Severity:** low. **Disposition:** defer to the spec/PRD owners after the spine update.
- **Location:** `specs/spec-platform/SPEC.md` L88 (notifications "only" to Administrator), L89 ("no module MCP host is deployed or routed"), L104, L116; addendum L20 (sibling `../mcpcli` still called canonical).
- **Finding:** The PRD downstream row (prd.md L346) makes "Platform architecture/spec owners" synchronize this wording, and the SPEC lists the spine as a companion.
- **Fix:** Re-sync the SPEC after RC-1 to RC-5 land. Update addendum L20 to the root-declared submodule.

## Frontmatter accuracy

| Field | Accurate? | Note |
| --- | --- | --- |
| `status: final` | **No** | Amended after `spine finalized` with no memlog decision or gate; contradicts the final PRD (RC-14) |
| `updated: '2026-09-27'` | Yes | Same day |
| `sources` | **Incomplete** | PRD, addendum, memlog and reviews paths resolve. Missing: the sprint change proposal and the McpCli spine/PRD |
| `binds` | Yes | FR-1–FR-12, NFR-1–NFR-3 |
