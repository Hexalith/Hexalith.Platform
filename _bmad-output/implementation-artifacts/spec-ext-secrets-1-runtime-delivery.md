---
title: EXT-SECRETS-1 remaining runtime adapters and acceptance packet
type: feature
created: 2026-10-15
status: done
route: dispatch
baseline_commit: 3a29fe039320ab13d95ff7d9801761dad72bfed9
review_loop_iteration: 1
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EXT-SECRETS-1 source preparation is done, but host 192.168.1.30 has no enrolled custody provider, tenant inventory, issuer/audience/profile, independent signer and trust/revocation authority, restricted replay credential, or independent qualification observers. Register target `f448db29493c1b30b0f18380188fe99b8b7606fe` stays Committed, so Story 5.4 cannot treat the seam as Available.

**Approach:** Add enrollable Dapr adapters and reference-only bindings, prepare isolated unapplied artifacts, and return a replacement target, configuration, and command packet. Name every missing input and its provisioning action.

**Decision:** Pin configuration, plan, and public-anchor hashes. Leave `AcceptedReplacementCommit` unset and do not commit. The owner fills that SHA and accepts it before any Live run.

## Boundaries & Constraints

**Always:** Date 2026-10-08; host 192.168.1.30; L/S/O/H/R = 300/30/600/86400/604800. Decision keys stay outside Platform and Agents. Open export disposition blocks with zero effects. Use Dapr. Credential references only. Adapters register only from valid explicit config. Executor, observer, and enrollment are separate processes. `dependencyAvailable` stays false. Story 5.4 stays in progress. Leave story 1.7 edits untouched.

**Never:** Deploy, apply, rotate, stop, restore, publish, push, or edit the Agents register. No Live compatibility run before acceptance. No new database or substitute all-copy, restore, or signer provider. Do not invent tenant, key, issuer, audience, profile, signer, or replay identifiers. In-repo fixtures are not installed proof.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Missing reference | Required reference absent or stale | No custody, sign, replay, revoke, or qualification effect | `DependencyNotAvailable: EXT-SECRETS-1` plus the input and provisioning action |
| Infrastructure only | Host, API, Dapr 1.18.1, OpenBao `hexalith-keys` | Record public references; application identities stay null | Readiness is not a binding |
| Enrolled binding | Distinct apps, current inventory/profile, external anchor, restricted replay reference | Exact reference and accepted timing | Revoked, changed, cross-tenant, or expired authority denies without cache |
| Unaccepted delivery | Packet exists; register binding does not | Local passes; Live preflight has zero effects | Available and Story 5.4 stay unchanged |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Custody/DaprPlatformHmacBinding.cs` — reuse `AddDaprPlatformHmac` and `IDaprPlatformHmacAuthority`; keep `version_id`, map mode, 30-second deadline, and unavailable defaults.
- Custody ports `IPlatformKeyInventoryAuthority`, `IIndependentDecisionAuthority`, `IDeletionCapabilitySigningProvider`, `IDeletionCapabilitySigningTrustProvider`, `IDeletionCapabilityRevocationAuthenticator`, `ITrustedEnvelopeReplayRegistrar` — decision authority stays verification-only. `PlatformPrivateCapabilityComposition` keeps replay and compromise credentials out of general DI.
- `eng/ext-secrets-1/qualify.py`, `cases.py`, `contract-case-matrix.txt` — Live client and 234-case plan. Source presence must not satisfy installation.
- `eng/ext-secrets-1` examples — application identities are null. Verified facts stop at 192.168.1.30, `https://192.168.1.30:6443`, Dapr 1.18.1, and OpenBao `hexalith-keys` / `hexalith-keys-active`.
- `eng/verify-ext-secrets-1.ps1` — preserve the Local class and XML audit. Do not touch story 1.7 composition, access-control, architecture, sprint status, or submodules.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Platform.Secrets.Runtime/` — add Dapr adapters for HMAC authority, inventory, issuer/audience/profile, verification-only trust/revocation, restricted replay, and compromise subscription. A missing reference names itself and performs no effect.
- [x] `src/Hexalith.Platform.Secrets.Qualification.Executor/`, `Observer/`, `Enrollment/` — add three apps for v1 `enroll`, `execute`/`lookup`, and `observe`. Observer state is an independent content-free journal. Missing all-copy or nonrollback references stay unavailable.
- [x] `tests/Hexalith.Platform.Secrets.Runtime.Tests/` and qualification tests — cover missing input, stale and cross-tenant denial, exact resolution, distinct identities, and observer proof independent of the executor.
- [x] `eng/ext-secrets-1/` — record a read-only census and unapplied reference-only artifacts. List each remaining null and its provisioning action. Keep production mutation disabled.
- [x] `eng/ext-secrets-1/qualify.py`, `test_qualify.py`, `replacement-acceptance-packet.json`, `README.md` — unaccepted source cannot clear preflight. Publish config, plan, and anchor pins plus the command template. Available requires a later passing S1–S4/v23 run on the accepted commit.

**Acceptance Criteria:**
- Given a missing reference, when preflight or an adapter runs, then it names that input and its provisioning action and performs no effect.
- Given only verified OpenBao and Dapr infrastructure, when the census is recorded, then inventory, profile, signer, replay, and observer identities stay unset.
- Given an unaccepted target, when Local and Live preflight run, then Local passes, Live returns `DependencyNotAvailable`, and Available stays false.
- Given the packet, when it is read, then the register and Story 5.4 status are unchanged and Live compatibility is not authorized.

## Implementation Notes

## Spec Change Log

- 2026-10-15 review loop 1: The enrollable adapters accepted proofs and effects that the custody ports they register into reject, and several missing inputs were named as a different input. Amended Design Notes and the acceptance criteria below. Known-bad state avoided: a replay receipt with a non-1 source revision or a retention the caller did not supply; a signing or transition effect that does not echo the request identity; a decision, trust, or revocation proof that does not echo the call; duplicate JSON or an out-of-range lifetime becoming current; caller cancellation becoming an unavailable proof; a missing store, mirror, or subscription blamed on another reference. KEEP: absent references still throw `DependencyNotAvailable: EXT-SECRETS-1`, name that input, and make zero Dapr calls; decision verification has no signing method; replay and compromise stay out of general DI; stale, revoked, cross-tenant, and secret-property proofs are not cached; the three qualification processes stay separate and unapplied; census and packet stay reference-only with null identities, null `acceptedReplacementCommit`, `dependencyAvailable` false, and the register target `f448db29493c1b30b0f18380188fe99b8b7606fe` still Committed; do not edit story 1.7 files; do not commit or deploy.

## Review Triage Log

- BH1 `MissingQualificationInputs` omits inventory and profile names — `false` — those adapters name their own missing references when invoked, and the packet lists every remaining null. Qualification is specified to stay unavailable for all-copy and nonrollback.
- BH2 observer HTTP ignores the body — `false` — with no accepted configuration, `Observe` denies before any journal append, so a null request id does not change the zero-effect result.
- BH3 reference strings are the Dapr app ids — `false` — the frozen intent forbids invented identifiers, and a failed call becomes an unavailable proof with no effect.
- BH4 transition digests are omitted and `ApplyKeyInventory` is authorized by a read — `medium` — `TransitionAsync` sends only scope and revisions, then `effect-transition`; the operation name is not sent. Recover is not a local restore substitute: a missing journal reference throws before any effect.
- BH5 signing effects again and omits the capability digest; revocation does not echo the evidence digest — `medium` — `SignAsync` calls `effect-sign` whenever the query is current, and the body is only tenant, request id, and key version.
- BH6 decision checks — `false` for another tenant, because `DecisionAuthorityExpectedBasis` has no tenant. `medium` for ignored `issuedAt`, uncompared basis fields, empty SPKI, and `TimeSpan.FromSeconds` on an unchecked lifetime.
- BH7 replay registration — `high` — `RegisterAsync` returns null unless the query is already current, ignores `evaluatedAt` and `retention`, and `TrustedEnvelopeReplayVerifier.Exact` requires source revision 1 and `RetainUntil == FirstSeenAt + retention`.
- BH8 wrong denial names — `medium` for blaming every HMAC gap on the profile and every compromise gap on the credential. `false` for numeric `keyState` JSON, because `String` rejects non-strings and the HMAC binding `Check` rejects undefined states. `low` rejected for `GetResult`: the HMAC profile port is synchronous and the host does not capture a synchronization context.
- BH9 packet gate — `false` — a missing packet leaves the existing fail-closed preflight; null anchors match the unaccepted packet; `commit == target == HEAD` is the later acceptance condition, not a bypass.
- BH10 reference-file screening — `medium` for `UnauthorizedAccessException` escaping `Load`. `false` for unmapped `secret` or `token` names, because `Load` copies only known fields. `low` rejected for `IsReference` width: tightening it adds a new identifier policy.
- BH11 story 1.7 files in the diff — `false` — those edits were already in the working tree before this delivery, and this delivery does not modify them.
- BH12 static transport preflight — `medium` — `static_transport_preflight.py` reads one fixed production kubeconfig path. Pre-existing recovery preparation, not this delivery.
- EC1 caller cancellation — `medium` — a caller `OperationCanceledException` misses the timeout filter and is swallowed by `catch (Exception)`.
- EC2 lifetime overflow — `medium` — `TimeSpan.FromSeconds` is called on the raw proof integer.
- EC3 undefined HMAC enum — `false` — `DaprPlatformHmacKeyProvider.Check` returns `OutsideKeyWindow` unless `Enum.IsDefined` succeeds.
- EC4 call timeout — `false` — the key provider's outer wait uses `options.CallTimeout`.
- EC5 synchronous profile wait — `low` rejected — same port constraint as BH8.
- EC6 key window inside the adapter — `false` — `Check` applies `NotBefore` and `VerifyUntil` before any secret read.
- EC7 approver issuance — `medium` — `VerifyApproverAsync` does not compare `issuedAt`, source, binding, or evidence.
- EC8 basis fields — `medium` — `VerifyExpectedBasisAsync` does not compare outcome, predecessor, root, evaluations, or required roles.
- EC9 boundary decision id — `medium` — success compares the proof to `manifest.DecisionId` and not to `expected.DecisionId`.
- EC10 replay retention — `medium` — same receipt path as BH7.
- EC11 revocation digest — `medium` — the digest is sent and not required back.
- EC12 signing echo — `medium` — a current `Signed` proof is not required to echo the request id or key version.
- EC13 and EC14 empty SPKI — `medium` — `FromBase64String` of an empty string becomes an empty key.
- EC15 inventory alias — `medium` — authorization compares purpose and version only.
- EC16 transition digests — `medium` — same path as BH4.
- EC17 duplicate JSON — `medium` — `JsonDocument.Parse` keeps the last duplicate, so a later `status` can hide `Revoked`.
- EC18 secret text in values — `medium` — the denylist matches property names only.
- EC19 unreadable reference file — `medium` — same escape as BH10.
- EC20 observer journal races — `medium` — `ContentFreeObserverJournal` uses an unsynchronized dictionary.
- EC21 observe request id — `false` — same zero-effect path as BH2.
- EC22 through EC28 `PlatformDaprBindings` ambiguous topics, dropped servers, wildcard verbs, duplicate subscriptions, and missing discovery — `medium` — the cited branches exist in story 1.7 composition. Pre-existing; this delivery does not edit that file.
- EC29 non-object replacement packet — `medium` — `packet.get` runs outside the parse guard, so a list becomes `AttributeError` and `QualificationFailed`.
- EC30 through EC32 local Dapr fixture cleanup, shared list, and non-object payload — `medium` — those paths are in the pre-existing story 1.7 fixture.
- EC33 recovery inventory key names — `medium` — pre-existing recovery verifier; `.pfx` is not in `secret_path`.
- EC34 HMAC denial name — `medium` — `AddExplicitHmac` returns `runtime.profileReference` for every invalid option.
- EC35 compromise denial name — `medium` — a null mirror or subscription is reported as the compromise credential.
- EC36 configuration pin — `medium` — `replacement_packet_inputs` accepts any 64 hex digits and does not compare them to the loaded configuration digest.
- VG1 `Dapr__DeclarativeSubscriptions` unasserted — `medium` — story 1.7 server environment. Pre-existing.
- VG2 packet pin branches unasserted — `medium` — the four denial names have no preflight test that deletes the corresponding check.
- VG3 HMAC `ResolveAsync` proof — `medium` — tests throw on null options and never execute a current or revoked proof.
- VG4 decision profile and boundary — `medium` — tests never call `ResolveAsync` or `VerifyAuthorityBoundaryAsync`.
- VG5 trust, revocation, and compromise proofs — `medium` — tests only cover the missing-reference throw.
- VG6 30-second window and secret property — `medium` — current fixtures use a 21-second window and no forbidden name, so deleting either check still passes.
- VG7 qualification HTTP status — `low` rejected — the hosts are unapplied and already return 503; a host harness is more than a direct correction.
- VG other replay receipt versus `Exact` — `high` — the adapter test accepts source revision 4, and `TrustedEnvelopeReplayVerifier.Exact` requires revision 1 and exact retention. Same defect as BH7.
- R2 carried EC22–EC28 and EC30 — `medium` — carried: `PlatformDaprBindings` and `eng/test-local-dapr-security.py` still have the story 1.7 branches described in those rows. This delivery did not edit them.
- R2 non-string replacement commit — `medium` — `acceptedReplacementCommit is None` does not catch `true`, `0`, or an object, and the later check requires `isinstance(commit, str)` before comparing target and HEAD.
- R2 missing packet file — `false` — a missing packet leaves the existing fail-closed preflight, which the amended spec keeps.
- R2 hosts always return 503 — `medium` — the remap exists, and the only test uses an empty configuration, so deleting the 200-to-503 remap still passes.
- R2 compromise mirror — `medium` — `RegisterAsync` calls `effect-compromise` without `RequireMirror`, so a missing mirror does not stop that call.
- R2 compromise receipt fields — `medium` — `Receipt` checks tenant and event identity and accepts an empty `affectedBatchIds`.
- R2 numeric key state — `medium` — `Enum.TryParse("0")` can yield a defined state whose name is not `0`, and `IsDefined` then succeeds.
- R2 invalid profile window name — `medium` — `ExpectedProfile is null` after the field checks returns `runtime.profileReference`.
- R2 record revision digests — `medium` — `RecordRevisionAsync` assigns `exactStateDigest` to both predecessor and target, and the method has no separate predecessor.
- R2 contradictory transition effect — `medium` — `TransitionAsync` calls `effect-transition` when the query proof is present, does not echo, and is not absent.
- R2 live-flag and mismatched-commit tests — `medium` — the pin tests cover a null commit and a matching commit with false flags, not a foreign commit or either flag set true.
- R2 declarative subscription flag — `medium` — story 1.7 sets `Dapr__DeclarativeSubscriptions` and no prepared-environment test reads it.
- R2 scheduler bind mount — `medium` — story 1.7 passes a scheduler data directory and the composition tests do not assert the `/var/lock` bind mount.
- R2 projection topic removal, empty Dapr home, gateway project lookup, and scheduler path guards — `medium` — those branches are in the pre-existing story 1.7 composition and host files.
- R2 recovery preflight wait, secret-path names, and v4 manifest anchor — `medium` — pre-existing recovery preparation, not this delivery.

## Design Notes

Adapters are enrollable code, not installed authorities. Live still requires an accepted binding and three distinct P-256 anchors. All-copy and nonrollback providers stay external.

The adapters must satisfy the custody consumers they register into:

- Replay receipts must pass `TrustedEnvelopeReplayVerifier.Exact`: source revision 1, UTC offsets, `FirstSeenAt <= evaluatedAt`, and `RetainUntil == FirstSeenAt +` the caller retention. `RegisterAsync` persists a first sighting when that exact receipt is absent. It does not demand a current proof before that first effect, and it does not effect again when the exact receipt already exists. `evaluatedAt` and `retention` are binding inputs.
- Signing query and effect bodies include tenant, signing request id, key version, and the capability digest. `effect-sign` runs only when the current proof is not already the signed outcome for that same request, key, and digest. The signed proof echoes those fields. Revocation authentication echoes the evidence digest and event identity.
- Transition query and effect bodies include scope, both revisions, and both digests, and the effect proof echoes them. `VerifyTransitionAsync` performs no effect. `ApplyKeyInventory` is not authorized by a read that omits that operation.
- Decision resolution returns null for a null basis, a non-positive or overflowing lifetime, or an empty SPKI. Basis verification compares decision, contract, both versions, predecessor, root, evaluations, and required roles. Approver verification compares `issuedAt`, source, binding, and evidence. Boundary verification returns false when `expected.DecisionId` differs from `manifest.DecisionId`. Trust resolution returns null for an empty SPKI.
- Inventory authorization compares tenant, purpose, version, alias, and authority. HMAC `ResolveAsync` must be tested with a current matching proof and a revoked `keyState`. The existing binding `Check` remains the secret-read gate.
- `SecretsProof` rejects duplicate names and secret-bearing property names. Caller cancellation propagates. Only the 30-second budget timeout becomes an unavailable proof.
- A missing HMAC store, credential, authority, profile, or timeout names that input. A null compromise mirror or subscription names that input. `Load` catches `UnauthorizedAccessException` and returns null. A non-object replacement packet is `ReplacementAcceptancePacket`, not `QualificationFailed`. `configurationSha256` equals the digest of the loaded configuration. Preflight tests cover `LiveCompatibilityNotAuthorized`, `PinnedPlanSha256`, `PinnedConfigurationSha256`, and `PinnedPublicAnchorSha256`, each with zero Dapr calls.
- `ContentFreeObserverJournal` serializes append and read. The HTTP hosts stay unapplied and return 503 while required references are missing.

## Verification

**Commands:**
- `pwsh -NoProfile -File eng/verify-ext-secrets-1.ps1 -Mode Local -EventStoreSourceRoot ../eventstore` with unique artifact and evidence directories — expected: warning-free build and XML audit.
- `python3 -m unittest discover -s eng/ext-secrets-1 -p 'test_*.py' -v` — expected: pass, Available remaining false.
- Live preflight without an accepted binding — expected: non-zero exit, `DependencyNotAvailable: EXT-SECRETS-1`, zero Dapr calls.

**Manual checks:**
- Packet and runtime documents contain references only, and story 1.7 files have no delivery diff.
