---
title: 'EventStore 4.16 qualification handoff: Platform acceptance plan'
date: '2026-11-09'
status: 'applied'
mode: 'batch'
change_scope: 'moderate; Platform execution and Builds contract coordination'
approval: 'Administrator accepted Q1-Q6 with I accept on 2026-11-09; Platform planning amendments applied; execution remains gated'
baseline_commit: '5cf3e8cbbea06cebcb8fc6ed2b8489ace1934732'
---

# Sprint Change Proposal — EventStore 4.16 Qualification Handoff

**Current disposition — 2026-11-09:** Administrator accepted the complete proposal
with “I accept”. Platform's planning amendments and the unnumbered
[PLAT-ES416-QH handoff](../implementation-artifacts/spec-plat-es416-qh-qualification-handoff.md)
are applied. The body below preserves the reviewed preparation account and its
initial pending states; [Approval Application](#approval-application--2026-11-09)
records the current partial intake. Builds/EventStore receipts, executing
maintainer/profile/tool acceptance and runtime qualification remain outstanding.
The original planning-only scope, pins and readiness gates remain binding.

## 1. Issue Summary

EventStore's approved [readiness-recovery proposal, P1](../../../eventstore/_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-09-readiness-recovery.md#p1--story-416-concrete-delivery-intake-and-stop-conditions) requests concrete Platform acceptance of qualification execution. Platform must identify the owning work, supported inputs, independent actor hosts, bounded fault controls, logical readback and cleanup. Builds must accept any missing runner/descriptor contract support. Assigning those responsibilities does not supply the missing Dapr primitive or constitute acceptance by an owner.

**Disposition: prepare the execution work; keep qualification execution blocked.** There is no currently eligible append-protection profile in the inspected evidence. Platform and Builds work acceptance, supported upstream delivery, verifier qualification and application conformance are separate decisions. This proposal records an acceptance plan, not an accepted delivery commitment.

The user authorized planning only. This run creates this proposal and leaves canonical PRD, architecture, epics, stories, tracking, schemas, runtime files, dependencies and pins unchanged. It sends no upstream request and starts no workload, service, fault campaign or migration. The requested sibling EventStore checkout supplies requirements; it is not a permitted source-resolution fallback for Platform execution.

### Evidence and reuse limits

| Evidence | Finding used by this plan | Consequence |
| --- | --- | --- |
| EventStore [4.16 spec](../../../eventstore/_bmad-output/implementation-artifacts/spec-4-16-append-write-once-conformance-through-enforced-envelope.md), Approved Commit Contract, AC1–AC11, November 1 screen and November 9 recheck | The recorded released candidate is .NET SDK 1.19.0, daprd 1.19.1 (`cfb5ee090dda7cfc26145c7703605d831350aa2a`), embedded components-contrib 1.19.0 (`1d331c389f11baaa1d664022b6b6c2f59811ce11`), `state.postgresql` v1. It lacks mandatory read-bound revision, create-once event identity and protected erasure. Existing transactional rollback does not reject a successful overwrite. | Reuse the negative finding. No new capability search, source screen or live result is claimed. G-APPEND remains FAIL/BLOCKED. |
| Same spec, exact-combinations table | Historical daprd 1.18.7/components 1.18.6/PostgreSQL v1 also failed; its SDK 1.19.0 skew was not the recorded supported pairing. | Neither the installed/catalog combination nor an optional ETag, API-group restriction, broker result or runtime refresh unblocks append. |
| Platform [descriptor/result contract](../../docs/contracts/test-environment-v1.md) and [policy v1](../../docs/contracts/candidate-and-attachment-policy-v1.md) | v1 is closed and version-specific. It represents lifecycle, endpoints, readiness and observed artifacts; it has no typed qualification profile, host-role or fault-control binding. Endpoint credentials use a separate strict private channel. | Reuse existing fields. Do not insert arbitrary control fields, put credentials in URIs/diagnostics, or treat parsing as adoption/acceptance. A versioned extension is proposed in Q2. |
| Builds [module test declaration](../../references/Hexalith.Builds/schemas/hexalith.module-manifest.v2.json), [handoff](../../references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestHandoff.cs) and [engine](../../references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs) | Declared `--profile` selects a suite; `module.tests` carries executor, entry, TRX format, finite execution/stop bounds, mode and optional runtime version. A legacy peer URL exists, but legacy second-instance allocation is conditional on `!platformComposition`. | A suite name or peer URL does not prove declaration-driven two-host actor qualification. Extend the existing plan/composition path and verify actual topology. |
| Platform [Dapr bindings](../../src/Hexalith.Platform.Composition/PlatformDaprBindings.cs) and [retention profile](../../src/Hexalith.Platform.Composition/PlatformRetentionProfile.cs) | Ordinary state bindings select Redis. The special Memories `functional-retention` source experiment selects `state.postgresql/v2`, PostgreSQL 18.6 and daprd 1.18.1 with `qualificationCredit=false`. | This is a distinct consumer/profile, not an EventStore 4.16 backend or append-support claim. Preserve its selection and other consumers' defaults. The older runner-prerequisite spec's “remaining PostgreSQL composition” account is historical where current source has advanced. |
| Platform [November 8 correction](sprint-change-proposal-2026-11-08.md), [2.10 execution spec](../implementation-artifacts/spec-2-10-qualify-the-runner-lifecycle-on-the-reference-composition.md), tracker and descriptor contract | 2.8 is review, 2.9 done, 2.10 ready-for-dev, 3.9 backlog and 4.9 backlog. Full reference qualification, tool acceptance, loaded-identity production and qualification authentication remain distinct gates. | Preserve these states. The current final candidate-validation path still refuses `tool-qualification-authentication-unavailable`. An installed tool or a new descriptor cannot self-authenticate acceptance. |

The historical Platform [readiness assessment](implementation-readiness.md) is CONCERNS, dated September 29. EventStore's current handoff retains G-APPEND, G-BASELINE and readiness FAIL/BLOCKED. These are different repositories' assessments; this proposal reruns neither and upgrades neither.

## 2. Impact Analysis

### Backlog and artifact ownership

| Platform area | Proposed impact | Retained boundary |
| --- | --- | --- |
| Epic 1 — minimum-environment development | Consume declarations, active-root mapping, readiness and content identities. | No source/package substitution, provider persistence helper or module-owned AppHost. |
| Epic 2 — real-service testing | Add a bounded EventStore qualification work package consuming 2.2–2.7 and 2.9. Q2 extends the existing runner; Q3–Q5 deliver topology, controls and lifecycle. | Do not reopen completed 2.9 migration. Keep 2.8's finite CI scope and 2.10's reference matrix/tool acceptance. No automatic new tracker entry or done transition. |
| Epic 3 — full composition and candidate evidence | Consume 3.9's expected/actually loaded identity producers and the existing authenticated candidate policy. | Local/source or baseline-only results are diagnostic. Missing loaded identities or qualification authority blocks candidate acceptance. |
| Epic 4 — hosted runtime/profile | Platform 4.9 consumes EventStore profile ratification; Platform 4.16 retains staging trust-domain/runtime-policy work. | **EventStore 4.16 is not Platform 4.16.** Candidate qualification does not complete either hosted story, select a production runtime or change shared infrastructure. |
| Epics 5–10 — staging, release, access, recovery and MVP | Consume only later applicable, ratified results and module prerequisites. | Preserve staging E2E, compatibility, restore, isolation, assurance and G1–G3. No recovery or release authorization is added. |
| Epics 11–12 — retirement and Agents | No direct change. | Existing retirement, post-MVP and owner-review conditions remain. |

The PRD's FR-4/FR-5, SM-3/SM-C4/SM-C5 and the [SPEC package](../specs/spec-platform/SPEC.md) already require real services, run ownership, finite readiness, honest outcomes and exact cleanup. No product requirement or MVP reduction is proposed. Architecture AD-1/AD-4/AD-5/AD-9/AD-10 already assigns Platform composition and Builds execution. Q6 adds a qualification dependency account under their existing conventions; it changes no persistence or activation-fencing decision. No Platform UX design artifact was found; this affects developer handoff and diagnostics, with no screen, wireframe or module UX amendment.

EventStore retains 4.16's complete seven-part commit contract and AC1–AC11. Its persistence/Test/3.17/3.21 owners supply semantics and upstream intake. EventStore 3.18, 3.19, 5.6/5.7, 7.6, 7.22 and AD-12/9.2/AD-26 remain separate prerequisites. This plan does not edit those artifacts or claim their acceptance.

### Invariants applied to every proposed work item

1. AggregateActor stages and saves through `IActorStateManager`; Dapr owns physical addressing and provider calls. Module tests receive supported actor/Dapr endpoints, never backend credentials, SQL/schema or private actor keys.
2. Every protected state-changing batch, including metadata-only writes, compares the revision bound to its original read and advances it atomically. Initial revision creation is atomic. Save cannot attach a freshly fetched revision to stale cached values. Preserve high-water and contiguous-range rules.
3. Event identity is create-once, including with a current ETag. Event TTL is refused. Any revision or identity collision rejects the complete actor batch, including earlier operations and all coupled state.
4. Events, metadata, snapshot, EventsStored checkpoint, publication index/receipts and existing witness remain one actor transaction. Allocator, admission, pub/sub and external effects remain outside it; no cross-component atomicity is claimed.
5. A definite conflict commits nothing and clears staged/cache state before bounded reconciliation. Lost acknowledgement is Unknown until fresh owning-actor reads identify the exact original batch. A later witness or unavailable read is insufficient; no blind reappend or silent stale-batch renumbering.
6. Preserve authorized, bounded, resumable erasure and minimal completion state. The enforcing Dapr boundary validates existing lifecycle authority and atomically updates revision/progress. Raw delete, TTL, forged progress, delete/reinsert and stale pre-erasure writes cannot recreate history. Keep no erased event bodies or new evidence ledger.
7. CAS ensures one winner for a shared revision; it does not promise that the newest activation wins. Retain current admission. New activation epochs, private runtime builds, provider exceptions and replacement authority require separate exact-scope decisions.

## 3. Recommended Approach

**Direct adjustment, moderate scope:** propose one Platform-owned work package, `PLAT-ES416-QH`, with Q1–Q6 below as its reviewable units. This is a proposed local work reference, not an existing story, external issue or accepted assignment. Its scope is qualification infrastructure and runner integration. Upstream primitive delivery stays with upstream maintainers, coordinated by EventStore.

| Option | Decision | Effort / risk / schedule effect |
| --- | --- | --- |
| Direct adjustment | Recommended; extend one runner and composition model and reuse EventStore logical assertions. | Medium implementation/coordination effort; high capability and custody risk. Contract preparation can precede capability delivery; semantic execution waits. |
| Rollback | Reject; reverting descriptor adoption, security or lifecycle work does not create the primitive. | Regression effort with no qualifying benefit. |
| MVP reduction | Reject; narrowing writers, permitting overwrite/erase bypass or counting observational success violates the accepted contract. | Cannot meet 4.16 exit criteria. |

No release date or person-day commitment is inferred. Platform/Builds must name accepting maintainers and estimate Q2–Q5 after the contract review; EventStore must supply an eligible supported release before a qualification date can be committed. Private-build and provider-exception options remain unselected.

Sequence: accept responsibilities and contract design → deliver/adopt the runner extension and resource-free checks → receive an eligible capability and qualify verifier inputs → authorize one exact disposable run → prove the profile and both falsifications → consume separately delivered migration/compatibility/restore inputs → run final canonical conformance. Do not repeat the negative capability screen merely to advance paperwork.

## 4. Detailed Change Proposals

### Q1 — Accept the owning work and complete the intake

**Target after review:** Epic 2 dependency notes / new non-frozen implementation spec for `PLAT-ES416-QH`; Platform's external-prerequisite register. Add no numbered story or tracker status in this run.

**OLD:** P1 assigns execution to Platform; an accepting owner, Platform work reference, supported executing profile and acceptance command are pending.

**NEW:** “Platform accepts qualification infrastructure only when the acceptance record below names the accepting maintainer, exact work scope, contract revision and supported input requirements. Builds separately accepts runner/reader adoption. Unsupported capability remains an explicit stop even after work acceptance. A work receipt or infrastructure-ready result is not append conformance.”

| Required acceptance field | Producer / accountable owner | Initial disposition / condition |
| --- | --- | --- |
| `PLAT-ES416-QH` acceptance: maintainer, date, reviewed proposal revision, Q1–Q6 scope, exclusions and work location | Platform maintainer / Product Owner | **Pending**; role assigned here, no personal acceptance fabricated. |
| Builds work reference, accepting maintainer, schema/reader/writer/executor adoption and compatible released tool identity | Builds maintainer with Platform contract owner | **Pending**; current tool remains pinned. Publication, adoption, Platform acceptance and integration qualification are separate receipts. |
| Eligible released SDK/runtime/embedded-component identities, protocol/capability negotiation and complete semantics | EventStore persistence with 3.17/3.21; upstream maintainers deliver releases | **Blocked** by recorded negative findings. No upstream commitment or delivery date evidenced. |
| Profile definition and digest, selected suite/declaration and required tests, readiness, run bounds and credentials contract | Platform supplies topology/profile; EventStore supplies lawful vectors/read routes and suite | **Pending**; fill exact inputs from the selected work, not sample URLs or invented identities. |
| Versioned erasure receiver/secret-reference and key-generation contract; trust and custody review | EventStore/Security/7.6 with enforcing Dapr implementation; Platform provisions only qualified inputs | **Blocked** until receiver and exact references can be qualified. Prior design approval remains accepted. |
| Concrete acceptance command and authorized disposable target/window, including cleanup/falsification limits | Platform execution owner with EventStore Test; separate authority for each falsification | **Pending**; Q4 defines the command form and evidence denominator. |

**Rationale:** Make acceptance inspectable without converting EventStore's approved ownership assignment into a Platform commitment.

### Q2 — Version the missing Builds qualification bindings

**Targets after acceptance:** Platform-owned schemas/docs under `docs/contracts`; byte-identical Builds mirrors and embedded assets; Builds `TestEnvironmentContractReader`, descriptor/result writers, existing run-plan/selection/native handoff, private credential reader and native report processing; EventStore descriptor consumer adoption.

**OLD:** v1 readers accept only `hexalith.test-environment.v1` / `hexalith.test-result.v1`. Resource endpoints and generic artifact observations exist, but no typed binding associates a qualified storage profile, independent host roles and permitted controls with the exact run. The strict private credential v1 supports its defined Dapr API-token channel, not an arbitrary control credential.

**NEW — proposed contract decision, not a published schema:** publish paired `hexalith.test-environment.v2` and `hexalith.test-result.v2`, preserving all existing bindings, policy and lifecycle semantics and adding a required typed `bindings.qualification` for qualification executions. Both owners must accept this exact design before implementation. Existing v1 suites continue to use v1; no in-place v1 amendment or silent downgrade. The result snapshots the same qualification identity and records subsequent control observations. Extend both result and descriptor versions together, as v1's pairing rule requires.

| Proposed typed group | Minimum data and validation |
| --- | --- |
| `profile` | Purpose (`candidate` or `canonical`), definition content identity, support reference, Platform/catalog/composition identities, expected SDK/runtime/component type **and API version**, component configuration identity, backend image/configuration identities, actor-contract/capability version and required capabilities. Keep expected inputs distinct from actually observed loaded identities and verifier readiness. No configured pin substitutes for execution observation. |
| `hosts` | Unique host/instance identity, owning resource ID, shared logical actor app/type, role (`writer-a`, `writer-b`, `reader`), actual HTTP/gRPC endpoint references, sidecar/process/container identity and placement membership observation. Reader may reuse a restarted host; distinct writer processes/sidecars are mandatory. All references resolve to owned resources; do not copy provider addresses into logical actor routes. |
| `operations` | EventStore-supplied lawful operation, owning-actor logical read route, partition scope, semantic barrier IDs and exact required-test-set identity. Requests use the existing synthetic tenant plus run-owned domain/aggregate identities. No private actor-key or provider-command field. |
| `controls` | Stable allowlisted operation ID, version, owned endpoint reference, permitted host/partition/phase, finite deadline, maximum uses and reset/cleanup obligation. Bind to run/owner/attempt/environment, profile and suite identities. No shell, caller-supplied executable, arbitrary target, backend credentials or unrestricted fault API. |
| `observations` | Executing/loaded SDK, runtime, embedded component and backend identities; capability negotiation; host/barrier/placement transitions; control requested/applied/released/reset states, UTC times, affected resources, stable failure reasons and classified diagnostic references. Missing producers use explicit unavailable states and block required lanes. Startup failure is never “control applied.” |

Resource/endpoints, run/isolation, readiness/tasks, tool qualifications, candidate/decision/no-reuse lineage, artifacts, suite counts, deadlines, terminal outcome, retention and cleanup retain their existing responsibilities. New metadata must cross-bind them, not duplicate an unauthenticated source of truth. Unknown properties, wrong versions/policy, foreign run/profile/resource references, duplicate host/control IDs, excessive size and contradictory observations refuse atomically before module execution or credit.

Keep `HEXALITH_TEST_ENVIRONMENT_PATH` as the handoff. The runner explicitly selects the descriptor/result pair for the accepted suite; EventStore must declare/adopt v2 support before launch. Existing `--profile` still selects an owner-declared suite from the validated module manifest; it is not a free-form backend selector. The present manifest's runtime-version string alone is insufficient: bind the accepted Platform profile and instance/control plan by content in the existing plan path. No new module-manifest field is assumed; if implementation needs one, Builds must specify its compatible version/adoption before use. Unknown profile or unsupported consumer fails before allocation; do not restore a module-owned fixture as fallback.

Propose private `hexalith.test-endpoint-credentials.v2` only for the new protocol/control credential kinds, with explicit consumer adoption. Preserve strict resource/module/name/exact-URI bindings and add the exact attempt/environment/profile and control scope/expiry required here. Credentials never enter descriptor/result snapshots, logs, TRX or diagnostic references. Ordinary v1 Dapr invocation suites keep their current channel. Module tests receive run-limited API/control credentials; the application receives no backend principal or erasure signing key. HMAC receiver material remains exclusively in the separately qualified OpenBao/Dapr secret boundary.

**Contract acceptance criteria:** Platform publishes the semantics; Builds accepts, implements and ships byte-identical schemas/reader/writer support; EventStore adopts the same pair. Focused checks cover v1 compatibility, v2 round-trip and refusal, negotiation/no downgrade, foreign bindings, missing controls, credential separation, malformed reports and count reconciliation. Required tests must have inspected passing TRX results; zero process exit alone is insufficient. Actual executing tool version/content, Builds adoption, Platform acceptance and integration qualification remain independently authenticated prerequisites.

**Rationale:** The current closed schemas cannot safely carry these semantics. A paired explicit version avoids overloading diagnostics, inventing a second environment runner or claiming that an opaque URL establishes qualification.

### Q3 — Select exact profiles and deliver independent actor hosts

**Targets after acceptance:** Platform composition/declaration bindings and readiness producers; Builds existing run plan/resource claims; `PLAT-ES416-QH` topology acceptance.

**OLD:** Normal Redis composition and the special Memories PostgreSQL v2 experiment do not declare EventStore append support; the legacy peer endpoint is not proof of two actor hosts on the Platform model.

**NEW — profile disposition:**

| Profile/input | Intended use | Current eligibility |
| --- | --- | --- |
| Current catalog/default Redis composition | Existing consumer behavior and runner regressions | No 4.16 append qualification claimed; unchanged. |
| Memories `functional-retention`, daprd 1.18.1 / PostgreSQL 18.6 / component v2 | Existing source-only local experiment | `qualificationCredit=false`; excluded from append acceptance and unchanged. |
| Recorded EventStore historical 1.18.7 and screened 1.19.1 PostgreSQL v1 combinations | Retain exact negative source findings | Rejected for this contract. No installation, rerun or pin selection proposed. |
| Proposed EventStore disposable PostgreSQL candidate | Simplest first semantic proof using existing logical vectors | **Blocked**. Released supported SDK/runtime/component/capability, backend image/configuration and verifier inputs are pending. PostgreSQL v1 is the recorded candidate family; v2 is not a substitute or selected migration. Any different component API/profile needs explicit acceptance. |
| EventStore canonical AD-26 profile, owned by 3.19 and consumed by Platform 4.9 | Final production-path conformance after prerequisites | **Blocked** on exact qualified profile and required ratification/assurance. Candidate proof cannot ratify it. |

No target profile has an accepted executing tuple yet. Before provisioning, the execution owner must bind: exact Platform source and direct Builds catalog; actual tool version/content; module source revision or Release candidate artifacts; SDK assemblies; CLI/daprd binaries or images; embedded components-contrib identity; component type/API/configuration; backend image and configuration; Dapr security/placement configuration; suite/configuration/test-set identities; profile purpose; verifier contract/generations and value-free secret references; finite readiness/execution/stop/cleanup limits. Compare expected and loaded values from actual processes/resources. Missing identity is pending and grants no credit.

For each eligible profile, Platform provisions two independent actor host processes/containers **with separate sidecars and caches**, the same logical actor app/type/partition and protected backing state, and run-owned placement/control resources. Distinct resource and instance identities must not be mistaken for different logical actors. The profile binds both public actor transports when exposed and all reachable generic/bulk/transaction aliases from 3.17. 3.17 proves exclusion of unreachable aliases beside a lawful generic-state control; “no known caller” is insufficient.

Use run-scoped ports, networks, volumes, certificates, components, data/catalog scopes and credentials. Observe both hosts' actual module/runtime/component identities, required startup tasks, actor activation/placement and protected-store/verifier readiness. Complete a lawful operation and fresh logical read before adversarial work. Readiness fails for old/mixed unguarded writers or unavailable verifier inputs. No module child starts its own AppHost, backend or container.

**Topology acceptance:** two independently controllable hosts read the same initial durable revision; Platform can suspend A after that read, let placement move and B commit, then resume A with its Dapr-to-storage path usable. Reverse the commit ordering using fresh run-owned partitions. Capture instance/barrier/placement observations and a lawful persistence control during the stale commit window. Merely allocating two HTTP URLs, killing A or isolating its entire sidecar does not meet acceptance.

**Rationale:** Prove the topology and fault reach the persistence condition being tested; do not mistake a disconnected writer for enforced stale-state rejection.

### Q4 — Supply bounded controls and execute the unchanged logical denominator

**Targets after acceptance:** Platform run-owned control implementation; EventStore existing `AppendDurabilityRaceControl` / `AppendDurabilityRaceSession`, LiveSidecar.Tests and Server.Tests assertions. Reuse the existing assertion lanes; do not extend `Oq8PostgresqlFixture` into provider-owned module infrastructure.

**OLD:** P1 lists required adversaries, but Platform has accepted no concrete control contract or executing command.

**NEW — controls Platform must deliver:**

| Control | Required boundary and attribution |
| --- | --- |
| Arm / observe / release semantic after-read barrier | Coordinate EventStore's existing semantic synchronization for the exact batch/partition. Record the revision basis before release; do not simulate the condition with an arbitrary delay. Timeout/reset is bounded. |
| Placement disconnect/rebalance and stale-host resume | Affect A's placement/admission connectivity as required while preserving its usable persistence path. Observe B's activation/commit and A's actual resumed commit attempt. Preserve existing admission and the one-winner CAS limit. |
| Lose acknowledgement after commit | Establish durable original-batch commit through subsequent logical readback while suppressing its acknowledgement. Also exercise unavailable readback/cancellation. Do not classify a pre-commit network failure as this fault. |
| Same-identity raw actor and reachable generic writes | Supply observed HTTP/gRPC Dapr endpoints and the hosting application's run-scoped sidecar identity/token. EventStore sends semantic attacks for omitted/current/stale ETags, flags, empty-stream and metadata-only races, late collisions, delete/reinsert and TTL. No adversary receives erasure issuance keys. |
| Isolation and lawful/invalid erasure controls | Run other-principal/component/environment negatives beside lawful controls; use existing valid lifecycle capability issuance and bounded batches, interrupted progress/resume and completion replay. Raw attackers get no authority to erase. |
| Two separate disposable falsifications | Under separate exact-scope authorization, remove revision enforcement alone, then create-once enforcement alone, for the same profile/adversary. Keep a passing intact lawful control in each comparison. No shipped bypass switch, implicit private build, production mutation or provider exception. If unavailable, AC10 remains blocked. |

Every control has run/attempt/profile ownership, a finite expiry, an observed applied/released outcome and an idempotent reset. It refuses another run/partition, an unsupported phase/control, expired access or a hosted target. Controls belong to the existing runner's owned execution; no standing shared fault service or new erasure-authority service is introduced. Platform alone performs provider lifecycle, physical diagnostics, process/network mutations and cleanup. Physical diagnostics can explain a result; they cannot establish EventStore prevention.

**Logical readback:** EventStore supplies the owning-actor route and lawful commands. After each adversary, clear the reader's cache/staged state or restart its host, then obtain fresh supported Dapr/actor reads of exact event identities/content and coupled metadata, snapshot sequence, checkpoint/pipeline, publication index/receipts/recovery ownership and existing witness as applicable. Record winner/loser and expected/actual logical state. Do not replace erased event bodies with retained proof copies. Readback unavailable, failed startup or status code alone yields no semantic pass.

| EventStore criterion | Platform execution responsibility | EventStore/Test oracle required on each explicitly supported profile |
| --- | --- | --- |
| AC1 — lawful operations | Ready intact profile, restart/cache-clear and supported routes | Lawful append, zero-event command, snapshots, publication/recovery and restart preserve established atomic behavior. |
| AC2 — existing event | Same-identity raw transport/alias access or demonstrated exclusion | Current/stale/omitted ETags and caller flags cannot replace an event; original identity/content remain. Lawful generic control distinguishes alias denial from broken access. |
| AC3 — raw-first race | After-read gates, both orderings and fresh empty stream | Unguarded contender is denied, or guarded first commit survives and stale actor loses. No acknowledged event disappears and no overwrite is used to force the actor to win. |
| AC4 — atomic rollback | Late-collision multi-event vector with otherwise usable storage | Losing batch leaves no earlier new event or coupled metadata/snapshot/checkpoint/publication/receipt/witness effect. Include stale revision. |
| AC5 — failover overlap | Q3 independent-host transition and persistence-positive control in both orders | Stale revision is rejected at the mandatory check; exactly one batch survives with coupled state. Attribute failure to the precondition, not transport/placement/startup/storage unavailability. |
| AC6 — unknown outcome | Commit-ack loss and cancellation/unavailable-readback variants | Recover exact original batch once or remain Unknown/fail closed. No blind reappend, fabricated conflict success or duplicate publication/effect. |
| AC7 — erasure | Qualified receiver/keys, bounded interruption/resume and raw destructive vectors | Valid erase removes bodies and resumes; invalid/expired/cross-partition/forged/replayed capabilities and raw DELETE/TTL/recreation fail. Stale pre-erasure writes cannot resurrect history. |
| AC8 — isolation | Run-owned foreign principals, component targets and namespace/environment copies | Mutation denied beside lawful control under delivered EventStore 5.6/5.7 bindings and fresh OQ8 regression. |
| AC9 — upgrade/restore | Execute supplied initialization/old-writer/mixed-config vectors; consume separately owned migration/restore observations | Atomic concurrent initialization retains old bytes; unsupported writers refuse readiness/writes. EventStore 3.18 package parity and 5.6/5.7/7.22 migration/rollback/restore stay required. |
| AC10 — falsification | Two authorized disposable mutations and intact controls | Removing revision admits stale metadata-only commit and makes its prevention assertion fail; removing create-once admits replacement even with fresh revision and makes its assertion fail. Startup/network failures are invalid falsifications. |
| AC11 — final gate | Exact ratified canonical profile, complete writer inventory, delivered prerequisites and loaded identities | All applicable prevention/erasure/failover/compatibility assertions and existing assurance pass. Unknown capability, missing lane or unguarded writer keeps G-APPEND FAIL/BLOCKED. |

A falsification is successful only when the corresponding prevention assertion fails for the intended missing rule while its lawful control passes; label the expected failing run explicitly. Never rewrite that failed native assertion as ordinary conformance success. Record the complete declared denominator, actual command, source SHA, suite/profile/artifact identities, counts, outcome and CI link in the owning work/spec. Missing/skipped/withdrawn/incomplete rows remain non-passing. One profile's pass supplies none for another profile. Bound-input/runtime-minor changes require affected re-proof under the existing policy, not copied green evidence.

The first candidate-mechanism suite declares its exact AC1–AC7 and AC10 vectors plus independently executable initialization/old-writer vectors from AC9. AC8's delivered-binding/OQ8 proof and AC9's migration/rollback/restore/package-parity proof remain **pending prerequisites** where not yet delivered. Preserve those rows in the overall AC1–AC11 ledger; do not count them as passed, remove them or mark them N/A. This permits a credible mechanism result before separately authorized migration, without manufacturing full 4.16 acceptance. Here “candidate profile” means a mechanism trial; the descriptor's authenticated CI `candidate` classification still follows the unchanged candidate policy.

**Proposed command forms — not executable acceptance commands yet:**

```text
dotnet tool run hexalith-module test --manifest <accepted-owner-manifest> --mode source --profile <declared-append-candidate-suite>
dotnet tool run hexalith-module test --manifest <accepted-owner-manifest> --mode package --profile <declared-append-suite> --candidate-request <authenticated-issuance>
dotnet tool run hexalith-module validate-result --candidate-request <authenticated-issuance> --environment <descriptor> --result <result>
```

These reuse the existing CLI forms and require the future Q2-compatible accepted tool; no new flag or suite is claimed available. Before execution, replace every placeholder with owner-accepted inputs, record the exact argv and executing content identity, and validate against that released tool. Source runs are diagnostic; package candidate credit additionally needs actual disposable Linux GitHub-hosted execution, Release candidate artifacts, authenticated decision/latest attempt/no reuse and all tool qualifications. Current qualification-authentication refusal remains blocking. Real local service scenarios also retain 2.10's existing execution restrictions; this proposal grants no local or hosted run authority.

### Q5 — Preserve lifecycle, erasure custody and cleanup

**Targets after acceptance:** Existing runner resource registry/claims, termination and cleanup; Platform profile/control reset handlers; erasure secret provisioning under the existing custody boundary.

**OLD:** Existing runner rules govern ordinary resource cleanup; the proposed independent hosts, fault rules and verifier deliveries have no accepted ownership census or cleanup proof yet.

**NEW:** Register every host/sidecar, placement process, backend/volume, network rule/proxy, certificate, component, workspace, port/claim and temporary credential before or as it is allocated, including partial startup. Undo applied faults and restore usable diagnostic access on all exits within bounds. End module/control access on terminal cancellation/withdrawal/expiry even when local resources are retained. A failed local run retains surviving owned resources; stopping fault injection is not permission to delete failure-retained data. Successful local cleanup waits for attachment holds; same-job CI withdraws attachments before cleanup and cleans on every outcome, including partial provisioning and cancellation. A separately started environment and every peer owner retain their resources and data.

Use the existing first-terminal-outcome, termination, retention and cleanup records. Retry exact-owner cleanup idempotently; list all leftovers, claims and discovery uncertainty. Do not call cleanup complete until actual discovery proves zero owned resources. An assertion pass cannot hide failed cleanup; failed cleanup cannot relabel the first assertion outcome. CI recovery/finalization uses the durable owner receipt and exact `down --run-id`, not a broad process/container sweep. Preserve classified redacted results/diagnostics after deletion; remove private endpoint/control credentials and avoid archiving raw logs or secrets.

The accepted erasure design already extends signing-capable HMAC custody to the enforcing receiver. EventStore/Security must qualify canonical bytes, purpose, tenant/domain/aggregate and inventory binding, lifetime, key generations/rotation, missing-key refusal, resumable progress and completion/replay. Platform records value-free OpenBao/Dapr secret references and provisions only the qualified generations/receiver path. No new issuer, key service or verification-only claim; no key distribution before this qualification. Ordinary run cleanup of disposable resources never becomes a protected raw-delete bypass or proof of application erasure.

**Lifecycle acceptance:** exercise success, assertion failure, startup timeout/partial startup, control timeout, real execution/startup cancellation, fault-reset failure and cleanup retry on the selected topology; account for existing 2.8/2.10 results under their original subjects. Demonstrate exact peer preservation, immutable first outcome, local failure retention, attachment withdrawal-before-teardown and zero-owned-resource CI completion. Re-prove affected behavior when Q2/Q3 changes it; do not expand 2.8 into another full reference campaign.

### Q6 — Apply only the reviewed planning amendments later

This run contains the following exact proposed edit blocks; it does not apply them.

| Artifact / section | OLD | NEW | Rationale |
| --- | --- | --- | --- |
| Platform epics / External Prerequisites Register, new EventStore qualification row | No specific accepted EventStore 4.16 execution row. | “EventStore 4.16 primitive and logical suite: EventStore persistence/3.17/3.21/Test own supported capability intake, complete writer inventory and AC1–AC11. Platform `PLAT-ES416-QH` owns accepted profile composition, independent actor hosts, bounded controls, observations and cleanup. Builds owns the accepted versioned runner/descriptor/private-channel extension. Work acceptance and execution readiness remain pending until Q1 receipts and Q2–Q5 criteria pass. No append or final profile credit is inferred.” | Place the cross-owner dependency where Platform schedules consuming work; do not confuse the two repositories' Story 4.16. |
| Platform architecture / Deferred and Qualification Work, runner-owned qualification row | Generic runner lifecycle/descriptor responsibilities; no accepted append-control binding. | “EventStore append qualification consumes the same Platform model and Builds runner. Publish and explicitly adopt the Q2 descriptor/result and private-control versions before qualification handoff. Supported Dapr owns mandatory storage enforcement; Platform executes run-owned host/fault/lifecycle controls and EventStore owns logical acceptance. Missing capability/control/custody proof blocks that lane.” | Clarify integration under AD-1/4/5/9/10. Architecture owner checks subject impact in the existing memlog; no new orchestration, persistence exception or fencing AD. |
| Platform descriptor contract / new versioned qualification section | “Changed semantics need a new published version and explicit consumer adoption.” | Preserve that sentence and v1; add the paired v2 semantics in Q2, documented alongside rather than silently replacing v1. Link published schemas and exact consumer support after they exist. | Retain the current closed-contract/versioning promise. |
| EventStore 4.16 / non-frozen Delivery Intake note, by EventStore owner only | “Platform acceptance of concrete qualification execution is pending.” | “Platform proposal `PLAT-ES416-QH` is prepared. Acceptance remains pending until named Platform/Builds receipts bind the reviewed work, profile/contract versions, prerequisites, controls, cleanup and exact command. Recorded negative capability findings and AC1–AC11 remain unchanged; acceptance of infrastructure alone does not unblock G-APPEND.” | Return a truthful written planning response to P1; do not edit sibling owner artifacts from this run. |

PRD FR/NFR/SM text, the SPEC acceptance contract, UI/UX and frozen intents need no amendment for this execution plan. Later accepted implementation may add owning work/specs and reconcile the relevant dependency notes through their owners; no tracker regeneration or digest/pin refresh is part of this proposal.

## 5. Implementation Handoff

**Classification: Moderate.** Product Owner / Platform Developer coordinates backlog work; Builds owns runner changes; EventStore owns semantic acceptance and upstream capability intake; Architecture/Security retain contract and custody decisions. Handoff here is a written plan, not a sent message, created issue, assigned person or started agent.

### Responsibilities and deliverables

| Accountable recipient | Owns / delivers | Does not supply by receipt alone |
| --- | --- | --- |
| Platform Product Owner / maintainer | Accept or revise `PLAT-ES416-QH`; name accepting maintainer, work location, Q1 scope and sequence; retain gates and current pins. | Upstream support, Builds acceptance or execution authority. |
| Platform contract owner with Builds maintainer | Accept Q2 design; publish/mirror paired schemas; implement selection, readers/writers, private-channel binding, refusals and adoption; release through existing controls. | Platform tool qualification, authentic qualification authority or EventStore consumer adoption. |
| Platform execution owner | Q3 profile/hosts/readiness, Q4 controls and observation, Q5 isolation/census/cleanup and exact authorized argv/target. Physical diagnostics remain here. | EventStore logical truth, erasure issuance authority or final profile ratification. |
| EventStore persistence / 3.17 / 3.21 / upstream maintainers | Eligible supported release and negotiation, all writer/alias coverage and negative findings; semantic vectors/barriers. | Platform acceptance or prevention from a source-only result. |
| EventStore Test / 4.16 owner | Adopt descriptor pair, own lawful operations/read routes and AC1–AC11 oracles; distinguish Conflict/Unknown; record exact logical results. | Provider lifecycle/credentials, private actor keys or a new fixture stack. |
| Security / EventStore 7.6 / enforcing Dapr implementation, with Platform secret provisioning | Qualify the accepted HMAC trust expansion, receiver/secret references, generations/rotation/refusal and erasure progress/replay. | Distribution authority before exact qualification or a new erasure design approval. |
| EventStore 5.6/5.7, 3.18, 7.22 and Platform deployment owners | Separately accepted isolation, reader/default/data migration, guarded initialization, rollback, compatibility and restore; fresh OQ8. | Migration authorization from a candidate race-test result. |
| EventStore 3.19 / AD-26 ratifiers and existing AD-12/9.2 assurance owners; Platform 4.9 / 2.10 / 3.9 | Canonical profile, remaining assurance, reference tool/lifecycle acceptance and actual loaded-artifact/candidate authority. | A final gate from this planning document or unchanged JSON references. |

### Acceptance and unblock conditions

| Gate | Exact condition to advance | Current status |
| --- | --- | --- |
| U1 — work accepted | Named Platform and Builds receipts bind this reviewed scope, owning work references, contract decision and estimable deliverables; EventStore acknowledges assertion/consumer responsibility. | **Pending**; this proposal is ready for review. |
| U2 — infrastructure ready | Q2 versions adopted by exact executing tool and consumer; Q3 independent topology/readiness/identities and Q4 controls verified; Q5 ownership/reset/retention/cleanup checks pass. Record precise inputs, command and target. | **Pending**; existing runner support is a reusable base, not this qualification. |
| U3 — capability eligible | Supported released SDK/runtime/component with mandatory read-bound revision, create-once, high-water/range, complete-batch rollback and authorized-erasure semantics across all reachable paths; qualified receiver/trust/secret contract and no legacy bypass. | **Blocked**; recorded 1.19.1/PostgreSQL v1 finding remains negative. No private build, alternate component or provider exception selected. |
| U4 — disposable candidate mechanism proof | U1–U3 plus exact run authorization; passing Q4's declared AC1–AC7/AC10 and executable AC9 initialization/old-writer subset, both AC5 orders, fresh logical readback, two separately authorized falsifications, honest cleanup and complete subset counts. Keep prerequisite-dependent AC8/AC9 rows explicitly pending in the full ledger. | **Blocked**; no run, result, falsification authorization or prevention pass supplied here. This partial mechanism result allows later migration planning; it does not close 4.16. Source proof remains diagnostic for candidate-policy purposes. |
| U5 — final conformance | Delivered 5.6/5.7/3.18/7.6/7.22 inputs, fresh OQ8, exact ratified canonical AD-26 profile, full 3.17 inventory, qualified 3.21 tuple, existing assurance, accepted tool and authentic candidate/loaded-identity proof where required; the full AC1–AC11 denominator passes with no missing lane. | **Blocked**. G-APPEND stays FAIL/BLOCKED until EventStore's complete gate is met. Other readiness/baseline/staging/production gates retain their own decisions. |

U2's implementation preparation may progress independently of U3; no semantic qualification execution proceeds around U3. A failed or unsupported required control stays blocked, not N/A. An excluded alias needs a demonstrated exclusion plus lawful control; that bounded result never excuses a missing applicable lane. Key distribution waits for receiver/custody qualification. Falsification, migration, pin changes and production actions keep their separate exact-scope approvals.

### Protected baseline and review record

At inspection Platform HEAD was `5cf3e8cbbea06cebcb8fc6ed2b8489ace1934732` with a clean working tree. The requested EventStore checkout HEAD was `51700ffb1542bee1f64d12f6ad2d9bf2c988fe78`; Platform's direct EventStore gitlink remains `cab8363c2b6e2c0588efc8ca9c2e09fab0006841`. This difference is recorded as provenance, not resolved by dependency update or sibling fallback.

Keep `.config/dotnet-tools.json` at `hexalith.builds.module.cli` **4.45.0**, Builds gitlink `27145e52ffab5623198ba5efdab5cdb71aa42d28`, catalog Dapr runtime **1.18.7**, CLI **1.18.2**, and .NET Dapr packages **1.19.0**. These observed pins establish no eligible append combination. The screened 1.19.1 candidate and the Memories 1.18.1 local profile are separate identities. No pin, tool-range, lock, profile ratification, epic digest or tracker value is renewed here.

| Input | Observed SHA-256 |
| --- | --- |
| Requested EventStore readiness-recovery proposal | `8edd46b63f47e277648e8a837af236b2e3bee23d50612d66f5f091b7cf7483f7` |
| Platform tool manifest | `f8cf363dd259788d71047ed7e3b14e5917f7531c43b06a2f83f438359dc23b6c` |
| Descriptor v1 canonical schema | `bf1e46acc773991037daaae20dff1d815faf4af13ab7887d88339609607ff357` |
| Result v1 canonical schema | `9773678febac38025fae837154341d9036ce8aa94e66b0d30c7e15ee668d7e39` |

| Correct Course checklist | Status / disposition |
| --- | --- |
| 1.1–1.3 — trigger and evidence | [x] P1 intake, existing runner contracts and recorded negative capability findings identified. |
| 2.1–2.5 — epic impact and order | [x] All twelve epic areas screened; bounded Epic 2 work proposed, Epic 3/4 dependencies retained; no epic removed, renumbered or declared obsolete. |
| 3.1–3.4 — artifact conflicts | [x] PRD/SPEC invariants retained; architecture/dependency and versioned contract edits proposed; UI/UX amendment N/A. |
| 4.1–4.4 — options | [x] Direct adjustment selected; rollback and guarantee reduction rejected; effort, risk and conditional sequence recorded. |
| 5.1–5.5 — proposal and handoff | [x] Five sections, Q1–Q6 old/new proposals, exact responsibilities, AC1–AC11 mapping and U1–U5 unblock criteria supplied. |
| 6.1–6.2 — consistency / verification | [x] Focused document and preservation checks recorded below; no product qualification inferred. |
| 6.3 — approval | [x] Administrator accepted complete Q1–Q6 with “I accept” on 2026-11-09. Platform planning application is recorded below; runtime implementation/execution and other owners' receipts remain outside that acceptance. |
| 6.4 — tracking | [N/A] No approved epic/story additions/removals to apply; tracker and readiness remain unchanged. |
| 6.5 — handoff | [x] Written handoff prepared for Platform/Builds/EventStore/Architecture/Security; actual recipients' acceptance and execution remain pending. |

The skill activation resolved no prepend/append steps, persistent facts or completion actions. Batch preparation follows the user's concrete planning request. Correct Course proposal preparation is complete; approval/application and runtime delivery are outside this run.

### Verification of this planning deliverable

- `python3 -B /tmp/platform-es416-plan-4r4kyp0u/check_proposal.py` — PASS, exit 0: 320 structural/content/preservation checks, including Q1–Q6, AC1–AC11, U1–U5, all 13 local links and the P1 fragment, 230 unchanged input files, canonical/Builds v1 schema parity, unchanged HEAD/index/gitlinks and the sole new-proposal working-tree scope. The scratch checker also checks this untracked document's whitespace and size.
- `git diff --check` — PASS, exit 0. No tracked file changed; the scratch check separately covers the new proposal.

These are document checks. Product builds, live tests, upstream capability re-screens, prevention passes and new CI runs: **zero**. Runtime qualification, work acceptance and readiness are unchanged. Scratch validation stays outside the repository; no new evidence framework or test tooling is added.

## Approval Application — 2026-11-09

Owner instruction: **“I accept”**. Accepting Platform planning owner:
**Administrator**, as identified by this session's project configuration.
Reviewed proposal SHA-256:
`ec27e5993acc5cdd6227add69154d1207411f5b2cd4f40f97e9b1a2a104d15f4`.
The complete Q1–Q6 planning scope is accepted. The reviewed Q1–Q6 text and its
initial-disposition tables remain historical; this application record governs
the current state. No accepting Builds/EventStore maintainer is invented, and
no tool `platformAcceptance` qualification is populated by this planning receipt.

| Applied artifact | Planning application |
| --- | --- |
| [PLAT-ES416-QH](../implementation-artifacts/spec-plat-es416-qh-qualification-handoff.md) | Created the unnumbered draft owning handoff for Q1–Q5, with the acceptance receipt, exact responsibilities/criteria, pending prerequisites and U1–U5 sequence. Draft means implementation is not delivered. |
| [Epics](epics.md) | Added Q6's EventStore qualification prerequisite and Epic 2 owning-work note. Preserved every numbered story, original criterion, lifecycle and 2.8/2.9/2.10/3.9 boundary. |
| [Architecture](architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md) and its existing memlog | Applied Q6's Deferred/Owned work row under AD-1/4/5/9/10. Recorded scope, accepted boundaries and subject impact; all AD rule blocks/IDs and existing final status remain unchanged. |
| [Descriptor contract documentation](../../docs/contracts/test-environment-v1.md#accepted-platform-qualification-plan--publication-and-adoption-pending) | Appended the accepted Platform v2 design plan beside unchanged v1 semantics. No v2 schema, reader support or publication is claimed; Builds acceptance and explicit consumer adoption remain required. |
| EventStore 4.16 non-frozen intake note | Prepared Q6's written return handoff here and in the owning work. No sibling EventStore file was changed; its owner applies any corresponding intake update. |

**Current intake:** Platform planning scope is accepted. U1 remains partially
satisfied until a named executing maintainer, Builds work/acceptance and EventStore
consumer/assertion receipts complete the intake. U2 is pending; U3–U5 are blocked.
No supported append profile is delivered by this approval. The negative
1.19.1/PostgreSQL v1 finding, mandatory revision/create-once/whole-batch/authorized
erasure contract, AC1–AC11 denominator and G-APPEND FAIL/BLOCKED remain intact.

This is the moderate PO/Platform Developer written handoff, with Builds,
EventStore persistence/Test, Architecture and Security responsibilities as listed
above. No external request, message, issue, runtime implementation, service/fault
execution, falsification, migration, secret distribution, dependency/pin update,
tracker regeneration or readiness rerun occurs. The Correct Course workflow is
complete for the accepted planning scope; runtime delivery remains separate work.

Application validation:

- `python3 -B /tmp/platform-es416-accept-ms3saam_/check_application.py` — PASS,
  exit 0: 314 focused checks cover the recorded acceptance, exact reviewed Q1–Q6
  text, unchanged numbered story bodies and all 15 AD groups, append-only
  historical memlog entries (with the script-managed timestamp update), unchanged
  current v1 documentation prefix, 27 local links/fragments, 226 preserved inputs,
  HEAD/index/gitlinks, and the six-file planning-only scope.
- `python3 .agents/skills/bmad-architecture/scripts/lint_spine.py --workspace
  _bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27`
  — PASS before and after the amendment, exit 0 and zero findings.
- Architecture's independent rubric/input-reconciliation, repository
  reality/currentness and adversarial-divergence reviewers all PASS with no
  findings for this bounded amendment. Full reviews remain in local scratch
  `/tmp/platform-es416-accept-ms3saam_/architecture-gate/reviews`.
- `git diff --check` — PASS, exit 0. The focused checker also covers whitespace
  and bounds for the untracked proposal and new draft handoff.

No product build, live test, upstream capability re-screen, prevention pass or new
CI run occurred. Pins, tracking, readiness and all runtime acceptance gates remain
unchanged. These checks finalize only the accepted planning amendments.
