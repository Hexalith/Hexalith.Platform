# Independent adversarial review — EXT-PROTECTION-1 custody

Reviewed: 2026-10-18. Scope: the companion decision design and feature architecture spine as present during review. This is a document/source review; no infrastructure, Live execution or dependency qualification occurred.

**Verdict: revise the feasibility substrate before handoff.** The single-common-epoch construction has a coherent safety argument if the stated complete-copy closure, genuinely surviving current authority and complete holder-drain premises hold. I found no additional direct resurrection execution that satisfies all those premises. The native KMS/journal transaction gap, minimum deletion delay, vendor-internal assurance and G2 dispatch/hold handoff are already explicitly unmet/open and are not repeated as new findings. Three additional lifecycle/integration gaps should become explicit model invariants or gates.

## F1 — An admitted terminal `NotPerformed` has no terminal EventStore representation

**Severity: high; protocol convergence/liveness.** Design lines 96–101 introduce terminal `NotPerformed` for both `Reserved` and `Prepared`, then map every persisted non-Consumed start to `Unknown`. The corresponding spine AD-22 preserves the existing three provider states. This is safe against fabricated success but does not define a recoverable owner outcome.

Concrete execution:

1. EventStore commits original `ConsumptionReserved` reservation R.
2. Custody admits R, records `Reserved`, and prepares survivors.
3. A stale pin revision, lost enrollment, or another pre-arm refusal causes the documented terminal `NotPerformed`.
4. Custody returns `Unknown`; its retained exact lookup returns `Unknown` forever because the internal outcome is terminal.
5. `DeletionConsumptionActor.RecoverAsync` only advances the owner on provider `Consumed`; after `Lookup`, it calls `Consume` again only for `NotStarted`. The owner therefore retains `ConsumptionReserved` forever. No new batch or original replacement is authorized by this design.

Source evidence: `../eventstore/src/Hexalith.EventStore.Server/Security/DeletionConsumptionActor.cs`, `RecoverAsync`, approximately lines 380–405. The actor's behavior is compatible with its port; an adapter cannot repair the mismatch merely by retaining an extra diagnostic.

Two implementations can satisfy the prose's non-success behavior but diverge operationally: one makes `NotPerformed` truly terminal and requires manual indefinite containment; the other resumes the same reservation after the transient condition clears, contradicting the diagram's terminal-refusal semantics and potentially its immutable original-result interpretation.

**Suggested change:** choose one explicit rule before F0. The smallest change within existing ports is to eliminate terminal admitted-abort states for destructive originals: pre-admission rejection has no physical start and may return authoritative `NotStarted`; after admission the same immutable original remains pending/resumable, with retained attempt diagnostics, and a durable worker advances that original. This needs a defined safe pre-arm reprepare path and must never treat a changed request as retry. If true terminal zero-effect cancellation is required, list an authenticated EventStore provider/owner outcome extension as a separate unmet contract requirement. Do not translate a recorded start to `NotStarted`.

## F2 — Failed preparation creates a second live epoch lineage whose retirement is unspecified

**Severity: high; state-machine completeness and bounded progress.** Design lines 63–67 require deleted older epochs and no alternate target path; lines 106–107 require tracking orphan keys and permit safe pre-arm refusal. AD-17 requires tracking all staging/orphan paths but gives no lifecycle for retiring a candidate successor that never became current.

Concrete execution:

1. Stable current E0 protects A, B and held control C.
2. Batch {A} prepares E1a and permanently retains successor wrapper copies of B and C in locked S3 versions.
3. A pin/head change prevents arm. The preparation is abandoned or enters `NotPerformed`; E0 remains current.
4. A later authorized batch selects {B}. Its ordinary new successor E1b excludes B.
5. Deleting E0 alone leaves B recoverable through the locked E1a wrapper and live E1a key.

Step 5 would violate the existing global complete-copy premise; it is **not** a counterexample to a correctly enforced closure check. It exposes a missing transition. A compliant conservative implementation must instead refuse the second batch, but the document never tells it how to retire E1a and regain the advertised stable-state premise. Another implementation may delete E1a while E0 still protects all roots, wait for actual deletion evidence, and then retry. Both preserve the stated safety rule but differ by an unbounded block versus an additional seven-day retirement cycle. The visible throughput and hold/receipt behavior differ materially.

**Suggested change:** define a permanent generation-attempt registry and a `Preparing / Current / Retiring / PermanentlyDeleted` lifecycle independent of manifest success. “Older epoch” must mean **every noncurrent generation that has ever received a wrapper**, including never-committed, abandoned and ambiguously completed attempts, not simply a lower committed generation number. Before admitting a different destructive target set, require all abandoned wrapper-bearing epochs to be verifiably deleted while all live roots remain covered by the current committed epoch; otherwise retain the original operation as pending. Never rely on S3 object deletion or inaccessible pointers to retire those copies. State the additional delay caused by failed preparations. Add the execution above to F0, including a lost `ReEncrypt` response and a retained locked staging version.

## F3 — Existing root-buffer ownership ports cannot acknowledge the required drain

**Severity: high; explicit integration requirement missing.** Design lines 105 and 160 correctly require actual key-buffer quiescence rather than lease expiration or a fence callback. Its interface table, however, describes `IInteractionOccurrenceKeyProvider` as an adapter implementation task without identifying the required ownership/acknowledgement change.

Current source:

- `IInteractionOccurrenceKeyProvider.ResolveRootAsync` transfers an `InteractionOccurrenceOwnedKey` to the caller; `IsCurrentAsync` returns a boolean.
- `InteractionOccurrenceOwnedKey` is sealed, exposes the mutable root byte array, and its `Dispose()` only zeroes that local array. It has no lease id, remote acknowledgement or extensible disposal callback.
- `InteractionOccurrenceProtector.UnprotectAsync` calls `IsCurrentAsync` around line 57 while the `using var root` remains alive until scope exit. Treating that call as a release acknowledgement would be false.

Concrete execution: a root is returned; the caller completes `IsCurrentAsync`; a destructive fence then closes; the process is suspended before root disposal. The physical owner cannot infer local zeroing from the existing calls. If it treats the last authority check or lease timeout as drain completion, a plaintext root survives its purported terminal barrier. If it correctly refuses to infer disposal, it has no existing callback through which this ordinary caller can ever satisfy drain.

The general “unreachable holder blocks receipt” rule preserves safety, but leaves a missing normal completion path. Two independently built adapters can therefore choose an unsafe timeout or safe permanent `Unknown` while each assumes the caller contract supplies more than it does.

**Suggested change:** record a required EventStore ownership/lease contract extension before any production adapter work. Each transferred root must have an independently recorded holder/invocation id, exact fence/authority revision, and an acknowledged release only after caller-owned root and derived-key buffers are actually cleared. Define how late task completion and process restart affect that release; transport timeout is not proof. A local disposal callback alone is not durable remote acknowledgement. Alternatively keep all key use inside a custody-owned operation with an explicit result-release protocol, but that is a deliberate port change. Treat the existing ports as insufficient for this proof, add the suspended-before-dispose trace to F0, and keep the new contract gate explicit.

## Areas with no additional finding

- Original DEK versus per-object wrapping key versus storage epoch is separated; unchanged AAD and stable opaque reference avoid migration-induced original replacement.
- Permanent interaction enrollment as well as full-target indexes addresses alias/version recreation by restored owner state.
- Pin/arm CAS ordering is coherent once the explicitly missing G2 handoff is provided; the document does not falsely equate a later governance hold with a successful effective physical pin.
- Exact S3 version/hash selection plus a conditional head/index commit avoids treating an orphan prepared receipt as committed; exact lookup must always return the winning retained bytes.
- A KMS permanent deletion before receipt publication remains recoverable from the original armed intent and native event; absence alone is correctly insufficient.
- Authority incarnation/TableId pinning and failure on total authority loss are internally consistent with the stated independent-survival assumption. They do not solve rollback of the independent authority itself, which is explicitly out of scope.

No source files or deliverables were changed by this review.

## Narrow correction check — 2026-10-18

Re-read only the changed generation, pending-state, lease-integration and related spine rules at the author's request.

- **F1 addressed at design level:** admitted pre-arm stops remain resumable `Unknown` under the same original, the state diagram has a return path, and the existing owner enum is preserved without invented `NotStarted`. Production worker/liveness behavior remains a future proof obligation.
- **F3 addressed at design level:** the design and AD-23 explicitly require the missing provider-neutral key-use token and authenticated zeroing/completion acknowledgement. Existing `Dispose`, cancellation and lease expiry are expressly insufficient. This is appropriately recorded as an unresolved production contract gate.
- **F2 substantially addressed:** a permanent generation registry, ambiguous-attempt denial, abandoned-successor deletion evidence and its additional delay are now explicit. One literal scoping correction is still needed in section 3 and AD-17: “delete every unadopted generation ... before another arm” includes the active `Prepared` successor, which is not adopted until arm. Exempt **only the exact designated current candidate successor bound to that same original and preparation attempt**; require permanent retirement of all other abandoned/unselected wrapper-bearing generations. The active candidate must remain durable and usable for survivor coverage when the old epoch is deleted. Without that exception, one literal implementation never arms, while another could destroy its survivor checkpoint key.

**Disposition: F1/F3 closed for this document review; F2 needs the narrow active-candidate exception.** This check does not qualify any implementation or discharge the separately explicit native-transaction, vendor-assurance, hold-handoff or holder-erasure gates.
