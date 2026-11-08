---
title: '2.10 Qualify the runner lifecycle on the reference composition'
type: 'feature'
created: '2026-11-08'
status: 'ready-for-dev'
story_key: '2-10-qualify-the-runner-lifecycle-on-the-reference-composition'
course_correction: '../planning-artifacts/sprint-change-proposal-2026-11-08.md'
context:
  - '{project-root}/docs/contracts/candidate-and-attachment-policy-v1.md'
  - '{project-root}/docs/contracts/test-environment-v1.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.Builds/AGENTS.md'
---

# Story 2.10 execution spec

## Intent and boundaries

Qualify the complete runner lifecycle on the Parties reference composition. Preserve the original Story 2.10 acceptance criteria in [epics.md](../planning-artifacts/epics.md#story-210-qualify-the-runner-lifecycle-on-the-reference-composition). This spec records the work transferred out of Story 2.8 by the [approved course correction](../planning-artifacts/sprint-change-proposal-2026-11-08.md).

Story 2.8 delivers CI execution/finalization and focused proof of its original six edge-case rows. Story 2.9's fixture migration is already recorded done. Neither story's completion establishes full SM-3 or final accepted-tool qualification. Keep prior results under their actual source/run/decision identities; no retrospective relabelling or cross-decision reuse is permitted by policy v1.

## Tasks and acceptance

- [ ] Inventory the reference composition's existing lifecycle evidence once. Classify each row as passed for its original recorded subject, failed, incomplete or unexecuted. Record exact source, suite/configuration, package/image, policy and run identities. Do not reset completed implementation or hide earlier failures.
- [ ] Close the complete local/reference scenario table below. Real local service execution remains subject to the existing restriction; resource-free checks may run locally, and service scenarios run in the authorized disposable hosted environment unless the user explicitly changes that restriction. An unexecuted required local row remains open.
- [ ] Finish the broad manual `matrix` campaign, including both metadata-lock refusal stages, exact retry, peer preservation and partial startup. Use the genuine current tool and fixed verified caller inputs. The three failed historical runs remain failed: `42400061797`, `42408333976`, `42418257508`.
- [ ] Account for real startup/execution cancellation proofs from 2.8 under their own identities. A new acceptance decision or changed relevant input requires fresh qualifying evidence; historical diagnostic observations cannot supply a new candidate pass.
- [ ] Record actual startup duration, effective deadline/override, immutable first outcome, attachment expiry/withdrawal, bounded redacted diagnostics and exact owned leftovers for every required scenario. Required missing/skipped cases do not pass.
- [ ] Record the qualified tool version as Platform-accepted and pin it in Platform and Parties only after every applicable qualification gate passes. A published version or a completed document alone supplies no acceptance.

| Required scenario | Required proof |
| --- | --- |
| Success and failure | Correct first outcome; local success cleanup and failure retention; CI cleanup in both cases |
| Startup timeout and partial startup | Original finite deadline; exact partial ownership recovery; diagnostics and correct local/CI disposition |
| Cancellation and collision | Real cancellation, exact ownership and explicit conflict; unrelated resources preserved |
| Owner finish before attachment end | Outcome recorded immediately; local cleanup waits for live holds; completed CI owner refuses new admission |
| Owner cancellation before attachment end | Active attachments withdrawn and work/access ended before owner teardown |
| Attached failure and hold expiry | First failure/local retention preserved; absolute expiry unchanged; expired work withdrawn |
| Failure followed by cancellation | Original failure and applicable local retention remain immutable |
| Metadata locks, cleanup retry and peer | Both real refusal stages observed; bounded refusal and exact retry; peer resources survive |

## Bounded optimization work

These are implementation tasks, not capabilities supplied by the planning correction. They do not block a valid qualification run when the existing implementation can complete it correctly.

- [ ] In the owning Parties/Builds controller, add fixed safe failure codes/checkpoints and bounded redacted diagnostic fields. Cover the actual missing-cache, reader-directory and process-acknowledgement regressions with focused tests. Never publish arbitrary exception messages, raw startup output, descriptors or credentials.
- [ ] Add an explicit focused CI qualification selection for 2.8 if the current full `matrix` controller is its only hosted entry point. Preserve the full matrix for this story. Report selected, required and completed cases; a focused pass must never appear as a full-matrix pass.
- [ ] Where setup dominates, reuse verified downloads/package inputs keyed by exact relevant toolchain/catalog/package identities. Revalidate consumed content and restore fresh job-local state. Keep isolated tests before provisioning and all fresh acceptance runs required by policy v1. Sharing an old green baseline as a new decision's test result is excluded.
- [ ] In Platform CI, separate current/legacy source resolution from execution so independent lanes can run concurrently. Preserve the actual `tuple` and `legacy-public-cohort` required check contexts, all original tests, real legacy source identity and an overall failure when either lane fails or is missing. The five-SDK matrix is already parallel.

Use a focused test first for each changed mechanism, then the owning repository's required checks. Run full qualification at the final fixed input set; rerun affected evidence when relevant inputs or the acceptance decision change. Retain separate actual workflow runs for cancellation and the ready-artifact-before-cancel protocol.

## Handoff and completion

Owner: Platform/Builds developer, with Parties owner changes made in Parties. The product owner tracks the scenario table; runtime implementation follows the normal development workflow. No agent or remote campaign was launched by this course correction.

This story is ready for development, not implemented or qualified. Its evidence ledger and final acceptance must remain finite and explicit. Preserve existing budgets and credentials policy; a timeout increase is not a performance improvement. Measure setup, build, test, startup, cleanup and audit time separately before reporting savings.
