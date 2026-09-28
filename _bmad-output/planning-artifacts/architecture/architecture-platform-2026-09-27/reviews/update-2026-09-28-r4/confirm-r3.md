# Confirmation of update run 3

**Verdict: PASS WITH WORDING FIXES.** The accepted G-1–G-5 decisions and their recovery, admission and fencing autofixes have landed. Four small consistency corrections remain; none requires a new architecture decision. They concern older general rules or an omitted notification verb, not a reversal of the accepted design.

Reviewed 2026-09-28. Authority: architecture `.memlog.md` entries at lines 243–261, especially G-1–G-5 and the accepted gate-autofix entry; the current 487-line spine; `update-2026-09-28-r3/gatefix.diff`; run-3 recovery, security, adversarial and rubric reviews. Scope is recovery execution, fencing/timing, admission authority and catalog recovery. New PRD changes, accepted risks, implementation evidence already assigned to owners, and the later full reviewer gate are outside this confirmation. Only this report was written.

## Accepted decisions confirmed

| Decision | Confirmation in the current spine |
| --- | --- |
| G-1 recovery execution | In-place entry distinguishes baseline re-deploy from data restore; failed data restore re-enters from the same point (L282). The common sequence removes workloads and post-cut broker/Scheduler state, checks release validity, preserves live keys in place, restores data before starting workloads, scopes credentials and surviving-authority proof, verifies the normal render and reopens within the attempt (L314–L322). Chart modes and attempt binding exist (L68, L226). A failed incompatible release removes candidate workloads (L280). Staging removes the reset candidate's obsolete environment-layer objects before later evidence (L279). |
| G-2 catalog recovery | Step 4 prepares a forward generation from the recovery point's committed generation, retains later idempotency/key entries as non-executable entries and commits at readiness (L319). Attempt records capture the generation through their environment-current values (L226). Baseline re-deploy uses the prepared rollback set when present (L282). EventStore confirmation remains an owned G3 gate. CF3-1 fixes older general wording. |
| G-3 fencing and credentials | Mutation credentials are per job, revocable and issued only to the current epoch; GitHub OIDC is not a Kubernetes bearer (L128). Takeover prevents re-minting, distinguishes shared credentials and authorities already fenced, and requires drain or authority checks (L283). Hook credentials bind attempt and epoch; tasks check epoch at commit (L319). The Kubernetes drain stages are in Owned work as the accepted seed. CF3-2/3 remove two wording ambiguities. |
| G-4 admission authority | Signed record-first grants/revocations, create-only chained records, retention, gap stop and live-authority-only admission projection are explicit (L118). Step 5 rebuilds from the reconciled or live realm, and step 6 tests denial through the gateway and asynchronous re-check (L320–L321). Recovery blocks actor-bearing asynchronous work before the rebuild (L314). Monitor matching, G1 expiry/G2 revocation evidence and the non-admission lost-window exception landed (L236, L289, L295, Accepted risks). CF3-4 restores the explicit mismatch notification. |
| G-5 contract versions | Release records bind the hook-contract version; both recovery entry paths retain support for all versions used by a rollback target or retained recovery-point release (L79, L185). |

## Corrections

### CF3-1 — General recovery rules still describe only rollback or replacement-capacity DR

**Medium · autofix · high confidence.**

AD-3 L89 says all “Application recovery” renders an AD-15 rollback set; Catalogs L227 ends “Recovery commits the rollback generation.” Those unqualified rules conflict with G-2's data-restore generation in step 4. AD-12 L183 also says the newly shared Recovery sequence is run by the recovery executor, although G-1 and L314 assign in-place restores to the owning staging or production executor. Teams implementing the general rules can still select the old generation or the wrong executor.

Exact suggested amendments:

- AD-3 L89: replace “Application recovery renders the prepared AD-15 rollback set through a Helm upgrade and commits its rollback generation at readiness.” with “Automatic application recovery renders the prepared AD-15 rollback set through a Helm upgrade and commits its rollback generation at readiness. Baseline re-deploy follows In-place recovery; data restore follows Recovery sequence step 4.”
- Catalogs L227: replace “Recovery commits the rollback generation.” with “Automatic recovery commits the prepared rollback generation; baseline re-deploy follows In-place recovery, and data restore commits the forward recovery generation in Recovery sequence step 4.”
- AD-12 L183: replace “Platform owns the exercised Recovery sequence, run by the AD-7 recovery executor” with “Platform owns the exercised Recovery sequence, run by the executor owning the attempt under AD-7”.

These are cross-reference repairs to G-1/G-2, not new behavior.

### CF3-2 — Timing text retains three overbroad phrases

**Medium · autofix · high confidence.**

L284 now distinguishes kinds correctly, but “a maximum lifetime anchored at the outage where one applies” can anchor an outside-coverage DR or named in-place recovery at an old outage. The accepted RB3-13 fix instead gives outside-coverage DR an entry-time anchor. “Maximum lifetimes derive from these deadlines” follows the explicitly fixed recovery-kind lifetime and can overwrite it with phase deadlines. Finally, “inherits every recorded value” includes the old lock epoch under L226, whereas every takeover increments that epoch under L283.

Exact suggested amendments within L284:

- Replace the recovery-kind lifetime sentence with: “**Recovery-kind** attempts — in-place data restore and DR — record their entry time, any original outage timestamp and an absolute maximum-lifetime deadline: for DR when the outage began within coverage, outage time plus four hours; for DR otherwise, entry time plus the last drill's measured duration, or plus four hours before the first drill; for an approved release's named recovery, entry time plus the duration its Administrator record states; for a staging reset, entry time plus the declared staging bound.”
- Replace “Maximum lifetimes derive from these deadlines.” with “Release-kind and infrastructure-kind maximum lifetimes derive from their applicable phase deadlines and grace; recovery-kind phase deadlines do not replace their recorded maximum-lifetime deadline.”
- Replace “A replacement job resuming the same attempt inherits every recorded value” with “A replacement job resuming the same attempt inherits its recorded kind, timing values, grace and consumed recovery allowance, and records the new lock epoch”.
- Replace “Timers never restart” with “Resuming the same attempt never restarts its timers”. The adjacent accepted new-attempt and late re-entry rules then remain unambiguous.

### CF3-3 — Define acceptance after the takeover fence has completed

**Medium · autofix · medium confidence.**

L283 forbids an older in-flight mutation from committing “once a takeover is accepted”, but its revoke-and-drain path is required only before the new owner's first mutation. If accepting takeover means acquiring the next epoch, an already authenticated request can finish during the required drain after acceptance. That violates the invariant even when the implementation follows the prescribed mechanism. This is a terminology/ordering gap; the accepted revoke-and-drain choice already supplies the fix.

Exact suggested amendment at L283: replace “Once a takeover is accepted, no older-epoch mutation, including one in flight, may commit at any mutation authority. Before its first mutation at each authority the new owner” with “After reserving the new epoch, the takeover becomes accepted for mutation only when every affected authority has been fenced or drained as below; after acceptance no older-epoch mutation, including one in flight, may commit. For each affected authority the new owner”. Keep the existing authority-side check, revoke/no-reissue/drain, fresh-read, exceptions and intervention clauses. Reserving an epoch must still permit the control actions needed to revoke and drain; it does not authorize the release/recovery mutation.

This preserves both G-3 and the stricter VAL-02 invariant. If the record encoding already defines acceptance this way, a direct reference to that definition is sufficient.

### CF3-4 — Admission drift sets the stop but loses the explicit notification

**Low · autofix · high confidence.**

G-4 requires an unmatched exported admission-group change to notify and set the promotion stop. L236 explicitly sets the stop but does not explicitly notify. The paragraph defines the notification transport; it does not say this particular mismatch emits one.

Exact suggested amendment at L236: replace “setting the promotion stop on a mismatch” with “notifying Administrator and the deputy and setting the promotion stop on a mismatch”.

## Decision requests

**None.** All suggested amendments follow accepted run-3 decisions. Do not reopen the live-authority admission choice, record-first scope, accepted lost-window risks, takeover credential mechanism, in-place recovery scope or hook-version window.

## Confirmation boundary

This confirms the documented contract, not deployed readiness. EventStore protocol confirmations, recovery-hook implementation, fencing proof, record encoding, custody and the drills remain the explicitly owned gates. The full gate after the parent's PRD reconciliation remains necessary.
