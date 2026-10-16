---
title: EXT-SECRETS-1 exact-target runtime delivery and qualification
type: feature
created: 2026-10-16
status: done
route: dispatch
review_loop_iteration: 1
baseline_commit: 114e08830da2d8cca0e77cd870b8867506b4c68b
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EXT-SECRETS-1 remains Committed. Adapters have authorization defects, qualification hosts always deny, and independent installed authorities remain unverified. The tracked packet requires its own commit hash, preventing acceptance.

**Approach:** Complete runtime/provisioning source, local verification and a detached immutable-candidate packet. Bind verified references. After acceptance and independent enrollment, qualify all 234 S1–S4/v23 cases, then follow Platform host → Parties → Conversations → Agents Story 5.4 with each dependency gate.

## Boundaries & Constraints

**Always:** Reuse custody/Dapr on `192.168.1.30`. Preserve 2026-10-08 integration, Branch B, 365-day history, named approvals, L/S/O/H/R=300/30/600/86400/604800 seconds, Open export disposition with zero effects, and original recovery journals. Decision private keys stay outside Platform/Agents. Record credential references only. Keep executor/observer/enrollment independent. Return missing provisioning inputs.

**Never:** Invent identities, qualification or availability; substitute fixtures/providers/databases; reuse operator/Memories credentials; change unrelated work; or execute live effects before accepted target/configuration/plan/anchors and independent isolated-target enrollment.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Required behavior | Failure |
| --- | --- | --- | --- |
| Authority unavailable | Missing, stale, denied, conflicting or cross-request proof | No new effect; identify missing authority | DependencyNotAvailable |
| Original uncertain | Signing, mutation or execution result lost | Persist identity; exact lookup only across restart/restore | Retain Unknown and journal |
| Candidate prepared | Clean immutable source plus installed references/public anchors | Detached packet pins exact commit, command and binding | Missing references block readiness |
| Target accepted | Independent enrollment and all exact pins verified | Complete isolated 234-case qualification with persisted end-state/ACL proofs | Any missing case blocks availability |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Secrets.Runtime/`: capability/inventory/decision adapters; empty absence, incomplete identities and echo-only authorization need correction.
- `src/Hexalith.Platform.Custody/`: reuse contracts, canonical capability codec, retained originals and private composition.
- `src/Hexalith.Platform.Secrets.Qualification.{Executor,Observer,Enrollment}/`: unconditional denials; observer storage is volatile.
- `eng/ext-secrets-1/{qualify.py,cases.py,contract-case-matrix.txt}`: preserve complete plan, proof validation and original-run journal.
- `eng/verify-ext-secrets-1.ps1`: currently verifies custody only; runtime tests live in `tests/Hexalith.Platform.Secrets.Runtime.Tests/`.

## Tasks & Acceptance

**Execution:**
- [x] Runtime `DaprCapabilityAdapters.cs` — require fresh request-bound typed absence; bind complete revocation/revisions; allow valid empty-batch blocks; supply canonical signing payload.
- [x] Runtime `DaprInventoryAuthorityAdapter.cs`, `DaprDecisionAuthorityAdapter.cs` — require fresh affirmative authorization, exact provider/anchor/inventory, transition bytes and complete final manifest/cohort/profile.
- [x] Runtime `SecretsDapr.cs`, `SecretsProof.cs` — bound streaming responses within existing deadlines.
- [x] Qualification projects — prepare enrolled execution/lookup, independent persisted-owner observations and isolation enrollment; deny until real providers/storage/signers/scopes exist. Never derive proof from expected outcomes.
- [x] `eng/ext-secrets-1/provisioning/` — add unapplied role-specific workload/storage/credential, Dapr ACL/isolation artifacts and owner-input map; refresh census.
- [x] `eng/ext-secrets-1/prepare_acceptance.py`, `qualify.py`, packet/README — detach packet after candidate commit; bind real PEM hashes to authoritative RuntimeQualificationBinding/configuration/plan; generate exact command and blocked-input report.
- [x] Runtime tests, `eng/ext-secrets-1/test_*.py`, verification script — regress all authority/absence/substitution/oversize failures; retain custody/XML audit and add runtime/host coverage.
- [x] Spec evidence — record results, candidate and remaining inputs; qualify and continue downstream only after prerequisite gates pass. Source preparation is complete; live qualification and downstream gates remain blocked as recorded below.

**Acceptance Criteria:**
- Given denial/staleness/incomplete identity/unbound absence, when adapters run, then no effect occurs.
- Given changed bytes/cohort/profile/revocation, when checked, then substitution fails; exact-original recovery survives.
- Given missing installed authorities, when preparing/verifying, then live stays blocked and register/status unchanged.
- Given accepted target and independent enrollment, when qualifying, then every case proves persisted end-state, isolation, revocation, rotation, restart, restore and lost-result recovery before availability acceptance.

## Implementation Notes

Review requirements for re-derivation (source-only scope confirmed by the user):

- In `QualificationHost.cs`, remove `scenario` before either provider call, while retaining the complete original in durable storage. Failed new execution, repeated execution, and explicit lookup must all retain an uncertain original as Unknown with lookup required; no retry may claim zero effects after an uncertain predecessor.
- In `QualificationProofVerifier.cs`, validate exact public role schemas and required typed outcome/digest/enrollment/observation fields, matching `qualify.py` protocol shapes, before retaining any outcome. Do not infer observations from client expected outcomes. Reject missing/extra/incorrectly typed fields even when freshly signed by an enrolled key.
- Public-anchor verification must have a bounded 4096-byte input and obey the existing call deadline, including oversized regular files and special files/FIFOs. An accepted immutable in-memory public anchor is permitted if the installer validates/hashes it and copies it defensively; do not synchronously open arbitrary paths inside verification. Never add a private signer or pretend to install real anchors.
- In the HTTP hosts and provisioning templates, require independently provisioned app-channel authentication before any provider call, and bind an authenticated caller context to authorization. Missing/invalid token or missing caller scope must deny with zero new effects. Reuse repository Dapr app-channel conventions, separate role credentials, and return missing references. Test direct unsigned requests and genuine authenticated admission without deploying anything.
- In `ContentFreeObserverJournal.cs`, make acknowledged publication durable across a host crash (file and directory metadata); unsupported durability must explicitly deny. This remains an optional identifier index, never an all-copy/nonrollback authority. Test persistence boundary failure and restart using real filesystem storage.
- In `prepare_acceptance.py` and `qualify.py`, allowlist only documented public-reference configuration fields and types. Reject unknown/secret-bearing fields, malformed JSON, missing files and nonobject roots into sanitized zero-effect blocked reports; never copy rejected values into logs or artifacts. Support packet versions explicitly; unknown schemas must never enter legacy acceptance. Use one quoted command suffix serializer for generation and preflight, including paths with spaces/metacharacters, and test the generated v2 packet through preflight.
- In `DaprInventoryAuthorityAdapter.cs`, reject successor revision overflow before any authority call. In `DaprCapabilityAdapters.cs`, an absent revocation response grants no authorization. In `SecretsProof.cs`, a present nonstring status is malformed, not omitted.
- Regression tests must independently sign mismatched and expired public proofs, exercise the real verifier through dispatcher refusal with no accepted-result retention, and supply otherwise authorized/fresh profiles with revoked/non-independent/missing flags. Each demonstrated defect above needs a focused regression that fails if its guard is removed.


2026-10-16 source delivery:

- Runtime effects require fresh affirmative typed request-bound absence. Inventory authorization binds provider/anchor identity and exact transition bytes; decision checks bind the complete canonical manifest, cohort and published profile. Revocation binds every envelope field/revision. Signing receives the shared canonical payload and retains original receipt identity. Empty affected-batch lists are valid key-block receipts. Proof/request streams are capped at 1 MiB within the existing call budget.
- Qualification hosts expose separate executor/observer/enrollment provider ports, an independent durable original-store port and accepted ES256 proof verification. Missing implementations deny with named inputs. Originals are retained before execution; repeat or uncertain execution uses lookup; client expected outcomes are excluded from provider invocation. The observer identifier index uses explicit persistent storage. These ports and templates do not constitute installed owner readers, signers, storage, scope authorization or isolated enrollment.
- Unapplied role-specific workloads, storage/credential references, default-deny Dapr ACL/isolation templates and an owner-input map are under `eng/ext-secrets-1/provisioning/`. The census preserves the original host facts and records no new cluster query.
- The source packet is now a template. `prepare_acceptance.py` writes a detached v2 candidate packet after a clean source commit, hashing actual P-256 PEM bytes and copying the exact public configuration. Live preflight compares packet pins with the authoritative RuntimeQualificationBinding, actual PEMs, complete plan, accepted target and exact owner command. No self-identifying tracked packet is required.

Source verification passed using EventStore source at `/home/administrator/projects/hexalith/eventstore` and fresh paths:

- `eng/verify-ext-secrets-1.ps1 -Mode Local`: 709/709 custody tests and 53/53 runtime/host tests; warning-free builds; XML audits reported zero errors, failures, skips or unrun cases. Evidence: `/tmp/ext-secrets-final-local-20261016-evidence/local-evidence.json`, `local-tests.xml`, `runtime-tests.xml` and `python-tests.log`.
- Follow-up runtime-only verification after separating profile validity from proof freshness: 54/54 tests passed with zero warnings/errors; `/tmp/ext-secrets-runtime-tests-final.xml` and `/tmp/ext-secrets-runtime-tests-final.log`. The 709-test custody evidence remains unchanged.
- Python discovery: 56/56 tests passed, including detached-candidate/real-anchor/substitution/dirty-checkout and zero-effect denials.
- Real-checkout preparation with the example references exited 2, produced only `/tmp/ext-secrets-preparation-blocked-20261016/blocked-inputs.json` and reported zero effects. The dirty checkout and missing installed identities/anchors correctly prevent a candidate packet here.

Superseded first source candidate: `799b80b7dd6c31aa737f4ed248cc30efdc7dbacd` on local branch `codex/ext-secrets-1-exact-target-candidate-20261016`. The detached candidate packet and 64-input blocked report are at `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/exact-target-20261016T112413Z/acceptance/`; preparation exited 2 with zero effects. Real authority anchors remain null; this packet is not ready for acceptance. Owner acceptance of the exact candidate/configuration/plan/anchors/command, independently installed providers, role-specific signing/storage/caller scopes, complete all-copy/nonrollback authorities and isolated enrollment remain external inputs. No live effect, 234-case live qualification, availability acceptance, register/status change or downstream advancement has occurred. Source evidence is complete. Live qualification and downstream execution remain pending those external gates; no availability acceptance is claimed.

Matrix audit: all covering tests ran and passed. Authority-unavailable coverage is in `SecretsAuthorityFailureTests` and the three-role host denial theory; uncertain-original coverage is in `QualificationHostTests.LostResultRetainsOriginalAndNewHostOnlyLooksUpItWithoutExpectedOutcomes` plus the Python journal-reopen/lookup cases; candidate coverage is in all five `AcceptancePreparationTests`; accepted-target protocol coverage is in Python `test_complete_executable_plan_is_only_offline_proof`, `test_partial_enrolled_plan_prevents_every_execute`, and persisted-state contradiction checks. These are local tests and do not replace the blocked installed-target qualification.

## Spec Change Log

- 2026-10-16, iteration 1: B4/E6, B5/E7, B6, B9 and B11 exposed underspecified host proof schemas, bounded anchor access, durable publication, public configuration handling and authenticated app-channel admission. Added the concrete requirements above; preserved frozen intent. Avoid signed malformed outcomes becoming immutable originals, unbounded file access, acknowledged-but-lost journal entries, public copying of private fields and unauthenticated effects. Remaining direct fixes and three test gaps are included in re-derivation.
- KEEP: preserve the already verified canonical capability payload/digest, fresh request-bound typed absence, complete revocation identity, exact transition bytes, final manifest/cohort/profile binding, valid empty key-block batches, 1 MiB bounded response transport, separate role ports, pre-effect original retention, Python 234-case plan and original journals, source-only blocked provisioning, detached immutable packet design, and custody/XML audit. Positive source preservation reference is local immutable candidate `799b80b7dd6c31aa737f4ed248cc30efdc7dbacd`; reuse its correct implementation while replacing the reviewed defects. Its detached packet remains superseded evidence and never authorizes live work. Preserve unrelated recovery/epic-4 edits.

## Review Triage Log

Parent audit: an empty detached packet skipped packet validation after the missing-file change. Added explicit empty-packet refusal and `test_empty_packet_never_clears_preflight`; all 64 Python tests passed.

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | false | reject | The source explicitly leaves installed providers/storage/signers as external dependencies; typed provider ports are the composition seam. The user confirmed source preparation and blocker reporting only; these binaries are not claimed operational. |
| B2 | medium | patch | HandleAsync passes scenario to AuthorizeAsync before removing it for InvokeAsync. Both provider calls must receive the same expectation-free request; the original journal retains complete bytes. |
| B3 | high | patch | The exception path keys uncertainty only to new execute. A failed repeat/explicit lookup can follow an applied original and must preserve Unknown and exact lookup required. |
| B4 | medium | bad_spec | The verifier validates signature/common identifiers but permits signed executor payloads without outcome/resultSha256. Retaining this malformed immutable result can conflict with corrected recovery; exact role schemas must precede retention. |
| B5 | medium | bad_spec | File.ReadAllBytes precedes the 4096-byte anchor check and synchronous verification has no cancellation budget. Oversized files and special files must never cause unbounded verification. |
| B6 | medium | bad_spec | Append flushes file contents but not publication metadata. A machine crash can lose an acknowledged persistent index entry; the persistence contract needs explicit directory publication durability or an explicit refused dependency. |
| B7 | medium | patch | Unknown packet schemas enter the legacy branch and skip v2 command matching. Reject unknown schemas before pin acceptance. |
| B8 | medium | patch | Preparation quotes arguments with shlex.quote while preflight compares raw arguments. Reuse one exact command suffix serializer and exercise quoted paths through preflight. |
| B9 | high | bad_spec | Preparation copies the full input object into a public runtime-reference.json, including an unknown runtime.privateKey. Public configuration must be allowlisted and secret-bearing or unknown shapes rejected without retaining values. |
| B10 | medium | patch | Preparation reads configuration before creating the blocked report and assumes an object. Missing/malformed/nonobject inputs need sanitized DependencyNotAvailable reporting with zero effects. |
| B11 | high | bad_spec | Hosts receive no authenticated channel/caller context; proposed workloads have only Dapr API tokens. App-channel authentication must precede scope authorization and direct unauthenticated application requests must deny. |
| E1 | medium | patch | Confirmed independently: quoted artifact/evidence/EventStore paths differ from preflight raw suffix; same root cause as B8. |
| E2 | medium | patch | Confirmed independently: a nonobject configuration raises before a blocked report; same root cause as B10. |
| E3 | medium | patch | Unchecked long.MaxValue + 1 equals long.MinValue, so TransitionBody admits a negative successor. Reject overflow before invoking authority. |
| E4 | high | patch | Authorized permits status absent; VerifyAsync then creates revocation authorization. Reject absent revocation proof before downstream registration. |
| E5 | medium | patch | Text maps nonstring status to null, and Authorized accepts null as omitted. A present nonstring status must fail. |
| E6 | medium | bad_spec | Confirmed independently: signed executor payloads missing outcome/resultSha256 can be retained, poisoning exact-original recovery; same root cause as B4. |
| E7 | medium | bad_spec | Confirmed independently: anchor size checked after unbounded synchronous read, including blocking special files; same root cause as B5. |
| V1 | medium | patch | Pre-verified gap: mismatched/expired payload tests mutate signed bytes without re-signing, so signature failure masks missing request/freshness guards. Sign each negative case independently. |
| V2 | high | patch | Pre-verified gap: dispatcher tests always use a verifier returning true. Use the concrete verifier with an untrusted-key proof and assert refusal plus no accepted retention. |
| V3 | high | patch | Pre-verified gap: otherwise valid concrete adapter profiles with revoked/non-independent/missing flags are untested; without guards hard-coded returned flags normalize them. Add direct parameterized cases. |


Second review findings (after iteration 1):

| R2-B1 | high | patch | Verified outer HTTP budget cancellation is rethrown by dispatcher and converted into zero-effect caller-scope denial after invocation. Preserve Unknown once executor dispatch begins; independently reproduced by reviewer harness. |
| R2-B2 | high | patch | Provider authorization refusal returns Deny without uncertainty even for lookup or potentially repeated execute. Conservatively preserve an executor original when current scope denies recovery. |
| R2-B3 | medium | patch | Request validation checks only a few common fields; unknown root properties are forwarded to provider callbacks. Restrict known enrollment/case request fields, exact plan and case identity before callbacks, and forward only allowed fields. |
| R2-B4 | maybe-false | defer | No installed caller authenticator exists to establish its actual accepted credential/sidecar identity transport. The interface receives HttpContext; Dapr invocation may carry the trusted installed workload identity, so absence of a custom client header alone does not prove failure. Independent owner must supply and qualify the concrete serialized Dapr caller authentication and scope mechanism before live enrollment; user confirmed those references are unavailable. |
| R2-B5 | medium | patch | Snapshot stateSha256 is checked only as a hash-shaped string; changed signed state with the old digest can be retained. Compare canonical state digest and original identity/outcome/result bindings before retention. |
| R2-B6 | medium | patch | C# structural checks permit identities outside the Python IDENTITY alphabet, unrecognized receipt kinds and integers above 9007199254740991. Apply existing documented protocol constraints before immutable retention. |
| R2-B7 | high | patch | ResolveAsync accepts status absent through Authorized while otherwise complete fields produce a published profile. Explicitly reject absence; existing positive authority semantics determine the correction. |
| R2-B8 | medium | patch | Preparation hashes relative anchor paths from its current directory, then preserves them for an Agents invocation from another directory. Normalize or reject relative anchor paths before digesting public configuration. |
| R2-B9 | medium | patch | Preparation omits endpoint, qualification timeout, mounted API credential and role-version validation that preflight already requires. Share those existing checks so blocked report names the actual invalid inputs. |
| R2-B10 | medium | patch | Git rev-parse/status failures occur outside blocked-report handling. Missing Git, invalid checkout or timeout raises instead of returning sanitized zero-effect candidate prerequisites. |
| R2-E1 | high | patch | Confirmed independently: HTTP deadline after dispatch loses executor uncertainty; same root cause as R2-B1. |
| R2-E2 | high | patch | Confirmed independently: an otherwise valid absent published profile is accepted; same root cause as R2-B7. |
| R2-E3 | medium | patch | Confirmed independently: relative anchors change meaning between Platform preparation and Agents execution; same root cause as R2-B8. |
| R2-V1 | medium | patch | Pre-verified gap: signing fixtures echo the request digest; JSON-serialization digest mutation remains undetected. Assert transmitted canonical bytes and their independently computed SHA-256 against a fixed signer receipt. |
| R2-V2 | medium | patch | Pre-verified gap: only the bounded memory-stream helper is tested. A local unfinished chunked HTTP response must prove the concrete invoker returns before full-body buffering. |
| R2-V3 | high | patch | Reviewer harness independently retained/applied an original, expired the HTTP budget and observed qualificationEffects=0 with no lookup flag. This confirms R2-B1; add regression through HTTP dispatch. |

## Design Notes

No intent choices remain. Missing identities are provisioning dependencies. Source/candidate commit are reversible; live effects stay gated. The packet cannot belong to the commit it identifies.

## Verification

- Run `eng/verify-ext-secrets-1.ps1 -Mode Local` with fresh paths and EventStore source; require warning-free builds and complete passing XML.
- Run Python `test_*.py` and runtime/host regressions; verify zero-effect acceptance/enrollment denials.
- Retain exact-target qualification evidence and original journals; missing gates block downstream advancement.


## 2026-10-16 Review Correction Evidence

The implementation phase reused the scoped source from preservation candidate `799b80b7dd6c31aa737f4ed248cc30efdc7dbacd` and applied B2–B11/E1–E7/V1–V3 corrections. The frozen intent, complete 234-case plan, original journals and unrelated recovery/epic-4 work remain preserved. This source is pending the parent workflow's immutable candidate and independent re-review; the earlier detached packet remains superseded evidence.

- Both provider authorization and invocation exclude the complete client `scenario`; the durable original retains it. Failed new execution, repeated execution and explicit lookup preserve Unknown once an original may be applied, with exact lookup required and no accepted-result retention on invalid proofs.
- Role proofs are parsed, bounded and checked against exact public executor/observer/enrollment schemas, including required typed nested observations and complete case scopes, before retention. The embedded public case schemas are independently compared with all 234 canonical plan cases; they contain field names and allowed protocol outcomes, never inferred owner observations. Substituted and expired proofs are independently signed in regression tests. Dispatcher refusal uses the concrete verifier with an untrusted key and proves no result retention.
- Accepted P-256 public PEM bytes are hash-validated, limited to 4096 bytes and copied defensively at installation. Verification performs no filesystem access, and checks the existing call cancellation budget. Python public-file reads reject symlinks, oversized files and special files/FIFOs before bounded reads.
- Every HTTP role checks the independently provisioned APP_API_TOKEN/dapr-api-token channel before provider calls. An independently installed caller authenticator must provide workload identity and current scope to authorization. Separate role channel-token annotations/environment references and missing caller/scope reference fields are present only in unapplied provisioning templates. No authenticator, signer, credential or installed anchor is invented.
- Observer identifier publication flushes file contents, journal-directory metadata and parent publication metadata before acknowledgement. Unsupported durability refuses acknowledgement. Real filesystem tests cover post-rename persistence failure and a fresh host reopening/reacknowledging the identifier; this optional index remains separate from all-copy/nonrollback authority.
- Public-reference configuration is restricted to documented fields and exact types. Missing/malformed/nonobject/unknown/secret-bearing inputs yield only a sanitized zero-effect blocked report. Packet v1/v2 are explicit; unknown schemas never use legacy acceptance. Preparation and preflight share one quoted command serializer, tested through generated v2 preflight with spaces, quotes, dollar expressions, semicolons and backticks.
- Successor revision overflow is rejected before authority invocation. Fresh absent revocation grants no authorization; present nonstring status is malformed. Otherwise authorized/fresh decision profiles with revoked, non-independent or missing flags are directly rejected.

Required Local verification passed with EventStore source `/home/administrator/projects/hexalith/eventstore` and fresh artifacts/evidence under `/tmp/ext-secrets-review-final-20261016-{artifacts,evidence}`: **709/709 custody**, **81/81 runtime/host**, **63/63 Python**, warning/error-free builds and XML with zero errors, failures, skips or unrun cases. Focused runtime/Python follow-up verified the final directory-parent fsync and bounded packet-read changes without repeating unchanged custody checks. Final source evidence is `/tmp/ext-secrets-review-final-20261016-evidence/review-corrections-evidence.json`, with `local-tests.xml`, `runtime-followup-tests.xml`, `python-followup-tests.log` and the warning-free build logs retained.

Real-checkout preparation with public example references produced only `/tmp/ext-secrets-review-blocked-20261016-_iipyxtn/acceptance/blocked-inputs.json`: DependencyNotAvailable, **75 missing inputs**, `qualificationEffects: 0`, `packetPrepared: false`. Dirty source, real installed providers/public anchors, role signing/storage/channel credentials, authenticated caller-scope authorities, independent isolated enrollment and owner acceptance of exact candidate/configuration/plan/anchors/command remain gates. No resource was deployed; no live case, live effect, availability acceptance, register/status update or downstream advancement occurred.

## Final Source Handoff

The user confirmed: finish the source candidate and report provisioning blockers. Status `done` records that source-only completion; installed-target qualification and downstream acceptance remain blocked. No real identity, signer, store, caller scope or enrollment is claimed.

All verified actionable findings from both independent three-layer reviews are corrected. R2-B4 remains one deferred, unverified external caller-authentication integration requirement; the independent owner must qualify the exact serialized Dapr request and isolated scope before live enrollment. Original candidate/journals/evidence remain retained.

Parent final verification: `eng/verify-ext-secrets-1.ps1 -Mode Local` exited 0 with **709/709 custody**, **107/107 runtime/host**, **67/67 Python**, zero build warnings/errors and zero failed/skipped/unrun XML tests. It includes the applied-effect HTTP timeout, authorization refusal, closed request, signed snapshot contradiction, canonical signing vector and unfinished oversized chunked-response regressions. No code changed after this run.

Durable evidence and the final detached candidate identity/packet/blockers are recorded outside the source commit at `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/exact-target-final-20261016T120636Z`. Keeping the candidate identity external avoids self-identifying tracked artifacts. The local candidate branch is `codex/ext-secrets-1-exact-target-candidate-20261016`; the shared main checkout and unrelated concurrent work remain preserved. No push, deployment, live qualification, availability/register change or downstream advancement occurred.
