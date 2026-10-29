# Dependency direction: staging foundation and consuming modules

The dependency runs one way: **consumer module → Platform staging foundation**.

- Platform stories 4.14–4.22 are accepted against Platform's own reference release
  (Parties, EventStore, Tenants, Memories; Story 4.22). They never gate on, read, or
  name a consuming module's evidence, story or acceptance.
- A consuming module's deployed acceptance (for example Timesheets Story 3.6) needs the
  accepted foundation and reads only its released, pinned artifacts. Its acceptance never
  feeds back into a Platform story's status.
- Module-specific enrollment (`eng/timesheets-deployment`) is a consumer of the foundation.
  Its compatibility checks live there (`test_foundation_compat.py`), not in the foundation.
  `ModuleAgnosticTests` fails if foundation sources name a module.

## Known remaining coupling (not removed here)

- `eng/timesheets-deployment/platform-managed-operations.json` and `prepare.py` reference
  Timesheets paths and the Platform-in-Timesheets submodule. Moving that enrollment into the
  Timesheets repo (the "move manifest" option) is a separate, owner-approved change.
- Operator notes in `eng/cluster-management/NEXT-OPERATOR-ACTION.md` repeat "Timesheets 3.6
  open/FAIL" as a status line. It is informational, not a gate for 4.x acceptance.
