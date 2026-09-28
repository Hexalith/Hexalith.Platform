# Reconciliation: 2026-09-28 validation report against the updated PRD and addendum

- **Input:** `validation-report.md` (5 findings: S1, S2 High; A1, A2, S3 Medium), with reviewer detail in `validate-2026-09-28/review-recovery-access.md` and `validate-2026-09-28/review-adversarial.md`
- **Updated documents:** `prd.md` (396 lines) and `addendum.md` (354 lines), both `status: draft`, `updated: 2026-09-28`
- **User decisions:** `.memlog.md:89–99`, from "PRD Update invoked with the 2026-09-28 validation report" onward
- **Cross-checked against:** `ARCHITECTURE-SPINE.md:280–296` (release modes, empty/degraded production, promotion stop, after-DR posture) and `update-2026-09-28/extract-recovery-access.md` / `extract-ci-attachment-drift.md`
- **Method:** each finding was judged against its proposed fix and the user's recorded decision. `git diff` of both documents located every changed sentence. Both documents were then grepped for revocation, healthy-baseline, working-baseline, promotion-stop, first-deployment, cleanup/attachment, operations-writer and deputy wording to find any contradictions the fixes introduced. The local link targets were also checked. The PRD and addendum were not edited.

All line numbers are 1-based and refer to the current files.

## Verdict summary

| ID | Severity | Verdict | Basis |
| --- | --- | --- | --- |
| S1 | High | **RESOLVED** | FR-9, NFR-2, NFR-3, SM-6 and the addendum now carry the admission-survives rule, the two named lost-window exceptions, reporting before reopening and Administrator review before the stop is cleared. One addendum sentence has weaker wording (gap 3). |
| S2 | High | **RESOLVED** | The release modes, FR-6, FR-7, FR-8, SM-5, the glossary and the addendum carry the one-attempt empty/degraded approval with its observed-stop boundary, newer-stop precedence, common gates and verified-success rule. Two wording seams remain (gaps 1 and 2). |
| A1 | Medium | **RESOLVED** | FR-4, SM-3, the addendum's AD-4 paragraph and the downstream table bind CI evidence to the candidate's Release artifact and source revision, refuse other artifacts or revisions, and label baseline-only runs. |
| A2 | Medium | **RESOLVED** (per the user's chosen option) | FR-4, SM-3, SM-C4 and the addendum state that an accepted attachment holds the owner's environment and that an explicit developer stop ends and reports attached runs. Failed-local retention is preserved and CI cleanup is deferred, never skipped. The rule for a cancelled owner is still ambiguous (gap 4). |
| S3 | Medium | **RESOLVED** | The addendum now names two operations writers, triggers review only beyond them, gives the deputy recovery without repository write, and moves deputy identity/custody/alerts/rehearsal to a G2 proof. The obsolete "architecture does not yet implement" text and the PRD's architecture-alignment row are gone. |

No finding is PARTIAL or UNRESOLVED on the substance of its fix. The five gaps below are small seams the fixes introduced or left behind. None reverses a user decision.

---

## S1 — Restoration revocation guarantees (High) → RESOLVED

**Fix required (validation-report.md:85):** synchronize FR-9, NFR-2/NFR-3 and the addendum so that production admission revocations survive restoration and the named module and realm-removal exceptions use their own lost windows and reporting/review boundaries. Preserve Memories continuity, live authorization and current-security application rollback.

**User decision (.memlog.md:92):** admission revocations always survive and recovery never grants admission. Module-owned revocations after the cut and non-admission identity-provider removals not yet captured off the failed environment are accepted exceptions. They are recorded in the recovery report before reopening and reviewed by Administrator before the stop is cleared.

| Requirement element | Where it now appears |
| --- | --- |
| Admission reconciled to Administrator's recorded grants/revocations; recovery never grants admission | prd.md:232; addendum.md:277, :279 |
| Admission revocations always survive, including one made just before the failure | prd.md:233; NFR-3 prd.md:317; SM-6 prd.md:328; addendum.md:257, :279 |
| Exception 1: module-owned revocations acknowledged after the recovery-point cut | prd.md:233; addendum.md:279, :352 |
| Exception 2: non-admission identity-provider removals not yet captured outside the failed environment | prd.md:233; addendum.md:279 ("after the durable event-export frontier"), :352 |
| Containment that must survive DR is made as an admission revocation | prd.md:233; addendum.md:279 |
| Exceptions recorded in the recovery report before reopening; Administrator reviews before clearing the stop | prd.md:233; addendum.md:277, :279 |
| "Reapply other revocations" limited to those captured outside the failed environment (replaces "reapply post-cut revocations") | prd.md:232 |
| NFR-2 RPO row now references admission protections and the listed exceptions | prd.md:304 |
| NFR-2 closing states FR-9 lists the only accepted revocation losses and preserves live authorization | prd.md:311 |
| Memories continuity unchanged | prd.md:234; addendum.md:275, :279 |
| Application rollback still preserves revocations and current security authority | NFR-1 prd.md:292 |
| SM-6 acceptance includes the denial check and recording of the exceptions | prd.md:328 |

**Contradiction sweep.** No remaining sentence in the PRD promises that *all* revocations are reapplied or survive restoration. The old phrases "reapply post-cut revocations" and "denial of revoked principals" are gone from both documents. prd.md:292 applies only to application rollback, and prd.md:296 separates the incompatible-release data restore from rollback. Residual: addendum.md:277 says "reapply revocations captured before the failure", which is weaker than prd.md:232 (gap 3).

## S2 — Administrator-approved release into degraded production (High) → RESOLVED

**Fix required (validation-report.md:91):** carry the approved degraded-production use case into the release modes, FR-7/FR-8, the addendum and SM-5. State the one-attempt authorization, the observed-stop boundary, newer-stop precedence, unchanged common gates and the verified-success requirement. Preserve Administrator-only release authorization and separate deputy recovery authority.

**User decision (.memlog.md:93):** for empty or degraded production, Administrator alone may approve one identified attempt. It lifts only the observed stop and the healthy-baseline precondition. A later stop still blocks and the common gates apply. A verified success clears the observed stop and becomes the working baseline. The path is not an emergency route after DR. Whether incident-only stops count as degraded stays with architecture.

| Requirement element | Where it now appears |
| --- | --- |
| Release modes include "one identified attempt … into empty or degraded production" | prd.md:26, :48; FR-6 prd.md:171; addendum.md:177 |
| Healthy-baseline precondition carved out for this path | FR-7 prd.md:188; addendum.md:197; glossary prd.md:384 |
| One attempt, Administrator alone, names reason and observed stop state | prd.md:209; addendum.md:180 |
| Lifts only the observed stop and the healthy-baseline check; a later stop still blocks | prd.md:209; addendum.md:180, :211; glossary prd.md:383 |
| Every FR-6 gate and FR-7 verification still applies; staging rehearses fresh install plus candidate | prd.md:209, :171; addendum.md:177, :180 |
| Failure removes candidate workloads, keeps data, sets a new stop; verified success clears the observed stop and becomes the baseline | prd.md:209, :213; addendum.md:180 |
| Not an emergency release path after DR | prd.md:209, :236; addendum.md:180 |
| Incident/probe-stop qualification deferred to architecture | prd.md:352 |
| First deployment runs as an approved empty-production attempt | prd.md:188, :212; addendum.md:180 |
| SM-5: clears only the observed stop, a later stop blocks, deputy cannot approve | prd.md:327 |
| Deputy boundaries preserved | prd.md:34, :208, glossary :388; addendum.md:211, :222, :229, :346 |

**Contradiction sweep.**

- *Healthy baseline without exception:* no unqualified requirement remains. prd.md:188 and addendum.md:197 both now route to the approved path.
- *First deployment:* prd.md:188, :212 and addendum.md:180 agree that the first deployment uses the empty-production approval. addendum.md:212 ("keep ingress closed, report the bootstrap failure and stop") and prd.md:209 ("failure removes the candidate's workloads … sets a new stop") can both apply, and spine :281 and :287 state the same pair.
- *"Only an Administrator record clears the stop":* the exception is explicit in addendum.md:211 and the glossary at prd.md:383. prd.md:208 relies on the next bullet (:209) rather than naming the exception, which is minor.

Two seams remain: gaps 1 and 2.

## A1 — CI candidate-artifact binding (Medium) → RESOLVED

**Fix required (validation-report.md:99):** CI acceptance for a candidate must identify and exercise the module's Release artifact from the candidate revision and reject evidence for another artifact or revision. Label baseline-only runs. Leave package mechanics to Platform/Builds.

**User decision (.memlog.md:95):** applied as stated.

- FR-4, prd.md:121: identifies the Release artifact and its source revision, exercises that artifact, refuses evidence for a different artifact or revision, and labels baseline-only runs so they cannot satisfy candidate acceptance.
- SM-3, prd.md:325: the acceptance measure repeats the identification and refusal.
- addendum.md:34: the module under test builds in Release against NuGet library packages. Its CI evidence identifies that artifact and the candidate revision, and baseline-only runs are labelled. The Platform-tool identity is kept separate from module-candidate identity, which was the gap the reviewer identified.
- prd.md:347: the downstream row assigns "CI candidate-artifact identity" to Platform with Builds and module maintainers. Mechanics stay out of the PRD.

**Contradiction sweep:** clean. FR-2 prd.md:90 and FR-4 prd.md:120 still state the general CI Release/NuGet rule, which is consistent with the new binding.

## A2 — Attachment when the environment owner finishes (Medium) → RESOLVED per the user's option

**Fix required (validation-report.md:105):** either preserve the environment until attached work ends, or terminate and report attached runs before cleanup, including failed-local retention. Cover the ordering in SM-3.

**User decision (.memlog.md:94), option 1:** an accepted attachment holds the owner's environment. The owner's first terminal outcome is recorded immediately, but automatic cleanup waits until attached runs end. An explicit developer stop terminates and reports attached runs before cleanup. Failed-local retention is unchanged. This combines both reviewer alternatives: hold for automatic cleanup, end-and-report for an explicit stop. It is a valid resolution.

| Requirement element | Where it now appears |
| --- | --- |
| Attachment holds the owner's environment; first terminal outcome recorded immediately; automatic cleanup waits for all attached runs | prd.md:136; addendum.md:54, :105 |
| An attached run's cancellation never stops the owner's environment | prd.md:136; addendum.md:105 |
| Explicit developer stop ends and reports attached runs before cleanup | prd.md:136; addendum.md:54, :105 |
| Failed owner stays retained under the failed-local rule | prd.md:136, :134–135 |
| Retained environments show attached runs | prd.md:135 |
| CI cleanup deferred but never skipped | prd.md:138 |
| SM-3 covers an owner finishing before its attached run | prd.md:325 |
| SM-C4 forbids automatic cleanup of an environment still serving an attached run | prd.md:335 |
| Downstream qualification of attachment lifetime | prd.md:347 |

**Contradiction sweep for CI cleanup against deferred cleanup.** prd.md:108 ("CI environments are automatically cleaned up after completion or cancellation") and prd.md:138 are consistent: cleanup still happens, only later. In the addendum, the CI rows at addendum.md:50, :93 and :106 state cleanup without the deferral, but the attachment row at addendum.md:105 explicitly covers "local or CI" environments. This is a readability point, not a contradiction. The case that remains ambiguous is owner *cancellation* (gap 4).

## S3 — Operations writers and deputy controls (Medium) → RESOLVED

**Fix required (validation-report.md:111):** refresh the summary to two named writers with the revised review trigger. Replace the obsolete architecture follow-up with the remaining implementation and qualification of deputy identity, custody, alert delivery and rehearsed recovery. Retain spec synchronization and open evidence gates.

**User decision (.memlog.md:95):** applied as stated.

- Two named writers, Administrator and the second organization owner, with the org base permission set to read or none; the either-writer risk is accepted: addendum.md:330, with the accepted risk at :352.
- The operations repository's "only writers are the named writers": addendum.md:329.
- GitHub Team controls are adopted "once anyone beyond the named writers gains write or admin": addendum.md:330.
- The deputy needs no repository write and recovers under their own MFA identity without a GitHub dependency: addendum.md:229, :330; PRD user role prd.md:34.
- "The accepted architecture implements it" replaces "architecture follow-up is required … do not yet implement": addendum.md:229.
- The G2 proof covers deputy identity, minimum permissions, key custody, alert delivery and rehearsed restore/reopen: addendum.md:229, :346; PRD G2 prd.md:53 and downstream row prd.md:353.
- The PRD downstream row "Align architecture operational access and notification recipients with the selected deputy role" was removed. Spec synchronization of deputy authority is kept in prd.md:355.

**Contradiction sweep:** grep finds no "single-writer", "second writer" or "do/does not yet implement" in either document. The older memlog entry at .memlog.md:82 still mentions "deputy … architecture alignment", but it is a historical log entry, not document text.

---

## Gaps and contradictions found

1. **The failed pre-update health check is missing from the deferred degraded-path question, and the addendum appears to answer it.** addendum.md:197 says an unhealthy production environment "stops the update … and sets the promotion stop, unless Administrator approves one attempt under the empty or degraded production rule". prd.md:188 juxtaposes the failed health check with the approved path in the same way. This reads as if any unhealthy baseline qualifies. The definition, however, is "no working release, or whose last outcome was non-working" (prd.md:209, :384; spine :281), and prd.md:352 defers only "stops raised by incidents or probe failures on a verified baseline" to architecture. The failed pre-update health check is the third ambiguous case that extract-recovery-access.md:200 flagged, and the deferral row omits it. Suggested fix: add it to prd.md:352, and change addendum.md:197 to say "unless the empty or degraded production rule applies".
2. **The FR-8 lead-ins still prescribe automatic rollback for every failed compatible deployment.** prd.md:178 and prd.md:200 (and addendum.md:209) require one automatic recovery to the previous working application after any failed compatible deployment. They carve out only approved incompatible releases. The approved empty/degraded attempt instead uses "remove the candidate's workloads, keep data, set a new stop" (prd.md:209; addendum.md:180; spine :281, whose rollback set is "remove workloads, keep data"). For a compatible candidate into degraded non-empty production, both rules literally apply. prd.md:352 defers compatibility and named-recovery evidence for that case, but the lead-ins do not point to that deferral. Also, prd.md:208 says "Only an authenticated Administrator record … clears the stop" without the "only exception" pointer that addendum.md:211 has.
3. **The addendum's recovery order uses a weaker revocation qualifier than the PRD.** addendum.md:277 says "reapply revocations captured before the failure". Read literally, that means every pre-failure revocation, which conflicts with the two accepted exceptions at addendum.md:279 and prd.md:233. prd.md:232 correctly says "captured outside the failed environment". The drill wording at addendum.md:257, "including a revocation made before event export", is also easy to misread. It means an admission revocation not yet exported, which should be said explicitly.
4. **A cancelled owner with attached runs has no stated outcome.** prd.md:137 and addendum.md:104, :136 still say explicit cancellation of a local run "cleans up resources it created", with no attachment qualifier. prd.md:136 does not say whether cancelling the owner counts as an "explicit developer stop" (end and report attached runs) or as a terminal outcome whose "automatic cleanup waits". Option 1 in the extract (extract-ci-attachment-drift.md:154) intended "a successful or cancelled owner cleans once the last attached run ends". The reviewer named this case explicitly (review-adversarial.md:29). SM-3 at prd.md:325 covers only "an owner finishing before its attached run".
5. **Document-state claims run ahead of the evidence.** prd.md:16 links `update-2026-09-28/update-summary.md`, which does not exist yet; the directory holds only the two extracts and memlog-audit.md. It is the only missing local link target. Separately, prd.md:340 says the architecture's "passing handoff review establishes document readiness". But addendum.md:10 and prd.md:355 note that the third update run's confirmation review is pending (spine `status: draft`), and the S1 and S2 text rests on its VAL-04, VAL-08 and G-4 decisions. Write the update summary during finalize, and qualify prd.md:340 or keep the prd.md:355 re-check as the controlling caveat.

## Clean checks (no contradiction found)

- No surviving promise that all revocations are reapplied or survive DR. NFR-1's revocation clause (prd.md:292) is limited to application rollback.
- No surviving unconditional healthy-baseline requirement. Both prd.md:188 and addendum.md:197 carry the carve-out.
- No "single-writer" or "second writer" wording, and no statement that the architecture lacks deputy controls.
- CI cleanup wording (prd.md:108, :138; addendum.md:50, :93, :106) is consistent with deferred-but-not-skipped cleanup.
- First-deployment wording (prd.md:188, :212; addendum.md:180, :212) is consistent with the approved empty-production path.
- The post-DR reduced-recovery state is kept distinct from degraded production (prd.md:209, :236, :385; addendum.md:180; spine :296).
- The deputy cannot approve, clear or administer admission in any of prd.md:34, :208, :327, :388 or addendum.md:211, :222, :229, :346.
- All requirement IDs FR-1–FR-12, NFR-1–NFR-3, SM-1–SM-6 and SM-C1–SM-C5 are still present.
