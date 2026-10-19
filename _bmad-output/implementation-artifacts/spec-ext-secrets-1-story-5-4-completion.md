---
title: EXT-SECRETS-1 Story 5.4 binding completion and qualification reconciliation
type: feature
created: 2026-10-19
status: in-progress
route: dispatch
baseline_commit: f0e33167fcdb5f539da159c54f7505b72a65c9b2
review_loop_iteration: 2
context: []
---

<frozen-after-approval reason="user's explicit complete-delivery instruction; preserve accepted policy">

## Intent

Complete the implementable EXT-SECRETS-1 runtime bindings and reconcile every missing input from the accepted Agents Story 5.4 qualification. Deliver reviewed source, concrete reference-only provisioning artifacts, reproducible validation and a detached immutable candidate packet. Genuine persisted Live qualification is required for availability; preserve closed gates whenever prerequisites are absent.

## Boundaries & Constraints

Reuse Platform custody, its private capability composition and independently current authority ports. Preserve the accepted 2026-10-08 integration date, host 192.168.1.30, L/S/O/H/R = 300/30/600/86400/604800, Open export disposition, and decision private keys outside Platform/Agents. Keep signing, authenticated transport credentials and public trust references distinct. Originals, terminal results and lost-result lookup retain exact identity across restart/restore.

Preserve unrelated dirty work in the original Platform checkout by developing in a separate worktree. Do not edit the Agents register, enable consumers, fabricate anchors/authorities, generate replacement destructive identities, or use ordinary OpenBao snapshot custody as an all-copy provider. The accepted erasure-first decision stops option A production construction until a concrete independently surviving authority and copy hierarchy exist. Missing runtime inputs and that architecture dependency are delivery blockers, not facts to invent. No local test establishes Available.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Historical reconciliation | Exact 67-input evidence and installed reference configuration | Every original name accounted for with owner, action, dependency, evidence and current status | Reject changed/partial evidence |
| Private credentials | App identity absent, or credential reference supplied as endpoint | No dispatch or authority inferred from a credential name | Name exact missing binding |
| Compromise composition | Explicit subscription, authenticator, registrar and mirror | Existing private subscriber registers; raw registrar remains private | Missing inputs create no subscriber/effects |
| Lifecycle | Qualified backend/independent antirollback authority absent | Keep custody unavailable and prepare exact provisioning requirements | No irreversible receipt or restoration claim |
| Qualification | Target, configuration, plan, independent anchors or cohort prerequisite missing | Persist refusal with zero effects | DependencyNotAvailable |
| Loss/restart/restore | Original operation uncertain | Exact original lookup; no new execution identity | Preserve journals and Unknown |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Secrets.Runtime/SecretsRuntimeConfiguration.cs`, `SecretsRuntimeServiceCollectionExtensions.cs`, `DaprCapabilityAdapters.cs`, `SecretsDapr.cs`: explicit adapters currently conflate some credential references and app IDs; private compromise registration only validates.
- `src/Hexalith.Platform.Custody/PlatformPrivateCapabilityComposition.cs`: reuse private replay/subscriber closures. Existing lifecycle actors require real qualified physical provider and independent anchored authority.
- `eng/ext-secrets-1/qualify.py`, `prepare_acceptance.py`, `cases.py`: preserve complete 234-case client, immutable journals, public-only schema, exact register/target/configuration/plan/anchor/command pins.
- Agents `external-dependency-register.md` and `tests/story-5-4-owner-qualification-2026-10-18/`: authoritative historical command and 67-input refusal. `OQ-5.4-1` is gated on protection commitment, passing host verifier/profile and installed authorities.
- `docs/implementation/ext-secrets-1-dek-provider-decision-2026-10-18.md`: real Raft revival, stopped production construction; retain this blocker.

## Tasks & Acceptance

- [x] Runtime adapters and configuration: separate endpoint/credential bindings, implement missing safe private composition and necessary lifecycle bindings without a substitute backend; add meaningful denial and exact identity tests.
- [x] `eng/ext-secrets-1/reconcile_inputs.py` and artifacts: account for all 67 historical items and distinguish operator input from implementation and architecture blockers; produce concrete reference-only requests and retained input hashes.
- [x] `eng/ext-secrets-1/prepare_acceptance.py`: prepare truthful immutable candidate pins and complete executable verification command; preserve missing-anchor/installation and acceptance gates.
- [x] Tests and evidence: run affected .NET and Python suites and full Local gate, review independently, fix verified findings, retain refusals and exact blockers. Run no Live effects without complete accepted installed prerequisites.
- [x] Delivery docs and detached packet: full canonical source target, input/configuration/plan/anchor hashes, exact commands, remaining owners/actions and recovery requirements.

Given the historical refusal, when reconciliation runs, then its 67 names are preserved exactly and no evidence is silently marked installed. Given missing or changed authority, when adapters run, then no private effect is authorized. Given a prepared candidate, when acceptance is inspected, then missing public anchors, backend, provider implementation or cohort prerequisites remain blockers. Given complete runtime prerequisites, when the separately accepted full owner command runs, then only genuine independent persisted Live evidence for the complete plan permits availability; local source checks never do so.

## Implementation Notes

The current user explicitly authorizes this implementation and concrete provisioning preparation. Worktree isolation resolves the dirty-tree safeguard without modifying unrelated work. Missing deployed authorities are not intent questions. Source delivery may be complete while the requested Live outcome remains blocked; report this distinction and leave unfinished tasks open.

## Spec Change Log

## Review Triage Log

## Verification

Use unique external artifact/evidence directories. Run the Local `eng/verify-ext-secrets-1.ps1` gate against explicit EventStore source, affected runtime test assembly and Python regressions. Capture full commits and hashes. Do not execute the accepted seam with an unaccepted replacement command or reset recovery journals to retry.

Implementation boundary: real lifecycle bindings cannot be installed because their physical provider and independent surviving authority do not exist. The existing actor cannot be made production-capable by registering a passthrough. Qualification role providers and the joint phase runner remain implementation blockers, not operator-only fields. Genuine Live validation is outstanding and the overall request is not complete.


## Review correction plan and triage — iteration 1

All three independent layers reported before triage. Each finding was assessed separately before shared fixes. Preserve endpoint/credential separation, private captures, original identity/lookup, accepted policies, exact67 accounting and all closed lifecycle/Live gates. Fix only demonstrated transport, evidence-snapshot, cohort-admission and verification defects; no new authority is inferred.

| ID | Verdict | Verified evidence and disposition |
|---|---|---|
| B1 | high | FileInfo length validation precedes a synchronous open; swapping a file to FIFO can block that open. Validate a nonblocking opened descriptor. |
| B2 | high | Default Dapr HTTP handler permits redirects; no current loopback redirect refusal test exists. Use SDK-created invocation request with nonredirecting transport and verify no redirected credential request. |
| B3 | high | Removing cohort heading returns an empty prerequisite list and can remove the only unconditional joint-phase gate. Missing cohort must deny. |
| B4 | medium | Status-only protection and malformed host --profile argument are classified as met despite incomplete accepted owner contracts. Validate immutable target/date/command/status and a real profile argument; retain joint-runner blocker. |
| B5 | medium | New cohort reader uses unbounded Path.read_text before hashing; FIFO/large register can hang or exhaust memory. Bound and capture register bytes. |
| B6 | medium | Exact name-set validation permits substitution of historical report metadata. Pin the retained original report digest. |
| B7 | high | Configuration/original/register decision reads are later reopened for hashing; concurrent replacement can misbind evidence. Interpret and hash one captured snapshot. |
| B8 | medium | Private subscriber composition registers a global clock and captures adapters before later DI clock resolution; unrelated services change and clocks may diverge. Capture one clock in its factory without global replacement. |
| B9 | low | PendingOwnerAcceptance branch is unreachable because missing always contains owner acceptance and implementation obligations. Remove the dead classification. |
| B10 | medium | Explicit public source-evidence can equal a configured credential path and be hashed despite credentialsInspected=false. Reject known credential-path/inode aliases before reading. |
| E1 | high | Independently confirms B1: path replacement can block synchronous credential open. Same correction. |
| E2 | medium | Independently confirms B5: unbounded register read precedes bounded hashing. Same correction. |
| E3 | medium | Missing register sections cause IndexError/AttributeError outside CLI catch and expose a traceback instead of structured refusal. Normalize failure and add CLI regressions. |
| V1 | medium | Pre-verified regression gap: real HTTP checks cover direct transport and adapter lookups, but dropping private credentials on effect branches remains undetected. Exercise each native effect over loopback with distinct workload/channel tokens. |
| V2 | medium | Pre-verified regression gap: composition tests all use matching issuer/audience, so removing mismatch guards stays green. Assert both mismatches register no subscriber. |

These are corrections within the original intent. Physical lifecycle/provider/qualification-role/joint-runner absence remains a named implementation or architecture blocker, not deferred as a successful delivery.


### Correction closure

- Edge-case layer verified E1–E3 closed; four targeted Python checks passed.
- Verification-gap layer verified V1/V2 closed from actual loopback effect assertions and issuer/audience denial cases; all five cases appear passed in the full 132-test runtime XML.
- Blind layer verified B1–B9 closed. Its second check found B10's inode exclusion still preceded source open: a hardlink swap could publish a credential hash. R1 (medium, same verified root cause) requires the exact opened public-evidence descriptor to be excluded before reading; add a deterministic swap regression. No runtime effect occurred in the synthetic reproduction. Keep all previous successful corrections and original public-input snapshot binding.
- Initial correction Python failures were three former pass-path expectations invalidated by the new required cohort gate; corrected tests now assert exact cohort-only refusal while still verifying all packet/configuration/anchor pins. Full corrected runtime 132 and Python 153 suites passed before the final descriptor exclusion patch.

Final correction closure: all three layers verified their findings closed. B10/R1 now checks the exact opened evidence descriptor before any read; deterministic alias-swap regression passes. Root final source validation: custody709 (unchanged), runtime132, Python154, zero failed/skipped/unrun; .NET builds warning/error free. Source/provisioning tasks are complete. Overall status remains in-progress because genuine persisted Live, lifecycle backend/authority and qualification/cohort implementation are not complete. No Available result or owner-register amendment is claimed.
