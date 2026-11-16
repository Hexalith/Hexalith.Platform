# Proposed profile-template and release-binding contract

Status: **draft, unadopted**. EventStore and Platform jointly review this proposal.
Names below describe required information, not an approved JSON/YAML schema or an
implemented validator. Nulls and empty inventories in the companion drafts are
explicit unresolved inputs and cannot qualify a release or deployment.

## Ownership and inputs

| Input | Owner | Contents |
| --- | --- | --- |
| Profile template | EventStore with Platform | Runtime/Dapr minor, component types and versions, per-app sidecar mode, actor isolation, ACL and resiliency shapes, mandatory append/erasure requirements and secret-reference contracts |
| Platform infrastructure inventory | Platform | Actual environment-layer/shared providers and exact pins, topology, separately pinned deploy tools, management/workload pairing, currency/renewal owners and upstream sources |
| Per-release binding | Release-record writers under existing publication rules | Module packages/images, composed host/extensions, retained application Helm package, catalog route-content digest, template identity, qualified infrastructure sets, Dapr sidecar patch, realm/recovery/suite contracts and evidence references |
| Environment-current bindings | Authorized environment/shared writers | Actual endpoints and physical targets, environment identity, key and activated catalog generations with root digest, applied realm/secret-contract identities, current credential references and effective applied configuration; captured by the attempt, with no secret values |
| Complete resolved inventory | Platform deployment owner through EventStore 3.19's publication flow | Exact actual apps, components, physical targets, runtime, issuer, catalogs, restore/protection inputs and rendered-deployment bindings required by AD-26 |

Applications retain the existing Dapr/actor boundary. Each actor-hosting app has
its own `actorStateStore: true` component scoped to its app ID, namespace and
physical target. EventStore's component name remains `statestore`. Separately
scoped read-model/operational components and shared reads are enumerated; the
proposal grants no cross-app actor-state access, provider credentials or new
application SQL/schema ownership.

The template describes the AD-26 target under review and its mandatory semantics.
It does not select a broker, runtime image, protection implementation or restore
posture. A selected implementation must supply the qualification those semantics
require. Existing approval of the append-protection engineering scope does not
replace AD-26 role ratification.

AD-26's proposed, unratified target includes OQ8 `oq8-postgresql-v1` and excludes
Redis (including the SignalR backplane) and Cosmos template alternatives as
authorizing EventStore production profiles. Currency coverage of another module's
actual production Redis provider does not authorize EventStore to use it.

## Distinct identity domains

Two profile identities serve different contracts and MUST NOT be treated as
interchangeable. Their proposed names are local to this draft:

1. **Platform infrastructure profile digest:** template plus the environment-layer,
   shared-provider/topology and separately pinned deploy-tool inputs required by
   the Platform architecture. Exclude other environment-current values and all
   release-invariant values. Changing only an image, catalog generation, endpoint
   or credential generation must not silently redefine this digest's domain.
2. **EventStore canonical resolved profile digest:** the complete actual resolved
   inventory required by AD-26, at EventStore's sole
   `deploy/dapr/production-profile.yaml` slot. Bind the infrastructure profile
   identity, applicable release and environment bindings, actual loaded component
   and image identities, app/namespace/physical-target inventory, active catalogs,
   issuer, restore posture, append-protection identity and required evidence.

Release/deployment records retain each identity under its own meaning. Matching
the infrastructure-only digest cannot establish completeness of the resolved
inventory. A complete resolved inventory cannot silently expand the Platform
infrastructure-only digest's inputs. EventStore/Platform must accept this mapping
before adoption; no existing validator is claimed to understand it.

This draft fixes no final canonical-byte encoding. The agreed schema, ordering,
null/number/string rules and canonical-byte implementation remain joint contract
work; reuse the owning approved codec if one is delivered. No ad hoc local
serialization may issue authority. The preparation index's raw-byte SHA-256
identifies review documents only and is not either production profile digest.

## Required per-release and resolved information

The release binding must identify the exact retained module package/image set,
composed host and extensions, immutable application Helm package, template,
catalog content, stable McpCli package hash, qualified shared/environment sets,
Dapr sidecar patch and realm/recovery/check-suite contract identities. Changing
an immutable subject begins its lifecycle at `built`; an unchanged release is
not granted a later state by reusing another subject's receipts.

The resolved inventory must additionally contain the exact runtime image
(at least AD-26's Dapr 1.18.3 floor) and CLI compatibility, Kubernetes/sidecar mode,
Scheduler and placement settings,
every app ID and component scope/ACL, API-token enablement, resiliency, OpenBao
contract and server floor, route/idempotency catalog digests, profile-scoped
AD-34 inventory/register, qualified append-protection tuple and writer inventory,
authorized erasure, workload-assertion issuer and bootstrap credential reference,
replica/freshness posture and complete restore posture. Public physical-target
identities are explicit; credentials remain secret-store references.

The restore posture enumerates actor events/revisions/erasure completion,
admission/fence, global allocation, read models/checkpoints/command status,
Scheduler jobs/reminders and placement. It names backup method, consistency
point, order and forbidden independent restores. Profile-specific readback must
prove the invariants required by EventStore 7.22 before acceptance.

The Platform inventory also carries the complete Rancher/agents/CRDs, management
cluster and backup units, workload cluster, private access/authentication,
capacity, native recovery access and writer boundaries required by 4.9. Currency
evaluation uses actual component instances, their pins and current upstream
support/advisory data, including production environment providers. One provider
serving several roles retains each coverage scope without inventing deployments.

## Resolution and acceptance boundary

1. Joint owners agree the template and binding contract, canonical encoding and
   exact schema/validator/codec identities. These drafts alone establish none.
2. Platform supplies actual pins, topology and complete value-free bindings from
   its qualified inputs. Resolve no component, target or qualified set by default.
3. Check exact package/image/runtime/component identities and full instance
   coverage, supported/qualified combinations, append and erasure semantics,
   catalog/issuer bindings and restore proof. Missing or unknown inputs refuse.
4. The registered owner issues the separate authenticated Architecture and
   Platform deployment ratifications under AD-26/AD-11, bound to their exact
   subjects and selected qualified target. The consuming sealed transition
   evaluates their required/achieved assurance under AD-12.
5. The authorized publication flow writes the accepted canonical resolved bytes
   at EventStore's sole slot; its validator computes and retains that digest.
   Platform's release/deployment records bind that same digest. Templates,
   partial bindings, a second profile file or a summary pass cannot substitute.

Steps express dependencies, not authorization to run them. Candidate preparation
and proof may precede ratification where the governing stories permit it, while
final acceptance and lifecycle issuance retain their existing gates. This
contract creates no new release writer, signer, proof store or deployment tool.
