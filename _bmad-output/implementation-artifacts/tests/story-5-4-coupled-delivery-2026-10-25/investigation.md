# Coupled Platform delivery investigation — 2026-10-25

Planning investigation only. Source baseline from `git rev-parse HEAD`: `1b7a00381794bb7ecf40caf643c63c50758a81eb`; branch `main`, initially clean. No Platform AGENTS.md or ancestor AGENTS.md was found. Independent investigation agents inspected secrets, host and protection. No implementation, deployment, qualification, native effect, attempt reservation or journal mutation occurred.

The Agents dependency register and October 22 reconciliation were read as the governing contract. Both accepted Platform records remain Committed at `abbec4093715c1db854433119b32d4e16df20d08`, date 2026-10-08. They are not Available. The October 25 source does not replace those accepted records.

## Source findings

| Area | Exact source | Finding and required implementation |
| --- | --- | --- |
| Joint runner | `eng/ext-secrets-1/qualify.py::cohort_prerequisite_inputs` | Unconditionally appends `OQ-5.4-1.JointLivePhaseRunner`. Implement fixed phases 0–3, prerequisite validation before reservation, complete accepted-set identity, retained phase receipts and exact recovery. Do not merely remove the blocker. |
| Qualification roles | `src/Hexalith.Platform.Secrets.Qualification.Executor/Program.cs`, corresponding Observer/Enrollment programs | No concrete caller authenticator, role provider, original store or public verifier is registered. Reuse bounded `QualificationHost`/`QualificationHttpHost`; implement each receiving provider and authenticate independent caller scopes. |
| Current authority | `DaprInventoryAuthorityAdapter.cs`, `DaprDecisionAuthorityAdapter.cs`, `DaprCapabilityAdapters.cs` in Runtime | Clients exist; they do not install owner services. Supply independently current nonrollback inventory, role/history authority, revocation and exact owner operations. |
| Signing | `PrivateCustodyBindingAuthority.cs`, `DeletionBatchCapabilityCodec.cs` | Supplied-key ES256 helper exists; captured current flag and in-memory originals are not independent current production state. Reuse codec, implement durable original signing outcomes/current authorization/published trust and revocation. |
| Private replay | `SecretsRuntimeServiceCollectionExtensions.AddPrivateReplay`, `PrivateReplayCredentialBinding.cs`, `DaprReplayRegistrarAdapter` | Private constructor capture exists, but revision is captured as 1 and no receiving production query/effect registrar was found. Need exact authenticated durable admission and current credential authority. |
| Spool | `ReplicatedSecurityObservationSpool.cs`, `ReplicatedSecurityObservationSpoolWorker.cs`, `PrivateReplicatedSpoolAuthority.cs` | Dapr CAS spool and drain primitives exist. Only concrete authority uses process dictionaries for head/proofs/grants. Implement independent durable current/nonrollback authority and actual recorder/recovery host. |
| Host | `src/Hexalith.Platform.Composition/AgentsHostComposition.cs` | Development-only `TryCompose` supplies null capabilities. Opaque child environment references and AppHost DI registrations do not configure services in child processes. Bind in actual owning-process startup. |
| FR-34 | Custody `Fr34ProtectionGate`, startup/readiness classes and `IFr34ProtectionCanaryEngine`/`IFr34PersistedCanaryReader`/`IFr34CanaryAuthority` | Gate scaffolding exists without concrete installed engine/persisted observer/current authority or production registration. Missing/no-op/self-reporting wrappers must remain denied. |
| Full verifier | `eng/ext-host-1/verify_full.py` | Thirteen component Passed cells plus persisted digest can pass. It explicitly keeps dependencyAvailable=false, productionEngineRegistered=false, liveEffects=0. Preserve this evidence classification. |
| Dates | `AgentsDeploymentProfile.cs`, host `prepare_acceptance.py`, secrets `qualify.py::acceptance` | Missed 2026-10-08 is enforced literally. Replace with exact newly accepted date/hash validation without changing host, Branch B or timing. |

The 14 host slots are currentTenant, globalAdministrator, humanIdentity, approverEvidence, replay, recorder, worker, spoolBackend, failureModel, protectionEngine, protectionObserver, partiesBinding, conversationsBinding and replicaCount. Existing five service paths point at checkouts, not independently verified installations.

## Newer custody and protection source

`docs/implementation/custody-lifecycle-foundation-2026-10-25/REVIEWED-DELIVERY.md` records implemented Dapr lifecycle clients, ephemeral key delivery, holder completion, exact-original recovery, lifecycle/manifest providers and owner-currentness bridge. Its component results are retained historical evidence, not newly run checks.

Reuse `CustodyLifecycleClient`, `DaprCustodyAuthorizerClient`, `CustodyOwnerAuthority`, `DaprAtomicDeletionManifestProvider` and `CustodyDeletionConsumptionOwnerAuthority`. Do not regress to the older `CustodyBindingDaprCall.DispatchAsync`, which builds without authority and refuses.

`installation-input-ledger-after-review.json` lists 68 unique missing installation inputs. `source-dispositions.json` separately identifies missing implementations/installations of private authorizer, durable hold/dispatch/protection-reservation control issuer, independent physical closure observer, receiving custody API/composition, complete key-registration original locator, complete reserved-manifest original locator and accepted EventStore source/package integration. A credential string cannot fill a missing service implementation.

Registration lookup must retain the complete original registration, actor revision, KEK version, opaque root/wrap reference and receipt. Manifest lookup must retain the exact ConsumptionReserved-to-complete-signed-request mapping with guard/dispatch facts and sorted vector; no reconstruction, singleton derivation or re-signing.

`../eventstore/docs/implementation/ext-protection-1-delivery-2026-10-25.md` still reports BlockedNotSubmitted, G1–G7 open, no accepted installed profile. The installed package graph lacks the accepted integration; local source builds do not supply its immutable installation. 2026-11-02 remains an unaccepted owner proposal.

## Public installation observations

Repository configuration inspection confirms null executor, observer and enrollment publicAnchorFile values in `eng/ext-secrets-1/runtime-reference.example.json`. No actual hashes exist for those null fields. Local metadata checks confirm these absent files:

- `/home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json`
- `/home/administrator/hexalith-conversations-evidence/ext-conv-ai-1/live-manifest.json`

A delegated investigator ran the following three read-only metadata commands from the Platform checkout on 2026-10-25. Each exited 0. No context override was supplied; context name and exact execution time were not captured. Results establish only inspected namespace state, not global resource absence or the cluster-node mapping to host 192.168.1.30. Secret contents were not read.

```bash
kubectl --kubeconfig /home/administrator/.kube/hexalith-production --request-timeout=20s get deployments,statefulsets -n ext-secrets-1-isolated -o 'custom-columns=KIND:.kind,NAME:.metadata.name,REPLICAS:.spec.replicas,READY:.status.readyReplicas,IMAGE:.spec.template.spec.containers[*].image'
kubectl --kubeconfig /home/administrator/.kube/hexalith-production --request-timeout=20s get components -n ext-secrets-1-isolated -o 'custom-columns=NAME:.metadata.name,TYPE:.spec.type,SCOPES:.scopes'
kubectl --kubeconfig /home/administrator/.kube/hexalith-production --request-timeout=20s get statefulsets -n openbao -o 'custom-columns=NAME:.metadata.name,REPLICAS:.spec.replicas,READY:.status.readyReplicas,IMAGE:.spec.template.spec.containers[*].image'
```

Observed deployments eventstore, inventory, registry-idp and tenants were each 1/1 ready; registry-idp-db and registry-state StatefulSets were 1/1. No custody, authorizer, observer or enrollment deployment appeared. Components were configstore, eventstore-configstore, eventstore-pubsub, eventstore-statestore, ext-secrets-1-hmac, pubsub and statestore. ext-secrets-1-hmac is `secretstores.hashicorp.vault` scoped to `[ext-secrets-1-custody]`; no lifecycle binding appeared. OpenBao hexalith-keys remained 3/3 at `quay.io/openbao/openbao:2.6.2@sha256:11fd73a2102cda9c55d5d881a8c3210303146a7ec1e8ac76f526e175c6d24641`. This image observation is not custody qualification or an installed protection-engine hash.

## Inputs that source cannot decide

The adopted architecture requires independent authority/enrollment/trust and recovery outside the complete application restore cut. Repository templates do not select an installed qualifying backend, account/region/resources, independent authority/archive failure domains, administrator/deputy or enrolled trust bundle. AWS internals are an uninstalled reference, not an adopted mandatory backend. Same-host replicas and locally generated trust statements cannot meet these obligations.

Need either the existing independently administered installation/enrollment bundle, or the chosen backend/resources and independent administrative/recovery placement for provisioning. Public P-256 anchors, signatures and opaque credential references suffice; never request private-key values in chat. Required installation proof includes physical non-revival, survivor recovery, complete holder/copy/transport closure and current revocation; fixture assertions cannot create it.

Replacement integration dates are owner promises, not implementation constants to guess. The missed October 8 date and unaccepted November 2 proposal cannot silently become fresh accepted delivery dates.

## Scope and execution boundary

This is one coupled delivery goal: both records must share a target, installation and joint qualification protocol. No goal was split or deferred. Footprint spans the above source, role executables, verifiers, deployment artifacts, tests and detached packets. Deployment, trust enrollment and durable writes have operational side effects; physical deletion/expiry is irreversible and no new destructive probe is included in this delivery verification.

The drafted spec remains draft pending its independent installation and date questions. It does not describe implemented or installed work. The requested complete delivery remains outstanding. No complete replacement packet was emitted, no commitment/status changed and no test pass was used to claim availability. OQ-5.4-1, its remaining attempt, original journals and all historical evidence remain preserved.
