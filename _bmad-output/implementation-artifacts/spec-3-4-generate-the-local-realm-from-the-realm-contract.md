---
title: '3.4 Generate the local realm from the realm contract'
type: 'feature'
created: '2026-11-24'
status: 'done'
route: 'dispatch'
story_key: '3-4-generate-the-local-realm-from-the-realm-contract'
baseline_commit: '3a87b90739a248a62b526beb928813899bb9c262'
review_loop_iteration: 1
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
- Works workload bindings admit the configured synthetic tenant and the `work` domain. Reuse the existing workload-binding provider in the preview realm. Other tenant or domain values are refused. Complete-source authority enrollment remains outside this preview.

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
| Works recovery | Preview uses the SDK workload client for the configured tenant and `work` | Keycloak signs the required bound assertion and the real gateway authorizes the stream read | Refuse other tenants or domains |

</frozen-after-approval>

## Code Map

- `TestResults/platform-realm-implementation.patch` preserves the first implementation, including the binary provider. It applies to the current baseline.
- `WorksPreviewModel.Compose` builds the opt-in graph. `WorksPreviewModelTests.Builder` creates its private development catalog profile. A live run needs an owned Redis and actual Aspire/Dapr.
- `WorksRecoveryExtensions` selects SDK workload transport for `work`. `DomainServiceGatewayWorkloadAssertionSource` binds tenant/domain. `DomainServiceTrustedEffectAssertionHandler` requests an unbound operation with separate delegation.
- `infrastructure/keycloak-workload-binding/run_owned.py:208` shows the existing provider enrollment. Its mapper requires credential-grant admission. Gateway complete-source and query authority are opt-in and absent in this preview.
- `eng/works_host_snapshot.py` and `eng/test-extension-release/qualify-signed-component.py` select source build inputs.

## Tasks & Acceptance

- [x] `docs/contracts/realm-contract-v1.md`, `docs/contracts/schemas/hexalith.realm-contract.v1.json`, `src/Hexalith.Platform.Composition/Declarations/realm-contract.v1.json` -- Publish v1 with the existing EventStore claims, three classes, explicit client grants, session and exchange settings, and tenant slot.
- [x] `PlatformRealmContract.cs` -- Validate schema and client invariants, diagnose invalid claims/classes by client, reserve Keycloak client IDs, and enforce declared principal audiences with the explicit gateway alias.
- [x] `PlatformLocalRealm.cs`, `infrastructure/keycloak-local-realm` -- Render deterministic import, mount the pinned Keycloak providers, and bind secrets and finite grants at startup. Use both sparse exports' build closure for the embedded inputs.
- [x] `WorksPreviewModel.cs` -- Bind user and workload validation to this RS256 realm, clear inherited symmetric settings, and pin the workload token endpoint. Enable binding for Works replicas only. Grant bound stream-read, command-submit and command-status for the configured tenant and `work`, plus the existing unbound trusted-effect operation. Disable exchange for the bound Works client; keep gateway exchange.
- [x] `PlatformLocalRealmTests.cs`, `PlatformLocalRealmLiveTests.cs`, `WorksPreviewModelTests.cs` -- Cover every matrix row, the actual preview import mounts, the trusted-effect grant, and a permitted cross-client downscope. Run the gated live preview via its normal entry and exercise the real EventStore gateway with the Works SDK transport.

Given a generated local realm, when the selected Works preview hosts start, then they use its authority and the Works recovery stream transport authenticates with the exact tenant/domain scope. Given an inherited legacy key or token endpoint, when the preview composes, then the effective settings select only the generated realm.

## Implementation Notes

- Review paused pending a decision on which tenant and domain bindings the Works preview issuer may sign. Works recovery requests bound workload assertions and ordinary client credentials cannot issue them. The first implementation was reverted from the working tree after saving its binary-capable patch in `TestResults/platform-realm-implementation.patch`. The test results below apply to that patch.

- Mounted a separate Keycloak 26.6.4 client policy provider to enforce the requester-audience rule for exchanges of the requester's own token. Its source and reproducible build ship with the embedded JAR.
- The recorded baseline is not a Git object. The implementation audit used the story planning commit's parent, `cee3fe6840bd82437975fbe638b48ca1570379a7`, which matches the recorded short revision.
- Both test projects built. The Composition build used `-p:HexalithCommonsVersion=3.0.2` to resolve the current Parties and Tenants dependency graph. The Works model test and its child build used the same selected EventStore source root.
- Keycloak live and generator tests passed 12 of 12. The Works preview model tests passed 22 of 22. The source scan found zero old signing-key or issuer references. The provider rebuilt byte-identically.

- The single-tenant implementation passed all 22 generator cases, 22 Works model cases, and both gated live tests. The full preview used a matching EventStore source revision, SDK version, and shared artifact tree. The model suite used the package SDK. Composition used the Commons 3.0.2 override for the current dependency graph.
- The live Works SDK probe ran inside the actual preview and received EventStore's authored missing-stream 404 for the configured tenant. It refused other tenant and domain requests. The public gateway returned 403 for the authenticated McpCli agent without a tenant grant and 401 for both legacy HS256 probes.
- Both provider JARs rebuilt byte-identically. All 19 Works snapshot tests and both sparse build input checks passed. The source scan found no legacy signing key or issuer.

- After review, all 25 generator cases, 22 Works model cases, both gated live cases, and 19 snapshot tests passed. The independent final wrappers exited successfully. The source preview used aligned EventStore sources and version 5.5.1 with Commons 3.0.2. The live Docker endpoint and OIDC issuer shared the same fixed loopback port. The full Composition test project built with the selected EventStore 4.2.0 tuple. The selected 5.1.0 build excluded one unrelated older fixture from scratch MSBuild inputs. Both embedded providers rebuilt byte-identically.
- Final evidence is in [the live preview result](../../TestResults/realm-final-preview-final.log), [the Works model result](../../TestResults/realm-final-model-final.log), and [the standalone Keycloak result](../../TestResults/realm-review-keycloak.log). The cross-repository parity verifier awaits the deferred Works owner fixture adoption.

## Spec Change Log

- Review found that ordinary client credentials broke Works recovery and the original standalone smoke missed the failure. The maintainer selected one synthetic tenant. Reuse existing bound grants, verify the real preview, and preserve the working PKCE, secret placeholders, determinism, and strict requester-audience exchange checks. Close the build exports and inherited configuration gaps in the same implementation. Re-anchored the invalid hash to the current verified Git HEAD before touching code.

## Review Triage Log

| Review finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| Blind hunter: bound workload assertions | high | intent_gap | Works stream and command clients supply tenant/domain bindings. The SDK refuses them in ordinary client-credentials mode. The approved intent leaves the local binding authority's tenant policy open. |
| Blind hunter: live preview proof | high | bad_spec | The test starts Keycloak directly and authorizes a TestServer route. It does not reach the Works recovery path that the new issuer currently refuses. |
| Blind hunter: inherited JWT signing key | high | patch | The SDK requires exactly one of Authority and SigningKey. Its own Aspire helper overwrites inherited SigningKey with an empty value. |
| Blind hunter: inherited token endpoint | high | patch | ResolveTokenEndpointAsync prefers the explicitly configured endpoint and sends the newly bound credential to it. |
| Blind hunter: principal audience drift | medium | bad_spec | PrincipalAudience is checked against the declaration while Audiences drives scope creation. A service can acquire an additional receiver without a matching declaration. |
| Blind hunter: agent admin audience via slash | medium | patch | The schema accepts slash as a separator and the semantic admin check omits it. An additional agent can acquire a custom eventstore/admin audience. |
| Blind hunter: built-in client collisions | medium | patch | The schema and parser accept reserved Keycloak IDs. The importer uses the supplied representation for these IDs, which can displace built-in client initialization. |
| Blind hunter: generic error on invalid class or claim syntax | medium | patch | Schema validation precedes semantic validation, so values outside the token pattern produce a generic error without a client. |
| Blind hunter: exchange role test | medium | bad_spec | The successful case uses a single requester/target. The account refusal tests an invalid target with no assigned scope, so it cannot distinguish a missing role from the missing scope. |
| Blind hunter: source and embedded JAR can diverge | low | reject | The verifier rebuilt this JAR byte-identically. Continuous source-to-JAR verification would require a Java-enabled lane outside the opted-in check. |
| Blind hunter: invalid recorded baseline | medium | reject | The stated hash cannot resolve. The audit used the planning commit's parent. This finding targets the existing spec metadata. |
| Blind hunter: build arguments missing from verification recipe | medium | reject | The clean build used Commons 3.0.2 and implementation notes report the override. This finding targets the verification recipe in this spec. |
| Edge case: recovery stream reads | high | intent_gap | The normal gateway assertion source requests the tenant and domain bindings on stream reads. The SDK returns null without bound authority support. |
| Edge case: inherited token endpoint | high | patch | The SDK uses a configured endpoint before discovery and passes client credentials in its form body. |
| Edge case: service grants exceed declared audience | medium | bad_spec | Rendered audiences come from a separate array, with no check against the declared principal's resolved audience. |
| Edge case: unknown service audience silently dropped | medium | patch | Rendering intersects with selected service IDs without first distinguishing invalid target IDs from valid but excluded clients. |
| Edge case: null manifest identity need | low | reject | The directly read manifest can contain a null array entry and fail while dereferencing the deserialized principal. The source manifest is committed and the malformed input fails before creating resources. |
| Edge case: null audience element | low | reject | Keycloak 26.6.4 hasAudience calls equals on each element. Keycloak validates the subject token before issuing an exchange and the generated realm never emits null audience elements. This would harden malformed-input handling rather than affect accepted tokens. |
| Edge case: readiness timeout | low | reject | HttpClient can throw an uncaught TaskCanceledException before the outer test timeout, losing container diagnostics. The bounded test fails rather than claiming readiness. |
| Edge case: Docker cleanup failure | medium | patch | The test ignores a nonzero Docker removal result, then deletes the workspace, even if the container is still running. |
| Edge case: inherited workload JWT contract | high | patch | EventStore prefers its dedicated WorkloadJwtBearer contract when configured. The preview currently only sets the fallback user JWT section. |
| Edge case: synthetic endpoint as preview proof | high | bad_spec | The TestServer consumes the shared JWT configuration helper but leaves normal Works authorization and workload paths untested. |
| Verification gap: actual Aspire imports and provider mount | medium | patch | The manual Docker launch duplicates the mounts and the model test does not inspect them. Removing either mount leaves the existing assertions unchanged. |
| Verification gap: Works trusted-effect workload grant | medium | patch | The live test acquires a workload token only as EventStore. Removing trusted-effect from the Works credential leaves both live and model assertions unchanged. |
| Verification gap: live test absent from routine tuple CI | medium | defer | Tuple CI uses fixed test selectors. The story intentionally requires an opt-in Docker smoke test. Routine CI enrollment is separate. |
| Verification gap: signed fixture export misses embedded inputs | high | patch | The normal extensions tuple archives src and eng but omits docs and infrastructure. The new schema and JAR are required to compile its Composition probe. |
| Verification gap: Works immutable export misses embedded inputs | high | patch | The export selector omits both new resources and the Works verifier hashes literal EmbeddedResource paths before launching tests. |

| Single-tenant review finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| Blind hunter: native scan constant missing in retained SDK packages | high | patch | Reflection strings from the installed 4.2.0 and 5.1.0 assemblies confirm the symbol is absent. New code needs only the declared v1 operation inventory. |
| Blind hunter: randomized Keycloak port | high | patch | The issuer URL fixes the port before allocation and the live fixture disables DCP randomization. Use the same fixed proxyless port convention as the existing Platform controls. |
| Blind hunter: retained profile recomposition changes default tenant | medium | patch | The fallback generates a new tenant at each compose. The retained-profile guide needs the original configured tenant to preserve access. |
| Blind hunter: legacy Works parity fixture uses HS256 | high | defer | Platform's old cross-repository parity verifier uses the Works fixture that self-mints HS256. Works test adoption is explicitly owner-deferred and the Platform-owned live proof ran instead. |
| Blind hunter: missing selected principal audience | medium | patch | A selected Works principal can render without the required EventStore receiving scope because rendering filters unselected audiences. |
| Blind hunter: publicly exposed incomplete import API | low | patch | Only same-assembly code and friend tests call this surface. Keep the preview generator internal until a caller needs a complete deployment API. |
| Blind hunter: child-only inherited JWT arrays | low | reject | The selected child configurations have empty default arrays. The preview overwrites AppHost-visible values, and child-only invalid algorithms fail closed. Adding child startup components to override custom files would exceed the demonstrated need. |
| Blind hunter: live test isolates Dapr configuration | false | reject | The probe runs in the normal Works host with its registered SDK client, catalog handler and actual Dapr authorization. Test changes isolate ports and service discovery so the authenticated route can run. |
| Blind hunter: grant deadline leaves discovery healthy | low | reject | Discovery reports issuer reachability, while bounded grants refuse requests after the documented deadline. Keeping the preview open beyond a day requires recomposition. |
| Blind hunter: unused admin exchange permission | medium | patch | The shipped client map contains no subjects with eventstore-admin audience, so disable its unnecessary exchange flag. |
| Blind hunter: grant ID and revision assertions absent | medium | patch | The SDK validates bindings and operation but the proof does not pin these other published claims from the provider. |
| Edge case: randomized Keycloak port | high | patch | The fixed hostname and host configuration must use the actual Keycloak port when DCP randomizes proxied resources. |
| Edge case: null subject audience element | low | reject | Carried. Keycloak verifies the subject token before exchange and the generated realm never emits null audience elements. |
| Edge case: ambiguous pre-existing file AppHost artifacts | medium | patch | The test recursively enumerates the inherited artifact root then assumes it contains one apphost.dll. Load the exact Debug output built for the file AppHost. |
| Edge case: workspace cleanup after Docker failure | low | patch | The container removal assertion precedes workspace deletion in the same finally. Use a nested finally for each independent cleanup. |
| Edge case: interrupted JAR regeneration | low | reject | The manual regeneration script can be interrupted before closing an output. The checked-in outputs and repeat-build hashes match, and no normal build regenerates them. |
| Edge case: selected principal without receiving scope | medium | patch | A direct audience can be in the contract but excluded from the selected run, yielding a nonfunctional client. |
| Edge case: child-only symmetric JWT array entry | low | reject | The selected child configs carry no populated arrays and an invalid custom child-only algorithm refuses startup. The model verifies the AppHost-level inheritance overrides. |
| Verification gap: immutable export closure regression | medium | patch | Removing an embedded JAR from the selector still passes the existing 19 snapshot tests. Extend the existing fixture to cover the three inputs. |
| Verification gap: schema-only refusal | medium | patch | Existing negatives still pass when schema evaluation is removed. Test the invalid refresh value to prove the contract schema is applied. |
| Verification gap: grant deadline | medium | patch | Fixed-clock authorization settings are mechanically testable and the existing tests do not distinguish changing the deadline from 24 to 48 hours. |

## Design Notes

These are planner decisions:
- The realm is `hexalith-local`.
- McpCli has an empty explicit role set.
- User and admin events are enabled with details and stored locally.
- Refresh tokens are revoked on use (`refreshTokenMaxReuse=0`), with a 30-minute idle and 10-hour maximum session.
- The admission groups are `hexalith-admission` and `hexalith-synthetic-admission`, both empty locally.
- Follow-ups for default and runner adoption, Works tests and EventStore claims are recorded in `deferred-work.md`.

## Verification

Build the Composition and Works test projects with the selected Platform EventStore and Builds source roots. Use the verification-only Commons 3.0.2 override if the current dependency graph requires it. Run the realm and Works model classes, including gated `HEXALITH_PLATFORM_LIVE_SMOKE=1` assertions against Keycloak and the actual preview. Check both sparse export selections, rebuild the embedded providers byte-identically, and scan `src` for the old key and issuer.
