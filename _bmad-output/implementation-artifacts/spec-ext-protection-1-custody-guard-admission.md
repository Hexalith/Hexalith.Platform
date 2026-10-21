---
title: 'EXT-PROTECTION-1 custody guard admission'
type: 'feature'
created: '2026-10-21'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'c65d87a6f8f4a05fa3bea4e24ac778bf23f12cd6'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-ext-protection-1-custody-provider.md'
  - '{project-root}/_bmad-output/specs/spec-ext-secrets-1-interaction-custody/custody-binding-contract.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `CustodyKeyLifecycleBindingAuthority` still denies every actor check that runs before `ExecuteAsync`. The custody provider at `cd702594989a513749cdb8e446cd78d3a421bc49` can pin, unpin, and destroy, but admission never reaches that provider.

**Approach:** Supply the installed guard admission for protocol `custody-lifecycle-v1`, logical role `interaction-custody`, and component `bindings.hexalithcustody`. A missing guard or a missing nonrollback journal denies and writes nothing. A present guard accepts an exact operation and rejects a changed digest, a foreign tenant, and a stale revision.

## Boundaries & Constraints

**Always:** Implement `AuthorizeOperationAsync`, `AuthorizeEffectAsync`, `VerifyRegistrationAsync`, `ValidateStateAsync`, and the anchored admit, record, recover, and verify transitions. Keep one C# type per file. Preserve `VerifyOutcomeAsync` as a witness read. Preserve the ObservedRevival file hash `6efa381bc70b3de57a24142e5e679f53781c7f89d5ab6740fb7664966f30888e`.

**Never:** Treat OpenBao HMAC or ordinary Raft restore as a DEK or manifest provider. Edit the Agents register. Mark EXT-PROTECTION-1 `Committed` or `Available`. Run the EventStore Live command. Fill `runtime.compromiseGuardMirrorReference`, `runtime.currentRevocationReference`, `runtime.publicTrustReference`, `runtime.publicAnchorRetentionReference`, or the executor, observer, and enrollment role anchors. Change the actor, the authority interface, host startup, or the installed runtime reference.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Exact operation | Guard and journal present; tenant, digest, and current revision match | Operation, effect, registration, and state checks return true | Reads write nothing |
| Anchored transitions | Same exact predecessor | Admit retains; record advances; recover keeps the predecessor and then verify succeeds; verify reads | A second exact admit does not write again |
| Changed digest | Same tenant and revision, different digest | False | No write |
| Foreign tenant | Any other tenant, including another actor scope | False | No write |
| Stale revision | Revision below the journal head | False | No write |
| Missing guard or journal | Either port null | Every new check returns false | No write; witness outcome checks stay as they are |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Secrets.Runtime/CustodyKeyLifecycleBindingAuthority.cs` — add optional guard and journal ports after the witness. Null either port keeps today's denial. `RecordRevisionAsync` stays denied. `VerifyOutcomeAsync` still reads only `ICustodyOutsideWitness`.
- `src/Hexalith.Platform.Custody/CustodyKeyLifecycleActor.cs` — do not change. It already calls the named checks before `ExecuteAsync`. Scope is `GetActorId(tenant)` plus the private state key. A transition is in-tenant only when `ScopeId` starts with `GetActorId` of the enrolled tenant.
- `src/Hexalith.Platform.Custody/ICustodyKeyLifecycleAuthority.cs` and `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IAnchoredStateTransitionAuthority.cs` — reuse. Admit does not advance. Record advances one revision. Recover resolves only an already admitted exact original at the unchanged predecessor. Verify is a fresh read.
- `src/Hexalith.Platform.Secrets.Runtime/CustodyBindingContract.cs` — reuse `custody-lifecycle-v1`. Do not add a binding operation or sort a signed manifest.
- Installed `runtime-reference.json` and `eng/ext-secrets-1/provisioning/role-inputs.proposed.json` — do not edit. Role anchors `qualification.executor`, `qualification.observer`, and `qualification.enrollment` stay null, as do `runtime.publicTrustReference` and `runtime.publicAnchorRetentionReference`.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Platform.Secrets.Runtime/ICustodyInstalledGuard.cs` — internal port for one enrolled exact operation: tenant, operation, method, request digest, revision, and state digest. Enrollment is accepted only for `custody-lifecycle-v1`, `interaction-custody`, and `bindings.hexalithcustody`.
- [x] `src/Hexalith.Platform.Secrets.Runtime/CustodyInstalledGuard.cs` — in-process enrollment. It stores no DEK and does not read HMAC or a Raft snapshot.
- [x] `src/Hexalith.Platform.Secrets.Runtime/ICustodyNonrollbackJournal.cs` — internal head, admitted original, and write counter. A refused call must not increment the counter.
- [x] `src/Hexalith.Platform.Secrets.Runtime/CustodyNonrollbackJournal.cs` — admit retains, record advances, recover marks the admitted original without moving the head, verify reads. This is not a local restore of a destroyed key.
- [x] `src/Hexalith.Platform.Secrets.Runtime/CustodyKeyLifecycleBindingAuthority.cs` — consult both ports. Exact tenant, digest, and current revision pass. A changed digest, foreign tenant, or stale revision returns false before any journal mutation.
- [x] `tests/Hexalith.Platform.Secrets.Runtime.Tests/CustodyKeyLifecycleGuardAdmissionTests.cs` — cover the matrix, including either port missing. Keep the existing witness tests green.
- [x] Commit only those Platform sources and tests after `npx commitlint --edit` accepts the subject.

**Acceptance Criteria:**
- Given a present guard and journal, when the actor's pre-execute checks run for an exact operation, then each named check returns true and only admit, record, and recover write.
- Given a missing guard or a missing journal, when any named check runs, then it returns false and the write counter stays unchanged.
- Given this delivery, then the Agents register is unchanged, EXT-PROTECTION-1 is not `Committed` or `Available`, Live did not run, and the named role and trust-anchor fields are still null.
- Given `raft-probe-attempt-6.result.json`, then its SHA-256 is still the ObservedRevival hash in Intent.

## Implementation Notes

Commit `d189c7de74293e1a718e5e78b1b3b7c0e47b48b5` contains the guard, the journal, the authority, and `CustodyKeyLifecycleGuardAdmissionTests`. Review follow-up `d0f992807692161dbc2b0b11b4ed0008de1f738c` adds the forged-target and divergent-head assertions. Debug build had 0 warnings. The two named test classes ran 13 tests, 0 failed, 0 skipped. ObservedRevival SHA-256 is unchanged. EXT-PROTECTION-1 remains `Uncommitted`. Host composition still supplies the witness only, so an installed process keeps denying until both ports are supplied.

## Spec Change Log

## Review Triage Log

| ID | Verdict | Evidence and route |
|---|---|---|
| B1 | false | `ApplyAsync` does save twice, but the second save is after `ExecuteAsync`. Design Notes require another admit or record to stay on the original checkpoint, so the retain save returning false is the specified limit. |
| B2 | false | Recover is specified to leave the predecessor unchanged. `ValidateStateAsync` rejecting the prospective target revision is that rule, not an accidental advance. |
| B3 | false | After recover the head is still the predecessor, so the admitted original still meets record's current-head rule. The spec does not make recover exclusive of record. |
| B4 | false | `AuthorizeEffectAsync` has no digest argument. The actor passes the digest to `AuthorizeOperationAsync`, which rejects a changed digest. The exact-operation row requires effect and registration to pass together, and those payloads do not share one canonical digest. |
| B5 | false | A transition target digest is the state fingerprint. The spec does not bind it to the enrolled request digest. |
| B6 | low | A scope with the enrolled actor id and another suffix is admitted. Rejected: the code map defines in-tenant as that prefix, the actor has one state key, and pinning the private key would add a constant. |
| B7 | false | `RecoverableAnchoredState.Validate` rejects a malformed transition before the authority. A null `TargetBytes` is not an actor input. |
| B8 | false | The second admit before record returns true and does not write. The anchored-transition test requires the post-record retry to return false because the head has moved. |
| B9 | false | Host startup is unchanged on purpose. Absent ports still deny. |
| B10 | false | The journal is the in-process one-slot port the spec asks for. Actor turns are non-reentrant, and `Prepare` already rejects an oversized pending state. |
| B11 | false | The code map says not to host the actor. The tests call the checks the actor calls. |
| B12 | medium | `WorksProductionProfile` accepts a runtime floor while the proof pin is an exact hash. Not this story. Defer. |
| B13 | medium | `works_toolchain.qualify` can miss the installed `daprd` hash in the live gate tests. Not this story. Defer. |
| B14 | medium | Proof rows store `target_commands`, so earlier rows record later commands. Not this story. Defer. |
| B15 | medium | `real-module-compare.py.txt` slices failing rows with `[:0]`. That file is Story 1.13 evidence, not this guard. Defer. |
| E1 | false | Same claim and disproof as B4. |
| E2 | false | The exact-operation row requires registration to pass for the same enrollment as effect. A `RegisterWrappedKey` method gate would fail that row. |
| E3 | false | Foreign identity is defined as another tenant. The actor digest includes the object identity, so another object fails `AuthorizeOperationAsync`. |
| E4 | low | Same prefix claim as B6. Rejected for the same reason. |
| E5 | false | `VerifyTransitionAsync` returns false for a null transition before the journal. Same reachability disproof as B7. |
| E6 | false | Same pending-size disproof as B10. |
| E7 | medium | `qualify` hashes `daprd` only after the version command, and the live gate test does not fail a mismatched pin. Not this story. Defer. |
| E8 | medium | `works_toolchain.py` calls `check_output` with no timeout. Not this story. Defer. |
| E9 | medium | Nothing compares the qualified hash with the hash used at prepare. Not this story. Defer. |
| E10 | false | Same claim and disproof as B4. |
| V1 | medium | Pre-verified: deleting `qualify` still lets the earlier `run_verification` failures pass. Not this story. Defer. |
| V2 | medium | Pre-verified: deleting the target-byte hash check still passes because every test transition either matches or fails on the predecessor. Patch: add the mismatched-bytes case. |
| V3 | medium | Pre-verified: equal revision with a different head digest is untested, and returning true on equal revision would still pass. Patch: add that case. |
| V4 | medium | Works CI still sets `runtime-version: '1.18.3'` while the new pin says `1.18.7`. Not this story. Defer. |
| V5 | medium | `verify_works_host.py` still says only "Identity or Parties" after a linked-snapshot drift. Not this story. Defer. |

## Design Notes

The guard and the journal are separate optional constructor arguments. Production composition does not receive them, so an installed null reference still fails closed. Authorization, registration, state validation, and verify are reads. Admit, record, and recover are the only writers, and only after the exact check passes. A lower revision is stale. A higher revision is not current, so it also returns false and writes nothing.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Platform.Secrets.Runtime.Tests/Hexalith.Platform.Secrets.Runtime.Tests.csproj --configuration Debug -nodeReuse:false` — expected: success, then run the built test assembly for `CustodyKeyLifecycleGuardAdmissionTests` and `CustodyKeyLifecycleBindingTests`.
- `sha256sum` of `raft-probe-attempt-6.result.json` — expected: `6efa381bc70b3de57a24142e5e679f53781c7f89d5ab6740fb7664966f30888e`.
