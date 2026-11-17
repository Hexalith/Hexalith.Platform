# Hexalith.Parties

This Story 2.8 proof ref selects the already released module CLI **4.48.2**
with `rollForward=false`. Its manual lifecycle workflow uses the separate
immutable Builds executor `6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b` and
that tool's Aspire 13.6.5 / Dapr CLI and runtime 1.19.1 pins. The genuine
caller catalog `3c8096180ecf2aa539ac454815efbfceb8a92822`, Platform
`0f4e27f1f78310bd731c10c19dc51bb1e0345d44`, declaration, native suites,
and all runtime budgets remain unchanged. Only `cancel-execution` is an
authorized proof scenario here. This holds-only subject supplies no attached
native-suite or Story 2.10 qualification credit. The general instructions
below retain their original 4.44.13 subject.


Hexalith.Parties is a ready-to-deploy party management domain service for people and organizations. Public command and query traffic goes through Hexalith.EventStore; the `parties` service runs an EventStore SDK domain-service host behind that gateway, and consumers normally use the typed .NET client package. The solution also includes `parties-ui`, a Blazor Server browser UI/BFF for the Admin and Consumer experiences.

> **GDPR Notice:** Some GDPR infrastructure exists, and crypto-shredding is enabled by default, but the default key store is `LocalDevKeyStorageBackend` (in-memory, dev-only). **Do not store regulated EU personal data** until a production KMS or secret-store-backed key provider is provisioned. The MVP warning switch is separate from the crypto feature; see [deployment-guide.md](docs/deployment-guide.md) for the current runtime ownership boundary.

## Consuming the Parties Aspire helper

`Hexalith.Parties.Aspire` adds the separately built Parties server to a consuming
AppHost with `AddHexalithPartiesServer(eventStore, daprConfigPath)`. It delegates
state-store, pub/sub, health and sidecar wiring to `Hexalith.EventStore.Aspire`;
the package does not depend on the Parties server. The consumer supplies
Gateway routing, JWT validation and the app-channel token through platform helpers.
Source is resolved only in the owning repository or its direct
`references/Hexalith.Parties` checkout, and must be built before launch.
The helper is included in the normal protected release; adding source does not
mean a version is published or approved for downstream enrollment.

## Key Features

- **EventStore gateway** -- Public command/query ingress uses `POST /api/v1/commands` and `POST /api/v1/queries` with `Domain="party"`.
- **Parties domain-service host** -- The `parties` resource owns domain execution and SDK projection/query handlers behind EventStore.
- **Typed client package** -- `IPartiesCommandClient` and `IPartiesQueryClient` hide EventStore envelope plumbing for .NET consumers.
- **Parties UI/BFF** -- `parties-ui` is a Blazor Server host with FrontComposer, FluentUI, host-owned OIDC, role-gated Admin/Consumer areas, accessibility gates, and server-side token handling.
- **Admin portal RCL** -- `Hexalith.Parties.AdminPortal` provides the protected `/admin/parties*` browse/detail/create/edit/GDPR surfaces embedded by `parties-ui`.
- **Consumer portal RCL** -- `Hexalith.Parties.ConsumerPortal` provides the protected `/me*` Consumer profile, consent, data export, erasure, and processing-transparency surfaces embedded by `parties-ui`.
- **Embeddable party picker** -- `Hexalith.Parties.Picker` provides a Blazor/custom-element selector that searches through `IPartiesQueryClient` and emits durable party-id selections.
- **Separate MCP host** -- `parties-mcp` exposes `create_party`, `get_party`, `find_parties`, `update_party`, and `delete_party` through the typed client boundary.
- **DAPR event subscription** -- Subscriber apps consume EventStore-published party events with their own idempotent handlers.
- **EventStore Admin UI** -- Use `eventstore-admin-ui` for generic stream and event browsing.
- **.NET Aspire** -- One-command local topology with EventStore, Parties, Parties UI, Tenants, DAPR sidecars, Redis, and optional Keycloak.

## Quick Start

Set `HEXALITH_DAPR_HOME` to a prepared, absolute isolated Dapr home before
starting. Follow the [pinned Platform v1.7.1 runtime prerequisites](https://github.com/Hexalith/Hexalith.Platform/blob/0f4e27f1f78310bd731c10c19dc51bb1e0345d44/README.md#run-and-debug-a-technical-module);
the tool refuses an unset or unprepared home.

```bash
git clone https://github.com/Hexalith/Hexalith.Parties.git
cd Hexalith.Parties
git submodule update --init references/Hexalith.Platform references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants references/Hexalith.Memories references/Hexalith.McpCli
dotnet tool restore
dotnet tool run hexalith-module -- debug --manifest module/hexalith.module-manifest.v2.json
```

The repository pins module tool **4.44.13** alongside aspirate and uses Aspire CLI
**13.6.4**. From this independent checkout, `debug` resolves direct references
once, builds the selected
source projects in Debug, and waits for exactly `parties`, `eventstore`, `tenants`,
`tenants-api`, `memories`, `memories-access-telemetry-clock`, and `memories-access-telemetry`.
The tool reports its run ID and source mapping; attach the debugger to the Parties
service child process. Stop that exact environment with
`dotnet tool run hexalith-module -- down --manifest module/hexalith.module-manifest.v2.json --run-id <run-id>`.
Initialize only this checkout's direct references; leave their nested references
uninitialized. Ordinary package CI keeps package references and Release builds.

Source preparation on 2026-10-31 succeeds with module tool 4.39.0 after
synchronizing the committed Platform identity. The tool derives its catalog
from the parent repository's committed Platform gitlink, then that Platform
commit's Builds gitlink. Stage selected source checkouts consistently and commit
the Platform selection before retrying a stale-catalog diagnostic. Runtime
topology qualification remains separate from successful build preparation.

The owner ACL remains `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml`.
The renderer derives only `callers` and `invocationRules` from that ACL; its
`TEMPLATE` in `scripts/render-platform-declaration.py` owns all other declaration
fields. The strict parser accepts exactly thirteen unique POST routes from
EventStore with the existing deny defaults, trust domain and namespace. After an
owner-approved ACL change, run `python3 scripts/render-platform-declaration.py`;
adding or removing routes also requires an approved update to the parser's
thirteen-route invariant and its tests. Change `TEMPLATE` for non-ACL declaration
updates, then regenerate. CI requires
`python3 scripts/render-platform-declaration.py --check` and validates the generated
declaration with its reference declarations using the pinned tool.
Platform binds security storage through `Parties:Security:StateStoreName` and the
existing projection/mapping component keys. The declarative tenant subscription
uses `system.tenants.events` at `/tenants/events`; rich Memories search stays disabled.

**Legacy hosting scope:** `Hexalith.Parties.AppHost`, Aspire helpers, UI, proprietary
MCP and sample hosts remain for existing owner tests and continuity. They are frozen
and are outside this Platform qualification. The legacy local command is
`aspire run --apphost src/Hexalith.Parties.AppHost`; its Keycloak, admin and
UI topology is separate from the seven-server minimum above. Retirement requires
owner-approved parity and consumer-removal gates.

> **Prerequisite - tenant access state.** Provision or use an active Hexalith.Tenants tenant membership before the first Parties call. EventStore owns public authentication, tenant validation, RBAC, command/query routing, and generic response mapping. Parties consumes the authorized command/query behind the domain-service host and does not manage tenant lifecycle or roles itself.

The internal DAPR access-control policy is deny-by-default and permits only the
`eventstore` app ID to invoke the SDK domain routes: `/process`, `/query`,
`/admin/operational-index-metadata`, `/project`, `/project/v2`,
`/project/v2/reconcile`, `/replay-state`, `/project/rebuild/v1`,
`/project/rebuild/shared/v1`, `/project/rebuild/stage/v1`,
`/project/rebuild/commit/v1`, `/project/rebuild/abort/v1`, and
`/project/rebuild/verify/v1`. One documented exception: `accesscontrol.eventstore-admin.yaml`
is `defaultAction: allow` at the top level, because local self-hosted DAPR does
not reliably expose caller identity without mTLS (see
[`docs/event-publishing.md`](docs/event-publishing.md) for the full rationale).
Runtime deployment orchestration is externally
owned; the external orchestrator owns manifests, environment secrets, ingress,
promotion, and rollback. This repository publishes immutable workload images
and retains only local AppHost wiring.

See the [Getting Started Guide](docs/getting-started.md) for the full EventStore-fronted walkthrough.

## Documentation

- [Getting Started Guide](docs/getting-started.md) -- Deploy and send your first EventStore-fronted command in under 30 minutes
- [Accessibility Contract](docs/accessibility.md) -- Parties UI WCAG 2.2 AA guardrails and test expectations
- [Tenants Access Projection](docs/tenant-access-projection.md) -- Event-driven local tenant access state, consistency window, and fail-closed rules
- [Embeddable Party Picker](docs/frontend/party-picker.md) -- Blazor/custom-element picker integration for consuming applications
- [Architecture Overview](_bmad-output/planning-artifacts/architecture.md) -- System topology and design decisions
- Event streams -- Browse through the EventStore Admin UI resource in the Aspire dashboard

## Project Structure

The repository contains exactly **15 projects under `src` plus one sample
project**, **17 runnable .NET test projects plus three support hosts and one contracts artifact**
(`Hexalith.Parties.EventStoreGateway.TestHost`, `Hexalith.Parties.EventStoreAdminUI.TestHost`, `Hexalith.Parties.EventStoreSample.TestHost`
and `Hexalith.Parties.EventStoreSample.Contracts.TestArtifact`), with Playwright e2e coverage
alongside them. Adopter-facing packages (left column) are the only modules
consumers normally reference directly. Modules under
"Internal" are private to the domain-service host and not adopter-facing
dependencies — do not reference them from consumer applications.

```
Hexalith.Parties/
  src/
    Hexalith.Parties.AppHost/        # Aspire orchestration (entry point, dev-only)
    Hexalith.Parties.Aspire/         # platform composition helper; no server package dependency
    # Adopter-facing
    Hexalith.Parties.Client/         # Typed EventStore gateway client (IPartiesCommandClient / IPartiesQueryClient)
    Hexalith.Parties.Contracts/      # Shared DTOs, commands, events, value objects
    Hexalith.Parties.AdminPortal/    # Protected Admin party records and GDPR RCL
    Hexalith.Parties.ConsumerPortal/ # Protected Consumer /me self-service RCL
    Hexalith.Parties.Picker/         # Embeddable Blazor/custom-element party picker
    Hexalith.Parties.UI/             # Blazor Server browser UI/BFF for Admin and Consumer experiences
    Hexalith.Parties.Mcp/            # Separate parties-mcp host over the typed client
    # Internal (domain-service-host private — not adopter-facing dependencies)
    Hexalith.Parties.Authentication/ # Internal claims-transformation library; packable but not adopter-facing. Retained as the rollback surface until 8.8 runtime-boundary cleanup proves G7/G9 parity
    Hexalith.Parties/                # SDK domain-service host and PartyAggregate behind EventStore
    Hexalith.Parties.Projections/    # SDK projection/query handlers and read models (internal)
    Hexalith.Parties.Security/       # GDPR payload protection and erasure infrastructure
    Hexalith.Parties.Testing/        # Test utilities
  tests/                             # Unit, integration, and architectural tests
  samples/
    Hexalith.Parties.Sample/         # Sample integration project
  docs/
    getting-started.md               # Step-by-step onboarding guide
  references/
    Hexalith.Builds/                 # Shared build configuration
    Hexalith.Commons/                # Shared platform helpers
    Hexalith.EventStore/             # Gateway/eventing submodule
    Hexalith.FrontComposer/          # UI shell submodule
    Hexalith.PolymorphicSerializations/ # Source generators / serialization helpers
    Hexalith.Tenants/                # Tenancy submodule
    Hexalith.Memories/               # Optional rich-search submodule
```

## Positioning

Hexalith.Parties manages **party records** -- people and organizations with contact channels and identifiers. It is **not** an authentication provider, CRM, or identity server. Use it as the party/contact data backbone behind your own application logic.

## Prerequisites

- [.NET 10 SDK](https://dot.net) (10.0.403)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Git

## License

This project is licensed under the [MIT License](LICENSE).

### Generic McpCli command declarations (local migration increment)

The Contracts assembly declares exactly `parties.create-party`,
`parties.update-party`, `parties.reactivate-party` and `parties.deactivate-party`.
The module uses String identifiers, domain `party`, the existing full CLR command
wire names, and root `PartyId` for aggregate routing. Tenant authority remains in
the gateway envelope. `Hexalith.McpCli.Abstractions` 1.0.0 supplies declaration
attributes only; the package/source pair follows `UseHexalithProjectReferences`.
This increment does not enroll or publish a module or retire the legacy host.

McpCli payloads use exact CLR property casing and typed nested values. For example,
`parties.create-party` accepts:

```json
{"PartyId":"party-49","Type":"Person","PersonDetails":{"FirstName":"Ada","LastName":"Lovelace"},"ContactChannels":[{"PartyId":"party-49","ContactChannelId":"email-49","Type":"Email","Value":"ada@example.test","IsPreferred":true}]}
```

Supply IDs explicitly; every child `PartyId` must equal the root. No alias mapping,
ID generation, person/organization inference or read/merge patch is performed.
McpCli PartyId values use ASCII letters or digits plus `.`, `_` or `-`, start
and end with an ASCII letter or digit, and contain at most 128 characters.
McpCli refuses legacy braced, parenthesized and X-format GUID PartyId values
before Gateway submission, although Parties accepts them. Preserving those
legacy IDs or approving a conversion remains a C07 retirement gate.
An update supplies complete `PersonDetails` or `OrganizationDetails` replacements
and/or typed add, update or removal lists. A `PartyId`-only update is rejected;
every `UpdateContactChannels` item must supply at least one of `Type`, `Value` or
`IsPreferred` (including `false`). Activation is a separate command. Repeated
soft deactivation is a safe no-op after lifecycle guards and never performs GDPR
erasure.

Both shared composite validators enforce a maximum **raw UTF-16 length of 512
per string and 4096 combined**, without trimming or rewriting values. Every root,
nested, child and removal string occurrence counts, including whitespace and
repeated IDs. The tighter support-safe 128-character rule applies to `PartyId`,
`ContactChannelId`, `IdentifierId` and removal ID entries. Business identifier
`Value` strings retain the 512 UTF-16-unit limit. This is stricter than legacy wrapper counting of trimmed fields; the total
budget can bind before the configured composite sub-operation cap (default 100).
Explicit null lists and null list items reject; omitted lists retain empty defaults.
Create counts 1 plus every item in `ContactChannels` and `Identifiers` toward
the operation cap. Update counts each non-null details object plus every item
in its five operation lists. Removing a channel ID also listed for addition or
update rejects the entire update; adding and removing the same identifier ID
also rejects. An existing channel ID listed for both addition and update has
its addition skipped and its update applied.
All these checks run before protected aggregate state is accessed.

The four local vectors in `tests/conformance-vectors/mcpcli-v1/` target only the
unpublished scratch package `Hexalith.Parties.Contracts` **0.0.0-story49**. They
include prerequisite writes, repeat deactivation and semantic `parties.get-party`
postconditions using the approved `PartyDetail` / `party-detail` /
`PartyDetailProjectionQueryActor` route. Those Query contracts do not exist in this
increment: postconditions remain unexecuted until Story 4.10, which must rebind
vectors to the complete immutable artifact. Shared-validator format and artifact
checks do not prove handler effects. Stories 4.11/4.13 own parity and persistent
semantic proof. Maintainer approval must name the exact artifact hash and shared
validator revision. C06 error privacy, C07 legacy-ID reachability and C08 credential
and cross-tenant authorization evidence remain replacement/retirement gates.

The detail kind on update must match the existing party type; an update cannot
change that type. Omitted/null details preserve the existing object, while a
supplied object replaces it completely. Create requires the detail kind matching
`Type`, but also accepts/stores an extra opposite-kind object without changing
`Type`. Business identifiers are added or removed; there is no identifier-update
list. Contact updates require at least one non-null mutable member: omitted/null
preserves that member, `IsPreferred: false` clears that channel's preference,
and `Value: ""` replaces the value with an empty string. All contact kinds,
including PostalAddress/SocialMedia, store a raw string; no structured format is
defined. Preference is scoped to channel type; supply `IsPreferred: true` with a
type change to reconcile preferences in the target type.

`OrganizationDetails.IsNaturalPerson` defaults to false and initializes party
classification on create. Existing detail replacement changes the nested flag
without updating the established party classification; these four operations do
not provide a classification-change command. Lifecycle operations require an
existing unrestricted party with current erasure status Active, including
when requesting an already-current active state.

### Generic McpCli read declarations (local candidate)

The Contracts candidate declares exactly seven operations: `parties.get-party`,
`parties.list-parties`, `parties.search-parties`, `parties.create-party`,
`parties.update-party`, `parties.reactivate-party` and `parties.deactivate-party`.
The complete candidate still requires fresh maintainer approval, exact-source CI,
and the protected owning release workflow before its published version can be
pinned in Builds and enrolled in McpCli. The existing five-tool MCP host remains
available; this declaration work does not authorize retirement.

Queries use domain `party` and envelope tenant authority. Detail uses wire type
`PartyDetail`, projection `party-detail`, historical compatibility name
`PartyDetailProjectionQueryActor`, and root `PartyId`. List and search use wire
`PartyIndex` / `PartySearch`, projection `party-index`, compatibility name
`PartyIndexProjectionQueryActor`, and aggregate `parties`. The current SDK
`PartyDetailQueryHandler`, `PartyIndexQueryHandler` and `PartySearchQueryHandler`
dispatch directly to canonical read models; actor compatibility fields do not
prove actor execution. Omitted EntityId falls back to AggregateId; supplied
EntityId must match before any read. String IDs retain existing Parties syntax;
McpCli still refuses legacy braced, parenthesized and X-format GUID IDs.

Erased detail has the same safe not-found outcome as missing detail. Inactive
detail stays visible. List accepts optional `Type` (Person or Organization),
`Active`, and inclusive `CreatedAfter`, `CreatedBefore`, `ModifiedAfter` and
`ModifiedBefore` instants with explicit offsets. Invalid instants and inverted
ranges reject. Search accepts `Query`, `Type` and `Active`; it trims the query
and uses case-insensitive, diacritic-normalized display-name matching, including
fuzzy matches. Empty/whitespace query returns no matches. Date filters, CaseId,
unknown fields and unsupported search modes reject rather than being ignored;
legacy Lexical/DisplayName mode names remain accepted. Duplicate case aliases
such as `Type` plus `type` reject. Filters and erasure suppression apply before
paging and totals exclude erased entries.

The new Query DTOs have no payload paging members. When envelope Paging is
present, it alone governs: size defaults to 20, position to offset 0, maximum
size 100, and offsets must align with the effective size. A nonblank cursor
cannot combine with an offset. Cursors bind query type, tenant, user, filters
and size; changed scope rejects before reading. Response paging reports effective
size/offset, filtered total, hasMore and nextCursor. When envelope Paging is
absent, the legacy payload Page/PageSize behavior remains (page defaults to 1,
size to 20, maximum 100; nonpositive values reject).

`tests/conformance-vectors/mcpcli-v1/` contains seven vectors bound to the local
`0.0.0-story410` artifact, with prerequisite creates and original write-vector
Query postconditions retained. Shared McpCli validation and both-head replay
check wire envelopes, filtered totals, paging and result assertions. Scripted
replay does not prove live authorization, persisted domain effects or C06–C08;
Stories 4.11/4.13 remain required before replacement or retirement.

### Platform-owned integration suite

`tests/Hexalith.Parties.Platform.IntegrationTests` consumes the absolute
`HEXALITH_TEST_ENVIRONMENT_PATH` through the EventStore shared descriptor fixtures. It has no
AppHost reference and never provisions services or creates credentials. The runner's private
identity channel must match the descriptor. Missing/unready handoffs fail without a successful skip.
The suite observes real Tenants through its declared REST interface, reads the synthetic Memories
tenant through its declared REST interface, creates and queries a Party, and verifies persisted
aggregate metadata and every event envelope in the run-owned state store.

From the Platform checkout, use the exact published module CLI 4.44.13 restored from its local tool manifest:

```bash
hexalith-module test --manifest references/Hexalith.Parties/module/hexalith.module-manifest.v2.json --profile platform-source --mode source
hexalith-module test --manifest references/Hexalith.Parties/module/hexalith.module-manifest.v2.json --profile platform-current-package --mode package
# Separate published-consumer purpose, after its public cohort is admitted:
hexalith-module test --manifest references/Hexalith.Parties/module/hexalith.module-manifest.v2.json --profile platform-package --mode package
```

The renderer owns both explicit suite declarations. Package execution requires authoritative
publications of the migrated EventStore testing packages; Debug source validation grants no package
acceptance. It also requires Platform's native owner preparation (the synthetic tenant and membership,
the native scope and the private Memories invocation channel), now enabled for the selected
Release/package cohort. CI selects the declared package suite through the pinned v2 workflow
after all fifteen isolated projects pass. Actual native service and cleanup qualification
remain required; source/model/protocol results do not establish hosted lifecycle acceptance. The existing `Hexalith.Parties.IntegrationTests` topology and infrastructure-control tests
remain a frozen compatibility lane. Unit and isolated integration projects retain their test-only
doubles and run without Platform/domain servers. `scripts/test.ps1` inventories the new project but
leaves its environment-owning execution to the declared runner suite. Attached execution, expiry
enforcement and termination qualification remain open follow-up work.

Isolated package probes use their test build configuration: Debug during local source work and
Release in the CI package lane. Each probe removes inherited source-mapping imports before it
restores an independent package consumer. Debug probes check package structure and APIs only;
the Release counterpart and authoritative publications remain required for package acceptance.

The `Hexalith.Parties.EventStoreExtensions` package carries the existing owner-declared canonical intent adapters for catalog composition. It is included in the public release inventory and remains nonpublishable as an application. Package delivery does not activate a catalog; each participating host must independently qualify and commit the bound runtime generation.

The catalog-enabled release cohort requires trusted catalog enrollment before a service becomes ready or accepts catalog dispatch. The run-only packaged test profile prepares the exact selected public packages through `scripts/prepare-aspire-runtime-hosts.sh` and supplies a private Development catalog. Production composition must provide the SDK authority and trust-provider configuration.

Public browser calls require the signed `eventstore:surface=ui` claim; agent calls use `agent` when the declared operation permits that surface. The local confidential `hexalith-parties-ui` OAuth client emits the UI claim alongside its existing audience and authorization claims. Configure the corresponding explicit client claim in an external identity provider; roles and request headers do not determine the caller surface.
