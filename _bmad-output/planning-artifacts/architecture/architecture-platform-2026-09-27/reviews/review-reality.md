# Reality and version review

Date: 2026-09-27

Verdict: **PASS.** The spine correctly separates adopted architecture, observed repository pins, installed-infrastructure observations, and unqualified implementation candidates. No unsupported production-compatibility claim or required technology upgrade was found. The one minor factual-precision finding was corrected by the parent agent and verified.

## Finding

**Resolved — Describe the observed node at the level actually established.** The initial Structural Seed said inspection found “one physical node with local storage.” The recorded inspection in `.memlog.md` line 64 establishes one Ready **Kubernetes Node**, its allocatable resources, and local storage classes; it does not separately establish bare-metal versus virtual-machine hosting. The spine now says “one Kubernetes node with local storage.” The conclusions that replica counts do not prove node resilience and recovery readiness remains unproven are sound.

## Evidence checked

- Read the current [spine](../ARCHITECTURE-SPINE.md) and [.memlog.md](../.memlog.md), root `apphost.cs`, `global.json`, relevant `DaprSelfHostedMtls.cs` declarations, and the existing Memories reconciliation evidence.
- All nine local Markdown links in the spine resolve to existing files, including the canonical sibling McpCli input. The frontmatter's PRD/addendum and memlog sources also exist.
- The five Stack rows match repository declarations exactly: .NET SDK **10.0.401** with `latestPatch` roll-forward; Aspire AppHost **13.5.4**; Docker/Redis hosting **13.5.4**; CommunityToolkit Dapr hosting **13.5.1-beta.757**; EventStore.Aspire **3.106.0**. These establish declared pins, not the active installed SDK, latest available versions, or a qualified production combination.

| Decision or capability | Existing reality/version evidence | Review result |
| --- | --- | --- |
| Aspire composition and retained Helm artifacts | Memlog lines 17–27 record official Aspire Kubernetes/export and deployment documentation, matching package availability, and Helm behavior. Root host is file-based. | Valid architectural choice with explicit qualification and maintained-chart fallback. The prerelease exporter is not presented as an already-proven Dapr/Keycloak/storage deployment path; retained artifacts avoid assuming `aspire deploy` consumes previously published artifacts. |
| Local/CI lifecycle | Memlog lines 37–38 and 106–108 record existing Builds workflows and official runner/Aspire support. Current Dapr helper has fixed ports, a fixed scheduler volume and `ExcludeFromManifest` resources. | The spine correctly assigns source/package alignment and safe resource ownership as implementation work. Neither generic isolation flags nor shared AppHost-path teardown are treated as sufficient. |
| Private deployment runner | Memlog lines 55–62 record workflow inspection, GitHub runner/network documentation and the adopted executor choice. | A suitable runner and private connectivity remain unproven; the diagram is target architecture, not a deployed inventory. |
| Keycloak and environment isolation | Memlog lines 46–53 and 64–69 record official realm/audience guidance and bounded cluster observations. | Separate realms on the existing server are a supported choice. Installed Keycloak semantic version and actual authorization/network/backend enforcement remain qualification work. |
| Dapr, provider profile and specialized adapter | Memlog lines 80–102 record official Dapr capabilities and provider limits, followed by the user's accepted FalkorDB exception. | No broker brand or tested provider combination is silently adopted. Dapr API portability is expressly distinguished from state migration, recovery and provider compatibility. The bounded adapter is a user-approved architecture choice. |
| CLI/MCP transport | Memlog line 119 records McpCli's pinned SDK/source and official MCP transport evidence. | Client-hosted stdio with HTTPS EventStore calls fits the accepted design; no hosted HTTP MCP endpoint is assumed. Connected metadata remains an explicit new qualification task. |
| Backup and disaster recovery | Memlog lines 129–141 record bounded inventory and official CNPG/Barman, Velero, PostgreSQL and OpenBao evidence. | Existing schedule success does not become proof of usable recovery points. Tool versions are observations/candidates, native compatibility is unverified, raw volume copies are not database consistency proof, and replacement capacity plus timed drills remain required. |

## Scope and limits

The current root host is accurately described as an opt-in Development-only Works preview rather than the complete MVP. Staging/production composition, release gates, replacement capacity, full backup coverage and finite recovery are not claimed as implemented. Memories' canonical digest ownership is a module contract; it does not certify the assembled Platform production profile.

The Folders operational override is authoritative. Its superseded stronger RPO, retention, topology and separate provider preferences do not reappear as review findings or enrollment blockers. Common profile qualification still verifies actual capability and data safety for every consumer.

Existing same-day primary-source research and local reality evidence cover the technology claims, so this review did not repeat browsing, run a deployment, query secrets, start workloads or alter runtime/source files. Only this report was written.
