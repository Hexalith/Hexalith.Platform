# Dependency direction: staging foundation and consuming modules

The dependency runs one way: **consumer module → Platform staging foundation**.

- Platform stories 4.14–4.22 are accepted against Platform's own reference release
  (Parties, EventStore, Tenants, Memories; Story 4.22). They never gate on, read, or
  name a consuming module's evidence, story or acceptance.
- A consuming module's deployed acceptance needs the
  accepted foundation and reads only its released, pinned artifacts. Its acceptance never
  feeds back into a Platform story's status.
- Module-specific enrollment is a consumer of the foundation.
  Its compatibility checks live with the enrollment tooling, not in the foundation.
  `ModuleAgnosticTests` fails if foundation sources name a module.

## Known remaining coupling (not removed here)

- Consumer-specific operations proposals and preparation scripts still reference the
  consumer's paths and its embedded Platform checkout. Moving enrollment into its owning
  repository is a separate, owner-approved change.
- Operator notes in `eng/cluster-management/NEXT-OPERATOR-ACTION.md` repeat a consumer
  status line. It is informational, not a gate for 4.x acceptance.
