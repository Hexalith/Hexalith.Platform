# Recovery and operability review — update run 4

**Verdict: PASS WITH ONE REQUIRED CORRECTION.** The accepted recovery branches, forward catalog generation, takeover fencing, lifetime anchors, restore admission and ingress rules are coherent. One unconditional key-coverage clause conflicts with the required preservation of an erasure acknowledged after the recovery cut.

**Counts:** 0 critical, 1 high, 0 medium, 0 low. **Disposition:** 1 autofix; no decision request.

Reviewed 2026-09-28 against the current update-4 `ARCHITECTURE-SPINE.md`, architecture `.memlog.md` entries 255–272, `confirm-r3.md`, and the finalized Platform PRD FR-7–FR-11, NFR-1–NFR-3 and SM-5/SM-6. Line references below identify the reviewed spine. The parent’s first-empty-install, exception-path catalog and per-environment stop corrections are included in this review. Only this report was written; no source document, decision log, code or runtime was changed.

## Required correction

### REC4-1 — An erased tenant’s deliberately missing key blocks the mandated named recovery

**High · autofix · high confidence.**

**Evidence.** Recovery sequence step 3, L325, requires an in-place restore to keep the live tenant-key store and “verify it covers every key generation the recovery point references.” AD-12 key custody, L189, requires erasure to make earlier key backups unusable for the erased tenant. Memories erasure continuity, L301, forbids resurrecting an erased tenant or key and requires unknown lineage to fail closed. The PRD expressly requires SM-5 to prove an erasure acknowledged during a failed incompatible attempt survives its named recovery (PRD L380; FR-8 L237). Recovery point and freshness, L298, uses the same unconditional coverage language.

**Executable failure.** A usable point is cut after the approved release acquires the lock. That release then acknowledges a Memories tenant erasure, destroys the tenant’s key, and fails. Its named recovery correctly preserves the live key store, but the old point still references the destroyed generation. A literal implementation of step 3 must stop before data restore because coverage is incomplete. Restoring the missing key to satisfy the check would violate the erasure invariant. This prevents the existing SM-5 scenario from completing safely; it is not a request to change the accepted lost-window envelope or introduce new custody machinery.

**Correction implied by the accepted decisions.** Scope key coverage to non-erased recoverable data. A missing generation is an expected absence only when the current surviving tombstone authority, with known lineage, proves the corresponding tenant has been erased. Such a key must remain absent, and the existing admission/purge recovery processing removes the erased tenant’s restored state before replay. Missing coverage for any other data and unknown lineage still fail closed. Apply this clarification to step 3 and the recovery-point coverage convention so a later valid erasure does not make the point unusable for every surviving tenant.

Suggested step-3 wording:

> In place: keep the live tenant-key store and verify coverage for every referenced generation needed by non-erased data. A missing generation is permitted only when the current surviving tombstone authority with known lineage proves the tenant was erased; its key remains absent and the admission/purge hooks remove its restored state before replay. Missing coverage otherwise, or unknown lineage, fails closed.

**Qualification.** Extend the existing named-recovery qualification row with the already-required SM-5 case: cut the point, acknowledge a tenant erasure during the release attempt, fail that attempt, and complete its named recovery with the erased tenant and keys absent while other tenants recover. No new rehearsal category or product decision is needed. The parent confirmed this correction follows the accepted erasure and recovery decisions.

## Accepted paths confirmed

| Area | Result and evidence |
| --- | --- |
| Entry and executor | Production in-place recovery uses the local MFA entry on the production executor; staging reset uses its executor; replacement-capacity DR uses the off-site recovery executor. Recovery keeps its environment’s stop set. Baseline re-deploy and data restore are distinct; an interrupted data restore repeats from the same point. AD-7, L131–135; In-place recovery, L289. |
| Retained-data degraded attempt | An approved degraded repair still needs valid compatibility evidence or the named data restore with checks, duration and a usable point cut after its lock. Failure removes candidate workloads while retaining data; compatibility evidence does not enable automatic rollback on this branch. The first empty installation without retained application data has its separate fresh-install proof. L287–289, L317. |
| Data and post-cut state | Quiesce removes workloads before native data restore, discards post-cut broker and Scheduler state, and checks the recovered release against current qualification. Keys and data precede starting recovery workloads; hooks and subscriber catch-up occur in recovery mode. L321–326. REC4-1 is the remaining key-coverage correction. |
| Catalogs | Automatic recovery uses its prepared rollback generation. Data restore prepares a forward generation from the point’s committed generation, retaining later idempotency/key entries as non-executable entries; commit is at readiness. Exception release paths do not require the unavailable live baseline to validate an automatic rollback set. AD-3, L90; AD-15, L219–222; Catalogs, L232; step 4, L326. EventStore protocol confirmation remains an owned qualification gate. |
| Lifetimes and interruption | Recovery-kind maximum lifetimes have explicit outage/entry anchors, notify without aborting and remain independent of phase deadlines. Same-attempt continuation preserves timers, grace and consumed recovery allowance while recording its new epoch. A distinct recovery entry records its own kind and retains the original outage timestamp. L291. |
| Takeover | Reserving the next epoch permits fencing control actions; operational mutation is admitted only after each affected authority is fenced or drained. Revocable per-job authority cannot be re-minted by the old job, shared credentials do not count as revoked, and recovery tasks check their invoking epoch at commit. AD-7, L132; L290; step 4, L326. The Kubernetes drain values and proof remain owned implementation work. |
| Admission after restore | Admission records remain the authority. Data restore invalidates the projection until it is rebuilt from the reconciled DR realm or the live in-place realm. Recovery adds no admission, lists post-cut grants for Administrator, and verifies revoked principals through gateway and asynchronous checks. L122–123, L321, L327–328. The two accepted lost-window revocation categories are preserved. |
| Verification and reopen | Subscriptions follow broker reprovisioning. The normal render is verified with ingress closed; workers resume only after module reconciliation. Reopen occurs inside the recovery attempt, restores only recorded pre-incident ingress, and preserves pre-G2 closure. Staging reset restores its own prior ingress. L326–329; Roles, L37. |
| Stops and revisions | Stops are per environment. Each new cause advances the revision; continuing causes do not churn it. Administrator records identify every cause as resolved or accepted and require lost-window handling and admission agreement. A later revision prevents an older clear; approved degraded attempts retain the accepted before-start/during-execution distinction. L288, L295. |
| After DR | The verified attempt becomes the baseline; reduced-recovery status is distinct from degraded production. Staging restoration and remaining operating-policy qualification continue to gate promotion, and the reduced RTO commitment is stated. L303; Owned work. |

## Review boundary

G-1 through G-5 remain accepted. This review does not reopen recovery scope, the deputy’s limited authority, the RPO exceptions, the single-node envelope, forward-only catalog authority, the credential-fencing mechanism or hook-version support. Recovery-hook implementation, EventStore confirmations, numerical operational bounds, prepared capacity, custody and drills remain the document’s assigned qualification work; the architecture review supplies no runtime readiness claim.
