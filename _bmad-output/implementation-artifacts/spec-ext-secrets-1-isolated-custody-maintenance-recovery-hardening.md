---
title: EXT-SECRETS-1 isolated custody maintenance and recovery hardening
type: feature
created: 2026-10-28
status: done
route: dispatch
baseline_commit: 9658932b0f04d9fc9daf7c30cc0d9afc2146ed7c
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — explicitly authorized in invocation">

## Intent

**Problem:** The completed isolated custody binding has owner-managed profile/enrollment and credential deadlines, while administrative reads, preload completion recovery and future metadata publication need hardening.

**Approach:** Add a separate journaled maintenance command for the existing isolated binding. Reconcile original evidence and current facts first; renew only exact existing records and leases, preserve original outcomes and verify metadata/health. Harden recovery and block unfenced future metadata publication.

## Boundaries & Constraints

**Always:** Preserve the completed context spec, original evidence, receipts, keys/validity, existing OpenBao policies, production, Memories, Agents register, settled constants and unrelated checkout work. Installed scope is only the existing isolated authority/consumer bindings and credential references. New public journals precede effects. Unknown effects stop for exact reconciliation without replay. Credential identity requires original accessor, policy and original recorded Secret UID/resourceVersion; missing legacy identity is a blocker, never synthesized. Seven days is the credential absolute lifetime limit. Enrollment expiry cannot exceed the four successors' existing technical windows. Keep all 42 null inputs outside scope and availability false.

**Never:** Replay completed installers, generate keys/successors, extend key validity, publish provider metadata in this story, expose credential values or hashes, run Live or installed contract/cohort cases, edit the Agents register, or claim dependency availability.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Maintenance | Exact profile, two hosts/callers, four successor statuses and original receipts | New revision-checked operations maintain those records and coherent runtime references, retaining every receipt | Changed/foreign/revoked identities deny |
| Credentials | Exact original identities, restricted policies, renewable leases below absolute cap | Journal renewal with bounded increment and exact post-lookup; retain same credentials when viable | Unknown renewal is observation-only; expired/nonrenewable/mismatched identity stops with owner/action |
| Maximum lifetime | Lease cannot be maintained within seven days | Stop with separately scoped successor credential/new Secret and verified consumer cutover/revocation required | Never issue or replace implicitly or exceed cap |
| Admin bounds | Valid JSON up to 16 MiB | Read full response within repository bound | Reject oversized/error responses |
| Preload recovery | Original create-response identities, retained exact pull evidence and resources absent | Reconcile completion without creation or pull | Missing identities/evidence, replacement or uncertain cleanup deny |
| Provider publication | No verified provider-native fence | Deny before metadata write | Local process lock/assertion is insufficient |

</frozen-after-approval>

## Code Map

- `eng/ext-secrets-1/maintain_isolated_custody.py` (new): reuse sanitized `Installer`, `forward`, `administration_request`, `application_request`, pinned credential reads and durable journals; never call installer entrypoints. Coordinator executes installed maintenance after offline verification.
- `eng/ext-secrets-1/bind_isolated_custody.py`: reuse original plan validation/public object helpers; gate only the metadata-write branch, preserving completed read reconciliation.
- `eng/ext-secrets-1/install_hmac_authority.py`: administration response bound must match `AuthorityRepository.cs` 16 MiB capacity.
- `eng/ext-secrets-1/preload_isolated_image.py`: preserve original create-return identities; reconcile confirmed cleanup using retained image/node/native-image evidence. Missing legacy identities stay blocked.
- `eng/ext-secrets-1/test_maintain_isolated_custody.py`, existing authority/preload/binding tests: focused offline failures and recovery, no installed cases.
- `eng/ext-secrets-1/README.md`: separate maintenance usage, maximum lifetime and operational blockers.
- External evidence `custody-maintenance-20261027T234210095124Z/`: read-only baseline, original public fingerprints, ten retained receipts, revision `e19e68c9537246148141ec9d3841cfe4:10`, credentials/key metadata and nine passing health checks.

## Tasks & Acceptance

**Execution:**
- [x] `maintain_isolated_custody.py` — add inspect/maintain commands with original evidence reconciliation, deterministic persisted plan, new per-operation journals, exact revision/receipt checks and bounded 24-hour owner horizon. Preserve profile version while extending owner-managed validity; update every dependent public runtime copy and restart only isolated consumers with native UID/resourceVersion conditions. Key technical metadata stays exact.
- [x] Same command — maintain exact existing two leases using original resource-journal identities and issuance accessors, policy/capability checks, seven-day absolute cap and before/after observations. Journal before renewal; unknown renewal cannot repeat. If replacement is actually needed, leave an explicit owner/action blocker rather than unsafe implicit issuance; coordinator may implement the authorized separate replacement path if required.
- [x] Authority/preload/binding helpers — implement 16 MiB bound, original-identity completion reconciliation with confirmed absence, and unconditional denial of metadata writes pending a real provider fencing mechanism.
- [x] Focused Python tests — cover every matrix row, lost responses, expiry/cap, receipt continuity, exact record scope and coherent dependent references.
- [x] README and new public evidence — document implementation and run focused offline suites; coordinator performs bounded installed maintenance, health/metadata, preservation, original-journal and credential exposure checks. Report resulting deadlines and blockers.

**Acceptance Criteria:**
- Given verified originals, when maintenance completes, then only existing isolated records/references and exact leases change, ten original receipts remain, keys/policies and 42 nulls stay unchanged.
- Given interrupted execution, when recovery runs, then only exact observation/owner-operation reconciliation proceeds; no uncertain issuance, renewal, cleanup or publication is repeated.
- Given completed maintenance, when health and preservation are checked, then all nine health checks pass and evidence states no Live/contract/provider-data operation or availability claim.

## Implementation Notes

Invocation explicitly authorizes source changes, focused offline tests and scoped installed maintenance. No unresolved human intent gaps. External effects are bounded owner administration, exact lease renewals and native-conditional isolated consumer reference updates. Reconciliation verified supplied deadlines unchanged; four technical key expiries are 2026-11-03 18:29 UTC. No replacement is currently required.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence / decision |
| --- | --- | --- | --- |
| B01 | medium | patch | Persisted plans check directory strings only; changed operations can alter issuer. Derive and verify exact permitted intent before every reuse. |
| B02 | medium | patch | A fresh run rejects the completed 19-receipt state. Remove fixed audit offsets and retain verified prior maintenance as a usable next baseline. |
| B03 | medium | patch | Current runtime timing drift can be adopted at fresh inspection. Compare every settled field with original binding evidence. |
| B04 | medium | patch | Fresh inspection snapshots provider state without checking original preservation baseline. Reject policy/mount/auth drift before effects. |
| B05 | medium | patch | Cached inspection performs no installed observations. Reconcile current prerequisites while preserving the original plan. |
| B06 | medium | patch | Progress checks compare records but omit current windows on cached-plan recovery. Recheck actual current records before new effects. |
| B07 | medium | patch | Final verification lacks a current owner-deadline check. Reject expiry during rollout/finalization. |
| B08 | medium | patch | Flooring fractional remaining lifetime can reject a viable native capped renewal. Handle sufficient leases first and validate provider-capped readback. |
| B09 | medium | patch | Preload completion ignores a present conflicting image/name/identity journal. Require agreement among retained originals. |
| B10 | medium | patch | Exposure report asserts no hashes while scanning only literal/base64 forms. Compare prohibited hash representations in memory without recording them. |
| E01 | medium | patch | Same cached-inspection omission as B05; profile revocation can still produce successful inspect. |
| E02 | medium | patch | Same window omission as B06; cached plans can revive expired owner records. |
| E03 | medium | patch | Sufficient leases are checked after unused fractional-increment calculation. Move sufficiency before renewal-only arithmetic. |
| E04 | medium | patch | Base Installer stores created references outside resources; legacy interrupted preloads can be retried if both identity/pull files are absent. Check all original reference stores. |
| E05 | medium | patch | Same provider preservation omission as B04; broadened original policy can become the accepted baseline. |
| V01 | medium | patch | Verified mutation gap: capability rejection can be removed while all tests pass. Add forbidden/missing/write-capability fixtures with zero effects. |
| V02 | medium | patch | Verified mutation gap: renewal post-lookup validation can be removed while all tests pass. Add acknowledged-but-invalid lookup fixtures and no-replay assertions. |
| R01 | medium | resolved | Repository remove-and-append order differed from client in-place replacement. Corrected model and independent service fixture; original :12 receipt reconciliation retained. |

## Verification

Run focused maintenance, preload, authority installer, custody binding and credential cleanup unittests. Independently inspect the full diff and retained installed metadata/health and preservation evidence. Never rerun completed installer or installed contract cases.

Installed maintenance evidence: [new operation journals](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-maintenance-20261027T234210095124Z/operations/), [read-only baseline](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-maintenance-20261027T234210095124Z/reconciliation.json). Original ten receipts plus nine new operations are retained at revision `e19e68c9537246148141ec9d3841cfe4:19`. Profile, two hosts/two callers and four statuses expire at `2026-10-28T23:51:51.830725Z`. Same-identity data/metadata leases expire at `2026-10-28T23:54:17.544944674Z` / `2026-10-28T23:54:22.570603711Z`; replacement was unnecessary. Both final-image preload completions reconciled from copied original evidence and confirmed absence, with no creation/pull/deletion. Focused offline verification passes 75 tests; administration-response limit is 16 MiB and unfenced metadata publication is disabled.

The first maintenance pass stopped after two acknowledged owner operations because the client expected in-place array replacement; the actual repository removes and appends records. The source model and independent service fixture were corrected. Read-only state reconciliation proved both committed operations and renewals; continuation used the same unchanged plan, skipped those effects and completed only the remaining operations. The original stop/blocker evidence is retained. No uncertain effect was repeated.

Concurrent owner version-control work advanced HEAD to `b742b1b7a496ac72d6961ca6db5213d3df42d234` and recorded early source changes with unrelated submodule work. This session staged/committed nothing and preserves the original full baseline and all unrelated work.

Review hardening preserves the frozen intent and installed outcomes. Internal corrections will revalidate deterministic plans/current windows, retain verified maintenance history, reject original runtime/policy drift, reconcile all preload identity sources, and exercise negative capability/renewal paths. Review fixes are source-only; never rerun installed effects for them.

Final review resolution: all 17 independently reported findings were fixed in source and covered by executed passing tests; no code finding was deferred. The final focused run passed 90 tests (30 maintenance, 9 preload, 27 binding, 17 authority administration, 7 cleanup). Tests cover two cycles retaining 28 receipts, mutated plans, exact native collection ordering, original runtime/policy/controller drift, expiry during recovery/rollout, fractional native caps, negative effective permissions, uncertain renewal readback, legacy preload stores/journal conflicts and credential hash exposure. [Resolution and executed tests](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-maintenance-20261027T234210095124Z/review-resolution.json).

Final read-only [verification](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-maintenance-20261027T234210095124Z/final-verification.json) confirms revision :19, all nine health checks, unchanged original keys/metadata/policies, all 194 original public evidence files, all 42 nulls, and zero literal/base64/hash credential exposure. No installed maintenance was rerun for review fixes.

The new next-baseline helper was exercised read-only against current public facts and correctly withheld export on a later unrelated Secret resourceVersion change: `kubesphere-system/kubesphere-sa-token`, original UID retained. Public managed fields show a kube-apiserver last-used label update at `2026-10-28T00:00:02Z`, after the initial successful 39-check completion verification. No value was read or mutation performed for this diagnosis. [Exact owner/action blocker](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-maintenance-20261027T234210095124Z/next-maintenance-baseline-blocker.json) requires cluster credential-owner reconciliation and an accepted public preservation baseline before the next cycle; no baseline was fabricated or issued. The completed isolated maintenance remains verified. Future metadata publication also remains disabled pending a verified provider fence; seven-day credential maximums and original four technical key windows are unchanged.
