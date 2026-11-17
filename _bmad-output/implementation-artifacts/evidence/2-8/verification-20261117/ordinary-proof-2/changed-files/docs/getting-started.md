# Getting Started with Hexalith.Parties

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


This guide starts the seven-server reference environment with the pinned Platform
module tool for readiness checks and source debugging. The command/query, typed
.NET client, UI and MCP walkthroughs then use the retained legacy AppHost and its
authentication setup; those hosts are outside the reference environment.

**Time estimate:** Under 30 minutes from clone to first command/query round-trip.

> **GDPR notice:** The service logs the current MVP GDPR limitation at startup and emits `X-Hexalith-Parties-Mvp-Compliance-Warning` while `Parties:Compliance:GdprFeaturesActive` is not enabled. The MVP is not approved for regulated EU personal data until v1.1 GDPR features are active; do not store regulated personal data in local examples.

---

## Prerequisites

| Tool | Version | Download |
|------|---------|----------|
| .NET SDK | 10.0.403 (pinned in `global.json`) | [dot.net](https://dot.net) |
| Docker Desktop | Latest | [docker.com](https://www.docker.com/) |
| Git | Any recent version | [git-scm.com](https://git-scm.com) |
| `jq` | Latest, optional for Bash examples | [jqlang.org](https://jqlang.org) |

Install Aspire CLI **13.6.4** to match the catalog embedded in module tool **4.44.13**. The local tool manifest pins that module version; `dotnet tool restore` restores that exact version. The tool owns run-scoped infrastructure, readiness and diagnostics; its catalog requires Dapr CLI 1.18.2 and runtime 1.18.7. Set `HEXALITH_DAPR_HOME` to a prepared absolute isolated Dapr home using the [pinned Platform prerequisites](https://github.com/Hexalith/Hexalith.Platform/blob/0f4e27f1f78310bd731c10c19dc51bb1e0345d44/README.md#run-and-debug-a-technical-module); an unset or unprepared home is refused.

Verify your setup:

```bash
dotnet --version
docker --version
```

Docker Desktop must be running before you start the Aspire host.

---

## Step 1: Deploy Locally

```bash
git clone https://github.com/Hexalith/Hexalith.Parties.git
cd Hexalith.Parties
git submodule update --init references/Hexalith.Platform references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants references/Hexalith.Memories references/Hexalith.McpCli
dotnet tool restore
dotnet tool run hexalith-module -- debug --manifest module/hexalith.module-manifest.v2.json
```

The command waits for seven servers: `parties`, `eventstore`, `tenants`,
`tenants-api`, `memories`, `memories-access-telemetry-clock`, and `memories-access-telemetry`.
It uses only mapped direct projects and Debug assets. Missing selected source fails
with a diagnostic; sibling checkouts and packages are not fallback sources.
Do not initialize references inside those direct references.

Source preparation on 2026-10-31 succeeds with module tool 4.39.0 after
synchronizing the committed Platform identity. The tool derives its catalog
from the parent repository's committed Platform gitlink, then that Platform
commit's Builds gitlink. Stage selected source checkouts consistently and commit
the Platform selection before retrying a stale-catalog diagnostic. Runtime
topology qualification remains separate from successful build preparation.
Memories is a required reference service; optional rich Parties search remains disabled.
Stop only this run with `dotnet tool run hexalith-module -- down --manifest module/hexalith.module-manifest.v2.json --run-id <run-id>`.

**Legacy hosting scope:** the retained `Hexalith.Parties.AppHost` command
`aspire run --apphost src/Hexalith.Parties.AppHost` remains available for
existing owner tests and UI/MCP continuity. The legacy Keycloak/admin/UI/MCP/sample
hosts are outside Platform's reference composition and have not been extended or retired.
The remaining browser and MCP walkthroughs describe those legacy surfaces.

EventStore owns public authentication, tenant validation, RBAC, command/query routing, and generic response mapping. Parties owns domain execution and SDK projection/query handlers behind the domain-service host. Do not call Parties internals to manage tenant lifecycle, RBAC, authorization, read models, or domain invocation.

The Parties DAPR sidecar is deny-by-default. Only the `eventstore` app ID may
invoke the exact POST SDK routes (`/process`, `/query`,
`/admin/operational-index-metadata`, `/project`, `/project/v2`,
`/project/v2/reconcile`, `/replay-state`, `/project/rebuild/v1`,
`/project/rebuild/shared/v1`, `/project/rebuild/stage/v1`,
`/project/rebuild/commit/v1`, `/project/rebuild/abort/v1`, and
`/project/rebuild/verify/v1`). These are internal service-invocation endpoints,
not an alternate public API. The `accesscontrol.eventstore-admin.yaml`
component is an explicit exception (`defaultAction: allow`); see
[event-publishing.md](event-publishing.md).

The tool reports readiness only after all seven declared servers pass `/ready`.
Use its run ID, readiness result and preserved diagnostics to assess startup;
the default workflow does not require a browser dashboard. On failure, check
Docker Desktop, the prepared Dapr home and the selected direct references named
by the diagnostic. A partially started graph is not ready.

Memories is required in this reference environment. Parties rich search remains
disabled; `EnableMemoriesSearch` belongs to the retained legacy AppHost and does
not select Platform services. The HTTP resources expose `/ready` for readiness,
`/health` for full health, and `/alive` for liveness. Attach your debugger to the
Parties service child process identified by the tool to inspect the active source.

---

## Step 1b: Container Publication and Runtime Deployment

The historical in-repo Kubernetes walkthrough has been retired. This repository now publishes only the Parties-owned container images to Zot through GitHub Actions:

- `registry.hexalith.com/parties`
- `registry.hexalith.com/parties-mcp`
- `registry.hexalith.com/parties-ui`

Runtime deployment manifests, platform dependencies, Dapr components, ingress, secrets, and promotion policy are owned by the external deployment orchestrator. See [deployment-guide.md](deployment-guide.md) and [ci.md](ci.md).

---

## Step 2: Configure Tenant and Auth

**Legacy walkthrough (the following steps):** stop the tool run with its exact
`down --run-id` command, then start the retained AppHost using the legacy command
in Step 1. The following dashboard ports and Keycloak token examples belong to
that host. The default seven-server tool composition uses per-run development
authentication and starts no `security`, UI, admin or MCP resource; it does not
provide the Keycloak credentials used below.

Provision or use an active Hexalith.Tenants tenant membership before the first Parties call:

1. Create or use an active tenant in Hexalith.Tenants.
2. Add the local user as a tenant member.
3. Assign `TenantContributor` for create/update flows, or `TenantReader` for read/search-only flows.
4. Confirm Parties is subscribed to tenant events through DAPR pub/sub.

Set the EventStore gateway URL and tenant identifier before the first call. Copy the `eventstore` HTTPS port from the Aspire dashboard.

```bash
export EVENTSTORE_URL=https://localhost:<eventstore-port>
export TENANT_ID=tenant-a
```

```powershell
$env:EVENTSTORE_URL = "https://localhost:<eventstore-port>"
$env:TENANT_ID = "tenant-a"
```

Fetch a bearer token from the local Keycloak realm. Replace `<security-port>`, `<client-id>`, and `<client-secret>` with the values shown for the `security` resource in the Aspire dashboard:

```bash
export TOKEN=$(curl -s -X POST \
  "https://localhost:<security-port>/realms/hexalith/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=<client-id>" \
  -d "client_secret=<client-secret>" \
  -d "scope=openid" | jq -r .access_token)
```

```powershell
$tokenResponse = Invoke-RestMethod -Method Post `
  -Uri "https://localhost:<security-port>/realms/hexalith/protocol/openid-connect/token" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{
    grant_type    = "client_credentials"
    client_id     = "<client-id>"
    client_secret = "<client-secret>"
    scope         = "openid"
  }
$env:TOKEN = $tokenResponse.access_token
```

If you disable Keycloak for local development, use the repository's symmetric-key development token settings and keep the issuer, audience, tenant claim, and permission claims aligned with EventStore gateway expectations. Verify the environment variables are populated (`echo $EVENTSTORE_URL $TENANT_ID` / `echo $env:EVENTSTORE_URL $env:TENANT_ID`) before running the curl examples in Step 3.

---

## Step 3: First Command

All public write traffic goes through EventStore:

- Method: `POST`
- Route: `/api/v1/commands`
- Domain: `party`
- Command type: concrete Parties contract type, such as `Hexalith.Parties.Contracts.Commands.CreateParty`

**Bash**

The `-w` flag echoes the HTTP status alongside the body so non-JSON error responses are visible. Add `-k` only if the local dev certificate is not trusted (see Troubleshooting below for the recommended `dotnet dev-certs https --trust` fix).

```bash
curl --fail-with-body -sS -w '\n[HTTP %{http_code}]\n' -X POST "$EVENTSTORE_URL/api/v1/commands" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "messageId": "cmd-demo-party-001",
    "tenant": "'"$TENANT_ID"'",
    "domain": "party",
    "aggregateId": "party-demo-001",
    "commandType": "Hexalith.Parties.Contracts.Commands.CreateParty",
    "payload": {
      "partyId": "party-demo-001",
      "type": "Person",
      "personDetails": {
        "firstName": "Demo",
        "lastName": "Contact"
      }
    },
    "correlationId": "cmd-demo-party-001"
  }'
```

**PowerShell**

```powershell
$body = @{
  messageId = "cmd-demo-party-001"
  tenant = $env:TENANT_ID
  domain = "party"
  aggregateId = "party-demo-001"
  commandType = "Hexalith.Parties.Contracts.Commands.CreateParty"
  payload = @{
    partyId = "party-demo-001"
    type = "Person"
    personDetails = @{
      firstName = "Demo"
      lastName = "Contact"
    }
  }
  correlationId = "cmd-demo-party-001"
} | ConvertTo-Json -Depth 8

Invoke-RestMethod -Method Post -Uri "$env:EVENTSTORE_URL/api/v1/commands" `
  -ContentType "application/json" `
  -Headers @{ Authorization = "Bearer $env:TOKEN" } `
  -Body $body
```

Expected result: EventStore accepts the command and returns a correlation id. Command acceptance is not a read-your-write guarantee; projections may need a short moment before query results reflect the event.

The command -> event -> projection flow is: EventStore authenticates and authorizes the request, routes the command envelope to the Parties SDK domain-service host, Parties validates and emits domain events, EventStore persists and publishes those events, and Parties SDK projection handlers update read models that SDK query handlers use. Use the returned correlation id when checking logs or EventStore Admin UI evidence.

---

## Step 4: First Queries

All public read traffic goes through EventStore:

- Method: `POST`
- Route: `/api/v1/queries`
- Domain: `party`

### Get a Party

**Bash**

```bash
curl -s -X POST "$EVENTSTORE_URL/api/v1/queries" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "tenant": "'"$TENANT_ID"'",
    "domain": "party",
    "aggregateId": "party-demo-001",
    "queryType": "PartyDetail",
    "entityId": "party-demo-001"
  }' | jq
```

**PowerShell**

```powershell
$body = @{
  tenant = $env:TENANT_ID
  domain = "party"
  aggregateId = "party-demo-001"
  queryType = "PartyDetail"
  entityId = "party-demo-001"
} | ConvertTo-Json -Depth 6

Invoke-RestMethod -Method Post -Uri "$env:EVENTSTORE_URL/api/v1/queries" `
  -ContentType "application/json" `
  -Headers @{ Authorization = "Bearer $env:TOKEN" } `
  -Body $body
```

### Search Parties

```bash
curl -s -X POST "$EVENTSTORE_URL/api/v1/queries" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "tenant": "'"$TENANT_ID"'",
    "domain": "party",
    "aggregateId": "parties",
    "queryType": "PartySearch",
    "entityId": "parties",
    "payload": {
      "query": "Demo",
      "page": 1,
      "pageSize": 20
    }
  }' | jq
```

### List Parties

```bash
curl -s -X POST "$EVENTSTORE_URL/api/v1/queries" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "tenant": "'"$TENANT_ID"'",
    "domain": "party",
    "aggregateId": "parties",
    "queryType": "PartyIndex",
    "entityId": "parties",
    "payload": {
      "page": 1,
      "pageSize": 20,
      "active": true
    }
  }' | jq
```

### Freshness and Eventual Consistency

Queries read projection state, not the write path directly. Immediately after a successful command, a first query can briefly miss the new party or include freshness metadata showing the projection is rebuilding or stale. Retry with bounded backoff, prefer the `PartyDetail` query for the created party id when checking the first round trip, and use EventStore Admin UI stream evidence when you need to distinguish a rejected command from a delayed projection.

---

## Step 5: Typed .NET Client

For .NET service-to-service integration, prefer the typed client package. It submits the same EventStore command/query envelopes internally.

```bash
dotnet add package Hexalith.Parties.Client
```

```csharp
using Hexalith.Parties.Client.Extensions;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

builder.Services.AddPartiesClient(builder.Configuration);
```

```json
{
  "Parties": {
    "BaseUrl": "https://localhost:<eventstore-port>",
    "Tenant": "tenant-a"
  }
}
```

`Parties:BaseUrl` is the EventStore gateway base URL. It is not the `parties` domain-service host URL.

```csharp
public sealed class MyService(
    IPartiesCommandClient commands,
    IPartiesQueryClient queries)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        string correlationId = await commands.CreatePartyAsync(
            new CreateParty
            {
                PartyId = "party-demo-001",
                Type = PartyType.Person,
                PersonDetails = new PersonDetails
                {
                    FirstName = "Demo",
                    LastName = "Contact"
                }
            },
            cancellationToken);

        PartyDetail party = await queries.GetPartyAsync("party-demo-001", cancellationToken);
        PagedResult<PartySearchResult> results = await queries.SearchPartiesAsync("Demo", 1, 20, cancellationToken);
    }
}
```

---

## Step 6: MCP Host

AI assistants connect to the separate `parties-mcp` host, not to `/mcp` on the `parties` domain-service host.

Canonical tools:

- `create_party`
- `get_party`
- `find_parties`
- `update_party`
- `delete_party`

Use the `parties-mcp` endpoint shown in the Aspire dashboard. Replace `<parties-mcp-port>` with the parties-mcp HTTPS port from the Aspire dashboard, `<token>` with the bearer token from Step 2, and `<user-id>` with the authenticated user's identifier.

```json
{
  "mcpServers": {
    "hexalith-parties": {
      "url": "https://localhost:<parties-mcp-port>/mcp",
      "headers": {
        "Authorization": "Bearer <token>",
        "X-Tenant-Id": "tenant-a",
        "X-User-Id": "<user-id>"
      }
    }
  }
}
```

The MCP host forwards tenant, user, and authorization context to the EventStore gateway through the typed Parties client boundary. It does not call the Parties domain-service host, its projection or query handlers, validators, controllers, or DAPR service invocation directly.

---

## Step 7: Event Subscription

Subscriber applications consume EventStore-published DAPR events. The sample app owns its subscription and local read model:

- Subscription route: `/events/parties`
- Example topic: `tenant-a.parties.events`
- DAPR app id: `sample`

EventStore persists party events before publishing them to DAPR pub/sub. If publishing fails after persistence, drain/recovery processing retries the persisted event; a subscriber failure before acknowledgement can therefore result in the same envelope being delivered again. Subscriber code should persist processed event ids or otherwise be idempotent, tolerate duplicate delivery, acknowledge unknown additive event types, and define its own local event envelope type matching only the fields it consumes. Unknown future events should return success unless the subscriber explicitly owns a retryable failure.

---

## Non-.NET Developer Path

Non-.NET consumers can call the EventStore HTTP gateway directly with the command/query shapes above. Keep the same boundary:

| Operation | Method | Gateway route | Domain | Type |
|-----------|--------|---------------|--------|------|
| Submit command | POST | `/api/v1/commands` | `party` | `commandType` names a Parties command contract |
| Submit query | POST | `/api/v1/queries` | `party` | `queryType` is `PartyDetail`, `PartyIndex`, or `PartySearch` |
| Browse streams | Browser | EventStore Admin UI | n/a | Use `eventstore-admin-ui` from Aspire |
| AI tool access | MCP | `parties-mcp /mcp` endpoint | n/a | Use canonical MCP tool names |

---

## Troubleshooting

### SSL/TLS Certificate Problems

Local Aspire-issued dev certificates must be trusted before curl or HttpClient can reach `https://localhost`. Run `dotnet dev-certs https --trust` once per developer machine. On WSL or CI runners without that command, add `-k` (curl) or set `HttpClientHandler.ServerCertificateCustomValidationCallback` to a development-only override.

### EventStore Gateway Not Ready

For the default tool run, inspect its readiness result and diagnostics for all
seven servers listed in Step 1. For the legacy walkthrough, check the Aspire
dashboard for `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`,
and `tenants`. Public command/query readiness is EventStore-owned; Parties health
only proves the SDK domain-service host is alive behind the gateway.

### Docker or Submodule Startup Failures

For a default tool-run failure, follow the named resource and cause in its
diagnostics, confirm Docker Desktop is running, and check the direct-reference
command and Dapr prerequisites from Step 1. Keycloak and dashboard failures
belong to the legacy walkthrough. Do not use --recursive for either local path;
leave nested references uninitialized.

### Authentication Errors

`401` responses usually mean the request is missing a valid bearer token, tenant claim, or user identifier. Fix the identity provider or local development token configuration rather than calling Parties internals.

### Authorization Errors

`403` responses mean EventStore or its tenant/RBAC plug-ins rejected the request before Parties domain execution. Verify Hexalith.Tenants lifecycle, membership, and roles. `TenantReader` can read/search, `TenantContributor` can write, and `TenantOwner` can perform administration operations.

### Projection Delay

A command can be accepted before the projection is queryable. Retry with bounded backoff and check projection freshness or EventStore Admin UI stream evidence. Do not bypass EventStore by invoking SDK query/read-model internals directly.

### MVP Compliance Boundary

This MVP is not approved for regulated EU personal data until the production KMS gate is met. `Parties:CryptoShredding:IsEnabled` is already enabled by default, but the current `LocalDevKeyStorageBackend` is in-memory and dev-only; a production KMS or secret-store-backed key provider must replace it before any real EU PII is processed. For evaluation, use synthetic names and non-sensitive contact data only. If sensitive data is accidentally entered in a non-production evaluation environment, stop using that dataset and follow your operator's manual deletion or environment rebuild procedure; do not treat the MVP as an erasure-complete system.

The warning remains non-dismissable in service startup logs and response metadata through `X-Hexalith-Parties-Mvp-Compliance-Warning`. It can only be removed by explicitly setting `Parties:Compliance:GdprFeaturesActive=true` after the v1.1 GDPR feature set is active.

### MCP Unavailable

Verify the `parties-mcp` resource is running and configured with the EventStore gateway base URL. Do not route assistants to `/mcp` on the `parties` domain-service host.

---

## What's Next

- Use the typed .NET client for normal service integration.
- Use `parties-mcp` for AI assistant integration.
- Use EventStore Admin UI for stream/event inspection.
- Review the picker and admin portal docs for UI embedding patterns.
