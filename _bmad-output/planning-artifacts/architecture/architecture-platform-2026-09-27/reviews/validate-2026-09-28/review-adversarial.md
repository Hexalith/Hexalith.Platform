# Adversarial validation — 2026-09-28

**Verdict: FAIL pending two high-severity contract corrections.** Four findings: 0 critical, 2 high, 2 medium, 0 low. Triage: 1 autofix, 3 discuss. This is a validation of the final September 28 spine, not an implementation-readiness claim.

The full spine was read, including the qualification gates and accepted risks. The September 28 D1–D10 decisions and confirmation pass were checked selectively in the memlog and prior review. These findings preserve those decisions. Missing implementations behind named gates, the selected hosting model, single-node exposure, named repository writers, and accepted RPO losses are not defects in this report. No spine, spec, memlog, code or cluster resource was changed.

The adversarial test is independently built cooperating units: each implements its stated side of the contract and the interaction fails. ADVR-1 and ADVR-4 are residual inconsistencies across caller/authorization clauses; ADVR-2 and ADVR-3 are concurrency rules absent from the shared contract. Technical implementation choices remain with their named owners.

## ADVR-1 — In-place recovery cannot reach the recovery hooks it must invoke

**Severity:** high. **Action:** autofix. **Confidence:** high.

**Evidence.**

- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:277`: “In production, Administrator or the deputy starts it on the production executor …; a staging reset runs it on the staging executor” and “runs DR steps 3–6 against the live environment”.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:309`: “in-place recovery reuses steps 3–6”.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:314`: recovery hooks use “a declared internal endpoint reachable only from the recovery executor”.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:129`: distinguishes the production executor's locally started in-place recovery from the recovery executor's replacement-capacity recovery.

**Two units.** Unit A implements the D5 in-place recovery runner on the production executor, or the staging reset on its staging executor. It invokes the module hook required by reused step 4. Unit B implements the module recovery endpoint and its access policy, allowing only the separately named replacement-capacity recovery executor. Unit B denies Unit A. Moving the runner to the replacement-capacity executor would violate the accepted placement; opening the endpoint to all executors would abandon its isolation rule.

**Consequence.** Planned recovery from an incompatible production candidate, and staging reset after an incompatible unadopted candidate, cannot complete. Keeping hooks inside the owning workload correctly resolves the earlier execution-locus issue, but does not resolve the caller identity.

**Minimal correction.** Define a recovery-attempt executor role and map it explicitly to production, staging or replacement-capacity execution. Step 4 must admit only the executor currently owning that environment's recovery attempt, with the current epoch and scoped recovery credentials. No move of hook execution or change to deputy authority is needed.

## ADVR-2 — The earlier executor can mutate after an epoch takeover

**Severity:** high. **Action:** discuss. **Confidence:** high.

**Evidence.**

- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:278`: “The lock carries a monotonic epoch checked before every mutation” and permitted takeover occurs by “incrementing the epoch”.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:279`: takeover and in-flight retries share a bounded grace, but there is no stale-writer fencing rule.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:126`: executors obtain “per-job credentials”; no takeover revocation or in-flight write drain is specified.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:223`: the CAS-capable store holds the lock and records; signatures and pointer epochs govern record authority, not acceptance of writes at Kubernetes or the catalog authority.

**Two units.** Unit A, the release executor, implements every mutation as “read current epoch; reject mismatch; send write”. For a Helm/Kubernetes or catalog mutation it reads epoch e successfully, then pauses before sending or while the request is queued. Unit B implements an explicitly allowed takeover within grace: acquire e+1 and perform the remaining single recovery. B restores and verifies the baseline. A resumes, and its e-era write is accepted because the target sees valid job credentials and no required epoch fence. A checked before its mutation, and B held the new epoch for every one of its mutations.

**Consequence.** Recovery can be overwritten by a superseded candidate after verification. The monotonic epoch prevents competing *record-store ownership*, but the stated client check does not make ownership and an external mutation atomic. CAS and signed records cannot retract an accepted target write.

**Minimal correction.** State that after takeover is accepted, no older-epoch mutation, including an in-flight mutation, can commit to any workload/catalog/record authority. Require fencing at the authority or proof that the prior writer is revoked, stopped and drained before the replacement may mutate. If neither can be established, stop for intervention. The owners may select the mechanism; the cross-unit guarantee belongs in Attempt ownership. This does not reopen the approved takeover paths.

## ADVR-3 — A delayed clear record can remove a stop caused by a newer incident

**Severity:** medium. **Action:** discuss. **Confidence:** high.

**Evidence.**

- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:223`: “promotion-stop set records by any executor, the off-site monitor, Administrator or the deputy, and clear records only as Administrator records”.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:283`: a “recorded incident” sets the stop; “Only an Administrator record naming the reason and a verified current working release clears it”.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:278`: the environment lock covers workload-affecting changes, while monitor stop records can arrive independently.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:276`: empty/degraded approval lifts the stop “for that attempt only”.

**Two units.** Unit A implements the Administrator-record producer. It inspects S1, verifies baseline B, and signs a reasoned clear for B. Delivery or processing is delayed. Unit B implements the monitor and record store. It records a new incident S2 while B remains the working baseline, then accepts the signed clear according to its authorized writer. Every named requirement holds; neither S2's generation nor its identity must appear in the clear. A serial store is sufficient to reproduce the error: S1, S2, then the delayed clear.

**Consequence.** The stop for S2 disappears despite no Administrator review of S2. A fresh health precheck may pass when the new incident concerns compromised credentials, data integrity or another incident that does not make the readiness probe fail.

**Minimal correction.** A clear must identify the observed stop revision or exact set-records it acknowledges and the verified working-attempt identity; application is conditional on no newer unacknowledged stop. New set records dominate older clears. Bind the degraded-path override to its single attempt and specify that it cannot silently consume later stop records. The actual record encoding remains Builds-owned.

## ADVR-4 — The staging lifecycle exception lacks an executable admission scope

**Severity:** medium. **Action:** discuss. **Confidence:** high.

**Evidence.**

- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:127`: module-supplied E2E on an executor “receives only short-lived synthetic-client tokens and the declared verification endpoint”.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:232`: synthetic clients “carry no admin or cross-tenant grant”; synthetic-admission is “limited to the synthetic tenant”. The same row permits staging E2E to “create and delete only run-scoped tenants carrying the synthetic exclusion marker within a module-declared tenant-lifecycle critical flow”.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:437`: the shared admission predicate admits “the synthetic group for the synthetic tenant only”.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:273`: all declared required critical flows must pass for the exact release.

**Two units.** Unit A implements the accepted D8 staging tenant-lifecycle E2E. It uses only synthetic tokens, creates a run-scoped test tenant T carrying the exclusion marker, and then invokes lifecycle operations for T. Unit B implements the gateway admission predicate and tenant scope exactly as specified: the synthetic actor is admitted only for environment tenant S, not T. T's exclusion marker controls reporting and aggregate visibility; the contract does not make it an authorization attestation or extend admission. B denies the calls, and A has no permitted alternative identity.

**Consequence.** The declared lifecycle flow cannot meet its staging gate using the allowed sandbox credentials. Local/CI exemptions and the prohibition on production smoke tenant creation are correct but do not supply the staging authorization bridge.

**Minimal correction.** Retain D8 and add a staging-only, run-bound lifecycle authorization scope covering the explicitly attested test tenant IDs and allowed operations. Define how creation, later calls and cleanup obtain that same scope; never grant production or real-tenant authority and never treat a caller-supplied exclusion marker alone as admission. The gateway/realm and lifecycle-test implementers must agree before consuming this shared contract.

## Scope of proposed changes

ADVR-1 is a naming and caller-binding fix preserving D5. ADVR-2 requires a cross-authority concurrency guarantee; ADVR-3 requires stop-record conflict semantics; ADVR-4 requires the authorization meaning of the already accepted D8 exception. None requires an extra architectural component, a deployment, an HA commitment or reopening a settled risk.

