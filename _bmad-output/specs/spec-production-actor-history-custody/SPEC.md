---
id: SPEC-production-actor-history-custody
created: 2026-10-08
companions:
  - brownfield.md
  - lifecycle-contract.md
  - implementation-plan.md
  - test-plan.md
  - ../../../docs/implementation/actor-history-lifecycle-2026-10-07.md
  - ../../../../parties/_bmad-output/implementation-artifacts/spec-ext-parties-1-branch-b-authoritative-identity.md
  - ../../planning-artifacts/epics.md
sources: []
---

# Production actor-history custody

## Why

EXT-PARTIES-1 Branch B requires bounded attribution after profile erasure, irreversible expiry and restore safety. Platform currently supplies a cleanup library and SDK seam, but no production custody implementation or host registration. This dedicated Platform story supplies that missing owner capability; installed Branch B availability also requires the follow-ups in [implementation-plan.md](implementation-plan.md).

## Capabilities

- **CAP-1**
  - **intent:** Trusted owning hosts can admit an exact history unit only under enforceable finite custody.
  - **success:** The seven `IIdentityHistoryCustody` operations and their owning-host dependencies are composed; admission retries recover original evidence across restart; missing policy, backend or current authority denies admission and retained reads.
- **CAP-2**
  - **intent:** Authorized attribution survives profile erasure only until its original purpose deadline.
  - **success:** Events and persisted snapshots retain readable attribution within the window after profile-key destruction, expose no profile plaintext, and deny release at the exclusive expiry instant.
- **CAP-3**
  - **intent:** Owners can account for every evidence copy and recover the original destruction outcome.
  - **success:** Durable inventory and one authenticated final receipt survive crash, concurrent retry and lost acknowledgement; expired evidence and every decrypting key generation are irrecoverable across all copies, with unrelated units preserved.
- **CAP-4**
  - **intent:** Rollback and restore cannot resurrect expired attribution.
  - **success:** Restored pre-expiry data, keys and lifecycle records cannot release expired evidence; quarantine remains closed until surviving authority, fencing and owner recovery hooks are reconciled.
- **CAP-5**
  - **intent:** Platform can demonstrate custody on installed production-class targets.
  - **success:** Exact-target test receipts and independently observed persisted end states satisfy [test-plan.md](test-plan.md); absent targets, skipped lanes and incomplete follow-ups remain explicit qualification failures.

## Constraints

- Enforce exactly `party-actor-retention-v1`, purpose `party-actor-history-v1`, and **365 fixed 24-hour days from original binding-effective-at**. Allow only `now < ExpiresAt`; retries, erasure, rotation, copying and restore never extend expiry.
- Profile erasure immediately blocks current eligibility. Actor, provenance and logical references remain protected data; issuer/subject mappings, tokens, names and emails stay outside Party attribution history.
- Existing EventStore SDK paths own source persistence. Preserve authenticated tenant, operation, exact target and current-source/actor authorization before protected lookup; custody grants none of those permissions.
- Admission capability flags must describe proven backend guarantees. A fixture, TTL, tombstone, recoverable deletion or queued future purge cannot establish irreversible expiry.
- Missing, stale or unavailable lifecycle authority denies release even when a key is accessible. Final receipts cannot precede all-copy destruction or permit a late committed copy.
- Reuse existing hosts and qualified owner mechanisms. No new scheduler, database, service, vault or proprietary CLI is selected here; absent durable mechanisms are implementation blockers.
- Preserve the existing cleanup operation's five-second provider waits, cancellation and Pending semantics. Its provider confirmation is distinct from independent backend qualification.

## Non-goals

- Approved actor-free successor-binding continuation after predecessor expiry: explicit follow-up **F1**.
- Complete installed P-01–P-10 qualification and closure of EXT-PARTIES-1 Branch B: explicit follow-up **F2**.
- Replacing authentication, exposing direct Parties APIs/actors/state stores, changing Consumer onboarding or unrelated GDPR behavior, or implementing HMAC/export custody work.

## Success signal

An installed production-class provider preserves authorized attribution after profile erasure, denies and irreversibly expires the exact original unit, recovers the same all-copy receipt after restart/lost acknowledgement, and prevents a pre-expiry restore from reviving it. Independent persisted assertions prove isolation and completeness. Local fixtures and this story's completion alone do not establish Branch B availability.

## Assumptions

- `PLAT-ACTOR-HISTORY-1` is a symbolic dedicated story ID; its epic placement is not assigned. Proposed paths and backend-neutral contracts are implementation recommendations, not evidence of installed resources.

## Open Questions

- **Q1 — Backend:** Which existing backend/key hierarchy makes every unit and historical decrypting key backup irrecoverable, including copies inside immutable backups?
- **Q2 — Authority:** Where does the surviving monotonic lifecycle/receipt anchor live, and who owns its fencing, reissue, authenticated time, skew and currency bounds?
- **Q3 — Inventory and operators:** Which durable inventory/work/receipt mechanisms, copy owners, recovery hooks, approved targets and probe credentials exist? What receipt retention/minimization policy applies?
- **Q4 — Expiry operations:** Who operates cleanup and monitoring, with which bounded retry/outage/receipt-completion limits? How is hard expiry enforced when workers or backend access fail?
- **Q5 — Unit mapping:** How are admission collisions and revocation evidence mapped to the original binding deadline? The processor currently admits revocation using its own `effectiveAt`; it must not renew the referenced actor evidence.
- **Q6 — Host ownership:** Which approved packaging/bootstrap path installs the provider and Parties snapshot codec in both hosts, and who supplies exact-source retained-read admission?
- **Q7 — Erasure boundaries:** How does Story 8.6 tenant erasure interact with bounded post-profile-erasure attribution, and what minimal receipt/anchor data may survive evidence expiry?

Decision consequences and blocking points are in [implementation-plan.md](implementation-plan.md). The approved duration and trigger are settled; these questions do not reopen them.
