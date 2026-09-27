# Platform architecture rubric review

Date: 2026-09-27

Verdict: **PASS for architecture finalization.** No critical, high, medium or low authoring finding remains in the reviewed draft. This verdict does not establish implemented capability or production readiness.

## Review scope and evidence

Reviewed the current `ARCHITECTURE-SPINE.md` against the good-spine checklist in `.agents/skills/bmad-architecture/references/reviewer-gate.md`, the finalized Platform PRD and its addendum. Checked recorded technology research and brownfield observations in the architecture memlog and the completed source reconciliation reports where relevant. The review preserves the explicitly accepted Platform MVP and Folders operational override; it does not reopen superseded Folders-only infrastructure requirements.

Deterministic check:

```text
uv run .agents/skills/bmad-architecture/scripts/lint_spine.py --workspace _bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27
exit 0; ok: true; total_findings: 0
```

## Checklist assessment

| Rubric item | Assessment |
| --- | --- |
| Correct initiative altitude | Pass. The spine fixes composition, runtime ownership, environment identity, lifecycle, release authority and recovery boundaries for separately built module/infrastructure work. Module algorithms, UI behavior, packet schemas and provider configuration remain in their owning inputs. |
| Enforceable ADs with matching prevention | Pass. AD-1 through AD-12 each identify the divergence prevented and an observable rule. Examples include one topology owner, content-bound retained artifacts, no data/key rollback, one active-root mapping, isolated run ownership, explicit identity realms, scoped deploy execution, separated application state, bounded provider exceptions, catalog-derived operation availability and qualified recovery sets. |
| Local development and test coverage | Pass. FR-1 through FR-5 retain the seven-module composition, minimum domain dependencies, developer-owned server list, active-checkout Debug/project mode, CI Release/NuGet mode, isolated-test independence, finite startup and attributable cleanup. Local failure retention and cancellation remain distinct. |
| Release and rollback coverage | Pass. FR-6 through FR-8 and NFR-1 retain exact-release E2E, non-empty module checks, readiness and timed verification, one recorded rollback attempt, interrupted-job reconciliation, predecessor compatibility and truthful failure reporting. Helm packaging supplements mandatory module publication/profile authority. |
| Hosted security and access coverage | Pass. FR-10 through FR-12 and NFR-3 retain the designated environments, existing Keycloak, explicit production membership, separate application credentials/data, API-level authorization and negative isolation evidence. Operation surface restrictions survive discovery, including restrictions for an otherwise authorized principal. |
| Operational and recovery dimensions | Pass. Deployment execution, topology/provider qualification, secrets, ingress, observability, backups, dependency inventory, writer fencing, key/deletion authority, prepared capacity, independent failure reporting and recovery validation are all decided or assigned. The one-hour/four-hour targets, cadence, retention and drill frequency match accepted requirements. |
| Deferred work cannot silently authorize divergent consumers | Pass. One Platform enrollment schema/validator must be defined before consumers implement it; EventStore owns routing schema/codec and shared authentication; Platform owns profile/secret/catalog instances; module owners retain behavior and recovery classification. Discovery metadata must derive from the committed catalog and be jointly qualified before FR-12 acceptance. Deferred choices do not authorize a competing registry, SDK, profile, source resolver or recovery authority. |
| Minimal structural seed | Pass. Two diagrams explain dependency and operating boundaries, and five observed package/runtime pins give the starting point. There is no speculative project tree, API inventory, deployment manifest or custom platform language. The long numeric acceptance table carries inherited product rules, not extra implementation design. |
| Named technology and brownfield fit | Pass for this authoring review. The memlog records dated official-source checks for the adopted Aspire/Helm/Kubernetes approach and existing repository/cluster observations. The draft distinguishes observed pins from a proven hosted combination, retains an explicit exporter qualification/fallback and treats remaining provider/runtime pins as qualified inventory work. It does not infer an upgrade or a compliant implementation from package availability. |
| Input precedence and intentional changes | Pass. The source-integration section preserves EventStore/Tenants/Parties/Projects/Memories boundaries and assigns McpCli source-contract alignment. Folders' stronger operational targets and provider preference are explicitly superseded without waiving shared-profile ratification, ordinary evidence or module data safety. |
| Qualification versus delivered evidence | Pass. The opening, structural seed and deferred section consistently distinguish the target contract from the opt-in Works preview and unproven production prerequisites. No finalization state can be read as a release, consumer-removal or production-readiness authorization. |

## Findings and disposition

There are no authoring changes requested by this lens. In particular, the following are already correctly assigned qualification work rather than missing architecture decisions:

- The tested hosted provider/runtime profile and immutable release authority, including current EventStore gaps.
- Exact enrollment/discovery wire shapes and their producer/consumer conformance evidence.
- Independent replacement capacity, backup tooling and notification delivery configuration.
- Memories tombstone/key continuity, module-specific service guarantees and Tenants replica-readiness evidence.

Their acceptance conditions remain binding before the corresponding capability or production claim. This review does not expand their implementation scope or reinstate the superseded Folders enrollment block.

Only this review file was written. No source, spine, memlog, Git state, runtime or external system was changed.
