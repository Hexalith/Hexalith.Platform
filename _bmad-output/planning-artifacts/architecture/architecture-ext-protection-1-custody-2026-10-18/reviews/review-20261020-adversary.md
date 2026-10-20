# Adversarial and prior-input reconciliation review — 2026-10-20

Verdict on the initial reviewed draft: **two concrete invariants need clarification before handoff**. Neither finding changes the proposed backend nor treats approval as given. The openly stated lack of a native KMS/DynamoDB/S3 transaction is an explicit owner decision and is not an undisclosed defect in this review.

Reviewed the 2026-10-20 erasure-authority decision, updated AD-16–AD-24 spine, 2026-10-18 provider decision and companion custody design, source bindings, current custody wrap/lifecycle interfaces and the 2026-10-19 handoff. No infrastructure deployment, destructive experiment, register amendment or runtime qualification occurred. Line references below identify the initial draft, before parent-agent corrections.

## Findings

### A1 — High: whole-tenant preparation admission is implicit

**Location:** decision B1–B3 / key hierarchy (initial lines 70, 80, 85–89); spine AD-20 and AD-22.

The new decision freezes manifest M and forbids selected-target copies, while the complete tenant survivor sweep is prepared against immutable checkpoints. It does not expressly freeze all tenant allocation/copy/write admission during B1–B3. “Every non-target,” same-head CAS and recomputation suggest the necessary invariant, but leave incompatible implementations: one pauses the tenant map; another permits new non-target roots under E(g), merges an updated head after the sweep, and mistakes the old verified survivor checkpoint for complete coverage. Deleting E(g) then destroys that newly allocated survivor.

The 2026-10-18 design explicitly freezes the tenant key map for the finite sweep and permits pausing all tenant root acquisitions. That load-bearing constraint should survive the redistillation.

**Required correction:** make B1 close whole-tenant old-epoch allocation, key-copy and write admission; drain every effect admitted before that barrier; bind the complete survivor map/checkpoint and its revision in B3. Resume survivor/new-root operations only on the exactly adopted successor after B3. Governance hold/unpin transitions must still be accepted or conservatively retained, and their head changes must force revalidation rather than be overwritten. An alternative concurrent-write algorithm would need its own explicit admission/cutover proof; it cannot be inferred from generic CAS.

**Acceptance counterexample:** allocate a non-target root after the initial survivor enumeration but before arm, including a lost-allocation response and delayed writer. It must either be refused/queued before effect or become durably covered by the adopted successor before arm. Verify that B5 preserves writes and pins admitted after B3.

### A2 — High: permanent abandoned-generation closure must precede arm

**Location:** decision key hierarchy/B3/B5 (initial lines 70, 87–89); spine AD-17, AD-20 and AD-21.

“Retire” every abandoned generation does not state the pre-arm physical terminal condition. The sentence requiring older epochs to have been permanently deleted can be read as numeric predecessors only, while AD-21 places actual final deletion of abandoned generations at B5. The original 2026-10-18 design was stronger: before each arm, permanently delete every abandoned/unselected generation that received wrappers, including generations numerically newer than the current epoch.

Concrete counterexample: failed preparation A copied a's X into abandoned E(h), because a was then a survivor. Later M={a,b} is prepared against current E(g). E(h) and E(g) are both scheduled, but E(h) remains recoverable when arm succeeds. E(g) is actually deleted first. b is irrecoverable while a can still be recovered through E(h). Returning Unknown until B5 does not restore the claimed common physical cut.

**Required correction:** B3 requires retained actual final DeleteKey evidence for every decrypting predecessor/abandoned/unselected generation, including numerically newer generations, except the exact adopted successor that contains no selected X. Scheduled/disabled/unknown generations cannot satisfy that precondition. Survivor coverage must already be safe before any cleanup deletion; current E(g) remains intact until closure is complete. State this in the decision boundary table and spine, not only historical prose.

**Acceptance counterexample:** prepare/abandon a successor, select a previous survivor in the next manifest, then explore both orders of actual KMS deletion. No arm may occur while a selected X has an alternate recoverable generation. A lost cleanup response remains Unknown until the exact generation's terminal evidence is recovered.

## Other adversarial probes

- **Historical K/R and bare C0:** the revised outer X gate correctly preserves the actual tenant KEK version/AAD while withholding C0. The explicit ban on bare-C0 ancestry and separate migration-closure requirement prevents a current wrapper upgrade from being mistaken for erasure of old ungated backups.
- **Root reissue:** permanent indexes by full target and interaction close alias/version/store changes; missing-key recovery cannot generate roots; unrecoverable unpublished generations burn the identity. The design does not rely on deterministic re-derivation.
- **Original outcomes:** prewritten immutable bytes, a single conditional selected pointer, no original-record TTL and read-only lookup distinguish prepared candidates from the one published original. B5's merge into the current head closes the stale preparation-checkpoint overwrite scenario.
- **Restored authority:** TableId/incarnation plus independent enrollment prevents an ordinary restored table/name/config from becoming current. Total surviving current-evidence loss explicitly fails closed; the design does not claim transparent latest-head recovery from an arbitrary backup.
- **Holds:** earlier registered/unknown holds deny destruction, later dispatched-batch holds cannot falsely receive a conflicting successful physical pin, and EventStore retains compromise block/reserve ownership. The real immutable handoff remains an explicit, unimplemented acceptance gate; no native cross-owner ordering is claimed.
- **Buffers and stale workers:** real holder completion/zeroing or accepted termination proof is required; timeout/lease expiry/revocation alone is insufficient. Stale worker head publication is fenced and sent native effects must be reconciled. No proof of actual SDK/TLS/host cleanup is asserted. These conservative conditions must remain blocking in F0/F3 and OA-4.
- **Native key operation limits:** AWS documents separate source/destination permissions and eventual consistency for [ReEncrypt](https://docs.aws.amazon.com/kms/latest/APIReference/API_ReEncrypt.html). The head CAS therefore cannot be treated as cancellation of an already sent key operation. The design correctly requires original-operation reconciliation and independent holder closure rather than making that claim.

## Prior-input reconciliation

The historical OpenBao negative control survives without dilution: successful authentication and retained-control decrypts distinguished native deleted-key revival from outage/refusal; the result is rejection of ordinary Raft recovery for this custody guarantee. No plugin, transaction layer, extra local replicas or snapshot retention policy is presented as repairing it.

The prior provider criteria for original root identity, exact outcomes, hold safety, full copy inventory, independent restoration/reissue authority, manifest membership and separate qualification remain represented. The literal native-transaction requirement remains explicitly unmet and OA-1 is the contract-owner decision. The new hierarchy fixes the prior companion's ambiguous object-wrapping-key/tenant-KEK mapping without reinterpreting accepted KekVersion values. The two missing explicit preparation/closure rules above are the only load-bearing reconciliation omissions found in this lens.

All proposal approvals remain outstanding. This review supplies no Available result and does not authorize feasibility effects or native deployment.

## Targeted follow-up on revised draft

**A1 closed:** B1 now explicitly freezes whole-tenant allocation/copy/content-write/key-acquisition admission. B2 drains all pre-freeze native intents/producers/holders/writers. B3 alone atomically adopts the verified successor and resumes non-target/new-root admission exclusively there. Hold/unpin/revocation mutations invalidate stale comparisons. The prior incomplete-survivor-checkpoint counterexample no longer satisfies the stated rules.

**A2 closed:** the hierarchy and B3 now require actual permanent-deletion evidence for all alternate decrypting predecessor/abandoned/tainted epochs, including numerically newer candidates. Scheduling/disablement is expressly insufficient. The common-cut counterexample cannot reach arm.

The new private broker/NativeIntentV1 rules explicitly retain uncertainty after a potentially sent effect, disallow SDK/HTTP retries and send-unknown replay, and require producer/I/O/buffer closure. Tainting an unknown staging destination and permanently deleting it before arm makes a delayed native copy harmless to the eventual selected-target cut. Unknown effects toward current/adopted/unidentified destinations instead reject qualification. This is conservative safety, with the actual runtime isolation/termination/native request behavior still explicitly unproved under OA-4/F2/F3. No new unreported safety bypass was found in this targeted recheck.

### A3 — Medium: native experiment key cap does not cover the added taint branch

**Location:** revised F2/F3 experiment table.

F2 caps four epoch resources across the two synthetic lanes. Current plus adopted successor for each tenant already uses four epoch ARNs. F3 now explicitly exercises an unknown staging-copy result, permanent deletion of that tainted destination, and a fresh fully tracked successor. That branch needs at least one additional epoch ARN while preserving both lane originals. “No new key cycle beyond approved scope” does not itself reserve the fifth key.

**Required correction:** make the proposed synthetic manifest/cap explicit, for example five total epoch ARNs: small-lane current, tainted staging, clean successor; 1,000-target-lane current and successor. Alternatively state a different exact allocation that actually executes the branch. Keep this a proposed bound requiring separate experiment authorization and a monetary cap. Do not silently reuse a key identity or omit native taint recovery to stay within four.

**Follow-up verdict:** initial High findings resolved; one Medium experiment-scope correction remains. All deployment/adoption/native qualification gates remain outstanding.

## Final targeted closure

**A3 closed:** F2 now explicitly caps five **total epoch ARNs**, including one tainted staging candidate, plus two KEK protectors. F3's fault schedule is prepared before F2 and runs within the same original pending cycles/resources. Completed destructive originals are never repeated. A second unknown candidate, another cycle or a sixth epoch ARN stops for fresh scoped authorization; the caps cannot be reset through resource deletion or new request identities.

**Final verdict for this review lens: clear to hand off the proposed architecture decision. A1–A3 are resolved; no remaining actionable adversarial/reconciliation findings.** Native transaction equivalence, runtime/copy/vendor assurance, owner approvals, separate experiment authorization and actual feasibility remain the document's explicit blocking gates. No deployment, destructive effect or test execution was performed by this review.
