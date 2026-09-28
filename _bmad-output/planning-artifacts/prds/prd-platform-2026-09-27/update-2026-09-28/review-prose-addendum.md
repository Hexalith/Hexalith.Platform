# Addendum prose review (2026-09-28 finalize)

Lens: `bmad-review` `lenses=prose`, which ran on top of the structure pass in `review-structure-addendum.md`. The reviewer applied the fixes to `addendum.md`; the dispositions are listed below.

This document exists to help Platform, module, and operations owners and architects find the accepted architecture summaries, selected policies with their rejected alternatives, and the remaining qualification work behind `prd.md`.

- **Structure model** (from the structure pass): Strategic/Context (Pyramid).
- **Style guide:** Microsoft Writing Style Guide.
- **Reader:** humans.
- **Voice preserved:**
  - A dense, declarative policy register, with imperatives for selected behavior ("Use", "Keep", "Validate").
  - "Administrator" used as a role name without an article.
  - Slash compounds and noun stacks as technical shorthand ("source/Debug", "recovery/key").
  - Architecture decision IDs used inline (AD-n, FR-n, G1–G3, SM-n).
- **Constraints honored:**
  - No decision, threshold, number, actor, gate, accepted risk, or exception changed.
  - Every "awaits architecture adoption" statement is kept.
  - Frontmatter is unchanged.
  - Headings are unchanged, including those linked from `prd.md`.
  - Glossary terms were checked against `prd.md` §Glossary.
- **Skipped:** passages that the structure pass cut (S6–S8).

| # | Pass | Original Text | Revised Text | Changes | Disposition |
| --- | --- | --- | --- | --- | --- |
| P1 | prose | "Repository observations below are historical observations from brief creation on 2026-09-27, not a fresh runtime inspection." | "Repository observations below date from brief creation on 2026-09-27 and are not a fresh runtime inspection." | Removed the repeated "observations". | Applied |
| P2 | prose | "This update reconciles accepted internal decisions;" (paragraph moved into the intro by S5) | "This revision reconciles accepted internal decisions;" | The intro calls this version "This revision", so the moved paragraph now uses the same word. | Applied |
| P3 | prose | "…the sibling `../mcpcli` repository as McpCli's canonical location, absent from Platform's inspected root submodule declarations." | "…the sibling `../mcpcli` repository, which Platform's inspected root submodule declarations did not include, as McpCli's canonical location." | Fixed a dangling modifier: "absent from" attached to "location". | Applied |
| P4 | prose | "documented stdio MCP, deferred HTTP transport, and described an empty catalog" | "documented stdio MCP and deferred HTTP transport, and described an empty catalog" | Fixed faulty parallelism. | Applied |
| P5 | prose | "…with a named migration record and removal gate, and any new MCP/CLI surface or transport, including McpCli HTTP, needs an architecture decision. Each is retired after…" | "…with a named migration record and removal gate. Each obsolete source is retired after… Any new MCP/CLI surface or transport, including McpCli HTTP, needs an architecture decision." | Split two unrelated rules that "and" had joined. Moved the retirement sentence next to the rule about obsolete sources and gave "Each" a clear antecedent. ARCHITECTURE-SPINE line 175 says "Retire a source…", which confirms that reading. | Applied |
| P6 | prose | "; embedded Platform's own nested references remain uninitialized." | "; the embedded Platform's own nested references remain uninitialized." | Added the missing article. | Applied |
| P7 | prose | "In a module workspace the Platform submodule commit is the sole Platform identity: local mode runs that source; in CI package mode the tool builds … (an untagged or dirty submodule is source-mode only), and refuses a composition commit…" (a 73-word sentence) | "In a module workspace, the Platform submodule commit is the sole Platform identity. Local mode runs that source. In CI package mode, the tool builds … (an untagged or dirty submodule is source-mode only). In that mode, it also refuses a composition commit…" | Split into four sentences. "In that mode" keeps the refusal scoped to CI package mode. | Applied |
| P8 | prose | "FR-4 additionally requires its CI evidence to identify … candidate acceptance; this PRD requirement awaits adoption into AD-4/AD-5." | "FR-4 additionally requires a module candidate's CI evidence to identify … candidate acceptance. This PRD requirement awaits adoption into AD-4/AD-5." | "its" lost its antecedent when S11 split the paragraph. The fix uses the glossary term "Module candidate". The adoption statement is kept as its own sentence. | Applied |
| P9 | prose | "Current and previous schema majors are supported" | "Current and previous major schema versions are supported" | Replaced the jargon "majors". | Applied |
| P10 | prose | "stops/cleans up the environment resources" | "stops and cleans up the environment resources" | Replaced the slash with a conjunction for a sequence of actions. | Applied |
| P11 | prose | "submodule-owned production smoke tests", "selected by each submodule", "Each submodule chooses" (3 locations) | "module-owned", "each module", "Each module" | Terminology: in `prd.md`, smoke suites are "owned by the enrolled module" (Glossary: Smoke test), and the options table here already says "module-owned smoke checks". Elsewhere in this document, "submodule" means a git submodule. | Applied |
| P12 | prose | "Administrator approval is not a waiver of failed critical-flow checks." | "Administrator approval does not waive failed critical-flow checks." | Replaced a nominalization with a verb. | Applied |
| P13 | prose | "The attempt starts only after a complete recovery point cut after the lock. On a non-working outcome the executor … and stops; the named recovery then restores…" | "The attempt starts only after the lock is taken and a complete recovery point is then cut. On a non-working outcome, the executor … and stops. The named recovery then restores…" | "Recovery point cut after the lock" is a garden-path phrase. The rewrite keeps the order: lock, then recovery point, then attempt. The semicolon clause is now its own sentence. | Applied |
| P14 | prose | Empty or degraded production sub-bullets (created by S2): "It lifts that observed stop …, never a stop recorded later: a later stop…"; "Staging rehearses a fresh install plus the candidate; failure removes … sets a new stop, while a verified success…"; "…no longer working; this awaits architecture adoption, as does deciding whether…" | "The approval lifts … never a stop recorded later. A later stop…"; "Staging rehearses a fresh install plus the candidate. A failed attempt removes … sets a new stop; a verified success…"; "…no longer working. This definition awaits architecture adoption, as does the decision whether…" | Gave "It" and "this" explicit subjects after the split. "Failure" now names the failed attempt. Removed the double "while". The adoption caveat is kept word for word in substance. | Applied |
| P15 | prose | "From G1 it is first rehearsed … as part of the monthly drill, except an urgent security patch applied in place with an Administrator record." | "From G1, it is first rehearsed … as part of the monthly drill; the exception is an urgent security patch, applied in place with an Administrator record." | The exception clause no longer reads as if it modified the drill. | Applied |
| P16 | prose | "re-runs each environment's served-release smokes" and the exercised-order step "release smokes" (2 locations) | "re-runs the smoke checks of the release each environment serves"; "release smoke tests" | "Smokes" is shorthand. The first fix follows `prd.md` FR-10 ("re-run the smoke checks of the release each environment serves"), and the second uses the glossary term "Smoke test". | Applied |
| P17 | prose | "…to the human production-admission group, distinct from the standing synthetic-admission access, to prove SM-4 positive access." | "…to the human production-admission group to prove SM-4 positive access. This test grant is distinct from the standing synthetic-admission access." | Fixed a dangling appositive: "distinct from" could attach to the group. | Applied |
| P18 | prose | "The recorded working baseline must be ready and pass its smoke suite now before an update begins. … A pre-existing unhealthy production environment stops the update for investigation, sets the promotion stop and, under the PRD's degraded-production definition, can proceed only through one Administrator-approved attempt." | "Before an update begins, the recorded working baseline must be ready and pass its smoke suite at that time. … A pre-existing unhealthy production environment stops the update for investigation and sets the promotion stop. Under the PRD's degraded-production definition, the update can then proceed only through one Administrator-approved attempt." | Removed the ambiguous "now". Fixed a subject mismatch: in the original, the environment was the subject of "can proceed". "That retained application" in the next sentence still refers to the working baseline. | Applied |
| P19 | prose | "Retirement cannot invalidate that combination before the release is working." | "Key, secret or catalog-entry retirement cannot invalidate that combination before the release is working." | "Retirement" had no object. The wording comes from ARCHITECTURE-SPINE line 216 ("key, secret and catalog-entry retirement") and `prd.md` NFR-1. | Applied |
| P20 | prose | "…with compatible environment-current values, including partially updated workloads and committed routing changes." | "…with compatible environment-current values; the attempt also covers partially updated workloads and committed routing changes." | "Including" read as if it modified "values". The rewrite states what the attempt covers. | Applied |
| P21 | prose | "…clears the stop, and a later stop always prevails; the approved empty or degraded attempt is the only exception." | "…clears the stop; the approved empty or degraded attempt is the only exception. A later stop always prevails." | The exception applies to the clearing rule, not to "a later stop always prevails", which S2's bullet confirms. Moving it next to the clearing rule removes a misreading. | Applied |
| P22 | prose | "re-applying grants recorded after the recovery point remains Administrator's." | "…remains Administrator's responsibility." | Completed an elliptical possessive. | Applied |
| P23 | prose | "without operations-repository write", "gains write or admin", "needs no repository write" (3 locations) | "…write access", "…write or admin access", "…write access" | "Write" and "admin" were used as bare nouns; the fix completes them as "access". | Applied |
| P24 | prose | "**Recovery time objective (RTO)** measures the original service outage to verified service restoration" | "…measures the time from the original service outage to verified service restoration" | "Measures X to Y" was missing its object. The fix matches the Glossary definition of RTO. | Applied |
| P25 | prose | "Use frequent independent backups … with the one-hour potential ordinary-data loss target and validate the four-hour recovery target…" | "Use frequent independent backups … with the one-hour target for potential ordinary-data loss. Validate the four-hour recovery target…" | Unstacked the noun phrase and split two instructions. | Applied |
| P26 | prose | "Keep immutable encrypted completed recovery points off-site with restricted access and independently available access/decryption material and retained application artifacts." | "Keep completed recovery points immutable, encrypted and off-site, with restricted access, independently available access/decryption material and retained application artifacts." | Removed the stack of three adjectives and the chained "and … and". The list keeps its original grouping. | Applied |
| P27 | prose | "Assume primary server and storage are unavailable." / "…when the failure occurred; record the accepted revocation exceptions." | "Assume the primary server and storage are unavailable." / "…when the failure occurred. Record the accepted revocation exceptions." | Added the missing article, and split the second instruction out of a long "Check" sentence. | Applied |
| P28 | prose | "…acknowledged after the recovery cut (a per-module off-site revocation journal was rejected), and identity-provider access removals other than admission, such as…" | "…(a per-module off-site revocation journal was rejected); and identity-provider access removals…" | The two list items contain internal commas, so a semicolon now separates them. | Applied |
| P29 | prose | "either writer can change what executors run, an accepted risk." | "either writer can change what executors run, which is an accepted risk." | Fixed an unclear absolute appositive. | Applied |
| P30 | prose | "Move the privileged CI runner off the cluster node, and off any executor host, earlier if staging holds real data." | "Move the privileged CI runner off the cluster node and off any executor host; do so earlier if staging holds real data." | "Earlier" read as if it modified "executor host". The row's "Before G1" boundary still applies. | Applied |
| Q1 | prose | "Memories/EventStore must qualify the independent synchronous complete tombstone mirror and lineage protocol before G2." | Consider: "…must qualify the tombstone mirror (independent, synchronous and complete) and the lineage protocol before G2"? | The scope of the three adjectives, mirror only or mirror and protocol, is ambiguous. Only the author can resolve it without risking a change in meaning. | Proposed, not applied |
| Q2 | prose | "If the unadopted candidate wrote state that production's working baseline cannot read, staging first restores its data to the recovery point cut at the start of that candidate's attempt." | Consider: say what the restore happens before, for example "before any later candidate's staging attempt proceeds"? | "First" has no stated reference point. Supplying one would add a sequencing rule. | Proposed, not applied |
| Q3 | prose | "Include required shared dependencies, particularly Keycloak, its admin/user revocation event export, each environment's OpenBao and production access configuration." | Consider: "…each environment's OpenBao, and the production access configuration"? | It is unclear whether "each environment's" also governs "production access configuration". The fix depends on intent. | Proposed, not applied |
| R1 | prose | Serial comma used inconsistently throughout (the Microsoft style guide prefers the Oxford comma) | Normalize | Rejected. The change would touch about 100 lists and is preference-level churn. It would also make the addendum diverge from `prd.md`, which is being polished in parallel. It should be done in both documents together. | Rejected |
| R2 | prose | British spellings: "labelled", "acknowledgement" (3), "catalogue" | Change to US spellings | Rejected. `prd.md` uses the same spellings ("labelled" ×2, "acknowledgement" ×3, "catalogue" ×1), and cross-document consistency comes first. Change both documents together if wanted. | Rejected |
| R3 | prose | "Local success cleans automatically" | "…cleans up automatically" | Rejected. The phrase matches the AD-10 wording in ARCHITECTURE-SPINE and is understood in context. | Rejected |
| R4 | prose | "The recommendation adopted is Option 1 because…" | Reword | Rejected. The sentence is clear, so a rewrite would be preference only. | Rejected |
| R5 | prose | Slash compounds and noun stacks ("EventStore/admin/Operations", "state/events", "recovery/key access") | Expand | Rejected. They are pervasive, intentional technical shorthand that matches the PRD and architecture. | Rejected |
| R6 | prose | The promotion-stop trigger sentence (a 45-word list subject followed by "sets the durable promotion stop") | Convert to passive with a nested list | Rejected. S1 already made it its own bullet. Converting it to a list would be a structural change, and the enumeration mirrors the glossary's Promotion stop entry. | Rejected |

## Overlap with the structure pass

- **P8 and P14:** these fix antecedents that the S11 and S2 splits left dangling, so the two passes stay consistent.
- **GitHub notification channel:** S14 (proposed) is the only row for the duplicate "single accepted notification channel" sentence. The prose pass did not add a second row.

## Summary

| Disposition | Count | Findings |
| --- | --- | --- |
| Applied | 30 | P1–P30 |
| Proposed, not applied | 3 | Q1–Q3 |
| Rejected | 6 | R1–R6 |

**Word counts** (`word_metrics.py`):

| Stage | Words |
| --- | --- |
| Original | 8,799 |
| After the structure pass | 8,781 |
| After the prose pass | 8,836 (+55) |

The prose pass added words on purpose: explicit subjects, articles, and "access" completions. Clarity took priority over length.

**Density** (paragraphs and bullets, not counting table rows):

| Measure | Before | After |
| --- | --- | --- |
| Longest block | 195 words | 115 words |
| Blocks of 150 words or more | 7 | 0 |
| Blocks of 120 words or more | 8 | 0 |
| Longest single sentence | 76 words | 45 words |

## Verification

- **Numeric tokens:** compared against the post-structure snapshot, ignoring ordered-list markers: no tokens were removed and none were added. Against the original, the only delta is the structure pass's S8 cut of duplicate option references (`3` twice, `1` once).
- **Links and anchors:** every relative link and anchor in `addendum.md` resolves, and all three `prd.md` → `addendum.md#…` anchors resolve (`#mcpcli-context`, `#module-owned-configuration-example`, `#hosted-architecture-questions`).
- **Frontmatter:** byte-identical to the original.
- **Other files:** none were edited except `addendum.md` and the two review reports.
