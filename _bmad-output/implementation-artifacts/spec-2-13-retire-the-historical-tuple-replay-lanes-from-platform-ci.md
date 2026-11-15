---
title: '2.13 Retire the historical tuple-replay lanes from Platform CI'
type: 'refactor'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
story_key: '2-13-retire-the-historical-tuple-replay-lanes-from-platform-ci'
baseline_commit: 'ddb1c0b141372a66d4a9d9c231d0041afbb7cd0c'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Historical tuple replay requires companion Platform commits and branches for every change. Merging those branches can restore obsolete pins, without providing qualification credit.

**Approach:** Execute source resolution and current-tuple checks on the candidate alone. Remove replay machinery, including the package-reader checkout hidden inside the current job, and update preparation instructions.

## Boundaries & Constraints

**Always:** Require both prerequisite jobs to succeed. Preserve pins, identity/integrity, runtime/output verification and zero-skip enforcement. Retire only historical-source-specific assertions. Preserve purpose/key/snapshot refusals, archive authenticity, signed-host handoff, reader integrity, readiness and model transport. Old packages may remain explicit fixture data without another Platform checkout or invented provenance. Record coverage changes.

**Never:** Create companion commits/refs, weaken image or runtime admission, equate reader authenticity with execution qualification, delete archived qualification/broker tags, modify `/pushall`, or implement Story 2.14's version-only pin redesign. Historical observations keep their original subjects.

## I/O & Edge-Case Matrix

| State | Expected behavior |
| --- | --- |
| PR, main push or explicit candidate dispatch | All Platform checkouts select only that candidate |
| Either prerequisite fails, cancels, skips or is absent | Combined verdict fails |
| Documentation-only or gitlink-only change | No historical descriptor update or extra ref |
| Changed owner, provenance, package body or runtime | Existing identity checks refuse |
| Old replay-only positive | Retired; no new image/runtime permission |

</frozen-after-approval>

## Code Map

Aliases: E = `eng`; T = `tests/Hexalith.Platform.Composition.Tests`; S = `tests/source-release`.

- `E/run_tuple_ci.py`: `collector_for` routes 450/478 current42 and all 97 base42 cases into signed4.47/NET10 replay; removing YAML jobs alone is insufficient.
- `E/current_tuple_source.py`: imports `PROVENANCE`, `commit`, duplicate-rejecting `decode` and `git` from the legacy module. Retain current owner/tag/blob checks.
- `T/PlatformPartiesPackageFixture.cs`: `PreparedFixtureRoot` authenticates another Platform revision. `currentArtifacts` is not a replacement for old image-positive tests: production deliberately refuses those newer image cohorts.
- Pins are repaired at HEAD; local qualification branches are absent. Preserve 105 archived qualification tags and both broker tags.

## Tasks & Acceptance

**Execution:**

- [x] `.github/workflows/tuple-ci.yml`, `S/tuple-workflow.test.mjs`, `S/source-release.test.mjs`: retain resolution, current execution and verdict only; remove historical inputs/outputs and Platform fetches; use candidate-only shallow checkouts. Test trigger coverage, checkout identity and the executable two-result verdict matrix.
- [x] `E/current_tuple_source.py`, `E/file_host_catalog.py`, `E/prepare_tuple_sources.py`, `E/test_current_tuple_source.py`: move shared helpers into current ownership; keep binding generation only; remove companion preparation/verification and test doc/gitlink changes without companions.
- [x] `E/{legacy_tuple_source,pre_extension_tuple_source,package_reader_source}.py`, corresponding `test_*.py`, and `E/{legacy-tuple-source,pre-extension-tuple-source}.json`: delete retired source modules, tests and descriptors.
- [x] `E/run_tuple_ci.py`, `E/tuple-ci-*.json`, `E/test_tuple_{collectors,runtime_context,sdk_suites}.py`: remove historical runtimes, reader workspace and fallback execution. Delete replay-only profiles; rebuild remaining current inventories with explicit positive counts and current collectors. Preserve extension controls and generic package-authenticity cases from base42; remove obsolete CLI selections and defaults.
- [x] `T/PlatformPartiesPackageFixture.cs`, `T/PlatformPackageCatalogReaderFixture.cs`, `T/PlatformQualificationPackageFixture.cs`, `T/Hexalith.Platform.Composition.Tests.csproj`: remove reader-workspace metadata and historical Platform roots. Use candidate/tool inputs and genuine fixture-owned declarations/packages; follow `T/PlatformPartiesProofFixture.cs` for local histories granting no publication authority.
- [x] `T/PlatformCurrentOwner{Package,Handoff,MemoriesTransport,Preparation}Tests.cs`, `T/PlatformPublishedPartiesMemoriesTransportTests.cs`, `T/PlatformParties{PackageEntry,ValidationHandoff,CatalogHandoff}Tests.cs`, `T/PlatformSdk42{PackageEntry,CurrentOwnerMemoriesTransport,PublishedPartiesMemoriesTransport,EventStorePackageTestCohort}Tests.cs`, `T/PlatformEventStorePackageTestCohortTests.cs`: adapt fixture callers while preserving substantive positive/negative behavior. Isolate static old-body checks from loaded current assemblies. Explain each replay-only selector removed.
- [x] `docs/current-tuple-preparation.md`, `docs/domain-module-enrollment.md`, `E/tuple-source-preparation.md`, `README.md`: replace L/M and reader-companion obligations with candidate-only preparation; align current descriptions with the selected binding and retain historical evidence labels.
- [x] `_bmad-output/implementation-artifacts/evidence/2-13/`: save coverage mapping, canonical revisions, before/after pin/tag inventories and checks. Remove any local `qualification/*` branches remaining at delivery after recording their targets. Verify an isolated merge leaves pins unchanged without invoking `/pushall`.

**Acceptance Criteria:**

- Given a candidate, when preparation and CI execute, then no other Platform revision or qualification ref is required.
- Given retired replay machinery, when repository controls run, then remaining tuple, source-release and Composition controls pass with positive totals and no skips.
- Given landing on main, when hosted tuple CI completes, then resolution, current execution and their verdict are green; local audit alone cannot satisfy this criterion.
- Given unchanged archived tags and current pins, when cleanup and merge verification finish, then both inventories are preserved.

## Implementation Notes

Workflow execution constraints: no push or remote operations; implementation proceeds sequentially. Hosted main verification remains required after an authorized landing and cannot be claimed from local checks.

Local implementation verification passed: current-only workflow/source publisher gates; retired source modules, descriptors, frozen profiles and NET10 switch; current fixture/collector execution; complete coverage mapping and candidate-only instructions. Necessary compatibility corrections preserve central transitive pinning in copied readers, authenticate the signed Builds 6.0.5 host/Tooling pair, and expose the existing authenticated graph checks internally without granting image admission.

[Verification](evidence/2-13/verification-summary.json) reconciles 565 Composition executions, 173 standalone Python, 83 source-release Node, 14 extension Node plus 30 Python, graph 1 and source-SDK 28: positive totals and zero skips. The 31-project package build has zero warnings/errors. Test-only corrections retain their actual snapshot/output identities and explicitly reuse unchanged controls. All private jobs finished and their workspace was removed.

[Parent audit](evidence/2-13/parent-verification-audit.json) independently checked every archived member hash, all 36 final XML groups, all 186 runtime/test source files, the trigger/verdict and real-Git matrix logs, and live pins/gitlinks/tags. Parent staging added one further CRLF attribute for the new reader helper and explicit TSV total labels; these policy/evidence-only edits change no verified C# or runtime source. Hosted main acceptance is pending the conditional landing phase and is not claimed by local verification or review.

[Post-review verification](evidence/2-13/post-review/README.md) passed all 19 affected C# cases and 14 current-source Python tests at `9b27493ed62183dd37c31894f2ac712da33bfed4` (tree `7bcf050c12f28d2cc8ae4ac8f01ce56009045c31`). It strengthens the actual Production caller, cold/warm snapshot derivation and both changed-receipt proof refusals, removes dead fixture inputs, corrects both guides, and proves depth-one Platform selection with independent owner/tag/blob checks. All 77 production/build source files remain identical to the preserved full run. The [independent parent audit](evidence/2-13/parent-post-review-audit.json) checked the five XML groups, eight edited source/guide hashes, actual output stability and compressed raw metadata. The [parent control pass](evidence/2-13/parent-post-review/summary.json) freshly passed 174 standalone Python, 83 source-release Node, 14 extension Node and 30 extension Python checks, zero skips. The original 565-case observations retain their original subjects; only the affected cases were rerun after review.

Background fetches advanced `origin/main` to `5b44f2d058f9221d8b54782c377e09fec5786b65` and exposed public tag `v2.1.6`. All original 152 tags, including 105 archive and both broker tags, remain unchanged; the additional tag is preserved. The [updated isolated merge proof](evidence/2-13/upstream-merge/summary.json) resolved exactly the two descriptor modify/delete conflicts by keeping their retirement, preserved incoming release tooling/lockfile/assertions, matched all 85 production/review source hashes and passed all 83 source-release checks after fresh `npm ci`. Its actual private merge is `ef81f17c21f3593de4c16fa3e0ceef0ba1e6acf5`. Completion then rebased the local feature branch onto that observed main with the same two resolutions. [Final code verification](evidence/2-13/final-code-verification.json) proves every code/documentation path outside generated metadata matches the tested merge; the original captured baseline remains unchanged. No remote mutation or `/pushall` was invoked. Hosted main acceptance remains outstanding.

The local build workflow is complete. Sprint status remains `review` until an authorized landing and actual hosted main resolution/current/verdict acceptance; spec `done` does not claim that conditional hosted result.

## Spec Change Log

## Review Triage Log

All three independent layers completed before triage. The verification-gap layer reported no gaps. Each finding below was assessed individually before grouping.

| ID | Verdict | Evidence and route |
| --- | --- | --- |
| Blind-1 | medium | The Production branch calls `PlatformCurrentOwnerProof.Create(..., false)` directly and constructs an unused Production builder. It therefore misses incorrect environment propagation in `ComposeRun`; patch the actual caller and require refusal before its configuration callback/effects. The reviewer's withdrawn well-formed-key objection is not a finding: an ordinary initial key has no prior anchor. |
| Blind-2 | low | The two EventStore mutation callbacks exercise the actual production `ValidateTestInputs` method with positive baselines and distinct sentinels, but do not independently isolate removal of its call from `Bind`. `Compose` still calls `Bind` before probes and `Bind` calls this layer before body/image checks. Reject this additional caller-isolation request: current unqualified images already refuse, and isolating a deeper admitted caller would require new instrumentation or an image-positive fixture, beyond a direct correction for a path developers cannot execute today. |
| Blind-3 | low | The transport component deliberately registers the real subscriber itself; it cannot detect a change to the current-package registration argument. Current full package composition refuses in `Bind` before reaching that registration, while `ActualDaprCliListenersAndTokensRemainServerScopedAsync` still composes the production source path and asserts actual registration. Reject additional current-image caller coverage: establishing that admitted path requires new qualification/instrumentation, beyond a direct correction for an unavailable execution path. |
| Blind-4 | low | The deleted old-image positive asserted a composed native preparation and its constant `RoutingCatalogObservationsRequired` property. Current native preparation has separate executable controls; `CatalogBootstrapBindsActualKeysPoliciesFilesAndCohortStartWaitsAsync` still composes the source path and checks all six participants. Isolating package registration would require an admitted package-image path; reject this negligible residual coverage request because the fix adds more than a direct correction to an unavailable path. |
| Blind-5 | medium | Both handoff tests preserve post-generation bytes/proof identity but exclude the catalog snapshot from their immutable-input comparisons without structurally bounding its cold-to-warm changes. Patch with a shared assertion allowing only genuine derived consumer cache entries and the correctly recomputed snapshot hash. |
| Blind-6 | low | Deterministic bodies legitimately let the original proof authenticate the repeated receipt, so that branch must remain. The existing controlled changed digest checks only the replacement proof; patch the same negative receipt to also require original-proof refusal, providing unconditional stale-measurement coverage without inventing body drift. |
| Blind-7 | low | `sdk42` became unused and its private705/25 comments are now misleading; patch by removing the dead argument and correcting comments. The two generic/SDK42 suite variants already selected the same SDK42 fixture at baseline (`SelectedSdkTest` delegated to `ExtensionSdkTest`), so broad suite deduplication is not a new defect and is not part of this patch. |
| Blind-8 | medium | The linked operational `docs/source-releases.md` still says five required tuple jobs, historical 412/349 lanes and obsolete totals. Patch its current operational description to three candidate-only jobs and the measured continuing profiles. |
| Blind-9 | medium | `docs/package-catalog-qualification.md` still presents deleted `PlatformPartiesPackageReaderTests` and `eng/tuple-ci-suites.json` as current unconditional positive instructions. Patch with a clear historical label and links to the continuing controls/preparation guide. |
| Blind-10 | medium | Workflow controls assert depth-one checkout syntax, but the recorded complete candidate execution used a shared full-history clone. Patch a real depth-one current-source test that proves the sole Platform commit suffices while preserving independent owner release/tag verification; no alternate Platform revision may become available. |
| Blind-11 | low | The retired old-image topology test included explicit non-Linux/ARM64 variants; the production runtime guard is unchanged and the tuple collector still enforces actual current runtime/output identity. A current package-image positive is unavailable, so simply reusing those variants would pass on the later image refusal. Reject this additional isolated runtime-layer request: meaningful coverage needs new instrumentation/admission-layer factoring, beyond a direct correction on platforms this tuple does not execute. |
| Edge-1 | medium | The qualification guide unconditionally points readers at a deleted replay inventory/class. This independently confirms Blind-9; patch the same stale historical/current documentation boundary. |

After individual verdicts, Blind-9 and Edge-1 share one stale-guide root cause. The remaining accepted entries are independent trivial patches with no public surface or new runtime authority. Low caller-isolation requests are rejected under the review's low-finding filter; no intent gap, bad spec or deferred implementation issue remains.

All accepted patches are applied and their focused checks passed (19 C# cases and the expanded 14-test real-Git module, zero skips). Parent verification additionally passed the complete 174-test standalone Python set, 83 source-release checks and 44 extension-release checks. Source/output hashes and all five focused XML groups were independently audited. Post-test changes are evidence, notes and the scoped verbatim-patch whitespace policy only.

## Verification

Use `run-with-temp`; keep disposable copies/feeds/outputs under `$TMPDIR`, useful logs outside it, and await children.

Run remaining `E/test_*.py` tuple/source/publication/catalog/restore controls through their documented entry points; `npm run test:source-release`; extension-release controls; fresh Composition package builds and direct xUnit DLL execution for remaining inventories. Verify current tuple locally with `run_tuple_ci.py --audit-only`, then actual hosted execution. Test verdict failures/cancellation/missing results and absence of alternate Platform checkout/fetch paths. Record exact commands and no-skip counts.

Planning baseline: 82 source-release and 55 Python tuple tests passed. Collection-graph failed under generic discovery, then passed through its required `eng/test_tuple_collection_graph.py --evidence ...` entry point. Pre-implementation logs: `evidence/2-13/planning/`.
