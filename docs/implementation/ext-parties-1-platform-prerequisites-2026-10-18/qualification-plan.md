# Parties implementation and qualification after owner decisions

The final implementation/qualification packet is blocked until the references in `decision-packet.json` and `command-bindings.json` are resolved and accepted. This plan preserves the approved Branch B and policy and defines reviewable completion criteria. No source candidate, integration date, target or Live command is accepted by it.

## Original unit and successor decisions

Q5 and F1 require Parties/EventStore/Platform contract decisions before provider admission and history integration. The following rules are proposed for those decisions; no new wire contract is approved here.

| Operation | Original evidence and deadline | Required decision or proof |
| --- | --- | --- |
| Establish binding at t0 | Allocate one original unit E0 at t0, expiry t0 + 31,536,000 seconds | Bind exact aggregate/environment/policy/purpose, logical intent, binding version and source position to durable idempotency. Same intent recovers E0 after concurrency/crash/lost ack. |
| Exact retry | Reuse E0, original effective-at, evidence/admission revision, key lineage and deadline | Changed logical intent at the same aggregate/policy/purpose/effective-at must conflict rather than gain E0 authority or silently mint another unit. SDK `AdmitAsync` currently carries neither binding version nor logical ID; owners must choose an authenticated mapping or approve a shared contract change. |
| Rebind at t1 | Predecessor E0 retains t0 deadline; successor E1 gets its own t1 deadline | Close X and open Y at the same immutable t1; preserve original source positions and unique non-overlapping versions. Copies containing X and Y need independent protection by original unit, including duplicated snapshot transition evidence. |
| Revoke at tr | Actor evidence referring to E0 remains bounded by t0, never tr + 365 days | Resolve the predecessor through the authorized exact source/binding version before admission; choose how transition/certificate metadata survives without retaining expired actor data. The current processor calls `AdmitAsync(..., effectiveAt)` and `Satisfies(policy, effectiveAt)` for revocation too, so this requires an explicit coordinated mapping change, not a backend-only assumption. |
| Retry or restore after terminal expiry | No read, key recreation, readmission or new E0 authority | Reconcile current independent authority and original receipt; deny unknown lineage/stale epoch. A later revocation record cannot re-protect expired E0. |
| Read live successor Y after X expires | Y's own ciphertext/key/deadline survives independently | F1 must provide actor-free proof of complete source coverage, transition/version/interval uniqueness and authorization without reconstructing X. No current-binding substitution or relabeling expired actor events as profile exclusions. |

F1 decisions must name the certificate producer, schema/version and migration policy; exact tenant/Party/environment/source scope and partition positions; current authority revision/epoch/observation/valid-until and withdrawal checks; collision/conflict/gap behavior; signing/verifier authority; key/copy lifecycle; and Q7-approved minimal retention. Actor, provenance and logical references are protected data even if opaque. An actor-free structural claim cannot smuggle them into a longer-lived certificate.

Required positive control: X opens at t0, Y at t1; after t0 + 365 fixed days and before t1 + 365 fixed days, Y resolves uniquely with its original version/interval/source basis after restart and genuine restore, while X and all decrypting generations are irrecoverable. Before/at each transition and expiry, preserve half-open interval/exclusive-expiry behavior. Unknown, overlapping, stale, foreign or unverifiable proofs fail closed. Keep the current strict fold until the accepted continuation contract is implemented and qualified.

## Implementation scope after decisions

Use the existing [dedicated implementation plan](../../../_bmad-output/specs/spec-production-actor-history-custody/implementation-plan.md); T1 contracts already exist. T2 selects actual backend/lifecycle/key/copy adapters after Q1/Q2 and relevant Q3/Q4. T3 implements all seven SDK operations and authenticated event/snapshot envelopes after Q5, with a Parties-owned mixed-deadline codec. T4 explicitly registers dependencies in the actual Parties and EventStore persistence/replay/query hosts after Q6. T5 composes durable pending work/original receipts, complete copy fencing and approved recovery hooks after Q3/Q4/Q7. T6 and F2 add installed probes and execute the full matrix after targets are accepted. F1 remains a separate explicit continuation dependency.

Concrete existing/proposed paths are already listed in T2–T6: `src/Hexalith.Platform.Custody/`, `src/Hexalith.Platform.EventStoreHost/{Program.cs,Hexalith.Platform.EventStoreHost.csproj}`, Parties `Program.cs`/extensions, `PartyPayloadProtectionService.cs`, proposed `PartyIdentityHistorySnapshotCodec.cs`, `PartyDomainProcessor.cs`, EventStore retained-source reader/admission, and Parties `eng/verify-ext-parties-1.ps1` plus real installed integration probes. Proposed adapter filenames must follow the selected mechanism; no dummy provider is production.

Preserve current authentication, private alias mapping, exact tenant/source/target authorization before protected access, unavailable defaults, Consumer onboarding and no new direct Parties API/store. Protect actual persisted `HumanActorBindings` and `HumanActorTransitions.OriginalEvidence`, including typed/JsonElement/base64 snapshot paths. Never encrypt a mixed-deadline snapshot under only the oldest or newest deadline. Every key rotation/copy inherits its original unit's deadline.

Missing/stale authority denies release even if a key is readable. Hard expiry must remain cryptographically irreversible during backend/worker outages; a queued purge, tombstone, 404, recoverable deletion or application denial cannot establish it. Freeze inventory under a current fence and cover/invalidate every in-flight copy before one final original receipt. Cleanup's five-second waits and Pending semantics remain unchanged; they do not define unspecified operator bounds.

## Complete installed acceptance matrix

P-01–P-10 meanings come from [the owning Parties contract](../../../../agents/_bmad-output/specs/spec-story-5-4-dependency-unblock/parties-identity-contract.md#required-acceptance-vectors). Each row needs actual authenticated persisted end states independent of the tested provider. Cross-tenant scenarios use only owner-approved enrolled tenant references. Production's standing synthetic-tenant scope cannot silently become a second-tenant grant.

| Vector | Installed exercise | Independent receipt requirements |
| --- | --- | --- |
| P-01 | First provision, exact repeated/concurrent retry, lost acknowledgement and restart | Exactly one original tenant/Agent/Party creation+marker and original result revision; immutable ID retained. |
| P-02 | Changed Party/Agent/type/label/contract at same logical key; occupied unmarked ID; generic/composite bypass | Typed conflict and byte/count/digest evidence that original identity and foreign state stayed unchanged. |
| P-03 | Rejection-only replay then valid authorized provision | Rejection is not a creation marker; valid conditional creation occurs exactly once. |
| P-04 | Tenant role/wrong service/forged tenant or ID; concurrent tenants through one client | Denial before protected target lookup/effect; foreign persisted state and public count/name/log/accessibility disclosure unchanged. |
| P-05 | Wrong returned ID/tenant, unrelated same-type Organization, attempted type change | Exact identity mismatch blocks; no type-only substitution or replacement Party. Fault adapter/negative response authority must be approved. |
| P-06 | Missing/inactive/restricted/erased/unknown Party, null/gapped/stale/future source, outage | Fail-closed typed outcome; exact retry never repairs liveness or fabricates currency. |
| P-07 | Matching active human, non-human, missing/ambiguous/mismatched actor | Only a verified matching active human is eligible; safe results/logging; no human role created for the Agent Organization. |
| P-08 | Bind/rebind/revoke boundary, approved role/tenant/login rotation, action before/at T; profile erasure; successor after predecessor expiry | Original actor/version/interval/source proof within its retained window; current eligibility blocked on erasure; real restart/restore and F1 positive successor control with expired predecessor unrecoverable. |
| P-09 | Gap/overlap/conflicting revision, stale/absent authority, foreign historical read | Safe typed failure with no current-binding fallback; persisted source/authority checkpoints support the verdict. |
| P-10 | Caller/editor/regenerator aliases and current-read revocation | Stable-actor exclusions and withdrawal before terminal release; unavailable never becomes an empty eligible roster or proposal abandonment; Consumer behavior preserved. |

In addition to those vectors, every relevant installed row of [Platform test-plan.md](../../../_bmad-output/specs/spec-production-actor-history-custody/test-plan.md) is mandatory: actual host composition; concurrent/crashed admission; real profile-key destruction; exact expiry/rotation/decrypt-await boundaries; crash/lost-ack recovery at every destructive/receipt stage; late-copy races and foreign isolation; genuine pre-expiry data/key/lifecycle restore into quarantine; stale writers/epochs and unavailable anchor; successful still-retained same-Party and foreign-tenant controls. Test clocks and faults must be owner-approved target controls, not production timestamp edits or fixtures.

For each row retain exact source/package/image/profile/configuration identities, environment/instance/workload UIDs, target/cut/backup versions, operator and independent probe identities, before/after content-free source/snapshot/key/copy/work/receipt/authority observations, raw sanitized results, exit codes and artifact hashes. One durable stable operation/original receipt must survive restart and lost acknowledgement. Final copy coverage includes all generations, immutable backups and quarantine/drill artifacts. A missing class needs authenticated absence, never an omitted row.

## Owner acceptance and exact command packet

The existing register remains `Committed`, target `56cb4401c4179d431385ad3d931fefa402e4fc5a`, date `2026-10-08`. Its complete exact command is preserved as data in `decision-packet.json`. Current observed source commits are not replacement targets. The current Live branch fails closed even with complete environment-variable presence; do not run it while prerequisite references remain unresolved.

After decisions and implementation are complete, the Parties Maintainer/project owner and relevant Platform/EventStore/Product owners must accept:

1. Exact immutable Parties, EventStore and Platform commits; every selected root-submodule revision and source/package mode; package/image/profile/configuration digests and actual installed bindings.
2. New integration date from the owner, acceptance/approval records, exact enrolled tenant/credential/authority/inventory/restore/fault manifests and command bindings. No date is filled by this packet.
3. One exact full command and working directory that checks all three repository HEADs, clean tracked/untracked worktrees and exact root-submodule checkouts/content, binds installed digests/references, and runs the complete authenticated persisted P-01–P-10 plus custody/restart/restore/fault matrix. Do not reuse a stale single-repository guard or source-root fallback.
4. Exact intended evidence output location and hashing/receipt requirements, with no failed/skipped/unrun required lane and no credential/actor plaintext in output. Record clean-source state again after execution and retain any drift as failed qualification.

The existing invocation interface to bind is the argument array below, not an accepted executable compatibility command. Absolute approved paths avoid accidental source fallback. Guards, operator reference loading, complete implementation and acceptance are prerequisites; no new runner is created here.

```json
["pwsh", "-NoProfile", "-File", "<accepted-clean-Parties-root>/eng/verify-ext-parties-1.ps1", "-Mode", "Live", "-EventStoreRoot", "<accepted-clean-EventStore-root>", "-PlatformRoot", "<accepted-clean-Platform-root>", "-DependencyRegisterPath", "<accepted-register-path>", "-EvidenceDirectory", "<owner-approved-new-evidence-directory>"]
```

`acceptance.replacement.fullLiveCommand`, `integrationDate`, commits and acceptance record stay `null` until that final packet is concrete. A command template or populated environment variables do not authorize execution. Only the accepted complete command passing against clean exact checkouts can make EXT-PARTIES-1 Available. Local's retained 1,991 passes and LiveReadiness remain insufficient. This continuation runs neither.
