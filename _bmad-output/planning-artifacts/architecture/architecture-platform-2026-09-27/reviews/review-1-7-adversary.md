# Story 1.7 architecture adversarial review

Verdict: pass after the physical-topic recheck correction. AD-9 and the companion document now identify a subscription by receiver, resolved pubsub component and physical topic, and refuse duplicates. No actionable scoped findings remain. This is a contract review; it establishes no live mTLS enforcement, subscription delivery, package release or image availability.

## Scope

Reviewed the Story 1.7 additions to AD-9 in `ARCHITECTURE-SPINE.md` and their companion description in `docs/platform-apphost-form.md` against the frozen intent, constraints and edge-case matrix in `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`. Existing unrelated decisions and implementation correctness are outside this review.

The adversarial test was whether two units built independently could both follow the additions literally while producing incompatible local Dapr bindings or security policy. The following candidate pairs were tested; each incompatible choice is prohibited by the recorded contract.

| Independent units and attempted divergence | Contract that closes the seam |
| --- | --- |
| An owner SDK keeps its isolated `statestore` default while Platform injects a generated name into a convenient generic `Dapr:*` key that the SDK never reads. | AD-9 requires injection through the actual owner configuration keys and permits logical defaults only for isolated use. The resulting Platform composition would violate the binding rule. |
| An ACL renderer infers a POST permission from an interface's operation inventory while another renderer produces no grant from that same inventory. | Only explicit `invocationRules` supply caller app ID, full path and HTTP verbs. Inventory without the addition grants nothing, and every receiver has a deny-default policy. Both inference and a startup-driven permissive fallback fail the rule. |
| A subscription renderer treats every topic inventory as a subscription while an owner treats the same topic as publisher-only, or continues programmatic discovery alongside its declarative subscription. | An explicit subscription identifies the receiving server and delivery route; inventory alone creates none. Declarative mode preserves the dead-letter policy and disables programmatic discovery while retaining delivery endpoints. |
| Two component adapters assign separate names and scopes independently, or merge Memories telemetry with the shared EventStore store because both capabilities are state stores. | Platform resolves names once from capability and recovery class, injects the assigned names, and scopes components to selected consumers. Shared EventStore readers retain access and Memories telemetry remains separate. These explicit sharing and separation constraints override any convenience grouping. |
| A declaration validator interprets capability server scopes as app IDs while a renderer interprets them as owner server IDs, or one grants every server when the list is absent and another grants only a preferred server. | The contract names owner server IDs and defines absence as all enabled owner servers. Component scope is restricted to selected consumers. |
| The file AppHost and signed Builds path use separate security conventions: one runs authenticated Sentry/placement/scheduler and the other retains the old unauthenticated local-init control plane. | AD-9 applies the same local security rules to projects, containers and signed plans, names matching `localhost` control-plane trust domain and `default` namespace, and keeps Builds as version owner. The companion document expressly replaces the old local-init prerequisite with the authenticated control plane. |
| A control-plane adapter correctly uses `localhost` for Sentry verification while an independently written ACL renderer also uses `localhost` for caller identities, denying the self-hosted workload certificates issued in `public`. | The corrected AD-9 expressly separates the `localhost` control-plane domain from the `public` workload SPIFFE domain and retains namespace `default`. An ACL using the control-plane domain for workload matching now violates the contract. |
| One helper provisions and cleans the signed-run control plane while the runner does so independently, or a helper silently introduces a second canonical composition. | The canonical set and helper ownership gate reject another composition; signed plans preserve runner lifecycle ownership and cleanup. |

## Initial findings

None. The additions settle the consequential choices shared by the validator, renderer, owner SDKs and lifecycle adapters without requiring independently maintained naming algorithms or owner defaults: name assignment and resource preparation have one Platform owner.

Concrete generated names, file layouts, certificate transport and individual configuration option types remain implementation details to verify against the shared validated binding model and actual owner readers. The rules already prohibit a mismatch in those outputs; their correctness needs the Story 1.7 implementation and tests, rather than another architecture decision.

## Identity correction recheck

Rechecked the amendment after the parent reported that a live Dapr 1.18.2 allowed-call fixture exposed the previous domain assumption. The earlier single-domain wording did leave an interoperability error: a deny-default receiver could trust the control plane successfully and still deny every intended caller because its policy matched the wrong workload domain. The corrected AD-9 and companion document close that error explicitly; no remaining local security interoperability hole was found in this scoped contract recheck.

Supporting implementation inspection agrees with the corrected division: `PlatformDaprBindings.Render` emits `public` in both the ACL root and caller policies, with caller namespace `default`; `DaprSelfHostedMtls` and `PlatformDaprEndpointSubscriber` configure control-plane verification with `localhost` and namespace `default`. The architecture memlog records the runtime observation and retains Builds catalog ownership. This inspection checks consistency of the correction; this reviewer did not rerun the live fixture or independently establish its success.

## Physical-topic and owner-subscription recheck

Scope: the subsequent AD-9 and companion-document amendments for optional `subscription.topic`, physical dead-letter target resolution, replacement of existing owner subscriptions, generated CLI resource paths, and the confirmed reference-owner bindings. No implementation edits were made.

### S17-ADV-01 — Name duplicate subscription identity after resolving aliases

Severity: medium. Status: closed after independent reread of the parent-authored correction in AD-9 and the companion document. The current validator already implements the intended rejection.

- **Location:** AD-9, Local bindings; `docs/platform-apphost-form.md`, local subscription conventions.
- **Independent pair:** one declaration author supplies inventory `incoming-events` with role `events`, receiver `server`, physical topic `orders` and route `/orders`; another supplies distinct inventory `order-notifications` with that same role, receiver and physical topic but route `/notifications`. Both names and explicit bindings satisfy the new documented field semantics. A validator keyed by inventory name accepts both, while a renderer keyed by the resolved Dapr subscription identity cannot select one route without discarding the other declaration. Distinct generated Subscription names do not settle which physical delivery contract was intended.
- **Original gap:** AD-9 stated that it prevents duplicate subscriptions but its rules closed programmatic-versus-declarative duplication only. Adding physical topic aliases created a second duplicate identity that inventory-name uniqueness did not identify.
- **Applied correction:** Both reviewed documents now state: “A receiver, resolved pubsub component and physical topic identify one subscription; duplicate identities are refused.” The pre-resource validation rule continues to govern this refusal. Distinct inventory names and routes no longer permit the independent pair above. No naming algorithm or new provider rule was introduced.
- **Reality check:** `PlatformManifestValidator` already rejects duplicate receiver/pubsub-role/physical-topic bindings. Recording that existing behavior closes the cross-unit contract without broadening this story.

Other attempted divergences are closed by the amended rules:

| Attempt | Result |
| --- | --- |
| Source inventory `incoming` uses physical topic `orders`; its dead-letter reference names `failed`, whose physical alias is also `orders`. | Refused: distinct inventory names do not make the resolved physical target distinct from the physical source. |
| A dead-letter reference finds same-role candidates resolving to different physical destinations. | Refused: the target must resolve uniquely to one concrete physical topic; another role cannot disambiguate a same-role conflict by selection order. |
| A dead-letter destination resolves to a wildcard pattern. | Refused: the target must be concrete. A source pattern remains an owner-supplied source value, not evidence that the selected provider supports wildcard delivery. |
| A renderer uses the inventory name despite an explicit `subscription.topic` alias. | Prohibited: the optional value is the physical Dapr topic or pattern; inventory-name fallback applies only when absent. |
| An owner disables programmatic discovery while omitting one of its established subscriptions, or the AppHost renders Subscription files without including them in the actual CLI paths. | Prohibited by the explicit replacement and actual-resource-path rules. Rendering a file alone does not satisfy them. |
| The gateway ACL grants broad or readiness access to make the Tenants API start. | Prohibited by explicit caller/path/verb grants. The companion records only `tenants-api` POST to `/api/v1/commands` and `/api/v1/queries`; readiness uses the separate resolved gateway HTTP health endpoint. |

The confirmed reference descriptions preserve Memories `memories-events` at `/events/ingest` and EventStore `*.*.projection-changed` at `/projections/changed`. The companion explicitly withholds a Redis wildcard fan-out claim and records the provider limitation; preserving that existing owner pattern creates no new provider exception. This review does not claim concrete projection delivery.

The package-graph boundary remains the required Builds gateway project seed plus the five owner containers. The latest wording does not invent or qualify an alternate container-only gateway mode, and no additional gateway-mode finding is raised.

## Limits retained

Hosted-policy migration and concurrent-run isolation remain explicitly deferred. Identical local trust-domain/namespace values across separate runs therefore are not reported as a new Story 1.7 gap. The review does not convert those deferrals, model assertions or fixture checks into acceptance evidence for live behavior.
