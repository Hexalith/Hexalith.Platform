# Story 2.8 stop/save handoff — 2026-11-08

> Superseded for execution by the user-authorized [2026-11-08 course correction](../planning-artifacts/sprint-change-proposal-2026-11-08.md) and the [compact active Story 2.8 spec](spec-2-8-run-ci-integration-on-disposable-runners-with-guaranteed-cleanup.md). The body below remains the historical stop/save record. Its expanding archive-reading queue, latest-main prerequisite chain and whole-reference-matrix scope are no longer instructions to resume. Historical outcomes, preservation requirements and mutation receipts remain valid under their original identities.

The user explicitly requested: **“stop and save. I will continue in a new session.”** Work is stopped. Story 2.8 remains incomplete, BMAD Step 03 remains active, and no acceptance boxes or sprint status have changed. Resume the entire original `$bmad-build 2.8` only when the user continues it.

The sole implementation source of truth is [the Story 2.8 spec](spec-2-8-run-ci-integration-on-disposable-runners-with-guaranteed-cleanup.md), including its append-only scope history. Load its frontmatter contexts before implementation. The already rendered workflow is `_bmad/render/bmad-build/platform-fcb6e4841134/df57414dae3b284c0fb7/workflow.md`; current step is `step-03-implement.md` in that directory. Rendering already happened for this workflow run; do not restart the workflow or load Step 04 early.

## Preservation and authorization

- Canonical local Platform HEAD at the last observation is `a1ebd7affb28b61dead22c3adbf97b5c8e56e541`, preserving external `d7da8a08453449e138dc26d2127ec34bb21c60ba` and earlier `bdc4547cf68a3f3c960f8c1b59c3ec5455608ce2` history. It does not yet incorporate all final qualified owner selections. Preserve all incoming Story 3.2 work and all 17 root pins except later explicitly qualified owner adoption. Never reset this workspace to an older publication.
- Immutable original Platform baseline is `03a099bba258c37a8f16c8b2e842ae842b1ad6d6`; original Builds baseline is `a22b55f0a4f6d2aaa3a5ccc505896064ac595364`. Keep both. The spec's original 10,345-byte prefix has SHA-256 `89fe71635d283b3d77c24ffb28c802a8a2a399d94551d1f3e9cca5b56cd86813`.
- User-approved final audit method: read every code/configuration/documentation change fully, and verify archived evidence through complete manifests, hashes and original receipts. This is an audit representation exception, not an acceptance waiver. Approval receipt is in the evidence directory below.
- User-approved PR #34 CODEOWNER exception applied only to exact head `870e48b9f19b243c98bd28c585d4561b8fa8d0b6`. PR #34 is already normally merged. Never repeat its merge or reuse that exception for a later PR.
- Normal source delivery and necessary prerequisite publication were previously authorized. No force, amend, rebase, reset, clean, protected-main push, manual tags, policy/credential changes, fabricated identities/reviews, or unchanged retry-to-green. Inspect uncertain mutation outcomes before any further request.
- No local real AppHost, DCP, Dapr, Docker/services, TCP/HTTP listener or service probes. Resource-free compiler and pure filesystem/Git/process/transport fixtures/private MTP IPC are allowed within their recorded scope. No positive fake CI identity. Only necessary direct root-declared dependencies may be initialized; never nested/recursive ones.
- Use full owning locked commitlint 21.2.4 messages before/after real commits/titles. Use `git -c core.commitGraph=false` because incoming commit graphs are broken. Preserve immutable first failures and every existing seal.

## Durable locations

Parent cache, abbreviated **P** below:
`/home/administrator/.cache/hexalith/story-2-8/parent-story28-resume-20261108T094224Z`.

Finite repository evidence directory, abbreviated **EV**:
`_bmad-output/implementation-artifacts/evidence/2-8/release-phase/remote-resumption-20261103/`.

P contains `capture_api.py`, `capture_operation.py` and `capture_eventstore_artifact.py`, all parent-read. API capture is GET-only and writes unique command/time/stream hashes; operation capture returns **bytes**, not JSON. Never reuse an uncertain operation label. Artifact capture verifies actual API size/digest and safely hashes every member. Do not print raw logs, credentials or large encoded data.

## Qualified prerequisites already complete

**Builds:** genuine `3c8096180ecf2aa539ac454815efbfceb8a92822` / `v4.44.13`, CI `42436815567`, normal publisher `42438005017`. Module archive `036d32fb2c7ea961d4ec055ad59a70f2cdd6377ab034d67f2b82e01e16b62bec`; complete admitted pair AppHost `5d3ed600a11c921ad1ca8d98e90430c000681ab2b9d41dfb7d5df3a0dae6f252` and Tooling `39583273ccfbf9fac6b921e1996c109cc66b0d5336ff59fc7ac70923edcf0d2d`; inventory `887866d05fda3b6283dd3ea34b3e605f7f403c8df504eefdd095b3cc62f56717`; catalog blob `b45555806e78ea6ce66ad6a8f0fcc5f7c70b691d`, raw SHA-256 `c66fb47d42633dd51bdfa6f31216ee22412e0137a664bf472edf72ac103ad5d1`.

**Platform prerequisite:** PR #34 was externally normally merged as `0f4e27f1f78310bd731c10c19dc51bb1e0345d44`, tree `c998624b1aa6ec2d94b4fe77fda62f37d18f55ce`, preserving real legacy ancestor `3cd036f78ccdfeb72521122a65f1a3aef0bff9da`. Fresh main tuple `42472676506` passed current 412/original legacy 349; all five SDK lanes in `42472676518` passed (75 consumer/236 finite capability/14 owner per SDK). Normal source publisher `42475186387` produced genuine `v1.7.1`, release `438196836`, source-only assets empty. EV `parent-platform-main0f4-v171-all-primary-gates-and-normal-source-publication-20261108.json`. No final native or Story acceptance transfers from this prerequisite.

**Parties:** PR #67 normally merged as `566b9bfcaff14639003414ee1b5db3e27c7c956a`, tree `9771ed23baca776cbe8c2d1d8ef2a5f75fec4d86`. Fresh main CI `42479678863` passed all nine jobs, all 15 original isolated projects and all 28 original native names. Actual public reader/source/image roles, persistence, projection, anonymous 401, diagnostics and all six empty owned inventories were verified from genuine artifacts. Applicable main CodeQL/commitlint/release-candidate checks passed. EV `parent-parties-main566-first-source-ci-native28-public-reader-primary-audit-20261108.json` and related PR/main receipts. Runtime/public package roles stay distinct. Parties selects the genuine Platform v1.7.1 / Builds v4.44.13 pair; internal EventStore pin and SDK 4.1 / public Parties 3.1 authorities were preserved.

## Critical latest change: EventStore main advanced externally

PR #397 was normally squashed **once**, yielding main `b0ea2fce1d03ab8b90c44415128e2e60c64cd614`, parent `e1980560934736688a05a53a0aadc9397277afae`, tree `f91200d435c32880a6d78989fd56e0da232ee635`. Never repeat that merge. Its source PR `bebce14225eea8f60eb01dba81438cccc3882fbc` passed all original service/native/project checks. Preserve every earlier Dapr download/checksum/workflow-context failure. The final checksum repair verifies all six authentic original Dapr 1.18.4 GNU binary checksum lines, without changed URLs, input archives, tests or budgets.

EventStore deliberately retains its independently qualified Platform `52bc7986170f268afaef6227d0519a1d662af256` / v1.7.0 and Builds `b4703ec2247321f867f14cd9caca43fa00190feb` / v4.44.12 pair. **Do not migrate EventStore to Builds 4.44.13 merely to match Parties.**

Fresh b0ea main CI `42492835374` passed all six jobs, all 20 original projects / 21 exact case tables, native 9, pure 20, same original 49 optional cases, both native TRX copies/diagnostic hashes and all six empty inventories. Four genuine archives / 31 members / 34,358,307 member bytes were verified. EV `parent-eventstore-mainb0ea-first-source-ci-native9-and-pure20-primary-audit-20261108.json`.

Fresh b0ea extra suites passed: seven original tables / 1,382 passes / 21 same optional cases, all original 63 PF02 cases. P `parent-eventstore-mainb0ea-extra-primary-and-original63-pf02-audit.json`. Only two original owned Azure Guid canaries use the previously documented bounded comparison; actual names are retained, all other rows exact.

**b0ea main integration `42492834892` is CANCELLED, not qualified.** Its compatibility job `128768701780` passed all original 444 names. Live job `128768701972` was automatically cancelled at 15:45:50 UTC. The actual GitHub annotation is: “Canceling since a higher priority waiting request for integration-refs/heads/main exists”. Workflow concurrency is `integration-${{ github.ref }}`, `cancel-in-progress: true`; both original 90-minute job budgets were unchanged. Parent issued **zero cancellation, retry, or Parties dispatch requests**.

The external Story 3.2 PR #398 advanced EventStore main to **`b13271ba1d5ce4553372ee5e9edb91ff4ca7b0ce`**, sole parent b0ea, at 15:45:36 UTC. Preserve this work. The parent fully read its exact proper 44,838-byte / 806-line / 14-added-file diff, SHA-256 `879e90673eb1a934225bbb5192642001889ffadb517a93c2f96de62d758a91ed`, at `/tmp/story28-parent-incoming-eventstore-mainb132-zw_oa0ww.diff`. It adds extension API source and its tests; no old path, caller, workflow, tuple or existing test body changed. Do not implement or rewrite Story 3.2. New required extension cases must be accounted for as an explicit union with all original cases when auditing its actual CI; source inspection suggests 24 new registration cases, but that is **not yet an observed passing table**.

The exact new source was fetched read-only from the official remote into NEW P `eventstore-mainb132-incoming-owner-source-observation.git`. Canonical source and all closed roots were untouched.

New automatic main runs (attempt 1; no parent dispatch):

| Workflow | Run |
| --- | --- |
| CI | 42496430957 |
| Integration Tests | 42496430617 |
| CodeQL | 42496430947 |
| Payload Protection Lifecycle | 42496430557 |
| Payload Protection | 42496430596 |
| Payload Protection Azure | 42496430567 |
| Advisory Tests | 42496430817 |
| Commitlint | 42496430891 |

Live and compatibility jobs are respectively `128779734721` and `128779734925`. Several new runs were still active at stop. Obtain fresh actual API state when resuming. P `eventstore-mainb132-source-configuration.json` records all actual source/workflow/path bindings; `eventstore-mainb132-actual-primary/` holds the partial GET-only observation and terminal files saved so far. **Local watcher session 51212 / PID 131818 was stopped by SIGINT at the user's request, exit 130; this is not a GitHub cancellation.** Do not simply restart its script into the existing directory: it uses `mkdir(exist_ok=False)` and never overwrites completed captures. Use a new read-only observation root/labels after inspecting existing results.

Both b0ea integration archives were fully retained after the cancellation: compatibility artifact `13623627320` (8,028 members / 143,607,289 bytes), live cancellation artifact `13623453945` (18 members / 10,235 bytes). All member bytes and API digests verified; original compatibility 444 table exact. P `eventstore-mainb0ea-first-main-superseded-integration-primary/` and `parent-eventstore-mainb0ea-original-compat444-and-new-main-automatic-cancellation-20261108.json`. The live artifact is partial cancellation evidence and supplies no passing live/guard table or cleanup qualification.

## Parties manual campaign has NOT started

Actual workflow ID `406038512`, `.github/workflows/ci-lifecycle-qualification.yml`. Exactly three prior failed matrices `42400061797`, `42408333976`, `42418257508` are preserved. **Dispatch count 0, cancellation count 0 for the new campaign.**

After fresh actual qualified EventStore main (now b132 or a later independently reviewed successor), run one fourth matrix, then startup cancellation, then execution cancellation, strictly sequentially. The prepared P `dispatch_parties_main566_fourth_matrix_after_exact_main_proof_once.py` is **stale and NEVER EXECUTED**: it correctly refuses without b0ea full service proof and exact b0ea branch head. Prepare a new version against the actual qualified successor and its fresh primary receipts; do not weaken its prerequisite checks or replay old labels.

The source controller was parent-read completely: `scripts/qualify-disposable-ci.py`, 58,406 bytes, SHA-256 `7c1daf0f172b6ad77731cb9dc2de053d69df5f285a2012bc3223d30bf9b921e4`. Workflow SHA-256 `03de067657fff291ec5bd04fcafe455e0746db5fd255c331aa7c75f06594f792`. Whole tools manifest SHA-256 `31630169343fea0ee840559251631149540e8c5b5e2d6fe42abdae325d5c7550`. Module SHA-256 `d9c193b0e7b61601b8fcceb906b5dafe4769a1b9d7538a6ce164b4aa0840b348`. Keep baseline 60m, qualification 45m, hold 240s and readiness barrier 120s unchanged.

Dormant ready/cancel observer: `parent-final-native-and-manual-20261107/await-fresh-ready-and-request-one-cancellation.py` under the Story cache; SHA-256 `56e1a0ea504118a4fe3fad594633ecd1de1e320b6f24651ce5cdc320d8330afa`. EV `parent-lifecycle-ready-audit-procedure.py`, SHA-256 `bf2e51f720d522390e22a94e7814faf043ea27ef30be43c3f1b8abf4b7b38759`. Before ONE normal cancellation, download the exact ready artifact and require actual source/run/attempt/job/owner/PID birth/group/phase/active hold/whole tool manifest, fresh ready age at most 20s and unexpired original hold. Never infer readiness from an old artifact. Never replay an uncertain cancellation.

## Baseline audit progress and remaining work

Parent active source/config/docs audit: **350 exact text paths** fully reviewed, plus independently authenticated copied OCI binary fixture `b2ddfb18…` used only as the static wrong-role control. P `parent350-complete-active-path-acknowledgement-union-not-final-audit.json`. Current guide staleness is a tracked follow-up, not a no-findings claim.

Parent archived source/config/docs: **358 exact stable-queue units / 644 original contexts fully read and acknowledged**, plus separate 18 actual recorded HTML-response observations / 27 network-bound origins and all 50 unique inline CSS snippets fully read. Do not label whole HTML bodies as read. The stable 492-unit queue SHA-256 is `5ac0233ba7a632ebe91a09cea4078e4ee6dc7bbdde6212212067bbc1781d754c`, in `original-baseline-archival-role-refinement-after-bebc-20261108/evidence/stable-prepared-parent-confirmed-source-document-configuration-queue.json`.

Complete raw context acknowledgements cover indices 0–329 and 342–369. HTML typed indices are 330–341 and 413–418. Remaining confirmed-source indices begin **370–412, then 419–491** (116 units). P `prepare_archived_full_context_batch.py` and `acknowledge_archived_full_context_batch.py` preserve exact original blobs, containers, full patches and every path origin. No unfinished batch remains among the newly prepared ones: 342–350, 350–360, 360–370 are all complete. The 6277–6496 display window of the last batch was truncated once and reread fully before ACK. Never ACK truncated content.

The final Builds original-baseline diff at exact 3c is reconciled completely: `/tmp/story28-parent-final-original-builds-a22-3c809-rnqal_70.diff`, 3,776,861 bytes / 66,568 lines, SHA-256 `9db7bb5a87c02340a73eb954bf8191d34cb81a63b98b01ce21e01ba66e2f487d`. All 364 complete proper sections exactly map to 356 already fully read original-body sections plus eight fully read new source sections. Original 311-chunk raw body was reconstructed byte-exact; old chunk file locators remain unavailable, without invented reread claims. EV `parent-final-builds-original-a22-to-qualified3c-exact-complete-diff-reconciliation-20261108.json`; detailed P `parent-final-original-builds-a22-3c809-complete-diff-exact-read-origin-reconciliation.json`. No later producer edit may reuse this frozen-source result.

The last whole Platform baseline snapshot remains INTERIM: `/tmp/story28-parent-interim-original-baseline-zm92oys0.diff`, 422,978,926 bytes / 5,973,132 lines, SHA-256 `d06a52f3e4fad7026beec6494e7a4d5e72da306f9c7f9c7b97e7277577f89352`. It predates later incoming work and appends. Rewrite the complete proper untracked-inclusive original-baseline diff after final functional source integration; do not claim this snapshot is final.

Three historical 15.8 MB package-version policies remain configuration obligations, not blanket generated-output exemptions. The exact five-line historical producer policy change was already fully reviewed; whole policy bodies were not. A NEW pure preparation P `three-policy-exact-byte-subtree-preparation/` losslessly reconstructs all three originals from exact-byte subtree aliases (17,514 unique raw subtrees, 1,363,459 occurrence bindings). Its 13,184,201-byte context view is **NOT READ OR ACCEPTED**; no policy obligation is closed by it. Script `prepare_exact_raw_json_subtree_read_dictionary.py` was parent-authored/read and finished successfully, no source/runtime mutation. Keep this preparation honest; further representation/reading work is still needed.

The same sequential child completed its bounded 200 JSON role scope and is now OPEN/IDLE, zero active commands. Root: `/home/administrator/.cache/hexalith/story-2-8/original-api-observation-role-resolution-20261108`. Authoritative report `evidence/bounded200-authoritative-final-handoff-report.md`; index `complete-additive-body-index-authoritative.json`; checkpoint `OPEN-IDLE-checkpoint-authoritative.json`; seal `complete-seal-receipt-authoritative.json`. The compact authoritative report/checkpoint were parent-read at stop; **independent full 289-body seal verification and detailed report/decision-map review are pending**.

Its report: 200 bodies / 520,654 bytes / 289 origins, 196 public bodies read, four protected connectionToken gaps; 144 documentary observations, 56 full parent-context obligations including 11 unresolved-purpose/protected groups. Metadata support: 786 associations, 55 source bodies (53 authenticated, two path-only gaps), 45 source references; 81 browser origins (65 actual resource/attachment joins, 16 producer gaps). No complete original GitHub GET/stdout binding was invented. Correct body80 count is 37 package rows; body184 is 135 manifest entries. First seals are preserved with additive corrections. Frozen-cut270 queue count 1,224 is conservative; later parent reads are additive, not a universal closure. All earlier 315-body predecessor seals and gaps remain unchanged.

## Concrete documentation proposal, not applied

P `package-catalog-current13-opening-proposal.md`, 25,436 bytes / SHA-256 `2cb250e77640f8d3eb0d5b2acc8c7b675bd3468f3bedf18c850a7c2c16ed3939`. It adds the genuine Builds 4.44.13/public Parties 3.1/current412/original349 opening and explicitly preserves all old prose as history. Canonical old guide is 22,265 bytes / SHA-256 `55686ecebd6e3c6d144d69d0a4fbae7a78a287f8f6769d25a7c47f83a021882a`; the entire original body after its first heading is unchanged. Parent fully read the 3,650-byte proposal diff `/tmp/story28-parent-package-catalog-current13-opening-djml6y71.diff`, SHA-256 `ade59a175311d8749f210e442515ebe23265fb9dcf9c33b4d158f5fcb80de521`. P `parent-package-catalog-current13-opening-concrete-proposal.json`. Integrate this correction into the final COMMON functional source before real L/M; canonical guide is still untouched at stop.

## Actual remaining whole-story gates

1. Freshly inspect the actual successor EventStore main and all eight existing automatic runs. Complete all original/native/service/extra case and archive/source/cleanup audits on that exact source, accounting only for justified incoming added tests. Do not rerun superseded b0ea or borrow its passing source credit.
2. Fourth actual Parties matrix, then real startup cancellation, then real execution cancellation, strictly sequential and fully primary-audited. No dispatch/cancel has occurred for this campaign.
3. Final canonical functional common source Q preserving incoming a1eb and genuine published 0f4 ancestry, qualified E/P selections and all unrelated pins/work. Include final receipts/docs before the real validation cuts.
4. Create genuine descendant legacy L and metadata-only current M. Only the original eight paths may differ: composition `Declarations/provenance.json`, `eng/legacy-tuple-source.json`, `eng/builds-tuple.json`, `eng/tuple-ci-suites.json`, `.config/dotnet-tools.json`, `hexalith-platform.json`, `references/Hexalith.Builds`, `references/Hexalith.Parties`. Historical inputs come from actual `feb9fd3dab7a99a4c6657e1fff7c6c19a0348f7b`, original349 profile `61689f1598dac43a9fa28dcf5319008059e0fea0f1018209b1a8e79e4f63d7b0`, genuine Builds4.44.3/fc212/Partiesbb278. Descriptor must name the actual newly created L; every non-eight object/mode equal Q/L/M. Never relabel an old L or expand the boundary.
5. Actual final PR and main current412 (or justified meaningful union)/original349/all-five-SDK checks and genuine normal source publication. Any new CODEOWNER exception needs a new concrete exact-head user decision after checks, not PR #34's approval.
6. Finish approved complete original-baseline audit; verify all three original tasks/AC and all six frozen matrix rows with actual passing executions. Only then complete Step 03 and load Step 04, retaining the same single implementation role for required review fixes.

There is no active child scope or pending mutation to replay. No task is marked complete merely because the session stopped. Local monitoring is stopped; GitHub runs continue independently.
