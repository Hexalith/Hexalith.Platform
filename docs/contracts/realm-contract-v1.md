# Platform realm contract v1

The [schema](schemas/hexalith.realm-contract.v1.json) describes the
[value-free declaration](../../src/Hexalith.Platform.Composition/Declarations/realm-contract.v1.json)
for `hexalith-local`. Platform owns the client-to-surface mapping, audiences,
role grants, admission groups, exchange policy, audit events and refresh
settings. Keycloak runs on `quay.io/keycloak/keycloak:26.6.4` and signs access
tokens with RS256.

## EventStore claims

EventStore 5.4.0 defines the wire names and their meaning. The signed
`eventstore:surface` claim identifies the calling surface. EventStore
interprets `eventstore:tenant`, `eventstore:domain` and
`eventstore:permission` for authorization. Its workload validator uses
`azp` and `eventstore:operation` to verify an assertion and builds its own
`eventstore:workload` and `eventstore:channel` principal claims. Works uses
EventStore's `eventstore:bound-tenant` and `eventstore:bound-domain` for
resource binding. The binding provider emits the existing `eventstore:grant-id`
and `eventstore:grant-revision` workload claims. [Surface policy](https://github.com/Hexalith/Hexalith.EventStore/blob/v5.4.0/src/Hexalith.EventStore.ServiceDefaults/Authentication/RouteCatalogSurfacePolicy.cs),
[claim transformation](https://github.com/Hexalith/Hexalith.EventStore/blob/v5.4.0/src/Hexalith.EventStore/Authentication/EventStoreClaimsTransformation.cs),
[workload authentication](https://github.com/Hexalith/Hexalith.EventStore/blob/v5.4.0/src/Hexalith.EventStore.ServiceDefaults/Authentication/EventStoreWorkloadAuthenticationDefaults.cs),
[workload binding](https://github.com/Hexalith/Hexalith.EventStore/blob/v5.4.0/src/Hexalith.EventStore.ServiceDefaults/Authentication/WorkloadBindingAuthority.cs).

| Class | Access and mapping |
| --- | --- |
| `ui` | Confidential. The local Works preview declares no UI caller. |
| `agent` | Public. `mcpcli` uses authorization code with PKCE S256 and `http://127.0.0.1/*`. Its only audience is `hexalith-eventstore`, with no roles. |
| `service` | Confidential. Each selected application principal has its own service account and explicit audiences, roles and operation scopes. |

Every client has one class, one surface mapper and full scope disabled.
McpCli has no offline scope, password grant, device grant, implicit flow
or service account.

The generator selects service clients from declared `applicationPrincipal`
identity needs and checks their audience, roles and claims. Works preview
selects the EventStore owner's gateway principal and the two Platform preview
principals for Works and EventStore Admin. The gateway's `domain-services`
alias authorizes `eventstore` and `works` when selected. Each other service
uses its declared principal audience directly.
Works replicas share the Works principal because they serve the same
calling surface.

Works can request bound stream reads, command submission and command status
for the configured synthetic tenant and the `work` domain. Trusted-effect
submission uses an unbound workload assertion and separate delegation.
The Works credential refuses an unbound request for any of its three
resource-bound operations, and has token exchange disabled.

## Exchange and session settings

The contract requires the requester in the subject token's audience, and
allows only target audiences granted by role-scoped client scopes.
Standard exchange is enabled per confidential service client and the
downscope policy restricts selection to scopes already in the subject token.
A separately mounted Platform client-policy executor enforces the requester
audience even when the requester originally obtained the subject token.
Refresh-token exchange is disabled. [Keycloak token exchange](https://www.keycloak.org/securing-apps/token-exchange).

The local realm starts with empty `hexalith-admission` and
`hexalith-synthetic-admission` groups. Keycloak stores user and admin
events locally, including admin event details. Refresh tokens are revoked
on use, with zero reuse, a 30-minute idle timeout and a 10-hour session limit.

## Generation

Pass the run's realm URL, synthetic tenant identifier and confidential
client secrets to the generator. Builds exposes the identifier as
`CompositionRunPlan.TenantNamespace` and records it in the environment
descriptor as `isolation.syntheticTenantId`. The standalone Works preview
uses `Platform:Works:SyntheticTenantId` when supplied and otherwise allocates
a new identifier for the run.

The JSON uses stable ordering and a secret environment placeholder per client.
Aspire binds the same per-run secret to Keycloak and the calling workload.
The import records the synthetic tenant identifier as a realm attribute.
Tenant creation and synthetic client provisioning belong to Story 3.5.

The .NET assembly ships the Keycloak 26.6.4 requester-audience policy and the
existing workload-binding provider, and mounts both JARs read-only with the
import. The import uses environment placeholders for the configured grants
and signer deadline. Aspire binds the 24-hour grants at startup. Start a new
preview after the grant deadline. The build script compiles both providers
for Java 21. [Requester-audience provider](../../infrastructure/keycloak-local-realm/src/main/java/com/hexalith/security/RequesterAudiencePolicyFactory.java),
[workload-binding provider](../../infrastructure/keycloak-workload-binding/src/main/java/com/hexalith/security/WorkloadBindingGrantPolicy.java),
[build script](../../infrastructure/keycloak-local-realm/build-policy.sh).
