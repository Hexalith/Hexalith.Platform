# Proposed hosted profile binding v2

Status: **proposal awaiting Platform adoption after owner implementation review**.
This records the focused EventStore v2 construction contract required by the
[three-case mapping assessment](../implementation/hosted-profile-v1-mapping-assessment.md).
It accepts no inventory, selects no target or pin, and supplies no qualification,
ratification or publication authority. The approved
[preparation package](ad26-preparation/README.md) remains unchanged.

## Owner implementation and source identity

Reuse the EventStore-owned [v2 grammar and digest contract](../../references/Hexalith.EventStore/docs/architecture/production-profile-v2.md),
[model](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileV2.cs),
[codec](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileV2Codec.cs)
and [validator](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileV2Validator.cs).
Platform must not introduce its own canonicalizer or parallel profile schema.

The reviewed owner implementation is local commit
`57459e03bfe4cdbcbf0ecc1435c7aeb49d60812e`, based on
`70bf20dceaafd37229236e11d279a5d6ea434759`; its preserved implementation baseline
is `54f83e3b6a6eca3c8cf0caeb734cbcad30e18e41`. Exact committed file hashes and
verification references are recorded in the
[readiness observation](../../_bmad-output/implementation-artifacts/evidence/9-2/profile-v2-implementation-20261116.json).
This source identity is not a package pin or complete authenticated execution-input
closure. A future source update requires a recorded compatibility check and
explicit identity, without silently following a later version. The owner commit
has not been pushed by this workflow.

## Complete actual-instance mapping

V2 retains the owner's template, invariant provider/topology/tool pins, release,
applications, environment, catalogs, protection, issuer, freshness, restore and
evidence sections. Its complete `components` inventory represents:

| Required case | Owner representation |
| --- | --- |
| Standalone namespaced Dapr provider | `kind: dapr-component`, actual namespace scope, provider pin, software version and mandatory `dapr` configuration/API version |
| Bundled node containerd or OS | Node scope with actual environment/cluster/node identity, explicit null namespace and Dapr selection, own provider/software/loaded identities and distribution `bundleParentId` |
| Cluster-scoped CRD | Cluster scope, own provider/software/loaded identities, applicable distribution parent and no fabricated namespace/configuration |
| Management/workload/Rancher pair | Declared topology tuple plus exactly bound actual Kubernetes and Rancher instances; qualification remains separately required |

Every instance retains support/advisory sources, currency and renewal owners,
roles and evidence associations. Software versions match their own provider
pins; neither distribution labels nor Dapr API versions substitute for constituent
versions. Bundle parents and constituents are both independently inventoried.
Same-kind scoped names are unique. Mandatory Dapr token, actor, physical-target,
credential, restore and evidence checks remain in force.
Actual v2 namespaces use lowercase Kubernetes DNS labels of 1–63 characters;
other scoped identities retain the owner's opaque identifier grammar.

For generic infrastructure providers, a declared kind and matching software
version do not prove that the selected provider supplies that kind. The current
model has no authenticated provider-category registry. That correspondence
requires authoritative inventory and qualification; the owner's explicit
OpenBao and topology provider checks still apply.

These mappings establish representation only. The
[currency policy](shared-infrastructure-currency-policy-v1.md) still requires
exhaustive actual deployed instances and authoritative, current support,
security and exact pairing evidence. Synthetic vectors provide none of those.

## Exact encoding and distinct identities

Use only `ProductionProfileV2Codec`. The sole eventual resolved-profile slot is
EventStore's `deploy/dapr/production-profile.yaml`, containing owner-canonical
strict JSON: UTF-8 without BOM or final LF, ordinal object/set ordering, default
BCL escaping and preserved restore execution order. The owner specifies closed
fields, applicable nulls, bounds and all refusal rules. No decoder fallback or
v1 migration is implied; v1 golden bytes and digest preimages remain unchanged.

The infrastructure digest is lowercase SHA-256 of the canonical object
`{"infrastructure":I,"purpose":"platform-infrastructure-profile","schemaVersion":2,"template":T}`.
`I` and `T` contain the owner's declared invariant provider/topology/tool pins and
template. Actual instance scope, node/namespace/bundle bindings, loaded
observations, sources, owners and other current bindings are excluded.

The resolved digest is lowercase SHA-256 of the complete canonical v2 document,
including its recomputed `infrastructureDigest`; the resolved digest stays
outside its own preimage. Current-binding changes affect the resolved identity;
invariant-pin changes affect both. Neither identity substitutes for an evidence,
catalog, rendered-deployment or review-file digest.

## Independent coverage and authority boundary

The separate owner composing inventory explicitly selects
`hexalith.eventstore.production-composing-inventory.v2` and has exactly `schema`,
`source` (`path`, `sha256`), `expectedApplicationIds` and `expectedComponentIds`.
Its content-bound source must independently enumerate every expected actual
instance, never derive coverage from the profile being checked. Platform's
proposed `deploy/dapr/hosted-profile-inventory.json` is a composition input,
not an alternate authorizing profile. No such inventory is populated here.

Structural success leaves authorization and qualification `unevaluated`.
The [owner authority flow](../../references/Hexalith.EventStore/docs/architecture/publication-authority-v1.md)
retains build-proof, release-codec, qualification and authenticated ratification
refusals. Acceptance still requires actual qualified inventory and external
coverage, hosted catalog generation, the value-free secret contract, renewal
ownership, measured capture-lag policy and accepted owner publication. Admission
and drill callers remain prerequisites to the live Story 9.2 checker.

Adoption of this construction format alone completes none of Stories 3.19,
4.9 or 9.2 and cannot establish production eligibility.
