# D1 — recommended closed public serialization

Revision `PLAT-ES416-QH-Q2-R2`. **PROPOSED; owner acceptance pending.** This is a schema-authoring table, not a published schema or implemented API. All new spelling, limits and enums below are recommendations. The adopted v2 sketch remains historical input; these tables make its missing choices explicit. Accepted v1/policy semantics and Platform planning are distinct from acceptance of these recommendations.

## Grammar and applicability

`I` = v1 identity (1–2,048 characters, no outer whitespace/C0/C1); `H` = lowercase `sha256:` + 64 hex; `S` = complete 40/64 hex revision; `T` = strict v1 UTC; `N` = plain Int32, 0–2,147,483,647; `N+` = 1–2,147,483,647. No normalization, null, implicit defaults or unknown/wrong-case keys. `E(a,b)` is a finite string enum. `L<X>[a,b]` is an ordered array with inclusive cardinality. IDs are unique ordinally in their stated scope; sets additionally forbid duplicate values. Public input stays at most 1,048,576 bytes (optional UTF-8 BOM included), 64 levels, strict parsing and bounded regular-file I/O as v1.

`U` is exactly `{"state":"not-yet-populated"}`. `P<X>` is exactly the members of closed object X plus `state:"populated"`. `A<X>` is `U` or exactly `{"state":"populated","items":L<X>[0,4096]}`. U is permitted only where a table says U. An observed empty array is not unavailable and supplies no required observation. A table's field list is the complete allowed and required set unless `?` explicitly marks an optional member. These rules apply recursively; no extensible metadata bag exists.

| Object/path | Exact members or inherited definition | Availability / cross-check |
| --- | --- | --- |
| Descriptor | `schema` constant `hexalith.test-environment.v2`; `policyId`, `bindings`, `diagnostics` exactly the v1 descriptor definitions except bindings below | No other top-level members. |
| Result | `schema` constant `hexalith.test-result.v2`; all other top-level members and terminal/termination/retention/cleanup definitions exactly the v1 result | No descriptor/result mixed pair; first terminal outcome unchanged. |
| `bindings` | Every v1 binding and nested definition unchanged, plus required `qualification` | No in-place v1 schema edit; v2 is selected only by explicit qualification selection (D3). |
| `bindings.qualification` | U or `Qualification` | U allowed only for early/partial-startup snapshots when producer absent; never launch/credit. Ordinary suites select v1, not a v2 U placeholder. |
| `Qualification` | `scope:Scope`, `profile:Profile`, `hosts:L<Host>[2,3]`, `operations:Operations`, `controls:L<Control>[1,32]`, `observations:Observations` | First five members immutable; every observation is append-only under D5. |
| `Scope` | `runId:I`, `ownerRunId:I`, `attemptId:I`, `environmentId:I`, `syntheticTenantId:I`, `dataIsolationId:I`, `profileContentIdentity:H`, `suiteId:I`, `suiteContentIdentity:H`, `configurationContentIdentity:H`, `requiredTestSetContentIdentity:H` | Equal corresponding existing run/isolation/suite and D2 profile identity; attached producer run differs from owner exactly as v1. |
| `Reference` | `referenceId:I`, `contentIdentity:H` | Opaque owner record/artifact, not fetched by offline reader or self-authenticating. |
| `EndpointRef` | `resourceId:I`, `endpointName:I` | Resolve exactly once within owned `environment.resources`; endpoint module/protocol checked. U resources mean no launch, not fabricated endpoints. |
| `Partition` | `syntheticTenantId:I`, `domain:I`, `aggregateId:I` | Exact owner-issued partition; tenant equals Scope. No provider/private actor key. |
| `ArtifactIdentity` | `version:I`, `contentIdentity:H` | Expected and actual separate. |
| `ToolIdentity` | `name:I`, `version:I`, `contentIdentity:H` | Actual tool equals existing populated `bindings.tool`; independent qualifications stay there. |

## Profile and immutable declarations

| Closed type | Required fields | Availability / bounds / references |
| --- | --- | --- |
| `Profile` | `purpose:E(candidate,canonical)`, `definition:Definition`, `supportReference:U or P<Support>`, `platformSourceRevision:S`, `catalog:Catalog`, `composition:Definition`, `tool:ToolIdentity`, `expected:Expected` | Definition digest equals Scope profile; known catalog/composition equal existing bindings. Actual executing tool cannot be a copied pin. |
| `Definition` | `id:I`, `contentIdentity:H` | Exact D2 artifact or existing composition bytes, by subject. |
| `Catalog` | `commit:S`, `instanceId:I` | Existing run catalog, no synthetic substitute. |
| `Support` | `referenceId:I`, `releaseContentIdentity:H`, `capabilityVersion:I` | Owner's supported-release record; U blocks eligibility. An example or proposed receipt grants no support. |
| `Expected` | `moduleArtifacts` (unchanged v1 `artifacts.expected` definition), `sdk:ArtifactIdentity`, `daprCli:ArtifactIdentity`, `runtime:ArtifactIdentity`, `embeddedComponent:Embedded`, `component:Component`, `backend:Backend`, `securityConfigurationContentIdentity:H`, `placementConfigurationContentIdentity:H`, `actorContractVersion:I`, `capabilityVersion:I`, `requiredCapabilities:L<I>[1,64]`, `verifierInputs:U or P<VerifierInputs>`, `writerAliasInventory:U or P<InventoryRef>` | Required capabilities unique; moduleArtifacts may be U by v1, never launch/credit. All new artifact digests resolve to exact producer bytes; no credential value in any input. |
| `Embedded` | `sourceRevision:S`, `contentIdentity:H` | Runtime-embedded component source and binary identity remain distinguishable. |
| `Component` | `type:I`, `apiVersion:I`, `configurationContentIdentity:H` | Type and API version both mandatory. |
| `Backend` | `image:I`, `configurationContentIdentity:H` | Image must end in `@sha256:` plus 64 lowercase hex; no connection URI/userinfo/secret. |
| `VerifierInputs` | `contractId:I`, `contentIdentity:H`, `receiverId:I`, `secretBindings:L<SecretBinding>[1,32]` | Qualified value-free receiver contract and generations; secret reference IDs unique; U blocks lane. No custody/distribution authority. |
| `SecretBinding` | `referenceId:I`, `generationId:I` | Opaque value-free references only; existing qualified custody boundary supplies meaning. |
| `InventoryRef` | `artifactId:I`, `contentIdentity:H` | D2 complete EventStore 3.17 inventory, not an inferred subset. |
| `Host` | `hostId:I`, `resourceId:I`, `logicalActorAppId:I`, `actorType:I`, `roles:L<E(writer-a,writer-b,reader)>[1,2]`, `httpEndpointRef:EndpointRef`, `grpcEndpointRef:EndpointRef` | Host/resource IDs unique; exactly one writer-a and writer-b on different resources, at least one reader, roles unique. All hosts share logicalActorAppId/actorType and the declared partition. Both transports required here; unexposed required transport remains blocked. |
| `Operations` | `lawfulOperationId:I`, `logicalReadRouteId:I`, `ownerReceipt:U or P<Reference>`, `partition:Partition`, `barrierIds:L<I>[1,32]`, `requiredTestSetContentIdentity:H` | Route/operation resolve to EventStore-owned operation-contract bytes in D2 configuration; barrier IDs unique, test-set equals Scope. OwnerReceipt U blocks launch. |
| `Control` | `controlId:I`, `operationId` and `version` below, `endpointRef:EndpointRef`, `scope:Scope`, `hostIds:L<I>[1,3]`, `partition:Partition`, `phase` below, `expiresAt:T`, `maximumUses:N+` (max 32), `reset:Reset`, `cleanupObligation` constant `reset-and-expire-access` | Control IDs unique; hosts unique and declared, scope/partition equal Qualification, endpoint owned. Deadline within original run/access bounds. |
| `Reset` | `required` constant true, `operationId` constant `restore-owned-state`, `version` constant `1`, `deadline:T` | No module reset credential. Deadline no later than original finite owner stopping/cleanup bound; D4/D5 separate reset from admission/access. |

Recommended finite operations, all version `1`; these names describe future owner handlers, not available endpoints. A selected plan declares only applicable controls; missing a required control blocks that lane. Physical execution remains Q4/Q5.

| `operationId` | Only accepted `phase` | Target and reset obligation |
| --- | --- | --- |
| `after-read-barrier` | `after-original-read` | Exact EventStore barrier/batch; release/reset owns that barrier. |
| `placement-disconnect` | `after-original-read` | Declared writer's placement connectivity; usable persistence path must remain. |
| `stale-host-resume` | `after-peer-commit` | Same declared writer/partition; no new activation-fencing guarantee. |
| `commit-ack-suppress` | `after-durable-commit` | Exact original batch acknowledgement; Unknown requires fresh readback. |
| `isolation-negative` | `before-isolation-attempt` | Declared run-owned foreign principal/target only, with lawful control. |
| `erasure-interrupt` | `during-authorized-erasure` | Existing authorized bounded erasure operation; no key issuance/deletion bypass. |

## Advancing observation shapes

| Closed type | Required fields | Availability / references |
| --- | --- | --- |
| `Observations` | `observedAt:T`, `executingTool:U or P<ToolIdentity>`, `executingDaprCli:U or P<ArtifactIdentity>`, `loadedIdentities:A<Loaded>`, `capabilityNegotiation:A<Capability>`, `verifierReadiness:A<VerifierReady>`, `hostInstances:A<HostGeneration>`, `placement:A<Placement>`, `barriers:A<Barrier>`, `freshness:A<Freshness>`, `readbacks:A<Readback>`, `pathCoverage:A<PathCoverage>`, `controlEvents:A<ControlEvent>` | All required, no omission-as-unavailable. Type details below/D5; latest observedAt never precedes an enclosed event. |
| `HostGeneration` | `observationId:I`, `hostId:I`, `resourceId:I`, `generationId:I`, `processIdentity:I`, `sidecarIdentity:I`, `cacheGenerationId:I`, `observedAt:T`, `diagnosticIds:L<I>[0,32]` | Host/resource match declaration; the (hostId,generationId,cacheGenerationId) tuple is unique; process/sidecar identities cannot change within a host generation, and distinct active writers have different process/sidecar/cache IDs. Restart changes host generation/cache and records the actual new process or sidecar identity plus freshly observed retained identities; evidence is never copied from the old generation. Cache-clear may change cache generation alone via Freshness. |
| `Loaded` | `observationId:I`, `hostId:I`, `generationId:I`, `cacheGenerationId:I`, `moduleArtifacts` (populated v1 artifacts.loaded shape), `sdk:ArtifactIdentity`, `runtime:ArtifactIdentity`, `embeddedComponent:Embedded`, `component:Component`, `backend:Backend`, `observedAt:T`, `diagnosticIds:L<I>[0,32]` | Resolve observed host generation, all consumed artifacts and expected identities; process-local identity observation never borrowed by a new generation. |
| `Capability` | `observationId:I`, `version:I`, `supportReference:Reference`, `capabilities:L<I>[1,64]`, `hostGenerationObservationIds:L<I>[2,3]`, `observedAt:T`, `diagnosticIds:L<I>[0,32]` | All required capabilities and both active writers covered; strings/negotiation alone grant no semantic proof. |
| `VerifierReady` | `observationId:I`, `receiverId:I`, `contractContentIdentity:H`, `secretBindings:L<SecretBinding>[1,32]`, `status:E(ready,failed)`, `observedAt:T`, `diagnosticIds:L<I>[0,32]` | Exact qualified expected inputs; failed/U never ready; no key value. |
| `Placement` | `observationId:I`, `hostId:I`, `generationId:I`, `membershipId:I`, `status:E(joined,disconnected,rebalanced)`, `observedAt:T`, `diagnosticIds:L<I>[0,32]` | Attributable actual membership event; not a sleep/startup inference. |
| `Barrier`, `Freshness`, `Readback`, `PathCoverage`, `ControlEvent` | Exact closed tables in [observation-report-rules.md](observation-report-rules.md) | Those tables are part of D1; no arbitrary payload property. |

Observation IDs are unique across all observation collections, including controlEvents. References target earlier or same-document records of the exact permitted type; chronological causality is enforced under D5. Every diagnostic ID resolves to existing top-level classified/redacted diagnostics. Opaque external references require their owner's later authentication; parsing never fetches them.

## Acceptance still needed

D1 recommendation is concrete; owner acceptance is **pending**. Complete valid/invalid synthetic *full* descriptor/result closure fixtures against these tables and the unchanged v1 envelope remain pending owner review work; historical fragments remain deliberately invalid full documents. No v2 schema or executable fixture is produced now. Owners must accept the exact tables and closure examples in one final hashed revision before the implementation gate can pass; an edited table/example requires another revision and receipts.
