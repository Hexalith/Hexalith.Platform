---
title: EXT-SECRETS-1 isolated custody maintenance and recovery hardening
type: feature
created: 2026-10-28
status: in-progress
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
- [ ] `maintain_isolated_custody.py` — add inspect/maintain commands with original evidence reconciliation, deterministic persisted plan, new per-operation journals, exact revision/receipt checks and bounded 24-hour owner horizon. Preserve profile version while extending owner-managed validity; update every dependent public runtime copy and restart only isolated consumers with native UID/resourceVersion conditions. Key technical metadata stays exact.
- [ ] Same command — maintain exact existing two leases using original resource-journal identities and issuance accessors, policy/capability checks, seven-day absolute cap and before/after observations. Journal before renewal; unknown renewal cannot repeat. If replacement is actually needed, leave an explicit owner/action blocker rather than unsafe implicit issuance; coordinator may implement the authorized separate replacement path if required.
- [ ] Authority/preload/binding helpers — implement 16 MiB bound, original-identity completion reconciliation with confirmed absence, and unconditional denial of metadata writes pending a real provider fencing mechanism.
- [ ] Focused Python tests — cover every matrix row, lost responses, expiry/cap, receipt continuity, exact record scope and coherent dependent references.
- [ ] README and new public evidence — document implementation and run focused offline suites; coordinator performs bounded installed maintenance, health/metadata, preservation, original-journal and credential exposure checks. Report resulting deadlines and blockers.

**Acceptance Criteria:**
- Given verified originals, when maintenance completes, then only existing isolated records/references and exact leases change, ten original receipts remain, keys/policies and 42 nulls stay unchanged.
- Given interrupted execution, when recovery runs, then only exact observation/owner-operation reconciliation proceeds; no uncertain issuance, renewal, cleanup or publication is repeated.
- Given completed maintenance, when health and preservation are checked, then all nine health checks pass and evidence states no Live/contract/provider-data operation or availability claim.

## Implementation Notes

Invocation explicitly authorizes source changes, focused offline tests and scoped installed maintenance. No unresolved human intent gaps. External effects are bounded owner administration, exact lease renewals and native-conditional isolated consumer reference updates. Reconciliation verified supplied deadlines unchanged; four technical key expiries are 2026-11-03 18:29 UTC. No replacement is currently required.

## Spec Change Log

## Review Triage Log

## Verification

Run focused maintenance, preload, authority installer, custody binding and credential cleanup unittests. Independently inspect the full diff and retained installed metadata/health and preservation evidence. Never rerun completed installer or installed contract cases.
