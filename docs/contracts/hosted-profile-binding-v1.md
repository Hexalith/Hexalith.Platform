# Proposed hosted profile binding v1

Status: **assessed v1 proposal; adoption withheld because the complete inventory
mapping has a concrete scope gap**. The user selected the mapping-first
recommendation on 2026-11-16. The [three-case assessment](../implementation/hosted-profile-v1-mapping-assessment.md)
finds that node-level containerd cannot satisfy v1's mandatory namespace and
Dapr-style configuration fields truthfully. A focused owner v2 is the next
construction step. This document retains the inspected v1 proposal and supplies
no inventory, qualification, ratification or publication authority.

## Exact owner implementation

The proposed owner baseline is EventStore
`54f83e3b6a6eca3c8cf0caeb734cbcad30e18e41`, inspected from Platform
`575aa1889b0dc654b36de2e6958878c5eadbc871`. These are source identities,
not new package pins or attestations of a complete execution-input closure.

| Owner source | Proposed Platform use |
| --- | --- |
| [Production profile v1 construction contract](../../references/Hexalith.EventStore/docs/architecture/production-profile-v1.md) | Normative closed grammar, requirement mapping, canonical bytes and digest domains |
| [Deployment types](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Deployment/ProductionProfile.cs) | Reuse the owner's complete typed model and its `ProductionProfile*` dependencies; introduce no parallel profile schema |
| [ProductionProfileCodec](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileCodec.cs) | Reuse `Seal`, `Encode`, `Decode`, `ComputeInfrastructureDigest` and `ComputeResolvedDigest`; implement no Platform canonicalizer |
| [ProductionProfileValidator](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileValidator.cs) | Structural validation with independently supplied expected instance coverage |
| [Publication authority v1](../../references/Hexalith.EventStore/docs/architecture/publication-authority-v1.md) | Sole profile slot, separate composing inventory, lifecycle associations and retained fixed refusals |

The owner completed both construction specs. Parent EventStore 3.19 remains
in progress. A future source update needs an explicit compatibility check and
recorded source identity; consumers must not silently follow a latest version.

## Proposed mapping of the approved design

The approved [preparation package](ad26-preparation/README.md) and its
[design approval](ad26-preparation/design-approval.json) remain unchanged.
Their draft JSON and raw-byte review hashes are review artifacts, not production
profile inputs. The conditional mapping-first decision is recorded below; it
does not adopt v1 or change that package's approved bytes.

| Design input | Implemented owner representation |
| --- | --- |
| Stable template and runtime/component/sidecar/ACL/resiliency contract | `template`, including secret/issuer/restore contracts and mandatory append semantics |
| Infrastructure provider, topology and deploy-tool pins | `infrastructure.providers`, `infrastructure.topology`, `infrastructure.tools` |
| Immutable release bindings | `release`, including artifacts, runtime, host/extensions, Helm, catalog content and qualified infrastructure set references |
| Environment-current bindings | `environment`, actual applications/components, active `catalogs`, issuer, freshness, protection, restore and evidence associations |
| Currency coverage and ownership | Scoped `components` instances, provider references, versions and loaded identities, support/advisory sources, currency and renewal owners; separately composed expected coverage |
| Management/workload identity and qualification inputs | Topology provider/cluster references, Rancher/backup/native-access/writer contracts and evidence references |

These are declarations. A provider pin does not establish the version or pin of
every bundled constituent. Platform 4.9 must still enumerate all actual shared
and production-provider instances required by the
[currency policy](shared-infrastructure-currency-policy-v1.md), including each
versioned constituent, actual usage scopes and qualified management/workload
pairing. Source references and evidence identifiers do not authenticate their
contents or prove current support, advisory status or qualification.

## Two distinct profile identities

Use only the owner's canonical encoding: strict JSON in the YAML profile slot,
UTF-8 without BOM, extra whitespace or final LF; ordinal object/set ordering,
with restore execution order preserved. The owner contract defines all escaping,
integer, duplicate, unknown-property, null, size and depth rules. Do not
reimplement those rules using a general YAML or JSON serializer.

The **infrastructure digest** is lowercase SHA-256 of the owner-canonical object
`{"infrastructure":I,"purpose":"platform-infrastructure-profile","schemaVersion":1,"template":T}`.
It includes template, provider/topology and deploy-tool pins. It excludes release
inputs, environment-current endpoints and credential generations, loaded
observations, owners and support/advisory sources. This digest includes deploy
tools even though the currency check excludes them.

The **resolved digest** is lowercase SHA-256 of the complete canonical profile,
including its recomputed `infrastructureDigest`. The resolved digest stays
outside its own preimage. A release/environment-only change can leave the
infrastructure digest unchanged while changing the resolved digest. A provider
pin change affects both. Neither digest substitutes for a catalog digest,
publication record identity, rendered-deployment identity or review-file hash.

## Coverage and publication boundary

The eventual accepted complete resolved bytes belong only at EventStore's
`deploy/dapr/production-profile.yaml`. Platform's proposed
`deploy/dapr/hosted-profile-inventory.json` is a separately owned composition
input; no such inventory is created here, and its final representation still
requires implementation. It is not an alternate authorizing profile.

The owner defines `deploy/dapr/production-composing-inventory.json` with exactly
`schema: hexalith.eventstore.production-composing-inventory.v1`, `source`
(`path`, `sha256`), `expectedApplicationIds` and `expectedComponentIds`.
Its source must independently identify the composing inventory, never itself or
the profile under validation. Platform must provide exhaustive actual instance
coverage; copying IDs out of the profile cannot establish that coverage.
The owner validator consumes the nonempty expected sets through
`ProductionProfileValidationContext`.

Structural success reports both digests with `Authorization` and `Qualification`
always `unevaluated`. It does not validate the truth of the supplied composing
inventory, qualify a target or evaluate currency. `Seal` is a structural codec
operation, separate from authenticated sealed-transition consumption.

Authenticated owner execution currently refuses
`PROFILE_BUILD_INPUT_PROOF_UNAVAILABLE` before launching .NET. The Builds
`ReleaseEvidenceCodec` integration and fixed runtime/broker, isolation, catalogs,
restore, append-protection, issuer and AD-26 verifiers also remain unavailable.
Adopting the structural contract must retain every refusal. Actual qualification,
authenticated role records and consuming-transition assurance remain required
before publication or lifecycle authority.

## Mapping decision and remaining work

The user's “apply recommended” selects a bounded check followed by v1 adoption
if clean, or focused v2 if a structural gap is demonstrated. The assessment
demonstrates the latter: provider pins cannot replace complete instance coverage,
and synthetic namespaces/configuration are unacceptable. Keep v1 and its digests
unchanged; do not adopt it as the complete Platform inventory format. The focused
[owner construction spec](../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-3-19-production-profile-v2-infrastructure-scope.md)
is approved and `ready-for-dev` after the user's “approve and stop”. Its reviewed
body is unchanged; implementation has not started. No final format adoption is
claimed by this construction approval.

After adoption, Platform 4.9 still needs actual qualified inventory and external
coverage, hosted catalog generation, the value-free secret contract, renewal
ownership, measured capture-lag policy and accepted owner publication. Stories
6.14 and 8.17 still need their real admission and drill contracts. Only then can
9.2 be replanned against accepted inputs under the selected prerequisites-first
sequence. This proposal completes none of those stories.
