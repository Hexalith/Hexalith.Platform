# PRD Quality Review — Hexalith Platform

## Overall verdict

**Adequate. The PRD can drive architecture sync and most story planning, but it is not yet ready for the FR-7/FR-8 promotion-stop and approval stories.** The update carries all five validation findings (S1, S2, A1, A2, S3) with the intended substance and keeps every ID stable. It also routes the un-adopted A1/A2 rules back to architecture (lines 340, 355). The core thesis, gates and module ownership still hold. However, the new rules for release authority and the promotion stop were packed into FR-8, which grew from about 400 to about 710 words. That leaves one common production path undecided: shipping a fix after an incident on a verified baseline. Its revisit condition cannot fire before the path is needed, and some new triggers and identity terms lack a bound or definition.

Scope: I reviewed the full current `prd.md` (397 lines) and `addendum.md` (355 lines) against the seven rubric dimensions. I read the decision log from line 89 onward and checked the carried degraded-path and stop rules against the architecture spine. Calibration: an internal developer/operator platform with a solo product owner, feeding architecture and stories, where capability/acceptance scenarios replace UJs by design. Counts: **0 critical, 1 high, 7 medium, 4 low.** No main document was edited.

## Decision-readiness — adequate

Most of the new decisions are stated as decisions and name what was given up. Examples:

- Two revocation exceptions are accepted, and the rule "Containing a person's production access through disaster recovery therefore requires revoking their production admission" is explicit (line 233).
- An approved incompatible release loses "state written after the point", and exceeding the approved duration "is reported but does not abort the recovery" (line 210).
- After disaster recovery "there is no emergency release path" (line 236).
- The attachment choice (line 136) picks one ordering and names it.

The approved empty/degraded path has a tight boundary: one attempt, only the observed stop lifted, and "a stop recorded afterward still blocks" (line 209).

The weak point is the case the update explicitly deferred. The definition, the deferral and the interim rule do not line up, and the deferral's trigger comes too late (see the high finding). Line 340 also says "The product rules above are settled" while line 352 holds an open rule about who may deploy what, and when, during a production incident. There is one more unstated trade-off: the deputy can act "whether or not Administrator is available" (line 34), but has no way to contain a person's production access.

### Findings

- **[high]** No forward-fix path after an incident on a verified baseline, and the revisit condition cannot fire (§ FR-7 line 188; FR-8 lines 208, 209, 214; Glossary line 385; Downstream line 352). A failed pre-update health check "sets the promotion stop" (188). "An incident establishing that production is no longer working sets or retains the promotion stop until Administrator records a verified baseline" (214). Clearing the stop needs "a verified current working release" (208), and the degraded path is open only to production "with no working release, or whose last outcome was non-working" (209, 385). Row 352 leaves open whether incident, probe and health-check stops qualify, and says "until then those stops clear only through the standard Administrator record". So when a defect needs a new release, nothing permits one: a new release is an approved attempt that FR-7's healthy-baseline check blocks, and the standard clearance needs a working release that no longer exists. Only a manual recovery that re-verifies the same release can clear the stop. The definitions also pull against each other. "No working release" (385) reads naturally as covering production that "is no longer working" (214), while the architecture uses "no working *baseline*". The revisit condition, "Before the first approved degraded-production attempt", never triggers for the undecided case, so G2 can open with the gap unresolved. *Fix:* keep the user's deferral to architecture, but (a) state the interim consequence in FR-8: until row 352 is decided, an incident stop on a verified baseline clears only by re-verifying the existing release through manual recovery, and no forward-fix release is permitted; (b) define "empty or degraded" in terms of the recorded baseline and the last *attempt* outcome; (c) move row 352's revisit condition to "before G2"; (d) soften line 340's "settled" to name row 352 as the one open rule.
- **[medium]** The deputy cannot contain a production user when Administrator is unavailable, and the PRD does not say so (§ Target users line 34; FR-8 line 208; FR-9 line 233; FR-11 line 263). The deputy exists so recovery works "whether or not Administrator is available". But "Administrator alone grants and revokes explicit production admission" (263), and admission revocation is now the only containment that survives restoration (233). The deputy's only lever is declaring a promotion stop, which blocks releases, not access. After disaster recovery the deputy may also reopen service while the lost module-owned revocations are merely "record[ed]". Administrator's review only gates clearing the stop, not reopening, and nothing requires the lost revocations to be re-established. *Fix:* state the trade-off next to the deputy role: while Administrator is unavailable, no one can revoke production access. Alternatively, give the deputy a narrow containment action, such as closing user ingress or suspending a principal pending Administrator review. Also say what Administrator's lost-window review must produce, for example re-issuing the known revocations.

## Substance over theater — strong

The additions carry weight rather than serving as furniture. The update brings in:

- tenant-lifecycle limits on staging checks and "Production smoke tests never create or delete tenants" (lines 166, 186)
- a time-bounded SM-4 test grant with a recorded denial check (lines 53, 56)
- CI evidence bound to the candidate's Release artifact and source revision (line 121)
- an explicit list of lost-window exceptions (line 233)

Each is a checkable behavior that names its actor. The NFRs keep product-specific numbers, and the document still refuses to invent an uptime or throughput target (line 358). No findings.

## Strategic coherence — strong

The thesis is unchanged and still drives the structure: one consistent composition from checkout to production, with modules owning behavior. The update deepens the production half without adding capability outside that thesis. SM-3, SM-4, SM-5 and SM-6 were extended so they test the new rules, not activity. Examples: the owner-before-attached-run scenario, grant expiry, an approved degraded attempt clearing "only the stop it observed", and denial of an admission revoked just before the failure (lines 325–328). SM-C4 now guards against the new attachment hold (line 335), and SM-C1 still states that "Approval cannot waive common gates" (line 332).

The document's weight has shifted: lines 153–317 on release, recovery and access are now clearly most of the requirements. That fits the risks the user chose to own. No findings.

## Done-ness clarity — adequate

Much of the new material is directly testable:

- CI evidence for another artifact or revision "is refused as candidate evidence" (121)
- "An accepted attachment defers but never skips that cleanup" (138)
- the SM-4 grant "expires no later than G2" (56)
- "Recovery never grants production admission" (232)

The gaps come from three new triggers and scopes that have no bound or owner, and from one clause the architecture phrases more precisely.

### Findings

- **[medium]** The new probe-failure stop trigger has no value and no owner (§ FR-8 line 207; addendum line 212). The stop is set by an "availability-probe failure beyond its declared bound". Nothing says what the bound is or who declares it. The downstream table (lines 344–356) has no row for it, and FR-9 fixes only the probe cadence ("at least every five minutes", line 231). The stop halts all promotion and needs an Administrator record to clear, so this bound decides how often the platform freezes itself. *Fix:* give an initial default, for example N consecutive failed probes or X minutes of unavailability outside an attempt window. Otherwise add the bound to the row at line 351 with Platform/Administrator as owner and "before G1" as the condition.
- **[medium]** Production changes that are not releases have no defined gate scope or failure path (§ FR-6 line 172; FR-8 line 213; Release scope line 48; addendum line 182). "Any other manual production change is an Administrator-approved attempt" (213). Approved attempts need "the same staging E2E gate, release identity … verification and recorded outcome" (48). As written, a credential rotation, a configuration tweak or an admission change would need a full staging E2E run. The addendum meanwhile lets "an urgent security patch … be applied in place" (182). Shared-infrastructure changes "start after a complete recovery point" and "a failed verification is a non-working outcome" (172). FR-8's automatic recovery covers only a "failed compatible production deployment", and NFR-1 says rollback does not rewind shared infrastructure (292), so nothing says how a failed shared change is recovered. The recovery point exists, but no requirement says it is used. *Fix:* in FR-8, list which production changes count as attempts, and exempt environment-current operations that advance independently (rotations, admission records). Carry the urgent-patch exception into the PRD. State that a failed shared-infrastructure verification enters in-place recovery or disaster recovery from the pre-change recovery point, and say which environments' stops it sets.
- **[low]** "If safe automatic rollback cannot be demonstrated" is ambiguous for pre-G3 releases (§ FR-6 line 171). Before G3, SM-5 has by definition not yet demonstrated rollback, so one reading makes every pre-G3 approval, including the first deployment, name a separate recovery procedure. FR-8 (line 200) and the architecture instead key this on compatibility evidence. *Fix:* write "If compatibility evidence is missing, failing or breaking, approval names …".
- **[low]** The attachment hold has no local bound (§ FR-4 line 136). Automatic cleanup "waits until every attached run has ended", with no maximum hold. A hung attached run can keep a successful owner's local environment alive indefinitely. CI runner timeouts bound it only in CI. *Fix:* add "attached runs are subject to the owner's configured finite hold limit, after which they are ended and reported". Alternatively, state that the retained-environment listing (line 135) is the accepted mitigation.

## Scope honesty — strong

The update is candid about what is borrowed and what is still pending. Line 340 says the architecture's third-run revision "awaits a confirmation review" and that the FR-4 CI and attachment rules are "PRD requirements the architecture has not yet adopted". Row 356 lists the carried third-run decisions that must be re-checked. The accepted losses are enumerated rather than implied (lines 233–234, 304). The downstream table still keeps requirements separate from qualification evidence, with named owners. One post-disaster limitation is still left for the reader to infer.

### Findings

- **[medium]** The reduced-recovery state does not say what it means for the RTO commitment (§ FR-9 line 236; NFR-2 lines 305–309; Glossary line 386). After a verified disaster restore, production runs "in a recorded reduced-recovery state until new prepared replacement capacity is identified". The glossary defines only how the state starts and ends. During that state the prepared capacity has been consumed, so a second major failure cannot meet the four-hour RTO. NFR-2 still states the RTO without that qualifier, and says only that "Whole-site loss is covered only when exercised replacement capacity is at an independent location". *Fix:* add to FR-9 and NFR-2 that no four-hour RTO commitment applies while production is in the reduced-recovery state, and that incidents in that state record their full duration as outside the commitment. Update the glossary entry with that consequence.
- **[low]** Decisions pending the architecture confirmation review are not flagged where they are used (§ lines 56, 207, 209, 233; line 356). Row 356 correctly lists "admission records, access-removal exceptions, the SM-4 grant expiry, admission-drift stops and approved-attempt behavior" as contingent. A reader or story author who extracts FR-8, FR-9 or the G1 paragraph alone will see them as settled. *Fix:* add a short inline marker, such as "(pending architecture run-3 confirmation; see Downstream decisions)", to each carried clause. Or state once, under Release scope, which subsections carry pending decisions.

## Downstream usability — adequate

IDs are stable and contiguous. The new glossary entries ("Empty or degraded production", "Reduced-recovery state") give story authors anchors, and each approval path is cross-referenced by FR number. Extraction is harder than before the update in two places. FR-8 now spans several capabilities under a title that describes only one of them. The synthetic-identity rules also use several near-synonyms for identities that have different lifetimes.

### Findings

- **[medium]** FR-8 has become an omnibus requirement (§ FR-8 lines 198–214). The title is "Restore and verify the previous working release", but its eleven consequence bullets (about 710 words, up from about 400) define:
  - automatic recovery
  - notification
  - the full promotion-stop lifecycle, both setting and clearing it
  - the approved empty/degraded release path
  - the approved incompatible-release path and its data restore
  - first deployment
  - manual-change and baseline policy
  - incidents

  FR-6 (171), FR-7 (188) and NFR-1 (296) all point into it. A story set extracted from "FR-8" gets release-authorization and stop-governance rules mixed in with rollback. The two approval bullets (209, 210) are the densest sentences in the document. *Fix:* keep the existing IDs and append new ones, for example **FR-13 Promotion stop** (lines 207–208, 214) and **FR-14 Administrator-approved production attempts** (lines 209, 210, 212, 213). Leave FR-8 as automatic and failed recovery. If no new IDs are wanted, at least add FR-4-style bold subgroups ("Automatic recovery", "Promotion stop", "Approved attempts", "Baseline and incidents").
- **[medium]** The PRD blurs which synthetic identities exist and why one must be revoked by G2 (§ G1 line 52; G2 line 53; line 56; FR-7 line 186; SM-4 line 326). G1 has standing "Synthetic check identities" with "separate Administrator-granted access limited to synthetic test data". Line 56 then admits "a designated synthetic production test identity, with production permissions limited to synthetic test data", which must expire by G2. SM-4 calls this "a controlled explicitly admitted test user", and FR-7 uses "dedicated synthetic identities". The PRD never says what distinguishes the temporary identity: it goes through the *human* production-admission path to prove FR-11's positive case, which is why G1 requires that group to be empty and G2 requires the grant to be revoked. The addendum and architecture say so (addendum lines 186, 190). Without that, an implementer could satisfy SM-4 with the standing synthetic-check access, which proves nothing about human admission, or could revoke the standing smoke identities at G2. *Fix:* name the two roles once, for example "synthetic check identities (standing, separate admission group)" and "SM-4 admission test identity (temporary member of the human production-admission group)". Use those names at lines 52, 56, 186 and 326, and add both to the glossary.

## Shape fit — adequate

The capability-and-acceptance shape still fits a single-operator internal platform, and line 36 justifies leaving out UJs. The update's instruction was to carry D-1..D-7 "at capability level" (`.memlog.md` line 96). Several carried clauses restate architecture mechanism instead, for example "Staging rehearses a fresh install plus the candidate" and "removes the candidate's workloads while keeping data objects" (lines 209, 210). This mechanism is a main reason the document became harder to read. It also couples the PRD tightly to an architecture revision that is still under review.

### Findings

- **[medium]** The PRD restates architecture mechanism, so every architecture change needs a PRD sync (§ FR-8 lines 209, 210; FR-9 line 232; Downstream line 356). The degraded-production bullet closely follows the spine's "Empty or degraded production" row minus its compare-and-set wording. The incompatible-release bullet specifies the executor's order: set the stop, close ingress, remove workloads, stop. FR-9 specifies the restore sequence ("fences … quarantine … rotate … reconcile"). Row 356 already has to list five carried items to re-check if the pending review changes them, and this is the second full PRD sync in two days. Stories built from PRD text will encode mechanism that the architecture may still revise. *Fix:* in these bullets, keep the product invariants: who may authorize, the one-attempt and observed-stop boundary, that data is never destroyed, that user ingress is closed on failure, and what must be verified. Move the step order and the rehearsal/removal mechanics to the addendum's "Release modes and production entry" section, with a link. This also shortens FR-8 (see the FR-8 omnibus finding under Downstream usability).
- **[low]** "Earlier releases" in the Vision can be misread as rollback to older versions (§ Vision line 26). "An Administrator-approved path supports earlier releases, incompatible releases …" is meant to mean releases before G3. Read cold, "earlier releases" suggests older versions, which FR-8 forbids cycling through (211, 214). *Fix:* write "releases before automatic promotion is qualified (G3)".

## Mechanical notes

- **IDs:** FR-1–FR-12, NFR-1–NFR-3, SM-1–SM-6 and SM-C1–SM-C5 are unique and contiguous. Every in-text ID reference resolves.
- **Broken link:** line 16 links to `update-2026-09-28/update-summary.md`, which does not exist yet. It is presumably written at finalize close, so confirm before closing. All other local file links resolve.
- **Glossary drift, promotion stop:** the "Promotion stop" entry (line 384) lists "disaster recovery" and omits automatic and in-place recovery entries and probe failures. FR-8 line 207 says "every recovery entry (automatic, in-place or disaster recovery)" and includes probe failures. The addendum (line 212) still says "disaster recovery entry". Align both with FR-8.
- **Glossary drift, recovery owner:** the "Recovery owner" entry (line 388) means Administrator alone. FR-9 line 222 ("The recovery owner can restore production") and FR-8 line 211 ("reports failed or unverified recovery to the recovery owner") use the term for actions and notifications the deputy also performs and receives (lines 34, 206). Write "Administrator and the deputy", or broaden the definition.
- **Missing glossary terms:** "Production admission" and the "human production-admission group" appear 13 times but have no glossary entry. "Production user" (line 397) is defined through "declared", and FR-11 mixes "production-user declaration" (260) with "production admission" (263). Pick one term and add it to the glossary.
- **Minor synonym:** "Recovery set" (lines 229, 328) and "Recovery point" (glossary line 390) are used interchangeably. This is harmless, but the glossary could note it.
- **Addendum drift:** addendum line 177 lists "SM-5 fault rehearsals before G2" as an approved-mode use. PRD line 48 names three uses; only the G3 row (line 54) mentions "controlled rehearsals".
- **Tags:** there are no `[ASSUMPTION]` or `[NOTE FOR PM]` tags, so an Assumptions Index roundtrip does not apply. The open items live in the downstream table.
- **UJs:** none, by design (line 36). No protagonist checks apply.
- **Size:** the PRD grew from about 7,300 to about 8,500 words (+17%), and most of the growth is in FR-8, FR-9 and the gates. Frontmatter is `status: draft`, updated 2026-09-28, which matches the pending-finalize state in the decision log.
