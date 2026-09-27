---
title: Platform PRD — Brief reconciliation
status: complete
reviewed: 2026-09-27
source: ../../briefs/brief-platform-2026-09-27/brief.md
---

# Brief reconciliation

Compared the original [platform brief](../../briefs/brief-platform-2026-09-27/brief.md) against the current [PRD](prd.md), [supporting addendum](addendum.md), and accepted later decisions in the PRD workspace's `.memlog.md`. This is source reconciliation, not a runtime or infrastructure assessment.

**Result: no missing brief requirements or contradictions found.** The PRD preserves the brief's scope and constraints, resolves its product questions with the recorded user decisions, and assigns the remaining implementation work to downstream owners.

## Requirement and intent coverage

| Original brief content | Current coverage | Assessment |
| --- | --- | --- |
| One common environment solution for development, testing, staging, and production | Vision; confirmed MVP scope; FR-1 through FR-12 | Preserved across all required contexts. Platform remains the shared host and modules retain domain behavior. |
| Reference and support all Hexalith servers/components, starting with the seven-module MVP | Confirmed MVP scope; FR-1, FR-10; addendum's existing implementation context | The seven named modules are unchanged. Broader support remains the longer-term scope; the initial list is explicitly not a permanent exclusion. |
| Complete-system local Aspire startup for testing/debugging | FR-1; FR-4; SM-1 | Preserved with measurable readiness/startup outcomes added during coaching. |
| Platform usable as a direct submodule in an individual module repository, with minimum composition | Individual-module development; FR-2, FR-3; SM-2; addendum's workspace/configuration sections | Preserved. Developer-owned dependencies and the Parties example resolve the brief's ownership question. |
| Local developer tests, including a complete environment when needed | FR-1, FR-4, FR-5; SM-1, SM-3 | Preserved. The accepted distinction between isolated module tests and Platform integration tests clarifies which tests need provisioned services. |
| Automated CI environments through Platform using established build/dependency rules | FR-2, FR-4, FR-5; SM-3 | Preserved, including CI lifecycle and cleanup requirements added during coaching. |
| Staging at `hexalith.com` and production at `tache.ai` on the designated Kubernetes installation `192.168.1.30` | Confirmed MVP scope; FR-10; SM-4 | Exact environments, domains, and address preserved. No node count or availability guarantee is inferred. |
| Separate staging/production application data and credentials despite shared hosting | FR-10, FR-11; NFR-3; SM-4 and SM-C2 | Preserved and strengthened by accepted environment-specific authorization rules and reuse of the existing Keycloak server. |
| Automatic production deployment after staging checks; automatic rollback to the previous working release on deployment failure | FR-6 through FR-8; NFR-1; SM-5 | Preserved. Module-owned E2E checks, failure detection, verified rollback, and notification details implement the user's later decisions. |
| McpCli provides CLI and MCP access through EventStore in local Aspire and hosted Kubernetes contexts | FR-12; SM-1, SM-4; addendum's McpCli context | Preserved. Operation discovery, enabled-module scope, selected-environment routing, and permissions are accepted refinements. Current implementation gaps remain explicitly distinguished from requirements. |
| Only direct `references/` submodules declared by the active root may be initialized; embedded Platform references remain uninitialized | FR-2, FR-3; SM-2; addendum's workspace/build constraints | Preserved for Platform-root and module-root work. No recursive initialization requirement has been introduced. |
| Local development/testing uses project references and Debug assets; CI/CD uses NuGet and Release assets | FR-2, FR-4; addendum's workspace/build constraints | Preserved without substituting a different dependency strategy. Source resolution remains an architecture decision. |
| Existing host is an optional Works preview rather than demonstrated full MVP composition | Addendum's existing implementation context | Baseline and limitations retained without presenting planned capabilities as implemented. |
| Five MVP success outcomes | SM-1 through SM-5 | All five are represented. SM-6 records the separately accepted disaster recovery outcome. |

## Original open questions

| Brief question | Disposition |
| --- | --- |
| Who defines minimum module dependencies? | Resolved in FR-3: each module developer owns the server list, with EventStore, Tenants, and Memories mandatory for domain modules. |
| May development configurations substitute test doubles? | Resolved in FR-3 through FR-5: Platform runs configured real services; isolated module tests outside Platform may use lightweight doubles. Alternatives and rationale remain in the addendum. |
| Which staging checks permit automatic promotion? | Resolved in FR-6: every required module-owned critical-business-flow E2E check must pass for the release being promoted. |
| How is production deployment failure detected and recovered? | Resolved in FR-7 and FR-8, with NFR-1 protecting data during application rollback. Specific deployment/rollback mechanisms remain assigned architecture work. |
| Which capabilities are reused from Hexalith build/release components? | Intentionally deferred as implementation integration: the addendum explicitly retains the relationship to Hexalith.Builds, while the downstream decisions table assigns release mechanisms to Platform architecture and implementation. |
| What downtime and recovery expectations apply? | User-selected rollback timing and disaster recovery RPO/RTO are recorded in FR-7 through FR-9 and NFR-2. The absence of an uptime percentage or round-the-clock response guarantee is explicit, with owner and revisit conditions. |
| What infrastructure may be shared without sharing data or credentials? | Resolved behaviorally in FR-10, FR-11, and NFR-3. Supporting infrastructure may be shared if isolation holds. Exact identity/data mechanisms remain architecture work. |
| Source resolution, deployment mechanism, CI lifecycle, and hosted McpCli access | Behavioral lifecycle and hosted-access requirements are now specified. Mechanisms have named downstream owners and revisit conditions. |

## Intentional refinements and nonissues

- The detailed production, backup, authorization, and test-lifecycle policies are supported by later user decisions. They are not unexplained scope inferred from the original brief.
- The user accepted module-owned business-flow definitions. Keeping their concrete business scenarios out of this central platform PRD preserves that ownership and does not omit a centrally specified flow from the brief.
- Isolated tests do not start Platform because that policy was expressly accepted after the brief. Local integration tests still use Aspire, and the complete supported environment remains available when needed.
- The brief's five demonstration outcomes have been converted to acceptance measures rather than claimed implementation results. Recovery targets are also labeled as accepted targets requiring evidence.
- Remaining configuration schemas, transport choices, hosting mechanisms, topology, capacity, and recovery feasibility evidence are assigned downstream work. Their deferral matches the brief's separation between requirements and architecture.

## Gaps requiring correction

None found from this source. The separate quality and recovery/access reviews may still identify internal requirement ambiguities; this reconciliation establishes fidelity to the brief, not that all implementation risks are resolved.
