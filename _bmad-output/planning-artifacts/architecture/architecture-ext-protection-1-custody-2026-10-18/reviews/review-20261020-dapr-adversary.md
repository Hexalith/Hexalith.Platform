# Dapr-boundary adversarial review — 2026-10-20

Reviewed the **current** `/home/administrator/projects/hexalith/platform` decision, custody spine AD-16–AD-25, spec/contract/implementation plan, OWNER-APPROVAL record and retained Dapr v1.18.1 output-binding proto. This review does not use the earlier detached checkout as the current design.

Initial verdict: **clarify two high-risk operation classes before handoff; add the explicit UDS/Init conformance gate below.** The Dapr-only dependency direction and architecture-only approval boundary are represented consistently. No component, physical provider, memory-erasure guarantee or Available result is established by these documents.

## D1 — High: immutable outcome replay cannot include key-bearing unwrap results

**Locations:** decision “Dapr contract, retries and authentication”; binding contract “Checkpoints, physical intents and results”; implementation units I1/I2/I4.

The contract broadly says Dapr duplicates return the original status/result and native intents close after exact result persistence. For `unwrap`/owned-key delivery, that result can contain D. At the same time, no D/X/K may persist and every transport/sidecar copy must join holder closure. Two implementations can choose incompatibly: one retains/replays the complete unwrap result to satisfy exact-result replay; another stores only a value-free receipt and never replays key bytes. A delayed duplicate delivered after `complete-key-use`, freeze or erasure can therefore create an untracked key holder or return cached raw D after the original holder was declared closed.

**Fix:** divide immutable value-free outcomes from ephemeral key-bearing delivery. Durable original bytes apply to receipts/status/proofs/encrypted references, never raw D/X/K. Bind every actual key delivery attempt, including duplicates, to an admitted key-use lease and exact recipient/transport/sidecar incarnation before release. Completion must cover all such attempts and buffers. After closure/freeze/erasure, duplicate Execute or original lookup returns status/proof only, never cached key bytes. A later legitimate key acquisition needs fresh current key-use admission while retaining the same original root; it cannot generate a replacement root. Freeze/revocation must win before any new release is authorized.

**Required cases:** response lost before/after sidecar receipt; duplicate before/after holder completion; late old-sidecar response after restart; duplicate after B1/B3/B5; and lookup after erasure. No key result is durably journaled, replayed after closure or omitted from holder accounting. Label actual sidecar/serializer cleanup unqualified until native proof.

## D2 — High: `read-current` must not use exact-original cached response semantics

**Locations:** decision operation allowlist and duplicate paragraph; binding contract immutable/common envelope and response rules.

The same broad duplicate-original rule includes `read-current`. Replaying a signed original head/proof under an old OperationId can remain authentic after a new hold, revocation, freeze or authority revision. An immutable request ID proves identity, not that the response reflects a fresh current read. One component can cache this response as an exact original while a caller treats it as the current authority required for release/admission. A second component can perform a new linearizable read. The current text does not classify the operation to resolve the difference.

**Fix:** make `read-current` explicitly noncached, linearizable per attempt, and bind its authenticated response to a fresh non-reused RequestAttemptId/challenge plus authority incarnation/current revision and exact request. Application verification must reject another attempt's proof. Keep original OperationId/reservation stable for immutable effect/outcome recovery; use a separate freshness identity rather than resetting that original. Current admission/key release still rechecks the live head, not a cached original outcome or a historical physical receipt. An immutable `lookup-original` may recover original bytes, but cannot promote those bytes into a current-authority assertion.

**Required cases:** retain an authentic read-current response, change hold/revocation/head, replay that response under exact retry or restored actor/config state, and verify it cannot authorize new release/effects or qualify current state. No new destructive identity is created to get a fresh read.

## D3 — Medium: pin UDS ownership and Init/re-registration behavior

**Locations:** decision component scopes/app-channel authentication and UDS re-registration paragraph; binding contract AuthorityEnrollmentV1; I2/I4.

The retained output-binding proto provides Init/Invoke/ListOperations/Ping. Invoke contains data/metadata/operation and does not itself supply an authenticated application principal. Dapr scopes are not an authorization statement for arbitrary clients opening the component socket. The current design correctly requires private semantic proof, but leaves socket and Init behavior implicit enough for component/deployment units to disagree.

**Fix:** make F1/I2 require socket ownership/mount/ACL isolation allowing only the qualified daprd/component identities; apps must have no direct socket path. Bind Init to the enrolled component/configuration/authority digest. Reinitialization or re-registration with the same allowed profile must retain originals/native intents/holder uncertainty; changed store/trust/authority configuration must be rejected rather than reset or self-enroll. Keep semantic proof/current authorization mandatory even on a local connection. Include direct unauthorized socket invocation, changed Init, duplicate Init and Init during an uncertain operation in I4.

This is a design/conformance requirement, **not a claim that an installed socket is exposed**: the proposed component is absent and its isolation remains unqualified.

## Checked invariants and scope

- Platform/EventStore/Agents/custody applications use Dapr/neutral contracts only; provider SDKs, native clients, resource IDs and infrastructure credentials remain component-private. No parent AD-9 exception is asserted. Provider changes are configuration/component changes, subject to initial enrollment and unsupported first-release authority rollover.
- A complete semantic component owns admission/head/physical intent/original outcomes; applications do not recreate the protocol using generic state/crypto/object-storage calls. The pluggable component is explicitly proposed and unimplemented, not a discovered turnkey backend.
- Whole-tenant freeze, native/producer drain, verified survivor checkpoint, pre-arm actual alternate/tainted epoch destruction and atomic successor adoption survive the Dapr rewrite. Delayed native sends remain Unknown rather than being “canceled” by transport timeout or sidecar replacement.
- Earlier/unknown holds block; post-dispatch holds cannot obtain a conflicting successful pin; EventStore retains protection reserve/block/activation authority. The actual handoff and independently current proofs remain qualification gates.
- Dapr SDK/sidecar/UDS/gRPC/TLS/component buffers and diagnostics join copy closure. The design makes no stock-Dapr zeroing guarantee and rejects qualification if closure cannot be established.
- Original hierarchy/no-bare-C0 ancestry, interaction-level allocation exclusions, separate export closure, exact immutable receipts, independently retained trust and fail-closed unsupported authority replacement remain intact.
- OWNER-APPROVAL records architecture adoption under the later Dapr-only instruction; it grants no infrastructure spending/deployment, destructive experiment, Live/cohort run, register amendment or Available state. Existing OpenBao revival remains a negative control.

No deployment, native experiment, destructive effect or source change was performed. This file is the only artifact written by this review.

## Targeted closure review

**D1 closed:** the current decision, AD-25 and binding contract now distinguish durable value-free originals from ephemeral key delivery. KeyUseLeaseId/DeliveryAttemptId is admitted before release and binds exact recipient-process/transport incarnation. One release send is committed; duplicates/lookups after release admission return status/proof only. Closed/frozen/erased leases emit no keys. A fresh authorized use preserves the original root rather than generating one. Actual Dapr/serializer/sidecar buffer closure remains an explicitly unqualified prerequisite, not assumed from this rule.

**D2 closed:** read-current performs a new linearizable read on every invocation, binds the response to fresh caller attempt/challenge and current authority incarnation/revision/digest, and never uses an original-response cache. A subsequent release/effect still requires live permission and native comparison. Historical outcome lookup cannot become present authority.

**D3 closed:** daprd/component-only socket mount/ownership/ACL, immutable enrolled Init identity/configuration/trust and same-profile re-registration preserving originals/native intents/uncertain holders are explicit. Changed profiles/identities cannot enter through Init or create implicit authority rollover. Required proto imports/reflection/discovery checks remain F1 evidence gates.

### Original-result paging

The added paging rule preserves one physical manifest rather than introducing per-target admission: B1–B5 still owns the entire signed target set and one original terminal vector. A bounded header identifies the retained original by request/reservation/manifest, immutable reference, exact byte length/count and full SHA-256. Pages authenticate the same header identity, offsets/lengths and byte digests. The caller must reject holes/overlap/substitution and verify the complete reassembled vector/signature before exposing public Consumed. Interrupted assembly stays Unknown; page reads start no physical operation. Key delivery is never paged or cached. These rules close mixed-original/partial-vector/replay-as-new-result scenarios without changing the public receipt contract.

The full input remains unsplit and must fit actual pinned transport limits; no unverified 4 MiB override, truncation or hidden tighter public field limit is assumed. Exact wire sizes/runtime behavior remain F1/F2/F3 proof obligations. This review establishes design consistency, not successful transfer or native cleanup.

**Final verdict: D1–D3 resolved; no remaining actionable findings in this adversarial lens.** Dapr-only dependency isolation and the architecture-only approval boundary remain intact. Physical/component/provider/transport feasibility, deployment authorization and the complete separate owner qualification remain outstanding. No deployment, destructive experiment or test execution occurred during this follow-up.
