---
title: 'Platform custody-lifecycle-v1 foundation for Agents 5.4'
type: 'feature'
created: '2026-10-25'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: eecea3fa467d27226ef6173c19f853add36bb26c
context:
  - ../agents/_bmad-output/planning-artifacts/external-dependency-register.md
  - ../agents/_bmad-output/implementation-artifacts/story-5-4-dependency-reconciliation-2026-10-22.md
  - _bmad-output/planning-artifacts/architecture/architecture-ext-protection-1-custody-2026-10-18/ARCHITECTURE-SPINE.md
  - docs/implementation/ext-secrets-1-semantic-provider-2026-10-22/FINAL-VERIFICATION.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Continue the production custody-lifecycle-v1 foundation needed by Agents Story 5.4, reusing Platform custody work. Complete the adopted Dapr-only provider/component, actual key delivery, independent current/nonrollback authority, copy inventory, lifecycle operations and exact original receipt lookup. Return independently reviewed implementation, deployment artifacts, local validation and exact missing installation inputs.

Connect existing component internals through public authenticated adapters and deployment packaging. Distinguish source completion from missing installations, owning-service integrations and physical qualification.

## Boundaries & Constraints

**Always:** Applications use configured logical Dapr roles and provider-neutral contracts. Native SDKs, identifiers and credentials stay inside infrastructure. Require fresh authenticated current authority, independently enrolled genesis, retained immutable originals and allocation exclusions; local actor state never supplies authority. Preserve exact identity/AAD/KEK bytes, guard dispatch versus protection reservation ordering, all-or-none sorted manifests, survivor recovery, hold ordering, completion reserves and existing limits. Track actual key holders and copies before release; uncertain sends remain lookup-only. Key material is ephemeral, owned and cleared, never serialized into authorizer requests, originals, logs or evidence. Record credential references only.

Preserve the OpenBao ObservedRevival negative result, SHA-256 `6efa381bc70b3de57a24142e5e679f53781c7f89d5ab6740fb7664966f30888e`, with G1 unqualified. Wrappers, tombstones, missing keys, scheduled deletion and local doubles supply no irreversible-erasure proof. Retain historical evidence unchanged. Existing AWS infrastructure internals remain an uninstalled reference implementation, not an adopted mandatory backend.

**Never:** Provision/deploy, create/delete real keys, run destructive experiments, serve/worker against native installations, owner Live/Full commands, qualification/cohort or consuming Agents seams. Change dependency acceptance, integration dates, timing, Open export disposition or history policy. Edit sibling repositories, introduce application-native SDK access, self-issued approval evidence or production DI activation. Distinguish missing implementation from missing installation; references alone close neither.

## I/O & Edge-Case Matrix

| Given | When | Then |
| --- | --- | --- |
| Current enrolled authority and exact holder admission | Wrap/unwrap and complete a key use | Actual original key flows through Dapr; every copy remains tracked until independently verified completion. |
| Lost key response or duplicate delivery | Retry/lookup | Return value-free original status; never replay a release or regenerate a root. |
| Effective hold, stale proof, unavailable authority or substituted identity | Invoke lifecycle/release | Deny before effect; uncertainty remains Unknown. |
| Complete reserved multi-target manifest and survivor | Execute component lifecycle locally with doubles | One shared cut and complete ordered original, or no terminal destruction claim. |
| Retained terminal result | Recover pages after response loss | Verify exact bytes/signature/vector before exposing Consumed; corrupt/missing pages remain Unknown. |
| Application restore or missing genesis | Read current/admit transition | Independent challenge-bound authority governs; no bootstrap or restored rollback. |
| Missing actual installation/closure evidence | Render/preflight deployment | Name every missing input; produce no runnable fabricated installation or availability claim. |

</frozen-after-approval>

## Code Map

- `infrastructure/Hexalith.Platform.Custody.Dapr/{SemanticProvider,KeyExchange,RecoverableLifecycle,ClosureInventory,AwsAuthorityStore,CheckpointArchive,BindingService}.cs`: reuse native intents, whole-head CAS, B1–B5, copy closure and originals; repair gaps found during integration.
- `src/Hexalith.Platform.Secrets.Runtime/CustodyBinding{Contract,DaprInvoker,Originals,OwnerAdapter}.cs` and `CustodyOwnerAuthority.cs`: reuse exact wire validation, transport admission, signed currentness and paged assembly. Add public adapters without exposing internal native configuration.
- `CustodyBindingDaprCall.cs`: Build lacks authority; Dispatch discards outcomes. Replace production integration; retain fixture restrictions.
- `src/Hexalith.Platform.Protection/AtomicDeletionManifestBindingProvider.cs`: currently depends on the in-memory store/witness. Add an authenticated Dapr implementation of the existing manifest port.
- `CustodyKeyLifecycleBindingAuthority.cs`: replace snapshot authority in production.
- `docs/implementation/ext-secrets-1-semantic-provider-2026-10-22/`: historical handback, inputs and evidence; preserve bytes.

## Tasks & Acceptance

- [x] `src/Hexalith.Platform.Secrets.Runtime/CustodyLifecycleClient.cs` and related adapters: expose usable wrap/unwrap/completion, pin/unpin and original lookup; connect an authenticated Dapr authorizer client with exact body binding and no local signing fallback.
- [x] `src/Hexalith.Platform.Protection/DaprAtomicDeletionManifestProvider.cs`: implement existing manifest port using complete signed request, fresh authority and authenticated full-original assembly, preserving Unknown on loss.
- [x] `infrastructure/Hexalith.Platform.Custody.Dapr/`: repair integration defects in authority, inventory, lifecycle and original recovery while preserving existing physical gates.
- [x] `eng/ext-secrets-1/custody-deployment/`: add component container/build, Dapr registration, protected socket/workload/worker templates and offline input preflight. Require pinned artifacts, scoped credential references, independent authority placement and exact profiles; never apply.
- [x] `infrastructure/Hexalith.Platform.Custody.Dapr.Tests/` and Runtime/Protection tests: exercise public adapter through actual SDK/service boundary; test matrix failures, multi-target lifecycle, loss, key cleanup and signed original recovery.
- [x] `docs/implementation/custody-lifecycle-foundation-2026-10-25/`: retain local command results, review findings, source/artifact hashes and installation-input ledger with owner/action per gap; distinguish source, installation and physical-proof status.

Acceptance: Given the existing foundation, when local integration tests pass and independent review findings are resolved, then usable Dapr-only production adapters and deployment artifacts are reviewable with exact remaining blockers. Given absent physical installations/proof, when reporting completion, then no installed-provider, irreversible-erasure or dependency-availability claim is made.

## Implementation Notes

No irreversible operation is required. Give absent authorizer/control/observer implementations, EventStore integration and enrollments explicit dispositions; absent source is not configuration.

Implementation handoff verified against the custody-scoped since-baseline diff and retained command outputs: 20 component scenarios, 118 targeted Runtime tests, 5 Protection tests and 5 offline checks passed. All seven frozen matrix rows have executed passing coverage, recorded in the delivery's FINAL-VERIFICATION.md. Release source-graph publish and a 41-file hash-checked image context passed. Source-package failures and the initial context refusal remain retained. No image build, native operation or qualification ran. The full since-baseline diff also contains unrelated concurrent Story 2.5/schema/submodule work; it is preserved, with a separate custody review diff. The independently committed owner bridge is included as dependency context, not attributed to this implementation.

## Spec Change Log

## Review Triage Log

All three independent layers reviewed custody diff SHA-256 `bec84e415e7c1d8ff6513cbea2441b678c22bc5915cfb16c4e11ad9afb68b885`. Full baseline evidence is retained separately. Every finding is classified below before grouping; all confirmed defects preserve the frozen intent and require no new application contract or policy. Route: patch.

| ID / layer | Verdict / route | Verified evidence |
| --- | --- | --- |
| B1 blind | high / patch | Actor ApplyCore compares ExpectedRevision to its own ledger; binding validation also compares that field to the independently advancing custody head. An ordinary owner save makes valid actor pin/unpin impossible. Separate the existing revision domains without rewriting original request bytes. |
| B2 blind | medium / patch | Actor RetainAsync accepts an authenticated terminal NotPerformed to remove a pending pin; the wire validator supports it, but the new authority's status set excludes it. Accept only exact independently retained NotPerformed originals; do not synthesize absence. |
| B3 blind | medium / patch | Broad catches in the new client/authorizer/provider convert explicit cancellation to refusal/Unknown. Lower invokers can also return a refusal after cancellation. Preserve caller cancellation and clear discarded key results. |
| B4 blind | medium / patch | Manifest assembly restarts a 30-second budget after the initial call; per-key lookup plus a locator can extend it further or wait indefinitely. Keep one bounded deadline across all stages. |
| B5 blind | high / patch | The manual Kubernetes daprd has neither mounted identity/trust bootstrap nor enabled mTLS/control-plane wiring, while service-account automount is disabled. The template cannot establish its claimed authenticated service boundary. Wire explicit scoped references and required bootstrap inputs. |
| B6 blind | medium / patch | The manual daprd omits app port/protocol, so the documented remote custody API has no application channel. Wire the actual receiving host inputs. |
| B7 blind | medium / patch | WorkerEntry.RetainPreparedOriginalAsync creates the admitted packet's output file; the worker mounts a PVC as a non-root user without volume group ownership. Ordinary root-owned output storage can block original preparation. Match the configured worker group with fsGroup. |
| B8 blind | medium / patch | Both Python input loaders accept duplicate properties with last-value semantics, contradicting exact closed inputs and inventory identity. Reject duplicates recursively. |
| B9 blind | medium / patch | The prepared context authenticates publish files but leaves the Docker runtime image as an unrestricted external ARG. Bind a digest-pinned runtime image into the reviewed context/recipe. |
| B10 blind | medium / patch | Required verification logs are ignored and absent from the review diff. Retain the specific custody logs in the repository bundle so a checkout can verify SHA256SUMS. |
| E1 edge | high / patch | Independently confirms B1: wrap and owner-anchor commits advance custody independently from the actor ledger. |
| E2 edge | medium / patch | Independently confirms explicit authorizer cancellation is swallowed. Same underlying cancellation defect as B3. |
| E3 edge | medium / patch | Independently confirms the missing app-channel port in B6. |
| V1 verification gap | medium / patch | Pre-verified regression gap: existing tests reject a corrupt signature but never replay an earlier valid signed unwrap response against a fresh attempt/challenge. Add that exact negative case. |
| V2 verification other | high / patch | Independently confirms the actor/custody revision mismatch; direct tests set the lifecycle revision from the custody head and hide it. |
| R1 parent | high / patch | The same revision conflation affects another root: KeyExchange.Wrap and wire validation require the registration revision to equal custody head, but RegisterWrappedAsync requires actor ledger revision. Cover two registrations and actor-driven pin/unpin with diverged heads. |
| R2 parent | medium / patch | New authority/provider constructors additionally require ICustodyKeyRegistrationSource and ICustodyReservedManifestSource. The missing-source ledger does not explicitly name these concrete dependencies. Record their source/installation owners and exact required retained mappings. |
| R3 parent | medium / patch | Component/worker secrets are mounted at /credentials but the native clients use the SDK default credential chain, with no mapping to that mount. Explicitly bind the scoped reference-backend credential file/profile and disable unintended metadata fallback inside infrastructure. |
| R4 parent | medium / patch | New lifecycle-authority forwarding methods can receive false from an owner adapter that swallowed explicit cancellation. Apply the same post-await caller check as B3 to that wrapper. |
| R5 parent | medium / patch | Manual daprd lacks injector registration and a discovery Service, so wiring an app port alone cannot make the documented remote app ID resolvable. Supply the internal-gRPC Service and matching explicit port/selector; do not expose local Dapr APIs. |

Grouping after individual verdicts: B1/E1/V2/R1; B3/E2/R4; B6/E3; remaining findings separately. No finding is discarded or deferred. The corrections use existing identities, revisions, credential-reference inputs and safety boundaries; they add no new application capability. Independent role implementations and physical proof remain explicitly missing.

First repair follow-up: verification reviewer confirmed both filed gaps closed. Blind and edge reviewers confirmed revision, NotPerformed, replay, cancellation refusal handling and deployment repairs, with these separately classified remaining findings:

| ID / layer | Verdict / route | Verified evidence |
| --- | --- | --- |
| FB4 blind follow-up | medium / patch | The authority's registration/owner forwarders still wait only on the supplied caller token; default-token calls can hang, or registration delay can precede a fresh lifecycle budget. Apply the existing whole-operation budget to the authority too, with a shared downstream token and bounded waits. |
| FBDoc blind follow-up | low / patch | Deployment README's primary recipe still uses the superseded flat inventory and omits the now-required runtime-image argument. Correct that existing command/schema directly. |
| FE2 edge follow-up | medium / patch | Bare cancellation rethrows in direct authorizer/authority wrappers retain a callee's foreign linked token when both tokens are canceled. Normalize to the original caller token and exercise an already-canceled foreign-token task. |

All follow-up layers returned before this triage. No new intent, public wire field, physical authority or approval is needed. Parent full checks before these final corrections passed 23 component, 121 Runtime, 5 Protection and 10 offline checks, with no drift across 2,575 source files; those exact intermediate results remain retained.

Final closure: blind reviewer confirmed the remaining authority deadline and primary command fixes; edge reviewer confirmed foreign-token cancellation normalization; verification reviewer confirmed both original regression gaps and scenario registration. All 23 individually recorded observations are resolved, with nothing deferred. The parent rebuilt and executed the final source: 23 component scenarios, 128 targeted Runtime tests, 5 Protection tests and 10 offline tests passed, with zero failures/skips/unrun and zero build warnings/errors. All 2,575 captured source files stayed unchanged during that run. Release publish produced 41 files; actual image-context preparation correctly remains Blocked without a reviewed runtime-image pin. OpenBao ObservedRevival bytes remain unchanged. The reviewed delivery and exact source/install/physical blockers are in `docs/implementation/custody-lifecycle-foundation-2026-10-25/REVIEWED-DELIVERY.md`. Completion denotes this reviewed source/deployment-preparation foundation, not a provider installation, missing owning-service implementation, physical qualification, accepted dependency target or resumed Agents story.

## Verification

Build/run component, targeted Runtime and Protection suites in fresh external artifact directories. Cover every matrix row, including signed refusals. Run offline deployment validation, scoped diff checks and negative-evidence checksum verification. Doubles establish code behavior only.
