---
title: '1.7 Ratify local Dapr conventions and bind component names'
type: 'feature'
epic: 1
story: 7
created: '2026-10-15'
status: 'done'
baseline_commit: 'c3af1f2c9e7b0d1b003a1bd5978545f664e38167'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Declaration composition uses fixed component names, allow-by-default ACL templates and no local mTLS or declarative subscriptions. Existing topic and interface declarations cannot express subscriber routes or precise HTTP invocation permissions.

**Approach:** Ratify local Dapr conventions through `bmad-architecture` and apply them to the shared composition and owner contracts.

## Boundaries & Constraints

**Always:** Render security and bindings before adding resources; failures identify source, field and reason. Give every receiver a deny-by-default policy with explicit caller identity, path and verb grants. Match sidecar, Sentry and control-plane trust settings. Assign deterministic component names, inject them through configuration keys actually read by owners, and restrict scopes to intended selected servers. Preserve shared-state readers and Memories telemetry-store isolation. Render subscriptions only from explicit subscriber bindings, preserving dead-letter policy and disabling duplicate programmatic discovery. Publisher inventory alone creates no subscription.

Record `Hexalith.EventStore.Aspire`, `Hexalith.EventStore.ServiceDefaults` and the toolkit as canonical, consulting Commons. Builds owns versions, including local Dapr images; reconcile file-entry drift. Refuse new hosting helpers until recorded. Cover projects, containers and signed Builds runs. Preserve isolated defaults, Works, readiness/cleanup and snapshot provenance.

**Never:** Guess routes or HTTP methods, broaden grants to pass startup, migrate hosted environments, introduce provider exceptions, or initialize nested references. Hosted policy and concurrent-run isolation remain later stories. Separate release/image availability from fixture evidence.

**Decision (2026-10-15):** Extend v2 declarations with optional explicit server scopes, subscription bindings and HTTP invocation rules, then update owner manifests. Keep one validated contract; preserve existing inventories and isolated use without fabricating bindings.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Local invocation | Selected receivers and explicit caller grants | mTLS; allowed invocation succeeds; unlisted caller is denied | No permissive fallback |
| Capability | Logical role, recovery class, consuming servers and configuration keys | YAML name, owner options and sidecar references agree; exact scopes | Conflicting/unsupported bindings fail before resources |
| Topic | Publisher inventory or explicit subscriber binding | Only subscribers get scoped Subscription objects using bound pubsub name and delivery route | Missing receiver/route or invalid dead-letter target refuses |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Composition/PlatformAppModel.cs` — atomic preparation, resources and template rendering; `PlatformLocalProfile.cs` assigns literals; `PlatformDaprEndpointSubscriber.cs` configures actual generated CLI processes.
- `DaprSelfHostedMtls.cs` — Works foundation: project-only, fixed ports, runtime 1.18.3 versus catalog 1.18.2.
- `references/Hexalith.Builds/schemas/hexalith.module-manifest.v2.json` — topics lack direction/server/route; operations lack paths/verbs; capabilities lack server scope. Story 1.1 permits optional additions within v2.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` hardcodes resource names. Generic `Dapr:*Component` declaration keys do not configure actual publisher/projection/state options.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md`, `docs/platform-apphost-form.md` — use the architecture workflow to record convention, canonical package ownership, binding rules and helper gate.
- [x] `references/Hexalith.Builds/{schemas/hexalith.module-manifest.v2.json,src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs}`, `references/{Hexalith.EventStore,Hexalith.Memories,Hexalith.Tenants}/module/hexalith.module-manifest.v2.json` — add optional v2 scopes, subscription bindings and HTTP invocation rules with accurate owner data; preserve v1 compatibility and atomic diagnostics.
- [x] `src/Hexalith.Platform.Composition/{PlatformDaprBindings.cs,PlatformAppModel.cs,PlatformLocalProfile.cs,PlatformServerBindings.cs}`, `DaprComponents/reference/` — resolve capabilities once; render names, scopes, per-receiver ACLs and subscriptions; bind actual owner configuration keys.
- [x] `DaprSelfHostedMtls.cs`, `src/Hexalith.Platform.Composition/PlatformDaprEndpointSubscriber.cs`, `apphost.cs` — integrate authenticated local control plane for projects/containers and signed plans, using catalog pins and existing lifecycle ownership.
- [x] `references/Hexalith.EventStore/src/{Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs,Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs,Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs,Hexalith.EventStore/Controllers/ProjectionNotificationController.cs,Hexalith.EventStore.Server/Configuration/ProjectionChangeNotifierOptions.cs}`, `references/Hexalith.Memories/src/{Hexalith.Memories.EventStore/EventIngestionController.cs,Hexalith.Memories.Server/Program.cs}`, `references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs` — accept assigned names and declarative mode while retaining delivery endpoints and isolated defaults.
- [x] `tests/Hexalith.Platform.Composition.Tests/{PlatformDaprBindingsTests.cs,PlatformAppModelTests.cs,BuildsSourceStartupTests.cs}`, `references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/PlatformManifestValidationTests.cs` — test matrix, effective CLI configuration, subscription delivery/dead-letter handling and real allowed/denied mTLS invocation; run affected owner regressions.

**Acceptance Criteria:**
- Given the canonical set is absent/present in the spine, When a new hosting helper is evaluated, Then it is refused/accepted against that recorded set.
- Given reference-composition checks, When results are retained, Then evidence distinguishes live enforcement/delivery from model assertions.

## Implementation Notes

Resolved the selected v2 declarations once into deterministic component names, actual owner option bindings, exact scopes, receiver ACLs and explicit subscriptions before adding resources. Stable default names preserve shared readers and telemetry-store separation. Publisher inventories do not create subscriptions. Owner delivery endpoints and isolated defaults remain available while declarative mode suppresses programmatic registrations.

Projects, containers and signed Builds plans use catalog-pinned local mTLS. Workload ACL trust is `public`; Sentry/control-plane trust is `localhost`, namespace `default`. Signed scheduler data lives under the owned workspace. Tenants API dependency readiness uses an explicitly configured gateway HTTP health endpoint, preserving its real `/ready` check without additional invocation grants.

Explicit owner subscriptions preserve Memories `memories-events` at `/events/ingest` and EventStore `*.*.projection-changed` at `/projections/changed`. Optional `subscription.topic` supplies the physical topic or pattern; `deadLetter.topic` remains a same-role inventory reference resolving uniquely to a distinct concrete physical target. Scoped Subscription files reach the actual Aspire-generated CLI resource paths. Tenants API receives only source-established POST grants for `/api/v1/commands` and `/api/v1/queries`.

The architecture workflow and required independent reviews passed. Owner changes still need owner review/merge/release and catalog promotion; released snapshots/provenance were preserved. Live package-image qualification is not claimed. Cold default file startup encountered concurrent owner build-output contention; the documented sequential-prebuild file-entry invocation passed. The local build workflow is complete and sprint tracking is in review; this does not close the owner release prerequisites.

## Spec Change Log

## Review Triage Log

- Parent diff audit: confirmed missing replacement subscriptions and gateway business permissions; added bindings from the actual owner topic attributes, configuration and client paths. Added actual CLI path/content assertions and a full six-server package fixture preserving the required gateway project seed. The suspected container-only gateway lookup regression was narrowed to an unsupported branch; no release behavior was expanded.
- Scoped architecture rechecks passed for physical-topic/dead-letter semantics and duplicate physical subscription identity. Negative cases cover invalid aliases, self-loops, wildcard targets and ambiguous targets.
- Parent matrix audit: added configuration-key and recovery-class conflict cases to the invalid-binding theory; all 12 cases passed with source/field diagnostics and null declarations. This final addition changes tests only.
- Live fixture: retained separate port-conflict and CLI transport-option failures. The passing fixture uses direct pinned `daprd` with an explicitly recorded fixture-only Sentry port adaptation; it does not exercise CLI launch. Actual generated CLI assertions and the refreshed real Builds source smoke remain separate evidence.
- A concrete projection publication returned 204 but no wildcard subscriber callback appeared within two seconds. Preserved the established owner pattern and recorded the existing Redis provider limitation in `deferred-work.md`; projection wildcard delivery is not claimed.

### Independent review triage (2026-10-15)

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | high | patch | Timesheets opt-in reaches the new deny-default renderer, while its retained candidate has EventStore callers but no invocationRules for its established process/query/project/replay endpoints; those calls now have no grant. |
| B2 | medium | patch | ProjectionChangeNotifierOptions binds IConfiguration, while ProjectionNotificationTopicAttribute reads only an environment variable; a newly accepted appsettings custom component publishes and subscribes under different names. |
| B3 | medium | patch | Confirmed against decompiled pinned toolkit: LocalPath bypasses component generation/name replacement and adds the original file directory to resource paths; custom assigned names with default YAML metadata therefore diverge. |
| B4 | medium | patch | Schema accepts mixed-case configuration keys; both semantic key tracking and resolved environments use Ordinal, whereas .NET configuration collapses casing. Conflicting effective options can be admitted. |
| B5 | medium | patch | Hyphen concatenation does not uniquely encode owner/server/topic tuples: server a + topic b-c and server a-b + topic c generate the same subscription filename despite different valid subscriptions. |
| B6 | maybe-false | defer (medium, unverified) | WaitAndWriteAsync first waits for all application health checks, then checks each sidecar outbound health; no current reachable wrong-channel/discovery success was demonstrated. A fixture with healthy applications and a broken permitted invocation would settle whether this readiness contract needs an additional safe probe; no invented startup grant will be added. |
| B7 | medium | patch | The new sidecar loop returns false on timeout without ReportFailureAsync, unlike the existing path; its resource/app ID and last HTTP/connection status are lost from startup failure evidence. |
| B8 | medium | defer (pre-existing) | The file entry already selected ambient global Dapr tools before this story; adding a DaprHome option does not remove that existing fallback. Current live qualification explicitly supplies catalog-pinned tools. Universal ambient-tool preflight remains follow-up work. |
| B9 | medium | patch | Empty YAML can index an absent document and metadata replacement is outside the template diagnostic try/catch. Those admitted template inputs produce collection exceptions without the required source/field context. |
| B10 | medium | patch | The helper gate only searches for package names anywhere in the document. A document mentioning all three outside an adopted AD-9 canonical-hosting record passes despite the required decision being absent. |
| E1 | medium | patch | The custom state name is injected into the gateway but not optional Admin.Server; AdminServerOptions defaults to statestore while that sidecar references the assigned component. |
| E2 | medium | patch | Independently confirmed the same IConfiguration/environment projection component mismatch as B2; no provider-specific assumption is required to reproduce the metadata divergence. |
| E3 | high | patch | ResolveCertificateDirectory unconditionally locates a repository above builder.AppHostDirectory. Builds creates its builder without a Platform directory override, so a host installed outside the checkout can fail despite valid options.PlatformRoot. |
| E4 | medium | patch | Independently confirmed B4: mixed-case binding keys pass the ordinal dictionaries and collide at the case-insensitive .NET configuration consumer. |
| V1 | medium | patch | Pre-verified gap: no test exercises DaprRateLimitConfigSync with an assigned store; existing tenant-limit tests seed configuration directly, so reverting the actual consumer to configstore would escape verification. |
| V2 | medium | patch | Pre-verified gap: invalid-verb schema tests do not exercise caller-inventory or concrete/in-prefix path guards. Removing either semantic guard leaves current tests passing while accepting broader permissions. |
| V3 | medium | patch | Verified the filed other finding against owner option registration and controller metadata: appsettings custom PubSubName can now validate while the subscription still names pubsub; same root cause as B2/E2. |
| I1 | medium | patch | The implementation handoff noted a second demonstrated naming shape: same inventory name/server with distinct physical topics passes subscription identity validation but reuses the renderer filename. Same non-unique filename encoding root cause as B5. |

| P1 | high | patch | Parent patch audit found both actual gateway seed programs still mapped programmatic discovery unconditionally and bypassed configured metadata binding. Four actual-entry HTTP cases now verify configured names and declarative 404/metadata removal in both seeds. |
| P2 | medium | patch | Parent full-suite execution reached the supported package fixture and failed because the explicit already-resolved PlatformRoot was searched again for AppHost marker files. The legacy inferred-root overload can retain discovery; explicit-root exclusion must use the supplied normalized root. |
| P3 | medium | patch | Compose invokes certificate validation only after adding Redis/components. An inside-repository or symlinked certificate directory therefore leaves resources on refusal; move the existing Sentry validation/creation before the first resource and cover atomic refusal. |
| P4 | medium | patch | Parent affected Builds suite reproduced two NullReferenceExceptions for a missing required configurationKey after effective-key normalization was added. Preserve the existing required-field diagnostic and skip semantic key comparison when the key is absent. |

Grouped patches: B2/E2/V3 share projection metadata configuration; B4/E4 share effective-key comparison; B5/I1 share subscription filename identity. Other patch rows have separate root causes. No intent gap or spec correction is required: each patch is a direct correction with no new public surface and covers demonstrated inputs.

All patch findings above are fixed and verified. The final Platform suite covers Timesheets grants, case-insensitive effective keys, collision-free subscription identities, malformed templates, explicit certificate boundaries/atomic refusal, both actual gateway seeds, and preserved Works/package graphs. The final Builds classes cover missing-key diagnostics, caller/path guards and structured sidecar timeouts; EventStore classes cover supplied YAML names, Admin.Server binding, configured projection metadata and assigned-store effective rate limits. B6 and B8 remain deferred with their original verdicts; the existing Redis wildcard-delivery limitation is also recorded in the deferred ledger. No intent/spec loopback occurred.

### Resumed independent review triage (2026-10-16)

Three context-free layers reviewed the complete baseline diff, including unrelated work committed independently before/during this resumed run. Every finding is classified below before grouping. `R2-B*` are Blind Hunter findings, `R2-E*` Edge Case Hunter findings, and `R2-V1` the pre-verified verification-gap finding.

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| R2-B1 | high | patch | Timesheets ServiceCollectionExtensions registers AddEventStoreDaprServiceInvocation for eventstore, and EventStoreGatewayClient uses POST commands/queries and GET command status paths. EventStore's receiver declares only tenants-api, so enabled Timesheets traffic is denied. |
| R2-B2 | medium | patch | The retained Timesheets admin surface inventories eventstore but has no invocation rule. EventStore AdminOperationalIndexHostedService actually invokes POST admin/operational-index-metadata, which the SDK maps, so discovery is denied. |
| R2-B3 | medium | defer | The pinned Dapr 1.18.2 ACL trie returns an existing path action before consuming descendant segments. This underlying provider behavior predates the story; previously permissive policies already admitted descendants. Existing explicit grants improve that boundary but do not establish exact matching. Record the provider limitation without inventing a new hosting helper, application authorization surface, or provider exception. Owner endpoint authentication still applies. |
| R2-B4 | medium | patch | Dapr 1.18.2 lowercases HTTP operation paths and retains the first action inserted at a trie node. Separate valid rules for one caller/path therefore lose later verbs. Group the existing rendered rules by case-insensitive path and union their verbs. |
| R2-B5 | medium | defer (carried) | carried: signed-run control-plane ports remain fixed, as already deferred in Review Findings. Concurrent-run isolation is explicitly excluded by the frozen intent; no duplicate patch or ledger entry. |
| R2-B6 | false | reject | TenantEventRoutingOptionsValidator compares configured PubSubName with EventIngestionController.PubSubName and rejects a mismatch on startup when Topic is enabled. The reported silent divergent subscription cannot occur; the isolated appsettings restriction existed with the earlier constant name. Platform supplies the accepted environment binding. |
| R2-B7 | medium | patch | A child may exit between poll and killpg in the new live fixture, raising ProcessLookupError and stopping cleanup. Catch that specific disappearance at both signals, preserving remaining cleanup. |
| R2-B8 | high | defer | DaprSigningProviderAdapter falls through from a parsed non-Signed query to effect-sign without checking explicit absence. Confirmed in the independently committed EXT-SECRETS-1 adapters (26628896b4fc7b70337c22b4ee34a4b6b0edc6d8), which predate this resumed story work and were not caused by Story 1.7. |
| R2-B9 | high | defer | DaprRevocationAuthenticatorAdapter sends the complete envelope but accepts a response echoing only its evidence digest, event, tenant and signature digest before attaching the requested envelope. Issuer/audience/key/revision echoes are not checked. This belongs to the same independent EXT-SECRETS-1 commit, not this story. |
| R2-B10 | high | defer | DaprCompromiseRegistrarAdapter.Body and Receipt omit both revocation and trust-profile revisions, then attach the supplied complete envelope. Revision-bearing envelopes can share accepted proof. Independently committed EXT-SECRETS-1 work; no Story 1.7 path changes those adapters. |
| R2-E1 | high | defer | Independently confirmed R2-B8: parsed Denied/Conflict/Unknown or mismatched Signed metadata reaches effect-sign. Same root cause in the pre-existing independent adapter commit. |
| R2-E2 | medium | defer | DaprSecretsInvoker uses default buffered SendAsync before SecretsProof validates size; excessive response bytes can consume substantial memory before parser refusal. The framework has a large default buffer cap, so literally unlimited memory is not established. The missing small response bound is real and belongs to independently committed EXT-SECRETS-1. |
| R2-E3 | medium | patch | Render validates that secretstore metadata is a sequence but never requires a secretsFile item. An otherwise valid admitted template can omit the generated credential binding and still add resources. Require that existing binding inside the scoped diagnostic block and cover atomic refusal. |
| R2-E4 | medium | patch | Independently confirms R2-B7: disappearance at SIGTERM or SIGKILL aborts later owned cleanup. Same root cause, one patch. |
| R2-E5 | medium | defer | Recovery inventory excludes several private-file suffixes/names but not id_ecdsa or .p12, so it reads/fingerprints such bytes if present. The recovery verifier is independent work already present on activation and was not introduced by Story 1.7. |
| R2-E6 | maybe-false | defer (medium, unverified) | The recovery preflight calls wait without a timeout after SIGKILL; indefinite blocking would require a parser that remains unreapable. A process fixture demonstrating that condition under the fixed local in-memory parser is needed to establish reachability. This is independent recovery code present before this resumed story. |
| R2-E7 | medium | defer (carried) | carried B8 from the earlier triage: without explicit DaprHome, ambient CLI/runtime fallback predates this story. Fresh qualification supplies pinned tools; universal preflight remains the previously recorded follow-up, with no duplicate ledger entry. |
| R2-V1 | medium | patch | Pre-verified: within-owner recovery-conflict tests stop at ManifestValidator; none exercise Resolve's graph-level recovery comparison. Add two individually valid owners with incompatible recovery classes and assert combined atomic refusal with source/field diagnostics. |

Grouped patches: R2-B7/R2-E4 share process-disappearance cleanup; all other patch rows have separate root causes. Six direct patches add no new public surface and guard demonstrated states. No intent or specification loopback is needed. Seven unrelated/provider follow-up groups are recorded in the deferred ledger; previously carried deferrals are not appended again.

Provider evidence: [pinned Dapr 1.18.2 ACL trie](https://raw.githubusercontent.com/dapr/dapr/v1.18.2/pkg/config/acl_trie.go), [pinned ACL parsing/evaluation](https://raw.githubusercontent.com/dapr/dapr/v1.18.2/pkg/acl/acl.go). These sources establish both path-prefix matching and first-operation retention.

### Final resumed review triage (2026-10-16)

All three context-free layers reviewed `/tmp/story-1-7-review-2zmffirf.diff`, the complete 3,378,474-byte baseline diff including untracked files and owner changes. Each finding is classified separately below before grouping. `R3-B*` are Blind Hunter findings, `R3-E*` Edge Case Hunter findings, and `R3-V1` the pre-verified verification gap. The review includes independent EXT-SECRETS-1 and recovery commits already present when this run started.

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| R3-B1 | high | defer (carried) | carried R2-B8/R2-E1: the unchanged signing adapter still reaches effect-sign after a parsed non-Signed query; the independent EXT-SECRETS-1 commit owns this defect. |
| R3-B2 | high | defer (carried) | carried R2-B9: revocation authorization still checks only a subset of the complete requested envelope, in unchanged independent adapter code. |
| R3-B3 | high | defer (carried) | carried R2-B10: compromise request/receipt binding still omits both requested revisions; no Story 1.7 change touches this adapter. |
| R3-B4 | high | defer | Confirmed `DaprCompromiseRegistrarAdapter.Receipt` rejects zero batches, while EventStore `DeletionConsumptionActor.RegisterRevocationAsyncCoreAsync` durably records an empty affected set and the custody subscriber accepts it. This prevents that valid revocation from reaching the guard mirror. The defect belongs to independent EXT-SECRETS-1 commit 26628896b4fc7b70337c22b4ee34a4b6b0edc6d8. |
| R3-B5 | high | defer | `TransitionBody` omits `TargetBytes`; the existing `IAnchoredStateTransitionAuthority` admission/recovery contract explicitly requires retaining and verifying prospective bytes. Different byte arrays can produce identical authority requests. This is unchanged independent EXT-SECRETS-1 code. |
| R3-B6 | high | defer | `VerifyAuthorityBoundaryAsync` sends expected basis, signing-key version, decision ID, issuer and audience, but not the actual manifest approvers/issuance time or exact profile revision/anchor. Its interface requires a coherent current cohort/profile check; earlier individual reads do not implement it. Independent EXT-SECRETS-1 owns this path. |
| R3-B7 | high | defer | `DaprInventoryAuthorityAdapter.Echoes` and `DaprDecisionAuthorityAdapter.EchoAsync` compare request fields and then return true without requiring an affirmative fresh authority outcome. Matching fields plus an additional denial/revocation status can authorize a call. These methods were introduced by the independent EXT-SECRETS-1 commit. |
| R3-B8 | high | defer | `CapabilityIdentity.Absent` explicitly accepts an empty object; replay, compromise and transition callers then permit mutation without a typed, request-bound absence. This is a distinct defect in unchanged independent EXT-SECRETS-1 code. |
| R3-B9 | medium | defer (carried) | carried R2-E2: default buffered SendAsync and ReadAsStringAsync still precede proof-size validation; excessive memory use is established, literally unlimited buffering is not. |
| R3-B10 | medium | defer (carried) | carried R2-E5: the unchanged recovery `secret_path` filter omits id_ecdsa and PKCS#12 suffixes, including .pfx as well as .p12. The same previously deferred filename-filter defect owns all these examples. |
| R3-E1 | high | defer (carried) | carried R2-B8/R2-E1: independently reports the same unchanged signing-query fallthrough as R3-B1. |
| R3-E2 | high | defer (carried) | carried R2-B9: independently reports the same incomplete revocation-envelope checks as R3-B2. |
| R3-E3 | high | defer (carried) | carried R2-B10: independently reports the same omitted compromise revisions as R3-B3. |
| R3-E4 | medium | defer (carried) | carried R2-E2: independently reports the same buffered-response allocation as R3-B9. |
| R3-E5 | medium | defer (carried) | carried R2-E5: independently reports the same recovery private-filename omission as R3-B10. |
| R3-V1 | medium | patch | Pre-verified: Memories' real HTTP host tests check only default programmatic discovery; composition checks only the environment flag and the live fixture supplies its own 404. Extend the existing host test to cover default, explicit false and declarative true, asserting 404 for the latter. |

The sole new Story 1.7 patch is the actual Memories host HTTP assertion. Default and explicit false retain anonymous topic discovery; declarative true authenticates the probe to reach routing beyond the host's existing fallback authorization and proves 404. All seven middleware/host cases pass with current source-mapped Debug dependencies. The initial anonymous true-mode probe received the expected fallback 401; no production authentication behavior was changed. The five new independent EXT-SECRETS-1 defects have distinct root causes and are recorded separately in the deferred ledger. Carried findings retain their earlier routes without duplicate patches or ledger entries. No intent/specification loopback is required.

## Verification

- Build `Hexalith.Platform.slnx` and the Platform test project in Debug; run its direct xUnit executable and affected Builds/owner tests.
- Run `python3 scripts/test-reference-module-declarations.py` and `eng/verify-agents-host.sh --mode LocalScaffold`.
- Extend the existing gated Docker/Dapr source smokes to prove permitted invocation, denied caller, declarative delivery and teardown. Inspect package graph/security with verified fixtures; do not claim live package qualification while release/image prerequisites are absent.

Final post-review evidence (2026-10-15): Platform 134 total / 129 passed / 5 gated skips / zero failures; Builds affected classes 145 passed; EventStore Aspire 12 and Server 19 passed; helper gate 6 and historical Timesheets preparation 31 passed; 41 declaration checks passed. The final direct pinned-runtime security fixture passed 1/1, including all seven negative ACL checks, all four established Timesheets POST operations, scoped ingestion/dead-letter delivery and cleanup. The final real six-server Builds source smoke passed 1/1 in 101.015 s including declared readiness, owned teardown and workspace/scheduler-data removal. Solution/test-project builds, LocalScaffold, architecture lint, Python compilation and scoped whitespace checks passed. Earlier owner regressions and technical/file-entry smokes remain explicitly historical. See [retained commands, failed attempts, raw evidence and qualification limits](evidence/story-1-7/verification.md).

Review-patch evidence (2026-10-16): all four verification gaps below are covered. The focused matrix passed 12/12; the full Platform suite passed 142 total / 137 passed / five gated skips / zero failures. Solution/test-project Debug builds, 41 declaration checks, six helper-gate tests, LocalScaffold and architecture lint passed. These additions verify generated models, rendered environments and actual toolkit CLI resources without starting them; previous live enforcement/delivery and owner regression evidence remains historical. See [commands, logs and scope](evidence/story-1-7/review-patches-2026-10-16/verification.md).

### Review Findings

Final resumed verification (2026-10-16): Platform 148 total / 143 passed / five gated skips / zero failures; Builds 145, EventStore Aspire 12, EventStore Server 19 and the source-mapped Memories HTTP host seven passed. The fresh direct-runtime fixture passed, as did the technical signed source run (44.488 s) and final six-server signed source run (119.514 s), including readiness, owned teardown and workspace removal. All six selected owner prebuilds passed. Earlier startup attempts failed at varying resources and are retained; the successful warm retries do not qualify reliable cold startup. The new Memories verification gap is fixed, all 16 final review findings are triaged, and five new independent secrets follow-ups are deferred. See [current evidence, exact commands, retained failures and diagnostic boundaries](evidence/story-1-7/resumed-20261016T074246Z/verification.md).

Code review 2026-10-15 of `c3af1f2..cb122dd` (layers: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor; none failed).

- [x] [Review][Patch] Tenants and Memories receiver grants are asserted only by the gated live fixture — add unit assertions of the exact `accesscontrol.tenants.yaml` / `accesscontrol.memories*.yaml` grant sets (e.g. `eventstore` POST `/process`); the deny-by-default theory passes with `policies: []` (medium) [tests/Hexalith.Platform.Composition.Tests/PlatformDaprBindingsTests.cs:128]
- [x] [Review][Patch] `Dapr__DeclarativeSubscriptions=true` injection is untested — assert it in each server's rendered environment; `GatewaySeedSubscriptionTests` sets the flag by hand (medium) [src/Hexalith.Platform.Composition/PlatformServerBindings.cs:85]
- [x] [Review][Patch] Scheduler bind mount untested — assert a `Bind` mount at `<rendered>/scheduler-data` → `/var/lock`, and the named-volume fallback when the directory is null (low) [src/Hexalith.Platform.Composition/PlatformAppModel.cs:167]
- [x] [Review][Patch] `RunPlan.DaprHome` / `HEXALITH_DAPR_HOME` → `AddDapr` path and sidecar `RuntimePath` untested (low) [src/Hexalith.Platform.Composition/PlatformAppModel.cs:107]
- [x] [Review][Defer] Run-plan placement/scheduler ports are ignored; the mTLS control plane uses fixed `localhost:51005/51006` and Sentry `:50001`, so concurrent runs collide — deferred: already disclosed as out of scope (concurrent-run isolation)
- [x] [Review][Defer] Declarative-only subscription mode removes programmatic discovery for every server and the projection wildcard topic `*.*.projection-changed` delivery is unproven on Redis; an owner with attribute-only subscriptions and no explicit binding silently loses delivery — deferred: unverified medium; settle by enumerating owner `[Topic]` attributes against rendered subscriptions and a live wildcard delivery assertion
- [x] [Review][Defer] Reference ACL grants for `eventstore-admin`, `sample-blazor-ui`, `sample-api` were deleted and the stub templates are no longer read by `Render` — deferred: unverified medium; settle by confirming no selected topology still loads those callers

#### Rejected

- Timesheets template ignores `127.0.0.1:6379` / missing-file check removed (false): `timesheets-readmodels.yaml` uses `localhost:6379` only.
- EventStore host drops `TopicAttribute` options (false): the replaced attribute carries only pubsub name and topic.
- `projects["eventstore"]` KeyNotFound (false): the line is unchanged and the gateway is always a project resource.
- Pins script fails when the spine is absent (false): refusing an unrecorded hosting set is the specified gate.
- Timesheets history file untracked (false): present in the commit tree.
- Hosting-helper allowlist is a literal tuple, not the recorded set (low): owner edits both together; a fix adds parsing complexity.
- `Resolve` raw exceptions for duplicate `appId`, missing dead-letter target, unknown strategy or server scope (low): upstream Builds validation rejects these first; guards add branches.
- Live fixture hygiene — fixed sleep, hard-coded `deadLetterPhysicalTopic`, cleanup `check=True`, `/proc` portability, port race, argv/KeyError messages (low): test-only script, disclosed adaptation.
- Hard-coded `localhost:50001`/`public` literals in `Render` (low): matches the control-plane constants today.
- Orphaned ACL stub templates (low): removal touches mapping and tests for no user impact.
- Spec `status: done` vs sprint `review`, Aspire helper bypass, owner changes pending release (low/process-only): tracked by sprint status, not code defects.
