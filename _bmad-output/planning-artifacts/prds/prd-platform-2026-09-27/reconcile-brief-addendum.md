---
title: Brief Addendum Reconciliation
status: complete
reviewed: 2026-09-27
source: ../../briefs/brief-platform-2026-09-27/addendum.md
---

# Brief addendum reconciliation

Compared the original [brief addendum](../../briefs/brief-platform-2026-09-27/addendum.md) with the current [PRD](prd.md), [PRD addendum](addendum.md), and accepted decisions in the PRD workspace's `.memlog.md`. This review checks preservation of source intent and constraints; it does not verify repository or deployment readiness.

## Result

No missing source requirements or material losses of useful context were found. The PRD preserves the source constraints and resolves its open product questions through later accepted decisions. Remaining architecture and readiness work has downstream owners and revisit conditions. No PRD or addendum correction is required from this reconciliation.

## User-supplied constraints

The vision and confirmed MVP scope preserve one common solution for development, local and CI testing, staging, and production, with shared hosting owned by Platform and domain behavior retained by modules. The seven-module MVP is unchanged: EventStore, Tenants, Parties, Folders, Projects, McpCli, and Memories, with required supporting components. Longer-term coverage of all Hexalith servers and components remains explicit rather than being silently narrowed to the MVP.

FR-1 through FR-4 preserve local Aspire use, a complete local environment, module-repository use of Platform, and minimum module environments. FR-10 preserves Kubernetes location `192.168.1.30`, staging publication under `hexalith.com`, and production under `tache.ai`. FR-6 preserves automatic production promotion after staging checks. FR-7 and FR-8 preserve automatic rollback after a failed production deployment, with the later-approved bounded verification and recovery policy. FR-10, FR-11, and NFR-3 retain separate staging/production data and credentials, with stronger user-confirmed access requirements.

Both documents continue to distinguish desired capabilities from verified deployment facts. The PRD success measures are acceptance targets, and the addendum identifies repository evidence as dated observations.

## Repository baseline

The PRD addendum's “Existing implementation context” preserves the Development-only, opt-in Works preview, sibling Works and nested EventStore paths, EventStore/admin/Operations and Dapr composition, and the limitation that this does not establish general selection or complete MVP integration. It preserves the existing Works rollback composition and migration parity gate, together with the existing Agents story's wiring/live-evidence responsibility.

The sixteen-reference inventory remains intact, as do the cautions that repository references are not runtime services or evidence of integration, that immediate module declarations did not yet include Platform, and that requested hosted configuration was not found in the root implementation. Exact source file lines and the Agents Story 5.6 identifier remain available through the linked original addendum. Their omission from the condensed baseline prose does not lose a requirement or create an ambiguity about the platform's target behavior.

## McpCli context

The canonical sibling `../mcpcli` location, absence from the inspected root submodule inventory, `hexalith` tool, decorated Contracts discovery, and EventStore command/query routing are preserved. The stdio-only documentation, deferred HTTP transport, empty catalog/enrollment observation, and unverified runtime readiness remain explicitly dated context.

FR-12 preserves local Aspire, staging, and production usage and clarifies later-approved enabled-module operations and selected-environment permissions. The implementation choice for hosted transport and process placement remains downstream work; the PRD does not treat the README as evidence that hosted integration already exists.

## Workspace and build policy

FR-2 and the addendum preserve the two active-root contexts: Platform-root work initializes Platform's direct declarations, while module-root work directly declares Platform and its other dependencies under the module's own `references/`. Nested Platform references stay uninitialized. The addendum also retains the ban on recursive or remote submodule updates and the policy for deinitializing accidentally initialized nested dependencies without treating the PRD workflow as authorization to change the current tree.

Active-checkout debugging, local project references/Debug assets, and CI/CD NuGet references/Release assets are all retained. The Works preview's fixed paths are not treated as a general implementation. The source's illustrative workspace layout is usefully replaced by the user's concrete Parties example, while schema and source-resolution choices remain architecture work.

The source's uncertainty over minimum dependencies and real services versus doubles was intentionally resolved by later decisions: each module developer owns one server list; domain modules require at least EventStore, Tenants, and Memories; Platform uses real services; isolated module tests may use lightweight doubles outside Platform. No source requirement mandated automatic dependency discovery or a generic substitution system.

## Assumptions and downstream decisions

The source's tentative integration-tester and operator personas are represented pragmatically by developers, automated CI workflows, and the user-confirmed recovery owner Administrator. The PRD does not promote the unquantified expectation of reduced hosting effort or configuration drift into a measured fact.

Candidate startup and deployment-recovery measures have become accepted policy values and acceptance evidence through coaching. The source did not require separate setup-effort or resource-consumption targets, so their absence as numerical commitments is intentional. SM-C5 records actual startup duration and overrides without claiming an unestablished improvement target.

The CI lifecycle is now defined through accepted cleanup requirements while its hosting mechanism remains assigned architecture work. Deployment checks, failure detection, recovery responsibilities, and backup restoration targets were explicitly decided later and are covered by FR-6 through FR-9, NFR-1, NFR-2, and their addendum rationale. The absence of an uptime percentage remains explicit.

## Hosted constraints and research

The addendum preserves that publication domains do not settle per-service addressing, ingress, DNS, or certificates; the server address does not establish cluster topology; and storage, capacity, backup mechanisms, credentials/configuration, and the relationship with Hexalith.Builds still require architecture work. The downstream table gives these work items owners and deadlines relative to implementation or production readiness.

Later approval permits supporting infrastructure to be shared subject to isolated application data and credentials. The existing shared Keycloak server and explicit production-user authorization are additional accepted constraints, not contradictions of the original brief.

Original external research remains accessible in the linked source addendum. The PRD addendum preserves its role as context rather than proof of installed-version compatibility or local-cluster readiness and adds source-grounded analysis for decisions made during coaching. No original research claim has been converted into an unsupported implementation guarantee.

## Follow-up

No source-reconciliation blocker or new product decision is identified. The planned quality and recovery/access reviews can proceed. Architecture must still produce and validate the mechanisms and readiness evidence already assigned in the PRD; this reconciliation does not satisfy those implementation checks.
