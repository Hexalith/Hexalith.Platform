# Focused PRD review: recovery and environment access

Reviewed: 2026-09-27. Scope: `prd.md` and `addendum.md`; the decision log was consulted to confirm the adopted recovery policy and shared Keycloak requirement. This is a requirements review, not an implementation or infrastructure validation.

## Verdict

The policy is coherent and appropriately scoped for an internal platform. Finalization needs a small acceptance correction and two wording clarifications. One recovery dependency should also be made explicit in the architecture handoff. No new recovery technology, identity topology, availability commitment, or central business-flow catalogue is needed.

Finding counts: **0 critical, 1 high, 3 medium, 0 low**. Findings R1–R3 are concrete document corrections. R4 is a downstream handoff and does not require choosing an implementation before finalizing the PRD.

## R1 — High: acceptance can pass without demonstrating successful rollback

**Location:** `prd.md`, Success measures, SM-5 (line 294 in reviewed draft); FR-8 (lines 178–185).

SM-5 currently permits a controlled deployment-failure rehearsal to show “verified recovery or an accurate failed/unverified result.” An implementation that attempts rollback but never successfully restores the previous release could therefore satisfy the stated acceptance measure by accurately reporting every failure. Correct failure reporting is necessary, but does not demonstrate the promised automatic rollback capability.

**Concrete fix:** Require at least one controlled failed-update rehearsal, with a valid previous working release and available infrastructure, to restore and verify that release within the FR-8 budgets while preserving NFR-1 data. Also retain separate failure-path evidence that an unsuccessful or unverifiable recovery stops promotions and reports the correct status through GitHub. A partial-update case is a useful concrete rehearsal because it exercises an explicitly required behavior; no large failure-injection framework is needed.

**Disposition:** Correct the acceptance wording before finalization. The actual rehearsal remains implementation readiness evidence before enabling automatic promotion.

## R2 — Medium: rollback verification can be read as requiring the failed release's new smoke suite

**Location:** `prd.md`, FR-8 second testable consequence (line 183); `addendum.md`, Production rollback policy and rationale → Recovery and its limits, second bullet.

Recovery requires the “same” production-safe smoke tests used for deployment verification. Read literally, a smoke check for a feature or module introduced by the failed release must also pass after restoring a previous release that does not provide that feature. This can report a correct rollback as failed and leave the required verification behavior ambiguous.

**Concrete fix:** Keep the same readiness, safety, timing, and failure-trigger policy, but explicitly verify the smoke suite applicable to the release being restored. Retain the previous working release's required-check identity with its release/configuration record. A check must not be dropped merely because it fails; the applicable suite follows the identified target release.

**Disposition:** Clarify before finalization. Storing or resolving the versioned check definition is architecture work; the PRD need only make target-release verification unambiguous.

## R3 — Medium: missing check definitions are not explicitly a closed gate

**Location:** `prd.md`, FR-6 testable consequences and closing paragraph (lines 149–155), FR-7 smoke-test ownership (line 167), and SM-C1 (line 299).

The document blocks absent results for required tests and requires modules to supply flow lists at enrollment. It does not explicitly say what happens if the required-test declaration itself is missing, unreadable, or not discovered. An implementation could accidentally produce an empty required set and consider all required checks passing without ever obtaining the module's declaration. This affects both staging E2E eligibility and the production smoke suite.

**Concrete fix:** State that release eligibility requires a resolved, usable required-check declaration from each included module, and missing or invalid declarations block promotion/verification. An empty discovered suite must not silently pass as successful validation. Keep the flow content module-owned; this does not call for Platform to invent flows or centrally enumerate domain tests.

**Disposition:** Clarify fail-closed configuration behavior before finalization. The declaration format and validation mechanism remain architecture work.

## R4 — Medium: the recovery handoff does not explicitly cover the required shared identity dependency

**Location:** `prd.md`, FR-9 backup coverage and restore verification (lines 199–204), NFR-2 (lines 262–271), and downstream recovery work (line 316); `addendum.md`, Disaster recovery approach → Selected approach and operational scope; Hosted architecture questions.

The existing shared Keycloak server is required for access and resides on the designated Kubernetes installation. Backup inventory is assigned to deployed modules, while the recovery handoff lists topology/storage/backup/capacity without explicitly assigning recovery coverage for shared supporting services. Recovering application data within four hours is insufficient if the failed infrastructure also hosted the identity service and there is no usable way to restore authorized access. Production authorization must also remain isolated after the recovery procedure restores configuration or identity state.

FR-11 and NFR-3 already require the correct authorization outcome. This is therefore an inventory, ownership, and readiness-evidence omission, not a reason to choose realms or build a separate identity system in the PRD.

**Concrete fix:** Add required shared dependencies, expressly including the existing Keycloak service and necessary environment-authorization configuration, to the recovery inventory/handoff. Identify who owns their recoverability and include their recovery or availability in RTO evidence. Require restore verification to include permitted production access and rejection of staging-only access before reopening the restored service. Architecture can reuse an independently managed backup/recovery arrangement where one exists; Platform need not create duplicate ownership.

**Disposition:** Explicit downstream handoff, owned by Administrator with Platform architecture before the first production recovery exercise. No identity-layout decision or extra user approval is needed to record this consequence of the accepted access and recovery requirements.

## Areas that are already sufficient

- Rollout and recovery have distinct readiness and verification budgets; missing results cannot establish success. The document does not confuse these budgets with an availability guarantee.
- First deployment, partial updates, an unreachable cluster, one automatic recovery attempt, stopped subsequent promotions, and operator notification are covered.
- Application rollback preserves business data, event history, and credential rotations. Disaster recovery has a separate explicitly accepted data-loss target. These operations are not conflated.
- Backup age is measured from usable recovery points, retained chains are required, and independent access/decryption material is covered. Whole-site protection is appropriately conditional on independent copies and recovery capacity.
- RTO includes detection, operator response, capacity provision, restore, and validation. The lack of established round-the-clock coverage is disclosed and assigned for readiness assessment rather than silently assumed.
- Shared infrastructure and shared Keycloak do not convey production authorization. Direct API, CLI, and MCP requests are covered by the mandatory environment boundary, with both positive and negative acceptance evidence.
- GitHub delivery/account mechanics, Keycloak realm/client layout, storage mechanisms, and infrastructure topology are reasonable architecture handoffs rather than missing product decisions.

## Resolution recheck — 2026-09-27

Rechecked only the changes addressing R1–R4 in the updated PRD and addendum. **All four findings are resolved at the requirements level; no findings remain open.** This does not establish implementation or infrastructure readiness.

- **R1 resolved:** SM-5 now requires a successful rollback within the FR-8 readiness and verification budgets with NFR-1 data preservation. Failed/unverified recovery reporting and first-deployment failure handling are separate acceptance scenarios, with GitHub delivery verified.
- **R2 resolved:** FR-8 and the addendum explicitly apply the recorded smoke suite for the restored release under the same verification policy and failure thresholds.
- **R3 resolved:** FR-6 requires non-empty module declarations with mapped E2E checks and blocks missing/invalid declarations and empty sets. FR-7 validates required readiness/smoke declarations before an update and rejects missing, invalid, or empty check sets.
- **R4 resolved:** FR-9 and the addendum include Keycloak and production access configuration in the shared-dependency recovery inventory, require identified recovery ownership and availability/restoration evidence, and require restored production access plus staging-only denial checks. The downstream table assigns the recovery work and owner confirmation before production use and the first timed exercise.
