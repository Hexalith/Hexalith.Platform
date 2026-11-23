# Attempt store v1

`Hexalith.Platform.AttemptStore` serializes signed attempt, lock, working-pointer and promotion-stop changes using one current head per environment. Callers supply an independently enrolled `RecordTrustPolicy` and signed Builds envelopes. The same policy verifies incoming and stored records used in a transition. Use `ReleaseRecordVersion.V2` explicitly for v2 attempts. Promotion-decision actions always use v2. [Builds record contracts](../../references/Hexalith.Builds/ReleaseRecords.md) · [Staging evidence policy v2](staging-evidence-policy-v2.md)

## Objects and conditional writes

- The mutable head is `environments/{env}/head.json`. Its schema is `hexalith.attempt-store.head.v1`. It carries the revision, prior head's SHA-256 byte digest, epoch, holder, current attempt and working-pointer references, stop state, and receipts for committed transitions. The staging head also carries the v2 decisions.
- Signed envelopes are retained at `environments/{env}/records/{kind}/{recordId}/{digest}.json`. The digest is the Builds SHA-256 signing-content digest. References in the head also bind the exact bytes and schema version where applicable. Record ID path segments are percent-encoded for unsafe identifiers, and encodings longer than 640 characters use `~` followed by the SHA-256 hex of the original UTF-8 identifier. A retained release has a copy in each environment that uses it.
- A valid first transition can bootstrap an empty, revision-zero head with `If-None-Match: *`. Signed envelopes also use create-only PUTs. Every transition replaces the head with `If-Match` against its freshly observed ETag. No mutation of the logical state occurs until the head replacement commits. A losing writer may leave immutable archives without references. Preserve every submitted envelope's exact bytes, including its retained release, so a retry can use the same bytes even when its first write left an unreferenced archive.
- Each head replacement increments the revision and records the previous head's digest. The library rereads and validates state after a conditional conflict. A committed replay returns the original receipt and writes nothing. Replays compare exact submitted bytes. An in-progress attempt replay requires the current epoch. A receipt records a past effect, so callers read the current head for live authority.
- A timeout or other lost acknowledgment requires a strong read. A matching committed receipt and its archived bytes prove the effect, even if later transitions advanced the head. An advanced head without the receipt triggers revalidation. If the expected ETag still matches or the store cannot read the receipt or archives, it reports `AttemptStoreWriteUncertainException` and callers wait for reconciliation. The S3 adapter disables SDK write retries.
- The current implementation retains receipts in the head and stops accepting transitions if the encoded head exceeds 32 MiB. Retain bucket versions and referenced archives so audit and reconciliation can read them.

## Transitions

| Operation | Required current state and effect |
| --- | --- |
| Acquire | An unheld lock and a new signed in-progress attempt at the next epoch. The record binds the retained release and any active promotion stop. The transition advances epoch once. |
| Revise | The exact current holder and epoch with a valid signed revision. A working or non-working terminal revision releases the lock. Terminal records are immutable. |
| Take over | The current nonterminal holder, a new in-progress successor at the next epoch and an exact, successor-signed terminal revision of the predecessor. The signed links bind both revisions. One head write installs the successor, terminalizes the predecessor and advances epoch. |
| Move latest working | The exact working terminal attempt from this environment and the current epoch, with a signed pointer from the attempt's writer. When a prior pointer exists, the new pointer advances its epoch. |
| Set a stop | A trusted environment-scoped record naming the next stop revision and its preceding stop. The stop sequence continues across clearances. |
| Clear a stop | An Administrator action naming this environment, the active stop and its revision, and the exact working attempt named by the current pointer. The action must dispose of every outstanding cause. Only that environment's active stop changes. |
| Reserve admission | An authorized production evaluator binds the exact in-progress, pre-mutation production attempt at the current production epoch. The action reserves the matching pending decision in the staging head. |
| Reserve as Administrator | A separately signed production-scoped Administrator action protects the pending decision against supersession. |
| Supersede | The initiating staging writer binds the exact current staging attempt and epoch. The decision must still be pending with neither reservation. Callers supply independently verified preparation context for a prerequisite reset, which the store checks against the verified reset and release bindings on each attempt. |
| Complete or release admission | The production executor binds the current terminal revision, the current production epoch, the admission reservation and the exact revision originally admitted. Completion also binds the committed working pointer. A verified non-working outcome releases the matching admission reservation back to pending. |

The first accepted signed v2 staging attempt supplies the immutable release and decision binding from intake. A fresh decision identity for the same retained release requires all previous decisions for it to be terminal. Their identities and terminal states remain bound. Lifecycle callers own intake, production-baseline eligibility and v2 activation. Production admission and staging supersession both write the staging head. Whichever valid action commits first prevents the other from taking effect. The production head is read during validation and its ETag is checked again immediately before the staging write. This is a read check across two objects. Callers serialize production work through the production lock and reconcile the outcome before advancing its epoch or triggering work on a new baseline. The staging writer uses only the staging head. The unchanged Builds validator leaves reservations pending when a takeover successor or a later epoch cannot establish closure.

## Failure results

Operations return `AttemptStoreCommit` or throw. `RecordValidationException.Code` reports a stable failure without echoing record content. Builds verification and transition codes propagate unchanged.

| Code | Meaning |
| --- | --- |
| `lock-held`, `lock-not-held`, `attempt-missing` | The required holder state or attempt is absent or differs from the request. |
| `stale-epoch`, `invalid-epoch`, `terminal-attempt` | The requested epoch or revision cannot mutate current attempt state. |
| `record-id-conflict`, `record-content-conflict`, `record-reference-mismatch` | An identity, committed action, exact envelope or reference differs from the value already bound. |
| `stop-revision-mismatch`, `stop-not-set`, `working-attempt-mismatch`, `undisposed-stop-cause` | The stop or clearance lacks the current stop state, exact pointer attempt or required cause dispositions. |
| `decision-missing`, `decision-binding-conflict`, `decision-reserved`, `decision-terminal` | The target decision has a different binding or an incompatible current state. |
| `unverified-prerequisite-reset`, `reservation-mismatch` | The supersession lacks prerequisite evidence or closure names a different admission reservation. |
| `invalid-head`, `head-missing`, `record-missing`, `contention` | The store cannot establish valid context or safely finish within the conditional retry bound. |
| `invalid-environment`, `environment-mismatch`, `invalid-record-reference` | The environment is unsupported, the records name different environments or a reference cannot identify an object. |
| `head-too-large`, `revision-exhausted` | The head has reached its size or revision limit. |

## Hosting and consumer obligations

The staging instance is hosted in a dedicated Scaleway Object Storage project in `fr-par`, outside the cluster and executor hosts. The bucket has versioning and default 30-day `COMPLIANCE` retention. The staging writer can read its staging keys and use the allowed conditional PUTs. It has no object or version delete permission and no write or read access to the production prefix. The reader identity can read for verification. Keep signing enrollment and private keys independent of object storage credentials, and provide each consumer only the storage access its operation needs. [Hosting and denial evidence](../../_bmad-output/implementation-artifacts/evidence/story-4-7/)

Route operational writes through the library so the signed transition checks run. Object Storage enforces the write conditions and retains head versions for audit. Callers must check and fence actual rollout authority before takeover or mutation. Use only confirmed commits for effects that depend on the lock, stop or decision. Preserve outstanding reservations while an outcome is uncertain. Scan evidence, eligibility, issuer integration, executor health, credential vending and v2 adoption remain part of their respective lifecycle gates.

The test assembly runs deterministic in-memory races by default. The hosted suite requires `ATTEMPT_STORE_LIVE_ENDPOINT`, `ATTEMPT_STORE_LIVE_REGION`, `ATTEMPT_STORE_LIVE_BUCKET`, `ATTEMPT_STORE_LIVE_WRITER_ACCESS_KEY`, `ATTEMPT_STORE_LIVE_WRITER_SECRET_KEY`, `ATTEMPT_STORE_LIVE_READER_ACCESS_KEY` and `ATTEMPT_STORE_LIVE_READER_SECRET_KEY`. Supply credentials through the process environment. The live suite creates new signed fixtures under `environments/staging/verification/{runId}/` and reports a skip when the configuration is absent.
