Retained SDK 4.2 package selections leaked into current source graphs and caused OpenAPI and EventStore.Server downgrades. Source projects now use the owning catalog's SDK 5.1/OpenAPI 11 view. Finite package consumers retain SDK 4.2/OpenAPI 2/MCP 2.0.1; generic and current-source MCP consumers retain 3.0.0. Explicit selections and package owners in mixed mappings keep their existing boundaries.

Verified model common props now apply mapping-derived mode and owner-origin flags before the first pinned-catalog import. Package-purpose snapshots, public APIs and later override/tamper checks retain their existing behavior.

Validation:

- 352 real catalog probes, 17 independent frozen/producer controls and 4 reader-mode cases pass.
- All 99 source/package/mixed model, snapshot and tamper cases pass without skips.
- Platform Debug build has zero warnings/errors; 43 extension/seed/registration cases and 28 source propagation controls pass.
- All 9 published-tool package component cases pass; all 127 tuple/history Python controls pass.
- Fresh audit generation and strict audit validation pass. Earlier failed/expired verification attempts remain separately retained.

The audit authenticates source f49a2021d757a0fe8d7344d7baa67e721155afa6. Preserve the source/audit commit ancestry with a normal merge; squashing away that source would invalidate provenance.

This is the Story 3.2 owner release prerequisite. Hosted qualification, SDK publication, Platform tuple adoption and signed fixture/native host qualification remain outstanding.
