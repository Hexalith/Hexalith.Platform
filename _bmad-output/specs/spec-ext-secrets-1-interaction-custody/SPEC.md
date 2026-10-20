---
id: SPEC-ext-secrets-1-interaction-custody
created: 2026-10-20
companions:
  - implementation-plan.md
  - custody-binding-contract.md
  - ../../planning-artifacts/architecture/architecture-ext-protection-1-custody-2026-10-18/ARCHITECTURE-SPINE.md
  - ../../../docs/implementation/ext-secrets-1-erasure-authority-decision-2026-10-20.md
sources: []
---

# Interaction custody through Dapr

## Why

Agents Story 5.4 lacks a qualified owner of interaction-root destruction and independently surviving lifecycle authority. Ordinary OpenBao/Raft restore demonstrated revival. Implement the approved erasure/recovery design through Dapr while preserving existing custody contracts and preventing any application dependency on a particular infrastructure provider.

## Capabilities

- **CAP-1**
  - **intent:** Authorized callers use the one original protected interaction root.
  - **success:** Complete tenant/interaction/alias/purpose/version/lifecycle/store identity and existing AES-GCM/AAD/KEK semantics survive wrap, unwrap, rotation and exact retry; no erased or uncertain root is reissued.
- **CAP-2**
  - **intent:** Hold and destruction authorities enforce their original precedence.
  - **success:** Earlier effective/unknown pins block destruction; later post-dispatch holds never receive a conflicting successful physical pin; guard dispatch, protection reservation/block/activation and custody arm keep their distinct owners.
- **CAP-3**
  - **intent:** One manifest erases every selected target while preserving survivors.
  - **success:** Exact sorted 1–1,000 targets lose decryptability at one irreversible shared cut after complete preparation; held/non-target controls recover and no partial/early terminal vector appears.
- **CAP-4**
  - **intent:** Callers recover the original outcome after loss or restart.
  - **success:** Exact original request/reservation returns byte-identical ordered receipts/signatures and authenticated coverage; duplicates create no new destructive identity, lookup starts no effect and outage never becomes NotStarted.
- **CAP-5**
  - **intent:** Restore and rotation cannot revive content or stale authority.
  - **success:** Independently enrolled current authority fences restored app/config/key/receipt/actor state and alias/version reissue; missing authority fails closed, including unsupported first-release authority replacement.
- **CAP-6**
  - **intent:** Owners can account for every content, key and transport copy.
  - **success:** Inventory and verified closure cover backups, separately encrypted exports, late producers and Dapr SDK/sidecar/component/UDS/gRPC/TLS buffers; incomplete holder/copy evidence prevents irreversible receipts.
- **CAP-7**
  - **intent:** Custody remains infrastructure-independent and accurately qualified.
  - **success:** Application paths use existing Dapr/provider-neutral contracts only; configurable component/provider changes require no application code change; transport retries/loss/restarts/limits fail closed and actual physical proofs precede production binding.

## Constraints

- Adopt custody AD-16–AD-25 and the corrected decision as required companions. All application infrastructure access uses Dapr. No native cloud/database SDK, provider endpoint/resource type/credential or new AD-9 exception enters Platform, custody application services, EventStore or Agents.
- Provider-specific implementation/configuration is isolated inside a qualifying Dapr infrastructure component. A generic binding, secret delete, state/crypto saga or transport success supplies no physical guarantee; the complete custody component/provider is currently absent.
- Shared physical cut with recoverable exact-original publication is the approved criterion. No native cross-service ACID claim, all-copy waiver or optimistic terminal result is permitted.
- Preserve existing six operations, statuses, AES-GCM/AAD/opaque original identities, anchored transitions, v21/v23 ordering, non-expiring revocable capability and independent approval issuer. Preserve timing `300/30/600/86400/604800`, date `2026-10-08`, host `192.168.1.30` and **Open** export disposition.
- Retain the 1,000-registration/10,000-request actor limits; reserve completion capacity and fail closed before exhaustion. No implicit archive/shard/eviction or premature original/public-verifier expiry.
- First-release authority replacement/incarnation rollover is unsupported. Current authority, copy closure, actual provider/transport/key-buffer evidence and all other Story 5.4 inputs remain mandatory.
- Native experiments require separate exact resource/cost/cleanup authorization. This spec adoption executes no code, deployment, destructive qualification, owner Live command or register amendment and establishes no Available state.

## Non-goals

- Mandating AWS or another infrastructure provider; adding native application dependencies or changing accepted Product hold/export policy.
- Qualifying export/actor-history lifecycle, replacing owner authority, expanding actor limits or bypassing the accepted OQ-5.4-1 cohort.

## Success signal

An independently observed end-to-end Dapr custody implementation preserves exact originals, holds and survivors while old backups/configuration/keys/receipts cannot revive selected content. Every required native and Dapr transport/copy proof passes before production binding; dependency availability remains governed by the separately accepted complete owner cohort.

## Open Questions

- **Q1 — Feasibility evidence:** Can the selected semantic Dapr component and an actual physical provider satisfy the complete erasure/current-authority contract, including sidecar/transport key-copy closure? F0/F1 must prove a viable construction or stop; a native bypass is forbidden.
- **Q2 — Installation inputs:** Actual component/provider artifacts, independent placement/enrollment/public trust/scopes, copy/restore/original references and numerical operating/retention/admission limits must be produced and verified. No architecture approval or proposed string supplies them.
