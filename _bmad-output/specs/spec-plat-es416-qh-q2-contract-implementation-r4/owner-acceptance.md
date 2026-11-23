# Owner review for PLAT-ES416-QH-Q2-R4

Prepared 2026-11-22. **R-P1, R-B1 and R-E1 are pending. v2 implementation and publication are gated.** Review the [spec](SPEC.md), the [D1–D5 and LC-01–LC-06 examples](synthetic-closure-examples.md) and the adopted design companions in the spec.

## Exact revision

The [40-file inventory](design-files.sha256) binds revision `PLAT-ES416-QH-Q2-R4` to aggregate `sha256:8d3849f084413f027ad22139db6ccfaa5fe44d2069be2b74e8a91a71fcee6d2e`. Paths are relative to the Platform root and sorted ordinally by UTF-8 bytes. File digests hash exact checkout bytes. For the aggregate, hash the concatenation of one record per inventory row in order, each `UTF-8 path + NUL + lowercase ASCII file digest + LF`.

From the Platform root, check exact file bytes and run the offline example checks with Python and `jsonschema`:

```bash
sha256sum --check --strict _bmad-output/specs/spec-plat-es416-qh-q2-contract-implementation-r4/design-files.sha256
run-with-temp python3 _bmad-output/specs/spec-plat-es416-qh-q2-contract-implementation-r4/verify_fixtures.py
```

The inventory, this acceptance record and BMAD memory sit outside the design set. The aggregate covers the inventory entries. Any bound-byte change requires a new revision, inventory and matching exact-design receipts. Completing the receipt record does not change the bound bytes.

## Recorded decisions and authority

- **Requester R4 approval.** On 2026-11-22 UTC, the requester replied “I approve all” to the completed R4 review. This records requester approval of the D1–D5 proposals, CI lifecycle examples and all three proposed owner scopes for `PLAT-ES416-QH-Q2-R4` at `sha256:8d3849f084413f027ad22139db6ccfaa5fe44d2069be2b74e8a91a71fcee6d2e`. The review conversation is the approval reference. The formal owner receipts below still require named authority and owning work references.
- **Requester R3 approval.** The requester said “approve all” for the three proposed R3 scopes. The approval remains attached to `PLAT-ES416-QH-Q2-R3` and its aggregate `sha256:a3e780d122f87fc58a9b689d2da8400893c551f34ea0fb9189b6eee3bcc85593`. R4 exact-design receipts are independent. [R3 record](../spec-plat-es416-qh-q2-contract-implementation-r3/owner-acceptance.md)
- **Platform R-P0 planning.** Administrator accepted Q1–Q6 planning on 2026-11-09. Platform owns the concrete profile, hosting, physical controls and lifecycle. Builds owns runner/tool delivery, and EventStore retains logical semantics and application assertions. [Approved Platform planning](../../planning-artifacts/sprint-change-proposal-2026-11-09.md)
- **EventStore planning.** The November 1 approval covers the mandatory atomic conflicting-write contract, supported Dapr dependency and erasure-verifier custody boundary. The November 9 approval applies P1–P5, including provider-free EventStore assertions and Platform-owned physical execution. The commit contract and AC1–AC11 apply in full. [November 1 decision](../../../references/Hexalith.EventStore/_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-01-story-4-16-atomic-conflicting-writes.md) · [November 9 decision](../../../references/Hexalith.EventStore/_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-09.md)
- **Published runner lifecycle.** The existing v1 attachment, timeout, exact-owner recovery and verified cleanup obligations govern the v2 proposal. [Published v1 contract](../../../docs/contracts/test-environment-v1.md#ci-ownership-recovery-and-finalization)

Review started at Platform `44dd1e52904cd07ef49be74edd947c75f9b76374`, direct Builds `0de063655f61d43f80410cbc01d2b492c5eac9cf` and direct EventStore `d810b7a9a4db5454783a5eb776d60b708264ea45`. No newer packet or qualifying R4 receipt was found in the checked-out documents, remote branches or related GitHub issue and PR search.

## Formal receipts

Record a receipt when a named maintainer acting under the relevant owner authority approves the exact final revision, aggregate and inventory, identifies their owning work, and accepts the corresponding scope with D1–D5 resolved. Record the UTC acceptance date and authoritative receipt reference. Independent scopes need independent evidence. A conditional response leaves its slot pending.

| Receipt | Owner decision and owning work needed | Formal state |
| --- | --- | --- |
| R-P1 · Platform contract and execution | Name the contract/execution maintainer and confirm the Platform owning work for the proposed `PLAT-ES416-QH` Q2 scope. Approve the paired public/private schema boundaries and v1 compatibility, profile/config producer split, observation and lifecycle truth, exact cleanup, and the Q2/Q5 boundary. | **Pending** · named maintainer, owning work confirmation, exact acceptance and receipt reference |
| R-B1 · Builds runner and tool | Name the runner/tool maintainer and Builds owning work. Approve mirror/embedded/package parity, registry and explicit plan/reader/writer support, credential confinement, native row/count accounting and helper timeout, late receipt and exact recovery. Identify the intended compatible tool release and its delivery work. | **Pending** · named maintainer, owning work, release identity, exact acceptance and receipt reference |
| R-E1 · EventStore consumer and assertions | Name the consumer and Test/assertion maintainer(s) and owning adoption work linked to 4.16. Approve explicit paired public/private v2 adoption with v1 compatibility, typed owned routes, test-set and 3.17 inventory ownership, provider-free oracles and both separately authorized comparison roles. Confirm how consumers preserve first outcomes and deny lane credit for unavailable or unverified observations. | **Pending** · named maintainer(s), owning work, consumer adoption boundary, exact acceptance and receipt reference |

D4 also requires the applicable credential/Security and EventStore 7.6 review. Receiver trust, custody, generations and qualified delivery remain separate prerequisites. Producer names, published tool identity, accepted consumer revision, supported executing tuple and the authorized command and target are pending.

## Verification

| Check | Result |
| --- | --- |
| Inventory and preservation | All 40 R4 bound files checked against the checkout. Independent aggregate recomputation matched. All 19 inherited R3 digests matched, and all five R3 packet files matched their committed bytes. |
| BMAD documents and fixtures | Coherence and preservation passes. Local R4 links, frontmatter, CAP-1–CAP-6, D1–D5, LC-01–LC-06, receipt slots and fixture inventory checked. Seven offline fixture tests passed, including published v1 envelope projections, canonical artifact bytes and references, credential scope, TRX joins and counts, and CI lifecycle traces. |
| Existing resource-free checks | Twelve focused Builds helper tests, 18 Platform lifecycle qualification unit tests, and the canonical v1 descriptor/result mirror test passed. |
| EventStore and publication gates | Story 4.16 verified in backlog. U1 partial, U2 pending, U3–U5 blocked and G-APPEND FAIL/BLOCKED. |

The checks are preparation evidence. RF-00–RF-12 and LC-01–LC-06 product acceptance, release publication, Builds adoption, Platform tool acceptance, integration qualification, EventStore consumer delivery and Q3–Q5 execution each require their existing evidence. Story 4.16 stays in backlog until a supported Dapr release can atomically reject conflicting event writes. Final conformance still requires the existing proof.
