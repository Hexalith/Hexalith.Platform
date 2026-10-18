# Parties Platform prerequisite decisions

EXT-PARTIES-1 remains **Committed**, with implementation and installed qualification blocked on unresolved Platform references. No Parties staging or production deployment was established in the configured cluster or inspected owner records. The [installation plan](installation-plan.md) identifies the owning prerequisite stories and resource boundaries. The [qualification plan](qualification-plan.md) defines the implementation and evidence required after owners resolve those references.

This is an unsigned decision packet for Platform, Parties, EventStore, Tenants and recovery owners. **Owner acceptance, installation authority, Live execution and Available are all false.** The [completion form](decision-packet.json) uses `null` for unknown references, principals, command bindings and dates. A role requested to decide something is not an assigned operator or a granted capability.

## Deployment findings

The fresh metadata observation at `2026-10-18T07:18:30.339441+00:00` returned exit 0 for all eight native reads: server version, namespaces, workloads, services/routes, Dapr components, application enrollment, backup metadata and API resource discovery. It found 24 namespaces, 219 workload objects, 74 service/route objects, 14 Dapr components, no application enrollments and no Parties name matches. Gateway API Gateway/HTTPRoute resources were not advertised. [Exact commands and sanitized results](native-metadata.json) accompany the supplied [discovery record](sources.json).

Both `/home/administrator/.kube/config` and `/home/administrator/.kube/hexalith-production` identify `jpiquot@local`, API `https://192.168.1.30:6443`; they do not identify two environments. The server reports `v1.34.9`. Namespace `kube-system` UID `b5860674-1d1e-4a3b-b413-e1bb3b9a4557` is a cluster binding witness, not lifecycle authority. The historical configuration has no contexts. Neither this native scope nor the GitHub metadata below rules out a deployment under an undisclosed context, outside Kubernetes or under unrelated names.

| Required binding | Actual reference established | Owning prerequisite |
| --- | --- | --- |
| Parties staging release, workload UIDs, image digests, environment instance and application/data namespaces | None. Operator records name desired `hexalith-staging` and `hexalith-staging-data`; neither is an observed installed target. | 4.8, 4.9, 4.14–4.22 |
| Parties production release and the same complete binding | None. Production namespace and instance names remain unset. | 6.7, 6.11, 6.23, production enrollment/release gates |
| Authenticated EventStore gateway URL, route/service, TLS certificate and issuer per environment | None. A shared Traefik installation is not a Parties gateway. | 4.18; 6.7 |
| Authoritative Tenants registry deployment/issuer and two approved enrolled tenants | None. No Parties qualification tenant IDs were selected. | Tenants owner; 4.8, 4.19, 6.11 |

EventStore and Tenants workloads under `ext-secrets-1-isolated` are excluded. No fixture, local host, isolated secret registry or generic infrastructure resource substitutes for a Parties environment. Generic `openbao/hexalith-keys` UID `df2bbbbe-1fe8-4e0b-a8a6-174bd3287806` and `velero/scaleway-kube-green` UID `ece66776-b06a-4889-8a8c-d54260f47825`, bucket `hexalith-velero-backups`, establish only infrastructure presence.

GitHub exposes a Parties `production` environment (ID `19131558330`) and ten deployment records. All associated jobs are named `release` or `release / release`; their environment URLs are empty. Latest deployment `7119924400` records success for source `1ff55d5a79ac90de515de72e48371c2d6f95b72a`, but `production_environment` is false. Its [release job](https://github.com/Hexalith/Hexalith.Parties/actions/runs/38906617412/job/116991739201) uses the immutable [release workflow](https://github.com/Hexalith/Hexalith.Parties/blob/1ff55d5a79ac90de515de72e48371c2d6f95b72a/.github/workflows/release.yml), whose `production` job publishes packages/containers through semantic-release. These are release records without an installed runtime binding. Platform's GitHub deployment/environment lists are empty. [All 25 successful metadata reads](github-metadata.json) retain the distinction; a GitHub environment label supplies no gateway, tenancy or custody authority.

## Credential decisions

No scoped Parties credential reference or renewal owner was established. The form requests separate staging and production secret-store, namespace/mount, policy/role, issuer, audience, workload/subject, expiry metadata, renewal/revocation procedure and owner references for eleven roles: provisioner, trusted identity writer, reader, independent persisted-state observer, custody provider, lifecycle/fence writer, cleanup/destruction operator, backup operator, restore operator, fault injector and credential renewer.

The three existing Live inputs are `EXT_PARTIES_PROVISIONER_CREDENTIAL`, `EXT_PARTIES_IDENTITY_WRITER_CREDENTIAL` and `EXT_PARTIES_READER_CREDENTIAL`. An environment variable name is not a credential reference. Additional observer/backend/recovery credentials need owner-approved bindings and verifier integration; inventing new environment-variable contracts would not resolve them. The public packet carries references and scope only, with no token, password, private key, secret payload or authenticated kubeconfig content.

[Story 4.15](../../../_bmad-output/planning-artifacts/epics.md#story-415-provide-staging-secrets-through-a-per-environment-openbao-and-tenant-key-store) requires per-app tokens, pod restart on rotation and named renewers for the shared bootstrap-token split. Its recorded `2027-07-19` bootstrap expiry is not an expiry for any Parties credential. Recovery writer/validator credentials and `jpiquot` native access are not application credentials.

## Custody and surviving authority decisions

Preserve Branch B and every immutable tenant-scoped Agent Party ID. An Organization classification neither replaces identity-by-ID checks nor grants human approval authority. Preserve exactly `party-actor-retention-v1`, purpose `party-actor-history-v1`: **365 fixed 24-hour days, or 31,536,000 seconds, from original binding effective-at**. Release is allowed only while `now < ExpiresAt`. Retry, revocation, erasure, rotation, copying and restore never renew the original deadline.

| Decision | Required owner record | Current disposition |
| --- | --- | --- |
| Q1 backend | Actual backend/mount, unit key hierarchy, all historical key generations, wrapping/escrow paths, backup destruction mechanism and independent capability evidence | Unresolved |
| Q2 authority | Monotonic lifecycle/receipt anchor outside the EventStore/OpenBao/ciphertext restore cut, authority identity/failure domain, only writers, fence/reissue owner/procedure, authenticated time source, skew and currentness bounds | Unresolved |
| Q3 inventory | Durable all-copy inventory/work/original receipts, per-copy owners, authenticated absence, probe and recovery-hook bindings, receipt minimization and retention | Unresolved |
| Q4 operations | Cleanup/monitoring/escalation operators, bounded retries/outage/receipt completion, and cryptographic expiry during worker/backend failure | Unresolved |
| Q5 mapping | Collision/retry identity and original-binding mapping for establish, rebind and revoke without a new predecessor deadline | Unresolved; proposed rules in the qualification plan |
| Q6 host integration | Approved provider packaging in both owning hosts, Parties snapshot codec/payload adapter and independently authorized retained-source admission | Unresolved |
| Q7 erasure | Profile/tenant erasure boundaries and minimal surviving receipt/anchor/continuation metadata | Unresolved |

The current [registration](../../../src/Hexalith.Platform.Custody/PlatformCustodyServiceCollectionExtensions.cs) supplies cleanup and unavailable cryptographic defaults. The [EventStore host](../../../src/Hexalith.Platform.EventStoreHost/Program.cs) composes identity without a production history provider. T1 immutable custody contracts already exist; they do not supply T2–T6 or backend guarantees. The five-second cleanup provider wait is not an expiry, retry or destruction-completion service guarantee.

The historical [OpenBao recovery summary](../../../_bmad-output/implementation-artifacts/evidence/epic-4/4-0/20261001t061620z/openbao-proof-summary.json) demonstrates restoration of KV paths, mounts and policies from an off-site snapshot. It expired on `2026-10-02T07:21:38Z`. **Inference:** putting actor keys and lifecycle state in the same ordinarily restorable snapshot can restore old decrypting authority. Neither separate key names nor application read denial proves irreversible expiry. Q1/Q2 must account for immutable backups and independent authority before any capability flag becomes true.

## Copies and destruction owners

The [copy inventory form](decision-packet.json) enumerates sixteen classes: source events; current snapshots and both embedded binding/transition history paths; older snapshots/state versions; projections/read models; caches; broker/outbox/DLQ/replay; spools/exports/delivery; logs/telemetry; online replicas/storage copies; native backup chains; off-site/versioned/immutable objects; raw/wrapped/escrowed/rotated purpose keys; every key backup and wrapping/seal dependency; quarantine/drill/temporary copies; lifecycle/admission/work/receipt records; and F1 continuation records.

Every class is **unknown**, with an actual location/generation/owner or authenticated absence required. No class is declared absent from a name search. Copy and destruction principals remain unset. Each physical record must bind environment/instance, original unit/deadline, key generation, recovery class, location/object version, only writers, copy registration/fence, destruction mechanism, owner, and receipt. A backup unit holds one accepted recovery class. The receipt closes a frozen inventory generation and covers late/in-flight writes before reporting completion.

Ordinary backup retention and continued availability of rotated backup keys cannot extend actor-purpose recoverability. Owners must resolve that boundary under Q1/Q7 and 8.6. Tenant/profile-key erasure blocks current eligibility immediately while independently retained attribution remains usable only for its original finite purpose. Minimal surviving receipts must not become indefinite actor history.

## Restore and fault targets

No approved Parties restore or failure-injection target exists in the inspected records. The historical `recovery-4-0-bao` namespace and isolated secret runtime are not candidates. The form requests exact cluster/namespace/workload UIDs, immutable backup versions/cut, key coverage, surviving authority, old-incarnation fence, recovery hooks, attempt/epoch, operator and independent observer, quarantine policy, rollback/abort/cleanup procedure and verified disposal bound.

Administrator `jpiquot@itaneo.com` is named in the cluster recovery records. Those records grant no Parties restore, fault-injection, signing or destruction scope. The deputy needs an actual identity and qualified custody (6.3/8.14); this packet supplies neither. Story 6.1 owns rehearsal fault architecture, but rollout-failure rehearsal alone does not exercise custody admission, copy, destruction, receipt-commit and lost-ack races.

## Decisions before implementation

Owners complete D01/D02, Q1–Q7, F1, D03/D04 and F2 in the JSON form with exact source/approval references and assigned principals. F1 must approve actor-free completeness and successor continuity after predecessor expiry; no expired predecessor actor/provenance/logical evidence may be retained or re-encrypted into the successor. Q5 must map revocation back to the original binding unit. The current processor admits all binding operations using the command's own `effectiveAt`; accepting that unchanged for revocation would not settle Q5.

The [Platform implementation plan](../../../_bmad-output/specs/spec-production-actor-history-custody/implementation-plan.md) assigns T2–T6 to the dedicated symbolic `PLAT-ACTOR-HISTORY-1`, with no invented epic placement. This packet adds no implementation, adapter, scheduler, service, database, vault, proprietary CLI or acceptance. It describes the exact work and blocked command bindings for owner review.

## Qualification and preservation

Retained Local evidence remains **1,991/1,991 across twelve lanes**. The [receipt audit](local-evidence-audit.json) rechecked 37 existing manifest/XML/log hashes and XML pass counts without running builds or tests. Local evidence does not establish installed or current-source qualification.

The authoritative register still records `Committed`, Parties target `56cb4401c4179d431385ad3d931fefa402e4fc5a`, integration date `2026-10-08`. These historical commitments are preserved. Observed Parties HEAD `d2ec85c9e2b85e2b0c7a50d92ffaf197590ea053` is not an accepted replacement. The form retains the exact registered command as data; it was not executed. The current verifier refuses full Live even if all twelve environment inputs are populated.

[Preservation evidence](preservation.json) records existing dirty/staged work and root submodule revisions. Concurrent external Platform commits moved HEAD and recorded its earlier dirty files without changing their preserved bytes. EventStore acquired two external documentation edits after the snapshot; they are recorded and untouched. No whole-workspace-unchanged claim is made. This task performs no Git mutation or dependency update. Parties' Platform gitlink remains distinct from the sibling Platform checkout. Only the additive packet is written by this task. [Validation and source hashes](validation.json) bound the finished packet.

No installation, Live, LiveReadiness or consuming seam ran. The final exact implementation/qualification command cannot be populated until references, code and owner acceptance exist. **EXT-PARTIES-1 stays short of Available until the owner-accepted full Live command passes against clean exact checkouts.**
