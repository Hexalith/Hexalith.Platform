---
title: EXT-SECRETS-1 next-maintenance baseline reconciliation
type: bugfix
created: 2026-10-28
status: in-review
route: dispatch
baseline_commit: 8de90958bc41b7fd01b7ed22b8ab364447a800e4
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — explicitly authorized in invocation">

## Intent

**Problem:** Completed isolated maintenance has 19 authority receipts, but exporting its next baseline rejects a later system Secret resourceVersion refresh. Current managed fields alone cannot establish historical update scope.

**Approach:** Preserve strict default export and provide a narrowly bounded, journaled owner-evidence acceptance input for the single recorded public metadata transition. Reconcile current public facts without reading credential values. Export only if historical scope is established; otherwise retain an exact owner/action blocker.

## Boundaries & Constraints

**Always:** Preserve all original baselines, operation journals, 19 receipts, original credential identities, keys/technical windows, policies, runtime constants, 42 null inputs, production, Memories, Agents register and unrelated checkout work. Accepted difference is only Secret/kubesphere-system/kubesphere-sa-token, UID a97fa037-798b-46b6-9232-76e600480142, resourceVersion 36774577 to 37223806; kube-apiserver legacy-token-last-used refresh at 2026-10-28T00:00:02Z. Current metadata must still match retained blocker metadata. Explicitly identify the trust boundary for owner evidence; hashes establish integrity, never truth or authenticity.

**Never:** Edit that Secret, read credential values, replay completed maintenance, renew/replace credentials, generate successors, extend technical validity, publish provider metadata, run Live/installed contract/cohort cases, edit Agents register or claim availability. Never infer full historical scope or credential-value equality from current managed fields alone.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Missing historical scope | Only original UID/RV and current managed fields | No baseline; exact missing owner evidence | Fail before output creation |
| Accepted public refresh | Explicit owner-accepted historical scope evidence, exact original/current public metadata and identity | Export retains all 19 receipts and journals explicit accepted difference/provenance | Evidence is an owner trust input, not fabricated automation proof |
| Other drift | Wrong UID/RV, later refresh, changed current metadata or another resource | Deny export | No wildcard/ignore-label rule |
| Tampering/reuse | Changed acceptance/evidence after export, changed accepted record at same output | Deny provenance validation/reuse | Preserve originals and strict default behavior |

</frozen-after-approval>

## Code Map

- `eng/ext-secrets-1/maintain_isolated_custody.py`: `emit_verified_baseline` performs public-facts export without installed calls; `preserved_originals`, `validate_baseline`, `retained_receipts` preserve provenance. Keep normal inspect/maintain drift detection strict. Add optional acceptance parameter only to export and pin its full evidence chain. Existing Secret preservation material is UID/RV only.
- `eng/ext-secrets-1/test_maintain_isolated_custody.py`: reuse completed-maintenance public fixture and coordinator-only exporter test. Test independent extra drift, original byte preservation, provenance and no installed calls.
- `eng/ext-secrets-1/README.md`: document exact bounded input, owner trust boundary and blocked status; no runnable maintain command for this reconciliation.
- Original external evidence: `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/custody-maintenance-20261027T234210095124Z/`; exact blocker/current metadata files already exist. Coordinator exclusively performs fresh read-only public observations and writes new evidence outside this original directory.

## Tasks & Acceptance

**Execution:**
- [x] `maintain_isolated_custody.py` — add a bounded public metadata acceptance argument to exporter requiring an explicit owner-accepted historical scope record and retained blocker metadata; missing proof denies. Define and validate a small exact schema: owner identity, historical evidence source description and fingerprinted public evidence file(s), exact transition and an explicit attestation that the evidence establishes only the label refresh with no other Secret field changes or intervening writes. This schema is a trusted human evidence input, not automatic authenticity verification. Require label manager/time/fields exact and all other current metadata equal to retained blocker public metadata (allow only creationTimestamp absent in retained metadata). Persist accepted difference and source fingerprints into new baseline; preserve default comparisons and all other resource checks. Pin original maintenance plan and all nine operation journals, including legacy `operations/` evidence layout if present. Reuse must validate accepted material as well as current preservation facts.
- [x] `test_maintain_isolated_custody.py` — run focused positive synthetic owner-evidence fixture and every negative matrix case, unchanged 19 receipts/42 nulls/identities/keys/policies/runtime, no installed calls, original bytes preserved. No real evidence acceptance is authorized without actual historical scope evidence.
- [x] `README.md` — document schema, exact owner input, trust limitation and strict default behavior.
- [x] New external reconciliation evidence — coordinator records fresh public authority/health/Secret identities, original journal preservation and provenance; emits baseline only if actual evidence permits, otherwise precise blocker and current versus last-verified deadlines.

**Acceptance Criteria:**
- Given the exact supported historical transition, when export is accepted, then the new baseline retains all 19 receipts, original credential identities, keys/windows/policies/runtime constants and 42 nulls, and explicitly retains the accepted public difference.
- Given insufficient historical evidence, when reconciliation completes, then no baseline is fabricated and the owner receives the exact missing input, known deadlines and next action.
- Given unrelated drift, when either normal maintenance inspection or export runs, then existing strict denial remains effective.

## Implementation Notes

User explicitly authorizes source changes, focused offline tests and conditional journaled acceptance, so no further spec approval is needed. No intent gaps; historical evidence is a runtime prerequisite, with an explicitly authorized blocked outcome. Three source files plus this spec; only new local evidence writes. Initial tree is clean on main. Current public metadata matches recorded UID/RV and label timestamp. Running kube-apiserver command has no audit-log or audit-policy flags; historical scope proof has not been found. No Secret values have been read.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence / decision |
| --- | --- | --- | --- |
| B01 | medium | patch | Owner-record read/hash race was demonstrated to return a baseline that immediately fails its own provenance. Capture one byte snapshot and recheck accepted evidence before publication. |
| B02 | medium | patch | creationTimestamp is the sole unanchored metadata exception; arbitrary nested JSON currently passes. Require a valid timestamp string to keep that exception public metadata. |
| B03 | medium | patch | Different paths with current-metadata bytes are demonstrably insufficient historical scope evidence. Reject copied/hard-linked content without claiming authenticity verification. |
| B04 | medium | patch | An empty historical file passes structural validation despite supplying no scope evidence. Reject empty/whitespace historical input. |
| B05 | medium | patch | Missing completed renewal/runtime/completion journals vanish from glob pins. Require the demonstrated original journal set before accepted export. |
| B06 | medium | patch | A changed valid supplied lease expiry is accepted on reuse while the output retains the earlier expiry. Compare preservation-relevant credential facts, excluding only transient ttl. |
| B07 | low | patch | Malformed nested metadata types deny through generic exceptions rather than the acceptance reason. Direct type guards provide an actionable rejection without expanding scope. |
| B08 | medium | patch | Conflicting duplicate JSON authorization fields are silently collapsed. Reject duplicate fields in the owner authorization record. |
| E01 | medium | patch | Independent confirmation of B05: missing originals are omitted before glob pinning, so require the completed original journal set. |
| V01 | medium | patch | Preverified mutation gap: removing the new Kubernetes reuse comparison leaves all 37 tests passing. Add independent changed-preservation reuse coverage and assert output byte preservation. |

## Verification

Run focused maintenance unittests (offline); inspect full diff and immutable evidence fingerprints. Coordinator independently observes only public metadata, durable authority snapshot and nine health endpoints. Historical credential/policy/technical facts must be labeled last verified if current authenticated read would require prohibited credential reads.

Coordinator reconciliation: all 19 authority receipts exactly match original committed journals at revision `e19e68c9537246148141ec9d3841cfe4:19`; public durable authority state is unchanged, all nine current health endpoints return 200, original credential Secret UID/resourceVersion pairs match, and runtime/constants/42 nulls remain exact. All 85 original maintenance files and 194 original public evidence files remain byte-identical; Agents register unchanged. Only the specified system Secret differs outside runtime scope. No real historical proof or acceptance was created, and no baseline was issued. Current provider lease/key/policy observations were not performed because they would require credential reads; retained facts are explicitly last verified. Exact expiry fractions and seven-day caps are retained in [deadline evidence](../../../../../hexalith-secrets-evidence/ext-secrets-1/next-baseline-reconciliation-20261028T070042156838Z/maintenance-deadlines.json). [Current reconciliation](../../../../../hexalith-secrets-evidence/ext-secrets-1/next-baseline-reconciliation-20261028T070042156838Z/reconciliation.json) and [remaining owner input](../../../../../hexalith-secrets-evidence/ext-secrets-1/next-baseline-reconciliation-20261028T070042156838Z/blocker.json) distinguish observed public facts from retained provider facts. Concurrent unrelated submodule work was preserved.
