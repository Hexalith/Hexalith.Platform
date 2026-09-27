# Glossary

Product terms come from the PRD. Architecture terms point to their spine definitions and are not redefined here.

## Product terms

**Scope and composition**

- **Platform:** the shared Hexalith environment-provisioning and hosting solution this spec defines.
- **Module:** a Hexalith component whose domain behavior stays in its own repository.
- **Domain module:** a module that provides domain behavior. Its minimum environment is at least EventStore, Tenants and Memories; Parties is one example. Technical modules and tools are classified in the spine's Design Paradigm.
- **MVP module set:** EventStore, Tenants, Parties, Folders, Projects, McpCli and Memories.
- **Complete environment:** the MVP module set plus the supporting components needed to run it.
- **Module workspace:** a module's own repository, from which a developer edits, runs, tests and debugs that module. In the MVP, the domain modules are the ones debugged this way.
- **Platform workspace:** the Platform repository used as the active root. EventStore, Memories and McpCli are run and debugged from source there.
- **Active root repository:** the repository work is performed from. Its direct submodule declarations decide which dependencies are initialized.
- **Minimum environment:** the components a module developer declares as required for development or testing, in addition to the module itself.
- **Module configuration / declaration:** the module-owned, versioned input that lists the servers Platform enables. The spine's Module declaration convention enumerates its fields.
- **Enabled module:** a module whose services are enabled in the selected environment.

**Access and environments**

- **McpCli:** the CLI and MCP tool that exposes enabled modules' commands and queries. It routes them through EventStore and is governed by the selected environment's permissions.
- **Staging:** the hosted environment under `hexalith.com`, where checks gate production promotion.
- **Production:** the hosted environment under `tache.ai`.
- **Production user:** a user explicitly admitted to production. Being a staging user never confers this status or any production permissions.

**Testing**

- **Isolated test:** a module-owned unit or focused component test that runs without Platform and may use lightweight test doubles.
- **Readiness:** a service is initialized and able to handle its intended requests. A running process is not enough. Full business-flow validation belongs to integration and E2E tests.
- **Integration test:** a test that exercises a module against its configured real services in a Platform environment.
- **Critical business flow:** a business operation that its owning module designates as mandatory to verify with E2E tests before production promotion.
- **E2E test:** an end-to-end test that verifies a business flow through the deployed services that deliver its outcome.
- **Smoke test:** a short, production-safe check, chosen and maintained by a submodule, that verifies essential behavior after deployment or rollback.

**Release and recovery**

- **Working release:** an identified release, with compatible versioned configuration, that has passed production readiness and smoke verification.
- **Verification window:** the 5 minutes after rollout readiness, during which availability and smoke results are checked before deployment or recovery is declared successful.
- **Recovery owner:** Administrator, the product owner. Receives GitHub notifications and intervenes when automatic recovery fails or cannot be verified.
- **Recovery point:** a usable copy of authoritative data, together with the compatible application version and configuration needed to restore service. The spine's RRA "Recovery point and freshness" row gives the full definition.
- **RPO:** the maximum target age of recoverable data at the time of failure.
- **RTO:** the maximum target duration from outage to verified restoration, including detection, response, capacity, restore and validation.

## Architecture terms (spine)

**Roles**

- **Administrator:** the production authority, recovery owner and Platform architecture owner (spine Design Paradigm).
- **Recovery deputy:** backs Administrator for recovery and key custody (spine Design Paradigm).

**Composition and testing**

- **Composed `eventstore` host / gateway:** AD-13.
- **Platform runner, environment descriptor:** AD-10.
- **Platform tool:** AD-4; spine "Local tool and readiness".
- **Environment layer:** AD-3.
- **Extension package:** AD-13.
- **Intake manifest, change classification:** spine "Module intake" and "Module declaration".
- **Catalog generation, rollback combination:** spine "Catalogs"; AD-15.

**Release records and binding classes**

- **Release record:** AD-2.
- **Release-invariant, environment-current and attempt-bound values; attempt record; working baseline:** spine "Binding classes".
- **Release modes (automatic, Administrator-approved):** RRA "Release modes".
- **Promotion stop:** RRA "Automatic recovery".

**Identity and gates**

- **Surface class, synthetic identities:** AD-14; spine "Synthetic identities".
- **G1, G2, G3:** RRA "Production entry gates"; see [sequencing.md](sequencing.md).
