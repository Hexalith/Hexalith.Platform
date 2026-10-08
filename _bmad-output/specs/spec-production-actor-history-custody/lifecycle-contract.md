# Custody unit and lifecycle contract

## Immutable scope and policy

A provider-owned unit binds the full `AggregateIdentity`, environment/instance scope, policy, purpose, original binding-effective instant, derived deadline, opaque evidence ID and original admission revision. Reject any domain other than `party` before backend access. Candidate admission lookup is the exact aggregate/purpose/policy/effective-instant tuple; confirm collision and revocation mapping under Q5 before production admission. Authenticate the supplied identity/evidence against the durable original record, not just `IdentityHistoryCustodyEvidence.Satisfies` or its flags.

`ExpiresAt = original binding-effective-at + TimeSpan.FromDays(365)` equals 31,536,000 seconds. Normalize offsets without changing the instant; do not use a calendar year, local midnight, last access or profile-erasure time. Reject missing/wrong policy ID, duration, purpose or trigger, arithmetic overflow, and already-expired new admission. Policy reload invalidates changed admission; it never rewrites existing unit deadlines.

Reserve admission and key allocation with a durable idempotency record. Concurrent retries return the same original evidence and unit; interrupted allocation is reconciled as pending owner work, never forgotten orphan keys. Changed scope cannot obtain the original unit's authority; the owning admission/domain path rejects changed logical intent. The SDK does not carry a binding version/logical ID into `AdmitAsync`; Q5 must resolve that limitation without inventing a retry identity.

## Event and snapshot protection

Use independently governed purpose keys with destruction granularity no broader than the original expiry unit. Profile, tenant-content HMAC, delivery, export and gateway-signing keys do not serve as interchangeable history keys. Every rotated generation inherits the same original deadline; retained generations cannot extend it.

The provider implements every SDK method:

| Method | Required behavior |
| --- | --- |
| `AdmitAsync` | Validate exact policy and trusted scope; return original durable evidence only when current custody can enforce all three capability flags. |
| `CanReadAsync` | Authenticate original evidence, obtain current nonrollback authority/fence/time, and permit only an active, unexpired unit. Deny on uncertainty or unavailable authority. |
| `ProtectEventAsync` | Protect admitted history using authenticated encryption and exact unit/key scope; register the durable source/copy obligation before release. |
| `UnprotectEventAsync` | Validate authenticated framing, resolve exact original unit/generation, and recheck current authority and exclusive expiry immediately before returning plaintext. |
| `ProtectSnapshotAsync` | Partition every embedded history copy by original unit and protect each under its own deadline without changing profile protection. |
| `UnprotectSnapshotAsync` | Validate typed/serialized envelopes and current authority for every history segment. Unsupported, missing or expired required proof fails closed; no silent invented continuation. |
| `DestroyExpiredAsync` | Authenticate the exact expired unit; recover or complete its stable all-copy destruction operation. Return true only for a durable final original receipt. |

Persist history events as `json+identity-history-v1`, with `PayloadProtectionState.Protected` and scheme `party-actor-history-v1`. Authenticated envelope context includes full aggregate/environment scope, event type, schema/format, original evidence/policy/purpose/deadline and exact key generation. Reject foreign substitutions, malformed/unknown envelopes, changed deadlines and tampered payloads. Successful unprotection returns the original JSON with Unprotected metadata. Bound backend waits, honor cancellation, observe late completion and clear owned key/plaintext buffers on unsuccessful release.

The current Parties snapshot path may contain `ProtectedSnapshotState.Payload` as base64 profile-protected JSON. A domain-owned codec must cover both `HumanActorBindings` and `HumanActorTransitions.OriginalEvidence`, including their actor/provenance/logical references. Handle actual typed and persisted `JsonElement` forms; maintain the existing outer `IdentityHistorySnapshot("identity-history-snapshot-v1", ...)` contract unless an owner-approved version migration is needed.

Do not encrypt a multi-deadline snapshot once under a later-lived key: that retains an expired predecessor. Do not encrypt the whole snapshot under the earliest deadline: that destroys still-retained independent units. A new snapshot must never decrypt/re-encrypt an expired unit under a successor key. Mixed-deadline protection and raw successor unit survival belong here; removing predecessor proof while preserving query/replay semantics requires F1.

## Durable authority, copies and receipts

Persist lifecycle state/revision, current authority epoch/fence, exact key generations, inventory generation, stable destruction operation and receipt reference alongside immutable unit facts. The original evidence's admission revision remains immutable; authenticate it against the original record while checking the newer current authority. Do not require a caller to rewrite evidence after rotation or destruction.

State progresses from Active to Expired/PendingDestruction to Destroyed/ReceiptFinal. The deadline itself denies reads even if no worker has persisted the pending transition. Terminal state never reverses. Re-admission, key recreation, stale writers and restored old revisions cannot reopen a terminal unit. Surviving monotonic authority must be independent of the EventStore/OpenBao/ciphertext restore cut; replicas or a revision counter rolled back with that cut are insufficient.

Each inventoried copy binds its original unit, generation, class/location reference, owner, deadline/protection, registration state and completion/receipt. Cover the following classes or retain an owner-verified absence:

- Source events, snapshots and older snapshot/state versions, including duplicated embedded history.
- Projections, read models and caches, plus broker/outbox/DLQ/replay/spool/export/log artifacts containing attribution where present.
- Replicas, native backups, off-site/versioned/immutable objects and every raw, wrapped or escrowed decrypting key generation/backup.

No derived copy gets a later deadline. Copy registration and release must participate in current lifecycle fencing. At expiry, prevent new copies/writes/releases, reconcile or invalidate in-flight registrations, freeze the covered inventory generation and irreversibly destroy all decrypting paths. An in-flight write cannot commit after the final receipt unless it is already provably unrecoverable and covered by the same receipt. Missing/unacknowledged copies keep destruction pending.

A content-free authenticated receipt records exact original identity/evidence/policy/effective-at/expiry, stable operation ID, terminal authority revision/epoch, covered inventory generation/digest, per-copy-class irreversible outcomes and confirmation time. Minimize and protect references; Q3/Q7 determine owner retention without creating an indefinite actor history. One final receipt survives concurrent retries, restart and lost acknowledgements. No new evidence, unit, key or deadline is allocated during cleanup retry.

The SDK returns a Boolean, not that receipt. The provider must expose the original durable receipt to the authorized owner through its existing lifecycle/receipt mechanism; the caller persists the receipt reference and pending-work outcome. `IdentityHistoryCleanup.ProcessAsync` still requires destruction followed by a fresh denied read. A timeout may leave the provider completing work; retry the same original tuple and recover its result. `ProviderConfirmedDestroyed` alone is not an independent all-copy audit.

Hard expiry includes cryptographic unrecoverability of ciphertext retained in immutable backups. A tombstone, application read denial, recoverable key deletion or a promise to purge later is insufficient. Q1/Q4 must establish the backend mechanism and its behavior during outages before admission claims `SourceExpiryEnforced`, `RestoreSafe` and `DerivedCopiesCovered`.

## Restore and rollback

Use the versioned Platform recovery hooks and existing owner workflow. Fence the old incarnation; restore into quarantine with user ingress, external effects and ordinary destructive-retention workers disabled. Reconcile each original deadline and surviving terminal authority before any key release, snapshot replay or projection rebuild. Reapply irreversible expiry through authorized recovery hooks; render expired historical key backups unusable rather than merely hiding their references.

Restoring an earlier Active record, old key generation or pre-expiry ciphertext cannot lower current authority or renew a deadline. Missing lineage, current authority, required generation coverage or a valid fence keeps quarantine closed. Prove expired evidence unreadable through source, snapshot and derived-copy paths, and still-retained independent units recoverable. Only the current authorized epoch may commit recovery results and permit reopening. Agree Q7's tenant-erasure mapping without changing the approved bounded post-profile-erasure purpose.
