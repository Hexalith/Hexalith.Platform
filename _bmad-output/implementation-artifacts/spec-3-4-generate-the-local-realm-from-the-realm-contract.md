---
title: '3.4 Generate the local realm from the realm contract'
type: 'feature'
created: '2026-11-24'
status: 'ready-for-dev'
route: 'dispatch'
story_key: '3-4-generate-the-local-realm-from-the-realm-contract'
baseline_commit: 'cee3fe6814a3df6cfdcfb3ad5c63b2a9dc2a7de1'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** No realm contract exists, and Platform composes no identity realm. The Works preview trusts a hard-coded HS256 key (`DevOnlySigningKey-AtLeast32Chars!`, issuer `hexalith-dev`) for both user and workload tokens.

**Approach:** Author Platform's versioned, value-free realm contract v1 and one deterministic generator. The generator renders a Keycloak realm import from the contract, the selected declarations and run values. Run that realm in pinned Keycloak for the Works preview and remove the preview's HS256 key.

**Decisions (2026-11-24):**
- Scope covers the generator and the Works preview only. The default `aspire run` composition and the Builds runner and CI keep their per-run HS256 key; that adoption is deferred to Story 3.6.
- Platform changes only. Works test adoption is a recorded owner follow-up, and Platform proves the preview with its own live test.
- Proceed by citing EventStore 5.4.0's existing claim constants. EventStore's formal publication is an owner follow-up that gates Story 4.19.
- McpCli is enabled only for authorization code with PKCE S256 and a loopback redirect `http://127.0.0.1/*`. Direct-access, device, implicit and service-account grants are disabled.

## Boundaries & Constraints

**Always:** Reference only existing EventStore claims (`eventstore:surface`, `:tenant`, `:domain`, `:permission` and workload claims); invent none. Platform owns the instance contents: surface classes, client-to-surface map, token exchange, admission groups, event settings, refresh-token settings and the synthetic-tenant slot. Each client:
- maps to exactly one class and emits exactly one `eventstore:surface` value;
- sets `fullScopeAllowed=false`;
- maps its audiences and roles explicitly.

Public clients never get `offline_access`. Run values (realm URL, synthetic tenant identifier, confidential secrets) enter only at generation, and secrets stay out of the file and the logs. Use `quay.io/keycloak/keycloak:26.6.4`.

**Never:**
- hosted realms, admission records or event export (Stories 4.19 and 6.11);
- synthetic clients (Story 3.5);
- the McpCli login code (Story 3.7);
- changes to EventStore claim semantics, the descriptor schema or story 2.12's run-owned workload realm;
- any symmetric key in the Works preview.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected | Error Handling |
|---|---|---|---|
| Repeat generation | Same contract, declarations and run values | Byte-identical realm JSON | No timestamps, paths or secrets in the content |
| Contract violation | Any of: public client with `offline_access`; `fullScopeAllowed=true`; zero or two classes; unknown class or claim; agent client with realm-management or admin audience | Refuse, naming the client and rule | No file and no Keycloak start |
| McpCli disallowed request | `offline_access` scope, password grant or device grant | Keycloak refuses | — |
| McpCli token | Issued through authorization code with PKCE | `azp=mcpcli`, one `eventstore:surface=agent`, only audience `hexalith-eventstore`, no roles | — |
| Token exchange | Requester absent from the subject token's audience, or target outside its role-scoped audiences | Keycloak refuses | — |
| Works preview | `Platform:Works:Enabled=true` | Hosts validate RS256 realm tokens; workload issuers use realm client credentials | A token signed with the old HS256 key gets 401 |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Composition/WorksPreviewModel.cs:314-328` -- Holds the HS256 key and the `hexalith-dev` issuer for eventstore, admin, operations and works hosts. Lines 194-197 and 245 hold the workload audience and issuer.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtWorkloadAssertionIssuer.cs:132,193,241-251` -- Self-signs with `SigningKey`. Otherwise it uses Authority client credentials with `eventstore-audience.*` and `eventstore-operation.*` scopes, which the realm must provide.
- `src/Hexalith.Platform.Composition/PlatformOwnerServices.cs:52-95` -- `AppendOwnerClientsToRealm` has the audience and operation scope shapes to reuse. It is called only by ChatBot.
- EventStore owner realm (read-only reference) -- `references/Hexalith.EventStore/src/Hexalith.EventStore.AppHost/KeycloakRealms/hexalith-realm.json` provides the service client and attribute mappers. The Tenants realm's `eventstore-ui-surface` hard-coded mapper is the precedent. `Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs:107-108` shows `AddKeycloak` with `WithRealmImport`. `Aspire.Hosting.Keycloak` is already referenced at `Hexalith.Platform.Composition.csproj:33`.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/RouteCatalogSurfacePolicy.cs:11,30` -- The surface comes only from the signed claim; there is no `azp` map.
- `references/Hexalith.Builds/schemas/hexalith.module-manifest.v2.json:459,735` -- Declares `surfaceClass` and `identityNeed`. Platform declarations have only `service` interfaces, and McpCli declares none.
- `infrastructure/keycloak-workload-binding/run_owned.py:26` -- Pins Keycloak 26.6.4. Its jars provide `standard.token.exchange.enabled`, `standard.token.exchange.enableRefreshRequestedTokenType` and the `downscope-assertion-grant-enforcer` executor.
- Builds `CompositionRunPlan.TenantNamespace` and descriptor `isolation.syntheticTenantId` -- Supply the synthetic tenant value.

## Tasks & Acceptance

**Execution:**
- [ ] `docs/contracts/realm-contract-v1.md`, `docs/contracts/schemas/hexalith.realm-contract.v1.json`, `src/Hexalith.Platform.Composition/Declarations/realm-contract.v1.json` -- Author contract v1 with the claim references, the three classes and the client-to-surface map. The map has `mcpcli` as the public `agent` client and confidential `service` clients from declared application principals. Also cover the exchange switches, the requester-in-audience precondition and the downscope policy, no refresh-token exchange, the admission groups, events, refresh lifetimes and the synthetic-tenant slot.
- [ ] `src/Hexalith.Platform.Composition/PlatformRealmContract.cs` -- Load the contract, validate the schema and invariants, and refuse the matrix violations.
- [ ] `src/Hexalith.Platform.Composition/PlatformLocalRealm.cs` -- Render canonical realm JSON with secret placeholders, and add the pinned Keycloak resource with the realm import.
- [ ] `src/Hexalith.Platform.Composition/WorksPreviewModel.cs` -- Compose the realm and set Authority, Issuer and RS256 on every validator. Give workload issuers realm client credentials, and delete `Works:Authentication:DevSigningKey` and the literal key.
- [ ] `tests/Hexalith.Platform.Composition.Tests/PlatformLocalRealmTests.cs` -- Cover determinism, the acceptance criteria and every refusal.
- [ ] `tests/Hexalith.Platform.Composition.Tests/PlatformLocalRealmLiveTests.cs` -- Gated by `HEXALITH_PLATFORM_LIVE_SMOKE=1`: import into Keycloak 26.6.4 and prove the McpCli, token-exchange and HS256-401 rows.
- [ ] `tests/Hexalith.Platform.Works.Tests/WorksPreviewModelTests.cs` -- Assert there is no SigningKey or `hexalith-dev`, and that validators use the realm Authority.

**Acceptance Criteria:**
- Given contract v1, when the local realm is generated, then it contains classes `ui`, `agent` and `service`, a distinct client per calling surface mapped to exactly one class, token-exchange permissions and preconditions, admission groups, refresh-token settings and the run's synthetic tenant identifier.
- Given the public McpCli client, when it is generated, then it is `agent`-class and public, has no `offline_access` and has full scope disabled, with audiences and roles mapped explicitly.
- Given the Works preview, when it composes, then no HS256 key or symmetric setting remains, and its hosts accept only realm-issued tokens.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

These are planner decisions:
- The realm is `hexalith-local`.
- McpCli has an empty explicit role set.
- User and admin events are enabled with details and stored locally.
- Refresh tokens are revoked on use (`refreshTokenMaxReuse=0`), with a 30-minute idle and 10-hour maximum session.
- The admission groups are `hexalith-admission` and `hexalith-synthetic-admission`, both empty locally.
- Follow-ups for default and runner adoption, Works tests and EventStore claims are recorded in `deferred-work.md`.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -p:HexalithEventStoreRoot="$PWD/references/Hexalith.EventStore" -p:HexalithBuildsRoot="$PWD/references/Hexalith.Builds"` -- Builds with no errors. Run both realm test classes with `-class`; with `HEXALITH_PLATFORM_LIVE_SMOKE=1` they all pass.
- Build `tests/Hexalith.Platform.Works.Tests` with the same properties and run `WorksPreviewModelTests` -- passes.
- `rg -n 'DevSigningKey|DevOnlySigningKey|hexalith-dev' src` -- no matches.
