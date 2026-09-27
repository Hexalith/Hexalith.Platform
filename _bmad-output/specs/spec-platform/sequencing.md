# Sequencing

These are the ordering constraints taken from the spine, which is authoritative, and from the PRD. Spine rows keep the spine's own names so they can be traced back. "Blocks" columns are derived from the spine's Capability → Architecture Map; if the spine changes, re-derive this file.

## Production entry gates

The gates run strictly in the order G1 → G2 → G3. The PRD's "before production use" means G2; "before enabling automatic production promotion" means G3. Before G3, every production release uses the **Administrator-approved** release mode (spine Release modes). The staging gate, lock, provenance, verification and records apply in both modes. SM-5 fault rehearsals on a staged release may run before G2 in approved mode.

### G1 — Production deployed, ingress closed

**Permits:** deployment into the production application namespace with ingress closed:

- Production hostnames admit only executor and probe sources.
- The production admission group is empty.
- The off-site probe runs from G1 onwards, sending pre-G2 notifications.

**Required first** (spine Deferred rows):

- **Infrastructure currency.**
  - Kubernetes moves off 1.34 (end of life 2026-10-27) to a supported minor.
  - OpenBao runs a patched release.
  - Redis Stack 7.4 moves to Redis 8 through the Memories digest set.
  - Keycloak runs a supported minor.
  - CloudNativePG, both PostgreSQL instances, FalkorDB and the Dapr control-plane patch are current.
- **Exposure, DNS and certificates.** Each environment's public exposure path; the DNS zone owners; the ACME challenge and its credentials; the internal executor verification endpoint; the registry's failure domain and off-site replica; the off-site monitor host and its dead-man check.

**Checks at G1:** the NFR-3 negative checks that need neither an admitted user nor open ingress:

- staging pods cannot reach production services, sidecars or data;
- a staging release that declares a production hostname is rejected.

**Blocks:** production-side rehearsal paths for CAP-7 and CAP-8; the production half of CAP-10, which stays closed.

### G2 — Users admitted, ingress open

**Permits:** admitting production users and opening production ingress.

**Required first:**

- The AD-12 drill passes, including Memories tombstone continuity.
- The real fence steps have been rehearsed on staging.
- **Recovery capacity and coverage:** replacement capacity and its location; declared response arrangements; the recovery executor host and its credential custody; artifacts, access and keys available independently; the key-custody mechanism; a telemetry sink with minimum retention; a capacity budget; representative data sizes.
- **Memories conformance and continuity:** tombstone continuity; the tenant-key store; per-tenant principals and the operator artifact; the adapter boundary; the remaining Memories Redis coordination migrated to Dapr (transitional until G2 under AD-9).

**Evidence:** SM-6 before opening; SM-4 completes during the opening order.

**Opening order:**

1. The AD-12 drill has verified both access directions in the restored environment: an authorized production user is allowed, and staging-only users and staging credentials are denied.
2. Open ingress and admit the first user (e.g. Administrator). This is an access-configuration change, so SM-4 must be re-verified.
3. On the live production realm, run the SM-4 positive check with that user, and confirm that staging users and staging-issued tokens are rejected through public ingress.
4. Admit anyone else only after step 3 passes. If it fails, G2 aborts and the admitted user is removed.

**Blocks:** acceptance of CAP-9; CAP-10, CAP-11 and CAP-12 in production.

### G3 — Automatic promotion

**Permits:** automatic production promotion.

**Required first:**

- The SM-5 rehearsals pass.
- **Release state, checks and notifications:**
  - EventStore confirms the AD-15 retention entries, the catalog activation order and renewed production-promoted records.
  - Interruption, epoch and concurrency behavior, the one-rollback rule, the timers and the rehearsal all work.
  - Provenance and stale-attempt detection are in place.
  - The executor runner is updated, with version monitoring.
  - Delivery of real GitHub issues is confirmed.
- The release and attempt record encoding exists (First shared versions table below).

**Evidence:** SM-5.

**Blocks:** the automatic path CAP-6 → CAP-7 → CAP-8.

## First shared versions

Module adoption epics wait for the artifacts they consume (spine Deferred, verbatim). The "Blocks" column is derived.

| Artifact | Owner | Consumers | Must precede | Blocks |
| --- | --- | --- | --- | --- |
| Enrollment declaration schema and validator (adopt or supersede Projects' `hexalith.module.v1`) | Platform | All modules | Any module enrollment | Every CAP |
| EventStore extension API with supported majors | EventStore | Folders, Agents, modules with extensions | AD-13 composed host | CAP-1, CAP-6–CAP-8, CAP-12 |
| Routing catalog schema with per-operation schema digests and retention entries | EventStore.Contracts | Platform, McpCli | Catalog generator, McpCli discovery | CAP-1, CAP-8 (AD-15), CAP-12 |
| Gateway metadata endpoint | EventStore | McpCli | FR-12 acceptance | CAP-12 |
| Realm contract with client-to-surface map | EventStore claims, Platform instance | All modules, McpCli | Local realm and hosted enrollment | CAP-1, CAP-10–CAP-12 |
| Test environment descriptor | Platform | EventStore.Testing, module and McpCli tests | Fixture migration | CAP-4 |
| Check-suite invocation and result contract | Builds | Modules, executors | Staging gate | CAP-6–CAP-8 |
| Recovery hook contract: quarantine admission, purge, rebuild, integrity, external-effect reconciliation, result shape | Platform | All modules | First AD-12 drill | CAP-9 |
| Profile template and per-release binding split | EventStore with Platform | Deployment workflows | Production profile proof | CAP-7, CAP-10 |
| Release and attempt record encoding | Builds | Publication and deployment workflows | Automated promotion | CAP-6–CAP-8 (G3) |

## Other ordering rules

**Build and release order (AD-13, AD-11)**

1. EventStore extension API and Contracts codec
2. Module Contracts and extension packages
3. Platform intake manifest
4. Composed `eventstore` host and McpCli candidate
5. Catalog generation
6. Release record and package
7. Staging validation
8. McpCli publication

**EventStore lifecycle states**

- Staging accepts candidates whose server package is evidence-validated.
- Production requires the package to be release-available.
- EventStore ratifies AD-13 once, registering Platform as the issuer for composed subjects.

**Before the first module enrolls**

- One canonical package set for the shared ServiceDefaults and Aspire Dapr helpers is recorded in the spine; new hosting helpers are refused until then.
- The local mTLS, per-receiver ACL and scoped-component convention is ratified.

**Before SM-2 can be demonstrated**

- Each domain module (Tenants, Parties, Folders, Projects) adds Platform as a direct submodule and a module declaration.
- McpCli's Platform-workspace path needs canonical McpCli enrollment in Platform's references (spine Source/package adoption).

**Before a module is enrolled in its environment and release checks (PRD)**

The module developer supplies:

- the server list and readiness checks;
- timeout overrides;
- public operations;
- critical-flow E2E suites and production-safe smoke suites;
- the authoritative-state inventory.

**PRD downstream-owner rows** (each keeps the PRD's own boundary)

- **Composition and testing** (configuration schema, source resolution, local lifecycle, CI hosting and cleanup): before the composition and testing implementation stories are finalized. The spine settles the mechanisms; the schema follows the First shared versions row 1.
- **McpCli** (enrollment, transport, placement, authenticated routing): before CLI/MCP implementation and acceptance testing.
- **Keycloak and isolation** (environment configuration, production-user administration, data and credential separation): before hosted-access implementation and isolation verification.
- **Production deployment** (ingress, DNS and certificates; release identity and promotion; rollback compatibility evidence; diagnostics; GitHub recipient and delivery): before production deployment rehearsals.
- **Recovery** (topology, storage and data volume; backup mechanism and location; shared-dependency recovery including Keycloak; response arrangements; replacement capacity; whole-site coverage): before production use and the first timed recovery exercise. Administrator confirms each shared dependency's recovery owner.

**Before Folders joins a Platform environment**

- The extension API is published.
- The Folders adapters move into a package.
- The composed host and its lifecycle-subject registration are built and qualified.

**Before hosted readiness** (spine "Secrets, identity, network and transport")

- **Identity:** Keycloak inventory; the realm contract with its event-export channel and retention; token-exchange clients; Administrator MFA, break-glass access and the recovery deputy.
- **Secrets:** per-environment OpenBao and tenant-key store; the existing OpenBao instance classified; its shared `openbao-runtime-bootstrap` token split per app and renewed before its 2027-07-19 expiry.
- **Network and credentials:** executor credentials or OIDC; proven CNI enforcement; Pod Security compatibility of data services and OpenBao; hostname admission; Dapr trust domains and workflow policies.
- **Isolation tests:** all negative isolation cases.

**Before FR-12 acceptance**

- McpCli's availability, token, actor and package/source-mode contracts are amended.
- Digest compatibility, freshness, token renewal and every required agent-eligible operation are proven.

**Before production profile proof**

- EventStore AD-26 is ratified; it is still unratified.
- The catalog and secret-contract instances exist.
- An approved durable production broker is qualified; Redis pub/sub is excluded.

**Composition path**

- The Aspire-to-Helm qualification uses Parties with EventStore, Tenants and Memories and applies AD-1's fallback trigger. It decides whether the generated chart or the maintained Helm fallback produces the retained package.

**Change order**

- An environment-layer change is its own attempt and goes to staging first.
- A staging failure blocks only that candidate.

**Legacy hosting retirement**

- Domain-module AppHosts stay frozen, with no new cross-module wiring, until the owning module shows source, package and deployed parity and holds EventStore AD-22 authority.
- Producers are adopted before consumers are retired.
