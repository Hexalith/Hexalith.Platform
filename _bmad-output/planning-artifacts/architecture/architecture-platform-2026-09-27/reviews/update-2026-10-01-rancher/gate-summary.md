# Rancher course-correction architecture gate — 2026-10-01

**PASS.** The approved migration slice is reconciled across the PRD/addendum, spine, spec, epics, current stories and operational handoff. No unresolved migration-specific review finding remains. This is a planning/consistency gate, not live migration or operational qualification evidence.

Administrator approved the [complete proposal](../../../../sprint-change-proposal-2026-10-01.md) with “I approve.” The existing spine was amended through the architecture update workflow, preserving AD-1 through AD-15. The scope was the approved replacement, not a new platform architecture or workload distribution.

| Independent lens and input reconciliation | Result |
| --- | --- |
| [Rubric and PRD](review-rubric.md) | Pass; explicit migration ordering and approved urgent procedure before environment locks exist are now in the spine. |
| [Reality and spec](review-reality.md) | Pass; primary sources support the manager/import boundary and license/backup scope. Candidates remain unselected execution pins; manager tooling is separately qualified. |
| [Adversarial and proposal](review-adversarial.md) | Pass; independently durable current management grant/revocation authority and manager-independent workload recovery are explicit. |

## Applied corrections

- Restore the correct heading hierarchy and place each additive acceptance criterion inside its intended story. All original story IDs remain; only 4.26–4.28 are added.
- Record qualification → retirement → supported native hop → private Rancher → staging in the spine itself. Procurement and console availability do not block the native retirement/hop. The older staging-after-1.34-end-of-life waiver is superseded.
- Permit the exact approved urgent owner-controlled procedure while staging/production and their locks do not yet exist. Existing current recovery, access, preservation and per-hop gates still apply.
- Separate management application backup from downstream workload data/external etcd and management-cluster native recovery. Keep encryption configuration/keys separately available and initial 4.28 proof distinct from integrated Epic 8 qualification.
- Qualify manager installation Helm/native recovery tools separately; retain the application-executor tool floor.
- Use independently retained Administrator-approved current management grant/revocation records with complete lineage; fail closed on gaps/conflicts before reconnection. Manager restoration is independent of native workload recovery/verification, with necessary steps included in applicable RTO accounting.

These clarify already approved preservation, authority, version/procedure qualification and independent recovery requirements; they create no additional release writer, admission grant or deputy power.

## Mechanical verification and handoff

The architecture linter reports zero findings. Markdown links/fences/whitespace, canonical heading/story IDs, criterion placement, YAML parsing and dependency consistency pass. Every previous sprint status is preserved; exactly three backlog entries are added. Existing signed continuation evidence and the exact hashed maintenance proposal remain byte-for-byte intact. Removing only the approved dependency/addendum from active 4.1, and the approved README append, reproduces their earlier content byte-for-byte.

The [migration spec](../../../../../specs/spec-kubesphere-to-rancher/SPEC.md), three scoped implementation inputs and [operator package](../../../../../../eng/cluster-management/README.md) provide PO/Developer/Administrator/QA handoff. Next is Story 4.26 qualification. Exact pins, resource allowlists, private endpoint/authentication, capacity and live acceptance outcomes remain that implementation work. No cluster operation was run for this planning update.
