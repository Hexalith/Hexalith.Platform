# Recovery validation — 2026-09-28

**Verdict: conditional pass; clarify the in-place restore protocol before implementing recovery and staging reset.** Two findings: **1 high, 1 medium**. Both are `discuss` findings. The reviewed artifact is the current September 28 spine, SHA-256 `79f0d65346e9b644de605791a0183e42182bacf7e25b79060ad9e00a2a356c06`.

Scope: recovery safety and liveness, restore identity, entry/exit conditions, timers, backup consistency, and independent recovery execution. This review preserves accepted decisions D1–D10, the single-node/no-HA envelope, four-hour RTO within declared coverage, and the recorded RPO exceptions. It reports contract gaps, not missing implementation evidence already assigned to owned work. No runtime, credentials, source artifacts, or memlogs were changed.

## REC-1 — In-place restore skips the preparation that makes DR steps 3–6 safe

**High · discuss · high confidence**

Sources in `ARCHITECTURE-SPINE.md`: **L274–L277**, **L278**, **L309–L314**, **L245**.

The in-place restore branch runs DR steps 3–6 against the live environment and explicitly omits fence, Keycloak restore, and cutover. The omitted steps also contain two different prerequisites: step 1 disables external-effect and destructive-retention workers; step 2 deploys the recovery point's release with ingress closed and workers disabled, admitting only the restored workloads and the recovery executor to quarantine. These prerequisites have no equivalent in the in-place row. Its explicit redeployment of the recorded working baseline belongs to the separate branch for a failed/interrupted recovery. Rendering environment-layer objects from the recovered release at L245 does not deploy that release's application workloads.

Concrete failure: an incompatible candidate fails an Administrator-approved release. The release procedure closes ingress, but its candidate workloads, message consumers, and scheduled workers can remain running. The in-place procedure then restores the old tenant-key store and old data while step 4 invokes hooks inside whatever workload is currently present. A live candidate can mutate the restored state, run incompatible recovery code against it, or perform an external effect before reconciliation. A staging reset has the same problem and does not even inherit the production failure's ingress closure. The environment lock serializes deployment attempts; it does not stop application writes or broker delivery.

**Recommendation:** Add a shared preparation phase for the in-place data-restore branch, preserving D5's exclusion of old-instance fencing, Keycloak database restore, and DNS cutover. Before restoring keys or data, close user admission, stop ordinary writers/consumers and external-effect/destructive workers, enter quarantine, select the compatible retained release and configuration bound to the named recovery point, and deploy its recovery-mode workloads. Permit only module recovery tasks to mutate state until recovery and access checks pass. Reference the existing Administrator/deputy reopening authority and keep the promotion stop set. Specify a separate path for simple baseline redeployment when no data restore is needed.

## REC-2 — The generic attempt lifetime is defined using release-only phases

**Medium · discuss · high confidence**

Sources in `ARCHITECTURE-SPINE.md`: **L277–L279**, **L282**, **L287–L288**, **L311–L317**.

Attempt ownership includes in-place recovery, DR restore, environment-layer changes, and shared infrastructure changes. Timing then says **each attempt** begins at its first mutation, identified as catalog preparation, has a ten-minute rollout deadline, and derives its maximum lifetime from those timers. DR's first mutation is fencing or environment preparation, and a data restore may legitimately spend much of the separate four-hour outage-to-restoration budget before workloads can become ready. Neither the phase from which an in-place recovery's ten-minute readiness clock starts nor the lifetime reported to the stale-attempt monitor is defined for those operations.

The four-hour DR requirement is clear and is not itself contradicted. The ambiguity is the additional generic attempt contract: one implementation can treat a valid ninety-minute restore as stale or timed out after a release-sized window, while another can omit a useful persisted lifetime because no release verification deadline exists. The instruction that timers never restart does not identify the intended phase boundaries.

**Recommendation:** Scope release rollout and verification deadlines to release attempts and give recovery attempts explicit, persisted phase deadlines and an overall maximum lifetime derived from the existing incident RTO. Define when in-place recovery's readiness/verification phases begin after data preparation. Keep the original outage timestamp, fixed deadlines across takeover, and no timer reset; have the monitor use the deadline belonging to the attempt kind. This clarifies the accepted timing model without loosening the four-hour bound.

## Checked without an additional finding

- Recovery points bind a cut, retained release, configuration/profile identity, key coverage, register sequence, and independently verified artifact integrity. The common hook contract owns class positions relative to that cut, with G2 drill and continuity evidence required; no new backup mechanism is requested here.
- Replacement DR restores the recovery point's environment digest or an already qualified later one. Off-site definitions, artifacts, replicated provenance, credentials, deputy authority, and reissue ownership are represented.
- Post-cut Memories erasure protection remains distinct from accepted non-Memories RPO losses. External-effect reconciliation and explicit reopen authority already exist. An extra high finding merely for an omitted in-place “reopen” step would overstate the gap.
- The endpoint's recovery-executor-only restriction, recovery-hook gate timing, mutation fencing race, promotion-stop race, and Dapr activation gaps are intentionally left to the other reviewers who already own them.

Machine-readable findings: [review-recovery.findings.json](review-recovery.findings.json).
