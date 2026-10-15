# Story 1.7 reality and version review

**Verdict: PASS after identity correction and scoped subscription recheck.** No open findings in the revised Story 1.7 additions. The original review missed a conflation of control-plane and workload trust domains; the live failure exposed it, and AD-9 and the AppHost document now distinguish them correctly. The subsequent live fixture result is confirmed below; owner release qualification is not established.

## Scope

Reviewed the three new AD-9 bullets (*Canonical hosting*, *Local bindings*, *Local security*) in `ARCHITECTURE-SPINE.md`, the corresponding changes to `docs/platform-apphost-form.md`, and the new memlog evidence against `_bmad-output/implementation-artifacts/spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`. Earlier spine decisions and unrelated documentation are out of scope.

## Reality and version evidence

| Commitment | Evidence and assessment |
| --- | --- |
| Canonical hosting packages; Commons supplies supporting primitives | `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/Hexalith.EventStore.Aspire.csproj` references the toolkit, and `HexalithEventStoreExtensions.cs` owns EventStore topology wiring. `Hexalith.EventStore.ServiceDefaults.csproj` exists with the stated shared-service role. Commons' `src/libraries/Hexalith.Commons.Aspire/AspireDaprDomainModuleExtensions.cs` supplies domain-neutral project/sidecar wiring, and `Hexalith.Commons.ServiceDefaults/HexalithServiceDefaults.cs` supplies shared observability, health, discovery and resilience. Treating these as supporting primitives is a ratified ownership choice, not an invented package or implementation claim. |
| Builds owns version selection | `references/Hexalith.Builds/Props/Directory.Packages.props` selects EventStore **3.120.0**, toolkit **13.6.0-preview.1.261001-0243**, Aspire AppHost SDK **13.6.1**, Dapr runtime **1.18.2**, and CLI **1.18.0**, exactly as recorded in the new memlog entry. `PlatformVersionCatalog.cs` carries those evaluated selections. Reconciling file-entry and local image pins is consistent with the spec, which identifies the baseline SDK 13.6.0 and image tag 1.18.3 drift; those baseline values do not establish another approved version authority. |
| Local mTLS with separate control-plane and workload identities | Dapr documents self-hosted Sentry, certificate variables and namespace selection. The revised AD-9 distinguishes control-plane trust `localhost` from the workload SPIFFE trust `public`, with namespace `default`. That distinction agrees with the captured runtime failure and pinned v1.18.2 source, detailed below; matching control-plane settings does not mean using the control-plane domain in workload ACLs. [Primary Dapr mTLS documentation](https://docs.dapr.io/operations/security/mtls/) |
| Receiver ACLs match caller, path and HTTP verbs with deny defaults | Dapr's documented ACL model supports global and per-caller denial, trust domain, namespace, app ID, operation paths and `httpVerb`. The new requirement for explicit grants and no grants from inventory is implementable with that model. [Primary Dapr invocation ACL documentation](https://docs.dapr.io/operations/configuration/invoke-allowlist/) |
| Declarative subscription routing, scopes and dead letters | Dapr's Subscription resource supports the bound pubsub component, routes, `deadLetterTopic` and application scopes. The explicit subscriber binding requirement avoids inventing routes or turning publishers into receivers. [Primary Dapr Subscription specification](https://docs.dapr.io/reference/resource-specs/subscription-schema/) |
| Disable discovery while keeping delivery endpoints | Dapr documents `--disable-init-endpoints subscribe` to suppress the programmatic subscription discovery call. This supports the stated discovery change without requiring removal of message delivery routes. [Primary Dapr subscription-method documentation](https://docs.dapr.io/developing-applications/building-blocks/pubsub/subscription-methods/) |

## Good-spine assessment

- The additions fix shared decisions that independent owner and Platform work could otherwise choose incompatibly: hosting ownership, version authority, capability-to-server interpretation, assigned names, receiver permissions and subscriber intent.
- The rules are enforceable: validate/render before resources; bind names through owner-read keys; scope selected consumers; deny undeclared invocation; require explicit subscriber bindings; preserve shared readers and telemetry isolation; apply the same contract to projects, containers and signed plans while retaining lifecycle ownership.
- The optional v2 additions are identified as the approved contract extension. Their absence preserves inventories without fabricating invocation grants or subscriptions. The capability-scope default is stated explicitly.
- AD-9 retains its stable ID and strengthens its existing component-name and subscription rules. No new provider exception or hosted migration is introduced.
- Operational scope is explicit: matched local mTLS settings, preparation order, lifecycle and cleanup are decided; hosted policy migration and concurrent-run isolation are deferred as the spec requires. Those deferrals do not reopen the local binding or permission contract.
- The explicit evidence boundary is sound: neither model/fixture assertions nor catalog selection establish live enforcement, delivery, package release availability or image availability. Source changes and runtime qualification remain the implementation's obligations.

## Findings

### R1 — Workload trust domain conflated with control-plane trust — resolved

The original wording and this review's earlier common-identity assessment were insufficiently precise. `/tmp/story1-7-security-evidence/result.log` captured a failed intended-allowed invocation: HTTP 403, with caller `spiffe://public/ns/default/eventstore`. The retained Tenants configuration used `localhost` for both ACL trust fields. The sidecar and Sentry logs identify runtime **1.18.2**. This is direct evidence of the workload identity and failed authorization; it is not passing invocation evidence.

The revised distinction is supported by the exact runtime source:

1. `cmd/daprd/options/options.go` defaults control-plane trust to `localhost`, control-plane namespace to `default`, and honors the control-plane trust environment variable. [Dapr v1.18.2 runtime options](https://github.com/dapr/dapr/blob/v1.18.2/cmd/daprd/options/options.go)
2. `pkg/security/sentry.go` puts the control-plane trust domain into certificate requests only for control-plane services in the control-plane namespace. Ordinary application certificate requests leave that field unset. [Dapr v1.18.2 certificate requests](https://github.com/dapr/dapr/blob/v1.18.2/pkg/security/sentry.go)
3. Sentry's common validator assigns `public` when the request does not specify a trust domain. [Dapr v1.18.2 certificate-request validation](https://github.com/dapr/dapr/blob/v1.18.2/pkg/sentry/server/validator/internal/common.go)

AD-9 and `docs/platform-apphost-form.md` now require `localhost` for the authenticated control plane and `public` for this profile's workload ACL identities. The checked renderer sets the global and caller ACL trust fields to `public`, while keeping `mtls.controlPlaneTrustDomain` and sidecar control-plane environment settings at `localhost`. This resolves the architectural error without widening caller, route or verb grants. The successful corrected fixture run is recorded below.

No source edits were made during this review; only this review artifact was updated.

## Implementation evidence update — 2026-10-15

The temporary fixture path referenced in R1 was reused during reruns and now contains the successful corrected run; the original failed raw log is not retained. On the subsequent recheck, this reviewer read the retained [fixture result](../../../../implementation-artifacts/evidence/story-1-7/security/result.txt): runtime 1.18.2 and CLI 1.18.0, allowed invocation 200, denied caller/verb/path 403, declarative delivery, dead-letter delivery and owned-resource cleanup with no leftovers. The retained [effective runtime commands](../../../../implementation-artifacts/evidence/story-1-7/security/runtime-commands.json) include `--enable-mtls` and `--sentry-address` for all three fixture applications. This confirms evidence from the executed fixture; this reviewer did not rerun it. Zero programmatic registrations is not a claim of zero discovery probes. The fixture does not qualify released owner applications or package images. Other source-smoke results remain separately described in [Story 1.7 verification](../../../../implementation-artifacts/evidence/story-1-7/verification.md).

## Scoped contract recheck — physical topics, owner routes and CLI resources

**Verdict: PASS; no new findings.** The small AD-9/AppHost-document delta preserves established owner contracts and makes previously implicit distinctions enforceable.

| Delta | Reality check |
| --- | --- |
| Optional `subscription.topic` supplies a physical topic or pattern while inventory names remain logical | Memories' `EventIngestionController` combines `[Route("events")]` and `[HttpPost("ingest")]`; `Hexalith.Memories.Server/appsettings.Development.json` sets `EventStoreIntegration:Routing:Topic` to `memories-events`. EventStore's `ProjectionNotificationController` combines `[Route("projections")]` and `[HttpPost("changed")]`; its adjacent `ProjectionNotificationTopicAttribute.cs` supplies `*.*.projection-changed`. The manifests now preserve these exact physical values and routes without renaming the `projection-notifications` inventory entry. No topic or route was invented for architectural uniformity. |
| Dead-letter references stay inventory references | The new rule requires a same-pubsub-role inventory reference resolving to one concrete, distinct physical target. The schema keeps `deadLetter.topic` as the reference and makes only `subscription.topic` an optional physical override. `PlatformManifestValidator.cs` checks the same-role resolution, a single physical target, absence of `*`, and inequality with the source. `PlatformDaprBindings.cs` resolves that target before rendering `deadLetterTopic`. This prevents implementations from disagreeing about reference names versus physical stream names. |
| Actual CLI resources must include scoped subscriptions | AD-9 explicitly requires the rendered scoped files to reach each actual CLI resource path. `PlatformAppModel.cs` writes a dedicated subscription directory and assigns it through `DaprSidecarOptions.ResourcesPaths`; the renderer retains receiver app-ID scopes. This is an enforceable integration obligation, not a claim that writing YAML alone proves delivery through every final source/package graph. |
| Tenants API business invocation grants | `Hexalith.Tenants.Api/Program.cs` registers the gateway client with Dapr service invocation to app ID `eventstore`. `EventStoreGatewayClientOptions.cs` defaults to `api/v1/commands` and `api/v1/queries`; `EventStoreGatewayClient.cs` uses POST for both. The documented `tenants-api` grants therefore match owner code. No readiness grant is added, and the package gateway seed contract remains unchanged. |
| Redis pattern limitation remains explicit | In the pinned Redis component, `Subscribe` passes `req.Topic` directly to consumer-group creation and polling; polling reads the named stream with `XReadGroupResult`. The documented refusal to infer wildcard fan-out or concrete projection-notification delivery from `*.*.projection-changed` is supported by that source. [Redis component v1.18.2](https://raw.githubusercontent.com/dapr/components-contrib/v1.18.2/pubsub/redis/redis.go) |

The reviewed additions keep one mapping contract across publishers, receivers, dead-letter targets and effective CLI resources. They distinguish preserved owner subscription metadata from provider delivery support and fixture evidence from release qualification. No implementation files were edited during this recheck.
