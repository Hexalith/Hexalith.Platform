# Hosted profile v1 mapping assessment

Decision date: 2026-11-16. Scope: the three-case mapping exercise selected by
the user with “apply recommended”. This is a source-level representation check;
it creates no production inventory and makes no runtime, support or qualification
claim. No deployed version, pin, namespace or configuration digest is invented.

The inspected baselines are Platform
`575aa1889b0dc654b36de2e6958878c5eadbc871` and EventStore
`54f83e3b6a6eca3c8cf0caeb734cbcad30e18e41`.

## Result

**Do not adopt v1 as the complete Platform 4.9 inventory contract. Prepare a
focused owner v2 for non-namespaced infrastructure instances.** The standalone
namespaced-provider and management/workload-pair cases are representable. The
node-level containerd case requires a namespace and configuration facts that do
not apply to it. Accepting arbitrary placeholder values would misrepresent the
deployment.

The owner's [construction contract](../../references/Hexalith.EventStore/docs/architecture/production-profile-v1.md)
maps complete shared/environment instance coverage to `components`. Each
[component](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileComponent.cs)
requires `namespace`, `aclDigest`, `resiliencyDigest` and `dataMigrationDigest`.
The [validator](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileValidator.cs)
refuses nulls and empty strings in these fields. Its independent expected
coverage also checks components rather than provider pins. These facts establish
the representation gap without an executable probe.

## Three-case mapping

| Case | Approved pin | Deployed identity and software version | Sources and ownership | Qualification | Finding |
| --- | --- | --- | --- | --- | --- |
| Namespaced OpenBao service | Provider version and immutable digest | Actual component loaded identity; `environment.openBaoServerVersion` must match provider pin | Component sources, currency/renewal owners and evidence IDs | Real proof separately required | Representable when scope/configuration facts actually apply |
| Node-level containerd, including a bundled instance | Separate provider version and immutable pin; distribution version alone is insufficient | No Kubernetes namespace, but a component requires it and three configuration digests | Provider rows lack loaded-instance identity, owners/sources and independent instance coverage | Exact constituent and bundle proof separately required | Not cleanly representable as a complete shared instance |
| Rancher management/workload pair | Three provider identities, versions and pins | Topology binds actual cluster/provider identities; applicable component loaded identities remain explicit | Applicable component owners/sources and exact qualification references | Upstream compatibility and qualification of the exact tuple separately required | Identities representable; structural success does not prove qualification |

The OpenBao case maps fields; it approves no installed version. Historical
observations are not substituted for accepted current inventory. Required
cluster-scoped CRDs and node OS entries expose the same namespace problem.

## Rejected workarounds

- Invented namespaces and arbitrary configuration digests do not describe
  node-level containerd. A valid hash shape does not establish applicability.
- Provider pins alone do not satisfy complete shared-instance coverage.
- Keeping omitted instance facts only in Platform's separate inventory leaves
  the canonical resolved profile incomplete under the existing owner mapping.
- Allowing nulls across all components weakens mandatory Dapr checks. Absence
  must depend on the actual kind and scope.

## Focused v2 boundary

Revise only shared-instance representation: genuine environment/cluster/node/
namespace scope, distinct software and Dapr API versions, explicit bundle
relationships where present, and applicable Dapr details. Keep common pin,
loaded-identity, source, owner and evidence bindings and independently complete
expected coverage. Preserve Dapr isolation, token, physical-target and restore
requirements.

Preserve the two digest domains. Template and declared invariant provider,
topology and tool pins remain the infrastructure inputs. Actual instance scope,
node/namespace and bundle bindings, loaded observations and other environment-
current values belong only to the resolved identity, as actual component scope
does in v1. Specify the exact v2 field partition and preimages without silently
expanding the infrastructure domain or reinterpreting v1 golden digests. Use
explicit consumer version selection; no migration service or fallback is needed.

The focused [construction spec](../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-3-19-production-profile-v2-infrastructure-scope.md)
defines the next implementation for review. This assessment does not implement
v2 or authorize publication. Qualified inventory, build-input proof, authenticated
ratification and real admission/drill callers remain unfinished prerequisites.
