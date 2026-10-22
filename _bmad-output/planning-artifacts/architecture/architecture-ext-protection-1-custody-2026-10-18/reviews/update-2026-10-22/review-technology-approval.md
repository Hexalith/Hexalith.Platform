# Technology and approval-boundary review — 2026-10-22

Verdict: **PASS for the architecture recommendation and owner-decision handoff.** No blocking technology claim or approval-scope regression was found. This verdict establishes no implemented or qualified custody backend.

Reviewed the updated custody spine and custody design against the October 20 erasure-authority decision, its OWNER-APPROVAL record and frozen research/observation records, the October 18 design at `HEAD`, the production history lifecycle contract, and relevant source contracts. No deliverable was edited by this reviewer.

## Findings and required dispositions

No Critical, High or Medium correction is required in the reviewed artifacts. Keep these already explicit gates open:

1. **C1: component/provider selection and physical proof.** `bindings.hexalithcustody v1` and `custody-lifecycle-v1` are proposed private contracts, not names of an existing Dapr custody product. The text consistently says the component is unimplemented and no backend qualifies. Owners must still choose an exact immutable component/provider profile and establish independent authority, common-cut target erasure, survivor recovery, holder closure and exact-original publication. Do not convert this review into provider acceptance.
2. **Q1/Q2/Q4: exact history expiry feasibility.** The autonomous per-unit deadline boundary is a capability requirement; no verified product, installed implementation or proof of power-loss/time behavior is supplied. The design appropriately recommends investigation while refusing production admission. A denied read, a five-second cleanup wait, an expiration flag, or delayed KMS deletion cannot close this gate. Physical deadline enforcement and every backup/holder path require evidence before admission.
3. **Q2/Q3/Q7: recovery and retained evidence.** The independent failure domain, actual authority/time/fence/deputy access, copy owners, numerical retention and linkable-receipt minimization are owner inputs, not solved by Dapr or a storage product. Source `IdentityHistoryDestructionReceipt.Unit` retains a full original unit with aggregate identity, evidence reference and timestamps; the new warning against treating its “content-free” name as anonymity is warranted. Preserve immutable interaction-original obligations separately.

These are implementation/owner gates already captured in the design, not requests to change accepted policy or reopen OA-1–OA-6.

## Decision-to-evidence check

| Decisions | Evidence/reality check | Result |
| --- | --- | --- |
| AD-16 / AD-25: semantic output-binding boundary | Dapr 1.18 documentation supports pluggable bindings; pinned `v1.18.1` binding proto exposes Init, Invoke, ListOperations and Ping. The general binding API accepts component-specific operations and opaque data/metadata. These are transport/extension capabilities, not custody semantics. | Fit as an extension direction. No thin relay, installed custody provider, native application exception or exactly-once guarantee is inferred. |
| AD-17: original DEK/KEK plus epoch gate | Original October 18 design and October 20 OA-5 retain the existing AES-GCM/AAD/version identity while adding the private gate. The update preserves survivor evacuation, prohibits bare original wrappers and demands all predecessor/tainted-generation closure. | An adopted construction with unresolved physical proof; no ordinary OpenBao guarantee is inferred. |
| AD-18 / AD-28: forward-only authority and independent restore | October 20 OA-2/OA-3 and the observed single-node installation distinguish replicas from independent host/site survival. Ordinary OpenBao/Raft negative control records `ObservedRevival`. First-release authority replacement remains unsupported and total authority loss fails closed. | Correctly treats authority independence as a required deployment/recovery property, not a consequence of replication or a signed old checkpoint. |
| AD-19 / AD-23: ordering and source compatibility | October 20 approved decision preserves EventStore-owned v23 reserve/block/cancellation, original hold ordering, existing provider-neutral contracts and explicit occurrence-completion proof obligations. | No ownership/policy change or technology substitute introduced. |
| AD-20 / AD-21: common physical fate plus recoverable publication | OA-1 expressly accepts this criterion without a native cross-service transaction. AWS KMS documentation distinguishes scheduling from actual irreversible deletion and documents its waiting period. Independent lineage/holder/survivor evidence remains mandatory. | The previous October 18 native-transaction ambiguity is correctly superseded by recorded approval; physical feasibility remains open. |
| AD-22: fixed head transaction and permanent original identity | DynamoDB primary documentation limits transactions to 100 actions, one account/Region and 4 MB aggregate items; its client token lasts ten minutes. It also warns that transaction changes can propagate partially into backups. | Complete immutable objects selected by a bounded head transaction are a candidate design, not 1,000 independent atomic deletes or permanent provider-token idempotency. Restore clones cannot self-promote. |
| AD-24 / AD-25: transport and secret holders | Dapr documents retry policies, and pinned runtime source implements pluggable transport over UDS/gRPC. The design explicitly inventories buffers, uncertain sends and actual completion/termination; it does not equate timeout, lease expiry or sidecar restart with erasure. | Guarantees remain custom implementation/proof obligations. Dapr itself is not claimed to provide buffer zeroing or custody closure. |
| AD-26: accepted fixed history policy | Existing lifecycle contract, `IdentityHistoryCustodyUnit` validation and owner-accepted `party-actor-retention-v1` establish original effective time plus 365 fixed days, UTC-instant preservation and exclusive expiry. | Adopted policy, not a new backend claim. Rebind/revoke/snapshot copies cannot extend predecessors. |
| AD-27: autonomous expiry under outage/restore | Existing lifecycle contract requires cryptographic unrecoverability, including immutable backups. The update labels the mechanism a recommendation and explicitly reports that no provider was identified. AWS waiting-period constraints rule out using the interaction profile to claim exact expiry. | No unproved expiry product or solved power-loss/time mechanism is inferred. Q1/Q2/Q4 remain blocked. |

## Named technologies and observed versions

| Name | Check | Disposition |
| --- | --- | --- |
| Dapr runtime 1.18.1 | October 20 `infrastructure-observation.json` records deployed Dapr images; the pinned runtime proto and implementation are retrievable from the upstream `v1.18.1` tag. Current documentation identifies the 1.18 documentation track as latest and 1.19 as preview. | Observed baseline, not a new latest-patch selection or qualification. |
| Dapr.Client / Dapr.Actors 1.18.10 | Both current `references/Hexalith.Builds/Props/Directory.Packages.props` and sibling Builds catalog pin these versions. | Existing application dependency reality; no cloud package is introduced. |
| .NET SDK 10.0.402 / runtime 10.0.13 | `global.json` pins SDK 10.0.402; October 20 `version-observation.json` records runtime 10.0.13 among installed runtimes. | Correctly labeled source/observed baseline, not an accepted production runtime artifact. |
| OpenBao 2.6.2 | October 20 observed immutable image and prior actual restore negative control. | Existing HMAC-only scope preserved; rejected as ordinary recoverable erasure authority. |
| AWS KMS / DynamoDB / S3 / CloudTrail | Existing primary research names real managed primitives. Fresh checks confirm KMS waiting and actual-event behavior and DynamoDB transaction/idempotency limits. | Unaccepted reference infrastructure profile inside a possible component. No AWS account/Region/provider/deployment dependency is accepted. |
| Pinned pluggable 4 MiB response default | Dapr `v1.18.1` output binding constructs the default gRPC connector; the inspected connector and Invoke path set no response-size override. gRPC Go documents a 4 MB default receive limit. | The cautious “without a verified override” statement is justified. Full actual-hop/request-limit and buffer tests remain F1; paging value-free immutable results does not split manifest consumption. |

## Approval reconciliation

The October 18 design proposed a direct AWS adapter, a Platform AD-9 exception, Paris investigation assumptions and unresolved equivalence to a single native transaction. The October 20 OWNER-APPROVAL record expressly supersedes those points: OA-1 adopts the shared-cut criterion; subsequent Dapr-only direction amends OA-2/OA-5; OA-3 accepts bounded tenant serialization/pause and the reference profile's seven-plus-day pending interval; OA-4 and OA-6 preserve assurance and finite retention principles while requiring actual proof/settings.

The update follows that hierarchy. It retains AWS only as reference research, does not mandate a Region/account, does not add a native exception, does not reopen accepted atomicity or history-duration policy, and does not change dependency status or qualification. New history enforcement/recovery recommendations remain visibly distinct from adopted choices.

## Primary web evidence checked on 2026-10-22

- [KMS key deletion](https://docs.aws.amazon.com/kms/latest/developerguide/deleting-keys.html): seven-to-thirty-day waiting period, potential additional twenty-four hours, reversible scheduling, actual permanent deletion and already released data-key caveat.
- [Actual KMS DeleteKey CloudTrail record](https://docs.aws.amazon.com/kms/latest/developerguide/ct-delete-key.html): distinct final deletion event; its existence alone is not the application's complete-copy proof.
- [DynamoDB transaction behavior](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/transaction-apis.html): action/size/scope limits, finite token and backup-propagation caveat.
- [Dapr pluggable-component implementation](https://docs.dapr.io/developing-applications/develop-components/pluggable-components/develop-pluggable/): binding extension surface and implementation responsibilities.
- [Dapr binding API](https://docs.dapr.io/reference/api/bindings_api/): output endpoint and component-specific operation/payload/metadata.
- [Pinned binding proto](https://raw.githubusercontent.com/dapr/dapr/v1.18.1/dapr/proto/components/v1/bindings.proto), [pinned output binding](https://raw.githubusercontent.com/dapr/dapr/v1.18.1/pkg/components/bindings/output_pluggable.go), [pinned gRPC connector](https://raw.githubusercontent.com/dapr/dapr/v1.18.1/pkg/components/pluggable/grpc.go): exact supported methods and default transport path.
- [gRPC Go receive-size option](https://pkg.go.dev/google.golang.org/grpc#MaxCallRecvMsgSize): default receive limit; deployed override/every-hop behavior remains a test gate.
- [Dapr retry policies](https://docs.dapr.io/operations/resiliency/policies/retries/retries-overview/): configured retries can resend calls and are not lifecycle idempotency.

No runtime, infrastructure, secret, deployment or destructive experiment was performed by this review. Source observations and vendor documentation cannot establish an implementation's all-copy closure, exact expiry, trusted-time behavior, or independent authority survival.
