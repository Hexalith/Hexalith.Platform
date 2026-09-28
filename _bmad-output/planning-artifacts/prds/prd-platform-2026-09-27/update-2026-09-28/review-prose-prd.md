# PRD prose review (Finalize, 2026-09-28)

This document exists to help the internal development, operations and architecture team plan and verify the agreed Hexalith Platform scope without reopening product decisions.

**Lens:** `bmad-review lenses=prose`. It ran after the structure pass, whose findings (S1–S9) are already applied; see [review-structure-prd.md](review-structure-prd.md). No structure row was tagged CUT on text that still exists. **Readers:** humans. **Style guide:** Microsoft Writing Style Guide. **Constraints:** the caller's Finalize brief. Only the smallest clarity fix is made. No requirement, threshold, number, actor, gate, scope, exception, decision, ID, heading or frontmatter changes.

**Voice to preserve:** a formal requirements voice. Capability statements are in the present tense. Obligations are explicit, and "Administrator" is a role name used without an article. Thresholds are in bold. Clauses are compact and joined by semicolons. Every FR ends with **Testable consequences** bullets, and constraints are stated in the negative ("does not", "never"). Fixes below keep that voice. They fix unclear antecedents and over-packed sentences and align terms with the glossary. They do not restyle.

## Word metrics

Command: `uv run .claude/skills/bmad-review/scripts/word_metrics.py prd.md`.

| Stage | Words |
| --- | ---: |
| Before the Finalize polish | 9,831 |
| After the structure pass | 9,795 |
| After the prose pass (final) | 9,818 |

The prose pass adds 23 words. Most of them replace ambiguous pronouns with their nouns ("the environment", "attached runs", "the attempt's") and split sentences. The net change for the whole polish is −13 words (−0.1%). Most of the PRD's density is normative content, which the brief does not allow this pass to remove.

## Findings and dispositions

| # | Pass | Original Text | Revised Text | Changes | Disposition |
| --- | --- | --- | --- | --- | --- |
| W1 | prose | Target users, Recovery deputy: "a named person who receives recovery alerts, has independent recovery/key access, and can execute the documented recovery procedure, verify restoration and reopen service whether or not Administrator is available, without operations-repository write access." | "a named person who receives recovery alerts and has independent recovery/key access. Whether or not Administrator is available, the deputy can execute the documented recovery procedure, verify restoration and reopen service, without operations-repository write access." | Splits a 34-word definition and fronts the availability condition, so it no longer seems to modify only "reopen service". The three permitted actions and the write-access limit are unchanged. | Applied |
| W2 | prose | Confirmed MVP scope ¶2: "…as selected by the architecture. Its address does not establish cluster topology or availability." | "The designated hosted Kubernetes installation is at `192.168.1.30`; this address does not establish cluster topology or availability. Staging and production may share…" | "Its" pointed to "the architecture". The caveat now sits next to the address it qualifies. | Applied |
| W3 | prose | FR-2: "EventStore, Memories, and McpCli must be directly declared in Platform and run and debugged from source in that workspace" | "…from source in the Platform workspace" | "That workspace" read as the domain module's workspace. That reading contradicts Target users, the FR-2 consequence ("checkouts in the Platform workspace"), the Platform workspace glossary entry and the non-goal that excludes standalone workspaces for these modules. | Applied |
| W4 | prose | FR-4: "…including extension packages, identified by content rather than version label; the environment reports the identities it actually loaded. Evidence where any of them is another build or revision, or from uncommitted local changes, is refused…" | "…including extension packages. Artifacts are identified by content rather than version label, and the environment reports the identities it actually loaded. Evidence in which any loaded artifact is another build or revision, or comes from uncommitted local changes, is refused…" | Splits a 41-word sentence. "Any of them" had no clear referent. "From uncommitted local changes" gains its missing verb. | Applied |
| W5 | prose | FR-4, three pronouns: "unless it is retained after failure"; "owner or attached, retains it under the failed-local rule"; "CI attachment is limited to runs within the same CI job; they end with the job" | "unless that environment is retained after failure"; "retains the environment under the failed-local rule"; "…same CI job; attached runs end with the job" | Each pronoun could bind to the wrong noun (owner, run or job). The same issue in three places is merged into one row. | Applied |
| W6 | prose | FR-6 closing paragraph: "…must be supplied when their release checks are integrated with Platform. Changes to declarations or checked inputs require matching evidence. They are not enumerated centrally in this PRD." | "The concrete flow lists belong to the modules and are not enumerated centrally in this PRD. They must be supplied when the modules' release checks are integrated with Platform. Changes to declarations or checked inputs require matching evidence." | The final "They" had come to follow "Changes…", so it referred to the wrong thing. The sentences are reordered so that each pronoun follows "flow lists". Voice (passive) and obligation are kept. | Applied |
| W7 | prose | FR-7: "smoke checks … run at the window start and a declared finite cadence" | "…run at the window start and then at a declared finite cadence" | Adds the missing preposition. Without it, "at … a cadence" reads as a second point in time rather than a repetition. | Applied |
| W8 | prose | FR-8 Promotion stop: "A verified approved empty or degraded attempt is the only other route; Administrator completes…" | "…is the only other route to clearing; Administrator completes…" | "Route" had no object after a sentence about reviews. | Applied |
| W9 | prose | FR-8: "one recorded during the attempt lets it finish, but its success then does not clear the stop." | "…but the attempt's success then does not clear the stop." | "Its" could mean the newer stop. "The stop" is deliberately kept, not narrowed to "that stop", so the rule that the later stop prevails is not reinterpreted. | Applied |
| W10 | prose | FR-8 Approved incompatible release: "The attempt starts only after a usable recovery point is recorded once the attempt controls production." | "Once the attempt controls production, a usable recovery point is recorded; only then does the attempt start." | "Only after … once" hid the order of events. The sequence (take control, record the point, start) is unchanged. | Applied |
| W11 | prose | FR-9: "Administrator's review of the report, recording how each known loss is handled, gates clearing the promotion stop, not reopening service." | "Administrator's review of the report records how each known loss is handled. That review gates clearing the promotion stop, not reopening service." | The participle phrase made the main verb and the "not reopening" contrast hard to parse. | Applied |
| W12 | prose | G3 cell: "…until the affected SM-5 rehearsals repeat; Platform and Administrator record which rehearsals a change affects, including whether a module readiness or smoke declaration change does." | "…until the affected SM-5 rehearsals repeat. Platform and Administrator record which rehearsals a change affects, including whether a change to module readiness or smoke declarations affects any." | This was a 73-word cell. The split, together with replacing the elided "does", makes the recording duty readable on its own. | Applied |
| W13 | prose | Vision ¶4: "…recovery readiness; an Administrator-approved path supports…" | "…recovery readiness. An Administrator-approved path supports…" | Splits a 40-word sentence at the change of topic. | Applied |
| W14 | prose | SM-6: "…RTO at most four hours under declared coverage, including worst-case response, capacity, … and denial of an admission revoked just before the failure." | "…under declared coverage. It includes worst-case response, capacity, … and denial of an admission revoked just before the failure." | Splits a 48-word sentence, and "including" no longer seems to modify "declared coverage". | Applied |
| W15 | prose | SM-C4: "zero deletions of another environment's resources or, through automatic cleanup, of an environment still serving an attached run…" | "zero deletions of another environment's resources, zero automatic-cleanup deletions of an environment still serving an attached run…" | Removes a split construction ("deletions … or, through …, of"). Both zero-conditions and their scopes are unchanged. | Applied |
| W16 | prose | Glossary, Empty or degraded production: "production with no working baseline (empty), or whose last outcome was non-working or which a recorded incident … shows is no longer working (degraded)." | "…(empty), or production whose last outcome was non-working or that a recorded incident … shows is no longer working (degraded)." | Resolves the nested "or whose … or which" that recheck N-11 flagged. | Applied |
| W17 | prose | "minimum composition(s)" in FR-3, SM-2 and SM-3 | "minimum environment(s)" | The glossary defines **Minimum environment** as the developer-defined component set. "Minimum composition" was an undefined synonym for it. | Applied |
| W18 | prose | Non-goals: "fake-service catalogue" | "fake-service catalog" | Matches "Builds catalog" and "executable catalog" elsewhere in the PRD, and US spelling per the style guide. | Applied |
| W19 | prose | Downstream intro: "Rules marked in the adoption row below…" | "…in the adoption rows below…" | There are two adoption rows (FR-4, and FR-8/FR-9). | Applied |
| W20 | prose | Last downstream row: "changes any third-run decision carried here" | "changes any third-revision decision carried here" | The PRD elsewhere calls it "the architecture's third 2026-09-28 revision". The row text is otherwise unchanged. | Applied |
| W21 | prose | Downstream intro: "…The following implementation and qualification work remains. None of these documents establishes a passed production gate." | Swap the two sentences so "The following … work remains." sits directly above the table. | The lead-in sentence was separated from the table it introduces. | Applied |
| W22 | prose | NFR-2 ¶4: "The Platform MVP recovery envelope governs stricter infrastructure RPO/RTO or availability expectations in Folders and Projects." | Consider: "…takes precedence over stricter infrastructure RPO/RTO or availability expectations stated in Folders and Projects"? | "Governs" is ambiguous: it could mean overrides or bounds. | Proposed, not applied: the fix depends on the author's intent. |
| W23 | prose | NFR-1 ¶2: "…requires the Administrator-approved release and separately planned recovery procedure in FR-6." | Consider: "…in FR-6 and FR-8"? | FR-6 only permits the approval "under the rules in FR-8". The incompatible-release procedure itself is defined in FR-8. | Proposed, not applied: it changes a cross-reference. |
| W24 | prose | FR-8: "If the recovery attempt fails or its outcome cannot be verified because the cluster is unreachable…" | Consider: "…cannot be verified, for example because the cluster is unreachable…"? | As written, an unreachable cluster reads as the only cause of unverified recovery. FR-7 also lists observation gaps and identity uncertainty. | Proposed, not applied: it may widen the trigger. |
| W25 | prose | Downstream intro: "None of these documents establishes a passed production gate." | Consider: "Neither this PRD, the addendum nor the architecture establishes a passed production gate"? | "These documents" has no explicit list. | Proposed, not applied: it names the documents, which is an interpretation. |
| W26 | prose | Mixed serial-comma use ("Tenants, Parties, Folders, and Projects" versus "EventStore, Memories and McpCli") | — | This does not impede comprehension. Normalizing it would touch dozens of lists, hide the substantive diff, and diverge from the addendum, which a parallel agent is editing. | Rejected |
| W27 | prose | British "labelled/labelling" (FR-4, SM-3, downstream row) | — | The spelling is consistent within the PRD and shared with the addendum. Changing only the PRD would create cross-document inconsistency. | Rejected |
| W28 | prose | "smoke check" and "smoke test" used interchangeably | — | Both forms appear in established compound terms ("smoke-check declarations", "smoke-test results") across the PRD and addendum. Normalizing them in one document risks term drift, and the meaning is clear. | Rejected |
| W29 | prose | FR-8 Automatic recovery: overlap between "reports failed or unverified recovery to Administrator and the deputy for intervention" and the next bullet's "reports every deployment failure and recovery result" | — | Merging them would drop "for intervention" and the placement of the no-cycling rule. Content is sacrosanct. | Rejected |

## Final verification

The checks compare the file before the Finalize polish with the final file.

- **IDs:**
  - FR-1..FR-12 (`####` headings), NFR-1..NFR-3 (`###` headings), SM-1..SM-6 and SM-C1..SM-C5 (bold bullet labels) and G1..G3 (gate-table rows) are each defined exactly once.
  - ID mentions changed only for FR-8 (+1) and FR-9 (+1). Both come from the glossary cross-references added by S8.
- **Numbers:**
  - The multiset of numeric tokens is identical (every digit sequence, including thresholds, dates, `192.168.1.30` and ID-free numbers).
  - Among number words, "four" appears once fewer. That is the glossary's restatement of the four-hour RTO in Reduced-recovery state, removed by structure finding S8. The rule itself is unchanged in FR-9, NFR-2, SM-6 and the RTO and Response coverage glossary entries.
- **Headings:** all 36 are identical, so every anchor is preserved, including `#downstream-decisions-and-readiness-evidence`. `addendum.md` links to `prd.md` only, not to PRD anchors.
- **Frontmatter:** identical.
- **Downstream table:** 17 rows before and after. One cell changed (W20).
- **Glossary:** the same 43 terms, now alphabetical (S9).
- **Links:** all 11 local links resolve:
  - the brief and brief addendum;
  - `validation-report.md` and the architecture spine;
  - `addendum.md` and its anchors `#module-owned-configuration-example`, `#mcpcli-context` and `#hosted-architecture-questions`;
  - both update summaries (`update-2026-09-28/update-summary.md` now exists);
  - the in-page downstream anchor.

## Summary

- **Totals:** 29 findings: 21 applied, 4 proposed and not applied, 4 rejected.
- **Scope of changes:** this polish edited only `prd.md`, plus this report and its companion. It did not edit `addendum.md`.
