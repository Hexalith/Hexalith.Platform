# PRD prose polish — 2026-09-27

This document exists to help the internal Platform team understand and implement the agreed requirements and assemble the evidence needed for acceptance.

**Readers:** humans. **Style guide:** Microsoft Writing Style Guide. The voice is direct, technical and normative: named actors, stable requirement IDs, explicit failure behavior and measurable limits. Preserve those choices, the deliberate repetition of acceptance policy, and the approved structure changes. No content or policy change is proposed.

Executed `uv run .agents/skills/bmad-review/scripts/word_metrics.py _bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md` after the structure edits. Exact total: **7,215 words**. Relevant section counts: FR-2 **154**, FR-7 **394**, FR-9 **507**. Individual snippet counts below use the same script's `word_count` function.

| Pass | Original Text | Revised Text | Changes |
| --- | --- | --- | --- |
| prose | FR-2, `prd.md:82`: “only the components required by the selected scenario.” | “only the components specified by the module configuration.” | Replaces an undefined selection term with the configuration that FR-3 already defines. **8 → 8 words.** |
| prose | FR-9, `prd.md:226`: “It assumes primary server and storage loss and proves detection, worst-case response within declared coverage, replacement capacity, restore and verification inside NFR-2.” | “It assumes primary server and storage loss and proves that detection, worst-case response within declared coverage, replacement capacity, restore and verification meet the NFR-2 targets.” | Clarifies that the exercise must meet the targets; a requirement section is not a location in which activities occur. **22 → 25 words.** |
| prose | FR-7, `prd.md:189`: “A gap in observation invalidates verification; uncertain release identity, ownership or records stops changes for intervention.” | “A gap in observation invalidates verification; uncertainty about release identity, ownership or records stops changes pending intervention.” | Gives the clause a clear singular subject and makes the waiting condition explicit. **16 → 17 words.** |

Three small clarity fixes, no further recommendations. Accepting all adds **4 words**, producing **7,219 words**. No thresholds, actors, permissions, release modes, response-coverage rules, IDs or requirement order change. No frontmatter or markup was copy-edited; the PRD itself was not modified.
