---
title: EXT-SECRETS-1 runtime binding and complete qualification delivery
type: feature
created: 2026-10-14
status: done
route: dispatch
delivery_status: source-prepared-runtime-blocked
live_qualification: not-run-missing-owner-inputs
baseline_commit: 8580eb64ac8a9ac3c025dd6c7f64aa6f39ce5cbb
review_loop_iteration: 0
context:
  - /home/administrator/projects/hexalith/agents/_bmad-output/planning-artifacts/external-dependency-register.md
  - /home/administrator/projects/hexalith/agents/_bmad-output/implementation-artifacts/tests/owner-delivery-2026-10-08/owner-inputs.md
  - /home/administrator/projects/hexalith/agents/_bmad-output/implementation-artifacts/tests/owner-delivery-2026-10-08/ext-secrets-1.md
---

<frozen-after-approval reason="user explicitly authorized complete delivery and concrete provisioning for missing inputs">

## Intent

Complete the Platform owner delivery for Hexalith.Agents Story 5.4 EXT-SECRETS-1. Reuse the custody library, bind verified runtime inputs where available, prepare concrete configuration/provisioning and full executable qualification where external inputs are absent, and return reviewed source, evidence, remaining inputs and an immutable replacement target packet. Live qualification and Available require the accepted exact target and real installed authorities; missing deployment cannot become a simulated pass.

## Boundaries & Constraints

Preserve every unrelated working-tree edit, accepted integration date 2026-10-08, host 192.168.1.30, L/S/O/H/R=300/30/600/86400/604800 seconds, independent decision keys outside platform/Agents custody, and Open overlapping-export Product disposition. Credentials are references only in code/config/logs/evidence. Platform AD-9 requires Dapr for secrets and invocation; do not invent a direct OpenBao exception. Do not deploy, rotate production keys, stop workloads, restore production, publish, push or change the accepted dependency register. No new database or service substitutes for an independently qualified existing provider. Full runtime prerequisites include qualified all-copy destruction/nonrollback restore, authority/enrollment and exact replay scope; expose their absence explicitly.

## I/O & Edge-Case Matrix

| State | Expected behavior |
|---|---|
| Missing runtime configuration/accepted target/credential reference/current inventory/trust/qualified adapter | Persist sanitized DependencyNotAvailable and missing input names; no effects, never Available |
| Exact enrolled Dapr HMAC scope/version and independently current public inventory/profile | Resolve only the exact referenced key; short-lived owned bytes; accepted timing; no secret output |
| Revoked/changed/cross-tenant authority, unexpected key response, stale profile, outage | Content-free restrictive outcome; no cached revival or fallback |
| Complete isolated qualification target | Execute all S1-S4/v23 cases; independently verify outcomes and persisted end-state with exact run/target/request binding; retain failure/lost-result/restart/restore evidence |
| Lost result/unknown consume/sign/release | Exact original lookup only, no blind repeat, immutable identities/outcomes |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Custody`: reuse PlatformHmacService, accepted timing, crypto codecs, inventory/lifecycle/export/signing actors, independent decision verifier, private replay/revocation composition. Interfaces deliberately require independently current providers and exact receipts.
- `eng/verify-ext-secrets-1.ps1`: preserve complete Local class selection/build/XML audit; replace blanket Live throw with prerequisite preflight and full executable case orchestration, keeping unavailable before effects.
- `eng/ext-secrets-1/`: new runtime reference configuration, qualification protocol, provisionable Dapr/component/policy templates, tests and operational handoff. `preservation-baseline.json` records pre-existing edits.
- Complete contract and owner packet in frontmatter are normative, including migration epochs, public-anchor retention, replay namespace, signer prohibition and v23 protection-owner races.
- Runtime verified resource: Kubernetes API https://192.168.1.30:6443, openbao/StatefulSet/hexalith-keys. No Agents custody workload, tenant inventory, independent signer or replay binding found by fresh Kubernetes name census; these must remain unset.

## Tasks & Acceptance

- [x] Add a narrow concrete Dapr secret-store HMAC adapter and runtime profile binding in the existing library, with explicit independently current inventory authority input, exact purpose/version/reference validation, current revocation checks, bounded calls and buffer cleanup. Register only on explicit valid configuration; unavailable defaults remain when inputs are absent. Do not imply that this adapter implements missing all-copy lifecycle/signing/replay authorities.
- [x] Replace Live blanket guard with exact clean accepted-target preflight, explicit full runtime-reference validation, complete S1-S4/v23 scenario plan and bounded executable transport to separately installed owner qualification adapters and independent persisted-state observer. Pin adapter identity/version and public observation anchor; prohibit fixture/self-certified pass and partial-plan availability. Check actual outcome/state invariants for every required contract case, including all before/at/after races, two stale signing attempts, rotation, revocation, restart, restore, lost result and leakage. If those owner adapters are missing, name them as missing implementation inputs. Qualification cannot authorize production mutation from a config alone: require isolated enrolled target and accepted owner command.
- [x] Add concrete reference-only config and provisioning templates including tenant key inventory, issuer/audience/profile validity, independent signer/public trust/revocation, current actor-role authority, replay registrar and compromise subscriber isolation, nonrollback journal/restore target and qualification credentials. Keep unknown identifiers null, proposed identifiers labeled proposed, no Secret values/objects.
- [x] Add meaningful adapter and executable gate tests covering missing input/no effects, counterfeit/partial evidence, stale/changed authority, exact target, full case coverage, persisted state and recovery invariants. Preserve Local source checks and run them freshly.
- [x] Write delivery README with protocol endpoint schemas and copyable external provisioning prompts; distinguish implemented code from uninstalled runtime adapter guarantees. Root captures fresh runtime references, review/validation evidence and immutable candidate without unrelated files.

Given missing inputs, when Live is requested, then sanitized evidence names the exact blockers and no runtime qualification effect occurs. Given all installed qualified inputs and accepted target, when any required case fails or lacks independent persisted proof, then qualification fails. Given independently valid complete qualification, then evidence permits owner acceptance only for its exact immutable target; the script never edits the register itself.

## Implementation Notes

User authorization includes reversible implementation and preserving unrelated dirty work. No further policy selection or intermediate permission is needed for this source/provisioning delivery. External installed authorities remain inputs, not intent questions. The root handles immutable candidate creation and final acceptance packet after review; leave actual runtime installation and qualification evidence unclaimed.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence |
|---|---|---|
| B1 | high | Whole HTTP call can exceed timeout with a trickled response; reviewer reproduced 1.6 seconds under 1 second. |
| B2 | high | Enrollment and profile validity checked only at entry; long run can issue effects after expiry. |
| B3 | medium | A crash can truncate final journal records and block exact recovery permanently. |
| B4 | high | Unrelated trace receipt/state hashes pass syntax validation. |
| B5 | high | Invented race positions and missing Hold/Dispatch events pass. |
| B6 | high | Active credential metrics contradict empty persisted vectors. |
| B7 | high | Consumed outcome passes with empty target/destruction vectors; root reproduced. |
| B8 | high | Replay foreign target tenant passes without authenticated target binding. |
| B9 | high | Same-batch re-attestation passes with absent seal/batch/manifest/ordinal. |
| B10 | high | Final key version and required rotation are not bound to signing attempts. |
| B11 | high | Envelope expiry cases have no verification/expiry instant proof. |
| B12 | high | Denied state can claim Delivered and nonnull delivered instant. |
| E1 | high | Dapr Vault selects backend version only through version_id; version defaults to latest. |
| E2 | medium | Text response mode is incompatible with named-field canonical base64 adapter. |
| E3 | high | Decoded material remains owned across noncooperative final authority wait. |
| E4 | high | Distinct PEM byte hashes can encode one public key, defeating independent proof holders. |
| E5 | high | HTTP total timeout exceeded by continuously arriving bytes; reproduced 1.8 seconds under 1 second. |
| E6 | high | Persisted active credential count contradicts required metric. |
| E7 | high | Blocked reserve can contain destroyed targets. |
| E8 | high | Replay record is not bound to authenticated business tenant. |
| E9 | high | Race position claims need not correspond to persisted events. |
| E10 | high | Consumed need not include a Consume event. |
| E11 | high | Claim of exact attested Dapr version is contradicted by version metadata name. |
| E12 | high | Claim of bounded transport contradicted by idle-only timeout. |
| E13 | high | Claim of actual outcome/state invariants contradicted by accepted inconsistent evidence. |
| V1 | medium | Removing TenantKek inventory leaves all protocol tests green; mandatory matrix needs independent assertion. |
| V2 | high | Removing journal retained-result comparisons leaves all tests green; changed signed original recovery must reject. |
| V3 | medium | Successful concrete active-version digest caller has no golden consumer test. |
| V4 | high | Second-compromise activeCredentials=0 accepts one persisted active credential. |
| R1 | high | Primary source confirms version_id requirement; duplicate of independently found E1. |
| R2 | medium | Primary source confirms map response mode required; duplicate E2. |
| R3 | high | Root reproduced empty targets for lifecycle destruction and Consumed cases. |
| R4 | high | Root inspected idle-only HTTP timeout; both reviewers supplied timed reproductions. |
| R5 | medium | Final implementation Local log is incomplete, XML absent and process no longer exists; rerun after corrections. |

## Verification

Run `pwsh -NoProfile -File eng/verify-ext-secrets-1.ps1 -Mode Local -EventStoreSourceRoot ../eventstore` with unique artifact/evidence directories. Run gate/protocol tests and new .NET adapter tests. Run Live prerequisite-only negative checks with no enrolled adapter and verify zero effects. No actual Committed seam invocation outside its accepted exact owner command.

## Review correction instructions

All three independent layers completed. Thirty-four individual findings were verified before grouping. Correct the demonstrated implementation defects in seven groups without changing frozen intent, accepted timing/policy, original source actors, unrelated work, or runtime availability. Keep the explicit external-adapter gaps, unavailable defaults, exact accepted-target/config/anchor pins, and immutable original request/outcome approach. These corrections implement the already required exact binding, bounded transport and actual persisted proof.

- Dapr: use provider-native `version_id` and map response mode. Bound post-fetch authority/profile waits so decoded material is not retained by pure noncooperative authority reads; clear the Dapr response as soon as captured, avoid decoded buffers across those reads. Add actual active-version registered digest caller golden test with a nonzero key.
- Authorities: compare decoded P-256 public keys, not just PEM hashes, for independence. Keep byte hashes as exact accepted anchor pins. Validate current profile/enrollment before each new effect, refresh the same enrollment identity/target map; never renew capability/delivery identity or expiry. Lookup recovery remains exact-original only.
- HTTP: impose a total monotonic deadline over connect/headers/body and reject redirects, oversized/duplicate/nonfinite input. Test actual slow/trickled transport, not merely timeout arguments.
- Persisted proof: independently enrolled concrete tenant/target bindings must drive state validation. Reconcile required credential/effect counts against vectors; consumed destruction requires nonempty exact enrolled targets, full destroyed vector and Consume proof; winning blocks/denials require no forbidden effects. Bind replay target to authenticated business tenant and exact issuer/nonce scope. Race positions must match actual typed events/receipts; recovery events must match snapshot state/receipt hashes. Prove nonempty stable seal/batch/manifest and next attestation from predecessor/successor evidence. Bind final signing key to successful attempt and demonstrate actual healthy rotation. Prove before/at/after expiry with actual verification/release/expiry instants. Add whatever reference-only protocol fields are necessary and document them; no field may carry keys, plaintext or credentials.
- Journal: atomically publish fully flushed records without overwriting originals, then fsync directories. Interrupted serialization must not leave a truncated authoritative record; process lock stays restrictive. Test conflicting retained outcomes and enrollment mappings after reopening.
- Coverage: assert the complete mandatory contract matrix independently of the production generator, so removing any inventory or required case cannot leave source checks green. Add negative mutations for every demonstrated contradictory proof.
- Validation: final implementation Local run was abandoned without XML and cannot count as a pass; run selected changed-file checks in engineering, then root runs complete fresh checks against immutable candidate.

## Correction closure review triage

All three independent layers reported against the frozen corrected diff before classification. Prior Dapr/version/bounds/key-identity/journal/matrix/digest-caller fixes are closed. Each new finding is rendered separately before grouping.

| Finding | Verdict | Evidence |
|---|---|---|
| B-C1 | high | Enrollment may expire during durable original publication; both clock-advance reproductions dispatch execute after expiry. Recheck after publication immediately before dispatch. |
| B-C2 | high | Activate receipt contradicts activations=0 in admission-block-never-reopens. A retained credential alone does not prove a new activation; preserve that distinction. |
| B-C3 | high | Unrelated or late NoIssue receipts/hashes pass; exact terminal proofs must precede the next signing attempt. |
| B-C4 | medium | Retained initial-key overlap verification is rejected while successor-key fixture passes. Bind old verification key separately from current issuance key. |
| B-C5 | high | Foreign-only pins satisfy retainedPins while protecting none of the enrolled targets. Pin vector must be a nonempty subset of the enrolled targets. |
| E14 | high | RecoveredOriginal masks native Delivered/Consumed outcomes, rejecting the immutable delivery instant and bypassing destruction/order proofs. Use operation-native outcomes and invariants. |
| E15 | high | Foreign-key/revision NoIssue records after Issue pass; verified independently, same root cause as B-C3. |
| E16 | high | Forty-second durable-publication delay crosses exclusive enrollment validity and still dispatches; same root cause as B-C1. |
| VC1 | high | Byte-identical replay claims nonceRecords=1 with all replay state null; same exact scope and retention invariants must apply to original idempotent replay. |
| VC2 | high | Retained NoIssue records may use unrelated key/revision despite exact attempts; same root cause as B-C3/E15. |
| E-profile-observation | false | Expired installed current profile is an absent current-trust prerequisite; restrictive denial is required. This does not renew original envelope/capability expiry. Document needed current lookup authority, rather than bypass trust. |

Seven small correction groups preserve the frozen intent: native original recovery outcomes, exact ordered terminal no-issue proofs, validity after durable publication, activation receipt counts, retained old-key overlap semantics, enrolled pin vectors, and original replay scope/retention. These add no new authority or runtime availability. Installed current trust remains required for lookup; an expired capability or envelope never renews its original identity/expiry.

## Additional root invariant triage

| Finding | Verdict | Evidence |
|---|---|---|
| R-C1 | high | Root reproduced nonnull persisted exclusiveExpiry with capabilityExpiryFields=0 on the frozen pre-correction copy. Scope a rejection to non-expiring capability cases and native signing/issue/dispatch/consume recovery; preserve valid delivery timing. |

Root counterexample evidence is retained outside reusable source as `root-nonexpiring-counterexample.json`; correction stays within the existing non-expiry requirement.

## Final semantic closure triage

All three layers reported before triage. Earlier correction groups and root non-expiry finding are closed. Each newly demonstrated receipt/window finding is rendered before grouping.

| Finding | Verdict | Evidence |
|---|---|---|
| E17 | high | Native Reserve/Consume, activation Issue/Activate and compromise Block records can name foreign-key outside original enrolled state; source predicates only bind selected Dispatch/history records. Bind each relevant native operation receipt to its original key. |
| VF1 | high | Overlap-before Verified accepts checkedAt == oldVerifyUntil, violating the retained key exclusive interval despite being before the maximum O boundary. Require positive verification strictly before both limits. |
| VF2 | high | Native consume accepts foreign-key Reserve/Consume while original state/Dispatch use v1. Same key-binding root cause as E17; a higher current guard revision alone is permitted and is not this defect. |

Two narrow patches bind native operation receipt keys and the retained verifier exclusive interval. Keep legitimate current guard advancement; never require its revision to equal the earlier dispatch revision merely because it advanced. No timing, authority, runtime status or frozen intent change.

## Exact immutable issue receipt correction

| Finding | Verdict | Evidence |
|---|---|---|
| R-C2 | high | Root reproduced Issue.guardRevision=12345 with actualIssuedGuardRevision=1 for native activation and same-batch re-attestation. Issue is the immutable successful append receipt and must equal that actual revision; later Dispatch/Activate/Reserve/Consume online revisions may advance. |

## Source delivery completion

All known demonstrated code findings are corrected and closed by three independent review layers. Root independently passed the complete 709-test Local custody gate with zero failures/errors/skips and a warning-free build, and the final 39-test protocol suite. The source/provisioning preparation is done; actual runtime binding, independent qualifier implementations, owner acceptance of replacement target/config/anchors, and exact accepted-target Live qualification remain outstanding. EXT-SECRETS-1 stays Committed at its existing target; neither this spec status nor any offline proof declares Available or completes consuming Story 5.4 readiness. Root returns the immutable candidate and evidence packet separately.
